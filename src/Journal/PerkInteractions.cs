using Sims3.SimIFace;
using Sims3.UI;
using System;

namespace SupernaturalExpanded.Journal
{
    // The perk tooltip (title, blue tagline, description, red conflict line) in the mod's own layout, sized like the stock SimpleTextTooltip: each Text is fitted to its wrapped height and the window is sized around them.
    internal sealed class PerkTooltip : Tooltip
    {
        private const uint TaglineColor = 0xFF2D8CD8;
        private const uint ConflictColor = 0xFFD03030;

        // In UI units, before the UI scale; the stock tooltips wrap at 180, which is too narrow for these descriptions.
        private const float TextWidth = 320f;
        private const float Padding = 10f;
        private const float TaglineGap = 2f;
        private const float BodyGap = 6f;
        private const float ConflictGap = 4f;

        private const uint TitleId = 0x53E00081;
        private const uint TaglineId = 0x53E00082;
        private const uint BodyId = 0x53E00083;
        private const uint ConflictId = 0x53E00084;

        private PerkTooltip()
            : base(NativeLayouts.PerkTooltip, NativeLayouts.RootExport)
        {
        }

        // Never shows a half-built tooltip: a failure falls back to the stock plain one, which is always readable.
        internal static Tooltip Create(string title, string tagline, string description, string conflict)
        {
            PerkTooltip tooltip = null;
            try
            {
                tooltip = new PerkTooltip();
                if (tooltip.Fill(title, tagline, description, conflict))
                {
                    return tooltip;
                }
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Building the perk tooltip", error);
            }

            Discard(tooltip);
            return Plain(title + "\n" + tagline + "\n\n" + description
                + (string.IsNullOrEmpty(conflict) ? "" : "\n" + conflict));
        }

        // Returns false when the layout did not load with all four text controls.
        private bool Fill(string title, string tagline, string description, string conflict)
        {
            Window root = TooltipWindow as Window;
            if (root == null)
            {
                return false;
            }

            Text name = root.GetChildByID(TitleId, true) as Text;
            Text line = root.GetChildByID(TaglineId, true) as Text;
            Text body = root.GetChildByID(BodyId, true) as Text;
            Text warning = root.GetChildByID(ConflictId, true) as Text;
            if (name == null || line == null || body == null || warning == null)
            {
                return false;
            }

            JournalTheme.ApplyTooltip(root, name, line, body);
            line.TextColor = new Color(TaglineColor);
            warning.TextStyle = body.TextStyle;
            warning.TextColor = new Color(ConflictColor);

            float scale = NativeLayouts.UIScale();
            float width = TextWidth * scale;
            float left = Padding * scale;
            float top = left;
            top += Place(name, title, left, top, width, scale) + TaglineGap * scale;
            top += Place(line, tagline, left, top, width, scale) + BodyGap * scale;
            top += Place(body, description, left, top, width, scale);
            if (!string.IsNullOrEmpty(conflict))
            {
                top += ConflictGap * scale;
                top += Place(warning, conflict, left, top, width, scale);
            }
            else
            {
                warning.Visible = false;
            }
            top += Padding * scale;

            root.Area = new Rect(0f, 0f, left + width + left, top);
            return true;
        }

        // Sets the caption, fits the Text's height to its wrapped text at the given width, and returns that height.
        private float Place(Text text, string caption, float left, float top, float width, float scale)
        {
            text.Caption = caption;
            text.Area = new Rect(left, top, left + width, top + 10f);
            text.AutoSize(true);
            float height = text.Area.Height;
            if (!(height >= 1f) || height > 4000f)
            {
                height = Estimate(caption, width, scale);
            }
            text.Area = new Rect(left, top, left + width, top + height);
            return height;
        }

        // Only used if AutoSize does not report a usable height: a rough line count from the text length.
        private static float Estimate(string caption, float width, float scale)
        {
            float perLine = Math.Max(1f, width / (6.5f * scale));
            int lines = Math.Max(1, (int)Math.Ceiling(caption.Length / perLine));
            return lines * 16f * scale;
        }

        // The stock plain tooltip, at the same width where the game allows it.
        private static Tooltip Plain(string text)
        {
            try
            {
                return new SimpleTextTooltip(text, false, "SimpleTextTooltip", 1, (int)(TextWidth * NativeLayouts.UIScale()));
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Creating the wide plain tooltip", error);
                return new SimpleTextTooltip(text);
            }
        }

