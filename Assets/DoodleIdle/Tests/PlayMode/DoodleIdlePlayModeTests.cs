using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
