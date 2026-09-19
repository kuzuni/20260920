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
        Rigidbody2D PlayerBody() => game.GetComponentsInChildren<Rigidbody2D>().Single(b => b.name.StartsWith("Player -"));
        Rigidbody2D[] IsolateSummonTest()
        {
            game.basicSkillsEnabled = game.extraSkillsEnabled = game.summonSkillsEnabled = game.autoPlay = false;
            game.moveSpeed = 0;
            PlayerBody().position = Vector2.zero;
            PlayerBody().linearVelocity = Vector2.zero;
            var bodies = EnemyBodies();
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].simulated = false;
                bodies[i].position = new Vector2(-16 + i % 8 * 1.2f, -10 + i / 8 * 1.2f);
            }
            return bodies;
        }
        IEnumerator PhysicsTicks(int count) { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }
        SpriteRenderer[] NamedArt(string name) => game.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == name).ToArray();

        [UnityTest]
        public IEnumerator ShotgunFiresTwentyPelletsAndCucumberPiercesWithoutTurning()
        {
            var bodies = IsolateSummonTest(); bodies[0].position = new Vector2(3, 0);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Shotgun);
            Assert.That(game.ShotgunPellets, Is.EqualTo(20));
            Assert.That(NamedArt("Shotgun moving skill").Length, Is.EqualTo(20));
            Assert.That(NamedArt("Shotgun moving skill").Select(r => r.transform.eulerAngles.z).Distinct().Count(), Is.EqualTo(20));
            yield return PhysicsTicks(45);
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.Shotgun), Is.GreaterThan(0));
            game.ResetGame(); yield return null;
            bodies = IsolateSummonTest();
            for (int i = 0; i < 3; i++) bodies[i].position = new Vector2(3 + i * 2, 0);
            var victims = new List<int>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.Cucumber) victims.Add(id); };
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cucumber);
            var cucumber = NamedArt("Cucumber moving skill").Single();
            yield return PhysicsTicks(70);
            Assert.That(cucumber.transform.position.y, Is.EqualTo(0).Within(.02f));
            Assert.That(cucumber.transform.position.x, Is.GreaterThan(7));
            Assert.That(victims.Distinct().Count(), Is.GreaterThanOrEqualTo(3));
            Assert.That(victims.Count, Is.EqualTo(victims.Distinct().Count()), "A passing cucumber hits each enemy once.");
        }

        [UnityTest]
        public IEnumerator CannonStaysAtDeploymentForTenSecondsAndExplodesInAnArea()
        {
            Time.timeScale = 4;
            var bodies = IsolateSummonTest();
            bodies[0].position = new Vector2(3, 0); bodies[1].position = new Vector2(3, 1.3f); bodies[2].position = new Vector2(4.3f, 0);
            var victims = new HashSet<int>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.Cannon) victims.Add(id); };
            Vector2 origin = PlayerBody().position;
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cannon);
            var cannon = NamedArt("Stationary ten second cannon").Single();
            PlayerBody().position = new Vector2(4, -3);
            yield return PhysicsTicks(60);
            Assert.That((Vector2)cannon.transform.position, Is.EqualTo(origin));
            Assert.That(game.CannonExplosions, Is.GreaterThan(0));
            Assert.That(victims.Count, Is.GreaterThanOrEqualTo(3), "Explosion must damage neighbours as well as its target.");
            yield return PhysicsTicks(60);
            Assert.That(game.ActiveStains, Is.GreaterThan(0));
            foreach (var stain in NamedArt("Fading black death stain"))
            {
                Assert.That(stain.color.r, Is.Zero); Assert.That(stain.color.a, Is.InRange(0f, .28f));
                Assert.That(stain.sortingOrder, Is.LessThan(-900));
            }
            yield return PhysicsTicks(360);
            Assert.That(game.ActiveCannons, Is.EqualTo(1), "Cannon should still exist before ten seconds.");
            yield return PhysicsTicks(25);
            Assert.That(game.ActiveCannons, Is.Zero);
            Assert.That(game.LastCannonLifetime, Is.InRange(10, 10.05f));
            int shots = game.CannonShots;
            yield return PhysicsTicks(80);
            Assert.That(game.CannonShots, Is.EqualTo(shots));
            yield return PhysicsTicks(350);
            Assert.That(game.ActiveStains, Is.Zero, "Death marks must fade away, not accumulate forever.");
        }

        [UnityTest]
        public IEnumerator FiveWaveSnakesEmergeAndAttachedSnakeRetargetsWhileRootFollowsPlayer()
        {
            var bodies = IsolateSummonTest(); bodies[0].position = new Vector2(3, 0); bodies[1].position = new Vector2(4.5f, 0);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.WaveSnakes);
            Assert.That(NamedArt("WaveSnakes head").Length, Is.EqualTo(5));
            yield return PhysicsTicks(5);
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("WaveSnakes segment") && r.enabled), Is.LessThan(50));
            yield return PhysicsTicks(35);
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("WaveSnakes segment") && r.enabled), Is.EqualTo(50));
            Assert.That(NamedArt("WaveSnakes head").Select(r => r.transform.position).Distinct().Count(), Is.EqualTo(5));
            game.ResetGame(); yield return null;
            bodies = IsolateSummonTest(); bodies[0].position = new Vector2(2.5f, 0); bodies[1].position = new Vector2(4.1f, .5f);
            var impacts = new Dictionary<int, int>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.TetherSnake) impacts[id] = impacts.TryGetValue(id, out int count) ? count + 1 : 1; };
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.TetherSnake);
            yield return PhysicsTicks(65);
            PlayerBody().position = new Vector2(.3f, -.5f);
            yield return PhysicsTicks(2);
            var root = NamedArt("TetherSnake segment 31").Single();
            Assert.That(Vector2.Distance(root.transform.position, PlayerBody().position), Is.LessThan(.03f));
            yield return PhysicsTicks(130);
            Assert.That(impacts.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(impacts.Values.Max(), Is.GreaterThanOrEqualTo(8), "Attached snake must repeatedly hit its living target.");
            Assert.That(game.TetherRetargets, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator GuardianOnlyAttacksInRangeAndRequiredTrailsAreVisible()
        {
            var bodies = IsolateSummonTest(); bodies[0].position = new Vector2(8, 0);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.GuardianSword);
            Assert.That(game.SummonCasts(DoodleIdleGame.SummonSkill.GuardianSword), Is.Zero);
            bodies[0].position = new Vector2(4, 0);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.GuardianSword);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.FireRing);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Sand);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.StormCloud);
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.BouncyBall);
            yield return PhysicsTicks(8);
            foreach (string name in new[] { "FireRing afterimage", "Lightning afterimage", "Bouncy ball afterimage" })
                Assert.That(NamedArt(name).Length, Is.GreaterThan(0), name);
            Assert.That(game.GetComponentsInChildren<ParticleSystem>().Single(p => p.name == "Sand Spray Particle System").particleCount, Is.GreaterThan(0));
            yield return PhysicsTicks(15);
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.GuardianSword), Is.GreaterThan(0));
            var shadow = NamedArt("Soft ground shadow");
            Assert.That(shadow.Length, Is.GreaterThan(20));
            Assert.That(shadow.All(s => s.color.a >= .3f && s.sortingOrder == -900), Is.True);
            game.TogglePause();
            var cloud = NamedArt("Drifting storm cloud").Single(); Vector3 cloudPosition = cloud.transform.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(cloud.transform.position, Is.EqualTo(cloudPosition));
            game.ResetGame(); yield return null;
            Assert.That(game.ActiveSummonObjects, Is.Zero);
            Assert.That(game.ActiveStains, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DragonFlapsAndRedWaveFiresFiveAnimatedWideWavesAtNormalSpeed()
        {
            var bodies = IsolateSummonTest(); bodies[0].position = new Vector2(4, 0);
            bodies[1].position = new Vector2(4, 1.8f); bodies[2].position = new Vector2(2, 3.5f);
            var launchTimes = new List<float>();
            game.RedWaveLaunched += time => launchTimes.Add(time);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Dragon);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.RedWave);
            var wave = NamedArt("RedWave moving skill").Single();
            Assert.That(game.RedWavesLaunched, Is.EqualTo(1), "The volley must be sequential, not five simultaneous waves.");
            Assert.That(Vector2.Dot(-wave.transform.right, Vector2.right), Is.GreaterThan(.99f), "The convex edge must lead, with the open crescent facing back.");
            Assert.That(wave.transform.localScale.x, Is.GreaterThan(4));
            var wings = NamedArt("Animated dragon wings").Single();
            var wingFrames = new HashSet<string>(); var slashFrames = new HashSet<string>();
            Vector3 start = wave.transform.position;
            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
                wingFrames.Add(wings.sprite.name); slashFrames.Add(wave.sprite.name);
                Assert.That(wings.sharedMaterial.mainTexture, Is.SameAs(wings.sprite.texture));
                Assert.That(wave.sharedMaterial.mainTexture, Is.SameAs(wave.sprite.texture));
            }
            Assert.That(wingFrames.Count, Is.EqualTo(2)); Assert.That(slashFrames.Count, Is.EqualTo(2));
            Assert.That(Vector3.Distance(start, wave.transform.position), Is.InRange(DoodleIdleGame.SlashSpeed * .98f, DoodleIdleGame.SlashSpeed * 1.04f));
            Assert.That(launchTimes.Count, Is.EqualTo(5));
            for (int i = 1; i < launchTimes.Count; i++) Assert.That(launchTimes[i] - launchTimes[i - 1], Is.InRange(.139f, .181f));
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.RedWave), Is.GreaterThanOrEqualTo(2));
            Assert.That(game.DragonFlames, Is.GreaterThanOrEqualTo(6));
            Assert.That(NamedArt("RedWave afterimage").Length, Is.GreaterThan(0));
            Assert.That(NamedArt("Dragon afterimage").Length, Is.GreaterThan(0));
            yield return PhysicsTicks(200);
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.Dragon), Is.GreaterThan(0));
            Assert.That(game.RedWavesLaunched, Is.EqualTo(5));
            Assert.That(NamedArt("RedWave moving skill").Length, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SummonSkillsExportSeparateReadableCombatFrames()
        {
            IsolateSummonTest();
            var bodies = EnemyBodies();
            for (int i = 0; i < 12; i++) bodies[i].position = new Vector2(Mathf.Cos(i * Mathf.PI / 6), Mathf.Sin(i * Mathf.PI / 6)) * 4;
            Camera.main.orthographicSize = 6.5f;
            foreach (var skill in new[] { DoodleIdleGame.SummonSkill.Cannon, DoodleIdleGame.SummonSkill.Cucumber, DoodleIdleGame.SummonSkill.TetherSnake, DoodleIdleGame.SummonSkill.StormCloud, DoodleIdleGame.SummonSkill.Dragon, DoodleIdleGame.SummonSkill.RedWave }) game.CastSummonSkill(skill);
            yield return PhysicsTicks(50);
            game.TogglePause(); yield return null;
            Object.Destroy(CaptureFrame("08-summons-landscape.png", 1440, 900));
            Object.Destroy(CaptureFrame("09-summons-portrait.png", 720, 1560));
            game.TogglePause();
            yield return PhysicsTicks(7);
            game.TogglePause(); yield return null;
            Object.Destroy(CaptureFrame("10-dragon-animation-next-frame.png", 1440, 900));
            game.ResetGame(); yield return null; IsolateSummonTest();
            bodies = EnemyBodies();
            for (int i = 0; i < 12; i++) bodies[i].position = new Vector2(Mathf.Cos(i * Mathf.PI / 6), Mathf.Sin(i * Mathf.PI / 6)) * 4;
            foreach (var skill in new[] { DoodleIdleGame.SummonSkill.WaveSnakes, DoodleIdleGame.SummonSkill.FireRing, DoodleIdleGame.SummonSkill.Sand, DoodleIdleGame.SummonSkill.Shotgun }) game.CastSummonSkill(skill);
            yield return PhysicsTicks(28);
            game.TogglePause(); yield return null;
            Object.Destroy(CaptureFrame("11-area-skills.png", 1440, 900));
        }
    }
}
