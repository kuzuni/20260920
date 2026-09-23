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
            var bodies = DurableSkillTargets(); game.enemyDashEnabled = true;
            // Stage 1000 enables the dash; this movement fixture retains baseline
            // contact damage so defeat does not replace its observed actors.
            var liveTuning = (DoodleUi.ServiceTuning)typeof(DoodleUi).GetField("serviceTuning", ServicePrivate).GetValue(game.Ui);
            liveTuning.enemyDamageStageGrowth = 0;
            liveTuning.earlyEnemyDamageMax = 64;
            Assert.That(game.Ui.EnemyDamageMultiplier(1000), Is.EqualTo(1));
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
            foreach (int displayedStage in new[] { 1, 999 }) {
                ServiceSetSavedField(ServiceStateObject, "mainStage", displayedStage - 1);
                yield return PhysicsTicks(24);
                Assert.That(game.EnemyDashCasts, Is.Zero, "No dash before displayed stage 1000.");
                Assert.That(NamedArt("Enemy dash afterimage"), Is.Empty);
                Assert.That(moving.All(b => b.linearVelocity.magnitude < 2), Is.True);
            }
            ServiceSetSavedField(ServiceStateObject, "mainStage", 999); // Displayed stage 1000.
            for (int i = 0; i < moving.Count; i++) Place(moving[i], starts[i]);
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
            ServiceSetSavedField(ServiceStateObject, "mainStage", 1000); // Still enabled at stage 1001.
            for (int i = 0; i < moving.Count; i++) {
                Place(moving[i], starts[i]);
                representatives[i].GetType().GetField("dashCooldown").SetValue(representatives[i], 0f);
            }
            yield return PhysicsTicks(24);
            Assert.That(game.EnemyDashCasts, Is.EqualTo(casts + representatives.Length));
            ServiceSetSavedField(ServiceStateObject, "mainStage", 998);
            yield return PhysicsTicks(1);
            Assert.That(moving.All(b => b.linearVelocity.magnitude < 2), Is.True, "Returning below the threshold cancels an active dash.");
            ServiceSetSavedField(ServiceStateObject,"activeDungeon",0);
            ServiceSetSavedField(ServiceStateObject,"dungeonStages",new[]{19,0,0});
            ServiceSetSavedField(ServiceStateObject,"mainStage",0);
            int beforeCave=game.EnemyDashCasts;
            for(int i=0;i<moving.Count;i++) {
                Place(moving[i],starts[i]);
                representatives[i].GetType().GetField("dashCooldown").SetValue(representatives[i],0f);
            }
            yield return PhysicsTicks(24);
            Assert.That(game.EnemyDashCasts,Is.EqualTo(beforeCave+representatives.Length),"Cave stage 20 has main stage 1000 dash behavior.");
            ServiceSetSavedField(ServiceStateObject,"dungeonStages",new[]{0,0,0});
            ServiceSetSavedField(ServiceStateObject,"mainStage",1199);
            yield return PhysicsTicks(1);
            Assert.That(moving.All(b=>b.linearVelocity.magnitude<2),Is.True,"Cave stage 1 stays at stage 50 difficulty even for a veteran profile.");
        }
    }
}
