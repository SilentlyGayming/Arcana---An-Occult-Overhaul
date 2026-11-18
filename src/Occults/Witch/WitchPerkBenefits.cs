using System.Collections.Generic;

namespace SupernaturalExpanded.Journal
{
    // One benefit source for both pages. Index matches Perk.Describe, not a sum of upgrades.
    // Player-facing benefit summaries describe the intended perk design; implementation coverage is tracked in docs.
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
