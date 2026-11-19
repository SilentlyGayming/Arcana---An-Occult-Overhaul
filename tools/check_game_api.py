import re
import struct
from dataclasses import dataclass

ASSEMBLIES = ("UI", "SimIFace", "Sims3GameplaySystems", "Sims3GameplayObjects", "Sims3MetaData")
PUBLIC, NESTED_PUBLIC, METHOD_PUBLIC = 1, 2, 6
CLR_V2 = b"v2.0.50727"


def metadata(data):
    # Locates the CLR metadata tables and string heap in a managed PE32 image.
    if len(data) < 512 or data[:2] != b"MZ":
        raise ValueError("not a PE image")
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("invalid PE signature")
    machine, sections = struct.unpack_from("<HH", data, pe + 4)
    optional_size = struct.unpack_from("<H", data, pe + 20)[0]
    optional = pe + 24
    magic = struct.unpack_from("<H", data, optional)[0]
    if (machine, magic) != (0x14C, 0x10B):
        raise ValueError("expected an x86 or AnyCPU managed PE32 assembly")
    if optional_size < 96 + 15 * 8:
        raise ValueError("no CLR directory")
    clr = struct.unpack_from("<I", data, optional + 96 + 14 * 8)[0]
    if not clr:
        raise ValueError("PE image is not managed")
    spans = [struct.unpack_from("<IIII", data, optional + optional_size + i * 40 + 8)
             for i in range(sections)]

    def offset(rva):
        for virtual_size, start, raw_size, raw in spans:
            if start <= rva < start + max(virtual_size, raw_size):
                result = rva - start + raw
                if rva - start < raw_size and result < len(data):
                    return result
        raise ValueError("PE RVA outside file data")

    base = offset(struct.unpack_from("<I", data, offset(clr) + 8)[0])
    if data[base:base + 4] != b"BSJB":
        raise ValueError("invalid CLR metadata signature")
    length = struct.unpack_from("<I", data, base + 12)[0]
    version = data[base + 16:base + 16 + length].rstrip(b"\0")
    if version != CLR_V2:
        raise ValueError("expected CLR v2 metadata, got " + repr(version))
    position = base + 16 + ((length + 3) & ~3)
    stream_count = struct.unpack_from("<H", data, position + 2)[0]
    position += 4
    streams = {}
    for _ in range(stream_count):
        start, size = struct.unpack_from("<II", data, position)
        end = data.index(b"\0", position + 8)
        name = data[position + 8:end].decode("ascii")
        if base + start + size > len(data):
            raise ValueError("truncated CLR metadata stream " + name)
        streams[name] = base + start
        position = (end + 4) & ~3
    table = streams.get("#~", streams.get("#-"))
    if table is None or "#Strings" not in streams:
        raise ValueError("missing metadata tables or string heap")
    return table, streams["#Strings"]


def module_name(data):
    table, strings = metadata(data)
    valid = struct.unpack_from("<Q", data, table + 8)[0]
    if not valid & 1 or struct.unpack_from("<I", data, table + 24)[0] != 1:
        raise ValueError("expected one Module metadata row")
    first_row = table + 24 + 4 * bin(valid).count("1")
    string_size = 4 if data[table + 6] & 1 else 2
    position = strings + int.from_bytes(data[first_row + 2:first_row + 2 + string_size], "little")
    end = data.index(b"\0", position)
    name = data[position:end].decode("utf-8")
    if not re.fullmatch(r"[\w.\-]+\.dll", name, re.IGNORECASE):
        raise ValueError("invalid .NET module name: " + repr(name))
    return name


@dataclass
class TypeInfo:
    flags: int
    fields: dict
    methods: dict

    def public_member(self, name):
        return (self.fields.get(name) == METHOD_PUBLIC
                or any(METHOD_PUBLIC in self.methods.get(prefix + name, ())
                       for prefix in ("", "get_", "set_", "add_", "remove_")))


