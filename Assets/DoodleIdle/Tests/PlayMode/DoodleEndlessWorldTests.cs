using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator EndlessMapSupportsTravelBeyondOldWallsAndReusesFloor()
        {
            var bodies = DurableSkillTargets(); game.paused = true; game.autoPlay = true;
            Assert.That(game.endlessWorld, Is.True);
            Assert.That(game.GetComponentsInChildren<Collider2D>().Any(c => c.name == "Invisible arena boundary"), Is.False);
            var player = PlayerBody(); var camera = Camera.main;
            var floor = game.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Generated dirt floor");
            var update = typeof(DoodleIdleGame).GetMethod("UpdateEndlessGround", GrowthPrivate);
            var sweep = typeof(DoodleIdleGame).GetMethod("LimitAutomaticStep", GrowthPrivate);
            foreach (var position in new[] { new Vector2(1000, 2000), new Vector2(-2000, -1000) }) {
                Place(player, position);
                Place(bodies[0], position + Vector2.up * 6);
                bodies[0].simulated = true;
                bodies[0].linearVelocity = position.normalized * 4;
                typeof(DoodleIdleGame).GetMethod("LimitEnemyCrowdMotion", GrowthPrivate).Invoke(game, new object[] { .02f });
                Assert.That(bodies[0].linearVelocity.magnitude, Is.EqualTo(4).Within(.001), "Enemies must also move beyond the old bounds.");
                camera.transform.position = new Vector3(position.x, position.y, -10);
                foreach (float aspect in new[] { .5f, 2f }) {
                    camera.aspect = aspect; camera.orthographicSize = 12;
                    update.Invoke(game, null);
                    Assert.That(floor.bounds.min.x, Is.LessThan(position.x - 12 * aspect));
                    Assert.That(floor.bounds.max.x, Is.GreaterThan(position.x + 12 * aspect));
                    Assert.That(floor.bounds.min.y, Is.LessThan(position.y - 12));
                    Assert.That(floor.bounds.max.y, Is.GreaterThan(position.y + 12));
                    Assert.That((Vector2)sweep.Invoke(game, new object[] { Vector2.right * 3 }), Is.EqualTo(Vector2.right * 3));
                }
                typeof(DoodleIdleGame).GetMethod("Update", GrowthPrivate).Invoke(game, null);
                var order = typeof(DoodleIdleGame).GetMethod("Order", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                int front = (int)order.Invoke(null, new object[] { position - Vector2.up });
                int back = (int)order.Invoke(null, new object[] { position + Vector2.up });
                Assert.That(front, Is.GreaterThan(back));
                Assert.That(back, Is.GreaterThan(floor.sortingOrder));
            }
            Place(bodies[0], player.position + Vector2.right * 6);
            foreach (var visual in game.GetComponentsInChildren<DoodleRigVisual>()) visual.Sync();
            Object.Destroy(CaptureFrame("endless-map-distant-world.png", 900, 1200, true, () => update.Invoke(game, null)));
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name == "Generated dirt floor"), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnVolumesIgnoreHitScaleAndResumeIncompleteWaveWhenExpanded()
        {
            game.paused = true;
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var release = typeof(DoodleIdleGame).GetMethod("ReleaseEnemy", GrowthPrivate);
            foreach (var actor in actors) release.Invoke(game, new[] { actor });
            actors.Clear();
            var visual = PlayerBody().GetComponentInChildren<DoodleRigVisual>();
            // Visual squash must not change authored spawn radii.
            var before = game.PlayerSpawnBoundary.transform.lossyScale;
            visual.BeginHitFeedback(); visual.AdvanceHitFeedback(.02f);
            Assert.That(game.PlayerSpawnBoundary.transform.lossyScale, Is.EqualTo(before));
            Assert.That(game.PlayerSpawnExclusion.transform.lossyScale, Is.EqualTo(Vector3.one));
            visual.ResetHitFeedback();
            game.PlayerSpawnBoundary.radius = 6;
            var refill = typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate);
            refill.Invoke(game, null);
            Assert.That(game.EnemyCount, Is.InRange(1, 49));
            foreach (var actor in actors) {
                var body = (Rigidbody2D)ActorField(actor, "body");
                Assert.That(Vector2.Distance(body.position, PlayerBody().position), Is.InRange(5.06f, 5.441f));
            }
            game.PlayerSpawnBoundary.radius = 16;
            typeof(DoodleIdleGame).GetField("spawnBlockedUntil", GrowthPrivate).SetValue(game, 0f);
            refill.Invoke(game, null);
            Assert.That(game.EnemyCount, Is.EqualTo(50), "Partially filled waves must resume without waiting to drop below ten enemies.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DistantTravelRecyclesSpatialCellsWithoutLosingTargets()
        {
            var bodies = DurableSkillTargets(); game.paused = true;
            var spawn = typeof(DoodleIdleGame).GetMethod("SnapshotSpawnPositions", GrowthPrivate);
            var targets = typeof(DoodleIdleGame).GetMethod("SnapshotSummonTargets", GrowthPrivate);
            var occupied = typeof(DoodleIdleGame).GetMethod("SpawnPositionOccupied", GrowthPrivate);
            var spawnCells = (IDictionary)typeof(DoodleIdleGame).GetField("spawnCells", GrowthPrivate).GetValue(game);
            var enemyCells = (IDictionary)typeof(DoodleIdleGame).GetField("enemyCells", GrowthPrivate).GetValue(game);
            var points = (IList)typeof(DoodleIdleGame).GetField("enemyPoints", GrowthPrivate).GetValue(game);
            for (int step = 0; step < 150; step++) {
                for (int i = 0; i < bodies.Length; i++) Place(bodies[i], new Vector2(step * 100 + i * 3, step * -100));
                spawn.Invoke(game, null); targets.Invoke(game, null);
                Assert.That(spawnCells.Count, Is.LessThanOrEqualTo(1024 + bodies.Length));
                Assert.That(enemyCells.Count, Is.LessThanOrEqualTo(1024 + bodies.Length));
                Assert.That(points.Count, Is.EqualTo(bodies.Length));
                Assert.That((bool)occupied.Invoke(game, new object[] { bodies[0].position }), Is.True);
            }
            yield return null;
        }
    }
}
