using System;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Skills;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    [Persistable]
    public sealed class GenieMagicSkill : Skill
    {
        internal const SkillNames GenieGuid = (SkillNames)0xA9C4B6E2;
        internal const int MaxLevel = 10;
        private float power = GeniePower.Maximum;

        public GenieMagicSkill() : this(GenieGuid) { }
        public GenieMagicSkill(SkillNames guid) : base(guid) { }

        internal float Power
        {
            get { return float.IsNaN(power) ? GeniePower.Maximum : Math.Max(0f, Math.Min(GeniePower.Maximum, power)); }
            set { power = Math.Max(0f, Math.Min(GeniePower.Maximum, value)); }
        }

        internal static GenieMagicSkill Prototype()
        {
            // Builds the visible Genie Magic skill from Spellcasting's category, ages and icons, or returns null before skills load.
            Skill casting = SkillManager.GetStaticSkill(SkillNames.Spellcasting);
            if (casting == null || casting.NonPersistableData == null) return null;
            NonPersistableSkillData source = casting.NonPersistableData;
            NonPersistableSkillData data = new NonPersistableSkillData();
            data.Name = "Arcana.Skill.GenieMagic.Name";
            data.Description = "Arcana.Skill.GenieMagic.Description";
            data.Commodity = Sims3.Gameplay.Autonomy.CommodityKind.None;
            data.MaxSkillLevel = MaxLevel;
            data.PointsForNextLevel = source.PointsForNextLevel != null && source.PointsForNextLevel.Length > MaxLevel
                ? (int[])source.PointsForNextLevel.Clone() : new int[] { 0, 100, 300, 600, 1000, 1500, 2100, 2800, 3600, 4500, 5500 };
            data.SkillCategory = source.SkillCategory;
            data.AvailableAgeSpecies = source.AvailableAgeSpecies;
            data.AlwaysDisplayLevelUpTns = source.AlwaysDisplayLevelUpTns;
            data.DreamsAndPromisesIcon = source.DreamsAndPromisesIcon;
            data.DreamsAndPromisesIconKey = source.DreamsAndPromisesIconKey;
            data.IconKey = source.IconKey;
            data.SkillUIIconKey = source.SkillUIIconKey;
            data.SkillProductVersion = source.SkillProductVersion;
            data.SkillVersion = source.SkillVersion;
            string[] messages = new string[MaxLevel + 1];
            for (int i = 1; i < messages.Length; i++) messages[i] = "Arcana.Skill.GenieMagic.LevelUp";
            data.LevelUpStrings = messages;
            GenieMagicSkill skill = new GenieMagicSkill();
            skill.mNonPersistableData = data;
            return skill;
        }
    }

    internal sealed class GenieMagicRegistration : SkillManager
    {
        internal static void Register()
        {
            // Registers the Genie Magic skill once skills have loaded, refusing to overwrite a different skill with the same ID.
            Skill existing;
            if (sDictionary.TryGetValue((ulong)GenieMagicSkill.GenieGuid, out existing))
            {
                if (!(existing is GenieMagicSkill)) throw new InvalidOperationException("Arcana Genie Magic skill ID collision.");
                return;
            }
            GenieMagicSkill prototype = GenieMagicSkill.Prototype();
            if (prototype == null) return;
            sDictionary.Add((ulong)GenieMagicSkill.GenieGuid, prototype);
            sSkillEnumValues.AddNewEnumValue("ArcanaGenieMagic", GenieMagicSkill.GenieGuid);
        }
    }

    internal static class GeniePower
    {
        internal const float Maximum = 100f;
        internal const float RegenerationPerHour = 10f;

        internal static GenieMagicSkill Read(SimDescription sim, bool create)
        {
            // Returns the Genie's Genie Magic skill, which also stores the Genie's power pool, creating it when asked.
            if (sim == null || sim.SkillManager == null) return null;
            GenieMagicSkill skill = sim.SkillManager.GetElement(GenieMagicSkill.GenieGuid) as GenieMagicSkill;
            if (skill == null && create)
            {
                GenieMagicRegistration.Register();
                sim.SkillManager.AddElement(GenieMagicSkill.GenieGuid);
                skill = sim.SkillManager.GetElement(GenieMagicSkill.GenieGuid) as GenieMagicSkill;
            }
            return skill;
        }

        internal static float Get(SimDescription sim)
        {
            GenieMagicSkill skill = Read(sim, false);
            return skill == null ? Maximum : skill.Power;
        }

        internal static void Change(SimDescription sim, float amount)
        {
            // Adds or spends Genie power and redraws the power meter.
            GenieMagicSkill skill = Read(sim, true);
            if (skill == null || amount == 0f) return;
            skill.Power += amount;
            JournalBootstrap.RefreshMeter();
        }

        internal static void RegenerateAll(float minutes)
        {
            // Restores every instantiated Genie's power at the fixed hourly rate.
            bool changed = false;
            foreach (Sim sim in Sims3.Gameplay.Queries.GetObjects<Sim>())
            {
                CasterKind kind;
                if (!MagicCasters.TryKind(sim, out kind) || kind != CasterKind.Genie) continue;
                GenieMagicSkill skill = Read(sim.SimDescription, false);
                if (skill == null || skill.Power >= Maximum) continue;
                skill.Power += RegenerationPerHour * minutes / 60f;
                changed = true;
            }
            if (changed) JournalBootstrap.RefreshMeter();
        }
    }
}
