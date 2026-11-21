using System;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Seasons;

namespace SupernaturalExpanded.Journal
{
    internal static class RainLuck
    {
        private const float Minutes = 180f;

        internal static void Refresh()
        {
            foreach (Sim sim in Sims3.Gameplay.Queries.GetObjects<Sim>()) Apply(sim);
        }

        internal static void Apply(Sim sim)
        {
            if (sim == null || sim.HasBeenDestroyed || sim.SimDescription == null || sim.BuffManager == null) return;
            try
            {
                if (GenieAbilities.IsGenie(sim)) Give(sim, BuffNames.FeelingLucky);
                else if (RainExposure.ReceivesUnlucky(sim.SimDescription.IsHuman, false, sim.IsOutside,
                    SimTemperature.OutsideSimIsConsideredInside(sim))) Give(sim, BuffNames.FeelingUnlucky);
            }
            catch (Sims3.SimIFace.ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Applying Fortune's Rain luck", error); }
        }

        private static void Give(Sim sim, BuffNames guid)
        {
            BuffInstance existing = sim.BuffManager.GetElement(guid);
            if (existing == null) LuckMoodlet.Add(sim, guid, Minutes, Origin.FromWeatherStone);
            else if (existing.BuffOrigin == Origin.FromWeatherStone && existing.TimeoutCount < Minutes) existing.TimeoutCount = Minutes;
        }
    }

    internal static class LuckMoodlet
    {
        private delegate bool AddBuff(BuffInstance buff, BuffNames guid, int mood, float timeout, bool paused,
            MoodAxis axis, Origin origin, bool considerDelay, bool travelReaddition);
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo Info = typeof(Buff).GetField("mInfo", PrivateInstance);
        private static readonly MethodInfo NativeAdd = typeof(BuffManager).GetMethod("AddBuff", PrivateInstance, null,
            new Type[] { typeof(BuffInstance), typeof(BuffNames), typeof(int), typeof(float), typeof(bool),
                typeof(MoodAxis), typeof(Origin), typeof(bool), typeof(bool) }, null);

        internal static bool Covers(BuffNames guid)
        {
            return guid == BuffNames.FeelingLucky || guid == BuffNames.FeelingUnlucky;
        }

        internal static bool Add(Sim sim, BuffNames guid, float minutes, Origin origin)
        {
            BuffInstance native;
            if (!BuffManager.BuffDictionary.TryGetValue((ulong)guid, out native))
                throw new InvalidOperationException("Native moodlet is not registered: " + guid);
            if (Info == null || NativeAdd == null) throw new InvalidOperationException("Missing native luck moodlet API.");
            BuffInstance plain = new Buff((Buff.BuffData)Info.GetValue(native.mBuff)).CreateBuffInstance();
            AddBuff add = (AddBuff)NativeWeatherCalls.Bind(typeof(AddBuff), sim.BuffManager, NativeAdd);
            if (!add(plain, guid, int.MaxValue, minutes, false, MoodAxis.None, origin, false, false)) return false;
            sim.BuffManager.RemoveElement(guid == BuffNames.FeelingLucky ? BuffNames.FeelingUnlucky : BuffNames.FeelingLucky);
            return true;
        }
    }

    internal static class RainExposure
    {
        internal static bool ReceivesUnlucky(bool human, bool genie, bool outside, bool consideredInside)
        {
            return human && !genie && outside && !consideredInside;
        }
    }
}
