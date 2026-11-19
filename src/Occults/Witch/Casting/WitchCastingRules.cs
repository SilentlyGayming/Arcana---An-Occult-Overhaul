using System;

namespace SupernaturalExpanded.Journal
{
    [Flags]
    internal enum WitchSpellKind
    {
        Spell = 0,
        Curse = 1,
        Ritual = 2,
        Charm = 4
    }

    internal struct WitchSpell
    {
        internal readonly int Tier;
        internal readonly WitchSpellKind Kind;

        internal WitchSpell(int tier, WitchSpellKind kind)
        {
            Tier = tier;
            Kind = kind;
        }

        internal bool IsCurse { get { return (Kind & WitchSpellKind.Curse) != 0; } }
        internal bool IsRitual { get { return (Kind & WitchSpellKind.Ritual) != 0; } }
        internal bool IsCharm { get { return (Kind & WitchSpellKind.Charm) != 0; } }

        internal static WitchSpell For(SpellEntry entry)
        {
            return new WitchSpell(entry.Tier, KindOf(entry.Category));
        }

        internal static WitchSpellKind KindOf(SpellCategory category)
        {
            // Maps a Grimoire category to its casting kind explicitly so tab order never decides casting behavior.
            switch (category)
            {
                case SpellCategory.Charm: return WitchSpellKind.Charm;
                case SpellCategory.Curse: return WitchSpellKind.Curse;
                case SpellCategory.Ritual: return WitchSpellKind.Ritual;
                default: return WitchSpellKind.Spell;
            }
        }
    }

    internal struct WitchAccuracy
    {
        internal const float Base = 50f;
        internal const float Minimum = 0f;
        internal const float Maximum = 95f;

        internal float RankBonus;
        internal float Wand;
        internal float Mood;
        internal float Needs;
        internal float DarkArts;
        internal float Ascendancy;
        internal float Coven;
        internal float Limit;
        internal float Ceiling;
        internal float Total;
    }

    internal struct WitchRegeneration
    {
        internal static readonly WitchRegeneration Baseline = new WitchRegeneration(1f, 1f, 1f, 1f, 1f);

        internal readonly float Rank;
        internal readonly float Rest;
        internal readonly float Hour;
        internal readonly float Coven;
        internal readonly float Fizzle;

        internal WitchRegeneration(float rank, float rest, float hour, float coven, float fizzle)
        {
            Rank = rank;
            Rest = rest;
            Hour = hour;
            Coven = coven;
            Fizzle = fizzle;
        }

        internal float Rate { get { return Rank * Rest * Hour * Coven * Fizzle; } }
    }

    internal struct WitchCostFactors
    {
        internal float Apprentice, Lessons, HighWitch, Familiar, Hexweaver, OldWays, Eidetic;

        internal float Multiplier
        {
            get { return Apprentice * Lessons * HighWitch * Familiar * Hexweaver * OldWays * Eidetic; }
        }
    }

    internal struct WitchCastingRules
    {
        internal const int Legendary = 5;
        internal const float PracticeExperience = 1.10f;
        internal const float MasterStudySpeed = 1.10f;
        internal const float MasterFizzleDuration = 0.80f;
        internal const float FamiliarProtection = 40f;
        internal const float DarkArtsRefundChance = 25f;
        internal static float NeedPenalty(float remaining)
        {
            // Returns the accuracy penalty for a need's remaining fraction of its native range.
            if (remaining <= 0.05f) return 35f;
            if (remaining <= 0.20f) return 25f;
            if (remaining <= 0.35f) return 10f;
            return 0f;
        }
        internal const float MaxEmotionPenalty = 30f;
        internal const float MagicHandsAccuracyBonus = 20f;
        internal const float ApprenticeDiscount = 0.05f;
        internal const float HighWitchDiscount = 0.05f;
        internal const float LessonsDiscount = 0.25f;
        internal const float FamiliarDiscount = 0.15f;
        internal const float HexweaverDiscount = 0.25f;
        internal const float HexweaverDuration = 1.25f;
        internal const float RestPerUpgrade = 0.05f;
        internal const float WitchingHourRegen = 0.25f;
        internal const float CovenRegen = 0.20f;
        internal const float CovenFailureReduction = 0.10f;
        internal const int CovenMaximumTier = (int)Rank.Adept;
        internal const float DarkArtsPenaltyReduction = 0.50f;
        internal const float AscendancyCapacityBonus = 0.20f;
        internal const float AscendancyAccuracyBonus = 10f;
        internal const float OldWaysDiscount = 0.25f;
        internal const float OldWaysAccuracyBonus = 15f;

