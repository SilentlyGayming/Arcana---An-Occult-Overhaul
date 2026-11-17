using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SupernaturalExpanded.Journal
{
    // The chance a Sim's spells succeed: one overall figure for the Attributes row and one per spell tier for its tooltip.
    internal sealed class AccuracyReading
    {
        // Tiers 1 to 5, then Legendary.
        internal const int TierCount = 6;

        private readonly float overall;
        private readonly float[] byTier = new float[TierCount];

        // Percentages from 0 to 100; a NaN, or a tier past the end of byTier, shows as a dash.
        internal AccuracyReading(float overall, float[] byTier)
        {
            this.overall = overall;
            for (int tier = 0; tier < TierCount; tier++)
            {
                this.byTier[tier] = byTier != null && tier < byTier.Length ? byTier[tier] : float.NaN;
            }
        }

        internal string OverallText { get { return Format(overall); } }

        internal string TierText(int tier)
        {
            return Format(byTier[tier]);
        }

        // A whole percentage; a figure with a fraction keeps one decimal, as ModifierReading does.
        private static string Format(float percent)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent))
            {
                return JournalText.Get("Arcana.Common.UnavailableValue");
            }

            double clamped = Math.Max(0.0, Math.Min(100.0, percent));
            double whole = Math.Round(clamped, MidpointRounding.AwayFromZero);
            bool exact = Math.Abs(clamped - whole) < 0.05;
            double shown = exact ? whole : Math.Round(clamped, 1, MidpointRounding.AwayFromZero);
            return JournalText.Format("Arcana.Common.Percent", shown.ToString(exact ? "0" : "0.#", CultureInfo.InvariantCulture));
        }
    }

    internal delegate AccuracyReading AccuracySource(OverviewContext context);

    // Where a species' casting system supplies the Accuracy the Attributes panel shows under Power Regeneration. The Overview only
    // formats what a source reports, so a species without one shows a dash and no figure is ever invented.
    internal static class AccuracySources
    {
        private static readonly string[] TierKeys =
        {
            "Arcana.Journal.Overview.Accuracy.Tier.1",
            "Arcana.Journal.Overview.Accuracy.Tier.2",
            "Arcana.Journal.Overview.Accuracy.Tier.3",
            "Arcana.Journal.Overview.Accuracy.Tier.4",
            "Arcana.Journal.Overview.Accuracy.Tier.5",
            "Arcana.Journal.Overview.Accuracy.Tier.Legendary"
        };

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

        // The Accuracy row with its per-tier tooltip, or null when the Sim's species casts no spells.
        internal static OverviewRow Row(OverviewContext context)
        {
            if (!Casts(context.Primary))
            {
                return null;
            }

            AccuracyReading reading = Read(context);
            string unavailable = JournalText.Get("Arcana.Common.UnavailableValue");
            string label = JournalText.Get("Arcana.Journal.Overview.Attributes.Accuracy");
            string[] lines = new string[TierKeys.Length];
            for (int tier = 0; tier < lines.Length; tier++)
            {
                lines[tier] = JournalText.Format("Arcana.Journal.Overview.Accuracy.Line", JournalText.Get(TierKeys[tier]),
                    reading == null ? unavailable : reading.TierText(tier));
            }

            OverviewTip tip = new OverviewTip(label, JournalText.Get("Arcana.Journal.Overview.Accuracy.Tagline"), string.Join("\n", lines));
            UIImage icon = context.Power == null ? null : context.Power.Icon;
            return new OverviewRow(icon, label, "", reading == null ? unavailable : reading.OverallText, tip);
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
