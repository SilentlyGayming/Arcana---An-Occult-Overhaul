using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Controllers;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Interfaces;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal static class SpellKnowledgeGates
    {
        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static bool installed;
        private static readonly Dictionary<Type, InteractionDefinition> wrappers = new Dictionary<Type, InteractionDefinition>();
        private static readonly Dictionary<string, TargetSpell.Definition> arcana = new Dictionary<string, TargetSpell.Definition>();
        private static readonly CommodityKind[] Motives = { CommodityKind.Bladder, CommodityKind.Hunger, CommodityKind.Energy,
            CommodityKind.Hygiene, CommodityKind.Fun, CommodityKind.Social };
        private static readonly FieldInfo SpellLevels = typeof(MagicWand).GetField("kSpellLevels", StaticFields);
        private static readonly FieldInfo CharmLevels = typeof(MagicWand).GetField("kMotiveCharmUnlockLevels", StaticFields);
        private static readonly FieldInfo CurseLevels = typeof(MagicWand).GetField("kMotiveCurseUnlockLevels", StaticFields);

        internal static bool Installed { get { return installed; } }

        internal static bool UsesArcanaCasting(string name) { return arcana.ContainsKey(name); }
        internal static TargetSpell.Definition Casting(string name)
        {
            TargetSpell.Definition definition;
            return arcana.TryGetValue(name, out definition) ? definition : null;
        }

        internal static void Prepare()
        {
            // Captures the native spell definitions and replaces their singletons with knowledge-gated wrappers.
            SpellKnowledgeRegistration.Register();
            if (installed) return;
            if (arcana.Count == 0)
            {
                arcana.Add("CastGoodLuckCharm", new TargetSpell.GoodLuckCharm());
                arcana.Add("CastLoveCharm", new TargetSpell.LoveCharm());
                arcana.Add("CastIceBlast", new TargetSpell.IceBlast());
                arcana.Add("CastSunlightCharm", new TargetSpell.SunlightCharm());
                arcana.Add("CastPestilenceCurse", new TargetSpell.PestilenceCurse());
                arcana.Add("CastHauntingCurse", new TargetSpell.HauntingCurse());
            }
            foreach (TargetSpell.Definition definition in arcana.Values)
            {
                if (definition.Spell.Stock == null) throw new InvalidOperationException("Missing native casting descriptor.");
            }
            string[] names = { "CastGoodLuckCharm", "CastLoveCharm", "CastSunlightCharm", "CastPestilenceCurse",
                "CastFireBlast", "CastIceBlast", "CastToadify", "CastHauntingCurse", "CastMotiveCharm", "CastMotiveCurse",
                "CastFireBlastTerrain", "CastIceBlastTerrain", "CastIceBlastPlumbing", "CastFireBlastOnStatue",
                "CastSunlightCharmOnStatue", "CastReanimate", "CastConvert", "CastRestoration", "MagicallyUpgrade" };
            List<KeyValuePair<FieldInfo, InteractionDefinition>> originals = new List<KeyValuePair<FieldInfo, InteractionDefinition>>();
            foreach (string name in names)
            {
                Type type = typeof(MagicWand).GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo singleton = type == null ? null : type.GetField("Singleton", StaticFields);
                if (singleton == null) throw new InvalidOperationException("Missing native spell singleton: " + name);
                originals.Add(new KeyValuePair<FieldInfo, InteractionDefinition>(singleton, (InteractionDefinition)singleton.GetValue(null)));
            }
            FieldInfo apple = typeof(TraitFunctions.ConjureApple).GetField("Singleton", StaticFields);
            if (apple == null) throw new InvalidOperationException("Missing native Conjure Apple singleton.");
            originals.Add(new KeyValuePair<FieldInfo, InteractionDefinition>(apple, (InteractionDefinition)apple.GetValue(null)));
            try
            {
                foreach (KeyValuePair<FieldInfo, InteractionDefinition> original in originals)
                {
                    InteractionDefinition wrapper = Wrap(original.Value);
                    wrappers[original.Value.GetType()] = wrapper;
                    original.Key.SetValue(null, wrapper);
                }
            }
            catch
            {
                foreach (KeyValuePair<FieldInfo, InteractionDefinition> original in originals)
                    original.Key.SetValue(null, original.Value);
                wrappers.Clear();
                throw;
            }
            installed = true;
        }

        internal static InteractionDefinition Wrap(InteractionDefinition native)
        {
            // Wraps a native spell in a knowledge gate, keeping its fire-usable marker, and Conjure Apple in its fixed-cost wrapper.
            if (native is KnowledgeSpellDefinition || native is InnateAbilityDefinition) return native;
            if (native.GetType().DeclaringType == typeof(TraitFunctions.ConjureApple)) return new InnateAbilityDefinition(native);
            return native is IUsableDuringFire ? new FireKnowledgeSpellDefinition(native) : new KnowledgeSpellDefinition(native);
        }

        internal static void InstallFor(GameObject target)
        {
            // Swaps an object's spell interactions for gated wrappers and cancels queued casts of unknown spells.
            if (target == null || target.HasBeenDestroyed) return;
            Replace(target.Interactions);
            if (target.ItemComp != null) Replace(target.ItemComp.InteractionsInventory);
            Sim sim = target as Sim;
            if (sim != null) Replace(sim.SoloInteractions);
            SpellKnowledge.EnsureStarting(sim);
            if (WitchCastingContext.IsWitch(sim) && sim.InteractionQueue != null)
            {
                List<InteractionInstance> queued = new List<InteractionInstance>(sim.InteractionQueue.InteractionList);
                foreach (InteractionInstance interaction in queued)
                {
                    SpellEntry entry = EntryFor(interaction.InteractionDefinition);
                    if (entry != null && !SpellKnowledge.Knows(sim, entry))
                        sim.InteractionQueue.CancelInteraction(interaction, false);
                }
            }
        }

        private static void Replace(List<InteractionObjectPair> pairs)
        {
            if (pairs == null) return;
            for (int i = 0; i < pairs.Count; i++)
            {
                InteractionObjectPair pair = pairs[i];
                InteractionDefinition wrapper;
                if (wrappers.TryGetValue(pair.InteractionDefinition.GetType(), out wrapper))
                    pairs[i] = new KnowledgePair(wrapper, pair.Target, pair.Tuning);
            }
        }

        internal static SpellEntry EntryFor(InteractionDefinition definition)
        {
            // Resolves a native spell definition, including target and motive variants, to its catalog entry.
            KnowledgeSpellDefinition wrapper = definition as KnowledgeSpellDefinition;
            if (wrapper != null) definition = wrapper.Native;
            if (definition == null) return null;
            Type owner = definition.GetType().DeclaringType;
            if (owner == null || owner.DeclaringType != typeof(MagicWand)) return null;
            string name = owner.Name;
            if (name == "CastFireBlastTerrain" || name == "CastFireBlastOnStatue") name = "CastFireBlast";
            if (name == "CastIceBlastTerrain" || name == "CastIceBlastPlumbing") name = "CastIceBlast";
            if (name == "CastSunlightCharmOnStatue") name = "CastSunlightCharm";
            CommodityKind motive = CommodityKind.None;
            MagicWand.CastMotiveCharm.Definition charm = definition as MagicWand.CastMotiveCharm.Definition;
            MagicWand.CastMotiveCurse.Definition curse = definition as MagicWand.CastMotiveCurse.Definition;
            if (charm != null) motive = charm.mMotiveAffected;
            if (curse != null) motive = curse.mMotiveAffected;
            if (motive == CommodityKind.VampireThirst) motive = CommodityKind.Hunger;
            foreach (SpellEntry entry in SpellCatalog.Entries)
            {
                if (entry.NativeClass != name) continue;
                if (entry.MotiveIndex < 0 || MotiveAt(entry.MotiveIndex) == motive) return entry;
            }
            return null;
        }

        private static CommodityKind MotiveAt(int index)
        {
            return Motives[index];
        }

        internal static InteractionTestResult TestNative(InteractionDefinition native, ref InteractionInstanceParameters parameters,
            ref GreyedOutTooltipCallback tooltip)
        {
            // Runs the native availability test for known spells with the old skill-level unlocks lifted.
            Sim actor = parameters.Actor as Sim;
            InteractionInstanceParameters original = parameters.CloneForNewInteraction(native);
            if (!WitchCastingContext.IsWitch(actor)) return native.Test(ref original, ref tooltip);
            SpellEntry entry = EntryFor(native);
            if (entry == null || !SpellKnowledge.Knows(actor, entry)) return InteractionTestResult.Def_TestFailed;
            Type owner = native.GetType().DeclaringType;
            bool hands = owner != null && UsesArcanaCasting(owner.Name) && !MagicWand.HasWand(actor)
                && WitchCastingContext.HasPracticallyMagic(actor);
            OccultWitch witch = hands ? actor.OccultManager.GetOccultType(OccultTypes.Witch) as OccultWitch : null;
            bool wasCasting = witch != null && witch.IsCastingSpell;
            try
            {
                if (witch != null) witch.IsCastingSpell = true;
                using (new AvailabilityScope(false)) return native.Test(ref original, ref tooltip);
            }
            finally
            {
                if (witch != null) witch.IsCastingSpell = wasCasting;
            }
        }

        private sealed class AvailabilityScope : IDisposable
        {
            private readonly FieldInfo[] fields;
            private readonly int[][] originals;
            internal AvailabilityScope(bool motives)
            {
                // Zeroes the native unlock levels for one synchronous availability check.
                fields = motives ? new FieldInfo[] { SpellLevels, CharmLevels, CurseLevels } : new FieldInfo[] { SpellLevels };
                originals = new int[fields.Length][];
                for (int i = 0; i < fields.Length; i++)
                {
                    originals[i] = (int[])fields[i].GetValue(null);
                    fields[i].SetValue(null, new int[originals[i].Length]);
                }
            }
            public void Dispose()
            {
                // Restores the native unlock level arrays.
                for (int i = fields.Length - 1; i >= 0; i--) fields[i].SetValue(null, originals[i]);
            }
        }

        internal static void Expand(InteractionDefinition native, InteractionObjectPair pair, IActor actor, List<InteractionObjectPair> results)
        {
            // Expands native motive spells and keeps only learned variants with their real difficulty.
            List<InteractionObjectPair> expanded = new List<InteractionObjectPair>();
            Sim sim = actor as Sim;
            if (WitchCastingContext.IsWitch(sim))
            {
                using (new AvailabilityScope(true)) native.AddInteractions(new InteractionObjectPair(native, pair.Target), actor, expanded);
            }
            else native.AddInteractions(new InteractionObjectPair(native, pair.Target), actor, expanded);
            foreach (InteractionObjectPair item in expanded)
            {
                SpellEntry entry = EntryFor(item.InteractionDefinition);
                if (WitchCastingContext.IsWitch(sim) && (entry == null || !SpellKnowledge.Knows(sim, entry))) continue;
                MagicWand.CastMotiveCharm.Definition charm = item.InteractionDefinition as MagicWand.CastMotiveCharm.Definition;
                MagicWand.CastMotiveCurse.Definition curse = item.InteractionDefinition as MagicWand.CastMotiveCurse.Definition;
                if (entry != null && entry.MotiveIndex >= 0)
                {
                    if (charm != null) charm.mSpellLevel = ((int[])CharmLevels.GetValue(null))[entry.MotiveIndex];
                    if (curse != null) curse.mSpellLevel = ((int[])CurseLevels.GetValue(null))[entry.MotiveIndex];
                }
                results.Add(new KnowledgePair(Wrap(item.InteractionDefinition), item.Target, item.Tuning));
            }
        }
    }

    internal sealed class KnowledgePair : InteractionObjectPair
    {
        internal KnowledgePair(InteractionDefinition definition, IGameObject target, InteractionTuning tuning) : base(definition, target, tuning) { }
    }

    [Persistable]
    internal class KnowledgeSpellDefinition : InteractionDefinition, IHasTraitIcon, IHasMenuPathIcon, IOverridesVisualType
    {
        internal InteractionDefinition Native;
        internal KnowledgeSpellDefinition(InteractionDefinition native) { Native = native; }
        public InteractionVisualTypes GetVisualType { get { return ((IOverridesVisualType)Native).GetVisualType; } }
        public ResourceKey GetTraitIcon(Sim sim, GameObject target) { return ((IHasTraitIcon)Native).GetTraitIcon(sim, target); }
        public ResourceKey GetPathIcon(Sim sim, GameObject target) { return ((IHasMenuPathIcon)Native).GetPathIcon(sim, target); }
        public string[] GetPath(bool female) { return Native.GetPath(female); }
        public string GetInteractionName(IActor actor, IGameObject target, InteractionObjectPair pair)
        { return Native.GetInteractionName(actor, target, new InteractionObjectPair(Native, target)); }
        public string GetInteractionName(ref InteractionInstanceParameters parameters)
        { InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native); return Native.GetInteractionName(ref original); }
        public InteractionTestResult Test(ref InteractionInstanceParameters parameters, ref GreyedOutTooltipCallback tooltip)
        { return SpellKnowledgeGates.TestNative(Native, ref parameters, ref tooltip); }
        public void AddInteractions(InteractionObjectPair pair, IActor actor, List<InteractionObjectPair> results)
        { SpellKnowledgeGates.Expand(Native, pair, actor, results); }
        public InteractionInstance CreateInstanceFromParameters(ref InteractionInstanceParameters parameters)
        {
            // Creates the Arcana or native cast for a known spell.
            Sim actor = parameters.Actor as Sim;
            if (WitchCastingContext.IsWitch(actor) && !SpellKnowledge.Knows(actor, SpellKnowledgeGates.EntryFor(Native))) return null;
            Type owner = Native.GetType().DeclaringType;
            TargetSpell.Definition casting = owner == null ? null : SpellKnowledgeGates.Casting(owner.Name);
            InteractionDefinition implementation = WitchCastingContext.IsWitch(actor) && casting != null ? casting : Native;
            InteractionInstanceParameters original = parameters.CloneForNewInteraction(implementation);
            original.SetInteractionObjectPair(new KnowledgePair(implementation, parameters.Target,
                new InteractionObjectPair(Native, parameters.Target).Tuning));
            return implementation.CreateInstanceFromParameters(ref original);
        }
        public InteractionInstance CreateInstance(IGameObject target, IActor actor, InteractionPriority priority, bool autonomous, bool cancellable)
        {
            InteractionInstanceParameters parameters = new InteractionInstanceParameters(new InteractionObjectPair(Native, target), actor, priority, autonomous, cancellable);
            return CreateInstanceFromParameters(ref parameters);
        }
        public InteractionInstance CreateInstanceWithCallbacks(IGameObject target, IActor actor, InteractionPriority priority, bool autonomous,
            bool cancellable, Callback started, Callback completed, Callback failed)
        {
            // Creates the Arcana or native cast for a known spell with callbacks and the native tuning.
            Sim sim = actor as Sim;
            if (WitchCastingContext.IsWitch(sim) && !SpellKnowledge.Knows(sim, SpellKnowledgeGates.EntryFor(Native))) return null;
            Type owner = Native.GetType().DeclaringType;
            TargetSpell.Definition casting = owner == null ? null : SpellKnowledgeGates.Casting(owner.Name);
            InteractionDefinition implementation = WitchCastingContext.IsWitch(sim) && casting != null ? casting : Native;
            InteractionObjectPair.sTuningCache[new Pair<Type, Type>(implementation.GetType(), target.GetType())]
                = new InteractionObjectPair(Native, target).Tuning;
            return implementation.CreateInstanceWithCallbacks(target, actor, priority, autonomous, cancellable, started, completed, failed);
        }
        public void PopulatePieMenuPicker(ref InteractionInstanceParameters parameters, out List<ObjectPicker.TabInfo> tabs,
            out List<ObjectPicker.HeaderInfo> headers, out int selectable)
        { InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native); Native.PopulatePieMenuPicker(ref original, out tabs, out headers, out selectable); }
        public MenuItem.ObjectPickerTitleDelegate GetPickerTitleDelegate() { return Native.GetPickerTitleDelegate(); }
    }

    [Persistable]
    internal sealed class FireKnowledgeSpellDefinition : KnowledgeSpellDefinition, IUsableDuringFire
    {
        internal FireKnowledgeSpellDefinition(InteractionDefinition native) : base(native) { }
    }

    [Persistable]
    internal sealed class InnateAbilityDefinition : InteractionDefinition, IHasTraitIcon, IOverridesVisualType
    {
        internal InteractionDefinition Native;
        internal InnateAbilityDefinition(InteractionDefinition native) { Native = native; }
        public InteractionVisualTypes GetVisualType { get { return ((IOverridesVisualType)Native).GetVisualType; } }
        public ResourceKey GetTraitIcon(Sim sim, GameObject target) { return ((IHasTraitIcon)Native).GetTraitIcon(sim, target); }
        public string[] GetPath(bool female) { return Native.GetPath(female); }
        public string GetInteractionName(IActor actor, IGameObject target, InteractionObjectPair pair)
        { return Native.GetInteractionName(actor, target, new InteractionObjectPair(Native, target)); }
        public string GetInteractionName(ref InteractionInstanceParameters parameters)
        { InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native); return Native.GetInteractionName(ref original); }
        public InteractionTestResult Test(ref InteractionInstanceParameters parameters, ref GreyedOutTooltipCallback tooltip)
        { InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native); return Native.Test(ref original, ref tooltip); }
        public void AddInteractions(InteractionObjectPair pair, IActor actor, List<InteractionObjectPair> results)
        {
            // Lists the native ability under this wrapper so the fixed cost applies wherever the menu creates it.
            List<InteractionObjectPair> expanded = new List<InteractionObjectPair>();
            Native.AddInteractions(new InteractionObjectPair(Native, pair.Target), actor, expanded);
            foreach (InteractionObjectPair item in expanded)
                results.Add(new KnowledgePair(SpellKnowledgeGates.Wrap(item.InteractionDefinition), item.Target, item.Tuning));
        }
        public InteractionInstance CreateInstanceFromParameters(ref InteractionInstanceParameters parameters)
        {
            // Creates the native ability with its own tuning copy that charges the fixed Magical Power cost.
            InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native);
            original.SetInteractionObjectPair(new KnowledgePair(Native, parameters.Target, new InteractionObjectPair(Native, parameters.Target).Tuning));
            InteractionInstance instance = Native.CreateInstanceFromParameters(ref original);
            WitchNativeCosts.UseFixedAppleCost(instance, parameters.Actor as Sim);
            return instance;
        }
        public InteractionInstance CreateInstance(IGameObject target, IActor actor, InteractionPriority priority, bool autonomous, bool cancellable)
        {
            InteractionInstanceParameters parameters = new InteractionInstanceParameters(new InteractionObjectPair(Native, target), actor, priority, autonomous, cancellable);
            return CreateInstanceFromParameters(ref parameters);
        }
        public InteractionInstance CreateInstanceWithCallbacks(IGameObject target, IActor actor, InteractionPriority priority, bool autonomous,
            bool cancellable, Callback started, Callback completed, Callback failed)
        {
            // Creates the native ability with callbacks and the fixed Magical Power cost.
            InteractionInstance instance = Native.CreateInstanceWithCallbacks(target, actor, priority, autonomous, cancellable, started, completed, failed);
            WitchNativeCosts.UseFixedAppleCost(instance, actor as Sim);
            return instance;
        }
        public void PopulatePieMenuPicker(ref InteractionInstanceParameters parameters, out List<ObjectPicker.TabInfo> tabs,
            out List<ObjectPicker.HeaderInfo> headers, out int selectable)
        { InteractionInstanceParameters original = parameters.CloneForNewInteraction(Native); Native.PopulatePieMenuPicker(ref original, out tabs, out headers, out selectable); }
        public MenuItem.ObjectPickerTitleDelegate GetPickerTitleDelegate() { return Native.GetPickerTitleDelegate(); }
    }
}