        internal static float EideticDiscount(int upgrade)
        {
            return upgrade <= 0 ? 0f : upgrade == 1 ? 0.07f : upgrade == 2 ? 0.15f : 0.30f;
        }

        internal static float RestBonus(int upgrade)
        {
            return RestPerUpgrade * Math.Max(0, Math.Min(3, upgrade));
        }

        internal static float ScapegoatProtection(int upgrade)
        {
            return upgrade <= 0 ? 0f : upgrade == 1 ? 25f : upgrade == 2 ? 40f : 55f;
        }

        internal float GeneralAccuracyBonus
        {
            get { return Ascendancy ? AscendancyAccuracyBonus : 0f; }
        }

        internal float SpecialtyAccuracyBonus(WitchSpell spell)
        {
            // Returns The Old Ways' accuracy bonus for curses, rituals and Legendary magic.
            return OldWays && !Ascendancy && (spell.IsCurse || spell.IsRitual || spell.Tier == Legendary)
                ? OldWaysAccuracyBonus : 0f;
        }

        internal readonly Rank Rank;
        internal bool Lessons;
        internal bool Familiar;
        internal bool Hexweaver;
        internal bool DarkArts;
        internal bool InCoven;
        internal bool Ascendancy;
        internal bool OldWays;
        internal int Eidetic;
        internal int Rest;
        internal bool WitchingHour;
        internal int Scapegoat;

        internal WitchCastingRules(Rank rank) : this()
        {
            Rank = rank;
        }

        internal const float NoviceCapacity = 50f;
        internal float RankCapacity { get { return NoviceCapacity + 25f * (int)Rank; } }
        internal float Capacity { get { return RankCapacity * (Ascendancy ? 1f + AscendancyCapacityBonus : 1f); } }
        internal float StudySpeed { get { return Rank >= Rank.Master ? MasterStudySpeed : 1f; } }
        internal float FizzleDuration { get { return Rank >= Rank.Master ? MasterFizzleDuration : 1f; } }

        internal float Regeneration(bool asleep, bool witchingHour)
        {
            return RegenerationFactors(asleep, witchingHour, false).Rate;
        }

        internal WitchRegeneration RegenerationFactors(bool asleep, bool witchingHour, bool fizzled)
        {
            // Returns each regeneration factor from rank, sleep, the witching hour, the coven and fizzles.
            float rankBonus = Rank >= Rank.HighWitch ? 1.15f : Rank >= Rank.Adept ? 1.10f : 1f;
            float sleepBonus = asleep ? 1f + RestBonus(Rest) : 1f;
            float hourBonus = !asleep && witchingHour && WitchingHour ? 1f + WitchingHourRegen : 1f;
            return new WitchRegeneration(rankBonus, sleepBonus, hourBonus, InCoven ? 1f + CovenRegen : 1f, fizzled ? 0.80f : 1f);
        }

        internal float FatigueDrain(float powerCost, float nativeMinimum, float nativeMaximum)
        {
            // Converts a power cost to the native fatigue range.
            return powerCost * Math.Max(0f, nativeMaximum - nativeMinimum) / Capacity;
        }

        internal static float PowerFraction(float fatigue, float nativeMinimum, float nativeMaximum)
        {
            // Returns the remaining power as a fraction of the native fatigue range.
            if (nativeMaximum <= nativeMinimum)
            {
                return 0f;
            }
            return Math.Max(0f, Math.Min(1f, (nativeMaximum - fatigue) / (nativeMaximum - nativeMinimum)));
        }

        internal bool Overreaches(WitchSpell spell)
        {
            // Returns whether an ordinary spell's tier is above the caster's rank.
            return spell.Tier < Legendary && spell.Tier > (int)Rank;
        }

        internal float Accuracy(WitchSpell spell, float wand, float emotionPenalty, float needPenalty, int emptyNeeds)
        {
            return AccuracyFor(spell, wand, emotionPenalty, needPenalty, emptyNeeds).Total;
        }

