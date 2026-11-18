using System;

namespace SupernaturalExpanded.Journal
{
    [Flags]
    internal enum WitchSpellKind
    {
        Spell = 0,
        Curse = 1,
        Ritual = 2
    }

    // Tiers use the same zero-based indices as Rank. Legendary has separate rules.
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
    }

    internal struct WitchAccuracy
    {
        internal const float Base = 50f;

        internal float RankBonus;
        internal float Wand;
        internal float Mood;
        internal float Needs;
        internal float DarkArts;
        internal float Ascendancy;
        internal float Coven;
        internal float Limit;
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

    internal struct WitchCastingRules
    {
        internal const int Legendary = 5;
        internal const float PracticeExperience = 1.10f;
        internal const float MasterStudySpeed = 1.10f;
        internal const float MasterFizzleDuration = 0.80f;
        internal const float FamiliarProtection = 40f;
        internal const float DarkArtsRefundChance = 25f;
        internal const float LowNeedFraction = 0.25f;
        internal const float MaxEmotionPenalty = 30f;
        internal const float MagicHandsAccuracyBonus = 20f;

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
        internal float Capacity { get { return RankCapacity * (Ascendancy ? 1.20f : 1f); } }
        internal float StudySpeed { get { return Rank >= Rank.Master ? MasterStudySpeed : 1f; } }
        internal float FizzleDuration { get { return Rank >= Rank.Master ? MasterFizzleDuration : 1f; } }

        internal float Regeneration(bool asleep, bool witchingHour)
        {
            return RegenerationFactors(asleep, witchingHour, false).Rate;
        }

        internal WitchRegeneration RegenerationFactors(bool asleep, bool witchingHour, bool fizzled)
        {
            float rankBonus = Rank >= Rank.HighWitch ? 1.15f : Rank >= Rank.Adept ? 1.10f : 1f;
            float sleepBonus = asleep ? 1f + 0.05f * Rest : 1f;
            float hourBonus = !asleep && witchingHour && WitchingHour ? 1.25f : 1f;
            return new WitchRegeneration(rankBonus, sleepBonus, hourBonus, InCoven ? 1.20f : 1f, fizzled ? 0.80f : 1f);
        }

        internal float FatigueDrain(float powerCost, float nativeMinimum, float nativeMaximum)
        {
            return powerCost * Math.Max(0f, nativeMaximum - nativeMinimum) / Capacity;
        }

        internal static float PowerFraction(float fatigue, float nativeMinimum, float nativeMaximum)
        {
            if (nativeMaximum <= nativeMinimum)
            {
                return 0f;
            }
            return Math.Max(0f, Math.Min(1f, (nativeMaximum - fatigue) / (nativeMaximum - nativeMinimum)));
        }

        internal bool Overreaches(WitchSpell spell)
        {
            return spell.Tier < Legendary && spell.Tier > (int)Rank;
        }

        internal float Accuracy(WitchSpell spell, float wand, float emotionPenalty, int lowNeeds, int emptyNeeds)
        {
            return AccuracyFor(spell, wand, emotionPenalty, lowNeeds, emptyNeeds).Total;
        }

        internal WitchAccuracy AccuracyFor(WitchSpell spell, float wand, float emotionPenalty, int lowNeeds, int emptyNeeds)
        {
            WitchAccuracy result = new WitchAccuracy();
            if (Overreaches(spell))
            {
                return result;
            }
            if (spell.Tier == Legendary)
            {
                result.Total = float.NaN;
                return result;
            }

            result.RankBonus = 5f * (int)Rank;
            result.Wand = wand;
            result.Mood = -Math.Max(0f, Math.Min(MaxEmotionPenalty, emotionPenalty));
            result.Needs = -10f * lowNeeds;
            result.DarkArts = DarkArts ? -0.5f * result.Mood : 0f;
            result.Ascendancy = Ascendancy ? 10f : 0f;
            float tierBonus = 10f * ((int)Rank - spell.Tier);
            float curseOrRitual = OldWays && (spell.IsCurse || spell.IsRitual) ? 10f : 0f;
            float raw = WitchAccuracy.Base + result.RankBonus + tierBonus + wand;
            raw += result.Mood + result.DarkArts + result.Needs;
            raw += result.Ascendancy + curseOrRitual;
            float bounded = Math.Max(5f, Math.Min(95f, raw));
            float withCoven = InCoven && !spell.IsRitual ? 100f - (100f - bounded) * 0.90f : bounded;
            result.Coven = withCoven - bounded;
            float cap = emptyNeeds >= 2 ? 5f : emptyNeeds == 1 ? 15f : 95f;
            result.Total = Math.Min(cap, withCoven);
            result.Limit = result.Total - raw - result.Coven;
            return result;
        }

        internal float CostMultiplier(WitchSpell spell)
        {
            float cost = 1f;
            if (spell.Tier <= (int)Rank.Apprentice)
            {
                if (Rank >= Rank.Apprentice)
                {
                    cost *= 0.95f;
                }
                if (Lessons)
                {
                    cost *= 0.75f;
                }
            }
            if (Rank == Rank.HighWitch)
            {
                cost *= 0.95f;
            }
            if (Familiar)
            {
                cost *= 0.85f;
            }
            if (Hexweaver && spell.IsCurse)
            {
                cost *= 0.75f;
            }
            if (OldWays && (spell.IsRitual || spell.Tier == (int)Rank.HighWitch))
            {
                cost *= 0.75f;
            }
            if (spell.Tier < (int)Rank && Eidetic > 0)
            {
                cost *= Eidetic == 1 ? 0.93f : Eidetic == 2 ? 0.85f : 0.70f;
            }
            return cost;
        }

        internal float RefundFraction(WitchSpell spell)
        {
            return spell.IsCurse ? 0.85f : 0.70f;
        }

        internal float ScapegoatChance
        {
            get { return Scapegoat == 0 ? 0f : Scapegoat == 1 ? 25f : Scapegoat == 2 ? 40f : 55f; }
        }
    }
}
