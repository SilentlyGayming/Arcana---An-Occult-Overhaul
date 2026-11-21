using System;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal sealed class SimologyEntry : IDisposable
    {
        private Delegate infoStateChanged;
        private IResponder responder;
        private HudController hud;
        private IHudModel model;
        private WindowBase panel;
        private WindowBase buttonParent;
        private Layout layout;
        private Button button;
        private Window glyph;
        private PerkPulse pulse;
        private Text name;
        private Rect originalNameArea;
        private Rect reservedNameArea;
        private Text.OverflowModes originalNameOverflow;
        private bool nameReserved;
        private bool panelGone;
        private bool queued;
        private bool pointsQueued;
        private bool pointsStale = true;
        private bool pointsTracked;
        private bool hasPoints;
        private bool disposed;

        internal void Refresh()
        {
            if (queued || disposed)
            {
                return;
            }

            queued = true;
            JournalBootstrap.RunLater(delegate
            {
                queued = false;
                if (!disposed)
                {
                    ConnectSafely();
                }
            });
        }

        private void ConnectSafely()
        {
            try
            {
                Connect();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Connecting the Supernatural Journal button to Simology", error);
            }
        }

        private void Connect()
        {
            IResponder currentResponder = Responder.Instance;
            if (responder != currentResponder)
            {
                if (responder != null)
                {
                    responder.GameStateChanging -= OnGameStateChanging;
                }
                responder = currentResponder;
                if (responder != null)
                {
                    responder.GameStateChanging += OnGameStateChanging;
                }
            }

            IHudModel currentModel = currentResponder == null ? null : currentResponder.HudModel;
            if (model != currentModel)
            {
                if (model != null)
                {
                    UnsubscribeFromModel(model);
                }
                model = currentModel;
                pointsStale = true;
                if (model != null)
                {
                    SubscribeToModel(model);
                }
            }

            UpdateHudRegistration(NativeUi.GetHud());
            WindowBase active = NativeUi.GetSimologyPanel();
            if (panel != active || panelGone)
            {
                DetachPanel();
                if (active != null && !active.Disposed)
                {
                    NativeUi.WatchShutdown(active, OnPanelShutdown, true);
                    panel = active;
                    try
                    {
                        panel.VisibilityChange += OnVisibilityChange;
                        panel.AreaChange += OnAreaChange;
                    }
                    catch
                    {
                        DetachPanel();
                        throw;
                    }
                }
            }

            ObjectGuid id;
            if (SimIdentity.ReadOccults(model, out id) == OccultTypes.None)
            {
                RestoreName();
                ReleaseButton(panelGone || (panel != null && panel.Disposed));
                pointsStale = true;
                pointsTracked = false;
                hasPoints = false;
                return;
            }

            if (PanelShowing)
            {
                Place();
            }
        }

        private void UpdateHudRegistration(HudController current)
        {
            if (hud == current)
            {
                return;
            }
            if (hud != null)
            {
                NativeUi.WatchInfoState(hud, infoStateChanged, false);
                hud = null;
            }
            if (current != null)
            {
                if (infoStateChanged == null)
                {
                    infoStateChanged = NativeUi.BindInfoStateHandler(OnInfoStateChange);
                }
                NativeUi.WatchInfoState(current, infoStateChanged, true);
                hud = current;
            }
        }

        private void OnGameStateChanging(Responder.GameSubState previousState, Responder.GameSubState newState)
        {
            Refresh();
            JournalBootstrap.RefreshMeter();
        }

        private void OnInfoStateChange(InfoState state)
        {
            if (state == InfoState.Simology)
            {
                Refresh();
            }
        }

        private void SubscribeToModel(IHudModel target)
        {
            target.SimChanged += OnSimChanged;
            target.OccultUpdated += OnOccultUpdated;
            target.SkillChanged += RefreshPoints;
            target.MinuteChanged += OnMinuteChanged;
        }

        private void UnsubscribeFromModel(IHudModel target)
        {
            target.SimChanged -= OnSimChanged;
            target.OccultUpdated -= OnOccultUpdated;
            target.SkillChanged -= RefreshPoints;
            target.MinuteChanged -= OnMinuteChanged;
        }

        private void OnSimChanged(ObjectGuid id)
        {
            pointsStale = true;
            Refresh();
            JournalBootstrap.RefreshMeter();
        }

        private void OnOccultUpdated()
        {
            pointsStale = true;
            Refresh();
        }

        private void OnMinuteChanged()
        {
            if (pointsTracked)
            {
                RefreshPoints();
            }
        }

        internal bool TryButtonCenter(out Vector2 center)
        {
            center = new Vector2(0f, 0f);
            if (disposed || button == null || button.Disposed || !button.Visible)
            {
                return false;
            }

            Rect area = button.Area;
            center = button.WindowToScreen(new Vector2(area.Width / 2f, area.Height / 2f));
            return true;
        }

        internal void RefreshPoints()
        {
            pointsStale = true;
            if (pointsQueued || disposed || !PanelShowing)
            {
                return;
            }

            pointsQueued = true;
            JournalBootstrap.RunLater(delegate
            {
                pointsQueued = false;
                if (!disposed)
                {
                    UpdatePointsSafely();
                }
            });
        }

        private bool PanelShowing
        {
            get { return panel != null && !panel.Disposed && panel.Visible; }
        }

        private void UpdatePointsSafely()
        {
            try
            {
                UpdatePoints();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Updating the Perk Point glow", error);
            }
        }

        private void UpdatePoints()
        {
            if (pointsStale && PanelShowing)
            {
                SimIdentity identity = model == null ? null : SimIdentity.Read(model);
                pointsTracked = identity != null && PerkPoints.Applies(identity.Occults);
                hasPoints = pointsTracked && PerkPoints.Available(identity)
                    > JournalBootstrap.ReservedPerkPoints(identity.SimDescriptionId);
                pointsStale = false;
            }
            ShowPulse();
        }

        private void ShowPulse()
        {
            if (pulse != null)
            {
                pulse.Show(hasPoints && PanelShowing && button != null && !button.Disposed && button.Visible);
            }
        }

        private void OnVisibilityChange(WindowBase sender, UIVisibilityChangeEventArgs args)
        {
            if (!PanelShowing)
            {
                ShowPulse();
            }
            Refresh();
        }

        private void OnAreaChange(WindowBase sender, UIAreaChangeEventArgs args)
        {
            Refresh();
        }

        private void Place()
        {
            Button familyTree = NativeLayouts.Find<Button>(panel, NativeLayouts.FamilyTree);
            WindowBase traits = NativeLayouts.Find<WindowBase>(panel, NativeLayouts.SimologyTraits);
            Text simName = NativeLayouts.Find<Text>(panel, NativeLayouts.SimName);
            WindowBase parent = familyTree == null || familyTree.Disposed ? null : familyTree.Parent;
            if (parent == null || parent.Disposed || traits == null || traits.Disposed
                || simName == null || simName.Disposed)
            {
                HideButton();
                return;
            }

            Rect family = familyTree.Area;
            Rect traitArea = GetAreaInParent(traits, parent);
            Rect nameArea = GetAreaInParent(simName, parent);
            float left;
            if (!SimologyRow.TryJournalLeft(family.TopLeft.x, family.Width, traitArea.TopLeft.x, out left)
                || !(family.Height > 0f)
                || !FindOpenSlot(parent, family, nameArea.TopLeft.x, ref left))
            {
                HideButton();
                return;
            }

            if (button == null || button.Disposed || buttonParent != parent)
            {
                ReleaseButton(panelGone);
                Layout candidate = UIManager.LoadLayoutAndAddToWindow(NativeLayouts.Key(NativeLayouts.HudButton), parent);
                try
                {
                    Button added = NativeLayouts.Root<Button>(candidate, NativeLayouts.HudButton);
                    if (added.Parent != parent)
                    {
                        throw new InvalidOperationException("The journal button did not attach to the Simology name row.");
                    }
                    Window icon = NativeLayouts.Require<Window>(added, NativeLayouts.HudGlyph);
                    icon.IgnoreMouse = true;
                    PerkPulse glow = new PerkPulse(added);
                    JournalTheme.ApplyHud(added, icon, familyTree, panel);
                    added.TooltipText = JournalText.Get("Arcana.Journal.Title");
                    added.Click += OnClick;
                    layout = candidate;
                    button = added;
                    glyph = icon;
                    pulse = glow;
                    buttonParent = parent;
                }
                catch
                {
                    NativeLayouts.Release(candidate, false);
                    throw;
                }
            }

            ReserveName(simName, parent, left - SimologyRow.Inset(family.Width));
            button.Area = NativeLayouts.Box(left, family.TopLeft.y, family.Width, family.Height);
            glyph.Area = NativeLayouts.Box(0f, 0f, family.Width, family.Height);
            if (!button.Visible)
            {
                button.Visible = true;
                button.MoveToFront();
            }
            UpdatePointsSafely();
        }

        private bool FindOpenSlot(WindowBase parent, Rect family, float nameLeft, ref float left)
        {
            float inset = SimologyRow.Inset(family.Width);
            float min = Math.Max(family.BottomRight.x, nameLeft + family.Width) + inset;
            WindowBase celebrity = NativeLayouts.Find<WindowBase>(panel, NativeLayouts.Celebrity);

            while (left >= min)
            {
                Rect slot = NativeLayouts.Box(left, family.TopLeft.y, family.Width, family.Height);
                if (celebrity != null && !celebrity.Disposed)
                {
                    left = GetLeftOfObstacle(slot, GetAreaInParent(celebrity, parent), left, inset);
                }

                for (uint i = 0; ; i++)
                {
                    WindowBase child = parent.GetChildByIndex(i);
                    if (child == null)
                    {
                        break;
                    }
                    if (child != button && !child.Disposed && child.Visible
                        && (child is Button || child is ItemGrid))
                    {
                        left = GetLeftOfObstacle(slot, child.Area, left, inset);
                    }
                }
                if (left == slot.TopLeft.x)
                {
                    return true;
                }
            }
            return false;
        }

        private static float GetLeftOfObstacle(Rect slot, Rect obstacle, float left, float gap)
        {
            if (obstacle.Width > 0f && obstacle.Height > 0f
                && slot.TopLeft.x < obstacle.BottomRight.x && slot.BottomRight.x > obstacle.TopLeft.x
                && slot.TopLeft.y < obstacle.BottomRight.y && slot.BottomRight.y > obstacle.TopLeft.y)
            {
                return Math.Min(left, obstacle.TopLeft.x - gap - slot.Width);
            }
            return left;
        }

        private void HideButton()
        {
            if (button != null && !button.Disposed)
            {
                button.Visible = false;
            }
            ShowPulse();
            RestoreName();
        }

        private static Rect GetAreaInParent(WindowBase control, WindowBase parent)
        {
            Rect area = control.Area;
            if (control.Parent == parent)
            {
                return area;
            }

            Vector2 start = parent.ScreenToWindow(control.WindowToScreen(new Vector2(0f, 0f)));
            Vector2 end = parent.ScreenToWindow(control.WindowToScreen(new Vector2(area.Width, area.Height)));
            return new Rect(start, end);
        }

        private void ReserveName(Text current, WindowBase parent, float right)
        {
            if (name != current)
            {
                RestoreName();
                name = current;
            }

            Rect area = name.Area;
            if (!nameReserved || area != reservedNameArea)
            {
                originalNameArea = area;
                originalNameOverflow = name.Overflow;
                nameReserved = false;
            }

            WindowBase nameParent = name.Parent;
            if (nameParent != null && nameParent != parent)
            {
                right = nameParent.ScreenToWindow(parent.WindowToScreen(new Vector2(right, 0f))).x;
            }
            if (right >= originalNameArea.BottomRight.x || right <= originalNameArea.TopLeft.x)
            {
                RestoreName();
                return;
            }

            reservedNameArea = new Rect(originalNameArea.TopLeft.x, originalNameArea.TopLeft.y,
                right, originalNameArea.BottomRight.y);
            name.Area = reservedNameArea;
            name.Overflow = Text.OverflowModes.kTOEllipsis;
            nameReserved = true;
        }

        private void RestoreName()
        {
            if (nameReserved && name != null && !name.Disposed && name.Area == reservedNameArea)
            {
                name.Area = originalNameArea;
                name.Overflow = originalNameOverflow;
            }
            nameReserved = false;
        }

        private void OnClick(WindowBase sender, UIButtonClickEventArgs args)
        {
            args.Handled = true;
            JournalBootstrap.OpenJournal();
        }

        private void OnPanelShutdown(WindowBase sender, UIEventArgs args)
        {
            panelGone = true;
            if (pulse != null) pulse.Release();
            Refresh();
        }

        private void ReleaseButton(bool windowsGone)
        {
            if (!windowsGone && button != null && !button.Disposed)
            {
                button.Click -= OnClick;
            }
            Layout old = layout;
            layout = null;
            button = null;
            glyph = null;
            if (pulse != null) pulse.Release();
            pulse = null;
            buttonParent = null;
            NativeLayouts.Release(old, windowsGone);
        }

        private void DetachPanel()
        {
            bool windowsGone = panelGone || (panel != null && panel.Disposed);
            try
            {
                if (!windowsGone && panel != null)
                {
                    panel.VisibilityChange -= OnVisibilityChange;
                    panel.AreaChange -= OnAreaChange;
                    RestoreName();
                    NativeUi.WatchShutdown(panel, OnPanelShutdown, false);
                }
            }
            finally
            {
                panel = null;
                panelGone = false;
                name = null;
                nameReserved = false;
                ReleaseButton(windowsGone);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (responder != null)
            {
                responder.GameStateChanging -= OnGameStateChanging;
            }
            if (model != null)
            {
                UnsubscribeFromModel(model);
            }
            try
            {
                UpdateHudRegistration(null);
            }
            finally
            {
                DetachPanel();
            }
        }
    }

    internal sealed class PerkPulse
    {
        private readonly Button button;
        private readonly Window ring;
        private readonly UiAnimation animation;
        private const float PulseExpansion = 2.6f;
        private const float PulseMinOpacity = 0.20f;
        private const float PulseMaxOpacity = 0.78f;

        internal PerkPulse(Button button)
        {
            this.button = button;
            ring = NativeLayouts.Require<Window>(button, NativeLayouts.PerkPulse);
            animation = new UiAnimation("Journal HUD pulse", Paint, Hide);
        }

        internal void Show(bool visible)
        {
            if (visible)
            {
                animation.Start();
            }
            else
            {
                animation.Stop();
            }
        }

        private bool Paint(int elapsed)
        {
            if (button.Disposed || ring.Disposed || !button.Visible)
            {
                return false;
            }

            Rect bounds = button.Area;
            float wave = (float)((1.0 - Math.Cos(2.0 * Math.PI * (elapsed % 1600) / 1600.0)) * 0.5);
            float width = bounds.Width * (32.2f + PulseExpansion * wave) / 36f;
            float height = bounds.Height * (27.2f + PulseExpansion * wave) / 30f;
            ring.Area = NativeLayouts.Box((bounds.Width - width) / 2f,
                (bounds.Height - height) / 2f, width, height);
            ring.ShadeColor = UiAnimation.Tint(0xFFFFFF, PulseMinOpacity + (PulseMaxOpacity - PulseMinOpacity) * wave);
            ring.Visible = true;
            ring.Invalidate();
            return true;
        }

        private void Hide()
        {
            if (!ring.Disposed)
            {
                ring.Visible = false;
            }
        }

        internal void Release()
        {
            animation.Stop();
        }
    }

    internal static class SimologyRow
    {
        internal static float Inset(float buttonWidth)
        {
            return buttonWidth / 6f;
        }

        internal static bool TryJournalLeft(float familyLeft, float buttonWidth, float traitsLeft, out float journalLeft)
        {
            float inset = Inset(buttonWidth);
            journalLeft = traitsLeft - inset - buttonWidth;
            return buttonWidth > 0f && !float.IsInfinity(journalLeft)
                && journalLeft >= familyLeft + buttonWidth + inset;
        }
    }
}
