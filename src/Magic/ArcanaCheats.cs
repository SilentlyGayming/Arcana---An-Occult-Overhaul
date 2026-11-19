using System;
using Sims3.Gameplay.Actors;
using Sims3.SimIFace;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal static class ArcanaCheats
    {
        internal const string UnlockCommand = "arcanaunlockallspells";
        private static readonly SpellUnlockCommand Command = new SpellUnlockCommand(UnlockSelected, ShowUsage);
        private static readonly CommandHandler Handler = new CommandHandler(Command.Execute);
        private static bool registered;
        private static bool active;

        internal static void Start()
        {
            // Registers the unlock cheat now and checks it again once world startup finishes.
            active = true;
            VerifyRegistration();
            try { JournalBootstrap.RunLater(VerifyRegistration); }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Scheduling the cheat registration check", error); }
        }

        private static void VerifyRegistration()
        {
            if (!active) return;
            try
            {
                if (!Register(JournalText.Get("Arcana.Cheat.UnlockAll.Help")))
                    JournalBootstrap.ReportOnce("Registering the spell unlock cheat",
                        new InvalidOperationException("The native console cannot find " + UnlockCommand));
            }
            catch (ResetException) { throw; }
            catch (Exception error) { JournalBootstrap.ReportOnce("Registering the spell unlock cheat", error); }
        }

        internal static bool Register(string description)
        {
            // Registers the cheat and confirms the console can find its help text.
            if (!CommandSystem.Exists()) return false;
            if (registered && !string.IsNullOrEmpty(CommandSystem.GetCommandHelp(UnlockCommand))) return true;
            registered = CommandSystem.RegisterCommand(UnlockCommand, description, Handler);
            return registered && !string.IsNullOrEmpty(CommandSystem.GetCommandHelp(UnlockCommand));
        }

        internal static void Stop()
        {
            active = false;
            if (!registered) return;
            CommandSystem.UnregisterCommand(UnlockCommand);
            registered = false;
        }

        private static void ShowUsage()
        {
            SimpleMessageDialog.Show(JournalText.Get("Arcana.Journal.Title"), JournalText.Get("Arcana.Cheat.UnlockAll.Help"));
        }

        private static int UnlockSelected()
        {
            // Unlocks every ordinary spell for the selected Witch directly in the command callback.
            try
            {
                Sim selected = Sim.ActiveActor;
                if (selected == null || selected.HasBeenDestroyed || !WitchCastingContext.IsWitch(selected))
                {
                    SimpleMessageDialog.Show(JournalText.Get("Arcana.Journal.Title"), JournalText.Get("Arcana.Cheat.SelectWitch"));
                    return -1;
                }
                int learned = SpellKnowledge.UnlockAllOrdinary(selected);
                if (learned < 0)
                {
                    SpellKnowledge.Notify(selected, "Arcana.Cheat.CannotSave");
                    return -1;
                }
                SpellKnowledge.Notify(selected, "Arcana.Cheat.UnlockAll.Success", selected.FullName, learned);
                return 1;
            }
            catch (ResetException) { throw; }
            catch (Exception error)
            {
                JournalBootstrap.Report("Unlocking all ordinary spells", error);
                SimpleMessageDialog.Show(JournalText.Get("Arcana.Journal.Title"), JournalText.Get("Arcana.Cheat.CannotSave"));
                return -1;
            }
        }
    }

    internal sealed class SpellUnlockCommand
    {
        internal delegate int UnlockSelectedSpells();
        internal delegate void ShowSpellUnlockUsage();

        private readonly UnlockSelectedSpells unlock;
        private readonly ShowSpellUnlockUsage usage;

        internal SpellUnlockCommand(UnlockSelectedSpells unlock, ShowSpellUnlockUsage usage)
        {
            this.unlock = unlock;
            this.usage = usage;
        }

        internal int Execute(object[] arguments)
        {
            // Runs the unlock, or shows usage when the console passes arguments.
            if (arguments != null && arguments.Length != 0)
            {
                usage();
                return -1;
            }
            return unlock();
        }
    }
}
