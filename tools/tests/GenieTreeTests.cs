using System;
using System.Collections.Generic;

namespace SupernaturalExpanded.Journal
{
    internal static class GenieTreeTests
    {
        private static int checks;
        private static readonly OccultTree Tree = GenieTree.Tree;

        public static void Main()
        {
            Geometry();
            Selection();
            Purchases();
            Migration();
            MergedBenefits();
            Console.WriteLine("PASS: " + checks + " Genie tree geometry, prerequisites, atomic purchases, save refunds and merged-effect checks");
            Console.WriteLine("Host checks do not run The Sims 3 UI or world simulation.");
        }

        private static void Check(bool result, string label)
        {
            checks++;
            if (!result) throw new InvalidOperationException(label);
        }

        private static PerkRecord Record(int available)
        {
            return new PerkRecord { Claimed = PerkProgression.AllMask, Earned = 40, Available = available };
        }

        private static PowerSelection Select(PerkRecord record, Rank rank)
        {
            PowerProgress progress = PowerProgress.From(1, 0, new RankState((int)rank, 0f, 13), 1f, record, Tree);
            return new PowerSelection(1, progress, Tree);
        }

        private static Dictionary<string, int> Batch(params string[] keys)
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            foreach (string key in keys) result.Add(key, 1);
            return result;
        }

        private static void Geometry()
        {
            Check(Tree.Perks.Length == 15, "15 perks");
            int[] rows = new int[5];
            int upgrades = 0;
            foreach (Perk perk in Tree.Perks)
            {
                rows[(int)perk.Rank]++;
                upgrades += perk.Tiers;
                float x = PowerLayout.LaneX(perk.Column + perk.Shift);
                float y = PowerLayout.RowY(4 - (int)perk.Rank);
                Check(x >= WindowGeometry.ContentLeft && x + PowerLayout.TileSize <= WindowGeometry.ContentRight, "tile within horizontal bounds: " + perk.Key);
                Check(y >= WindowGeometry.ContentTop && y + PowerLayout.CellHeight <= WindowGeometry.ContentBottom, "tile within vertical bounds: " + perk.Key);
                foreach (Perk other in Tree.Perks)
                {
                    if (other.Index <= perk.Index) continue;
                    float ox = PowerLayout.LaneX(other.Column + other.Shift);
                    float oy = PowerLayout.RowY(4 - (int)other.Rank);
                    Check(Math.Abs(x - ox) >= PowerLayout.TileSize || Math.Abs(y - oy) >= PowerLayout.CellHeight,
                        "tiles never overlap: " + perk.Key + "/" + other.Key);
                }
            }
            Check(upgrades == 19, "19 upgrade slots");
            Check(Tree.Find("Counterattack").Costs[0] == 8 && Tree.Find("LawOfTheLamp").Costs[0] == 8,
                "both ultimate perks cost eight points");
            Check(Tree.Find("Unleashed").Costs[0] == 8 && Tree.Find("GenieLaw").Costs[0] == 8,
                "both Rank V paths use equal eight-point perk prices");
            Check(string.Join(",", Array.ConvertAll(rows, delegate(int n) { return n.ToString(); })) == "3,2,4,2,4", "five-rank silhouette");
            Check(Tree.Find("DomesticDjinn").Column + Tree.Find("DomesticDjinn").Shift == 0.5f
                && Tree.Find("AThousandAndOneNights").Column + Tree.Find("AThousandAndOneNights").Shift == 1.5f
                && Tree.Find("FinePrint").Column + Tree.Find("FinePrint").Shift == 2.5f, "three centered base options in one row");
            Check(Tree.Find("Unbound").Face == GenieTree.Unbound && Tree.Find("Counterattack").Face == GenieTree.Unbound, "Path of the Unbound is red");
            Check(Tree.Find("Lampbound").Face == GenieTree.Lampbound && Tree.Find("LawOfTheLamp").Face == GenieTree.Lampbound, "Path of the Lampbound is blue");
            float counter = Tree.Find("Counterattack").Column + Tree.Find("Counterattack").Shift;
            float law = Tree.Find("LawOfTheLamp").Column + Tree.Find("LawOfTheLamp").Shift;
            Check(counter > 0f && counter < law && law < 3f && Tree.Find("Counterattack").Rank == Tree.Find("Unleashed").Rank
                && Tree.Find("LawOfTheLamp").Rank == Tree.Find("GenieLaw").Rank, "both ultimates sit between the paths on the Rank V row");
            Check(Tree.Paths.Length == 2 && Tree.Connections.Length == 9, "two named paths and nine connection groups");
            int sides = 0, segments = 0;
            foreach (PerkConnection connection in Tree.Connections)
            {
                if (connection.Side)
                {
                    sides++;
                    Check(connection.Lower[0].Rank == connection.Upper[0].Rank, "side connectors stay on one rank");
                    segments++;
                    continue;
                }
                Rank lower = connection.Lower[0].Rank;
                Rank upper = connection.Upper[0].Rank;
                Check((int)upper == (int)lower + 1, "each connection climbs one rank");
                float minimum = float.MaxValue, maximum = float.MinValue;
                foreach (Perk[] side in new[] { connection.Lower, connection.Upper })
                    foreach (Perk perk in side)
                    {
                        float lane = perk.Column + perk.Shift;
                        minimum = Math.Min(minimum, lane);
                        maximum = Math.Max(maximum, lane);
                    }
                segments += connection.Lower.Length + connection.Upper.Length + (maximum > minimum ? 1 : 0);
                Check(PowerLayout.RowY(4 - (int)lower) > PowerLayout.RowY(4 - (int)upper) + PowerLayout.CellHeight,
                    "connections fit in the gap between tiles");
            }
            Check(sides == 2, "one side connector for each ultimate");
            Check(segments <= PowerLayout.ConnectionSlots, "enough connection controls");
        }

