using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Skills;
using System;

namespace SupernaturalExpanded.Journal
{
    internal struct RankState
    {
        internal readonly int Rank;
        internal readonly float Progress;
        internal readonly int Reached;

        internal RankState(int rank, float progress, int reached)
        {
            Rank = rank;
            Progress = progress;
            Reached = reached;
        }
    }

    // The Witch rank ladder and its Perk Point schedule; no game types, so it can be tested on its own.
    internal static class WitchProgression
    {
        internal const int RankCount = 5;
        internal const int MilestoneCount = 13;
        internal const int AllMask = (1 << MilestoneCount) - 1;
        private const int StepsPerRank = 3;
        private const long Milli = 1000L;

        // Perk Points per milestone, 40 in all; milestone i sits i thirds up the ladder: rank i / 3, step i % 3 (0 = the rank itself, 1 and 2 = the notches above it).
        private static readonly int[] points = { 1, 1, 2, 2, 3, 3, 3, 3, 3, 4, 4, 5, 6 };

        // Skill level at which each rank begins when the skill tops out at level 10; other maxima scale from this row.
        private static readonly int[] rankStartLevels = { 0, 3, 6, 8, 10 };

        internal static int PointsFor(int milestone)
        {
            return points[milestone];
        }

        internal static int PointsIn(int mask)
        {
            int total = 0;
            for (int i = 0; i < MilestoneCount; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    total += points[i];
                }
            }
            return total;
        }

        private static int HighestClaimed(int mask)
        {
            for (int i = MilestoneCount - 1; i >= 0; i--)
            {
                if ((mask & (1 << i)) != 0)
                {
                    return i;
                }
            }
            return -1;
        }

        // Whole-and-fractional skill points as thousandths, rounded down, so every threshold test is integer arithmetic.
        internal static long ToThousandths(float skillPoints)
        {
            if (float.IsNaN(skillPoints) || skillPoints <= 0f)
            {
                return 0L;
            }
            double scaled = Math.Floor((double)skillPoints * 1000.0);
            return scaled > 1e15 ? 1000000000000000L : (long)scaled;
        }

        // A loaded Witch's attained native level provides a floor if saved points lag behind it.
        internal static long ExperienceForSkill(float skillPoints, int level, int[] pointsForNextLevel)
        {
            long experience = ToThousandths(skillPoints);
            if (pointsForNextLevel == null || level <= 0 || level > pointsForNextLevel.Length)
            {
                return experience;
            }
            return Math.Max(experience, (long)pointsForNextLevel[level - 1] * Milli);
        }

        // Cumulative thousandths of skill points at which each rank begins, or null when the skill data is unusable.
        internal static long[] RankThresholds(int[] pointsForNextLevel, int maxLevel)
        {
            if (pointsForNextLevel == null || maxLevel <= 0)
            {
                return null;
            }

            long[] thresholds = new long[RankCount];
            long previous = 0L;
            for (int rank = 0; rank < RankCount; rank++)
            {
                int level = (rankStartLevels[rank] * maxLevel + 5) / 10;
                long xp = 0L;
                if (level > 0)
                {
                    if (level > pointsForNextLevel.Length)
                    {
                        return null;
                    }
                    xp = (long)pointsForNextLevel[level - 1] * Milli;
                }
                if (xp < previous)
                {
                    xp = previous;
                }
                thresholds[rank] = xp;
                previous = xp;
            }
            return thresholds;
        }

        private static bool IsReached(int milestone, long xpMilli, long[] thresholds)
        {
            if (milestone == 0)
            {
                return true;
            }
            if (thresholds == null)
            {
                return false;
            }

            int rank = milestone / StepsPerRank;
            int step = milestone % StepsPerRank;
            long low = thresholds[rank];
            if (step == 0)
            {
                return xpMilli >= low;
            }
            long span = thresholds[rank + 1] - low;
            return 3L * (xpMilli - low) >= step * span;
        }

        // How many milestones, counted from the first, the experience has reached.
        internal static int Reached(long xpMilli, long[] thresholds)
        {
            int count = 0;
            while (count < MilestoneCount && IsReached(count, xpMilli, thresholds))
            {
                count++;
            }
            return count;
        }

