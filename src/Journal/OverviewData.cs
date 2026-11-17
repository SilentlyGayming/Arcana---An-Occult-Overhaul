using Sims3.SimIFace;
using Sims3.UI.Hud;
using Sims3.UI;
using System.Collections.Generic;
using System.Globalization;
using System;

namespace SupernaturalExpanded.Journal
{
    // The text of a row's hover tooltip, shown in the same tooltip window the Perks page uses.
    internal sealed class OverviewTip
    {
        internal readonly string Title;
        internal readonly string Tagline;
        internal readonly string Body;

        internal OverviewTip(string title, string tagline, string body)
        {
            Title = title ?? "";
            Tagline = tagline ?? "";
            Body = body ?? "";
        }

        internal static bool Same(OverviewTip a, OverviewTip b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }
            return a.Title == b.Title && a.Tagline == b.Tagline && a.Body == b.Body;
        }
    }

    // One line of a dashboard list: an optional icon, a name, an optional detail beside the value, the value, and an optional tooltip.
    internal sealed class OverviewRow
    {
        internal readonly UIImage Icon;
        internal readonly string Name;
        internal readonly string Detail;
        internal readonly string Value;
        internal readonly OverviewTip Tip;

        internal OverviewRow(UIImage icon, string name, string detail, string value)
            : this(icon, name, detail, value, null)
        {
        }

        internal OverviewRow(UIImage icon, string name, string detail, string value, OverviewTip tip)
        {
            Icon = icon;
            Name = name ?? "";
            Detail = detail ?? "";
            Value = value ?? "";
            Tip = tip;
        }
    }

    // Everything the Overview shows for one Sim. A species provider fills the parts it has; whatever stays empty is shown as an empty state.
    internal sealed class OverviewData
    {
        internal string RankTitle = "";
        internal string RankDescription = "";
        // The rank as a number from 1, drawn as a Roman numeral; 0 shows no numeral.
        internal int RankLevel;
        internal UIImage RankArtwork = null;
        // The number of acquired perks, or -1 when the species has no perk list.
        internal int PerkCount = -1;
        internal readonly List<OverviewRow> Statistics = new List<OverviewRow>();
        internal readonly List<OverviewRow> Perks = new List<OverviewRow>();
        internal readonly List<OverviewRow> Knowledge = new List<OverviewRow>();
        internal string LifeStateTitle = "";
        internal string LifeStateText = "";
        internal UIImage LifeStateIcon;
        // Full-size art for the life state, shown stretched to its square instead of the glyph; null while a species has none.
        internal UIImage LifeStateArt = null;
    }

    internal sealed class OverviewContext
    {
        internal readonly SimIdentity Identity;
        internal readonly OccultTypes Primary;
        internal readonly PowerSelection Witch;
        // The power row the journal header shows for this Sim; not Available when the game has no reading for it.
        internal readonly StatusField Power;
        private readonly PowerPage perkPage;
        private readonly GrimoirePage grimoire;

        internal OverviewContext(SimIdentity identity, PowerSelection witch, StatusField power, PowerPage perkPage, GrimoirePage grimoire)
        {
            Identity = identity;
            Primary = OccultAssetData.Primary(identity.Occults);
            Witch = witch;
            Power = power;
            this.perkPage = perkPage;
            this.grimoire = grimoire;
        }

        // The Grimoire category a knowledge row reads: its tab icon and the number of entries the Sim has in it.
        internal UIImage KnowledgeIcon(int category)
        {
            return grimoire == null ? null : grimoire.GetCategoryIcon(category);
        }

        internal int KnowledgeCount(int category)
        {
            return grimoire == null ? 0 : grimoire.GetEntryCount(category);
        }

        internal UIImage PerkIcon(Perk perk)
        {
            return perkPage == null ? null : perkPage.PerkIcon(perk);
        }
    }

    // A species supplies its own rows and rank text through this; the Overview layout never changes.
    internal interface IOverviewProvider
    {
        void Fill(OverviewContext context, OverviewData data);
    }

    internal static class OverviewProviders
    {
        private static readonly IOverviewProvider common = new CommonOverview();
        private static readonly IOverviewProvider witch = new WitchOverview();
        private static readonly IOverviewProvider magical = new MagicalOverview();

        // Registers a species here when it has data for the Overview; the others keep the common content.
        private static IOverviewProvider For(OccultTypes primary)
        {
            switch (primary)
            {
                case OccultTypes.Witch:
                    return witch;
                case OccultTypes.Fairy:
                case OccultTypes.Genie:
                    return magical;
                default:
                    return null;
            }
        }

        internal static OverviewData Build(OverviewContext context)
        {
            OverviewData data = new OverviewData();
            common.Fill(context, data);
            IOverviewProvider species = For(context.Primary);
            if (species != null)
            {
                species.Fill(context, data);
            }
            return data;
        }

        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        internal static string GetRomanNumeral(int value)
        {
            return value >= 1 && value <= Numerals.Length ? Numerals[value - 1] : value.ToString();
        }

        // The life state, its description and icon, and the rank every occult shows in the header; this is the part that already exists.
        private sealed class CommonOverview : IOverviewProvider
        {
            public void Fill(OverviewContext context, OverviewData data)
            {
                SimIdentity identity = context.Identity;
                data.LifeStateTitle = JournalText.Format("Arcana.LifeState.Header", identity.LifeState);
                data.LifeStateText = LifeStateDescriptions.For(identity);
                data.LifeStateIcon = OverviewIcons.Species(context.Primary);
                data.LifeStateArt = OverviewIcons.Art(context.Primary);

                string rank = OccultRankLabel.Rank(identity.Occults, context.Witch);
                if (rank.Length != 0)
                {
                    data.RankTitle = JournalText.Format("Arcana.Journal.Overview.Rank.Header", rank);
                    data.RankLevel = 1;
                }
                else if (OccultRankLabel.HasLadder(identity.Occults))
                {
                    data.RankTitle = JournalText.Get("Arcana.Journal.Overview.Rank.Unavailable");
                }
                else
                {
                    data.RankTitle = JournalText.Get("Arcana.Journal.Overview.Rank.Unranked");
                    data.RankDescription = JournalText.Get("Arcana.Journal.Overview.Rank.Unimplemented");
                }

                StatusField power = context.Power;
                if (power != null && power.Available)
                {
                    data.Statistics.Add(new OverviewRow(power.Icon, power.Label, "", power.Caption));
                }
                AttributeSources.AddRows(context, data);
            }
        }

        // Witches, Fairies and Genies share the Grimoire's four knowledge categories; each row shows the entries the Sim has
        // unlocked in that category (the Grimoire holds nothing else), 0 included, so the rows are always present.
        private static void AddKnowledge(OverviewContext context, OverviewData data)
        {
            for (int category = 0; category < KnowledgeNames.Length; category++)
            {
                data.Knowledge.Add(new OverviewRow(context.KnowledgeIcon(category), KnowledgeNames[category], "",
                    context.KnowledgeCount(category).ToString()));
            }
        }

        // The Grimoire's categories in tab order.
        private static readonly string[] KnowledgeNames = { JournalText.Get("Arcana.Journal.Overview.Knowledge.Spells"), JournalText.Get("Arcana.Journal.Overview.Knowledge.Curses"), JournalText.Get("Arcana.Journal.Overview.Knowledge.Rituals"), JournalText.Get("Arcana.Journal.Overview.Knowledge.Abilities") };

        // Fairy and Genie: the shared knowledge categories; rank, perks and statistics stay with the common content until their systems exist.
        private sealed class MagicalOverview : IOverviewProvider
        {
            public void Fill(OverviewContext context, OverviewData data)
            {
                AddKnowledge(context, data);
            }
        }

        // Witch: the knowledge categories, the rank text and the perks bought on the Perks page, read from the same saved progression.
        private sealed class WitchOverview : IOverviewProvider
        {
            public void Fill(OverviewContext context, OverviewData data)
            {
                AddKnowledge(context, data);

                PowerSelection selection = context.Witch;
                if (selection == null || !selection.Available)
                {
                    return;
                }

                OccultTree tree = WitchTree.Tree;
                int rank = (int)selection.Rank;
                int top = WitchProgression.RankCount - 1;
                data.RankDescription = WitchRankDescriptions.For(rank, context.Identity.FirstName);
                data.RankLevel = rank + 1;

                // Highest unlock rank first; the value is the rank that unlocks the perk, the detail the ranks bought of a multi-rank perk.
                for (int unlock = top; unlock >= 0; unlock--)
                {
                    foreach (Perk perk in tree.Perks)
                    {
                        int bought = selection.Acquired(perk);
                        if ((int)perk.Rank != unlock || bought <= 0)
                        {
                            continue;
                        }
                        data.Perks.Add(new OverviewRow(context.PerkIcon(perk), perk.Name,
                            perk.Tiers > 1 ? JournalText.Format("Arcana.Common.CompactRatio", bought, perk.Tiers) : "",
                            JournalText.Format("Arcana.Journal.Overview.Perks.UnlockRank", GetRomanNumeral(unlock + 1)), PerkTip(perk, bought, selection.SimName)));
                    }
                }
                data.PerkCount = data.Perks.Count;
            }

            // The Perks page's own title, tagline and description for the rank bought, so the two pages can never disagree; a perk
            // with several ranks says which one is owned, and ranks not yet bought are never described.
            private static OverviewTip PerkTip(Perk perk, int bought, string simName)
            {
                string description = perk.Describe(bought - 1, simName);
                string body = perk.Tiers > 1 ? JournalText.Format("Arcana.Journal.Overview.Perks.OwnedRank", bought, perk.Tiers, description) : description;
                return new OverviewTip(perk.Name, perk.Tagline, body);
            }
        }
    }

    // The Witch's description for each rank, shown in the Rank panel; {Name} becomes the selected Sim's first name.
    internal static class WitchRankDescriptions
    {
        private const string Placeholder = "{Name}";
        private static readonly string[] Descriptions =
        {
            "Arcana.Witch.Rank.Description.Novice",
            "Arcana.Witch.Rank.Description.Apprentice",
            "Arcana.Witch.Rank.Description.Adept",
            "Arcana.Witch.Rank.Description.Master",
            "Arcana.Witch.Rank.Description.HighWitch"
        };

        // The rank is clamped to the five Witch ranks, so a rank the table does not know still shows a description.
        internal static string For(int rank, string firstName)
        {
            int index = Math.Max(0, Math.Min(Descriptions.Length - 1, rank));
            string name = string.IsNullOrEmpty(firstName) ? JournalText.Get("Arcana.Common.ThisSim") : firstName;
            return JournalText.Get(Descriptions[index]).Replace(Placeholder, name);
        }
    }

    internal static class OverviewIcons
    {
        // The large life state art a species ships with, or null so the glyph below is shown instead.
        internal static UIImage Art(OccultTypes primary)
        {
            ulong instance = OccultAssetData.LifeStateArt(primary);
            return instance == 0ul ? null : StatusReaders.StatusIcon(instance);
        }

        // The icon that identifies a life state. The Witch is the project's hat icon from the journal header, because the HUD meter's
        // wand is the game's own artwork; every other species keeps the HUD meter icon, else its skill icon. Null when none exists.
        internal static UIImage Species(OccultTypes primary)
        {
            if (primary == OccultTypes.None)
            {
                return null;
            }

            ulong instance = OccultAssetData.StatusIcon(primary, false);
            UIImage skill = instance == 0ul ? null : StatusReaders.StatusIcon(instance);
            if (primary == OccultTypes.Witch && skill != null)
            {
                return skill;
            }
            return OccultMeter.MeterIcon(primary) ?? skill;
        }
    }

    // A percentage a gameplay system reports for the selected Sim: the effective change against the baseline,
    // +15 for fifteen per cent more, -10 for ten per cent less, 0 for no change.
    internal sealed class ModifierReading
    {
        // Reported by a system that has no figure for this Sim, so the Overview leaves the row out.
        internal static readonly ModifierReading None = new ModifierReading(0f, "", false);

        internal readonly float Percent;
        // The situation the figure applies to ("Sleeping"), or empty when it always does.
        internal readonly string Condition;
        private readonly bool available;

        internal ModifierReading(float percent, string condition)
            : this(percent, condition, !float.IsNaN(percent) && !float.IsInfinity(percent))
        {
        }

        private ModifierReading(float percent, string condition, bool available)
        {
            Percent = percent;
            Condition = condition ?? "";
            this.available = available;
        }

        internal bool Available { get { return available; } }

        // "+15%", "-10%" or "+0%"; a figure that is not a whole number keeps one decimal.
        internal string Text
        {
            get
            {
                double whole = Math.Round((double)Percent, MidpointRounding.AwayFromZero);
                bool exact = Math.Abs(Percent - whole) < 0.05;
                double shown = exact ? whole : Math.Round((double)Percent, 1, MidpointRounding.AwayFromZero);
                string sign = shown < 0.0 ? "-" : "+";
                return JournalText.Format("Arcana.Common.SignedPercent", sign, Math.Abs(shown).ToString(exact ? "0" : "0.#", CultureInfo.InvariantCulture));
            }
        }
    }

    internal delegate ModifierReading ModifierSource(OverviewContext context);

    // Where a species' gameplay systems supply the percentages the Attributes panel shows under Magical Power. A system registers
    // the label it wants and a source that reads the same effective modifier the gameplay applies (stacking included); the Overview
    // only formats it. A source that reports ModifierReading.None adds no row, so nothing is shown for a mechanic that does not exist.
    internal static class AttributeSources
    {
        private sealed class Entry
        {
            internal readonly OccultTypes Species;
            internal readonly string Label;
            internal readonly ModifierSource Source;

            internal Entry(OccultTypes species, string label, ModifierSource source)
            {
                Species = species;
                Label = label;
                Source = source;
            }
        }

        private const string RegenerationKey = "Arcana.Journal.Overview.Attributes.PowerRegeneration";
        private static readonly List<Entry> entries = new List<Entry>();

        static AttributeSources()
        {
#if ARCANA_GRIMOIRE_TEST
            AttributeTestData.Register();
#endif
        }

        internal static void Register(OccultTypes species, string label, ModifierSource source)
        {
            entries.Add(new Entry(species, label, source));
        }

        // Appends a row for every source registered for the Sim's species that has a figure, in registration order.
        internal static void AddRows(OverviewContext context, OverviewData data)
        {
            UIImage icon = context.Power == null ? null : context.Power.Icon;
            string regenerationLabel = JournalText.Get(RegenerationKey);
            string englishRegeneration = JournalText.English(RegenerationKey);
            bool reportedRegeneration = false;
            int regenerationRow = -1;
            foreach (Entry entry in entries)
            {
                if (entry.Species != context.Primary)
                {
                    continue;
                }

                ModifierReading reading;
                try
                {
                    reading = entry.Source(context);
                }
                catch (ResetException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    JournalBootstrap.ReportOnce("Reading " + entry.Label, error);
                    continue;
                }

                if (reading == null || !reading.Available)
                {
                    continue;
                }
                bool isRegeneration = entry.Label == englishRegeneration || entry.Label == regenerationLabel;
                string label = isRegeneration ? regenerationLabel : entry.Label;
                string name = reading.Condition.Length == 0 ? label
                    : JournalText.Format("Arcana.Common.WithQualifier", label, reading.Condition);
                data.Statistics.Add(new OverviewRow(icon, name, "", reading.Text));
                if (isRegeneration)
                {
                    regenerationRow = data.Statistics.Count - 1;
                }
                reportedRegeneration |= isRegeneration;
            }
            // The UI should not silently lose the regeneration row just because the
            // gameplay modifier provider is still unimplemented. An em dash is NOT a
            // made-up percentage. Once an effective percentage source is registered,
            // its actual value replaces the dash without modifying the Overview UI.
            if (!reportedRegeneration && context.Power != null && context.Power.Available)
            {
                data.Statistics.Add(new OverviewRow(icon, regenerationLabel, "", JournalText.Get("Arcana.Common.UnavailableValue")));
                regenerationRow = data.Statistics.Count - 1;
            }

            // Accuracy sits directly under the regeneration row, or last when the Sim has none.
            OverviewRow accuracy = AccuracySources.Row(context);
            if (accuracy != null)
            {
                data.Statistics.Insert(regenerationRow < 0 ? data.Statistics.Count : regenerationRow + 1, accuracy);
            }
        }
    }
}
