using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.SimIFace;
using SupernaturalExpanded.Journal;

internal static class SpellKnowledgeTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static void Main(string[] args)
    {
        HashSet<string> ids = new HashSet<string>();
        int starting = 0, disabled = 0, study = 0;
        foreach (SpellEntry entry in SpellCatalog.Entries)
        {
            Check(ids.Add(entry.Id), "Duplicate catalog ID");
            Check(entry.Tier >= 0 && entry.Tier < 5 && entry.Category != SpellCategory.Ability, "Ordinary spell has invalid tier/category");
            if (entry.Learning == SpellLearning.Starting) starting++;
            else if (entry.Learning == SpellLearning.NativeDisabled) disabled++;
            else study++;
        }
        Check(ids.Count == 24 && starting == 2 && disabled == 6 && study == 16, "Unexpected catalog coverage/policies");
        string[] charmIds = { "good_luck", "love", "charm_bladder", "charm_hunger", "charm_hygiene", "sunlight" };
        int[] charmTiers = { 0, 2, 2, 2, 2, 3 };
        for (int i = 0; i < charmIds.Length; i++)
        {
            SpellEntry charm = SpellCatalog.Find(charmIds[i]);
            Check(charm != null && charm.Category == SpellCategory.Charm, "Enabled charm is not in Charms: " + charmIds[i]);
            Check(charm.Tier == charmTiers[i] && charm.Learning == SpellLearning.Study, "Charm tier or learning policy changed: " + charmIds[i]);
        }
        foreach (string disabledCharm in new string[] { "charm_energy", "charm_fun", "charm_social" })
            Check(SpellCatalog.Find(disabledCharm).Category == SpellCategory.Charm
                && SpellCatalog.Find(disabledCharm).Learning == SpellLearning.NativeDisabled, "Cut charm metadata changed: " + disabledCharm);
        string[][] kept = {
            new string[] { "fire_blast", "ice_blast", "upgrade" },
            new string[] { "pestilence", "toadification", "haunting", "curse_bladder", "curse_hunger", "curse_energy", "curse_hygiene", "curse_fun", "curse_social" },
            new string[] { "conversion", "restoration", "reanimation" } };
        SpellCategory[] keptCategories = { SpellCategory.Spell, SpellCategory.Curse, SpellCategory.Ritual };
        for (int i = 0; i < kept.Length; i++)
            foreach (string id in kept[i]) Check(SpellCatalog.Find(id).Category == keptCategories[i], "Non-charm category changed: " + id);
        int charms = 0;
        foreach (SpellEntry entry in SpellCatalog.Entries) if (entry.Category == SpellCategory.Charm) charms++;
        Check(charms == 9, "Unexpected number of registered charms");
        List<string> novice = new List<string>();
        foreach (SpellEntry entry in SpellCatalog.Entries)
            if (SpellCatalog.Discoverable(entry, 0, false)) novice.Add(entry.Id);
        Check(novice.Count == 1 && novice[0] == "good_luck", "First Novice discovery is not Good Luck Charm");
        Check(!SpellCatalog.Discoverable(SpellCatalog.Find("good_luck"), 4, true), "Known spell reentered research");
        Check(!SpellCatalog.Discoverable(SpellCatalog.Find("love"), 1, false), "Higher-tier spell entered research");
        Check(SpellCatalog.Discoverable(SpellCatalog.Find("love"), 2, false), "Rank did not unlock research eligibility");
        SpellEntry legendary = new SpellEntry("future_legendary", 5, SpellCategory.Ritual, SpellLearning.Study, "", -1, 0f);
        Check(!SpellCatalog.Discoverable(legendary, 99, false), "Legendary entered ordinary study");
        foreach (SpellEntry entry in SpellCatalog.Entries)
            if (entry.Learning != SpellLearning.Study) Check(!SpellCatalog.Discoverable(entry, 4, false), "Non-study policy leaked");

        string[] costIds = { "fire_blast", "ice_blast", "upgrade", "good_luck", "charm_bladder", "charm_hunger", "charm_hygiene", "love",
            "sunlight", "toadification", "haunting", "curse_bladder", "curse_hunger", "curse_hygiene", "pestilence", "conversion",
            "restoration", "reanimation" };
        float[] costs = { 12f, 16f, 30f, 30f, 30f, 34f, 30f, 48f, 72f, 52f, 62f, 36f, 40f, 36f, 88f, 45f, 100f, 110f };
        for (int i = 0; i < costIds.Length; i++)
            Check(SpellCatalog.Find(costIds[i]).Cost == costs[i], "Fixed base cost changed: " + costIds[i]);
        Check(SpellCatalog.ConjureAppleCost == 6f, "Conjure Apple fixed cost changed");
        int enabled = 0;
        foreach (SpellEntry entry in SpellCatalog.Entries)
        {
            if (entry.Learning == SpellLearning.NativeDisabled) continue;
            enabled++;
            Check(entry.Cost > 0f, "Enabled entry has no cost: " + entry.Id);
            Check(entry.Cost * 1.3f <= 50f + 25f * entry.Tier, "Cost does not fit the intended rank's pool with modifiers: " + entry.Id);
        }
        Check(enabled == costIds.Length, "An enabled entry has no fixed cost assertion");

        float next = 30f;
        Check(!SpellKnowledgeRecord.ResearchDue(29.9f, ref next, 30f), "Research rolled early");
        Check(SpellKnowledgeRecord.ResearchDue(30f, ref next, 30f) && next == 60f, "First research interval failed");
        Check(!SpellKnowledgeRecord.ResearchDue(30f, ref next, 30f), "Repeated callback duplicated a roll");
        Check(SpellKnowledgeRecord.ResearchDue(60f, ref next, 30f), "Failed roll prevented subsequent research");
        Check(SpellKnowledgeRecord.ResearchDue(360f, ref next, 30f) && next == 390f, "Long loop stall broke scheduling");
        Check(!SpellKnowledgeRecord.ResearchDue(float.NaN, ref next, 30f), "Invalid loop time rolled");
        List<string> normalized = SpellKnowledgeRecord.Normalize(new string[] { "good_luck", "", null, "good_luck", "future_spell" });
        Check(normalized.Count == 2 && normalized.Contains("future_spell"), "Normalization lost future IDs or retained duplicates");

        SpellKnowledgeSkill first = new SpellKnowledgeSkill();
        SpellKnowledgeSkill second = new SpellKnowledgeSkill();
        Check(first.Learn("good_luck") && !first.Learn("good_luck"), "Learning was not idempotent");
        Check(!second.Knows("good_luck"), "Independent Sim inherited discoveries");
        SpellKnowledgeSkill clone = (SpellKnowledgeSkill)first.Clone(null);
        Check(clone.Knows("good_luck") && clone.Learn("love") && !first.Knows("love"), "Skill clone shares a mutable spell list");
        foreach (SpellEntry entry in SpellCatalog.Entries) SpellCatalog.Discoverable(entry, 4, first.Knows(entry.Id));
        Check(!first.Knows("love"), "Eligibility taught a spell");
        MemoryStreamData stream = new MemoryStreamData();
        Check(first.ExportContent(stream), "Native skill export failed");
        SpellKnowledgeSkill restored = new SpellKnowledgeSkill();
        Check(restored.ImportContent(stream) && restored.Knows("good_luck") && !restored.Knows("love"), "Property-stream round trip failed");
        restored.Learn("haunting");
        Check(!first.Knows("haunting"), "Imported collection aliases original");
        MemoryStreamData old = new MemoryStreamData();
        old.Data[0xAC510002] = new string[] { "good_luck", "good_luck", null, "future_spell" };
        SpellKnowledgeSkill migrated = new SpellKnowledgeSkill();
        Check(migrated.ImportContent(old) && migrated.Knows("future_spell"), "Old data migration lost future IDs");
        MemoryStreamData migratedData = new MemoryStreamData(); migrated.ExportContent(migratedData);
        Check(((string[])migratedData.Data[0xAC510002]).Length == 2, "Duplicate IDs survived import");
        old.Data[0xAC510001] = 2;
        SpellKnowledgeSkill newer = new SpellKnowledgeSkill(); newer.ImportContent(old);
        Check(newer.Knows("good_luck") && !newer.Learn("love"), "Newer save schema was overwritten");
        SpellKnowledgeSkill missing = new SpellKnowledgeSkill();
        Check(missing.ImportContent(new MemoryStreamData()) && !missing.Knows("good_luck"), "Missing data inherited state");

        SpellKnowledgeSkill all = new SpellKnowledgeSkill();
        Check(all.LearnAllOrdinary() == 18, "Bulk cheat did not grant all ordinary knowledge");
        Check(all.LearnAllOrdinary() == 0, "Repeated bulk unlock duplicated knowledge");
        foreach (SpellEntry entry in SpellCatalog.Entries)
            Check(all.Knows(entry.Id) == (entry.Learning != SpellLearning.NativeDisabled), "Bulk unlock policy mismatch: " + entry.Id);
        Check(!SpellCatalog.CheatUnlockable(legendary), "Cheat admitted Legendary magic");
        Check(!second.Knows("love"), "Bulk cheat changed another Sim's record");
        Check(newer.LearnAllOrdinary() == -1 && !newer.Knows("love"), "Bulk cheat overwrote newer save data");
        SpellKnowledgeSkill partlyKnown = new SpellKnowledgeSkill();
        partlyKnown.Learn("fire_blast"); partlyKnown.Learn("ice_blast");
        Check(partlyKnown.LearnAllOrdinary() == 16, "Bulk cheat miscounted starting knowledge");
        MemoryStreamData unlockedStream = new MemoryStreamData(); all.ExportContent(unlockedStream);
        SpellKnowledgeSkill unlockedReload = new SpellKnowledgeSkill(); unlockedReload.ImportContent(unlockedStream);
        Check(unlockedReload.Knows("reanimation") && unlockedReload.Knows("love")
            && unlockedReload.LearnAllOrdinary() == 0, "Bulk-unlocked knowledge did not persist");

        Check(SpellCatalog.Find("fire_blast").Tier == 0 && SpellCatalog.Find("ice_blast").Tier == 0,
            "Approved starting elemental spells are not Tier I");
        Check(SpellCatalog.Find("fire_blast").Learning == SpellLearning.Starting
            && SpellCatalog.Find("ice_blast").Learning == SpellLearning.Starting, "Elemental starting knowledge changed");
        SpellKnowledgeSkill[] commandSims = { new SpellKnowledgeSkill(), new SpellKnowledgeSkill() };
        int selectedIndex = 0, executions = 0, usage = 0;
        SpellUnlockCommand callback = new SpellUnlockCommand(delegate
        {
            executions++;
            return commandSims[selectedIndex].LearnAllOrdinary() >= 0 ? 1 : -1;
        }, delegate { usage++; });
        Check(callback.Execute(new object[0]) == 1 && executions == 1
            && commandSims[0].Knows("love"), "Zero-argument console callback did not unlock immediately");
        Check(!commandSims[1].Knows("love"), "Console callback changed a nonselected Sim");
        Check(callback.Execute(null) == 1 && executions == 2
            && commandSims[0].LearnAllOrdinary() == 0, "Repeat console callback failed");
        Check(callback.Execute(new object[] { "unexpected" }) == -1 && usage == 1
            && executions == 2, "Console callback accepted unexpected arguments");
        selectedIndex = 1;
        Check(callback.Execute(new object[0]) == 1 && commandSims[1].Knows("reanimation"),
            "Console callback did not use the selected Sim at execution time");

        Assembly mod = Assembly.LoadFrom(args[0]);
        Type gates = mod.GetType("SupernaturalExpanded.Journal.SpellKnowledgeGates", true);
        MethodInfo resolve = gates.GetMethod("EntryFor", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo wrap = gates.GetMethod("Wrap", BindingFlags.Static | BindingFlags.NonPublic);
        string[] names = { "CastGoodLuckCharm", "CastLoveCharm", "CastSunlightCharm", "CastPestilenceCurse", "CastFireBlast", "CastIceBlast", "CastToadify", "CastHauntingCurse", "CastFireBlastTerrain", "CastIceBlastTerrain", "CastIceBlastPlumbing", "CastFireBlastOnStatue", "CastSunlightCharmOnStatue", "CastReanimate", "CastConvert", "CastRestoration", "MagicallyUpgrade" };
        string[] expected = { "good_luck", "love", "sunlight", "pestilence", "fire_blast", "ice_blast", "toadification", "haunting", "fire_blast", "ice_blast", "ice_blast", "fire_blast", "sunlight", "reanimation", "conversion", "restoration", "upgrade" };
        for (int i = 0; i < names.Length; i++)
        {
            Type owner = typeof(MagicWand).GetNestedType(names[i], BindingFlags.Public | BindingFlags.NonPublic);
            Type definition = owner.GetNestedType("Definition", BindingFlags.Public | BindingFlags.NonPublic);
            object native = Activator.CreateInstance(definition, true);
            object entry = resolve.Invoke(null, new object[] { native });
            Check((string)entry.GetType().GetField("Id", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry) == expected[i], "Wrong native spell binding: " + names[i]);
            if (expected[i] == "fire_blast" || expected[i] == "ice_blast")
                Check((int)entry.GetType().GetField("Tier", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry) == 0,
                    "Elemental target variant still uses Tier II: " + names[i]);
            object wrapper = wrap.Invoke(null, new object[] { native });
            Check(resolve.Invoke(null, new object[] { wrapper }) == entry, "Wrapper changed learned identity");
        }
        CommodityKind[] motives = { CommodityKind.Bladder, CommodityKind.Hunger, CommodityKind.Energy, CommodityKind.Hygiene, CommodityKind.Fun, CommodityKind.Social, CommodityKind.VampireThirst };
        foreach (CommodityKind motive in motives)
        {
            foreach (bool charm in new bool[] { true, false })
            {
                object definition = charm ? (object)new MagicWand.CastMotiveCharm.Definition(motive, 6) : new MagicWand.CastMotiveCurse.Definition(motive, 7);
                object entry = resolve.Invoke(null, new object[] { definition });
                string id = (string)entry.GetType().GetField("Id", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry);
                Check(id == (charm ? "charm_" : "curse_") + (motive == CommodityKind.VampireThirst ? "hunger" : motive.ToString().ToLowerInvariant()), "Wrong motive alias");
            }
        }
        NativeCommandBridge bridge = new NativeCommandBridge();
        AppDomain.CurrentDomain.SetData("CommandSystem", bridge);
        Type cheats = mod.GetType("SupernaturalExpanded.Journal.ArcanaCheats", true);
        MethodInfo register = cheats.GetMethod("Register", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo stop = cheats.GetMethod("Stop", BindingFlags.NonPublic | BindingFlags.Static);
        Check((bool)register.Invoke(null, new object[] { "Unlock spell help" }), "Cheat registration failed");
        Check(bridge.GetCommandHelp("arcanaunlockallspells") == "Unlock spell help", "Native lowercase help lookup cannot find cheat");
        CommandHandler rooted = (CommandHandler)cheats.GetField("Handler", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Check(object.ReferenceEquals(bridge.Handler, rooted) && rooted.Method.DeclaringType.FullName.EndsWith("SpellUnlockCommand"),
            "Registered callback is not the rooted, tested command handler");
        Check((bool)register.Invoke(null, new object[] { "Unlock spell help" }) && bridge.Registrations == 1,
            "Cheat registered twice while already available");
        bridge.Clear();
        Check((bool)register.Invoke(null, new object[] { "Unlock spell help" }) && bridge.Registrations == 2,
            "Cheat was not re-registered after native startup cleared it");
        stop.Invoke(null, null);
        Check(bridge.GetCommandHelp("arcanaunlockallspells") == null, "World exit left a registered cheat behind");

        string[] descriptors = { "GoodLuckCharm", "LoveCharm", "SunlightCharm", "PestilenceCurse", "IceBlast", "HauntingCurse" };
        string[] descriptorIds = { "good_luck", "love", "sunlight", "pestilence", "ice_blast", "haunting" };
        int[] descriptorKinds = { 4, 4, 4, 1, 0, 1 };
        for (int i = 0; i < descriptors.Length; i++)
        {
            Type type = mod.GetType("SupernaturalExpanded.Journal.TargetSpell+" + descriptors[i], true);
            object instance = Activator.CreateInstance(type, true);
            object spell = type.GetProperty("Spell", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance, null);
            object kind = spell.GetType().GetField("Category", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(spell);
            Check(Convert.ToInt32(kind.GetType().GetField("Kind", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(kind)) == descriptorKinds[i],
                "Arcana casting descriptor has the wrong casting kind: " + descriptors[i]);
            Check((int)kind.GetType().GetField("Tier", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(kind) == SpellCatalog.Find(descriptorIds[i]).Tier,
                "Arcana casting descriptor tier differs from the catalog: " + descriptors[i]);
        }

        int[] beforeDescriptor = (int[])MagicWand.kSpellLevels.Clone();
        Type iceDefinition = mod.GetType("SupernaturalExpanded.Journal.TargetSpell+IceBlast", true);
        object ice = Activator.CreateInstance(iceDefinition, true);
        object descriptor = iceDefinition.GetProperty("Spell", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ice, null);
        object category = descriptor.GetType().GetField("Category", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(descriptor);
        Check((int)category.GetType().GetField("Tier", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(category) == 0,
            "Ice Blast's actual Arcana casting descriptor still overreaches at Novice");
        for (int i = 0; i < beforeDescriptor.Length; i++)
            Check(MagicWand.kSpellLevels[i] == beforeDescriptor[i], "Arcana tier correction changed the separate native difficulty table");

        int[] levels = MagicWand.kSpellLevels;
        int[] values = (int[])levels.Clone();
        Type scopeType = gates.GetNestedType("AvailabilityScope", BindingFlags.NonPublic);
        IDisposable scope = (IDisposable)Activator.CreateInstance(scopeType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { true }, null);
        foreach (int level in MagicWand.kSpellLevels) Check(level == 0, "Availability scope did not bypass old unlock gates");
        scope.Dispose();
        Check(object.ReferenceEquals(levels, MagicWand.kSpellLevels), "Availability scope did not restore the native array");
        for (int i = 0; i < levels.Length; i++) Check(levels[i] == values[i], "Availability scope changed native difficulty");
        try
        {
            using ((IDisposable)Activator.CreateInstance(scopeType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { true }, null))
                throw new InvalidOperationException("Test native failure");
        }
        catch (InvalidOperationException) { }
        Check(object.ReferenceEquals(levels, MagicWand.kSpellLevels), "Native exception left unlock tuning altered");

        Console.WriteLine("PASS: " + checks + " spell catalog, timer, per-Sim data, clone, native property-stream and native definition binding checks");
        Console.WriteLine("These are host-side checks. They do not run the Sims 3 simulation, world saving, travel or UI.");
    }
}

internal sealed class MemoryStreamData : IPropertyStreamReader, IPropertyStreamWriter
{
 public readonly Dictionary<uint,object> Data = new Dictionary<uint,object>();
 public int Count { get { return Data.Count; } }
 public IPropertyStreamReader GetChild(uint key) { return null; }
 public IPropertyStreamReader AdoptChild(uint key,IPropertyStreamReader customReader) { return customReader; }
 public IPropertyStreamWriter AdoptChild(uint key,IPropertyStreamWriter customWriter) { return customWriter; }
 public IPropertyStreamWriter CreateChild(uint key) { return this; }
 public void CommitChild() {}
 public bool CancelChild(uint key) { return true; }
 public bool WriteBool(uint key,bool value) { Data[key]=value; return true; }
 public bool WriteBool(uint key,bool[] value) { Data[key]=value; return true; }
 public bool ReadBool(uint key,out bool value,bool defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (bool)stored : defaultValue; return found; }
 public bool ReadBool(uint key,out bool[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (bool[])stored : new bool[0]; return found; }
 public bool WriteInt16(uint key,short value) { Data[key]=value; return true; }
 public bool WriteInt16(uint key,short[] value) { Data[key]=value; return true; }
 public bool ReadInt16(uint key,out short value,short defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (short)stored : defaultValue; return found; }
 public bool ReadInt16(uint key,out short[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (short[])stored : new short[0]; return found; }
 public bool WriteInt32(uint key,int value) { Data[key]=value; return true; }
 public bool WriteInt32(uint key,int[] value) { Data[key]=value; return true; }
 public bool ReadInt32(uint key,out int value,int defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (int)stored : defaultValue; return found; }
 public bool ReadInt32(uint key,out int[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (int[])stored : new int[0]; return found; }
 public bool WriteInt64(uint key,long value) { Data[key]=value; return true; }
 public bool WriteInt64(uint key,long[] value) { Data[key]=value; return true; }
 public bool ReadInt64(uint key,out long value,long defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (long)stored : defaultValue; return found; }
 public bool ReadInt64(uint key,out long[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (long[])stored : new long[0]; return found; }
 public bool WriteUint16(uint key,ushort value) { Data[key]=value; return true; }
 public bool WriteUint16(uint key,ushort[] value) { Data[key]=value; return true; }
 public bool ReadUint16(uint key,out ushort value,ushort defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (ushort)stored : defaultValue; return found; }
 public bool ReadUint16(uint key,out ushort[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (ushort[])stored : new ushort[0]; return found; }
 public bool WriteUint32(uint key,uint value) { Data[key]=value; return true; }
 public bool WriteUint32(uint key,uint[] value) { Data[key]=value; return true; }
 public bool ReadUint32(uint key,out uint value,uint defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (uint)stored : defaultValue; return found; }
 public bool ReadUint32(uint key,out uint[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (uint[])stored : new uint[0]; return found; }
 public bool WriteUint64(uint key,ulong value) { Data[key]=value; return true; }
 public bool WriteUint64(uint key,ulong[] value) { Data[key]=value; return true; }
 public bool ReadUint64(uint key,out ulong value,ulong defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (ulong)stored : defaultValue; return found; }
 public bool ReadUint64(uint key,out ulong[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (ulong[])stored : new ulong[0]; return found; }
 public bool WriteFloat(uint key,float value) { Data[key]=value; return true; }
 public bool WriteFloat(uint key,float[] value) { Data[key]=value; return true; }
 public bool ReadFloat(uint key,out float value,float defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (float)stored : defaultValue; return found; }
 public bool ReadFloat(uint key,out float[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (float[])stored : new float[0]; return found; }
 public bool WriteByte(uint key,byte value) { Data[key]=value; return true; }
 public bool WriteByte(uint key,byte[] value) { Data[key]=value; return true; }
 public bool ReadByte(uint key,out byte value,byte defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (byte)stored : defaultValue; return found; }
 public bool ReadByte(uint key,out byte[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (byte[])stored : new byte[0]; return found; }
 public bool WriteString(uint key,string value) { Data[key]=value; return true; }
 public bool WriteString(uint key,string[] value) { Data[key]=value; return true; }
 public bool ReadString(uint key,out string value,string defaultValue) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (string)stored : defaultValue; return found; }
 public bool ReadString(uint key,out string[] value) { object stored; bool found=Data.TryGetValue(key,out stored); value=found ? (string[])stored : new string[0]; return found; }
}

internal sealed class NativeCommandBridge : ICommandSystem
{
    private string key, help;
    internal int Registrations;
    internal CommandHandler Handler;
    public bool RegisterCommand(string commandName, string commandDesc, CommandHandler commandHandler, bool hidden)
    { key = commandName; help = commandDesc; Handler = commandHandler; Registrations++; return true; }
    public void UnregisterCommand(string commandName) { if (commandName == key) Clear(); }
    public bool ExecuteCommandString(string command) { return false; }
    public string GetCommandList() { return key; }
    public string GetCommandHelp(string command) { return command == key ? help : null; }
    internal void Clear() { key = null; help = null; Handler = null; }
}
