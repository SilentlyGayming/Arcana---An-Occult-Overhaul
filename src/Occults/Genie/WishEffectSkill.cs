using System;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Careers;
using Sims3.Gameplay.Skills;
using Sims3.SimIFace;
using Sims3.SimIFace.CAS;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    public sealed class WishEffectSkill : Skill
    {
        internal const SkillNames WishGuid = (SkillNames)0xA9C4B6E3;
        private WishRecord record = new WishRecord();
        private bool restoreTravelBuffs;
        private float wrongMinutes, beautyMinutes, loveMinutes, joyMinutes;
        private int wrongMood;
        private ulong loveTargetId;

        internal void RestoreTravelBuffs(Sims3.Gameplay.Actors.Sim sim)
        {
            if (!restoreTravelBuffs) return;
            WishBuffs.RestoreTravel(sim, wrongMinutes, wrongMood, beautyMinutes, loveMinutes, loveTargetId, joyMinutes);
            restoreTravelBuffs = false;
        }

        internal WishRecord Record { get { if (record == null) record = new WishRecord(); return record; } }
        public WishEffectSkill() : this(WishGuid) { }
        public WishEffectSkill(SkillNames guid) : base(guid) { }
        internal static WishEffectSkill Prototype()
        {
            WishEffectSkill skill = new WishEffectSkill();
            NonPersistableSkillData data = new NonPersistableSkillData();
            data.Name = "Arcana.Genie.WishRecord"; data.Description = "Arcana.Genie.WishRecord";
            data.MaxSkillLevel = 1; data.PointsForNextLevel = new int[] { int.MaxValue };
            data.SkillCategory = SkillCategory.Hidden; data.SkillProductVersion = ProductVersion.EP6;
            data.AvailableAgeSpecies = CASAGSAvailabilityFlags.HumanAgeMask;
            skill.mNonPersistableData = data;
            return skill;
        }
        public override Skill Clone(SimDescription owner)
        {
            WishEffectSkill copy = (WishEffectSkill)base.Clone(owner);
            copy.record = Record.Clone(); return copy;
        }
        public override void OnSkillAddition(bool suppressTNS) { }
        public override bool OnLoadFixup() { SkillGainRate = 0f; return base.OnLoadFixup(); }
        public override bool ExportContent(IPropertyStreamWriter writer)
        {
            if (!base.ExportContent(writer)) return false;
            Sims3.Gameplay.Actors.Sim sim = SkillOwner == null ? null : SkillOwner.CreatedSim;
            if (sim != null && sim.BuffManager != null)
                WishBuffs.CaptureTravel(sim, out wrongMinutes, out wrongMood, out beautyMinutes, out loveMinutes, out loveTargetId, out joyMinutes);
            WishRecord value = Record;
            return writer.WriteBool(0xAC520001, value.CheatDeath)
                && writer.WriteInt32(0xAC520002, value.Escapes)
                && writer.WriteInt64(0xAC520003, value.JoyEnds)
                && writer.WriteInt64(0xAC520004, value.SadEnds)
                && writer.WriteBool(0xAC520005, value.CareerPending)
                && writer.WriteUint64(0xAC520006, (ulong)value.CareerGuid)
                && writer.WriteInt64(0xAC520007, value.CareerExpires)
                && writer.WriteBool(0xAC520008, value.WorkStarted)
                && writer.WriteBool(0xAC520009, value.WaitForCurrentShift)
                && writer.WriteFloat(0xAC52000A, value.PenaltyRemaining)
                && writer.WriteFloat(0xAC52000B, value.LastPerformance)
                && writer.WriteInt32(0xAC52000C, value.LastCareerLevel)
                && writer.WriteBool(0xAC52000D, value.CheatDeathAttempted)
                && writer.WriteFloat(0xAC520020, wrongMinutes)
                && writer.WriteInt32(0xAC520021, wrongMood)
                && writer.WriteFloat(0xAC520022, beautyMinutes)
                && writer.WriteFloat(0xAC520023, loveMinutes)
                && writer.WriteUint64(0xAC520024, loveTargetId)
                && writer.WriteFloat(0xAC520025, joyMinutes);
        }
        public override bool ImportContent(IPropertyStreamReader reader)
        {
            if (!base.ImportContent(reader)) return false;
            record = new WishRecord();
            reader.ReadBool(0xAC520001, out record.CheatDeath, false);
            reader.ReadInt32(0xAC520002, out record.Escapes, 0);
            reader.ReadInt64(0xAC520003, out record.JoyEnds, 0);
            reader.ReadInt64(0xAC520004, out record.SadEnds, 0);
            reader.ReadBool(0xAC520005, out record.CareerPending, false);
            ulong career; reader.ReadUint64(0xAC520006, out career, 0); record.CareerGuid = (OccupationNames)career;
            reader.ReadInt64(0xAC520007, out record.CareerExpires, 0);
            reader.ReadBool(0xAC520008, out record.WorkStarted, false);
            reader.ReadBool(0xAC520009, out record.WaitForCurrentShift, false);
            reader.ReadFloat(0xAC52000A, out record.PenaltyRemaining, 0f);
            reader.ReadFloat(0xAC52000B, out record.LastPerformance, 0f);
            reader.ReadInt32(0xAC52000C, out record.LastCareerLevel, 0);
            reader.ReadBool(0xAC52000D, out record.CheatDeathAttempted, record.CheatDeath);
            reader.ReadFloat(0xAC520020, out wrongMinutes, 0f);
            reader.ReadInt32(0xAC520021, out wrongMood, -25);
            reader.ReadFloat(0xAC520022, out beautyMinutes, 0f);
            reader.ReadFloat(0xAC520023, out loveMinutes, 0f);
            reader.ReadUint64(0xAC520024, out loveTargetId, 0);
            reader.ReadFloat(0xAC520025, out joyMinutes, 0f);
            restoreTravelBuffs = wrongMinutes > 0f || loveMinutes > 0f || joyMinutes > 0f;
            record.Escapes = Math.Max(0, record.Escapes); SkillGainRate = 0f; return true;
        }
    }

    internal sealed class WishEffectRegistration : SkillManager
    {
        internal static void Register()
        {
            Skill existing;
            if (sDictionary.TryGetValue((ulong)WishEffectSkill.WishGuid, out existing))
            {
                if (!(existing is WishEffectSkill)) throw new InvalidOperationException("Arcana wish record skill ID collision.");
                return;
            }
            sDictionary.Add((ulong)WishEffectSkill.WishGuid, WishEffectSkill.Prototype());
            sSkillEnumValues.AddNewEnumValue("ArcanaWishEffects", WishEffectSkill.WishGuid);
        }
    }
}
