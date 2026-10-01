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
        public IEnumerator GoldPrefabSizeAndSeparateLandingHeightsAreRespected()
        {
            DurableSkillTargets(); game.paused = true;
            var gold = Particles("Gold Coin Particle System");
            var emit = typeof(DoodleIdleGame).GetMethod("EmitGold", GrowthPrivate);
            var tick = typeof(DoodleIdleGame).GetMethod("TickParticles", GrowthPrivate);
            var main = gold.main; main.startSize = 1.25f;
            emit.Invoke(game, new object[] { new Vector2(-2, -1) });
            emit.Invoke(game, new object[] { new Vector2(2, 2) });
            var buffer = new ParticleSystem.Particle[64];
            int count = gold.GetParticles(buffer); Assert.That(count, Is.EqualTo(18));
            var floors = new Dictionary<uint, float>();
            for (int i = 0; i < count; i++) {
                Assert.That(buffer[i].startSize, Is.EqualTo(1.25f).Within(.001), "Native prefab Start Size must not be overwritten.");
                Assert.That(buffer[i].velocity.y, Is.GreaterThan(0));
                floors[buffer[i].randomSeed] = buffer[i].position.y;
            }
            var landed = new HashSet<uint>(); bool rose = false;
            for (int frame = 0; frame < 70; frame++) {
                tick.Invoke(game, new object[] { .02f });
                count = gold.GetParticles(buffer);
                for (int i = 0; i < count; i++) {
                    float floor = floors[buffer[i].randomSeed];
                    Assert.That(buffer[i].position.y, Is.GreaterThanOrEqualTo(floor - .001f));
                    rose |= buffer[i].position.y > floor + .25f;
                    if (buffer[i].velocity.sqrMagnitude < .001f) {
                        landed.Add(buffer[i].randomSeed);
                        Assert.That(buffer[i].position.y, Is.EqualTo(floor).Within(.001));
                    }
                }
            }
            Assert.That(rose, Is.True); Assert.That(landed.Count, Is.EqualTo(18));
            Assert.That(gold.particleCount, Is.Zero, "Landed coins fade and return to the shared pool.");
            Assert.That(game.GetComponentsInChildren<DoodleGoldCoinBurst>().Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerAndEnemyHitsEmitSlashButInvulnerabilityDoesNot()
        {
            var bodies = DurableSkillTargets(); game.paused = false;
            DayOneState("mainStage", 2); // Exercise a damaging hit independently of tutorial zero-damage feedback.
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var enemy = actors.Cast<object>().Single(a => (Rigidbody2D)a.GetType().GetField("body").GetValue(a) == bodies[0]);
            Place(PlayerBody(), Vector2.zero); Place(bodies[0], Vector2.right * .6f);
            bodies[0].simulated = PlayerBody().simulated = true;
            var slash = Particles("Hit Slash Particle System");
            var damage = typeof(DoodleIdleGame).GetMethod("DamageAmount", GrowthPrivate);
            damage.Invoke(game, new object[] { enemy, (GameNumber)1, Vector2.zero, "basicAttack", null });
            Assert.That(slash.particleCount, Is.EqualTo(1));
            var hit = typeof(DoodleIdleGame).GetMethod("ResolveEnemyAttack", GrowthPrivate);
            hit.Invoke(game, new[] { enemy });
            Assert.That(game.PlayerContactHits, Is.EqualTo(1));
            Assert.That(slash.particleCount, Is.EqualTo(2));
            hit.Invoke(game, new[] { enemy });
            Assert.That(slash.particleCount, Is.EqualTo(2), "Blocked hits must not create a new slash.");
            game.paused = true;
            var particles = new ParticleSystem.Particle[2]; slash.GetParticles(particles);
            Assert.That(particles.All(p => p.startLifetime > 0 && p.startLifetime < .25f), Is.True);
            var emitGold = typeof(DoodleIdleGame).GetMethod("EmitGold", GrowthPrivate);
            emitGold.Invoke(game, new object[] { new Vector2(-2, -1) });
            emitGold.Invoke(game, new object[] { new Vector2(2, -1) });
            typeof(DoodleIdleGame).GetMethod("TickParticles", GrowthPrivate).Invoke(game, new object[] { .04f });
            Object.Destroy(CaptureFrame("gold-and-hit-slash.png", 900, 1200));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(slash.particleCount, Is.EqualTo(2), "Paused effects do not advance.");
            typeof(DoodleIdleGame).GetMethod("TickParticles", GrowthPrivate).Invoke(game, new object[] { .3f });
            Assert.That(slash.particleCount, Is.Zero);
            game.ResetGame();
            Assert.That(Particles("Gold Coin Particle System").particleCount, Is.Zero);
        }
    }
}
