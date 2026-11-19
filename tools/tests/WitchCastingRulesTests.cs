using System;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchCastingRulesTests
    {
        private static readonly WitchSpell TierOne = new WitchSpell(0, WitchSpellKind.Spell);
        private static readonly WitchSpell TierTwo = new WitchSpell(1, WitchSpellKind.Spell);
        private static readonly WitchSpell TierThree = new WitchSpell(2, WitchSpellKind.Spell);
        private static readonly WitchSpell TierFour = new WitchSpell(3, WitchSpellKind.Spell);
        private static readonly WitchSpell TierFive = new WitchSpell(4, WitchSpellKind.Spell);
        private static readonly WitchSpell Legendary = new WitchSpell(5, WitchSpellKind.Ritual);
        private static int checks;

        public static void Main()
        {
            Accuracy();
            NeedsSeverity();
            PerkAccuracy();
            AccuracyBreakdown();
            GroupedAccuracy();
            LoadedWitchRank();
            PercentRounding();
            ProgressionAndCost();
            CostBreakdown();
            CostRanges();
            PowerPoolConversion();
            RecoveryAndProtection();
            CharmClassification();
            FixedCosts();
            Console.WriteLine("PASS: " + checks + " deterministic Witch casting and progression checks");
        }

        private static void Accuracy()
        {
            WitchCastingRules novice = new WitchCastingRules(Rank.Novice);
            Equal(50f, novice.Accuracy(TierOne, 0f, 0f, 0, 0), "base");
            foreach (float wand in new[] { 5f, 10f, 15f, 20f, 25f })
            {
                Equal(50f + wand, novice.Accuracy(TierOne, wand, 0f, 0, 0), "wand bonus");
            }
            Equal(0f, novice.Accuracy(TierTwo, 25f, 0f, 0, 0), "overreach");
            Equal(40f, novice.Accuracy(TierOne, 0f, 0f, 10f, 0), "low Need");
            Equal(20f, novice.Accuracy(TierOne, 0f, 60f, 0, 0), "emotions cap");
            Equal(0f, novice.Accuracy(TierOne, 0f, 30f, 30f, 0), "negative totals show 0%");
            True(float.IsNaN(novice.Accuracy(Legendary, 0f, 0f, 0, 0)), "Legendary special");
            WitchCastingRules master = new WitchCastingRules(Rank.Master);
            Equal(65f, master.Accuracy(TierFour, 0f, 0f, 0, 0), "rank bonus");
            WitchAccuracy hands = master.AccuracyFor(TierFour, WitchCastingRules.MagicHandsAccuracyBonus, 0f, 0, 0);
            Equal(20f, hands.Wand, "Magic Hands breakdown");
            Equal(85f, hands.Total, "Magic Hands chance at Master rank");
            Equal(85f, master.Accuracy(TierTwo, 0f, 0f, 0, 0), "tier bonus");
            Equal(95f, master.Accuracy(TierTwo, 20f, 0f, 0, 0), "ceiling");
            Equal(15f, master.Accuracy(TierTwo, 20f, 0f, 10f, 1), "one empty Need");
            Equal(5f, master.Accuracy(TierTwo, 20f, 0f, 20f, 2), "two empty Needs");
        }

        private static void NeedsSeverity()
        {
            Equal(0f, WitchCastingRules.NeedPenalty(0.36f), "healthy Need");
            Equal(10f, WitchCastingRules.NeedPenalty(0.35f), "low Need boundary");
            Equal(10f, WitchCastingRules.NeedPenalty(0.21f), "low Need");
            Equal(25f, WitchCastingRules.NeedPenalty(0.20f), "very low Need boundary");
            Equal(25f, WitchCastingRules.NeedPenalty(0.06f), "very low Need");
            Equal(35f, WitchCastingRules.NeedPenalty(0.05f), "critical Need boundary");
            Equal(35f, WitchCastingRules.NeedPenalty(0f), "empty Need");
            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            high.DarkArts = true;
            Equal(40f, high.Accuracy(TierFive, 20f, 30f, 35f, 0), "critical Hunger screenshot example");
            high.OldWays = true;
            Equal(55f, high.Accuracy(new WitchSpell(4, WitchSpellKind.Curse), 20f, 30f, 35f, 0), "curse specialty");
            Equal(55f, high.Accuracy(new WitchSpell(4, WitchSpellKind.Ritual), 20f, 30f, 35f, 0), "ritual specialty");
            Equal(15f, high.Accuracy(TierFive, 20f, 30f, 35f, 1), "empty Need cap");
            Equal(0f, high.Accuracy(TierFive, 20f, 30f, 70f, 2), "two empty Needs with a negative total");
            high.OldWays = false;
            high.Ascendancy = true;
            Equal(50f, high.Accuracy(TierFive, 20f, 30f, 35f, 0), "Ascendancy with critical Hunger");
            Equal(10f, high.GeneralAccuracyBonus, "general capstone modifier available to future Legendary resolver");
        }

        private static void PerkAccuracy()
        {
            WitchCastingRules adept = new WitchCastingRules(Rank.Adept);
            Equal(30f, adept.Accuracy(TierThree, 0f, 20f, 10f, 0), "unbought Dark Arts");
            adept.DarkArts = true;
            Equal(40f, adept.Accuracy(TierThree, 0f, 20f, 10f, 0), "Dark Arts halves emotions only");
            adept.InCoven = true;
            Equal(82f, adept.Accuracy(TierThree, 20f, 0f, 0, 0), "relative coven failure reduction");
            Equal(80f, adept.Accuracy(new WitchSpell(2, WitchSpellKind.Ritual), 20f, 0f, 0, 0), "no coven ritual bonus");
            Equal(0f, adept.Accuracy(TierFour, 25f, 0f, 0, 0), "coven overreach");
            Equal(15f, adept.Accuracy(TierThree, 25f, 0f, 10f, 1), "coven empty Need cap");
            WitchCastingRules covenHigh = new WitchCastingRules(Rank.HighWitch);
            covenHigh.InCoven = true;
            Equal(91f, covenHigh.Accuracy(TierThree, 0f, 0f, 0, 0), "coven tier III");
            Equal(91f, covenHigh.Accuracy(new WitchSpell(2, WitchSpellKind.Curse), 0f, 0f, 0, 0), "coven tier III curse");
            Equal(80f, covenHigh.Accuracy(TierFour, 0f, 0f, 0, 0), "no coven tier IV bonus");
            Equal(70f, covenHigh.Accuracy(TierFive, 0f, 0f, 0, 0), "no coven tier V bonus");
            Equal(90f, covenHigh.Accuracy(new WitchSpell(2, WitchSpellKind.Ritual), 0f, 0f, 0, 0), "no coven tier III ritual bonus");

            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            high.Ascendancy = true;
            Equal(80f, high.Accuracy(TierFive, 0f, 0f, 0, 0), "Ascendancy accuracy");
            Equal(180f, high.Capacity, "Ascendancy capacity");
            high.Ascendancy = false;
            high.OldWays = true;
            Equal(70f, high.Accuracy(TierFive, 0f, 0f, 0, 0), "Old Ways category gate");
            Equal(85f, high.Accuracy(new WitchSpell(4, WitchSpellKind.Curse | WitchSpellKind.Ritual), 0f, 0f, 0, 0), "Old Ways applies once");
            Equal(15f, high.SpecialtyAccuracyBonus(Legendary), "Legendary specialist modifier available");
            high.Ascendancy = true;
            Equal(0f, high.SpecialtyAccuracyBonus(Legendary), "Conflicting capstones do not double-apply");
            high.Ascendancy = false;
            True(float.IsNaN(high.Accuracy(Legendary, 0f, 0f, 0, 0)), "Legendary success still separate");
            Equal(95f, high.Accuracy(new WitchSpell(4, WitchSpellKind.Curse), 25f, 0f, 0, 0), "Old Ways respects cap");
            Equal(15f, high.Accuracy(new WitchSpell(4, WitchSpellKind.Curse), 25f, 0f, 10f, 1), "Old Ways respects empty Need");
            Equal(0.85f, high.RefundFraction(new WitchSpell(2, WitchSpellKind.Curse)), "curse refund");
            Equal(0.70f, high.RefundFraction(TierThree), "ordinary refund");
        }

        private static void AccuracyBreakdown()
        {
            WitchCastingRules master = new WitchCastingRules(Rank.Master);
            WitchAccuracy accuracy = master.AccuracyFor(TierFour, 20f, 10f, 0, 0);
            Equal(15f, accuracy.RankBonus, "current rank breakdown");
            Equal(20f, accuracy.Wand, "wand breakdown");
            Equal(-10f, accuracy.Mood, "mood breakdown");
            Equal(75f, accuracy.Total, "current-rank total");
            Equal(95f, master.Accuracy(TierTwo, 20f, 10f, 0, 0), "lower-tier gameplay bonus remains in spell rolls");

            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            high.Ascendancy = true;
            high.DarkArts = true;
            high.InCoven = true;
            accuracy = high.AccuracyFor(TierFive, 25f, 20f, 10f, 1);
            Equal(10f, accuracy.Ascendancy, "Ascendancy breakdown");
            Equal(10f, accuracy.DarkArts, "Dark Arts breakdown");
            Equal(-10f, accuracy.Needs, "Needs breakdown");
            Equal(0f, accuracy.Coven, "no coven breakdown at Tier V");
            Equal(15f, accuracy.Total, "empty Need still caps current-rank accuracy");
            Equal(accuracy.Total, WitchAccuracy.Base + accuracy.RankBonus + accuracy.Wand + accuracy.Mood + accuracy.Needs
                + accuracy.DarkArts + accuracy.Ascendancy + accuracy.Coven + accuracy.Limit, "listed modifiers sum to the total");
        }

        private static void GroupedAccuracy()
        {
            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            high.DarkArts = true;
            high.OldWays = true;
            WitchSpell curse = new WitchSpell(4, WitchSpellKind.Curse);
            WitchSpell ritual = new WitchSpell(4, WitchSpellKind.Ritual);
            WitchAccuracy baseline = high.AccuracyFor(TierFive, 20f, 30f, 25f, 0);
            Equal(50f, baseline.Total, "grouped spells example");
            Equal(65f, high.Accuracy(curse, 20f, 30f, 25f, 0), "grouped curses example");
            Equal(65f, high.Accuracy(ritual, 20f, 30f, 25f, 0), "grouped rituals example");
            foreach (WitchSpell spell in new[] { curse, ritual })
            {
                foreach (int empty in new[] { 0, 1, 2 })
                {
                    WitchAccuracy a = high.AccuracyFor(TierFive, 25f, 0f, 35f * empty, empty);
                    WitchAccuracy b = high.AccuracyFor(spell, 25f, 0f, 35f * empty, empty);
                    Equal(b.Total, a.Total + high.SpecialtyAccuracyBonus(spell) + b.Coven - a.Coven + b.Limit - a.Limit,
                        "category adjustments reconcile with total, including caps");
                }
            }
            WitchCastingRules adept = new WitchCastingRules(Rank.Adept);
            adept.InCoven = true;
            WitchSpell adeptRitual = new WitchSpell(2, WitchSpellKind.Ritual);
            WitchAccuracy regular = adept.AccuracyFor(TierThree, 20f, 0f, 0f, 0);
            WitchAccuracy ritualReading = adept.AccuracyFor(adeptRitual, 20f, 0f, 0f, 0);
            Equal(ritualReading.Total, regular.Total + ritualReading.Coven - regular.Coven + ritualReading.Limit - regular.Limit,
                "ritual category accounts for absent coven bonus");
            foreach (WitchSpell spell in new[] { TierFive, curse, ritual, Legendary })
            {
                WitchAccuracy reading = high.AccuracyFor(spell, 20f, 30f, 35f, 1);
                Equal(-30f, reading.Mood, "all categories use emotional penalty");
                Equal(15f, reading.DarkArts, "all categories use Dark Arts mood offset");
                Equal(-35f, reading.Needs, "all categories use Needs penalty");
                Equal(15f, reading.Ceiling, "all categories expose empty Need ceiling");
                if (spell.Tier != WitchCastingRules.Legendary)
                    Equal(15f, reading.Total, "implemented category enforces empty Need cap");
            }
            True(float.IsNaN(high.Accuracy(Legendary, 20f, 30f, 25f, 0)), "no invented Legendary total");
        }

        private static void LoadedWitchRank()
        {
            int[] cumulative = { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };
            long[] thresholds = WitchProgression.RankThresholds(cumulative, 10);
            long loaded = WitchProgression.ExperienceForSkill(0f, 8, cumulative);
            Equal((int)Rank.Master, WitchProgression.GetRankState(loaded, thresholds, 0).Rank, "loaded Master with stale XP");
            long experienced = WitchProgression.ExperienceForSkill(850f, 8, cumulative);
            Equal(850000f, experienced, "earned XP above its level floor");
            long novice = WitchProgression.ExperienceForSkill(0f, 0, cumulative);
            Equal((int)Rank.Novice, WitchProgression.GetRankState(novice, thresholds, 0).Rank, "new Novice");
            Equal((int)Rank.HighWitch, WitchProgression.GetRankState(0L, thresholds, WitchProgression.AllMask).Rank, "saved milestone rank floor");
        }

        private static void PercentRounding()
        {
            Equal(438f, PercentText.AbsoluteTenths(43.75), "the reported fractional regeneration bonus");
            Equal(438f, PercentText.AbsoluteTenths(-43.75), "negative adjustment magnitude");
            Equal(0f, PercentText.AbsoluteTenths(-0.04), "no signed negative zero");
            Equal(150f, PercentText.AbsoluteTenths(14.99), "rounds across a whole percentage");
        }

        private static void ProgressionAndCost()
        {
            Equal(40f, WitchProgression.PointsIn(WitchProgression.AllMask), "lifetime points");
            Equal(13f, WitchProgression.MilestoneCount, "milestones");
            Equal(1.1f, WitchCastingRules.PracticeExperience, "Practice Magic XP");
            for (int rank = 0; rank < WitchProgression.RankCount; rank++)
            {
                Equal(50f + 25f * rank, new WitchCastingRules((Rank)rank).Capacity, "rank capacity");
            }

            WitchCastingRules apprentice = new WitchCastingRules(Rank.Apprentice);
            Equal(0.95f, apprentice.CostMultiplier(TierOne), "Apprentice tier I");
            Equal(0.95f, apprentice.CostMultiplier(TierTwo), "Apprentice tier II");
            Equal(1f, apprentice.CostMultiplier(TierThree), "Apprentice tier III");
            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            Equal(0.95f * 0.95f, high.CostMultiplier(TierTwo), "rank discounts stack");
            Equal(0.95f, high.CostMultiplier(TierFive), "High Witch discount");
            high.Lessons = true;
            high.Familiar = true;
            high.Eidetic = 3;
            high.Hexweaver = true;
            high.OldWays = true;
            Equal(0.95f * 0.75f * 0.95f * 0.85f * 0.75f * 0.70f,
                high.CostMultiplier(new WitchSpell(1, WitchSpellKind.Curse)), "compatible discounts");
            Equal(0.95f * 0.85f * 0.75f,
                high.CostMultiplier(new WitchSpell(4, WitchSpellKind.Ritual)), "Old Ways ritual once");
            Equal(0.95f * 0.85f, high.CostMultiplier(TierFive), "ordinary Tier V no specialty discount");
            Equal(0.95f * 0.85f * 0.75f, high.CostMultiplier(Legendary), "Legendary ritual discount once");
            Equal(0.95f * 0.85f * 0.75f, high.CostMultiplier(new WitchSpell(5, WitchSpellKind.Spell)), "Legendary ordinary discount");
        }

        private static void CostBreakdown()
        {
            // Checks that the cost breakdown matches the original rules across every combination.
            for (int rank = 0; rank <= (int)Rank.HighWitch; rank++)
            for (int tier = 0; tier <= WitchCastingRules.Legendary; tier++)
            foreach (WitchSpellKind kind in new[] { WitchSpellKind.Spell, WitchSpellKind.Curse, WitchSpellKind.Ritual,
                WitchSpellKind.Curse | WitchSpellKind.Ritual, WitchSpellKind.Charm })
            for (int perks = 0; perks < 32; perks++)
            for (int eidetic = 0; eidetic <= 3; eidetic++)
            {
                WitchCastingRules rules = new WitchCastingRules((Rank)rank);
                rules.Lessons = (perks & 1) != 0;
                rules.Familiar = (perks & 2) != 0;
                rules.Hexweaver = (perks & 4) != 0;
                rules.OldWays = (perks & 8) != 0;
                rules.Ascendancy = (perks & 16) != 0;
                rules.Eidetic = eidetic;
                WitchSpell spell = new WitchSpell(tier, kind);
                float expected = 1f;
                if (tier <= 1 && rank >= 1) expected *= 0.95f;
                if (tier <= 1 && rules.Lessons) expected *= 0.75f;
                if (rank == 4) expected *= 0.95f;
                if (rules.Familiar) expected *= 0.85f;
                if (rules.Hexweaver && spell.IsCurse) expected *= 0.75f;
                if (rules.OldWays && !rules.Ascendancy && (spell.IsRitual || tier == 5)) expected *= 0.75f;
                if (tier < rank && eidetic > 0) expected *= 1f - (eidetic == 1 ? 0.07f : eidetic == 2 ? 0.15f : 0.30f);
                Equal(expected, rules.CostMultiplier(spell), "cost breakdown preserves original cost");
                Equal(expected, rules.CostFactors(spell).Multiplier, "tooltip factors match cast");
            }
        }

        private static void CostRanges()
        {
            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            high.Lessons = true;
            high.Eidetic = 1;
            high.Hexweaver = true;
            float minimum, maximum;
            high.CostRange(WitchSpellKind.Spell, out minimum, out maximum);
            Equal(0.95f * 0.95f * 0.75f * 0.93f, minimum, "ordinary lowest-tier cost");
            Equal(0.95f, maximum, "ordinary current-rank cost");
            high.CostRange(WitchSpellKind.Curse, out minimum, out maximum);
            Equal(0.95f * 0.95f * 0.75f * 0.75f * 0.93f, minimum, "curse lowest-tier cost");
            Equal(0.95f * 0.75f, maximum, "curse current-rank cost");
            high.OldWays = true;
            high.CostRange(WitchSpellKind.Ritual, out minimum, out maximum);
            Equal(0.95f * 0.95f * 0.75f * 0.75f * 0.93f, minimum, "ritual lowest-tier cost");
            Equal(0.95f * 0.75f, maximum, "ritual current-rank cost");
            WitchCastingRules novice = new WitchCastingRules(Rank.Novice);
            novice.Lessons = true;
            novice.CostRange(WitchSpellKind.Spell, out minimum, out maximum);
            Equal(0.75f, minimum, "novice cost");
            Equal(minimum, maximum, "single-tier range collapses");
        }

        private static void PowerPoolConversion()
        {
            WitchCastingRules novice = new WitchCastingRules(Rank.Novice);
            Equal(50f, novice.Capacity, "Novice power pool");
            Equal(40f, novice.FatigueDrain(10f, -100f, 100f), "10 power on a 200-point native range");
            Equal(40f, WitchCastingRules.PowerFraction(-60f, -100f, 100f) * novice.Capacity, "remaining power after Good Luck Charm");
            Equal(20f, novice.FatigueDrain(10f, 0f, 100f), "10 power on a 100-point native range");
            Equal(40f, WitchCastingRules.PowerFraction(20f, 0f, 100f) * novice.Capacity, "same power with a different native range");
            Equal(1f, WitchCastingRules.PowerFraction(-110f, -100f, 100f), "full power clamps to capacity");
            Equal(0f, WitchCastingRules.PowerFraction(110f, -100f, 100f), "depleted power clamps to zero");

            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            high.Ascendancy = true;
            Equal(30f, high.Capacity - high.RankCapacity, "capacity tooltip Ascendancy contribution");
            Equal(10f, high.FatigueDrain(10f, -100f, 80f), "cast cost scales to the perk-adjusted pool");
            Equal(170f, WitchCastingRules.PowerFraction(-90f, -100f, 80f) * high.Capacity, "remaining power with the perk");
        }

        private static void RecoveryAndProtection()
        {
            WitchCastingRules apprentice = new WitchCastingRules(Rank.Apprentice);
            Equal(1f, apprentice.Regeneration(false, false), "early regen");
            Equal(1f, apprentice.StudySpeed, "early study");
            Equal(1f, apprentice.FizzleDuration, "early fizzle");
            WitchCastingRules adept = new WitchCastingRules(Rank.Adept);
            Equal(1.1f, adept.Regeneration(false, false), "Adept regen");
            WitchCastingRules master = new WitchCastingRules(Rank.Master);
            Equal(1.1f, master.StudySpeed, "Master study");
            Equal(0.8f, master.FizzleDuration, "Master fizzle");
            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            Equal(1.15f, high.Regeneration(false, false), "High Witch regen");
            high.InCoven = true;
            high.Rest = 3;
            high.WitchingHour = true;
            Equal(1.15f * 1.15f * 1.20f, high.Regeneration(true, true), "stacked sleep and coven");
            Equal(1.15f * 1.25f * 1.20f, high.Regeneration(false, true), "stacked witching hour and coven");
            WitchRegeneration regen = high.RegenerationFactors(false, true, true);
            Equal(1.15f, regen.Rank, "rank regeneration factor");
            Equal(0.80f, regen.Fizzle, "Fizzle regeneration factor");
            Equal(1.15f * 1.25f * 1.20f * 0.80f, regen.Rate, "regeneration includes active Fizzle");
            WitchCastingRules apprenticeWithHour = new WitchCastingRules(Rank.Apprentice);
            apprenticeWithHour.WitchingHour = true;
            Equal(1f, apprenticeWithHour.RegenerationFactors(false, true, true).Rate, "opposing effects can cancel");
            Equal(40f, WitchCastingRules.FamiliarProtection, "Familiar Bond protection");
            for (int upgrade = 1; upgrade <= 3; upgrade++)
            {
                Equal(0.05f * upgrade, WitchCastingRules.RestBonus(upgrade), "Rest tooltip and gameplay total");
                Equal(new[] { 0.07f, 0.15f, 0.30f }[upgrade - 1], WitchCastingRules.EideticDiscount(upgrade), "Eidetic tooltip and gameplay total");
            }
            float[] chances = { 0f, 25f, 40f, 55f };
            for (int i = 0; i < chances.Length; i++)
            {
                high.Scapegoat = i;
                Equal(chances[i], high.ScapegoatChance, "Scapegoat total chance");
            }
        }

        private static void CharmClassification()
        {
            True(WitchSpell.KindOf(SpellCategory.Spell) == WitchSpellKind.Spell, "Spell category maps to Spell");
            True(WitchSpell.KindOf(SpellCategory.Charm) == WitchSpellKind.Charm, "Charm category maps to Charm");
            True(WitchSpell.KindOf(SpellCategory.Curse) == WitchSpellKind.Curse, "Curse category maps to Curse");
            True(WitchSpell.KindOf(SpellCategory.Ritual) == WitchSpellKind.Ritual, "Ritual category maps to Ritual");
            True((WitchSpellKind.Charm & (WitchSpellKind.Curse | WitchSpellKind.Ritual)) == 0, "Charm flag overlaps Curse or Ritual");
            True((int)WitchSpellKind.Curse == 1 && (int)WitchSpellKind.Ritual == 2, "Existing kind values changed");
            for (int tier = 0; tier <= 5; tier++)
            {
                WitchSpell charm = new WitchSpell(tier, WitchSpell.KindOf(SpellCategory.Charm));
                True(charm.IsCharm && !charm.IsCurse && !charm.IsRitual, "Charm satisfies a Curse or Ritual test");
                True(!new WitchSpell(tier, WitchSpellKind.Spell).IsCharm && !new WitchSpell(tier, WitchSpellKind.Curse).IsCharm
                    && !new WitchSpell(tier, WitchSpellKind.Ritual).IsCharm, "Other kinds read as Charm");
            }
            float[] wands = { 0f, 5f, 20f, 25f };
            for (int rank = 0; rank <= 4; rank++)
            for (int tier = 0; tier <= 5; tier++)
            for (int perks = 0; perks < 256; perks++)
            {
                WitchCastingRules rules = new WitchCastingRules((Rank)rank);
                rules.Lessons = (perks & 1) != 0;
                rules.Familiar = (perks & 2) != 0;
                rules.Hexweaver = (perks & 4) != 0;
                rules.OldWays = (perks & 8) != 0;
                rules.Ascendancy = (perks & 16) != 0;
                rules.InCoven = (perks & 32) != 0;
                rules.DarkArts = (perks & 64) != 0;
                rules.Eidetic = (perks & 128) != 0 ? 3 : 0;
                WitchSpell charm = new WitchSpell(tier, WitchSpellKind.Charm);
                WitchSpell spell = new WitchSpell(tier, WitchSpellKind.Spell);
                Equal(rules.CostMultiplier(spell), rules.CostMultiplier(charm), "Charm cost differs from the ordinary baseline");
                WitchCostFactors factors = rules.CostFactors(charm);
                Equal(1f, factors.Hexweaver, "Hexweaver discounted a charm");
                Equal(0.70f, rules.RefundFraction(charm), "Charm refund is not the ordinary spell refund");
                True(WitchCastingRules.CovenEligible(charm) == (tier <= 2), "Power of Three eligibility for Tier I-III charms");
                True(WitchCastingRules.CovenEligible(charm) == WitchCastingRules.CovenEligible(spell), "Charm and spell coven eligibility differ");
                True(rules.Overreaches(charm) == rules.Overreaches(spell), "Charm rank eligibility differs");
                if (tier == WitchCastingRules.Legendary)
                {
                    continue;
                }
                Equal(1f, factors.OldWays, "The Old Ways discounted an ordinary charm");
                Equal(0f, rules.SpecialtyAccuracyBonus(charm), "Specialty accuracy applied to an ordinary charm");
                foreach (float wand in wands)
                {
                    WitchAccuracy a = rules.AccuracyFor(charm, wand, 18f, 10f, 0);
                    WitchAccuracy b = rules.AccuracyFor(spell, wand, 18f, 10f, 0);
                    Equal(b.Total, a.Total, "Charm accuracy differs from the ordinary baseline");
                    Equal(b.Coven, a.Coven, "Charm coven adjustment differs");
                    Equal(b.DarkArts, a.DarkArts, "Charm Dark Arts offset differs");
                    Equal(b.Ascendancy, a.Ascendancy, "Charm Ascendancy bonus differs");
                }
            }
            WitchCastingRules covenAdept = new WitchCastingRules(Rank.Adept);
            covenAdept.InCoven = true;
            WitchAccuracy coven = covenAdept.AccuracyFor(new WitchSpell(2, WitchSpellKind.Charm), 0f, 0f, 0, 0);
            True(coven.Coven > 0f, "The Power of Three skipped a Tier III charm");
            Equal(0f, covenAdept.AccuracyFor(new WitchSpell(2, WitchSpellKind.Ritual), 0f, 0f, 0, 0).Coven, "The Power of Three reached a ritual");
            WitchCastingRules hex = new WitchCastingRules(Rank.HighWitch);
            hex.Hexweaver = true;
            Equal(0.75f, hex.CostFactors(new WitchSpell(2, WitchSpellKind.Curse)).Hexweaver, "Hexweaver lost its curse discount");
            WitchCastingRules oldWays = new WitchCastingRules(Rank.HighWitch);
            oldWays.OldWays = true;
            Equal(WitchCastingRules.OldWaysAccuracyBonus, oldWays.SpecialtyAccuracyBonus(new WitchSpell(4, WitchSpellKind.Curse)), "The Old Ways lost its curse accuracy");
            Equal(0.75f, oldWays.CostFactors(new WitchSpell(4, WitchSpellKind.Ritual)).OldWays, "The Old Ways lost its ritual discount");
            Equal(0.75f, oldWays.CostFactors(new WitchSpell(5, WitchSpellKind.Charm)).OldWays, "The Old Ways lost its Legendary discount");
        }

        private static void FixedCosts()
        {
            // Checks that fixed costs ignore capacity and that native drain plus the correction equals the Grimoire cost exactly once.
            foreach (SpellEntry entry in SpellCatalog.Entries)
            {
                if (entry.Learning == SpellLearning.NativeDisabled) continue;
                WitchSpell spell = WitchSpell.For(entry);
                for (int rank = 0; rank <= (int)Rank.HighWitch; rank++)
                for (int ascendancy = 0; ascendancy < 2; ascendancy++)
                {
                    WitchCastingRules rules = new WitchCastingRules((Rank)rank);
                    rules.Ascendancy = ascendancy == 1;
                    float multiplier = rules.CostMultiplier(spell);
                    Equal(entry.Cost * multiplier, rules.FinalCost(entry.Cost, spell, 1f), "final cost is base times perks: " + entry.Id);
                    Equal(entry.Cost * multiplier * 1.3f, rules.FinalCost(entry.Cost, spell, 1.3f), "wand and moon multiply the final cost");
                    float final = rules.FinalCost(entry.Cost, spell, 1.15f);
                    float drain = rules.FatigueDrain(final, -100f, 100f);
                    Equal(final, drain / 200f * rules.Capacity, "power drained equals the quoted cost: " + entry.Id);
                    foreach (float native in new[] { 0f, 10f, 25f, 60f })
                    {
                        float correction = rules.NativeCorrection(entry.Cost, spell, native, 1.15f, -100f, 100f);
                        Equal(drain, native * 1.15f + correction, "native drain is corrected, not doubled: " + entry.Id);
                    }
                }
            }
            SpellEntry conversion = SpellCatalog.Find("conversion");
            for (int rank = 0; rank <= (int)Rank.HighWitch; rank++)
            {
                WitchCastingRules rules = new WitchCastingRules((Rank)rank);
                Equal(45f * rules.CostMultiplier(WitchSpell.For(conversion)), rules.FinalCost(conversion.Cost, WitchSpell.For(conversion), 1f),
                    "Conversion stays 45 before modifiers at every rank");
            }
            WitchCastingRules novice = new WitchCastingRules(Rank.Novice);
            Equal(45f, novice.FinalCost(conversion.Cost, WitchSpell.For(conversion), 1f), "Novice Conversion costs exactly 45");
            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            high.Lessons = true;
            high.Familiar = true;
            Equal(30f * 0.95f * 0.95f * 0.75f * 0.85f, high.FinalCost(30f, WitchSpell.For(SpellCatalog.Find("good_luck")), 1f),
                "discounts apply to the fixed cost");
            Equal(110f * 0.95f * 0.85f, high.FinalCost(110f, WitchSpell.For(SpellCatalog.Find("reanimation")), 1f),
                "Tier V ritual skips lower-tier discounts");
            Equal(FullDrain(high, 30f, WitchSpell.For(SpellCatalog.Find("upgrade"))),
                high.NativeCorrection(30f, WitchSpell.For(SpellCatalog.Find("upgrade")), 0f, 1f, -100f, 100f),
                "Magically Upgrade receives its whole cost from the correction");
        }

        private static float FullDrain(WitchCastingRules rules, float cost, WitchSpell spell)
        {
            return rules.FatigueDrain(rules.FinalCost(cost, spell, 1f), -100f, 100f);
        }

        private static void Equal(float expected, float actual, string check)
        {
            True(Math.Abs(actual - expected) < 0.001f, check + " (" + expected + " expected, got " + actual + ")");
        }

        private static void True(bool pass, string check)
        {
            if (!pass)
            {
                throw new InvalidOperationException(check);
            }
            checks++;
        }
    }
}
