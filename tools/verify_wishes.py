"""Source contracts for wish execution. These checks do not run The Sims 3."""
import json
import re
from pathlib import Path
import cs_source

ROOT = Path(__file__).resolve().parents[1]


def check(root=ROOT):
    catalog = cs_source.file_code(root, 'WishCatalog')
    effects = cs_source.class_code(root, 'WishEffects')
    grants = cs_source.class_code(root, 'GenieWishes')
    interaction = cs_source.class_code(root, 'GrantWish')
    names = re.findall(r'new WishDefinition\(WishKind\.(\w+),', catalog)
    assert len(names) == len(set(names)) == 14
    eligible = effects.split('internal static Occupation Career')[0]
    success = effects.split('internal static bool Apply(')[1].split('private static bool Fail(')[0]
    failure = effects.split('private static bool Fail(')[1].split('private static bool SpawnGhost(')[0]
    for name in names:
        for label, source in [('eligibility', eligible), ('success', success), ('failure', failure)]:
            assert 'case WishKind.' + name + ':' in source, name + ' missing ' + label
    assert 'WishRules.Fortune' in success and 'ModifyFamilyFunds' not in failure
    assert 'WishRepossession.Request(wisher)' in failure and 'WishLamp.Escape(genie, wisher)' in failure
    assert 'YoungAgainPotion { IsMixedDrink = true }.DoDrinkBehavior(wisher, false)' in success and 'YoungAgainPotion { IsMixedDrink = true }.UsePotionTest' in effects
    assert 'ResetAndExtendAgingStage(0f)' in failure and 'ExtendAgingState(7f)' in failure
    assert 'career.PromoteSim()' in success and 'career.Level == before + 1' in success
    assert 'ForceSetLevel(level + 1)' in success and 'DisgracefulActionType.FalselyAccused' in failure
    assert 'relationship.GenieWishForLove()' in success and 'GenieWishForLove' not in failure
    assert 'WishBuffs.OneSidedLove' in failure and 'SetTargetSim(sim)' in failure
    assert 'GhostToSim(ghost, true, !ghost.SimDescription.IsPlayableGhost)' in success and 'SpawnGhost(wisher, selected.Grave)' in failure
    for buff in ['WishForWorldPeace', 'WishForWorldMisery', 'WishHappiness', 'WishForLargeFamily']:
        assert buff in success
    assert grants.index('WishEffects.Eligible') < grants.index('RandomUtil.RandomChance')
    assert grants.index('if (!applied)') < grants.index('MagicCasters.SpendInnate')
    assert grants.index('if (!applied)') < grants.index('skill.GuaranteeReady =')
    assert 'RollWishBacklash' in grants and 'CommitWishBacklash' in grants
    assert 'mode != WishMode.Own && mode != WishMode.Lamp && !guaranteed' in grants
    assert 'committed = true;' in interaction and 'executing || committed' in interaction
    assert 'DisableSave(this,' in interaction and 'finally' in interaction and 'EnableSave(this)' in interaction
    assert 'RouteToObjectRadiusAndCheckInUse' not in interaction and 'a_genie_clap_x' in interaction
    assert 'StartSync(true, false' in interaction and 'WishRendezvous.Approach' in interaction
    assert 'new Definition(wish.Kind, WishMode.Guaranteed)' not in interaction
    dialog = cs_source.file_code(root, 'GenieWishDialog')
    assert 'GenieWishSelectionDialog.Show(choices)' in dialog
    assert 'WishEffects.Eligible(genie, wisher, entry.Kind, mode)' in dialog
    assert 'OccultGenie.CleanSim.Singleton.GetPath(female)' in dialog
    assert 'field.SetValue(null, GrantWish.LampSingleton);' in dialog
    assert 'sim.Interactions.RemoveAll' in dialog and 'IsLampWish(pair.InteractionDefinition)' in dialog
    assert 'GenieWishDialog.Show(genie, wisher, definition.Mode)' in interaction
    assert 'if (choice == null) return false;' in interaction
    assert interaction.index('GenieWishDialog.Show') < interaction.index('committed = true;')
    assert 'new Definition(WishMode.Other), new Definition(WishMode.Own)' in interaction
    assert 'if (Mode == WishMode.Lamp) return new string[0];' in interaction
    repo = cs_source.file_code(root, 'WishRepossession')
    for native in ['Repoman.Instance', 'MakeServiceRequest', 'ServiceSituation.FindServiceSituationInvolving',
                   'RepomanSituation.Repossess', 'base.Run()', 'RouteRepomanToObject', 'TeleportRepomanToObject']:
        assert native in repo
    assert 'int value = Target.Value;' in repo and 'household.DelinquentFunds += WishRules.RepossessionCredit(debt, household.DelinquentFunds, value);' in repo
    assert 'DisableSave(this,' in repo and 'EnableSave(this)' in repo
    lamp = cs_source.file_code(root, 'WishLamp')
    for native in ['PlayReturnToLamp()', 'TryToRemove(lamp)', 'TryToAdd(lamp)', 'FreedomSteps', 'sHandoffs', 'Household.ActiveHousehold']:
        assert native in lamp
    assert not re.search(r'(?:genie|lamp)\.(?:Destroy|Kill)\(', lamp, re.I)
    state = cs_source.file_code(root, 'WishState')
    assert 'WishState.JoyExpired(manager.Actor)' in cs_source.file_code(root, 'WishBuffs')
    assert 'career.Guid != state.CareerGuid' in state and 'CareerExpires' in state and 'WaitForCurrentShift' in state
    assert 'career.UpdatePerformanceOrExperience(-withheld * units)' in state
    assert 'career.Level != state.LastCareerLevel' in state
    death = cs_source.file_code(root, 'WishDeath')
    assert 'ReapSoulDiving' in death and 'ReapSoul' in death and 'return base.Run()' in death
    assert 'DeathType.OldAge' in death and 'record.Escapes++' in death and 'CountEscape' in death
    assert 'GhostToSim(Target, false, false)' in death
    bootstrap = cs_source.class_code(root, 'JournalBootstrap')
    for owner in ['WishLamp', 'WishRepossession']:
        assert owner + '.ResetSaved();' in bootstrap
    assert 'sim.SkillManager.GetElement(WishEffectSkill.WishGuid)' in state
    assert 'WishState.Start();' in bootstrap and 'WishState.Stop();' in bootstrap
    strings = json.loads((root / 'src/Localization/en-US/strings.json').read_text())
    assert not any(key.endswith(".Description") and key.startswith("Arcana.Genie.Wishes.") for key in strings)
    assert 'GenieWishInstance(name, NativeDescriptionName(kind, evil), null, false, female).GetWishDescription()' in dialog
    assert 'CostText(' not in dialog
    assert 'BuffNames.Disappointed' not in effects and 'BuffNames.RejectedProposal' not in effects
    assert 'WishBuffs.GiveWrong' in failure and 'WishBuffs.GiveBeautyFailure' in failure
    assert 'WishRules.CanReceive' in effects and 'WishEffects.CanReceive(wisher)' in lamp
    print('PASS: all 14 wish execution branches; native effect/service bindings; single outcome/cost/guarantee order; persistence lifecycle and submenu contracts')
    print('Not tested here: native effects, routing, animation, service arrival, death recovery, save/load or travel inside The Sims 3.')


if __name__ == '__main__':
    check()
