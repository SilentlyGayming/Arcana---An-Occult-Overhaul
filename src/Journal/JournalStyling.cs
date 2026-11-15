using Sims3.SimIFace;
using Sims3.UI;
using System;

namespace SupernaturalExpanded.Journal
{
    internal static class JournalTheme
    {

        // Falls back to the layout's stock artwork and colors when the active UI skin cannot be read.
        internal static void ApplyHud(Button book, Window glyph, Button familyTree, WindowBase panel)
        {
            try
            {
                CopyFace(Frame(book.Drawable), Frame(familyTree.Drawable));
                Window zodiac = NativeLayouts.Find<Window>(panel, NativeLayouts.Zodiac);
                if (zodiac != null && !zodiac.Disposed)
                {
                    Color shade = zodiac.ShadeColor;
                    if (shade.Alpha != 0 && shade.ARGB != 0xFF002D74 && shade.ARGB != 0xFFFFFFFF)
                    {
                        glyph.ShadeColor = shade;
                    }
                }
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Reading the active UI skin for the HUD button", error);
            }
        }

        // Returns the two shells, so the window can show the single-notch one on Overview and Grimoire and the two-notch one on Perks.
        internal static JournalFrames ApplyDialog(Dialog journal, Button[] tabs, Button[] grimoireTabs)
        {
            JournalFrames frames = new JournalFrames(journal);
            UseDonor("HUDCollectionJournalDialog", 1, delegate(WindowBase donor)
            {
                CopyFace(DialogFrame(journal), DialogFrame(donor));
                journal.ShadeColor = donor.ShadeColor;
                StyleInnerPanels(journal, donor);
                StyleClose(journal, donor);
            });
            // The Collection Journal donor supplies the typography, panel textures and the single-notch frame, which Overview and Grimoire show;
            // the frame comes with its own slicing, as the two-notch one does.
            // Perks shows the two-notch dialog built for both buttons; its slicing must travel with its artwork.
            frames.CaptureSingleNotch();
            if (StyleTwoButtonFrame(journal))
            {
                frames.CaptureTwoNotch();
            }
            // The party picker's Cancel face does not render a circular backdrop outside its dialog, so Cancel copies the checkmark's frame and keeps its own X glyph.
            StyleCancel(journal);
            UseDonor("ThreeButtonDialog", 0x10000, delegate(WindowBase donor)
            {
                Text message = NativeLayouts.Find<Text>(donor, 1);
                if (message != null && message.TextColor.Alpha != 0)
                {
                    Color color = message.TextColor;
                    for (uint i = 0; ; i++)
                    {
                        WindowBase child = journal.GetChildByIndex(i);
                        if (child == null)
                        {
                            break;
                        }
                        Text label = child as Text;
                        if (label != null)
                        {
                            label.TextColor = color;
                        }
                    }
                }

                Button sample = NativeLayouts.Find<Button>(donor, 2);
                if (sample != null)
                {
                    StdDrawable face = Frame(sample.Drawable);
                    foreach (Button tab in tabs)
                    {
                        CopyFace(Frame(tab.Drawable), face);
                    }
                    // Grimoire buttons deliberately use the native Simology face,
                    // not this pale dialog face (which blends into the page).
                    StyleCounters(journal, face);
                }
            });
            // Unlike native button captions, the Grimoire labels remain fixed
            // regardless of button state and use dark ink for light/blue buttons.
            for (uint i = 0; i < 4; i++)
            {
                Text caption = NativeLayouts.Find<Text>(journal, NativeLayouts.GrimoireFirstCaption + i);
                if (caption != null)
                {
                    caption.TextColor = new Color(0xFF0B246E);
                }
            }
            // Native table header and description text must remain navy regardless of any white
            // typography supplied by a UI retexture's dialog button state.
            uint[] headerIds = {
                NativeLayouts.GrimoireListTitle,
                NativeLayouts.GrimoireTierHeader,
                NativeLayouts.GrimoireCostHeader,
                NativeLayouts.GrimoireDescriptionName,
                NativeLayouts.GrimoireDescriptionText
            };
            foreach (uint id in headerIds)
            {
                Text heading = NativeLayouts.Find<Text>(journal, id);
                if (heading != null)
                {
                    heading.TextColor = new Color(0xFF0B246E);
                }
            }
            // UI skins may supply white text colors for unrelated native dialogs.
            // The Grimoire list/header all sit on a light EA-colored background,
            // so restore consistent dark ink for every independent text control.
            Text empty = NativeLayouts.Find<Text>(journal, NativeLayouts.GrimoireEmptyList);
            if (empty != null) empty.TextColor = new Color(0xFF0B246E);
            uint[] mutedIds = { NativeLayouts.GrimoireDescriptionMeta, NativeLayouts.GrimoireDescriptionPrompt };
            foreach (uint id in mutedIds)
            {
                Text note = NativeLayouts.Find<Text>(journal, id);
                if (note != null) note.TextColor = new Color(0xFF5F78A3);
            }
            for (uint row = 0; row < GrimoirePage.VisibleRows; row++)
            {
                uint first = NativeLayouts.GrimoireFirstRow + row * 5;
                for (uint field = 2; field <= 4; field++)
                {
                    Text cell = NativeLayouts.Find<Text>(journal, first + field);
                    if (cell != null) cell.TextColor = new Color(0xFF0B246E);
                }
            }
            return frames;
        }

