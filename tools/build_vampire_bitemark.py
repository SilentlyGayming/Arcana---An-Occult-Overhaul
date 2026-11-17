#!/usr/bin/env python3
"""Build or merge Arcana's independent TS4-to-TS3 vampire bite-mark default replacement.

Automatically includes two DDS overrides and four new TS3-generated CAS thumbnails
through Arcana's normal package_journal.py packaging step.
The commands below are optional for standalone testing or merging old packages:
  py -3 tools/build_vampire_bitemark.py
  py -3 tools/build_vampire_bitemark.py --merge "Arcana - An Occult Overhaul.package" \
      --output "Arcana - An Occult Overhaul.package"

Standard-library only. Does NOT modify any installed EA game packages.
"""
from __future__ import annotations
import argparse
import os
from pathlib import Path
import struct
import tempfile
from dataclasses import dataclass

IMG=0x00B2D882
THUMB=0x626F60CE
THUMB_INSTANCE=0x08146A9FB7FA3A0A
THUMB_GROUPS=(1, 2, 3, 4)
REPLACE={
  (IMG, 0, 0x9C4DC2ABCC9D8BFE): 'BiteMark_Multiplier.dds',
  (IMG, 0, 0x9C4DC2ABCC9D8BE0): 'BiteMark_Specular.dds',
}

# The project intentionally uses the plural directory name: Textures/Vampires.
DEFAULT_ASSETS = (Path(__file__).resolve().parent.parent / "assets" / "Textures"
                  / "Vampires" / "BiteMark")
DEFAULT_THUMBNAILS = (Path(__file__).resolve().parent.parent / "assets" / "Thumbnails"
                      / "Vampires" / "BiteMark")

@dataclass
class Item:
    typ: int
    group: int
    instance: int
    data: bytes
    decompressed_size: int
    compression: int
    stored_highbit: int=0

    @property
    def key(self): return self.typ,self.group,self.instance


def read_package(path: Path) -> list[Item]:
    with path.open('rb') as f:
        head=f.read(96)
        if len(head)<96 or head[:4]!=b'DBPF':
            raise ValueError(f'{path}: not a DBPF package')
        major,minor=struct.unpack_from('<II',head,4)
        if (major,minor)!=(2,0):
            raise ValueError(f'{path}: unsupported DBPF version {major}.{minor}')
        count=struct.unpack_from('<I',head,0x24)[0]
        index_size=struct.unpack_from('<I',head,0x2C)[0]
        indexpos=struct.unpack_from('<I',head,0x40)[0]
        if count>1_000_000 or index_size>100_000_000:
            raise ValueError(f'{path}: unreasonable index dimensions')
        f.seek(indexpos)
        raw=f.read(index_size)
        if len(raw)!=index_size or len(raw)<4:
            raise ValueError(f'{path}: index truncated')
        flags=struct.unpack_from('<I',raw,0)[0]
        if flags & ~0xFF:
            raise ValueError(f'{path}: unsupported index flags {flags:#x}')
        expected=4+4*flags.bit_count()+4*count*(8-flags.bit_count())
        if len(raw)!=expected:
            raise ValueError(f'{path}: index layout inconsistency; expected {expected}, got {len(raw)}')
        common=[None]*8
        ix=4
        for k in range(8):
            if flags & (1<<k):
                common[k]=struct.unpack_from('<I',raw,ix)[0];ix+=4
        out=[]
        seen=set()
        for _ in range(count):
            words=common.copy()
            for k in range(8):
                if words[k] is None:
                    words[k]=struct.unpack_from('<I',raw,ix)[0];ix+=4
            typ,group,hi,lo,off,stored,expanded,compression=words
            key=(typ,group,(hi<<32)|lo)
            if key in seen:
                raise ValueError(f'{path}: duplicate TGI {key}')
            seen.add(key)
            length=stored&0x7FFFFFFF
            f.seek(off)
            data=f.read(length)
            if len(data)!=length:
                raise ValueError(f'{path}: truncated TGI {key}')
            # Preserve original compressed bytes and flags exactly, without rewriting contents
            out.append(Item(typ,group,(hi<<32)|lo,data,expanded,compression,stored>>31))
        return out


def check_dds(blob: bytes, fmt: bytes, name: str):
    if len(blob)<128 or blob[:4]!=b'DDS ':
        raise ValueError(f'{name}: not a DDS')
    w=struct.unpack_from('<I',blob,16)[0]
    h=struct.unpack_from('<I',blob,12)[0]
    mip=struct.unpack_from('<I',blob,28)[0]
    fourcc=blob[84:88]
    if (w,h,mip,fourcc)!=(1024,1024,11,fmt):
        raise ValueError(f'{name}: expected 1024x1024, 11 mips, {fmt!r}; got {w}x{h}, {mip} mips, {fourcc!r}')
    if len(blob)!=(1398256 if fmt==b'DXT5' else 699192):
        raise ValueError(f'{name}: unexpected DDS length {len(blob)}')


