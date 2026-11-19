using System;
using System.Collections.Generic;

namespace SupernaturalExpanded.Journal
{
    internal enum SpellLearning { Study, Starting, NativeDisabled }

    internal enum SpellCategory { Spell, Charm, Curse, Ritual, Ability }

    internal sealed class SpellEntry
    {
        internal readonly string Id, NativeClass;
        internal readonly int Tier, MotiveIndex;
        internal readonly SpellCategory Category;
        internal readonly SpellLearning Learning;

        internal SpellEntry(string id, int tier, SpellCategory category, SpellLearning learning, string nativeClass, int motiveIndex)
        {
            Id = id; Tier = tier; Category = category; Learning = learning; NativeClass = nativeClass; MotiveIndex = motiveIndex;
        }
    }

    internal static class SpellCatalog
    {
        internal const int LegendaryTier = 5;
        internal static readonly SpellEntry[] Entries = Create();
        private static readonly Dictionary<string, SpellEntry> ById = Index(Entries);

        private static SpellEntry[] Create()
        {
            // Builds the species-neutral spell registry with stable knowledge IDs, tiers, categories and learning policies.
            List<SpellEntry> entries = new List<SpellEntry>();
            Add(entries, "good_luck", 0, SpellCategory.Charm, SpellLearning.Study, "CastGoodLuckCharm");
            Add(entries, "love", 2, SpellCategory.Charm, SpellLearning.Study, "CastLoveCharm");
            Add(entries, "sunlight", 3, SpellCategory.Charm, SpellLearning.Study, "CastSunlightCharm");
            Add(entries, "pestilence", 3, SpellCategory.Curse, SpellLearning.Study, "CastPestilenceCurse");
            Add(entries, "fire_blast", 0, SpellCategory.Spell, SpellLearning.Starting, "CastFireBlast");
            Add(entries, "ice_blast", 0, SpellCategory.Spell, SpellLearning.Starting, "CastIceBlast");
            Add(entries, "toadification", 2, SpellCategory.Curse, SpellLearning.Study, "CastToadify");
            Add(entries, "haunting", 2, SpellCategory.Curse, SpellLearning.Study, "CastHauntingCurse");
            Add(entries, "upgrade", 1, SpellCategory.Spell, SpellLearning.Study, "MagicallyUpgrade");
            Add(entries, "conversion", 1, SpellCategory.Ritual, SpellLearning.Study, "CastConvert");
            Add(entries, "restoration", 4, SpellCategory.Ritual, SpellLearning.Study, "CastRestoration");
            Add(entries, "reanimation", 4, SpellCategory.Ritual, SpellLearning.Study, "CastReanimate");
            string[] motives = { "bladder", "hunger", "energy", "hygiene", "fun", "social" };
            for (int i = 0; i < motives.Length; i++)
            {
                SpellLearning learning = i == 0 || i == 1 || i == 3 ? SpellLearning.Study : SpellLearning.NativeDisabled;
                string motive = motives[i];
                entries.Add(new SpellEntry("charm_" + motive, 2, SpellCategory.Charm, learning, "CastMotiveCharm", i));
                entries.Add(new SpellEntry("curse_" + motive, 3, SpellCategory.Curse, learning, "CastMotiveCurse", i));
            }
            return entries.ToArray();
        }

        private static void Add(List<SpellEntry> entries, string id, int tier, SpellCategory category, SpellLearning learning, string nativeClass)
        {
            entries.Add(new SpellEntry(id, tier, category, learning, nativeClass, -1));
        }

        internal static SpellEntry Find(string id)
        {
            // Returns the catalog entry with a stable knowledge ID, or null.
            SpellEntry entry;
            return id != null && ById.TryGetValue(id, out entry) ? entry : null;
        }

        private static Dictionary<string, SpellEntry> Index(SpellEntry[] entries)
        {
            Dictionary<string, SpellEntry> result = new Dictionary<string, SpellEntry>(StringComparer.Ordinal);
            foreach (SpellEntry entry in entries) result.Add(entry.Id, entry);
            return result;
        }

        internal static bool CheatUnlockable(SpellEntry entry)
        {
            // Returns whether the unlock cheat may teach an entry, excluding cut content and Legendary magic.
            return entry != null && entry.Learning != SpellLearning.NativeDisabled && entry.Tier >= 0
                && entry.Tier < LegendaryTier;
        }

        internal static bool Discoverable(SpellEntry entry, int highestTier, bool known)
        {
            // Returns whether Study Spells may discover an unknown entry at the given highest tier.
            return entry != null && entry.Learning == SpellLearning.Study && !known && entry.Tier >= 0
                && entry.Tier < LegendaryTier && entry.Tier <= highestTier;
        }
    }
}