        private static void Selection()
        {
            foreach (string foundation in new[] { "FinePrint", "AThousandAndOneNights", "DomesticDjinn" })
            {
                PowerSelection selection = Select(Record(40), Rank.HighWitch);
                Check(!selection.CanStage(Tree.Find("Unbound")) && !selection.CanStage(Tree.Find("Lampbound")), "paths require a Rank I perk");
                Check(selection.StageNext(Tree.Find(foundation)), "stage any base option");
                Check(selection.CanStage(Tree.Find("Unbound")) && selection.CanStage(Tree.Find("Lampbound")), "one base option opens either path");
                Check(selection.StageNext(Tree.Find("Unbound")), "choose the Path of the Unbound");
                Check(!selection.CanStage(Tree.Find("Lampbound")), "the opposite path locks");
            }
            PowerSelection novice = Select(Record(40), Rank.Novice);
            novice.StageNext(Tree.Find("FinePrint"));
            Check(!novice.CanStage(Tree.Find("Unbound")), "rank requirement still applies");
            PerkRecord power = Record(37); power.SetTier("FinePrint", 1); power.SetTier("Unbound", 1);
            PowerSelection pick = Select(power, Rank.HighWitch);
            Check(!pick.CanStage(Tree.Find("FugitiveOfOld")), "Rank IV waits for a Rank III power perk");
            Check(pick.CanStage(Tree.Find("RisingTide")) && pick.CanStage(Tree.Find("Wellspring")), "both Rank III power perks open after Unbound");
            Check(!pick.CanStage(Tree.Find("PocketPalace")) && !pick.CanStage(Tree.Find("SilkenCommand")), "the other path's perks stay closed");
            foreach (string option in new[] { "RisingTide", "Wellspring" })
            {
                PowerSelection selection = Select(power, Rank.HighWitch);
                Check(selection.StageNext(Tree.Find(option)), "stage either Rank III power perk");
                Check(selection.CanStage(Tree.Find("FugitiveOfOld")), "either Rank III power perk opens Fugitive of Old");
                Check(!selection.CanStage(Tree.Find("Unleashed")), "Unleashed waits for Fugitive of Old");
                selection.StageNext(Tree.Find("FugitiveOfOld"));
                Check(selection.CanStage(Tree.Find("Unleashed")) && !selection.CanStage(Tree.Find("Counterattack")), "Fugitive opens Unleashed but not Counterattack");
                selection.StageNext(Tree.Find("Unleashed"));
                Check(selection.CanStage(Tree.Find("Counterattack")), "Unleashed opens Counterattack");
            }
            PerkRecord lamp = Record(37); lamp.SetTier("FinePrint", 1); lamp.SetTier("Lampbound", 1);
            PowerSelection genie = Select(lamp, Rank.HighWitch);
            Check(genie.CanStage(Tree.Find("PocketPalace")) && genie.CanStage(Tree.Find("SilkenCommand")), "both Rank III Genie perks open after Lampbound");
            Check(!genie.CanStage(Tree.Find("HeartsDesire")) && !genie.CanStage(Tree.Find("GenieLaw")), "Heart's Desire needs a Rank III Genie perk");
            Check(!genie.CanStage(Tree.Find("RisingTide")) && !genie.CanStage(Tree.Find("Wellspring")), "power perks stay closed on the Lampbound path");
            genie.StageNext(Tree.Find("SilkenCommand"));
            Check(genie.CanStage(Tree.Find("HeartsDesire")) && !genie.CanStage(Tree.Find("GenieLaw")), "either Genie perk opens Heart's Desire and no further");
            genie.StageNext(Tree.Find("HeartsDesire"));
            Check(genie.CanStage(Tree.Find("GenieLaw")) && !genie.CanStage(Tree.Find("LawOfTheLamp")), "Heart's Desire opens Genie Law");
            genie.StageNext(Tree.Find("GenieLaw"));
            Check(genie.CanStage(Tree.Find("LawOfTheLamp")), "Genie Law opens Law of the Lamp");
            PowerSelection adept = Select(lamp, Rank.Adept);
            Check(adept.CanStage(Tree.Find("PocketPalace")), "Rank III Genie perks need Rank III");
            adept.StageNext(Tree.Find("PocketPalace"));
            Check(!adept.CanStage(Tree.Find("HeartsDesire")), "Rank IV waits for the Rank IV title");
            PerkRecord upgrade = Record(37); upgrade.SetTier("Unbound", 1); upgrade.SetTier("FinePrint", 1);
            PowerSelection wellspring = Select(upgrade, Rank.Adept);
            Check(wellspring.StageNext(Tree.Find("Wellspring")) && wellspring.StageNext(Tree.Find("Wellspring"))
                && wellspring.StageNext(Tree.Find("Wellspring")) && !wellspring.StageNext(Tree.Find("Wellspring")), "Wellspring has three upgrades");
        }

