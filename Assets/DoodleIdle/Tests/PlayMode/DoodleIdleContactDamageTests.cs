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
        public IEnumerator AutoMovementKeepsConfigurableBodyClearanceAndStopsDashes()
        {
            var bodies = DurableSkillTargets();
            game.autoPlay = true; game.moveSpeed = 3.1f;
            var tuning = game.Ui.ReadBalanceTuning(); tuning.playerKeepDistance = .6f;
            game.Ui.ApplyBalanceTuning(tuning);
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            object target = actors.Cast<object>().Single(a => (Rigidbody2D)a.GetType().GetField("body").GetValue(a) == bodies[0]);
            var move = typeof(DoodleIdleGame).GetMethod("AutomaticMoveVelocity", GrowthPrivate);
            var sweep = typeof(DoodleIdleGame).GetMethod("LimitAutomaticStep", GrowthPrivate);
            Vector2 Velocity() => (Vector2)move.Invoke(game, new object[] { target, .02f });
            Vector2 Sweep(Vector2 step) => (Vector2)sweep.Invoke(game, new object[] { step });
            float radii = PlayerBody().GetComponent<CircleCollider2D>().radius + bodies[0].GetComponent<CircleCollider2D>().radius;
            Place(PlayerBody(), Vector2.zero); Place(bodies[0], new Vector2(4, 0));
            Assert.That(Velocity().x, Is.GreaterThan(3));
            Place(bodies[0], new Vector2(radii + .2f, 0));
            Assert.That(Velocity().x, Is.LessThan(-1), "Retreat before bodies touch.");
            Place(bodies[0], new Vector2(radii + .6f, 0));
            Assert.That(Velocity().magnitude, Is.LessThan(.001));
            Place(bodies[0], new Vector2(radii + .9f, 0));
            tuning.playerKeepDistance = 1.2f; game.Ui.ApplyBalanceTuning(tuning);
            Assert.That(Velocity().x, Is.LessThan(-1), "Applying a larger distance changes the current movement immediately.");
            var copy = new DoodleUi.ServiceTuning(); DoodleUi.CopyBalanceTuning(tuning, copy);
            Assert.That(JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(copy)).playerKeepDistance, Is.EqualTo(1.2f));
            tuning.playerKeepDistance = .6f; game.Ui.ApplyBalanceTuning(tuning);
            Place(bodies[0], new Vector2(4, 0));
            Assert.That(Sweep(Vector2.right * 8).x, Is.EqualTo(4 - radii - .61f).Within(.001), "The entire dash stops outside the collision body.");
            bodies[0].transform.localScale = Vector3.one * 3;
            float bossRadii = PlayerBody().GetComponent<CircleCollider2D>().radius + bodies[0].GetComponent<CircleCollider2D>().radius * 3;
            Assert.That(Sweep(Vector2.right * 8).x, Is.EqualTo(4 - bossRadii - .61f).Within(.001));
            bodies[0].transform.localScale = Vector3.one;
            game.autoPlay = false;
            Assert.That(Sweep(Vector2.right * 8), Is.EqualTo(Vector2.right * 8), "Manual movement bypasses avoidance.");
            game.autoPlay = true;
            tuning.playerKeepDistance = 0; game.Ui.ApplyBalanceTuning(tuning);
            Assert.That(Sweep(Vector2.right * 8), Is.EqualTo(Vector2.right * 8));
            tuning.playerKeepDistance = .6f; game.Ui.ApplyBalanceTuning(tuning);
            DayOneState("mainStage", 1);
            Place(bodies[0], new Vector2(radii + .6f, 0)); bodies[0].simulated = true;
            int hits = game.PlayerContactHits;
            for (int i = 0; i < 150; i++) {
                yield return new WaitForFixedUpdate();
                Assert.That(Vector2.Distance(PlayerBody().position, bodies[0].position), Is.GreaterThan(radii + .15f));
            }
            Assert.That(PlayerBody().position.x, Is.LessThan(-.5f), "The player backs away as the enemy follows.");
            Assert.That(game.PlayerContactHits, Is.EqualTo(hits));
        }

        [UnityTest]
        public IEnumerator ContactDamageUsesSharedOneSecondImmunityBlinkAndAllEnemyKinds()
        {
            var bodies = DurableSkillTargets(); game.TogglePause();
            DayOneState("mainStage", 1); // First-stage enemies are intentionally harmless.
            ((DoodleUi.ServiceTuning)typeof(DoodleUi).GetField("serviceTuning", ServicePrivate).GetValue(game.Ui)).earlyEnemyDamageMax = 64 * 69;
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
            Color original = new Color(.3f, .6f, .9f, 1);
            Color faded = (Color)tint.Invoke(game, new object[] { original });
            Assert.That(faded, Is.EqualTo(new Color(original.r, original.g, original.b, .6f)));
            var art = (SpriteRenderer)player.GetType().GetField("art").GetValue(player);
            var appearance = typeof(DoodleIdleGame).GetMethod("ApplyPlayerHitAppearance", GrowthPrivate);
            art.color = original; appearance.Invoke(game, null);
            Assert.That(art.color, Is.EqualTo(faded));
            Color materialTint=art.sharedMaterial.GetColor("_TintColor");
            Assert.That(materialTint.r,Is.EqualTo(original.r).Within(.00001));
            Assert.That(materialTint.g,Is.EqualTo(original.g).Within(.00001));
            Assert.That(materialTint.b,Is.EqualTo(original.b).Within(.00001));
            Assert.That(art.sharedMaterial.shader.name, Is.EqualTo("DoodleIdle/Player Hit Fade"));
            Assert.That(art.sharedMaterial.GetFloat("_Opacity"), Is.EqualTo(.6f).Within(.001f));
            art.color = Color.white; appearance.Invoke(game, null);
            Object.Destroy(CaptureFrame("player-contact-invulnerability-faded.png", 1000, 1000, false));
            Step(.2f);
            Assert.That((Color)tint.Invoke(game, new object[] { original }), Is.EqualTo(faded), "The fade phase remains stable for a quarter second.");
            Step(.06f);
            Color light = (Color)tint.Invoke(game, new object[] { original });
            Assert.That(light, Is.EqualTo(new Color(original.r, original.g, original.b, .8f)));
            art.color = Color.white; appearance.Invoke(game, null);
            Assert.That(art.sharedMaterial.shader.name, Is.Not.EqualTo("DoodleIdle/Player Hit Fade"));
            Object.Destroy(CaptureFrame("player-contact-invulnerability-light.png", 1000, 1000, false));
            Step(.73f); Assert.That(game.PlayerContactHits, Is.EqualTo(1));
            Step(.011f); Assert.That(game.PlayerContactHits, Is.EqualTo(2));
            foreach (var body in bodies) Place(body, new Vector2(25, 25));
            Step(1.01f);
            Assert.That(game.PlayerInvulnerable, Is.False);
            Assert.That((Color)tint.Invoke(game, new object[] { Color.white }), Is.EqualTo(Color.white));
            art.color = Color.white; appearance.Invoke(game, null);
            Assert.That(art.color, Is.EqualTo(Color.white));
            Assert.That(art.sharedMaterial.shader.name, Is.Not.EqualTo("DoodleIdle/Player Hit Fade"));
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
        public IEnumerator EarlyEnemiesRampFromZeroTo100WithoutFakeHitsAtStageOne()
        {
            var bodies = DurableSkillTargets(); game.TogglePause(); var ui = game.Ui;
            var reset = typeof(DoodleIdleGame).GetMethod("ResetPlayerContactDamage", GrowthPrivate);
            var tick = typeof(DoodleIdleGame).GetMethod("TickPlayerContactDamage", GrowthPrivate);
            DayOneState("mainStage", 0); reset.Invoke(game, null);
            Place(bodies[0], Vector2.zero);
            float full = game.PlayerHealth;
            tick.Invoke(game, new object[] { 0f });
            bodies[0].transform.localScale = Vector3.one * 3;
            tick.Invoke(game, new object[] { 1.1f });
            Assert.That(game.PlayerHealth, Is.EqualTo(full));
            Assert.That(game.PlayerContactHits, Is.Zero);
            Assert.That(game.PlayerInvulnerable, Is.False);
            Assert.That(game.GetComponentsInChildren<Text>().Any(x => x.name == "Player damage number" && x.gameObject.activeSelf), Is.False);
            Assert.That(ui.EnemyDamageMultiplier(0), Is.Zero);
            Assert.That(ui.EnemyDamageMultiplier(1), Is.Zero);
            float previous = 0;
            for (int stage = 2; stage <= 70; stage++) {
                float damage = 64 * ui.EnemyDamageMultiplier(stage);
                Assert.That(damage, Is.GreaterThan(previous).And.LessThanOrEqualTo(100));
                previous = damage;
            }
            Assert.That(previous, Is.EqualTo(100).Within(.001));
            Assert.That(64 * ui.EnemyDamageMultiplier(71), Is.InRange(100, 120));
            for (int stage = 71; stage <= 151; stage++) {
                float damage = 64 * ui.EnemyDamageMultiplier(stage);
                Assert.That(damage, Is.GreaterThanOrEqualTo(previous).And.LessThan(previous * 1.3f));
                previous = damage;
            }
            DayOneState("mainStage", 69); reset.Invoke(game, null);
            tick.Invoke(game, new object[] { 0f });
            Assert.That(game.PlayerHealth, Is.EqualTo(full - 100).Within(.01));
            Assert.That(game.PlayerContactHits, Is.EqualTo(1));
            DayOneState("mainStage", 0); DayOneState("activeDungeon", 0);
            Assert.That(ui.CombatDifficultyStage, Is.EqualTo(50));
            Assert.That(ui.EnemyDamageMultiplier(ui.CombatDifficultyStage), Is.GreaterThan(0), "Dungeon difficulty is independent of the field's harmless first stage.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyDashPreparesChargesWithFacingTrailsThenReturnsToMovement()
        {
            game.basicSkillsEnabled = game.extraSkillsEnabled = game.summonSkillsEnabled = game.companionsEnabled = false;
            game.autoPlay = false; game.enemyContactDamage = 0; game.enemyDashEnabled = true;
            var movement = typeof(DoodleIdleGame).GetMethod("TickEnemyMovement", GrowthPrivate);
            foreach (int stage in new[] { 1,99,100,1000,1200 }) {
                game.Ui.DebugSetMainStage(stage);
                var field = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
                foreach (var actor in field) actor.GetType().GetField("dashCooldown").SetValue(actor, 0f);
                yield return PhysicsTicks(40);
                Assert.That(game.EnemyDashCasts, Is.Zero, "Ordinary enemies never dash at any stage.");
            }
            AscensionSpawnBoss(99);
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            actors[0].GetType().GetField("dashCooldown").SetValue(actors[0],0f);
            yield return PhysicsTicks(40);
            Assert.That(game.EnemyDashCasts, Is.Zero, "Boss 99 cannot dash.");
            AscensionSpawnBoss(100);
            var boss = actors[0]; var type = boss.GetType();
            var body = (Rigidbody2D)type.GetField("body").GetValue(boss);
            type.GetField("hp").SetValue(boss, (GameNumber)(1e15f)); type.GetField("maxHp").SetValue(boss, (GameNumber)(1e15f));
            Place(PlayerBody(), Vector2.zero); Place(body,new Vector2(5,0));
            type.GetField("dashCooldown").SetValue(boss,0f);
            yield return PhysicsTicks(12);
            Assert.That(game.EnemyDashCasts,Is.Zero);
            Assert.That(Vector2.Distance(body.position,new Vector2(5,0)),Is.LessThan(.03));
            yield return PhysicsTicks(12);
            Assert.That(game.EnemyDashCasts,Is.EqualTo(1));
            Assert.That(body.linearVelocity.magnitude,Is.GreaterThan(5));
            Assert.That(NamedArt("Enemy dash afterimage").Any(t=>t.flipX),Is.True);
            Object.Destroy(CaptureFrame("boss-dash-facing-trails.png",1000,1000,false));
            yield return PhysicsTicks(44);
            Assert.That(body.linearVelocity.magnitude,Is.LessThan(2));
            yield return PhysicsTicks(55);
            Assert.That(game.EnemyDashCasts,Is.EqualTo(1));
            Assert.That(NamedArt("Enemy dash afterimage"),Is.Empty);
            Place(body,new Vector2(-5,0)); type.GetField("dashCooldown").SetValue(boss,0f);
            yield return PhysicsTicks(24);
            Assert.That(game.EnemyDashCasts,Is.EqualTo(2));
            Assert.That(NamedArt("Enemy dash afterimage").Any(t=>!t.flipX),Is.True);
            ServiceSetSavedField(ServiceStateObject,"mainStage",98);
            yield return PhysicsTicks(1);
            Assert.That(body.linearVelocity.magnitude,Is.LessThan(2),"Dropping below stage 100 cancels a boss dash.");
            ServiceSetSavedField(ServiceStateObject,"mainStage",1199);
            ServiceSetSavedField(ServiceStateObject,"activeDungeon",0);
            type.GetField("dashCooldown").SetValue(boss,0f);
            movement.Invoke(game,new object[]{.02f});
            Assert.That((float)type.GetField("dashWindup").GetValue(boss),Is.Zero,"Dungeons do not inherit boss dashes.");
        }
    }
}
