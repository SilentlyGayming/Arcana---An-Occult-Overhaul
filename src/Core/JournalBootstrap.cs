using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;

[assembly: Tunable]
[assembly: PersistableStatic]
[assembly: AssemblyVersion("0.8.8.0")]

namespace SupernaturalExpanded.Journal
{
    public static class JournalBootstrap
    {
        [Tunable]
        public static bool kInstantiator = false;

        private static SimologyEntry entry;
        private static OccultMeter meter;
        private static JournalWindow window;
        private static bool worldReady;
        private static int generation;
        private static readonly Dictionary<string, bool> reportedOperations = new Dictionary<string, bool>();

        static JournalBootstrap()
        {
            World.OnWorldLoadFinishedEventHandler += OnWorldLoaded;
            World.OnWorldLoadStartedEventHandler += OnWorldExiting;
            World.OnWorldQuitEventHandler += OnWorldExiting;
        }

        private static void OnWorldLoaded(object sender, EventArgs args)
        {
            OnWorldExiting(sender, args);
            worldReady = true;
            entry = new SimologyEntry();
            entry.Refresh();
            meter = new OccultMeter();
            meter.Refresh();
        }

        private static void OnWorldExiting(object sender, EventArgs args)
        {
            worldReady = false;
            generation++;
            UiAnimationClock.Shutdown();
            Close(window);
            if (meter != null)
            {
                OccultMeter oldMeter = meter;
                meter = null;
                oldMeter.Dispose();
            }
            if (entry != null)
            {
                SimologyEntry old = entry;
                entry = null;
                try
                {
                    old.Dispose();
                }
                catch (Exception error)
                {
                    Report("Removing the Simology button", error);
                }
            }
        }

        internal static void RefreshMeter()
        {
            if (meter != null)
            {
                meter.Refresh();
            }
        }

        internal static void RefreshPerkPoints()
        {
            if (entry != null)
            {
                entry.RefreshPoints();
            }
        }

        internal static int ReservedPerkPoints(ulong simDescriptionId)
        {
            return window == null ? 0 : window.ReservedPerkPoints(simDescriptionId);
        }

        internal static bool TryButtonCenter(out Vector2 center)
        {
            center = new Vector2(0f, 0f);
            return entry != null && entry.TryButtonCenter(out center);
        }

        internal static void RunLater(Sims3.UI.Function action)
        {
            if (!worldReady)
            {
                return;
            }
            int token = generation;
            Simulator.AddObject(new Sims3.UI.OneShotFunctionTask(delegate
            {
                if (worldReady && token == generation)
                {
                    action();
                }
            }));
        }

        internal static void OpenJournal()
        {
            RunLater(Open);
        }

        private static void Open()
        {
            if (!worldReady)
            {
                return;
            }

            JournalWindow candidate = null;
            try
            {
                IResponder responder = Responder.Instance;
                IHudModel model = responder == null ? null : responder.HudModel;
                SimIdentity selected = SimIdentity.Read(model);
                if (selected == null)
                {
                    return;
                }

                if (window != null && window.IsOpen)
                {
                    window.Focus();
                    return;
                }
                if (UIManager.IsModal())
                {
                    return;
                }

                Close(window);
                candidate = new JournalWindow(model);
                window = candidate;
                candidate.Open(selected);
                RefreshPerkPoints();
            }
            catch (ResetException)
            {
                Close(candidate);
                throw;
            }
            catch (Exception error)
            {
                Report("Opening Supernatural Journal", error);
                Close(candidate);
            }
        }

        internal static void Close(JournalWindow closing)
        {
            if (closing == null || window != closing)
            {
                return;
            }

            window = null;
            try
            {
                closing.Dispose();
                RefreshPerkPoints();
            }
            catch (Exception error)
            {
                Report("Closing Supernatural Journal", error);
            }
        }

        // For fallbacks that can fire on every hover or click: the first failure of each operation is written, later ones are not.
        internal static void ReportOnce(string operation, Exception error)
        {
            if (reportedOperations.ContainsKey(operation))
            {
                return;
            }
            reportedOperations[operation] = true;
            Report(operation, error);
        }

        internal static void Report(string operation, Exception error)
        {
            WriteLog("<Operation>" + Escape(operation) + "</Operation><Exception>"
                + Escape(error.ToString()) + "</Exception>");
        }

#if ARCANA_ANIMATION_LOG
        internal static void TraceAnimation(string name, int frames, int elapsed)
        {
            WriteLog("<Animation name=\"" + name + "\" paintedFrames=\"" + frames
                + "\" elapsedMilliseconds=\"" + elapsed + "\" />");
        }
#endif

        private static void WriteLog(string payload)
        {
            try
            {
                uint handle = 0;
                Simulator.CreateScriptErrorFile(ref handle);
                if (handle == 0)
                {
                    return;
                }
                try
                {
                    string xml = "<SupernaturalJournal>" + payload + "</SupernaturalJournal>";
                    Simulator.AppendToScriptErrorFile(handle, xml.ToCharArray());
                }
                finally
                {
                    Simulator.CloseScriptErrorFile(handle);
                }
            }
            catch (Exception)
            {
            }
        }

        private static string Escape(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