def read_types(data):
    table, strings = metadata(data)
    heap = data[table + 6]
    valid = struct.unpack_from("<Q", data, table + 8)[0]
    cursor, rows = table + 24, {}
    for number in range(64):
        if valid >> number & 1:
            rows[number] = struct.unpack_from("<I", data, cursor)[0]
            cursor += 4
    count = lambda number: rows.get(number, 0)
    string_size, guid_size, blob_size = (4 if heap & bit else 2 for bit in (1, 2, 4))

    def index(*tables):
        return 4 if max(count(t) for t in tables) >= 0x10000 else 2

    def coded(bits, tables):
        return 4 if max(count(t) for t in tables) >= 1 << (16 - bits) else 2

    scope, extends = coded(2, [0, 0x1A, 0x23, 1]), coded(2, [2, 1, 0x1B])
    parent = coded(3, [2, 1, 0x1A, 6, 0x1B])
    sizes = {0: 2 + string_size + 3 * guid_size, 1: scope + 2 * string_size,
             2: 4 + 2 * string_size + extends + index(4) + index(6), 3: index(4), 4: 2 + string_size + blob_size,
             5: index(6), 6: 4 + 2 + 2 + string_size + blob_size + index(8), 7: index(8), 8: 4 + string_size,
             9: index(2) + extends, 10: parent + string_size + blob_size}
    offsets, cursor = {}, cursor
    for number in range(11):
        offsets[number] = cursor
        cursor += sizes[number] * count(number)

    def read(where, size):
        return int.from_bytes(data[where:where + size], "little")

    def text(number):
        end = data.index(b"\0", strings + number)
        return data[strings + number:end].decode("utf8", "replace")

    starts = []
    for i in range(count(2)):
        row = offsets[2] + i * sizes[2]
        flags = read(row, 4)
        name, namespace = text(read(row + 4, string_size)), text(read(row + 4 + string_size, string_size))
        first_field = read(row + 4 + 2 * string_size + extends, index(4))
        first_method = read(row + 4 + 2 * string_size + extends + index(4), index(6))
        starts.append((namespace, name, flags, first_field, first_method))

    types = {}
    for i, (namespace, name, flags, first_field, first_method) in enumerate(starts):
        last_field = starts[i + 1][3] if i + 1 < len(starts) else count(4) + 1
        last_method = starts[i + 1][4] if i + 1 < len(starts) else count(6) + 1
        fields, methods = {}, {}
        for field in range(first_field, last_field):
            row = offsets[4] + (field - 1) * sizes[4]
            fields[text(read(row + 2, string_size))] = read(row, 2) & 7
        for method in range(first_method, last_method):
            row = offsets[6] + (method - 1) * sizes[6]
            member = text(read(row + 8, string_size))
            methods.setdefault(member, set()).add(read(row + 6, 2) & 7)
        types[(namespace, name, i)] = TypeInfo(flags, fields, methods)
    return types


def read_assembly(path):
    return read_types(path.read_bytes())


def load(root_dir):
    folder = root_dir / ".deps" / "Sims3"
    found = {}
    for name in ASSEMBLIES:
        path = folder / (name + ".dll")
        if path.exists():
            found[name] = read_assembly(path)
    return found


def find(found, qualified, allow_private_nested=False):
    # Looks up a top-level type or a named nested type on a public owner.
    owner, _, nested = qualified.partition("/")
    namespace, _, name = owner.rpartition(".")
    for assembly in found.values():
        for (ns, nm, i), value in assembly.items():
            if (ns, nm) == (namespace, name):
                if not nested:
                    return value
                step = i + 1
                following = {k[2]: (k, v) for k, v in assembly.items()}
                while step in following and following[step][0][0] == "":
                    (_, nm2, _), value2 = following[step]
                    if nm2 == nested and (allow_private_nested or value2.flags & 7 == NESTED_PUBLIC):
                        return value2
                    step += 1
    return None


