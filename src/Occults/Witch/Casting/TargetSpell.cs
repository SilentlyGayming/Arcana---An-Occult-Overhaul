using System;
using System.Reflection;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.UI.Hud;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal sealed class TargetSpell
    {
        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.NonPublic;

        internal readonly InteractionDefinition Stock;
        internal readonly WitchSpell Category;
        internal readonly MagicWand.SpellType Id;
        internal readonly BuffNames Backfire;
        internal readonly BuffNames TimedCurse;
        internal readonly FieldInfo NativeCost;
        internal readonly MethodInfo NativeSuccess;
        internal readonly MethodInfo NativeFailure;
        internal readonly MethodInfo NativeEpicAllowed;
        internal readonly MethodInfo NativePreparation;
        internal readonly MethodInfo NativeSuccessVisual;
        internal readonly string JazzState;
        internal readonly MethodInfo SuccessVfx;
        internal readonly MethodInfo EpicVfx;
        internal readonly MethodInfo HitVfx;

        internal TargetSpell(InteractionDefinition stock, MagicWand.CastSpell template, int tier, WitchSpellKind kind, BuffNames backfire)
            : this(stock, template, tier, kind, backfire, BuffNames.Undefined)
        {
        }

        internal TargetSpell(InteractionDefinition stock, MagicWand.CastSpell template, int tier, WitchSpellKind kind,
            BuffNames backfire, BuffNames timedCurse)
        {
            Stock = stock;
            Category = new WitchSpell(tier, kind);
            Type type = template.GetType();
            Backfire = backfire;
            TimedCurse = timedCurse;
            NativeCost = type.GetField("kMotiveDrain", BindingFlags.Static | BindingFlags.NonPublic);
            NativeSuccessVisual = type.GetMethod("ShowSuccessVfx", Methods);
            NativeSuccess = type.GetMethod("OnSpellSuccess", Methods);
            NativeFailure = type.GetMethod("OnSpellEpicFailure", Methods);
            NativeEpicAllowed = type.GetMethod("EpicFailureAllowed", Methods);
            NativePreparation = type.GetMethod("AdditionalInteractionPreparation", Methods);
            Id = (MagicWand.SpellType)Read(type, "TypeOfSpell", template);
            JazzState = (string)Read(type, "JazzStateName", template);
            SuccessVfx = type.GetProperty("SuccessVfxName", Methods).GetGetMethod(true);
            EpicVfx = type.GetProperty("EpicFailVfxName", Methods).GetGetMethod(true);
            HitVfx = type.GetProperty("HitVfxName", Methods).GetGetMethod(true);
        }

        private static object Read(Type type, string property, MagicWand.CastSpell template)
        {
            return WitchSimCast.Invoke(type.GetProperty(property, Methods).GetGetMethod(true), template, null);
        }

        public abstract class Definition : InteractionDefinition<Sim, Sim, WitchSimCast>, IHasTraitIcon, IHasMenuPathIcon, IOverridesVisualType
        {
            internal abstract TargetSpell Spell { get; }

            protected override InteractionInstance CreateInstance(ref InteractionInstanceParameters parameters)
            {
                if (!WitchCastingContext.IsWitch(parameters.Actor as Sim))
                {
                    InteractionInstanceParameters original = parameters.CloneForNewInteraction(Spell.Stock);
                    return Spell.Stock.CreateInstanceFromParameters(ref original);
                }
                return base.CreateInstance(ref parameters);
            }

            public override string GetInteractionName(ref InteractionInstanceParameters parameters)
            {
                InteractionInstanceParameters original = parameters.CloneForNewInteraction(Spell.Stock);
                return Spell.Stock.GetInteractionName(ref original);
            }

            public override string[] GetPath(bool female)
            {
                return Spell.Stock.GetPath(female);
            }

            public InteractionVisualTypes GetVisualType
            {
                get { return ((IOverridesVisualType)Spell.Stock).GetVisualType; }
            }

            public ResourceKey GetTraitIcon(Sim sim, Sims3.Gameplay.Abstracts.GameObject target)
            {
                return ((IHasTraitIcon)Spell.Stock).GetTraitIcon(sim, target);
            }

            public ResourceKey GetPathIcon(Sim sim, Sims3.Gameplay.Abstracts.GameObject target)
            {
                return ((IHasMenuPathIcon)Spell.Stock).GetPathIcon(sim, target);
            }

            protected override bool Test(Sim actor, Sim target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                InteractionInstanceParameters parameters = new InteractionInstanceParameters(new InteractionObjectPair(Spell.Stock, target),
                    actor, new InteractionPriority(InteractionPriorityLevel.UserDirected), autonomous, true);
                if (actor.OccultManager == null || MagicWand.HasWand(actor) || !WitchCastingContext.HasPracticallyMagic(actor))
                {
                    return Spell.Stock.Test(ref parameters, ref tooltip) == InteractionTestResult.Pass;
                }

                // HasWand recognizes the native casting flag. Stock Test is synchronous.
                OccultWitch witch = actor.OccultManager.GetOccultType(OccultTypes.Witch) as OccultWitch;
                bool casting = witch.IsCastingSpell;
                try
                {
                    witch.IsCastingSpell = true;
                    return Spell.Stock.Test(ref parameters, ref tooltip) == InteractionTestResult.Pass;
                }
                finally
                {
                    witch.IsCastingSpell = casting;
                }
            }
        }

        public sealed class GoodLuckCharm : Definition
        {
            private static readonly TargetSpell value = new TargetSpell(MagicWand.CastGoodLuckCharm.Singleton,
                new MagicWand.CastGoodLuckCharm(), 0, WitchSpellKind.Spell, BuffNames.FeelingUnlucky);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class LoveCharm : Definition
        {
            private static readonly TargetSpell value = new TargetSpell(MagicWand.CastLoveCharm.Singleton,
                new MagicWand.CastLoveCharm(), 2, WitchSpellKind.Spell, BuffNames.Undefined);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class SunlightCharm : Definition
        {
            private static readonly TargetSpell value = new TargetSpell(MagicWand.CastSunlightCharm.Singleton,
                new MagicWand.CastSunlightCharm(), 3, WitchSpellKind.Spell, BuffNames.Dazed);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class PestilenceCurse : Definition
        {
            private static readonly TargetSpell value = new TargetSpell(MagicWand.CastPestilenceCurse.Singleton,
                new MagicWand.CastPestilenceCurse(), 3, WitchSpellKind.Curse, BuffNames.SickAndTired, BuffNames.SickAndTired);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class IceBlast : Definition
        {
            private static readonly TargetSpell value = new TargetSpell(MagicWand.CastIceBlast.Singleton,
                new MagicWand.CastIceBlast(), 1, WitchSpellKind.Spell, BuffNames.SoCold);

            internal override TargetSpell Spell { get { return value; } }
        }

        public sealed class HauntingCurse : Definition
        {
            private static readonly TargetSpell value = new TargetSpell(MagicWand.CastHauntingCurse.Singleton,
                new MagicWand.CastHauntingCurse(), 2, WitchSpellKind.Curse, BuffNames.Undefined, BuffNames.Haunted);

            internal override TargetSpell Spell { get { return value; } }
        }
    }
}
