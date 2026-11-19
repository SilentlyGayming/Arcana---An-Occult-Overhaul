using System;
using System.Globalization;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class JournalText
    {
        private const string MissingKey = "Arcana.Common.TextUnavailable";

        internal static string Get(string key)
        {
            // Looks up a localized string by key, falling back to English.
            string text = StringTable.GetLocalizedString(ResourceUtils.HashString64(key));
            if (!string.IsNullOrEmpty(text) && text.Trim().Length != 0)
            {
                return text;
            }

#if DEBUG || ARCANA_LOCALIZATION_LOG
            ReportFallback(key, "No string in the active table.");
#endif
            return English(key);
        }

        internal static string English(string key)
        {
            return ArcanaEnglish.Lookup(key) ?? ArcanaEnglish.Lookup(MissingKey);
        }

        internal static string Format(string key, params object[] values)
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, Get(key), values);
            }
            catch (FormatException)
            {
#if DEBUG || ARCANA_LOCALIZATION_LOG
                ReportFallback(key, "Invalid numbered placeholders in the active table.");
#endif
                return string.Format(CultureInfo.CurrentCulture, English(key), values);
            }
        }

#if DEBUG || ARCANA_LOCALIZATION_LOG
        private static void ReportFallback(string key, string reason)
        {
            string locale = StringTable.GetLocale() ?? "unknown";
            JournalBootstrap.ReportOnce("Localizing " + key + " (" + locale + ")",
                new InvalidOperationException(reason + " Using the English catalog."));
        }
#endif
    }

    internal static class WitchText
    {
        internal static string[] Entry(string key)
        {
            int tiers = ArcanaEnglish.PerkTiers(key);
            if (tiers == 0)
            {
                return null;
            }

            string prefix = "Arcana.Witch.Perk." + key;
            string[] entry = new string[tiers + 1];
            entry[0] = JournalText.Get(prefix + ".Tagline");
            for (int tier = 1; tier <= tiers; tier++)
            {
                entry[tier] = JournalText.Get(prefix + ".Description." + tier.ToString(CultureInfo.InvariantCulture));
            }
            return entry;
        }
    }

    internal static class PercentText
    {
        internal static long AbsoluteTenths(double percent)
        {
            // Rounds a percentage to whole tenths without MidpointRounding, which the game's CLR lacks.
            return (long)Math.Floor(Math.Abs(percent) * 10.0 + 0.5);
        }

        internal static string Format(float percent, bool signed)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent))
            {
                return JournalText.Get("Arcana.Common.UnavailableValue");
            }

            long tenths = AbsoluteTenths(percent);
            string number = (tenths / 10).ToString(CultureInfo.InvariantCulture);
            if (tenths % 10 != 0)
            {
                number += "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);
            }

            if (signed)
            {
                return JournalText.Format("Arcana.Common.SignedPercent", percent < 0f && tenths != 0 ? "-" : "+", number);
            }
            return JournalText.Format("Arcana.Common.Percent", number);
        }
    }
}
