using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Core;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchBackfires
    {
        internal static bool TryIntercept(Sim caster, TargetSpell spell, WitchCastingRules rules)
        {
            BuffInstance template;
            if (spell.Backfire == BuffNames.Undefined || !BuffManager.BuffDictionary.TryGetValue((ulong)spell.Backfire, out template) ||
                !Accepts(caster, template))
            {
                return false;
            }

            if (rules.Familiar && RandomUtil.RandomChance(WitchCastingRules.FamiliarProtection))
            {
                return true;
            }
            if (rules.ScapegoatChance == 0f || caster.LotCurrent == null)
            {
                return false;
            }

            List<Sim> eligible = new List<Sim>();
            foreach (Sim sim in caster.LotCurrent.GetSims())
            {
                if (WitchCastingContext.Nearby(caster, sim) && Accepts(sim, template) &&
                    !sim.BuffManager.HasElement(spell.Backfire))
                {
                    eligible.Add(sim);
                }
            }
            if (eligible.Count == 0 || !RandomUtil.RandomChance(rules.ScapegoatChance))
            {
                return false;
            }

            eligible.Sort(delegate(Sim a, Sim b)
            {
                return caster.GetDistanceToObjectSquared(a).CompareTo(caster.GetDistanceToObjectSquared(b));
            });
            foreach (Sim sim in eligible)
            {
                if (sim.BuffManager.AddElement(spell.Backfire, Origin.FromSpell))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Accepts(Sim sim, BuffInstance template)
        {
            return sim.SimDescription.IsHuman && sim.SimDescription.TeenOrAbove &&
                !sim.SimDescription.IsDead && !sim.SimDescription.IsGhost &&
                sim.BuffManager != null && (sim.SimDescription.GetCASAGSAvailabilityFlags() & template.mBuff.AvailabilityFlags) != 0 &&
                template.mBuff.ShouldAdd(sim.BuffManager, MoodAxis.None, template.EffectValue);
        }
    }
}
