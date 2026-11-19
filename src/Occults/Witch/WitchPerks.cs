using System.Collections.Generic;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchTree
    {
        internal static readonly OccultTree Tree = new OccultTree(
            JournalText.Get("Arcana.LifeState.Name.Witch"), JournalText.Get("Arcana.Journal.Perks.Points"),
            JournalText.Get("Arcana.Journal.Perks.PointsHelp"),
            new[]
            {
                JournalText.Get("Arcana.Witch.Rank.Short.Novice"),
                JournalText.Get("Arcana.Witch.Rank.Short.Apprentice"),
                JournalText.Get("Arcana.Witch.Rank.Short.Adept"),
                JournalText.Get("Arcana.Witch.Rank.Short.Master"),
                JournalText.Get("Arcana.Witch.Rank.Short.HighWitch"),
            },
            new[]
            {
                JournalText.Get("Arcana.Witch.Rank.Full.Novice"),
                JournalText.Get("Arcana.Witch.Rank.Full.Apprentice"),
                JournalText.Get("Arcana.Witch.Rank.Full.Adept"),
                JournalText.Get("Arcana.Witch.Rank.Full.Master"),
                JournalText.Get("Arcana.Witch.Rank.Full.HighWitch"),
            },
            CreatePerks());

        private static Perk[] CreatePerks()
        {
            Perk[] perks = new Perk[]
            {
                Power("RestForTheWicked", Rank.Novice, 0, 0.5f, JournalText.Get("Arcana.Witch.Perk.RestForTheWicked.Name"), 1, 1, 2),
                Power("LessonsInTheUnseenArts", Rank.Novice, 1, 0.5f, JournalText.Get("Arcana.Witch.Perk.LessonsInTheUnseenArts.Name"), 2),
                Power("TravellingScholar", Rank.Novice, 2, 0.5f, JournalText.Get("Arcana.Witch.Perk.TravellingScholar.Name"), 1),

                Power("SorcerersApprentice", Rank.Apprentice, 0, 0f, JournalText.Get("Arcana.Witch.Perk.SorcerersApprentice.Name"), 2),
                Power("AlchemicalSavant", Rank.Apprentice, 1, 0f, JournalText.Get("Arcana.Witch.Perk.AlchemicalSavant.Name"), 3),
                Power("FamiliarBond", Rank.Apprentice, 2, 0f, JournalText.Get("Arcana.Witch.Perk.FamiliarBond.Name"), 2),
                Power("Hexweaver", Rank.Apprentice, 3, 0f, JournalText.Get("Arcana.Witch.Perk.Hexweaver.Name"), 3),

                Power("TrialByFire", Rank.Adept, 0, 0f, JournalText.Get("Arcana.Witch.Perk.TrialByFire.Name"), 3),
                Power("TheWitchingHour", Rank.Adept, 1, 0f, JournalText.Get("Arcana.Witch.Perk.TheWitchingHour.Name"), 3),
                Power("ThePowerOfThree", Rank.Adept, 2, 0f, JournalText.Get("Arcana.Witch.Perk.ThePowerOfThree.Name"), 3),
                Power("DarkArts", Rank.Adept, 3, 0f, JournalText.Get("Arcana.Witch.Perk.DarkArts.Name"), 4),

                Power("PracticallyMagic", Rank.Master, 0, 0f, JournalText.Get("Arcana.Witch.Perk.PracticallyMagic.Name"), 3),
                Power("EideticWitchary", Rank.Master, 1, 0f, JournalText.Get("Arcana.Witch.Perk.EideticWitchary.Name"), 3, 4, 5),
                Power("PowerShunt", Rank.Master, 2, 0f, JournalText.Get("Arcana.Witch.Perk.PowerShunt.Name"), 4),
                Power("Scapegoat", Rank.Master, 3, 0f, JournalText.Get("Arcana.Witch.Perk.Scapegoat.Name"), 3, 4, 5),

                Power("ArcaneAscendancy", Rank.HighWitch, 0, 0f, JournalText.Get("Arcana.Witch.Perk.ArcaneAscendancy.Name"), 6),
                Power("TheOldWays", Rank.HighWitch, 3, 0f, JournalText.Get("Arcana.Witch.Perk.TheOldWays.Name"), 6)
            };

            perks[15].Conflict = perks[16];
            perks[16].Conflict = perks[15];
            perks[15].Face = 0xFF7A45E0;
            perks[16].Face = 0xFF2E9E4F;
            return perks;
        }

        private static Perk Power(string key, Rank rank, int column, float shift, string name, params int[] costs)
        {
            // Creates a perk with one cost per upgrade rank.
            return new Perk(key, rank, column, shift, name, costs);
        }
    }

    internal static class WitchPerkBenefits
    {
        internal static TooltipModifier[] For(Perk perk, int descriptionIndex)
        {
            int upgrade = perk.PreviewIndex(descriptionIndex) + 1;
            List<TooltipModifier> lines = new List<TooltipModifier>();
            WitchCastingRules rules = new WitchCastingRules(Rank.Novice);
            switch (perk.Key)
            {
                case "RestForTheWicked":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.SleepRegen", Signed(WitchCastingRules.RestBonus(upgrade) * 100f)), true, false));
                    break;
                case "LessonsInTheUnseenArts":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.BeginnerCost", Signed(-WitchCastingRules.LessonsDiscount * 100f)), true, false));
                    break;
                case "TravellingScholar":
                    lines.Add(new TooltipModifier(JournalText.Get("Arcana.Witch.Benefit.PortableStudy"), true, false));
                    break;
                case "SorcerersApprentice":
                    lines.Add(new TooltipModifier(JournalText.Get("Arcana.Witch.Benefit.Mentor"), true, false));
                    lines.Add(new TooltipModifier(JournalText.Get("Arcana.Witch.Benefit.MentorTier"), true, false));
                    break;
                case "AlchemicalSavant":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.ExtraElixir", Unsigned(30f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.CommonIngredient", Unsigned(35f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.RareIngredient", Unsigned(5f)), true, false));
                    break;
                case "FamiliarBond":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.FamiliarCost", Signed(-WitchCastingRules.FamiliarDiscount * 100f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.FamiliarProtect", Unsigned(WitchCastingRules.FamiliarProtection)), true, false));
                    break;
                case "Hexweaver":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.CurseCost", Signed(-WitchCastingRules.HexweaverDiscount * 100f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.CurseDuration", Signed((WitchCastingRules.HexweaverDuration - 1f) * 100f)), true, false));
                    break;
                case "TrialByFire":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.DuelDiscovery", Unsigned(30f)), true, false));
                    break;
                case "TheWitchingHour":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.NightRegen", Signed(WitchCastingRules.WitchingHourRegen * 100f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.NightStudy", Signed(20f)), true, false));
                    break;
                case "ThePowerOfThree":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.CovenRegen", Signed(WitchCastingRules.CovenRegen * 100f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.CovenFailure", Signed(-WitchCastingRules.CovenFailureReduction * 100f)), true, false));
                    break;
                case "DarkArts":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.EmotionPenalty", Signed(-WitchCastingRules.DarkArtsPenaltyReduction * 100f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.RefundChance", Unsigned(WitchCastingRules.DarkArtsRefundChance)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.SpellRefund", Unsigned(rules.RefundFraction(new WitchSpell(0, WitchSpellKind.Spell)) * 100f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.CurseRefund", Unsigned(rules.RefundFraction(new WitchSpell(0, WitchSpellKind.Curse)) * 100f)), true, false));
                    break;
                case "PracticallyMagic":
                    lines.Add(new TooltipModifier(JournalText.Get("Arcana.Witch.Benefit.Wandless"), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.HandsAccuracy", Signed(WitchCastingRules.MagicHandsAccuracyBonus)), true, false));
                    break;
                case "EideticWitchary":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.LowerTierCost", Signed(-WitchCastingRules.EideticDiscount(upgrade) * 100f)), true, false));
                    break;
                case "PowerShunt":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.ShuntPower", Unsigned(20f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.ShuntEnergy", Unsigned(30f)), false, false));
                    lines.Add(new TooltipModifier(JournalText.Get("Arcana.Witch.Benefit.ShuntCooldown"), false, false));
                    break;
                case "Scapegoat":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.Redirect", Unsigned(WitchCastingRules.ScapegoatProtection(upgrade))), true, false));
                    break;
                case "ArcaneAscendancy":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.Capacity", Signed(WitchCastingRules.AscendancyCapacityBonus * 100f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.GeneralAccuracy", Signed(WitchCastingRules.AscendancyAccuracyBonus)), true, false));
                    break;
                case "TheOldWays":
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.RitualCost", Signed(-WitchCastingRules.OldWaysDiscount * 100f)), true, false));
                    lines.Add(new TooltipModifier(JournalText.Format("Arcana.Witch.Benefit.SpecialtyAccuracy", Signed(WitchCastingRules.OldWaysAccuracyBonus)), true, false));
                    break;
            }
            return lines.ToArray();
        }

        private static string Signed(float amount) { return PercentText.Format(amount, true); }
        private static string Unsigned(float amount) { return PercentText.Format(amount, false); }
    }
}
