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
        public IEnumerator WhiteHitTweenGrowsReturnsAndResetsOnRepeatAndPooling()
        {
            DurableSkillTargets(); game.paused = false;
            var enemy = ((IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game))[0];
            var player = typeof(DoodleIdleGame).GetField("player", GrowthPrivate).GetValue(game);
            Place((Rigidbody2D)ActorField(enemy, "body"), new Vector2(-2, 0));
            var hit = typeof(DoodleIdleGame).GetMethod("ApplyHitStop", GrowthPrivate);
            foreach (var actor in new[] { player, enemy }) {
                var visual = (DoodleRigVisual)ActorField(actor, "rigVisual");
                var art = (SpriteRenderer)ActorField(actor, "art"); art.flipX = true; visual.Sync();
                var rig = visual.Rig; var originalScale = rig.transform.localScale;
                rig.hitBlood.GetComponent<ParticleSystemRenderer>().enabled = true;
                var originalMaterials = rig.partRenderers.Select(r => r.sharedMaterial).ToArray();
                var body = (Rigidbody2D)ActorField(actor, "body"); var bodyScale = body.transform.localScale;
                hit.Invoke(game, new[] { actor }); StepHitStop(game, .016f);
                Assert.That(rig.transform.localScale.x, Is.EqualTo(originalScale.x * 1.18f).Within(.001));
                Assert.That(rig.transform.localScale.y, Is.EqualTo(originalScale.y * 1.18f).Within(.001));
                Assert.That(body.transform.localScale, Is.EqualTo(bodyScale));
                Assert.That(rig.partRenderers.All(r => r.sharedMaterial.shader.name == "DoodleIdle/White Hit"), Is.True);
                Assert.That(rig.partRenderers[0].sharedMaterial.GetFloat("_Flash"), Is.EqualTo(1));
                Object.Destroy(CaptureFrame(actor == player ? "player-hit-blood-scaled.png" : "enemy-hit-blood-scaled.png", 900, 1200));
                float size = rig.transform.localScale.y;
                game.paused = true; yield return null; yield return null;
                Assert.That(rig.transform.localScale.y, Is.EqualTo(size)); game.paused = false;
                hit.Invoke(game, new[] { actor }); StepHitStop(game, .016f);
                Assert.That(rig.transform.localScale.y, Is.EqualTo(originalScale.y * 1.18f).Within(.001), "Repeated hits must not compound scale.");
                StepHitStop(game, .084f);
                Assert.That(Vector3.Distance(rig.transform.localScale, originalScale), Is.LessThan(.001));
                Assert.That(rig.partRenderers.Select(r => r.sharedMaterial), Is.EqualTo(originalMaterials));
                hit.Invoke(game, new[] { actor }); StepHitStop(game, .016f);
                visual.ParkRig(); visual.RestoreRig();
                Assert.That(Vector3.Distance(rig.transform.localScale, originalScale), Is.LessThan(.001));
                Assert.That(rig.partRenderers.Select(r => r.sharedMaterial), Is.EqualTo(originalMaterials));
                StepHitStop(game, .1f);
            }
            game.paused = true;
        }

        [UnityTest]
        public IEnumerator BossHitSlashScalesWithActualBossSize()
        {
            DurableSkillTargets(); game.paused = false;
            var enemy = ((IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game))[0];
            var damage = typeof(DoodleIdleGame).GetMethod("Damage", GrowthPrivate);
            var slash = Particles("Hit Slash Particle System");
            var main = slash.main; main.startSize3D = false; main.startSize = 2;
            var root = (GameObject)ActorField(enemy, "root");
            var drops = new ParticleSystem.Particle[16];
            foreach (float scale in new[] { 1f, 3f, 4.5f }) {
                root.transform.localScale = Vector3.one * scale;
                slash.Clear(); damage.Invoke(game, new object[] { enemy, 1f, Vector2.zero });
                Assert.That(slash.GetParticles(drops), Is.EqualTo(1));
                Assert.That(drops[0].startSize, Is.EqualTo(2 * scale).Within(.001));
            }
            game.paused = true; yield return null;
        }

        [UnityTest]
        public IEnumerator SharedHitSettingsControlIntensityTimingsAndClampToHitStop()
        {
            DurableSkillTargets(); game.paused = false;
            var settings = DoodleHitFeedbackSettings.Shared;
            string original = JsonUtility.ToJson(settings);
            var actor = typeof(DoodleIdleGame).GetField("player", GrowthPrivate).GetValue(game);
            var visual = (DoodleRigVisual)ActorField(actor, "rigVisual"); visual.Sync();
            float originalScale = visual.Rig.transform.localScale.y;
            var hit = typeof(DoodleIdleGame).GetMethod("ApplyHitStop", GrowthPrivate);
            try {
                settings.hitStopDuration = .3f; settings.scaleMultiplier = 1.5f;
                settings.growDuration = .06f; settings.returnDuration = .12f;
                settings.whiteIntensity = .6f; settings.whiteHoldDuration = .03f; settings.whiteFadeDuration = .06f;
                hit.Invoke(game, new[] { actor });
                Assert.That((float)ActorField(actor, "hitStop"), Is.EqualTo(.3f));
                Assert.That(visual.Rig.partRenderers[0].sharedMaterial.GetFloat("_Flash"), Is.EqualTo(.6f).Within(.001));
                StepHitStop(game, .06f);
                Assert.That(visual.Rig.transform.localScale.y, Is.EqualTo(originalScale * 1.5f).Within(.001));
                Assert.That(visual.Rig.partRenderers[0].sharedMaterial.GetFloat("_Flash"), Is.EqualTo(.3f).Within(.001));
                settings.hitStopDuration = .9f; // In-progress feedback keeps its captured clock.
                StepHitStop(game, .12f);
                Assert.That(visual.Rig.transform.localScale.y, Is.EqualTo(originalScale).Within(.001));
                Assert.That(visual.HitStopped, Is.True);
                StepHitStop(game, .12f); Assert.That(visual.HitStopped, Is.False);
                settings.hitStopDuration = .1f; settings.growDuration = settings.returnDuration = 3;
                hit.Invoke(game, new[] { actor }); StepHitStop(game, .05f);
                Assert.That(visual.Rig.transform.localScale.y, Is.EqualTo(originalScale * 1.5f).Within(.001));
                StepHitStop(game, .05f);
                Assert.That(visual.HitStopped, Is.False);
                Assert.That(visual.Rig.transform.localScale.y, Is.EqualTo(originalScale).Within(.001));
            } finally { JsonUtility.FromJsonOverwrite(original, settings); game.paused = true; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BloodSpraysUpAndBehindBothFacingDirectionsAtCharacterScale()
        {
            DurableSkillTargets(); game.paused = true;
            var player = typeof(DoodleIdleGame).GetField("player", GrowthPrivate).GetValue(game);
            var enemy = ((IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game))[0];
            Place((Rigidbody2D)ActorField(enemy, "body"), new Vector2(-2, 0));
            foreach (var actor in new[] { player, enemy }) {
                var visual = (DoodleRigVisual)ActorField(actor, "rigVisual");
                var art = (SpriteRenderer)ActorField(actor, "art");
                var blood = visual.Rig.hitBlood;
                var ps = blood.GetComponent<ParticleSystem>();
                Assert.That(ps.main.scalingMode, Is.EqualTo(ParticleSystemScalingMode.Hierarchy));
                var authoredRotation = ps.shape.rotation;
                var shape = ps.shape; shape.angle = 0;
                foreach (bool left in new[] { false, true }) {
                    art.flipX = left; visual.Sync(); blood.Clear(); blood.Burst(left);
                    var drops = new ParticleSystem.Particle[ps.main.maxParticles];
                    int count = ps.GetParticles(drops); Assert.That(count, Is.GreaterThan(0));
                    foreach (var drop in drops.Take(count)) {
                        var velocity = ps.main.simulationSpace == ParticleSystemSimulationSpace.World
                            ? drop.velocity : ps.transform.TransformVector(drop.velocity);
                        Assert.That(Mathf.Atan2(velocity.y, Mathf.Abs(velocity.x)) * Mathf.Rad2Deg, Is.EqualTo(-blood.rearUpAngle).Within(.05f));
                        Assert.That(velocity.y, Is.GreaterThan(0), "Blood must initially fly upward.");
                        Assert.That(velocity.x * (left ? 1 : -1), Is.GreaterThan(0), "Blood must fly behind the character.");
                    }
                    Assert.That(ps.shape.rotation, Is.EqualTo(authoredRotation));
                    ps.Simulate(.06f, false, false, false);
                    Object.Destroy(CaptureFrame((actor == player ? "player" : "enemy") + "-blood-" + (left ? "left" : "right") + ".png", 900, 1200));
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyHitStopsForTenthSecondAndDeathReturnsToPoolAfterHold()
        {
            var bodies = DurableSkillTargets(); game.paused = false;
            var enemies = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var enemy = enemies[0]; var visual = (DoodleRigVisual)ActorField(enemy, "rigVisual");
            var body = (Rigidbody2D)ActorField(enemy, "body");
            var damage = typeof(DoodleIdleGame).GetMethod("Damage", GrowthPrivate);
            var configuredBlood = visual.Rig.hitBlood.GetComponent<ParticleSystem>();
            var bloodEmission = configuredBlood.emission;
            bloodEmission.SetBursts(new[] { new ParticleSystem.Burst(0, 7) });
            var bloodShape = configuredBlood.shape;
            bloodShape.angle = 22; bloodShape.rotation = new Vector3(0, -90, 0);
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
            StepHitStop(game, .09f); Assert.That(visual.HitStopped, Is.True);
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
            StepHitStop(game, .09f); Assert.That((bool)ActorField(enemy, "returnedToPool"), Is.False);
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
            StepHitStop(game, .09f); Assert.That(visual.HitStopped, Is.True);
            StepHitStop(game, .02f); Assert.That(visual.HitStopped, Is.False);
            typeof(DoodleIdleGame).GetField("contactInvulnerability", GrowthPrivate).SetValue(game, 0f);
            SetActorField(player, "hp", (GameNumber)1);
            hit.Invoke(game, new[] { enemy });
            Assert.That(game.PlayerHealth, Is.Zero);
            Assert.That(PlayerBody().position.x, Is.EqualTo(3).Within(.001));
            StepHitStop(game, .09f); Assert.That(game.PlayerHealth, Is.Zero);
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
                StepHitStop(left, .06f); StepHitStop(right, .06f);
                Assert.That(visual.HitStopped, Is.True, "Attacker must not tick the same actor twice.");
                Assert.That((float)ActorField(victim, "hitStop"), Is.EqualTo(.04f).Within(.001));
                StepHitStop(right, .05f); Assert.That(visual.HitStopped, Is.False);
            } finally { Object.Destroy(left.gameObject); Object.Destroy(right.gameObject); }
            yield return null;
        }
    }
}
