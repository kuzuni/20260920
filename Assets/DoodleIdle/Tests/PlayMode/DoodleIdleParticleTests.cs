using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        ParticleSystem Particles(string name) => game.GetComponentsInChildren<ParticleSystem>().Single(p => p.name == name);

        [UnityTest]
        public IEnumerator CloudAnimatesAndPurpleTetherStaysBehindPlayer()
        {
            var bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(4, 0));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.TetherSnake);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.StormCloud);
            var cloud = NamedArt("Drifting storm cloud").Single();
            var frames = new HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                yield return new WaitForFixedUpdate(); frames.Add(cloud.sprite.name);
                Assert.That(cloud.sharedMaterial.mainTexture, Is.SameAs(cloud.sprite.texture));
            }
            Assert.That(frames.Count, Is.EqualTo(2));
            var player = NamedArt("Generated head sprite").Single(r => r.transform.parent.name.StartsWith("Player -"));
            foreach (var part in game.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("TetherSnake")))
            {
                Assert.That(part.sprite.name, Does.StartWith("PurpleSnake"));
                Assert.That(part.sortingOrder, Is.LessThan(player.sortingOrder));
            }
            Camera.main.orthographicSize = 4.5f;
            game.TogglePause(); yield return null;
            Object.Destroy(CaptureFrame("12-purple-tether-cloud.png", 1440, 900));
        }

        [UnityTest]
        public IEnumerator CannonDustAndGoldUseVisibleParticlesWithHandDrawnHealthFeedback()
        {
            var bodies = IsolateSummonTest();
            Place(bodies[0], new Vector2(3, 0)); Place(bodies[1], new Vector2(3, 1.3f)); Place(bodies[2], new Vector2(4.3f, 0));
            var fill = bodies[0].GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Enemy HP fill");
            Assert.That(fill.sprite.name, Is.EqualTo("HealthBarFill"));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cannon);
            yield return PhysicsTicks(45);
            Assert.That(fill.transform.localScale.x / .9f, Is.EqualTo(22f / 68).Within(.01f));
            Assert.That(game.ActiveDamageNumbers, Is.GreaterThan(0));
            var text = game.GetComponentsInChildren<Text>().First(t => t.name == "Enemy damage number");
            Assert.That(text.text, Is.EqualTo("46"));
            Assert.That(text.font, Is.SameAs(Resources.Load<Font>("DoodleIdle/InterfaceFont")));
            Assert.That(Particles("Cannon Explosion Particle System").particleCount, Is.GreaterThan(0));
            Assert.That(Particles("Dust Particle System").particleCount, Is.GreaterThan(0));
            Assert.That(NamedArt("Cannon explosion").Length, Is.Zero, "The explosion must not be a single fading SpriteRenderer.");
            Assert.That(NamedArt("Hit dust").Length, Is.Zero);
            Camera.main.orthographicSize = 5;
            game.TogglePause(); yield return null;
            var visible = CaptureFrame("13-particle-explosion-hp.png", 1440, 900, false);
            var renderers = game.GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
                renderer.enabled = false;
            }
            var hidden = CaptureFrame("13-particle-comparison-hidden.png", 1440, 900, false);
            var a = visible.GetPixels32(); var b = hidden.GetPixels32(); int changed = 0;
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 25) changed++;
            Object.Destroy(visible); Object.Destroy(hidden);
            Assert.That(changed, Is.GreaterThan(150), "Particles must actually render on the CI GPU, not only exist as components.");
            foreach (var renderer in renderers) renderer.enabled = true;
            var dust = Particles("Dust Particle System"); var before = new ParticleSystem.Particle[dust.particleCount];
            dust.GetParticles(before);
            yield return new WaitForSecondsRealtime(.15f);
            var after = new ParticleSystem.Particle[dust.particleCount]; dust.GetParticles(after);
            Assert.That(after.Length, Is.EqualTo(before.Length));
            for (int i = 0; i < before.Length; i++)
            {
                Assert.That(after[i].position, Is.EqualTo(before[i].position));
                Assert.That(after[i].remainingLifetime, Is.EqualTo(before[i].remainingLifetime));
            }
            game.TogglePause(); foreach (var body in EnemyBodies()) body.simulated = false;
            while (game.GoldCoinsEmitted == 0 && game.Elapsed < 4) yield return new WaitForFixedUpdate();
            Assert.That(game.GoldCoinsEmitted, Is.EqualTo(27));
            yield return PhysicsTicks(6);
            game.TogglePause(); yield return null;
            var gold = Particles("Gold Coin Particle System");
            Assert.That(gold.particleCount, Is.GreaterThan(0));
            Assert.That(gold.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(gold.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture, Is.SameAs(Resources.Load<Texture2D>("DoodleIdle/GoldCoin")));
            Object.Destroy(CaptureFrame("14-death-gold-coins.png", 1440, 900));
            var stains = NamedArt("Fading black death stain");
            Assert.That(stains.Length, Is.EqualTo(3));
            var positions = stains.Select(s => s.transform.position).ToArray();
            foreach (var stain in stains)
                Assert.That(stain.sprite.bounds.size.x * stain.transform.localScale.x / (stain.sprite.bounds.size.y * stain.transform.localScale.y), Is.EqualTo(1).Within(.01f));
            PlayerBody().position = new Vector2(-3, -3);
            for (int i = 0; i < stains.Length; i++) Assert.That(stains[i].transform.position, Is.EqualTo(positions[i]));
            game.ResetGame(); yield return null;
            Assert.That(game.ActiveDamageNumbers, Is.Zero);
            Assert.That(game.GoldCoinsEmitted, Is.Zero);
            Assert.That(game.GetComponentsInChildren<ParticleSystem>().All(p => p.particleCount == 0), Is.True);
        }
    }
}

