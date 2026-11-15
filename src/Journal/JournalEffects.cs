using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;
using System;

namespace SupernaturalExpanded.Journal
{
    // The two stock sparkle textures stay in place and twinkle independently.
    internal sealed class BarSparkles
    {
        internal const int Layers = 2;
        private static readonly int[] TwinkleMilliseconds = { 1000, 1620 };
        private static readonly int[] PhaseMilliseconds = { 0, 410 };
        private static readonly float[] PeakOpacity = { 0.78f, 0.88f };
        private const float MinimumOpacity = 0.16f;

        private readonly JournalWindow journal;
        private readonly Window clip;
        private readonly Window[] layers = new Window[Layers];
        private readonly UiAnimation animation;

        internal BarSparkles(JournalWindow journal, uint clipId)
        {
            this.journal = journal;
            clip = journal.Control<Window>(clipId);
            clip.Clip = true;
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = journal.Control<Window>(NativeLayouts.StatusSparkleFirst + (uint)i);
            }
            animation = new UiAnimation("Magical Power twinkle", Paint, Hide);
        }

        internal void SetFill(HeaderMeterBounds bounds)
        {
            if (clip.Disposed)
            {
                return;
            }

            Rect fill = bounds.SparkleClip;
            if (fill.Width <= 0f || fill.Height <= 0f)
            {
                Stop();
                return;
            }

            if (clip.Area != fill)
            {
                clip.Area = fill;
                Rect local = NativeLayouts.Box(0f, 0f, fill.Width, fill.Height);
                foreach (Window layer in layers)
                {
                    layer.Area = local;
                }
            }
            animation.Start();
        }

        private bool Paint(int elapsed)
        {
            if (clip.Disposed || !journal.IsOpen)
            {
                return false;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                int period = TwinkleMilliseconds[i];
                float phase = ((elapsed % period + PhaseMilliseconds[i]) % period) / (float)period;
                float wave = (float)(0.5 - 0.5 * Math.Cos(phase * 2.0 * Math.PI));
                Window layer = layers[i];
                layer.ShadeColor = UiAnimation.Tint(0xFFFFFF, MinimumOpacity + (PeakOpacity[i] - MinimumOpacity) * wave);
                layer.Visible = true;
                layer.Invalidate();
            }
            clip.Visible = true;
            return true;
        }

        internal void Stop()
        {
            animation.Stop();
        }

