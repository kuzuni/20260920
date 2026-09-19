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
        public IEnumerator MolotovArcsThenLeavesStationaryParticleFireWithRepeatedHits()
        {
            var bodies = IsolateSummonTest();
            Place(bodies[0], new Vector2(3, 0)); Place(bodies[1], new Vector2(3, 1.5f));
            var hits = new List<int>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.Molotov) hits.Add(id); };
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Molotov);
            var bottle = NamedArt("Molotov airborne bottle").Single();
            yield return PhysicsTicks(20);
            Assert.That(bottle.transform.position.x, Is.InRange(1.4f, 1.6f));
            Assert.That(bottle.transform.position.y, Is.GreaterThan(2.4f));
            Assert.That(game.ActiveFireZones, Is.Zero);
            Camera.main.orthographicSize = 5;
            Object.Destroy(CaptureFrame("20-molotov-arc.png", 1440, 900, false));
            yield return PhysicsTicks(23); yield return null;
            Assert.That(NamedArt("Molotov airborne bottle").Length, Is.Zero);
            Assert.That(game.ActiveFireZones, Is.EqualTo(1));
            var fire = Particles("Molotov Ground Fire Particle System");
            Assert.That(fire.particleCount, Is.GreaterThan(20));
            Assert.That(fire.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(hits.GroupBy(id => id).Count(g => g.Count() >= 2), Is.GreaterThanOrEqualTo(2));
            game.TogglePause(); yield return null;
            var visible = CaptureFrame("21-molotov-fire-damage.png", 1440, 900, false);
            fire.GetComponent<ParticleSystemRenderer>().enabled = false;
            var hidden = CaptureFrame("21-molotov-hidden-comparison.png", 1440, 900, false);
            var a = visible.GetPixels32(); var b = hidden.GetPixels32(); int changed = 0;
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 25) changed++;
            Object.Destroy(visible); Object.Destroy(hidden);
            Assert.That(changed, Is.GreaterThan(250), "The ground fire must actually render as particles.");
            fire.GetComponent<ParticleSystemRenderer>().enabled = true;
            int pausedHits = hits.Count;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(hits.Count, Is.EqualTo(pausedHits));
            game.TogglePause(); foreach (var body in EnemyBodies()) body.simulated = false;
            Place(PlayerBody(), new Vector2(-3, -3));
            yield return PhysicsTicks(38);
            Assert.That(hits.GroupBy(id => id).Any(g => g.Count() >= 4), Is.True, "A stationary victim takes repeated burn ticks after the player moves away.");
            yield return PhysicsTicks(205);
            Assert.That(game.ActiveFireZones, Is.Zero);
            Assert.That(fire.particleCount, Is.Zero);
            game.ResetGame(); yield return null;
            Assert.That(game.ActiveFireZones, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SoundWaveExpandsAsAnEmptyAnnulusAndHitsEachEnemyOnce()
        {
            var bodies = IsolateSummonTest();
            for (int i = 0; i < bodies.Length; i++) Place(bodies[i], new Vector2(14 + i % 4, 15 + i / 4));
            Place(bodies[0], new Vector2(3, 0)); Place(bodies[1], new Vector2(-3, 0));
            Place(bodies[2], new Vector2(0, 5)); Place(bodies[3], new Vector2(10, 0));
            var hits = new List<int>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.SoundWave) hits.Add(id); };
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.SoundWave);
            var wave = NamedArt("Expanding sound wave").Single();
            var texture = wave.sprite.texture;
            Assert.That(texture.GetPixel(texture.width / 2, texture.height / 2).a, Is.LessThan(.05f), "The donut's center is transparent.");
            yield return PhysicsTicks(30);
            Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(wave.bounds.size.x, Is.EqualTo(6.6f).Within(.05f));
            Camera.main.orthographicSize = 6;
            Object.Destroy(CaptureFrame("22-sound-wave.png", 1440, 900, false));
            yield return PhysicsTicks(12);
            Place(bodies[3], Vector2.zero); // Enter the empty center after the leading ring passed.
            yield return PhysicsTicks(40); yield return null;
            Assert.That(hits.Count, Is.EqualTo(3));
            Assert.That(hits.Distinct().Count(), Is.EqualTo(3));
            Assert.That(NamedArt("Expanding sound wave").Length, Is.Zero);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.SoundWave);
            game.ResetGame(); yield return null;
            Assert.That(NamedArt("Expanding sound wave").Length, Is.Zero);
        }
    }
}
