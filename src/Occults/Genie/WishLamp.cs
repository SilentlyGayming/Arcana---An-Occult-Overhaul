using Sims3.UI.Hud;
using Sims3.UI.Controller;
using System;
using System.Collections.Generic;
using Sims3.Gameplay;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Rewards;
using Sims3.Gameplay.Opportunities;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    internal sealed class LampHandoff
    {
        internal GenieLamp Lamp;
        internal ulong PreviousHousehold;
        internal long ReturnQueuedAt;
    }

    internal static class WishLamp
    {
        [PersistableStatic] internal static List<LampHandoff> sHandoffs;
        private static readonly OpportunityNames[] FreedomSteps = {
            OpportunityNames.EP6_ReleaseGenie_FreezeLamp, OpportunityNames.EP6_ReleaseGenie_HeatLamp,
            OpportunityNames.EP6_ReleaseGenie_TakeUnderground, OpportunityNames.EP6_ReleaseGenie_SummonGenie };

        internal static GenieLamp Lamp(Sim genie)
        {
            OccultGenie occult = genie == null || genie.OccultManager == null ? null : genie.OccultManager.GetOccultType(OccultTypes.Genie) as OccultGenie;
            GenieLamp lamp = occult == null ? null : occult.LampOfGenie;
            return lamp == null || lamp.HasBeenDestroyed || lamp.mGenieDescription != genie.SimDescription ? null : lamp;
        }

        internal static bool Allowed(Sim genie, Sim wisher)
        {
            GenieLamp lamp = Lamp(genie);
            if (lamp == null || wisher == null || wisher == genie || wisher.SimDescription == null || !wisher.SimDescription.IsHuman || !WishEffects.CanReceive(wisher) ||
                genie.SimDescription.Household == Household.ActiveHousehold || genie.IsSelectable) return false;
            if (sHandoffs != null) foreach (LampHandoff handoff in sHandoffs) if (handoff.Lamp == lamp) return false;
            OccultGenie occult = genie.OccultManager.GetOccultType(OccultTypes.Genie) as OccultGenie;
            return occult.Summoner == wisher && lamp.mRemainingWishes > 0 && lamp.GenieCanMakeWishAlarm == AlarmHandle.kInvalidHandle;
        }

        internal static bool CanFree(Sim genie, Sim wisher)
        {
            if (!Allowed(genie, wisher) || !wisher.SimDescription.TeenOrAbove || wisher.OpportunityManager == null ||
                wisher.SimDescription.IsEP11Bot && !wisher.HasTrait(TraitNames.SentienceChip)) return false;
            foreach (OpportunityNames step in FreedomSteps) if (wisher.OpportunityManager.HasOpportunity(step)) return false;
            Relationship rel = Relationship.Get(wisher, genie, false);
            return rel != null && rel.CurrentLTR >= LongTermRelationshipTypes.GoodFriend;
        }

        internal static void Commit(Sim genie, Sim wisher, WishKind kind, bool success)
        {
            GenieLamp lamp = Lamp(genie);
            if (lamp == null) return;
            int delta = kind == WishKind.MoreWishes ? WishRules.MoreWishesDelta(success, success && RandomUtil.RandomChance(5f)) : -1;
            if (kind == WishKind.FreeGenie && !success)
            {
                lamp.mRemainingWishes = Math.Max(0, lamp.mRemainingWishes + delta);
            }
            else lamp.AdjustRemainingWishes(wisher, genie, delta);
            DaysOfTheWeek day = SimClock.DayAfter(SimClock.CurrentDayOfWeek);
            long ticks = SimClock.GetTicksToNext(day) + SimClock.ConvertToTicks(7f, TimeUnit.Hours);
            lamp.GenieCanMakeWishAlarm = lamp.AddAlarm(SimClock.ConvertFromTicks(ticks, TimeUnit.Minutes), TimeUnit.Minutes,
                lamp.SetGenieCanWishAgain, "Genie Wait Day To Grant Next Wish", AlarmType.AlwaysPersisted);
        }

        internal static bool Escape(Sim genie, Sim previous)
        {
            GenieLamp lamp = Lamp(genie);
            if (lamp == null || genie.IsSelectable || genie.Household == Household.ActiveHousehold || previous.Household == null) return false;
            Sims3.Gameplay.Inventory original = lamp.InInventory && lamp.ItemComp != null ? lamp.ItemComp.InventoryParent : null;
            if (original == null || original.Owner != previous) return false;
            LampHandoff handoff = new LampHandoff();
            handoff.Lamp = lamp; handoff.PreviousHousehold = previous.Household.HouseholdId; handoff.ReturnQueuedAt = SimClock.CurrentTicks;
            if (sHandoffs == null) sHandoffs = new List<LampHandoff>();
            sHandoffs.Add(handoff);
            if (!original.TryToRemove(lamp)) { sHandoffs.Remove(handoff); return false; }
            lamp.RemoveFromWorld();
            if (!lamp.PlayReturnToLamp())
            { original.ForceAdd(lamp); sHandoffs.Remove(handoff); return false; }
            RemoveOldControl(genie);
            foreach (OpportunityNames step in FreedomSteps) previous.OpportunityManager.ClearOpportunity(OpportunityCategory.Special, step);
            return true;
        }

        internal static void Tick(Sim[] sims)
        {
            if (sHandoffs == null || sHandoffs.Count == 0) return;
            foreach (LampHandoff handoff in new List<LampHandoff>(sHandoffs))
            {
                GenieLamp lamp = handoff.Lamp;
                if (lamp == null || lamp.HasBeenDestroyed) { sHandoffs.Remove(handoff); continue; }
                if (lamp.mGenieDescription == null) continue;
                Sim summoned = lamp.mGenieDescription.CreatedSim;
                if (summoned != null)
                {
                    RemoveOldControl(summoned);
                    bool returning = summoned.CurrentInteraction is GenieLamp.ReturnToLamp ||
                        summoned.InteractionQueue != null && summoned.InteractionQueue.HasInteractionOfType(GenieLamp.ReturnToLamp.Singleton);
                    if (!returning && SimClock.CurrentTicks >= handoff.ReturnQueuedAt + SimClock.ConvertToTicks(5f, TimeUnit.Minutes))
                    {
                        handoff.ReturnQueuedAt = SimClock.CurrentTicks;
                        lamp.PlayReturnToLamp();
                    }
                    continue;
                }
                foreach (Sim next in sims)
                {
                    if (next.HasBeenDestroyed || next.SimDescription == null || !next.SimDescription.IsHuman || !next.SimDescription.YoungAdultOrAbove ||
                        next.SimDescription.IsDead || next.Household == null || next.Household == Household.ActiveHousehold ||
                        next.Household.HouseholdId == handoff.PreviousHousehold || next.LotHome == null || next.Inventory == null) continue;
                    OccultGenie occult = lamp.mGenieDescription.OccultManager == null ? null :
                        lamp.mGenieDescription.OccultManager.GetOccultType(OccultTypes.Genie) as OccultGenie;
                    if (occult == null || !next.Inventory.TryToAdd(lamp)) continue;
                    occult.Summoner = next;
                    sHandoffs.Remove(handoff); break;
                }
            }
        }

        private static void RemoveOldControl(Sim genie)
        {
            genie.Interactions.RemoveAll(delegate(InteractionObjectPair pair) { return GenieWishMenu.IsLampWish(pair.InteractionDefinition); });
        }

        internal static void ResetSaved() { sHandoffs = null; }
    }
}
