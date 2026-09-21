using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator EquippedSlotsUseCenteredTranslucentLabelsAndToastsAnimateInsideFrames()
        {
            game.TogglePause(); Time.timeScale = 0;
            foreach (var pair in new[] { new[] { "Armor", "Equipment" }, new[] { "Skill", "Skills" }, new[] { "Companion", "Companions" } }) {
                var item = game.Ui.Items(pair[0]).First();
                item.discovered = item.equipped = true; item.level = 1; item.slot = 0;
                UiOpen(pair[1]); Canvas.ForceUpdateCanvases();
                var inventory = UiNode("Collection inventory");
                var card = UiNode("Slot: " + item.name, inventory);
                var marker = (RectTransform)card.Find("Equipped label");
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.GetComponentInChildren<Text>().text, Is.EqualTo("장착중"));
                Assert.That(marker.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(marker.anchorMin.y, Is.EqualTo(.5f));
                Assert.That(marker.GetComponent<Image>().color.a, Is.InRange(.5f, .9f));
                Assert.That(card.Find("Equipped check"), Is.Null);
                if (pair[0] == "Skill") Object.Destroy(CaptureFrame("equipped-center-labels-five-columns.png", 720, 1520));
            }
            game.Ui.Toast("장착이 완료되었습니다.");
            var panel = UiNode("Toast frame");
            var motion = panel.GetComponent<DoodleToastMotion>();
            var group = panel.GetComponent<CanvasGroup>();
            Assert.That(panel.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(group.alpha, Is.Zero); Assert.That(group.blocksRaycasts, Is.False);
            var tween = (Sequence)typeof(DoodleToastMotion).GetField("sequence", GrowthPrivate).GetValue(motion);
            tween.Goto(.3f, false);
            Assert.That(group.alpha, Is.EqualTo(1).Within(.001));
            Assert.That(Vector3.Distance(panel.localScale, Vector3.one), Is.LessThan(.001));
            Object.Destroy(CaptureFrame("framed-animated-toast.png", 720, 1520));
            game.Ui.Toast(""); Assert.That(group.alpha, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameInfoResetClearsOnlyGameKeysAndRestoresStarterCatalog()
        {
            game.TogglePause();
            bool hadSkip = PlayerPrefs.HasKey("DoodleUi.SkipSummonAnimations");
            int skip = PlayerPrefs.GetInt("DoodleUi.SkipSummonAnimations");
            const string sentinel = "DoodleResetTest.UnrelatedPreference";
            bool hadSentinel = PlayerPrefs.HasKey(sentinel); string sentinelValue = PlayerPrefs.GetString(sentinel);
            var probes = new List<GameObject>();
            try {
                game.Ui.Gold = 999999; game.Ui.Diamonds = 99999;
                GrowthLevels["attack"] = 100;
                game.Ui.Items("Skill").First().level = 100;
                game.Ui.Save(); PlayerPrefs.SetInt("DoodleUi.SkipSummonAnimations", 1);
                PlayerPrefs.SetString(sentinel, "keep");
#if UNITY_EDITOR
                var editorType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DoodleIdle.Editor.DoodleGameInfoWindow")).First(t => t != null);
                Assert.That(editorType.GetMethod("ResetGameInformation"), Is.Not.Null);
#endif
                DoodleGameData.ResetSavedProgress();
                foreach (string key in DoodleGameData.SaveKeys) Assert.That(PlayerPrefs.HasKey(key), Is.False, key);
                Assert.That(PlayerPrefs.GetString(sentinel), Is.EqualTo("keep"));
                var fresh = GrowthProbe(probes);
                Assert.That(fresh.StatLevel("attack"), Is.Zero);
                Assert.That(fresh.Items("Skill").First().level, Is.LessThan(100));
                Assert.That(PlayerPrefs.GetInt("DoodleUi.Diamonds", 1250), Is.EqualTo(1250));
                Assert.That(PlayerPrefs.GetString("DoodleUi.Gold", "125480"), Is.EqualTo("125480"));
            }
            finally {
                foreach (var probe in probes) Object.Destroy(probe);
                if (hadSkip) PlayerPrefs.SetInt("DoodleUi.SkipSummonAnimations", skip); else PlayerPrefs.DeleteKey("DoodleUi.SkipSummonAnimations");
                if (hadSentinel) PlayerPrefs.SetString(sentinel, sentinelValue); else PlayerPrefs.DeleteKey(sentinel);
            }
            yield return null;
        }
    }
}