MEMBERS = (
    ("Sims3.UI.UIManager", "BeginModal EndModal IsModal GetModalWindow DarkenBackground GetFocus SetFocus LoadLayoutAndAddToWindow"),
    ("Sims3.UI.WindowBase", "AddModalTriggerHook RemoveTriggerHook WindowToScreen Disposed Visible Area FadeTransitionFinished "
                            "AreaChange TriggerDown IgnoreMouse Invalidate"),
    ("Sims3.UI.StdDrawable", "ScaleType ScaleAmount ScaleArea"),
    ("Sims3.UI.StdDrawable/ScalingType", "StretchCenter StretchImage TileCenter TileImage"),
    ("Sims3.UI.Hud.IconZoop", "Zoop"),
    ("Sims3.UI.ImageDrawable", "Image"),
    ("Sims3.UI.Window", "Drawable"),
    ("Sims3.UI.Responder", "Instance"),
    ("Sims3.UI.IResponder", "ClockSpeedModel HudModel"),
    ("Sims3.UI.ISimClockSpeedModel", "GameSpeedLocked LockGameSpeed UnlockGameSpeed"),
    ("Sims3.SimIFace.Audio", "StartSound"),
    ("Sims3.SimIFace.World", "OnWorldLoadFinishedEventHandler OnWorldLoadStartedEventHandler OnWorldQuitEventHandler "
                            "OnObjectPlacedInLotEventHandler"),
    ("Sims3.Gameplay.Abstracts.GameObject", "AddInteraction GetObject SetGeometryState"),
    ("Sims3.Gameplay.Abstracts.ScriptObject", "HasBeenDestroyed"),
    ("Sims3.Gameplay.Skills.SkillManager", "AddElement StartSkillGain StopSkillGain"),
    ("Sims3.Gameplay.Skills.Skill", "ReachedMaxLevel"),
    ("Sims3.SimIFace.StringTable", "GetLocalizedString GetLocale"),
    ("Sims3.SimIFace.ResourceUtils", "HashString64"),
    ("Sims3.SimIFace.Gameflow/GameSpeed", "Pause"),
    ("Sims3.UI.TriggerActivationMode", "kPermanent"),
    ("Sims3.UI.InputContext", "kICKeyboard"),
)

NAMESPACES = {
    "Sims3.UI.Hud": ("IconZoop", "OccultTypes", "IHudModel"),
    "Sims3.UI": ("UIImage", "ImageDrawable", "ISimClockSpeedModel", "UIManager", "StdDrawable", "DrawableBase"),
    "Sims3.SimIFace": ("Audio", "ResetException", "Vector2", "Rect"),
}


def require_ui_contract(ui_types):
    # Verifies the native dialog override contract and the reflected UI entry points.
    types = {"UI": ui_types}
    modal = find(types, "Sims3.UI.ModalDialog")
    if modal is None or modal.methods.get("EndDialog") not in ({4}, {5}):
        raise ValueError("UI.dll has the wrong ModalDialog.EndDialog access. "
                         "Use the original Sims 3 1.67.2 game assembly; older publicized references are incompatible.")

    bindings = (
        ("Hud.HudController", "get_Instance add_InfoStateChange remove_InfoStateChange", ""),
        ("Hud.SimologyPanel", "get_Instance", ""),
        ("Hud.SimDisplay", "get_Instance", "mMagicMotiveBarWindow mMagicMotiveSizeControlWin mMagicMotiveSizeReferenceWin"),
        ("WindowBase", "get_WinHandle add_InternalWindowShutdown remove_InternalWindowShutdown", ""),
        ("UIManager", "GetDrawableProperty", ""),
        ("Layout", "get_LayoutHandle", ""),
        ("UIImage", "get_UIImageHandle", ""),
        ("ModalDialog", "", "mPauseMode"),
    )
    for owner, methods, fields in bindings:
        info = find(types, "Sims3.UI." + owner)
        if info is None:
            raise ValueError("UI.dll is missing Sims3.UI." + owner)
        missing = (set(methods.split()) - info.methods.keys()) | (set(fields.split()) - info.fields.keys())
        if missing:
            raise ValueError("UI.dll is missing " + owner + "." + ", ".join(sorted(missing)))
    window = find(types, "Sims3.UI.WindowBase")
    if any(window.methods.get(accessor) != {METHOD_PUBLIC}
           for accessor in ("add_AreaChange", "remove_AreaChange")):
        raise ValueError("UI.dll lacks the public WindowBase.AreaChange event")


