using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;

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
                    "I", 0, SpellCatalog.ConjureAppleCost, JournalText.Get(evil
                        ? "Arcana.Ability.ConjurePoisonedApple.Description" : "Arcana.Ability.ConjureApple.Description")));
            }
            return entries;
        }
    }
}
