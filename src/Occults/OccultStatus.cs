using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Skills;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    // One row of the journal's Skill & Power header; a row a species cannot supply stays unavailable and is hidden.
    internal sealed class StatusField
    {
        internal string Label = "";
        internal string Caption = "";
        internal float Fraction;
        internal UIImage Icon;
        internal bool Available;

        internal void Clear()
        {
            Label = "";
            Caption = "";
            Fraction = 0f;
            Icon = null;
            Available = false;
        }

        internal void Set(string label, string caption, float fraction, UIImage icon)
        {
            Label = label;
            Caption = caption;
            Fraction = float.IsNaN(fraction) || float.IsInfinity(fraction) || fraction < 0f
                ? 0f : (fraction > 1f ? 1f : fraction);
            Icon = icon;
            Available = true;
        }
    }

    // What a provider may read for the selected Sim.
    internal sealed class StatusSource
    {
        internal readonly ObjectGuid Id;
        internal readonly PowerSelection Selection;
        // The HUD model's record for the Sim, refreshed before every read.
        internal SimInfo Info;
        private Sim sim;

        internal StatusSource(ObjectGuid id, SimInfo info, PowerSelection selection)
        {
            Id = id;
            Info = info;
            Selection = selection;
        }

        // Null for a household Sim that is away from the lot; looked up again until the Sim is instantiated.
        internal Sim LiveSim
        {
            get
            {
                if (sim == null && Id.IsValid)
                {
                    sim = GlobalFunctions.ConvertGuidToObject<Sim>(Id);
                }
                return sim;
            }
        }
    }

    // Supplies one species' progression and power rows to the shared header, which owns the controls and the layout.
    internal abstract class OccultStatusProvider
    {
        internal readonly OccultTypes Type;

        protected OccultStatusProvider(OccultTypes type)
        {
            Type = type;
        }

        // Colour of the power bar: the same accent the HUD meter uses for this occult.
        internal uint Accent { get { return OccultAssetData.Accent(Type); } }

        internal abstract void ReadProgress(StatusSource source, StatusField field);
        internal abstract void ReadPower(StatusSource source, StatusField field);

        private static readonly OccultStatusProvider[] Providers =
        {
            new WitchStatus(), new FairyStatus(), new WerewolfStatus(), new VampireStatus(),
            new GenieStatus(), new MermaidStatus(), new GhostStatus()
        };

        // The provider for a Sim's primary occult, or null for humans, pets and occults without a status panel.
        internal static OccultStatusProvider For(OccultTypes primary)
        {
            foreach (OccultStatusProvider provider in Providers)
            {
                if (provider.Type == primary)
                {
                    return provider;
                }
            }
            return null;
        }
    }

    // Readers shared by the species providers; each returns false, leaving the field cleared, when the game has no data for the Sim.
    internal static class StatusReaders
    {
        // The HUD magic bar's own scale: the meter value runs from -100 (empty) to +100 (full).
        private const float HudMeterEmpty = -100f;
        private const float HudMeterScale = 0.005f;
        private static readonly Dictionary<SkillNames, UIImage> skillIcons = new Dictionary<SkillNames, UIImage>();
        private static readonly Dictionary<ulong, UIImage> statusIcons = new Dictionary<ulong, UIImage>();

        internal static UIImage StatusIcon(ulong instance)
        {
            UIImage image;
            if (statusIcons.TryGetValue(instance, out image))
            {
                return image;
            }

            image = UIManager.LoadUIImage(new ResourceKey(instance, 0x2F7D0004, 0));
            if (!NativeUi.HasImage(image))
            {
                return null;
            }
            statusIcons[instance] = image;
            return image;
        }

        // A skill as "Level n" with the ratio Skill.GetSkillPointsProgress computes, guarded for the top level.
        // A skill the game does not define, or one with unusable data, has no reading, which is not the same as level 0.
        internal static bool ReadSkill(StatusSource source, SkillNames name, string label, StatusField field)
        {
            Skill template = SkillManager.GetStaticSkill(name);
            if (template == null || template.MaxSkillLevel <= 0)
            {
                return false;
            }

            Sim sim = source.LiveSim;
            SkillManager skills = sim == null ? null : sim.SkillManager;
            Skill skill = skills == null ? null : skills.GetElement(name);
            if (skill == null)
            {
                if (sim == null)
                {
                    return false;
                }
                // The Sim is present and the game defines the skill, but the Sim has not started it: level 0.
                field.Set(label, JournalText.Format("Arcana.Status.Level", 0), 0f, SkillIcon(name, template));
                return true;
            }

            int level = skill.SkillLevel;
            int max = skill.MaxSkillLevel;
            if (level < 0 || max <= 0)
            {
                return false;
            }
            if (level > max)
            {
                level = max;
            }

            // PointsForNextLevel holds cumulative thresholds. Dividing total XP by the next
            // level's threshold makes a Level 5 Sim look almost full at the start of Level 5.
            // Only XP earned inside the CURRENT level belongs in its progress meter.
            int[] thresholds = skill.PointsForNextLevel;
            float fraction = 1f;
            if (level < max)
            {
                if (thresholds == null || level >= thresholds.Length ||
                    (level > 0 && level - 1 >= thresholds.Length))
                {
                    return false; // An unknown threshold must not masquerade as a full bar.
                }
                float lower = level == 0 ? 0f : thresholds[level - 1];
                float upper = thresholds[level];
                if (upper <= lower || float.IsNaN(skill.SkillPoints) ||
                    float.IsInfinity(skill.SkillPoints))
                {
                    return false;
                }
                fraction = (skill.SkillPoints - lower) / (upper - lower);
            }
            field.Set(label, JournalText.Format("Arcana.Status.Level", level), fraction, SkillIcon(name, skill));
            return true;
        }

        // The game's own icon for the skill, loaded once; null when the skill has none, so the header keeps its stock icon.
        private static UIImage SkillIcon(SkillNames name, Skill skill)
        {
            UIImage image;
            if (skillIcons.TryGetValue(name, out image))
            {
                return image;
            }

            image = null;
            ResourceKey key = skill.IconKey;
            if (key.InstanceId != 0)
            {
                image = UIManager.LoadUIImage(key);
                if (!NativeUi.HasImage(image))
                {
                    image = null;
                }
            }
            skillIcons[name] = image;
            return image;
        }

        // A magic motive as the 0-100 figure the HUD magic bar shows; MagicFatigue counts fatigue, so the Witch's value is negated as the HUD model does.
        internal static bool ReadMotive(StatusSource source, CommodityKind kind, string label, StatusField field)
        {
            float meter;
            Sim sim = source.LiveSim;
            Motives motives = sim == null ? null : sim.Motives;
            Motive motive = motives == null ? null : motives.GetMotive(kind);
            if (motive != null)
            {
                meter = kind == CommodityKind.MagicFatigue ? -motive.UIValue : motive.UIValue;
            }
            else if (source.Info != null && source.Info.OccultInfo != null)
            {
                meter = source.Info.OccultInfo.MagicMotiveValue;
            }
            else
            {
                return false;
            }

            float fraction = (meter - HudMeterEmpty) * HudMeterScale;
            fraction = fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
            field.Set(label, JournalText.Format("Arcana.Common.Ratio", (int)Math.Round(fraction * 100f), 100), fraction,
                OccultMeter.MeterIcon(MotiveOccult(kind)));
            return true;
        }

        private static OccultTypes MotiveOccult(CommodityKind kind)
        {
            return kind == CommodityKind.AuraPower ? OccultTypes.Fairy : OccultTypes.Witch;
        }

        // The mod's per-Sim energy pool behind the HUD meter of occults without a native magic motive; gameplay does not drain or save it yet.
        internal static bool ReadEnergy(StatusSource source, OccultTypes type, string label, StatusField field)
        {
            OccultEnergy energy = OccultEnergy.For(type);
            if (energy == null)
            {
                return false;
            }
            float value = energy.Get(source.Id);
            field.Set(label, JournalText.Format("Arcana.Common.Ratio", (int)Math.Round(value), (int)OccultEnergy.Max), value / OccultEnergy.Max,
                OccultMeter.MeterIcon(type));
            return true;
        }
    }

    internal sealed class WitchStatus : OccultStatusProvider
    {
        internal WitchStatus() : base(OccultTypes.Witch) { }

        // Keep the rank caption and show progress across the whole native
        // Spellcasting skill, including the XP earned before level 1.
        internal override void ReadProgress(StatusSource source, StatusField field)
        {
            PowerSelection selection = source.Selection;
            if (selection != null && selection.Available)
            {
                field.Set(JournalText.Get("Arcana.Status.Label.MagicalProficiency"), WitchTree.Tree.RankLabel((int)selection.Rank), selection.Proficiency,
                    StatusReaders.StatusIcon(OccultAssetData.WitchSkillStatus));
            }
        }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            if (StatusReaders.ReadMotive(source, CommodityKind.MagicFatigue, JournalText.Get("Arcana.Status.Label.MagicalPower"), field))
            {
                UIImage powerIcon = StatusReaders.StatusIcon(OccultAssetData.WitchPowerStatus);
                if (powerIcon != null)
                {
                    field.Icon = powerIcon;
                }
            }
        }
    }

    internal sealed class FairyStatus : OccultStatusProvider
    {
        internal FairyStatus() : base(OccultTypes.Fairy) { }

        internal override void ReadProgress(StatusSource source, StatusField field)
        {
            StatusReaders.ReadSkill(source, SkillNames.FairyMagic, JournalText.Get("Arcana.Status.Label.FairyMagic"), field);
        }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadMotive(source, CommodityKind.AuraPower, JournalText.Get("Arcana.Status.Label.FaericPower"), field);
        }
    }

    internal sealed class WerewolfStatus : OccultStatusProvider
    {
        internal WerewolfStatus() : base(OccultTypes.Werewolf) { }

        internal override void ReadProgress(StatusSource source, StatusField field)
        {
            StatusReaders.ReadSkill(source, SkillNames.Lycanthropy, JournalText.Get("Arcana.Status.Label.Lycanthropy"), field);
        }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.LycanthropicPower"), field);
        }
    }

    // Vampires have no native rank or skill, so only power shows.
    internal sealed class VampireStatus : OccultStatusProvider
    {
        internal VampireStatus() : base(OccultTypes.Vampire) { }

        internal override void ReadProgress(StatusSource source, StatusField field) { }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.VampiricPower"), field);
        }
    }

    // Genies keep their own magic points in the game; the HUD meter's pool is the fallback for a genie that is not instantiated.
    internal sealed class GenieStatus : OccultStatusProvider
    {
        private const string MaximumPointsName = "mMaxMagicPoints";
        private const string CurrentPointsName = "mCurrentMagicPointValue";
        private static readonly FieldInfo MaximumPoints = typeof(OccultMagicPointSystem).GetField(MaximumPointsName, NativeReflection.Instance);
        private static readonly FieldInfo CurrentPoints = typeof(OccultMagicPointSystem).GetField(CurrentPointsName, NativeReflection.Instance);

        internal GenieStatus() : base(OccultTypes.Genie) { }

        internal override void ReadProgress(StatusSource source, StatusField field) { }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            Sim sim = source.LiveSim;
            OccultManager occults = sim == null ? null : sim.OccultManager;
            OccultGenie genie = occults == null ? null : occults.GetOccultType(OccultTypes.Genie) as OccultGenie;
            OccultMagicPointSystem points = genie == null ? null : genie.MagicPoints;
            int maximum = points == null ? 0 : (int)NativeReflection.Field(MaximumPoints, points, MaximumPointsName);
            if (maximum > 0)
            {
                int nativeValue = (int)NativeReflection.Field(CurrentPoints, points, CurrentPointsName);
                int current = Math.Max(0, Math.Min(maximum, nativeValue));
                field.Set(JournalText.Get("Arcana.Status.Label.MagicPoints"), JournalText.Format("Arcana.Common.Ratio", current, maximum),
                    current / (float)maximum, OccultMeter.MeterIcon(Type));
                return;
            }
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.MagicPoints"), field);
        }
    }

    internal sealed class MermaidStatus : OccultStatusProvider
    {
        internal MermaidStatus() : base(OccultTypes.Mermaid) { }

        internal override void ReadProgress(StatusSource source, StatusField field) { }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.MermadicPower"), field);
        }
    }

    internal sealed class GhostStatus : OccultStatusProvider
    {
        internal GhostStatus() : base(OccultTypes.Ghost) { }

        internal override void ReadProgress(StatusSource source, StatusField field) { }

        internal override void ReadPower(StatusSource source, StatusField field)
        {
            StatusReaders.ReadEnergy(source, Type, JournalText.Get("Arcana.Status.Label.PhantasmalPower"), field);
        }
    }
}