def require_genie_contract(gameplay_types):
    points = find({"Sims3GameplaySystems": gameplay_types}, "Sims3.Gameplay.ActorSystems.OccultMagicPointSystem")
    if points is None or not {"mMaxMagicPoints", "mCurrentMagicPointValue"} <= points.fields.keys():
        raise ValueError("Sims3GameplaySystems.dll is missing the native Genie magic point fields")


def require_witch_contract(gameplay_types):
    # Checks the reflected Witch bindings used by the cast and motive adapters.
    types = {"Sims3GameplaySystems": gameplay_types}
    wand = "Sims3.Gameplay.Objects.Alchemy.MagicWand"
    bindings = [
        (wand + "/CastSpell", "AdditionalInteractionPreparation EpicFailureAllowed ShowSuccessVfx",
         "mJig mWand mSpellEffect mHitEffect mSpellCastingSucceeded mSpellCastingEpiclyFailed "
         "kCloseEnoughToPlaceJig kDistanceToForceRouteAway"),
        (wand + "/BeTargetOfSpell", "set_Caster set_Jig", "Singleton"),
        ("Sims3.Gameplay.Interactions.InteractionInstance", "", "mSkillMultipliers"),
        ("Sims3.Gameplay.Autonomy.MotiveTuning", "", "mBuffTriggers mMotiveDecayCurve"),
    ]
    bindings += [
        (wand, "DrainMotive get_MotiveDrainMultiplier", "kMoonPhaseMotiveDrainMultiplier kFamiliarMotiveMultiplier"),
        ("Sims3.Gameplay.Objects.Alchemy.MagicHands", "", "kMotiveDrainMultiplier"),
        ("Sims3.Gameplay.ActorSystems.TraitFunctions/ConjureApple", "", "Singleton"),
        ("Sims3.Gameplay.Autonomy.InteractionTuning", "", "mTradeoff"),
        ("Sims3.Gameplay.Autonomy.Tradeoff", "", "mOutputs"),
        ("Sims3.Gameplay.Autonomy.CommodityChange", "get_Commodity", "mActualValue"),
        ("Sims3.Gameplay.Interactions.InteractionInstance", "", "InteractionObjectPair"),
        ("Sims3.Gameplay.Actors.Sim", "", "SoloInteractions"),
        ("Sims3.Gameplay.UI.HudModel", "FlashMagicMotiveBar", "MagicMotiveChanged"),
        (wand + "/CastFireBlast", "OnSpellSuccess OnSpellEpicFailure", "Singleton"),
        (wand + "/CastToadify", "OnSpellSuccess OnSpellEpicFailure", "Singleton"),
        (wand + "/CastSpell", "CommonSpellTests", ""),
        ("Sims3.Gameplay.ActorSystems.Posture", "AllowsNormalSocials", ""),
        ("Sims3.Gameplay.CAS.SimDescription", "get_IsEP11Bot get_ShowSocialsOnSim get_IsZombie get_ChildOrBelow get_IsPet get_IsGhost get_IsPregnant",
         "AssignedRole"),
        ("Sims3.Gameplay.Actors.Sim", "get_CanBeSocializedWith get_Service get_CurrentInteraction", ""),
        ("Sims3.Gameplay.Socializing.SocialComponent", "IsInServicePreventingSocialization", ""),
        ("Sims3.Gameplay.Socializing.IPreventSocialization", "SocializationAllowed", ""),
        ("Sims3.Gameplay.ActorSystems.BuffManager", "HasElement HasAnyElement", ""),
        ("Sims3.Gameplay.ActorSystems.OccultManager", "HasAnyOccultType HasOccultType", ""),
        ("Sims3.Gameplay.Utilities.Localization", "LocalizeString", ""),
        ("Sims3.Gameplay.Skills.Skill/NonPersistableSkillData", "",
         "Name Description ThoughtBalloonTopicString Commodity MaxSkillLevel PointsForNextLevel LevelUpStrings"),
        ("Sims3.Gameplay.Skills.SkillManager", "StartSkillGain StopSkillGain ShowSkillMeter HideSkillMeter", ""),
        ("Sims3.Gameplay.Inventory", "FindAll TryToAdd TryToRemove", ""),
        ("Sims3.Gameplay.Abstracts.GameObject", "get_Value get_PurchasedPrice get_Cost", "mPurchasedPrice mIntPrice"),
        ("Sims3.Gameplay.Controllers.FireManager", "UnregisterFire NotifyPlayerOfFireOnLot", "mHouseholdValuePreFire"),
        ("Sims3.Gameplay.GlobalFunctions", "CreateObject", ""),
        ("Sims3.Gameplay.Interactions.InteractionGameObjectHit`2", "Init GetInteractionParameters", "Hit"),
        ("Sims3.Gameplay.Core.FailureObject", "", ""),
        ("Sims3.Gameplay.Core.LotManager", "GetLotAtPoint", ""),
        ("Sims3.Gameplay.Core.Lot", "get_LotId", ""),
        ("Sims3.Gameplay.Interfaces.ISculptureFrozenSim", "GetFrozenSim", ""),
        ("Sims3.Gameplay.ObjectComponents.UpgradableComponent", "get_Unbreakable", ""),
        ("Sims3.Gameplay.ObjectComponents.RepairableComponent", "get_Broken HasChanceOfBreaking", ""),
        ("Sims3.Gameplay.Interfaces.ITelevision", "get_HasAllChannels", ""),
        ("Sims3.Gameplay.Abstracts.GameObject", "GetContainedObject get_UpgradableComponent get_Repairable", ""),
        ("Sims3.Gameplay.Interactions.InteractionInstance", "RunInteraction GetInteractionParameters GetIconKey", ""),
        ("Sims3.Gameplay.ActorSystems.InteractionQueue", "GetCurrentInteraction PushRunningInteraction", ""),
    ]
    for native in NATIVE_DRAINS:
        bindings.append((wand + "/" + native, "", "Singleton"))
    for native in set(NATIVE_DRAINS.values()):
        bindings.append((wand + "/" + native, "", "kMotiveDrain"))
    for spell in ("GoodLuckCharm", "LoveCharm", "IceBlast", "SunlightCharm", "PestilenceCurse", "HauntingCurse"):
        bindings.append((wand + "/Cast" + spell,
                         "get_TypeOfSpell get_JazzStateName get_SuccessVfxName get_EpicFailVfxName get_HitVfxName "
                         "OnSpellSuccess OnSpellEpicFailure", "Singleton kMotiveDrain"))
    for owner, methods, fields in bindings:
        info = find(types, owner, allow_private_nested=True)
        if info is None:
            raise ValueError("Sims3GameplaySystems.dll is missing " + owner)
        missing = (set(methods.split()) - info.methods.keys()) | (set(fields.split()) - info.fields.keys())
        if missing:
            raise ValueError("Sims3GameplaySystems.dll is missing " + owner + "." + ", ".join(sorted(missing)))


