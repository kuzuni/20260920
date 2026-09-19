using System.Collections;
using System.Linq;
using System.IO;
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
    public class DoodleIdlePlayModeTests
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
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = originalTimeScale;
            Random.state = originalRandom;
            SceneManager.SetActiveScene(originalScene);
            yield return SceneManager.UnloadSceneAsync(testScene);
        }

        Rigidbody2D[] EnemyBodies() => game.GetComponentsInChildren<Rigidbody2D>().Where(b => b.name == "Horned enemy").ToArray();

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
            var canvas = game.GetComponentInChildren<Canvas>();
            canvas.enabled = includeHud;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            try
            {
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "Visual regression checks require a real graphics device on the CI server.");
                for (int pass = 0; pass < 2; pass++)
                {
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
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator SpriteRenderingKeepsOpaqueArtAndExportsRealGameFrames()
        {
            yield return new WaitForSeconds(.3f);
            game.TogglePause();
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
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = originals[i];
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
        public IEnumerator StartsWith80SolidSeparatedEnemiesAndFiveBananas()
        {
            Assert.That(game.EnemyCount, Is.EqualTo(80));
            var bodies = EnemyBodies();
            Assert.That(bodies.Length, Is.EqualTo(80));
            foreach (var body in bodies)
            {
                Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
                Assert.That(body.gravityScale, Is.Zero);
                Assert.That(body.GetComponent<CircleCollider2D>().isTrigger, Is.False);
            }
            for (int i = 0; i < bodies.Length; i++) for (int j = i + 1; j < bodies.Length; j++)
                Assert.That(Vector2.Distance(bodies[i].position, bodies[j].position), Is.GreaterThanOrEqualTo(1.12f));
            Assert.That(game.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Orbit banana ")), Is.EqualTo(5));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhysicsSeparatesOverlappingEnemies()
        {
            game.autoPlay = false;
            var bodies = EnemyBodies();
            bodies[0].position = new Vector2(13, 8);
            bodies[1].position = new Vector2(13.2f, 8);
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
            Assert.That(game.EnemyCount, Is.EqualTo(80));
            Assert.That(game.Elapsed, Is.Zero);
            Assert.That(game.Kills, Is.Zero);
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AutomaticCombatCastsEverySkillAndRefills()
        {
            Time.timeScale = 8;
            while (game.Elapsed < 4.8f) yield return new WaitForFixedUpdate();
            Assert.That(game.DashCasts, Is.Zero, "Dash must wait for its five-second cooldown.");
            while (game.Elapsed < 5.3f) yield return new WaitForFixedUpdate();
            Assert.That(game.DashCasts, Is.EqualTo(1));
            float worstPenetration = 0;
            while (game.Elapsed < 120)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(game.EnemyCount, Is.InRange(20, 80), "Population should refill immediately below 20.");
                var bodies = EnemyBodies();
                for (int i = 0; i < bodies.Length; i++) for (int j = i + 1; j < bodies.Length; j++)
                    worstPenetration = Mathf.Max(worstPenetration, 1.12f - Vector2.Distance(bodies[i].position, bodies[j].position));
            }
            Assert.That(game.Kills, Is.GreaterThanOrEqualTo(61));
            Assert.That(game.Refills, Is.GreaterThan(0));
            Assert.That(game.DashCasts, Is.GreaterThanOrEqualTo(23));
            Assert.That(game.StonesLaunched, Is.GreaterThan(30));
            Assert.That(game.StonesLaunched % 3, Is.Zero, "Each stone cast should select three distinct targets.");
            Assert.That(game.BananaHits, Is.GreaterThan(0));
            Assert.That(game.SlashHits, Is.GreaterThan(0));
            Assert.That(game.DashHits, Is.GreaterThan(0));
            Assert.That(worstPenetration, Is.LessThan(.09f), "Physics separation must hold throughout combat, within solver tolerance.");
            Debug.Log("Doodle combat diagnostics: " + game.Diagnostics());
        }
    }
}
