using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal sealed class JournalPage
    {
        internal readonly OccultTypes Occult;
        internal readonly string Title;
        internal readonly string Body;

        internal JournalPage(OccultTypes occult, string title, string body)
        {
            Occult = occult;
            Title = title;
            Body = body;
        }
    }

    internal static class JournalPages
    {
        internal const int Overview = 0;
        internal const int Perks = 1;
        internal const int Grimoire = 2;

        private static readonly string MissingLayout = JournalText.Get("Arcana.Journal.Perks.MissingLayout");

        internal static readonly JournalPage[] All =
        {
            new JournalPage(OccultAssetData.SupportedTypes, JournalText.Get("Arcana.Journal.Tab.Overview"), ""),
            new JournalPage(OccultTypes.Witch, JournalText.Get("Arcana.Journal.Tab.Perks"), MissingLayout),
            new JournalPage(OccultTypes.Witch | OccultTypes.Fairy | OccultTypes.Genie, JournalText.Get("Arcana.Journal.Tab.Grimoire"), "")
        };

        internal static bool IsAvailable(int page, OccultTypes flags)
        {
            return (flags & All[page].Occult) != OccultTypes.None;
        }
    }
}
