using System;
using System.Reflection;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    // Adds Study Spells to every station in the world: the ones already placed when a save finishes loading, and each one placed afterwards.
    internal static class SpellStudyStations
    {
        private static bool active;
        private static PropertyInfo placedObject;

        internal static void Start()
        {
            if (active)
            {
                return;
            }

            active = true;
            World.OnObjectPlacedInLotEventHandler += OnPlaced;
            foreach (AlchemyStation station in Sims3.Gameplay.Queries.GetObjects<AlchemyStation>())
            {
                Install(station);
            }
        }

        internal static void Stop()
        {
            if (!active)
            {
                return;
            }

            active = false;
            World.OnObjectPlacedInLotEventHandler -= OnPlaced;
        }

        private static void OnPlaced(object sender, EventArgs args)
        {
            if (args == null || args.GetType().Name != "OnObjectPlacedInLotEventArgs")
            {
                return;
            }

            if (placedObject == null)
            {
                placedObject = args.GetType().GetProperty("ObjectId");
            }
            if (placedObject != null)
            {
                ObjectGuid id = (ObjectGuid)placedObject.GetValue(args, null);
                Install(Sims3.Gameplay.Abstracts.GameObject.GetObject(id) as AlchemyStation);
            }
        }

        // The duplicate check makes this safe to repeat; interactions are rebuilt by the game on every load, so nothing is saved.
        private static void Install(AlchemyStation station)
        {
            if (station == null || station.HasBeenDestroyed)
            {
                return;
            }

            try
            {
                station.AddInteraction(StudySpells.Singleton, true);
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Adding Study Spells to a station", error);
            }
        }
    }
}
