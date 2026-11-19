using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchNativeCosts
    {
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string UpgradeClass = "MagicallyUpgrade";
        private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ActualValue = typeof(CommodityChange).GetField("mActualValue", BindingFlags.Instance | BindingFlags.Public);
        private static readonly Dictionary<string, FieldInfo> nativeCosts = new Dictionary<string, FieldInfo>();
        private static readonly Dictionary<ulong, WeakReference> charged = new Dictionary<ulong, WeakReference>();
        private static IHudModel model;
        private static bool active;

        internal static void Start()
        {
            // Watches every native wand drain so each native spell spends its fixed Arcana cost.
            active = true;
            Attach();
            if (model == null)
            {
                JournalBootstrap.RunLater(Attach);
            }
        }

        internal static void Stop()
        {
            active = false;
            if (model != null)
            {
                model.MagicMotiveChanged -= OnWandDrain;
                model = null;
            }
            charged.Clear();
        }

        private static void Attach()
        {
            if (!active || model != null)
            {
                return;
            }
            IResponder responder = Responder.Instance;
            model = responder == null ? null : responder.HudModel;
            if (model != null)
            {
                model.MagicMotiveChanged += OnWandDrain;
            }
        }

        private static void OnWandDrain()
        {
            // Charges the running native cast of the Sim whose wand drain just raised this event.
            try
            {
                Charge(CurrentTaskSim() ?? OnlyUnchargedCaster());
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Applying a fixed spell cost", error);
            }
        }

        private static Sim CurrentTaskSim()
        {
            // Returns the Sim whose script task is running the drain, which is the casting Witch.
            IScriptProxy proxy = Simulator.GetProxy(Simulator.CurrentTask);
            return proxy == null ? null : proxy.Target as Sim;
        }

        private static Sim OnlyUnchargedCaster()
        {
            // Falls back to the single Witch with an uncharged native cast, and gives up when there are several.
            Sim found = null;
            foreach (Sim sim in Sims3.Gameplay.Queries.GetObjects<Sim>())
            {
                if (Pending(sim) == null)
                {
                    continue;
                }
                if (found != null)
                {
                    return null;
                }
                found = sim;
            }
            return found;
        }

        private static InteractionInstance Pending(Sim sim)
        {
            // Returns the caster's running native spell cast when its fixed cost has not been charged yet.
            if (!MagicCasters.IsCaster(sim) || sim.Motives == null || sim.SimDescription == null)
            {
                return null;
            }
            InteractionInstance cast = sim.CurrentInteraction;
            NativeSpellCast known = cast as NativeSpellCast;
            if (known != null) cast = known.Running;
            if (cast == null || cast is WitchSimCast || SpellKnowledgeGates.EntryFor(cast.InteractionDefinition) == null)
            {
                return null;
            }
            WeakReference previous;
            return charged.TryGetValue(sim.SimDescription.SimDescriptionId, out previous) && previous.Target == cast ? null : cast;
        }

        private static void Charge(Sim sim)
        {
            // Replaces the native drain of the running cast with the fixed cost, once per cast.
            InteractionInstance cast = Pending(sim);
            if (cast == null)
            {
                return;
            }
            charged[sim.SimDescription.SimDescriptionId] = new WeakReference(cast);
            SpellEntry entry = SpellKnowledgeGates.EntryFor(cast.InteractionDefinition);
            if (MagicCasters.IsInnateCaster(sim))
            {
                WitchCastingRules innate = MagicCasters.ReadRules(sim);
                MagicCasters.SpendInnate(sim, innate, innate.FinalCost(entry.Cost, WitchSpell.For(entry), 1f));
                return;
            }
            MagicWand wand = WitchCastingContext.NativeWand(cast);
            float multiplier = (wand == null ? 1f : wand.MotiveDrainMultiplier) * WitchCastingContext.SituationalDrain(sim);
            WitchCastingRules rules = WitchCastingContext.ReadRules(sim);
            float change = rules.NativeCorrection(entry.Cost, WitchSpell.For(entry), NativeDrain(entry), multiplier,
                sim.Motives.GetMin(CommodityKind.MagicFatigue), sim.Motives.GetMax(CommodityKind.MagicFatigue));
            if (change != 0f)
            {
                sim.Motives.ChangeValue(CommodityKind.MagicFatigue, change);
            }
        }

        internal static float NativeDrain(SpellEntry entry)
        {
            // Returns the fatigue the native interaction drains before multipliers; Magically Upgrade drains none.
            if (entry.NativeClass == UpgradeClass)
            {
                return 0f;
            }
            FieldInfo cost;
            if (!nativeCosts.TryGetValue(entry.NativeClass, out cost))
            {
                Type type = typeof(MagicWand).GetNestedType(entry.NativeClass, BindingFlags.Public | BindingFlags.NonPublic);
                cost = type == null ? null : type.GetField("kMotiveDrain", StaticFields);
                nativeCosts[entry.NativeClass] = cost;
            }
            if (cost == null)
            {
                throw new InvalidOperationException("Missing native spell drain: " + entry.NativeClass);
            }
            return (float)cost.GetValue(null);
        }

        internal static void UseFixedAppleCost(InteractionInstance instance, Sim actor)
        {
            // Gives one Conjure Apple cast its own tuning copy whose power output is the fixed ability cost.
            InteractionObjectPair pair = instance == null ? null : instance.InteractionObjectPair;
            InteractionTuning tuning = pair == null ? null : pair.Tuning;
            if (tuning == null || tuning.mTradeoff == null || tuning.mTradeoff.mOutputs == null || !WitchCastingContext.IsWitch(actor)
                || actor.Motives == null)
            {
                return;
            }
            float fatigue = WitchCastingContext.ReadCapacityRules(actor).FatigueDrain(SpellCatalog.ConjureAppleCost,
                actor.Motives.GetMin(CommodityKind.MagicFatigue), actor.Motives.GetMax(CommodityKind.MagicFatigue));
            List<CommodityChange> outputs = new List<CommodityChange>();
            bool found = false;
            foreach (CommodityChange output in tuning.mTradeoff.mOutputs)
            {
                if (output == null || output.Commodity != CommodityKind.MagicFatigue)
                {
                    outputs.Add(output);
                    continue;
                }
                CommodityChange copy = (CommodityChange)CloneMethod.Invoke(output, null);
                ActualValue.SetValue(copy, output.mActualValue < 0f ? -fatigue : fatigue);
                outputs.Add(copy);
                found = true;
            }
            if (!found)
            {
                return;
            }
            InteractionTuning tuningCopy = (InteractionTuning)CloneMethod.Invoke(tuning, null);
            Tradeoff tradeoff = (Tradeoff)CloneMethod.Invoke(tuning.mTradeoff, null);
            tradeoff.mOutputs = outputs;
            tuningCopy.mTradeoff = tradeoff;
            instance.InteractionObjectPair = new KnowledgePair(pair.InteractionDefinition, pair.Target, tuningCopy);
        }
    }
}
