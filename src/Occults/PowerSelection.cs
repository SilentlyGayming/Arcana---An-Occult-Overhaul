using System;
using System.Collections.Generic;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal sealed class PowerSelection
    {
        private enum Block
        {
            None,
            Unavailable,
            Rank,
            Complete,
            Conflict,
            Points
        }

        private readonly OccultTree tree;
        private readonly int[] acquired;
        private readonly int[] staged;
        private readonly ulong simId;
        private int powerPoints;
        private int earned;
        private int milestones;

        internal Rank Rank;
        internal float RankProgress;
        internal float Proficiency { get; private set; }
        internal bool Available;
        internal long Experience;

        internal string SimName = JournalText.Get("Arcana.Common.ThisSim");

        internal PowerSelection(ulong simId, PowerProgress progress, OccultTree tree)
        {
            this.tree = tree;
            this.simId = simId;
            acquired = new int[tree.Perks.Length];
            staged = new int[tree.Perks.Length];
            Apply(progress);
        }

        internal ulong SimDescriptionId { get { return simId; } }
        internal int Milestones { get { return milestones; } }

        internal bool Update(PowerProgress progress)
        {
            // Takes in newly read progression, keeping staged picks, and reports whether anything shown changed.
            if (progress.Available && progress.SimDescriptionId != simId)
            {
                return false;
            }

            bool changed = Available != progress.Available || Rank != progress.Rank || powerPoints != progress.PowerPoints
                || earned != progress.Earned || milestones != progress.Milestones || Proficiency != progress.Proficiency
                || (int)(RankProgress * 200f) != (int)(progress.RankProgress * 200f);
            Apply(progress);
            return changed;
        }

        private void Apply(PowerProgress progress)
        {
            Available = progress.Available;
            Experience = progress.Experience;
            Rank = progress.Rank;
            RankProgress = progress.RankProgress;
            Proficiency = progress.Proficiency;
            powerPoints = progress.PowerPoints;
            earned = progress.Earned;
            milestones = progress.Milestones;
            foreach (Perk perk in tree.Perks)
            {
                int tier;
                acquired[perk.Index] = progress.Acquired.TryGetValue(perk.Key, out tier) ? tier : 0;
            }
            if (!Available)
            {
                Array.Clear(staged, 0, staged.Length);
            }
        }

        internal int Acquired(Perk perk) { return acquired[perk.Index]; }
        internal int Staged(Perk perk) { return staged[perk.Index]; }
        internal int Tier(Perk perk) { return Acquired(perk) + Staged(perk); }

        internal int PowerPointsLeft { get { return powerPoints - PendingPowerCost; } }

        internal int PendingPowerCost
        {
            get
            {
                int total = 0;
                foreach (Perk perk in tree.Perks)
                {
                    for (int tier = Acquired(perk); tier < Tier(perk); tier++)
                    {
                        total += perk.Costs[tier];
                    }
                }
                return total;
            }
        }

        private Block Blocked(Perk perk)
        {
            if (!Available)
            {
                return Block.Unavailable;
            }
            if (perk.Rank > Rank)
            {
                return Block.Rank;
            }
            int tier = Tier(perk);
            if (tier >= perk.Tiers)
            {
                return Block.Complete;
            }
            if (perk.Conflict != null && Tier(perk.Conflict) > 0)
            {
                return Block.Conflict;
            }
            return PowerPointsLeft < perk.Costs[tier] ? Block.Points : Block.None;
        }

        internal bool CanStage(Perk perk)
        {
            return Blocked(perk) == Block.None;
        }

        internal bool ShortOnPoints(Perk perk)
        {
            // Returns whether Perk Points alone keep the next rank from being selected.
            return Blocked(perk) == Block.Points;
        }

        internal int PendingCount
        {
            get
            {
                int total = 0;
                foreach (Perk perk in tree.Perks)
                {
                    total += Staged(perk);
                }
                return total;
            }
        }

        internal string Requirement(Perk perk)
        {
            // Returns the requirement line a tooltip adds below the description.
            switch (Blocked(perk))
            {
                case Block.Rank: return JournalText.Format("Arcana.Journal.Perks.Requirement.Rank", tree.RankTitle((int)perk.Rank));
                case Block.Complete: return null;
                case Block.Points: return null;
                default: return null;
            }
        }

        internal bool StageNext(Perk perk)
        {
            if (!CanStage(perk))
            {
                return false;
            }
            staged[perk.Index]++;
            return true;
        }

        internal void DiscardStaged()
        {
            // Drops every staged rank; nothing is bought and the points stay unspent.
            Array.Clear(staged, 0, staged.Length);
        }

        internal bool Commit()
        {
            // Buys every staged rank in one step.
            if (!Available)
            {
                return false;
            }

            Dictionary<string, int> purchased = new Dictionary<string, int>();
            foreach (Perk perk in tree.Perks)
            {
                if (staged[perk.Index] > 0)
                {
                    if (perk.Rank > Rank)
                    {
                        return false;
                    }
                    purchased[perk.Key] = Tier(perk);
                }
            }
            if (purchased.Count == 0)
            {
                return false;
            }

            WitchRecord record = WitchProgressStore.Purchase(simId, tree, purchased);
            if (record == null)
            {
                return false;
            }

            Array.Clear(staged, 0, staged.Length);
            powerPoints = record.Available;
            foreach (Perk perk in tree.Perks)
            {
                acquired[perk.Index] = record.TierOf(perk.Key);
            }
            return true;
        }
    }

    internal sealed class PowerProgress
    {
        internal readonly ulong SimDescriptionId;
        internal readonly long Experience;
        internal readonly Rank Rank;
        internal readonly float RankProgress;
        internal readonly float Proficiency;
        internal readonly int PowerPoints;
        internal readonly int Earned;
        internal readonly int Milestones;
        internal readonly Dictionary<string, int> Acquired;
        internal readonly bool Available;

        internal static readonly PowerProgress Unavailable = new PowerProgress(0UL, WitchProgressSource.NoExperience,
            Rank.Novice, 0f, 0f, 0, 0, 0, new Dictionary<string, int>(), false);

        private PowerProgress(ulong simDescriptionId, long experience, Rank rank, float rankProgress, float proficiency, int powerPoints,
            int earned, int milestones, Dictionary<string, int> acquired, bool available)
        {
            SimDescriptionId = simDescriptionId;
            Experience = experience;
            Rank = rank;
            RankProgress = rankProgress;
            Proficiency = proficiency;
            PowerPoints = powerPoints;
            Earned = earned;
            Milestones = milestones;
            Acquired = acquired;
            Available = available;
        }

        internal static PowerProgress From(ulong simDescriptionId, long experience, RankState state, float proficiency, WitchRecord record,
            OccultTree tree)
        {
            Dictionary<string, int> acquired = new Dictionary<string, int>();
            foreach (Perk perk in tree.Perks)
            {
                int tier = record.TierOf(perk.Key);
                if (tier > 0)
                {
                    acquired[perk.Key] = Math.Min(tier, perk.Tiers);
                }
            }
            return new PowerProgress(simDescriptionId, experience, (Rank)state.Rank, state.Progress, proficiency, record.Available,
                record.Earned, state.Reached, acquired, true);
        }
    }

    internal delegate int PointsReader(SimIdentity identity);

    internal static class PerkPoints
    {
        private sealed class Source
        {
            internal readonly OccultTypes Occult;
            internal readonly PointsReader Read;

            internal Source(OccultTypes occult, PointsReader read)
            {
                Occult = occult;
                Read = read;
            }
        }

        private static readonly Source[] sources =
        {
            new Source(OccultTypes.Witch, WitchProgressSource.GetAvailablePoints)
        };

        internal static bool Applies(OccultTypes occults)
        {
            foreach (Source source in sources)
            {
                if ((occults & source.Occult) != OccultTypes.None)
                {
                    return true;
                }
            }
            return false;
        }

        internal static int Available(SimIdentity identity)
        {
            int total = 0;
            foreach (Source source in sources)
            {
                if ((identity.Occults & source.Occult) != OccultTypes.None)
                {
                    total += source.Read(identity);
                }
            }
            return total;
        }
    }
}
