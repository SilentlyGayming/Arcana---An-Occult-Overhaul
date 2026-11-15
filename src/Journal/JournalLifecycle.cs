using Sims3.SimIFace;
using Sims3.UI;
using System;

namespace SupernaturalExpanded.Journal
{
    // Makes the Journal modal the way the game's own journals are. ModalDialog does this with the same UIManager calls, in the
    // same order, but holds the calling task until the dialog closes; the Journal returns at once, so the steps are kept here.
    // End undoes whatever Begin reached, in the order StopModal uses, and can be called any number of times.
    internal sealed class JournalModal
    {
        // The key map and priority ModalDialog hooks onto a window-modal dialog.
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

        // True while a different modal window is above the Journal, so that its keys are not ours.
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

        // The dim layer is counted; the last modal to leave clears the count, as the game's dialogs do.
        private void Undim()
        {
            if (dimmed)
            {
                dimmed = false;
                UIManager.DarkenBackground(false, UIManager.GetModalWindow() == null);
            }
        }

        // The game keeps the speed it had and puts it back on unlock, so a game the player had paused stays paused.
        // A speed some other system has locked is left to that system.
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

    // The game's own interface sounds, started by name the way its dialogs start them. The engine applies the player's audio settings.
    internal static class JournalSounds
    {
        // Added to the cart: the Lifetime Rewards shop plays it when an item is picked.
        internal const string Staged = "ui_cart_add_to";
        // Every native modal dialog plays this when it closes.
        internal const string Close = "ui_hardwindow_close";
        // The Lifetime Rewards shop swaps its close sound for this one when something was bought.
        internal const string Purchase = "ui_lifetime_reward_buy";
        // The native dialogs play this when OK or Cancel is pressed from the keyboard.
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
