using System.Collections;
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
        public IEnumerator SkillsAndCompanionsCapAt100AndSynthesizeThroughEveryTier()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Skill", "Companion" }) {
                var items = ui.Items(category);
                for (int i = 0; i < items.Count; i++) {
                    var item = items[i];
                    Assert.That(ui.ItemMaxLevel(item), Is.EqualTo(100), item.id);
                    item.discovered = true; item.level = 99; item.count = 100;
                    Assert.That(ui.SynthesizeItem(item), Is.Zero);
                    int cost = ui.CopiesNeeded(item);
                    Assert.That(ui.UpgradeItem(item), Is.True);
                    Assert.That(item.level, Is.EqualTo(100));
                    Assert.That(item.count, Is.EqualTo(100 - cost));
                    Assert.That(ui.UpgradeItem(item), Is.False);
                    Assert.That(ui.CanUpgradeItem(item), Is.False);
                    Assert.That(item.count, Is.EqualTo(100 - cost));
                    if (i == items.Count - 1) {
                        Assert.That(ui.SynthesisTarget(item), Is.Null);
                        Assert.That(ui.SynthesizeItem(item, true), Is.Zero);
                        continue;
                    }
                    var next = items[i + 1];
                    Assert.That(ui.SynthesisTarget(item), Is.SameAs(next), item.id);
                    item.count = 4;
                    Assert.That(ui.CanSynthesize(item), Is.False);
                    Assert.That(ui.SynthesizeItem(item), Is.Zero);
                    item.count = 14; int before = next.count;
                    Assert.That(ui.SynthesizeItem(item, true), Is.EqualTo(2));
                    Assert.That(item.count, Is.EqualTo(4));
                    Assert.That(item.level, Is.EqualTo(100));
                    Assert.That(next.count, Is.EqualTo(before + 2));
                    Assert.That(next.discovered, Is.True);
                    next.count = int.MaxValue; item.count = 5;
                    Assert.That(ui.CanSynthesize(item), Is.False);
                    Assert.That(ui.SynthesizeItem(item), Is.Zero);
                    Assert.That(item.count, Is.EqualTo(5));
                    next.count = 0;
                }
                // An already-maxed next tier can chain the newly received copies in the same batch.
                foreach (var item in items) item.count = 0;
                items[0].count = 25;
                Assert.That(ui.SynthesizeAll(category), Is.EqualTo(6));
                Assert.That(items[0].count, Is.Zero); Assert.That(items[1].count, Is.Zero);
                Assert.That(items[2].count, Is.EqualTo(1));
                items[0].count = 10;
                UiOpen(category == "Skill" ? "Skills" : "Companions");
                Assert.That(UiNode("일괄 합성", UiNode("Collection actions")), Is.Not.Null);
                typeof(DoodleUi).GetMethod("ShowCollectionDetail", GrowthPrivate).Invoke(ui, new object[] { items[0] });
                yield return null;
                var button = UiNode("합성", UiNode("Detail actions")).GetComponent<Button>();
                Assert.That(button.interactable, Is.True);
                button.onClick.Invoke();
                Assert.That(items[0].count, Is.EqualTo(5));
                Assert.That(items[1].count, Is.EqualTo(1));
                Object.Destroy(CaptureFrame(category.ToLowerInvariant() + "-synthesis-detail.png", 720, 1520));
                ui.CloseDetail();
                if (category == "Companion") {
                    typeof(DoodleUi).GetMethod("ShowCollectionDetail", GrowthPrivate).Invoke(ui, new object[] { items.Last() });
                    Assert.That(UiNode("최대 레벨", UiNode("Detail actions")).GetComponent<Button>().interactable, Is.False);
                    ui.CloseDetail();
                }
                yield return (IEnumerator)typeof(DoodleUi).GetMethod("UpgradeCollectionBulk", GrowthPrivate).Invoke(ui, new object[] { category });
                Assert.That(items.All(x => x.level == 100), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator LegacyCompanionOverCapRefundsUpgradeCopiesOnlyOnce()
        {
            game.TogglePause();
            const string key = "DoodleUi.Collections.v1";
            string saved = PlayerPrefs.GetString(key, "");
            var host = new GameObject("Companion migration");
            var reload = new GameObject("Companion migration reload");
            try {
                PlayerPrefs.SetString(key, "{\"version\":2,\"items\":[{\"id\":\"companion_cat\",\"count\":7,\"level\":102,\"discovered\":true},{\"id\":\"companion_sun\",\"count\":3,\"level\":1000,\"discovered\":true}],\"stats\":[]}");
                var ui = host.AddComponent<DoodleUi>(); ui.InitCollections();
                var cat = ui.Items("Companion").First(x => x.id == "companion_cat");
                var sun = ui.Items("Companion").First(x => x.id == "companion_sun");
                Assert.That(cat.level, Is.EqualTo(100)); Assert.That(cat.count, Is.EqualTo(36));
                int copies = 3;
                for (int level = 100; level < 1000; level++) copies += 5 + (level - 1) / 10;
                Assert.That(sun.level, Is.EqualTo(100)); Assert.That(sun.count, Is.EqualTo(copies));
                ui.SaveCollections();
                var restored = reload.AddComponent<DoodleUi>(); restored.InitCollections();
                Assert.That(restored.Items("Companion").First(x => x.id == cat.id).count, Is.EqualTo(36));
                Assert.That(restored.Items("Companion").First(x => x.id == sun.id).count, Is.EqualTo(copies));
            }
            finally {
                Object.DestroyImmediate(host); Object.DestroyImmediate(reload);
                PlayerPrefs.SetString(key, saved);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CollectionLevelsShowFullNumbersInRelicsStatsInventoryAndSummonDetails()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (var relic in ui.AllRelics) { relic.discovered = true; relic.level = 1000; }
            UiOpen("Relics"); yield return null;
            var labels = ui.GetComponentsInChildren<Text>().Where(x => x.text.StartsWith("Lv. ")).ToArray();
            Assert.That(labels.Length, Is.GreaterThan(0));
            Assert.That(labels.All(x => x.text == "Lv. 1,000"), Is.True);
            Object.Destroy(CaptureFrame("relic-full-level-numbers.png", 720, 1520));
            GrowthLevels["attack"] = 10000;
            UiOpen("Stats");
            Assert.That(UiNode("Stat attack").GetComponentsInChildren<Text>().Any(x => x.text.Contains("Lv.10,000")), Is.True);
            var armor = ui.Items("Armor").Last(); armor.discovered = true; armor.level = 10000;
            UiOpen("Equipment");
            Assert.That(UiNode("Collection inventory").GetComponentsInChildren<Text>().Any(x => x.text == "Lv.10,000"), Is.True);
            typeof(DoodleUi).GetMethod("ShowSummonItem", GrowthPrivate).Invoke(ui, new object[] { armor });
            Assert.That(ui.GetComponentsInChildren<Text>().Any(x => x.text.Contains("· Lv. 10,000")), Is.True);
            ui.CloseDetail();
            yield return null;
        }
    }
}
