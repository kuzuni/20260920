using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        DoodleIdleGame game;
        Scene originalScene, testScene;
        Random.State originalRandom;
        float originalTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalRandom = Random.state;
            Random.InitState(20260920);
            originalScene = SceneManager.GetActiveScene();
            // Load the delivered scene so CI also verifies its GUID/component wiring.
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/DoodleIdle/DoodleIdle.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            testScene = SceneManager.GetSceneByPath("Assets/DoodleIdle/DoodleIdle.unity");
#else
            yield return SceneManager.LoadSceneAsync("DoodleIdle", LoadSceneMode.Additive);
            testScene = SceneManager.GetSceneByName("DoodleIdle");
#endif
            SceneManager.SetActiveScene(testScene);
            game = testScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DoodleIdleGame>()).Single();
            yield return null;
            Assert.That(game.Ready, Is.True, "Generated assets and game bootstrap must load.");
            game.summonSkillsEnabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = originalTimeScale;
            Random.state = originalRandom;
            SceneManager.SetActiveScene(originalScene);
            yield return SceneManager.UnloadSceneAsync(testScene);
        }

        Rigidbody2D[] EnemyBodies() => game.GetComponentsInChildren<Rigidbody2D>().Where(b => b.name.StartsWith("Enemy - ")).ToArray();

        Texture2D CaptureFrame(string filename, int width, int height, bool includeHud = true)
        {
            var camera = Camera.main;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            camera.targetTexture = target;
            camera.aspect = width / (float)height;
            game.RefreshHudLayout();
            var canvas = game.GetComponentsInChildren<Canvas>().Single(c => c.name == "Prototype HUD");
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            bool scalerEnabled = scaler.enabled;
            float previousScale = canvas.scaleFactor;
            // The CI desktop is smaller than the render target. Request glyphs at capture resolution,
            // rather than upscaling the desktop's low-resolution dynamic counter glyphs.
            scaler.enabled = false;
            canvas.scaleFactor = width / (width < height ? 720f : 1440f);
            canvas.enabled = includeHud;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            foreach (var label in canvas.GetComponentsInChildren<UnityEngine.UI.Text>()) label.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            try
            {
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "Visual regression checks require a real graphics device on the CI server.");
                for (int pass = 0; pass < 2; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    else camera.Render();
                }
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/screenshots"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, filename), image.EncodeToPNG());
                return image;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.enabled = true;
                canvas.scaleFactor = previousScale;
                scaler.enabled = scalerEnabled;
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator SpriteRenderingKeepsOpaqueArtAndExportsRealGameFrames()
        {
            yield return new WaitForSeconds(.3f);
            game.TogglePause();
            yield return null; // Flush effects already queued for Destroy before collecting renderers.
            var renderers = game.GetComponentsInChildren<SpriteRenderer>();
            var playerHead = renderers.Single(r => r.name == "Generated head sprite" && r.transform.parent.name == "Player - head and club");
            var floor = renderers.First(r => r.name == "Generated dirt floor");
            Assert.That(playerHead.sharedMaterial, Is.Not.SameAs(floor.sharedMaterial));
            Assert.That(playerHead.sharedMaterial.mainTexture, Is.SameAs(playerHead.sprite.texture));
            Assert.That(floor.sharedMaterial.mainTexture, Is.SameAs(floor.sprite.texture));
            var originals = renderers.Select(r => r.sharedMaterial).ToArray();
            var legacy = new Material(Shader.Find("Sprites/Default"));
            try
            {
                foreach (var renderer in renderers) renderer.sharedMaterial = legacy;
                yield return null; // Let Unity rebuild its sprite render data after material changes.
                Object.Destroy(CaptureFrame("01-legacy-material-portrait.png", 720, 1560));
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) if (renderers[i]) renderers[i].sharedMaterial = originals[i];
                Object.Destroy(legacy);
            }
            yield return null;
            Object.Destroy(CaptureFrame("02-fixed-portrait.png", 720, 1560));
            Object.Destroy(CaptureFrame("03-fixed-landscape.png", 1440, 900));

            var head = renderers.Single(r => r.name == "Generated head sprite" && r.transform.parent.name == "Player - head and club");
            // Keep the floor visible: rendering the head alone would miss texture-binding regressions.
            foreach (var renderer in renderers) renderer.forceRenderingOff = renderer != head && renderer.name != "Generated dirt floor";
            var camera = Camera.main;
            camera.transform.position = head.transform.position + Vector3.back * 10;
            camera.orthographicSize = 1;
            camera.backgroundColor = Color.magenta;
            var closeup = CaptureFrame("04-opaque-player-check.png", 256, 256, false);
            int whitePixels = closeup.GetPixels32().Count(p => p.r > 210 && p.g > 210 && p.b > 210);
            Object.Destroy(closeup);
            Assert.That(whitePixels, Is.GreaterThan(2500), "The player must render its opaque off-white body, not the floor texture or transparent cutouts.");
            Debug.Log("Rendered player opaque white pixel count: " + whitePixels);
        }

        [UnityTest]
        public IEnumerator StartsWith200SolidSeparatedEnemiesAndFiveBananas()
        {
            // Inspect spawn placement before movement/solver contact tolerance can change it.
            game.ResetGame();
            game.TogglePause();
            yield return null; // Flush the previous population and banana visuals queued for Destroy.
            Assert.That(game.EnemyCount, Is.EqualTo(200));
            Assert.That(game.arenaHalfSize, Is.EqualTo(new Vector2(17, 20)));
            var bodies = EnemyBodies();
            Assert.That(bodies.Length, Is.EqualTo(200));
            Assert.That(bodies.Any(b => b.position.y > 12) && bodies.Any(b => b.position.y < -12), Is.True);
            Assert.That(NamedArt("Enemy HP fill").Length, Is.EqualTo(200));
            foreach (var body in bodies)
            {
                Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
                Assert.That(body.gravityScale, Is.Zero);
                Assert.That(body.GetComponent<CircleCollider2D>().isTrigger, Is.False);
            }
            for (int i = 0; i < bodies.Length; i++) for (int j = i + 1; j < bodies.Length; j++)
                Assert.That(Vector2.Distance(bodies[i].position, bodies[j].position), Is.GreaterThanOrEqualTo(1.12f));
            Assert.That(game.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Orbit banana ")), Is.EqualTo(5));
            Assert.That(Application.runInBackground, Is.True);
            foreach (var banana in game.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Orbit banana ")))
                Assert.That(banana.localScale.x, Is.EqualTo(1.16f).Within(.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArrowAndDroneBurstsAreSequentialAndHaveExactCounts()
        {
            game.extraSkillsEnabled = false;
            var arrows = new List<float>(); var missiles = new List<float>();
            game.SkillProjectileLaunched += (skill, time, target) =>
            {
                if (skill == DoodleIdleGame.ExtraSkill.Arrows) arrows.Add(time);
                if (skill == DoodleIdleGame.ExtraSkill.Drone) missiles.Add(time);
            };
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Arrows);
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Drone);
            yield return new WaitForSeconds(1.7f);
            Assert.That(arrows.Count, Is.EqualTo(10)); Assert.That(missiles.Count, Is.EqualTo(20));
            for (int i = 1; i < arrows.Count; i++) Assert.That(arrows[i] - arrows[i - 1], Is.GreaterThanOrEqualTo(.099f));
            for (int i = 1; i < missiles.Count; i++) Assert.That(missiles[i] - missiles[i - 1], Is.GreaterThanOrEqualTo(.059f));
            Assert.That(missiles.Last() - missiles.First(), Is.LessThan(1.6f));
            yield return new WaitForSeconds(2);
            Assert.That(game.ArrowHits, Is.GreaterThan(0)); Assert.That(game.MissileHits, Is.GreaterThan(0));
            Assert.That(game.ArrowsLaunched, Is.EqualTo(10)); Assert.That(game.MissilesLaunched, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator BallHitsSevenEnemiesWithoutConsecutiveRepeatThenDisappears()
        {
            game.extraSkillsEnabled = false;
            var targets = new List<int>(); var counts = new List<int>();
            game.BallEnemyHit += (target, count) => { targets.Add(target); counts.Add(count); };
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.BouncyBall);
            float timeout = game.Elapsed + 15;
            while (game.BallsCompleted == 0 && game.Elapsed < timeout) yield return new WaitForFixedUpdate();
            Assert.That(game.BallsCompleted, Is.EqualTo(1));
            Assert.That(game.LastCompletedBallHits, Is.EqualTo(7));
            Assert.That(targets.Count, Is.EqualTo(7));
            CollectionAssert.AreEqual(Enumerable.Range(1, 7), counts);
            for (int i = 1; i < targets.Count; i++) Assert.That(targets[i], Is.Not.EqualTo(targets[i - 1]));
            yield return null;
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Ball skill projectile"), Is.False);
            yield return new WaitForSeconds(.5f);
            Assert.That(game.BallHits, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator FireTargetsThreeEnemiesAndWormAnimatesWithPauseAndReset()
        {
            game.extraSkillsEnabled = false;
            var targets = new HashSet<int>();
            game.SkillProjectileLaunched += (skill, time, target) => { if (skill == DoodleIdleGame.ExtraSkill.Fire) targets.Add(target); };
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Fire);
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Worm);
            Assert.That(targets.Count, Is.EqualTo(3)); Assert.That(game.FireballsLaunched, Is.EqualTo(3));
            yield return new WaitForFixedUpdate();
            var parts = game.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Spiral worm")).ToArray();
            Assert.That(parts.Count(r => r.enabled), Is.LessThan(13), "Segments must emerge sequentially.");
            yield return new WaitForSeconds(.2f);
            Assert.That(game.GetComponentsInChildren<Transform>().Any(t => t.name == "Flame afterimage"), Is.True);
            // Body spacing is distance-based: the head must travel far enough to reveal all 12 links.
            yield return new WaitForSeconds(1.7f);
            Assert.That(parts.Count(r => r.enabled), Is.EqualTo(13));
            game.TogglePause();
            var positions = parts.Select(p => p.transform.position).ToArray();
            yield return new WaitForSecondsRealtime(.15f);
            for (int i = 0; i < parts.Length; i++) Assert.That(parts[i].transform.position, Is.EqualTo(positions[i]));
            game.TogglePause();
            yield return new WaitForSeconds(2);
            Assert.That(game.FireHits, Is.GreaterThan(0));
            game.ResetGame();
            yield return null;
            Assert.That(game.ActiveExtraProjectiles, Is.Zero);
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("Spiral worm")), Is.Zero);
            Assert.That(game.GetComponentsInChildren<Transform>().Count(t => t.name == "Following missile drone"), Is.EqualTo(1));
            Assert.That(game.FireballsLaunched, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ExpandedCombatExportsActualRenderedSkillFrames()
        {
            game.extraSkillsEnabled = false;
            game.autoPlay = false;
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Worm);
            yield return new WaitForSeconds(1.6f);
            foreach (var skill in new[] { DoodleIdleGame.ExtraSkill.Arrows, DoodleIdleGame.ExtraSkill.BouncyBall, DoodleIdleGame.ExtraSkill.Fire, DoodleIdleGame.ExtraSkill.Drone }) game.CastExtraSkill(skill);
            yield return new WaitForSeconds(.16f);
            game.TogglePause();
            Camera.main.orthographicSize = 6;
            Object.Destroy(CaptureFrame("05-new-skills-portrait.png", 720, 1560));
            Object.Destroy(CaptureFrame("06-new-skills-landscape.png", 1440, 900));
            game.TogglePause(); game.extraSkillsEnabled = true; game.autoPlay = true;
            yield return new WaitForSeconds(5);
            game.TogglePause();
            Object.Destroy(CaptureFrame("07-live-combat.png", 1440, 900));
        }

        [UnityTest]
        public IEnumerator PhysicsSeparatesOverlappingEnemies()
        {
            game.autoPlay = false;
            var bodies = EnemyBodies();
            Place(bodies[0], new Vector2(13, 8));
            Place(bodies[1], new Vector2(13.2f, 8));
            for (int i = 0; i < 35; i++) yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(bodies[0].position, bodies[1].position), Is.GreaterThan(1.07f), "Solid circles should resolve an overlapping starting position.");
        }

        [UnityTest]
        public IEnumerator PauseFreezesPhysicsAndRestartRestoresPopulation()
        {
            yield return new WaitForSeconds(.3f);
            game.TogglePause();
            float elapsed = game.Elapsed;
            var bodies = EnemyBodies();
            var positions = bodies.Select(b => b.position).ToArray();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(game.Elapsed, Is.EqualTo(elapsed));
            for (int i = 0; i < bodies.Length; i++) Assert.That(bodies[i].position, Is.EqualTo(positions[i]));
            game.ResetGame();
            Assert.That(game.paused, Is.False);
            Assert.That(game.EnemyCount, Is.EqualTo(200));
            Assert.That(game.Elapsed, Is.Zero);
            Assert.That(game.Kills, Is.Zero);
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AutomaticCombatCastsEverySkillAndRefills()
        {
            game.summonSkillsEnabled = true;
            Time.timeScale = 8;
            // At 8x speed one rendered frame can cross the five-second boundary.
            // Verify the actual cast timestamp instead of sampling Update's elapsed clock before it.
            while (game.DashCasts == 0 && game.Elapsed < 10) yield return new WaitForFixedUpdate();
            Assert.That(game.DashCasts, Is.EqualTo(1));
            Assert.That(game.FirstDashTime, Is.InRange(4.98f, 5.05f), "Dash must first activate after five seconds of physics simulation.");
            float worstPenetration = 0;
            while (game.Elapsed < 120)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(game.EnemyCount, Is.InRange(20, 200), "Population should refill immediately below 20.");
                var bodies = EnemyBodies();
                for (int i = 0; i < bodies.Length; i++) for (int j = i + 1; j < bodies.Length; j++)
                    worstPenetration = Mathf.Max(worstPenetration, 1.12f - Vector2.Distance(bodies[i].position, bodies[j].position));
            }
            Assert.That(game.Kills, Is.GreaterThanOrEqualTo(181));
            Assert.That(game.Refills, Is.GreaterThan(0));
            Assert.That(game.DashCasts, Is.GreaterThanOrEqualTo(23));
            Assert.That(game.StonesLaunched, Is.GreaterThan(30));
            Assert.That(game.StonesLaunched % 3, Is.Zero, "Each stone cast should select three distinct targets.");
            Assert.That(game.BananaHits, Is.GreaterThan(0));
            Assert.That(game.SlashHits, Is.GreaterThan(0));
            Assert.That(game.DashHits, Is.GreaterThan(0));
            Assert.That(game.ArrowHits, Is.GreaterThan(0));
            Assert.That(game.BallsCompleted, Is.GreaterThan(0));
            Assert.That(game.FireHits, Is.GreaterThan(0));
            Assert.That(game.MissileHits, Is.GreaterThan(0));
            Assert.That(game.WormHits, Is.GreaterThan(0));
            Assert.That(game.ActiveExtraProjectiles, Is.LessThan(100), "Expired projectiles must be cleaned up during extended combat.");
            foreach (DoodleIdleGame.SummonSkill skill in System.Enum.GetValues(typeof(DoodleIdleGame.SummonSkill)))
            {
                Assert.That(game.SummonCasts(skill), Is.GreaterThan(0), skill + " must cast automatically.");
                Assert.That(game.SummonHits(skill), Is.GreaterThan(0), skill + " must damage real enemies.");
            }
            Assert.That(game.ActiveSummonObjects, Is.LessThan(180));
            Assert.That(game.ActiveStains, Is.LessThanOrEqualTo(180));
            Assert.That(game.ActiveDamageNumbers, Is.LessThanOrEqualTo(128));
            Assert.That(game.GetComponentsInChildren<ParticleSystem>().Length, Is.EqualTo(5));
            Assert.That(game.GoldCoinsEmitted, Is.EqualTo(game.Kills * 9));
            Assert.That(worstPenetration, Is.LessThan(.09f), "Physics separation must hold throughout combat, within solver tolerance.");
            Debug.Log("Doodle combat diagnostics: " + game.Diagnostics());
        }
    }
}