        private static void Purchases()
        {
            Dictionary<ulong, List<int>> table = new Dictionary<ulong, List<int>>();
            table[1] = Record(40).Encode();
            PerkLedger ledger = new PerkLedger(delegate { return table; });
            Perk preview = Tree.Find("FinePrint");
            preview.PreviewOnly = true;
            try
            {
                Check(!Select(Record(40), Rank.HighWitch).CanStage(preview), "Preview perk was staged");
                Check(ledger.Purchase(1, Tree, Batch("FinePrint")) == null && ledger.ReadCommitted(1).Available == 40,
                    "Preview perk could be purchased directly");
            }
            finally { preview.PreviewOnly = false; }
            Check(ledger.Purchase(1, Tree, Batch("Unbound")) == null, "ledger rejects a path without a Rank I perk");
            Check(ledger.ReadCommitted(1).Available == 40, "rejected purchase spends nothing");
            Check(ledger.Purchase(1, Tree, Batch("FinePrint", "Unbound", "Lampbound")) == null, "atomic batch rejects opposite paths");
            Check(ledger.ReadCommitted(1).TierOf("FinePrint") == 0, "rejected batch grants nothing");
            Check(ledger.Purchase(1, Tree, Batch("FinePrint", "Unbound", "Unleashed")) == null, "ledger rejects skipping Fugitive of Old");
            Check(ledger.Purchase(1, Tree, Batch("FinePrint", "Unbound", "RisingTide", "FugitiveOfOld", "Counterattack")) == null, "ledger rejects Counterattack without Unleashed");
            PerkRecord bought = ledger.Purchase(1, Tree, Batch("FinePrint", "Unbound", "RisingTide", "FugitiveOfOld", "Unleashed", "Counterattack"));
            Check(bought != null && bought.Available == 14 && bought.TierOf("Counterattack") == 1, "the whole Path of the Unbound succeeds at exact cost");
            Check(ledger.Purchase(1, Tree, Batch("PocketPalace")) == null, "ledger rejects a perk of the opposite path");
            table[2] = Record(40).Encode();
            PerkRecord sworn = ledger.Purchase(2, Tree, Batch("FinePrint", "Lampbound", "PocketPalace", "HeartsDesire", "GenieLaw", "LawOfTheLamp"));
            Check(sworn != null && sworn.Available == 10 && sworn.TierOf("LawOfTheLamp") == 1, "the whole Path of the Lampbound succeeds at exact cost");
            table[5] = Record(40).Encode();
            Dictionary<string, int> fullUnbound = Batch("DomesticDjinn", "FinePrint", "Unbound", "RisingTide",
                "FugitiveOfOld", "Unleashed", "Counterattack");
            fullUnbound.Add("AThousandAndOneNights", 3);
            fullUnbound.Add("Wellspring", 3);
            PerkRecord maxUnbound = ledger.Purchase(5, Tree, fullUnbound);
            Check(maxUnbound != null && maxUnbound.Available == 0 && maxUnbound.TierOf("Counterattack") == 1,
                "all Unbound perks and upgrades spend exactly 40 points");
            table[6] = Record(40).Encode();
            Dictionary<string, int> fullLampbound = Batch("DomesticDjinn", "FinePrint", "Lampbound", "PocketPalace",
                "SilkenCommand", "HeartsDesire", "GenieLaw", "LawOfTheLamp");
            fullLampbound.Add("AThousandAndOneNights", 3);
            PerkRecord maxLampbound = ledger.Purchase(6, Tree, fullLampbound);
            Check(maxLampbound != null && maxLampbound.Available == 0 && maxLampbound.TierOf("LawOfTheLamp") == 1,
                "all Lampbound perks and upgrades spend exactly 40 points");
            table[3] = Record(40).Encode();
            Check(ledger.Purchase(3, Tree, Batch("Lampbound", "PocketPalace", "LawOfTheLamp")) == null, "ledger rejects Law of the Lamp without Genie Law");
            table[4] = Record(40).Encode();
            Dictionary<string, int> deep = Batch("DomesticDjinn", "Unbound");
            deep.Add("Wellspring", 3);
            deep.Add("AThousandAndOneNights", 3);
            PerkRecord upgraded = ledger.Purchase(4, Tree, deep);
            Check(upgraded != null && upgraded.Available == 40 - 2 - 2 - 9 - 3 && upgraded.TierOf("Wellspring") == 3, "multi-rank perks charge each upgrade");
        }

