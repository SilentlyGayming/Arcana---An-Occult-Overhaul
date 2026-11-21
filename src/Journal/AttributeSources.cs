using System;
using System.Collections.Generic;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class ModifierReading
    {
        internal static readonly ModifierReading None = new ModifierReading(0f, "", false, null);

        internal readonly float Percent;
        internal readonly string Condition;
        internal readonly OverviewTip Tip;
        private readonly bool available;
        private readonly string displayText;

        internal ModifierReading(float percent, string condition)
            : this(percent, condition, null)
        {
        }

        internal ModifierReading(float percent, string condition, OverviewTip tip)
            : this(percent, condition, !float.IsNaN(percent) && !float.IsInfinity(percent), tip)
        {
        }

        internal ModifierReading(float percent, string condition, OverviewTip tip, string displayText)
            : this(percent, condition, tip)
        {
            this.displayText = displayText;
        }

        private ModifierReading(float percent, string condition, bool available, OverviewTip tip)
        {
            Percent = percent;
            Condition = condition ?? "";
            Tip = tip;
            this.available = available;
        }

        internal bool Available { get { return available; } }

        internal string Text { get { return displayText ?? PercentText.Format(Percent, true); } }
    }

    internal delegate ModifierReading ModifierSource(OverviewContext context);

    internal static class AttributeSources
    {
        private sealed class Entry
        {
            internal readonly OccultTypes Species;
            internal readonly string Label;
            internal readonly ModifierSource Source;

            internal Entry(OccultTypes species, string label, ModifierSource source)
            {
                Species = species;
                Label = label;
                Source = source;
            }
        }

        private const string RegenerationKey = "Arcana.Journal.Overview.Attributes.PowerRegeneration";
        private static readonly List<Entry> entries = new List<Entry>();

        internal static void Register(OccultTypes species, string label, ModifierSource source)
        {
            entries.Add(new Entry(species, label, source));
        }

        internal static void AddRows(OverviewContext context, OverviewData data)
        {
            UIImage icon = context.Power == null ? null : context.Power.Icon;
            string regenerationLabel = JournalText.Get(RegenerationKey);
            string englishRegeneration = JournalText.English(RegenerationKey);
            bool reportedRegeneration = false;
            int regenerationRow = -1;
            foreach (Entry entry in entries)
            {
                if (entry.Species != context.Primary)
                {
                    continue;
                }

                ModifierReading reading;
                try
                {
                    reading = entry.Source(context);
                }
                catch (ResetException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    JournalBootstrap.ReportOnce("Reading " + entry.Label, error);
                    continue;
                }

                if (reading == null || !reading.Available)
                {
                    continue;
                }
                bool isRegeneration = entry.Label == englishRegeneration || entry.Label == regenerationLabel;
                string label = isRegeneration ? regenerationLabel : entry.Label;
                string name = reading.Condition.Length == 0 ? label
                    : JournalText.Format("Arcana.Common.WithQualifier", label, reading.Condition);
                data.Statistics.Add(new OverviewRow(icon, name, "", reading.Text, reading.Tip));
                if (isRegeneration)
                {
                    regenerationRow = data.Statistics.Count - 1;
                }
                reportedRegeneration |= isRegeneration;
            }
            if (!reportedRegeneration && context.Power != null && context.Power.Available)
            {
                data.Statistics.Add(new OverviewRow(icon, regenerationLabel, "", JournalText.Get("Arcana.Common.UnavailableValue")));
                regenerationRow = data.Statistics.Count - 1;
            }

            OverviewRow accuracy = AccuracySources.Row(context);
            if (accuracy != null)
            {
                data.Statistics.Insert(regenerationRow < 0 ? data.Statistics.Count : regenerationRow + 1, accuracy);
            }
        }
    }

    internal sealed class AccuracyReading
    {
        internal readonly float Overall;
        internal readonly string BaseLine;
        internal readonly TooltipModifier[] Modifiers;

        internal AccuracyReading(float overall, string baseLine, TooltipModifier[] modifiers)
        {
            Overall = overall;
            BaseLine = baseLine;
            Modifiers = modifiers;
        }
    }

    internal delegate AccuracyReading AccuracySource(OverviewContext context);

    internal static class AccuracySources
    {
        private static readonly Dictionary<OccultTypes, AccuracySource> sources = new Dictionary<OccultTypes, AccuracySource>();

        internal static void Register(OccultTypes species, AccuracySource source)
        {
            sources[species] = source;
        }

        private static bool Casts(OccultTypes species)
        {
            return species == OccultTypes.Witch || species == OccultTypes.Genie || species == OccultTypes.Fairy;
        }

        internal static OverviewRow Row(OverviewContext context)
        {
            if (!Casts(context.Primary))
            {
                return null;
            }

            AccuracyReading reading = Read(context);
            string label = JournalText.Get("Arcana.Journal.Overview.Attributes.Accuracy");
            string value = reading == null ? JournalText.Get("Arcana.Common.UnavailableValue") : PercentText.Format(reading.Overall, false);
            OverviewTip tip = reading == null ? null :
                new OverviewTip(label, JournalText.Get("Arcana.Journal.Overview.Accuracy.Tagline"), reading.BaseLine, reading.Modifiers);
            UIImage icon = context.Power == null ? null : context.Power.Icon;
            return new OverviewRow(icon, label, "", value, tip);
        }

        private static AccuracyReading Read(OverviewContext context)
        {
            AccuracySource source;
            if (!sources.TryGetValue(context.Primary, out source))
            {
                return null;
            }

            try
            {
                return source(context);
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Reading Accuracy", error);
                return null;
            }
        }
    }
}
