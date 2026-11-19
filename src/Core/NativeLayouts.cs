using System;
using System.Reflection;
using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;

namespace SupernaturalExpanded.Journal
{
    public static class TinyUIFixForTS3Integration
    {
        public delegate float ScaleProvider();
        public static ScaleProvider getUIScale = delegate { return 1f; };
    }

    internal static class NativeLayouts
    {
        internal const string Journal = "SupernaturalExpandedJournalDialog_r82";
        internal const string HudButton = "SupernaturalExpandedJournalHudButton_r16";
        internal const string PerkTooltip = "SupernaturalExpandedPerkTooltip_r4";
        internal const string PurchaseFlight = "SupernaturalExpandedPurchaseFlight_r1";
        internal const int RootExport = 1;

        internal const uint MagicBar = 0x0DFC29C0;
        internal const uint SimName = 0xEF984401;
        internal const uint SimologyTraits = 0xEF98440A;
        internal const uint FamilyTree = 0xEF984410;
        internal const uint Celebrity = 0xEF984412;
        internal const uint Zodiac = 0xEF984420;
        internal const uint CollectionOkay = 0x05EF6BD1;

        internal const uint Title = 0x53E00003;
        internal const uint Portrait = 0x53E00004;
        internal const uint PortraitFrame = 0x53E00005;
        internal const uint JournalSimName = 0x53E00006;
        internal const uint OccultRank = 0x53E0000C;
        internal const uint Close = 0x53E00009;
        internal const uint Content = 0x53E0000A;
        internal const uint Cancel = 0x53E0000D;
        internal const uint FrameUpper = 0x53E00020;
        internal const uint FrameLowerLeft = 0x53E00022;
        internal const uint FrameFooter = 0x53E00024;
        internal const uint HudGlyph = 0x53E0000B;
        internal const uint PerkPulse = 0x53E000ED;
        internal const uint FirstTab = 0x53E00010;

        internal const uint GrimoireFirstTab = 0x53E00180;
        internal const uint GrimoireFirstIcon = 0x53E00188;
        internal const uint GrimoireFirstCaption = 0x53E00190;
        internal const uint GrimoireListPanel = 0x53E000D0;
        internal const uint GrimoireListBody = 0x53E000D1;
        internal const uint GrimoireListTitle = 0x53E000D2;
        internal const uint GrimoireEmptyList = 0x53E000D4;
        internal const uint GrimoireTierHeader = 0x53E000D7;
        internal const uint GrimoireCostHeader = 0x53E000D8;
        internal const uint GrimoireFirstColumnButton = 0x53E000D9;
        internal const uint GrimoireTableHeader = 0x53E000DC;
        internal const uint GrimoireFirstSortArrow = 0x53E000DF;
        internal const uint GrimoireFirstSortDescending = 0x53E000E2;
        internal const uint GrimoireDescriptionPanel = 0x53E000E5;
        internal const uint GrimoireDescriptionBody = 0x53E000E6;
        internal const uint GrimoireDescriptionName = 0x53E000E7;
        internal const uint GrimoireDescriptionText = 0x53E000E8;
        internal const uint GrimoireDescriptionPrompt = 0x53E000F0;
        internal const uint GrimoireDescriptionMeta = 0x53E000F1;
        internal const uint GrimoireDescriptionCost = 0x53E000F2;
        internal const uint GrimoireFirstRow = 0x53E00100;
        internal const uint GrimoireScrollTrack = 0x53E00140;
        internal const uint GrimoireScrollThumb = 0x53E00141;
        internal const uint GrimoireScrollUp = 0x53E00142;
        internal const uint GrimoireScrollDown = 0x53E00143;
        internal const uint OverviewFrame = 0x53E00300;
        internal const uint OverviewHeaderCell = 0x53E0030A;
        internal const uint OverviewHeaderTitle = 0x53E0030D;
        internal const uint OverviewRankTitle = 0x53E0031C;
        internal const uint OverviewRankText = 0x53E0031D;
        internal const uint OverviewArtwork = 0x53E00320;
        internal const uint OverviewLifeIcon = 0x53E00321;
        internal const uint OverviewLifeTitle = 0x53E00322;
        internal const uint OverviewLifeText = 0x53E00323;
        internal const uint OverviewRankNumeral = 0x53E00325;
        internal const uint OverviewLifeArt = 0x53E0032E;
        internal const uint OverviewFirstList = 0x53E00340;
        internal const uint OverviewListStride = 0x40;
        internal const uint OverviewScrollOffset = 0x30;
        internal const uint StatusPanel = 0x53E00090;
        internal const uint StatusProgress = 0x53E000A0;
        internal const uint StatusPower = 0x53E000B0;
        internal const uint StatusSparkleFirst = 0x53E00260;

        internal static ResourceKey Key(string name)
        {
            return ResourceKey.CreateUILayoutKey(name, 0);
        }

        internal static T Root<T>(Layout layout, string name) where T : WindowBase
        {
            T control = !NativeUi.HasLayout(layout)
                ? null : layout.GetWindowByExportID(RootExport) as T;
            if (control == null)
            {
                throw new InvalidOperationException("Could not load the journal layout " + name + ".");
            }
            return control;
        }

        internal static T Find<T>(WindowBase root, uint id) where T : WindowBase
        {
            return root.GetChildByID(id, true) as T;
        }

        internal static T Require<T>(WindowBase root, uint id) where T : WindowBase
        {
            T control = Find<T>(root, id);
            if (control == null)
            {
                throw new InvalidOperationException("Expected " + typeof(T).Name + " at control " + id.ToString("X8") + ".");
            }
            return control;
        }

