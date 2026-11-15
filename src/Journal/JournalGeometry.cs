using System;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    // The Skill & Power panel in the journal header, in unscaled layout units; its rows line up with the title and Sim name lines.
    internal static class HeaderLayout
    {
        // Centred on the portrait and the three identity lines (68), which the 72 high panel spans.
        internal const float PanelTop = 32f;
        internal const float PanelWidth = 388f;
        // The content panel ends this far from the window's right edge.
        internal const float RightMargin = 36f;
        internal const float PanelGap = 12f;
        internal const float Padding = 12f;
        internal const float IconSize = 24f;
        internal const float IconGap = 8f;
        // Wide enough for the longest label ("Magical Proficiency").
        internal const float LabelWidth = 136f;
        internal const float LabelGap = 8f;
        internal const float BarWidth = 104f;
        internal const float BarHeight = 12f;
        internal const float BarGap = 8f;
        // Wide enough for the longest value ("Apprentice", "High Witch").
        internal const float ValueWidth = 76f;
        internal const float RowHeight = 24f;
        internal const float RowPitch = 34f;
        internal const float FirstRowCenter = 19f;

        internal static float PanelRight(float windowWidth) { return windowWidth - RightMargin; }
        internal static float PanelLeft(float windowWidth) { return PanelRight(windowWidth) - PanelWidth; }
        internal static float RowCenter(int row) { return PanelTop + FirstRowCenter + row * RowPitch; }
        internal static float IconLeft(float windowWidth) { return PanelLeft(windowWidth) + Padding; }
        internal static float LabelLeft(float windowWidth) { return IconLeft(windowWidth) + IconSize + IconGap; }
        internal static float BarLeft(float windowWidth) { return LabelLeft(windowWidth) + LabelWidth + LabelGap; }
        internal static float ValueLeft(float windowWidth) { return BarLeft(windowWidth) + BarWidth + BarGap; }
    }

    // Geometry is measured in the already scaled Journal parent coordinates.
    internal struct HeaderMeterBounds
    {
        // Effective painted bounds of the stock 104 x 12 capsule measured at
        // the centerline in the supplied full-resolution 1x screenshot.
        private const float LeftInset = 4f;
        private const float RightInset = 7f;
        private const float SparkleCapInset = 2f;
        private const float SparkleHeight = 5f;

        internal readonly Rect Track;
        internal readonly Rect Interior;
        internal readonly Rect Fill;
        internal readonly Rect SparkleClip;

        internal HeaderMeterBounds(Rect track, float fraction)
        {
            Track = track;
            float horizontalScale = track.Width / HeaderLayout.BarWidth;
            float innerLeft = track.TopLeft.x + LeftInset * horizontalScale;
            float innerRight = track.BottomRight.x - RightInset * horizontalScale;
            Interior = new Rect(innerLeft, track.TopLeft.y, innerRight, track.BottomRight.y);

            // The texture supplies the vertical outline and rounded caps.
            // The reveal advances from the first painted column to the last.
            float clamped = fraction > 0f ? Math.Min(fraction, 1f) : 0f;
            float end = clamped == 1f ? innerRight : innerLeft + (innerRight - innerLeft) * clamped;
            Fill = new Rect(innerLeft, track.TopLeft.y, end, track.BottomRight.y);

            // Keep the stationary textures in the common center band of both caps.
            float inset = SparkleCapInset * horizontalScale;
            float height = SparkleHeight * track.Height / HeaderLayout.BarHeight;
            float y = track.TopLeft.y + (track.Height - height) * .5f;
            float left = Math.Min(Fill.BottomRight.x, innerLeft + inset);
            float right = Math.Max(left, Math.Min(Fill.BottomRight.x, innerRight - inset));
            SparkleClip = new Rect(left, y, right, y + height);
        }

        // Child coordinates relative to the fill viewport. The stock drawable
        // stays co-registered with the track at all fractions and UI scales.
        internal Rect FillArt
        {
            get
            {
                return NativeLayouts.Box(Track.TopLeft.x - Fill.TopLeft.x,
                    Track.TopLeft.y - Fill.TopLeft.y, Track.Width, Track.Height);
            }
        }

    }

    // The one outer size and content region of the Journal for a Sim with an occult life state. Overview, Perks and Grimoire all use
    // it, so the window never changes size or position between pages; the Perks page is the reference these numbers come from.
    internal static class WindowGeometry
    {
        internal const float Width = 1000f;
        internal const float Height = 860f;
        internal const float ContentLeft = 24f;
        internal const float ContentTop = 144f;
        internal const float ContentRight = 968f;
        internal const float ContentBottom = 758f;
        // Overview and Grimoire keep this much clear inside the content region.
        internal const float ContentInset = 24f;
        internal const float ContentWidth = ContentRight - ContentLeft;
        internal const float ContentHeight = ContentBottom - ContentTop;
    }

    internal static class PowerLayout
    {
        internal const float TileSize = 84f;
        internal const float IconSize = 64f;
        internal const float IconTop = 4f;
        internal const float BandTop = IconTop + IconSize + 2f;
        internal const float BandHeight = 18f;
        internal const float CellHeight = BandTop + BandHeight + 2f;
        internal const float PitchX = 140f;
        internal const float PitchY = 114f;
        internal const float GridTop = 178f;

        internal const float RankMarker = 40f;
        internal const float RankTrackWidth = 13f;
        internal const float PowerLeft = 110f;
        internal const float PanelTop = WindowGeometry.ContentTop;
        internal const float PanelBottom = WindowGeometry.ContentBottom;
        internal const float PowersPanelLeft = WindowGeometry.ContentLeft;
        internal const float PowersPanelRight = WindowGeometry.ContentRight;

        // Cost strip: book icon, a small gap, then one digit, centred together on the tile.
        internal const float CostIconSize = 16f;
        internal const float CostGap = 3f;
        internal const float CostDigitWidth = 8f;
        // Footer group (help disc, label, book icon, value): every size is the earlier 13pt footer scaled by about 1.15 along with the font.
        // "Perk Points:" is right-aligned to end CaptionWidth after its start, so the icon follows it directly.
        internal const float FooterIconSize = 22f;
        internal const float FooterLabelGap = 16f;
        internal const float FooterValueGap = 6f;
        // "Pending: n" follows the Perk Points value, clear of a two-digit count.
        internal const float PendingLeftGap = 44f;
        internal const float PendingWidth = 140f;
        internal const float CaptionWidth = 110f;
        internal const float CostGroupWidth = CostIconSize + CostGap + CostDigitWidth;

        // Rank bar milestones: two notches between each pair of rank markers (slots NotchSlot upwards).
        internal const int NotchesPerRank = 2;
        internal const int NotchSlot = 16;
        internal const float NotchWidth = 10f;
        internal const float NotchHeight = 4f;

        // Multi-rank badge ("0/3"), anchored at the tile's top-left.
        internal const float CounterWidth = 38f;
        internal const float CounterHeight = 20f;

        internal const float FooterHeight = 44f;
        internal const float FooterTop = PanelBottom + 8f;
        internal const float HelpSize = 44f;
        // Native checkmark face from the working question-mark build.
        // The help disc and all footer counters keep their existing positions.
        internal const float HelpDiscSize = 31f;
        internal const float HelpOffsetX = 12f;
        internal const float HelpGlyphShiftX = 1f;
        internal const float HelpGlyphShiftY = 2.3f; // the question mark sits slightly low in the disc, as in the working help build
        internal const float HelpRaise = 6.5f; // lifts the disc to the optical centre of the 15pt text, which sits above the middle of its line box
        internal const float FooterIconRaise = 1.5f; // the book art sits low in its box, so it is lifted to the same centre
        internal const float HelpGap = 14f;
        internal const float PointsLeft = PowersPanelLeft;

        private const int TopRank = WitchProgression.RankCount - 1;

        internal const float LanesLeft = (PowerLeft + PowersPanelRight - 24f) / 2f - (3f * PitchX + TileSize) / 2f;

        internal static float LaneX(float lane) { return LanesLeft + lane * PitchX; }
        internal static float RowY(int row) { return GridTop + row * PitchY; }
        internal static float RankY(int row) { return RowY(row) + (CellHeight - RankMarker) / 2f; }
        // Top edge of notch `notch` (1 = nearest the upper marker) above the marker of rank `rank`; the notches split the space between the two markers' facing edges into equal gaps.
        internal static float NotchTop(int rank, int notch)
        {
            float upperBottom = RankY(TopRank - rank - 1) + RankMarker;
            float lowerTop = RankY(TopRank - rank);
            float gaps = (lowerTop - upperBottom) - NotchesPerRank * NotchHeight;
            float gap = gaps * notch / (NotchesPerRank + 1);
            return (float)Math.Floor(upperBottom + gap + 0.5f) + (notch - 1) * NotchHeight;
        }
        // The fill's share of the track for a Witch `progress` of the way up from marker `rank`, stretched so its tip meets each notch exactly at its milestone.
        internal static float RankFillFraction(int rank, float progress)
        {
            if (rank >= TopRank)
            {
                return 1f;
            }
            if (rank < 0)
            {
                return 0f;
            }

            float lowerCenter = RankY(TopRank - rank) + RankMarker / 2f;
            float first = lowerCenter - (NotchTop(rank, 2) + NotchHeight / 2f);
            float second = lowerCenter - (NotchTop(rank, 1) + NotchHeight / 2f);
            float thirds = Math.Max(0f, Math.Min(1f, progress)) * 3f;
            float height = thirds < 1f ? first * thirds
                : thirds < 2f ? first + (second - first) * (thirds - 1f)
                : second + (PitchY - second) * (thirds - 2f);
            return (rank * PitchY + height) / RankTrackHeight;
        }

        // The rank bar, its notches and its markers share one centre line, midway between the panel's left edge and the leftmost perk column.
        internal static float RankBarCenter(float leftmostPerk) { return (PowersPanelLeft + leftmostPerk) / 2f; }
        internal static float RankTrackTop { get { return RowY(0) + CellHeight / 2f; } }
        internal static float RankTrackHeight { get { return RowY(TopRank) - RowY(0); } }
    }
}
