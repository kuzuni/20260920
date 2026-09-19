using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator OrbitGunUsesMuzzleBulletsAndBothOrbitSkillsExpireAndReturn()
        {
            var bodies = IsolateSummonTest();
            for (int i = 0; i < bodies.Length; i++) Place(bodies[i], new Vector2(15, 15 + i));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.OrbitGun);
            var gun = NamedArt("Orbiting automatic gun").Single();
            var muzzle = gun.transform.Find("Orbit gun muzzle");
            Assert.That(gun.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
            var hp = bodies[0].GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Enemy HP fill");
            float initialHp = hp.transform.localScale.x;
            Place(bodies[0], gun.transform.position);
            yield return PhysicsTicks(3);
            Assert.That(hp.transform.localScale.x, Is.EqualTo(initialHp), "Touching the orbiting gun must not damage an enemy.");
            Assert.That(game.OrbitBulletsLaunched, Is.Zero);
            Place(bodies[0], new Vector2(6, 0));
            var launches = new List<float>();
            game.OrbitBulletLaunched += (time, position, direction) => {
                launches.Add(time);
                Assert.That(Vector2.Distance(position, muzzle.position), Is.LessThan(.001f));
                Assert.That(direction.magnitude, Is.EqualTo(1).Within(.001f));
            };
            yield return PhysicsTicks(3); // Sample the flash/recoil immediately after the first shot.
            Assert.That(game.OrbitBulletsLaunched, Is.EqualTo(1));
            Assert.That(NamedArt("Orbit gun muzzle flash").Length, Is.GreaterThan(0));
            Assert.That(DOTween.IsTweening(gun.transform), Is.True);
            Assert.That(Vector3.Distance(gun.transform.localScale, new Vector3(1.3f, 1.3f, 1)), Is.GreaterThan(.005f));
            Camera.main.orthographicSize = 5;
            Object.Destroy(CaptureFrame("23-orbit-gun-muzzle.png", 1440, 900, false));
            game.TogglePause(); var positionBefore = gun.transform.position; var scaleBefore = gun.transform.localScale;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(gun.transform.position, Is.EqualTo(positionBefore));
            Assert.That(gun.transform.localScale, Is.EqualTo(scaleBefore));
            game.TogglePause(); launches.Clear(); foreach (var body in EnemyBodies()) body.simulated = false;
            yield return PhysicsTicks(35);
            Assert.That(game.OrbitBulletsLaunched, Is.GreaterThanOrEqualTo(7));
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.OrbitGun), Is.GreaterThan(0));
            for (int i = 1; i < launches.Count; i++) Assert.That(launches[i] - launches[i - 1], Is.InRange(.079f, .101f));
            Assert.That(Vector2.Distance(gun.transform.position, PlayerBody().position), Is.EqualTo(2.5f).Within(.01f));
            yield return PhysicsTicks(365); yield return null;
            Assert.That(game.OrbitGunActive, Is.False);
            Assert.That(game.BananasActive, Is.False);
            Assert.That(NamedArt("Orbiting automatic gun").Length, Is.Zero);
            Assert.That(game.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Orbit banana ")), Is.Zero);
            yield return PhysicsTicks(40);
            int bulletsAfterExpiry = game.OrbitBulletsLaunched;
            Assert.That(NamedArt("OrbitGun moving skill").Length, Is.Zero);
            yield return PhysicsTicks(100);
            Assert.That(game.OrbitBulletsLaunched, Is.EqualTo(bulletsAfterExpiry));
            Assert.That(game.BananasActive, Is.False);
            yield return PhysicsTicks(60);
            Assert.That(game.BananasActive, Is.True);
            Assert.That(game.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Orbit banana ")), Is.EqualTo(5));
            game.ResetGame(); yield return null; IsolateSummonTest();
            Assert.That(game.OrbitBulletsLaunched, Is.Zero);
            Assert.That(game.OrbitGunActive, Is.False);
            Assert.That(game.BananasActive, Is.True);
        }

        [UnityTest]
        public IEnumerator ScreenDragMovesPlayerWithMouseAndTouchAndHonorsUiAndSixtyFps()
        {
            IsolateSummonTest(); game.autoPlay = true; game.moveSpeed = 3;
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            var originalBackground = InputSystem.settings.backgroundBehavior;
            var originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            // CI has no user-focused Game view. Route synthetic devices into the game explicitly.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            Touchscreen touch = null;
            try
            {
                Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center, buttons = 1 });
                yield return null; yield return null;
                Object.Destroy(CaptureFrame("24-joystick-start.png", 1440, 900));
                Assert.That(game.JoystickActive, Is.True, "Synthetic mouse enabled=" + mouse.enabled + ", pressed=" + mouse.leftButton.isPressed + ", current=" + Pointer.current);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center + Vector2.right * 140, buttons = 1 });
                yield return null; yield return PhysicsTicks(4);
                Assert.That(PlayerBody().linearVelocity.x, Is.GreaterThan(2.5f));
                Assert.That(Mathf.Abs(PlayerBody().linearVelocity.y), Is.LessThan(.1f));
                Object.Destroy(CaptureFrame("24-touch-joystick.png", 1440, 900));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                yield return null; yield return null;
                Assert.That(game.JoystickActive, Is.False);
                Assert.That(game.autoPlay, Is.True);

                var pauseButton = game.GetComponentsInChildren<Button>().First(b => b.name == "일시정지");
                Vector2 button = RectTransformUtility.WorldToScreenPoint(null, pauseButton.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = button, buttons = 1 });
                yield return null; yield return null;
                Assert.That(game.JoystickActive, Is.False, "HUD clicks must not start movement.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = button });
                yield return null; yield return null;
                if (game.paused) game.TogglePause();

                touch = InputSystem.AddDevice<Touchscreen>();
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = center });
                yield return null; yield return null;
                Assert.That(game.JoystickActive, Is.True);
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = center + Vector2.left * 140 });
                yield return null; yield return PhysicsTicks(4);
                Assert.That(PlayerBody().linearVelocity.x, Is.LessThan(-2.5f));
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = center });
                yield return null; yield return null;
                Assert.That(game.JoystickActive, Is.False);
            }
            finally
            {
                if (touch != null) InputSystem.RemoveDevice(touch);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = originalBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
            }
        }
    }
}