        private void Hide()
        {
            if (!clip.Disposed)
            {
                clip.Visible = false;
            }
        }
    }

    internal sealed class RankGlitter
    {
        internal const int TileCount = 10;
        // The upward glitter travels one tile height per cycle; GlitterSpeed scales that speed relative to the original 1900 ms cycle.
        private const float BaseCycleMilliseconds = 1900f;
        internal const float GlitterSpeed = 0.82f;
        private const int CycleMilliseconds = (int)(BaseCycleMilliseconds / GlitterSpeed + 0.5f);
        // The endpoint glint, in track widths: it swells and brightens once per cycle and its centre stays TipCoreInset inside the track ends.
        private const int TipCycleMilliseconds = 1300;
        private const float TipMinSide = 0.85f;
        private const float TipMaxSide = 1.20f;
        private const float TipMinOpacity = 0.50f;
        private const float TipMaxOpacity = 1f;
        private const float TipCoreInset = 0.25f;
        private readonly Window clip;
        private readonly Window tipClip;
        private readonly Window tip;
        private readonly Window[] tiles = new Window[TileCount];
        private readonly UiAnimation animation;
        private Rect fill;
        private Rect track;

        internal RankGlitter(Window clip, Window tipClip)
        {
            this.clip = clip;
            this.tipClip = tipClip;
            tip = (Window)tipClip.GetChildByIndex(0);
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = (Window)clip.GetChildByIndex((uint)i);
            }
            animation = new UiAnimation("rank XP glitter", Paint, Hide);
        }

        internal void SetFill(Rect newFill, Rect newTrack)
        {
            fill = newFill;
            track = newTrack;
            if (fill.Width <= 0f || fill.Height <= 0f)
            {
                animation.Stop();
                return;
            }

            clip.Area = fill;
            tipClip.Area = track;
            animation.Start();
        }

        private bool Paint(int elapsed)
        {
            if (clip.Disposed || tipClip.Disposed || clip.Parent == null || !clip.Parent.Visible)
            {
                return false;
            }
            // The phase scrolls up through a clip at the *fill* bounds. Each tile
            // covers a constant-height interval and samples a vertically repeating image.
            float step = Math.Max(1f, (float)Math.Ceiling(track.Height / (TileCount - 2)));
            float phase = (elapsed % CycleMilliseconds) / (float)CycleMilliseconds * step;
            float y = -phase;
            for (int i = 0; i < tiles.Length; i++)
            {
                Window tile = tiles[i];
                bool visible = y < fill.Height;
                if (visible)
                {
                    tile.Area = NativeLayouts.Box(0f, y, fill.Width, step);
                }
                tile.Visible = visible;
                y += step;
            }

            // The glint is a square centred on the bar's centre line at the fill endpoint; its track-sized clip trims it at the bar's edges.
            float wave = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * (elapsed % TipCycleMilliseconds) / TipCycleMilliseconds));
            float glint = wave * wave;
            float side = track.Width * (TipMinSide + (TipMaxSide - TipMinSide) * glint);
            float core = Math.Min(track.Width * TipCoreInset, track.Height / 2f);
            float center = Math.Max(core, Math.Min(track.Height - core, fill.TopLeft.y - track.TopLeft.y));
            tip.Area = NativeLayouts.Box((track.Width - side) / 2f, center - side / 2f, side, side);
            tip.ShadeColor = UiAnimation.Tint(0xFFFFFF, TipMinOpacity + (TipMaxOpacity - TipMinOpacity) * glint);
            tip.Visible = true;
            tip.Invalidate();
            tipClip.Visible = true;
            clip.Visible = true;
            return true;
        }

        internal void Stop()
        {
            animation.Stop();
        }

        private void Hide()
        {
            if (!clip.Disposed)
            {
                clip.Visible = false;
            }
            if (!tipClip.Disposed)
            {
                tipClip.Visible = false;
            }
        }
    }

    internal sealed class SparkleBurst
    {
        internal const int ParticlesPerBurst = 28;
        internal const int BurstCount = 3;
        // tools/ui_resources.py packages one 72-degree sweep of a five-point star.
        private const int SpinFrames = 12;
        private const ulong FirstSpinFrame = 0x33D0573FF965EC9EUL;
        private const float FullTurn = (float)(2.0 * Math.PI);
        private static readonly uint[] Golds = { 0xFFCE4D, 0xFFE04F, 0xFFEE91, 0xFFF8C4, 0xFFFFF5 };

        private sealed class Burst
        {
            internal Button Tile;
            internal int Started;
        }

        private struct Spark
        {
            internal Window Sprite;
            internal ImageDrawable Drawable;
            internal float Angle, Curve, Distance, Size, Spin, Orientation, Brightness;
            internal int Delay, Lifetime, SpinFrame;
            internal uint Tint;
        }

        private readonly JournalWindow journal;
        private readonly Window overlay;
        private readonly Burst[] bursts = new Burst[BurstCount];
        private readonly Spark[] sparks = new Spark[BurstCount * ParticlesPerBurst];
        private readonly UIImage[] images = new UIImage[SpinFrames];
        private readonly Random random = new Random();
        private readonly UiAnimation animation;
        private bool imagesLoaded;

        internal SparkleBurst(JournalWindow journal, Window overlay)
        {
            this.journal = journal;
            this.overlay = overlay;
            animation = new UiAnimation("perk sparkler", Paint, Clear);
            for (int i = 0; i < bursts.Length; i++)
            {
                bursts[i] = new Burst();
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                Window sprite = overlay.GetChildByIndex((uint)i) as Window;
                ImageDrawable drawable = sprite == null ? null : sprite.Drawable as ImageDrawable;
                if (drawable == null)
                {
                    throw new InvalidOperationException("Missing sparkler image control " + i);
                }
                sparks[i].Sprite = sprite;
                sparks[i].Drawable = drawable;
                sparks[i].SpinFrame = -1;
            }
        }

        internal void Play(Button tile)
        {
            if (tile.Disposed || overlay.Disposed)
            {
                return;
            }
            if (!imagesLoaded && !LoadImages())
            {
                return;
            }

            int now = System.Environment.TickCount;
            int slot = 0;
            for (int i = 0; i < bursts.Length; i++)
            {
                if (bursts[i].Tile == null)
                {
                    slot = i;
                    break;
                }
                if (unchecked(now - bursts[i].Started) > unchecked(now - bursts[slot].Started))
                {
                    slot = i;
                }
            }

            ClearBurst(slot);
            bursts[slot].Tile = tile;
            bursts[slot].Started = now;
            int first = slot * ParticlesPerBurst;
            float startAngle = Range(0f, FullTurn);
            for (int i = 0; i < ParticlesPerBurst; i++)
            {
                Spark spark = sparks[first + i];
                spark.Angle = startAngle + (i + Range(-0.4f, 0.4f)) * FullTurn / ParticlesPerBurst;
                spark.Curve = Range(-0.38f, 0.38f);
                spark.Distance = Range(27f, 65f);
                spark.Size = i % 8 == 0 ? Range(6f, 8f) : i % 3 == 0 ? Range(4f, 5f) : Range(2.5f, 3.8f);
                spark.Spin = Range(2.5f, 6.5f) * (i % 2 == 0 ? 1 : -1);
                spark.Orientation = Range(0f, 1f);
                spark.Brightness = Range(0.74f, 1f);
                spark.Delay = random.Next(0, 85);
                spark.Lifetime = random.Next(900, 1190);
                spark.Tint = Golds[random.Next(Golds.Length)];
                sparks[first + i] = spark;
            }

            animation.Start();
        }

        private bool LoadImages()
        {
            try
            {
                for (int i = 0; i < images.Length; i++)
                {
                    images[i] = UIManager.LoadUIImage(new ResourceKey(FirstSpinFrame + (ulong)i, 0x2F7D0004, 0));
                    if (images[i] == null)
                    {
                        throw new InvalidOperationException("Missing spark image " + i);
                    }
                }
                imagesLoaded = true;
                return true;
            }
            catch (ResetException)
            {
                ReleaseImages();
                throw;
            }
            catch (Exception error)
            {
                ReleaseImages();
                JournalBootstrap.ReportOnce("Loading the perk sparkler", error);
                return false;
            }
        }

        private float Range(float min, float max)
        {
            return min + (max - min) * (float)random.NextDouble();
        }

        private bool Paint(int elapsed)
        {
            if (overlay.Disposed || !journal.IsOpen || !journal.RootWindow.Visible)
            {
                return false;
            }

            int now = System.Environment.TickCount;
            bool any = false;
            for (int slot = 0; slot < bursts.Length; slot++)
            {
                Burst burst = bursts[slot];
                if (burst.Tile == null)
                {
                    continue;
                }
                if (burst.Tile.Disposed || !PaintBurst(slot, unchecked(now - burst.Started)))
                {
                    ClearBurst(slot);
                }
                else
                {
                    any = true;
                }
            }
            overlay.Visible = any;
            return any;
        }

        private bool PaintBurst(int slot, int elapsed)
        {
            Button tile = bursts[slot].Tile;
            Rect area = tile.Area;
            Vector2 start = overlay.ScreenToWindow(tile.WindowToScreen(new Vector2(0f, 0f)));
            Vector2 end = overlay.ScreenToWindow(tile.WindowToScreen(new Vector2(area.Width, area.Height)));
            float scale = journal.LayoutScale;
            float halfWidth = (end.x - start.x) / 2f;
            float halfHeight = (end.y - start.y) / 2f;
            float centerX = start.x + halfWidth;
            float centerY = start.y + halfHeight;
            bool alive = false;

            int first = slot * ParticlesPerBurst;
            for (int i = first; i < first + ParticlesPerBurst; i++)
            {
                Spark spark = sparks[i];
                int age = elapsed - spark.Delay;
                if (age >= spark.Lifetime)
                {
                    spark.Sprite.Visible = false;
                    continue;
                }
                alive = true;
                if (age < 0)
                {
                    continue;
                }

                float t = age / (float)spark.Lifetime;
                float dx = (float)Math.Cos(spark.Angle);
                float dy = (float)Math.Sin(spark.Angle);
                float rim = Math.Min(halfWidth / Math.Max(0.001f, Math.Abs(dx)),
                    halfHeight / Math.Max(0.001f, Math.Abs(dy))) - (spark.Size / 2f + 1f) * scale;
                float travel = spark.Distance * scale * t * (1.72f - 0.72f * t);
                float heading = spark.Angle + spark.Curve * t;
                float x = centerX + dx * rim + (float)Math.Cos(heading) * travel;
                float y = centerY + dy * rim + (float)Math.Sin(heading) * travel + 7f * scale * t * t;
                float fade = Math.Min(1f, (1f - t) / 0.53f);
                float alpha = Math.Min(1f, 0.24f + age / 65f) * fade * fade * spark.Brightness;
                float size = Math.Max(1f, spark.Size * scale * (1f - 0.20f * t));
                float angle = spark.Orientation + spark.Spin * t;
                int spinFrame = (int)((angle - Math.Floor(angle)) * SpinFrames);
                if (spinFrame != spark.SpinFrame)
                {
                    spark.Drawable.Image = images[spinFrame];
                    sparks[i].SpinFrame = spinFrame;
                }
                spark.Sprite.Area = NativeLayouts.Box(x - size / 2f, y - size / 2f, size, size);
                spark.Sprite.ShadeColor = UiAnimation.Tint(spark.Tint, alpha);
                spark.Sprite.Visible = true;
                spark.Sprite.Invalidate();
            }
            return alive;
        }

        private void ClearBurst(int slot)
        {
            bursts[slot].Tile = null;
            int first = slot * ParticlesPerBurst;
            for (int i = first; i < first + ParticlesPerBurst; i++)
            {
                Window sprite = sparks[i].Sprite;
                if (!sprite.Disposed)
                {
                    sprite.Visible = false;
                }
            }
        }

        private void Clear()
        {
            for (int i = 0; i < bursts.Length; i++)
            {
                ClearBurst(i);
            }
            if (!overlay.Disposed)
            {
                overlay.Visible = false;
            }
        }

        internal void Hide()
        {
            animation.Stop();
        }

        internal void Release()
        {
            Hide();
            foreach (Spark spark in sparks)
            {
                if (!spark.Sprite.Disposed)
                {
                    spark.Drawable.Image = null;
                }
            }
            ReleaseImages();
        }

        private void ReleaseImages()
        {
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                {
                    images[i].Dispose();
                    images[i] = null;
                }
            }
            imagesLoaded = false;
        }
    }

    // Flies a larger copy of the perk icon from its card to the Journal button in a transient layout, falling back to the native zoop if that fails.
    internal static class PurchaseEffect
    {
        internal const float FlightIconSize = 64f;   // ~2x the stock 33px icon
        private const float EndIconSize = 46f;
        private const int DurationMilliseconds = 620;
        private const float Loft = 16f;
        private const float NativeHalfIcon = 16.5f;

        internal static void Fly(UIImage image, Vector2 start, Vector2 end)
        {
            if (image == null)
            {
                return;
            }

            try
            {
                new Flight(image, start, end).Start();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.Report("Starting the large perk purchase effect", error);
                // A failure of our larger visual should not break a successful purchase.
                try
                {
                    IconZoop.Zoop(image, null,
                        new Vector2(start.x - NativeHalfIcon, start.y - NativeHalfIcon),
                        new Vector2(end.x - NativeHalfIcon, end.y - NativeHalfIcon),
                        false, null, null, false, -1f);
                }
                catch (ResetException)
                {
                    throw;
                }
                catch (Exception fallback)
                {
                    JournalBootstrap.Report("Falling back to the native perk zoop", fallback);
                }
            }
        }

        private sealed class Flight
        {
            private Layout layout;
            private Window icon;
            private readonly UIImage image;
            private readonly Vector2 screenStart;
            private readonly Vector2 screenEnd;
            private Vector2 from;
            private Vector2 to;
            private int started;
            private float uiScale = 1f;
            private bool ended;

            internal Flight(UIImage image, Vector2 start, Vector2 end)
            {
                this.image = image;
                screenStart = start;
                screenEnd = end;
            }

            internal void Start()
            {
                try
                {
                    layout = UIManager.LoadLayoutAndAddToWindow(
                        NativeLayouts.Key(NativeLayouts.PurchaseFlight), UICategory.Dialogs);
                    icon = NativeLayouts.Root<Window>(layout, NativeLayouts.PurchaseFlight);
                    ImageDrawable drawable = icon.Drawable as ImageDrawable;
                    if (drawable == null || icon.Parent == null)
                    {
                        throw new InvalidOperationException("The perk flight layout has no image drawable or parent.");
                    }
                    icon.Visible = false;
                    drawable.Image = image;
                    icon.IgnoreMouse = true;
                    icon.Topmost = true;
                    icon.MoveToFront();
                    from = icon.Parent.ScreenToWindow(screenStart);
                    to = icon.Parent.ScreenToWindow(screenEnd);
                    uiScale = NativeLayouts.UIScale();
                    started = System.Environment.TickCount;
                    Frame();
                    if (!ended && !icon.Disposed)
                    {
                        icon.Visible = true;
                    }
                }
                catch
                {
                    End();
                    throw;
                }
            }

            private void Frame()
            {
                if (ended || icon == null || icon.Disposed)
                {
                    End();
                    return;
                }

                try
                {
                    int elapsed = unchecked(System.Environment.TickCount - started);
                    float t = Math.Min(1f, Math.Max(0f, elapsed / (float)DurationMilliseconds));
                    // Ease toward the HUD; shrink gently, but never to the stock tiny size.
                    float ease = 1f - (float)Math.Pow(1f - t, 2.2);
                    float centerX = from.x + (to.x - from.x) * ease;
                    float centerY = from.y + (to.y - from.y) * ease
                        - Loft * uiScale * (float)Math.Sin(Math.PI * t);
                    float size = uiScale * (FlightIconSize + (EndIconSize - FlightIconSize) * t * t);
                    icon.Area = NativeLayouts.Box(centerX - size / 2f, centerY - size / 2f,
                        size, size);

                    // Gentle final fade; preserve a full-size, full-opacity icon early in the flight.
                    float opacity = t < 0.78f ? 1f : Math.Max(0f, (1f - t) / 0.22f);
                    uint alpha = (uint)(opacity * 255f);
                    icon.ShadeColor = new Color((alpha << 24) | 0x00FFFFFF);
                    icon.Invalidate();

                    if (t >= 1f)
                    {
                        End();
                    }
                    else
                    {
                        JournalBootstrap.RunLater(Frame);
                    }
                }
                catch (ResetException)
                {
                    End();
                    throw;
                }
                catch (Exception error)
                {
                    JournalBootstrap.Report("Animating the large perk icon", error);
                    End();
                }
            }

            private void End()
            {
                if (ended)
                {
                    return;
                }
                ended = true;
                if (layout != null)
                {
                    NativeLayouts.Release(layout, icon != null && icon.Disposed);
                    layout = null;
                }
            }
        }
    }
}
