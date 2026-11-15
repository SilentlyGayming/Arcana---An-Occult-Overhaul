using System;
using System.Collections.Generic;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal delegate bool AnimationFrame(int elapsedMilliseconds);

    internal sealed class UiAnimation
    {
        private readonly string name;
        private readonly AnimationFrame paint;
        private readonly JournalAction hide;
        private int started;
        private bool running;
#if ARCANA_ANIMATION_LOG
        private int frames;
#endif

        internal UiAnimation(string name, AnimationFrame paint, JournalAction hide)
        {
            this.name = name;
            this.paint = paint;
            this.hide = hide;
        }

        internal void Start()
        {
            if (running)
            {
                return;
            }

            started = System.Environment.TickCount;
#if ARCANA_ANIMATION_LOG
            frames = 0;
#endif
            running = true;
            try
            {
                UiAnimationClock.Add(this);
            }
            catch (ResetException)
            {
                Stop();
                throw;
            }
            catch (Exception error)
            {
                Stop();
                JournalBootstrap.ReportOnce("Starting " + name, error);
            }
        }

        internal void Stop()
        {
            if (!running)
            {
                return;
            }
            running = false;
            UiAnimationClock.Remove(this);
            hide();
        }

        internal void Advance(int now)
        {
            if (!running)
            {
                return;
            }
            int elapsed = Math.Max(0, unchecked(now - started));
            try
            {
                if (!paint(elapsed))
                {
                    Stop();
                    return;
                }
#if ARCANA_ANIMATION_LOG
                frames++;
                if (frames == 1 || frames == 16 || frames == 46)
                {
                    JournalBootstrap.TraceAnimation(name, frames, elapsed);
                }
#endif
            }
            catch (ResetException)
            {
                Stop();
                throw;
            }
            catch (Exception error)
            {
                Stop();
                JournalBootstrap.ReportOnce("Animating " + name, error);
            }
        }

        internal static Color Tint(uint rgb, float opacity)
        {
            return new Color(((uint)(255f * opacity + 0.5f) << 24) | rgb);
        }
    }

    // UI.dll's IconZoopTask uses this same Simulator task type to advance native
    // HUD UI effects without a WindowBase.Tick subscription or a threaded task.
    internal sealed class UiAnimationClock : Sims3.SimIFace.Task
    {
        private const int MinimumFrameMilliseconds = 16;
        private static readonly List<UiAnimation> active = new List<UiAnimation>(3);
        private readonly List<UiAnimation> frame = new List<UiAnimation>(3);
        private static UiAnimationClock clock;
        private int lastFrame = unchecked(System.Environment.TickCount - MinimumFrameMilliseconds);

        internal static void Add(UiAnimation animation)
        {
            active.Add(animation);
            if (clock != null)
            {
                return;
            }

            UiAnimationClock candidate = new UiAnimationClock();
            ObjectGuid id = Simulator.AddObject(candidate);
            if (!id.IsValid)
            {
                throw new InvalidOperationException("The simulator did not register the UI animation task.");
            }
            clock = candidate;
        }

        internal static void Remove(UiAnimation animation)
        {
            active.Remove(animation);
        }

        internal static void Shutdown()
        {
            while (active.Count != 0)
            {
                active[active.Count - 1].Stop();
            }
            if (clock != null)
            {
                clock.Unregister();
            }
        }

        public override void Simulate()
        {
            if (clock != this)
            {
                return;
            }
            if (active.Count == 0)
            {
                Unregister();
                return;
            }

            int now = System.Environment.TickCount;
            if (unchecked(now - lastFrame) < MinimumFrameMilliseconds)
            {
                return;
            }
            lastFrame = now;
            frame.AddRange(active);
            try
            {
                foreach (UiAnimation animation in frame)
                {
                    animation.Advance(now);
                }
            }
            finally
            {
                frame.Clear();
            }
        }

        public override void Dispose()
        {
            if (clock == this)
            {
                clock = null;
                while (active.Count != 0)
                {
                    active[active.Count - 1].Stop();
                }
            }
        }

        private void Unregister()
        {
            clock = null;
            if (ObjectId.IsValid)
            {
                Simulator.DestroyObject(ObjectId);
            }
        }
    }
}