        // Gives the perk tooltip the stock tooltip's background, text colour and font, so it follows UI replacement mods; changes nothing when they cannot be read.
        internal static void ApplyTooltip(Window frame, Text title, Text tagline, Text body)
        {
            UseDonor("SimpleTextTooltip", 1, delegate(WindowBase donor)
            {
                StdDrawable source = DialogFrame(donor);
                StdDrawable target = Frame(frame.Drawable);
                if (source == null || target == null || source[DrawableBase.ControlStates.kNormal] == null)
                {
                    return;
                }

                CopyFace(target, source);
                frame.Invalidate();

                // Text colour and font only change together with the background they were designed for.
                Text sample = donor as Text;
                if (sample != null)
                {
                    Color color = sample.TextColor;
                    if (color.Alpha != 0)
                    {
                        title.TextColor = color;
                        body.TextColor = color;
                    }
                    uint font = sample.TextStyle;
                    if (font != 0)
                    {
                        tagline.TextStyle = font;
                        body.TextStyle = font;
                    }
                }
            });
        }

        // The Collection Journal's inset panel has no control ID. It is the largest plain Window
        // with a drawn face, so it is found by what it is rather than by its position in the layout.
        private static Window FindInsetPanel(WindowBase donor)
        {
            Rect bounds = donor.Area;
            Window best = null;
            float bestSize = 0f;
            for (uint i = 0; ; i++)
            {
                WindowBase child = donor.GetChildByIndex(i);
                if (child == null)
                {
                    break;
                }
                Window window = child as Window;
                if (window == null)
                {
                    continue;
                }
                StdDrawable face = Frame(window.Drawable);
                if (face == null || face[DrawableBase.ControlStates.kNormal] == null)
                {
                    continue;
                }
                Rect area = window.Area;
                float size = area.Width * area.Height;
                if (area.Width >= bounds.Width * 0.5f && area.Height >= bounds.Height * 0.4f && size > bestSize)
                {
                    best = window;
                    bestSize = size;
                }
            }
            return best;
        }

        private static void StyleInnerPanels(Dialog journal, WindowBase donor)
        {
            Window sample = FindInsetPanel(donor);
            StdDrawable face = sample == null ? null : Frame(sample.Drawable);
            if (face == null)
            {
                return;
            }

            // The Grimoire list and description keep the stock list outline named in the layout, like the Lifetime Rewards list.
            StyleFace(NativeLayouts.Find<Window>(journal, NativeLayouts.StatusPanel), face);
        }

        private static void StyleClose(Dialog journal, WindowBase donor)
        {
            Button sample = NativeLayouts.Find<Button>(donor, NativeLayouts.CollectionOkay);
            Button close = NativeLayouts.Find<Button>(journal, NativeLayouts.Close);
            if (sample == null || close == null)
            {
                return;
            }

            CopyFace(Frame(close.Drawable), Frame(sample.Drawable));
            if (sample.ShadeColor.Alpha != 0)
            {
                close.ShadeColor = sample.ShadeColor;
            }

            // Reuses the native checkmark face so a UI skin's replacement also reaches the '?' disc.
            Window help = NativeLayouts.Find<Window>(journal,
                WitchTree.Tree.Id(PageControl.Page, (int)PageSlot.PointsHelpOuter));
            if (help != null && !help.Disposed)
            {
                CopyFace(Frame(help.Drawable), Frame(close.Drawable));
                help.ShadeColor = close.ShadeColor;
                help.Invalidate();
            }
        }

        // Native two-notch dialog layouts (type 025C95B6), all sharing image D58F144D5B0FE27A with a 56x56 Accept at (-69,-69) and a 29x29 Cancel
        // at (-94,-56). The 686x636 one comes first because it needs the least resizing; the single-notch frame stays if none loads.
        private static readonly ulong[] TwoButtonLayouts =
        {
            0x54D0A9831E5754E7UL,
            0xB73E807A1250DC6FUL,
            0xBD8616686C83937BUL
        };
        private static readonly int[] TwoButtonExports = { 1, 1, 0x10000 };

