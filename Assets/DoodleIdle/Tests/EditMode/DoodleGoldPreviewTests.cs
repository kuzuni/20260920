using DoodleIdle;
using NUnit.Framework;
using UnityEngine;

namespace DoodleIdle.Tests
{
    public sealed class DoodleGoldPreviewTests
    {
        [Test]
        public void EditModePreviewKeepsEmittedCoinsThroughFirstSimulation()
        {
            var root = Object.Instantiate(Resources.Load<GameObject>("DoodleIdle/GoldCoinBurst"));
            try {
                var effect = root.GetComponent<DoodleGoldCoinBurst>(); effect.PrepareSimulation();
                effect.landingYOffset = new Vector2(-.25f, -.25f);
                var system = root.GetComponent<ParticleSystem>();
                var main = system.main; main.simulationSpeed = 1; main.startLifetime = 3;
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 6);
                effect.gravity = 18; effect.landedLifetime = .35f; effect.launchSpread = 40;
                uint seed = 1; Assert.That(effect.EmitBurst(Vector2.zero, ref seed), Is.EqualTo(9));
                Assert.That(system.particleCount, Is.EqualTo(9));
                for (int i = 0; i < 15; i++) effect.Simulate(.02f);
                Assert.That(system.particleCount, Is.EqualTo(9), "Editor preview must initialize the native system before emitting.");
                var buffer = new ParticleSystem.Particle[16]; bool landed = false;
                for (int i = 0; i < 65; i++) {
                    effect.Simulate(.02f); int count = system.GetParticles(buffer);
                    for (int p = 0; p < count; p++) if (buffer[p].velocity.sqrMagnitude < .001f) {
                        landed = true; Assert.That(buffer[p].position.y, Is.EqualTo(-.25f).Within(.001));
                    }
                }
                Assert.That(landed, Is.True); Assert.That(system.particleCount, Is.Zero);
            } finally { Object.DestroyImmediate(root); }
        }
    }
}
