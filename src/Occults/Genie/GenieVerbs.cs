using System;
using System.Collections.Generic;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.DreamsAndPromises;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    public sealed class LampRetreat : Interaction<Sim, Sim>
    {
        public static readonly InteractionDefinition Singleton = new Definition();

        protected override bool Run()
        {
            long start = SimClock.CurrentTicks;
            long end = start + SimClock.ConvertToTicks((float)CastingRules.RetreatMinutes, TimeUnit.Minutes);
            long step = SimClock.ConvertToTicks((float)CastingRules.RetreatStepMinutes, TimeUnit.Minutes);
            long half = Math.Max(1L, step / 2L);
            long next = start;
            bool slowed = false;
            bool finished = false;
            StandardEntry();
            try
            {
                Actor.PlaySoloAnimation("a_genie_clap_x", true, ProductVersion.EP6);
                while (SimClock.CurrentTicks < end && !Actor.HasExitReason(ExitReason.Default))
                {
                    long now = SimClock.CurrentTicks;
                    if (now >= next)
                    {
                        Actor.Motives.ChangeValue(CommodityKind.Energy, CastingRules.RetreatNeed);
                        Actor.Motives.ChangeValue(CommodityKind.Hygiene, CastingRules.RetreatNeed);
                        next += step;
                    }
                    bool freeze = ((now - start) / half) % 2L == 0L;
                    if (freeze != slowed)
                    {
                        FreezeDecay(Actor, freeze);
                        slowed = freeze;
                    }
                    Simulator.Sleep(30);
                }
                finished = SimClock.CurrentTicks >= end;
            }
            finally
            {
                if (slowed) FreezeDecay(Actor, false);
                StandardExit();
            }
            return finished;
        }

        private static void FreezeDecay(Sim sim, bool frozen)
        {
            foreach (CommodityKind need in new[] { CommodityKind.Hunger, CommodityKind.Bladder })
            {
                if (frozen) sim.Motives.FreezeDecay(need, true);
                else sim.Motives.RestoreDecay(need);
            }
        }

        public sealed class Definition : InteractionDefinition<Sim, Sim, LampRetreat>
        {
            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            {
                return JournalText.Get("Arcana.Genie.Retreat.Name");
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                return !autonomous && actor == target && GenieAbilities.IsGenie(actor) && actor.Motives != null &&
                    GeniePerks.Owns(actor.SimDescription, "PocketPalace") && MagicCasters.ReadRules(actor).Lampbound;
            }
        }
    }

    public sealed class HeartsDesire : ImmediateInteraction<Sim, Sim>
    {
        public static readonly InteractionDefinition Singleton = new Definition();

        protected override bool Run()
        {
            if (Target.SimDescription == null) return false;
            ActiveDreamNode desire = HeartsDesireLedger.Reveal(Target);
            if (desire == null)
            {
                SpellKnowledge.Notify(Actor, "Arcana.Genie.HeartsDesire.Nothing", Target.SimDescription.FirstName);
                return true;
            }
            HeartsDesireLedger.Ask(Actor.SimDescription, Target.SimDescription, desire);
            SpellKnowledge.Notify(Actor, "Arcana.Genie.HeartsDesire.Revealed", Target.SimDescription.FirstName, desire.Name);
            return true;
        }

        public sealed class Definition : ImmediateInteractionDefinition<Sim, Sim, HeartsDesire>
        {
            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            {
                return JournalText.Get("Arcana.Genie.HeartsDesire.Ask");
            }

            public override string[] GetPath(bool female)
            {
                return new[] { GenieMenu.Root(female) };
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (autonomous || actor == target || target.SimDescription == null || !GenieAbilities.IsGenie(actor)) return false;
                if (!GeniePerks.Owns(actor.SimDescription, "HeartsDesire")) return false;
                if (HeartsDesireLedger.Waiting(target.SimDescription))
                {
                    tooltip = delegate { return JournalText.Get("Arcana.Genie.HeartsDesire.Waiting"); };
                    return false;
                }
                return true;
            }
        }
    }

    internal static class HeartsDesireLedger
    {
        [Persistable]
        private sealed class Request
        {
            internal SimDescription Genie;
            internal ActiveDreamNode Desire;
            internal long Expires;
        }

        [PersistableStatic] private static Dictionary<ulong, Request> requests;

        internal static bool Waiting(SimDescription target)
        {
            Request request;
            return target != null && requests != null && requests.TryGetValue(target.SimDescriptionId, out request) && request.Expires > SimClock.CurrentTicks;
        }

        internal static ActiveDreamNode Reveal(Sim target)
        {
            DreamsAndPromisesManager manager = target.DreamsAndPromisesManager;
            if (manager == null) return null;
            List<ActiveDreamNode> open = new List<ActiveDreamNode>();
            foreach (ActiveNodeBase node in manager.ActiveNodes)
            {
                ActiveDreamNode dream = node as ActiveDreamNode;
                if (dream != null && dream.IsVisible && !dream.IsComplete && !dream.IsPromised) open.Add(dream);
            }
            return open.Count == 0 ? null : open[RandomUtil.GetInt(0, open.Count - 1)];
        }

        internal static void Ask(SimDescription genie, SimDescription target, ActiveDreamNode desire)
        {
            Request request = new Request();
            request.Genie = genie;
            request.Desire = desire;
            request.Expires = SimClock.CurrentTicks + SimClock.ConvertToTicks(CastingRules.HeartDays * 24f, TimeUnit.Hours);
            if (requests == null) requests = new Dictionary<ulong, Request>();
            requests[target.SimDescriptionId] = request;
        }

        internal static ListenerAction OnFulfilled(Event e)
        {
            Sim sim = e.Actor as Sim;
            DreamEvent dream = e as DreamEvent;
            Request request;
            if (sim == null || dream == null || sim.SimDescription == null || requests == null || !requests.TryGetValue(sim.SimDescription.SimDescriptionId, out request))
                return ListenerAction.Keep;
            if (request.Expires <= SimClock.CurrentTicks)
            {
                requests.Remove(sim.SimDescription.SimDescriptionId);
                return ListenerAction.Keep;
            }
            if (dream.ActiveDreamNode != request.Desire) return ListenerAction.Keep;
            requests.Remove(sim.SimDescription.SimDescriptionId);
            if (request.Genie != null && GeniePerks.Owns(request.Genie, "HeartsDesire"))
                request.Genie.IncrementLifetimeHappiness(dream.ActiveDreamNode.AchievementPoints * CastingRules.HeartReward);
            return ListenerAction.Keep;
        }

        internal static void Clear()
        {
            requests = null;
        }
    }

    internal enum Favor
    {
        Fortune,
        Vitality,
        Inspiration
    }

    public sealed class GeniesFavor : ImmediateInteraction<Sim, Sim>
    {
        public static readonly InteractionDefinition[] Singletons =
        {
            new Definition(Favor.Fortune), new Definition(Favor.Vitality), new Definition(Favor.Inspiration)
        };

        protected override bool Run()
        {
            Definition definition = InteractionDefinition as Definition;
            if (definition == null || GeniePower.Get(Actor.SimDescription) + 0.005f < CastingRules.FavorCost) return false;
            GeniePower.Change(Actor.SimDescription, -CastingRules.FavorCost);
            GenieWishes.GiveMoodlet(Target, definition.Buff, CastingRules.FavorHours);
            SpellKnowledge.Notify(Actor, "Arcana.Genie.Favor.Given", Target.SimDescription.FirstName);
            return true;
        }

        [Persistable]
        public sealed class Definition : ImmediateInteractionDefinition<Sim, Sim, GeniesFavor>
        {
            internal Favor Kind;

            public Definition() { }

            internal Definition(Favor kind)
            {
                Kind = kind;
            }

            internal BuffNames Buff
            {
                get { return Kind == Favor.Fortune ? BuffNames.FeelingLucky : Kind == Favor.Vitality ? BuffNames.Energized : BuffNames.Inspired; }
            }

            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            {
                return JournalText.Get(Kind == Favor.Fortune ? "Arcana.Genie.Favor.Fortune"
                    : Kind == Favor.Vitality ? "Arcana.Genie.Favor.Vitality" : "Arcana.Genie.Favor.Inspiration");
            }

            public override string[] GetPath(bool female)
            {
                return new[] { GenieMenu.Root(female), JournalText.Get("Arcana.Genie.Favor.Menu") };
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (autonomous || actor == target || target.SimDescription == null || !target.SimDescription.IsHuman || !GenieAbilities.IsGenie(actor)) return false;
                if (!GeniePerks.Owns(actor.SimDescription, "HeartsDesire")) return false;
                CastingRules rules = MagicCasters.ReadRules(actor);
                if (rules.Chained || GeniePower.Get(actor.SimDescription) + 0.005f < CastingRules.FavorCost)
                {
                    tooltip = delegate { return JournalText.Get(rules.Chained ? "Arcana.Genie.Wish.Chained" : "Arcana.Genie.Ability.NotEnoughPower"); };
                    return false;
                }
                return true;
            }
        }
    }
}
