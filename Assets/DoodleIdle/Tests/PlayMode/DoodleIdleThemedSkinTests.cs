using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator SkinBulkUnlockAndEquipmentTabNotificationsFollowAvailableActions()
        {
            game.TogglePause();var ui=game.Ui;
            AssertBadge(UiNode("Skins"),false);
            LoadServiceSnapshot(saved=>{ServiceSetSavedField(saved,"mainStage",300);ServiceSetSavedField(saved,"highestMainStage",300);});
            var appearances=ui.Skins("Appearance").Where(x=>!x.initiallyOwned).ToArray();
            SelectSkinForTest(appearances[0]);yield return null;
            AssertBadge(UiNode("Skins"),true);
            AssertBadge(UiNode("외형 스킨",UiNode("Skin tabs")),true);
            AssertBadge(UiNode("무기 스킨",UiNode("Skin tabs")),true);
            AssertBadge(UiNode("UnlockSkin_"+appearances[0].id),true);
            AssertBadge(UiNode("SkinSlot_"+appearances[0].id,UiNode("Skin inventory")),true);
            AssertBadge(UiNode("SkinSlot_"+appearances[3].id,UiNode("Skin inventory")),false);
            AssertBadge(UiNode("일괄 해금"),true);
            Object.Destroy(CaptureFrame("skin-unlock-notifications.png",720,1520));
            int diamonds=ui.Diamonds;string equipped=ui.EquippedSkinId("Appearance");
            UiClick("일괄 해금");
            Assert.That(appearances.Count(x=>x.owned),Is.EqualTo(3));
            Assert.That(ui.Diamonds,Is.EqualTo(diamonds));Assert.That(ui.EquippedSkinId("Appearance"),Is.EqualTo(equipped));
            Assert.That(ui.SkinOwnedBonus("health"),Is.EqualTo(150));Assert.That(ui.SkinOwnedBonus("healthRegen"),Is.EqualTo(150));
            Assert.That(ui.UnlockAllSkins("Appearance"),Is.Zero);
            AssertBadge(UiNode("외형 스킨",UiNode("Skin tabs")),false);
            AssertBadge(UiNode("무기 스킨",UiNode("Skin tabs")),true);
            AssertBadge(UiNode("일괄 해금"),false);AssertBadge(UiNode("Skins"),true);
            UiClick("무기 스킨",UiNode("Skin tabs"));UiClick("일괄 해금");AssertBadge(UiNode("Skins"),false);
            var probes=new System.Collections.Generic.List<GameObject>();
            try {var restored=GrowthProbe(probes);Assert.That(restored.Skins("Appearance").Count(x=>x.owned&&!x.initiallyOwned),Is.EqualTo(3));}
            finally {foreach(var probe in probes)Object.Destroy(probe);}
            string[] categories={"Armor","Club","Necklace"},tabs={"갑옷","몽둥이","목걸이"};
            foreach(string category in categories) foreach(var item in ui.Items(category)){item.discovered=item.equipped=false;item.level=item.count=0;}
            for(int i=0;i<categories.Length;i++) {
                var item=ui.Items(categories[i])[0];ui.AddItem(item,1);item.count=0;UiOpen("Equipment");
                for(int j=0;j<tabs.Length;j++)AssertBadge(UiNode(tabs[j],UiNode("Equipment tabs")),i==j);
                ui.AutoEquip(categories[i]);AssertBadge(UiNode(tabs[i],UiNode("Equipment tabs")),false);
                item.count=ui.CopiesNeeded(item);AssertBadge(UiNode(tabs[i],UiNode("Equipment tabs")),true);
                Assert.That(ui.UpgradeItem(item),Is.True);AssertBadge(UiNode(tabs[i],UiNode("Equipment tabs")),false);
                item.level=100;item.count=5;AssertBadge(UiNode(tabs[i],UiNode("Equipment tabs")),true);
                item.count=0;AssertBadge(UiNode(tabs[i],UiNode("Equipment tabs")),false);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThemedSkinsHaveCompletePairedFramesAndWorldEquipmentUsesThem()
        {
            game.TogglePause(); var ui = game.Ui;
            LoadServiceSnapshot(saved => { ServiceSetSavedField(saved, "mainStage", 4000); ServiceSetSavedField(saved, "highestMainStage", 4000); });
            var appearances = ui.Skins("Appearance").Where(s => !s.initiallyOwned).ToArray();
            var weapons = ui.Skins("Weapon").Where(s => !s.initiallyOwned).ToArray();
            Assert.That(appearances.Length, Is.EqualTo(40)); Assert.That(weapons.Length, Is.EqualTo(40));
            Assert.That(appearances.Concat(weapons).Select(s => s.name).Distinct().Count(), Is.EqualTo(80));
            var visual = game.GetComponentsInChildren<DoodleRigVisual>().Single(v => v.Entry.group == "Player");
            for (int i = 0; i < appearances.Length; i++) {
                Assert.That(appearances[i].requiredStage, Is.EqualTo((i + 1) * 100));
                Assert.That(weapons[i].requiredStage, Is.EqualTo((i + 1) * 100));
                Assert.That(ui.TryAcquireSkin(appearances[i].id), Is.True);
                Assert.That(ui.TryAcquireSkin(weapons[i].id), Is.True);
                Assert.That(ui.EquipSkin(appearances[i].id), Is.True);
                Assert.That(ui.EquipSkin(weapons[i].id), Is.True);
                yield return null;
                int costume = DoodleCharacterCatalog.Costume(appearances[i].icon);
                var entry = DoodleCharacterCatalog.Current.Player(costume);
                Assert.That(visual.Rig.appearance, Is.SameAs(entry.appearance));
                Assert.That(visual.Rig.weaponRenderer.sprite, Is.SameAs(UiKit.Art(weapons[i].icon)));
                Assert.That(visual.Rig.weaponRenderer.transform.IsChildOf(visual.Rig.skeleton), Is.True);
                for (int pose = 0; pose < 2; pose++) {
                    var sprite = DoodlePlayerCostumeArt.Frame(costume, pose);
                    Assert.That(sprite.texture, Is.SameAs(entry.portraits[pose].texture));
                    Assert.That(sprite.pixelsPerUnit, Is.EqualTo(256));
                    var pixels = sprite.texture.GetPixels32();
                    Assert.That(pixels.Count(p => p.a > 128), Is.GreaterThan(200));
                    Assert.That(pixels.Count(p => p.a == 0), Is.GreaterThan(100));
                }
            }
            SelectSkinForTest(appearances.Single(s => s.name.Contains("돼지"))); yield return null;
            Object.Destroy(CaptureFrame("skins-pig-costume.png", 720, 1520));
            UiScrollBottom(); yield return null;
            Object.Destroy(CaptureFrame("skins-appearance-inventory-bottom.png", 720, 1520));
        }

        [UnityTest]
        public IEnumerator AllCostumesKeepOriginalBodyScaleFaceAndAnimationTimeline()
        {
            game.TogglePause(); var ui = game.Ui;
            var visual = game.GetComponentsInChildren<DoodleRigVisual>().Single(v => v.Entry.group == "Player");
            var bones = visual.Rig.skeleton.GetComponentsInChildren<Transform>();
            var positions = bones.Select(b => b.localPosition).ToArray();
            var rotations = bones.Select(b => b.localRotation).ToArray();
            Vector3 scale = visual.Rig.transform.localScale;
            Vector3 center = visual.Rig.transform.localPosition;
            foreach (var skin in ui.Skins("Appearance")) {
                skin.owned = true;
                if (!skin.equipped) Assert.That(ui.EquipSkin(skin.id), Is.True);
                yield return null;
                Assert.That(visual.Rig.transform.localScale, Is.EqualTo(scale), skin.name);
                Assert.That(visual.Rig.transform.localPosition, Is.EqualTo(center), skin.name);
                CollectionAssert.AreEqual(positions, bones.Select(b => b.localPosition), skin.name);
                CollectionAssert.AreEqual(rotations, bones.Select(b => b.localRotation), skin.name);
                Assert.That(visual.Rig.animator.runtimeAnimatorController.animationClips.Length, Is.EqualTo(5));
                Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator StatCategoriesMultiplyAndSkinOwnershipStacksWithinItsCategory()
        {
            game.TogglePause(); var ui=game.Ui;
            string[] categories={"Armor","Club","Necklace","Skill","Companion","Relic"};
            foreach(var item in categories.SelectMany(c=>ui.Items(c))) { item.discovered=item.equipped=false; item.level=1; item.ownedGoldPercent=0; }
            var club=ui.Items("Club")[0]; var skill=ui.Items("Skill")[0]; var companion=ui.Items("Companion")[0]; var relic=ui.Items("Relic")[0];
            float baseline=ui.CombatDamageMultiplier;
            var selected=new[]{club,skill,companion,relic};
            for(int i=0;i<selected.Length;i++) { selected[i].discovered=true; selected[i].effect="attack"; selected[i].ownedPercent=100; }
            // Relics derive owned percentages from enhancement, unlike equipment/abilities.
            var collection=typeof(DoodleUi).GetField("collectionTuning",GrowthPrivate).GetValue(ui);
            float relicStep=(float)collection.GetType().GetField("relicStepPercent").GetValue(collection);
            float relicFactor=1+relicStep/100;
            Assert.That(ui.CombatDamageMultiplier/baseline,Is.EqualTo(8*relicFactor).Within(.001));
            LoadServiceSnapshot(saved=>{ServiceSetSavedField(saved,"mainStage",200);ServiceSetSavedField(saved,"highestMainStage",200);});
            float damage=ui.CombatDamageMultiplier;
            var skins=ui.Skins("Weapon").Where(x=>!x.initiallyOwned).Take(2).ToArray();
            Assert.That(ui.TryAcquireSkin(skins[0].id),Is.True);
            Assert.That(ui.CombatDamageMultiplier/damage,Is.EqualTo(1.5f).Within(.001));
            Assert.That(ui.TryAcquireSkin(skins[1].id),Is.True);
            Assert.That(ui.CombatDamageMultiplier/damage,Is.EqualTo(2f).Within(.001));
            foreach(string effect in new[]{"health","healthRegen","gold","basicAttack","skillAttack","companionAttack","critDamage"}) {
                foreach(var item in selected) item.effect=effect;
                float multiplier=(float)typeof(DoodleUi).GetMethod("OwnedEffectMultiplier",GrowthPrivate).Invoke(ui,new object[]{effect,true});
                Assert.That(multiplier,Is.EqualTo(8*relicFactor).Within(.001),effect);
                if(effect=="health") Assert.That(ui.MaxHealth/ui.StatValue("health"),Is.EqualTo(multiplier).Within(.001));
                if(effect=="healthRegen") Assert.That(ui.HealthRegen/ui.StatValue("healthRegen"),Is.EqualTo(multiplier).Within(.001));
                if(effect=="gold") {
                    float gold=ui.GoldGainMultiplier;
                    var health=ui.MaxHealthAmount;var regen=ui.HealthRegenAmount;
                    var costume=ui.Skins("Appearance").First(x=>!x.initiallyOwned);
                    Assert.That(ui.TryAcquireSkin(costume.id),Is.True);
                    Assert.That(ui.GoldGainMultiplier,Is.EqualTo(gold));
                    Assert.That((double)(ui.MaxHealthAmount/health),Is.EqualTo(1.5).Within(.001));
                    Assert.That((double)(ui.HealthRegenAmount/regen),Is.EqualTo(1.5).Within(.001));
                }
            }
            var firstCostume=ui.Skins("Appearance").First(x=>!x.initiallyOwned);
            var secondCostume=ui.Skins("Appearance").Where(x=>!x.initiallyOwned).Skip(1).First();
            Assert.That(ui.TryAcquireSkin(secondCostume.id),Is.True);
            Assert.That(ui.SkinOwnedBonus("health"),Is.EqualTo(100));
            Assert.That(ui.SkinOwnedBonus("healthRegen"),Is.EqualTo(100));
            Assert.That(ui.SkinOwnedBonus("gold"),Is.Zero);
            var ownedHealth=ui.MaxHealthAmount;var ownedRegen=ui.HealthRegenAmount;
            Assert.That(ui.EquipSkin(firstCostume.id),Is.True);
            Assert.That(ui.MaxHealthAmount,Is.EqualTo(ownedHealth));
            Assert.That(ui.HealthRegenAmount,Is.EqualTo(ownedRegen));
            ui.Save();var probes=new System.Collections.Generic.List<GameObject>();
            try {
                var restored=GrowthProbe(probes);
                Assert.That(restored.SkinOwnedBonus("health"),Is.EqualTo(100));
                Assert.That(restored.SkinOwnedBonus("healthRegen"),Is.EqualTo(100));
                Assert.That(restored.SkinOwnedBonus("gold"),Is.Zero);
            } finally { foreach(var probe in probes) Object.Destroy(probe); }
            UiOpen("Skins");UiClick("외형 스킨");UiClick("SkinSlot_"+firstCostume.id);
            Assert.That(UiNode("Selected skin").GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("체력·체력 회복 +50%")),Is.True);
            Assert.That(UiNode("Skin total ownership").GetComponentInChildren<UnityEngine.UI.Text>().text,Does.Not.Contain("골드"));
            yield return null;
            Object.Destroy(CaptureFrame("appearance-health-recovery-bonus.png",720,1520));
            var scroll=UiNode("Skin inventory").GetComponentInParent<UnityEngine.UI.ScrollRect>();
            var details=(RectTransform)UiNode("Selected skin");var totalBox=UiNode("Skin total ownership");
            Assert.That(details.IsChildOf(scroll.content),Is.False);
            Assert.That(totalBox.IsChildOf(scroll.content),Is.False);
            Assert.That(UiNode("Skin tabs").IsChildOf(scroll.content),Is.False);
            var fixedPosition=details.anchoredPosition;var contentPosition=scroll.content.anchoredPosition;
            scroll.verticalNormalizedPosition=0;yield return null;
            Assert.That(details.anchoredPosition,Is.EqualTo(fixedPosition));
            Assert.That(scroll.content.anchoredPosition,Is.Not.EqualTo(contentPosition));
            foreach(var skin in ui.Skins("Weapon").Concat(ui.Skins("Appearance"))) Assert.That(skin.rarity,Is.Zero);
            foreach(var layout in UiNode("Skin inventory").GetComponentsInChildren<DoodleUiSlotLayout>())
                Assert.That(layout.grade.gameObject.activeSelf,Is.False);
            Object.Destroy(CaptureFrame("appearance-fixed-details-list-bottom.png",720,1520));
        }
    }
}