        private static bool StyleTwoButtonFrame(Dialog journal)
        {
            StdDrawable target = DialogFrame(journal);
            if (target == null)
            {
                return false;
            }

            for (int i = 0; i < TwoButtonLayouts.Length; i++)
            {
                Layout layout = null;
                try
                {
                    layout = UIManager.LoadLayout(new ResourceKey(TwoButtonLayouts[i], 0x025C95B6, 0));
                    if (!NativeUi.HasLayout(layout))
                    {
                        continue;
                    }
                    WindowBase donor = layout.GetWindowByExportID(TwoButtonExports[i]);
                    if (donor == null || donor.Disposed)
                    {
                        continue;
                    }
                    StdDrawable source = DialogFrame(donor);
                    if (source == null || source[DrawableBase.ControlStates.kNormal] == null)
                    {
                        continue;
                    }

                    // The donor's ScaleType, ScaleAmount and ScaleArea travel with its image; the Collection frame's cuts would stretch the recesses.
                    CopyFace(target, source);
                    journal.Invalidate();
                    return true;
                }
                catch (ResetException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    // A missing donor (older installation or custom UI package) moves on to the next layout; the single-notch frame is the last resort.
                    JournalBootstrap.ReportOnce("Loading a two-button frame donor", error);
                }
                finally
                {
                    NativeLayouts.Release(layout, false);
                }
            }
            return false;
        }

        private static void StyleCancel(Dialog journal)
        {
            Button checkmark = NativeLayouts.Find<Button>(journal, NativeLayouts.Close);
            Button cancel = NativeLayouts.Find<Button>(journal, NativeLayouts.Cancel);
            if (checkmark == null || cancel == null)
            {
                return;
            }

            // The checkmark's circular face works in the current menu and follows the active UI skin.
            // Only its frame is copied, not the checkmark glyph: Cancel retains its original X glyph.
            CopyFace(Frame(cancel.Drawable), Frame(checkmark.Drawable));
            cancel.ShadeColor = checkmark.ShadeColor;
            cancel.Invalidate();
        }

        private static void StyleCounters(Dialog journal, StdDrawable face)
        {
            OccultTree tree = WitchTree.Tree;
            for (int i = 0; i < tree.Perks.Length; i++)
            {
                StyleFace(NativeLayouts.Find<Window>(journal, tree.Id(PageControl.CounterPlate, i)), face);
            }
        }

        private static void StyleFace(Window target, StdDrawable face)
        {
            if (target != null)
            {
                CopyFace(Frame(target.Drawable), face);
                target.Invalidate();
            }
        }

        // An unavailable donor layout leaves the stock style in place.
        private static void UseDonor(string name, int export, Action<WindowBase> apply)
        {
            try
            {
                Layout layout = UIManager.LoadLayout(NativeLayouts.Key(name));
                try
                {
                    if (NativeUi.HasLayout(layout))
                    {
                        WindowBase donor = layout.GetWindowByExportID(export);
                        if (donor != null && !donor.Disposed)
                        {
                            apply(donor);
                        }
                    }
                }
                finally
                {
                    NativeLayouts.Release(layout, false);
                }
            }
            catch (ResetException)
            {
                throw;
            }
            catch (Exception error)
            {
                JournalBootstrap.ReportOnce("Loading native donor layout " + name, error);
            }
        }

        internal static StdDrawable DialogFrame(WindowBase dialog)
        {
            return Frame(NativeUi.DialogDrawable(dialog));
        }

        internal static StdDrawable Frame(DrawableBase drawable)
        {
            MultiDrawable layers = drawable as MultiDrawable;
            return layers == null ? drawable as StdDrawable : layers[0] as StdDrawable;
        }

        private static void CopyFace(StdDrawable target, StdDrawable source)
        {
            if (CopyImages(target, source))
            {
                target.ScaleType = source.ScaleType;
                target.ScaleAmount = source.ScaleAmount;
                target.ScaleArea = source.ScaleArea;
            }
        }

        private static bool CopyImages(StdDrawable target, StdDrawable source)
        {
            if (source == null || target == null || source[DrawableBase.ControlStates.kNormal] == null)
            {
                return false;
            }

            for (int i = 0; i < 8; i++)
            {
                DrawableBase.ControlStates state = (DrawableBase.ControlStates)i;
                UIImage image = source[state];
                if (image != null)
                {
                    target[state] = image;
                }
            }
            return true;
        }
    }

    // The Grimoire categories keep the native faces; the three main tabs draw a clear face over their permanent shells (tinted by JournalWindow).
    internal sealed class TabFaces
    {
        private static readonly DrawableBase.ControlStates[] Replaced =
        {
            DrawableBase.ControlStates.kNormal,
            DrawableBase.ControlStates.kHighlighted,
            DrawableBase.ControlStates.kCheckedNormal,
            DrawableBase.ControlStates.kCheckedHighlighted,
            DrawableBase.ControlStates.kCheckedActive
        };

