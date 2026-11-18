using System;
using System.Globalization;

namespace SupernaturalExpanded.Journal
{
    internal static class PercentText
    {
        // The game's CLR does not implement Math.Round with MidpointRounding.
        internal static long AbsoluteTenths(double percent)
        {
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