NATIVE_DRAINS = {
    "CastFireBlast": "CastFireBlast", "CastFireBlastTerrain": "CastFireBlast", "CastFireBlastOnStatue": "CastFireBlast",
    "CastIceBlastTerrain": "CastIceBlast", "CastIceBlastPlumbing": "CastIceBlast", "CastSunlightCharmOnStatue": "CastSunlightCharm",
    "CastToadify": "CastToadify", "CastMotiveCharm": "CastMotiveCharm", "CastMotiveCurse": "CastMotiveCurse",
    "CastConvert": "CastConvert", "CastRestoration": "CastRestoration", "CastReanimate": "CastReanimate",
}
MAGIC_FATIGUE = 17774820
WRAPPED_SPELLS = ("CastGoodLuckCharm", "CastLoveCharm", "CastSunlightCharm", "CastPestilenceCurse", "CastFireBlast", "CastIceBlast",
                  "CastToadify", "CastHauntingCurse", "CastMotiveCharm", "CastMotiveCurse", "CastFireBlastTerrain", "CastIceBlastTerrain",
                  "CastIceBlastPlumbing", "CastFireBlastOnStatue", "CastSunlightCharmOnStatue", "CastReanimate", "CastConvert",
                  "CastRestoration", "MagicallyUpgrade")