        private static readonly Color SelectedTint = new Color(0xFFC4D0DE);
        private static readonly Color Untinted = new Color(0xFFFFFFFF);

        private readonly Button[] buttons;
        private readonly bool plainFace;
        private readonly UIImage open;
        private readonly UIImage[][] original;
        private readonly UIImage[] pressed;

        internal TabFaces(Button[] buttons) : this(buttons, false)
        {
        }

        // Without the packaged clear face, fall back to normal native button faces and hide the shells.
        internal TabFaces(Button[] buttons, UIImage open) : this(buttons, true)
        {
            this.open = open;
        }

        // Whether every main tab button draws a clear face over its corresponding shell.
        internal bool Open { get { return open != null; } }

        internal TabFaces(Button[] buttons, bool plainFace)
        {
            this.buttons = buttons;
            this.plainFace = plainFace;
            original = new UIImage[buttons.Length][];
            pressed = new UIImage[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                StdDrawable face = JournalTheme.Frame(buttons[i].Drawable);
                if (face == null)
                {
                    continue;
                }

                original[i] = new UIImage[Replaced.Length];
                for (int s = 0; s < Replaced.Length; s++)
                {
                    original[i][s] = face[Replaced[s]];
                }
                pressed[i] = face[DrawableBase.ControlStates.kActive];
            }
        }

        internal void Show(int active)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                StdDrawable face = JournalTheme.Frame(buttons[i].Drawable);
                if (face == null || original[i] == null || original[i][0] == null || pressed[i] == null)
                {
                    continue;
                }

                bool on = i == active;
                UIImage selected = plainFace ? original[i][0] : pressed[i];
                for (int s = 0; s < Replaced.Length; s++)
                {
                    UIImage keep = original[i][s] ?? original[i][0];
                    face[Replaced[s]] = open != null ? open : (on ? selected : keep);
                }
                // A pressed inactive tab must not flash its old rectangular button face on mouse-down.
                // Restore that native pressed image if the open tab texture is unavailable.
                face[DrawableBase.ControlStates.kActive] = open ?? pressed[i];
                if (plainFace && open == null)
                {
                    buttons[i].ShadeColor = on ? SelectedTint : Untinted;
                }
                buttons[i].Invalidate();
            }
        }
    }

    // The Journal's two native shells: the single-notch Collection frame (Overview, Grimoire) and the two-notch frame (Perks).
    // Each keeps its artwork with its own slicing and is copied back onto the dialog as a pair, so the window is never rebuilt or resized.
    internal sealed class JournalFrames
    {
        private const int StateCount = 8;

        private sealed class Face
        {
            internal readonly UIImage[] Images = new UIImage[StateCount];
            internal StdDrawable.ScalingType ScaleType;
            internal Vector2 ScaleAmount;
            internal Rect ScaleArea;
        }

        private readonly Dialog journal;
        private Face single;
        private Face twoNotch;
        private Face current;

        internal JournalFrames(Dialog journal)
        {
            this.journal = journal;
        }

        // The frame as it is now: the Collection Journal artwork with the layout's own slicing.
        internal void CaptureSingleNotch()
        {
            single = Capture();
            current = single;
        }

        // The frame as it is after the two-notch donor was copied onto it, with the donor's slicing.
        internal void CaptureTwoNotch()
        {
            twoNotch = Capture();
            current = twoNotch;
        }

        // Perks gets the two-notch shell when it loaded; every other page, and Perks without it, gets the single-notch one.
        internal void Show(bool wantTwoNotch)
        {
            Face face = wantTwoNotch && twoNotch != null ? twoNotch : single;
            if (face == null || face == current)
            {
                return;
            }
            StdDrawable target = JournalTheme.DialogFrame(journal);
            if (target == null)
            {
                return;
            }

            for (int i = 0; i < StateCount; i++)
            {
                if (face.Images[i] != null)
                {
                    target[(DrawableBase.ControlStates)i] = face.Images[i];
                }
            }
            target.ScaleType = face.ScaleType;
            target.ScaleAmount = face.ScaleAmount;
            target.ScaleArea = face.ScaleArea;
            current = face;
            journal.Invalidate();
        }

        private Face Capture()
        {
            StdDrawable source = JournalTheme.DialogFrame(journal);
            if (source == null || source[DrawableBase.ControlStates.kNormal] == null)
            {
                return null;
            }

            Face face = new Face();
            for (int i = 0; i < StateCount; i++)
            {
                face.Images[i] = source[(DrawableBase.ControlStates)i];
            }
            face.ScaleType = source.ScaleType;
            face.ScaleAmount = source.ScaleAmount;
            face.ScaleArea = source.ScaleArea;
            return face;
        }
    }
}
