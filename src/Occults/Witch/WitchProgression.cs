using System;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Skills;

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

    internal static class WitchProgression
    {
        internal const int RankCount = 5;
        internal const int MilestoneCount = 13;
        internal const int AllMask = (1 << MilestoneCount) - 1;
        private const int StepsPerRank = 3;
        private const long Milli = 1000L;

        private static readonly int[] points = { 1, 1, 2, 2, 3, 3, 3, 3, 3, 4, 4, 5, 6 };

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

        internal static long ToThousandths(float skillPoints)
        {
            // Converts skill points to thousandths, rounded down, so thresholds use integer arithmetic.
            if (float.IsNaN(skillPoints) || skillPoints <= 0f)
            {
                return 0L;
            }
            double scaled = Math.Floor((double)skillPoints * 1000.0);
            return scaled > 1e15 ? 1000000000000000L : (long)scaled;
        }

        internal static long ExperienceForSkill(float skillPoints, int level, int[] pointsForNextLevel)
        {
            // Returns the skill experience, using the attained native level as a floor.
            long experience = ToThousandths(skillPoints);
            if (pointsForNextLevel == null || level <= 0 || level > pointsForNextLevel.Length)
            {
                return experience;
            }
            return Math.Max(experience, (long)pointsForNextLevel[level - 1] * Milli);
        }

        internal static long[] RankThresholds(int[] pointsForNextLevel, int maxLevel)
        {
            // Returns cumulative thousandths at which each rank begins, or null when skill data is unusable.
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

        internal static int Reached(long xpMilli, long[] thresholds)
        {
            // Counts the milestones the experience has reached.
            int count = 0;
            while (count < MilestoneCount && IsReached(count, xpMilli, thresholds))
            {
                count++;
            }
            return count;
        }

        internal static RankState GetRankState(long xpMilli, long[] thresholds, int claimedMask)
        {
            // Returns the rank state, treating claimed milestones as a floor.
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

    internal static class WitchProgressSource
    {
        internal const long NoExperience = -1L;

        private static long[] thresholds;

        internal static PowerProgress Read(SimIdentity identity)
        {
            return Read(identity, null);
        }

        internal static PowerProgress Read(SimIdentity identity, PowerSelection previous)
        {
            // Reads the Witch's progress, including the raw experience and native skill fill.
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

        private static float ReadProficiency(Skill skill)
        {
            // Reads Magical Proficiency with every Spellcasting level given the same width.
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

        internal static int GetAvailablePoints(SimIdentity identity)
        {
            // Spendable Perk Points, or zero for a Sim that cannot be read.
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

        private static long[] GetRankThresholds(Skill skill)
        {
            // Returns the rank ladder, built once from fixed skill tuning.
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