        private static void Discard(PerkTooltip tooltip)
        {
            if (tooltip == null)
            {
                return;
            }
            try
            {
                tooltip.Dispose();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Disposing a partly built perk tooltip", error);
            }
        }
    }

    internal enum PerkPurchaseChoice
    {
        Dismissed,
        Yes,
        No
    }

    // Reuse the game's two-button layout, focus, keyboard navigation and modal
    // teardown. TwoButtonDialog.Show merges Escape and No into the same bool.
    internal sealed class PerkPurchaseDialog : TwoButtonDialog
    {
        private const uint DismissID = uint.MaxValue;

        private readonly Action<PerkPurchaseChoice> completed;
        private WindowBase observedWindow;
        private bool started;
        private bool ended;

        internal PerkPurchaseDialog(Action<PerkPurchaseChoice> completed)
            : base(JournalText.Get("Arcana.Journal.Perks.Confirmation.Message"),
                JournalText.Get("Arcana.Common.Yes"), JournalText.Get("Arcana.Common.No"), true, true, new Vector2(-2f, -2f), true)
        {
            this.completed = completed;
        }

        internal bool Open()
        {
            Dialog window = ModalDialogWindow as Dialog;
            if (window == null)
            {
                return false;
            }

            window.TitleText = string.Empty;
            window.ShowTitle = false;
            window.ShowTitleBar = false;
            NativeUi.DisableDialogPause(this);
            NativeUi.WatchShutdown(window, OnWindowShutdown, true);
            observedWindow = window;
            started = true;
            StartModal(false);
            return UIManager.GetModalWindow() == window;
        }

        public override void OnTriggerCancel()
        {
            EndDialog(DismissID);
        }

        protected override void EndDialog(uint endID)
        {
            if (ended)
            {
                return;
            }

            ended = true;
            PerkPurchaseChoice choice = endID == OkayID ? PerkPurchaseChoice.Yes
                : endID == CancelID ? PerkPurchaseChoice.No : PerkPurchaseChoice.Dismissed;
            bool closed = false;
            try
            {
                try
                {
                    StopWatchingWindow();
                }
                finally
                {
                    base.EndDialog(endID);
                }
                closed = true;
            }
            finally
            {
                completed(closed ? choice : PerkPurchaseChoice.Dismissed);
            }
        }

        private void OnWindowShutdown(WindowBase sender, UIEventArgs args)
        {
            observedWindow = null;
            if (!ended)
            {
                ended = true;
                completed(PerkPurchaseChoice.Dismissed);
            }
        }

        private void StopWatchingWindow()
        {
            WindowBase window = observedWindow;
            observedWindow = null;
            if (window != null && !window.Disposed)
            {
                NativeUi.WatchShutdown(window, OnWindowShutdown, false);
            }
        }

        internal void Dismiss()
        {
            if (ended)
            {
                return;
            }

            if (started && ModalDialogWindow != null && !ModalDialogWindow.Disposed)
            {
                EndDialog(DismissID);
            }
            else
            {
                try
                {
                    StopWatchingWindow();
                }
                finally
                {
                    Dispose();
                }
            }
        }
    }

    // The feedback for staging a perk: the sparkle burst, plus one sound for clicks that come close together.
    internal sealed class PerkFeedback
    {
        // Clicks closer together than this share one sound.
        private const int SoundGapMilliseconds = 120;

        private readonly SparkleBurst sparkles;
        private int lastSound;
        private bool soundPlayed;

        internal PerkFeedback(JournalWindow journal, Window overlay)
        {
            sparkles = new SparkleBurst(journal, overlay);
        }

        internal void Play(Button tile)
        {
            sparkles.Play(tile);
            PlaySound();
        }

        internal void Hide()
        {
            sparkles.Hide();
        }

        internal void Release()
        {
            sparkles.Release();
        }

        private void PlaySound()
        {
            int now = System.Environment.TickCount;
            if (soundPlayed && now - lastSound < SoundGapMilliseconds)
            {
                return;
            }

            soundPlayed = true;
            lastSound = now;
            JournalSounds.Play(JournalSounds.Staged);
        }
    }
}
