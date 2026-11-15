"""Locate an installed copy of The Sims 3 and extract Arcana's build references.

Run automatically by the build; requires Python 3 and the project's existing tools/dbpf.py.
No third-party Python packages, S3PE installation, or .NET SDK are needed to
*extract* references. Game files are read only; nothing is copied from a game
installation into a public repository automatically.

Optional: set SIMS3_GAME_DIR to your game folder (or its Game/Bin directory)
for unusual Steam/EA/Origin installations.
"""

import argparse
import hashlib
import json
import os
import re
import struct
import sys
from itertools import chain
from pathlib import Path
from tempfile import TemporaryDirectory

from check_game_api import module_name, read_types, require_genie_contract, require_ui_contract
from dbpf import S3SA, index, read

REQUIRED = (
    "mscorlib.dll", "System.dll", "SimIFace.dll", "UI.dll",
    "Sims3GameplaySystems.dll", "Sims3MetaData.dll",
)
CANONICAL = {name.casefold(): name for name in REQUIRED}
PACKAGES = ("simcore.package", "scripts.package", "gameplay.package")


MANIFEST = "references.json"
CACHE_VERSION = 1


def decode_s3sa(blob):
    """Unwrap the game's XOR-chained S3SA script resource (not simply a DLL)."""
    if not blob:
        raise ValueError("empty S3SA resource")
    p = 0
    version = blob[p]
    p += 1
    if version < 1 or version > 10:
        raise ValueError("unexpected S3SA version")
    if version > 1:
        n = struct.unpack_from("<i", blob, p)[0]
        if not 0 <= n <= 1024:
            raise ValueError("invalid S3SA game version length")
        p += 4 + n * 2
    p += 4 + 64  # unknown field and digest
    count = struct.unpack_from("<H", blob, p)[0]
    p += 2
    table = blob[p:p + count * 8]
    p += count * 8
    if not table or len(table) != count * 8:
        raise ValueError("invalid S3SA block table")

    seed = sum(struct.unpack_from("<Q", table, i)[0]
               for i in range(0, len(table), 8)) & (len(table) - 1)
    size = len(table)
    output = bytearray()
    plain_table = not any(table)
    for i in range(0, size, 8):
        if table[i] & 1:
            output.extend(bytes(512))
            continue
        if p + 512 > len(blob):
            raise ValueError("truncated S3SA assembly data")
        chunk = blob[p:p + 512]
        p += 512
        if plain_table:
            output.extend(chunk)
            continue
        for v in chunk:
            output.append(v ^ table[seed])
            seed = (seed + v) % size
    return bytes(output)


def installed_game_bin(candidate):
    """Resolve install root / Game/Bin / Steam common folder to core packages."""
    candidate = Path(candidate).expanduser()
    for folder in (candidate, candidate / "Game" / "Bin", candidate / "Bin",
                   candidate / "The Sims 3" / "Game" / "Bin"):
        if folder.is_dir() and all((folder / name).is_file() for name in PACKAGES):
            return folder
    return None


def windows_registry_paths():
    if os.name != "nt":
        return []
    try:
        import winreg
    except ImportError:
        return []
    found = []
    lookups = [
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Sims\The Sims 3", ("Install Dir", "InstallDir")),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Electronic Arts\Sims\The Sims 3", ("Install Dir", "InstallDir")),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 47890", ("InstallLocation",)),
        (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Valve\Steam", ("SteamPath", "SteamExe")),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Valve\Steam", ("InstallPath",)),
    ]
    for hive, key, names in lookups:
        for view in (0, winreg.KEY_WOW64_32KEY, winreg.KEY_WOW64_64KEY):
            try:
                with winreg.OpenKey(hive, key, 0, winreg.KEY_READ | view) as handle:
                    for name in names:
                        try:
                            found.append(str(winreg.QueryValueEx(handle, name)[0]))
                        except OSError:
                            continue
            except OSError:
                continue
    return found


