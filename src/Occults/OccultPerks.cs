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
        // The perk that cannot be taken together with this one (set on both perks).
        internal Perk Conflict;
        internal int Index;

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

        // Previews the rank a click would buy, stopping at the last description; tier counts owned plus staged ranks.
        internal string Describe(int tier, string simName)
        {
            int shown = Math.Min(Math.Max(tier, 0), Descriptions.Length - 1);
            return Descriptions[shown].Replace("{name}", simName);
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

        // The short rank name for narrow labels; the top rank keeps its full title, since "High" alone says nothing.
        internal string RankLabel(int rank)
        {
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

        // Must match Tree.id in tools/trees.py.
        internal uint Id(PageControl kind, int index)
        {
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
        Icon = 8
    }

    internal static class WitchTree
    {
        internal static readonly OccultTree Tree = new OccultTree(
            JournalText.Get("Arcana.LifeState.Name.Witch"), JournalText.Get("Arcana.Journal.Perks.Points"),
            JournalText.Get("Arcana.Journal.Perks.PointsHelp"),
            new[]
            {
                JournalText.Get("Arcana.Witch.Rank.Short.Novice"),
                JournalText.Get("Arcana.Witch.Rank.Short.Apprentice"),
                JournalText.Get("Arcana.Witch.Rank.Short.Adept"),
                JournalText.Get("Arcana.Witch.Rank.Short.Master"),
                JournalText.Get("Arcana.Witch.Rank.Short.HighWitch"),
            },
            new[]
            {
                JournalText.Get("Arcana.Witch.Rank.Full.Novice"),
                JournalText.Get("Arcana.Witch.Rank.Full.Apprentice"),
                JournalText.Get("Arcana.Witch.Rank.Full.Adept"),
                JournalText.Get("Arcana.Witch.Rank.Full.Master"),
                JournalText.Get("Arcana.Witch.Rank.Full.HighWitch"),
            },
            CreatePerks());

        private static Perk[] CreatePerks()
        {
            Perk[] perks = new Perk[]
            {
                Power("RestForTheWicked", Rank.Novice, 0, 0.5f, JournalText.Get("Arcana.Witch.Perk.RestForTheWicked.Name"), 1, 1, 2),
                Power("LessonsInTheUnseenArts", Rank.Novice, 1, 0.5f, JournalText.Get("Arcana.Witch.Perk.LessonsInTheUnseenArts.Name"), 2),
                Power("TravellingScholar", Rank.Novice, 2, 0.5f, JournalText.Get("Arcana.Witch.Perk.TravellingScholar.Name"), 1),

                Power("SorcerersApprentice", Rank.Apprentice, 0, 0f, JournalText.Get("Arcana.Witch.Perk.SorcerersApprentice.Name"), 2),
                Power("AlchemicalSavant", Rank.Apprentice, 1, 0f, JournalText.Get("Arcana.Witch.Perk.AlchemicalSavant.Name"), 3),
                Power("FamiliarBond", Rank.Apprentice, 2, 0f, JournalText.Get("Arcana.Witch.Perk.FamiliarBond.Name"), 2),
                Power("Hexweaver", Rank.Apprentice, 3, 0f, JournalText.Get("Arcana.Witch.Perk.Hexweaver.Name"), 3),

                Power("TrialByFire", Rank.Adept, 0, 0f, JournalText.Get("Arcana.Witch.Perk.TrialByFire.Name"), 3),
                Power("TheWitchingHour", Rank.Adept, 1, 0f, JournalText.Get("Arcana.Witch.Perk.TheWitchingHour.Name"), 3),
                Power("ThePowerOfThree", Rank.Adept, 2, 0f, JournalText.Get("Arcana.Witch.Perk.ThePowerOfThree.Name"), 3),
                Power("DarkArts", Rank.Adept, 3, 0f, JournalText.Get("Arcana.Witch.Perk.DarkArts.Name"), 4),

                Power("PracticallyMagic", Rank.Master, 0, 0f, JournalText.Get("Arcana.Witch.Perk.PracticallyMagic.Name"), 3),
                Power("EideticWitchary", Rank.Master, 1, 0f, JournalText.Get("Arcana.Witch.Perk.EideticWitchary.Name"), 3, 4, 5),
                Power("PowerShunt", Rank.Master, 2, 0f, JournalText.Get("Arcana.Witch.Perk.PowerShunt.Name"), 4),
                // Ranks 1-3 redirect backlash 25% / 40% / 55% of the time; the tooltip does not state them.
                Power("Scapegoat", Rank.Master, 3, 0f, JournalText.Get("Arcana.Witch.Perk.Scapegoat.Name"), 3, 4, 5),

                Power("ArcaneAscendancy", Rank.HighWitch, 0, 0f, JournalText.Get("Arcana.Witch.Perk.ArcaneAscendancy.Name"), 6),
                Power("TheOldWays", Rank.HighWitch, 3, 0f, JournalText.Get("Arcana.Witch.Perk.TheOldWays.Name"), 6)
            };

            // High Witch: choose only one; a staged choice can be cancelled to pick the other, an acquired one is final.
            perks[15].Conflict = perks[16];
            perks[16].Conflict = perks[15];
            return perks;
        }

        // One cost per upgrade rank, in order; the number of costs is the number of ranks.
        private static Perk Power(string key, Rank rank, int column, float shift, string name, params int[] costs)
        {
            return new Perk(key, rank, column, shift, name, costs);
        }
    }
}
