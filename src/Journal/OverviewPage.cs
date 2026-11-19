using System;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class OverviewPage
    {
        private const int PanelCount = 5;
        private const int Rank = 0, Statistics = 1, Perks = 2, Knowledge = 3, LifeState = 4;
        private const int StatisticsRows = 6;
        private const int PerkRows = 8;
        private const int KnowledgeRows = 8;
        private const int UpperSlots = 3;
        private const int LowerSlots = 5;
        private const float HeaderHeight = 38f;
        private const float HeadingPadding = 14f;
        private const float OutlineInset = 3f;
        private const float OutlineBottomExtra = 2f;
        private const float BottomEdge = 3f;
        private const float Gap = 12f;
        private const float TopInset = 14f;
        private const float BottomGap = 14f;
        private const float LeftShare = 0.6f;
        private const float LifeMinimumHeight = 120f;
        private const float RankPadding = 22f;
        private const float NumeralWidth = 84f;
        private const float RankTextGap = 16f;
        private const float RankTitleTop = 14f;
        private const float RankTitleHeight = 30f;
        private const float RankBodyGap = 4f;
        private const float RankBottomPadding = 10f;
        private const float ArtworkWidth = 150f;
        private const float LifeIconColumn = 124f;
        private const float LifeIconSize = 96f;
        private const float LifeTextLeft = 136f;
        private const float LifePadding = 22f;
        private const float LifeTextTop = 14f;
        private const float LifeTextBottom = 10f;
        private const float LifeTitleHeight = 30f;
        private static readonly string NoRank = JournalText.Get("Arcana.Journal.Overview.Rank.Empty");
        private static readonly Color Ink = new Color(0xFF0B246E);
        private static readonly Color Accent = new Color(0xFF124F92);
        private static readonly Color Muted = new Color(0xFF5F78A3);

        private readonly JournalWindow journal;
        private readonly Window[] outlines = new Window[PanelCount];
        private readonly Window[] bodies = new Window[PanelCount];
        private readonly Window[] cells = new Window[3];
        private readonly Text[] titles = new Text[3];
        private readonly OverviewList[] lists = new OverviewList[3];
        private readonly Text rankTitle;
        private readonly Text rankText;
        private readonly Text numeral;
        private readonly Window artwork;
        private readonly ImageDrawable artworkImage;
        private readonly Window lifeIcon;
        private readonly ImageDrawable lifeImage;
        private readonly Window lifeArt;
        private readonly ImageDrawable lifeArtImage;
        private readonly Text lifeTitle;
        private readonly Text lifeText;
        private OverviewData data = new OverviewData();
        private bool visible;
        private bool laidOut;
        private bool artworkPlaced;
        private float rankLeft, rankTop, rankWidth, rankHeight;
        private float placedTextWidth, placedTextHeight;

        internal static OverviewPage Create(JournalWindow journal)
        {
            return new OverviewPage(journal);
        }

        private OverviewPage(JournalWindow journal)
        {
            this.journal = journal;
            for (int i = 0; i < PanelCount; i++)
            {
                outlines[i] = journal.Control<Window>(NativeLayouts.OverviewFrame + (uint)(2 * i));
                bodies[i] = journal.Control<Window>(NativeLayouts.OverviewFrame + (uint)(2 * i + 1));
                outlines[i].IgnoreMouse = true;
            }
            bodies[Rank].IgnoreMouse = true;
            bodies[LifeState].IgnoreMouse = true;

            string[] headings = { JournalText.Get("Arcana.Journal.Overview.Section.Attributes"), JournalText.Get("Arcana.Journal.Tab.Perks"), JournalText.Get("Arcana.Journal.Overview.Section.Knowledge") };
            for (int i = 0; i < 3; i++)
            {
                cells[i] = journal.Control<Window>(NativeLayouts.OverviewHeaderCell + (uint)i);
                cells[i].IgnoreMouse = true;
                titles[i] = journal.Label(NativeLayouts.OverviewHeaderTitle + (uint)i, headings[i], 0, 0, 100, HeaderHeight);
            }

            lists[0] = new OverviewList(journal, GetListId(0), StatisticsRows, bodies[Statistics],
                JournalText.Get("Arcana.Journal.Overview.Empty.Attributes"), 0f, 124f, false, true);
            lists[1] = new OverviewList(journal, GetListId(1), PerkRows, bodies[Perks],
                JournalText.Get("Arcana.Journal.Overview.Empty.Perks"), 56f, 96f, false, false);
            lists[2] = new OverviewList(journal, GetListId(2), KnowledgeRows, bodies[Knowledge],
                JournalText.Get("Arcana.Journal.Overview.Empty.Knowledge"), 0f, 64f, true, false);

            rankTitle = journal.Label(NativeLayouts.OverviewRankTitle, JournalText.Get("Arcana.Journal.Overview.Section.Rank"), 0, 0, 100, RankTitleHeight);
            rankText = journal.Label(NativeLayouts.OverviewRankText, "", 0, 0, 100, 100);
            numeral = journal.Label(NativeLayouts.OverviewRankNumeral, "", 0, 0, NumeralWidth, 100);
            artwork = journal.Control<Window>(NativeLayouts.OverviewArtwork);
            artworkImage = artwork.Drawable as ImageDrawable;
            lifeIcon = journal.Control<Window>(NativeLayouts.OverviewLifeIcon);
            lifeImage = lifeIcon.Drawable as ImageDrawable;
            lifeArt = journal.Control<Window>(NativeLayouts.OverviewLifeArt);
            lifeArtImage = lifeArt.Drawable as ImageDrawable;
            lifeTitle = journal.Label(NativeLayouts.OverviewLifeTitle, "", 0, 0, 100, LifeTitleHeight);
            lifeText = journal.Label(NativeLayouts.OverviewLifeText, "", 0, 0, 100, 100);
            Show(false);
        }

        private static uint GetListId(int index)
        {
            return NativeLayouts.OverviewFirstList + (uint)index * NativeLayouts.OverviewListStride;
        }

        internal void ApplyInk()
        {
            // Restores the layout's ink colours after theming.
            for (int i = 0; i < titles.Length; i++)
            {
                titles[i].TextColor = Ink;
            }
            rankTitle.TextColor = Ink;
            rankText.TextColor = Ink;
            numeral.TextColor = Ink;
            lifeTitle.TextColor = Ink;
            lifeText.TextColor = Ink;
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i].ApplyInk(Ink, Accent, Muted);
            }
        }

        internal void Show(bool show)
        {
            visible = show;
            for (int i = 0; i < PanelCount; i++)
            {
                outlines[i].Visible = show;
                bodies[i].Visible = show;
            }
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Visible = show;
                titles[i].Visible = show;
            }
            rankTitle.Visible = show;
            rankText.Visible = show;
            lifeTitle.Visible = show;
            lifeText.Visible = show;
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i].Show(show);
            }
            Apply();
        }

        internal void Refresh(OverviewData next)
        {
            // Shows new data by updating captions, images and pooled rows only.
            data = next ?? new OverviewData();
            Apply();
        }

        private void Apply()
        {
            bool hasRank = data.RankTitle.Length != 0;
            OverviewList.SetCaption(rankTitle, hasRank ? data.RankTitle : JournalText.Get("Arcana.Journal.Overview.Section.Rank"));
            OverviewList.SetCaption(rankText, hasRank ? data.RankDescription : NoRank);
            OverviewList.SetCaption(lifeTitle, data.LifeStateTitle);
            OverviewList.SetCaption(lifeText, data.LifeStateText);
            SetImage(lifeIcon, lifeImage, data.LifeStateArt == null ? data.LifeStateIcon : null);
            SetImage(lifeArt, lifeArtImage, data.LifeStateArt);
            SetImage(artwork, artworkImage, data.RankArtwork);

            bool showNumeral = visible && data.RankLevel > 0;
            numeral.Visible = showNumeral;
            OverviewList.SetCaption(numeral, showNumeral ? OverviewProviders.GetRomanNumeral(data.RankLevel) : "");

            lists[0].SetRows(data.Statistics);
            lists[1].SetRows(data.Perks);
            lists[2].SetRows(data.Knowledge);

            if (laidOut && (data.RankArtwork != null) != artworkPlaced)
            {
                PlaceRank();
            }
        }

        private void SetImage(Window window, ImageDrawable drawable, UIImage image)
        {
            if (drawable != null && drawable.Image != image)
            {
                drawable.Image = image;
                window.Invalidate();
            }
            window.Visible = visible && image != null;
        }

        private static float GetTableHeight(int rows)
        {
            return HeaderHeight + rows * OverviewList.RowHeight + BottomEdge;
        }

        internal void Layout()
        {
            // Places every panel inside the content region.
            float left = WindowGeometry.ContentLeft + WindowGeometry.ContentInset;
            float paneWidth = WindowGeometry.ContentWidth - 2f * WindowGeometry.ContentInset;
            float leftWidth = (float)Math.Floor((paneWidth - Gap) * LeftShare + 0.5f);
            float rightWidth = paneWidth - Gap - leftWidth;
            float rightLeft = left + leftWidth + Gap;
            float top = WindowGeometry.ContentTop + TopInset;
            float upperHeight = GetTableHeight(UpperSlots);
            float lowerHeight = GetTableHeight(LowerSlots);
            float lowerTop = top + upperHeight + Gap;
            float lifeTop = lowerTop + lowerHeight + Gap;
            float lifeHeight = Math.Max(LifeMinimumHeight, WindowGeometry.ContentBottom - BottomGap - lifeTop);

            rankLeft = left;
            rankTop = top;
            rankWidth = leftWidth;
            rankHeight = upperHeight;
            laidOut = true;
            PlaceRank();
            PlaceTable(Statistics, 0, rightLeft, top, rightWidth, upperHeight);
            PlaceTable(Perks, 1, left, lowerTop, leftWidth, lowerHeight);
            PlaceTable(Knowledge, 2, rightLeft, lowerTop, rightWidth, lowerHeight);
            PlaceLifeState(left, lifeTop, paneWidth, lifeHeight);
            Apply();
        }

        private void PlaceFrame(int panel, float x, float y, float width, float height)
        {
            // Places a bordered panel with its outline on the panel's edge.
            journal.Place(outlines[panel], x - OutlineInset, y, width + 2f * OutlineInset, height);
            journal.Place(bodies[panel], x - OutlineInset + 1f, y + 1f, width + 2f * OutlineInset - 2f, height - 2f);
        }

        private void PlaceRank()
        {
            // Places the rank numeral, title, description and optional artwork.
            float x = rankLeft, y = rankTop, width = rankWidth, height = rankHeight;
            PlaceFrame(Rank, x, y, width, height);

            float numeralLeft = x + RankPadding;
            journal.Place(numeral, numeralLeft, y, NumeralWidth, height);

            artworkPlaced = data.RankArtwork != null;
            float textLeft = numeralLeft + NumeralWidth + RankTextGap;
            float textRight = x + width - RankPadding;
            if (artworkPlaced)
            {
                float artLeft = textRight - ArtworkWidth;
                journal.Place(artwork, artLeft, y + RankPadding, ArtworkWidth, height - 2f * RankPadding);
                textRight = artLeft - RankPadding;
            }
            float textWidth = Math.Max(1f, textRight - textLeft);

            journal.PlaceSnapped(rankTitle, textLeft, y + RankTitleTop, textWidth, RankTitleHeight);
            float bodyTop = y + RankTitleTop + RankTitleHeight + RankBodyGap;
            float bodyHeight = Math.Max(1f, y + height - RankBottomPadding - bodyTop);
            journal.PlaceSnapped(rankText, textLeft, bodyTop, textWidth, bodyHeight);
            if (textWidth != placedTextWidth || bodyHeight != placedTextHeight)
            {
                placedTextWidth = textWidth;
                placedTextHeight = bodyHeight;
                OverviewList.Reflow(rankText);
            }
        }

        private void PlaceTable(int panel, int list, float x, float y, float width, float height)
        {
            // Places a list panel built like the Grimoire table.
            float listTop = y + HeaderHeight - 1f;
            float outlineHeight = y + height + OutlineBottomExtra - listTop;
            journal.Place(outlines[panel], x - OutlineInset, listTop, width + 2f * OutlineInset, outlineHeight);
            journal.Place(bodies[panel], x - OutlineInset + 1f, listTop, width + 2f * OutlineInset - 2f, outlineHeight - 1f);
            journal.Place(cells[list], x, y, width, HeaderHeight);
            journal.Place(titles[list], x + HeadingPadding, y, width - 2f * HeadingPadding, HeaderHeight);
            lists[list].Layout(x + 1f, y + HeaderHeight, width - 2f, height - HeaderHeight - BottomEdge);
        }

        private void PlaceLifeState(float x, float y, float width, float height)
        {
            // Places the life state icon, title and description.
            PlaceFrame(LifeState, x, y, width, height);
            float iconLeft = x + (LifeIconColumn - LifeIconSize) / 2f;
            float iconTop = y + (float)Math.Floor((height - LifeIconSize) / 2f);
            journal.Place(lifeIcon, iconLeft, iconTop, LifeIconSize, LifeIconSize);
            journal.Place(lifeArt, iconLeft, iconTop, LifeIconSize, LifeIconSize);
            float textWidth = width - LifeTextLeft - LifePadding;
            float titleTop = y + LifeTextTop;
            journal.Place(lifeTitle, x + LifeTextLeft, titleTop, textWidth, LifeTitleHeight);
            float bodyTop = titleTop + LifeTitleHeight;
            journal.Place(lifeText, x + LifeTextLeft, bodyTop, textWidth, Math.Max(1f, y + height - LifeTextBottom - bodyTop));
        }

        internal void Release()
        {
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i].Release();
            }
        }
    }
}
