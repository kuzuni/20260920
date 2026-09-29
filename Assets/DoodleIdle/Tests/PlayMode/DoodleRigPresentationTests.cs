using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public sealed class DoodleRigPresentationTests
    {
        [Test]
        public void AllAppearancesPreservePrefabPartAndGroupSorting()
        {
            foreach (var group in DoodleCharacterCatalog.Current.entries.GroupBy(e => e.prefab))
            {
                var authored = group.Key;
                Assert.That(authored.GetComponent<SortingGroup>(), Is.Not.Null, authored.name);
                Assert.That(authored.GetComponent<SortingGroup>().enabled, Is.True);
                var root = new GameObject("Rig sorting test", typeof(SpriteRenderer), typeof(DoodleRigVisual));
                try
                {
                    var proxy = root.GetComponent<SpriteRenderer>();
                    var visual = root.GetComponent<DoodleRigVisual>();
                    foreach (var entry in group)
                    {
                        visual.Configure(entry);
                        proxy.flipX = false; visual.Sync();
                        var rightPosition = visual.Rig.transform.localPosition;
                        var sample = new Vector3(.75f, 2, 0);
                        var rightPoint = root.transform.InverseTransformPoint(visual.Rig.transform.TransformPoint(sample));
                        Assert.That(rightPosition.x, Is.Zero, entry.id + " must use the authored X pivot");
                        foreach (var part in visual.Rig.partRenderers)
                        {
                            var original = authored.partRenderers.Single(p => p.name == part.name);
                            Assert.That(part.sortingOrder, Is.EqualTo(original.sortingOrder), entry.id + "/" + part.name);
                            Assert.That(part.sortingLayerID, Is.EqualTo(original.sortingLayerID));
                        }
                        proxy.sortingOrder = 137; proxy.flipX = true; visual.Sync(); visual.Sync();
                        Assert.That(visual.Rig.transform.localPosition, Is.EqualTo(rightPosition), entry.id + " must not shift when turning");
                        var leftPoint = root.transform.InverseTransformPoint(visual.Rig.transform.TransformPoint(sample));
                        Assert.That(leftPoint.x, Is.EqualTo(-rightPoint.x).Within(.0001f));
                        Assert.That(leftPoint.y, Is.EqualTo(rightPoint.y).Within(.0001f));
                        Assert.That(visual.Rig.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(137 + authored.GetComponent<SortingGroup>().sortingOrder));
                        Assert.That(visual.Rig.GetComponent<SortingGroup>().sortingLayerID, Is.EqualTo(authored.GetComponent<SortingGroup>().sortingLayerID));
                        float multiplier = entry.group == "Companions" ? 1 : 2;
                        Assert.That(Mathf.Abs(visual.Rig.transform.localScale.x), Is.EqualTo(multiplier / entry.extent).Within(.0001f));
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [UnityTest]
        public IEnumerator BasicWeaponSkinShowsEquippedClubAndActorHasNoScriptBobbing()
        {
            Assert.That(DoodlePrefs.HasAccount, Is.False);
            DoodlePrefs.UseAccount("rig-presentation-test-" + System.Guid.NewGuid());
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/DoodleIdle/DoodleIdle.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
#else
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("DoodleIdle", UnityEngine.SceneManagement.LoadSceneMode.Additive);
#endif
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("DoodleIdle");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try
            {
                yield return null; yield return null;
                var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DoodleIdleGame>()).Single();
                Assert.That(game.Ready, Is.True);
                game.summonSkillsEnabled = false; game.companionsEnabled = false;
                var visual = game.GetComponentsInChildren<DoodleRigVisual>().Single(v => v.Entry.group == "Player");
                var basic = game.Ui.Skins("Weapon").Single(s => s.icon == "Club");
                game.Ui.EquipSkin(basic.id);
                var items = game.Ui.Items("Club");
                foreach (var item in items) item.equipped = false;
                foreach (var item in new[] { items[0], items[items.Count - 1] })
                {
                    foreach (var other in items) other.equipped = other == item;
                    yield return null;
                    Assert.That(game.Ui.EquippedWeaponIcon, Is.EqualTo(item.icon));
                    Assert.That(visual.Rig.weaponRenderer.sprite.name, Is.EqualTo("Skin " + item.icon));
                    Assert.That(visual.Rig.weaponRenderer.enabled, Is.True);
                }
                var custom = game.Ui.Skins("Weapon").First(s => s.icon != "Club");
                custom.owned = true; Assert.That(game.Ui.EquipSkin(custom.id), Is.True);
                yield return null;
                Assert.That(game.Ui.EquippedWeaponIcon, Is.EqualTo(custom.icon));
                var customSprite = visual.Rig.weaponRenderer.sprite;
                Assert.That(game.Ui.EquipSkin(basic.id), Is.True);
                yield return null;
                Assert.That(game.Ui.EquippedWeaponIcon, Is.EqualTo(items[items.Count - 1].icon));
                Assert.That(visual.Rig.weaponRenderer.sprite, Is.Not.SameAs(customSprite));
                var actors = game.GetComponentsInChildren<DoodleRigVisual>().Where(v => v.Entry.group != "Companions").ToArray();
                foreach (var actor in actors) actor.Rig.animator.enabled = false;
                var positions = actors.Select(a => a.transform.localPosition).ToArray();
                var rotations = actors.Select(a => a.transform.localRotation).ToArray();
                yield return new WaitForSeconds(.3f);
                for (int i = 0; i < actors.Length; i++)
                {
                    Assert.That(actors[i].transform.localPosition, Is.EqualTo(positions[i]));
                    Assert.That(actors[i].transform.localRotation, Is.EqualTo(rotations[i]));
                }
                game.TogglePause();
                Capture(game, visual);
            }
            finally
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
                foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
                DoodlePrefs.DeleteAccountCache();
            }
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        }

        static void Capture(DoodleIdleGame game, DoodleRigVisual player)
        {
            var camera = Camera.main;
            var target = new RenderTexture(720, 720, 24);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            try
            {
                game.Ui.Canvas.enabled = false;
                camera.transform.position = player.transform.position + new Vector3(0, -.2f, -10);
                camera.orthographicSize = 2; camera.aspect = 1; camera.targetTexture = target;
                for (int i = 0; i < 2; i++)
                    RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(720, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 720, 720), 0, 0); image.Apply();
                string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../artifacts/screenshots"));
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "rig-presentation-equipped-club.png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; Object.DestroyImmediate(target); }
        }

        [UnityTest]
        public IEnumerator PlayerGroundShadowUsesPrefabContactAndDoesNotBobWithoutAnimator()
        {
            var root = new GameObject("Ground test");
            var art = new GameObject("Proxy", typeof(SpriteRenderer), typeof(DoodleRigVisual));
            var shadow = new GameObject("Shadow", typeof(SpriteRenderer));
            art.transform.SetParent(root.transform, false); shadow.transform.SetParent(root.transform, false);
            try
            {
                root.transform.position = new Vector3(3, -4, 0); art.transform.localScale = Vector3.one * 1.28f;
                root.transform.localScale = new Vector3(1.5f, .9f, 1);
                root.transform.rotation = Quaternion.Euler(0, 0, 17);
                var visual = art.GetComponent<DoodleRigVisual>(); visual.GroundShadow = shadow.GetComponent<SpriteRenderer>();
                var entry = DoodleCharacterCatalog.Current.Player(-1); visual.Configure(entry);
                Assert.That(visual.Rig.groundContact, Is.Not.Null);
                Assert.That(visual.Rig.groundContact.parent, Is.EqualTo(visual.Rig.transform));
                visual.Rig.animator.enabled = false;
                foreach (bool left in new[] { false, true })
                {
                    art.GetComponent<SpriteRenderer>().flipX = left; visual.Sync();
                    var contact = visual.Rig.groundContact.localPosition * (2 / entry.extent);
                    contact.y -= entry.center.y * (2 / entry.extent);
                    contact.x *= left ? -1 : 1;
                    Assert.That(Vector3.Distance(shadow.transform.position, root.transform.TransformPoint(contact * 1.28f)), Is.LessThan(.0001f));
                    Assert.That(Vector3.Distance(shadow.transform.position, visual.Rig.groundContact.position), Is.LessThan(.0001f));
                    var before = art.transform.localPosition; var rotation = art.transform.localRotation; var ground = shadow.transform.position;
                    yield return new WaitForSeconds(.15f);
                    Assert.That(art.transform.localPosition, Is.EqualTo(before));
                    Assert.That(art.transform.localRotation, Is.EqualTo(rotation));
                    Assert.That(shadow.transform.position, Is.EqualTo(ground));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
