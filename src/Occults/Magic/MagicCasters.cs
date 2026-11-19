using System;
using System.Reflection;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.SimIFace;
using Sims3.UI.Hud;

namespace SupernaturalExpanded.Journal
{
    internal static class MagicCasters
    {
        private static readonly FieldInfo PurchasedPrice = typeof(GameObject).GetField("mPurchasedPrice", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CatalogPrice = typeof(GameObject).GetField("mIntPrice", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool TryKind(Sim sim, out CasterKind kind)
        {
            // Returns the Sim's casting tradition: Witch first, then Fairy, then Genie.
            kind = CasterKind.Witch;
            if (sim == null || sim.OccultManager == null) return false;
            if (sim.OccultManager.HasOccultType(OccultTypes.Witch)) return true;
            kind = CasterKind.Fairy;
            if (sim.OccultManager.HasOccultType(OccultTypes.Fairy)) return true;
            kind = CasterKind.Genie;
            return sim.OccultManager.HasOccultType(OccultTypes.Genie);
        }

        internal static bool IsCaster(Sim sim)
        {
            CasterKind kind;
            return TryKind(sim, out kind);
        }

        internal static bool IsInnateCaster(Sim sim)
        {
            CasterKind kind;
            return TryKind(sim, out kind) && kind != CasterKind.Witch;
        }

        internal static SkillNames SkillFor(CasterKind kind)
        {
            return kind == CasterKind.Fairy ? SkillNames.FairyMagic : kind == CasterKind.Genie ? GenieMagicSkill.GenieGuid : SkillNames.Spellcasting;
        }

        internal static int SkillLevel(Sim sim, CasterKind kind)
        {
            // Returns the level of the skill that decides a Fairy's or Genie's spell tiers.
            if (sim == null || sim.SkillManager == null) return 0;
            if (kind == CasterKind.Genie)
            {
                GenieMagicSkill genie = GeniePower.Read(sim.SimDescription, false);
                return genie == null ? 0 : genie.SkillLevel;
            }
            return Math.Max(0, sim.SkillManager.GetSkillLevel(SkillFor(kind)));
        }

        internal static WitchCastingRules ReadRules(Sim sim)
        {
            // Reads the casting rules for any caster: a Witch's rank and perks, or a Fairy's or Genie's skill tier.
            CasterKind kind;
            if (!TryKind(sim, out kind) || kind == CasterKind.Witch) return WitchCastingContext.ReadRules(sim);
            return new WitchCastingRules(WitchCastingRules.RankForSkill(SkillLevel(sim, kind)), kind);
        }

        internal static float AvailablePower(Sim sim, WitchCastingRules rules)
        {
            // Returns the caster's remaining power in the same units as spell costs.
            if (sim == null || sim.Motives == null) return 0f;
            switch (rules.Caster)
            {
                case CasterKind.Fairy:
                    return Fraction(sim, CommodityKind.AuraPower) * rules.Capacity;
                case CasterKind.Genie:
                    return GeniePower.Get(sim.SimDescription) / GeniePower.Maximum * rules.Capacity;
                default:
                    float minimum = sim.Motives.GetMin(CommodityKind.MagicFatigue);
                    float maximum = sim.Motives.GetMax(CommodityKind.MagicFatigue);
                    return rules.Capacity * WitchCastingRules.PowerFraction(sim.Motives.GetValue(CommodityKind.MagicFatigue), minimum, maximum);
            }
        }

        internal static float SpendInnate(Sim sim, WitchCastingRules rules, float cost)
        {
            // Spends a Fairy's Faeric Power or a Genie's power and returns the power actually spent.
            float spent = Math.Min(Math.Max(0f, cost), AvailablePower(sim, rules));
            if (spent <= 0f) return 0f;
            if (rules.Caster == CasterKind.Fairy)
            {
                float span = sim.Motives.GetMax(CommodityKind.AuraPower) - sim.Motives.GetMin(CommodityKind.AuraPower);
                sim.Motives.ChangeValue(CommodityKind.AuraPower, -spent / rules.Capacity * span);
                JournalBootstrap.RefreshMeter();
            }
            else if (rules.Caster == CasterKind.Genie)
            {
                GeniePower.Change(sim.SimDescription, -spent / rules.Capacity * GeniePower.Maximum);
            }
            return spent;
        }

        private static float Fraction(Sim sim, CommodityKind kind)
        {
            float minimum = sim.Motives.GetMin(kind);
            float span = sim.Motives.GetMax(kind) - minimum;
            return span <= 0f ? 0f : Math.Max(0f, Math.Min(1f, (sim.Motives.GetValue(kind) - minimum) / span));
        }

        internal static bool NeedsHands(Sim sim)
        {
            // Returns whether Arcana should keep Magic Hands in the Sim's inventory: every Fairy and Genie, and Witches with Practically Magic.
            if (sim == null || sim.HasBeenDestroyed) return false;
            return IsInnateCaster(sim) || (WitchCastingContext.IsWitch(sim) && WitchCastingContext.HasPracticallyMagic(sim));
        }

        internal static bool EnsureHands(Sim sim)
        {
            // Gives a Sim who needs them one worthless Magic Hands item, created exactly as the game's lifetime reward creates it.
            if (!NeedsHands(sim) || sim.Inventory == null) return false;
            if (IsInnateCaster(sim))
            {
                foreach (MagicHands owned in sim.Inventory.FindAll<MagicHands>(false)) MakeWorthless(owned);
            }
            if (sim.Inventory.FindAll<MagicWand>(false).Count > 0) return true;
            GameObject hands = Sims3.Gameplay.GlobalFunctions.CreateObject("magicHandsLHR", ProductVersion.EP7, Vector3.OutOfWorld, 0,
                Vector3.UnitZ, null, (Simulator.ObjectInitParameters)null) as GameObject;
            if (!(hands is MagicHands))
            {
                if (hands != null) hands.Destroy();
                return false;
            }
            MakeWorthless((MagicHands)hands);
            if (sim.Inventory.TryToAdd(hands)) return true;
            hands.Destroy();
            return false;
        }

        internal static void UpdateHands(Sim sim)
        {
            // Adds Magic Hands to Sims who need them and removes Arcana's from Sims who no longer do, such as a cured Fairy or Genie.
            if (sim == null || sim.Inventory == null) return;
            if (NeedsHands(sim))
            {
                EnsureHands(sim);
                return;
            }
            foreach (MagicHands hands in sim.Inventory.FindAll<MagicHands>(false))
            {
                if (!IsArcanaHands(hands)) continue;
                sim.Inventory.TryToRemove(hands);
                hands.Destroy();
            }
        }

        internal static void UpdateAllHands()
        {
            foreach (Sim sim in Sims3.Gameplay.Queries.GetObjects<Sim>()) UpdateHands(sim);
        }

        internal static void MakeWorthless(MagicHands hands)
        {
            // Zeroes Arcana's Magic Hands' price so they are never counted as household value, which fire insurance would pay out.
            PurchasedPrice.SetValue(hands, 0);
            CatalogPrice.SetValue(hands, 0);
        }

        private static bool IsArcanaHands(MagicHands hands)
        {
            // Recognizes Arcana's Magic Hands by their zero purchase price; the game's own reward keeps its catalog price.
            object price = PurchasedPrice.GetValue(hands);
            return price is int && (int)price == 0;
        }

        internal static void EnsureSkill(Sim sim)
        {
            // Gives a Genie the Genie Magic skill that stores its power and tier.
            CasterKind kind;
            if (TryKind(sim, out kind) && kind == CasterKind.Genie) GeniePower.Read(sim.SimDescription, true);
        }
    }
}
