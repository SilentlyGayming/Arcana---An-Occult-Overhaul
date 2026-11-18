using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;
using System;
using System.Collections.Generic;

namespace SupernaturalExpanded.Journal
{
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

    // Where a species' casting system supplies the Accuracy the Attributes panel shows under Power Regeneration. The Overview only
    // formats what a source reports, so a species without one shows a dash and no figure is ever invented.
    internal static class AccuracySources
    {
        private static readonly Dictionary<OccultTypes, AccuracySource> sources = new Dictionary<OccultTypes, AccuracySource>();

        internal static void Register(OccultTypes species, AccuracySource source)
        {
            sources[species] = source;
        }

        // Only the magically inclined occults cast spells.
        private static bool Casts(OccultTypes species)
        {
            return species == OccultTypes.Witch || species == OccultTypes.Genie || species == OccultTypes.Fairy;
        }

        // The Accuracy row describes the Sim's current rank when its gameplay source is available.
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
