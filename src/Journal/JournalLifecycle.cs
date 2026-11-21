using System;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class JournalModal
    {
        private const string TriggerMap = "OKCancelDialog";
        private const int WindowModalPriority = 18;

        private readonly Dialog window;
        private WindowBase previousFocus;
        private uint hook;
        private bool dimmed;
        private bool modal;
        private bool paused;
        private bool ended;

        internal JournalModal(Dialog window)
        {
            this.window = window;
        }

        internal bool Covered
        {
            get
            {
                WindowBase top = UIManager.GetModalWindow();
                return top != null && top != window;
            }
        }

        internal void Begin()
        {
            UIManager.DarkenBackground(true);
            dimmed = true;
            window.Visible = true;
            previousFocus = UIManager.GetFocus(InputContext.kICKeyboard);
            UIManager.SetFocus(InputContext.kICKeyboard, window);
            hook = window.AddModalTriggerHook(TriggerMap, TriggerActivationMode.kPermanent, WindowModalPriority);
            modal = UIManager.BeginModal(window);
            Pause();
        }

        internal void End()
        {
            if (ended)
            {
                return;
            }
            ended = true;

            ResetException reset = null;
            JournalAction[] steps = { RemoveHook, EndModal, Undim, Resume, RestoreFocus };
            foreach (JournalAction step in steps)
            {
                try
                {
                    step();
                }
                catch (ResetException error)
                {
                    reset = error;
                }
                catch (Exception error)
                {
                    JournalBootstrap.Report("Ending the Supernatural Journal modal state", error);
                }
            }
            if (reset != null)
            {
                throw reset;
            }
        }

        private void RemoveHook()
        {
            if (hook != 0 && !window.Disposed)
            {
                window.RemoveTriggerHook(hook);
            }
            hook = 0;
        }

        private void EndModal()
        {
            if (modal)
            {
                modal = false;
                if (!window.Disposed)
                {
                    UIManager.EndModal(window);
                }
            }
        }

        private void Undim()
        {
            if (dimmed)
            {
                dimmed = false;
                UIManager.DarkenBackground(false, UIManager.GetModalWindow() == null);
            }
        }

        private void Pause()
        {
            Sims3.UI.IResponder responder = Sims3.UI.Responder.Instance;
            ISimClockSpeedModel clock = responder == null ? null : responder.ClockSpeedModel;
            if (clock == null || clock.GameSpeedLocked)
            {
                return;
            }
            clock.LockGameSpeed(Sims3.SimIFace.Gameflow.GameSpeed.Pause);
            paused = true;
        }

        private void Resume()
        {
            if (!paused)
            {
                return;
            }
            paused = false;
            Sims3.UI.IResponder responder = Sims3.UI.Responder.Instance;
            ISimClockSpeedModel clock = responder == null ? null : responder.ClockSpeedModel;
            if (clock != null)
            {
                clock.UnlockGameSpeed();
            }
        }

        private void RestoreFocus()
        {
            WindowBase focus = previousFocus;
            previousFocus = null;
            if (focus != null && !focus.Disposed)
            {
                UIManager.SetFocus(InputContext.kICKeyboard, focus);
            }
        }
    }

    internal static class JournalSounds
    {
        internal const string Staged = "ui_cart_add_to";
        internal const string Close = "ui_hardwindow_close";
        internal const string Purchase = "ui_lifetime_reward_buy";
        internal const string Key = "ui_secondary_button";

        internal static void Play(string sound)
        {
            try
            {
                Audio.StartSound(sound);
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Playing the interface sound " + sound, error);
            }
        }
    }
}
