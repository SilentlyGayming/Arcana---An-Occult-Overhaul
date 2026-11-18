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
            PerkAccuracy();
            AccuracyBreakdown();
            LoadedWitchRank();
            PercentRounding();
            ProgressionAndCost();
            PowerPoolConversion();
            RecoveryAndProtection();
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
            Equal(40f, novice.Accuracy(TierOne, 0f, 0f, 1, 0), "low Need");
            Equal(20f, novice.Accuracy(TierOne, 0f, 60f, 0, 0), "emotions cap");
            Equal(5f, novice.Accuracy(TierOne, 0f, 30f, 3, 0), "floor");
            True(float.IsNaN(novice.Accuracy(Legendary, 0f, 0f, 0, 0)), "Legendary special");
            WitchCastingRules master = new WitchCastingRules(Rank.Master);
            Equal(65f, master.Accuracy(TierFour, 0f, 0f, 0, 0), "rank bonus");
            WitchAccuracy hands = master.AccuracyFor(TierFour, WitchCastingRules.MagicHandsAccuracyBonus, 0f, 0, 0);
            Equal(20f, hands.Wand, "Magic Hands breakdown");
            Equal(85f, hands.Total, "Magic Hands chance at Master rank");
            Equal(85f, master.Accuracy(TierTwo, 0f, 0f, 0, 0), "tier bonus");
            Equal(95f, master.Accuracy(TierTwo, 20f, 0f, 0, 0), "ceiling");
            Equal(15f, master.Accuracy(TierTwo, 20f, 0f, 1, 1), "one empty Need");
            Equal(5f, master.Accuracy(TierTwo, 20f, 0f, 2, 2), "two empty Needs");
        }

        private static void PerkAccuracy()
        {
            WitchCastingRules adept = new WitchCastingRules(Rank.Adept);
            Equal(30f, adept.Accuracy(TierThree, 0f, 20f, 1, 0), "unbought Dark Arts");
            adept.DarkArts = true;
            Equal(40f, adept.Accuracy(TierThree, 0f, 20f, 1, 0), "Dark Arts halves emotions only");
            adept.InCoven = true;
            Equal(82f, adept.Accuracy(TierThree, 20f, 0f, 0, 0), "relative coven failure reduction");
            Equal(80f, adept.Accuracy(new WitchSpell(2, WitchSpellKind.Ritual), 20f, 0f, 0, 0), "no coven ritual bonus");
            Equal(0f, adept.Accuracy(TierFour, 25f, 0f, 0, 0), "coven overreach");
            Equal(15f, adept.Accuracy(TierThree, 25f, 0f, 1, 1), "coven empty Need cap");

            WitchCastingRules high = new WitchCastingRules(Rank.HighWitch);
            high.Ascendancy = true;
            Equal(80f, high.Accuracy(TierFive, 0f, 0f, 0, 0), "Ascendancy accuracy");
            Equal(180f, high.Capacity, "Ascendancy capacity");
            high.Ascendancy = false;
            high.OldWays = true;
            Equal(70f, high.Accuracy(TierFive, 0f, 0f, 0, 0), "Old Ways category gate");
            Equal(80f, high.Accuracy(new WitchSpell(4, WitchSpellKind.Curse | WitchSpellKind.Ritual), 0f, 0f, 0, 0), "Old Ways applies once");
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
            accuracy = high.AccuracyFor(TierFive, 25f, 20f, 1, 1);
            Equal(10f, accuracy.Ascendancy, "Ascendancy breakdown");
            Equal(10f, accuracy.DarkArts, "Dark Arts breakdown");
            Equal(-10f, accuracy.Needs, "Needs breakdown");
            Equal(1.5f, accuracy.Coven, "coven breakdown");
            Equal(15f, accuracy.Total, "empty Need still caps current-rank accuracy");
            Equal(accuracy.Total, WitchAccuracy.Base + accuracy.RankBonus + accuracy.Wand + accuracy.Mood + accuracy.Needs
                + accuracy.DarkArts + accuracy.Ascendancy + accuracy.Coven + accuracy.Limit, "listed modifiers sum to the total");
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
                high.CostMultiplier(new WitchSpell(4, WitchSpellKind.Ritual)), "Old Ways Tier V ritual once");
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
            float[] chances = { 0f, 25f, 40f, 55f };
            for (int i = 0; i < chances.Length; i++)
            {
                high.Scapegoat = i;
                Equal(chances[i], high.ScapegoatChance, "Scapegoat total chance");
            }
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
