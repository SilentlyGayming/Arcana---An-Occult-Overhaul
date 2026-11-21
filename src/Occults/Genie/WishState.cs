using System;
using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Careers;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    internal sealed class WishRecord
    {
        internal bool CheatDeath;
        internal bool CheatDeathAttempted;
        internal int Escapes;
        internal long JoyEnds;
        internal long SadEnds;
        internal bool CareerPending;
        internal OccupationNames CareerGuid;
        internal long CareerExpires;
        internal bool WorkStarted;
        internal bool WaitForCurrentShift;
        internal float PenaltyRemaining;
        internal float LastPerformance;
        internal int LastCareerLevel;
        internal WishRecord Clone() { return (WishRecord)MemberwiseClone(); }
    }

    internal static class WishState
    {
        [Persistable(false)] private static AlarmHandle alarm = AlarmHandle.kInvalidHandle;
        [Persistable(false)] private static EventListener work;

        internal static WishRecord Read(SimDescription sim, bool create)
        {
            if (sim == null || sim.SkillManager == null) return null;
            WishEffectSkill skill = sim.SkillManager.GetElement(WishEffectSkill.WishGuid) as WishEffectSkill;
            if (skill == null && create)
            {
                WishEffectRegistration.Register();
                sim.SkillManager.AddElement(WishEffectSkill.WishGuid);
                skill = sim.SkillManager.GetElement(WishEffectSkill.WishGuid) as WishEffectSkill;
            }
            return skill == null ? null : skill.Record;
        }

        internal static long After(float hours)
        { return SimClock.CurrentTicks + SimClock.ConvertToTicks(hours, TimeUnit.Hours); }

        internal static void Prepare()
        {
            Guard("Registering wish records", WishEffectRegistration.Register);
            Guard("Registering wish moodlets", WishBuffs.Register);
            Guard("Installing wish death protection", WishDeath.Install);
            Guard("Installing wish menus", GenieWishMenu.Prepare);
        }
        private static void Guard(string operation, Sims3.UI.Function action)
        {
            try { action(); }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce(operation, error); }
        }
        internal static void Start()
        {
            Stop(); Prepare(); Tick();
            alarm = AlarmManager.Global.AddAlarmRepeating(1f, TimeUnit.Minutes, Tick, 1f, TimeUnit.Minutes,
                "Arcana wish effects", AlarmType.NeverPersisted, null);
            try { work = EventTracker.AddListener(EventTypeId.kWentToWork, OnWork); }
            catch { Stop(); throw; }
        }
        internal static void Stop()
        {
            WitchCastingLifecycle.RemoveAlarm(ref alarm);
            WitchCastingLifecycle.RemoveListener(ref work);
        }

        private static ListenerAction OnWork(Event e)
        {
            Sim sim = e.Actor as Sim;
            if (sim != null) Update(sim);
            return ListenerAction.Keep;
        }

        internal static void Tick()
        {
            Sim[] sims = Sims3.Gameplay.Queries.GetObjects<Sim>();
            foreach (Sim sim in sims)
            {
                try { Update(sim); }
                catch (ResetException) { throw; }
                catch (Exception error) { JournalBootstrap.ReportOnce("Updating wish effects", error); }
            }
            Guard("Updating wish repossession", WishRepossession.Tick);
            Guard("Updating wish lamp handoffs", delegate { WishLamp.Tick(sims); });
        }

        private static void Update(Sim sim)
        {
            if (sim == null || sim.HasBeenDestroyed || sim.BuffManager == null) return;
            WishBuffs.Refresh(sim);
            WishEffectSkill travel = sim.SkillManager == null ? null : sim.SkillManager.GetElement(WishEffectSkill.WishGuid) as WishEffectSkill;
            if (travel != null) travel.RestoreTravelBuffs(sim);
            WishRecord state = travel == null ? null : travel.Record;
            if (state == null) return;
            long now = SimClock.CurrentTicks;
            if (state.JoyEnds > 0 && (now >= state.JoyEnds || !sim.BuffManager.HasElement(WishBuffs.BorrowedJoy)))
                EndJoy(sim, state, now);
            if (!state.CareerPending) return;
            Occupation career = sim.SimDescription.CareerManager == null ? null : sim.SimDescription.CareerManager.Occupation;
            if (now >= state.CareerExpires || career == null || career.Guid != state.CareerGuid)
            { ClearCareer(sim, state); return; }
            if (state.WaitForCurrentShift)
            {
                if (!career.IsAtWork) state.WaitForCurrentShift = false;
                return;
            }
            if (career.IsAtWork)
            {
                float performance = WishRules.NormalizedPerformance(career.Performance, career is XpBasedCareer);
                if (!state.WorkStarted)
                {
                    state.WorkStarted = true;
                    state.LastPerformance = performance;
                    state.LastCareerLevel = career.Level;
                    state.PenaltyRemaining = WishRules.CareerPenalty;
                    WishBuffs.GiveWrong(sim, 24f, -10);
                }
                if (career.Level != state.LastCareerLevel) { ClearCareer(sim, state); return; }
                float gained = Math.Max(0f, performance - state.LastPerformance);
                float withheld = Math.Min(gained, state.PenaltyRemaining);
                if (withheld > 0f)
                {
                    XpBasedCareer xp = career as XpBasedCareer;
                    float units = xp != null && career.Performance > 0f ? xp.Experience / career.Performance / 100f : 1f;
                    career.UpdatePerformanceOrExperience(-withheld * units);
                    state.PenaltyRemaining -= withheld;
                }
                state.LastPerformance = WishRules.NormalizedPerformance(career.Performance, career is XpBasedCareer);
            }
            else if (state.WorkStarted) ClearCareer(sim, state);
        }

        internal static void JoyExpired(Sim sim)
        {
            WishRecord state = sim == null ? null : Read(sim.SimDescription, false);
            if (state != null && state.JoyEnds > 0) EndJoy(sim, state, SimClock.CurrentTicks);
        }

        private static void EndJoy(Sim sim, WishRecord state, long now)
        {
            if (now < state.JoyEnds) state.SadEnds = After(24f);
            state.JoyEnds = 0;
            sim.BuffManager.RemoveElement(WishBuffs.BorrowedJoy);
            if (state.SadEnds > now)
                WishBuffs.GiveWrong(sim, SimClock.ConvertFromTicks(state.SadEnds - now, TimeUnit.Hours));
        }

        private static void ClearCareer(Sim sim, WishRecord state)
        {
            state.CareerPending = false; state.CareerGuid = (OccupationNames)0; state.WorkStarted = false; state.PenaltyRemaining = 0;
            sim.BuffManager.RemoveElement(WishBuffs.CareerSetback);
        }
    }
}
