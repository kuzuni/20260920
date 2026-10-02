using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator AutomaticLoginShowsOnlyLoadingUntilFailureAndHandsCoverToGame()
        {
            game.paused = true;
            bool ownsSession = !DoodleBackendSession.Instance;
            var session = DoodleBackendSession.Get();
            GameObject root = null, receiver = null;
            try {
                foreach (bool success in new[] { false, true }) {
                    root = new GameObject("Isolated login presentation");
                    var login = root.AddComponent<DoodleLogin>(); login.enabled = false;
                    typeof(DoodleLogin).GetField("session", GrowthPrivate).SetValue(login, session);
                    var completion = new TaskCompletionSource<bool>();
                    var run = (Task)typeof(DoodleLogin).GetMethod("RunAutoLogin", GrowthPrivate).Invoke(login,
                        new object[] { (System.Func<Task<bool>>)(() => completion.Task) });
                    Assert.That(root.GetComponentsInChildren<Button>().Length, Is.Zero);
                    var cover = (DoodleLoadingScreen)typeof(DoodleLogin).GetField("startupLoading", GrowthPrivate).GetValue(login);
                    Assert.That(cover.Canvas.isActiveAndEnabled, Is.True);
                    yield return null;
                    Assert.That(root.GetComponentsInChildren<Button>().Length, Is.Zero, "No one-frame login flash while token response is pending.");
                    completion.SetResult(success);
                    while (!run.IsCompleted) yield return null;
                    Assert.That(run.IsFaulted, Is.False);
                    if (success) {
                        Assert.That(root.GetComponentsInChildren<Button>().Length, Is.Zero);
                        receiver = new GameObject("Game loading receiver");
                        Assert.That(DoodleLoadingScreen.TakeForGame(receiver.transform), Is.SameAs(cover));
                        Assert.That(cover.gameObject.activeInHierarchy, Is.True);
                    } else Assert.That(root.GetComponentsInChildren<Button>().Length, Is.GreaterThan(0));
                    Object.Destroy(root); root = null;
                    if (receiver) { Object.Destroy(receiver); receiver = null; }
                    yield return null;
                }
            } finally {
                if (root) Object.Destroy(root); if (receiver) Object.Destroy(receiver);
                if (ownsSession && session) Object.Destroy(session.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator StatUpgradeKeepsPortraitsAndAnimatesPowerWithPooledCelebration()
        {
            game.paused = true; var ui = game.Ui; ui.Gold = 10000000;
            ui.ShowPage("Stats"); yield return null; yield return null; yield return null;
            var portraits = ui.Canvas.GetComponentsInChildren<DoodleIdlePortrait>();
            var stats = portraits.Single(p => p.view == DoodleIdlePortrait.View.Stats);
            var profile = portraits.Single(p => p.view == DoodleIdlePortrait.View.Profile);
            var rig = stats.PreviewRig; var photo = profile.PreviewCamera.targetTexture;
            var counter = stats.transform.parent.GetComponentInChildren<DoodlePowerCounter>();
            var before = ui.PowerAmount;
            var row = ui.Canvas.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Stat attack");
            var button = row.GetComponentInChildren<Button>();
            button.onClick.Invoke();
            Assert.That(stats.PreviewRig, Is.SameAs(rig));
            Assert.That(profile.PreviewCamera.targetTexture, Is.SameAs(photo));
            Assert.That(stats.Celebrating && profile.Celebrating, Is.True);
            Assert.That(counter.Target, Is.GreaterThan(before));
            Assert.That(counter.Displayed, Is.EqualTo(before));
            var fx = profile.PreviewCamera.transform.parent.GetComponentInChildren<DoodleUpgradeEffect>(true);
            Assert.That(fx, Is.Not.Null); Assert.That(fx.gameObject.activeSelf, Is.True);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(counter.Displayed, Is.GreaterThan(before));
            Assert.That(counter.Displayed, Is.LessThan(counter.Target));
            Assert.That(fx.glow.particleCount, Is.GreaterThan(0));
            Object.Destroy(CaptureFrame("stat-upgrade-celebration.png", 720, 1520));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(counter.Displayed, Is.EqualTo(counter.Target));
            ui.UpgradeStat("attack", 1);
            Assert.That(profile.PreviewCamera.transform.parent.GetComponentInChildren<DoodleUpgradeEffect>(true), Is.SameAs(fx));
            Assert.That(stats.PreviewRig, Is.SameAs(rig));
        }

        [UnityTest]
        public IEnumerator PortraitGlintsStayVisibleWithOpenEyesAtDistantPreviewCoordinates()
        {
            game.paused = true; game.Ui.ShowPage("Stats");
            yield return null; yield return null; yield return null;
            var portrait = game.Ui.Canvas.GetComponentsInChildren<DoodleIdlePortrait>().Single(p => p.view == DoodleIdlePortrait.View.Stats);
            var face = portrait.PreviewRig.face; face.blinking = false; face.ResetExpression();
            foreach (float x in new[] { 10000f, 60000f, 100000f }) {
                portrait.PreviewCamera.transform.parent.position = new Vector3(x, 10000, 0);
                for (int frame = 0; frame < 30; frame++) {
                    yield return null;
                    Assert.That(face.leftEye.highlight.enabled && face.rightEye.highlight.enabled, Is.True, "Open-eye glints cannot flicker from world-coordinate precision.");
                }
            }
        }
    }
}