SINGLETON_TYPES = {"ConjureApple": "Sims3.Gameplay.Interactions.ISoloInteractionDefinition"}


def require_native_cost_contract(root_dir):
    # Checks in the game's IL that native spells drain their kMotiveDrain through MagicWand.DrainMotive, which raises MagicMotiveChanged.
    import il_reader
    module = il_reader.Module(root_dir / ".deps" / "Sims3" / "Sims3GameplaySystems.dll")
    names = {name: index for index, name in module.typedefs().items()}
    wand = "Sims3.Gameplay.Objects.Alchemy.MagicWand"

    def body(owner, method):
        for index, name in module.methods_of(names[owner]):
            if name == method:
                return module.il(index)
        raise ValueError(f"Sims3GameplaySystems.dll is missing {owner}.{method}")

    for native, source in NATIVE_DRAINS.items():
        code = "\n".join(body(wand + "/" + native, "DrainMotives"))
        if (f"ldsfld {wand}/{source}::kMotiveDrain" not in code or f"ldc.i4 {MAGIC_FATIGUE}" not in code
                or f"callvirt {wand}::DrainMotive" not in code):
            raise ValueError(f"{native}.DrainMotives no longer drains {source}.kMotiveDrain through MagicWand.DrainMotive")
    upgrade = "\n".join(body(wand + "/MagicallyUpgrade", "Run"))
    if f"ldc.i4 {MAGIC_FATIGUE}" in upgrade or f"callvirt {wand}::DrainMotive" not in upgrade:
        raise ValueError("Magically Upgrade changed how it drains motives")
    for motive in ("CastMotiveCharm", "CastMotiveCurse"):
        definition = names[wand + "/" + motive + "/Definition"]
        fields = [name for _, name in module.fields_of(definition)]
        constructors = [index for index, name in module.methods_of(definition) if name == ".ctor"]
        stores = ["\n".join(module.il(index)) for index in constructors]
        if "mMotiveAffected" not in fields or not any("::mMotiveAffected" in code and "::mSpellLevel" in code for code in stores):
            raise ValueError(f"{motive}.Definition no longer takes a motive and spell level")
    for owner in [wand + "/" + name for name in WRAPPED_SPELLS] + ["Sims3.Gameplay.ActorSystems.TraitFunctions/ConjureApple"]:
        declared = module.field_type(names[owner], "Singleton")
        allowed = SINGLETON_TYPES.get(owner.split("/")[-1], "Sims3.Gameplay.Interactions.InteractionDefinition")
        if declared != allowed:
            raise ValueError(f"{owner}.Singleton is typed {declared}; Arcana's wrapper must implement it")
    if "SelectedUpgrade" not in [name for _, name in module.fields_of(names[wand + "/MagicallyUpgrade/Definition"])]:
        raise ValueError("MagicallyUpgrade.Definition no longer has SelectedUpgrade")
    drain = "\n".join(body(wand, "DrainMotive"))
    flash = "\n".join(body("Sims3.Gameplay.UI.HudModel", "FlashMagicMotiveBar"))
    if "Sims3.Gameplay.UI.HudModel::FlashMagicMotiveBar" not in drain or "OccultUpdatedCallback::Invoke" not in flash:
        raise ValueError("MagicWand.DrainMotive no longer raises HudModel.MagicMotiveChanged synchronously")


