using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using DG.Tweening;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        ParticleSystem Particles(string name) => game.GetComponentsInChildren<ParticleSystem>().Single(p => p.name == name);

        [UnityTest]
        public IEnumerator PlayerAndThreeEnemySpeciesAnimateTwoMovementFrames()
        {
            var bodies = IsolateSummonTest();
            string[] species = { "새싹 슬라임", "들쥐", "분홍 버섯" };
            var selected = species.Select(name => bodies.First(b => b.name == "Enemy - " + name)).ToArray();
            for (int i = 0; i < selected.Length; i++)
            {
                Place(selected[i], new Vector2((i - 1) * 3, 3)); selected[i].simulated = true;
            }
            game.autoPlay = true; game.moveSpeed = 1;
            var arts = new[] { PlayerBody().GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Generated head sprite") }
                .Concat(selected.Select(b => b.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Generated head sprite"))).ToArray();
            var seen = arts.Select(a => new HashSet<string>()).ToArray();
            var drone = NamedArt("Following missile drone").Single();
            var droneFrames = new HashSet<string>();
            var club = NamedArt("Floating baseball club").Single();
            Assert.That(club.sprite.pivot.x / club.sprite.rect.width, Is.EqualTo(.15f).Within(.001f));
            Assert.That(club.sprite.pivot.y / club.sprite.rect.height, Is.EqualTo(.12f).Within(.001f));
            bool capturedA = false, capturedB = false;
            Camera.main.orthographicSize = 5;
            for (int frame = 0; frame < 70; frame++)
            {
                yield return null;
                droneFrames.Add(drone.sprite.name);
                Assert.That(drone.sharedMaterial.mainTexture, Is.SameAs(drone.sprite.texture));
                Vector3 hand = arts[0].transform.TransformPoint(new Vector3(club.flipX ? -.4f : .4f, -.18f, 0));
                Assert.That(Vector3.Distance(club.transform.position, hand), Is.LessThan(.001f), "The bat grip stays at the hand through head bob/tilt.");
                for (int i = 0; i < arts.Length; i++)
                {
                    seen[i].Add(arts[i].sprite.name);
                    Assert.That(arts[i].sharedMaterial.mainTexture, Is.SameAs(arts[i].sprite.texture));
                }
                if (!capturedA && arts[0].sprite.name == "PlayerWalkA")
                { Object.Destroy(CaptureFrame("17-movement-frame-a.png", 1440, 900)); capturedA = true; }
                if (!capturedB && arts[0].sprite.name == "PlayerWalkB")
                { Object.Destroy(CaptureFrame("18-movement-frame-b.png", 1440, 900)); capturedB = true; }
            }
            CollectionAssert.AreEquivalent(new[] { "PlayerWalkA", "PlayerWalkB" }, seen[0]);
            CollectionAssert.AreEquivalent(new[] { "RobotDroneA", "RobotDroneB" }, droneFrames);
            for (int i = 0; i < species.Length; i++)
                CollectionAssert.AreEquivalent(new[] { "Meadow" + i + "A", "Meadow" + i + "B" }, seen[i + 1]);
            Assert.That(game.EnemyCount, Is.EqualTo(200));
            game.TogglePause();
            var pausedFrames = arts.Select(a => a.sprite).ToArray();
            yield return new WaitForSecondsRealtime(.2f);
            for (int i = 0; i < arts.Length; i++) Assert.That(arts[i].sprite, Is.SameAs(pausedFrames[i]));
            game.TogglePause(); game.autoPlay = false; game.moveSpeed = 0;
            Place(PlayerBody(), new Vector2(10, 10)); PlayerBody().linearVelocity = Vector2.zero;
            yield return PhysicsTicks(3); yield return null;
            Assert.That(arts[0].sprite.name, Is.EqualTo("PlayerWalkA"), "A stationary player returns to the original design.");
            Assert.That(arts[0].sprite.texture, Is.SameAs(Resources.Load<Texture2D>("DoodleIdle/Characters")));
        }

        [UnityTest]
        public IEnumerator CannonLaunchesFromMirroredMuzzleAndBouncesWithDOTween()
        {
            foreach (int side in new[] { 1, -1 })
            {
                game.ResetGame(); yield return null;
                var bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(4 * side, 0));
                var launches = new List<Vector2>();
                System.Action<Vector2, Vector2> onLaunch = (origin, destination) => launches.Add(origin);
                game.CannonProjectileLaunched += onLaunch;
                game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cannon);
                var cannon = NamedArt("Stationary ten second cannon").Single();
                var muzzle = cannon.transform.Find("Cannon muzzle");
                yield return PhysicsTicks(1);
                Assert.That(launches.Count, Is.EqualTo(1));
                Assert.That(cannon.flipX, Is.EqualTo(side < 0));
                Assert.That(launches[0].x * side, Is.GreaterThan(.65f));
                Assert.That(Vector2.Distance(launches[0], muzzle.position), Is.LessThan(.001f));
                var ball = NamedArt("Cannon moving skill").Single();
                Assert.That(Vector2.Distance(ball.transform.position, launches[0]), Is.LessThan(.35f), "The first visible frame must emerge from the opening, not the center of the cannon.");
                Assert.That(DOTween.IsTweening(cannon.transform), Is.True);
                yield return PhysicsTicks(4);
                Assert.That(Vector3.Distance(cannon.transform.localScale, new Vector3(1.8f, 1.8f, 1)), Is.GreaterThan(.05f));
                var scale = cannon.transform.localScale; var rotation = cannon.transform.localRotation;
                game.TogglePause(); yield return new WaitForSecondsRealtime(.15f);
                Assert.That(cannon.transform.localScale, Is.EqualTo(scale));
                Assert.That(cannon.transform.localRotation, Is.EqualTo(rotation));
                Camera.main.orthographicSize = 3;
                Object.Destroy(CaptureFrame(side > 0 ? "15-cannon-bounce-right.png" : "16-cannon-bounce-left.png", 960, 720, false));
                game.TogglePause(); foreach (var body in EnemyBodies()) body.simulated = false;
                yield return PhysicsTicks(23);
                Assert.That(Vector3.Distance(cannon.transform.localScale, new Vector3(1.8f, 1.8f, 1)), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(cannon.transform.localRotation, Quaternion.identity), Is.LessThan(.01f));
                Assert.That((Vector2)cannon.transform.position, Is.EqualTo(Vector2.zero), "Recoil must not move the installed emplacement.");
                game.CannonProjectileLaunched -= onLaunch;
                yield return PhysicsTicks(20); // A second launch should create another recoil sequence.
                Assert.That(game.CannonShots, Is.EqualTo(2));
                Assert.That(DOTween.IsTweening(cannon.transform), Is.True);
                var previousVisual = cannon.transform;
                game.ResetGame();
                Assert.That(DOTween.IsTweening(previousVisual), Is.False, "Reset must kill the active recoil tween.");
                yield return null;
            }
        }

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
            Assert.That(text.fontSize, Is.EqualTo(84));
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
            var coins = new ParticleSystem.Particle[gold.particleCount]; gold.GetParticles(coins);
            Assert.That(coins.All(p => p.startLifetime >= .4f && p.startLifetime <= .625f), Is.True);
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

