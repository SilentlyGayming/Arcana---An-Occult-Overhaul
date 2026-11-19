using System;

namespace SupernaturalExpanded.Journal
{
    internal enum Rank
    {
        Novice,
        Apprentice,
        Adept,
        Master,
        HighWitch
    }

    internal sealed class Perk
    {
        internal readonly string Key;
        internal readonly Rank Rank;
        internal readonly int Column;
        internal readonly float Shift;
        internal readonly string Name;
        internal readonly int[] Costs;
        internal string Tagline;
        internal string[] Descriptions;
        internal Perk Conflict;
        internal int Index;

        internal uint Face;

        internal Perk(string key, Rank rank, int column, float shift, string name, int[] costs)
        {
            Key = key;
            Rank = rank;
            Column = column;
            Shift = shift;
            Name = name;
            Costs = costs;
        }

        internal int Tiers { get { return Costs.Length; } }

        internal string Describe(int tier, string simName)
        {
            // Returns the description of the rank a click would buy.
            int shown = PreviewIndex(tier);
            return Descriptions[shown].Replace("{name}", simName);
        }

        internal int PreviewIndex(int tier)
        {
            return Math.Min(Math.Max(tier, 0), Descriptions.Length - 1);
        }
    }

    internal sealed class OccultTree
    {
        private const uint IdBase = 0x53E12000;

        internal readonly string Name;
        internal readonly string PointsName;
        internal readonly string PointsHelp;
        internal readonly string[] RankNames;
        private readonly string[] rankTitles;
        internal readonly Perk[] Perks;

        internal OccultTree(string name, string pointsName, string pointsHelp, string[] rankNames, Perk[] perks)
            : this(name, pointsName, pointsHelp, rankNames, null, perks)
        {
        }

        internal OccultTree(string name, string pointsName, string pointsHelp, string[] rankNames, string[] rankTitles, Perk[] perks)
        {
            Name = name;
            PointsName = pointsName;
            PointsHelp = pointsHelp;
            RankNames = rankNames;
            this.rankTitles = rankTitles;
            Perks = perks;
            for (int i = 0; i < perks.Length; i++)
            {
                perks[i].Index = i;
                string[] entry = WitchText.Entry(perks[i].Key) ?? new[] { "", "" };
                perks[i].Tagline = entry[0];
                perks[i].Descriptions = new string[entry.Length - 1];
                Array.Copy(entry, 1, perks[i].Descriptions, 0, entry.Length - 1);
            }
        }

        internal string RankTitle(int rank)
        {
            int index = ClampRank(rank);
            return rankTitles == null
                ? JournalText.Format("Arcana.Common.RankTitle", RankNames[index], Name) : rankTitles[index];
        }

        private int ClampRank(int rank)
        {
            return rank < 0 ? 0 : (rank >= RankNames.Length ? RankNames.Length - 1 : rank);
        }

        internal string RankLabel(int rank)
        {
            // Returns the short rank name for narrow labels.
            rank = ClampRank(rank);
            return rank == RankNames.Length - 1 ? RankTitle(rank) : RankNames[rank];
        }

        internal Perk Find(string key)
        {
            foreach (Perk perk in Perks)
            {
                if (perk.Key == key)
                {
                    return perk;
                }
            }
            return null;
        }

        internal uint Id(PageControl kind, int index)
        {
            // Returns a control ID that matches Tree.id in tools/trees.py.
            return IdBase + (uint)kind * 0x100 + (uint)index;
        }
    }

    internal enum PageControl
    {
        Tile = 0,
        Counter = 1,
        CounterPlate = 2,
        Cost = 3,
        CostPlate = 4,
        RankMarker = 5,
        Page = 6,
        CostIcon = 7,
        Icon = 8,
        Edge = 9
    }
}
