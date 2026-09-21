using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        Rigidbody2D[] DurableSkillTargets()
        {
            var bodies = IsolateSummonTest();
            foreach (var item in game.Ui.Items("Skill")) item.equipped = false;
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            foreach (var actor in actors) {
                actor.GetType().GetField("hp").SetValue(actor, 100000f);
                actor.GetType().GetField("maxHp").SetValue(actor, 100000f);
            }
            for (int i = 0; i < bodies.Length; i++) Place(bodies[i], new Vector2(-25 - i % 10 * 2, -25 - i / 10 * 2));
            return bodies;
        }
        bool CastCatalogSkill(string ability) => game.DebugCastSkill(game.Ui.Items("Skill").Single(x => x.ability == ability).id);

        [UnityTest]
        public IEnumerator BasicAttackToggleStopsSlashAndDashButKeepsCompanionsAndEquippedSkills()
        {
            var bodies = DurableSkillTargets(); Place(bodies[0], new Vector2(3, 0));
            game.basicSkillsEnabled = true;
            // Bootstrap may already have consumed the initial attack cooldown.
            typeof(DoodleIdleGame).GetField("attackTimer", GrowthPrivate).SetValue(game, 0f);
            yield return PhysicsTicks(1);
            Assert.That(NamedArt("Club slash wave").Length, Is.GreaterThan(0));
#if UNITY_EDITOR
            var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DoodleIdle.Editor.DoodleSkillTestWindow")).First(t => t != null);
            var window = ScriptableObject.CreateInstance(type);
            type.GetMethod("ToggleBasicAttack").Invoke(window, null);
            Object.DestroyImmediate(window);
#else
            game.SetBasicAttackEnabled(false);
#endif
            Assert.That(game.BasicAttackEnabled, Is.False);
            yield return null;
            Assert.That(NamedArt("Club slash wave"), Is.Empty);
            var lightning = game.Ui.Items("Skill").Single(x => x.ability == "Lightning");
            lightning.equipped = lightning.discovered = true; lightning.level = 1; lightning.slot = 0;
            foreach (var item in game.Ui.Items("Companion")) item.equipped = false;
            var companion = game.Ui.Items("Companion").First();
            companion.equipped = companion.discovered = true; companion.level = 1; companion.slot = 0;
            game.summonSkillsEnabled = game.companionsEnabled = true;
            int slashes = game.SlashHits, dashes = game.DashCasts;
            yield return PhysicsTicks(300);
            Assert.That(game.DashCasts, Is.EqualTo(dashes));
            Assert.That(game.SlashHits, Is.EqualTo(slashes));
            Assert.That(game.CompanionShotsLaunched, Is.GreaterThan(0));
            Assert.That(game.CompanionHits, Is.GreaterThan(0));
            Assert.That(game.SkillActivationCount("Lightning"), Is.GreaterThan(0));
            game.SetBasicAttackEnabled(true);
            yield return PhysicsTicks(1);
            Assert.That(game.DashCasts, Is.GreaterThan(dashes));
            game.SetBasicAttackEnabled(false);
            Assert.That((float)typeof(DoodleIdleGame).GetField("dashRemaining", GrowthPrivate).GetValue(game), Is.Zero);
        }

        [UnityTest]
        public IEnumerator LightningHitsThreeAndDoubleClawHitsFiveTwice()
        {
            var bodies = DurableSkillTargets();
            for (int i = 0; i < 6; i++) Place(bodies[i], new Vector2(2 + i * 1.4f, 0));
            Assert.That(CastCatalogSkill("Lightning"), Is.True);
            Assert.That(game.LightningStrikes, Is.EqualTo(3));
            Assert.That(game.Ui.Items("Skill").First().ability, Is.EqualTo("Lightning"));
            Assert.That(game.Ui.Items("Skill").First().rarity, Is.Zero);
            var flashes = NamedArt("Direct lightning strike");
            Assert.That(flashes.Length, Is.EqualTo(3));
            Assert.That(flashes.All(x => x.sprite.name == "SkillLightning_0"), Is.True);
            Object.Destroy(CaptureFrame("lightning-strike-pose-0.png", 1000, 1000, false));
            yield return PhysicsTicks(8);
            Assert.That(flashes.All(x => x.sprite.name == "SkillLightning_1"), Is.True);
            Object.Destroy(CaptureFrame("lightning-strike-pose-1.png", 1000, 1000, false));
            yield return PhysicsTicks(9); yield return null;
            Assert.That(NamedArt("Direct lightning strike"), Is.Empty);
            Assert.That(CastCatalogSkill("DoubleClaw"), Is.True);
            Assert.That(game.ClawHits, Is.EqualTo(5));
            Assert.That(NamedArt("Double claw strike 0").Length, Is.EqualTo(5));
            Object.Destroy(CaptureFrame("expansion-claw-first.png", 1000, 1000, false));
            yield return PhysicsTicks(13);
            Assert.That(game.ClawHits, Is.EqualTo(10));
            Assert.That(NamedArt("Double claw strike 1").Length, Is.EqualTo(5));
            Object.Destroy(CaptureFrame("expansion-claw-second.png", 1000, 1000, false));
        }

        [UnityTest]
        public IEnumerator TornadoChasesAndRepeatsHitsAndGolemsWalkThenPunch()
        {
            var bodies = DurableSkillTargets(); Place(bodies[0], new Vector2(6, 0));
            Assert.That(CastCatalogSkill("Tornado"), Is.True);
            var tornado = NamedArt("Homing tornado").Single(); var first = tornado.sprite;
            yield return PhysicsTicks(7);
            Assert.That(tornado.transform.position.x, Is.GreaterThan(1));
            Assert.That(tornado.sprite, Is.Not.SameAs(first));
            Place(bodies[0], new Vector2(6, 3));
            yield return PhysicsTicks(60);
            Assert.That(tornado.transform.position.y, Is.GreaterThan(2));
            Assert.That(game.TornadoHits, Is.GreaterThanOrEqualTo(3));
            var tornadoShadow = NamedArt("Tornado ground shadow").Single();
            Assert.That((Vector2)tornadoShadow.transform.position, Is.EqualTo((Vector2)tornado.transform.position + Vector2.down * 1.1f));
            Assert.That(tornadoShadow.sortingOrder, Is.LessThan(tornado.sortingOrder));
            Object.Destroy(CaptureFrame("expansion-tornado.png", 1000, 1000, false));
            game.ResetGame(); yield return null; bodies = DurableSkillTargets();
            Assert.That(NamedArt("Tornado ground shadow"), Is.Empty);
            for (int i = 0; i < 5; i++) Place(bodies[i], new Vector2(5 + i, 3));
            Assert.That(CastCatalogSkill("Golem"), Is.True);
            Assert.That(game.GolemsSummoned, Is.EqualTo(5));
            var golems = NamedArt("Summoned golem"); Assert.That(golems.Length, Is.EqualTo(5));
            Vector3 start = golems[0].transform.position;
            var poses = new System.Collections.Generic.HashSet<string>();
            bool capturedSlam = false;
            for (int tick = 0; tick < 160; tick++) {
                yield return PhysicsTicks(1);
                foreach (var golem in golems) poses.Add(golem.sprite.name);
                if (!capturedSlam && game.GolemHits > 0) {
                    Assert.That(golems.Any(g => g.sprite.name == "SkillGolem_3"), Is.True);
                    Assert.That(Particles("Golem Ground Slam Dust Particle System").particleCount, Is.GreaterThan(0));
                    Object.Destroy(CaptureFrame("golem-downward-slam-dust.png", 1000, 1000, false));
                    capturedSlam = true;
                }
                if (tick == 45) Object.Destroy(CaptureFrame("expansion-golems.png", 1000, 1000, false));
            }
            Assert.That(Vector3.Distance(start, golems[0].transform.position), Is.GreaterThan(1));
            CollectionAssert.AreEquivalent(new[] { "SkillGolem_0", "SkillGolem_1", "SkillGolem_2", "SkillGolem_3" }, poses);
            Assert.That(game.GolemHits, Is.GreaterThan(5));
            Assert.That(capturedSlam, Is.True);
            game.TogglePause(); int hits = game.GolemHits; Vector3 paused = golems[0].transform.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(game.GolemHits, Is.EqualTo(hits)); Assert.That(golems[0].transform.position, Is.EqualTo(paused));
            game.TogglePause(); yield return PhysicsTicks(350); yield return null;
            Assert.That(NamedArt("Summoned golem"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator StoneAndDumbbellVolleysAreSequentialAndRedSwordIsRestored()
        {
            var bodies = DurableSkillTargets();
            for (int i = 0; i < 10; i++) Place(bodies[i], new Vector2(4 + i * .6f, 0));
            Assert.That(CastCatalogSkill("Stone"), Is.True);
            Assert.That(game.StonesLaunched, Is.EqualTo(1));
            yield return PhysicsTicks(8); Assert.That(game.StonesLaunched, Is.EqualTo(1));
            yield return PhysicsTicks(12); Assert.That(game.StonesLaunched, Is.EqualTo(3));
            int before = game.VariantProjectilesLaunched;
            Assert.That(CastCatalogSkill("Dumbbell"), Is.True);
            Assert.That(game.VariantProjectilesLaunched - before, Is.EqualTo(1));
            yield return PhysicsTicks(10);
            Assert.That(game.VariantProjectilesLaunched - before, Is.InRange(2, 3));
            Assert.That(NamedArt("SkillDumbbell variant projectile").Max(x => x.transform.position.y), Is.GreaterThan(1));
            Object.Destroy(CaptureFrame("expansion-dumbbells.png", 1000, 1000, false));
            yield return PhysicsTicks(60);
            Assert.That(game.VariantProjectilesLaunched - before, Is.EqualTo(10));
            Assert.That(CastCatalogSkill("RedWave"), Is.True);
            yield return PhysicsTicks(Mathf.CeilToInt(DoodleIdleGame.RedWaveShotGap * 4 / Time.fixedDeltaTime) + 2);
            Assert.That(game.RedWavesLaunched, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator MeteorRotatesWithDirectionalWorldFireThenExplodesAndLeavesCrater()
        {
            var bodies = DurableSkillTargets(); Place(bodies[0], new Vector2(2, 0)); Place(bodies[1], new Vector2(3, 0)); Place(bodies[2], new Vector2(8, 0));
            var launches = new System.Collections.Generic.List<float>();
            game.MeteorProjectileLaunched += launches.Add;
            Assert.That(CastCatalogSkill("Meteor"), Is.True);
            Assert.That(game.MeteorsLaunched, Is.EqualTo(1));
            var rock = NamedArt("Falling red meteor").Single(); Vector3 start = rock.transform.position;
            Assert.That(rock.sprite.texture.name, Is.EqualTo("SkillMeteorRock"));
            yield return PhysicsTicks(12);
            Assert.That(rock.transform.position.y, Is.LessThan(start.y));
            Assert.That(Quaternion.Angle(Quaternion.identity, rock.transform.rotation), Is.GreaterThan(40));
            var fire = game.GetComponentsInChildren<ParticleSystem>().Single(x => x.name == "Meteor Fire Trail Particle System");
            Assert.That(fire.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            var particles = new ParticleSystem.Particle[512]; int count = fire.GetParticles(particles);
            Assert.That(count, Is.GreaterThan(2));
            Assert.That(particles.Take(count).Any(x => x.position.y > rock.transform.position.y + .2f), Is.True);
            float rotation = particles[0].rotation;
            Assert.That(particles.Take(count).All(x => Mathf.Abs(Mathf.DeltaAngle(rotation, x.rotation)) < .1f), Is.True, "Flames align with travel and do not rotate with the rock.");
            Assert.That(NamedArt("Meteor rock afterimage").Length, Is.GreaterThan(0));
            Object.Destroy(CaptureFrame("expansion-meteor-falling.png", 1000, 1000, false));
            yield return PhysicsTicks(31); yield return null;
            Assert.That(game.MeteorsLanded, Is.EqualTo(1)); Assert.That(game.MeteorHits, Is.EqualTo(2));
            Assert.That(NamedArt("Falling red meteor"), Is.Empty);
            var crater = NamedArt("Meteor impact crater").Single();
            Assert.That(crater.sortingOrder, Is.LessThan(0));
            Assert.That(game.GetComponentsInChildren<ParticleSystem>().Single(x => x.name == "Meteor Explosion Particle System").particleCount, Is.GreaterThan(0));
            Object.Destroy(CaptureFrame("expansion-meteor-impact.png", 1000, 1000, false));
            yield return PhysicsTicks(6);

            yield return PhysicsTicks(2);
            Assert.That(game.MeteorsLaunched, Is.EqualTo(2));
            yield return PhysicsTicks(18);
            Object.Destroy(CaptureFrame("expansion-meteor-crater.png", 1000, 1000, false));
            yield return PhysicsTicks(30);

            yield return PhysicsTicks(2);
            Assert.That(game.MeteorsLaunched, Is.EqualTo(3));
            yield return PhysicsTicks(45);
            Assert.That(game.MeteorsLanded, Is.EqualTo(3));
            Assert.That(launches.Count, Is.EqualTo(3));
            Assert.That(launches[1] - launches[0], Is.EqualTo(1).Within(.021));
            Assert.That(launches[2] - launches[1], Is.EqualTo(1).Within(.021));
            Assert.That(game.MeteorsLaunched, Is.EqualTo(3));
        }
    }
}
