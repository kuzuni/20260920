using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator ContactDamageUsesSharedOneSecondImmunityBlinkAndAllEnemyKinds()
        {
            var bodies = DurableSkillTargets(); game.TogglePause();
            var player = typeof(DoodleIdleGame).GetField("player", GrowthPrivate).GetValue(game);
            var reset = typeof(DoodleIdleGame).GetMethod("ResetPlayerContactDamage", GrowthPrivate);
            var tick = typeof(DoodleIdleGame).GetMethod("TickPlayerContactDamage", GrowthPrivate);
            var tint = typeof(DoodleIdleGame).GetMethod("PlayerInvulnerabilityTint", GrowthPrivate);
            void Step(float dt) => tick.Invoke(game, new object[] { dt });
            reset.Invoke(game, null);
            float full = game.PlayerHealth;
            Place(bodies[0], new Vector2(1.16f, 0));
            Place(bodies[1], new Vector2(-1.16f, 0));
            Step(0);
            Assert.That(game.PlayerContactHits, Is.EqualTo(1));
            Assert.That(game.PlayerHealth, Is.EqualTo(full - game.enemyContactDamage).Within(.01));
            Assert.That(game.PlayerInvulnerable, Is.True);
            var damageText = game.GetComponentsInChildren<Text>().Single(t => t.name == "Player damage number" && t.gameObject.activeSelf);
            Assert.That(damageText.text, Is.EqualTo(UiNumber.Format(game.enemyContactDamage)));
            Assert.That(damageText.color.r, Is.EqualTo(damageText.color.g).Within(.001));
            Assert.That(damageText.color.g, Is.EqualTo(damageText.color.b).Within(.001));
            Assert.That(damageText.color.r, Is.InRange(.5f, .7f));
            Assert.That(damageText.transform.position.y, Is.GreaterThan(PlayerBody().position.y));
            typeof(DoodleIdleGame).GetMethod("TickDamageNumbers", GrowthPrivate).Invoke(game, new object[] { .2f });
            Assert.That(damageText.color.r, Is.EqualTo(.62f).Within(.001), "Floating damage keeps its gray color while fading.");
            Color dark = (Color)tint.Invoke(game, new object[] { Color.white });
            Assert.That(dark.r, Is.LessThan(.2)); Assert.That(dark.a, Is.InRange(.5f, .7f));
            var art = (SpriteRenderer)player.GetType().GetField("art").GetValue(player);
            art.color = dark;
            Object.Destroy(CaptureFrame("player-contact-invulnerability-dark.png", 1000, 1000, false));
            Step(.2f);
            Assert.That((Color)tint.Invoke(game, new object[] { Color.white }), Is.EqualTo(dark), "The dark phase remains stable for a quarter second.");
            Step(.06f);
            Color light = (Color)tint.Invoke(game, new object[] { Color.white });
            Assert.That(light.r, Is.GreaterThan(.9)); Assert.That(light.a, Is.LessThan(1));
            art.color = light;
            Object.Destroy(CaptureFrame("player-contact-invulnerability-light.png", 1000, 1000, false));
            Step(.73f); Assert.That(game.PlayerContactHits, Is.EqualTo(1));
            Step(.011f); Assert.That(game.PlayerContactHits, Is.EqualTo(2));
            foreach (var body in bodies) Place(body, new Vector2(25, 25));
            Step(1.01f);
            Assert.That(game.PlayerInvulnerable, Is.False);
            Assert.That((Color)tint.Invoke(game, new object[] { Color.white }), Is.EqualTo(Color.white));
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var representatives = actors.Cast<object>().GroupBy(a => (int)a.GetType().GetField("kind").GetValue(a)).Select(g => g.First());
            foreach (var enemy in representatives) {
                var body = (Rigidbody2D)enemy.GetType().GetField("body").GetValue(enemy);
                int before = game.PlayerContactHits;
                Place(body, new Vector2(1.16f, 0)); Step(1.01f);
                Assert.That(game.PlayerContactHits, Is.EqualTo(before + 1));
                Place(body, new Vector2(25, 25));
            }
            // Boss scale expands the contact radius, and fast movement is swept.
            bodies[0].transform.localScale = Vector3.one * 3;
            Place(bodies[0], new Vector2(2.28f, 0));
            int bossBefore = game.PlayerContactHits; Step(1.01f);
            Assert.That(game.PlayerContactHits, Is.EqualTo(bossBefore + 1));
            Place(bodies[0], new Vector2(25, 25)); Step(1.01f);
            bodies[0].transform.localScale = Vector3.one;
            Place(bodies[0], new Vector2(1.5f, 0)); PlayerBody().linearVelocity = Vector2.right * 25;
            int sweptBefore = game.PlayerContactHits; Step(.02f);
            Assert.That(game.PlayerContactHits, Is.EqualTo(sweptBefore + 1));
            reset.Invoke(game, null);
            Assert.That(game.PlayerHealth, Is.EqualTo(game.PlayerMaxHealth));
            Assert.That(game.PlayerInvulnerable, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyDashPreparesChargesWithFacingTrailsThenReturnsToMovement()
        {
            var bodies = DurableSkillTargets(); game.enemyDashEnabled = true;
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var representatives = actors.Cast<object>().GroupBy(a => (int)a.GetType().GetField("kind").GetValue(a)).Select(g => g.First()).ToArray();
            var moving = new System.Collections.Generic.List<Rigidbody2D>();
            for (int i = 0; i < representatives.Length; i++) {
                var actor = representatives[i];
                actor.GetType().GetField("dashCooldown").SetValue(actor, 0f);
                var body = (Rigidbody2D)actor.GetType().GetField("body").GetValue(actor);
                Place(body, i == 0 ? new Vector2(3, 0) : i == 1 ? new Vector2(-3, 0) : new Vector2(0, 3));
                body.simulated = true; moving.Add(body);
            }
            var starts = moving.Select(b => b.position).ToArray();
            yield return PhysicsTicks(12);
            Assert.That(game.EnemyDashCasts, Is.Zero);
            for (int i = 0; i < moving.Count; i++) Assert.That(Vector2.Distance(moving[i].position, starts[i]), Is.LessThan(.02));
            yield return PhysicsTicks(12);
            Assert.That(game.EnemyDashCasts, Is.EqualTo(representatives.Length));
            var trails = NamedArt("Enemy dash afterimage");
            Assert.That(trails.Length, Is.GreaterThanOrEqualTo(representatives.Length));
            Assert.That(trails.All(t => t.color.a > 0 && t.color.a <= .28f), Is.True);
            Assert.That(trails.Any(t => t.flipX) && trails.Any(t => !t.flipX), Is.True);
            Assert.That(moving.Any(b => b.linearVelocity.magnitude > 5), Is.True);
            Object.Destroy(CaptureFrame("enemy-dash-facing-trails.png", 1000, 1000, false));
            yield return PhysicsTicks(14);
            Assert.That(game.PlayerContactHits, Is.GreaterThan(0));
            Assert.That(moving.All(b => b.linearVelocity.magnitude < 2), Is.True);
            int casts = game.EnemyDashCasts;
            yield return PhysicsTicks(55);
            Assert.That(game.EnemyDashCasts, Is.EqualTo(casts), "The attack respects its cooldown.");
            Assert.That(NamedArt("Enemy dash afterimage"), Is.Empty);
        }
    }
}
