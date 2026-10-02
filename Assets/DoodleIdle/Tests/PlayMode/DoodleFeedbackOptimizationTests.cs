using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator DamageNumbersHoldLargeThenShrinkAndRiseWithReusedTween()
        {
            game.paused = true;
            var show = typeof(DoodleIdleGame).GetMethod("ShowDamageNumber", GrowthPrivate);
            var tick = typeof(DoodleIdleGame).GetMethod("TickDamageNumbers", GrowthPrivate);
            typeof(DoodleIdleGame).GetMethod("ClearDamageNumbers", GrowthPrivate).Invoke(game, null);
            show.Invoke(game, new object[] { Vector2.zero, (GameNumber)123, false });
            var text = game.GetComponentsInChildren<Text>().Single(t => t.name == "Enemy damage number");
            var numbers = (IList)typeof(DoodleIdleGame).GetField("damageNumbers", GrowthPrivate).GetValue(game);
            var number = numbers[0]; var tween = number.GetType().GetField("popTween").GetValue(number);
            Vector3 origin = text.rectTransform.localPosition;
            Assert.That(text.rectTransform.localScale.x, Is.EqualTo(2.3f));
            tick.Invoke(game, new object[] { .1f });
            Assert.That(text.rectTransform.localPosition, Is.EqualTo(origin));
            Assert.That(text.rectTransform.localScale.x, Is.EqualTo(2.3f));
            tick.Invoke(game, new object[] { .05f });
            Assert.That(text.rectTransform.localScale.x, Is.InRange(1.01f, 2.29f));
            Assert.That(text.rectTransform.localPosition.y, Is.GreaterThan(origin.y));
            tick.Invoke(game, new object[] { .05f });
            Assert.That(text.rectTransform.localScale.x, Is.EqualTo(1).Within(.001));
            tick.Invoke(game, new object[] { .56f });
            show.Invoke(game, new object[] { Vector2.right, (GameNumber)456, true });
            Assert.That(numbers[0], Is.SameAs(number));
            Assert.That(number.GetType().GetField("popTween").GetValue(number), Is.SameAs(tween));
            Assert.That(text.rectTransform.localScale.x, Is.EqualTo(2.3f));
            origin = text.rectTransform.localPosition;
            tick.Invoke(game, new object[] { .09f });
            Assert.That(text.rectTransform.localPosition, Is.EqualTo(origin));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageNumbersFadeWithoutRebuildingGlyphsAndResetWhenReused()
        {
            game.TogglePause();
            var show = typeof(DoodleIdleGame).GetMethod("ShowDamageNumber", GrowthPrivate);
            var tick = typeof(DoodleIdleGame).GetMethod("TickDamageNumbers", GrowthPrivate);
            typeof(DoodleIdleGame).GetMethod("ClearDamageNumbers", GrowthPrivate).Invoke(game, null);
            show.Invoke(game, new object[] { Vector2.zero, (GameNumber)123, false });
            yield return null; Canvas.ForceUpdateCanvases();
            var text = game.GetComponentsInChildren<Text>().Single(t => t.name == "Enemy damage number");
            int rebuilds = 0; UnityEngine.Events.UnityAction changed = () => rebuilds++;
            text.RegisterDirtyVerticesCallback(changed);
            tick.Invoke(game, new object[] { .55f });
            Assert.That(text.canvasRenderer.GetAlpha(), Is.EqualTo(.8f).Within(.0001f));
            Assert.That(rebuilds, Is.Zero, "Fading must reuse the Text/Outline mesh");
            Assert.That(text.rectTransform.localPosition.y, Is.EqualTo((1.05f + .45f * 1.05f) * 100).Within(.001f));
            text.UnregisterDirtyVerticesCallback(changed);
            tick.Invoke(game, new object[] { .21f });
            Assert.That(game.ActiveDamageNumbers, Is.Zero);
            show.Invoke(game, new object[] { Vector2.one, (GameNumber)456, true });
            Assert.That(text.gameObject.activeSelf, Is.True); Assert.That(text.text, Is.EqualTo("456"));
            Assert.That(text.canvasRenderer.GetAlpha(), Is.EqualTo(1));
            Assert.That(text.color.r, Is.EqualTo(.62f).Within(.0001f));
            for (int i = 0; i < 140; i++) show.Invoke(game, new object[] { Vector2.zero, (GameNumber)(i + 1), false });
            Assert.That(game.ActiveDamageNumbers, Is.EqualTo(128));
            Assert.That(game.GetComponentsInChildren<Text>(true).Count(t => t.name == "Enemy damage number" || t.name == "Player damage number"), Is.EqualTo(128));
        }

        [UnityTest]
        public IEnumerator HealthBarsOnlyMoveOnRealChangesAndKeepLiveInspectorOffsets()
        {
            game.TogglePause();
            var actor = ((IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game))[0];
            var type = actor.GetType();
            var refresh = typeof(DoodleIdleGame).GetMethod("RefreshHealthBar", GrowthPrivate);
            type.GetField("hp").SetValue(actor, (GameNumber)25); type.GetField("maxHp").SetValue(actor, (GameNumber)100);
            refresh.Invoke(game, new[] { actor });
            var fill = (SpriteRenderer)type.GetField("healthFill").GetValue(actor);
            var back = (SpriteRenderer)type.GetField("healthBack").GetValue(actor);
            fill.transform.hasChanged = false; back.transform.hasChanged = false;
            for (int i = 0; i < 10; i++) refresh.Invoke(game, new[] { actor });
            Assert.That(fill.transform.hasChanged, Is.False); Assert.That(back.transform.hasChanged, Is.False);
            Assert.That(fill.transform.localScale.x, Is.EqualTo(.225f).Within(.0001f));
            game.enemyHealthBarOffset = new Vector2(.4f, 3.1f); refresh.Invoke(game, new[] { actor });
            Assert.That(back.transform.localPosition, Is.EqualTo(new Vector3(.4f, 3.1f, 0)));
            Assert.That(fill.transform.localPosition.x, Is.EqualTo(.4f - .45f * .75f).Within(.0001f));
            type.GetField("hp").SetValue(actor, (GameNumber)50); refresh.Invoke(game, new[] { actor });
            Assert.That(fill.transform.localScale.x, Is.EqualTo(.45f).Within(.0001f));
            Assert.That(fill.color, Is.EqualTo(Color.Lerp(new Color(1,.45f,.45f), Color.white, .5f)));
            type.GetField("hp").SetValue(actor, (GameNumber)100); refresh.Invoke(game, new[] { actor });
            Assert.That(fill.enabled, Is.False); Assert.That(back.enabled, Is.False);
            yield return null;
        }
    }
}
