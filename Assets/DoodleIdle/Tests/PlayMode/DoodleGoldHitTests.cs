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
        static void ConfigureGoldFixture(ParticleSystem gold)
        {
            var main = gold.main; main.simulationSpeed = 1; main.startLifetime = 3;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 6);
            var effect = gold.GetComponent<DoodleGoldCoinBurst>();
            effect.launchSpread = 40; effect.gravity = 18; effect.landedLifetime = .35f; effect.landingYOffset = Vector2.zero;
        }
        [UnityTest]
        public IEnumerator LandedGoldFadesInRenderedPixelsBeforeReturningToPool()
        {
            DurableSkillTargets(); game.paused = true;
            var gold = Particles("GoldCoinBurst"); var effect = gold.GetComponent<DoodleGoldCoinBurst>();
            ConfigureGoldFixture(gold);
            var main = gold.main; main.startSpeed = 0; main.startSize = 1; main.startColor = Color.white;
            var emission = gold.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
            var overLife = gold.colorOverLifetime; overLife.enabled = false;
            effect.landedLifetime = 1;
            var cameraObject = new GameObject("Gold fade pixel test");
            var camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false;
            camera.transform.position = new Vector3(1000, 1000, -10); camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true; camera.orthographicSize = .7f; camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(96, 96, 24); camera.targetTexture = target;
            var pixels = new Texture2D(96, 96, TextureFormat.RGB24, false);
            var previousTarget = RenderTexture.active;
            float ReadBrightness(string filename)
            {
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 96, 96), 0, 0); pixels.Apply();
                System.IO.Directory.CreateDirectory("artifacts/screenshots");
                System.IO.File.WriteAllBytes("artifacts/screenshots/" + filename, pixels.EncodeToPNG());
                return pixels.GetPixels().Sum(c => c.r + c.g + c.b);
            }
            try {
                foreach (float speed in new[] { 1f, 5f }) {
                    main.simulationSpeed = speed; effect.PrepareSimulation();
                    uint seed = 1; effect.EmitBurst(new Vector2(1000, 1000), ref seed); effect.Simulate(.01f / speed);
                    float opaque = ReadBrightness("gold-fade-opaque.png");
                    Assert.That(opaque, Is.GreaterThan(100), "The coin must actually render in the test camera.");
                    for (int i = 0; i < 50; i++) effect.Simulate(.01f / speed);
                    float half = ReadBrightness("gold-fade-half.png");
                    Assert.That(half / opaque, Is.InRange(.25f, .8f), "Half-life alpha must affect rendered pixels, not only particle or renderer properties.");
                    for (int i = 0; i < 45; i++) effect.Simulate(.01f / speed);
                    float nearlyGone = ReadBrightness("gold-fade-nearly-gone.png");
                    Assert.That(nearlyGone / opaque, Is.LessThan(.3f));
                    for (int i = 0; i < 10; i++) effect.Simulate(.01f / speed);
                    Assert.That(gold.particleCount, Is.Zero);
                    Assert.That(gold.GetComponentsInChildren<SpriteRenderer>().All(r => !r.enabled), Is.True);
                }
            } finally {
                RenderTexture.active = previousTarget; camera.targetTexture = null;
                Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(cameraObject);
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator GoldCoinsSortIndividuallyByYAndReuseTheirRenderers()
        {
            var bodies = DurableSkillTargets(); game.paused = true;
            Place(PlayerBody(), Vector2.zero); Place(bodies[0], new Vector2(4, 0));
            var gold = Particles("GoldCoinBurst"); var effect = gold.GetComponent<DoodleGoldCoinBurst>();
            effect.PrepareSimulation();
            var main = gold.main; main.startSize = 1; main.startSpeed = 0;
            var emission = gold.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
            int capacity = gold.GetComponentsInChildren<SpriteRenderer>().Length;
            uint seed = 1;
            effect.EmitBurst(new Vector2(-.4f, -.4f), ref seed);
            effect.EmitBurst(new Vector2(.4f, .4f), ref seed);
            var visible = gold.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).OrderBy(r => r.transform.position.y).ToArray();
            Assert.That(visible.Length, Is.EqualTo(2));
            Assert.That(visible[0].sortingOrder, Is.GreaterThan(100));
            Assert.That(visible[1].sortingOrder, Is.LessThan(100));
            Assert.That(gold.GetComponent<ParticleSystemRenderer>().enabled, Is.False, "Do not draw all coins again in a single frontmost batch.");
            Object.Destroy(CaptureFrame("gold-y-sorting.png", 900, 1200));
            effect.PrepareSimulation();
            Assert.That(gold.GetComponentsInChildren<SpriteRenderer>().All(r => !r.enabled), Is.True);
            effect.EmitBurst(Vector2.zero, ref seed);
            Assert.That(gold.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(capacity));
            yield return null;
        }
        [UnityTest]
        public IEnumerator GoldLandingRangeAndPreviewUseIdenticalSimulation()
        {
            DurableSkillTargets(); game.paused = true;
            var gold = Particles("GoldCoinBurst");
            ConfigureGoldFixture(gold);
            var effect = gold.GetComponent<DoodleGoldCoinBurst>(); effect.landingYOffset = new Vector2(-.6f, .2f);
            var preview = Object.Instantiate(effect); preview.PrepareSimulation();
            try {
                effect.PrepareSimulation();
                typeof(DoodleIdleGame).GetField("particleSeed", GrowthPrivate).SetValue(game, 1u);
                typeof(DoodleIdleGame).GetMethod("EmitGold", GrowthPrivate).Invoke(game, new object[] { Vector2.up * 2 });
                uint seed = 1; preview.EmitBurst(Vector2.up * 2, ref seed);
                var a = new ParticleSystem.Particle[64]; var b = new ParticleSystem.Particle[64];
                var floors = new Dictionary<uint, float>();
                for (int step = 0; step < 90; step++) {
                    typeof(DoodleIdleGame).GetMethod("TickParticles", GrowthPrivate).Invoke(game, new object[] { .02f });
                    preview.Simulate(.02f);
                    int count = gold.GetParticles(a);
                    Assert.That(preview.GetComponent<ParticleSystem>().GetParticles(b), Is.EqualTo(count));
                    for (int i = 0; i < count; i++) {
                        Assert.That(Vector3.Distance(a[i].position, b[i].position), Is.LessThan(.0001f), "Editor preview and gameplay must share the same motion.");
                        if (a[i].velocity.sqrMagnitude > .001f) continue;
                        floors[a[i].randomSeed] = a[i].position.y;
                        Assert.That(a[i].position.y, Is.InRange(1.4f, 2.2f));
                    }
                }
                Assert.That(floors.Count, Is.EqualTo(9));
                Assert.That(floors.Values.Max() - floors.Values.Min(), Is.GreaterThan(.2f), "Coins must land throughout the configured range.");
                Assert.That(gold.particleCount, Is.Zero);
            } finally { Object.Destroy(preview.gameObject); }
            yield return null;
        }
        [UnityTest]
        public IEnumerator GoldPrefabSizeAndSeparateLandingHeightsAreRespected()
        {
            DurableSkillTargets(); game.paused = true;
            var gold = Particles("GoldCoinBurst");
            ConfigureGoldFixture(gold);
            var emit = typeof(DoodleIdleGame).GetMethod("EmitGold", GrowthPrivate);
            var tick = typeof(DoodleIdleGame).GetMethod("TickParticles", GrowthPrivate);
            var main = gold.main; main.startSize = 1.25f;
            emit.Invoke(game, new object[] { new Vector2(-2, -1) });
            emit.Invoke(game, new object[] { new Vector2(2, 2) });
            var buffer = new ParticleSystem.Particle[64];
            int count = gold.GetParticles(buffer); Assert.That(count, Is.EqualTo(18));
            var floors = new Dictionary<uint, float>();
            for (int i = 0; i < count; i++) {
                Assert.That(buffer[i].startSize, Is.EqualTo(1.25f).Within(.001), "Native prefab Start Size must not be overwritten.");
                Assert.That(buffer[i].velocity.y, Is.GreaterThan(0));
                floors[buffer[i].randomSeed] = buffer[i].position.y;
            }
            var landed = new HashSet<uint>(); bool rose = false;
            for (int frame = 0; frame < 70; frame++) {
                tick.Invoke(game, new object[] { .02f });
                count = gold.GetParticles(buffer);
                for (int i = 0; i < count; i++) {
                    float floor = floors[buffer[i].randomSeed];
                    Assert.That(buffer[i].position.y, Is.GreaterThanOrEqualTo(floor - .001f));
                    rose |= buffer[i].position.y > floor + .25f;
                    if (buffer[i].velocity.sqrMagnitude < .001f) {
                        landed.Add(buffer[i].randomSeed);
                        Assert.That(buffer[i].position.y, Is.EqualTo(floor).Within(.001));
                    }
                }
            }
            Assert.That(rose, Is.True); Assert.That(landed.Count, Is.EqualTo(18));
            Assert.That(gold.particleCount, Is.Zero, "Landed coins fade and return to the shared pool.");
            Assert.That(game.GetComponentsInChildren<DoodleGoldCoinBurst>().Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerAndEnemyHitsEmitSlashButInvulnerabilityDoesNot()
        {
            var bodies = DurableSkillTargets(); game.paused = false;
            DayOneState("mainStage", 2); // Exercise a damaging hit independently of tutorial zero-damage feedback.
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var enemy = actors.Cast<object>().Single(a => (Rigidbody2D)a.GetType().GetField("body").GetValue(a) == bodies[0]);
            Place(PlayerBody(), Vector2.zero); Place(bodies[0], Vector2.right * .6f);
            bodies[0].simulated = PlayerBody().simulated = true;
            var slash = Particles("Hit Slash Particle System");
            Assert.That(slash.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture,
                Is.SameAs(Resources.Load<Texture2D>("DoodleIdle/HitSlashStraight")));
            var damage = typeof(DoodleIdleGame).GetMethod("DamageAmount", GrowthPrivate);
            damage.Invoke(game, new object[] { enemy, (GameNumber)1, Vector2.zero, "basicAttack", null });
            Assert.That(slash.particleCount, Is.EqualTo(1));
            var hit = typeof(DoodleIdleGame).GetMethod("ResolveEnemyAttack", GrowthPrivate);
            hit.Invoke(game, new[] { enemy });
            Assert.That(game.PlayerContactHits, Is.EqualTo(1));
            Assert.That(slash.particleCount, Is.EqualTo(2));
            hit.Invoke(game, new[] { enemy });
            Assert.That(slash.particleCount, Is.EqualTo(2), "Blocked hits must not create a new slash.");
            game.paused = true;
            var particles = new ParticleSystem.Particle[2]; slash.GetParticles(particles);
            Assert.That(particles.All(p => p.startLifetime > 0 && p.startLifetime < .25f), Is.True);
            var emitGold = typeof(DoodleIdleGame).GetMethod("EmitGold", GrowthPrivate);
            emitGold.Invoke(game, new object[] { new Vector2(-2, -1) });
            emitGold.Invoke(game, new object[] { new Vector2(2, -1) });
            typeof(DoodleIdleGame).GetMethod("TickParticles", GrowthPrivate).Invoke(game, new object[] { .04f });
            Object.Destroy(CaptureFrame("gold-and-hit-slash.png", 900, 1200));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(slash.particleCount, Is.EqualTo(2), "Paused effects do not advance.");
            typeof(DoodleIdleGame).GetMethod("TickParticles", GrowthPrivate).Invoke(game, new object[] { .3f });
            Assert.That(slash.particleCount, Is.Zero);
            game.ResetGame();
            Assert.That(Particles("GoldCoinBurst").particleCount, Is.Zero);
        }
    }
}
