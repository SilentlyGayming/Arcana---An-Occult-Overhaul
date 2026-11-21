using System;
using System.Collections.Generic;
using Sims3.Gameplay;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Careers;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.CelebritySystem;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects;
using Sims3.Gameplay.Objects.HobbiesSkills;
using Sims3.Gameplay.Opportunities;
using Sims3.Gameplay.Socializing;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class WishSelection
    {
        internal Sim Lover;
        internal Urnstone Grave;
    }

    internal static class WishEffects
    {
        internal static bool CanReceive(Sim target)
        {
            if (target == null || target.HasBeenDestroyed || target.SimDescription == null || !target.SimDescription.IsHuman ||
                target.BuffManager == null || target.TraitManager == null) return false;
            InteractionInstance current = target.CurrentInteraction;
            bool reaping = target.DeathReactionBroadcast != null || current is Urnstone.KillSim || current != null && current.InteractionDefinition is SocialInteractionB.DefinitionDeathInteraction;
            return WishRules.CanReceive(target.SimDescription.IsDead, (target.SimDescription.IsPlayableGhost || target.SimDescription.IsGhost && target.IsSelectable), reaping);
        }

        internal static bool Eligible(Sim genie, Sim target, WishKind kind, WishMode mode)
        {
            if (!CanReceive(target)) return false;
            if (mode == WishMode.Own) return kind == WishKind.Fortune ? target.Household != null : kind == WishKind.Vitality || kind == WishKind.Heart;
            WishDefinition definition = WishCatalog.Find(kind);
            if (definition == null || !WishRules.CanOffer(definition, GameUtils.IsInstalled(definition.Product), mode == WishMode.Lamp)) return false;
            SimDescription sim = target.SimDescription;
            switch (kind)
            {
                case WishKind.WorldPeace: return !target.HasTrait(TraitNames.Evil) ? !target.HasTrait(TraitNames.WishedWorldPeace) : !target.HasTrait(TraitNames.WishedWorldMisery);
                case WishKind.Resurrect: return Graves(target).Count > 0;
                case WishKind.MoreWishes: return WishLamp.Lamp(genie) != null;
                case WishKind.Love: return Lovers(target).Count > 0 && (!sim.IsEP11Bot || target.HasTrait(TraitNames.CapacityToLoveChip));
                case WishKind.LongLife: return !sim.IsEP11Bot && !sim.IsFrankenstein && !target.HasTrait(TraitNames.WishedForLongLife) && sim.AgingState != null;
                case WishKind.LargeFamily: return sim.YoungAdultOrAbove && !sim.IsMummy && !sim.IsFrankenstein && !sim.IsEP11Bot && !sim.IsPregnant &&
                    !target.HasTrait(TraitNames.WishedForLargeFamily) && sim.Household != null && sim.Household.CanAddSpeciesToHousehold(sim.Species);
                case WishKind.Happiness: return !target.HasTrait(TraitNames.WishedForHappiness) && (!sim.IsEP11Bot || target.HasTrait(TraitNames.HumanEmotionChip));
                case WishKind.Fortune: return sim.Household != null;
                case WishKind.Beauty: return sim.TeenOrAbove && !target.HasTrait(TraitNames.WishedForBeauty) && !WishBuffs.HasBeautyPenalty(target);
                case WishKind.FreeGenie: return WishLamp.CanFree(genie, target);
                case WishKind.CheatDeath:
                    WishRecord protection = WishState.Read(sim, false);
                    return !sim.IsDead && sim.TeenOrAbove && !sim.IsEP11Bot && (protection == null || !protection.CheatDeath && !protection.CheatDeathAttempted);
                case WishKind.Youth: return CanUseYouth(target);
                case WishKind.Success: return Career(target) != null;
                case WishKind.Fame: return sim.TeenOrAbove && target.CelebrityManager != null && WishRules.CanGainFame(target.CelebrityManager.Level,
                    GameUtils.IsInstalled(ProductVersion.EP3), sim.CanBeCelebrity);
            }
            return false;
        }

        internal static Occupation Career(Sim target)
        {
            Occupation job = target == null || target.SimDescription == null || target.SimDescription.CareerManager == null ? null : target.SimDescription.CareerManager.Occupation;
            if (job == null || job is School || job.IsSkillBased) return null;
            bool supported = job is Sims3.Gameplay.Careers.Career || job is Sims3.Gameplay.ActiveCareer.ActiveCareer;
            Sims3.Gameplay.Careers.Career rabbitHole = job as Sims3.Gameplay.Careers.Career;
            if (rabbitHole != null && (rabbitHole.CurLevel == null || rabbitHole.CurLevel.NextLevels == null || rabbitHole.CurLevel.NextLevels.Count == 0)) return null;
            return WishRules.CanPromote(job.Level, job.HighestLevel, supported) ? job : null;
        }

        private static bool CanUseYouth(Sim target)
        {
            if (!GameUtils.IsInstalled(ProductVersion.EP4) || !target.SimDescription.Adult && !target.SimDescription.Elder) return false;
            GreyedOutTooltipCallback tooltip = null;
            return target.SimDescription.AgingState != null && new YoungAgainPotion { IsMixedDrink = true }.UsePotionTest(target, ref tooltip);
        }

        private static bool Romantic(Sim wisher, Sim other)
        {
            if (other == null || other == wisher || other.HasBeenDestroyed || other.SimDescription == null || !CanReceive(other) ||
                !wisher.SimDescription.TeenOrAbove || !other.SimDescription.TeenOrAbove || wisher.SimDescription.Teen != other.SimDescription.Teen ||
                GenieAbilities.IsGenie(other) || other.SimDescription.IsEP11Bot != wisher.SimDescription.IsEP11Bot) return false;
            if (wisher.Genealogy == null || other.Genealogy == null || wisher.Genealogy.IsSufficientlyRelatedToRuleOutRomance(other.Genealogy)) return false;
            return OccultImaginaryFriend.CanSimGetRomanticWithSim(wisher, other) && wisher.CanGetRomantic(other, false);
        }

        internal static List<Sim> Lovers(Sim target)
        {
            List<Sim> result = new List<Sim>();
            if (target.SocialComponent != null)
                foreach (Sim other in target.SocialComponent.GetRelationshipsWithLikingAbove(0f)) if (Romantic(target, other)) result.Add(other);
            return result;
        }

        internal static List<Urnstone> Graves(Sim wisher)
        {
            List<Urnstone> result = new List<Urnstone>();
            foreach (Urnstone grave in Sims3.Gameplay.Queries.GetObjects<Urnstone>())
            {
                SimDescription dead = grave.DeadSimsDescription;
                if (grave.HasBeenDestroyed || dead == null || !dead.IsGhost || !dead.IsDead || grave.InUse) continue;
                if (grave.LotCurrent != wisher.LotCurrent && (wisher.Inventory == null || !wisher.Inventory.Contains(grave)) && (wisher.Household == null || grave.LotCurrent != wisher.LotHome)) continue;
                Sim ghost = dead.CreatedSim;
                if (ghost != null && ghost.CurrentInteraction != null && ghost.CurrentInteraction.InteractionDefinition is SocialInteractionB.DefinitionDeathInteraction) continue;
                result.Add(grave);
            }
            return result;
        }

        internal static WishSelection Select(Sim wisher, WishKind kind)
        {
            WishSelection result = new WishSelection();
            if (kind == WishKind.Love) result.Lover = Pick(Lovers(wisher), kind) as Sim;
            if (kind == WishKind.Resurrect) result.Grave = Pick(Graves(wisher), kind) as Urnstone;
            return kind == WishKind.Love && result.Lover == null || kind == WishKind.Resurrect && result.Grave == null ? null : result;
        }

        private static object Pick<T>(List<T> choices, WishKind kind) where T : class
        {
            List<ObjectPicker.RowInfo> rows = new List<ObjectPicker.RowInfo>();
            foreach (T choice in choices)
            {
                Sim sim = choice as Sim;
                Urnstone grave = choice as Urnstone;
                SimDescription desc = sim != null ? sim.SimDescription : grave.DeadSimsDescription;
                rows.Add(new ObjectPicker.RowInfo(choice, new List<ObjectPicker.ColumnInfo> {
                    new ObjectPicker.ThumbAndTextColumn(desc.GetThumbnailKey(ThumbnailSize.Large, 0), desc.FullName) }));
            }
            List<ObjectPicker.RowInfo> selected = ObjectPickerDialog.Show(true, ModalDialog.PauseMode.PauseSimulator,
                JournalText.Get(WishCatalog.Find(kind).NameKey), Localization.LocalizeString("Ui/Caption/ObjectPicker:OK"),
                Localization.LocalizeString("Ui/Caption/ObjectPicker:Cancel"),
                new List<ObjectPicker.TabInfo> { new ObjectPicker.TabInfo("RelationshipSimImage", "", rows) },
                new List<ObjectPicker.HeaderInfo> { new ObjectPicker.HeaderInfo("Ui/Caption/ObjectPicker:Name", "Ui/Tooltip/ObjectPicker:Name", 500) }, 1);
            return selected == null || selected.Count == 0 ? null : selected[0].Item;
        }

        internal static bool ValidateSelection(Sim wisher, WishKind kind, WishSelection selection)
        {
            return selection != null && (kind != WishKind.Love || Romantic(wisher, selection.Lover)) &&
                (kind != WishKind.Resurrect || Graves(wisher).Contains(selection.Grave));
        }

        internal static bool Apply(Sim genie, Sim wisher, WishKind kind, bool success, WishSelection selected)
        {
            if (!success) return Fail(genie, wisher, kind, selected);
            SimDescription sim = wisher.SimDescription;
            EventTypeId evt = EventTypeId.kEventNone;
            switch (kind)
            {
                case WishKind.WorldPeace:
                    bool evil = wisher.HasTrait(TraitNames.Evil);
                    wisher.TraitManager.AddHiddenElement(evil ? TraitNames.WishedWorldMisery : TraitNames.WishedWorldPeace);
                    GenieWishes.GiveMoodlet(wisher, evil ? BuffNames.WishForWorldMisery : BuffNames.WishForWorldPeace, 72f);
                    ActiveTopic.AddToSim(wisher, "TalkAboutWorldPeaceMisery");
                    evt = evil ? EventTypeId.kWishForWorldMisery : EventTypeId.kWishForWorldPeace; break;
                case WishKind.Resurrect:
                    if (!SpawnGhost(wisher, selected.Grave)) return false;
                    Sim ghost = selected.Grave.DeadSimsDescription.CreatedSim;
                    selected.Grave.GhostToSim(ghost, true, !ghost.SimDescription.IsPlayableGhost);
                    ghost.UpdateWalkStyle(); EventTracker.SendEvent(EventTypeId.kResurrectedSim, wisher, ghost); return true;
                case WishKind.MoreWishes: return true;
                case WishKind.Love:
                    if (!Romantic(wisher, selected.Lover)) return false;
                    Relationship relationship = Relationship.Get(wisher, selected.Lover, true);
                    if (relationship == null) return false;
                    relationship.GenieWishForLove();
                    GenieWishes.GiveMoodlet(wisher, BuffNames.WishForLove, 24f);
                    GenieWishes.GiveMoodlet(selected.Lover, BuffNames.WishForLove, 24f);
                    EventTracker.SendEvent(EventTypeId.kWishForLove, wisher, selected.Lover); return true;
                case WishKind.LongLife: wisher.TraitManager.AddHiddenElement(TraitNames.WishedForLongLife); evt = EventTypeId.kWishForLongLife; break;
                case WishKind.LargeFamily:
                    wisher.TraitManager.AddHiddenElement(TraitNames.WishedForLargeFamily);
                    wisher.BuffManager.AddElement(BuffNames.WishForLargeFamily, Origin.FromGenie); evt = EventTypeId.kWishForLargeFamily; break;
                case WishKind.Happiness:
                    wisher.TraitManager.AddHiddenElement(TraitNames.WishedForHappiness);
                    wisher.BuffManager.AddElement(BuffNames.WishHappiness, Origin.FromGenie); evt = EventTypeId.kWishForHappiness; break;
                case WishKind.Fortune: wisher.Household.ModifyFamilyFunds(WishRules.Fortune); evt = EventTypeId.kWishForFortune; break;
                case WishKind.Beauty: wisher.TraitManager.AddHiddenElement(TraitNames.WishedForBeauty); evt = EventTypeId.kWishForBeauty; break;
                case WishKind.FreeGenie:
                    return wisher.OpportunityManager.AddOpportunityNow(OpportunityNames.EP6_ReleaseGenie_FreezeLamp, true, false);
                case WishKind.CheatDeath:
                    WishRecord protection = WishState.Read(sim, true); protection.CheatDeath = true; protection.CheatDeathAttempted = true; break;
                case WishKind.Youth: return new YoungAgainPotion { IsMixedDrink = true }.DoDrinkBehavior(wisher, false);
                case WishKind.Success:
                    Occupation career = Career(wisher);
                    if (career == null) return false;
                    int before = career.Level; career.PromoteSim(); return career.Level == before + 1;
                case WishKind.Fame:
                    uint level = wisher.CelebrityManager.Level;
                    wisher.CelebrityManager.ForceSetLevel(level + 1); return wisher.CelebrityManager.Level == level + 1;
                default: return false;
            }
            if (evt != EventTypeId.kEventNone) EventTracker.SendEvent(evt, wisher);
            return true;
        }

        private static bool Fail(Sim genie, Sim wisher, WishKind kind, WishSelection selected)
        {
            SimDescription sim = wisher.SimDescription;
            switch (kind)
            {
                case WishKind.WorldPeace: WishBuffs.GiveWrong(wisher, 72f); return true;
                case WishKind.Resurrect:
                    if (!SpawnGhost(wisher, selected.Grave)) return false;
                    WishBuffs.GiveWrong(wisher, 24f); return true;
                case WishKind.MoreWishes: WishBuffs.GiveWrong(wisher, 24f); return true;
                case WishKind.Love:
                    if (!Romantic(wisher, selected.Lover)) return false;
                    selected.Lover.BuffManager.AddElement(WishBuffs.OneSidedLove, Origin.FromGenie);
                    BuffInstance attraction = selected.Lover.BuffManager.GetElement(WishBuffs.OneSidedLove);
                    if (attraction != null) attraction.SetTargetSim(sim);
                    WishBuffs.GiveWrong(wisher, 24f); return true;
                case WishKind.LongLife:
                    if (sim.AgingState == null || AgingManager.Singleton == null) return false;
                    sim.AgingState.ExtendAgingState(7f); WishBuffs.GiveWrong(wisher, 24f); return true;
                case WishKind.LargeFamily: GenieWishes.GiveMoodlet(wisher, BuffNames.ATwinkleInTheEye, 24f); WishBuffs.GiveWrong(wisher, 24f); return true;
                case WishKind.Happiness:
                    WishRecord joy = WishState.Read(sim, true);
                    joy.JoyEnds = WishState.After(24f); joy.SadEnds = WishState.After(48f);
                    GenieWishes.GiveMoodlet(wisher, WishBuffs.BorrowedJoy, 24f); return true;
                case WishKind.Fortune:
                    if (!WishRepossession.Request(wisher)) return false;
                    WishBuffs.GiveWrong(wisher, 24f); return true;
                case WishKind.Beauty: WishBuffs.GiveBeautyFailure(wisher, 48f); return true;
                case WishKind.FreeGenie:
                    if (!WishLamp.Escape(genie, wisher)) return false;
                    WishBuffs.GiveWrong(wisher, 24f); return true;
                case WishKind.CheatDeath:
                    WishState.Read(sim, true).CheatDeathAttempted = true; return true;
                case WishKind.Youth:
                    sim.AgingState.ResetAndExtendAgingStage(0f);
                    WishBuffs.GiveWrong(wisher, 48f); return true;
                case WishKind.Success:
                    Occupation career = Career(wisher);
                    if (career == null) return false;
                    WishRecord setback = WishState.Read(sim, true);
                    setback.CareerPending = true; setback.CareerGuid = career.Guid; setback.CareerExpires = WishState.After(7f * 24f);
                    setback.WorkStarted = false; setback.WaitForCurrentShift = career.IsAtWork; return true;
                case WishKind.Fame:
                    if (wisher.CelebrityManager.Level > 0)
                        EventTracker.SendEvent(new DisgracefulActionEvent(EventTypeId.kSimCommittedDisgracefulAction, wisher, DisgracefulActionType.FalselyAccused));
                    WishBuffs.GiveWrong(wisher, 24f);
                    return true;
                default: return false;
            }
        }

        private static bool SpawnGhost(Sim wisher, Urnstone grave)
        {
            if (grave == null || grave.HasBeenDestroyed || grave.DeadSimsDescription == null) return false;
            if (grave.DeadSimsDescription.CreatedSim != null) return true;
            return grave.GhostSpawn(false, wisher.Position, wisher.LotCurrent, false, 5f);
        }
    }
}
