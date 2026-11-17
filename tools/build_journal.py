"""Compiles the journal assembly and packages it for The Sims 3.

The default build prepares and validates references from the installed game.
"""
import argparse
import hashlib
import os
import re
import shutil
import struct
import subprocess
import sys
from pathlib import Path
from tempfile import TemporaryDirectory

from package_journal import ASSEMBLY
from hud_icons import generate as generate_hud_keys
from localization import generate as generate_english
from prepare_sims3_deps import REQUIRED, prepare_references, validated_references
from build_vampire_bitemark import replacement_payloads, thumbnail_payloads

PROJECT = Path(__file__).resolve().parent.parent
PACKAGE = "Arcana - An Occult Overhaul.package"
DLL = "Arcana - An Occult Overhaul.dll"


def find_compiler(dotnet_path):
    if not dotnet_path and os.name == "nt":
        framework = Path(os.getenv("WINDIR", r"C:\Windows")) / "Microsoft.NET" / "Framework" / "v4.0.30319" / "csc.exe"
        if framework.is_file():
            return [str(framework)], False

    dotnet = dotnet_path or shutil.which("dotnet")
    if dotnet:
        versions = subprocess.check_output([dotnet, "--list-sdks"], text=True).splitlines()
        for entry in reversed(versions):
            match = re.fullmatch(r"(\S+) \[(.+)\]", entry)
            if match:
                version, directory = match.groups()
                roslyn = Path(directory) / version / "Roslyn" / "bincore" / "csc.dll"
                if roslyn.is_file():
                    return [dotnet, str(roslyn)], True
    raise RuntimeError("Install a .NET SDK or provide --dotnet with its executable path.")


def run_stage(name, command):
    result = subprocess.run(command)
    if result.returncode != 0:
        raise RuntimeError(f"{name} failed (exit code {result.returncode}). See the diagnostics above.")


def build(args):
    # Fail fast before dependency extraction and C# compilation if textures are absent.
    # The normal package_journal step includes these files in Arcana's one .package.
    replacement_payloads()
    thumbnail_payloads()
    print("Vampire bite-mark resources verified (2 DDS textures + 4 CAS thumbnails).", flush=True)
    if args.game_assemblies:
        paths, hashes = validated_references(args.game_assemblies.resolve())
    else:
        paths, hashes = prepare_references(PROJECT, args.game_dir, args.refresh_deps)

    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    compiler, roslyn = find_compiler(args.dotnet)
    options = ["/nologo", "/noconfig", "/nostdlib+", "/langversion:3", "/platform:x86",
               "/target:library", "/optimize+", "/warnaserror+", "/codepage:65001"]
    if roslyn:
        options.append("/deterministic+")
    if args.grimoire_test:
        options.append("/define:ARCANA_GRIMOIRE_TEST")
        print("WARNING: building with the Grimoire sample entries. Do not release this build.")
    if args.animation_log:
        options.append("/define:ARCANA_ANIMATION_LOG")
    if args.localization_log:
        options.append("/define:ARCANA_LOCALIZATION_LOG")
    source_root = PROJECT / "src"
    sources = {path.relative_to(source_root): path for path in source_root.rglob("*.cs")}

    with TemporaryDirectory(dir=output) as temp:
        staging = Path(temp)
        # Compile from the verified bytes even if a second build refreshes the cache.
        ref_dir = staging / "Sims3"
        ref_dir.mkdir()
        for name in REQUIRED:
            data = paths[name].read_bytes()
            if hashlib.sha256(data).hexdigest() != hashes[name]:
                raise ValueError(f"{paths[name]} changed while preparing the build. Run the build again.")
            (ref_dir / name).write_bytes(data)
        generated = (
            (Path("Occults/OccultAssetData.g.cs"), generate_hud_keys),
            (Path("Localization/ArcanaEnglish.g.cs"), generate_english),
        )
        for relative_path, generate in generated:
            path = staging / relative_path
            generate(path)
            sources[relative_path] = path

        dll = staging / (ASSEMBLY + ".dll")
        package = staging / PACKAGE
        print(f"Compiling {len(sources)} C# files for CLR v2 / x86.", flush=True)
        run_stage("C# compilation", compiler + options + ["/out:" + str(dll)]
                  + ["/r:" + str(ref_dir / name) for name in REQUIRED]
                  + [str(sources[path]) for path in sorted(sources)])
        run_stage("Journal packaging", [sys.executable, "-B", str(PROJECT / "tools" / "package_journal.py"),
                                       str(dll), str(package)])
        dll.replace(output / DLL)
        package.replace(output / PACKAGE)
    print(f"Build complete: {output / PACKAGE}")
    print(f"DLL: {output / DLL}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    references = parser.add_mutually_exclusive_group()
    references.add_argument("--game-assemblies", type=Path, help="Use six original DLLs from a local folder.")
    references.add_argument("--game-dir", help="Override the game installation or Game/Bin folder.")
    parser.add_argument("--refresh-deps", action="store_true", help="Re-extract the local references from the game.")
    parser.add_argument("--output", type=Path, default=PROJECT / "build")
    parser.add_argument("--dotnet", help="Use a specific .NET SDK executable.")
    parser.add_argument("--grimoire-test", action="store_true",
                        help="Development only: fill the Grimoire with sample entries. Never use for a release.")
    parser.add_argument("--animation-log", action="store_true",
                        help="Development only: log up to three successful frames per activation.")
    parser.add_argument("--localization-log", action="store_true",
                        help="Development only: log missing or invalid locale text once per key.")
    args = parser.parse_args()
    if args.game_assemblies and args.refresh_deps:
        parser.error("--refresh-deps needs an installation. Use --game-dir or SIMS3_GAME_DIR.")
    try:
        build(args)
    except (OSError, ValueError, RuntimeError, struct.error, subprocess.CalledProcessError) as error:
        print("ERROR: Arcana build failed:", error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
