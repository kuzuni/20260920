using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator ExistingSkillsKeepMovingWhileNextWaveArrives()
        {
            DurableSkillTargets(); game.paused = true;
            game.CastVariant("Shuriken");
            var shots = (IList)typeof(DoodleIdleGame).GetField("variantShots", GrowthPrivate).GetValue(game);
            var shot = shots[0]; var art = (SpriteRenderer)shot.GetType().GetField("art").GetValue(shot);
            var start = art.transform.position;
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var begin = typeof(DoodleIdleGame).GetMethod("BeginEnemyArrival", GrowthPrivate);
            foreach (var actor in actors) begin.Invoke(game, new[] { actor });
            game.TogglePause();
            foreach (var actor in actors) Assert.That(((Rigidbody2D)ActorField(actor, "body")).simulated, Is.False);
            var closest = typeof(DoodleIdleGame).GetMethod("Closest", GrowthPrivate);
            Assert.That(closest.Invoke(game, new object[] { PlayerBody().position }), Is.Null);
            typeof(DoodleIdleGame).GetMethod("FixedUpdate", GrowthPrivate).Invoke(game, null);
            Assert.That((art.transform.position - start).sqrMagnitude, Is.GreaterThan(0), "Existing projectiles must not freeze during portals.");
            var activate = typeof(DoodleIdleGame).GetMethod("ActivateSkill", GrowthPrivate);
            Assert.That((bool)activate.Invoke(game, new object[] { "MightyDragon" }), Is.False);
            game.paused = true; yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyArrivalUsesFlattenedRotatingPortalAndPooledGrowth()
        {
            DurableSkillTargets(); game.paused = true;
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var actor = actors[0]; var body = (Rigidbody2D)ActorField(actor, "body"); Place(body, new Vector2(3, 0));
            var visual = (DoodleRigVisual)ActorField(actor, "rigVisual"); visual.Sync();
            float fullScale = visual.Rig.transform.localScale.y;
            var begin = typeof(DoodleIdleGame).GetMethod("BeginEnemyArrival", GrowthPrivate);
            var tick = typeof(DoodleIdleGame).GetMethod("TickEnemyArrivals", GrowthPrivate);
            begin.Invoke(game, new[] { actor });
            var portals = (IList)typeof(DoodleIdleGame).GetField("activeSpawnPortals", GrowthPrivate).GetValue(game);
            var portal = (DoodleSpawnPortal)portals[portals.Count - 1];
            Assert.That(body.simulated, Is.False);
            Assert.That(visual.Rig.transform.localScale.y, Is.LessThan(fullScale * .01f));
            tick.Invoke(game, new object[] { .1f });
            Assert.That(portal.transform.localScale.y / portal.transform.localScale.x, Is.EqualTo(.35f).Within(.001));
            Assert.That(portal.rotatingCircle.localEulerAngles.z, Is.Not.EqualTo(0));
            Assert.That(portal.art.bounds.size.x, Is.GreaterThan(portal.art.bounds.size.y * 2));
            Object.Destroy(CaptureFrame("summon-circle-before-enemy.png", 900, 1200));
            tick.Invoke(game, new object[] { .2f });
            Assert.That(visual.Rig.transform.localScale.y, Is.InRange(fullScale * .1f, fullScale * .99f));
            Assert.That(body.simulated, Is.False);
            Object.Destroy(CaptureFrame("summon-circle-growing-enemy.png", 900, 1200));
            tick.Invoke(game, new object[] { .11f });
            Assert.That(body.simulated, Is.True);
            Assert.That(visual.Rig.transform.localScale.y, Is.EqualTo(fullScale).Within(.001));
            tick.Invoke(game, new object[] { .2f });
            Assert.That(portal.gameObject.activeSelf, Is.False);
            begin.Invoke(game, new[] { actor });
            Assert.That(portals[portals.Count - 1], Is.SameAs(portal));
            int totalPortals = game.GetComponentsInChildren<DoodleSpawnPortal>(true).Length;
            for (int i = 0; i < 150; i++) begin.Invoke(game, new[] { actor });
            Assert.That(portals.Count, Is.EqualTo(100));
            Assert.That(game.GetComponentsInChildren<DoodleSpawnPortal>(true).Length, Is.EqualTo(totalPortals));
            tick.Invoke(game, new object[] { 1f });
            yield return null;
        }

        [UnityTest]
        public IEnumerator SurroundSpawnsCoverCircleAndStaySafeNearWalls()
        {
            game.paused = true;
            var method = typeof(DoodleIdleGame).GetMethod("TryFindSurroundSpawn", GrowthPrivate);
            var cells = (IDictionary)typeof(DoodleIdleGame).GetField("spawnCells", GrowthPrivate).GetValue(game);
            var add = typeof(DoodleIdleGame).GetMethod("AddSpawnPosition", GrowthPrivate);
            foreach (int population in new[] { 50, 100 })
            foreach (var center in new[] { Vector2.zero, new Vector2(3, -2), new Vector2(15, 18) }) {
                Place(PlayerBody(), center); cells.Clear();
                var positions = new System.Collections.Generic.List<Vector2>(); var sectors = new bool[12];
                for (int i = 0; i < population; i++) {
                    var args = new object[] { i, population, 0f, .56f, Vector2.zero };
                    Assert.That((bool)method.Invoke(game, args), Is.True, "A wave must still fit near arena corners.");
                    var position = (Vector2)args[4]; var delta = position - center;
                    Assert.That(delta.magnitude, Is.GreaterThan(5.06f));
                    Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(game.arenaHalfSize.x - 1));
                    Assert.That(Mathf.Abs(position.y), Is.LessThanOrEqualTo(game.arenaHalfSize.y - 1));
                    foreach (var previous in positions) Assert.That((position - previous).sqrMagnitude, Is.GreaterThanOrEqualTo(1.6f));
                    positions.Add(position); add.Invoke(game, new object[] { position });
                    int sector = Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(delta.y, delta.x), 2 * Mathf.PI) / (2 * Mathf.PI) * 12);
                    sectors[sector] = true;
                }
                if (center == Vector2.zero) foreach (bool occupied in sectors) Assert.That(occupied, Is.True);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnExclusionFollowsPlayerAndProtectsCrowdedFallbacks()
        {
            game.TogglePause();
            game.RestartCombatForStageDebug(); // Inspect fresh spawns before any physics tick moves them.
            var zone = game.PlayerSpawnExclusion;
            Assert.That(zone, Is.Not.Null);
            Assert.That(zone.transform.parent, Is.EqualTo(PlayerBody().transform));
            Assert.That(zone.radius, Is.EqualTo(4.5f));
            Assert.That(zone.enabled, Is.False, "Spawn volume must not create combat contacts.");
            foreach(var enemy in EnemyBodies())
                Assert.That(Vector2.Distance(enemy.position, PlayerBody().position), Is.GreaterThan(5.05f));
            Place(PlayerBody(), new Vector2(3, -2));
            zone.offset = new Vector2(1, .5f); zone.transform.localScale = new Vector3(1.2f, 1.2f, 1);
            Vector2 center = PlayerBody().position + zone.offset * 1.2f;
            float radius = zone.radius * 1.2f + .56f;
            var add = typeof(DoodleIdleGame).GetMethod("AddSpawnPosition", GrowthPrivate);
            for(float x=-9;x<=9;x+=.5f)for(float y=-10;y<=10;y+=.5f)
                add.Invoke(game, new object[]{new Vector2(x,y)});
            var find = typeof(DoodleIdleGame).GetMethod("TryFindEnemySpawn", GrowthPrivate);
            for(int i=0;i<10;i++) {
                var args = new object[]{new Vector2(8,9.5f),.56f,Vector2.zero};
                Assert.That((bool)find.Invoke(game,args), Is.True);
                Assert.That(Vector2.Distance((Vector2)args[2],center), Is.GreaterThan(radius), "Crowding cannot bypass player exclusion.");
            }
            zone.radius = 100;
            var blocked = new object[]{new Vector2(8,9.5f),.56f,Vector2.zero};
            Assert.That((bool)find.Invoke(game,blocked), Is.False, "A fully blocked arena must defer spawning.");
            zone.radius = 4.5f;
            Assert.That((bool)find.Invoke(game,blocked), Is.True, "Valid space must become usable again.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossAndRefillRespectEditableSpawnExclusion()
        {
            game.TogglePause();
            var ui = game.Ui;
            if(!ui.BreakthroughMode)ui.ToggleBreakthroughMode();
            game.PlayerSpawnExclusion.radius = 7;
            DefeatActualServiceEnemies(ui.MainStageRemaining);
            typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate).Invoke(game,null);
            Assert.That(game.BossActive, Is.True);
            var boss = game.transform.Find("Doodle world/Stage boss").GetComponent<Rigidbody2D>();
            Assert.That(Vector2.Distance(boss.position,PlayerBody().position),Is.GreaterThan(7 + .56f * 3));
            typeof(DoodleIdleGame).GetMethod("TickEnemyArrivals", GrowthPrivate).Invoke(game, new object[] { 1f });
            DefeatActualServiceEnemies(1);
            typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate).Invoke(game,null);
            Assert.That(game.EnemyCount, Is.EqualTo(50));
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            foreach(var actor in actors) {
                var enemy = (Rigidbody2D)actor.GetType().GetField("body").GetValue(actor);
                Assert.That(Vector2.Distance(enemy.position,PlayerBody().position),Is.GreaterThan(7.55f));
            }
            var face = PlayerBody().GetComponentInChildren<DoodleIdle.CharacterRigs.CharacterFace>();
            face.hurtDuration = 10; face.ShowHit();
            Object.Destroy(CaptureFrame("spawn-exclusion-thin-hurt-eyes.png", 720, 720, false, () => {
                Camera.main.orthographicSize = 2;
                Camera.main.transform.position = new Vector3(PlayerBody().position.x, PlayerBody().position.y, -10);
            }));
            yield return null;
        }
    }
}
