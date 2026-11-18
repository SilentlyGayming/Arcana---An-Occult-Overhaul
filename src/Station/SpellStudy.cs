using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Skills;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    // One occult type that may study at the All-in-One Magic Station, and the skill that studying trains.
    internal sealed class SpellStudyTrack
    {
        internal readonly OccultTypes Occult;
        internal readonly SkillNames Skill;

        internal SpellStudyTrack(OccultTypes occult, SkillNames skill)
        {
            Occult = occult;
            Skill = skill;
        }
    }

    // The single place that decides who may study spells at the station and how fast the studied skill grows.
    internal static class SpellStudy
    {
        // Skill points per Sim-minute added to the studied skill; this is the one value that sets the study pace.
        internal const float SkillPointsPerSimMinute = 1f;

        // Adding an occult type here is all it takes to offer the interaction to it; only Witches are enabled.
        private static readonly SpellStudyTrack[] Tracks =
        {
            new SpellStudyTrack(OccultTypes.Witch, SkillNames.Spellcasting)
        };

        internal static SpellStudyTrack TrackFor(Sim sim)
        {
            if (sim == null || sim.OccultManager == null)
            {
                return null;
            }

            for (int i = 0; i < Tracks.Length; i++)
            {
                if (sim.OccultManager.HasOccultType(Tracks[i].Occult))
                {
                    return Tracks[i];
                }
            }
            return null;
        }
    }
}