def steam_libraries(steam_root):
    steam_root = Path(steam_root)
    if steam_root.name.casefold() == "steam.exe":
        steam_root = steam_root.parent
    yield steam_root
    vdf = steam_root / "steamapps" / "libraryfolders.vdf"
    if vdf.is_file():
        try:
            data = vdf.read_text(encoding="utf-8-sig", errors="replace")
            for found in re.findall(r'"path"\s+"([^"]+)"', data, flags=re.I):
                yield Path(found.replace("\\\\", "\\"))
            for found in re.findall(r'^\s*"\d+"\s+"([^"]+)"', data, flags=re.M):
                yield Path(found.replace("\\\\", "\\"))
        except OSError:
            pass


def install_candidates(explicit=None):
    """Search standard Windows install roots, registry entries and Steam libraries."""
    seen = set()

    def add(path):
        if not path:
            return
        name = str(path).strip().strip('"')
        if not name:
            return
        key = os.path.normcase(os.path.normpath(name))
        if key not in seen:
            seen.add(key)
            yield Path(name)

    if explicit:
        yield from add(explicit)
        return
    for env_name in ("SIMS3_GAME_DIR", "SIMS3_INSTALL_DIR"):
        yield from add(os.getenv(env_name))
    registry = windows_registry_paths()
    for path in registry:
        yield from add(path)

    roots = [os.getenv("ProgramFiles(x86)"), os.getenv("ProgramFiles"),
             os.getenv("PROGRAMW6432")]
    for root in roots:
        if not root:
            continue
        for segment in ("EA Games", "Origin Games", "Electronic Arts", "Steam"):
            yield from add(Path(root) / segment / "The Sims 3")

    steam_roots = []
    for item in registry:
        if "steam" in item.lower():
            steam_roots.append(item)
    for root in roots:
        if root:
            steam_roots.append(str(Path(root) / "Steam"))
    if os.name == "nt":
        for drive in "CDEFGHIJKLMNOPQRSTUVWXYZ":
            if not Path(drive + ":\\").exists():
                continue
            steam_roots.extend([drive + ":\\Steam", drive + ":\\SteamLibrary",
                                drive + ":\\Games\\Steam", drive + ":\\Games\\SteamLibrary"])
            for base in ("EA Games", "Origin Games", "Electronic Arts", "Games"):
                yield from add(Path(drive + ":\\") / base / "The Sims 3")

    for root in steam_roots:
        for library in steam_libraries(root):
            yield from add(library / "steamapps" / "common" / "The Sims 3")


def validated_references(folder):
    """Return the paths and hashes of one complete, compatible reference set."""
    paths, hashes = {}, {}
    if folder.is_dir():
        for path in folder.iterdir():
            name = CANONICAL.get(path.name.casefold())
            if name and path.is_file():
                if name in paths:
                    raise ValueError(f"{folder}: more than one copy of {name}")
                paths[name] = path
    missing = [name for name in REQUIRED if name not in paths]
    if missing:
        raise ValueError(f"{folder}: missing " + ", ".join(missing))
    for name in REQUIRED:
        path = paths[name]
        data = path.read_bytes()
        try:
            if module_name(data).casefold() != name.casefold():
                raise ValueError(f"expected module {name}")
            if name == "UI.dll":
                require_ui_contract(read_types(data))
            elif name == "Sims3GameplaySystems.dll":
                require_genie_contract(read_types(data))
        except (ValueError, IndexError, KeyError, struct.error) as error:
            raise ValueError(f"{path}: {error}") from error
        hashes[name] = hashlib.sha256(data).hexdigest()
    return paths, hashes


def source_stamp(bin_dir):
    packages = {}
    for name in PACKAGES:
        stat = (bin_dir / name).stat()
        packages[name] = [stat.st_size, stat.st_mtime_ns]
    return {"directory": str(bin_dir), "packages": packages}


def load_manifest(output):
    path = output / MANIFEST
    if not path.is_file():
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(data, dict) or data.get("version") != CACHE_VERSION:
            raise ValueError("unknown reference cache version")
        if not isinstance(data.get("source"), dict) or not isinstance(data.get("sha256"), dict):
            raise ValueError("incomplete reference cache record")
        return data
    except (OSError, ValueError) as error:
        print(f"Reference cache record needs refreshing: {error}", flush=True)
        return None


