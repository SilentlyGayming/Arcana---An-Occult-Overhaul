using System;
using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    internal delegate void JournalAction();

    internal sealed class JournalWindow : IDisposable
    {
        private const float IdentityLeft = 110f;
        private const float IdentityTop = 31f;
        private const float IdentityLineHeight = 24f;
        private const float IdentityLineSpacing = 25f;
        private const float TabTop = 107f;
        private const float TabLeft = 41f;
        private const float TabGap = 9f;
        private const float TabHeight = 43f;
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
        private bool Covered { get { return modal != null && modal.Covered; } }
        internal WindowBase RootWindow { get { return root; } }
        internal Sim CurrentActor { get { return identity == null || identity.Description == null ? null : identity.Description.CreatedSim; } }
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
            SpellKnowledge.Changed += OnSpellLearned;
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

        private static float GetFitScale(float width, float height)
        {
            // Returns one scale for the window so switching pages never rescales it.
            Rect screen = UIManager.ScreenArea;
            return Math.Min(NativeLayouts.UIScale(), Math.Min(Math.Max(1f, screen.Width - 30f) / width,
                Math.Max(1f, screen.Height - 72f) / height));
        }

        private void UpdateLayout()
        {
            PlaceWindow();
            LayoutActivePage();
        }

        private void PlaceWindow()
        {
            // Sizes and centers the window and places the header, footer and content panel.
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

            root.Area = NativeLayouts.Box(0, 0, (float)Math.Round(width * scale), (float)Math.Round(height * scale));
            root.CenterInParent();
            float top = Math.Max(4f, (float)Math.Round(root.Area.TopLeft.y - 24f * scale));
            root.Position = new Vector2((float)Math.Round(root.Area.TopLeft.x), top);
            frames.Layout(scale);
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

        private void LayoutActivePage()
        {
            // Places the controls of the page being shown.
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

        internal void PlaceSnapped(WindowBase control, float x, float y, float width, float height)
        {
            // Places a control like Place but rounds its scaled bounds to whole pixels.
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
            if (changed && grimoirePage != null) SpellKnowledge.Populate(grimoirePage, identity);
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

        private void RefreshOccultRank()
        {
            // Refreshes the rank label shown below the Sim name.
            string caption = OccultRankLabel.For(identity.Occults, selection);
            occultRank.Caption = caption;
            occultRank.Visible = caption.Length != 0;
        }

        private void RefreshHeader()
        {
            // Refreshes the status panel, hiding it after a reported failure instead of closing the Journal.
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
                RefreshGrimoireCosts();
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

        private void OnMinuteChanged()
        {
            // Checks for new experience on the minute tick because skill changes below level 1 raise no event.
            if (RefreshProgression())
            {
                UpdateHeaderFromHud();
            }
            else if (IsOpen && page == JournalPages.Overview && selection != null)
            {
                RefreshOverview();
            }
        }

        private bool RefreshProgression()
        {
            // Returns true when the native skill or Witch progression changes the Journal display.
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
            RefreshGrimoireCosts();
        }

        private void RefreshGrimoireCosts()
        {
            // Recomputes the shown Grimoire costs and their affordability colors when power, perks or conditions change.
            if (grimoirePage == null || page != JournalPages.Grimoire || identity == null)
            {
                return;
            }
            SpellKnowledge.Populate(grimoirePage, identity);
            grimoirePage.RefreshAffordability();
        }

        private static float TabWidth(string title)
        {
            return Math.Max(137f, 24f + 7f * title.Length);
        }

        private void RequestPage(int index)
        {
            // Handles user navigation to a page.
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
                if (page == JournalPages.Grimoire) SpellKnowledge.Populate(grimoirePage, identity);
                grimoirePage.Show(page == JournalPages.Grimoire);
            }
            UpdateLayout();
            if (!IsNavigable(keyboardButton))
            {
                SetKeyboardButton(tabs[page]);
            }
        }

        private void OnSpellLearned(ulong simId)
        {
            if (identity == null || identity.SimDescriptionId != simId || grimoirePage == null) return;
            SpellKnowledge.Populate(grimoirePage, identity);
            RefreshOverview();
        }

        private void OnKnowledgeChanged(object sender, EventArgs e)
        {
            RefreshOverview();
        }

        private void RefreshOverview()
        {
            // Rebuilds the dashboard data while reusing its controls.
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

        private void RequestClose()
        {
            // Buys staged perks, plays the matching sound and closes the Journal.
            Close(CloseMode.Confirm, keyActivation);
        }

        private void RequestCancel()
        {
            // Drops staged perks, plays the close sound and closes the Journal.
            Close(CloseMode.Cancel, keyActivation);
        }

        private void Abandon()
        {
            // Closes without sounds, effects or purchases when an error occurs or the game removes the window.
            Close(CloseMode.Abandon, false);
        }

        private void Close(CloseMode mode, bool keyboard)
        {
            // Closes the Journal through the single shared close path.
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
                    SpellKnowledge.Changed -= OnSpellLearned;
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
