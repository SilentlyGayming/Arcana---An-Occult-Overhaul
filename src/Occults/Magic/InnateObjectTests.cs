using System;
using System.Reflection;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Interfaces;
using Sims3.Gameplay.ObjectComponents;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class InnateObjectTests
    {
        private const BuffNames StatueBlock = (BuffNames)0x804E0048B3B91840UL;
        private const Slot ConvertSlot = (Slot)0xA820F8A6U;

        internal static bool Allows(InteractionDefinition native, Sim actor, ref InteractionInstanceParameters parameters,
            ref GreyedOutTooltipCallback tooltip)
        {
            // Mirrors the game's ground, plumbing, statue, upgrade and conversion tests for a Fairy or Genie, without its Witch and wand checks.
            System.Type owner = native.GetType().DeclaringType;
            GameObject target = parameters.Target as GameObject;
            if (owner == null || actor == null || target == null || target.HasBeenDestroyed) return false;
            switch (owner.Name)
            {
                case "CastFireBlastTerrain":
                    return GroundTarget(actor, parameters.Hit);
                case "CastIceBlastTerrain":
                    return true;
                case "CastIceBlastPlumbing":
                    if (target.UpgradableComponent != null && target.UpgradableComponent.Unbreakable) return false;
                    return target.Repairable == null || !target.Repairable.Broken;
                case "CastFireBlastOnStatue":
                case "CastSunlightCharmOnStatue":
                    return Statue(actor, target as ISculptureFrozenSim);
                case "MagicallyUpgrade":
                    return Upgrade(native, target);
                case "CastConvert":
                    return target.GetContainedObject(ConvertSlot) == null;
                default:
                    return false;
            }
        }

        private static bool GroundTarget(Sim actor, GameObjectHit hit)
        {
            // Requires a routable point on a lot that is not a wall, floor edge, roof or similar surface, as the game does.
            Lot lot = LotManager.GetLotAtPoint(hit.mPoint);
            if (lot == null || lot.LotId == ulong.MaxValue) return false;
            int type = (int)hit.mType;
            if (type >= 8 && type <= 11) return false;
            Route route = actor.CreateRoute();
            return route != null && route.IsPointRoutable(hit.mPoint);
        }

        private static bool Statue(Sim actor, ISculptureFrozenSim statue)
        {
            // Requires a frozen adult Sim other than the caster who is still under the statue moodlet.
            Sim frozen = statue == null ? null : statue.GetFrozenSim();
            if (frozen == null || frozen == actor || frozen.BuffManager == null || !frozen.BuffManager.HasElement(StatueBlock)) return false;
            return !frozen.SimDescription.ChildOrBelow && !frozen.SimDescription.IsPet;
        }

        private static bool Upgrade(InteractionDefinition definition, GameObject target)
        {
            // Requires an unbroken upgradable object; unbreakable needs a chance of breaking and channels need a TV without them.
            FieldInfo selected = definition == null ? null : definition.GetType().GetField("SelectedUpgrade", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selected == null || target.UpgradableComponent == null) return false;
            if (target.Repairable != null && target.Repairable.Broken) return false;
            int upgrade = Convert.ToInt32(selected.GetValue(definition));
            if (upgrade == 1) return target.Repairable != null && target.Repairable.HasChanceOfBreaking();
            ITelevision television = target as ITelevision;
            return upgrade != 10 || television == null || !television.HasAllChannels;
        }
    }
}
