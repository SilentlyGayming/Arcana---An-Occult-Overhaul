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
using Sims3.Gameplay.Skills;
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
        private static readonly Dictionary<string, TargetSpell.Definition> simCastings = new Dictionary<string, TargetSpell.Definition>();
        private static readonly List<string> SimSpellNames = new List<string>(new string[] { "CastGoodLuckCharm", "CastLoveCharm",
            "CastSunlightCharm", "CastPestilenceCurse", "CastFireBlast", "CastIceBlast", "CastToadify", "CastHauntingCurse",
            "CastMotiveCharm", "CastMotiveCurse" });
        private static readonly Dictionary<InteractionTuning, bool> openedTunings = new Dictionary<InteractionTuning, bool>();
        private static readonly CommodityKind[] Motives = { CommodityKind.Bladder, CommodityKind.Hunger, CommodityKind.Energy,
            CommodityKind.Hygiene, CommodityKind.Fun, CommodityKind.Social };
        private static readonly FieldInfo SpellLevels = typeof(MagicWand).GetField("kSpellLevels", StaticFields);
        private static readonly FieldInfo CharmLevels = typeof(MagicWand).GetField("kMotiveCharmUnlockLevels", StaticFields);
        private static readonly FieldInfo CurseLevels = typeof(MagicWand).GetField("kMotiveCurseUnlockLevels", StaticFields);

        internal static bool Installed { get { return installed; } }

        internal static bool UsesArcanaCasting(string name) { return SimSpellNames.Contains(name); }

        internal static TargetSpell.Definition SimCasting(SpellEntry entry)
        {
            // Returns Arcana's casting of an entry's Sim-targeted spell, used by every caster, or null when it has none.
            TargetSpell.Definition definition;
            return entry != null && simCastings.TryGetValue(entry.Id, out definition) ? definition : null;
        }

        internal static bool InnateCastable(SpellEntry entry)
        {
            return SimCasting(entry) != null || NativeSpellCastable(entry);
        }

        internal static TargetSpell.Definition Casting(string name)
        {
            TargetSpell.Definition definition;
            return arcana.TryGetValue(name, out definition) ? definition : null;
        }

        internal static void Prepare()
        {
            // Captures the native spell definitions and replaces their singletons with knowledge-gated wrappers.
            SpellKnowledgeRegistration.Register();
            Guard("Registering Genie Magic", GenieMagicRegistration.Register);
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
            if (simCastings.Count == 0) Guard("Preparing Sim spell castings", PrepareSimCastings);
            foreach (TargetSpell.Definition definition in arcana.Values)
            {
                if (definition.Spell.Stock == null) throw new InvalidOperationException("Missing native casting descriptor.");
            }
            PrepareWrappers();
        }

        private static void PrepareSimCastings()
        {
            // Builds Arcana's casting for every Sim-targeted spell, after the Witch castings and before the native singletons are wrapped.
            Dictionary<string, TargetSpell.Definition> built = new Dictionary<string, TargetSpell.Definition>();
            foreach (TargetSpell.Definition definition in arcana.Values) built[definition.Spell.CatalogEntry.Id] = definition;
            TargetSpell.Definition fire = new TargetSpell.FireBlast();
            TargetSpell.Definition toad = new TargetSpell.Toadify();
            built[fire.Spell.CatalogEntry.Id] = fire;
            built[toad.Spell.CatalogEntry.Id] = toad;
            int[] charmLevels = (int[])CharmLevels.GetValue(null);
            int[] curseLevels = (int[])CurseLevels.GetValue(null);
            foreach (SpellEntry entry in SpellCatalog.Entries)
            {
                if (entry.MotiveIndex < 0 || entry.Learning == SpellLearning.NativeDisabled) continue;
                int[] levels = entry.Category == SpellCategory.Charm ? charmLevels : curseLevels;
                built[entry.Id] = TargetSpell.MotiveSpell.Create(entry, MotiveAt(entry.MotiveIndex), levels[entry.MotiveIndex]);
            }
            foreach (KeyValuePair<string, TargetSpell.Definition> pair in built) simCastings[pair.Key] = pair.Value;
        }

        private static void Guard(string operation, Sims3.UI.Function action)
        {
            // Runs optional Fairy and Genie setup so a failure there is logged and never stops Witch casting.
            try
            {
                action();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce(operation, error);
            }
        }

        private static void PrepareWrappers()
        {
            // Replaces the native spell singletons with knowledge-gated wrappers.
            List<string> names = new List<string>(SimSpellNames);
            names.AddRange(new string[] { "CastFireBlastTerrain", "CastIceBlastTerrain", "CastIceBlastPlumbing", "CastFireBlastOnStatue",
                "CastSunlightCharmOnStatue", "CastReanimate", "CastConvert", "CastRestoration", "MagicallyUpgrade" });
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
            int wrapped = 0;
            foreach (KeyValuePair<FieldInfo, InteractionDefinition> original in originals)
            {
                if (WrapSingleton(original.Key, original.Value)) wrapped++;
            }
            if (wrapped == 0) throw new InvalidOperationException("No native spell could be wrapped.");
            installed = true;
        }

        private static bool WrapSingleton(FieldInfo singleton, InteractionDefinition native)
        {
            // Wraps one native singleton, leaving it native and logging its name if the wrapper cannot be built or installed.
            try
            {
                InteractionDefinition wrapper = Wrap(native);
                singleton.SetValue(null, wrapper);
                wrappers[native.GetType()] = wrapper;
                return true;
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                try
                {
                    singleton.SetValue(null, native);
                }
                catch (Exception)
                {
                }
                wrappers.Remove(native.GetType());
                JournalBootstrap.ReportOnce("Wrapping " + native.GetType().FullName, error);
                return false;
            }
        }

        internal static InteractionDefinition Wrap(InteractionDefinition native)
        {
            // Wraps a native spell in a knowledge gate, keeping its fire-usable marker, and Conjure Apple in its fixed-cost wrapper.
            if (native is KnowledgeSpellDefinition) return native;
            if (native.GetType().DeclaringType == typeof(TraitFunctions.ConjureApple)) return WrapAbility(native);
            return native is IUsableDuringFire ? WrapFireSpell(native) : WrapSpell(native);
        }

        private static InteractionDefinition WrapAbility(InteractionDefinition native)
        {
            return native is InnateAbilityDefinition ? native : new InnateAbilityDefinition(native);
        }

        private static InteractionDefinition WrapFireSpell(InteractionDefinition native)
        {
            return new FireKnowledgeSpellDefinition(native);
        }

        private static InteractionDefinition WrapSpell(InteractionDefinition native)
        {
            return new KnowledgeSpellDefinition(native);
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
            if (MagicCasters.IsInnateCaster(sim)) Guard("Adding Genie Magic", delegate { MagicCasters.EnsureSkill(sim); });
            if (sim != null) Guard("Updating Magic Hands", delegate { MagicCasters.UpdateHands(sim); });
            if (MagicCasters.IsCaster(sim) && sim.InteractionQueue != null)
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
            // Swaps native spell pairs for wrappers and gives wrapper pairs created at object startup the native spell's tuning.
            if (pairs == null) return;
            for (int i = 0; i < pairs.Count; i++)
            {
                InteractionObjectPair pair = pairs[i];
                if (pair == null || pair.InteractionDefinition == null || pair is KnowledgePair) continue;
                InteractionDefinition native = NativeOf(pair.InteractionDefinition);
                InteractionDefinition wrapper;
                if (native != null)
                {
                    InteractionTuning tuning = new InteractionObjectPair(native, pair.Target).Tuning;
                    if (InnateCastable(EntryFor(native))) OpenTuning(tuning);
                    pairs[i] = new KnowledgePair(pair.InteractionDefinition, pair.Target, tuning);
                }
                else if (wrappers.TryGetValue(pair.InteractionDefinition.GetType(), out wrapper))
                {
                    if (InnateCastable(EntryFor(pair.InteractionDefinition))) OpenTuning(pair.Tuning);
                    pairs[i] = new KnowledgePair(wrapper, pair.Target, pair.Tuning);
                }
            }
        }

        private static void OpenTuning(InteractionTuning tuning)
        {
            // Lets Fairies and Genies pass a spell's tuning: adds them to its occult list and drops the Spellcasting skill floor.
            if (tuning == null || tuning.Availability == null || openedTunings.ContainsKey(tuning)) return;
            openedTunings[tuning] = true;
            Availability availability = tuning.Availability;
            OccultTypes innate = OccultTypes.Fairy | OccultTypes.Genie;
            if (availability.OccultRestrictionType == OccultRestrictionType.Inclusive) availability.OccultRestrictions |= innate;
            else if (availability.OccultRestrictionType == OccultRestrictionType.Exclusive) availability.OccultRestrictions &= ~innate;
            if (availability.SkillThresholdType == SkillNames.Spellcasting) availability.SkillThresholdType = SkillNames.None;
        }

        private static bool IsSimSpell(InteractionDefinition native)
        {
            // Returns whether a native spell definition targets a Sim, which every caster casts through Arcana for Arcana's accuracy.
            Type owner = native == null ? null : native.GetType().DeclaringType;
            return owner != null && SimSpellNames.Contains(owner.Name);
        }

        private static InteractionDefinition NativeOf(InteractionDefinition definition)
        {
            // Returns the native definition inside an Arcana spell or ability wrapper, or null for anything else.
            KnowledgeSpellDefinition spell = definition as KnowledgeSpellDefinition;
            if (spell != null) return spell.Native;
            InnateAbilityDefinition ability = definition as InnateAbilityDefinition;
            return ability == null ? null : ability.Native;
        }

        internal static SpellEntry EntryFor(InteractionDefinition definition)
        {
            // Resolves a native spell definition, including target and motive variants, to its catalog entry.
            KnowledgeSpellDefinition wrapper = definition as KnowledgeSpellDefinition;
            if (wrapper != null) definition = wrapper.Native;
            NativeSpellCast.Definition innateCast = definition as NativeSpellCast.Definition;
            if (innateCast != null) definition = innateCast.Native;
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

        internal static InteractionDefinition Implementation(InteractionDefinition native, Sim actor)
        {
            // Picks the casting for a known spell: Arcana's for every Sim-targeted spell, otherwise the game's own through NativeSpellCast.
            SpellEntry entry = EntryFor(native);
            if (MagicCasters.IsInnateCaster(actor))
            {
                if (!SpellKnowledge.Knows(actor, entry)) return null;
                if (IsSimSpell(native)) return SimCasting(entry);
                if (!NativeSpellCastable(entry)) return null;
                return new NativeSpellCast.Definition(native);
            }
            if (!WitchCastingContext.IsWitch(actor)) return native;
            if (!SpellKnowledge.Knows(actor, entry)) return null;
            Type owner = native.GetType().DeclaringType;
            TargetSpell.Definition casting = owner == null ? null : Casting(owner.Name);
            if (casting == null && IsSimSpell(native)) casting = SimCasting(entry);
            if (casting != null) return casting;
            return entry == null ? native : new NativeSpellCast.Definition(native);
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
            if (MagicCasters.IsInnateCaster(actor))
            {
                return TestInnate(native, actor, ref parameters, ref tooltip) ? InteractionTestResult.Pass : InteractionTestResult.Def_TestFailed;
            }
            InteractionInstanceParameters original = parameters.CloneForNewInteraction(native);
            if (!WitchCastingContext.IsWitch(actor)) return native.Test(ref original, ref tooltip);
            SpellEntry entry = EntryFor(native);
            if (entry == null || !SpellKnowledge.Knows(actor, entry)) return InteractionTestResult.Def_TestFailed;
            bool hands = IsSimSpell(native) && !MagicWand.HasWand(actor) && WitchCastingContext.HasPracticallyMagic(actor);
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

        private static bool TestInnate(InteractionDefinition native, Sim actor, ref InteractionInstanceParameters parameters,
            ref GreyedOutTooltipCallback tooltip)
        {
            // Tests a Fairy's or Genie's cast: Arcana's own test on Sims, the game's ground and object tests without its Witch checks elsewhere.
            SpellEntry entry = EntryFor(native);
            Sim target = parameters.Target as Sim;
            if (target != null)
            {
                TargetSpell.Definition casting = SimCasting(entry);
                return casting != null && InnateSpellTests.Allows(casting.Spell, actor, target, ref tooltip);
            }
            if (!NativeSpellCastable(entry) || !SpellKnowledge.Knows(actor, entry)) return false;
            WitchCastingRules rules = MagicCasters.ReadRules(actor);
            WitchSpell spell = WitchSpell.For(entry);
            if (MagicCasters.AvailablePower(actor, rules) + 0.005f < rules.FinalCost(entry.Cost, spell, 1f))
            {
                tooltip = delegate { return JournalText.Get("Arcana.Cast.NotEnoughPower"); };
                return false;
            }
            return InnateObjectTests.Allows(native, actor, ref parameters, ref tooltip);
        }

        private static bool NativeSpellCastable(SpellEntry entry)
        {
            // Returns whether a Fairy or Genie may cast the game's own ground, object or ritual version of an entry.
            if (entry == null) return false;
            return entry.Id == "fire_blast" || entry.Id == "ice_blast" || entry.Id == "sunlight" || entry.Id == "upgrade" || entry.Id == "conversion";
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
            bool caster = MagicCasters.IsCaster(sim);
            bool charms = native is MagicWand.CastMotiveCharm.Definition;
            if ((charms || native is MagicWand.CastMotiveCurse.Definition) && MagicCasters.IsInnateCaster(sim))
            {
                AddInnateMotives(charms ? SpellCategory.Charm : SpellCategory.Curse, pair.Target, expanded);
            }
            else if (caster)
            {
                using (new AvailabilityScope(true)) native.AddInteractions(new InteractionObjectPair(native, pair.Target), actor, expanded);
            }
            else native.AddInteractions(new InteractionObjectPair(native, pair.Target), actor, expanded);
            foreach (InteractionObjectPair item in expanded)
            {
                SpellEntry entry = EntryFor(item.InteractionDefinition);
                if (caster && (entry == null || !SpellKnowledge.Knows(sim, entry))) continue;
                MagicWand.CastMotiveCharm.Definition charm = item.InteractionDefinition as MagicWand.CastMotiveCharm.Definition;
                MagicWand.CastMotiveCurse.Definition curse = item.InteractionDefinition as MagicWand.CastMotiveCurse.Definition;
                if (entry != null && entry.MotiveIndex >= 0)
                {
                    if (charm != null) charm.mSpellLevel = ((int[])CharmLevels.GetValue(null))[entry.MotiveIndex];
                    if (curse != null) curse.mSpellLevel = ((int[])CurseLevels.GetValue(null))[entry.MotiveIndex];
                }
                if (InnateCastable(entry)) OpenTuning(item.Tuning);
                results.Add(new KnowledgePair(Wrap(item.InteractionDefinition), item.Target, item.Tuning));
            }
        }

        private static void AddInnateMotives(SpellCategory category, IGameObject target, List<InteractionObjectPair> expanded)
        {
            // Lists every motive charm or curse a Fairy or Genie can cast, since the game only lists them by Spellcasting level.
            foreach (SpellEntry entry in SpellCatalog.Entries)
            {
                if (entry.MotiveIndex < 0 || entry.Category != category) continue;
                TargetSpell.Definition casting = SimCasting(entry);
                if (casting != null) expanded.Add(new InteractionObjectPair(casting.Spell.Stock, target));
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
            InteractionDefinition implementation = SpellKnowledgeGates.Implementation(Native, actor);
            if (implementation == null) return null;
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
            InteractionDefinition implementation = SpellKnowledgeGates.Implementation(Native, actor as Sim);
            if (implementation == null) return null;
            if (!(implementation is NativeSpellCast.Definition))
            {
                InteractionObjectPair.sTuningCache[new Pair<Type, Type>(implementation.GetType(), target.GetType())]
                    = new InteractionObjectPair(Native, target).Tuning;
            }
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
    internal sealed class InnateAbilityDefinition : InteractionDefinition, ISoloInteractionDefinition, IHasTraitIcon, IOverridesVisualType
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
