using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator DistantEnemiesDoNotAllocateAttackClosuresEveryPhysicsStep()
        {
            game.TogglePause();
            foreach (var body in EnemyBodies()) body.position += Vector2.one * 100;
            Physics2D.SyncTransforms();
            var method = typeof(DoodleIdleGame).GetMethod("TickPlayerContactDamage", GrowthPrivate);
            var tick = (System.Action<float>)System.Delegate.CreateDelegate(typeof(System.Action<float>), game, method);
            game.Ui.BeginCombatSnapshot();
            try {
                for (int i = 0; i < 10; i++) tick(.02f);
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 32; i++) tick(.02f);
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.LessThan(4096), "Idle enemies must not create per-step callback closures");
            } finally { game.Ui.EndCombatSnapshot(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadingFinishesBeforePhysicsCombatOrCooldownsAdvance()
        {
            SceneManager.SetActiveScene(originalScene);
            yield return SceneManager.UnloadSceneAsync(testScene);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/DoodleIdle/DoodleIdle.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            yield return SceneManager.LoadSceneAsync("DoodleIdle", LoadSceneMode.Additive);
#endif
            testScene = SceneManager.GetSceneByName("DoodleIdle"); SceneManager.SetActiveScene(testScene);
            game = testScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DoodleIdleGame>()).Single();
            Time.timeScale = 1;
            float deadline = Time.realtimeSinceStartup + 60;
            bool observedWorld = false;
            Vector2? artPosition=null;
            float? firstFill=null;
            bool observedMotion=false, captured=false;
            string randomAtPreparation = null;
            while (!game.Ready && Time.realtimeSinceStartup < deadline) {
                Assert.That(game.Elapsed, Is.Zero);
                Assert.That(game.Kills + game.CompanionAttacks + game.StonesLaunched + game.DashCasts, Is.Zero);
                if (game.LoadingProgress >= .6f) {
                    observedWorld = true;
                    Assert.That(game.paused, Is.True);
                    foreach (var body in game.GetComponentsInChildren<Rigidbody2D>()) Assert.That(body.simulated, Is.False);
                    var state = JsonUtility.ToJson(Random.state);
                    if (randomAtPreparation == null) randomAtPreparation = state;
                    else Assert.That(state, Is.EqualTo(randomAtPreparation), "Prewarming must not consume combat RNG");
                    Assert.That(game.GetComponentsInChildren<Canvas>().Any(c => c.name == "Preparing game" && c.isActiveAndEnabled), Is.True);
                    var motion=game.GetComponentInChildren<DoodleLoadingMotion>();
                    Assert.That(motion,Is.Not.Null);
                    Assert.That(motion.art.GetComponent<UnityEngine.UI.RawImage>().texture,Is.Not.Null);
                    if(artPosition.HasValue)Assert.That(motion.art.anchoredPosition,Is.EqualTo(artPosition.Value),"Loading illustration must remain still");
                    Assert.That(motion.art.localScale,Is.EqualTo(Vector3.one));
                    Assert.That(motion.art.localRotation,Is.EqualTo(Quaternion.identity));
                    Assert.That(motion.fill.sprite,Is.SameAs(UiKit.Art("HealthBarFill")));
                    if(firstFill.HasValue && motion.DisplayedProgress>firstFill.Value+.01f)observedMotion=true;
                    if(!firstFill.HasValue)firstFill=motion.DisplayedProgress;
                    if(!artPosition.HasValue)artPosition=motion.art.anchoredPosition;
                    if(!captured && observedMotion) { CaptureLoadingFrame(motion);captured=true; }
                }
                yield return null;
            }
            Assert.That(observedWorld && game.Ready, Is.True);
            Assert.That(observedMotion,Is.True,"Loading gauge must animate while combat remains paused");
            Assert.That(game.LoadingProgress, Is.EqualTo(1));
            Assert.That(game.paused, Is.False);
            Assert.That(game.GetComponentsInChildren<Canvas>().Any(c => c.name == "Preparing game" && c.isActiveAndEnabled), Is.False);
            Assert.That(PlayerBody().simulated, Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(game.Elapsed, Is.GreaterThan(0));
        }

        void CaptureLoadingFrame(DoodleLoadingMotion motion)
        {
            var canvas=motion.GetComponentInParent<Canvas>();
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            var camera=Camera.main;
            var target=new RenderTexture(720,1520,24);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            float previousAspect=camera.aspect,previousScale=canvas.scaleFactor;
            try {
                scaler.enabled=false;canvas.scaleFactor=1;
                camera.targetTexture=target;camera.aspect=720f/1520;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();motion.SendMessage("Update");Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;var image=new Texture2D(720,1520,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,720,1520),0,0);image.Apply();
                System.IO.Directory.CreateDirectory("artifacts/screenshots");
                System.IO.File.WriteAllBytes("artifacts/screenshots/loading-battle-motion.png",image.EncodeToPNG());
                Object.Destroy(image);
            } finally {
                camera.targetTexture=previousTarget;camera.aspect=previousAspect;RenderTexture.active=previousActive;
                canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;canvas.scaleFactor=previousScale;scaler.enabled=true;
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator PreparedVisualsServeNewEffectLabelsWithoutCreatingObjects()
        {
            game.TogglePause();
            int created = game.VisualObjectsCreated, reused = game.VisualObjectsReused;
            var rent = typeof(DoodleIdleGame).GetMethod("RentVisual", GrowthPrivate);
            var release = typeof(DoodleIdleGame).GetMethod("ReleaseVisual", GrowthPrivate);
            var sprite = (Sprite)typeof(DoodleIdleGame).GetField("disc", GrowthPrivate).GetValue(game);
            for (int i = 0; i < 16; i++) {
                var art = (SpriteRenderer)rent.Invoke(game, new object[] { "Prepared effect test " + i, sprite, Vector2.zero, Vector2.one, 500 });
                Assert.That(art.gameObject.activeSelf, Is.True);
                Assert.That(art.GetComponent<CircleCollider2D>().enabled, Is.False);
                release.Invoke(game, new object[] { art.gameObject });
            }
            Assert.That(game.VisualObjectsCreated, Is.EqualTo(created));
            Assert.That(game.VisualObjectsReused, Is.EqualTo(reused + 16));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstDenseHitUsesPreparedDamageNumbers()
        {
            game.TogglePause();
            var root = (Transform)typeof(DoodleIdleGame).GetField("damageCanvas", GrowthPrivate).GetValue(game);
            var show = typeof(DoodleIdleGame).GetMethod("ShowDamageNumber", GrowthPrivate);
            Assert.That(root.childCount, Is.EqualTo(128));
            for (int i = 0; i < 140; i++) show.Invoke(game, new object[] { Vector2.zero, (GameNumber)100, false });
            Assert.That(game.ActiveDamageNumbers, Is.EqualTo(128));
            Assert.That(root.childCount, Is.EqualTo(128), "The first mass hit must reuse the bounded UI pool");
            yield return null;
        }
    }
}
