using System.Collections.Generic;
using Sims3.Gameplay.Actors;

namespace SupernaturalExpanded.Journal
{
    internal static class InnateAbilities
    {
        private static readonly string[][] Fairy =
        {
            new string[] { "fairy_aura", "Arcana.Ability.fairy_aura.Name", "Arcana.Ability.fairy_aura.Description" },
            new string[] { "fairy_trick", "Arcana.Ability.fairy_trick.Name", "Arcana.Ability.fairy_trick.Description" },
            new string[] { "fairy_boon", "Arcana.Ability.fairy_boon.Name", "Arcana.Ability.fairy_boon.Description" },
            new string[] { "fairy_trap", "Arcana.Ability.fairy_trap.Name", "Arcana.Ability.fairy_trap.Description" },
            new string[] { "fairy_repair", "Arcana.Ability.fairy_repair.Name", "Arcana.Ability.fairy_repair.Description" }
        };
        private static readonly string[][] Genie =
        {
            new string[] { "genie_meal", "Arcana.Ability.genie_meal.Name", "Arcana.Ability.genie_meal.Description" },
            new string[] { "genie_clean_sim", "Arcana.Ability.genie_clean_sim.Name", "Arcana.Ability.genie_clean_sim.Description" },
            new string[] { "genie_enchant_home", "Arcana.Ability.genie_enchant_home.Name", "Arcana.Ability.genie_enchant_home.Description" },
            new string[] { "genie_wish", "Arcana.Ability.genie_wish.Name", "Arcana.Ability.genie_wish.Description" },
            new string[] { "genie_lamp", "Arcana.Ability.genie_lamp.Name", "Arcana.Ability.genie_lamp.Description" }
        };

        internal static List<GrimoireEntry> Entries(SimIdentity identity, SpellPowerQuote quote)
        {
            // Lists the caster's innate abilities: Conjure Apple for Witches, native powers for Fairies and Genies.
            Sim actor = identity == null || identity.Description == null ? null : identity.Description.CreatedSim;
            CasterKind kind;
            if (!MagicCasters.TryKind(actor, out kind)) return new List<GrimoireEntry>();
            if (kind == CasterKind.Witch) return WitchAbilities.Entries(identity);
            List<GrimoireEntry> entries = new List<GrimoireEntry>();
            foreach (string[] ability in kind == CasterKind.Fairy ? Fairy : Genie)
            {
                entries.Add(new GrimoireEntry(ability[0], JournalText.Get(ability[1]), "", 0, float.NaN, JournalText.Get(ability[2])));
            }
            return entries;
        }
    }
}