        // Claimed milestones are a floor: experience lost later never lowers the rank or the bar below what was earned.
        internal static RankState GetRankState(long xpMilli, long[] thresholds, int claimedMask)
        {
            int reached = Reached(xpMilli, thresholds);
            int floor = HighestClaimed(claimedMask) + 1;
            int count = Math.Max(reached, floor);
            int index = count - 1;
            int rank = Math.Min(index / StepsPerRank, RankCount - 1);
            if (rank >= RankCount - 1)
            {
                return new RankState(rank, 1f, count);
            }

            int step = index % StepsPerRank;
            float lower = step / 3f;
            float progress = lower;
            if (thresholds != null && reached >= floor)
            {
                long low = thresholds[rank];
                long span = thresholds[rank + 1] - low;
                if (span > 0L)
                {
                    progress = (float)(xpMilli - low) / span;
                }
            }
            return new RankState(rank, Math.Max(lower, Math.Min((step + 1) / 3f, progress)), count);
        }
    }

    // Connects the vanilla Spellcasting skill, the saved milestone claims and the journal's view of them.
    internal static class WitchProgressSource
    {
        internal const long NoExperience = -1L;

        private static long[] thresholds;

        internal static PowerProgress Read(SimIdentity identity)
        {
            return Read(identity, null);
        }

        // On a polling refresh, compare both the raw XP used by the perk
        // ladder and the native skill fill. A level can change at the same XP.
        internal static PowerProgress Read(SimIdentity identity, PowerSelection previous)
        {
            if (identity == null || identity.Description == null || identity.SimDescriptionId == 0UL)
            {
                return PowerProgress.Unavailable;
            }

            Skill skill = GetSpellcastingSkill(identity.Description);
            long experience = ReadExperience(skill);
            float proficiency = ReadProficiency(skill);
            long[] ladder = GetRankThresholds(skill);
            RankState rank = WitchProgression.GetRankState(experience, ladder, 0);
            if (previous != null && previous.Available && experience == previous.Experience && proficiency == previous.Proficiency
                && rank.Rank <= (int)previous.Rank && rank.Reached <= previous.Milestones)
            {
                return null;
            }

            WitchRecord record = WitchProgressStore.ClaimMilestones(identity.SimDescriptionId, experience, ladder);
            if (record == null)
            {
                return PowerProgress.Unavailable;
            }
            return PowerProgress.From(identity.SimDescriptionId, experience,
                WitchProgression.GetRankState(experience, ladder, record.Claimed), proficiency, record, WitchTree.Tree);
        }

        private static long ReadExperience(Skill skill)
        {
            return skill == null ? 0L : WitchProgression.ExperienceForSkill(
                skill.SkillPoints, skill.SkillLevel, skill.PointsForNextLevel);
        }

        // Give every vanilla Spellcasting level the same width. Skill.Progress
        // is the native within-level fraction, including during hidden level 0.
        private static float ReadProficiency(Skill skill)
        {
            if (skill == null || skill.MaxSkillLevel <= 0 || skill.SkillLevel < 0)
            {
                return 0f;
            }

            int maximum = skill.MaxSkillLevel;
            int level = skill.SkillLevel;
            if (level >= maximum)
            {
                return 1f;
            }

            int[] thresholds = skill.PointsForNextLevel;
            float within = thresholds != null && level < thresholds.Length ? skill.Progress : 0f;
            if (float.IsNaN(within) || float.IsInfinity(within))
            {
                within = 0f;
            }
            return (level + Math.Max(0f, Math.Min(1f, within))) / maximum;
        }

        // Spendable Perk Points, or zero for a Sim that cannot be read.
        internal static int GetAvailablePoints(SimIdentity identity)
        {
            PowerProgress progress = Read(identity);
            return progress.Available ? progress.PowerPoints : 0;
        }

        internal static WitchRecord ReadGameplay(SimDescription description, out Rank rank)
        {
            Skill skill = GetSpellcastingSkill(description);
            long xp = ReadExperience(skill);
            long[] ladder = GetRankThresholds(skill);
            WitchRecord record = WitchProgressStore.ClaimMilestones(description.SimDescriptionId, xp, ladder);
            rank = (Rank)WitchProgression.GetRankState(xp, ladder, record == null ? 0 : record.Claimed).Rank;
            return record;
        }

        private static Skill GetSpellcastingSkill(SimDescription description)
        {
            SkillManager skills = description.SkillManager;
            return skills == null ? null : skills.GetElement(SkillNames.Spellcasting);
        }

        // The ladder comes from skill tuning, which is fixed for the session, so it is built once.
        private static long[] GetRankThresholds(Skill skill)
        {
            if (thresholds == null)
            {
                if (skill != null)
                {
                    thresholds = WitchProgression.RankThresholds(skill.PointsForNextLevel, skill.MaxSkillLevel);
                }
                if (thresholds == null)
                {
                    Skill template = SkillManager.GetStaticSkill(SkillNames.Spellcasting);
                    if (template != null && template != skill)
                    {
                        thresholds = WitchProgression.RankThresholds(template.PointsForNextLevel, template.MaxSkillLevel);
                    }
                }
            }
            return thresholds;
        }
    }
}