def require_station_contract(objects_types):
    # Checks the native Alchemy Station types that Study Spells builds on.
    types = {"Sims3GameplayObjects": objects_types}
    station = "Sims3.Gameplay.Objects.Alchemy.AlchemyStation"
    bindings = (
        (station, "OnStartup StartLitCandlesVFX StopLitCandlesVFX FireOneShotCandleExtinguishVFX", ""),
        (station + "/ResearchAlchemy", "Run Cleanup", "Singleton"),
        (station + "/MixPotion", "", "Singleton"),
    )
    for owner, methods, fields in bindings:
        info = find(types, owner, allow_private_nested=True)
        if info is None:
            raise ValueError("Sims3GameplayObjects.dll is missing " + owner)
        missing = (set(methods.split()) - info.methods.keys()) | (set(fields.split()) - info.fields.keys())
        if missing:
            raise ValueError("Sims3GameplayObjects.dll is missing " + owner + "." + ", ".join(sorted(missing)))


def check(root_dir):
    found = load(root_dir)
    if len(found) != len(ASSEMBLIES):
        return False
    require_station_contract(found["Sims3GameplayObjects"])
    require_ui_contract(found["UI"])
    require_genie_contract(found["Sims3GameplaySystems"])
    require_witch_contract(found["Sims3GameplaySystems"])
    require_native_cost_contract(root_dir)
    for qualified, names in MEMBERS:
        entry = find(found, qualified)
        assert entry is not None and entry.flags & 7 in (PUBLIC, NESTED_PUBLIC), qualified
        for name in names.split():
            assert entry.public_member(name), f"{qualified}.{name} is missing or not public in the reference assemblies"
    for namespace, names in NAMESPACES.items():
        for name in names:
            entry = find(found, f"{namespace}.{name}")
            assert entry is not None and entry.flags & 7 == PUBLIC, f"{name} is not public in {namespace}"

    statics = {"UIManager": "Sims3.UI.UIManager", "Audio": "Sims3.SimIFace.Audio", "IconZoop": "Sims3.UI.Hud.IconZoop"}
    for path in sorted((root_dir / "src").rglob("*.cs")):
        text = re.sub(r'"(?:[^"\\\n]|\\.)*"', '""', re.sub(r"//[^\n]*", "", path.read_text(encoding="utf-8-sig")))
        for owner, member in re.findall(r"(?<![.\w])(UIManager|Audio|IconZoop)\.(\w+)", text):
            assert find(found, statics[owner]).public_member(member), f"{path.name}: {owner}.{member} is missing or not public"
        for nested, owner in (("GameSpeed", "Gameflow"), ("TickStyles", "StopWatch")):
            assert not re.search(r"(?<![.\w])" + nested + r"\b", text), \
                f"{path.name}: {nested} is nested in {owner}; write Sims3.SimIFace.{owner}.{nested}"
        assert not re.search(r"\.\s*(?:LayoutHandle|UIImageHandle|WinHandle|InternalWindowShutdown|mPauseMode|mMaxMagicPoints|mCurrentMagicPointValue)\b", text), \
            f"{path.name}: direct access to a non-public Sims 3 member"
        assert not re.search(r"\b(?:HudController|SimDisplay)\.Instance\b", text), \
            f"{path.name}: native singleton is internal"
    return True
