using System;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Services;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class InnateSpellTests
    {
        private const BuffNames PestilenceBlock = (BuffNames)0x804E0048B3B91820UL;
        private const BuffNames IceBlock = (BuffNames)0x804E0048B3B91830UL;
        private const BuffNames FireBlockA = (BuffNames)0xB82D0015B9294950UL;
        private const BuffNames FireBlockB = (BuffNames)0x04C800171BD46B80UL;
        private const BuffNames ToadBlockA = (BuffNames)0x804E0147B62F2560UL;
        private const BuffNames ToadBlockB = (BuffNames)0x04C8001714E6C330UL;
        private static readonly uint[] BusyRoles = { 140790011U, 140790015U, 209840865U, 246634757U, 230743240U };

        internal static bool Allows(TargetSpell spell, Sim actor, Sim target, ref GreyedOutTooltipCallback tooltip)
        {
            // Mirrors the game's own spell tests for a Fairy or Genie, without its Witch and wand checks; rank never blocks a cast.
            if (spell == null || actor == null || target == null || !SpellKnowledge.Knows(actor, spell.CatalogEntry)) return false;
            WitchCastingRules rules = MagicCasters.ReadRules(actor);
            string id = spell.CatalogEntry.Id;
            bool selfAllowed = id == "good_luck" || id == "love" || id == "sunlight" || spell.CatalogEntry.Category == SpellCategory.Charm;
            if (!selfAllowed && actor == target) return false;
            if (!Social(actor, target)) return false;
            if (MagicCasters.AvailablePower(actor, rules) + 0.005f < rules.FinalCost(spell.BaseCost, spell.Category, 1f))
            {
                tooltip = delegate { return JournalText.Get("Arcana.Cast.NotEnoughPower"); };
                return false;
            }
            BuffManager buffs = target.BuffManager;
            switch (id)
            {
                case "pestilence":
                    return buffs == null || !buffs.HasElement(PestilenceBlock);
                case "ice_blast":
                    if (buffs != null && buffs.HasElement(IceBlock)) return false;
                    return NotPregnant(target, ref tooltip);
                case "fire_blast":
                    if (buffs != null && buffs.HasAnyElement(new BuffNames[] { FireBlockA, FireBlockB })) return false;
                    return NotPregnant(target, ref tooltip);
                case "toadification":
                    if (buffs != null && (buffs.HasElement(ToadBlockA) || buffs.HasElement(ToadBlockB))) return false;
                    if ((target.OccultManager != null && target.OccultManager.HasAnyOccultType()) || target.SimDescription.IsGhost) return false;
                    return !(target.Service is GrimReaper);
                default:
                    return true;
            }
        }

        private static bool NotPregnant(Sim target, ref GreyedOutTooltipCallback tooltip)
        {
            if (!target.SimDescription.IsPregnant) return true;
            string text = Localization.LocalizeString(target.IsFemale, "Gameplay/Actors/Sim:PregnantFailure");
            tooltip = delegate { return text; };
            return false;
        }

        private static bool Social(Sim actor, Sim target)
        {
            // Ports the game's shared spell target checks: postures, socializing, services, roles, children and pets.
            if (target.SimDescription.IsEP11Bot) return false;
            if (!actor.Posture.AllowsNormalSocials() || !target.Posture.AllowsNormalSocials()) return false;
            if (!actor.SimDescription.ShowSocialsOnSim) return false;
            if (!target.SimDescription.IsZombie)
            {
                if (!target.SimDescription.ShowSocialsOnSim) return false;
                if (!target.CanBeSocializedWith || !actor.CanBeSocializedWith) return false;
            }
            if (SocialComponent.IsInServicePreventingSocialization(target)) return false;
            if (target.SimDescription.AssignedRole != null)
            {
                uint role = (uint)target.SimDescription.AssignedRole.Type;
                if (Array.IndexOf(BusyRoles, role) >= 0) return false;
            }
            if (target.Service is GrimReaper) return false;
            IPreventSocialization blocker = target.CurrentInteraction as IPreventSocialization;
            if (blocker != null && !blocker.SocializationAllowed(actor, target)) return false;
            return !target.SimDescription.ChildOrBelow && !target.SimDescription.IsPet;
        }
    }
}
