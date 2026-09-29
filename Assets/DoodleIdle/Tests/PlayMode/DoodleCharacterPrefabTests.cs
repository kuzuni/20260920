using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.U2D.Animation;

namespace DoodleIdle.Tests
{
    public sealed class DoodleCharacterCatalogSetup : IPrebuildSetup
    {
        public void Setup()
        {
#if UNITY_EDITOR
            // Verify the delivered assets. Running combat tests must never regenerate
            // the user's authored rigs, animation assets, or portrait catalog.
            if (!UnityEditor.AssetDatabase.LoadAssetAtPath<DoodleCharacterCatalog>("Assets/DoodleIdle/Resources/DoodleIdle/CharacterCatalog.asset"))
                throw new System.InvalidOperationException("The delivered character catalog is missing.");
#endif
        }
    }
    [PrebuildSetup(typeof(DoodleCharacterCatalogSetup))]
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator PrefabCatalogCoversEveryFinalAppearanceAndPreservesAllFiveRigTypes()
        {
            game.TogglePause();
            var catalog = DoodleCharacterCatalog.Current;
            Assert.That(catalog.entries.Length, Is.EqualTo(142));
            Assert.That(catalog.entries.Count(e => e.group == "Player"), Is.EqualTo(41));
            Assert.That(catalog.entries.Count(e => e.group == "Enemies"), Is.EqualTo(69));
            Assert.That(catalog.entries.Count(e => e.group == "Companions"), Is.EqualTo(32));
            Assert.That(catalog.entries.Select(e => e.appearance.rigType).Distinct().Count(), Is.EqualTo(5));
            foreach (var entry in catalog.entries) {
                var go = new GameObject("Prefab test", typeof(SpriteRenderer), typeof(DoodleRigVisual));
                try {
                    var visual = go.GetComponent<DoodleRigVisual>(); visual.Configure(entry);
                    Assert.That(visual.Rig.appearance, Is.SameAs(entry.appearance));
                    Assert.That(visual.Rig.animator.runtimeAnimatorController, Is.Not.Null);
                    Assert.That(visual.Rig.partRenderers.Length, Is.EqualTo(entry.appearance.parts.Length));
                    foreach (var part in visual.Rig.partRenderers) {
                        Assert.That(part.sprite, Is.Not.Null);
                        Assert.That(part.GetComponent<SpriteSkin>().boneTransforms.All(b => b), Is.True);
                    }
                    foreach (var portrait in entry.portraits) {
                        Assert.That(portrait, Is.Not.Null);
                        var pixels = portrait.texture.GetPixels32();
                        Assert.That(pixels.Count(p => p.a > 128), Is.GreaterThan(200));
                        Assert.That(pixels.Count(p => p.a == 0), Is.GreaterThan(100));
                    }
                } finally { Object.Destroy(go); }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrefabActorsUseSkinnedPartsFacingPauseAndPooledAppearanceChanges()
        {
            yield return new WaitForSeconds(.3f);
            var visuals = game.GetComponentsInChildren<DoodleRigVisual>();
            Assert.That(visuals.Length, Is.GreaterThanOrEqualTo(201));
            Assert.That(visuals.All(v => !v.GetComponent<SpriteRenderer>().enabled), Is.True, "Old flat character renderers must stay hidden.");
            var player = visuals.Single(v => v.Entry.group == "Player");
            Assert.That(player.Rig.weaponRenderer.enabled, Is.True);
            player.GetComponent<SpriteRenderer>().flipX = true; player.Sync();
            Assert.That(player.Rig.transform.localScale.x, Is.LessThan(0));
            game.TogglePause();
            Assert.That(visuals.All(v => v.Rig.animator.speed == 0), Is.True);
            var enemy = visuals.First(v => v.Entry.group == "Enemies");
            foreach (var entry in DoodleCharacterCatalog.Current.entries.Where(e => e.group == "Enemies")) {
                enemy.Configure(entry); enemy.Sync();
                Assert.That(enemy.Rig.rigType, Is.EqualTo(entry.appearance.rigType));
                Assert.That(enemy.Rig.appearance, Is.SameAs(entry.appearance));
            }
            Object.Destroy(CaptureFrame("prefab-live-combat.png", 720, 1520));
            game.TogglePause();
            yield return new WaitForSeconds(.25f);
            Assert.That(player.Rig.animator.speed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PrefabCompanionsAllSpawnTheirMatchingRigAndFireTheConfiguredProjectile()
        {
            game.companionsEnabled = true;
            var companions = game.Ui.Items("Companion").ToArray();
            foreach (var item in companions) { item.equipped = false; item.discovered = true; item.slot = 0; }
            foreach (var armor in game.Ui.Items("Armor")) armor.discovered = true;
            foreach (var item in companions) {
                item.equipped = true;
                yield return new WaitForFixedUpdate(); yield return null;
                var visual = game.GetComponentsInChildren<DoodleRigVisual>().Single(v => v.Entry.group == "Companions");
                Assert.That(visual.Entry.id.Substring(3), Is.EqualTo(item.id));
                Assert.That(visual.Rig.rigType, Is.EqualTo(item.rarity < 4 ? "biped" : "floating"));
                Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.False);
                Assert.That(UiKit.Art(item.projectile), Is.Not.Null);
                var actors = (System.Collections.IDictionary)typeof(DoodleIdleGame).GetField("companions", GrowthPrivate).GetValue(game);
                int before = game.CompanionShotCount(item.id);
                typeof(DoodleIdleGame).GetMethod("FireCompanionShot", GrowthPrivate).Invoke(game, new object[] {actors[item.id], 0, false});
                Assert.That(game.CompanionShotCount(item.id), Is.EqualTo(before + 1));
                if (item.trajectory != "Lightning")
                    Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Last(r => r.name == "Companion shot: " + item.id).sprite.texture, Is.SameAs(UiKit.Art(item.projectile).texture));
                item.equipped = false;
                yield return new WaitForFixedUpdate(); yield return null;
            }
            int slot = 0;
            foreach (var item in companions.Where(i => i.id == "companion_brick" || i.id == "companion_sun_drone")) { item.equipped = true; item.slot = slot++; }
            yield return new WaitForSeconds(1);
            Object.Destroy(CaptureFrame("prefab-new-companions.png", 720, 1520));
        }

        [UnityTest]
        public IEnumerator PrefabUiUsesFinalPlayerSkinsCompanionsAndAmethystProjectiles()
        {
            game.TogglePause();
            Assert.That(game.Ui.Skins("Appearance").Count, Is.EqualTo(41));
            Assert.That(game.Ui.Skins("Weapon").Count, Is.EqualTo(41));
            foreach (var skin in game.Ui.Skins("Appearance")) {
                var expected = DoodleCharacterCatalog.Current.Player(DoodleCharacterCatalog.Costume(skin.icon));
                Assert.That(UiKit.Art(skin.icon).texture, Is.SameAs(expected.portraits[0].texture));
            }
            foreach (var companion in game.Ui.Items("Companion")) {
                int index = DoodleCollectionArt.CompanionIndex(companion.icon);
                var entry = DoodleCharacterCatalog.Current.Companion(index);
                Assert.That(entry.id.Substring(3), Is.EqualTo(companion.id));
                Assert.That(UiKit.Art(companion.icon).texture, Is.SameAs(entry.portraits[0].texture));
            }
            Assert.That(game.Ui.Items("Companion").Single(i => i.id == "companion_brick").name, Is.EqualTo("자수정골렘"));
            Assert.That(DoodleCollectionArt.CompanionImpactName(18), Is.EqualTo("자수정 조각"));
            Assert.That(DoodleCollectionArt.CompanionImpact(18), Is.SameAs(DoodleCollectionArt.Get("CompanionShot_18")));
            foreach (string key in new[] { "Player", "PlayerWalkA", "PlayerWalkB" })
                Assert.That(UiKit.Art(key).texture.name, Does.StartWith("00_default_"));
            foreach (string page in new[] { "Skins", "Companions", "Equipment", "Pvp", "Chat" }) {
                UiOpen(page); yield return null;
                if (page == "Skins") { UiClick("외형 스킨", UiNode("Skin tabs")); yield return null; }
                Object.Destroy(CaptureFrame("prefab-ui-" + page + ".png", 720, 1520)); game.Ui.ClosePage();
            }
            var visual = game.GetComponentsInChildren<DoodleRigVisual>().Single(v => v.Entry.group == "Player");
            foreach (var skin in game.Ui.Skins("Appearance")) {
                skin.owned = true; game.Ui.EquipSkin(skin.id); yield return null;
                Assert.That(visual.Entry, Is.SameAs(DoodleCharacterCatalog.Current.Player(DoodleCharacterCatalog.Costume(skin.icon))));
                var profileImage = (UnityEngine.UI.Image)typeof(DoodleUi).GetField("profilePortrait", GrowthPrivate).GetValue(game.Ui);
                Assert.That(profileImage.sprite.texture, Is.SameAs(visual.Entry.portraits[0].texture));
            }
        }
    }
}
