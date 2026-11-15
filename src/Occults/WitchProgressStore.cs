using System;
using System.Collections.Generic;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    // One Witch's saved progression: claimed milestones, lifetime and spendable Perk Points, and perk ranks by key hash.
    internal sealed class WitchRecord
    {
        private const int Version = 1;
        private const int HeaderSize = 5;

        internal int Claimed;
        internal int Earned;
        internal int Available;
        private readonly Dictionary<uint, int> tiers = new Dictionary<uint, int>();

        private static uint Hash(string key)
        {
            uint hash = 2166136261;
            for (int i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            return hash;
        }

        internal int TierOf(string perkKey)
        {
            int tier;
            return tiers.TryGetValue(Hash(perkKey), out tier) ? tier : 0;
        }

        internal void SetTier(string perkKey, int tier)
        {
            tiers[Hash(perkKey)] = tier;
        }

        internal List<int> Encode()
        {
            List<int> data = new List<int>(HeaderSize + 2 * tiers.Count);
            data.Add(Version);
            data.Add(Claimed);
            data.Add(Earned);
            data.Add(Available);
            data.Add(tiers.Count);
            foreach (KeyValuePair<uint, int> pair in tiers)
            {
                data.Add((int)pair.Key);
                data.Add(pair.Value);
            }
            return data;
        }

        // The claimed milestones are authoritative: lifetime points are recomputed from them and the balance is clamped.
        internal static WitchRecord Decode(List<int> data, out bool newer)
        {
            newer = false;
            if (data == null || data.Count < HeaderSize || data[0] < 1)
            {
                return null;
            }
            if (data[0] > Version)
            {
                newer = true;
                return null;
            }

            WitchRecord record = new WitchRecord();
            record.Claimed = data[1] & WitchProgression.AllMask;
            record.Earned = WitchProgression.PointsIn(record.Claimed);
            record.Available = Math.Max(0, Math.Min(data[3], record.Earned));
            int count = data[4];
            for (int i = 0; i < count && HeaderSize + 2 * i + 1 < data.Count; i++)
            {
                record.tiers[(uint)data[HeaderSize + 2 * i]] = Math.Max(0, data[HeaderSize + 2 * i + 1]);
            }
            return record;
        }
    }

    // Per-Sim records keyed by SimDescriptionId. Only primitives are saved, so a save never depends on a type from this mod.
    internal static class WitchProgressStore
    {
        [PersistableStatic]
        internal static Dictionary<ulong, List<int>> sRecords = new Dictionary<ulong, List<int>>();

        private static Dictionary<ulong, List<int>> Records
        {
            get
            {
                if (sRecords == null)
                {
                    sRecords = new Dictionary<ulong, List<int>>();
                }
                return sRecords;
            }
        }

        private static WitchRecord Load(ulong simId, out bool newer)
        {
            newer = false;
            List<int> data;
            return Records.TryGetValue(simId, out data) ? WitchRecord.Decode(data, out newer) : null;
        }

        private static void Save(ulong simId, WitchRecord record)
        {
            Records[simId] = record.Encode();
        }

        // Awards every milestone the experience has reached and the Sim has not yet claimed; each is awarded once per Sim.
        internal static WitchRecord ClaimMilestones(ulong simId, long xpMilli, long[] thresholds)
        {
            if (simId == 0UL)
            {
                return null;
            }

            bool newer;
            WitchRecord record = Load(simId, out newer);
            if (newer)
            {
                return null;
            }

            bool changed = record == null;
            if (record == null)
            {
                record = new WitchRecord();
            }
            int reached = WitchProgression.Reached(xpMilli, thresholds);
            for (int i = 0; i < reached; i++)
            {
                int bit = 1 << i;
                if ((record.Claimed & bit) == 0)
                {
                    int award = WitchProgression.PointsFor(i);
                    record.Claimed |= bit;
                    record.Earned += award;
                    record.Available += award;
                    changed = true;
                }
            }
            if (changed)
            {
                Save(simId, record);
            }
            return record;
        }

        // Applies new perk ranks and deducts their cost in one step, or changes nothing and returns null.
        internal static WitchRecord Purchase(ulong simId, OccultTree tree, Dictionary<string, int> newTiers)
        {
            bool newer;
            WitchRecord record = Load(simId, out newer);
            if (record == null)
            {
                return null;
            }

            int cost = 0;
            foreach (KeyValuePair<string, int> pair in newTiers)
            {
                Perk perk = tree.Find(pair.Key);
                int owned = perk == null ? 0 : record.TierOf(perk.Key);
                if (perk == null || pair.Value <= owned || pair.Value > perk.Tiers)
                {
                    return null;
                }
                int together;
                if (perk.Conflict != null && (record.TierOf(perk.Conflict.Key) > 0
                    || (newTiers.TryGetValue(perk.Conflict.Key, out together) && together > 0)))
                {
                    return null;
                }
                for (int tier = owned; tier < pair.Value; tier++)
                {
                    cost += perk.Costs[tier];
                }
            }
            if (cost > record.Available)
            {
                return null;
            }

            foreach (KeyValuePair<string, int> pair in newTiers)
            {
                record.SetTier(pair.Key, pair.Value);
            }
            record.Available -= cost;
            Save(simId, record);
            return record;
        }
    }
}
