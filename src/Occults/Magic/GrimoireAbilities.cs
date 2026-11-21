using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
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
        private const int WishTier = 4;
        private static readonly string[] GenieLampRow = { "genie_lamp", "Arcana.Ability.genie_lamp.Name", "Arcana.Ability.genie_lamp.Description" };
        private static readonly string[] RetreatRow = { "genie_retreat", "Arcana.Ability.genie_retreat.Name", "Arcana.Ability.genie_retreat.Description" };
        private static readonly string[] DesireRow = { "genie_hearts_desire", "Arcana.Ability.genie_hearts_desire.Name", "Arcana.Ability.genie_hearts_desire.Description" };
        private static readonly string[] FavorRow = { "genie_favor", "Arcana.Ability.genie_favor.Name", "Arcana.Ability.genie_favor.Description" };
        private static readonly string[] OwnWishRow = { "genie_wish_own", "Arcana.Ability.genie_wish_own.Name", "Arcana.Ability.genie_wish_own.Description" };

        internal static List<GrimoireEntry> Entries(SimIdentity identity, SpellPowerQuote quote)
        {
            Sim actor = identity == null || identity.Description == null ? null : identity.Description.CreatedSim;
            CasterKind kind;
            if (!MagicCasters.TryKind(actor, out kind)) return new List<GrimoireEntry>();
            if (kind == CasterKind.Witch) return WitchAbilities.Entries(identity);
            List<GrimoireEntry> entries = new List<GrimoireEntry>();
            if (kind == CasterKind.Genie)
            {
                CastingRules rules = MagicCasters.ReadRules(actor);
                foreach (GenieAbility ability in GenieAbilities.All)
                {
                    if (rules.Rank < ability.Rank || ability.Id == "genie_release") continue;
                    string tier = ability.CanFail ? OverviewProviders.GetRomanNumeral(ability.Tier + 1) : "";
                    entries.Add(new GrimoireEntry(ability.Id, JournalText.Get(ability.NameKey), tier, ability.Tier,
                        ability.CostFor(rules), JournalText.Get(ability.DescriptionKey)));
                }
                entries.Add(Entry(GenieLampRow, 0f, 0));
                AddPerkAbilities(entries, actor, rules);
                return entries;
            }
            foreach (string[] ability in Fairy) entries.Add(Entry(ability, float.NaN));
            return entries;
        }

        private static void AddPerkAbilities(List<GrimoireEntry> entries, Sim actor, CastingRules rules)
        {
            if (rules.Lampbound && GeniePerks.Owns(actor.SimDescription, "PocketPalace")) entries.Add(Entry(RetreatRow, float.NaN));
            if (GeniePerks.Owns(actor.SimDescription, "HeartsDesire"))
            {
                entries.Add(Entry(DesireRow, float.NaN));
                entries.Add(Entry(FavorRow, CastingRules.FavorCost));
            }
            if (rules.GenieLaw && rules.Rank >= Rank.HighWitch)
            {
                foreach (WishDefinition wish in WishCatalog.All)
                {
                    if (wish.LampOnly || !Sims3.SimIFace.GameUtils.IsInstalled(wish.Product)) continue;
                    bool evil = actor.HasTrait(TraitNames.Evil);
                    entries.Add(new GrimoireEntry("genie_wish_" + wish.Kind.ToString(), GenieWishDialog.DisplayName(wish, evil, actor.IsFemale), "V", WishTier,
                        GenieWishes.Cost(WishMode.Other, rules), GenieWishDialog.Description(wish.Kind, evil, actor.IsFemale)));
                }
            }
            if (rules.Counterattack) entries.Add(Entry(OwnWishRow, GenieWishes.Cost(WishMode.Own), WishTier));
        }

        private static GrimoireEntry Entry(string[] row, float cost)
        {
            return new GrimoireEntry(row[0], JournalText.Get(row[1]), "", 0, cost, JournalText.Get(row[2]));
        }

        private static GrimoireEntry Entry(string[] row, float cost, int tier)
        {
            return new GrimoireEntry(row[0], JournalText.Get(row[1]), OverviewProviders.GetRomanNumeral(tier + 1), tier, cost, JournalText.Get(row[2]));
        }
    }

    internal static class WitchAbilities
    {
        internal const string AppleId = "conjure_apple";

        internal static List<GrimoireEntry> Entries(SimIdentity identity)
        {
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
