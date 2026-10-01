using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        object ActorField(object actor, string name) => actor.GetType().GetField(name).GetValue(actor);
        void SetActorField(object actor, string name, object value) => actor.GetType().GetField(name).SetValue(actor, value);
        void StepHitStop(DoodleIdleGame owner, float dt) => typeof(DoodleIdleGame).GetMethod("TickHitReactions", GrowthPrivate).Invoke(owner, new object[] { dt });

        [UnityTest]
        public IEnumerator EnemyHitStopsForHalfSecondAndDeathReturnsToPoolAfterHold()
        {
            var bodies = DurableSkillTargets(); game.paused = false;
            var enemies = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var enemy = enemies[0]; var visual = (DoodleRigVisual)ActorField(enemy, "rigVisual");
            var body = (Rigidbody2D)ActorField(enemy, "body");
            var damage = typeof(DoodleIdleGame).GetMethod("Damage", GrowthPrivate);
            Place(body, new Vector2(-2, 0)); ((SpriteRenderer)ActorField(enemy, "art")).flipX = false; visual.Sync();
            body.linearVelocity = Vector2.right * 3;
            damage.Invoke(game, new object[] { enemy, 1f, Vector2.right });
            Assert.That(visual.HitStopped, Is.True);
            Assert.That(visual.Rig.animator.speed, Is.Zero);
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(visual.TryAttack(null), Is.False);
            var blood = visual.Rig.hitBlood.GetComponent<ParticleSystem>();
            Assert.That(blood.particleCount, Is.EqualTo(7));
            var drops = new ParticleSystem.Particle[64]; int dropCount = blood.GetParticles(drops);
            Assert.That(drops.Take(dropCount).All(p => p.velocity.x < 0), Is.True, "Spray travels toward the back of the right-facing head.");
            StepHitStop(game, .49f); Assert.That(visual.HitStopped, Is.True);
            StepHitStop(game, .02f); Assert.That(visual.HitStopped, Is.False);
            Assert.That(visual.Rig.animator.speed, Is.EqualTo(1));
            Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
            SetActorField(enemy, "hp", (GameNumber)1);
            int kills = game.Kills;
            damage.Invoke(game, new object[] { enemy, 1000f, Vector2.zero });
            Assert.That(game.Kills, Is.EqualTo(kills + 1));
            Assert.That(enemies.Contains(enemy), Is.False);
            Assert.That((bool)ActorField(enemy, "returnedToPool"), Is.False);
            Assert.That(((Collider2D)ActorField(enemy, "collider")).enabled, Is.False);
            Assert.That(visual.Rig.partRenderers.Any(r => r.enabled), Is.True);
            blood.Simulate(.1f, false, false, false);
            Object.Destroy(CaptureFrame("enemy-hit-stop-blood.png", 900, 1200));
            StepHitStop(game, .49f); Assert.That((bool)ActorField(enemy, "returnedToPool"), Is.False);
            StepHitStop(game, .02f); Assert.That((bool)ActorField(enemy, "returnedToPool"), Is.True);
            Assert.That(visual.Rig.partRenderers.All(r => !r.enabled), Is.True);
            Assert.That(visual.Rig.hitBlood.GetComponent<ParticleSystem>().particleCount, Is.Zero);
            game.paused = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerHitStopsAndLethalHitWaitsBeforeRespawn()
        {
            var bodies = DurableSkillTargets(); game.paused = false;
            DayOneState("mainStage", 2);
            var enemies = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var enemy = enemies[0]; var player = typeof(DoodleIdleGame).GetField("player", GrowthPrivate).GetValue(game);
            var visual = (DoodleRigVisual)ActorField(player, "rigVisual"); visual.Sync();
            Place(PlayerBody(), Vector2.right * 3); Place(bodies[0], Vector2.right * 3.6f);
            bodies[0].simulated = PlayerBody().simulated = true;
            var hit = typeof(DoodleIdleGame).GetMethod("ResolveEnemyAttack", GrowthPrivate);
            hit.Invoke(game, new[] { enemy });
            Assert.That(visual.HitStopped, Is.True);
            Assert.That(visual.TryAttack(null), Is.False);
            Assert.That(visual.Rig.hitBlood.GetComponent<ParticleSystem>().particleCount, Is.GreaterThan(0));
            StepHitStop(game, .49f); Assert.That(visual.HitStopped, Is.True);
            StepHitStop(game, .02f); Assert.That(visual.HitStopped, Is.False);
            typeof(DoodleIdleGame).GetField("contactInvulnerability", GrowthPrivate).SetValue(game, 0f);
            SetActorField(player, "hp", (GameNumber)1);
            hit.Invoke(game, new[] { enemy });
            Assert.That(game.PlayerHealth, Is.Zero);
            Assert.That(PlayerBody().position.x, Is.EqualTo(3).Within(.001));
            StepHitStop(game, .49f); Assert.That(game.PlayerHealth, Is.Zero);
            StepHitStop(game, .02f);
            Assert.That(game.PlayerHealth, Is.EqualTo(game.PlayerMaxHealth));
            Assert.That(PlayerBody().position, Is.EqualTo(Vector2.zero));
            Assert.That(visual.HitStopped, Is.False);
            Assert.That(((Collider2D)ActorField(player, "collider")).enabled, Is.True);
            game.paused = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PvpHitStopAdvancesOnlyOnVictimsClock()
        {
            game.paused = true; var snapshot = game.Ui.CapturePvpLoadout();
            var left = PvpEngine(game, snapshot, Vector2.left * 2);
            var right = PvpEngine(game, snapshot, Vector2.right * 2);
            try {
                PvpConnect(left, right); PvpConnect(right, left);
                left.paused = right.paused = false;
                var victim = typeof(DoodleIdleGame).GetField("player", GrowthPrivate).GetValue(right);
                var visual = (DoodleRigVisual)ActorField(victim, "rigVisual"); visual.Sync();
                SetActorField(victim, "hp", (GameNumber)1e20);
                typeof(DoodleIdleGame).GetMethod("Damage", GrowthPrivate).Invoke(left, new object[] { victim, 1f, Vector2.right });
                Assert.That(visual.HitStopped, Is.True);
                StepHitStop(left, .3f); StepHitStop(right, .3f);
                Assert.That(visual.HitStopped, Is.True, "Attacker must not tick the same actor twice.");
                Assert.That((float)ActorField(victim, "hitStop"), Is.EqualTo(.2f).Within(.001));
                StepHitStop(right, .21f); Assert.That(visual.HitStopped, Is.False);
            } finally { Object.Destroy(left.gameObject); Object.Destroy(right.gameObject); }
            yield return null;
        }
    }
}
