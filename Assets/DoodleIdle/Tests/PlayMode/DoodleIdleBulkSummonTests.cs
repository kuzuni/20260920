using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        int CurrentSummonResultCount() => UiNode("SummonResultCards").GetComponentsInChildren<Text>(true)
            .Where(t => t.name == "Draw quantity").Sum(t => int.Parse(t.text.Substring(1), NumberStyles.AllowThousands, CultureInfo.InvariantCulture));

        [UnityTest]
        public IEnumerator BulkSummonsChangeProbabilitiesAtEachExactLevelBoundary()
        {
            game.TogglePause(); var ui = game.Ui; ui.SkipSummonAnimations = true;
            var tuning = (DoodleUi.CommerceTuning)typeof(DoodleUi).GetField("commerceTuning", GrowthPrivate).GetValue(ui);
            tuning.levelExperience = new[] { 10, 500, 100, 200, 300, 400, 500, 600, 700, 20000 };
            tuning.rates = Enumerable.Range(1, 50).Select(level => {
                var weights = new int[9]; weights[(level-1)%9] = DoodleUi.SummonWeightTotal;
                return new DoodleUi.SummonRateTier { level = level, basisPoints = weights };
            }).ToArray();
            var roll = typeof(DoodleUi).GetMethod("RollSummonRewards", GrowthPrivate);
            var rewards = (List<UiItem>)roll.Invoke(ui, new object[] { "Necklace", 10000, new System.Random(14) });
            Assert.That(ui.SummonLevel("Necklace"), Is.EqualTo(1), "Previewing the transaction cannot mutate progression before payment.");
            int start = 0;
            for (int level = 1; level <= 10; level++) {
                int count = level < 10 ? tuning.levelExperience[level-1] : 10000-start;
                Assert.That(rewards.Skip(start).Take(count).All(x => x.rarity == (level-1)%9), Is.True, "Every draw at level " + level);
                start += count;
            }
            int cost = ui.SummonCost("Necklace", 10000);
            ui.Diamonds = cost - 1;
            Assert.That(ui.TrySummon("Necklace", 10000, false), Is.False);
            Assert.That(ui.SummonLevel("Necklace"), Is.EqualTo(1));
            Assert.That(ui.Items("Necklace").Sum(x => x.count), Is.Zero);
            ui.Diamonds = cost;
            Assert.That(ui.TrySummon("Necklace", 10000, false), Is.True);
            Assert.That(ui.Diamonds, Is.Zero);
            Assert.That(ui.SummonLevel("Necklace"), Is.EqualTo(10));
            Assert.That(ui.SummonExperience("Necklace"), Is.EqualTo(6690));
            Assert.That(ui.Items("Necklace").Sum(x => x.count), Is.EqualTo(10000));
            Assert.That(ui.Items("Necklace").Where(x => x.rarity == 1).Sum(x => x.count), Is.EqualTo(500));
            Assert.That(CurrentSummonResultCount(), Is.EqualTo(10000));
            Assert.That(UiNode("SummonResultCards").childCount, Is.LessThanOrEqualTo(36));
            // This fixture uses synthetic XP thresholds; inspect the saved transaction before
            // a fresh loader normalizes it against the production progression thresholds.
            Assert.That(PlayerPrefs.GetString("DoodleUi.Commerce.Necklace"), Does.Contain("\"level\":10"));
            Assert.That(PlayerPrefs.GetString("DoodleUi.Commerce.Necklace"), Does.Contain("6690"));
            yield return null;
            Object.Destroy(CaptureFrame("bulk-summon-10000-level-boundaries.png", 720, 1520));
        }

        [UnityTest]
        public IEnumerator BulkSummonTogglesScalePriceAggregateCountsAndKeepFreeDrawsFixed()
        {
            game.TogglePause(); var ui = game.Ui; ui.SkipSummonAnimations = true;
            var items = ui.Items("Necklace"); ui.AddItem(items[0], 1234);
            typeof(DoodleUi).GetMethod("ShowSummonResults", GrowthPrivate).Invoke(ui, new object[] { "Necklace", new List<UiItem> { items[0], items[0], items[1] } });
            Assert.That(UiNode("SummonResultCards").childCount, Is.EqualTo(2));
            Assert.That(CurrentSummonResultCount(), Is.EqualTo(3), "Only this draw's count appears, not the inventory total.");
            Assert.That(UiNode("SummonResultCards").GetComponentsInChildren<Text>().Any(t => t.text == "일반 1"), Is.True);
            Assert.That(UiNode("SummonResultCards").GetComponentsInChildren<Text>().Any(t => t.text == "×2"), Is.True);
            ui.Diamonds = 20000000;
            foreach (int multiplier in new[] { 1, 10, 100, 1000 }) {
                UiClick("Summon multiplier " + multiplier); yield return null;
                var button = UiNode("PaidSummon" + (50*multiplier));
                Assert.That(button.GetComponentInChildren<Button>().name, Is.EqualTo((50*multiplier) + "회 뽑기"));
                Assert.That(UiNode("SummonDiamondCost", button).GetComponent<Text>().text, Is.EqualTo(ui.SummonCost("Necklace", 50*multiplier).ToString("N0")));
                Assert.That(ui.FreeSummonsRemaining("Necklace"), Is.EqualTo(3));
            }
            var skip = (RectTransform)UiNode("Summon animation skip");
            var toggle = (RectTransform)UiNode("Summon multipliers");
            Canvas.ForceUpdateCanvases();
            Assert.That(skip.position.x, Is.LessThan(toggle.position.x));
            int before = items.Sum(x => x.count), diamonds = ui.Diamonds;
            int price = ui.SummonCost("Necklace", 50000);
            UiClick("50000회 뽑기", UiNode("Fullscreen: 뽑기 결과"));
            Assert.That(items.Sum(x => x.count), Is.EqualTo(before + 50000));
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds-price));
            Assert.That(CurrentSummonResultCount(), Is.EqualTo(50000));
            Assert.That(UiNode("SummonResultCards").childCount, Is.LessThanOrEqualTo(36));
            Assert.That(ui.SummonLevel("Necklace"), Is.EqualTo(50));
            Assert.That(ui.SummonExperience("Necklace"), Is.Zero);
            yield return null;
            Object.Destroy(CaptureFrame("bulk-summon-50000-results.png", 720, 1520));
            before = items.Sum(x => x.count); diamonds = ui.Diamonds;
            UiClick("무료 5회\n뽑기", UiNode("Fullscreen: 뽑기 결과"));
            Assert.That(items.Sum(x => x.count), Is.EqualTo(before + 5));
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds));
            Assert.That(CurrentSummonResultCount(), Is.EqualTo(5));
            Assert.That(ui.TrySummon("Necklace", int.MaxValue, false), Is.False);
        }
    }
}
