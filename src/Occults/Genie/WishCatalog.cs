using System;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal enum WishKind
    {
        Fortune = 0, Vitality = 1, Heart = 2, WorldPeace = 3, Resurrect = 4, MoreWishes = 5,
        Love = 6, LongLife = 7, LargeFamily = 8, Happiness = 9, Beauty = 10, FreeGenie = 11,
        CheatDeath = 12, Youth = 13, Success = 14, Fame = 15
    }

    internal enum WishMode
    {
        Other,
        Guaranteed,
        Own,
        Lamp
    }

    internal sealed class WishDefinition
    {
        internal readonly WishKind Kind;
        internal readonly string NameKey;
        internal readonly ProductVersion Product;
        internal readonly bool LampOnly;

        internal WishDefinition(WishKind kind, string name, ProductVersion product, bool lampOnly)
        {
            Kind = kind; NameKey = name; Product = product; LampOnly = lampOnly;
        }
    }

    internal static class WishCatalog
    {
        internal static readonly WishDefinition[] All =
        {
            new WishDefinition(WishKind.WorldPeace, "Arcana.Genie.Wishes.WorldPeace.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Resurrect, "Arcana.Genie.Wishes.Resurrect.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.MoreWishes, "Arcana.Genie.Wishes.MoreWishes.Name", ProductVersion.EP6, true),
            new WishDefinition(WishKind.Love, "Arcana.Genie.Wishes.Love.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.LongLife, "Arcana.Genie.Wishes.LongLife.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.LargeFamily, "Arcana.Genie.Wishes.LargeFamily.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Happiness, "Arcana.Genie.Wishes.Happiness.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Fortune, "Arcana.Genie.Wishes.Fortune.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Beauty, "Arcana.Genie.Wishes.Beauty.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.FreeGenie, "Arcana.Genie.Wishes.FreeGenie.Name", ProductVersion.EP6, true),
            new WishDefinition(WishKind.CheatDeath, "Arcana.Genie.Wishes.CheatDeath.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Youth, "Arcana.Genie.Wishes.Youth.Name", ProductVersion.EP4, false),
            new WishDefinition(WishKind.Success, "Arcana.Genie.Wishes.Success.Name", ProductVersion.EP6, false),
            new WishDefinition(WishKind.Fame, "Arcana.Genie.Wishes.Fame.Name", ProductVersion.EP3, false)
        };

        internal static WishDefinition Find(WishKind kind)
        {
            foreach (WishDefinition entry in All) if (entry.Kind == kind) return entry;
            return null;
        }
    }

    internal static class WishRules
    {
        internal const int Fortune = CastingRules.WishFortune;
        internal const float CheatDeathInitialChance = 90f;
        internal const float CareerPenalty = 20f;

        internal static int RepossessionCredit(int debtBefore, int debtAfter, int value)
        {
            int deducted = Math.Min(debtBefore, Math.Max(0, value));
            return debtAfter == debtBefore - deducted ? deducted : 0;
        }

        internal static float DeathChance(int escapes)
        { return Math.Max(0f, CheatDeathInitialChance - 10f * Math.Max(0, escapes)); }

        internal static int MoreWishesDelta(bool success, bool extra)
        { return success ? (extra ? 1 : 0) : -1; }

        internal static bool GuaranteeAvailable(bool other, bool ownsPerk, long ready, long now)
        { return other && ownsPerk && ready <= now; }

        internal static bool CanReceive(bool dead, bool playableGhost, bool beingReaped)
        { return (!dead || playableGhost) && !beingReaped; }

        internal static bool CanOffer(WishDefinition wish, bool installed, bool lamp)
        { return wish != null && installed && (!wish.LampOnly || lamp); }

        internal static float NormalizedPerformance(float performance, bool xpBased)
        { return xpBased ? performance * 100f : performance; }

        internal static bool CanPromote(int level, int highest, bool supported)
        { return supported && level > 0 && highest > level; }

        internal static bool CanGainFame(uint level, bool installed, bool enabled)
        { return installed && enabled && level < 4; }
    }
}
