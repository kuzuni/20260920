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
                for (int attempt = 0; attempt < 2; attempt++) {
                    var previousScene = testScene;
#if UNITY_EDITOR
                    var window = (UnityEditor.EditorWindow)ScriptableObject.CreateInstance(editorType);
                    window.Show();
                    try { editorType.GetMethod("ResetGameInformation").Invoke(window,null); }
                    finally { window.Close(); }
#else
                    DoodleGameData.ResetAndRestart(game);
#endif
                    float deadline = Time.realtimeSinceStartup + 15;
                    while (previousScene.isLoaded && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(previousScene.isLoaded, Is.False, "The reset button must replace the running scene.");
                    testScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/DoodleIdle/DoodleIdle.unity");
                    Assert.That(testScene.IsValid() && testScene.isLoaded, Is.True);
                    game = testScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DoodleIdleGame>()).Single();
                    while (!game.Ready && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(game.Ready, Is.True);
                    game.TogglePause();
                    Assert.That(game.Ui.Gold, Is.Zero); Assert.That(game.Ui.Diamonds, Is.Zero);
                    Assert.That(game.Ui.MainStage, Is.Zero);
                    Assert.That(game.Ui.CombatDamageMultiplier, Is.EqualTo(1).Within(.00001f));
                    foreach (var stat in GrowthTuning.stats) Assert.That(game.Ui.StatLevel(stat.id), Is.Zero, stat.id);
                    foreach (string category in new[] { "Armor", "Club", "Skill", "Companion", "Relic" }) {
                        Assert.That(game.Ui.Items(category).All(x => !x.discovered && !x.equipped && x.count == 0 && x.level == 0), Is.True, category);
                        Assert.That(game.Ui.SummonLevel(category), Is.EqualTo(category == "Relic" ? 0 : 1));
                    }
                    Assert.That(game.Ui.EquippedSkills, Is.Empty); Assert.That(game.Ui.EquippedCompanions, Is.Empty);
                    Assert.That(PlayerPrefs.GetString(sentinel), Is.EqualTo("keep"));
                    Assert.That(PlayerPrefs.GetInt("DoodleUi.SkipSummonAnimations",0), Is.Zero);
                    if (attempt == 0) {
                        // Earned progress still restores normally; a second real reset clears it too.
                        game.Ui.Gold = 321; game.Ui.Diamonds = 123;
                        var earned = game.Ui.Items("Skill").First(); game.Ui.AddItem(earned,2); earned.equipped = true;
                        game.Ui.Save();
                        var restored = GrowthProbe(probes);
                        Assert.That(restored.Items("Skill").First().count, Is.EqualTo(2));
                        Assert.That(restored.EquippedSkills.Count, Is.EqualTo(1));
                    }
                }
                game.Ui.Save();
                Assert.That(PlayerPrefs.GetInt("DoodleUi.Diamonds",-1), Is.Zero);
                Assert.That(PlayerPrefs.GetString("DoodleUi.Gold","missing"), Is.EqualTo("0"));
                var emptyRestored = GrowthProbe(probes);
                Assert.That(emptyRestored.Items("Skill").All(x => !x.discovered && !x.equipped && x.count == 0 && x.level == 0), Is.True);
                UiOpen("Skills");
                Object.Destroy(CaptureFrame("game-info-reset-empty-skills.png",720,1520));
                UiOpen("Companions");
                Object.Destroy(CaptureFrame("game-info-reset-empty-companions.png",720,1520));
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
