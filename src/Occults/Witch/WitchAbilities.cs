using System;
using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchAbilities
    {
        internal const string AppleId = "conjure_apple";

        internal static List<GrimoireEntry> Entries(SimIdentity identity)
        {
            // Lists the Witch's innate abilities in the Abilities tab.
            List<GrimoireEntry> entries = new List<GrimoireEntry>();
            Sim actor = identity == null || identity.Description == null ? null : identity.Description.CreatedSim;
            if (WitchCastingContext.IsWitch(actor))
            {
                bool evil = actor.TraitManager != null && actor.TraitManager.HasElement(TraitNames.Evil);
                entries.Add(new GrimoireEntry(AppleId,
                    JournalText.Get(evil ? "Arcana.Ability.ConjurePoisonedApple.Name" : "Arcana.Ability.ConjureApple.Name"),
                    "I", 0, AppleCost(actor), JournalText.Get(evil
                        ? "Arcana.Ability.ConjurePoisonedApple.Description" : "Arcana.Ability.ConjureApple.Description")));
            }
            return entries;
        }

        private static float AppleCost(Sim actor)
        {
            // Reads Conjure Apple's power cost from its native commodity output, which wand and perk discounts do not affect.
            InteractionObjectPair pair = new InteractionObjectPair((InteractionDefinition)TraitFunctions.ConjureApple.Singleton, actor);
            if (pair.Tuning == null || pair.Tuning.mTradeoff == null)
                throw new InvalidOperationException("Conjure Apple power tuning is unavailable.");
            foreach (CommodityChange output in pair.Tuning.mTradeoff.mOutputs)
            {
                if (output.Commodity != CommodityKind.MagicFatigue) continue;
                float span = actor.Motives.GetMax(CommodityKind.MagicFatigue) - actor.Motives.GetMin(CommodityKind.MagicFatigue);
                return span > 0f ? Math.Abs(output.mActualValue) * WitchCastingContext.ReadCapacityRules(actor).Capacity / span : 0f;
            }
            throw new InvalidOperationException("Conjure Apple has no native power output.");
        }
    }
}
