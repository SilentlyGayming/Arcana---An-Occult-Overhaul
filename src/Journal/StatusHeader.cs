using System;
using System.Collections.Generic;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    // The Skill & Power panel in the journal header; the figures come from the selected Sim's OccultStatusProvider, and humans get no panel.
    internal sealed class StatusHeader
    {
        // Both bars use the occult accent shared with the HUD meter; their values and fills are tracked independently.

        private sealed class Row
        {
            internal readonly StatusField Field = new StatusField();
            internal Window Icon;
            internal ImageDrawable IconImage;
            internal UIImage DefaultIcon;
            internal UIImage ShownIcon;
            internal Text Label;
            internal Text Value;
            // The stock HUD meter bar: a tinted capsule for the track, the same capsule tinted with the occult colour for the fill,
            // and the glass overlay in front of both.
            internal Window Gloss, Fill, FillClip, Track;
            internal BarSparkles Sparkles;
            internal bool Shown;
        }

        private readonly JournalWindow journal;
        private readonly Window panel;
        private readonly Row progress = new Row();
        private readonly Row power = new Row();
        private readonly List<WindowBase> controls = new List<WindowBase>();
        private OccultStatusProvider provider;
        private StatusSource source;
        private float width;
        private int shape = -1;
        private bool visible = true;

        // Returns null when the installed layout predates the header, so the rest of the journal still works.
        internal static StatusHeader Create(JournalWindow journal)
        {
            return journal.HasControl(NativeLayouts.StatusPanel) ? new StatusHeader(journal) : null;
        }

        private StatusHeader(JournalWindow journal)
        {
            this.journal = journal;
            panel = journal.Control<Window>(NativeLayouts.StatusPanel);
            controls.Add(panel);
            Bind(progress, NativeLayouts.StatusProgress);
            Bind(power, NativeLayouts.StatusPower);
            Show(false);
        }

        // The first six IDs are icon, label, value, glass overlay, fill and track; the power row adds a clip and its stock HUD sparkle sprites.
        private void Bind(Row row, uint first)
        {
            row.Icon = journal.Control<Window>(first);
            row.IconImage = row.Icon.Drawable as ImageDrawable;
            row.DefaultIcon = row.IconImage == null ? null : row.IconImage.Image;
            row.ShownIcon = row.DefaultIcon;
            row.Label = journal.Label(first + 1, "", 0, 0, 1, 1);
            row.Value = journal.Label(first + 2, "", 0, 0, 1, 1);
            row.Gloss = journal.Control<Window>(first + 3);
            row.Fill = journal.Control<Window>(first + 4);
            row.FillClip = journal.Control<Window>(first + 6);
            row.FillClip.Clip = true;
            row.Track = journal.Control<Window>(first + 5);
            controls.Add(row.Icon);
            controls.Add(row.Label);
            controls.Add(row.Value);
            controls.Add(row.Gloss);
            controls.Add(row.Track);
            if (journal.HasControl(first + 8))
            {
                row.Sparkles = new BarSparkles(journal, first + 8);
            }
        }

        internal bool Visible { get { return visible; } }
        // The power row as the header last read it, so the Overview shows the same figure; null while the header shows none.
        internal StatusField PowerField { get { return visible && power.Field.Available ? power.Field : null; } }

        // Cheap enough for motive ticks: the controls only change when a value does.
        internal void Refresh(SimIdentity identity, IHudModel model, PowerSelection selection)
        {
            OccultTypes primary = identity == null ? OccultTypes.None : OccultAssetData.Primary(identity.Occults);
            provider = OccultStatusProvider.For(primary);
            if (provider == null)
            {
                source = null;
                Show(false);
                return;
            }

            if (source == null || source.Id != identity.Id || source.Selection != selection)
            {
                source = new StatusSource(identity.Id, model.GetCurrentSimInfo(), selection);
            }
            else
            {
                source.Info = model.GetCurrentSimInfo();
            }

            progress.Field.Clear();
            power.Field.Clear();
            provider.ReadProgress(source, progress.Field);
            provider.ReadPower(source, power.Field);
            SetIcon(progress.Field, primary, false);
            SetIcon(power.Field, primary, true);
            if (!progress.Field.Available && !power.Field.Available)
            {
                Show(false);
                return;
            }

            Show(true);
            int rows = (progress.Field.Available ? 1 : 0) + (power.Field.Available ? 2 : 0);
            if (rows != shape)
            {
                shape = rows;
                Layout(width);
            }
            uint tint = OccultAssetData.BarTint(primary);
            Apply(progress, tint);
            Apply(power, tint);
        }

        private static void SetIcon(StatusField field, OccultTypes type, bool powerRow)
        {
            if (!field.Available)
            {
                return;
            }
            ulong instance = OccultAssetData.StatusIcon(type, powerRow);
            if (instance == 0ul)
            {
                return;
            }
            UIImage icon = StatusReaders.StatusIcon(instance);
            if (icon != null)
            {
                field.Icon = icon;
            }
        }

        // Places the panel for a journal of the given width; JournalWindow.Resize then calls RefreshBars so the fills follow their tracks.
        internal void Layout(float windowWidth)
        {
            width = windowWidth;
            if (!visible)
            {
                return;
            }

            int row = 0;
            PlaceRow(progress, progress.Field.Available ? row++ : -1);
            PlaceRow(power, power.Field.Available ? row++ : -1);
            float height = HeaderLayout.FirstRowCenter * 2f + Math.Max(0, row - 1) * HeaderLayout.RowPitch;
            journal.Place(panel, HeaderLayout.PanelLeft(width), HeaderLayout.PanelTop, HeaderLayout.PanelWidth, height);
        }

        private void PlaceRow(Row row, int index)
        {
            if (index < 0)
            {
                return;
            }

            float center = HeaderLayout.RowCenter(index);
            journal.Place(row.Icon, HeaderLayout.IconLeft(width), center - HeaderLayout.IconSize / 2f,
                HeaderLayout.IconSize, HeaderLayout.IconSize);
            journal.Place(row.Label, HeaderLayout.LabelLeft(width), center - HeaderLayout.RowHeight / 2f,
                HeaderLayout.LabelWidth, HeaderLayout.RowHeight);
            journal.Place(row.Value, HeaderLayout.ValueLeft(width), center - HeaderLayout.RowHeight / 2f,
                HeaderLayout.ValueWidth, HeaderLayout.RowHeight);

            float x = HeaderLayout.BarLeft(width);
            float y = center - HeaderLayout.BarHeight / 2f;
            journal.Place(row.Track, x, y, HeaderLayout.BarWidth, HeaderLayout.BarHeight);
            journal.Place(row.Gloss, x, y, HeaderLayout.BarWidth, HeaderLayout.BarHeight);

            // The Journal scales control rectangles manually. Match the
            // native nine-slice caps to that scale as well for all three layers.
            SetSliceScale(row.Track, journal.LayoutScale);
            SetSliceScale(row.Fill, journal.LayoutScale);
            SetSliceScale(row.Gloss, journal.LayoutScale);
        }

        private static void SetSliceScale(Window window, float scale)
        {
            StdDrawable drawable = window.Drawable as StdDrawable;
            if (drawable != null)
            {
                drawable.ScaleAmount = new Vector2(scale, scale);
            }
        }

        // Sizes each fill inside its (already scaled) track, so call it after the placements have been scaled.
        internal void RefreshBars()
        {
            FillBar(progress);
            FillBar(power);
        }

        private void FillBar(Row row)
        {
            if (!visible || !row.Field.Available)
            {
                HideFill(row);
                return;
            }

            HeaderMeterBounds bounds = new HeaderMeterBounds(row.Track.Area, row.Field.Fraction);
            if (bounds.Fill.Width <= 0f || bounds.Fill.Height <= 0f)
            {
                HideFill(row);
                return;
            }

            row.FillClip.Area = bounds.Fill;
            row.Fill.Area = bounds.FillArt;
            row.FillClip.Visible = true;
            if (row.Sparkles != null)
            {
                row.Sparkles.SetFill(bounds);
            }
        }

        private static void HideFill(Row row)
        {
            row.FillClip.Visible = false;
            if (row.Sparkles != null)
            {
                row.Sparkles.Stop();
            }
        }

        internal void StopAnimations()
        {
            if (power.Sparkles != null)
            {
                power.Sparkles.Stop();
            }
        }

        private void Apply(Row row, uint fill)
        {
            bool show = row.Field.Available;
            if (row.Shown != show)
            {
                row.Shown = show;
                row.Label.Visible = show;
                row.Value.Visible = show;
                row.Gloss.Visible = show;
                row.Track.Visible = show;
            }
            if (!show)
            {
                row.Icon.Visible = false;
                HideFill(row);
                return;
            }

            if (row.Label.Caption != row.Field.Label)
            {
                row.Label.Caption = row.Field.Label;
            }
            if (row.Value.Caption != row.Field.Caption)
            {
                row.Value.Caption = row.Field.Caption;
            }
            if (row.Fill.ShadeColor.ARGB != fill)
            {
                row.Fill.ShadeColor = new Color(fill);
                row.Fill.Invalidate();
            }

            UIImage icon = row.Field.Icon ?? row.DefaultIcon;
            if (icon != row.ShownIcon && row.IconImage != null)
            {
                row.ShownIcon = icon;
                row.IconImage.Image = icon;
                row.Icon.Invalidate();
            }
            row.Icon.Visible = icon != null;
            FillBar(row);
        }

        private void Show(bool show)
        {
            if (visible == show)
            {
                return;
            }

            visible = show;
            foreach (WindowBase control in controls)
            {
                control.Visible = show;
            }
            // Every control now has the panel's visibility, so Apply can hide the rows that have nothing to show.
            progress.Shown = show;
            power.Shown = show;
            if (!show)
            {
                shape = -1;
                HideFill(progress);
                HideFill(power);
            }
        }
    }
}
