using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay;
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
            // Reads the Witch's rank and purchased perks into the casting rules.
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
            // Returns whether two friendly Witches are nearby for The Power of Three.
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
            // Returns whether a friendly cat familiar is nearby for Familiar Bond.
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
            // Returns the wand the current cast uses, falling back to the equipped wand.
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
            // Returns the accuracy bonus of a wand or Magic Hands.
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
            // Sums negative moodlets not caused by needs, capped at the maximum emotion penalty.
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
            // Returns whether a moodlet comes from a need.
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
            // Totals the low-need accuracy penalty and counts empty needs, optionally listing each need.
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
                    WitchAttributes.AddPercentModifier(details, WitchAttributes.NeedName(need), -amount);
                }
            }
        }
        internal static WitchCastingRules ReadCapacityRules(Sim sim)
        {
            // Reads only the rank and Arcane Ascendancy, which decide power capacity and study speed.
            Rank rank;
            WitchRecord record = WitchProgressSource.ReadGameplay(sim.SimDescription, out rank);
            WitchCastingRules rules = new WitchCastingRules(rank);
            rules.Ascendancy = record != null && record.TierOf("ArcaneAscendancy") > 0;
            return rules;
        }
    }
}
