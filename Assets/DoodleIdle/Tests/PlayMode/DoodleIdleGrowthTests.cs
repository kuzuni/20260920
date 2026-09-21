using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        const BindingFlags GrowthPrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        UiCollectionTuning GrowthTuning => (UiCollectionTuning)typeof(DoodleUi).GetField("collectionTuning", GrowthPrivate).GetValue(game.Ui);
        Dictionary<string, int> GrowthLevels => (Dictionary<string, int>)typeof(DoodleUi).GetField("statLevels", GrowthPrivate).GetValue(game.Ui);
        bool GrowthBulkRunning => (bool)typeof(DoodleUi).GetField("collectionBulkRunning", GrowthPrivate).GetValue(game.Ui);

        [UnityTest]
        public IEnumerator GrowthFiveStatsChargeOneHundredExactPurchasesAndRejectAnUnaffordableBatch()
        {
            game.TogglePause();
            var ui = game.Ui;
            CollectionAssert.AreEqual(new[] { "attack", "health", "healthRegen", "crit2Chance", "crit4Chance" }, GrowthTuning.stats.Select(x => x.id).ToArray());
            UiOpen("Stats");
            CollectionAssert.AreEquivalent(new[] { "×1", "×10", "×100", "MAX" }, UiNode("Stat quantity").GetComponentsInChildren<Button>().Select(x => x.name).ToArray());
            foreach (var stat in GrowthTuning.stats) Assert.That(UiNode("Stat " + stat.id), Is.Not.Null);

            var attack = GrowthTuning.stats.Single(x => x.id == "attack");
            int initialLevel = ui.StatLevel("attack"), upgrades;
            long expected = 0;
            for (int i = 0; i < 100; i++) expected += (long)Math.Ceiling(attack.baseCost * Math.Pow(GrowthTuning.costGrowth, initialLevel + i));
            long quoted = ui.StatUpgradeQuote("attack", 100, out upgrades);
            Assert.That(upgrades, Is.EqualTo(100));
            Assert.That(quoted, Is.EqualTo(expected));
            ui.Gold = quoted - 1;
            Assert.That(ui.UpgradeStat("attack", 100), Is.False);
            Assert.That(ui.StatLevel("attack"), Is.EqualTo(initialLevel));
            Assert.That(ui.Gold, Is.EqualTo(quoted - 1));

            ui.Gold = quoted;
            UiClick("×100");
            UiNode("Stat attack").GetComponentsInChildren<Button>().Single().onClick.Invoke();
            Assert.That(ui.StatLevel("attack"), Is.EqualTo(initialLevel + 100));
            Assert.That(ui.Gold, Is.Zero, "The UI batch must charge exactly the same one hundred prices as the quote.");
            Assert.That(ui.CombatDamageMultiplier, Is.GreaterThan(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrowthStatsAffectCombatHealthRegenExpectedPowerAndIndependentToast()
        {
            game.TogglePause();
            var ui = game.Ui;
            ui.Gold = 1000000;
            Assert.That(ui.Critical2Chance, Is.Zero);
            Assert.That(ui.Critical4Chance, Is.Zero);
            Assert.That(ui.CombatAttackSpeedMultiplier, Is.EqualTo(1));
            Assert.That(ui.CombatDamageMultiplier, Is.EqualTo(1).Within(.00001), "Fresh-profile damage must preserve the original combat baseline.");
            foreach (string id in new[] { "attack", "health", "healthRegen", "crit2Chance", "crit4Chance" })
            {
                if (id == "crit4Chance") GrowthLevels["crit2Chance"] = 1000;
                long before = ui.Power;
                float stat = ui.StatValue(id), health = ui.MaxHealth, regen = ui.HealthRegen, damage = ui.CombatDamageMultiplier;
                Assert.That(ui.UpgradeStat(id, 1), Is.True);
                Assert.That(ui.StatValue(id), Is.GreaterThan(stat));
                Assert.That(ui.Power, Is.GreaterThan(before), id + " must increase expected combat power.");
                if (id == "health") Assert.That(ui.MaxHealth, Is.GreaterThan(health));
                if (id == "healthRegen") Assert.That(ui.HealthRegen, Is.GreaterThan(regen));
                if (id == "attack") Assert.That(ui.CombatDamageMultiplier, Is.GreaterThan(damage));
                var toast = UiNode("Power change toast").GetComponent<Text>();
                Assert.That(toast.text, Does.Contain(UiNumber.Format(before) + " → " + UiNumber.Format(ui.Power)));
                Assert.That(toast.text, Does.Contain("(+" + UiNumber.Format(ui.Power - before) + ")"));
                string powerMessage = toast.text;
                ui.Toast("일반 안내");
                Assert.That(toast.text, Is.EqualTo(powerMessage), "An ordinary notice must not overwrite the independent power-change toast.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrowthCriticalChancesCapAtOneHundredAndFourTimesHasPriority()
        {
            game.TogglePause();
            var ui = game.Ui;
            // Seed a boundary profile with affordable test prices, avoiding a huge unrelated
            // economy grind while exercising the real quote, payment and upgrade paths.
            GrowthTuning.costGrowth = 1;
            ui.Gold = 1000000;
            foreach (string id in new[] { "crit2Chance", "crit4Chance" })
            {
                var stat = GrowthTuning.stats.Single(x => x.id == id);
                int cap = Mathf.CeilToInt((100 - stat.initial) / stat.increment);
                GrowthLevels[id] = cap - 1;
                long gold = ui.Gold;
                int count;
                Assert.That(ui.StatUpgradeQuote(id, 100, out count), Is.EqualTo(stat.baseCost));
                Assert.That(count, Is.EqualTo(1));
                Assert.That(ui.UpgradeStat(id, 100), Is.True);
                Assert.That(ui.Gold, Is.EqualTo(gold - stat.baseCost));
                Assert.That(ui.StatValue(id), Is.EqualTo(100));
                gold = ui.Gold;
                Assert.That(ui.UpgradeStat(id, -1), Is.False);
                Assert.That(ui.Gold, Is.EqualTo(gold));
            }
            Assert.That(ui.Critical2Chance, Is.EqualTo(100));
            Assert.That(ui.Critical4Chance, Is.EqualTo(100));
            Assert.That(ui.ExpectedCriticalMultiplier, Is.EqualTo(4).Within(.00001), "Four-times priority must not multiply a simultaneous two-times roll into eight-times damage.");
            GrowthLevels["crit2Chance"] = 200; // 20%
            GrowthLevels["crit4Chance"] = 600; // 30%
            Assert.That(ui.ExpectedCriticalMultiplier, Is.EqualTo(.8 + .2 * 2).Within(.00001));
            var criticalRelic = ui.Items("Relic").Single(x => x.effect == "critDamage");
            criticalRelic.discovered = true; criticalRelic.level = 10;
            Assert.That(ui.CriticalDamageBonus, Is.EqualTo(20));
            Assert.That(ui.ExpectedCriticalMultiplier, Is.EqualTo(.8 + .2 * 2 * 1.2).Within(.00001));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrowthRelicsConsumeOneCopyOnSuccessAndFailureWithoutChargingGold()
        {
            game.TogglePause();
            var ui = game.Ui;
            CollectionAssert.AreEqual(new[] { "attack", "health", "gold", "healthRegen", "critDamage", "basicAttack", "skillAttack", "companionAttack" }, ui.Items("Relic").Select(x => x.effect).ToArray());
            var relic = ui.Items("Relic")[0];
            foreach (int level in new[] { 1, 50, 999 })
            {
                relic.level = level;
                Assert.That(ui.RelicSuccessChance(relic), Is.EqualTo(.5f));
                Assert.That(ui.RelicUpgradeCost(relic), Is.EqualTo(1));
                Assert.That(ui.CopiesNeeded(relic), Is.EqualTo(1), "Summon result gauges must use the same one-copy relic requirement at every level.");
            }
            bool sawSuccess = false, sawFailure = false;
            long wallet = ui.Gold;
            // Cover each outcome, not a noisy statistical percentage. Missing either
            // outcome in 64 independent half-probability rolls has probability 2^-63.
            for (int i = 0; i < 64 && !(sawSuccess && sawFailure); i++)
            {
                relic.discovered = true; relic.level = 10; relic.count = 1;
                bool success;
                Assert.That(ui.TryUpgradeRelic(relic, out success, false), Is.True);
                Assert.That(relic.count, Is.Zero);
                Assert.That(relic.level, Is.EqualTo(success ? 11 : 10));
                Assert.That(relic.discovered, Is.True, "Spending the final duplicate must not erase ownership effects.");
                Assert.That(ui.Gold, Is.EqualTo(wallet));
                sawSuccess |= success; sawFailure |= !success;
            }
            Assert.That(sawSuccess && sawFailure, Is.True, "Both fixed-50% outcome branches must be reachable.");
            bool ignored;
            int before = relic.level;
            Assert.That(ui.TryUpgradeRelic(relic, out ignored), Is.False);
            Assert.That(relic.level, Is.EqualTo(before));
            Assert.That(ui.Gold, Is.EqualTo(wallet));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrowthRelicBulkYieldsAndConsumesUntilEmptyOrMaximumLevel()
        {
            game.TogglePause();
            var ui = game.Ui;
            foreach (var item in ui.Items("Relic")) item.count = 0;
            var relic = ui.Items("Relic")[0];
            relic.level = 1; relic.count = 161; relic.discovered = true;
            long wallet = ui.Gold;
            UiOpen("Relics"); UiClick("일괄강화");
            Assert.That(GrowthBulkRunning, Is.True, "A large batch must yield instead of monopolizing a frame.");
            Assert.That(relic.count, Is.GreaterThan(0).And.LessThan(161));
            int remaining = relic.count;
            UiNode("일괄강화").GetComponent<Button>().onClick.Invoke();
            Assert.That(relic.count, Is.EqualTo(remaining), "The in-flight guard must reject a duplicate bulk request.");
            for (int frame = 0; frame < 20 && GrowthBulkRunning; frame++) yield return null;
            Assert.That(GrowthBulkRunning, Is.False);
            Assert.That(relic.count, Is.Zero);
            Assert.That(ui.Gold, Is.EqualTo(wallet));
            Assert.That(relic.level, Is.InRange(1, 162));

            relic.level = GrowthTuning.maxItemLevel - 1; relic.count = 512;
            UiOpen("Relics"); UiClick("일괄강화");
            for (int frame = 0; frame < 20 && GrowthBulkRunning; frame++) yield return null;
            Assert.That(GrowthBulkRunning, Is.False);
            Assert.That(relic.level, Is.EqualTo(GrowthTuning.maxItemLevel));
            Assert.That(relic.count, Is.GreaterThan(0), "Remaining copies must be preserved at the level cap.");
            remaining = relic.count;
            UiClick("일괄강화");
            Assert.That(relic.count, Is.EqualTo(remaining));
            Assert.That(ui.Gold, Is.EqualTo(wallet));
        }

        [UnityTest]
        public IEnumerator GrowthUndiscoveredSelectionsNeverCreateUpgradeOrEquipActions()
        {
            game.TogglePause();
            UiOpen("Equipment");
            foreach (string category in new[] { "Armor", "Club" })
            {
                if (category == "Club") UiClick("몽둥이", UiNode("Equipment tabs"));
                var item = game.Ui.Items(category).First(x => !x.discovered);
                UiClick("Slot: " + item.name, UiNode("Collection inventory"));
                Assert.That(UiNode("Selected item actions").GetComponentsInChildren<Button>(), Is.Empty);
            }
            foreach (string category in new[] { "Skill", "Companion" })
            {
                UiOpen(category == "Skill" ? "Skills" : "Companions");
                var item = game.Ui.Items(category).First(x => !x.discovered);
                UiClick("Slot: " + item.name, UiNode("Collection inventory"));
                Assert.That(game.Ui.HasOverlay, Is.True);
                Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(t => t.name == "Detail actions"), Is.False);
                Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(t => t.name == "Collection detail footer"), Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrowthLegacyDefenseAndSpeedMigrateOnceWithoutOverwritingNewStatKeys()
        {
            game.TogglePause();
            const string key = "DoodleUi.Collections.v1";
            bool hadSave = PlayerPrefs.HasKey(key);
            string original = PlayerPrefs.GetString(key, "");
            var probes = new List<GameObject>();
            try
            {
                PlayerPrefs.SetString(key, "{\"items\":[],\"stats\":[{\"id\":\"attack\",\"level\":3},{\"id\":\"defense\",\"level\":7},{\"id\":\"speed\",\"level\":9}]}");
                var first = GrowthProbe(probes);
                Assert.That(first.StatLevel("attack"), Is.EqualTo(3));
                Assert.That(first.StatLevel("healthRegen"), Is.EqualTo(7));
                Assert.That(first.StatLevel("crit2Chance"), Is.EqualTo(9));
                Assert.That(first.StatLevel("crit4Chance"), Is.Zero);
                first.SaveCollections();
                string migrated = PlayerPrefs.GetString(key);
                Assert.That(migrated, Does.Contain("\"version\":2"));
                Assert.That(migrated, Does.Not.Contain("\"id\":\"defense\""));
                Assert.That(migrated, Does.Not.Contain("\"id\":\"speed\""));
                var restored = GrowthProbe(probes);
                Assert.That(restored.StatLevel("healthRegen"), Is.EqualTo(7));
                Assert.That(restored.StatLevel("crit2Chance"), Is.EqualTo(9));
                PlayerPrefs.SetString(key, "{\"items\":[],\"stats\":[{\"id\":\"defense\",\"level\":99},{\"id\":\"healthRegen\",\"level\":4},{\"id\":\"speed\",\"level\":10000}]}");
                var mixed = GrowthProbe(probes);
                Assert.That(mixed.StatLevel("healthRegen"), Is.EqualTo(4), "An explicit new-format value takes precedence over its legacy alias.");
                Assert.That(mixed.Critical2Chance, Is.EqualTo(100), "Legacy growth is clamped to the new probability limit.");
            }
            finally
            {
                foreach (var probe in probes) Object.Destroy(probe);
                if (hadSave) PlayerPrefs.SetString(key, original); else PlayerPrefs.DeleteKey(key);
            }
            yield return null;
        }

        static DoodleUi GrowthProbe(List<GameObject> objects)
        {
            var holder = new GameObject("Collection migration probe");
            objects.Add(holder);
            var ui = holder.AddComponent<DoodleUi>();
            ui.InitCollections();
            return ui;
        }
    }
}
