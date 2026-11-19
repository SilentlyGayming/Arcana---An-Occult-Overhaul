using System;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class HeaderLayout
    {
        internal const float PanelTop = 32f;
        internal const float PanelWidth = 388f;
        internal const float RightMargin = 36f;
        internal const float PanelGap = 12f;
        internal const float Padding = 12f;
        internal const float IconSize = 24f;
        internal const float IconGap = 8f;
        internal const float LabelWidth = 136f;
        internal const float LabelGap = 8f;
        internal const float BarWidth = 104f;
        internal const float BarHeight = 12f;
        internal const float BarGap = 8f;
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

    internal struct HeaderMeterBounds
    {
        private const float LeftInset = 4f;
        private const float RightInset = 7f;
        private const float SparkleCapInset = 2f;
        private const float SparkleHeight = 5f;

        internal readonly Rect Track;
        internal readonly Rect Fill;
        internal readonly Rect SparkleClip;

        internal HeaderMeterBounds(Rect track, float fraction)
        {
            Track = track;
            float horizontalScale = track.Width / HeaderLayout.BarWidth;
            float innerLeft = track.TopLeft.x + LeftInset * horizontalScale;
            float innerRight = track.BottomRight.x - RightInset * horizontalScale;

            float clamped = fraction > 0f ? Math.Min(fraction, 1f) : 0f;
            float end = clamped == 1f ? innerRight : innerLeft + (innerRight - innerLeft) * clamped;
            Fill = new Rect(innerLeft, track.TopLeft.y, end, track.BottomRight.y);

            float inset = SparkleCapInset * horizontalScale;
            float height = SparkleHeight * track.Height / HeaderLayout.BarHeight;
            float y = track.TopLeft.y + (track.Height - height) * .5f;
            float left = Math.Min(Fill.BottomRight.x, innerLeft + inset);
            float right = Math.Max(left, Math.Min(Fill.BottomRight.x, innerRight - inset));
            SparkleClip = new Rect(left, y, right, y + height);
        }

        internal Rect FillArt
        {
            get
            {
                return NativeLayouts.Box(Track.TopLeft.x - Fill.TopLeft.x,
                    Track.TopLeft.y - Fill.TopLeft.y, Track.Width, Track.Height);
            }
        }
    }

    internal static class WindowGeometry
    {
        internal const float Width = 1000f;
        internal const float Height = 860f;
        internal const float ContentLeft = 24f;
        internal const float ContentTop = 144f;
        internal const float ContentRight = 968f;
        internal const float ContentBottom = 758f;
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

        internal const float CostIconSize = 16f;
        internal const float CostGap = 3f;
        internal const float CostDigitWidth = 8f;
        internal const float FooterIconSize = 22f;
        internal const float FooterLabelGap = 16f;
        internal const float FooterValueGap = 6f;
        internal const float PendingLeftGap = 44f;
        internal const float PendingWidth = 140f;
        internal const float CaptionWidth = 110f;
        internal const float CostGroupWidth = CostIconSize + CostGap + CostDigitWidth;

        internal const int NotchesPerRank = 2;
        internal const int NotchSlot = 16;
        internal const float NotchWidth = 10f;
        internal const float NotchHeight = 4f;

        internal const float CounterWidth = 38f;
        internal const float CounterHeight = 20f;

        internal const float FooterHeight = 44f;
        internal const float FooterTop = PanelBottom + 8f;
        internal const float HelpSize = 44f;
        internal const float HelpDiscSize = 31f;
        internal const float HelpOffsetX = 12f;
        internal const float HelpGlyphShiftX = 1f;
        internal const float HelpGlyphShiftY = 2.3f;
        internal const float HelpRaise = 6.5f;
        internal const float FooterIconRaise = 1.5f;
        internal const float HelpGap = 14f;
        internal const float PointsLeft = PowersPanelLeft;

        private const int TopRank = WitchProgression.RankCount - 1;

        internal const float LanesLeft = (PowerLeft + PowersPanelRight - 24f) / 2f - (3f * PitchX + TileSize) / 2f;

        internal static float LaneX(float lane) { return LanesLeft + lane * PitchX; }
        internal static float RowY(int row) { return GridTop + row * PitchY; }
        internal static float RankY(int row) { return RowY(row) + (CellHeight - RankMarker) / 2f; }
        internal static float NotchTop(int rank, int notch)
        {
            // Returns the top edge of a notch above a rank marker, splitting the gap between markers evenly.
            float upperBottom = RankY(TopRank - rank - 1) + RankMarker;
            float lowerTop = RankY(TopRank - rank);
            float gaps = (lowerTop - upperBottom) - NotchesPerRank * NotchHeight;
            float gap = gaps * notch / (NotchesPerRank + 1);
            return (float)Math.Floor(upperBottom + gap + 0.5f) + (notch - 1) * NotchHeight;
        }
        internal static float RankFillFraction(int rank, float progress)
        {
            // Returns the fill's share of the track so its tip meets each notch at its milestone.
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

        internal static float RankBarCenter(float leftmostPerk) { return (PowersPanelLeft + leftmostPerk) / 2f; }
        internal static float RankTrackTop { get { return RowY(0) + CellHeight / 2f; } }
        internal static float RankTrackHeight { get { return RowY(TopRank) - RowY(0); } }
    }
}
