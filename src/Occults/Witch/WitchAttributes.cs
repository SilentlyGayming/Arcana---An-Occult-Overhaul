using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchAttributes
    {
        internal static OverviewTip PowerTooltip(string title, WitchCastingRules rules)
        {
            // Builds the Magical Power tooltip from rank and Arcane Ascendancy.
            List<TooltipModifier> lines = new List<TooltipModifier>();
            int rankBonus = (int)(rules.RankCapacity - WitchCastingRules.NoviceCapacity);
            if (rankBonus > 0)
            {
                lines.Add(new TooltipModifier(JournalText.Format("Arcana.Common.ModifierLine",
                    JournalText.Get("Arcana.Journal.Overview.Accuracy.Rank"), "+" + rankBonus), true));
            }
            int perkBonus = (int)Math.Round(rules.Capacity - rules.RankCapacity);
            if (perkBonus > 0)
            {
                lines.Add(new TooltipModifier(JournalText.Format("Arcana.Common.ModifierLine",
                    JournalText.Get("Arcana.Witch.Perk.ArcaneAscendancy.Name"), "+" + perkBonus), true));
            }
            string baseline = JournalText.Format("Arcana.Journal.Overview.Power.Base", (int)WitchCastingRules.NoviceCapacity);
            return new OverviewTip(title, JournalText.Get("Arcana.Journal.Overview.Power.Tagline"), baseline, lines.ToArray());
        }

        internal static ModifierReading ReadRegeneration(OverviewContext context)
        {
            // Reads the regeneration row and its tooltip from the applied regeneration factors.
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !WitchCastingContext.IsWitch(sim) || sim.BuffManager == null || sim.Motives == null)
            {
                return ModifierReading.None;
            }

            Motive motive = sim.Motives.GetMotive(CommodityKind.MagicFatigue);
            WitchRegeneration regeneration = WitchMagicRecovery.AppliedRegeneration(motive);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rank"), (regeneration.Rank - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.RestForTheWicked.Name"), (regeneration.Rest - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.TheWitchingHour.Name"), (regeneration.Hour - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ThePowerOfThree.Name"), (regeneration.Coven - 1f) * 100f);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Fizzle.Name"), (regeneration.Fizzle - 1f) * 100f);
            OverviewTip tip = new OverviewTip(JournalText.Get("Arcana.Journal.Overview.Attributes.PowerRegeneration"),
                JournalText.Get("Arcana.Journal.Overview.Regeneration.Tagline"),
                JournalText.Get("Arcana.Journal.Overview.Regeneration.Base"), lines.ToArray());
            return new ModifierReading((regeneration.Rate - 1f) * 100f, "", tip);
        }

        internal static ModifierReading ReadSpellCost(OverviewContext context)
        {
            // Reads the Spell Cost row and its per-category tooltip using the same factors as casting.
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !WitchCastingContext.IsWitch(sim))
            {
                return ModifierReading.None;
            }

            WitchCastingRules rules = WitchCastingContext.ReadRules(sim);
            int tier = (int)rules.Rank;
            WitchSpell current = new WitchSpell(tier, WitchSpellKind.Spell);
            WitchCostFactors ordinary = rules.CostFactors(current);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            AddCostFactor(lines, JournalText.Get("Arcana.Journal.Overview.Cost.HighWitch"), ordinary.HighWitch);
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.FamiliarBond.Name"), ordinary.Familiar);

            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Spells"), rules, current);
            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Charms"), rules, new WitchSpell(tier, WitchSpellKind.Charm));
            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Curses"), rules, new WitchSpell(tier, WitchSpellKind.Curse));
            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rituals"), rules, new WitchSpell(tier, WitchSpellKind.Ritual));
            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Legendary"), rules, new WitchSpell(WitchCastingRules.Legendary, WitchSpellKind.Spell));

            OverviewTip tip = new OverviewTip(JournalText.Get("Arcana.Journal.Overview.Attributes.SpellCost"),
                JournalText.Get("Arcana.Journal.Overview.Cost.Tagline"), JournalText.Get("Arcana.Journal.Overview.Cost.Base"), lines.ToArray());
            float minimum, maximum;
            rules.CostRange(WitchSpellKind.Spell, out minimum, out maximum);
            string range = FormatCostRange((maximum - 1f) * 100f, (minimum - 1f) * 100f, true);
            return new ModifierReading((rules.CostMultiplier(current) - 1f) * 100f, "", tip, range);
        }

        private static void AddCostCategory(List<TooltipModifier> lines, string label, WitchCastingRules rules, WitchSpell spell)
        {
            // Adds one category's cost range and the factors that shape it.
            WitchCostFactors factors = rules.CostFactors(spell);
            bool legendary = spell.Tier == WitchCastingRules.Legendary;
            float minimum = factors.Multiplier;
            float maximum = minimum;
            if (!legendary)
            {
                rules.CostRange(spell.Kind, out minimum, out maximum);
            }
            string range = FormatCostRange(minimum * 100f, maximum * 100f, false);
            lines.Add(new TooltipModifier(JournalText.Format("Arcana.Common.ModifierLine", label, range), true, true, false, true));
            if (!legendary && PercentText.AbsoluteTenths(minimum * 100f) != PercentText.AbsoluteTenths(maximum * 100f))
            {
                AddCostTierGroups(lines, rules, spell.Kind);
            }
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.Hexweaver.Name"), factors.Hexweaver);
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.TheOldWays.Name"), factors.OldWays);

            if (spell.Tier == WitchCastingRules.Legendary)
            {
                return;
            }
            WitchCostFactors lower = rules.CostFactors(new WitchSpell((int)Rank.Novice, spell.Kind));
            string earlyTiers = rules.Rank == Rank.Novice
                ? JournalText.Get("Arcana.Journal.Overview.Cost.FirstTier")
                : JournalText.Get("Arcana.Journal.Overview.Cost.EarlyTiers");
            AddCostFactor(lines, JournalText.Get("Arcana.Journal.Overview.Cost.Apprentice"), lower.Apprentice, earlyTiers);
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.LessonsInTheUnseenArts.Name"), lower.Lessons, earlyTiers);
            if (lower.Eidetic != 1f && rules.Rank > Rank.Novice)
            {
                string last = OverviewProviders.GetRomanNumeral((int)rules.Rank);
                string lowerTiers = rules.Rank == Rank.Apprentice
                    ? JournalText.Get("Arcana.Journal.Overview.Cost.FirstTier")
                    : JournalText.Format("Arcana.Journal.Overview.Cost.TierRange", last);
                AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.EideticWitchary.Name"), lower.Eidetic, lowerTiers);
            }
        }

        private static string FormatCostRange(float first, float last, bool signed)
        {
            string a = PercentText.Format(first, signed);
            string b = PercentText.Format(last, signed);
            if (a == b) return a;
            return signed ? JournalText.Format("Arcana.Journal.Overview.Cost.SignedRange", a, b)
                : JournalText.Format("Arcana.Journal.Overview.Cost.Range", a, b);
        }

        private static void AddCostTierGroups(List<TooltipModifier> lines, WitchCastingRules rules, WitchSpellKind kind)
        {
            // Merges contiguous tiers with equal displayed costs.
            int start = 0;
            string amount = PercentText.Format(rules.CostMultiplier(new WitchSpell(0, kind)) * 100f, false);
            for (int tier = 1; tier <= (int)rules.Rank + 1; tier++)
            {
                string next = tier <= (int)rules.Rank
                    ? PercentText.Format(rules.CostMultiplier(new WitchSpell(tier, kind)) * 100f, false) : null;
                if (next == amount)
                {
                    continue;
                }
                string first = OverviewProviders.GetRomanNumeral(start + 1);
                string last = OverviewProviders.GetRomanNumeral(tier);
                string name = start == tier - 1
                    ? JournalText.Format("Arcana.Journal.Overview.Cost.TierSingle", first)
                    : JournalText.Format("Arcana.Journal.Overview.Cost.TierSpan", first, last);
                lines.Add(new TooltipModifier(JournalText.Format("Arcana.Journal.Overview.Cost.TierTotal", name, amount), true, true));
                start = tier;
                amount = next;
            }
        }

        private static void AddCostFactor(List<TooltipModifier> lines, string label, float factor)
        {
            AddCostFactor(lines, label, factor, null);
        }

        private static void AddCostFactor(List<TooltipModifier> lines, string label, float factor, string qualifier)
        {
            float percent = (factor - 1f) * 100f;
            if (PercentText.AbsoluteTenths(percent) != 0)
            {
                string text = JournalText.Format("Arcana.Common.ModifierLine", label, PercentText.Format(percent, true));
                if (!string.IsNullOrEmpty(qualifier))
                {
                    text = JournalText.Format("Arcana.Common.WithQualifier", text, qualifier);
                }
                lines.Add(new TooltipModifier(text, percent < 0f));
            }
        }

        internal static AccuracyReading ReadAccuracy(OverviewContext context)
        {
            // Reads the Spell Accuracy row and its per-category breakdown using the casting rules.
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !WitchCastingContext.IsWitch(sim))
            {
                return null;
            }

            WitchCastingRules rules = WitchCastingContext.ReadRules(sim);
            MagicWand wand = WitchCastingContext.WandForAccuracy(sim);
            bool previewMagicHands = wand == null && WitchCastingContext.HasPracticallyMagic(sim);
            float wandBonus = WitchCastingContext.WandBonus(wand, previewMagicHands);
            float needPenalty;
            int empty;
            List<TooltipModifier> needLines = new List<TooltipModifier>();
            WitchCastingContext.CountNeeds(sim, out needPenalty, out empty, needLines);
            float emotions = WitchCastingContext.EmotionPenalty(sim);
            WitchSpell currentRank = new WitchSpell((int)rules.Rank, WitchSpellKind.Spell);
            WitchAccuracy accuracy = rules.AccuracyFor(currentRank, wandBonus, emotions, needPenalty, empty);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            string baseLine = JournalText.Format("Arcana.Common.ModifierLine",
                JournalText.Get("Arcana.Journal.Overview.Accuracy.Base"), PercentText.Format(WitchAccuracy.Base, false));
            string wandName = previewMagicHands || wand is MagicHands
                ? JournalText.Get("Arcana.Journal.Overview.Accuracy.MagicHands")
                : wand != null && accuracy.Wand != 0f ? wand.GetLocalizedName() : null;
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rank"), accuracy.RankBonus);
            AddPercentModifier(lines, string.IsNullOrEmpty(wandName)
                ? JournalText.Get("Arcana.Journal.Overview.Accuracy.Wand") : wandName, accuracy.Wand);
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Mood"), accuracy.Mood);
            if (PercentText.AbsoluteTenths(accuracy.DarkArts) != 0)
            {
                string offset = JournalText.Format("Arcana.Common.ModifierLine",
                    JournalText.Get("Arcana.Witch.Perk.DarkArts.Name"), PercentText.Format(accuracy.DarkArts, true));
                lines.Add(new TooltipModifier(offset, true, false, true));
            }
            lines.AddRange(needLines);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ArcaneAscendancy.Name"), accuracy.Ascendancy);
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Spells"), accuracy.Total);
            AddCategoryModifiers(lines, accuracy, rules.SpecialtyAccuracyBonus(currentRank));
            int tier = (int)rules.Rank;
            WitchSpell charmSpell = new WitchSpell(tier, WitchSpellKind.Charm);
            WitchSpell curseSpell = new WitchSpell(tier, WitchSpellKind.Curse);
            WitchSpell ritualSpell = new WitchSpell(tier, WitchSpellKind.Ritual);
            WitchSpell legendarySpell = new WitchSpell(WitchCastingRules.Legendary, WitchSpellKind.Spell);
            WitchAccuracy charm = rules.AccuracyFor(charmSpell, wandBonus, emotions, needPenalty, empty);
            WitchAccuracy curse = rules.AccuracyFor(curseSpell, wandBonus, emotions, needPenalty, empty);
            WitchAccuracy ritual = rules.AccuracyFor(ritualSpell, wandBonus, emotions, needPenalty, empty);
            WitchAccuracy legendary = rules.AccuracyFor(legendarySpell, wandBonus, emotions, needPenalty, empty);
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Charms"), charm.Total);
            AddCategoryModifiers(lines, charm, rules.SpecialtyAccuracyBonus(charmSpell));
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Curses"), curse.Total);
            AddCategoryModifiers(lines, curse, rules.SpecialtyAccuracyBonus(curseSpell));
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rituals"), ritual.Total);
            AddCategoryModifiers(lines, ritual, rules.SpecialtyAccuracyBonus(ritualSpell));
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Legendary"), legendary.Total);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.TheOldWays.Name"), rules.SpecialtyAccuracyBonus(legendarySpell));
            return new AccuracyReading(accuracy.Total, baseLine, lines.ToArray());
        }

        private static void AddCategory(List<TooltipModifier> lines, string label, float value)
        {
            string text = JournalText.Format("Arcana.Common.ModifierLine", label,
                float.IsNaN(value) ? JournalText.Get("Arcana.Common.UnavailableValue") : PercentText.Format(value, false));
            lines.Add(new TooltipModifier(text, true, true, false, true));
        }

        private static void AddCategoryModifiers(List<TooltipModifier> lines, WitchAccuracy category, float specialty)
        {
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.TheOldWays.Name"), specialty);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ThePowerOfThree.Name"), category.Coven);
            AddLimit(lines, category);
        }

        private static void AddLimit(List<TooltipModifier> lines, WitchAccuracy category)
        {
            // Names the cap that lowered a category; a total raised to 0% shows no line.
            if (PercentText.AbsoluteTenths(category.Limit) == 0 || category.Limit > 0f) return;
            string label;
            if (category.Ceiling < WitchAccuracy.Maximum)
                label = JournalText.Format("Arcana.Journal.Overview.Accuracy.NeedsCap", PercentText.Format(category.Ceiling, false));
            else
                label = JournalText.Format("Arcana.Journal.Overview.Accuracy.Maximum", PercentText.Format(WitchAccuracy.Maximum, false));
            AddPercentModifier(lines, label, category.Limit);
        }

        internal static void AddPercentModifier(List<TooltipModifier> lines, string label, float value)
        {
            if (PercentText.AbsoluteTenths(value) != 0)
            {
                string text = JournalText.Format("Arcana.Common.ModifierLine", label, PercentText.Format(value, true));
                lines.Add(new TooltipModifier(text, value > 0f));
            }
        }

        internal static string NeedName(CommodityKind need)
        {
            switch (need)
            {
                case CommodityKind.Hunger: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Hunger");
                case CommodityKind.VampireThirst: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Thirst");
                case CommodityKind.Bladder: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Bladder");
                case CommodityKind.Energy: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Energy");
                case CommodityKind.Fun: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Fun");
                case CommodityKind.Social: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Social");
                case CommodityKind.Hygiene: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Need.Hygiene");
                default: return JournalText.Get("Arcana.Journal.Overview.Accuracy.Needs");
            }
        }
    }

    internal static class SpellPowerQuote
    {
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo Lunar = typeof(MagicWand).GetField("kMoonPhaseMotiveDrainMultiplier", StaticFields);
        private static readonly FieldInfo Hands = typeof(MagicHands).GetField("kMotiveDrainMultiplier", StaticFields);
        private static readonly FieldInfo Familiar = typeof(MagicWand).GetField("kFamiliarMotiveMultiplier", StaticFields);
        private static readonly Dictionary<string, FieldInfo> Costs = new Dictionary<string, FieldInfo>();

        internal static float For(Sim actor, SpellEntry entry)
        {
            // Quotes an entry's Magical Power cost the way its actual casting path charges it.
            return Quote(actor, entry, SpellKnowledgeGates.UsesArcanaCasting(entry.NativeClass));
        }

        internal static float NativeVariant(Sim actor, SpellEntry entry) { return Quote(actor, entry, false); }

        private static float Quote(Sim actor, SpellEntry entry, bool arcanaCast)
        {
            // Converts native costs to Arcana power and applies wand, moon and familiar multipliers.
            FieldInfo cost;
            if (!Costs.TryGetValue(entry.NativeClass, out cost))
            {
                Type type = typeof(MagicWand).GetNestedType(entry.NativeClass, BindingFlags.Public | BindingFlags.NonPublic);
                cost = type == null ? null : type.GetField("kMotiveDrain", StaticFields);
                Costs[entry.NativeClass] = cost;
            }
            if (cost == null) throw new InvalidOperationException("Missing spell cost: " + entry.NativeClass);
            float result = (float)cost.GetValue(null);
            if (!WitchCastingContext.IsWitch(actor)) return result;
            WitchCastingRules rules = WitchCastingContext.ReadRules(actor);
            if (arcanaCast)
            {
                result *= rules.CostMultiplier(new WitchSpell(entry.Tier, WitchSpell.KindOf(entry.Category)));
            }
            else
            {
                float span = actor.Motives.GetMax(CommodityKind.MagicFatigue) - actor.Motives.GetMin(CommodityKind.MagicFatigue);
                result = span > 0f ? result * rules.Capacity / span : result;
            }
            MagicWand wand = WitchCastingContext.EquippedWand(actor);
            result *= wand != null ? wand.MotiveDrainMultiplier : WitchCastingContext.HasPracticallyMagic(actor)
                && arcanaCast ? (float)Hands.GetValue(null) : 1f;
            float[] lunar = (float[])Lunar.GetValue(null);
            int phase = (int)World.GetLunarPhase();
            if (lunar != null && phase >= 0 && phase < lunar.Length) result *= lunar[phase];
            if (actor.BuffManager != null && actor.BuffManager.HasElement(Sims3.Gameplay.ActorSystems.BuffNames.AnimalFamiliar))
                result *= (float)Familiar.GetValue(null);
            return result;
        }
    }
}
