using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator NecklaceRecoveryMatchesArmorAndNeitherEquipmentGrantsGold()
        {
            game.TogglePause(); var ui = game.Ui;
            var necklaces = ui.Items("Necklace"); var armors = ui.Items("Armor");
            Assert.That(necklaces.Count, Is.EqualTo(36));
            Assert.That(necklaces.All(x => !x.discovered && !x.equipped), Is.True);
            foreach (var armor in armors) { armor.discovered = armor.equipped = false; armor.level = armor.count = 0; }
            float startingGold = ui.GoldGainMultiplier;
            ui.AddItem(armors[0], 20); ui.AddItem(necklaces[0], 20);
            Assert.That(ui.GoldGainMultiplier, Is.EqualTo(startingGold));
            Assert.That(armors.Concat(necklaces).All(x => x.ownedGoldPercent == 0), Is.True);
            float ownedGold = ui.GoldGainMultiplier;
            ui.AutoEquip("Armor"); ui.AutoEquip("Necklace");
            Assert.That(ui.GoldGainMultiplier, Is.EqualTo(ownedGold));
            foreach (int index in new[] { 0, 5, 10, 15, 20, 25, 30, 33, 35 }) {
                var armor = armors[index]; var necklace = necklaces[index];
                ui.AddItem(armor, 1); ui.AddItem(necklace, 1);
                armor.level = necklace.level = 75;
                foreach (var item in armors.Concat(necklaces)) item.equipped = false;
                float baseHealth = ui.MaxHealth, baseRegen = ui.HealthRegen;
                armor.equipped = true; float addedHealth = ui.MaxHealth - baseHealth;
                // Owned recovery bonuses remain an independent multiplier, like relic recovery.
                float recoveryMultiplier = baseRegen / ui.StatValue("healthRegen");
                necklace.equipped = true;
                Assert.That((ui.HealthRegen - baseRegen) / recoveryMultiplier / addedHealth, Is.EqualTo(.05f).Within(.00001), necklace.name);
                Assert.That(ui.GoldGainMultiplier, Is.EqualTo(ownedGold));
            }
            foreach (var item in armors.Concat(necklaces)) item.equipped = false;
            var first = necklaces[0]; first.level = 1; first.count = 20;
            float beforeGold = ui.GoldGainMultiplier;
            Assert.That(ui.UpgradeItem(first), Is.True);
            Assert.That(ui.GoldGainMultiplier, Is.EqualTo(beforeGold));
            first.count = 0;
            Assert.That(ui.GoldGainMultiplier, Is.EqualTo(beforeGold), "Neither ownership nor enhancement adds gold.");
            ui.AutoEquip("Necklace"); ui.SaveCollections();
            var probes = new List<GameObject>();
            try {
                var restored = GrowthProbe(probes);
                Assert.That(restored.Items("Necklace").Count(x => x.equipped), Is.EqualTo(1));
                Assert.That(restored.Items("Necklace")[0].level, Is.EqualTo(2));
                Assert.That(restored.GoldGainMultiplier, Is.EqualTo(ui.GoldGainMultiplier).Within(.0001));
            } finally { foreach (var probe in probes) Object.Destroy(probe); }
            Assert.That(ui.GoldForMainKills(100, 50), Is.EqualTo(DoodleUi.GoldForMainKills(ui.ReadBalanceTuning(), 100, 50, (double)ui.GoldGainMultiplier * ui.GoldBuffMultiplier)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NecklaceInventorySummoningAndThreeTabsUseCompleteArt()
        {
            game.TogglePause(); var ui = game.Ui;
            var necklaces = ui.Items("Necklace");
            for (int grade = 0; grade < 9; grade++)
                Assert.That(necklaces.Count(x => x.rarity == grade), Is.EqualTo(ui.Items("Armor").Count(x => x.rarity == grade)));
            Assert.That(necklaces.Select(x => UiKit.Art(x.icon).rect).Distinct().Count(), Is.EqualTo(36));
            foreach (var item in necklaces) {
                var sprite = UiKit.Art(item.icon);
                Assert.That(sprite.texture.name, Is.EqualTo("EquipmentNecklace"));
                var rect = sprite.rect; int w = (int)rect.width, h = (int)rect.height;
                var pixels = sprite.texture.GetPixels((int)rect.x, (int)rect.y, w, h);
                Assert.That(pixels.Count(p => p.a > .125f), Is.GreaterThan(w * h / 10));
                Assert.That(NecklaceConnectedPixelFraction(pixels, w, h), Is.GreaterThan(.995f), item.name + " must be one connected necklace, without floating ornaments.");
                for (int x = 0; x < w; x++) { Assert.That(pixels[x].a, Is.LessThan(.13f)); Assert.That(pixels[(h-1)*w+x].a, Is.LessThan(.13f)); }
                for (int y = 0; y < h; y++) { Assert.That(pixels[y*w].a, Is.LessThan(.13f)); Assert.That(pixels[y*w+w-1].a, Is.LessThan(.13f)); }
                ui.AddItem(item, 6);
            }
            for (int level = 1; level <= 50; level++) CollectionAssert.AreEqual(ui.SummonWeights("Armor", level), ui.SummonWeights("Necklace", level));
            UiOpen("Equipment"); UiClick("목걸이"); yield return null;
            Assert.That(UiNode("Equipment tabs").GetComponentsInChildren<Button>().Length, Is.EqualTo(3));
            Assert.That(UiNode("Selected equipment").GetComponentsInChildren<Text>().Any(t => t.text.Contains("골드 +")), Is.False);
            UiClick("자동장착");
            Assert.That(necklaces.Last().equipped, Is.True);
            Object.Destroy(CaptureFrame("necklace-equipment.png", 720, 1520));
            UiClick("갑옷"); yield return null;
            Object.Destroy(CaptureFrame("necklace-armor-owned-gold.png", 720, 1520));
            UiClick("몽둥이"); UiClick("목걸이");
            var motion = UiNode("Equipment tabs").GetComponent<DoodleSlidingSelection>(); motion.CompleteMotion();
            Assert.That(motion.Position, Is.EqualTo(2));
            var selection = (RectTransform)UiNode("Sliding selection", UiNode("Equipment tabs"));
            Assert.That(selection.anchorMin.x, Is.EqualTo(2f/3).Within(.0001));
            ui.SkipSummonAnimations = true; ui.GrantSummonTickets("Necklace", 12); ui.Diamonds = 12345;
            Assert.That(ui.TrySummon("Necklace", 10, false), Is.True);
            Assert.That(ui.SummonTickets("Necklace"), Is.EqualTo(2));
            Assert.That(ui.Diamonds, Is.EqualTo(12345));
            yield return null;
            Object.Destroy(CaptureFrame("necklace-summon-wallet.png", 720, 1520));
            Assert.That(PlayerPrefs.GetString("DoodleUi.Commerce.Necklace"), Does.Contain("\"tickets\":2"));
            Assert.That(DoodleGameData.SaveKeys, Does.Contain("DoodleUi.Commerce.Necklace"));
        }

        static float NecklaceConnectedPixelFraction(Color[] pixels, int width, int height)
        {
            var seen = new bool[pixels.Length];
            var pending = new Queue<int>();
            int total = 0, largest = 0;
            for (int i = 0; i < pixels.Length; i++) {
                if (seen[i] || pixels[i].a <= 32f / 255f) continue;
                int count = 0;
                seen[i] = true; pending.Enqueue(i);
                while (pending.Count > 0) {
                    int p = pending.Dequeue(); count++;
                    int x = p % width, y = p / width;
                    if (x > 0) Visit(p - 1);
                    if (x + 1 < width) Visit(p + 1);
                    if (y > 0) Visit(p - width);
                    if (y + 1 < height) Visit(p + width);
                }
                total += count; largest = Mathf.Max(largest, count);
            }
            return total == 0 ? 0 : (float)largest / total;

            void Visit(int p)
            {
                if (seen[p] || pixels[p].a <= 32f / 255f) return;
                seen[p] = true; pending.Enqueue(p);
            }
        }

        [UnityTest]
        public IEnumerator CriticalTiersChainPricesUnlockInOrderAndUseHighestSuccessfulMultiplier()
        {
            game.TogglePause(); var ui = game.Ui;
            Assert.That(DoodleUi.CriticalStatIds.Length,Is.EqualTo(17));
            Assert.That(DoodleUi.CriticalMultiplierAt(16),Is.EqualTo(131072));
            var tuning = ui.ReadStatCostTuning();
            // Affordable but non-flat curve exercises every tier without wallet saturation.
            tuning.critical2BaseCost = 20; tuning.critical2Growth = .0001f;
            tuning.critical2GrowthSteps = new[] { new DoodleGrowthStep { from = 3900, growth = .0002f }, new DoodleGrowthStep { from = 4500, growth = .9f } };
            ui.ApplyStatCostTuning(tuning);
            Assert.That(DoodleUi.CriticalContinuationGrowth(tuning), Is.EqualTo(.0002f));
            for (int i = 0; i < DoodleUi.CriticalStatIds.Length; i++) {
                string id = DoodleUi.CriticalStatIds[i];
                Assert.That(ui.CriticalUnlocked(id), Is.True);
                if (i > 0) {
                    string previous = DoodleUi.CriticalStatIds[i-1];
                    long priorLast = DoodleUi.StatUpgradePrice(tuning, previous, DoodleUi.CriticalLevelCap(i-1)-1);
                    Assert.That(ui.StatUpgradeQuote(id, 1, out _), Is.EqualTo(priorLast));
                    Assert.That(DoodleUi.StatUpgradePrice(tuning, id, 1), Is.EqualTo((long)Math.Ceiling(priorLast * (1d + .0002f))));
                }
                if (i + 1 < DoodleUi.CriticalStatIds.Length) Assert.That(ui.UpgradeStat(DoodleUi.CriticalStatIds[i+1], 1), Is.False);
                int cap = DoodleUi.CriticalLevelCap(i); GrowthLevels[id] = cap - 1;
                long price = ui.StatUpgradeQuote(id, 100, out int count); Assert.That(count, Is.EqualTo(1));
                ui.Gold = price - 1; Assert.That(ui.UpgradeStat(id, 100), Is.False);
                ui.Gold = price; Assert.That(ui.UpgradeStat(id, 100), Is.True); Assert.That(ui.Gold, Is.Zero);
                Assert.That(ui.StatValue(id), Is.EqualTo(100));
                Assert.That(ui.ExpectedCriticalMultiplier, Is.EqualTo(DoodleUi.CriticalMultiplierAt(i)).Within(.001));
                for (int roll = 0; roll < 20; roll++) Assert.That(game.RollUiCriticalMultiplier(), Is.EqualTo(DoodleUi.CriticalMultiplierAt(i)));
                Assert.That(ui.UpgradeStat(id, 1), Is.False);
            }
            try {ui.BeginCombatSnapshot();Assert.That(game.RollUiCriticalMultiplier(),Is.EqualTo(131072));}
            finally {ui.EndCombatSnapshot();}
            GrowthLevels["crit128Chance"] = 1000;
            Assert.That(ui.ExpectedCriticalMultiplier, Is.EqualTo(96).Within(.001));
            int high = 0; for (int i = 0; i < 500; i++) { float value = game.RollUiCriticalMultiplier(); Assert.That(value == 64f || value == 128f, Is.True); if (value == 128) high++; }
            Assert.That(high, Is.InRange(180,320));
            ui.SaveCollections(); var probes = new List<GameObject>();
            try { var restored = GrowthProbe(probes); Assert.That(restored.StatLevel("crit128Chance"), Is.EqualTo(1000)); Assert.That(restored.CriticalChance(6), Is.EqualTo(50)); }
            finally { foreach (var probe in probes) Object.Destroy(probe); }
            UiOpen("Stats"); yield return null;
            Object.Destroy(CaptureFrame("critical-tier-stats-top.png", 720, 1520));
            var scroll = UiNode("Stat crit128Chance").GetComponentInParent<ScrollRect>(); scroll.verticalNormalizedPosition = 0; yield return null;
            Object.Destroy(CaptureFrame("critical-tier-stats-bottom.png", 720, 1520));
            GrowthLevels["crit2Chance"] = 3999;
            for (int i = 1; i < DoodleUi.CriticalStatIds.Length; i++) Assert.That(ui.CriticalChance(i), Is.Zero);
            Assert.That(DoodleUi.StatUpgradePrice(new UiStatCostTuning(), "crit128Chance", int.MaxValue), Is.EqualTo(long.MaxValue));
        }

        [UnityTest]
        public IEnumerator SummonResultsShowLiveCategoryTicketsAndDiamondsAfterRepeatDraws()
        {
            game.TogglePause(); var ui = game.Ui; ui.SkipSummonAnimations = true;
            foreach (string category in new[] { "Armor", "Club", "Necklace", "Skill", "Companion", "Relic", "DungeonRelic" }) {
                ui.GrantSummonTickets(category, 23); ui.Diamonds = 45678;
                int tickets = ui.SummonTickets(category);
                Assert.That(ui.TrySummonTickets(category, 10), Is.True); yield return null;
                Assert.That(UiNode("Summon result tickets").GetComponentInChildren<Text>().text, Is.EqualTo((tickets-10).ToString("N0") + "장"));
                Assert.That(UiNode("Summon result diamonds").GetComponentInChildren<Text>().text, Is.EqualTo("45,678개"));
                Assert.That(UiNode("Summon result tickets").GetComponentInChildren<Image>().sprite, Is.SameAs(UiKit.Art(DoodleUi.TicketIcon(category))));
                ui.Diamonds -= 321; ui.GrantSummonTickets(category, 2); yield return null;
                Assert.That(UiNode("Summon result tickets").GetComponentInChildren<Text>().text, Is.EqualTo((tickets-8).ToString("N0") + "장"));
                Assert.That(UiNode("Summon result diamonds").GetComponentInChildren<Text>().text, Is.EqualTo("45,357개"));
                Assert.That(ui.TrySummonTickets(category, 10), Is.True); yield return null;
                Assert.That(UiNode("Summon result tickets").GetComponentInChildren<Text>().text, Is.EqualTo((tickets-18).ToString("N0") + "장"));
                if (category == "Armor") Object.Destroy(CaptureFrame("summon-result-current-wallet.png", 720, 1520));
                ui.CloseFullscreen();
            }
            ui.Diamonds = 10000;
            int remaining = ui.SummonTickets("Necklace");
            int diamondCost = ui.SummonDiamondCost("Necklace", 10);
            Assert.That(remaining, Is.LessThan(10));
            Assert.That(ui.TrySummon("Necklace", 10, false), Is.True); yield return null;
            Assert.That(UiNode("Summon result tickets").GetComponentInChildren<Text>().text, Is.EqualTo("0장"));
            Assert.That(UiNode("Summon result diamonds").GetComponentInChildren<Text>().text, Is.EqualTo((10000-diamondCost).ToString("N0") + "개"));
        }
    }
}
