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
        public IEnumerator SoundWaveFiresFiveGrowingRingsTowardEnemiesAndCleansUpItsVolley()
        {
            var bodies = IsolateSummonTest();
            for (int i = 0; i < bodies.Length; i++) Place(bodies[i], new Vector2(14 + i % 4, 15 + i / 4));
            Place(bodies[0], new Vector2(4, 0)); Place(bodies[1], new Vector2(-5, 0));
            Place(bodies[2], new Vector2(7, 1));
            int frontId = bodies[0].gameObject.GetInstanceID(), behindId = bodies[1].gameObject.GetInstanceID();
            int distantId = bodies[2].gameObject.GetInstanceID();
            var hits = new List<int>();
            var launches = new List<float>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.SoundWave) hits.Add(id); };
            game.SoundWaveLaunched += time => launches.Add(time);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.SoundWave);
            var wave = NamedArt("Traveling sound wave").Single();
            Assert.That(wave.bounds.size.x / wave.bounds.size.y, Is.EqualTo(.55f).Within(.01f), "The wavefront must already be broad across travel on its first frame.");
            var texture = wave.sprite.texture;
            Assert.That(texture.GetPixel(texture.width / 2, texture.height / 2).a, Is.LessThan(.05f), "The donut's center is transparent.");
            yield return PhysicsTicks(10);
            Assert.That(game.SoundWavesLaunched, Is.EqualTo(2), "Rings must be launched sequentially.");
            Assert.That(wave.transform.position.x, Is.EqualTo(1.4f).Within(.03f));
            Assert.That(wave.transform.position.y, Is.EqualTo(0).Within(.01f));
            Assert.That(wave.bounds.size.x, Is.EqualTo(1.02f * .55f).Within(.03f));
            Assert.That(wave.bounds.size.y, Is.EqualTo(1.02f).Within(.03f));
            yield return PhysicsTicks(28);
            Assert.That(game.SoundWavesLaunched, Is.EqualTo(5));
            Assert.That(launches.Count, Is.EqualTo(5));
            for (int i = 1; i < launches.Count; i++) Assert.That(launches[i] - launches[i - 1], Is.InRange(.159f, .201f));
            var rings = NamedArt("Traveling sound wave").OrderBy(r => r.transform.position.x).ToArray();
            Assert.That(rings.Length, Is.EqualTo(5));
            for (int i = 1; i < rings.Length; i++)
            {
                Assert.That(rings[i].transform.position.x, Is.GreaterThan(rings[i - 1].transform.position.x + 1));
                Assert.That(rings[i].bounds.size.x, Is.GreaterThan(rings[i - 1].bounds.size.x));
            }
            Camera.main.orthographicSize = 6;
            Object.Destroy(CaptureFrame("22-directional-sound-volley.png", 1440, 900, false));
            yield return PhysicsTicks(100); yield return null;
            Assert.That(hits.Count(id => id == frontId), Is.EqualTo(3), "Separate rings can hit the same enemy until it dies.");
            Assert.That(hits.Count(id => id == distantId), Is.EqualTo(3));
            Assert.That(hits.Contains(behindId), Is.False, "A directional volley must not become a radial blast behind the player.");
            Assert.That(NamedArt("Traveling sound wave").Length, Is.Zero);

            // The remaining closest enemy is behind: the next volley must aim left.
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.SoundWave);
            yield return PhysicsTicks(4);
            var leftWave = NamedArt("Traveling sound wave").Single();
            Assert.That(leftWave.transform.position.x, Is.LessThan(-.5f));
            Assert.That(leftWave.bounds.size.x / leftWave.bounds.size.y, Is.EqualTo(.55f).Within(.01f));
            game.TogglePause();
            var position = leftWave.transform.position; var scale = leftWave.transform.localScale;
            int pausedLaunches = game.SoundWavesLaunched;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(leftWave.transform.position, Is.EqualTo(position));
            Assert.That(leftWave.transform.localScale, Is.EqualTo(scale));
            Assert.That(game.SoundWavesLaunched, Is.EqualTo(pausedLaunches));
            game.TogglePause(); foreach (var body in EnemyBodies()) body.simulated = false;
            yield return PhysicsTicks(8);
            Assert.That(game.SoundWavesLaunched, Is.EqualTo(pausedLaunches + 1));
            game.ResetGame(); yield return null;
            IsolateSummonTest(); yield return PhysicsTicks(50);
            Assert.That(game.SoundWavesLaunched, Is.Zero, "Reset clears queued shots as well as visible rings.");
            Assert.That(NamedArt("Traveling sound wave").Length, Is.Zero);
            // A vertical shot rotates the broad front with its direction instead of staying tall.
            var verticalBodies = EnemyBodies();
            for (int i = 0; i < verticalBodies.Length; i++) Place(verticalBodies[i], new Vector2(14 + i % 4, 15 + i / 4));
            Place(PlayerBody(), Vector2.zero); Place(verticalBodies[0], new Vector2(0, 4));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.SoundWave);
            var upWave = NamedArt("Traveling sound wave").Single();
            Assert.That(upWave.bounds.size.y / upWave.bounds.size.x, Is.EqualTo(.55f).Within(.01f));
            yield return PhysicsTicks(10);
            Assert.That(upWave.transform.position.y, Is.GreaterThan(1.3f));
            Assert.That(upWave.bounds.size.y / upWave.bounds.size.x, Is.EqualTo(.55f).Within(.01f));
            Object.Destroy(CaptureFrame("22-vertical-sound-front.png", 1440, 900, false));
        }
    }
}
