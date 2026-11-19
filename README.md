# Arcana – An Occult Overhaul

Arcana work in progress script mod to overhaul all of the occult races for the Sims 3. Current implementation is limited to a UI journal and perk system fully implemented for witches. This mod does utilise AI for research and assisting the overhaul project. No AI-generated images will ever be involved in the making of this mod, and all assets are either TS4, TS3, or hand-created. 

## Requirements

- Python 3.9 or newer. Use the Windows Python launcher or add `python` to your `PATH`. [Download Python](https://www.python.org/downloads/windows/)
- A C# compiler. The build uses the Windows .NET Framework compiler when available. For the SDK compiler, install a .NET SDK and add `dotnet` to your `PATH`. [Download the .NET SDK](https://dotnet.microsoft.com/en-us/download)

## User Requirements

- The Sims 3 v1.67.2 (Steam) with Supernatural for the Witch gameplay and Aleister's station
- A `Mods` folder configured to load `.package` files

## Building

Open Command Prompt in the project folder and run the build script.

```bat
build.bat
```

The first build extracts seven reference assemblies from the installed game and caches them in `.deps/Sims3/`. For a custom Steam library, pass the base-game folder or its `Game\Bin` directory.

```bat
build.bat --game-dir "D:\SteamLibrary\steamapps\common\The Sims 3"
```

The build writes `Arcana - An Occult Overhaul.package` and its companion DLL to `build/`. To run the source and package checks without compiling:

```bat
python tools\verify_grimoire.py
```

## Layout

| Folder | Contents |
| --- | --- |
| `src/Core` | Startup, world lifecycle, the bootstrap tuning XML and native layout IDs |
| `src/Journal`, `src/UI` | The Supernatural Journal pages, the Grimoire, styling and the Simology entry |
| `src/Localization` | String lookup and percentage formatting |
| `src/Magic` | The spell catalog with fixed costs, spell knowledge, Study Spells at Aleister's station and cheats |
| `src/Occults` | Shared occult status, meter and perk trees |
| `src/Occults/Witch` | Witch ranks, milestones, perks, saved progress, abilities and Grimoire cost quotes |
| `src/Occults/Witch/Casting` | Spell Accuracy, Magical Power, cost corrections for native spells, backfires, regeneration and Magical Fizzle |
| `assets` | Icons, the vampire bite-mark replacement and the station's catalog resource |
| `tools` | Build, packaging and verification scripts; `tools/tests` holds the C# formula checks |

## Witch gameplay

Every spell, charm, curse, ritual and Conjure Apple has a fixed Magical Power cost in `src/Magic/SpellCatalog.cs`. It does not grow with the Witch's power pool. The cost actually spent is that base cost × rank and perk discounts × wand, moon and familiar multipliers, and the Grimoire shows the same number.

Accuracy, power pools, discounts and recovery are tuned in `src/Occults/Witch/Casting/WitchCastingRules.cs`. Good Luck Charm, Ice Blast, Love Charm, Haunting Curse, Sunlight Charm and Pestilence Curse cast through Arcana. Other spells, rituals and object casts keep the game's own interactions; `WitchNativeCosts.cs` corrects their Magical Power drain to the fixed cost when the game drains it.

## Aleister's All-in-One Magic Station

The station keeps its price, model, recolours, catalog placement and alchemy. Arcana ships the game's catalog resource for it under the same key with its name and description pointing at Arcana's strings (`assets/Catalog/AlchemyStation`). That file is the game's resource from `DeltaBuild_p15.package` with only the two string hashes at offsets 5598 and 5606 replaced. English shows the new name and description; other languages keep the game's text. Nothing is read from the game for this.

Witches get Study Spells next to Research Alchemy. It raises Spellcasting and does not teach spells; the pace is `SpellStudy.SkillPointsPerSimMinute` in `src/Magic/StudySpells.cs`.

## Installing

Copy `Arcana - An Occult Overhaul.package` into `Documents/Electronic Arts/The Sims 3/Mods/Packages/`. In game, select a supported occult Sim and click the Supernatural Journal button in Simology.
