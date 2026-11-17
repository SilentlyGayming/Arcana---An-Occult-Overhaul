using System;
using System.Collections.Generic;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal enum PageSlot
    {
        RankTrack = 0, RankFill = 1, PointsHelp = 6, Points = 7,
        PointsIcon = 8, PointsValue = 9, PointsHelpOuter = 10,
        PowersPanel = 11, Pending = 14,
        RankSparkClip = 40, RankSparkTipClip = 64, SelectionOverlay = 119
    }

    internal sealed class PowerPage
    {
        private const uint FadedAlpha = 0x66000000;
        // At most this many icons fly to the Journal button, however many perks were bought at once.
        private const int MostFlights = 4;
        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };
        private static readonly Color LitIcon = new Color(0xFFFFFFFF);
        // Half-transparent and darkened, so the icon still reads as unavailable in a skin that ignores one of the two.
        private static readonly Color FadedIcon = new Color(0x80A0A8B4);
        // A cost the Sim cannot pay is red, as unaffordable prices are elsewhere in the game; a selected but unbought rank tints its badge green.
        private static readonly Color UnaffordableCost = new Color(0xFFC02828);
        private static readonly Color PendingPlate = new Color(0xFFFFD4D0);
        private static readonly Color PendingText = new Color(0xFFD42020);
        // A faded perk's outline keeps its colour but loses most of its strength, like the icon.
        private const uint FadedEdgeAlpha = 0x66000000;

        private readonly OccultTree tree;
        private readonly List<WindowBase> controls = new List<WindowBase>();
        private readonly Button[] tiles;
        private readonly Window[] icons;
        // Only a perk with a Face has an edge; the rest stay null.
        private readonly Window[] edges;
        private readonly UIImage[] iconImages;
        private readonly Text[] counts;
        private readonly Window[] counterPlates;
        private readonly Text[] costs;
        private readonly Window[] costPlates;
        private readonly Window[] costIcons;
        private readonly bool[] faded;
        private readonly bool[] unaffordableCost;
        private readonly bool[] pending;
        private readonly Button[] ranks;
        private readonly Text pointsValue;
        private readonly Text pendingText;
        private readonly Window progressTrack;
        private readonly Window progressFill;
        private readonly RankGlitter glitter;
        private readonly PerkFeedback feedback;

        private PowerSelection selection;
        private Color ink;
        private Color costShade;
        private Color plateShade;
        private bool styled;
        private bool visible = true;
        private bool repaintQueued;

        // Returns null when the installed layout lacks the tree's controls.
        internal static PowerPage Create(JournalWindow journal, OccultTree tree)
        {
            return journal.HasControl(tree.Id(PageControl.Page, (int)PageSlot.RankTrack)) ? new PowerPage(journal, tree) : null;
        }

        private PowerPage(JournalWindow journal, OccultTree tree)
        {
            this.tree = tree;
            int perks = tree.Perks.Length;
            tiles = new Button[perks];
            icons = new Window[perks];
            edges = new Window[perks];
            iconImages = new UIImage[perks];
            counts = new Text[perks];
            counterPlates = new Window[perks];
            costs = new Text[perks];
            costPlates = new Window[perks];
            costIcons = new Window[perks];
            faded = new bool[perks];
            unaffordableCost = new bool[perks];
            pending = new bool[perks];
            ranks = new Button[tree.RankNames.Length];

            progressTrack = Add(journal.Control<Window>(Slot(PageSlot.RankTrack)));
            progressFill = Add(journal.Control<Window>(Slot(PageSlot.RankFill)));
            glitter = new RankGlitter(journal.Control<Window>(Slot(PageSlot.RankSparkClip)),
                journal.Control<Window>(Slot(PageSlot.RankSparkTipClip)));
            float leftmost = PowerLayout.LanesLeft;
            if (perks > 0)
            {
                leftmost = float.MaxValue;
                foreach (Perk column in tree.Perks)
                {
                    leftmost = Math.Min(leftmost, PowerLayout.LaneX(column.Column + column.Shift));
                }
            }
            float barCenter = PowerLayout.RankBarCenter(leftmost);
            journal.Place(progressTrack, barCenter - PowerLayout.RankTrackWidth / 2f,
                PowerLayout.RankTrackTop, PowerLayout.RankTrackWidth, PowerLayout.RankTrackHeight);
            for (int rank = 0; rank < ranks.Length - 1; rank++)
            {
                for (int notch = 1; notch <= PowerLayout.NotchesPerRank; notch++)
                {
                    // Three stacked rows (short, full width, short) make one tick with softened ends.
                    int first = PowerLayout.NotchSlot + (rank * PowerLayout.NotchesPerRank + notch - 1) * 3;
                    float left = barCenter - PowerLayout.NotchWidth / 2f;
                    float top = PowerLayout.NotchTop(rank, notch);
                    float row = PowerLayout.NotchHeight / 4f;
                    journal.Place(Add(journal.Control<Window>(tree.Id(PageControl.Page, first))),
                        left + 1f, top, PowerLayout.NotchWidth - 2f, row);
                    journal.Place(Add(journal.Control<Window>(tree.Id(PageControl.Page, first + 1))),
                        left, top + row, PowerLayout.NotchWidth, row * 2f);
                    journal.Place(Add(journal.Control<Window>(tree.Id(PageControl.Page, first + 2))),
                        left + 1f, top + row * 3f, PowerLayout.NotchWidth - 2f, row);
                }
            }
            journal.Place(Add(journal.Control<Window>(Slot(PageSlot.PowersPanel))), PowerLayout.PowersPanelLeft,
                PowerLayout.PanelTop, PowerLayout.PowersPanelRight - PowerLayout.PowersPanelLeft,
                PowerLayout.PanelBottom - PowerLayout.PanelTop);

            for (int i = 0; i < ranks.Length; i++)
            {
                ranks[i] = Add(journal.Button(tree.Id(PageControl.RankMarker, i), Numerals[i], barCenter - PowerLayout.RankMarker / 2f,
                    PowerLayout.RankY(ranks.Length - 1 - i), PowerLayout.RankMarker, PowerLayout.RankMarker, delegate { }));
            }

            for (int i = 0; i < perks; i++)
            {
                Perk perk = tree.Perks[i];
                float x = PowerLayout.LaneX(perk.Column + perk.Shift);
                float y = PowerLayout.RowY(ranks.Length - 1 - (int)perk.Rank);
                tiles[i] = Add(journal.Button(tree.Id(PageControl.Tile, i), "", x, y, PowerLayout.TileSize, PowerLayout.CellHeight,
                    delegate { StagePerk(perk); }));
                tiles[i].CreateTooltipCallbackFunction = TooltipFor(perk);
                Window edge = journal.Control<Window>(tree.Id(PageControl.Edge, i));
                if (perk.Face != 0)
                {
                    // In front of the tile and its plates; the icon never takes this colour.
                    edges[i] = Add(edge);
                    journal.Place(edge, x, y, PowerLayout.TileSize, PowerLayout.CellHeight);
                    edge.ShadeColor = new Color(perk.Face);
                }
                else
                {
                    edge.Visible = false;
                }
                icons[i] = Add(journal.Control<Window>(tree.Id(PageControl.Icon, i)));
                journal.Place(icons[i], x + (PowerLayout.TileSize - PowerLayout.IconSize) / 2f, y + PowerLayout.IconTop,
                    PowerLayout.IconSize, PowerLayout.IconSize);
                // Multi-rank counters stay upper-left. The pending +1 for a single-rank perk
                // is centered at the TOP of the tile rather than floating at its left edge.
                float badgeLeft = perk.Tiers > 1 ? x + 2f : x + (PowerLayout.TileSize - PowerLayout.CounterWidth) / 2f;
                counts[i] = Add(journal.Label(tree.Id(PageControl.Counter, i), "", badgeLeft, y + 2f,
                    PowerLayout.CounterWidth, PowerLayout.CounterHeight));
                counterPlates[i] = Add(journal.Control<Window>(tree.Id(PageControl.CounterPlate, i)));
                journal.Place(counterPlates[i], badgeLeft, y + 2f, PowerLayout.CounterWidth, PowerLayout.CounterHeight);
                float top = y + PowerLayout.BandTop;
                costPlates[i] = Add(journal.Control<Window>(tree.Id(PageControl.CostPlate, i)));
                journal.Place(costPlates[i], x + 4f, top, PowerLayout.TileSize - 8f, PowerLayout.BandHeight);
                // Icon and number form one group centred on the tile; the number is centred in its own box.
                float groupLeft = x + (PowerLayout.TileSize - PowerLayout.CostGroupWidth) / 2f;
                costIcons[i] = Add(journal.Control<Window>(tree.Id(PageControl.CostIcon, i)));
                journal.Place(costIcons[i], groupLeft, top + 1f, PowerLayout.CostIconSize, PowerLayout.CostIconSize);
                float digitCenter = groupLeft + PowerLayout.CostIconSize + PowerLayout.CostGap + PowerLayout.CostDigitWidth / 2f;
                costs[i] = Add(journal.Label(tree.Id(PageControl.Cost, i), "", digitCenter - 11f, top,
                    22f, PowerLayout.BandHeight));
            }

            float pointsLeft = PowerLayout.PointsLeft;
            // The help disc takes the themed checkmark's face (JournalTheme.StyleClose); the '?' is stock text colored by the theme's text donor.
            Window helpDisc = Add(journal.Control<Window>(Slot(PageSlot.PointsHelpOuter)));
            Text helpGlyph = Add(journal.Control<Text>(Slot(PageSlot.PointsHelp)));
            float helpLeft = pointsLeft + PowerLayout.HelpOffsetX;
            PlaceHelp(journal, helpDisc, helpLeft, PowerLayout.HelpDiscSize, 0f, 0f);
            PlaceHelp(journal, helpGlyph, helpLeft, PowerLayout.HelpDiscSize,
                PowerLayout.HelpGlyphShiftX, PowerLayout.HelpGlyphShiftY);
            helpGlyph.Caption = "?";
            helpGlyph.TooltipText = tree.PointsHelp;
            // The caption is right-aligned in a box that ends CaptionWidth after its start, so the icon sits just after the colon.
            float captionLeft = pointsLeft + PowerLayout.HelpSize + PowerLayout.HelpGap;
            Add(journal.Label(Slot(PageSlot.Points), JournalText.Format("Arcana.Journal.Perks.PointsCaption", tree.PointsName), captionLeft - 28f,
                PowerLayout.FooterTop, PowerLayout.CaptionWidth + 28f, PowerLayout.FooterHeight));
            float iconLeft = captionLeft + PowerLayout.CaptionWidth + PowerLayout.FooterLabelGap;
            journal.Place(Add(journal.Control<Window>(Slot(PageSlot.PointsIcon))), iconLeft,
                PowerLayout.FooterTop + (PowerLayout.FooterHeight - PowerLayout.FooterIconSize) / 2f - PowerLayout.FooterIconRaise,
                PowerLayout.FooterIconSize, PowerLayout.FooterIconSize);
            pointsValue = Add(journal.Label(Slot(PageSlot.PointsValue), "", iconLeft + PowerLayout.FooterIconSize + PowerLayout.FooterValueGap,
                PowerLayout.FooterTop, 60f, PowerLayout.FooterHeight));
            pendingText = Add(journal.Label(Slot(PageSlot.Pending), "", iconLeft + PowerLayout.FooterIconSize + PowerLayout.FooterValueGap + PowerLayout.PendingLeftGap,
                PowerLayout.FooterTop, PowerLayout.PendingWidth, PowerLayout.FooterHeight));
            Window overlay = journal.Control<Window>(Slot(PageSlot.SelectionOverlay));
            journal.Place(overlay, 0f, 0f, WindowGeometry.Width, WindowGeometry.Height);
            feedback = new PerkFeedback(journal, overlay);
        }

        private static void PlaceHelp(JournalWindow journal, WindowBase control, float left,
            float size, float xOffset, float yOffset)
        {
            journal.Place(control,
                left + (PowerLayout.HelpSize - size) / 2f + xOffset,
                PowerLayout.FooterTop + (PowerLayout.FooterHeight - size) / 2f + yOffset - PowerLayout.HelpRaise,
                size, size);
        }

        private uint Slot(PageSlot slot)
        {
            return tree.Id(PageControl.Page, (int)slot);
        }

        private T Add<T>(T control) where T : WindowBase
        {
            controls.Add(control);
            return control;
        }

        internal void Show(PowerSelection current)
        {
            if (selection != current)
            {
                selection = current;
                feedback.Hide();
            }

            bool show = current != null;
            bool revealed = show && !visible;
            if (visible != show)
            {
                visible = show;
                if (!show)
                {
                    StopAnimations();
                }
                foreach (WindowBase control in controls)
                {
                    control.Visible = show;
                }
            }
            if (visible)
            {
                Refresh();
            }
            if (revealed)
            {
                QueueRankLabelRepaint();
            }
        }

        // A numeral whose glyphs are rasterised in the frame that first paints its marker stays blank until invalidated, so the markers are repainted once after the page appears.
        private void QueueRankLabelRepaint()
        {
            if (repaintQueued)
            {
                return;
            }
            repaintQueued = true;
            JournalBootstrap.RunLater(delegate
            {
                repaintQueued = false;
                if (!visible)
                {
                    return;
                }
                foreach (Button rank in ranks)
                {
                    if (!rank.Disposed)
                    {
                        rank.Invalidate();
                    }
                }
            });
        }

        // Drops what is staged without buying it, so the points stay unspent and no purchase effect plays.
        internal void Discard()
        {
            if (selection != null)
            {
                selection.DiscardStaged();
            }
        }

        // Buys what is staged and returns whether anything was bought. With celebrate set, the icon of each perk bought flies from its card to the Journal button.
        internal bool Commit(bool celebrate)
        {
            if (selection == null)
            {
                return false;
            }

            List<Perk> staged = celebrate && visible ? StagedPerks() : null;
            if (!selection.Commit())
            {
                return false;
            }

            JournalBootstrap.RefreshPerkPoints();
            if (staged != null)
            {
                Celebrate(staged);
            }
            return true;
        }

        private List<Perk> StagedPerks()
        {
            List<Perk> staged = new List<Perk>();
            foreach (Perk perk in tree.Perks)
            {
                if (selection.Staged(perk) > 0)
                {
                    staged.Add(perk);
                }
            }
            return staged;
        }

        private void Celebrate(List<Perk> bought)
        {
            Vector2 target;
            if (!JournalBootstrap.TryButtonCenter(out target))
            {
                return;
            }

            int flights = Math.Min(bought.Count, MostFlights);
            for (int i = 0; i < flights; i++)
            {
                Window icon = icons[bought[i].Index];
                ImageDrawable image = icon.Drawable as ImageDrawable;
                if (image == null)
                {
                    continue;
                }
                Rect area = icon.Area;
                PurchaseEffect.Fly(image.Image, icon.WindowToScreen(new Vector2(area.Width / 2f, area.Height / 2f)), target);
            }
        }

        // The perk's own icon, kept once loaded so the Overview list can show the same art.
        internal UIImage PerkIcon(Perk perk)
        {
            if (perk.Index < 0 || perk.Index >= icons.Length)
            {
                return null;
            }
            if (iconImages[perk.Index] == null)
            {
                ImageDrawable image = icons[perk.Index].Drawable as ImageDrawable;
                iconImages[perk.Index] = image == null ? null : image.Image;
            }
            return iconImages[perk.Index];
        }

        internal void StopAnimations()
        {
            glitter.Stop();
            feedback.Hide();
        }

        internal void Release()
        {
            glitter.Stop();
            feedback.Release();
        }

        private void StagePerk(Perk perk)
        {
            if (selection != null && selection.StageNext(perk))
            {
                Refresh();
                JournalBootstrap.RefreshPerkPoints();
                feedback.Play(tiles[perk.Index]);
            }
        }

        internal void RefreshProgress()
        {
            if (!visible || selection == null)
            {
                return;
            }

            Rect track = progressTrack.Area;
            float inset = track.Width / 8f;
            float fill = selection.Available
                ? PowerLayout.RankFillFraction((int)selection.Rank, selection.RankProgress)
                : 0f;
            float height = (track.Height - 2f * inset) * fill;
            float bottom = track.BottomRight.y - inset;
            progressFill.Area = NativeLayouts.Box(track.TopLeft.x + inset, bottom - height,
                track.Width - 2f * inset, height);
            progressFill.Visible = height > 0f;
            glitter.SetFill(progressFill.Area, track);
        }

        private void Refresh()
        {
            RefreshProgress();
            for (int i = 0; i < ranks.Length; i++)
            {
                ranks[i].Selected = selection.Available && i <= (int)selection.Rank;
                string title = tree.RankTitle(i);
                ranks[i].TooltipText = selection.Available && i == (int)selection.Rank
                    ? JournalText.Format("Arcana.Journal.Perks.CurrentRank", title) : title;
            }

            if (!styled)
            {
                ink = costs[0].TextColor;
                costShade = costIcons[0].ShadeColor;
                plateShade = counterPlates[0].ShadeColor;
            }
            for (int i = 0; i < tiles.Length; i++)
            {
                Perk perk = tree.Perks[i];
                int tier = selection.Tier(perk);
                // A perk with ranks owned or staged stays lit, even when no further rank can be picked.
                bool open = tier > 0 || selection.CanStage(perk);
                tiles[i].Enabled = open;
                tiles[i].Selected = tier > 0;
                SetEdge(i, perk, !open);
                SetPerkFaded(i, !open, selection.ShortOnPoints(perk));
                // A rank that is selected but not bought shows the badge even on a single-rank perk ("+1"), tinted, so it never
                // reads as already owned.
                bool staged = selection.Staged(perk) > 0;
                SetPending(i, staged);
                // Keep normal ownership counts navy; pending purchases are visibly red.
                Color badgeColor = staged ? PendingText : (faded[i] ? GetFadedColor(ink) : ink);
                if (counts[i].TextColor.ARGB != badgeColor.ARGB)
                {
                    counts[i].TextColor = badgeColor;
                    counts[i].Invalidate();
                }
                counts[i].Caption = perk.Tiers > 1 ? JournalText.Format("Arcana.Common.CompactRatio", tier, perk.Tiers) : (staged ? "+1" : "");
                counterPlates[i].Visible = perk.Tiers > 1 || staged;
                bool upgradable = tier < perk.Tiers;
                costs[i].Caption = upgradable ? perk.Costs[tier].ToString() : "";
                costPlates[i].Visible = upgradable;
                costIcons[i].Visible = upgradable;
            }
            styled = true;

            pointsValue.Caption = selection.PowerPointsLeft.ToString();
            int waiting = selection.PendingCount;
            pendingText.Caption = waiting > 0 ? JournalText.Format("Arcana.Journal.Perks.Pending", waiting) : "";
            pendingText.Visible = visible && waiting > 0;
        }

        // A specialization's outline takes the colour its tree names; the tile face and the icon are left as they are.
        private void SetEdge(int index, Perk perk, bool fade)
        {
            if (edges[index] == null)
            {
                return;
            }
            uint wanted = fade ? (perk.Face & 0x00FFFFFF) | FadedEdgeAlpha : perk.Face;
            if (edges[index].ShadeColor.ARGB != wanted)
            {
                edges[index].ShadeColor = new Color(wanted);
                edges[index].Invalidate();
            }
        }

        private void SetPending(int index, bool staged)
        {
            if (styled && pending[index] == staged)
            {
                return;
            }
            pending[index] = staged;
            counterPlates[index].ShadeColor = staged ? PendingPlate : plateShade;
            counterPlates[index].Invalidate();
        }

        // Greys out a perk that cannot be selected: its icon, cost and rank counter fade, and the disabled tile supplies the grey face.
        private void SetPerkFaded(int index, bool fade, bool unaffordable)
        {
            if (styled && faded[index] == fade && unaffordableCost[index] == unaffordable)
            {
                return;
            }
            faded[index] = fade;
            unaffordableCost[index] = unaffordable;
            icons[index].ShadeColor = fade ? FadedIcon : LitIcon;
            icons[index].Invalidate();
            costIcons[index].ShadeColor = fade ? GetFadedColor(costShade) : costShade;
            costIcons[index].Invalidate();
            Color price = unaffordable ? UnaffordableCost : ink;
            costs[index].TextColor = fade ? GetFadedColor(price) : price;
            costs[index].Invalidate();
            counts[index].TextColor = fade ? GetFadedColor(ink) : ink;
            counts[index].Invalidate();
        }

        private static Color GetFadedColor(Color color)
        {
            return new Color((color.ARGB & 0x00FFFFFF) | FadedAlpha);
        }

        // The tile supplies its tooltip on hover, so it always reflects the current owned and staged ranks.
        private WindowBase.CreateTooltipCallback TooltipFor(Perk perk)
        {
            return delegate(Vector2 mouse, WindowBase window, ref Vector2 position)
            {
                return BuildTooltip(perk);
            };
        }

        private Tooltip BuildTooltip(Perk perk)
        {
            if (selection == null)
            {
                return null;
            }

            string text = perk.Describe(selection.Tier(perk), selection.SimName);
            string requirement = selection.Requirement(perk);
            if (requirement != null)
            {
                text += "\n" + requirement;
            }
            return PerkTooltip.Create(perk.Name, perk.Tagline, text, ConflictLine(perk));
        }

        // The red line: which perk this one excludes and, once that one is chosen, why this one can no longer be.
        private string ConflictLine(Perk perk)
        {
            Perk other = perk.Conflict;
            if (other == null)
            {
                return null;
            }
            if (selection.Tier(perk) == 0)
            {
                if (selection.Acquired(other) > 0)
                {
                    return JournalText.Format("Arcana.Journal.Perks.Conflict.Acquired", other.Name);
                }
                if (selection.Staged(other) > 0)
                {
                    return JournalText.Format("Arcana.Journal.Perks.Conflict.Selected", other.Name);
                }
            }
            return JournalText.Format("Arcana.Journal.Perks.Conflict.Caption", other.Name);
        }
    }
}
