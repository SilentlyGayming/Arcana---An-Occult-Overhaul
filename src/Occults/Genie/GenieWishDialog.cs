using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Core;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class GenieMenu
    {
        internal static string Root(bool female)
        {
            return SelectRoot(OccultGenie.CleanSim.Singleton.GetPath(female), JournalText.Get("Arcana.Genie.Menu"));
        }

        internal static string SelectRoot(string[] nativePath, string fallback)
        { return nativePath != null && nativePath.Length > 0 && !string.IsNullOrEmpty(nativePath[0]) ? nativePath[0] : fallback; }
    }

    internal static class GenieWishMenu
    {
        [Persistable(false)] private static InteractionDefinition originalLampWish;

        internal static void Prepare()
        {
            FieldInfo field = typeof(OccultGenie.MakeWish).GetField("Singleton", BindingFlags.Public | BindingFlags.Static);
            if (field == null) throw new InvalidOperationException("Missing native MakeWish singleton.");
            InteractionDefinition current = field.GetValue(null) as InteractionDefinition;
            if (current == GrantWish.LampSingleton) return;
            if (current == null || current.GetType().DeclaringType != typeof(OccultGenie.MakeWish)) return;
            originalLampWish = current;
            field.SetValue(null, GrantWish.LampSingleton);
        }

        internal static void Restore()
        {
            FieldInfo field = typeof(OccultGenie.MakeWish).GetField("Singleton", BindingFlags.Public | BindingFlags.Static);
            if (originalLampWish != null && field != null && field.GetValue(null) == GrantWish.LampSingleton)
                field.SetValue(null, originalLampWish);
            originalLampWish = null;
        }

        internal static bool IsLampWish(InteractionDefinition definition)
        {
            GrantWish.Definition arcana = definition as GrantWish.Definition;
            return arcana != null ? arcana.Mode == WishMode.Lamp : definition != null &&
                (definition == originalLampWish || definition.GetType().DeclaringType == typeof(OccultGenie.MakeWish));
        }

        internal static void Install(Sim sim)
        {
            if (sim == null) return;
            sim.Interactions.RemoveAll(delegate(InteractionObjectPair pair)
            { return pair.InteractionDefinition is GrantWish.Definition || originalLampWish != null && IsLampWish(pair.InteractionDefinition); });
            foreach (InteractionDefinition definition in GrantWish.Singletons) sim.AddInteraction(definition, true);
            if (originalLampWish != null) sim.AddInteraction(GrantWish.LampSingleton, true);
        }
    }

    internal sealed class GenieWishChoice : IGenieWish
    {
        internal readonly WishKind Kind;
        private readonly string name;
        private readonly string description;
        internal readonly string NativeIcon;

        internal GenieWishChoice(WishKind kind, string title, string details, string nativeIcon)
        { Kind = kind; name = title; description = details; NativeIcon = nativeIcon; }

        public string GetWishName() { return name; }
        public string GetWishDescription() { return description; }
        public bool IsDisabled() { return false; }
        public ResourceKey PrimaryIconKey
        {
            get
            {
                ResourceKey key = ResourceKey.CreatePNGKey(NativeIcon, ResourceUtils.ProductVersionToGroupId(ProductVersion.EP6));
                if (!World.ResourceExists(key)) key.GroupId = 0u;
                return key;
            }
        }
    }

    internal static class GenieWishDialog
    {
        internal static GenieWishChoice Show(Sim genie, Sim wisher, WishMode mode)
        {
            List<IGenieWish> choices = Build(genie, wisher, mode);
            return choices.Count == 0 ? null : GenieWishSelectionDialog.Show(choices) as GenieWishChoice;
        }

        internal static List<IGenieWish> Build(Sim genie, Sim wisher, WishMode mode)
        {
            List<IGenieWish> choices = new List<IGenieWish>();
            if (mode == WishMode.Own)
            {
                foreach (WishKind kind in new[] { WishKind.Fortune, WishKind.Vitality, WishKind.Heart })
                {
                    if (!WishEffects.Eligible(genie, wisher, kind, mode)) continue;
                    string title = JournalText.Get(kind == WishKind.Fortune ? "Arcana.Genie.Wish.Fortune" : kind == WishKind.Vitality
                        ? "Arcana.Genie.Wish.Vitality" : "Arcana.Genie.Wish.Heart.Own");
                    choices.Add(new GenieWishChoice(kind, title, string.Empty, Icon(kind, false)));
                }
                return choices;
            }
            foreach (WishDefinition entry in WishCatalog.All)
            {
                if (!WishEffects.Eligible(genie, wisher, entry.Kind, mode)) continue;
                bool evil = wisher.HasTrait(TraitNames.Evil);
                string name = DisplayName(entry, evil, wisher.IsFemale);
                string description = Description(entry.Kind, evil, wisher.IsFemale);
                choices.Add(new GenieWishChoice(entry.Kind, name, description, Icon(entry.Kind, evil)));
            }
            return choices;
        }

        internal static string DisplayName(WishDefinition entry, bool evil, bool female)
        {
            string nativeName = NativeName(entry.Kind, evil);
            if (nativeName != null)
                return Localization.LocalizeString(female, "Gameplay/ActorSystems/OccultGenie/MakeWish:" + nativeName);
            switch (entry.Kind)
            {
                case WishKind.CheatDeath: return JournalText.Get("Arcana.Genie.Wish.Dialog.CheatDeath");
                case WishKind.Youth: return JournalText.Get("Arcana.Genie.Wish.Dialog.Youth");
                case WishKind.Success: return JournalText.Get("Arcana.Genie.Wish.Dialog.Success");
                case WishKind.Fame: return JournalText.Get("Arcana.Genie.Wish.Dialog.Fame");
                default: return JournalText.Get(entry.NameKey);
            }
        }

        internal static string NativeDescriptionName(WishKind kind, bool evil)
        {
            string name = NativeName(kind, evil);
            return name == null ? null : name.Substring(0, name.Length - 4) + "Description";
        }

        internal static string Description(WishKind kind, bool evil, bool female)
        {
            string name = NativeName(kind, evil);
            if (name == null) return string.Empty;
            return new OccultGenie.GenieWishInstance(name, NativeDescriptionName(kind, evil), null, false, female).GetWishDescription();
        }

        internal static bool HasAvailable(Sim genie, Sim wisher, WishMode mode)
        {
            if (mode == WishMode.Own)
            {
                foreach (WishKind kind in new[] { WishKind.Fortune, WishKind.Vitality, WishKind.Heart })
                    if (WishEffects.Eligible(genie, wisher, kind, mode)) return true;
                return false;
            }
            if (WishEffects.Eligible(genie, wisher, WishKind.Fortune, mode)) return true;
            foreach (WishDefinition entry in WishCatalog.All)
                if (entry.Kind != WishKind.Fortune && WishEffects.Eligible(genie, wisher, entry.Kind, mode)) return true;
            return false;
        }

        internal static string NativeName(WishKind kind, bool evil)
        {
            switch (kind)
            {
                case WishKind.Fortune: return "FortuneName";
                case WishKind.WorldPeace: return evil ? "WorldMiseryName" : "WorldPeaceName";
                case WishKind.Resurrect: return "RaiseDeadName";
                case WishKind.MoreWishes: return "MoreWishesName";
                case WishKind.Love: return "MakeSimLoveMeName";
                case WishKind.LongLife: return "LongLifeName";
                case WishKind.LargeFamily: return "LargeFamilyName";
                case WishKind.Happiness: return "HappinessName";
                case WishKind.Beauty: return "BeautyName";
                case WishKind.FreeGenie: return "FreeGenieName";
                default: return null;
            }
        }

        internal static string Icon(WishKind kind, bool evil)
        {
            string nativeName = NativeName(kind, evil);
            if (nativeName == null)
                nativeName = kind == WishKind.CheatDeath ? "RaiseDeadName" : kind == WishKind.Youth ? "LongLifeName"
                    : kind == WishKind.Success ? "FortuneName" : kind == WishKind.Fame ? "BeautyName"
                    : kind == WishKind.Vitality ? "LargeFamilyName" : "HappinessName";
            return "w_genieWish_" + nativeName;
        }
    }
}
