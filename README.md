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
| `src/Occults/Magic` | Shared Fairy and Genie casting: caster rules, power pools, the Genie Magic skill, spell tests and Grimoire abilities |
| `src/Occults/Witch` | Witch ranks, milestones, perks, saved progress, abilities and Grimoire cost quotes |
| `src/Occults/Witch/Casting` | Spell Accuracy, Magical Power, cost corrections for native spells, backfires, regeneration and Magical Fizzle |
| `assets` | Icons, the vampire bite-mark replacement and the station's catalog resource |
| `tools` | Build, packaging and verification scripts; `tools/tests` holds the C# formula checks |

## Witch gameplay

Every spell, charm, curse, ritual and Conjure Apple has a fixed Magical Power cost in `src/Magic/SpellCatalog.cs`. It does not grow with the Witch's power pool. The cost actually spent is that base cost × rank and perk discounts × wand, moon and familiar multipliers, and the Grimoire shows the same number.

Rank only limits which spells a Sim can learn from the spellbook and the alchemy station. A known spell can always be cast if the Sim has enough power; a spell above the caster's rank (Witch rank, or Fairy Magic / Genie Magic skill level ÷ 2) loses 45% accuracy per tier above it, shown directly under Base Accuracy in the Spell Accuracy tooltip. Every Sim-targeted spell, charm and curse uses Arcana's accuracy for every caster. The game's own ground, plumbing, statue, Magically Upgrade and Conversion spells and the native rituals still roll success with the game's own formula, which mods cannot change per cast.

Accuracy, power pools, discounts and recovery are tuned in `src/Occults/Witch/Casting/WitchCastingRules.cs`. Good Luck Charm, Ice Blast, Love Charm, Haunting Curse, Sunlight Charm and Pestilence Curse cast through Arcana. Other spells, rituals and object casts keep the game's own interactions; `WitchNativeCosts.cs` corrects their Magical Power drain to the fixed cost when the game drains it.

## Fairy and Genie magic

Fairies and Genies can study at the alchemy station, discover spells and cast them with their own power: Fairies spend Faeric Power, Genies spend the saved Genie power pool (0–100, refilling 10 per Sim hour). They study at half a Witch's speed and raise Fairy Magic or the new Genie Magic skill; every two skill levels let them learn the next spell tier, up to Tier IV. They cannot learn Tier V or Legendary magic.

They start at 60% accuracy (Witches start at 50%) and cast through Innate Power instead of a wand: +25% accuracy for Fairies, −15% spell cost for Genies. Witch rank discounts and Witch perks do not apply. They cast every Sim-targeted spell, charm and curse they know through Arcana's own casting, and the ground, plumbing, statue, Magically Upgrade and Conversion spells through the game's own interactions. For those, Arcana keeps the game's Magic Hands item in their inventory, which the native spells use in place of a wand, and charges the fixed cost from their own power. Witches with Practically Magic get the same Magic Hands, so every native spell works for them without a wand. Arcana's Magic Hands are worth nothing, so they can never count toward household value or fire insurance, and they are removed within a few Sim minutes from any Sim who stops being a Fairy or Genie or loses Practically Magic.

## Aleister's All-in-One Magic Station

The station keeps its price, model, recolours, catalog placement and alchemy. Arcana ships the game's catalog resource for it under the same key with its name and description pointing at Arcana's strings (`assets/Catalog/AlchemyStation`). That file is the game's resource from `DeltaBuild_p15.package` with only the two string hashes at offsets 5598 and 5606 replaced. English shows the new name and description; other languages keep the game's text. Nothing is read from the game for this.

Witches get Study Spells next to Research Alchemy. It raises Spellcasting and does not teach spells; the pace is `SpellStudy.SkillPointsPerSimMinute` in `src/Magic/StudySpells.cs`.

## Installing

Copy `Arcana - An Occult Overhaul.package` into `Documents/Electronic Arts/The Sims 3/Mods/Packages/`. In game, select a supported occult Sim and click the Supernatural Journal button in Simology.
