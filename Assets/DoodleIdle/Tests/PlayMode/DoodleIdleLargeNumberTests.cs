using System;
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
        public IEnumerator LargeNumbersKeepGrowingBeyondMachineExponentsAndRoundTrip()
        {
            game.TogglePause();
            Assert.That(GameNumber.TryParse("9.25e999999999999999999999999", out var huge), Is.True);
            var larger = huge * huge * 128;
            Assert.That(larger > huge, Is.True);
            Assert.That((double)((larger / huge) / huge), Is.EqualTo(128).Within(1e-10));
            Assert.That(GameNumber.TryParse(larger.ToString(), out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(larger));
            Assert.That(huge - huge, Is.EqualTo((GameNumber)0));
            Assert.That(huge * 0, Is.EqualTo((GameNumber)0));
            Assert.That((long)huge, Is.EqualTo(long.MaxValue));
            Assert.That((long)(-huge), Is.EqualTo(long.MinValue));
            Assert.That(float.IsInfinity((float)huge), Is.False);
            Assert.That(double.IsInfinity((double)huge), Is.False);
            Assert.That(UiNumber.Format(larger), Does.Not.Contain("∞").And.Not.Contain("NaN"));
            Assert.That(UiNumber.Format(larger), Does.Not.StartWith("-"));
            for (int i = 0; i < 100000; i++) Assert.That((long)(GameNumber)i, Is.EqualTo(i));
            for (int i = 0; i < 400; i++) huge *= huge;
            Assert.That(huge.Exponent > long.MaxValue, Is.True);
            Assert.That(GameNumber.TryParse(huge.ToString(), out restored), Is.True);
            Assert.That(restored, Is.EqualTo(huge));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LargeNumbersDriveActualDamageHealthGoldPricesAndVisiblePower()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (var stat in GrowthTuning.stats) {
                GrowthLevels[stat.id] = DoodleUi.CriticalStatIds.Contains(stat.id) ? 0 : 5000;
                if (!DoodleUi.CriticalStatIds.Contains(stat.id)) stat.valueGrowth = 2;
            }
            foreach (var item in ui.Items("Relic")) { item.discovered = true; item.level = 15000; }
            Assert.That(ui.AttackAmount > (GameNumber)double.MaxValue, Is.True);
            Assert.That(ui.MaxHealthAmount > (GameNumber)double.MaxValue, Is.True);
            Assert.That(ui.HealthRegenAmount > 0, Is.True);
            Assert.That(ui.Power, Is.EqualTo(long.MaxValue), "Legacy integration conversion must saturate, never wrap negative.");
            var power = ui.PowerAmount; GrowthLevels["attack"]++;
            Assert.That(ui.PowerAmount > power, Is.True, "Actual power must continue growing above the legacy integer boundary.");
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var actor = actors[0]; var type = actor.GetType();
            var hit = ui.AttackPercentAmount(100, "Basic");
            type.GetField("hp").SetValue(actor, hit * 10); type.GetField("maxHp").SetValue(actor, hit * 10);
            typeof(DoodleIdleGame).GetMethod("DamageByCategory", GrowthPrivate).Invoke(game, new object[] { actor, 128f, Vector2.zero, "Basic" });
            var hp = (GameNumber)type.GetField("hp").GetValue(actor);
            Assert.That((double)(hp / hit), Is.EqualTo(9).Within(1e-10));
            type.GetField("isBoss").SetValue(actor, true);
            Assert.That(game.BossHealthFraction, Is.EqualTo(.9f).Within(.0001));
            type.GetField("isBoss").SetValue(actor, false);
            typeof(DoodleIdleGame).GetMethod("ResetPlayerContactDamage", GrowthPrivate).Invoke(game, null);
            Assert.That(game.PlayerHealthAmount, Is.EqualTo(ui.MaxHealthAmount));
            Assert.That(game.GetComponentsInChildren<Text>().Where(x => x.name == "Enemy damage number").Any(x => x.text == UiNumber.Format(GameNumber.Ceiling(hit))), Is.True);

            GrowthTuning.statCosts.commonGrowth = 1; GrowthTuning.statCosts.commonGrowthSteps = Array.Empty<DoodleGrowthStep>();
            var price = ui.StatUpgradeQuoteAmount("attack", 1, out var count);
            Assert.That(count, Is.EqualTo(1)); Assert.That(price > (GameNumber)double.MaxValue, Is.True);
            ui.GoldAmount = price * 2;
            Assert.That(ui.UpgradeStat("attack", 1), Is.True);
            Assert.That((double)(ui.GoldAmount / price), Is.EqualTo(1).Within(1e-10));
            ui.Save(); Assert.That(GameNumber.TryParse(PlayerPrefs.GetString("DoodleUi.Gold"), out var saved), Is.True);
            Assert.That(saved, Is.EqualTo(ui.GoldAmount));
            var tuning = ui.ReadBalanceTuning(); tuning.enemyHealthGrowthSteps = Array.Empty<DoodleGrowthStep>(); tuning.enemyHealthStageGrowth = 1;
            tuning.goldGrowthSteps = Array.Empty<DoodleGrowthStep>(); tuning.goldStageGrowth = 1;
            Assert.That(DoodleUi.EnemyHealthAmount(tuning, 5000) > (GameNumber)double.MaxValue, Is.True);
            Assert.That(DoodleUi.GoldForMainKillsAmount(tuning, 5000, 50, 2) > (GameNumber)double.MaxValue, Is.True);
            ui.RefreshHud(); UiOpen("Stats");
            Assert.That(UiNode("Combat power").GetComponentsInChildren<Text>().Any(x => x.text == "전투력 " + UiNumber.Format(ui.PowerAmount)), Is.True);
            Assert.That(UiRoot.GetComponentsInChildren<Text>().All(x => !x.text.Contains("NaN") && !x.text.Contains("∞")), Is.True);
            Object.Destroy(CaptureFrame("large-number-power-and-stats.png", 720, 1520));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LateGameCombatSnapshotPreservesValuesAndRemovesPerHitCollectionWork()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Club", "Armor", "Necklace", "Skill", "Companion", "Relic" })
                foreach (var item in ui.Items(category)) { item.discovered = true; item.level = category == "Relic" ? 15000 : 100; }
            var expected = ui.AttackPercentAmount(300, "Skill");
            long allocation = GC.GetAllocatedBytesForCurrentThread(); var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) ui.AttackPercentAmount(300, "Skill");
            long uncachedBytes = GC.GetAllocatedBytesForCurrentThread() - allocation, uncachedTicks = clock.ElapsedTicks;
            try {
                ui.BeginCombatSnapshot(); allocation = GC.GetAllocatedBytesForCurrentThread(); clock.Restart();
                GameNumber actual = 0;
                for (int i = 0; i < 1000; i++) actual = ui.AttackPercentAmount(300, "Skill");
                long cachedBytes = GC.GetAllocatedBytesForCurrentThread() - allocation, cachedTicks = clock.ElapsedTicks;
                Assert.That((double)(actual / expected), Is.EqualTo(1).Within(1e-10));
                Assert.That(cachedBytes, Is.LessThan(uncachedBytes / 2));
                Debug.Log("Late-game 1000 damage calculations: allocations " + uncachedBytes + " -> " + cachedBytes + " bytes; ticks " + uncachedTicks + " -> " + cachedTicks);
            } finally { ui.EndCombatSnapshot(); }
            var relic = ui.Items("Relic").First(x => x.effect == "attack"); relic.level *= 2;
            Assert.That(ui.AttackPercentAmount(300, "Skill") > expected, Is.True, "Changes after the snapshot must be live.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LateGameCombatReusesEffectsAcrossSustainedKills()
        {
            game.TogglePause(); var ui = game.Ui;
            ServiceSetSavedField(ServiceStateObject, "mainStage", 1199);
            if (ui.BreakthroughMode) ui.ToggleBreakthroughMode();
            var tuning = (DoodleUi.ServiceTuning)typeof(DoodleUi).GetField("serviceTuning", GrowthPrivate).GetValue(ui);
            tuning.enemyHealthStageGrowth = 0; tuning.enemyHealthGrowthSteps = Array.Empty<DoodleGrowthStep>();
            foreach (string category in new[] { "Club", "Armor", "Necklace", "Skill", "Companion", "Relic" }) {
                var items = ui.Items(category);
                foreach (var item in items) { item.discovered = true; item.level = category == "Relic" ? 15000 : 100; item.equipped = false; }
                int slots = category == "Skill" ? 8 : category == "Companion" ? ui.UnlockedCompanionSlots : category == "Relic" ? 0 : 1;
                int slot = 0;
                foreach (var item in items.OrderByDescending(x => x.rarity).Take(slots)) { item.equipped = true; item.slot = slot++; }
            }
            game.enemyContactDamage = 0; game.companionsEnabled = true; game.summonSkillsEnabled = true;
            int roots = game.EnemyObjectsCreated;
            game.TogglePause(); Time.timeScale = 4;
            yield return PhysicsTicks(1200);
            Assert.That(game.Kills, Is.GreaterThan(200));
            Assert.That(game.EnemyObjectsReused, Is.GreaterThan(100));
            Assert.That(game.EnemyObjectsCreated, Is.LessThanOrEqualTo(roots + 20), "Refills must reuse the 200 physical enemies after kills.");
            Assert.That(game.VisualObjectsReused, Is.GreaterThan(100));
            Assert.That(game.PooledVisualCount, Is.LessThanOrEqualTo(2048));
            Assert.That(game.ActiveDamageNumbers, Is.LessThanOrEqualTo(128));
            Assert.That(ui.PowerAmount > 0, Is.True);
            Debug.Log("Late-game live battle: kills=" + game.Kills + ", enemy created/reused=" + game.EnemyObjectsCreated + "/" + game.EnemyObjectsReused + ", visuals created/reused=" + game.VisualObjectsCreated + "/" + game.VisualObjectsReused);
            Object.Destroy(CaptureFrame("late-game-pooled-combat.png", 720, 1520));
        }

        [UnityTest]
        public IEnumerator CombatPoolsReuseObjectsWithoutRetainingOldTargetsOrVisualState()
        {
            game.TogglePause();
            var create = typeof(DoodleIdleGame).GetMethod("CreateActor", GrowthPrivate);
            var release = typeof(DoodleIdleGame).GetMethod("ReleaseEnemy", GrowthPrivate);
            var oldActor = create.Invoke(game, new object[] { false, Vector2.one, 0 }); var type = oldActor.GetType();
            var oldRoot = (GameObject)type.GetField("root").GetValue(oldActor);
            oldRoot.transform.localScale = Vector3.one * 3;
            type.GetField("isBoss").SetValue(oldActor, true); oldRoot.GetComponent<Rigidbody2D>().mass = 50;
            release.Invoke(game, new[] { oldActor }); release.Invoke(game, new[] { oldActor });
            int created = game.EnemyObjectsCreated, reused = game.EnemyObjectsReused;
            for (int i = 0; i < 200; i++) {
                var actor = create.Invoke(game, new object[] { false, new Vector2(i, 2), i % 3 });
                Assert.That(ReferenceEquals(actor, oldActor), Is.False);
                Assert.That(type.GetField("root").GetValue(actor), Is.SameAs(oldRoot));
                Assert.That(oldRoot.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(oldRoot.GetComponent<Rigidbody2D>().mass, Is.EqualTo(1));
                Assert.That((bool)type.GetField("isBoss").GetValue(actor), Is.False);
                Assert.That((GameNumber)type.GetField("hp").GetValue(oldActor), Is.EqualTo((GameNumber)0));
                release.Invoke(game, new[] { actor });
            }
            Assert.That(game.EnemyObjectsCreated, Is.EqualTo(created)); Assert.That(game.EnemyObjectsReused - reused, Is.EqualTo(200));
            var rent = typeof(DoodleIdleGame).GetMethod("RentVisual", GrowthPrivate);
            var giveBack = typeof(DoodleIdleGame).GetMethod("ReleaseVisual", GrowthPrivate);
            var sprite = oldRoot.GetComponentsInChildren<SpriteRenderer>(true).First().sprite;
            var art = (SpriteRenderer)rent.Invoke(game, new object[] { "Pool test shot", sprite, Vector2.zero, Vector2.one, 50 });
            var identity = art.gameObject; created = game.VisualObjectsCreated;
            for (int i = 0; i < 200; i++) {
                art.name = "Changed shot name"; art.color = Color.red; art.flipX = true; art.transform.rotation = Quaternion.Euler(0, 0, 90);
                giveBack.Invoke(game, new object[] { art.gameObject });
                Assert.That(identity.activeSelf, Is.False);
                art = (SpriteRenderer)rent.Invoke(game, new object[] { "Pool test shot", sprite, Vector2.one, Vector2.one * 2, 60 });
                Assert.That(art.gameObject, Is.SameAs(identity)); Assert.That(art.name, Is.EqualTo("Pool test shot"));
                Assert.That(art.color, Is.EqualTo(Color.white)); Assert.That(art.flipX, Is.False);
                Assert.That(art.transform.rotation, Is.EqualTo(Quaternion.identity));
            }
            Assert.That(game.VisualObjectsCreated, Is.EqualTo(created)); Assert.That(game.PooledVisualCount, Is.LessThanOrEqualTo(2048));
            giveBack.Invoke(game, new object[] { art.gameObject });
            yield return null;
        }
    }
}
