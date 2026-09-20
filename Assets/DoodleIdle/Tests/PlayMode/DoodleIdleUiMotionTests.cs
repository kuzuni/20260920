using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator UiCameraCyclesExactSizesAndPersistsMode()
        {
            var camera = (Camera)typeof(DoodleIdleGame).GetField("gameCamera", GrowthPrivate).GetValue(game);
            Assert.That(camera.orthographicSize, Is.EqualTo(8.5f));
            var cameraRect = (RectTransform)UiNode("Camera mode");
            Assert.That(cameraRect.GetComponent<DoodleButtonMotion>(), Is.Not.Null);
            foreach (var expected in new[] { 11f, 14.5f, 8.5f })
            {
                UiClick("Camera mode");
                Assert.That(camera.orthographicSize, Is.EqualTo(expected));
                Assert.That(PlayerPrefs.GetInt("DoodleUi.CameraMode"), Is.EqualTo(game.Ui.CameraMode));
                Assert.That(cameraRect.GetComponentInChildren<Text>().text, Does.Contain(game.Ui.CameraMode.ToString()));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiPopupAndButtonTweensContinueWithTimeScaleZero()
        {
            Time.timeScale = 0;
            game.Ui.ShowPage("Stats");
            var motion = UiRoot.GetComponentsInChildren<DoodlePopupMotion>().Single();
            Assert.That(motion.transform.localScale.x, Is.LessThan(1));
            Assert.That(motion.GetComponent<CanvasGroup>().alpha, Is.Zero);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(motion.transform.localScale.x, Is.EqualTo(1).Within(.001));
            Assert.That(motion.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1).Within(.001));
            var button = UiNode("×100").GetComponent<Button>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(button.transform.localScale.x, Is.LessThan(.97f));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(button.transform.localScale.x, Is.EqualTo(1).Within(.001));
            game.Ui.ClosePage();
            Assert.That(game.Ui.ActivePage, Is.Null);
            Assert.That(motion.IsClosing, Is.True);
            Assert.That(motion.transform.parent.GetComponent<CanvasGroup>().interactable, Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(motion == null, Is.True, "Exit animation must remove the visual tree.");
            game.Ui.ShowRewards("획득 보상", new System.Collections.Generic.List<UiReward> { new UiReward { icon="Diamond", amount=500 } });
            Assert.That(UiNode("Floating rewards").GetComponent<DoodlePopupMotion>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator UiHeldStatAndRelicUpgradeSurviveRebuildAndStopOnReleaseOrExit()
        {
            game.TogglePause();
            var background = InputSystem.settings.backgroundBehavior;
            var editor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                game.Ui.Gold = 100000000;
                UiOpen("Stats");
                var button = UiNode("Stat attack").GetComponentInChildren<Button>();
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                int initial = game.Ui.AttackStatLevel;
                InputSystem.QueueStateEvent(mouse, new MouseState { position=point, buttons=1 });
                yield return new WaitForSecondsRealtime(1.05f);
                Assert.That(game.Ui.AttackStatLevel, Is.GreaterThanOrEqualTo(initial+3), "Holding must survive rebuilding the upgraded row.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position=point });
                yield return null;
                int released = game.Ui.AttackStatLevel;
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(game.Ui.AttackStatLevel, Is.EqualTo(released));

                var relic = game.Ui.Items("Relic")[0];
                game.Ui.AddItem(relic, 20);
                UiOpen("Relics");
                button = UiNode("Relic "+relic.id).GetComponentInChildren<Button>();
                point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                int copies = relic.count; long gold = game.Ui.Gold;
                InputSystem.QueueStateEvent(mouse, new MouseState { position=point, buttons=1 });
                yield return new WaitForSecondsRealtime(1.05f);
                Assert.That(relic.count, Is.LessThanOrEqualTo(copies-3));
                Assert.That(game.Ui.Gold, Is.EqualTo(gold));
                InputSystem.QueueStateEvent(mouse, new MouseState { position=Vector2.zero, buttons=1 });
                yield return null;
                copies = relic.count;
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(relic.count, Is.EqualTo(copies), "Dragging away cancels the held purchase.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position=Vector2.zero });
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editor;
            }
        }
    }
}
