using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.SimIFace;
using SupernaturalExpanded.Journal;

internal static class WishSystemTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Main(string[] args)
    {
        HashSet<WishKind> kinds = new HashSet<WishKind>();
        int lampOnly = 0;
        foreach (WishDefinition wish in WishCatalog.All)
        {
            Check(kinds.Add(wish.Kind), "duplicate wish");
            Check(WishCatalog.Find(wish.Kind) == wish, "lookup identity");
            Check(!WishRules.CanOffer(wish, false, true), "uninstalled expansion offered");
            Check(WishRules.CanOffer(wish, true, true), "lamp catalog lost an installed wish");
            Check(WishRules.CanOffer(wish, true, false) == !wish.LampOnly, "lamp-only wish leaked to playable menu");
            if (wish.LampOnly) lampOnly++;
            Check(!string.IsNullOrEmpty(wish.NameKey), "missing text");
        }
        Check(kinds.Count == 14 && lampOnly == 2, "14 wishes / two lamp-only");
        Check(WishCatalog.Find(WishKind.Youth).Product == ProductVersion.EP4, "Youth requires Generations");
        Check(WishCatalog.Find(WishKind.Fame).Product == ProductVersion.EP3, "Fame requires Late Night");
        Check(WishRules.RepossessionCredit(0, 0, 900) == 0, "Repossessing from a debt-free family created debt");
        Check(WishRules.RepossessionCredit(50, 0, 900) == 50, "Credit exceeded the outstanding debt");
        Check(WishRules.RepossessionCredit(1000, 100, 900) == 900, "Native debt reduction was lost");
        Check(WishRules.RepossessionCredit(1000, 400, 900) == 0, "Concurrent debt changes were overwritten");
        Check((int)WishKind.Fortune == 0 && (int)WishKind.Vitality == 1 && (int)WishKind.Heart == 2, "saved queue IDs changed");
        Check((int)WishMode.Other == 0 && (int)WishMode.Guaranteed == 1 && (int)WishMode.Own == 2, "saved mode IDs changed");
        Check(WishRules.CanReceive(false, false, false), "living wisher blocked");
        Check(WishRules.CanReceive(true, true, false), "playable ghost wisher blocked");
        Check(!WishRules.CanReceive(true, false, false), "visiting non-playable ghost accepted");
        Check(!WishRules.CanReceive(true, true, true) && !WishRules.CanReceive(false, false, true), "active death scene can receive wishes");
        Check(WishRules.Fortune == 100000, "Fortune must award exactly 100000");
        for (int escapes = 0; escapes < 1000; escapes++)
        {
            float chance = WishRules.DeathChance(escapes);
            Check(chance >= 0 && chance <= 90, "death protection out of bounds");
            if (escapes > 0 && escapes <= 9) Check(WishRules.DeathChance(escapes - 1) - chance == 10f, "escape reduction not 10 points");
            if (escapes >= 9) Check(chance == 0f, "wish became immortality");
        }
        Check(WishRules.MoreWishesDelta(false, false) == -1 && WishRules.MoreWishesDelta(false, true) == -1, "failed extra wishes refunded");
        Check(WishRules.MoreWishesDelta(true, false) == 0 && WishRules.MoreWishesDelta(true, true) == 1, "native lamp net allowance changed");
        for (uint level = 0; level < 12; level++)
        {
            Check(WishRules.CanGainFame(level, true, true) == (level < 4), "Fame cap");
            Check(!WishRules.CanGainFame(level, false, true) && !WishRules.CanGainFame(level, true, false), "disabled celebrity system");
        }
        for (int highest = 1; highest <= 15; highest++)
            for (int level = 0; level <= highest + 1; level++)
            {
                Check(WishRules.CanPromote(level, highest, true) == (level > 0 && level < highest), "career promotion boundary");
                Check(!WishRules.CanPromote(level, highest, false), "unsupported career promoted");
            }
        Check(!WishRules.GuaranteeAvailable(true, true, 101, 100), "guarantee fired early");
        Check(WishRules.GuaranteeAvailable(true, true, 100, 100), "guarantee not ready at boundary");
        Check(WishRules.GuaranteeAvailable(true, true, 99, 100), "overdue guarantee lost");
        Check(!WishRules.GuaranteeAvailable(false, true, 0, 100), "self or lamp wish spent playable guarantee");
        Check(!WishRules.GuaranteeAvailable(true, false, 0, 100), "unowned guarantee used");
        CastingRules rules = new CastingRules(Rank.HighWitch, CasterKind.Genie);
        Check(GenieWishes.Cost(WishMode.Other, rules) == CastingRules.WishCost, "base wish quote");
        Check(GenieWishes.Cost(WishMode.Lamp, rules) == 0, "lamp charged playable power");
        rules.Lampbound = true;
        Check(GenieWishes.Cost(WishMode.Other, rules) == CastingRules.WishCost * rules.AbilityCostFactor, "Lampbound cost quote");
        Check(GenieWishes.Cost(WishMode.Own, rules) == CasterProfile.Genie.Capacity(Rank.HighWitch), "self wish lost unique whole-pool price");
        Check(WishRules.NormalizedPerformance(0.5f, true) == 50f && WishRules.NormalizedPerformance(50f, false) == 50f, "profession and career progress scales differ");
        WishRecord original = new WishRecord();
        Check(!original.CheatDeath && original.Escapes == 0 && original.JoyEnds == 0 && !original.CareerPending, "old-save defaults unsafe");
        Assembly built = Assembly.LoadFrom(args[0]);
        foreach (string name in new[] { "WishRecord", "WishEffectSkill", "LampHandoff", "WishRepoRequest", "WishRepoArrival", "UnrequitedWishInstance", "WishGoneWrongInstance", "WishParticipant", "WishReapSoul", "WishReapSoulDiving", "GrantWish" })
        {
            Type type = built.GetType("SupernaturalExpanded.Journal." + name, true);
            bool persistable = false;
            foreach (CustomAttributeData attribute in type.GetCustomAttributesData())
                if (attribute.AttributeType.Name == "PersistableAttribute") persistable = true;
            Check(persistable, "nonpersistable long-lived record " + name);
        }
        foreach (string path in new[] { "WishLamp.sHandoffs", "WishRepossession.sRequests" })
        {
            string[] parts = path.Split('.');
            FieldInfo field = built.GetType("SupernaturalExpanded.Journal." + parts[0], true).GetField(parts[1], BindingFlags.Static | BindingFlags.NonPublic);
            bool persistent = false;
            foreach (CustomAttributeData attribute in field.GetCustomAttributesData())
                if (attribute.AttributeType.Name == "PersistableStaticAttribute") persistent = true;
            Check(persistent, "state stored only in transient static: " + path);
        }
        Type wishSkill = built.GetType("SupernaturalExpanded.Journal.WishEffectSkill", true);
        Check(wishSkill.GetMethod("ExportContent", new Type[] { typeof(IPropertyStreamWriter) }).DeclaringType == wishSkill && wishSkill.GetMethod("ImportContent", new Type[] { typeof(IPropertyStreamReader) }).DeclaringType == wishSkill,
            "travel records missing native property-stream support");
        Check(wishSkill.GetMethod("Clone").DeclaringType == wishSkill, "travel clone shares record");
        WishRecord copy = original.Clone(); copy.Escapes = 4;
        Check(original.Escapes == 0, "cloning a Sim shares death protection history");
        WishEffectSkill first = new WishEffectSkill();
        first.Record.CheatDeath = true; first.Record.CheatDeathAttempted = true; first.Record.Escapes = 4;
        first.Record.JoyEnds = 12345; first.Record.SadEnds = 23456;
        first.Record.CareerPending = true; first.Record.CareerGuid = (Sims3.Gameplay.Careers.OccupationNames)123;
        first.Record.CareerExpires = 34567; first.Record.WorkStarted = true; first.Record.WaitForCurrentShift = true;
        first.Record.PenaltyRemaining = 7.5f; first.Record.LastPerformance = 41.25f; first.Record.LastCareerLevel = 3;
        MemoryStreamData stream = new MemoryStreamData();
        Check(first.ExportContent(stream), "native wish property stream export failed");
        WishEffectSkill restored = new WishEffectSkill();
        Check(restored.ImportContent(stream), "native wish property stream import failed");
        Check(restored.Record.CheatDeath && restored.Record.CheatDeathAttempted && restored.Record.Escapes == 4, "travel lost decreasing death protection");
        Check(restored.Record.JoyEnds == 12345 && restored.Record.SadEnds == 23456, "travel reset delayed happiness deadlines");
        Check(restored.Record.CareerPending && restored.Record.CareerGuid == first.Record.CareerGuid && restored.Record.CareerExpires == 34567,
            "travel lost eligible career or penalty deadline");
        Check(restored.Record.WorkStarted && restored.Record.WaitForCurrentShift && restored.Record.PenaltyRemaining == 7.5f
            && restored.Record.LastPerformance == 41.25f && restored.Record.LastCareerLevel == 3, "travel reset workday penalty progress");
        WishEffectSkill travelClone = (WishEffectSkill)first.Clone(null); travelClone.Record.Escapes++;
        Check(first.Record.Escapes == 4 && travelClone.Record.Escapes == 5, "cloned wish histories alias");
        restored.ImportContent(new MemoryStreamData());
        Check(!restored.Record.CheatDeath && restored.Record.Escapes == 0 && !restored.Record.CareerPending && restored.Record.JoyEnds == 0,
            "missing records inherited effects from another Sim or save");
        foreach (string instanceName in new[] { "WishGoneWrongInstance", "UnrequitedWishInstance" })
        {
            Type instanceType = built.GetType("SupernaturalExpanded.Journal." + instanceName, true);
            string fieldName = instanceName == "WishGoneWrongInstance" ? "RomanceEnds" : "belovedId";
            FieldInfo savedField = instanceType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            object savedValue = instanceName == "WishGoneWrongInstance" ? (object)123456L : (object)987654UL;
            Sims3.Gameplay.ActorSystems.BuffInstance buff = (Sims3.Gameplay.ActorSystems.BuffInstance)Activator.CreateInstance(instanceType);
            savedField.SetValue(buff, savedValue);
            Sims3.Gameplay.ActorSystems.BuffInstance buffClone = buff.Clone();
            Check(buffClone.GetType() == instanceType && !object.ReferenceEquals(buff, buffClone), "custom moodlet clone lost its derived type: " + instanceName);
            Check(savedField.GetValue(buffClone).Equals(savedValue), "custom moodlet clone lost wish data: " + instanceName);
            savedField.SetValue(buffClone, instanceName == "WishGoneWrongInstance" ? (object)1L : (object)2UL);
            Check(savedField.GetValue(buff).Equals(savedValue), "custom moodlet clone shares instance state: " + instanceName);
            MemoryStreamData buffStream = new MemoryStreamData();
            Check(buff.ExportContent(null, null, buffStream), "custom moodlet export failed");
            Sims3.Gameplay.ActorSystems.BuffInstance imported = (Sims3.Gameplay.ActorSystems.BuffInstance)Activator.CreateInstance(instanceType);
            imported.ImportContentSpecial(null, null, buffStream);
            Check(savedField.GetValue(imported).Equals(savedValue), "custom moodlet property stream lost wish data: " + instanceName);
            imported.ImportContentSpecial(null, null, new MemoryStreamData());
            Check(savedField.GetValue(imported).Equals(instanceName == "WishGoneWrongInstance" ? (object)0L : (object)0UL), "missing moodlet fields retained another instance's effect");
        }
        object[] snapshots = { 900f, -20, 600f, 1200f, 123456UL, 800f };
        string[] snapshotFields = { "wrongMinutes", "wrongMood", "beautyMinutes", "loveMinutes", "loveTargetId", "joyMinutes" };
        object magicWishSource = Activator.CreateInstance(wishSkill);
        for (int i = 0; i < snapshotFields.Length; i++)
            wishSkill.GetField(snapshotFields[i], BindingFlags.Instance | BindingFlags.NonPublic).SetValue(magicWishSource, snapshots[i]);
        MemoryStreamData snapshotStream = new MemoryStreamData();
        Check((bool)wishSkill.GetMethod("ExportContent", new Type[] { typeof(IPropertyStreamWriter) }).Invoke(magicWishSource, new object[] { snapshotStream }), "travel moodlet snapshot export failed");
        object snapshotRestored = Activator.CreateInstance(wishSkill);
        MethodInfo importSnapshot = wishSkill.GetMethod("ImportContent", new Type[] { typeof(IPropertyStreamReader) });
        Check((bool)importSnapshot.Invoke(snapshotRestored, new object[] { snapshotStream }), "travel moodlet snapshot import failed");
        for (int i = 0; i < snapshotFields.Length; i++)
            Check(wishSkill.GetField(snapshotFields[i], BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snapshotRestored).Equals(snapshots[i]), "travel lost moodlet snapshot " + snapshotFields[i]);
        Check((bool)wishSkill.GetField("restoreTravelBuffs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snapshotRestored), "travel never restores buffs rejected by EA's custom-enum filter");
        importSnapshot.Invoke(snapshotRestored, new object[] { new MemoryStreamData() });
        Check(!(bool)wishSkill.GetField("restoreTravelBuffs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snapshotRestored), "empty travel stream inherited custom buffs");
        Type magicType = built.GetType("SupernaturalExpanded.Journal.GenieMagicSkill", true);
        object magic = Activator.CreateInstance(magicType);
        object magicImported = Activator.CreateInstance(magicType);
        string[] magicFields = { "power", "delicateCasting", "exhaustEnds", "vesselEnds", "chainsEnds", "redirectReady", "guaranteeReady" };
        object[] magicValues = { 137.5f, true, 10L, 20L, 30L, 40L, 50L };
        for (int i = 0; i < magicFields.Length; i++)
            magicType.GetField(magicFields[i], BindingFlags.NonPublic | BindingFlags.Instance).SetValue(magic, magicValues[i]);
        MethodInfo exportMagic = magicType.GetMethod("ExportContent", new Type[] { typeof(IPropertyStreamWriter) });
        MethodInfo importMagic = magicType.GetMethod("ImportContent", new Type[] { typeof(IPropertyStreamReader) });
        MemoryStreamData magicStream = new MemoryStreamData();
        Check((bool)exportMagic.Invoke(magic, new object[] { magicStream }), "Genie power/cooldown export failed");
        Check((bool)importMagic.Invoke(magicImported, new object[] { magicStream }), "Genie power/cooldown import failed");
        for (int i = 0; i < magicFields.Length; i++)
            Check(magicType.GetField(magicFields[i], BindingFlags.NonPublic | BindingFlags.Instance).GetValue(magicImported).Equals(magicValues[i]), "Genie travel lost " + magicFields[i]);
        importMagic.Invoke(magicImported, new object[] { new MemoryStreamData() });
        Check(float.IsNaN((float)magicType.GetField("power", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(magicImported)), "missing Genie power inherited a different Sim's value");
        for (int i = 1; i < magicFields.Length; i++)
            Check(magicType.GetField(magicFields[i], BindingFlags.NonPublic | BindingFlags.Instance).GetValue(magicImported).Equals(i == 1 ? (object)false : (object)0L), "missing Genie cooldown retained " + magicFields[i]);
        Type grim = typeof(Sims3.Gameplay.Services.GrimReaperSituation);
        Check(grim.GetField("mIsFirstSim", BindingFlags.NonPublic | BindingFlags.Instance) != null, "native reaping sequence flag missing");
        Check(grim.GetField("mScubaDeathJig", BindingFlags.NonPublic | BindingFlags.Instance) != null, "native diving jig binding missing");
        Type linked = grim.GetNestedType("BeReapedDiving", BindingFlags.NonPublic);
        Check(linked != null && linked.GetField("mHadDeathFlower", BindingFlags.Public | BindingFlags.Instance) != null,
            "native diving second-chance continuation missing");
        Console.WriteLine("PASS: " + checks + " wish catalog, EP, cost, lamp allowance, death, career, fame, guarantee and compiled persistence checks");
        Console.WriteLine("Host checks do not execute native gameplay, animations or a save/load cycle.");
    }
}
