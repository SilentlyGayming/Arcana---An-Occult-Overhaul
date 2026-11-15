# Arcana – An Occult Overhaul

Arcana work in progress script mod to overhaul all of the occult races for the Sims 3. Current implementation is limited to a UI journal and perk system fully implemented for witches. This mod does utilise AI for research and assisting the overhaul project. No AI-generated images will ever be involved in the making of this mod, and all assets are either TS4, TS3, or hand-created. 

## Requirements

- Python 3.9 or newer. Use the Windows Python launcher or add `python` to your `PATH`. [Download Python](https://www.python.org/downloads/windows/)
- A C# compiler. The build uses the Windows .NET Framework compiler when available. For the SDK compiler, install a .NET SDK and add `dotnet` to your `PATH`. [Download the .NET SDK](https://dotnet.microsoft.com/en-us/download)

## User Requirements

- The Sims 3 v1.67.2 (Steam)
- A `Mods` folder configured to load `.package` files

## Building

Open Command Prompt in the project folder and run the build script.

```bat
build.bat
```

The build extracts six reference assemblies from the installed game and caches them in `.deps/Sims3/`. For a custom Steam library, pass the base-game folder or its `Game\Bin` directory.

```bat
build.bat --game-dir "D:\SteamLibrary\steamapps\common\The Sims 3"
```

The build writes `Arcana - An Occult Overhaul.package` and its companion DLL to `build/`. [Build compatibility notes](BUILD_COMPATIBILITY.md)

## Installing

Copy `Arcana - An Occult Overhaul.package` into `Documents/Electronic Arts/The Sims 3/Mods/Packages/`. In game, select a supported occult Sim and click the Supernatural Journal button in Simology.
