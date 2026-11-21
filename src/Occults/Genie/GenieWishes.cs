using System;
using System.Collections.Generic;
using Sims3.Gameplay;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal enum FatePrice
    {
        DeathsDue,
        LifeForAWish,
        PowerSupreme,
        ForgottenBonds,
        BrokenVessel,
        ChainsOfOld,
        FortunesDebt,
        FatesMisfortune,
        WishExhaustion
    }

    internal static class GenieWishes
    {
        private const float CloseLiking = 60f;
        private static readonly CommodityKind[] Needs =
        {
            CommodityKind.Hunger, CommodityKind.Energy, CommodityKind.Bladder, CommodityKind.Hygiene, CommodityKind.Social, CommodityKind.Fun
        };
        private static readonly FatePrice[] Prices =
        {
            FatePrice.DeathsDue, FatePrice.LifeForAWish, FatePrice.PowerSupreme, FatePrice.ForgottenBonds, FatePrice.BrokenVessel,
            FatePrice.ChainsOfOld, FatePrice.FortunesDebt, FatePrice.FatesMisfortune, FatePrice.WishExhaustion
        };
        private static readonly float[] Chances = { 1f, 2f, 5f, 7f, 15f, 15f, 20f, 15f, 20f };

        internal static float Cost(WishMode mode)
        {
            return mode == WishMode.Own ? CasterProfile.Genie.Capacity(Rank.HighWitch) : CastingRules.WishCost;
        }

        internal static float SuccessChance(Sim caster, CastingRules rules)
        {
            float needPenalty;
            int empty;
            WitchCastingContext.CountNeeds(caster, out needPenalty, out empty, null);
            float accuracy = rules.Accuracy(new MagicSpell(4, MagicSpellKind.Spell), rules.InnateAccuracy,
                WitchCastingContext.EmotionPenalty(caster), needPenalty, empty);
            return Math.Max(0f, Math.Min(100f, accuracy + (rules.GenieLaw ? CastingRules.GenieLawWishBonus : 0f)));
        }

        internal static float Cost(WishMode mode, CastingRules rules)
        {
            return mode == WishMode.Lamp ? 0f : mode == WishMode.Own ? Cost(mode) : Cost(mode) * rules.AbilityCostFactor;
        }

        internal static bool Allowed(Sim actor, Sim target, WishMode mode, ref GreyedOutTooltipCallback tooltip)
        {
            if (mode == WishMode.Lamp) return WishLamp.Allowed(target, actor);
            if (actor == null || target == null || actor.SimDescription == null || !GenieAbilities.IsGenie(actor) || WishLamp.Lamp(actor) != null) return false;
            CastingRules rules = MagicCasters.ReadRules(actor);
            if (mode == WishMode.Own ? !rules.Counterattack || actor != target : actor == target || target.SimDescription == null ||
                !target.SimDescription.IsHuman || !rules.GenieLaw || rules.Rank < Rank.HighWitch) return false;
            GenieMagicSkill skill = GeniePower.Read(actor.SimDescription, false);
            if (skill == null || mode == WishMode.Guaranteed && (!rules.LawOfTheLamp || skill.GuaranteeReady > SimClock.CurrentTicks)) return false;
            if (rules.Chained)
            {
                tooltip = delegate { return JournalText.Get("Arcana.Genie.Wish.Chained"); };
                return false;
            }
            if (GeniePower.Get(actor.SimDescription) + 0.005f < Cost(mode, rules))
            {
                tooltip = delegate { return JournalText.Get("Arcana.Genie.Ability.NotEnoughPower"); };
                return false;
            }
            return true;
        }

        internal static bool Grant(Sim actor, Sim target, WishKind kind, WishMode mode, WishSelection selection)
        {
            GreyedOutTooltipCallback tooltip = null;
            if (!Allowed(actor, target, mode, ref tooltip)) return false;
            Sim genie = mode == WishMode.Lamp ? target : actor;
            Sim wisher = mode == WishMode.Lamp ? actor : target;
            if (!WishEffects.Eligible(genie, wisher, kind, mode) || !WishEffects.ValidateSelection(wisher, kind, selection)) return false;
            CastingRules rules = MagicCasters.ReadRules(genie);
            GenieMagicSkill skill = mode == WishMode.Lamp ? null : GeniePower.Read(genie.SimDescription, false);
            bool guaranteed = mode == WishMode.Guaranteed || WishRules.GuaranteeAvailable(mode == WishMode.Other, rules.LawOfTheLamp, skill == null ? long.MaxValue : skill.GuaranteeReady, SimClock.CurrentTicks);
            float accuracy = mode == WishMode.Lamp ? CastingRules.WishSuccess : SuccessChance(genie, rules);
            bool succeeded = mode == WishMode.Own || guaranteed || RandomUtil.RandomChance(accuracy);
            bool backlash = false;
            if (mode != WishMode.Own && mode != WishMode.Lamp && !guaranteed)
            {
                backlash = MagicCasters.RollWishBacklash(rules, accuracy, ref succeeded);
            }
            bool applied;
            if (mode == WishMode.Own)
            {
                ApplyOwnWish(actor, kind); applied = true;
            }
            else applied = WishEffects.Apply(genie, wisher, kind, succeeded, selection);
            if (!applied)
            {
                SpellKnowledge.Notify(actor, "Arcana.Genie.Wishes.Unavailable");
                return false;
            }
            if (mode == WishMode.Lamp) WishLamp.Commit(genie, wisher, kind, succeeded);
            else
            {
                bool penalty = !guaranteed && mode != WishMode.Own && MagicCasters.CommitWishBacklash(genie, rules, succeeded, backlash);
                MagicCasters.SpendInnate(genie, rules, Cost(mode, rules));
                if (guaranteed) skill.GuaranteeReady = WishState.After(24f);
                if (!succeeded) Exhaust(skill, rules);
                if (penalty) WitchMagicRecovery.Fizzle(genie, rules);
            }
            if (mode != WishMode.Lamp) JournalSounds.Play("sting_make_wish");
            if (kind == WishKind.FreeGenie && !succeeded)
                SpellKnowledge.Notify(actor, "Arcana.Genie.Wish.Result.Escaped");
            else if (kind == WishKind.Fortune && !succeeded)
                SpellKnowledge.Notify(actor, "Arcana.Genie.Wish.Result.Repo");
            else if (mode != WishMode.Lamp)
                SpellKnowledge.Notify(actor, kind == WishKind.CheatDeath || succeeded ? "Arcana.Genie.Wish.Result.Granted" : "Arcana.Genie.Wish.Result.Failed");
            if (mode == WishMode.Own) PayFatesPrice(actor, skill, rules);
            return true;
        }

        private static void ApplyOwnWish(Sim actor, WishKind kind)
        {
            if (kind == WishKind.Fortune) actor.Household.ModifyFamilyFunds(CastingRules.SelfWishFortune);
            else if (kind == WishKind.Vitality) Refill(actor);
            else GiveMoodlet(actor, BuffNames.Inspired, CastingRules.FavorHours);
        }

        internal static void Refill(Sim sim)
        {
            if (sim.Motives == null) return;
            foreach (CommodityKind need in Needs)
            {
                if (sim.Motives.HasMotive(need)) sim.Motives.ChangeValue(need, sim.Motives.GetMax(need) - sim.Motives.GetValue(need));
            }
        }

        internal static void GiveMoodlet(Sim sim, BuffNames buff, float hours)
        {
            if (sim == null || sim.BuffManager == null) return;
            BuffInstance applied = sim.BuffManager.GetElement(buff);
            if (applied == null && LuckMoodlet.Covers(buff))
            {
                LuckMoodlet.Add(sim, buff, hours * 60f, Origin.FromGenie);
                return;
            }
            sim.BuffManager.AddElement(buff, Origin.FromGenie);
            applied = sim.BuffManager.GetElement(buff);
            if (applied != null) applied.TimeoutCount = hours * 60f;
        }

        private static void Exhaust(GenieMagicSkill skill, CastingRules rules)
        {
            float days = CastingRules.ExhaustionDays * (rules.GenieLaw ? 1f - CastingRules.GenieLawExhaustion : 1f);
            skill.ExhaustEnds = SimClock.CurrentTicks + SimClock.ConvertToTicks(days * 24f, TimeUnit.Hours);
            JournalBootstrap.RefreshMeter();
        }

        private static void PayFatesPrice(Sim actor, GenieMagicSkill skill, CastingRules rules)
        {
            FatePrice price = FatePrice.PowerSupreme;
            float remaining = 100f;
            for (int i = 0; i < Prices.Length; i++)
            {
                if (RandomUtil.RandomChance(Chances[i] / remaining * 100f))
                {
                    price = Prices[i];
                    break;
                }
                remaining -= Chances[i];
            }
            long now = SimClock.CurrentTicks;
            switch (price)
            {
                case FatePrice.LifeForAWish:
                    Sim victim = CloseSim(actor);
                    if (victim == null) return;
                    SpellKnowledge.Notify(actor, "Arcana.Genie.Fate.LifeForAWish", Name(victim));
                    victim.Kill(SimDescription.DeathType.OldAge);
                    return;
                case FatePrice.ForgottenBonds:
                    ForgetBond(actor);
                    return;
                case FatePrice.BrokenVessel:
                    skill.VesselEnds = now + SimClock.ConvertToTicks(CastingRules.VesselDays * 24f, TimeUnit.Hours);
                    break;
                case FatePrice.ChainsOfOld:
                    skill.ChainsEnds = now + SimClock.ConvertToTicks(CastingRules.ChainsHours, TimeUnit.Hours);
                    break;
                case FatePrice.FortunesDebt:
                    Household household = actor.SimDescription.Household;
                    if (household != null) household.ModifyFamilyFunds(-(int)(household.FamilyFunds * CastingRules.DebtShare));
                    break;
                case FatePrice.FatesMisfortune:
                    GiveMoodlet(actor, BuffNames.FeelingUnlucky, CastingRules.MisfortuneHours);
                    break;
                case FatePrice.WishExhaustion:
                    Exhaust(skill, rules);
                    break;
            }
            SpellKnowledge.Notify(actor, price == FatePrice.DeathsDue ? "Arcana.Genie.Fate.DeathsDue"
                : price == FatePrice.BrokenVessel ? "Arcana.Genie.Fate.BrokenVessel"
                : price == FatePrice.ChainsOfOld ? "Arcana.Genie.Fate.ChainsOfOld"
                : price == FatePrice.FortunesDebt ? "Arcana.Genie.Fate.FortunesDebt"
                : price == FatePrice.FatesMisfortune ? "Arcana.Genie.Fate.FatesMisfortune"
                : price == FatePrice.WishExhaustion ? "Arcana.Genie.Fate.WishExhaustion" : "Arcana.Genie.Fate.PowerSupreme");
            JournalBootstrap.RefreshMeter();
            if (price == FatePrice.DeathsDue) actor.Kill(SimDescription.DeathType.OldAge);
        }

        private static Sim CloseSim(Sim genie)
        {
            List<Sim> close = new List<Sim>();
            foreach (Relationship relationship in Relationship.Get(genie.SimDescription))
            {
                Sim other = Other(genie, relationship);
                if (other != null && relationship.LTR != null && relationship.LTR.Liking >= CloseLiking) close.Add(other);
            }
            return close.Count == 0 ? null : close[RandomUtil.GetInt(0, close.Count - 1)];
        }

        private static void ForgetBond(Sim genie)
        {
            Relationship strongest = null;
            foreach (Relationship relationship in Relationship.Get(genie.SimDescription))
            {
                Sim other = Other(genie, relationship);
                if (other == null || relationship.LTR == null || relationship.LTR.Liking <= 0f || IsFamily(genie, other)) continue;
                if (strongest == null || relationship.LTR.Liking > strongest.LTR.Liking) strongest = relationship;
            }
            if (strongest == null) return;
            Sim forgotten = Other(genie, strongest);
            strongest.LTR.SetLiking(0f);
            SpellKnowledge.Notify(genie, "Arcana.Genie.Fate.ForgottenBonds", Name(forgotten));
        }

        private static Sim Other(Sim genie, Relationship relationship)
        {
            SimDescription description = relationship.GetOtherSimDescription(genie.SimDescription);
            Sim other = description == null ? null : description.CreatedSim;
            return other == null || other == genie || other.HasBeenDestroyed || other.SimDescription == null || other.SimDescription.IsDead ||
                !other.SimDescription.IsHuman ? null : other;
        }

        private static bool IsFamily(Sim genie, Sim other)
        {
            Genealogy mine = genie.Genealogy;
            Genealogy theirs = other.Genealogy;
            return mine != null && theirs != null && (mine.IsBloodRelated(theirs) || mine.IsStepRelated(theirs) || mine.Spouse == theirs ||
                mine.IsSufficientlyRelatedToRuleOutRomance(theirs)) ||
                (genie.SimDescription.Household != null && genie.SimDescription.Household == other.SimDescription.Household);
        }

        private static string Name(Sim sim)
        {
            return sim.SimDescription == null ? string.Empty : sim.SimDescription.FirstName;
        }
    }

    [Persistable]
    public sealed class GrantWish : Interaction<Sim, Sim>
    {
        public static readonly InteractionDefinition[] Singletons = { new Definition(WishMode.Other), new Definition(WishMode.Own) };
        public static readonly InteractionDefinition LampSingleton = new Definition(WishMode.Lamp);
        private bool committed;
        private bool executing;

        protected override bool Run()
        {
            Definition definition = InteractionDefinition as Definition;
            if (definition == null || executing || committed) return false;
            GreyedOutTooltipCallback tooltip = null;
            if (!GenieWishes.Allowed(Actor, Target, definition.Mode, ref tooltip)) return false;
            Sim genie = definition.Mode == WishMode.Lamp ? Target : Actor;
            Sim wisher = definition.Mode == WishMode.Lamp ? Actor : Target;
            WishKind kind = definition.Kind;
            if (definition.UsePicker)
            {
                GenieWishChoice choice = GenieWishDialog.Show(genie, wisher, definition.Mode);
                if (choice == null) return false;
                kind = choice.Kind;
            }
            if (!WishEffects.Eligible(genie, wisher, kind, definition.Mode)) return false;
            WishSelection selection = WishEffects.Select(wisher, kind);
            if (selection == null) return false;
            executing = true;
            bool entry = false;
            bool synced = false;
            bool updates = false;
            bool result = false;
            WishParticipant participant = null;
            Sims3.Gameplay.Gameflow.Singleton.DisableSave(this, "Gameplay/ActorSystems/OccultGenie/MakeWish:InteractionName");
            try
            {
                if (Actor != Target)
                {
                    participant = WishParticipant.Singleton.CreateInstance(Actor, Target, GetPriority(), Autonomous, CancellableByPlayer) as WishParticipant;
                    if (participant == null) return false;
                    participant.ApproachGenie = definition.Mode != WishMode.Lamp;
                    LinkedInteractionInstance = participant;
                    if (!Target.InteractionQueue.AddNext(participant)) return false;
                    if (!StartSync(true, false, null, 0f, false)) return false;
                    synced = true;
                    Actor.LoopIdle();
                    if (definition.Mode == WishMode.Lamp && !WishRendezvous.Approach(Actor, Target)) return false;
                    Actor.SynchronizationLevel = Sim.SyncLevel.Routed;
                    if (!Actor.WaitForSynchronizationLevelWithSim(Target, Sim.SyncLevel.Committed, 60f)) return false;
                    Actor.SynchronizationLevel = Sim.SyncLevel.Committed;
                }
                if (!WishEffects.Eligible(genie, wisher, kind, definition.Mode) || !WishEffects.ValidateSelection(wisher, kind, selection)) return false;
                StandardEntry(); entry = true;
                BeginCommodityUpdates(); updates = true;
                if (Actor != Target) Actor.RouteTurnToFace(Target.Position);
                genie.PlaySoloAnimation("a_genie_clap_x", true, ProductVersion.EP6);
                if (Actor.HasExitReason(ExitReason.Default)) return false;
                VisualEffect effect = VisualEffect.Create("ep6GenieAppearSmoke_main");
                if (effect != null)
                {
                    effect.SetPosAndOrient(wisher.Position, Vector3.UnitX, Vector3.UnitZ);
                    effect.SubmitOneShotEffect(VisualEffect.TransitionType.SoftTransition);
                }
                committed = true;
                result = GenieWishes.Grant(Actor, Target, kind, definition.Mode, selection);
                return result;
            }
            catch (ResetException) { throw; }
            catch (Exception error)
            {
                JournalBootstrap.Report("Granting Genie wish " + kind, error);
                SpellKnowledge.Notify(Actor, "Arcana.Genie.Wishes.Unavailable");
                return false;
            }
            finally
            {
                try
                {
                    if (participant != null)
                    {
                        if (synced)
                        {
                            FinishLinkedInteraction(true);
                            if (Simulator.CheckYieldingContext(false)) WaitForSyncComplete(10f);
                        }
                        else if (Target != null && Target.InteractionQueue != null)
                            Target.InteractionQueue.CancelInteraction(participant, false);
                    }
                }
                finally
                {
                    try
                    {
                        if (synced) Actor.ClearSynchronizationData();
                        if (updates) EndCommodityUpdates(result);
                        if (entry) StandardExit();
                    }
                    finally
                    {
                        Sims3.Gameplay.Gameflow.Singleton.EnableSave(this);
                        executing = false;
                    }
                }
            }
        }

        public override void Cleanup()
        {
            Sims3.Gameplay.Gameflow.Singleton.EnableSave(this);
            base.Cleanup();
        }

        [Persistable, DoesntRequireTuning]
        public sealed class Definition : InteractionDefinition<Sim, Sim, GrantWish>
        {
            internal WishKind Kind;
            internal WishMode Mode;
            internal bool UsePicker;
            public Definition() { }
            internal Definition(WishMode mode) { Mode = mode; UsePicker = true; }
            internal Definition(WishKind kind, WishMode mode) { Kind = kind; Mode = mode; }

            protected override string GetInteractionName(Sim actor, Sim target, InteractionObjectPair pair)
            {
                if (UsePicker)
                {
                    if (Mode == WishMode.Lamp)
                    {
                        Sims3.Gameplay.Objects.Rewards.GenieLamp lamp = WishLamp.Lamp(target);
                        return lamp == null ? JournalText.Get("Arcana.Genie.Wish.Dialog.MakeWish") : lamp.GetMakeWishInteractionName();
                    }
                    return JournalText.Get(Mode == WishMode.Own ? "Arcana.Genie.Wish.Menu.Own" : "Arcana.Genie.Wish.Menu.Other");
                }
                if (Mode == WishMode.Own) return JournalText.Get(Kind == WishKind.Fortune ? "Arcana.Genie.Wish.Fortune" : Kind == WishKind.Vitality ? "Arcana.Genie.Wish.Vitality" : "Arcana.Genie.Wish.Heart.Own");
                Sim wisher = Mode == WishMode.Lamp ? actor : target;
                return JournalText.Get(Kind == WishKind.WorldPeace && wisher != null && wisher.HasTrait(TraitNames.Evil)
                    ? "Arcana.Genie.Wishes.WorldMisery.Name" : WishCatalog.Find(Kind).NameKey);
            }

            public override string[] GetPath(bool female)
            {
                if (Mode == WishMode.Lamp) return new string[0];
                return new[] { GenieMenu.Root(female) };
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                if (autonomous || !GenieWishes.Allowed(actor, target, Mode, ref tooltip)) return false;
                Sim genie = Mode == WishMode.Lamp ? target : actor;
                Sim wisher = Mode == WishMode.Lamp ? actor : target;
                return UsePicker ? GenieWishDialog.HasAvailable(genie, wisher, Mode) : WishEffects.Eligible(genie, wisher, Kind, Mode);
            }
        }
    }
}