        internal static Rect Box(float x, float y, float width, float height)
        {
            return new Rect(x, y, x + width, y + height);
        }

        internal static float UIScale()
        {
            float scale = TinyUIFixForTS3Integration.getUIScale();
            return scale > 0 && scale <= 4f ? scale : 1f;
        }

        internal static void Release(Layout layout, bool windowsGone)
        {
            if (layout == null)
            {
                return;
            }

            try
            {
                if (!windowsGone && NativeUi.HasLayout(layout))
                {
                    layout.Shutdown();
                }
            }
            finally
            {
                layout.Dispose();
            }
        }
    }

    internal static class NativeUi
    {
        private static readonly MethodInfo HudInstance = NativeReflection.Getter(typeof(HudController), "Instance", NativeReflection.Static);
        private static readonly MethodInfo SimDisplayInstance = NativeReflection.Getter(typeof(SimDisplay), "Instance", NativeReflection.Static);
        private static readonly Type SimologyPanelType = typeof(HudController).Assembly.GetType("Sims3.UI.Hud.SimologyPanel");
        private static readonly MethodInfo SimologyInstance = NativeReflection.Getter(SimologyPanelType, "Instance", NativeReflection.Static);
        private static readonly EventInfo HudStateChanged = typeof(HudController).GetEvent("InfoStateChange", NativeReflection.Instance);
        private static readonly EventInfo WindowShutdown = typeof(WindowBase).GetEvent("InternalWindowShutdown", NativeReflection.Instance);
        private static readonly MethodInfo LayoutHandle = NativeReflection.Getter(typeof(Layout), "LayoutHandle", NativeReflection.Instance);
        private static readonly MethodInfo WindowHandle = NativeReflection.Getter(typeof(WindowBase), "WinHandle", NativeReflection.Instance);
        private static readonly MethodInfo ImageHandle = NativeReflection.Getter(typeof(UIImage), "UIImageHandle", NativeReflection.Instance);
        private static readonly MethodInfo GetDrawable = typeof(UIManager).GetMethod("GetDrawableProperty", NativeReflection.Static,
            null, new[] { typeof(uint), typeof(uint), typeof(uint) }, null);
        private static readonly FieldInfo PauseModeField = typeof(ModalDialog).GetField("mPauseMode", NativeReflection.Instance);

        internal static HudController GetHud()
        {
            return (HudController)NativeReflection.Call(HudInstance, null, null, "HudController.Instance");
        }

        internal static SimDisplay GetSimDisplay()
        {
            return (SimDisplay)NativeReflection.Call(SimDisplayInstance, null, null, "SimDisplay.Instance");
        }

        internal static WindowBase GetSimologyPanel()
        {
            return (WindowBase)NativeReflection.Call(SimologyInstance, null, null, "SimologyPanel.Instance");
        }

        internal static Delegate BindInfoStateHandler(Action<InfoState> callback)
        {
            if (HudStateChanged == null)
            {
                throw new MissingMemberException("HudController.InfoStateChange");
            }
            return Delegate.CreateDelegate(HudStateChanged.EventHandlerType, callback.Target, callback.Method);
        }

        internal static void WatchInfoState(HudController hud, Delegate handler, bool add)
        {
            Watch(HudStateChanged, hud, handler, add, "HudController.InfoStateChange");
        }

        internal static void WatchShutdown(WindowBase window, UIEventHandler<UIEventArgs> handler, bool add)
        {
            Watch(WindowShutdown, window, handler, add, "WindowBase.InternalWindowShutdown");
        }

        private static void Watch(EventInfo evt, object target, Delegate handler, bool add, string member)
        {
            MethodInfo accessor = evt == null ? null : (add ? evt.GetAddMethod(true) : evt.GetRemoveMethod(true));
            NativeReflection.Call(accessor, target, new object[] { handler }, (add ? "Adding " : "Removing ") + member);
        }

        internal static bool HasLayout(Layout layout)
        {
            return layout != null && (uint)NativeReflection.Call(LayoutHandle, layout, null, "Layout.LayoutHandle") != 0;
        }

        internal static bool HasImage(UIImage image)
        {
            return image != null && (uint)NativeReflection.Call(ImageHandle, image, null, "UIImage.UIImageHandle") != 0;
        }

        internal static DrawableBase DialogDrawable(WindowBase window)
        {
            const uint drawableProperty = 0xEEC1B007;
            object handle = NativeReflection.Call(WindowHandle, window, null, "WindowBase.WinHandle");
            return (DrawableBase)NativeReflection.Call(GetDrawable, null, new object[] { handle, drawableProperty, 0u },
                "UIManager.GetDrawableProperty(uint,uint,uint)");
        }

        internal static void DisableDialogPause(ModalDialog dialog)
        {
            if (PauseModeField == null)
            {
                throw new MissingFieldException("ModalDialog.mPauseMode");
            }
            PauseModeField.SetValue(dialog, ModalDialog.PauseMode.NoPause);
        }
    }

    internal static class NativeReflection
    {
        internal const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static MethodInfo Getter(Type type, string name, BindingFlags flags)
        {
            PropertyInfo property = type == null ? null : type.GetProperty(name, flags);
            return property == null ? null : property.GetGetMethod(true);
        }

        internal static object Call(MethodInfo method, object target, object[] args, string member)
        {
            if (method == null)
            {
                throw new MissingMethodException(member);
            }
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException error)
            {
                ResetException reset = error.InnerException as ResetException;
                if (reset != null)
                {
                    throw reset;
                }
                throw;
            }
        }

        internal static object Field(FieldInfo field, object target, string member)
        {
            if (field == null)
            {
                throw new MissingFieldException(member);
            }
            return field.GetValue(target);
        }
    }
}
