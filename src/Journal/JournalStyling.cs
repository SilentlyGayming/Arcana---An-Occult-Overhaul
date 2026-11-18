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

        internal static JournalFrames ApplyDialog(Dialog journal)
        {
            JournalFrames frames = new JournalFrames(journal);
            UseDonor("HUDCollectionJournalDialog", 1, delegate(WindowBase donor)
            {
                frames.StyleCollection(donor);
                StyleClose(journal, donor);
            });
            frames.HasTwoButtonFace = StyleTwoButtonFrame(frames.TwoButtonFace);
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
                    StyleCounters(journal, Frame(sample.Drawable));
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

        private static bool StyleTwoButtonFrame(Window footer)
        {
            StdDrawable target = Frame(footer.Drawable);
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
                    footer.ShadeColor = donor.ShadeColor;
                    footer.Invalidate();
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

        internal static void CopyFace(StdDrawable target, StdDrawable source)
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

    // Grimoire categories use the native pressed face to show which filter is active.
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

        private readonly Button[] buttons;
        private readonly UIImage[][] original;
        private readonly UIImage[] pressed;

        internal TabFaces(Button[] buttons)
        {
            this.buttons = buttons;
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

                for (int s = 0; s < Replaced.Length; s++)
                {
                    UIImage keep = original[i][s] ?? original[i][0];
                    face[Replaced[s]] = i == active ? pressed[i] : keep;
                }
                buttons[i].Invalidate();
            }
        }
    }

    // The Collection frame owns the silhouette on every page. Only the lower-right corner can switch to the two-notch artwork.
    internal sealed class JournalFrames
    {
        private const float FooterWidth = 144f;
        private const float FooterHeight = 86f;

        private readonly Dialog journal;
        private readonly Window upper;
        private readonly Window lowerLeft;
        private readonly Window footer;
        private readonly Window upperFace;
        private readonly Window lowerLeftFace;
        private readonly Window singleButtonFace;
        internal readonly Window TwoButtonFace;
        internal bool HasTwoButtonFace;

        internal JournalFrames(Dialog journal)
        {
            this.journal = journal;
            upper = Clip(NativeLayouts.FrameUpper);
            lowerLeft = Clip(NativeLayouts.FrameLowerLeft);
            footer = Clip(NativeLayouts.FrameFooter);
            upperFace = Face(upper, NativeLayouts.FrameUpper + 1);
            lowerLeftFace = Face(lowerLeft, NativeLayouts.FrameLowerLeft + 1);
            singleButtonFace = Face(footer, NativeLayouts.FrameFooter + 1);
            TwoButtonFace = Face(footer, NativeLayouts.FrameFooter + 2);
        }

        private Window Clip(uint id)
        {
            Window clip = NativeLayouts.Require<Window>(journal, id);
            clip.Clip = true;
            clip.IgnoreMouse = true;
            return clip;
        }

        private static Window Face(Window clip, uint id)
        {
            Window face = NativeLayouts.Require<Window>(clip, id);
            face.IgnoreMouse = true;
            return face;
        }

        internal void StyleCollection(WindowBase donor)
        {
            StdDrawable face = JournalTheme.DialogFrame(donor);
            Apply(upperFace, face, donor.ShadeColor);
            Apply(lowerLeftFace, face, donor.ShadeColor);
            Apply(singleButtonFace, face, donor.ShadeColor);
        }

        private static void Apply(Window target, StdDrawable face, Color shade)
        {
            JournalTheme.CopyFace(JournalTheme.Frame(target.Drawable), face);
            target.ShadeColor = shade;
            target.Invalidate();
        }

        internal void Layout(float scale)
        {
            float width = journal.Area.Width;
            float height = journal.Area.Height;
            float x = width - (float)Math.Round(FooterWidth * scale);
            float y = height - (float)Math.Round(FooterHeight * scale);
            // All copies draw at the same pixel coordinates so the clip boundaries meet without a seam.
            upper.Area = new Rect(0f, 0f, width, y);
            lowerLeft.Area = new Rect(0f, y, x, height);
            footer.Area = new Rect(x, y, width, height);
            upperFace.Area = NativeLayouts.Box(0f, 0f, width, height);
            lowerLeftFace.Area = NativeLayouts.Box(0f, -y, width, height);
            singleButtonFace.Area = NativeLayouts.Box(-x, -y, width, height);
            TwoButtonFace.Area = singleButtonFace.Area;
        }

        internal void Show(bool wantTwoNotch)
        {
            bool twoButtons = wantTwoNotch && HasTwoButtonFace;
            singleButtonFace.Visible = !twoButtons;
            TwoButtonFace.Visible = twoButtons;
        }
    }
}