        private static void Migration()
        {
            GenieProgressStore.sRecords.Clear();
            PerkRecord old = Record(20);
            old.SetTier("FinePrint", 1); old.SetTier("Unbound", 1); old.SetTier("Wellspring", 2);
            old.SetTier("StolenSecrets", 1); old.SetTier("RawPower", 1); old.SetTier("HomeSweetLamp", 1);
            GenieProgressStore.sRecords[1] = old.Encode();
            PerkRecord migrated = GenieProgressStore.Ledger.ReadCommitted(1);
            Check(migrated.Available == 37, "refund exact retired-perk prices");
            Check(migrated.TierOf("Unbound") == 1 && migrated.TierOf("FinePrint") == 1, "retained purchases preserved");
            Check(migrated.Earned == 40 && migrated.Claimed == PerkProgression.AllMask, "earned points and milestones preserved");
            Check(migrated.TierOf("StolenSecrets") == 0 && migrated.TierOf("RawPower") == 0 && migrated.TierOf("Wellspring") == 0, "retired ownership removed");
            Check(GenieProgressStore.Ledger.ReadCommitted(1).Available == 37, "refund is not repeated");
            migrated.SetTier("RisingTide", 1);
            migrated.Available -= 3;
            GenieProgressStore.sRecords[1] = migrated.Encode();
            Check(GenieProgressStore.Ledger.ReadCommitted(1).Available == 34 && GenieProgressStore.Ledger.ReadCommitted(1).TierOf("RisingTide") == 1, "new-tree purchases are never refunded again");
            PerkRecord all = Record(11);
            foreach (string key in new[] { "StolenSecrets", "RawPower", "HomeSweetLamp", "BottledUp", "EvenKeel", "Wellspring" }) all.SetTier(key, 1);
            all.SetTier("CushionedFall", 3);
            GenieProgressStore.sRecords[2] = all.Encode();
            Check(GenieProgressStore.Ledger.ReadCommitted(2).Available == 40, "all seven early perks refund every purchased upgrade");
            PerkRecord reworked = Record(3);
            foreach (string key in new[] { "HardLessons", "SilverLining", "FairExchange", "Overdraw", "TemperedMagic", "Unshaken", "ForbiddenLessons", "BindingOath", "Unleashed", "GenieLaw" }) reworked.SetTier(key, 1);
            GenieProgressStore.sRecords[7] = reworked.Encode();
            migrated = GenieProgressStore.Ledger.ReadCommitted(7);
            Check(migrated.Available == 40 && migrated.TierOf("Unleashed") == 0 && migrated.TierOf("GenieLaw") == 0 && migrated.TierOf("Overdraw") == 0,
                "every perk the 15-perk tree dropped or reworked is refunded and cleared");
            PerkRecord powerSilken = Record(33); powerSilken.SetTier("FinePrint", 1); powerSilken.SetTier("Unbound", 1); powerSilken.SetTier("SilkenCommand", 1);
            GenieProgressStore.sRecords[3] = powerSilken.Encode();
            migrated = GenieProgressStore.Ledger.ReadCommitted(3);
            Check(migrated.Available == 37 && migrated.TierOf("SilkenCommand") == 0, "old shared Silken Command refunded outside the Lampbound path");
            PerkRecord lamp = Record(34); lamp.SetTier("Lampbound", 1); lamp.SetTier("SilkenCommand", 1);
            GenieProgressStore.sRecords[4] = lamp.Encode();
            migrated = GenieProgressStore.Ledger.ReadCommitted(4);
            Check(migrated.Available == 34 && migrated.TierOf("SilkenCommand") == 1, "Silken Command preserved on the Lampbound path");
            PerkRecord orphan = Record(36); orphan.SetTier("Lampbound", 1); orphan.SetTier("HeartsDesire", 1);
            GenieProgressStore.sRecords[8] = orphan.Encode();
            migrated = GenieProgressStore.Ledger.ReadCommitted(8);
            Check(migrated.Available == 40 && migrated.TierOf("HeartsDesire") == 0, "Heart's Desire without a Rank III perk is refunded");
            PerkRecord inconsistent = Record(39); inconsistent.SetTier("RawPower", 1);
            GenieProgressStore.sRecords[5] = inconsistent.Encode();
            Check(GenieProgressStore.Ledger.ReadCommitted(5).Available == 40, "refund never creates points above earned total");
            List<int> truncated = Record(40).Encode();
            truncated[4] = 100;
            GenieProgressStore.sRecords[9] = truncated;
            Check(GenieProgressStore.Ledger.ClaimMilestones(9, 0, null) == null &&
                Object.ReferenceEquals(GenieProgressStore.sRecords[9], truncated), "Truncated data was overwritten");
            List<int> future = Record(40).Encode(); future[0] = 99;
            GenieProgressStore.sRecords[6] = future;
            Check(GenieProgressStore.Ledger.ReadCommitted(6) == null && GenieProgressStore.sRecords[6][0] == 99, "unknown future save version left untouched");
        }

