import argparse
from pathlib import Path
import struct
from tempfile import NamedTemporaryFile
import xml.etree.ElementTree as ET

from dbpf import Key, LAYO, S3SA, STBL, XML, fnv64, index, read, write
from journal_layouts import layouts
from localization import read_table, resources as locale_resources
from trees import IMAGE, icons
from hud_icons import resources as hud_resources, status_resources, life_state_resources
from ui_icons import resources as ui_icon_resources
from ui_resources import (portrait_mask_resources, star_resources as selection_star_resources, glitter_resources)
from build_vampire_bitemark import (replacement_payloads as vampire_bite_resources,
                                   thumbnail_payloads as vampire_bite_thumbnails)


ASSEMBLY = "SupernaturalExpanded.Journal"
BOOTSTRAP = ASSEMBLY + ".JournalBootstrap"
ASSEMBLY_KEY = Key(S3SA, 0, fnv64(ASSEMBLY))
TUNING_KEY = Key(XML, 0, fnv64(BOOTSTRAP))
TUNING = Path(__file__).resolve().parent.parent / "src" / "Tuning" / "JournalBootstrap.xml"


def validate_dll(data):
    if len(data) < 64 or data[:2] != b"MZ":
        raise ValueError("Expected a managed PE DLL")
    pe = struct.unpack_from("<I", data, 60)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("Missing PE header")
    machine, = struct.unpack_from("<H", data, pe + 4)
    pe_type, = struct.unpack_from("<H", data, pe + 24)
    if (machine, pe_type) != (0x014C, 0x010B):
        raise ValueError("Expected a 32-bit Windows game assembly")
    cli_rva, = struct.unpack_from("<I", data, pe + 24 + 96 + 14 * 8)
    metadata = data.find(b"BSJB")
    if not cli_rva or metadata < 0 or b"SupernaturalExpanded.Journal" not in data:
        raise ValueError("Expected the compiled SupernaturalExpanded.Journal managed assembly")
    version_length, = struct.unpack_from("<I", data, metadata + 12)
    version = data[metadata + 16:metadata + 16 + version_length].rstrip(b"\0")
    if version != b"v2.0.50727":
        raise ValueError("Expected CLR v2 metadata. Compile against the Sims 3 mscorlib.")


def validate_tuning(tuning):
    if not tuning.startswith(b"<"):
        raise ValueError("Bootstrap tuning must start with XML and have no byte-order mark")
    element = ET.fromstring(tuning).find("./Current_Tuning/kInstantiator")
    if element is None or element.get("value", "").lower() != "true":
        raise ValueError("Bootstrap tuning is missing kInstantiator=True")


def script_resource(assembly):
    block_size = 512
    blocks = (len(assembly) + block_size - 1) // block_size
    header = struct.pack("<BI64sH", 1, 0x2BC4F79F, bytes(64), blocks)
    return header + bytes(blocks * 8) + assembly.ljust(blocks * block_size, b"\0")


def read_script_resource(resource):
    version, checksum_type, checksum, blocks = struct.unpack_from("<BI64sH", resource)
    start = 71 + blocks * 8
    if (version != 1 or checksum_type != 0x2BC4F79F or any(checksum)
            or any(resource[71:start]) or len(resource) != start + blocks * 512):
        raise ValueError("Unexpected unencrypted custom S3SA resource")
    return resource[start:]


def verify(package, assembly, tuning, resources):
    entries = index(package)
    hud = dict(hud_resources() + status_resources() + life_state_resources())
    found = {entry.key: entry for entry in entries}
    for key, data in hud.items():
        if key not in found or read(package, found[key]) != data:
            raise ValueError(f"HUD icon missing or changed: {key}")
    if [entry.key for entry in entries] != [key for key, data in resources]:
        raise ValueError("Package resource list differs from the requested journal resources")
    extracted = read_script_resource(read(package, entries[0]))
    if extracted[:len(assembly)] != assembly or any(extracted[len(assembly):]):
        raise ValueError("S3SA round trip failed")
    if read(package, entries[1]) != tuning:
        raise ValueError("Bootstrap tuning round trip failed")
    for entry, (key, data) in zip(entries[2:], resources[2:]):
        packaged = read(package, entry)
        if packaged != data:
            raise ValueError(f"Round trip failed for {key}")
        if key.type == LAYO and ET.fromstring(data).tag != "graph":
            raise ValueError(f"Layout round trip failed for {key}")
        if key.type == STBL:
            read_table(packaged)


def content_resources():
    """Journal layouts, artwork and language tables."""
    resources = [(Key(LAYO, 0, fnv64(name)), data) for name, data in layouts()]
    resources.extend((Key(IMAGE, 0, instance), data) for _, instance, data in icons().values())
    resources.extend((Key(IMAGE, 0, instance), data) for instance, data in ui_icon_resources())
    resources.extend((Key(IMAGE, 0, instance), data) for instance, data in portrait_mask_resources())
    resources.extend((Key(IMAGE, 0, instance), data) for instance, data in selection_star_resources())
    resources.extend((Key(IMAGE, 0, instance), data) for instance, data in glitter_resources())
    resources.extend(hud_resources())
    resources.extend(status_resources())
    resources.extend(life_state_resources())
    resources.extend(locale_resources())
    # Default replacement: TS4 vampire punctures mapped to the vanilla TS3 mark UV.
    # Keep the original CASP/masks untouched. Override the two original-TGI IMG resources.
    resources.extend((Key(typ, group, instance), data)
                     for typ, group, instance, data in vampire_bite_resources())
    # Game-rendered mannequin previews from the user's regenerated CAS cache.
    # Four 256x256 THUM resources for the four observed preset groups (1..4).
    resources.extend((Key(typ, group, instance), data)
                     for typ, group, instance, data in vampire_bite_thumbnails())
    if len({key for key, _ in resources}) != len(resources):
        raise ValueError("Duplicate package resource key")
    return resources


def main():
    parser = argparse.ArgumentParser(description="Build the additive TS3 journal package.")
    parser.add_argument("assembly", type=Path)
    parser.add_argument("package", type=Path)
    args = parser.parse_args()
    dll = args.assembly.read_bytes()
    tuning = TUNING.read_bytes()
    validate_dll(dll)
    validate_tuning(tuning)
    resources = [(ASSEMBLY_KEY, script_resource(dll)), (TUNING_KEY, tuning)] + content_resources()

    args.package.parent.mkdir(parents=True, exist_ok=True)
    with NamedTemporaryFile(dir=args.package.parent, suffix=".package.tmp", delete=False) as temp:
        staging = Path(temp.name)
    try:
        write(staging, resources)
        verify(staging, dll, tuning, resources)
        staging.replace(args.package)
    finally:
        staging.unlink(missing_ok=True)
    print(f"Verified {args.package}")
    print("Included vampire bite-mark default replacement (2 DDS + 4 regenerated CAS thumbnails).")
    for key, _ in resources:
        print(key)


if __name__ == "__main__":
    main()
