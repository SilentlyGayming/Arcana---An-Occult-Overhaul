# Arcana – An Occult Overhaul

Arcana is a work in progress occult overhaul for The Sims 3. The current build includes the Journal, shared Witch/Fairy/Genie spell research and casting, Witch and Genie perk trees, and Genie's Fortune's Rain. Five Witch perks are visible as previews and cannot be purchased. Existing saved ranks are retained for Travelling Scholar, Sorcerer's Apprentice, Alchemical Savant, Trial by Fire and Power Shunt. No AI-generated images are used; assets come from TS3, TS4 or are hand-created.

## Requirements

- Python 3.9 or newer. Use the Windows Python launcher or add `python` to your `PATH`. [Download Python](https://www.python.org/downloads/windows/)
- A C# compiler. The build uses the Windows .NET Framework compiler when available. For the SDK compiler, install a .NET SDK and add `dotnet` to your `PATH`. [Download the .NET SDK](https://dotnet.microsoft.com/en-us/download)

## User Requirements

- The Sims 3 v1.67.2 (Steam) with Supernatural for the Witch gameplay and Aleister's station
- A `Mods` folder configured to load `.package` files

Showtime supplies Genies and lamps. Youth also requires Generations, Fame requires Late Night, and the Weather Stone requires Seasons and Supernatural. The local assembly checks use Steam 1.67.2. EA App 1.69 gameplay compatibility remains unverified.

Keep a pre-install save if you intend to remove Arcana. Saves can contain custom skills, moodlets, queued interactions and pending service or lamp records. For removal, exit the game, remove the package and reopen a pre-install backup. Retain the original modded save if you want its progression later.

## Building

Open Command Prompt in the project folder and run the build script.

```bat
build.bat
```

The first build extracts seven reference assemblies from the installed game and caches them in `.deps/Sims3/`. For a custom Steam library, pass the base-game folder or its `Game\Bin` directory.

```bat
build.bat --game-dir "D:\SteamLibrary\steamapps\common\The Sims 3"
```

For unattended batch runs, set `ARCANA_NO_PAUSE=1`. Use `--dotnet` to select the deterministic SDK compiler. The Windows Framework compiler can produce different DLL bytes across builds.

The build writes `Arcana - An Occult Overhaul.package` and its companion DLL to `build/`. To run the source and package checks without compiling:

```bat
python tools\verify_grimoire.py
```

The C# formula, perk tree, Spell Knowledge and Weather Stone checks run on the host with the .NET 8 SDK and the cached game references. They do not run the game or its UI. Pass suite names (`casting-rules`, `genie-tree`, `spell-knowledge`, `weather-stone`, `wish-system`, `wish-menu`) to run only some of them. When the .NET 8 targeting pack is unavailable, pass `--framework net9.0` or `--framework net10.0` for an installed host SDK. The mod still targets the game runtime.

```bat
python tools\run_host_tests.py
```

## Layout

| Folder | Contents |
| --- | --- |
| `src/Core` | Startup, world lifecycle, the bootstrap tuning XML and native layout IDs |
| `src/Journal` | The Supernatural Journal pages, the Grimoire, styling, animations and the Simology entry |
| `src/Localization` | String lookup and percentage formatting |
| `src/Spells` | The spell catalog with fixed costs, spell knowledge, Study Spells at Aleister's station and cheats |
| `src/Occults` | Shared occult status, meter, perk trees, milestones, saved perk records and the Perks page selection |
| `src/Occults/Magic` | Shared magic for every caster: casting rules, caster profiles, Overview attributes, power pools, spell tests and Grimoire abilities |
| `src/Occults/Genie` | The Genie Magic skill, Genie abilities, perks, wishes and Fortune's Rain at the Weather Stone |
| `src/Occults/Witch` | Witch perks |
| `src/Occults/Witch/Casting` | Spell Accuracy, Magical Power, cost corrections for native spells, backfires, regeneration and Magical Fizzle |
| `assets` | Icons, the vampire bite-mark replacement and the station's catalog resource |
| `tools` | Build, packaging and verification scripts; `tools/tests` holds the C# host checks |

## Witch gameplay

Every spell, charm, curse, ritual and Conjure Apple has a fixed Magical Power cost in `src/Spells/SpellCatalog.cs`. It does not grow with the Witch's power pool. The cost actually spent is that base cost × rank and perk discounts × wand, moon and familiar multipliers, and the Grimoire shows the same number.

Rank only limits which spells a Sim can discover through Study Spells at the alchemy station. A known spell can always be cast if the Sim has enough power; a spell above the caster's rank (Witch rank, or Fairy Magic / Genie Magic skill level ÷ 2) loses 45% accuracy per tier above it, shown directly under Base Accuracy in the Spell Accuracy tooltip. Every Sim-targeted spell, charm and curse uses Arcana's accuracy for every caster. The game's own ground, plumbing, statue, Magically Upgrade and Conversion spells and the native rituals still roll success with the game's own formula, which this build does not replace per cast.

Accuracy, costs, power pools and recovery are shared by every caster in `src/Occults/Magic/CastingRules.cs`; what differs per race (base accuracy, Innate Power, pool size by rank, rank bonuses, learnable tiers and Backlash) lives in one profile each in `src/Occults/Magic/CasterProfile.cs`. Research speed is centralized in `src/Spells/SpellResearchRules.cs` and configured in `src/Core/JournalBootstrap.xml`. Good Luck Charm, Ice Blast, Love Charm, Haunting Curse, Sunlight Charm and Pestilence Curse cast through Arcana. Other spells, rituals and object casts keep the game's own interactions; `WitchNativeCosts.cs` corrects their Magical Power drain to the fixed cost when the game drains it.

## Fairy and Genie magic

Fairies and Genies can study at the alchemy station, discover spells and cast them with their own Magical Power: Fairies spend their native fairy power (always 100), Genies spend the Magical Power saved in their Genie Magic skill. They study at half a Witch's speed and raise Fairy Magic or the new Genie Magic skill; every two skill levels let them learn the next spell tier, up to Tier III. They cannot learn Tier IV, Tier V or Legendary magic, except Genies on the Path of the Unbound, who reach Tier IV through Fugitive of Old and Tier V through Unleashed.

They start at 60% accuracy (Witches start at 50%) and cast through Innate Power instead of a wand: +25% accuracy for Fairies, +25% cost on borrowed Witch spells for Genies (Ensnared Sorcery), which Unbound replaces with -25% (Ancient Sorcery). Witch rank discounts and Witch perks do not apply. They cast every Sim-targeted spell, charm and curse they know through Arcana's own casting, and the ground, plumbing, statue, Magically Upgrade and Conversion spells through the game's own interactions. For those, Arcana keeps the game's Magic Hands item in their inventory, which the native spells use in place of a wand, and charges the fixed cost from their own power. Witches with Practically Magic get the same Magic Hands, so every native spell works for them without a wand. Arcana's Magic Hands are worth nothing, so they can never count toward household value or fire insurance, and they are removed within a few Sim minutes from any Sim who stops being a Fairy or Genie or loses Practically Magic.

### Genie ranks, abilities and Backlash

Genie Magic sets a Genie's rank, two levels per rank: Un-Chained, Awakened, Ascendant, Transcendent and Almighty. The Journal header, Overview, proficiency bar and rank tooltips show the rank. Ranks mainly add Maximum Magical Power (100, 150, 200, 250, 300) and +2 percentage points of Spell Accuracy each (+8 at Almighty); Magical Power refills 10 per Sim hour and held power is kept when the pool grows. Ascendant also adds +25% Spell Learning Speed once, which speeds study of spells, charms, curses and rituals but never Genie Magic experience or Legendary magic. Ranks give no ability cost, regeneration or Backlash bonus, so the perks keep those for themselves.

Genie abilities unlock automatically by rank and cost Magical Power: Clean Sim (10, Un-Chained), Summon Food (20, Awakened), Enchant Home (30) and Banish Sim (40) (Ascendant), Ensorcel Sim (80) (Transcendent) and the free, never-failing Release Sim (Transcendent, not listed in the Grimoire). Almighty adds no ability; wishes belong to the perk tree. The Grimoire Abilities tab lists only what the selected Genie has unlocked, from rank or perks: Return to Lamp always, then Lamp Retreat (Pocket Palace), Ask About Their Heart's Desire and Genie's Favor (Heart's Desire), the 12 playable wish entries (Genie Law, with automatic daily success from Law of the Lamp) and Grant Your Own Wish (Counterattack). Locked abilities are hidden everywhere in the Grimoire and listed only in the rank tooltips on the Perks page. The game's own Genie magic points are kept full, since Magical Power replaces them.

Every Genie failure is a Backlash, and from Tier II a spell or ability that works can still rebound (5%, 10%, 15% and 20% for Tiers II–V). Backlash never goes away: perks cut the chance, but it cannot drop below a floor that rises with the spell's tier (5%, 8%, 12%, 16% and 20% for Tiers I–V). Genie Law only keeps Genie abilities from failing. A Backlash fizzles the cast and applies Magical Fizzle; a cancelled failure is a plain fizzle and a cancelled rebound lets the cast work. Backlash Chance is a Genie-only Overview row, with each learnable tier and the floor in its tooltip.

### Genie perks

Genies have their own Perks page with 15 perks over five ranks. Rank I holds three shared perks in a centred row: Domestic Djinn, A Thousand and One Nights (three upgrades) and Fine Print (toggleable Delicate Casting). Buying any one opens the Rank II choice between Unbound (Path of the Unbound, red, left) and Lampbound (Path of the Lampbound, blue, right); the two paths are mutually exclusive. Genie Magic earns 40 Perk Points across 13 milestones.

| Rank | Path of the Unbound | Path of the Lampbound |
| --- | --- | --- |
| II | Unbound | Lampbound |
| III | Rising Tide, Wellspring (three upgrades) | Pocket Palace, Silken Command |
| IV | Fugitive of Old | Heart's Desire |
| V | Unleashed, Counterattack | Genie Law, Law of the Lamp |

Either Rank III perk opens the Rank IV perk, which opens the Rank V perk. Counterattack sits left of centre on the Rank V row, joined to Unleashed by a red line; Law of the Lamp sits right of centre, joined to Genie Law by a blue line. There is no sixth rank.

Unbound adds power and cheaper spells but costlier abilities and longer Backlash; Lampbound cuts Backlash and ability costs at the price of dearer spells. Backlash never goes away: no perk can take it below the floor for the spell's tier. Wellspring and A Thousand and One Nights show their own total at each upgrade, not a running sum. Genie wishes (Genie Law grants them to others, Counterattack to yourself, Law of the Lamp guarantees one a day) and Heart's Desire work through the Genie's Sim interactions; Counterattack's Fate's Price table is internal and never shown in tooltips.

Saves from the 19-perk tree are migrated once, marked inside the Genie record: perks whose name and cost did not change stay bought, and every removed or reworked perk is refunded at what it cost, never above the points earned. Witch and Fairy records are not touched.

The Perks page, perk points and selection work for any tree: each tree has its own control block in the layout (Witch `0x53E12000`, Genie `0x53E14000`), its own saved records (`WitchProgressStore`, `GenieProgressStore`) and its own text prefix, so Fairies can be added the same way.

### Genie wishes

Genie... has one Grant Wish... action that opens the actual vanilla wish-selection dialog. It lists World Peace / World Misery, Resurrect a Sim or pet, Love, Long Life, Large Family, Happiness, Fortune, Beauty, the Power to Cheat Death, Youth (Generations), Success, and Fame (Late Night), filtered for the recipient. The existing Make a Wish / Make Another Wish action on a lamp-bound Genie opens that same expanded dialog and additionally offers More Wishes and Free the Genie when eligible. Grant Your Own Wish... also uses the dialog for its three distinct existing self-wishes. Native and Arcana actions share the exact localized Genie submenu path.

Build 0.9.3.2 allows playable ghosts to receive wishes and pairs both Sims before the wisher approaches the Genie. Original wishes use EA's descriptions and the lamp's original popups. The four additions have blank descriptions; other new feedback is brief. Failed wishes share a Wish Gone Wrong moodlet, with old wish-origin moodlets migrated automatically. The audit also fixes custom moodlet cloning, proximity mood updates, Beauty's temporary romance modifier, and explicit travel storage for custom wish effects and Genie power/cooldowns.

Fortune grants exactly §100,000; failure grants §0 and requests the real Repo Man. Free the Genie starts the original freedom quest; failure returns the existing Genie to the existing lamp and persistently hands that lamp to a new owner outside the previous/active household. Sim-owned wish effects use a hidden native skill with clone and property-stream export/import; pending lamp and service operations use world-owned saved records.

Playable wishes use the caster's current accuracy, mood and needs, the Genie Law wish bonus, shared Genie Backlash, and the existing power-spend/refund code. The base cost is 100 Magical Power with path ability modifiers. Law of the Lamp automatically guarantees the next valid successful grant once per 24 hours. Self-wishes retain their original three outcomes, base Rank V pool cost and Fate's Price. Both perk specializations still cost 40 points.

See [the regression checklist](Arcana_Regression_Test_Checklist.txt) for live validation and [the earlier implementation notes](docs/history/GENIE_WISH_IMPLEMENTATION.txt) for the original wish design. This build compiles and has host/source checks; native services, death scenes, animation and real save/load/travel still need testing in The Sims 3.

```bat
python tools\verify_wishes.py
python tools\run_host_tests.py wish-system wish-menu
```

### Fortune's Rain

Teen and older Genies get Summon Weather: Fortune's Rain on the Weather Stone (Seasons and Supernatural), plus Join and Summon Weather With. The ritual uses the stone's own routing, animation, timing, success chance, joining bonus and cooldown, and its notifications follow the stone's wording and layout; Witches cannot join a Genie summon and Genies cannot join Bewitching Rain. On success, ordinary rain falls for the stone's tuned duration. Every Genie gets Feeling Lucky and every human caught outside gets Feeling Unlucky, each lasting three hours like the stone's other weather moodlets; the same luck moodlets from Genie's Favor and wishes also work on Sims without the Lucky or Unlucky trait. While the rain falls, Genies regenerate Magical Power 50% faster and their Backlash chance drops by a quarter, never below the tier floor; both show in the Journal's Power Regeneration and Backlash Chance tooltips.

## Aleister's All-in-One Magic Station

The station keeps its price, model, recolours, catalog placement and alchemy. Arcana ships the game's catalog resource for it under the same key with its name and description pointing at Arcana's strings (`assets/Catalog/AlchemyStation`). That file is the game's resource from `DeltaBuild_p15.package` with only the two string hashes at offsets 5598 and 5606 replaced. English shows the new name and description; other languages keep the game's text. Nothing is read from the game for this.

Witches, Fairies and Genies get Study Spells next to Research Alchemy. It awards their own magic skill XP and ends after one eligible spell is discovered. Legendary magic and disabled native content are excluded. Portable book study, mentorship and duel learning are not implemented.

Research tuning is in `src/Core/JournalBootstrap.xml`: `kStudyResearchIntervalMinutes` (30), `kStudyDiscoveryChancePercent` (25), `kStudyExperienceFraction` (0.40), the three `kStudyWitchSpeed` / `kStudyFairySpeed` / `kStudyGenieSpeed` factors (1 / 0.5 / 0.5), and percentage bonuses `kStudyWitchMasterBonusPercent` (10), `kStudyGenieAscendantBonusPercent` (25), `kStudyWitchingHourBonusPercent` (20). Compile after editing the source XML. Invalid settings fall back individually to their defaults.

Research speed = species factor x rank bonus x active night perk bonus. Roll interval = base minutes / speed; discovery chance is a separate roll. The Witching Hour requires the bought perk and runs from midnight until 4 AM. It stacks once with the Master rank bonus (1.10 x 1.20 = 1.32). Ascendant's bonus applies once at Rank III and above (0.5 x 1.25 = 0.625). Speed changes preserve fractional work within the running interaction and take effect at the next loop sample. A simulation stall produces at most one roll per callback. Partial research is not carried into another study interaction.

Learning bonuses do not boost skill XP. Study XP per Sim minute is the live native Practice Magic output / 60 x Arcana's practice factor (1.10) x `kStudyExperienceFraction` (0.40), before any native skill modifiers. Rank unlock and night perk tooltip values read the same tuning used by research.

## Installing

Copy `Arcana - An Occult Overhaul.package` into `Documents/Electronic Arts/The Sims 3/Mods/Packages/`. In game, select a supported occult Sim and click the Supernatural Journal button in Simology.
