using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator CollectionTotalsScopesStayLiveAndPreserveTheCombatSnapshot()
        {
            game.TogglePause(); var ui = game.Ui;
            var prepared = typeof(DoodleUi).GetField("snapshotPrepared", GrowthPrivate);
            var relic = ui.Items("Relic").First(x => x.effect == "attack");
            bool discovered = relic.discovered; int level = relic.level;
            try {
                relic.discovered = true; relic.level = 1;
                var before = ui.PowerAmount;
                Assert.That(prepared.GetValue(ui), Is.False);
                relic.level = 1000;
                Assert.That(ui.PowerAmount > before, Is.True, "The next query must see inventory edits immediately.");
                Assert.That(prepared.GetValue(ui), Is.False);
                ui.BeginCombatSnapshot(); var attack = ui.AttackAmount;
                var power = ui.PowerAmount;
                Assert.That(prepared.GetValue(ui), Is.True, "A nested UI query must leave the physics snapshot intact.");
                relic.level = 2000;
                Assert.That(ui.AttackAmount, Is.EqualTo(attack));
                ui.EndCombatSnapshot();
                Assert.That(ui.AttackAmount > attack, Is.True);
                Assert.That(prepared.GetValue(ui), Is.False);
            } finally { ui.EndCombatSnapshot(); relic.discovered = discovered; relic.level = level; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetiredHitHistoryKeepsLiveCooldownsAndNewEnemyIdentities()
        {
            game.TogglePause();
            var enemies = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var type = enemies[0].GetType();
            var history = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(type, typeof(float)));
            var create = typeof(DoodleIdleGame).GetMethod("CreateActor", GrowthPrivate);
            var release = typeof(DoodleIdleGame).GetMethod("ReleaseEnemy", GrowthPrivate);
            var prune = typeof(DoodleIdleGame).GetMethod("PruneRetiredHits", GrowthPrivate);
            int live = Math.Min(64, enemies.Count);
            for (int i = 0; i < live; i++) history.Add(enemies[i], 500f + i);
            int watermark = 128;
            for (int wave = 0; wave < 3; wave++) {
                for (int i = 0; i < 128; i++) {
                    var retired = create.Invoke(game, new object[] { false, new Vector2(20, 20), i % 3 });
                    history.Add(retired, float.MaxValue);
                    release.Invoke(game, new[] { retired });
                }
                object[] arguments = { history, watermark }; prune.Invoke(game, arguments); watermark = (int)arguments[1];
                Assert.That(history.Count, Is.EqualTo(live), "Old waves must not accumulate in a long-lived skill.");
                for (int i = 0; i < live; i++) Assert.That(history[enemies[i]], Is.EqualTo(500f + i), "Live hit deadlines must not change.");
                var fresh = create.Invoke(game, new object[] { false, Vector2.one, 0 });
                Assert.That(history.Contains(fresh), Is.False, "Reused enemy geometry must have no inherited hit immunity.");
                release.Invoke(game, new[] { fresh });
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedStatQueriesObserveLiveTuningWithoutSearchAllocations()
        {
            game.TogglePause(); var ui = game.Ui;
            var tuning = (UiCollectionTuning)typeof(DoodleUi).GetField("collectionTuning", GrowthPrivate).GetValue(ui);
            var definition = tuning.stats.First(s => s.id == "attack");
            var expected = ui.StatAmount("attack"); GameNumber actual = 0;
            for (int i = 0; i < 10; i++) actual = ui.StatAmount("attack");
            using var recorder = new Unity.Profiling.ProfilerRecorder(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 0);
            long before = recorder.CurrentValue;
            for (int i = 0; i < 1000; i++) actual = ui.StatAmount("attack");
            long allocated = recorder.CurrentValue - before;
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(allocated, Is.LessThan(1024));
            float original = definition.initial;
            try { definition.initial += 10; Assert.That(ui.StatAmount("attack") > expected, Is.True); }
            finally { definition.initial = original; }
            Assert.That(ui.StatAmount("missing-stat"), Is.EqualTo((GameNumber)0));
            yield return null;
        }
    }
}
