using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Socializing;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchCastingContext
    {
        internal const float NearbyRadius = 10f;
        private static readonly Dictionary<Type, FieldInfo> nativeWandFields = new Dictionary<Type, FieldInfo>();

        private static readonly CommodityKind[] Needs =
        {
            CommodityKind.Hunger, CommodityKind.VampireThirst, CommodityKind.Bladder,
            CommodityKind.Energy, CommodityKind.Fun, CommodityKind.Social, CommodityKind.Hygiene
        };

        internal static bool IsWitch(Sim sim)
        {
            return sim != null && sim.OccultManager != null && sim.OccultManager.HasOccultType(OccultTypes.Witch);
        }

        internal static WitchCastingRules ReadRules(Sim sim)
        {
            Rank rank;
            WitchRecord record = WitchProgressSource.ReadGameplay(sim.SimDescription, out rank);
            WitchCastingRules rules = new WitchCastingRules(rank);
            if (record == null)
            {
                return rules;
            }

            rules.Lessons = record.TierOf("LessonsInTheUnseenArts") > 0;
            rules.Familiar = record.TierOf("FamiliarBond") > 0 && HasFamiliar(sim);
            rules.Hexweaver = record.TierOf("Hexweaver") > 0;
            rules.DarkArts = record.TierOf("DarkArts") > 0;
            rules.InCoven = record.TierOf("ThePowerOfThree") > 0 && HasCoven(sim);
            rules.Ascendancy = record.TierOf("ArcaneAscendancy") > 0;
            rules.OldWays = !rules.Ascendancy && record.TierOf("TheOldWays") > 0;
            rules.Eidetic = Math.Min(3, record.TierOf("EideticWitchary"));
            rules.Rest = Math.Min(3, record.TierOf("RestForTheWicked"));
            rules.WitchingHour = record.TierOf("TheWitchingHour") > 0;
            rules.Scapegoat = Math.Min(3, record.TierOf("Scapegoat"));
            return rules;
        }

        internal static bool Nearby(Sim caster, Sim other)
        {
            return other != null && caster != other && other.InWorld && !other.HasBeenDestroyed &&
                caster.GetDistanceToObjectSquared(other) <= NearbyRadius * NearbyRadius;
        }

        private static bool HasCoven(Sim sim)
        {
            if (sim.LotCurrent == null)
            {
                return false;
            }

            int count = 0;
            foreach (Sim other in sim.LotCurrent.GetSims())
            {
                if (!Nearby(sim, other) || !IsWitch(other))
                {
                    continue;
                }
                Relationship relationship = Relationship.Get(sim, other, false);
                if (relationship != null && relationship.AreFriends() && ++count == 2)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasFamiliar(Sim sim)
        {
            if (sim.LotCurrent == null || sim.BuffManager == null || !sim.BuffManager.HasElement(BuffNames.AnimalFamiliar))
            {
                return false;
            }
            foreach (Sim other in sim.LotCurrent.GetSims())
            {
                if (Nearby(sim, other) && other.SimDescription.IsCat)
                {
                    Relationship relationship = Relationship.Get(sim, other, false);
                    if (relationship != null && relationship.AreFriends())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static bool HasPracticallyMagic(Sim sim)
        {
            if (!IsWitch(sim))
            {
                return false;
            }
            WitchRecord record = WitchProgressStore.ReadCommitted(sim.SimDescription.SimDescriptionId);
            return record != null && record.TierOf("PracticallyMagic") > 0;
        }

        internal static MagicWand EquippedWand(Sim sim)
        {
            SpellcastingSkill skill = sim.SkillManager == null ? null :
                sim.SkillManager.GetElement(SkillNames.Spellcasting) as SpellcastingSkill;
            return MagicWand.GetWandToUse(sim, skill);
        }

        internal static MagicWand WandForAccuracy(Sim sim)
        {
            InteractionInstance interaction = sim.CurrentInteraction;
            WitchSimCast arcanaCast = interaction as WitchSimCast;
            if (arcanaCast != null && arcanaCast.ActiveWand != null)
            {
                return arcanaCast.ActiveWand;
            }
            if (interaction != null && arcanaCast == null)
            {
                Type type = interaction.GetType();
                FieldInfo field;
                if (!nativeWandFields.TryGetValue(type, out field))
                {
                    for (Type owner = type; owner != null; owner = owner.BaseType)
                    {
                        if (owner.DeclaringType == typeof(MagicWand))
                        {
                            field = owner.GetField("mWand", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                            if (field != null && field.FieldType == typeof(MagicWand))
                            {
                                break;
                            }
                            field = null;
                        }
                    }
                    nativeWandFields.Add(type, field);
                }
                if (field != null)
                {
                    MagicWand wand = field.GetValue(interaction) as MagicWand;
                    if (wand != null)
                    {
                        return wand;
                    }
                }
            }
            return EquippedWand(sim);
        }

        internal static float WandBonus(MagicWand wand, bool previewMagicHands)
        {
            if (wand is MagicHands || (wand == null && previewMagicHands))
            {
                return WitchCastingRules.MagicHandsAccuracyBonus;
            }
            if (wand is ElegantWand) return 25f;
            if (wand is IvoryWand || wand is IridescentWand) return 20f;
            if (wand is VerdantWand || wand is CrystalWand) return 15f;
            if (wand is ArgentWand || wand is AzureWand || wand is CrimsonWand) return 10f;
            return wand is DefaultWand ? 5f : 0f;
        }

        internal static float EmotionPenalty(Sim sim)
        {
            float penalty = 0f;
            if (sim.BuffManager == null)
            {
                return penalty;
            }

            Motives motives = sim.Motives;
            foreach (BuffInstance instance in sim.BuffManager.Buffs)
            {
                if (instance == null || instance.mBuff == null || instance.EffectValue >= 0 ||
                    IsNeedMoodlet(instance, motives))
                {
                    continue;
                }
                penalty -= instance.EffectValue;
                if (penalty >= WitchCastingRules.MaxEmotionPenalty)
                {
                    return WitchCastingRules.MaxEmotionPenalty;
                }
            }
            return penalty;
        }

        private static bool IsNeedMoodlet(BuffInstance buff, Motives motives)
        {
            foreach (CommodityKind need in Needs)
            {
                if (buff.mBuff.SolveCommodity == need)
                {
                    return true;
                }
                Motive motive = motives == null ? null : motives.GetMotive(need);
                if (motive == null || motive.Tuning == null || motive.Tuning.BuffTriggers == null)
                {
                    continue;
                }
                // Some Need moodlets declare their source in the motive's tuning instead of SolveCommodity.
                foreach (MotiveTuning.MotiveBuffTrigger trigger in motive.Tuning.BuffTriggers)
                {
                    if (trigger.mAddBuff == buff.mBuffGuid)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static void CountNeeds(Sim sim, out float penalty, out int empty, List<TooltipModifier> details)
        {
            penalty = 0f;
            empty = 0;
            Motives motives = sim.Motives;
            if (motives == null)
            {
                return;
            }

            bool thirst = motives.GetMotive(CommodityKind.VampireThirst) != null;
            foreach (CommodityKind need in Needs)
            {
                if ((thirst && need == CommodityKind.Hunger) || motives.GetMotive(need) == null)
                {
                    continue;
                }

                float min = motives.GetMin(need);
                float span = motives.GetMax(need) - min;
                if (span <= 0f)
                {
                    continue;
                }

                float aboveMinimum = motives.GetValue(need) - min;
                float amount = WitchCastingRules.NeedPenalty(aboveMinimum / span);
                penalty += amount;
                if (aboveMinimum <= 0f)
                {
                    empty++;
                }
                if (amount > 0f && details != null)
                {
                    AddPercentModifier(details, NeedName(need), -amount);
                }
            }
        }

        private static string NeedName(CommodityKind need)
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

        internal static WitchCastingRules ReadCapacityRules(Sim sim)
        {
            Rank rank;
            WitchRecord record = WitchProgressSource.ReadGameplay(sim.SimDescription, out rank);
            WitchCastingRules rules = new WitchCastingRules(rank);
            rules.Ascendancy = record != null && record.TierOf("ArcaneAscendancy") > 0;
            return rules;
        }

        internal static OverviewTip PowerTooltip(string title, WitchCastingRules rules)
        {
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
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !IsWitch(sim) || sim.BuffManager == null || sim.Motives == null)
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

        // The row is the ordinary spell discount range across tiers I through current rank.
        // Tooltip totals use the same Arcana factors as DrainMotives, before native wand/moon effects.
        internal static ModifierReading ReadSpellCost(OverviewContext context)
        {
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !IsWitch(sim))
            {
                return ModifierReading.None;
            }

            WitchCastingRules rules = ReadRules(sim);
            int tier = (int)rules.Rank;
            WitchSpell current = new WitchSpell(tier, WitchSpellKind.Spell);
            WitchCostFactors ordinary = rules.CostFactors(current);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            AddCostFactor(lines, JournalText.Get("Arcana.Journal.Overview.Cost.HighWitch"), ordinary.HighWitch);
            AddCostFactor(lines, JournalText.Get("Arcana.Witch.Perk.FamiliarBond.Name"), ordinary.Familiar);

            AddCostCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Spells"), rules, current);
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

            // Preview the tier-dependent factors for this category using the same
            // gameplay rules. Legendary magic is not a lower-tier spell.
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
                // Rank indices are zero based: a High Witch (4) discounts Tiers I–IV.
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

        // Merge contiguous tiers with equal displayed costs; no artificial tier averages.
        private static void AddCostTierGroups(List<TooltipModifier> lines, WitchCastingRules rules, WitchSpellKind kind)
        {
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
                // Lower costs are beneficial (unlike lower Accuracy).
                lines.Add(new TooltipModifier(text, percent < 0f));
            }
        }

        internal static AccuracyReading ReadAccuracy(OverviewContext context)
        {
            Sim sim = context.Identity.Description == null ? null : context.Identity.Description.CreatedSim;
            if (!WitchCastingLifecycle.CastingInstalled || !IsWitch(sim))
            {
                return null;
            }

            WitchCastingRules rules = ReadRules(sim);
            MagicWand wand = WandForAccuracy(sim);
            bool previewMagicHands = wand == null && HasPracticallyMagic(sim);
            float wandBonus = WandBonus(wand, previewMagicHands);
            float needPenalty;
            int empty;
            List<TooltipModifier> needLines = new List<TooltipModifier>();
            CountNeeds(sim, out needPenalty, out empty, needLines);
            float emotions = EmotionPenalty(sim);
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
            // Dark Arts offsets the Mood penalty, rather than granting a standalone Accuracy bonus.
            if (PercentText.AbsoluteTenths(accuracy.DarkArts) != 0)
            {
                string offset = JournalText.Format("Arcana.Common.ModifierLine",
                    JournalText.Get("Arcana.Witch.Perk.DarkArts.Name"), PercentText.Format(accuracy.DarkArts, true));
                lines.Add(new TooltipModifier(offset, true, false, true));
            }
            lines.AddRange(needLines);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ArcaneAscendancy.Name"), accuracy.Ascendancy);
            // All category totals include the shared modifiers. Coven and bounds are category-specific.
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Spells"), accuracy.Total);
            AddCategoryModifiers(lines, accuracy, rules.SpecialtyAccuracyBonus(currentRank));
            int tier = (int)rules.Rank;
            WitchSpell curseSpell = new WitchSpell(tier, WitchSpellKind.Curse);
            WitchSpell ritualSpell = new WitchSpell(tier, WitchSpellKind.Ritual);
            WitchSpell legendarySpell = new WitchSpell(WitchCastingRules.Legendary, WitchSpellKind.Spell);
            WitchAccuracy curse = rules.AccuracyFor(curseSpell, wandBonus, emotions, needPenalty, empty);
            WitchAccuracy ritual = rules.AccuracyFor(ritualSpell, wandBonus, emotions, needPenalty, empty);
            WitchAccuracy legendary = rules.AccuracyFor(legendarySpell, wandBonus, emotions, needPenalty, empty);
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Curses"), curse.Total);
            AddCategoryModifiers(lines, curse, rules.SpecialtyAccuracyBonus(curseSpell));
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rituals"), ritual.Total);
            AddCategoryModifiers(lines, ritual, rules.SpecialtyAccuracyBonus(ritualSpell));
            AddCategory(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Legendary"), legendary.Total);
            // The capstone's configured bonus is known, but Legendary's separate total is not.
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
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Limit"), category.Limit);
        }

        private static void AddPercentModifier(List<TooltipModifier> lines, string label, float value)
        {
            if (PercentText.AbsoluteTenths(value) != 0)
            {
                string text = JournalText.Format("Arcana.Common.ModifierLine", label, PercentText.Format(value, true));
                lines.Add(new TooltipModifier(text, value > 0f));
            }
        }
    }
}