def replacement_payloads(assets: Path = None):
    """Load and validate the two original-TGI DDS payloads for Arcana's main build.

    Do not call the standalone CLI to include these in the normal Arcana package:
    tools/package_journal.py directly includes these via content_resources().
    """
    directory = Path(assets) if assets is not None else DEFAULT_ASSETS
    resources = []
    for (typ, group, instance), filename in REPLACE.items():
        path = directory / filename
        if not path.is_file():
            raise FileNotFoundError(
                f"Vampire bite-mark texture not found: {path}\n"
                "Keep both DDS files in assets/Textures/Vampires/BiteMark/."
            )
        data = path.read_bytes()
        check_dds(data, b'DXT5' if 'Multiplier' in filename else b'DXT1', filename)
        resources.append((typ, group, instance, data))
    return resources


def thumbnail_payloads(assets: Path = None):
    """The four unedited 256px CAS renders generated by TS3 with Arcana's bite marks.

    Obtained from the user's own regenerated CASThumbnails.package, not from
    another modder. Do not synthesize group zero or overwrite unrelated thumbnails.
    """
    directory=Path(assets) if assets is not None else DEFAULT_THUMBNAILS
    resources=[]
    for group in THUMB_GROUPS:
        filename=f'VampireBirthMark_256_Group_{group:08X}.png'
        path=directory / filename
        if not path.is_file():
            raise FileNotFoundError(
                f"Vampire birth-mark CAS thumbnail not found: {path}\n"
                "Keep all four PNGs in assets/Thumbnails/Vampires/BiteMark/."
            )
        data=path.read_bytes()
        if (len(data) < 33 or data[:8] != b'\x89PNG\r\n\x1a\n'
                or data[12:16] != b'IHDR'
                or struct.unpack_from('>II', data, 16) != (256,256)):
            raise ValueError(f'{filename}: expected an unmodified 256x256 PNG')
        resources.append((THUMB, group, THUMB_INSTANCE, data))
    return resources


def all_replacement_payloads(texture_assets: Path = None, thumbnail_assets: Path = None):
    return replacement_payloads(texture_assets) + thumbnail_payloads(thumbnail_assets)


def write_package(path:Path, items:list[Item]):
    path.parent.mkdir(parents=True,exist_ok=True)
    if len({x.key for x in items})!=len(items):
        raise ValueError('Refusing to write duplicate keys')
    head=bytearray(96)
    head[:4]=b'DBPF'
    struct.pack_into('<II',head,4,2,0)
    struct.pack_into('<I',head,0x24,len(items))
    struct.pack_into('<I',head,0x3C,3)
    fd,temp_name=tempfile.mkstemp(prefix='arcana-bite-',suffix='.package',dir=path.parent)
    try:
        with os.fdopen(fd,'wb') as f:
            f.write(head)
            entries=[]
            for item in items:
                pad=(-f.tell())&3
                if pad: f.write(b'\x00'*pad)
                off=f.tell()
                if off>0xFFFFFFFF:
                    raise ValueError('Package exceeds supported offset range')
                f.write(item.data)
                entries.append((item.typ,item.group,item.instance>>32,item.instance&0xFFFFFFFF,off,
                                len(item.data)|(item.stored_highbit<<31),item.decompressed_size,item.compression))
            indexpos=f.tell()
            f.write(struct.pack('<I',0)) # all 8 fields provided in every record
            for entry in entries:
                f.write(struct.pack('<8I',*entry))
            struct.pack_into('<I',head,0x2C,4+32*len(items))
            struct.pack_into('<I',head,0x40,indexpos)
            f.seek(0);f.write(head)
            f.flush();os.fsync(f.fileno())
        # Validate both content and TGI before touching intended destination.
        indexed=read_package(Path(temp_name))
        if len(indexed)!=len(items):
            raise RuntimeError('Verification failed: resource count differs')
        for before,after in zip(items,indexed):
            if before.key!=after.key or before.data!=after.data or before.compression!=after.compression or before.decompressed_size!=after.decompressed_size:
                raise RuntimeError(f'Verification failed for TGI {before.key}')
        os.replace(temp_name,path)
    finally:
        if os.path.exists(temp_name):os.unlink(temp_name)


def main():
    root=Path(__file__).resolve().parent.parent
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--assets',type=Path,default=DEFAULT_ASSETS)
    p.add_argument('--thumbnails',type=Path,default=DEFAULT_THUMBNAILS)
    p.add_argument('--merge',type=Path,help='Existing Arcana .package. Merge while preserving all other entries.')
    p.add_argument('--output',type=Path,help='Result .package (may equal --merge for safe atomic update).')
    args=p.parse_args()
    if args.merge is not None and not args.merge.exists():
        p.error(f'Merge input not found: {args.merge}')
    if args.merge is None:
        output=args.output or root/'build'/'Arcana - Vampire Bite Mark (Standalone Test).package'
        items=[]
    else:
        output=args.output or root/'build'/'Arcana - An Occult Overhaul - Bite Mark Merged.package'
        items=read_package(args.merge)
    replacements = [Item(typ, grp, inst, data, len(data), 0)
                    for typ, grp, inst, data in all_replacement_payloads(args.assets,args.thumbnails)]
    keyed={item.key:item for item in items}
    nold=len(keyed)
    for item in replacements:keyed[item.key]=item
    write_package(output, list(keyed.values()))
    print('Created:',output.resolve())
    print('Resources:',len(keyed),'(existing:',nold,', replaced/added:',len(replacements),')')
    for item in replacements:
        print('  TGI:',f'{item.typ:08X}:{item.group:08X}:{item.instance:016X}')
    print('This package is structurally verified; CAS thumbnails must be checked in-game.')

if __name__=='__main__':main()
