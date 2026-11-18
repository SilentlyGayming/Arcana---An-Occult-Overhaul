using System;
using System.Collections.Generic;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal delegate void JournalAction();

    internal sealed class JournalWindow : IDisposable
    {
        // The identity header is three evenly spaced lines beside the portrait, 25 units apart.
        private const float IdentityLeft = 110f;
        private const float IdentityTop = 31f;
        private const float IdentityLineHeight = 24f;
        private const float IdentityLineSpacing = 25f;
        private const float TabTop = 107f;
        // The group starts clear of the frame's curved left edge, and adjacent tabs have an even gap.
        private const float TabLeft = 41f;
        private const float TabGap = 9f;
        private const float TabHeight = 43f;
        // Native circular Confirm and Cancel buttons inside the two-button dialog shell: the 56-unit Accept
        // keeps its native size; the 29-unit X is centred at the donor's native -79.5 footer coordinate.
        private const float CloseSize = 56f;
        private const float CancelSize = 29f;
        private const float CloseCenterX = 41f;
        private const float CancelCenterX = 79.5f;
        private const float FooterCenterY = 41f;
        private const uint AcceptTrigger = 0x042B0000;
        private const uint CancelTrigger = 0x042B0001;
        private const uint NextTrigger = 0x042B0002;
        private const uint PreviousTrigger = 0x042B0003;
        private const uint AlternatePreviousTrigger = 0x042B0006;

        private readonly IHudModel model;
        private readonly Dictionary<WindowBase, Rect> placements = new Dictionary<WindowBase, Rect>();
        // Controls whose scaled bounds are rounded to whole pixels, so wrapped text is never drawn at a fractional size.
        private readonly Dictionary<WindowBase, bool> snapped = new Dictionary<WindowBase, bool>();
        private readonly Dictionary<Button, JournalAction> actions = new Dictionary<Button, JournalAction>();
        private readonly List<Button> keyboardOrder = new List<Button>();
        private readonly Button[] tabs = new Button[JournalPages.All.Length];

        private Layout layout;
        private Dialog root;
        private WindowBase viewport;
        private Window portrait;
        private ImageDrawable portraitImage;
        private Text title;
        private Text simName;
        private Text occultRank;
        private Button close;
        private Button cancel;
        private JournalFrames frames;
        private Window content;
        private SimIdentity identity;
        private PowerSelection selection;
        private PowerPage perkPage;
        private GrimoirePage grimoirePage;
        private OverviewPage overviewPage;
        private StatusHeader header;
        private bool headerReported;
        private bool overviewReported;
        private Button keyboardButton;
        private JournalModal modal;
        private PerkPurchaseDialog perkConfirmation;
        private int page;
        private float scale = 1f;
        private float placedScale = 1f;
        private bool windowPlaced;
        private bool placedHeader;
        private bool subscribed;
        private bool watchingShutdown;
        private bool gone;
        private bool disposed;
        private bool closing;
        private bool keyActivation;

        internal JournalWindow(IHudModel model)
        {
            this.model = model;
        }

        private PowerSelection ActiveSelection
        {
            get { return page == JournalPages.Perks && perkPage != null ? selection : null; }
        }

        internal int ReservedPerkPoints(ulong simDescriptionId)
        {
            return IsOpen && !closing && selection != null && selection.Available
                && selection.SimDescriptionId == simDescriptionId ? selection.PendingPowerCost : 0;
        }

        internal bool IsOpen { get { return !disposed && !gone && root != null && !root.Disposed; } }
        // Another modal window is above the Journal, so input is not ours to take.
        private bool Covered { get { return modal != null && modal.Covered; } }
        internal WindowBase RootWindow { get { return root; } }
        internal float LayoutScale { get { return scale; } }

        internal void Open(SimIdentity selected)
        {
            layout = UIManager.LoadLayoutAndAddToWindow(NativeLayouts.Key(NativeLayouts.Journal), UICategory.Dialogs);
            root = NativeLayouts.Root<Dialog>(layout, NativeLayouts.Journal);
            root.Visible = false;
            root.Topmost = false;
            root.ClickToFront = true;
            root.ShowCloseButton = false;

            title = Label(NativeLayouts.Title, JournalText.Get("Arcana.Journal.Title"),
                IdentityLeft, IdentityTop, 635, IdentityLineHeight);
            portrait = Control<Window>(NativeLayouts.Portrait);
            portraitImage = portrait.Drawable as ImageDrawable;
            if (portraitImage == null)
            {
                throw new InvalidOperationException("The native portrait control has no image drawable.");
            }
            // The native portrait group is centered on the three identity lines, with no square matte.
            float identityCenterY = IdentityTop + IdentityLineSpacing + IdentityLineHeight / 2f;
            Place(portrait, 25f, identityCenterY - 32f, 64f, 64f);
            Place(Control<Window>(NativeLayouts.PortraitFrame),
                23f, identityCenterY - 34f, 68f, 68f);
            simName = Label(NativeLayouts.JournalSimName, "",
                IdentityLeft, IdentityTop + IdentityLineSpacing, 635, IdentityLineHeight);
            occultRank = Label(NativeLayouts.OccultRank, "",
                IdentityLeft, IdentityTop + 2f * IdentityLineSpacing, 635, IdentityLineHeight);
            content = Control<Window>(NativeLayouts.Content);
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                tabs[i] = Button(NativeLayouts.FirstTab + (uint)i, JournalPages.All[i].Title, TabLeft, TabTop, 137, TabHeight,
                    delegate { RequestPage(tab); });
            }
            cancel = Control<Button>(NativeLayouts.Cancel);
            cancel.Visible = false;
            cancel.TooltipText = JournalText.Get("Arcana.Journal.Perks.Tooltip.Discard");
            Bind(cancel, RequestCancel);
            close = Control<Button>(NativeLayouts.Close);
            close.Visible = true;
            // The checkmark names what it will do: buy what is selected, or just close when nothing is.
            close.CreateTooltipCallbackFunction = delegate(Vector2 mouse, WindowBase window, ref Vector2 position)
            {
                PowerSelection active = ActiveSelection;
                return new SimpleTextTooltip(active != null && active.PendingCount > 0 ? JournalText.Get("Arcana.Journal.Perks.Tooltip.Confirm") : JournalText.Get("Arcana.Common.Close"));
            };
            Bind(close, RequestClose);
            perkPage = PowerPage.Create(this, WitchTree.Tree);
            grimoirePage = GrimoirePage.Create(this);
            overviewPage = OverviewPage.Create(this);
            grimoirePage.EntriesChanged += OnKnowledgeChanged;
            header = StatusHeader.Create(this);

            frames = JournalTheme.ApplyDialog(root);
            overviewPage.ApplyInk();
            grimoirePage.UseFaces(new TabFaces(grimoirePage.Tabs));
            viewport = root.Parent;
            if (viewport != null)
            {
                viewport.AreaChange += OnViewportChanged;
            }
            RefreshSim(selected);
            NativeUi.WatchShutdown(root, OnWindowShutdown, true);
            watchingShutdown = true;
            root.TriggerDown += OnTrigger;
            Subscribe();
            modal = new JournalModal(root);
            modal.Begin();
            Focus();
        }

        internal void Focus()
        {
            if (!IsOpen || closing || perkConfirmation != null || Covered)
            {
                return;
            }

            root.MoveToFront();
            UIManager.SetFocus(InputContext.kICKeyboard, root);
            SetKeyboardButton(tabs[page]);
        }

        private void OnViewportChanged(WindowBase sender, UIAreaChangeEventArgs args)
        {
            if (IsOpen)
            {
                windowPlaced = false;
                UpdateLayout();
            }
        }

        // One scale for the window, so switching pages never rescales it.
        private static float GetFitScale(float width, float height)
        {
            Rect screen = UIManager.ScreenArea;
            return Math.Min(NativeLayouts.UIScale(), Math.Min(Math.Max(1f, screen.Width - 30f) / width,
                Math.Max(1f, screen.Height - 72f) / height));
        }

        private void UpdateLayout()
        {
            PlaceWindow();
            LayoutActivePage();
        }

        // Sizes and centers the window and places the header, footer and content panel. Page switches skip it while none of its inputs changed.
        private void PlaceWindow()
        {
            float width = WindowGeometry.Width;
            float height = WindowGeometry.Height;
            float fit = GetFitScale(width, height);
            bool showHeader = header != null && header.Visible;
            if (windowPlaced && fit == scale && showHeader == placedHeader)
            {
                return;
            }
            windowPlaced = true;
            placedHeader = showHeader;
            scale = fit;

            // The window is a whole number of pixels in size and sits on a whole pixel: centring an odd screen width leaves it half a
            // pixel off, and every glyph inside would then be drawn between pixels.
            root.Area = NativeLayouts.Box(0, 0, (float)Math.Round(width * scale), (float)Math.Round(height * scale));
            root.CenterInParent();
            float top = Math.Max(4f, (float)Math.Round(root.Area.TopLeft.y - 24f * scale));
            root.Position = new Vector2((float)Math.Round(root.Area.TopLeft.x), top);
            frames.Layout(scale);
            // With the status panel shown, the title and name stop short of it.
            float textRight = showHeader ? HeaderLayout.PanelLeft(width) - HeaderLayout.PanelGap : width - 30f;
            Place(title, IdentityLeft, IdentityTop, textRight - IdentityLeft, IdentityLineHeight);
            if (showHeader)
            {
                header.Layout(width);
            }
            Place(simName, IdentityLeft, IdentityTop + IdentityLineSpacing,
                textRight - IdentityLeft, IdentityLineHeight);
            Place(occultRank, IdentityLeft, IdentityTop + 2f * IdentityLineSpacing,
                textRight - IdentityLeft, IdentityLineHeight);
            // Both centers sit on the two-notch frame's native button centers (measured from the bottom right).
            Place(close, width - CloseCenterX - CloseSize / 2f, height - FooterCenterY - CloseSize / 2f, CloseSize, CloseSize);
            Place(cancel, width - CancelCenterX - CancelSize / 2f, height - FooterCenterY - CancelSize / 2f, CancelSize, CancelSize);
            Place(content, WindowGeometry.ContentLeft, WindowGeometry.ContentTop, WindowGeometry.ContentWidth, WindowGeometry.ContentHeight);
            if (scale != placedScale)
            {
                placedScale = scale;
                foreach (KeyValuePair<WindowBase, Rect> item in placements)
                {
                    item.Key.Area = snapped.ContainsKey(item.Key) ? Snap(Scaled(item.Value)) : Scaled(item.Value);
                }
            }
            if (header != null)
            {
                header.RefreshBars();
            }
        }

        // Places what belongs to the page being shown; this runs on every page switch.
        private void LayoutActivePage()
        {
            content.Visible = ActiveSelection == null;
            if (page == JournalPages.Overview && overviewPage != null)
            {
                overviewPage.Layout();
            }
            if (grimoirePage != null)
            {
                grimoirePage.Layout();
            }
            if (perkPage != null)
            {
                perkPage.RefreshProgress();
            }
        }

        private Rect Scaled(Rect area)
        {
            return NativeLayouts.Box(area.TopLeft.x * scale, area.TopLeft.y * scale,
                area.Width * scale, area.Height * scale);
        }

        internal void Place(WindowBase control, float x, float y, float width, float height)
        {
            Rect area = NativeLayouts.Box(x, y, width, height);
            placements[control] = area;
            control.Area = Scaled(area);
        }

        // Like Place, with the scaled bounds rounded to whole pixels; used for wrapped paragraphs.
        internal void PlaceSnapped(WindowBase control, float x, float y, float width, float height)
        {
            Rect area = NativeLayouts.Box(x, y, width, height);
            placements[control] = area;
            snapped[control] = true;
            control.Area = Snap(Scaled(area));
        }

        private static Rect Snap(Rect area)
        {
            return NativeLayouts.Box((float)Math.Round(area.TopLeft.x), (float)Math.Round(area.TopLeft.y),
                Math.Max(1f, (float)Math.Round(area.Width)), Math.Max(1f, (float)Math.Round(area.Height)));
        }

        internal bool HasControl(uint id)
        {
            return NativeLayouts.Find<WindowBase>(root, id) != null;
        }

        internal T Control<T>(uint id) where T : WindowBase
        {
            return NativeLayouts.Require<T>(root, id);
        }

        internal Text Label(uint id, string caption, float x, float y, float width, float height)
        {
            Text label = Control<Text>(id);
            label.Caption = caption;
            Place(label, x, y, width, height);
            return label;
        }

        internal Button Button(uint id, string caption, float x, float y, float width, float height, JournalAction action)
        {
            Button button = Control<Button>(id);
            button.Caption = caption;
            button.ButtonType = Sims3.UI.Button.ButtonTypes.Standard;
            button.Visible = true;
            Place(button, x, y, width, height);
            Bind(button, action);
            return button;
        }

        private void Bind(Button button, JournalAction action)
        {
            actions.Add(button, action);
            keyboardOrder.Add(button);
            button.Click += OnClick;
        }

        private void OnClick(WindowBase sender, UIButtonClickEventArgs args)
        {
            Button button = sender as Button;
            JournalAction action;
            if (button == null || !IsOpen || closing || perkConfirmation != null || Covered
                || !actions.TryGetValue(button, out action))
            {
                return;
            }
            args.Handled = true;
            UIManager.SetFocus(InputContext.kICKeyboard, root);
            SetKeyboardButton(button);
            Activate(action);
        }

        private void SetKeyboardButton(Button button)
        {
            if (keyboardButton != null && !keyboardButton.Disposed)
            {
                keyboardButton.Highlighted = false;
            }
            keyboardButton = button;
            if (keyboardButton != null)
            {
                keyboardButton.Highlighted = true;
            }
        }

        private static bool IsNavigable(Button button)
        {
            return button != null && !button.Disposed && button.Visible && button.Enabled;
        }

        private void OnTrigger(WindowBase sender, UITriggerEventArgs args)
        {
            if (!IsOpen || closing || perkConfirmation != null || Covered)
            {
                return;
            }
            switch (args.TriggerCode)
            {
                case CancelTrigger:
                    args.Handled = true;
                    Close(CloseMode.Cancel, true);
                    break;
                case AcceptTrigger:
                    if (IsNavigable(keyboardButton))
                    {
                        args.Handled = true;
                        keyActivation = true;
                        try
                        {
                            Activate(actions[keyboardButton]);
                        }
                        finally
                        {
                            keyActivation = false;
                        }
                    }
                    break;
                case NextTrigger:
                    args.Handled = true;
                    MoveKeyboard(1);
                    break;
                case PreviousTrigger:
                case AlternatePreviousTrigger:
                    args.Handled = true;
                    MoveKeyboard(-1);
                    break;
            }
        }

        private void Activate(JournalAction action)
        {
            try
            {
                action();
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.Report("Handling Supernatural Journal input", error);
                Abandon();
            }
        }

        private void MoveKeyboard(int direction)
        {
            int index = keyboardOrder.IndexOf(keyboardButton);
            int count = keyboardOrder.Count;
            for (int i = 0; i < count; i++)
            {
                index = (index + direction + count) % count;
                Button button = keyboardOrder[index];
                if (IsNavigable(button))
                {
                    SetKeyboardButton(button);
                    return;
                }
            }
        }

        private void Subscribe()
        {
            subscribed = true;
            model.SimChanged += OnSimChanged;
            model.SimNameChanged += OnSimChanged;
            model.SimAppearanceChanged += OnSimChanged;
            model.OccultUpdated += OnOccultUpdated;
            model.SkillChanged += OnSkillChanged;
            model.MinuteChanged += OnMinuteChanged;
            model.SimBuffsChanged += OnBuffsChanged;
            if (header != null)
            {
                model.SimMotivesChanged += OnMotivesChanged;
                model.MagicMotiveChanged += OnMagicMotiveChanged;
            }
        }

        private void Unsubscribe()
        {
            if (subscribed)
            {
                subscribed = false;
                model.SimChanged -= OnSimChanged;
                model.SimNameChanged -= OnSimChanged;
                model.SimAppearanceChanged -= OnSimChanged;
                model.OccultUpdated -= OnOccultUpdated;
                model.SkillChanged -= OnSkillChanged;
                model.MinuteChanged -= OnMinuteChanged;
                model.SimBuffsChanged -= OnBuffsChanged;
                if (header != null)
                {
                    model.SimMotivesChanged -= OnMotivesChanged;
                    model.MagicMotiveChanged -= OnMagicMotiveChanged;
                }
            }
        }

        private void OnSimChanged(ObjectGuid id)
        {
            UpdateFromHud();
        }

        private void OnOccultUpdated()
        {
            UpdateFromHud();
        }

        private void UpdateFromHud()
        {
            if (!IsOpen || closing)
            {
                return;
            }
            try
            {
                SimIdentity next = SimIdentity.Read(model);
                if (next == null)
                {
                    Abandon();
                    return;
                }
                RefreshSim(next);
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.Report("Refreshing the selected Sim", error);
                Abandon();
            }
        }

        private void RefreshSim(SimIdentity next)
        {
            OccultTypes types = next.Occults;
            bool changed = identity == null || identity.Id != next.Id;
            if (changed)
            {
                DismissPerkConfirmation();
            }
            if (!JournalPages.IsAvailable(JournalPages.Perks, types))
            {
                selection = null;
            }
            else if (changed || selection == null || selection.SimDescriptionId != next.SimDescriptionId)
            {
                selection = new PowerSelection(next.SimDescriptionId, WitchProgressSource.Read(next), WitchTree.Tree);
            }
            else
            {
                selection.Update(WitchProgressSource.Read(next));
            }
            if (selection != null)
            {
                selection.SimName = next.FirstName;
            }

            identity = next;
            if (changed && grimoirePage != null)
            {
                grimoirePage.Reset();
#if ARCANA_GRIMOIRE_TEST
                GrimoireTestData.Apply(grimoirePage);
#endif
            }
            RefreshHeader();
            simName.Caption = identity.Name;
            RefreshOccultRank();
            portraitImage.Image = identity.Portrait(model);
            portrait.Visible = portraitImage.Image != null;
            portrait.Invalidate();

            float left = TabLeft;
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].Visible = JournalPages.IsAvailable(i, types);
                if (tabs[i].Visible)
                {
                    float width = TabWidth(JournalPages.All[i].Title);
                    Place(tabs[i], left, TabTop, width, TabHeight);
                    left += width + TabGap;
                }
            }

            if (!JournalPages.IsAvailable(page, types))
            {
                page = JournalPages.Overview;
                SetKeyboardButton(tabs[page]);
            }
            SelectPage(page);
        }

        // The rank text is its own label below the Sim name, keeping name and rank independent.
        // Witch ranks follow the saved progression; the other occult progression ladders are not implemented yet.
        private void RefreshOccultRank()
        {
            string caption = OccultRankLabel.For(identity.Occults, selection);
            occultRank.Caption = caption;
            occultRank.Visible = caption.Length != 0;
        }

        // The panel is informative only, so a failure is reported once and hides it instead of closing the journal.
        private void RefreshHeader()
        {
            if (header == null)
            {
                return;
            }
            try
            {
                header.Refresh(identity, model, selection);
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                if (!headerReported)
                {
                    headerReported = true;
                    JournalBootstrap.Report("Refreshing the journal status header", error);
                }
                header.Refresh(null, model, null);
            }
        }

        private void OnMotivesChanged(SimInfo info)
        {
            UpdateHeaderFromHud();
        }

        private void OnBuffsChanged(SimInfo info)
        {
            if (IsOpen && !closing)
            {
                RefreshOverview();
            }
        }

        private void OnMagicMotiveChanged()
        {
            UpdateHeaderFromHud();
        }

        private void OnSkillChanged()
        {
            RefreshProgression();
            UpdateHeaderFromHud();
        }

        // Skill changes are only reported once a skill is visible (level 1), so the minute tick also looks for new experience.
        private void OnMinuteChanged()
        {
            if (RefreshProgression())
            {
                UpdateHeaderFromHud();
            }
            else if (IsOpen && page == JournalPages.Overview && selection != null)
            {
                RefreshOverview();
            }
        }

        // Returns true when the native skill or Witch progression changes the Journal display.
        private bool RefreshProgression()
        {
            if (!IsOpen || identity == null || selection == null)
            {
                return false;
            }
            try
            {
                if (identity.SimDescriptionId == 0UL)
                {
                    SimIdentity refreshed = SimIdentity.Read(model);
                    if (refreshed == null || refreshed.Id != identity.Id || refreshed.SimDescriptionId == 0UL)
                    {
                        return false;
                    }
                    RefreshSim(refreshed);
                    return true;
                }

                PowerProgress progress = WitchProgressSource.Read(identity, selection);
                if (progress == null || !selection.Update(progress))
                {
                    return false;
                }
                RefreshOccultRank();
                if (ActiveSelection != null)
                {
                    perkPage.Show(ActiveSelection);
                }
                RefreshOverview();
                return true;
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.Report("Refreshing Witch progression", error);
                Abandon();
                return false;
            }
        }

        private void UpdateHeaderFromHud()
        {
            if (!IsOpen || header == null)
            {
                return;
            }
            bool shown = header.Visible;
            RefreshHeader();
            if (shown != header.Visible)
            {
                UpdateLayout();
            }
            RefreshOverview();
        }

        private static float TabWidth(string title)
        {
            return Math.Max(137f, 24f + 7f * title.Length);
        }

        // User navigation alone prompts. RefreshSim can still redraw the
        // current page or switch away from a page the new Sim cannot open.
        private void RequestPage(int index)
        {
            if (index == page || !tabs[index].Visible || perkConfirmation != null)
            {
                return;
            }

            PowerSelection pending = ActiveSelection;
            if (pending == null || pending.PendingCount == 0)
            {
                SelectPage(index);
                return;
            }

            if (!ModalDialog.EnableModalDialogs)
            {
                SetKeyboardButton(tabs[page]);
                return;
            }

            PerkPurchaseDialog prompt = null;
            try
            {
                prompt = new PerkPurchaseDialog(delegate(PerkPurchaseChoice choice)
                {
                    // Resolve on the next UI task, after the native input
                    // event (or shutdown notification) has returned.
                    JournalBootstrap.RunLater(delegate { ResolvePerkNavigation(prompt, index, pending, choice); });
                });
                perkConfirmation = prompt;
                if (!prompt.Open())
                {
                    DismissPerkConfirmation();
                    SetKeyboardButton(tabs[page]);
                }
            }
            catch (ResetException)
            {
                DismissPerkConfirmation();
                throw;
            }
            catch (Exception error)
            {
                DismissPerkConfirmation();
                JournalBootstrap.Report("Opening the perk purchase confirmation", error);
                SetKeyboardButton(tabs[page]);
            }
        }

        private void ResolvePerkNavigation(PerkPurchaseDialog prompt, int index, PowerSelection pending,
            PerkPurchaseChoice choice)
        {
            if (perkConfirmation != prompt)
            {
                return;
            }
            perkConfirmation = null;
            if (!IsOpen || closing || ActiveSelection != pending)
            {
                return;
            }
            if (choice == PerkPurchaseChoice.Dismissed || !tabs[index].Visible)
            {
                SetKeyboardButton(tabs[page]);
                return;
            }

            try
            {
                if (choice == PerkPurchaseChoice.Yes)
                {
                    if (!perkPage.Commit(true))
                    {
                        SetKeyboardButton(tabs[page]);
                        return;
                    }
                    JournalSounds.Play(JournalSounds.Purchase);
                }
                else
                {
                    perkPage.Discard();
                    JournalBootstrap.RefreshPerkPoints();
                }
                SelectPage(index);
                SetKeyboardButton(tabs[page]);
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.Report("Resolving the perk purchase confirmation", error);
                SetKeyboardButton(tabs[page]);
            }
        }

        private void DismissPerkConfirmation()
        {
            PerkPurchaseDialog prompt = perkConfirmation;
            perkConfirmation = null;
            if (prompt != null)
            {
                prompt.Dismiss();
            }
        }

        private void SelectPage(int index)
        {
            if (!tabs[index].Visible)
            {
                return;
            }
            page = index;
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].Selected = i == page;
            }
            // Perks shows the second notch and Cancel button for staged picks.
            bool twoNotch = page == JournalPages.Perks;
            cancel.Visible = twoNotch;
            if (frames != null)
            {
                frames.Show(twoNotch);
            }
            bool showDashboard = page == JournalPages.Overview;
            if (perkPage != null)
            {
                perkPage.Show(ActiveSelection);
            }
            if (overviewPage != null)
            {
                overviewPage.Show(showDashboard);
                if (showDashboard)
                {
                    RefreshOverview();
                }
            }
            if (grimoirePage != null)
            {
                grimoirePage.Show(page == JournalPages.Grimoire);
            }
            UpdateLayout();
            if (!IsNavigable(keyboardButton))
            {
                SetKeyboardButton(tabs[page]);
            }
        }

        private void OnKnowledgeChanged(object sender, EventArgs e)
        {
            RefreshOverview();
        }

        // Rebuilds only the data behind the dashboard; the controls are reused. A failure leaves the page empty and is reported once.
        private void RefreshOverview()
        {
            if (overviewPage == null || page != JournalPages.Overview)
            {
                return;
            }
            try
            {
                overviewPage.Refresh(OverviewProviders.Build(new OverviewContext(identity, selection,
                    header == null ? null : header.PowerField, perkPage, grimoirePage)));
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                if (!overviewReported)
                {
                    overviewReported = true;
                    JournalBootstrap.Report("Refreshing the Overview", error);
                }
                overviewPage.Refresh(null);
            }
        }

        private void OnWindowShutdown(WindowBase sender, UIEventArgs args)
        {
            gone = true;
            Unsubscribe();
            Abandon();
        }

        private enum CloseMode
        {
            Confirm,
            Cancel,
            Abandon
        }

        // The checkmark: buys what is staged, plays the purchase sound (or the close sound), and closes.
        private void RequestClose()
        {
            Close(CloseMode.Confirm, keyActivation);
        }

        // The X, and Escape: drops what is staged, plays the close sound and closes. Nothing is bought and no purchase effect plays.
        private void RequestCancel()
        {
            Close(CloseMode.Cancel, keyActivation);
        }

        // Closes without sounds, effects or purchases: for errors and for the game taking the window away.
        private void Abandon()
        {
            Close(CloseMode.Abandon, false);
        }

        // Closing the Journal goes through here once. The checkmark buys what was staged; the other close actions drop it. Then the window
        // is hidden and the game is handed back at once, whatever happened before; the window itself is disposed on the next tick.
        private void Close(CloseMode mode, bool keyboard)
        {
            if (closing)
            {
                return;
            }
            closing = true;

            try
            {
                bool bought = false;
                if (mode == CloseMode.Confirm)
                {
                    bought = perkPage != null && perkPage.Commit(true);
                }
                else if (perkPage != null)
                {
                    perkPage.Discard();
                }
                if (mode != CloseMode.Abandon)
                {
                    if (keyboard)
                    {
                        JournalSounds.Play(JournalSounds.Key);
                    }
                    JournalSounds.Play(bought ? JournalSounds.Purchase : JournalSounds.Close);
                }
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.Report("Closing Supernatural Journal", error);
            }
            finally
            {
                Dismiss();
            }
        }

        private void Dismiss()
        {
            try
            {
                DismissPerkConfirmation();
                if (!gone && root != null && !root.Disposed)
                {
                    root.Visible = false;
                }
                if (modal != null)
                {
                    modal.End();
                }
            }
            finally
            {
                try
                {
                    if (perkPage != null)
                    {
                        perkPage.StopAnimations();
                    }
                    if (header != null)
                    {
                        header.StopAnimations();
                    }
                    JournalBootstrap.RefreshPerkPoints();
                }
                finally
                {
                    JournalBootstrap.RunLater(delegate { JournalBootstrap.Close(this); });
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            Unsubscribe();

            try
            {
                DismissPerkConfirmation();
                if (modal != null)
                {
                    modal.End();
                }
                if (overviewPage != null)
                {
                    overviewPage.Release();
                }
                if (grimoirePage != null)
                {
                    grimoirePage.EntriesChanged -= OnKnowledgeChanged;
                    grimoirePage.Release();
                }
                if (perkPage != null)
                {
                    perkPage.Release();
                }
                if (header != null)
                {
                    header.StopAnimations();
                }
                if (viewport != null && !viewport.Disposed)
                {
                    viewport.AreaChange -= OnViewportChanged;
                }
                if (!gone && root != null && !root.Disposed)
                {
                    if (watchingShutdown)
                    {
                        NativeUi.WatchShutdown(root, OnWindowShutdown, false);
                        watchingShutdown = false;
                    }
                    root.TriggerDown -= OnTrigger;
                    root.Visible = false;
                }
                foreach (Button button in keyboardOrder)
                {
                    if (!button.Disposed)
                    {
                        button.Click -= OnClick;
                    }
                }
            }
            finally
            {
                NativeLayouts.Release(layout, gone);
            }
        }
    }
}
