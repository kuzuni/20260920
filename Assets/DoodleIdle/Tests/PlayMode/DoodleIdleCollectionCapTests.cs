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
        public IEnumerator EquipmentCopiesStartAtTwoRiseEveryUpgradeAndBatchMatchesIndividualPurchases()
        {
            game.TogglePause();var ui=game.Ui;
            int[] copies={1,2,4,5,188,189,208,209,1808,1809};
            int[] levels={1,2,2,3,18,19,19,20,99,100};
            int[] remainders={1,0,2,0,18,0,19,0,19,0};
            foreach(string category in new[]{"Armor","Club","Necklace"}) {
                var item=ui.Items(category).First();item.discovered=true;
                for(int i=0;i<copies.Length;i++) {
                    item.level=1;item.count=copies[i];
                    while(ui.UpgradeItem(item,false)) {}
                    Assert.That(item.level,Is.EqualTo(levels[i]),category+" individual budget "+copies[i]);
                    Assert.That(item.count,Is.EqualTo(remainders[i]));
                    item.level=1;item.count=copies[i];
                    int upgraded=(int)typeof(DoodleUi).GetMethod("UpgradeItemBatch",GrowthPrivate).Invoke(ui,new object[]{item});
                    Assert.That(upgraded,Is.EqualTo(levels[i]-1),category+" batch budget "+copies[i]);
                    Assert.That(item.level,Is.EqualTo(levels[i]));Assert.That(item.count,Is.EqualTo(remainders[i]));
                }
                item.level=1;item.count=2;
                Assert.That(ui.CanUpgradeItem(item),Is.True);Assert.That(ui.UpgradeItem(item),Is.True);
                Assert.That(ui.CopiesNeeded(item),Is.EqualTo(3));Assert.That(ui.CanUpgradeItem(item),Is.False);
                item.count=3;Assert.That(ui.CanUpgradeItem(item),Is.True);
                typeof(DoodleUi).GetField("equipmentCategory",GrowthPrivate).SetValue(ui,category);
                UiOpen("Equipment");yield return null;
                var slot=UiNode("Slot: "+item.name,UiNode("Collection inventory"));
                Assert.That(slot.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("3/3")||x.text.Contains("3 / 3")),Is.True);
            }
            foreach(string category in new[]{"Skill","Companion"}) {
                var item=ui.Items(category).First();item.level=1;Assert.That(ui.CopiesNeeded(item),Is.EqualTo(5));
                item.level=11;Assert.That(ui.CopiesNeeded(item),Is.EqualTo(6));
            }
        }

        [UnityTest]
        public IEnumerator BulkEquipmentSkillsAndCompanionsUseExactBatchedCostsAndProgress()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Armor", "Club", "Necklace", "Skill", "Companion" }) {
                var items = ui.Items(category);
                foreach (var item in items) { item.discovered = true; item.level = 99; item.count = 20; }
                string metric = DoodleUi.IsEquipmentCategory(category) ? "equipmentUpgrade" : category == "Skill" ? "skillUpgrade" : "companionUpgrade";
                long before = ui.CareerProgress(metric); long expected = items.Count;
                if (DoodleUi.IsEquipmentCategory(category)) {
                    var god = items.Last(); god.level = 151; god.count = 2000000007;
                    expected += 100000000 - 1;
                }
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var routine = (IEnumerator)typeof(DoodleUi).GetMethod("UpgradeCollectionBulk", GrowthPrivate).Invoke(ui, new object[] { category });
                Assert.That(routine.MoveNext(), Is.False, "Deterministic upgrades must finish without one iteration or yield per level.");
                foreach (var item in items) {
                    Assert.That(item.level, Is.EqualTo(item.rarity == 8 ? 100000151 : 100));
                    Assert.That(item.count, Is.EqualTo(item.rarity == 8 ? 7 : DoodleUi.IsEquipmentCategory(category) ? 0 : 6));
                }
                Assert.That(ui.CareerProgress(metric), Is.EqualTo(before + expected));
                Debug.Log("Bulk " + category + " benchmark: " + expected + " upgrades, " + timer.ElapsedMilliseconds + " ms including save.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GodEquipmentUpgradeCopiesNeverExceedTwenty()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Armor", "Club", "Necklace" }) {
                var god = ui.Items(category).Single(x => x.rarity == 8 && x.tier == 1);
                god.discovered = true;
                foreach (int level in new[] { 1, 2, 18, 19, 20, 100, 150, 151, 160, 161, 500, 1000, 100000000, int.MaxValue - 1 }) {
                    god.level = level;
                    int expected = level >= 19 ? 20 : level + 1;
                    Assert.That(ui.CopiesNeeded(god), Is.EqualTo(expected));
                    god.count = expected - 1;
                    Assert.That(ui.UpgradeItem(god), Is.False);
                    Assert.That(god.count, Is.EqualTo(expected - 1));
                    god.count = expected;
                    Assert.That(ui.UpgradeItem(god), Is.True);
                    Assert.That(god.level, Is.EqualTo(level + 1));
                    Assert.That(god.count, Is.Zero);
                }
                god.level = 540; god.count = 20;
                typeof(DoodleUi).GetField("equipmentCategory", GrowthPrivate).SetValue(ui, category);
                typeof(DoodleUi).GetField("selected" + category, GrowthPrivate).SetValue(ui, god.id);
                UiOpen("Equipment"); yield return null;
                var slot = UiNode("Slot: " + god.name, UiNode("Collection inventory"));
                Assert.That(slot.GetComponentsInChildren<Text>().Any(x => x.text.Contains("/20") || x.text.Contains("/ 20")), Is.True, "Inventory must show the capped copy requirement.");
                Object.Destroy(CaptureFrame("god-upgrade-cost-" + category.ToLowerInvariant() + ".png", 720, 1520));
            }
        }

        [UnityTest]
        public IEnumerator AbilityBulkRefundRequiresEntireCategoryMaxedAndPreservesUnpaidCopies()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Skill", "Companion" }) {
                var items = ui.Items(category);
                foreach (var item in items) { item.discovered = true; item.level = 100; item.count = 2; }
                items.Last().level = 99; ui.Diamonds = 100;
                Assert.That(ui.CollectionFullyMaxed(category), Is.False);
                Assert.That(ui.CollectionRefundQuote(category), Is.Zero);
                Assert.That(ui.RefundCollection(category), Is.Zero);
                if(category=="Skill") {
                    Assert.That(ui.CanRefundSkill(items[0]),Is.False);
                    Assert.That(ui.SkillRefundQuote(items[0]),Is.Zero);
                    Assert.That(ui.RefundSkill(items[0]),Is.Zero);
                    ui.AutoEquip("Skill");UiOpen("Skills");
                    AssertBadge(UiNode("Slot: "+items[0].name,UiNode("Collection inventory")),false);
                    UiClick("Slot: "+items[0].name,UiNode("Collection inventory"));
                    Assert.That(UiRoot.GetComponentsInChildren<Button>().Any(x=>x.name.StartsWith("환불")),Is.False);
                    ui.CloseDetail();
                }
                Assert.That(items.All(x => x.count == 2), Is.True);
                UiOpen(category == "Skill" ? "Skills" : "Companions");
                Assert.That(UiNode("Collection actions").GetComponentsInChildren<Button>().Any(x => x.name == "일괄 환불"), Is.False);
                items.Last().level = 100; items.Last().discovered = false;
                Assert.That(ui.RefundCollection(category), Is.Zero, "Unowned entries also prevent bulk refunds.");
                items.Last().discovered = true;
                int unit = ui.SummonCost(category, 1), expected = items.Count * 2 * unit;
                Assert.That(ui.CollectionRefundQuote(category), Is.EqualTo(expected));
                UiOpen(category == "Skill" ? "Skills" : "Companions"); yield return null;
                Object.Destroy(CaptureFrame(category.ToLowerInvariant() + "-bulk-refund.png", 720, 1520));
                UiClick("일괄 환불", UiNode("Collection actions"));
                Assert.That(ui.Diamonds, Is.EqualTo(100 + expected));
                Assert.That(items.All(x => x.count == 0 && x.level == 100 && x.discovered), Is.True);
                Assert.That(UiNode("일괄 환불", UiNode("Collection actions")).GetComponent<Button>().interactable, Is.False);
                Assert.That(ui.RefundCollection(category), Is.Zero, "A repeated click cannot refund twice.");
                var host = new GameObject("Refund collection reload");
                try {
                    var reload = host.AddComponent<DoodleUi>(); reload.InitCollections();
                    Assert.That(reload.Items(category).All(x => x.count == 0 && x.level == 100), Is.True);
                }
                finally { Object.DestroyImmediate(host); }
                items[0].count = items[1].count = 5;
                ui.Diamonds = int.MaxValue - unit * 3 - 1;
                Assert.That(ui.CollectionRefundQuote(category), Is.EqualTo(unit * 3));
                Assert.That(ui.RefundCollection(category), Is.EqualTo(unit * 3));
                Assert.That(ui.Diamonds, Is.EqualTo(int.MaxValue - 1));
                Assert.That(items.Sum(x => x.count), Is.EqualTo(7), "Only paid copies may be consumed.");
                Assert.That(ui.RefundCollection(category), Is.Zero);
                Assert.That(items.Sum(x => x.count), Is.EqualTo(7));
            }
            Assert.That(ui.RefundCollection("Armor"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator EquipmentOwnershipSummaryShowsOnlyCurrentTabStat()
        {
            game.TogglePause(); var ui = game.Ui;
            var categories = new[] { "Armor", "Club", "Necklace" };
            var labels = new[] { "체력", "공격력", "체력 회복" };
            for (int i = 0; i < categories.Length; i++) {
                string category = categories[i];
                foreach (var item in ui.Items(category)) { item.discovered = true; item.level = 10; }
                typeof(DoodleUi).GetField("equipmentCategory", GrowthPrivate).SetValue(ui, category);
                UiOpen("Equipment"); yield return null;
                string text = UiNode("Total ownership").GetComponentInChildren<Text>().text;
                Assert.That(text, Does.StartWith("총 보유 효과   " + labels[i] + " +"));
                Assert.That(text, Does.Not.Contain("골드"));
                Assert.That(text, Does.Not.Contain(" · "));
                Object.Destroy(CaptureFrame("equipment-ownership-" + category.ToLowerInvariant() + ".png", 720, 1520));
            }
        }

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
            foreach (var relic in ui.AllRelics) { relic.discovered = true; relic.level = 100000000; }
            UiOpen("Relics"); yield return null;
            var labels = ui.GetComponentsInChildren<Text>().Where(x => x.text.StartsWith("Lv. ")).ToArray();
            Assert.That(labels.Length, Is.GreaterThan(0));
            Assert.That(labels.All(x => x.text == "Lv. 100,000,000"), Is.True);
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

        [UnityTest]
        public IEnumerator AllRelicsUpgradeBeyond1000AndStopAtOneHundredMillion()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (var relic in ui.AllRelics) {
                Assert.That(ui.ItemMaxLevel(relic), Is.EqualTo(100000000));
                relic.discovered = true; relic.level = 1000; relic.count = 100;
                while (relic.level == 1000 && relic.count > 0)
                    Assert.That(ui.TryUpgradeRelic(relic, out _, false), Is.True);
                Assert.That(relic.level, Is.EqualTo(1001), relic.id);
                relic.level = 99999999; relic.count = 100;
                while (relic.level < 100000000 && relic.count > 0)
                    Assert.That(ui.TryUpgradeRelic(relic, out _, false), Is.True);
                Assert.That(relic.level, Is.EqualTo(100000000), relic.id);
                int before = relic.count;
                Assert.That(ui.TryUpgradeRelic(relic, out _, false), Is.False);
                Assert.That(ui.CanUpgradeItem(relic), Is.False);
                Assert.That(relic.count, Is.EqualTo(before));
            }
            ui.SaveCollections();
            var host = new GameObject("Relic high level reload");
            try {
                var restored = host.AddComponent<DoodleUi>(); restored.InitCollections();
                Assert.That(restored.AllRelics.All(x => x.level == 100000000), Is.True);
            }
            finally { Object.DestroyImmediate(host); }
            yield return (IEnumerator)typeof(DoodleUi).GetMethod("UpgradeCollectionBulk", GrowthPrivate).Invoke(ui, new object[] { "Relic" });
            Assert.That(ui.AllRelics.All(x => x.level == 100000000), Is.True);
        }
    }
}
