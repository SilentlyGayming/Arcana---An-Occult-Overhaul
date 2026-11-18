using System.Collections.Generic;
using System;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class SimIdentity
    {
        internal readonly ObjectGuid Id;
        internal readonly string Name;
        internal readonly string LifeState;
        internal readonly OccultTypes Occults;
        internal readonly int Mood;
        internal readonly SimDescription Description;
        // The save-stable identity; ObjectGuid can change when a Sim is re-instantiated or travels.
        internal readonly ulong SimDescriptionId;

        private SimIdentity(ObjectGuid id, string name, string lifeState, OccultTypes occults, int mood,
            SimDescription description)
        {
            Id = id;
            Name = name;
            LifeState = lifeState;
            Occults = occults;
            Mood = mood;
            Description = description;
            SimDescriptionId = description == null ? 0UL : description.SimDescriptionId;
        }

        internal string FirstName
        {
            get
            {
                int space = Name.IndexOf(' ');
                return space > 0 ? Name.Substring(0, space) : Name;
            }
        }

        internal static SimIdentity Read(IHudModel model)
        {
            SimInfo info = model == null ? null : model.GetCurrentSimInfo();
            if (info == null || !info.mGuid.IsValid)
            {
                return null;
            }

            Sim sim = GlobalFunctions.ConvertGuidToObject<Sim>(info.mGuid);
            OccultTypes types = OccultsOf(info, sim);
            if (types == OccultTypes.None)
            {
                return null;
            }

            SimDescription description = ReadDescription(model, info, sim);
            string name = info.mName;
            if (string.IsNullOrEmpty(name) && sim != null)
            {
                name = sim.FullName;
            }
            return new SimIdentity(info.mGuid, name ?? JournalText.Get("Arcana.Common.SelectedSim"),
                LifeStateName(types), types, Math.Max(0, (int)info.mMood), description);
        }

        private static SimDescription ReadDescription(IHudModel model, SimInfo info, Sim sim)
        {
            if (sim != null && sim.SimDescription != null)
            {
                return sim.SimDescription;
            }
            if (info.UninstantiatedButInHousehold && info.UninstantiatedSimDescriptionId != 0UL)
            {
                return SimDescription.Find(info.UninstantiatedSimDescriptionId);
            }

            // The HUD can report the selection before the ObjectGuid resolves through GlobalFunctions.
            SimDescription selected = model.GetSimDescription() as SimDescription;
            Sim actor = selected == null ? null : selected.CreatedSim;
            return actor != null && actor.ObjectId == info.mGuid ? selected : null;
        }

        // Reads just the supported occult flags for frequent HUD refreshes.
        internal static OccultTypes ReadOccults(IHudModel model, out ObjectGuid id)
        {
            SimInfo info = model == null ? null : model.GetCurrentSimInfo();
            if (info == null || !info.mGuid.IsValid)
            {
                id = default(ObjectGuid);
                return OccultTypes.None;
            }

            id = info.mGuid;
            return OccultsOf(info, GlobalFunctions.ConvertGuidToObject<Sim>(info.mGuid));
        }

        private static OccultTypes OccultsOf(SimInfo info, Sim sim)
        {
            OccultTypes types = info.OccultInfo == null ? OccultTypes.None : info.OccultInfo.OccultType;
            if (sim != null)
            {
                if (sim.OccultManager != null)
                {
                    types |= sim.OccultManager.CurrentOccultTypes;
                }
                if (sim.SimDescription != null && sim.SimDescription.IsGhost)
                {
                    types |= OccultTypes.Ghost;
                }
            }
            return types & OccultAssetData.SupportedTypes;
        }

        private static string LifeStateName(OccultTypes types)
        {
            List<string> names = new List<string>();
            OccultTypes remaining = types;
            foreach (OccultTypes type in Enum.GetValues(typeof(OccultTypes)))
            {
                if (type == OccultTypes.None || (remaining & type) != type)
                {
                    continue;
                }
                remaining &= ~type;
                names.Add(LifeStateNames.For(type));
            }

            return string.Join(JournalText.Get("Arcana.LifeState.Separator"), names.ToArray());
        }

        internal UIImage Portrait(IHudModel model)
        {
            ThumbnailKey key = model.GetThumbnailForGameObject(Id, ThumbnailSize.Large, Mood);
            return UIManager.GetThumbnailImage(key);
        }
    }

    // Shared header caption. Only witches have an implemented rank ladder; the other occults show no rank
    // until their progression arrives, so no rank is ever invented for them.
    internal static class OccultRankLabel
    {
        // True for the life states whose rank ladder is implemented.
        internal static bool HasLadder(OccultTypes types)
        {
            return OccultAssetData.Primary(types) == OccultTypes.Witch;
        }

        // The rank name when the Sim has a readable progression ladder.
        internal static string Rank(OccultTypes types, PowerSelection witchProgress)
        {
            if (HasLadder(types) && witchProgress != null && witchProgress.Available)
            {
                return WitchTree.Tree.RankLabel((int)witchProgress.Rank);
            }
            return "";
        }

        // The primary life state, with its rank when available.
        internal static string For(OccultTypes types, PowerSelection witchProgress)
        {
            OccultTypes primary = OccultAssetData.Primary(types);
            string rank = Rank(types, witchProgress);
            string lifeState = LifeStateNames.For(primary);
            return rank.Length == 0 ? lifeState : JournalText.Format("Arcana.Common.WithQualifier", lifeState, rank);
        }
    }

    internal static class LifeStateNames
    {
        internal static string For(OccultTypes state)
        {
            switch (state)
            {
                case OccultTypes.Vampire: return JournalText.Get("Arcana.LifeState.Name.Vampire");
                case OccultTypes.Genie: return JournalText.Get("Arcana.LifeState.Name.Genie");
                case OccultTypes.Werewolf: return JournalText.Get("Arcana.LifeState.Name.Werewolf");
                case OccultTypes.Ghost: return JournalText.Get("Arcana.LifeState.Name.Ghost");
                case OccultTypes.Fairy: return JournalText.Get("Arcana.LifeState.Name.Fairy");
                case OccultTypes.Witch: return JournalText.Get("Arcana.LifeState.Name.Witch");
                case OccultTypes.Mermaid: return JournalText.Get("Arcana.LifeState.Name.Mermaid");
                default: return "";
            }
        }
    }

    internal static class LifeStateDescriptions
    {
        // Keep the legacy description priority for hybrids.
        private static readonly OccultTypes[] States =
        {
            OccultTypes.Vampire, OccultTypes.Witch, OccultTypes.Werewolf, OccultTypes.Fairy,
            OccultTypes.Genie, OccultTypes.Ghost, OccultTypes.Mermaid
        };

        private static readonly string[] Descriptions =
        {
            "Arcana.LifeState.Description.Vampire",
            "Arcana.LifeState.Description.Witch",
            "Arcana.LifeState.Description.Werewolf",
            "Arcana.LifeState.Description.Fairy",
            "Arcana.LifeState.Description.Genie",
            "Arcana.LifeState.Description.Ghost",
            "Arcana.LifeState.Description.Mermaid"
        };

        internal static string For(SimIdentity identity)
        {
            for (int i = 0; i < States.Length; i++)
            {
                if ((identity.Occults & States[i]) != OccultTypes.None)
                {
                    return JournalText.Get(Descriptions[i]);
                }
            }

            return "";
        }
    }
}