        internal WitchAccuracy AccuracyFor(WitchSpell spell, float wand, float emotionPenalty, float needPenalty, int emptyNeeds)
        {
            // Returns the accuracy breakdown for a spell from rank, tier, wand, mood, needs and perks.
            WitchAccuracy result = new WitchAccuracy();
            if (Overreaches(spell))
            {
                return result;
            }
            result.RankBonus = 5f * (int)Rank;
            result.Wand = wand;
            result.Mood = -Math.Max(0f, Math.Min(MaxEmotionPenalty, emotionPenalty));
            result.Needs = -Math.Max(0f, needPenalty);
            result.DarkArts = DarkArts ? -DarkArtsPenaltyReduction * result.Mood : 0f;
            result.Ascendancy = GeneralAccuracyBonus;
            result.Ceiling = emptyNeeds >= 2 ? 5f : emptyNeeds == 1 ? 15f : WitchAccuracy.Maximum;
            if (spell.Tier == Legendary)
            {
                result.Total = float.NaN;
                return result;
            }
            float tierBonus = 10f * ((int)Rank - spell.Tier);
            float curseOrRitual = SpecialtyAccuracyBonus(spell);
            float raw = WitchAccuracy.Base + result.RankBonus + tierBonus + wand;
            raw += result.Mood + result.DarkArts + result.Needs;
            raw += result.Ascendancy + curseOrRitual;
            float bounded = Math.Max(WitchAccuracy.Minimum, Math.Min(WitchAccuracy.Maximum, raw));
            float withCoven = InCoven && CovenEligible(spell) ? 100f - (100f - bounded) * (1f - CovenFailureReduction) : bounded;
            result.Coven = withCoven - bounded;
            result.Total = Math.Min(result.Ceiling, withCoven);
            result.Limit = result.Total - raw - result.Coven;
            return result;
        }

        internal static bool CovenEligible(WitchSpell spell)
        {
            // The Power of Three covers Tier I-III spells, charms and curses; rituals and Legendary magic stay excluded.
            return spell.Tier >= 0 && spell.Tier <= CovenMaximumTier && !spell.IsRitual;
        }

        internal WitchCostFactors CostFactors(WitchSpell spell)
        {
            // Returns the cost factors shared by the actual drain and the cost tooltip.
            bool earlyTier = spell.Tier <= (int)Rank.Apprentice;
            WitchCostFactors factors = new WitchCostFactors();
            factors.Apprentice = earlyTier && Rank >= Rank.Apprentice ? 1f - ApprenticeDiscount : 1f;
            factors.Lessons = earlyTier && Lessons ? 1f - LessonsDiscount : 1f;
            factors.HighWitch = Rank == Rank.HighWitch ? 1f - HighWitchDiscount : 1f;
            factors.Familiar = Familiar ? 1f - FamiliarDiscount : 1f;
            factors.Hexweaver = Hexweaver && spell.IsCurse ? 1f - HexweaverDiscount : 1f;
            factors.OldWays = OldWays && !Ascendancy && (spell.IsRitual || spell.Tier == Legendary) ? 1f - OldWaysDiscount : 1f;
            factors.Eidetic = spell.Tier < (int)Rank && Eidetic > 0 ? 1f - EideticDiscount(Eidetic) : 1f;
            return factors;
        }

        internal float CostMultiplier(WitchSpell spell)
        {
            return CostFactors(spell).Multiplier;
        }

        internal float FinalCost(float baseCost, WitchSpell spell, float drainMultiplier)
        {
            // Returns the Magical Power a cast spends: fixed base cost, rank and perk factors, then wand, moon and familiar multipliers.
            return baseCost * CostMultiplier(spell) * drainMultiplier;
        }

        internal float NativeCorrection(float baseCost, WitchSpell spell, float nativeDrain, float drainMultiplier,
            float nativeMinimum, float nativeMaximum)
        {
            // Returns the fatigue change that turns a native wand drain into exactly the final fixed cost.
            return FatigueDrain(FinalCost(baseCost, spell, drainMultiplier), nativeMinimum, nativeMaximum) - nativeDrain * drainMultiplier;
        }

        internal void CostRange(WitchSpellKind kind, out float minimum, out float maximum)
        {
            // Returns display-only cost bounds across the tiers up to the caster's rank.
            minimum = maximum = CostMultiplier(new WitchSpell(0, kind));
            for (int tier = 1; tier <= (int)Rank; tier++)
            {
                float cost = CostMultiplier(new WitchSpell(tier, kind));
                minimum = Math.Min(minimum, cost);
                maximum = Math.Max(maximum, cost);
            }
        }

        internal float RefundFraction(WitchSpell spell)
        {
            // Returns the share of spent power Dark Arts refunds.
            return spell.IsCurse ? 0.85f : 0.70f;
        }

        internal float ScapegoatChance
        {
            get { return ScapegoatProtection(Scapegoat); }
        }
    }
}
