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
            Color dark = (Color)tint.Invoke(game, new object[] { Color.white });
            Assert.That(dark.r, Is.LessThan(.1)); Assert.That(dark.a, Is.InRange(.4f, .6f));
            var art = (SpriteRenderer)player.GetType().GetField("art").GetValue(player);
            art.color = dark;
            Object.Destroy(CaptureFrame("player-contact-invulnerability-dark.png", 1000, 1000, false));
            Step(.11f);
            Color light = (Color)tint.Invoke(game, new object[] { Color.white });
            Assert.That(light.r, Is.GreaterThan(.9)); Assert.That(light.a, Is.LessThan(1));
            art.color = light;
            Object.Destroy(CaptureFrame("player-contact-invulnerability-light.png", 1000, 1000, false));
            Step(.88f); Assert.That(game.PlayerContactHits, Is.EqualTo(1));
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
    }
}
