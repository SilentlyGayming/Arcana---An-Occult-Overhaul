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

        internal static void CountNeeds(Sim sim, out int low, out int empty)
        {
            low = 0;
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
                if (aboveMinimum <= span * WitchCastingRules.LowNeedFraction)
                {
                    low++;
                    if (aboveMinimum <= 0f)
                    {
                        empty++;
                    }
                }
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
            int low;
            int empty;
            CountNeeds(sim, out low, out empty);
            float emotions = EmotionPenalty(sim);
            WitchSpell currentRank = new WitchSpell((int)rules.Rank, WitchSpellKind.Spell);
            WitchAccuracy accuracy = rules.AccuracyFor(currentRank, wandBonus, emotions, low, empty);
            List<TooltipModifier> lines = new List<TooltipModifier>();
            string baseLine = JournalText.Format("Arcana.Journal.Overview.Accuracy.Line",
                JournalText.Get("Arcana.Journal.Overview.Accuracy.Base"), PercentText.Format(WitchAccuracy.Base, false));
            string wandName = previewMagicHands || wand is MagicHands
                ? JournalText.Get("Arcana.Journal.Overview.Accuracy.MagicHands")
                : wand != null && accuracy.Wand != 0f ? wand.GetLocalizedName() : null;
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Rank"), accuracy.RankBonus);
            AddPercentModifier(lines, string.IsNullOrEmpty(wandName)
                ? JournalText.Get("Arcana.Journal.Overview.Accuracy.Wand") : wandName, accuracy.Wand);
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Mood"), accuracy.Mood);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.DarkArts.Name"), accuracy.DarkArts);
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Needs"), accuracy.Needs);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ArcaneAscendancy.Name"), accuracy.Ascendancy);
            AddPercentModifier(lines, JournalText.Get("Arcana.Witch.Perk.ThePowerOfThree.Name"), accuracy.Coven);
            AddPercentModifier(lines, JournalText.Get("Arcana.Journal.Overview.Accuracy.Limit"), accuracy.Limit);
            return new AccuracyReading(accuracy.Total, baseLine, lines.ToArray());
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
