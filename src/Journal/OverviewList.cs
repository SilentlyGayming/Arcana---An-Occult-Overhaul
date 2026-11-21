using System;
using System.Collections.Generic;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class OverviewList
    {
        internal const float RowHeight = 42f;
        private const float IconSize = 30f;
        private const float FillShare = 0.88f;
        private const float MaxRowHeight = 84f;
        private const float MaxIconSize = 56f;
        private const float IconShare = 0.66f;
        private const float MinCompactRow = 30f;
        private const float CompactIconShare = 0.7f;
        private const float MinCompactIcon = 20f;
        private const int RowControls = 6;
        private const float Padding = 14f;
        private const float EdgePadding = 22f;
        private const float IconGap = 10f;
        private const float ScrollWidth = 15f;
        private const float ArrowHeight = 15f;
        private const float StripInset = 3f;
        private const float StripGap = 4f;
        private const int WheelRows = 1;

        private readonly JournalWindow journal;
        private readonly int capacity;
        private readonly bool fill;
        private readonly bool compact;
        private readonly float detailWidth;
        private readonly float valueWidth;
        private readonly Window[] icons;
        private readonly ImageDrawable[] iconImages;
        private readonly Text[] names;
        private readonly Text[] details;
        private readonly Text[] values;
        private readonly Window[] rules;
        private readonly Window[] hits;
        private readonly int[] hitRows;
        private readonly Window track;
        private readonly Button thumb;
        private readonly Button up;
        private readonly Button down;
        private readonly Text empty;
        private readonly List<OverviewRow> rows = new List<OverviewRow>();
        private bool visible;
        private bool barShown;
        private bool reviveQueued;
        private int first;
        private int slots;
        private float rowHeight = RowHeight;
        private float iconSize = IconSize;
        private float rowsTop;
        private float listHeight;
        private float listLeft;
        private float listTop;
        private float listWidth;
        private float scrollLeft;
        private float scrollTop;
        private float trackHeight;
        private float thumbSize;
        private float travel;
        private bool dragging;
        private float dragOffset;

        internal OverviewList(JournalWindow journal, uint firstId, int capacity, Window body, string emptyMessage,
            float detailWidth, float valueWidth, bool fill, bool compact)
        {
            this.journal = journal;
            this.capacity = capacity;
            this.fill = fill;
            this.compact = compact;
            slots = capacity;
            this.detailWidth = detailWidth;
            this.valueWidth = valueWidth;
            icons = new Window[capacity];
            iconImages = new ImageDrawable[capacity];
            names = new Text[capacity];
            details = new Text[capacity];
            values = new Text[capacity];
            rules = new Window[capacity];
            hits = new Window[capacity];
            hitRows = new int[capacity];
            for (int i = 0; i < capacity; i++)
            {
                uint id = firstId + (uint)(i * RowControls);
                icons[i] = journal.Control<Window>(id);
                iconImages[i] = icons[i].Drawable as ImageDrawable;
                names[i] = journal.Label(id + 1, "", 0, 0, 100, RowHeight);
                details[i] = journal.Label(id + 2, "", 0, 0, 100, RowHeight);
                values[i] = journal.Label(id + 3, "", 0, 0, 100, RowHeight);
                rules[i] = journal.Control<Window>(id + 4);
                icons[i].IgnoreMouse = true;
                names[i].IgnoreMouse = true;
                details[i].IgnoreMouse = true;
                values[i].IgnoreMouse = true;
                rules[i].IgnoreMouse = true;
                hits[i] = journal.Control<Window>(id + 5);
                hits[i].IgnoreMouse = false;
                hits[i].MouseWheel += OnWheel;
                hits[i].CreateTooltipCallbackFunction = TooltipFor(i);
                hits[i].Visible = false;
                hitRows[i] = -1;
            }

            uint parts = firstId + NativeLayouts.OverviewScrollOffset;
            track = journal.Control<Window>(parts);
            track.IgnoreMouse = false;
            thumb = journal.Button(parts + 1, "", 0, 0, ScrollWidth, 28, delegate {  });
            up = journal.Button(parts + 2, "", 0, 0, ScrollWidth, ArrowHeight, delegate { ScrollBy(-1); });
            down = journal.Button(parts + 3, "", 0, 0, ScrollWidth, ArrowHeight, delegate { ScrollBy(1); });
            empty = journal.Label(parts + 4, emptyMessage, 0, 0, 100, 40);

            body.MouseWheel += OnWheel;
            track.MouseWheel += OnWheel;
            thumb.MouseWheel += OnWheel;
            up.MouseWheel += OnWheel;
            down.MouseWheel += OnWheel;
            track.MouseDown += OnTrackDown;
            thumb.MouseDown += OnThumbDown;
            thumb.MouseMove += OnThumbMove;
            thumb.MouseUp += OnThumbUp;
            journal.RootWindow.MouseUp += OnThumbUp;
            Show(false);
        }

        internal void SetRows(List<OverviewRow> incoming)
        {
            if (HasSameRows(incoming))
            {
                return;
            }
            rows.Clear();
            if (incoming != null)
            {
                rows.AddRange(incoming);
            }
            Refresh();
        }

        internal void Show(bool show)
        {
            visible = show;
            Refresh();
        }

        private bool HasSameRows(List<OverviewRow> incoming)
        {
            int count = incoming == null ? 0 : incoming.Count;
            if (count != rows.Count)
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                OverviewRow a = rows[i];
                OverviewRow b = incoming[i];
                if (a.Icon != b.Icon || a.Name != b.Name || a.Detail != b.Detail || a.Value != b.Value || !OverviewTip.Same(a.Tip, b.Tip))
                {
                    return false;
                }
            }
            return true;
        }

        internal void Layout(float left, float top, float width, float height)
        {
            listLeft = left;
            listTop = top;
            listWidth = width;
            listHeight = height;
            Fit();
            PlaceRows();

            scrollLeft = left + width - ScrollWidth - StripInset;
            scrollTop = top + StripInset;
            trackHeight = height - 2f * StripInset - 2f * ArrowHeight;
            journal.Place(up, scrollLeft, scrollTop, ScrollWidth, ArrowHeight);
            journal.Place(track, scrollLeft, scrollTop + ArrowHeight, ScrollWidth, trackHeight);
            journal.Place(down, scrollLeft, scrollTop + ArrowHeight + trackHeight, ScrollWidth, ArrowHeight);
            journal.Place(empty, left, top, width, height);
            Refresh();
        }

        private void Fit()
        {
            float nextRow = RowHeight;
            float nextIcon = IconSize;
            int nextSlots = capacity;
            if (listHeight > 0f)
            {
                if (fill && rows.Count > 0)
                {
                    nextRow = Math.Max(RowHeight, Math.Min(MaxRowHeight, (float)Math.Floor(listHeight * FillShare / rows.Count)));
                    nextIcon = Math.Max(IconSize, Math.Min(MaxIconSize, (float)Math.Floor(nextRow * IconShare)));
                }
                else if (compact && rows.Count > (int)Math.Floor(listHeight / RowHeight + 0.001f))
                {
                    nextRow = Math.Max(MinCompactRow, (float)Math.Floor(listHeight / rows.Count));
                    nextIcon = Math.Max(MinCompactIcon, Math.Min(IconSize, (float)Math.Floor(nextRow * CompactIconShare)));
                }
                nextSlots = Math.Max(1, Math.Min(capacity, (int)Math.Floor(listHeight / nextRow + 0.001f)));
            }

            float nextTop = 0f;
            if (fill && listHeight > 0f && rows.Count > 0 && rows.Count <= nextSlots)
            {
                nextTop = (float)Math.Floor((listHeight - rows.Count * nextRow) / 2f);
            }

            if (nextRow != rowHeight || nextIcon != iconSize || nextSlots != slots || nextTop != rowsTop)
            {
                rowHeight = nextRow;
                iconSize = nextIcon;
                slots = nextSlots;
                rowsTop = nextTop;
                PlaceRows();
            }
        }

        private void PlaceRows()
        {
            if (listWidth <= 0f)
            {
                return;
            }
            float rowWidth = barShown ? listWidth - ScrollWidth - StripInset - StripGap : listWidth;
            float ruleInset = fill ? 0f : Padding;
            float textLeft = listLeft + Padding + iconSize + IconGap;
            float valueLeft = listLeft + rowWidth - (barShown ? Padding : EdgePadding) - valueWidth;
            float detailLeft = valueLeft - detailWidth;
            float nameWidth = Math.Max(1f, detailLeft - textLeft - IconGap);
            for (int i = 0; i < capacity; i++)
            {
                float y = listTop + rowsTop + i * rowHeight;
                journal.Place(icons[i], listLeft + Padding, y + (float)Math.Floor((rowHeight - iconSize) / 2f), iconSize, iconSize);
                journal.Place(names[i], textLeft, y, nameWidth, rowHeight);
                journal.Place(details[i], detailLeft, y, detailWidth, rowHeight);
                journal.Place(values[i], valueLeft, y, valueWidth, rowHeight);
                journal.Place(rules[i], listLeft + ruleInset, y + rowHeight - 1f, rowWidth - 2f * ruleInset, 1f);
                journal.Place(hits[i], listLeft, y, rowWidth, rowHeight);
            }
        }

        internal void ApplyInk(Color ink, Color accent, Color muted)
        {
            for (int i = 0; i < capacity; i++)
            {
                names[i].TextColor = ink;
                values[i].TextColor = ink;
                details[i].TextColor = accent;
            }
            empty.TextColor = muted;
        }

        private int GetMaxScroll()
        {
            return Math.Max(0, rows.Count - slots);
        }

        private void Refresh()
        {
            Fit();
            first = Math.Max(0, Math.Min(first, GetMaxScroll()));
            int count = rows.Count;
            empty.Visible = visible && count == 0;
            int lastRule = fill ? Math.Min(slots, count - first) - 1 : slots - 1;
            bool hitsChanged = false;
            for (int i = 0; i < capacity; i++)
            {
                int index = first + i;
                bool show = visible && i < slots && index < count;
                names[i].Visible = show;
                details[i].Visible = show;
                values[i].Visible = show;
                rules[i].Visible = show && i < lastRule;
                hitsChanged |= UpdateHit(i, show ? index : -1);
                if (!show)
                {
                    icons[i].Visible = false;
                    continue;
                }

                OverviewRow row = rows[index];
                SetCaption(names[i], row.Name);
                SetCaption(details[i], row.Detail);
                SetCaption(values[i], row.Value);
                if (iconImages[i] != null && iconImages[i].Image != row.Icon)
                {
                    iconImages[i].Image = row.Icon;
                    icons[i].Invalidate();
                }
                icons[i].Visible = row.Icon != null;
            }
            RefreshScrollbar(visible && count > 0);
            if (hitsChanged)
            {
                QueueRevive();
            }
        }

        private bool UpdateHit(int slot, int index)
        {
            int target = index >= 0 && rows[index].Tip != null ? index : -1;
            if (hitRows[slot] == target)
            {
                return false;
            }
            hitRows[slot] = target;
            hits[slot].Visible = false;
            return target >= 0;
        }

        private void QueueRevive()
        {
            if (reviveQueued)
            {
                return;
            }
            reviveQueued = true;
            JournalBootstrap.RunLater(delegate
            {
                reviveQueued = false;
                for (int i = 0; i < capacity; i++)
                {
                    if (!hits[i].Disposed)
                    {
                        hits[i].Visible = visible && hitRows[i] >= 0;
                    }
                }
            });
        }

        private WindowBase.CreateTooltipCallback TooltipFor(int slot)
        {
            return delegate(Vector2 mouse, WindowBase window, ref Vector2 position)
            {
                int index = hitRows[slot];
                OverviewTip tip = index >= 0 && index < rows.Count ? rows[index].Tip : null;
                return tip == null ? null : PerkTooltip.Create(tip.Title, tip.Tagline, tip.Body, null, tip.Modifiers);
            };
        }

        internal static void SetCaption(Text text, string caption)
        {
            if (text.Caption != caption)
            {
                text.Caption = caption;
            }
        }

        internal static void Reflow(Text text)
        {
            string caption = text.Caption;
            if (!string.IsNullOrEmpty(caption))
            {
                text.Caption = "";
                text.Caption = caption;
            }
        }

        private void RefreshScrollbar(bool shown)
        {
            int max = GetMaxScroll();
            bool scrollable = shown && max > 0;
            if (scrollable != barShown)
            {
                barShown = scrollable;
                PlaceRows();
            }
            track.Visible = scrollable;
            up.Visible = scrollable;
            down.Visible = scrollable;
            thumb.Visible = scrollable;
            if (!scrollable)
            {
                EndDrag();
                return;
            }

            up.Enabled = first > 0;
            down.Enabled = first < max;
            JournalTheme.TintArrow(up, up.Enabled);
            JournalTheme.TintArrow(down, down.Enabled);

            thumbSize = Math.Max(24f, trackHeight * slots / (float)rows.Count);
            travel = Math.Max(1f, trackHeight - thumbSize);
            journal.Place(thumb, scrollLeft, GetThumbTop(max), ScrollWidth, thumbSize);
        }

        private float GetThumbTop(int max)
        {
            return scrollTop + ArrowHeight + travel * first / (float)Math.Max(1, max);
        }

        private void ScrollBy(int delta)
        {
            int target = Math.Max(0, Math.Min(GetMaxScroll(), first + delta));
            if (target != first)
            {
                first = target;
                Refresh();
            }
        }

        private void OnWheel(WindowBase sender, UIMouseEventArgs args)
        {
            int delta = args.MouseWheelDelta;
            if (!visible || dragging || delta == 0 || GetMaxScroll() == 0)
            {
                return;
            }
            ScrollBy(delta > 0 ? -WheelRows : WheelRows);
            args.Handled = true;
        }

        private void OnTrackDown(WindowBase sender, UIMouseEventArgs args)
        {
            float y;
            if (!visible || dragging || GetMaxScroll() == 0 || !journal.TryGetCursorY(out y))
            {
                return;
            }
            ScrollBy(y < GetThumbTop(GetMaxScroll()) ? -slots : slots);
            args.Handled = true;
        }

        private void OnThumbDown(WindowBase sender, UIMouseEventArgs args)
        {
            float y;
            if (!visible || !journal.TryGetCursorY(out y))
            {
                return;
            }
            dragging = true;
            dragOffset = y - GetThumbTop(GetMaxScroll());
            UIManager.SetCaptureTarget(InputContext.kICMouse, thumb);
            args.Handled = true;
        }

        private void OnThumbMove(WindowBase sender, UIMouseEventArgs args)
        {
            float y;
            if (!dragging)
            {
                return;
            }
            if (!visible || UIManager.GetCaptureTarget(InputContext.kICMouse) != thumb)
            {
                dragging = false;
                return;
            }
            if (!journal.TryGetCursorY(out y))
            {
                return;
            }

            int max = GetMaxScroll();
            float fraction = (y - dragOffset - scrollTop - ArrowHeight) / travel;
            int target = Math.Max(0, Math.Min(max, (int)Math.Round(fraction * max)));
            if (target != first)
            {
                first = target;
                Refresh();
            }
            args.Handled = true;
        }

        private void OnThumbUp(WindowBase sender, UIMouseEventArgs args)
        {
            EndDrag();
        }

        private void EndDrag()
        {
            if (!dragging)
            {
                return;
            }
            dragging = false;
            if (thumb != null && !thumb.Disposed)
            {
                UIManager.ReleaseCapture(InputContext.kICMouse, thumb);
            }
        }

        internal void Release()
        {
            EndDrag();
        }
    }
}