        private static void MergedBenefits()
        {
            PerkRecord record = Record(40);
            foreach (string key in new[] { "DomesticDjinn", "Unbound", "RisingTide", "FugitiveOfOld", "Unleashed", "Counterattack", "Lampbound", "SilkenCommand", "GenieLaw", "LawOfTheLamp" }) record.SetTier(key, 1);
            record.SetTier("Wellspring", 2); record.SetTier("AThousandAndOneNights", 3);
            CastingRules rules = GeniePerks.Apply(record, new CastingRules(Rank.HighWitch, CasterKind.Genie));
            Check(rules.Unbound && !rules.Lampbound && rules.DomesticDjinn && rules.RisingTide && rules.Fugitive && rules.Unleashed && rules.Counterattack, "the Path of the Unbound effects apply");
            Check(rules.Wellspring == 2 && rules.Nights == 3 && rules.ExtraTiers == 2 && rules.HighestLearnableTier == 4, "tiers, Wellspring and Nights apply");
            Check(!rules.SilkenCommand && !rules.GenieLaw && !rules.LawOfTheLamp, "the other path's effects do not leak");
            PerkRecord genie = Record(40);
            foreach (string key in new[] { "Lampbound", "SilkenCommand", "GenieLaw", "LawOfTheLamp", "Wellspring", "RisingTide" }) genie.SetTier(key, 1);
            rules = GeniePerks.Apply(genie, new CastingRules(Rank.HighWitch, CasterKind.Genie));
            Check(rules.Lampbound && rules.SilkenCommand && rules.GenieLaw && rules.LawOfTheLamp, "the Path of the Lampbound effects apply");
            Check(!rules.Unbound && rules.Wellspring == 0 && !rules.RisingTide && rules.ExtraTiers == 0, "power perks never leak onto the Lampbound path");
            PerkRecord loose = Record(40); loose.SetTier("Unbound", 1); loose.SetTier("Counterattack", 1); loose.SetTier("Lampbound", 1);
            rules = GeniePerks.Apply(loose, new CastingRules(Rank.HighWitch, CasterKind.Genie));
            Check(!rules.Counterattack && !rules.Unleashed, "Counterattack does nothing without Unleashed");
            PerkRecord lawless = Record(40); lawless.SetTier("Lampbound", 1); lawless.SetTier("LawOfTheLamp", 1);
            Check(!GeniePerks.Apply(lawless, new CastingRules(Rank.HighWitch, CasterKind.Genie)).LawOfTheLamp, "Law of the Lamp does nothing without Genie Law");
            Check(Tree.BenefitsFor(Tree.Find("Wellspring"), 2)[0].Text.Contains("30%"), "Wellspring's last upgrade shows its own total");
            Check(Tree.BenefitsFor(Tree.Find("AThousandAndOneNights"), 1)[0].Text.Contains("20%"), "Nights' second upgrade shows its own total");
            Check(Tree.BenefitsFor(Tree.Find("Counterattack"), 0).Length == 2, "Counterattack shows only its two lines");
            Check(Tree.BenefitsFor(Tree.Find("Lampbound"), 0).Length == 3 && Tree.BenefitsFor(Tree.Find("Unbound"), 0).Length == 4, "path perks list every effect");
            foreach (Perk perk in Tree.Perks)
                Check(Tree.BenefitsFor(perk, 0).Length > 0, "every perk lists its stat lines: " + perk.Key);
        }
    }
}
