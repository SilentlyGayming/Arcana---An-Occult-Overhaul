using System;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class WishRendezvous
    {
        internal static bool Approach(Sim wisher, Sim genie)
        {
            if (wisher == null || genie == null || wisher.HasBeenDestroyed || genie.HasBeenDestroyed) return false;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (wisher.HasExitReason(ExitReason.Default) || genie.HasBeenDestroyed || genie.LotCurrent == null) return false;
                Route route = wisher.CreateRoute();
                route.PlanToPointRadialRange(genie.Position, 1f, 3.5f, RouteDistancePreference.PreferNearestToRouteDestination,
                    RouteOrientationPreference.TowardsObject, genie.LotCurrent.LotId, new int[] { genie.RoomId });
                route.DoRouteFail = false;
                if (wisher.DoRoute(route)) return true;
                wisher.RemoveExitReason(ExitReason.RouteFailed);
            }
            return false;
        }
    }

    [Persistable]
    public sealed class WishParticipant : Interaction<Sim, Sim>
    {
        public static readonly InteractionDefinition Singleton = new Definition();
        internal bool ApproachGenie;
        protected override bool Run()
        {
            bool synced = false;
            try
            {
                if (!StartSync(false, false, null, 0f, false)) return false;
                synced = true;
                if (ApproachGenie && !WishRendezvous.Approach(Actor, Target)) return false;
                Actor.LoopIdle();
                Actor.SynchronizationLevel = Sim.SyncLevel.Committed;
                if (!Actor.WaitForSynchronizationLevelWithSim(Target, Sim.SyncLevel.Committed, 60f)) return false;
                FinishLinkedInteraction(false);
                WaitForSyncComplete(10f);
                return true;
            }
            finally
            {
                if (synced) Actor.ClearSynchronizationData();
            }
        }
        public override void Cleanup()
        {
            if (Actor != null && Actor.SynchronizationTarget == Target) Actor.ClearSynchronizationData();
            base.Cleanup();
        }
        [DoesntRequireTuning]
        private sealed class Definition : InteractionDefinition<Sim, Sim, WishParticipant>
        {
            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip) { return true; }
            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            { return Localization.LocalizeString(actor.IsFemale, "Gameplay/Actors/Sim/GoMakeWish:InteractionName"); }
        }
    }
}