def extract(bin_dir, staging):
    """Decode each required assembly directly from the installed core packages."""
    missing = set(REQUIRED)
    for package_name in PACKAGES:
        package = bin_dir / package_name
        print("  examining", package_name, "...", flush=True)
        for entry in index(package):
            if entry.key.type != S3SA:
                continue
            try:
                data = decode_s3sa(read(package, entry))
                name = CANONICAL.get(module_name(data).casefold())
            except (ValueError, KeyError, IndexError, struct.error) as error:
                print(f"    skipped unreadable script {entry.key}: {error}", flush=True)
                continue
            if name in missing:
                (staging / name).write_bytes(data)
                missing.remove(name)
                print(f"  extracted {name} ({len(data):,} bytes)", flush=True)
                if not missing:
                    return
    raise ValueError("missing game package resources for " + ", ".join(sorted(missing)))


def extract_reference_set(bin_dir, output, stamp):
    # Stage a complete set. A later build detects an interrupted publication
    # by comparing all six hashes against the manifest written last.
    target = output.resolve()
    game_root = bin_dir.parent.parent if bin_dir.parent.name.casefold() == "game" else bin_dir
    if target == game_root or game_root in target.parents:
        raise ValueError("the dependency cache must be outside the game installation")
    output.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix="sims3-refs-", dir=output.parent) as temporary:
        staging = Path(temporary)
        extract(bin_dir, staging)
        _, hashes = validated_references(staging)
        if source_stamp(bin_dir) != stamp:
            raise ValueError("game packages changed during extraction")
        manifest = {"version": CACHE_VERSION, "source": stamp, "sha256": hashes}
        (staging / MANIFEST).write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        output.mkdir(exist_ok=True)
        for name in REQUIRED:
            (staging / name).replace(output / name)
        (staging / MANIFEST).replace(output / MANIFEST)
    print("All six Sims 3 references are ready in:", output, flush=True)
    return {name: output / name for name in REQUIRED}, hashes


def prepare_references(project, game_dir=None, refresh=False):
    output = project.resolve() / ".deps" / "Sims3"
    manifest = load_manifest(output)
    cached = None
    cache_error = None
    try:
        cached = validated_references(output)
    except (OSError, ValueError) as error:
        cache_error = error
        if output.exists():
            print(f"Reference cache needs refreshing: {error}", flush=True)

    override = game_dir or os.getenv("SIMS3_GAME_DIR") or os.getenv("SIMS3_INSTALL_DIR")
    candidates = install_candidates(override)
    if not override and manifest and isinstance(manifest["source"].get("directory"), str):
        recorded = Path(manifest["source"]["directory"])
        candidates = chain((recorded,), candidates)

    checked = set()
    last_error = None
    for candidate in candidates:
        bin_dir = installed_game_bin(candidate)
        if bin_dir is None:
            continue
        bin_dir = bin_dir.resolve()
        if bin_dir in checked:
            continue
        checked.add(bin_dir)
        try:
            stamp = source_stamp(bin_dir)
            if (not refresh and cached and manifest
                    and stamp == manifest["source"] and cached[1] == manifest["sha256"]):
                print("Reusing six verified Sims 3 references from:", output, flush=True)
                return cached
            print("Extracting Sims 3 references from:", bin_dir, flush=True)
            return extract_reference_set(bin_dir, output, stamp)
        except (OSError, ValueError, struct.error) as error:
            last_error = error
            print(f"  Dependency extraction failed for {bin_dir}: {error}", flush=True)

    if not checked and not override and not refresh and cached:
        if (not (output / MANIFEST).exists()
                or (manifest and cached[1] == manifest["sha256"])):
            print("Reusing six compatible cached references. No installed game packages were found.", flush=True)
            return cached
        cache_error = ValueError("the cached DLLs no longer match their extraction record")
    detail = last_error or cache_error
    message = ("Could not prepare a complete original Sims 3 reference set. "
               "Set SIMS3_GAME_DIR to the base-game folder or its Game\\Bin directory.")
    if detail:
        message += "\n" + str(detail)
    raise ValueError(message)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--game-dir", help="Override installation autodetection, including Game/Bin paths.")
    parser.add_argument("--refresh", action="store_true", help="Re-extract the entire reference set.")
    args = parser.parse_args(argv)
    try:
        prepare_references(args.project, args.game_dir, args.refresh)
    except (OSError, ValueError, struct.error) as error:
        print("ERROR: Dependency setup failed:", error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
