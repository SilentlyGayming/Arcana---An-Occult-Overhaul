#if ARCANA_GRIMOIRE_TEST
using System.Collections.Generic;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class AttributeTestData
    {
        internal static void Register()
        {
            AttributeSources.Register(OccultTypes.Witch, "Spellcasting Success", delegate { return new ModifierReading(10f, ""); });
            AttributeSources.Register(OccultTypes.Witch, "Magical Cost Reduction", delegate { return new ModifierReading(-25f, ""); });
        }
    }

    internal static class GrimoireTestData
    {
        private static readonly string[] Tiers = { "Novice", "Apprentice", "Adept", "Expert", "Master" };
        private const string Short = "A small, quick working.";
        private const string Medium = "Draws a thin veil of shimmering light around the caster, softening how others see them for a short while.";
        private const string Long = "Gathers the last of the evening light into a slow, turning ring that hangs in the air for as long as the caster can hold their focus, "
            + "dimming every flame it passes and humming faintly at the edge of hearing. Those who stand inside it report feeling rested, though never "
            + "quite able to say why, and the ring leaves a pale residue on anything it touched.";
        private const string Unbroken = "Averyveryveryveryverylongunbrokenwordtotestwrappingandclippingbehaviourinsidethepanel and then a short tail.";

        internal static void Apply(GrimoirePage page)
        {
            page.SetEntries((int)SpellCategory.Spell, Spells());
            page.ClearEntries((int)SpellCategory.Charm);
            page.SetEntries((int)SpellCategory.Curse, Curses());
            page.SetEntries((int)SpellCategory.Ritual, Rituals());
            page.ClearEntries((int)SpellCategory.Ability);
        }

        private static List<GrimoireEntry> Spells()
        {
            List<GrimoireEntry> list = new List<GrimoireEntry>();
            Add(list, "spell", "Hex", 0, 5f, Short);
            Add(list, "spell", "Ember", 0, 5f);
            Add(list, "spell", "Aura Veil", 1, 12.5f, Medium);
            Add(list, "spell", "Moonlit Reverie Of The Ever Waning Crescent Tide", 4, 80f, Long);
            Add(list, "spell", "Gust", 0, 0f, "");
            Add(list, "spell", "Frost Lattice", 2, 25f);
            Add(list, "spell", "Mend", 1, 10f, Short);
            Add(list, "spell", "Mend", 2, 10f);
            Add(list, "spell", "Spark Of Ordinary Inspiration", 1, 12.5f);
            Add(list, "spell", "Tidecall", 3, 40f);
            Add(list, "spell", "Illuminate", 0, 7.25f);
            Add(list, "spell", "Warding Circle", 2, 25f);
            Add(list, "spell", "Gloaming Step", 3, 40f);
            Add(list, "spell", "Rootbind", 2, 30f);
            Add(list, "spell", "Thornwhip", 1, 15f);
            Add(list, "spell", "Silvershine", 2, 25f);
            Add(list, "spell", "Eclipse", 4, 100f, Medium);
            Add(list, "spell", "Dew", 0, 2f);
            Add(list, "spell", "Stormcaller's Lament", 3, 55f);
            Add(list, "spell", "Whisper", 0, 5f);
            Add(list, "spell", "Cinderfall", 3, 45f);
            Add(list, "spell", "Lantern", 1, 8f);
            return list;
        }

        private static List<GrimoireEntry> Curses()
        {
            List<GrimoireEntry> list = new List<GrimoireEntry>();
            Add(list, "curse", "Hiccups", 0, 3f);
            Add(list, "curse", "Fumble", 0, 3f);
            Add(list, "curse", "Gloom Of The Hollow Hours And Other Misfortunes", 3, 60f, Unbroken);
            Add(list, "curse", "Itch", 1, 6.5f, Short);
            Add(list, "curse", "Sour Milk", 0, 4f);
            Add(list, "curse", "Wither", 2, 22f);
            Add(list, "curse", "Mislay", 1, 9f);
            Add(list, "curse", "Dread", 4, 90f, Long);
            Add(list, "curse", "Stumble", 2, 22f);
            return list;
        }

        private static List<GrimoireEntry> Rituals()
        {
            List<GrimoireEntry> list = new List<GrimoireEntry>();
            Add(list, "ritual", "Harvest Rite", 1, 20f);
            Add(list, "ritual", "Moon Bathing", 0, 10f, Medium);
            Add(list, "ritual", "Binding Of The Seven Winds Beneath A New Moon", 4, 120f, Long);
            Add(list, "ritual", "Hearth Blessing", 1, 20f);
            Add(list, "ritual", "Vigil", 2, 35f);
            Add(list, "ritual", "Seance", 3, 70f);
            Add(list, "ritual", "Offering", 0, 10f);
            Add(list, "ritual", "Rite Of Passage", 2, 30f);
            return list;
        }

        private static void Add(List<GrimoireEntry> list, string kind, string name, int tier, float cost)
        {
            Add(list, kind, name, tier, cost, null);
        }

        private static void Add(List<GrimoireEntry> list, string kind, string name, int tier, float cost, string description)
        {
            string id = "test." + kind + "." + (list.Count + 1).ToString("00");
            list.Add(new GrimoireEntry(id, name, Tiers[tier], tier, cost, description));
        }
    }
}
#endif
