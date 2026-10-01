using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
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
