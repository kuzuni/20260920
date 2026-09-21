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
        public IEnumerator LoadoutOnlyEquippedSkillsCastAndUnequipCancelsQueuedShots()
        {
            var skills = game.Ui.Items("Skill");
            foreach (var item in skills) item.equipped = false;
            game.ResetGame(); yield return null;
            var bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(7, 0));
            game.basicSkillsEnabled = game.extraSkillsEnabled = game.summonSkillsEnabled = true;
            yield return PhysicsTicks(100);
            Assert.That(skills.Sum(x => game.SkillActivationCount(x.ability)), Is.Zero);
            Assert.That(game.StonesLaunched + game.ArrowsLaunched + game.VariantProjectilesLaunched, Is.Zero);
            Assert.That(game.BananasActive, Is.False);
            var arrow = skills.Single(x => x.ability == "Arrows");
            arrow.discovered = true; arrow.equipped = true; arrow.slot = 0;
            yield return PhysicsTicks(30);
            Assert.That(game.SkillActivationCount("Arrows"), Is.EqualTo(1));
            Assert.That(skills.Where(x => x != arrow).Sum(x => game.SkillActivationCount(x.ability)), Is.Zero);
            Assert.That(game.ArrowsLaunched, Is.InRange(1, 9));
            arrow.equipped = false; int shots = game.ArrowsLaunched;
            yield return PhysicsTicks(50);
            Assert.That(game.ArrowsLaunched, Is.EqualTo(shots), "An unequipped automatic volley must not launch remaining shots.");
        }

        [UnityTest]
        public IEnumerator SkillDebugButtonsCanCastAllUnownedSkillsWithoutChangingLoadoutOrWallet()
        {
            var skills = game.Ui.Items("Skill");
            foreach (var item in skills) { item.equipped = false; item.discovered = false; item.count = 0; }
            game.ResetGame(); yield return null; IsolateSummonTest();
            int diamonds = game.Ui.Diamonds; long gold = game.Ui.Gold;
            yield return PhysicsTicks(1);
            foreach (var item in skills) {
                Assert.That(game.DebugCastSkill(item.id), Is.True, item.id);
                Assert.That(game.SkillActivationCount(item.ability), Is.EqualTo(1), item.id);
            }
            Assert.That(skills.All(x => !x.equipped && !x.discovered && x.count == 0), Is.True);
            Assert.That(game.Ui.Diamonds, Is.EqualTo(diamonds)); Assert.That(game.Ui.Gold, Is.EqualTo(gold));
            Assert.That(game.DebugCastSkill("missing-skill"), Is.False);
            yield return PhysicsTicks(1);
            Assert.That(game.BananasActive, Is.True, "The explicit test bypass also supports the orbit skill.");
#if UNITY_EDITOR
            var editorType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DoodleIdle.Editor.DoodleSkillTestWindow")).FirstOrDefault(t => t != null);
            Assert.That(editorType, Is.Not.Null);
            var window = ScriptableObject.CreateInstance(editorType);
            editorType.GetMethod("RefreshSkills").Invoke(window, null);
            var rows = (System.Collections.IList)editorType.GetField("skills", GrowthPrivate).GetValue(window);
            Assert.That(rows.Count, Is.EqualTo(30)); Object.DestroyImmediate(window);
#endif
        }

        [UnityTest]
        public IEnumerator FullLoadoutsReplaceThroughTheirExistingSlotsAndEmptySlotsShowPlus()
        {
            game.TogglePause();
            foreach (string category in new[] { "Skill", "Companion" }) {
                var items = game.Ui.Items(category); int capacity = category == "Skill" ? 8 : 5;
                foreach (var item in items) { game.Ui.AddItem(item, 1); item.equipped = false; }
                UiOpen(category == "Skill" ? "Skills" : "Companions");
                var empty = UiNode("Equipped " + category).GetComponentsInChildren<Image>().Where(x => x.sprite == UiKit.Art("AddSlot")).ToArray();
                Assert.That(empty.Length, Is.EqualTo(capacity));
                for (int i = 0; i < capacity; i++) { items[i].equipped = true; items[i].slot = i; }
                UiOpen(category == "Skill" ? "Skills" : "Companions");
                var candidate = items[capacity];
                UiClick("Slot: " + candidate.name, UiNode("Collection inventory"));
                UiClick("장착", UiNode("Detail actions"));
                Assert.That(game.Ui.HasOverlay, Is.False, "Replacement must return to the original loadout.");
                Assert.That(UiNode("Equipped " + category).GetComponentsInChildren<Transform>().Count(x => x.name == "Replacement arrow"), Is.EqualTo(capacity));
                yield return new WaitForSecondsRealtime(2.5f); // Let the preceding equip toast finish.
                Object.Destroy(CaptureFrame("loadout-replace-" + category + ".png", 720, 1520));
                UiClick("Slot: " + items[1].name, UiNode("Equipped " + category));
                Assert.That(items[1].equipped, Is.False); Assert.That(candidate.equipped, Is.True);
                Assert.That(candidate.slot, Is.EqualTo(1));
                Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(x => x.name == "Replacement arrow"), Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompanionAndSkillArtHasDistinctCompleteFramesAndExportsBothPoses()
        {
            game.TogglePause();
            foreach (string category in new[] { "Skill", "Companion" }) {
                var items = game.Ui.Items(category);
                Assert.That(items.Select(x => x.icon).Distinct().Count(), Is.EqualTo(category == "Skill" ? 30 : 24));
                foreach (var item in items) Assert.That(UiKit.Art(item.icon), Is.Not.Null, item.id);
            }
            Assert.That(game.Ui.Items("Companion").Select(x => x.projectile).Distinct().Count(), Is.EqualTo(24));
            foreach (var renderer in game.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var display = new GameObject("Companion motion atlas");
            display.transform.SetParent(game.transform);
            var renderers = new SpriteRenderer[24];
            for (int i = 0; i < 24; i++) {
                var a = DoodleCollectionArt.CompanionFrame(i, 0); var b = DoodleCollectionArt.CompanionFrame(i, 1);
                Assert.That(a, Is.Not.SameAs(b)); Assert.That(a.rect, Is.Not.EqualTo(b.rect));
                Assert.That(a.bounds.size, Is.EqualTo(b.bounds.size)); Assert.That(a.pixelsPerUnit, Is.EqualTo(b.pixelsPerUnit));
                var node = new GameObject("Companion pose " + i); node.transform.SetParent(display.transform);
                node.transform.position = new Vector3((i % 6 - 2.5f) * 1.8f, (1.5f - i / 6) * 1.8f, 0);
                renderers[i] = node.AddComponent<SpriteRenderer>(); renderers[i].sprite = a;
                renderers[i].sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            }
            Camera.main.transform.position = new Vector3(0, 0, -10); Camera.main.orthographicSize = 4.4f;
            Camera.main.backgroundColor = new Color(.85f, .87f, .82f);
            for (int pose = 0; pose < 2; pose++) {
                for (int i = 0; i < 24; i++) { renderers[i].sprite = DoodleCollectionArt.CompanionFrame(i, pose); renderers[i].sharedMaterial.mainTexture = renderers[i].sprite.texture; }
                Object.Destroy(CaptureFrame("companions-all-pose-" + pose + ".png", 1440, 1000, false));
            }
            foreach (var renderer in renderers) Object.Destroy(renderer.sharedMaterial);
            Object.Destroy(display); yield return null;
            ExportCompanionProjectileSheets();
        }

        void ExportCompanionProjectileSheets()
        {
            var items = game.Ui.Items("Companion");
            for (int page = 0; page < 3; page++) {
                var poster = UiKit.Rect(UiRoot, "Companion projectile reference"); UiKit.Stretch(poster);
                poster.gameObject.AddComponent<Image>().color = new Color(.96f, .96f, .92f);
                void PlaceRect(RectTransform rect, Vector2 low, Vector2 high) {
                    rect.anchorMin = low; rect.anchorMax = high; rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                var heading = UiKit.Text(poster, "동료 탄환 · 기존 폭발 연출(제거 전)  " + (page + 1) + "/3", 44, TextAnchor.MiddleCenter);
                PlaceRect(heading.rectTransform, new Vector2(.02f, .91f), new Vector2(.98f, .99f));
                for (int n = 0; n < 8; n++) {
                    var item = items[page * 8 + n];
                    var cell = UiKit.Rect(poster, item.name);
                    float left = .015f + n % 4 * .247f, bottom = .03f + (1 - n / 4) * .43f;
                    PlaceRect(cell, new Vector2(left, bottom), new Vector2(left + .235f, bottom + .41f));
                    var title = UiKit.Text(cell, item.name + " · " + UiKit.GradeName(item.rarity), 38, TextAnchor.MiddleCenter);
                    PlaceRect(title.rectTransform, new Vector2(0, .84f), Vector2.one);
                    var projectile = UiKit.Icon(cell, item.projectile, 145).rectTransform;
                    projectile.anchorMin = projectile.anchorMax = new Vector2(.26f, .53f); projectile.anchoredPosition = Vector2.zero;
                    var label = UiKit.Text(cell, "탄환", 32, TextAnchor.MiddleCenter);
                    PlaceRect(label.rectTransform, new Vector2(0, .24f), new Vector2(.49f, .35f));
                    var oldLabel = UiKit.Text(cell, item.explosionRadius > 0 ? "기존 퍼짐(삭제)" : "효과 없음", 30, TextAnchor.MiddleCenter);
                    PlaceRect(oldLabel.rectTransform, new Vector2(.5f, .24f), new Vector2(1, .35f));
                    if (item.explosionRadius > 0) {
                        var oldSprite = UiKit.Art(item.projectile);
                        if (item.icon == "CompanionMon_12")
                            oldSprite = (Sprite)typeof(DoodleCollectionArt).GetMethod("Cell", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                                .Invoke(null, new object[] { "CompanionAttacksB", 4, 4, 2 });
                        for (int k = 0; k < 8; k++) {
                            var fragment = UiKit.Icon(cell, item.projectile, 52); fragment.sprite = oldSprite;
                            fragment.color = new Color(1, 1, 1, .7f);
                            fragment.rectTransform.anchorMin = fragment.rectTransform.anchorMax = new Vector2(.75f, .53f);
                            float angle = k * Mathf.PI / 4;
                            fragment.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 72;
                            fragment.transform.localRotation = Quaternion.Euler(0, 0, k * 45);
                        }
                    }
                    string motion = item.trajectory == "Arc" ? "포물선" : item.trajectory == "Lightning" ? "번개 타격" : "직선";
                    var pattern = UiKit.Text(cell, motion + " · " + item.volleyCount + "발" + (item.volleyCount > 1 ? item.volleyGap > 0 ? " 순차" : " 동시" : ""), 30, TextAnchor.MiddleCenter);
                    PlaceRect(pattern.rectTransform, new Vector2(0, .02f), new Vector2(1, .17f));
                }
                Object.Destroy(CaptureFrame("companion-projectiles-and-former-bursts-" + (page + 1) + ".png", 1440, 1000));
                poster.gameObject.SetActive(false); Object.Destroy(poster.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator AllEnemyPairsExportAtEqualScaleFacingRight()
        {
            game.TogglePause();
            foreach (var renderer in game.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var display = new GameObject("Enemy motion atlas"); display.transform.SetParent(game.transform);
            var renderers = new SpriteRenderer[30]; var pairs = new Sprite[30][];
            var loader = typeof(DoodleIdleGame).GetMethod("LoadThemeFrames", GrowthPrivate);
            var orientation = (System.Collections.Generic.Dictionary<Sprite, bool>)typeof(DoodleIdleGame).GetField("sourceFrameFacesLeft", GrowthPrivate).GetValue(game);
            for (int theme = 0; theme < 10; theme++) {
                var frames = (Sprite[][])loader.Invoke(game, new object[] { theme });
                for (int kind = 0; kind < 3; kind++) {
                    int i = theme * 3 + kind; pairs[i] = frames[kind];
                    Assert.That(pairs[i][0].bounds.size, Is.EqualTo(pairs[i][1].bounds.size));
                    var node = new GameObject("Enemy pose " + i); node.transform.SetParent(display.transform);
                    node.transform.position = new Vector3((i % 6 - 2.5f) * 1.8f, (2f - i / 6) * 1.8f, 0);
                    renderers[i] = node.AddComponent<SpriteRenderer>();
                    renderers[i].sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                }
            }
            Camera.main.transform.position = new Vector3(0, 0, -10); Camera.main.orthographicSize = 5.1f;
            Camera.main.backgroundColor = new Color(.85f, .87f, .82f);
            for (int pose = 0; pose < 2; pose++) {
                for (int i = 0; i < 30; i++) {
                    renderers[i].sprite = pairs[i][pose]; renderers[i].flipX = orientation[pairs[i][pose]];
                    renderers[i].sharedMaterial.mainTexture = pairs[i][pose].texture;
                }
                Object.Destroy(CaptureFrame("enemies-all-right-pose-" + pose + ".png", 1440, 1200, false));
            }
            foreach (var renderer in renderers) Object.Destroy(renderer.sharedMaterial);
            Object.Destroy(display); yield return null;
        }
    }
}
