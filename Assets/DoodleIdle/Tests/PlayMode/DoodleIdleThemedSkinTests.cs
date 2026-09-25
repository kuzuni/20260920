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
        public IEnumerator ThemedSkinsHaveCompletePairedFramesAndWorldEquipmentUsesThem()
        {
            game.TogglePause(); var ui = game.Ui;
            LoadServiceSnapshot(saved => { ServiceSetSavedField(saved, "mainStage", 2000); ServiceSetSavedField(saved, "highestMainStage", 2000); });
            var appearances = ui.Skins("Appearance").Where(s => !s.initiallyOwned).ToArray();
            var weapons = ui.Skins("Weapon").Where(s => !s.initiallyOwned).ToArray();
            Assert.That(appearances.Length, Is.EqualTo(20)); Assert.That(weapons.Length, Is.EqualTo(20));
            Assert.That(appearances.Concat(weapons).Select(s => s.name).Distinct().Count(), Is.EqualTo(40));
            Assert.That(appearances[16].name, Does.Contain("돼지"));
            Assert.That(weapons[16].name, Does.Contain("돼지"));
            var actor = typeof(DoodleIdleGame).GetField("player", GrowthPrivate).GetValue(game);
            var animation = typeof(DoodleIdleGame).GetMethod("AnimateActorFrames", GrowthPrivate);
            var art = (SpriteRenderer)actor.GetType().GetField("art").GetValue(actor);
            var body = (Rigidbody2D)actor.GetType().GetField("body").GetValue(actor);
            var club = NamedArt("Floating baseball club").Single();
            var weaponUpdate = typeof(DoodleIdleGame).GetMethod("ApplyWeaponSkin", GrowthPrivate);
            body.simulated = true; body.linearVelocity = Vector2.right;
            actor.GetType().GetField("phase").SetValue(actor, 0f);
            for (int i = 0; i < 20; i++) {
                Assert.That(appearances[i].requiredStage, Is.EqualTo((i+1)*100));
                Assert.That(weapons[i].requiredStage, Is.EqualTo((i+1)*100));
                Assert.That(ui.TryAcquireSkin(appearances[i].id), Is.True);
                Assert.That(ui.TryAcquireSkin(weapons[i].id), Is.True);
                Assert.That(ui.EquipSkin(appearances[i].id), Is.True);
                Assert.That(ui.EquipSkin(weapons[i].id), Is.True);
                var first = UiKit.Art(appearances[i].icon); var second = UiKit.Art("SkinAppearance_"+i+"_1");
                Assert.That(first.texture, Is.Not.SameAs(second.texture));
                Assert.That(first.pixelsPerUnit, Is.EqualTo(384));
                Assert.That(second.pixelsPerUnit, Is.EqualTo(384));
                foreach (var sprite in new[] { first, second, UiKit.Art(weapons[i].icon) }) {
                    var r = sprite.rect; int w = (int)r.width, h = (int)r.height;
                    var pixels = sprite.texture.GetPixels((int)r.x, (int)r.y, w, h);
                    Assert.That(pixels.Count(p => p.a > .125f), Is.GreaterThan(w*h/12));
                    for (int x=0; x<w; x++) { Assert.That(pixels[x].a, Is.LessThan(.13f),sprite.name+" bottom"); Assert.That(pixels[(h-1)*w+x].a, Is.LessThan(.13f),sprite.name+" top"); }
                    for (int y=0; y<h; y++) { Assert.That(pixels[y*w].a, Is.LessThan(.13f),sprite.name+" left"); Assert.That(pixels[y*w+w-1].a, Is.LessThan(.13f),sprite.name+" right"); }
                }
                for (int frame=0; frame<2; frame++) {
                    actor.GetType().GetField("walkClock").SetValue(actor, frame/6f);
                    animation.Invoke(game, new object[] { actor, 0f });
                    Assert.That(art.sprite.name, Is.EqualTo("Skin SkinAppearance_"+i+"_"+frame));
                    Assert.That(art.sharedMaterial.mainTexture, Is.SameAs(art.sprite.texture));
                    Assert.That(art.sprite, Is.SameAs(UiKit.Art("SkinAppearance_"+i+"_"+frame)), "Do not renormalize the body by the hat bounds.");
                }
                weaponUpdate.Invoke(game, new object[] { club });
                Assert.That(club.sprite.texture.name, Is.EqualTo("SkinWeapons"));
                Assert.That(club.sprite.rect, Is.EqualTo(UiKit.Art(weapons[i].icon).rect));
                Assert.That(club.sprite.pivot.x / club.sprite.rect.width, Is.EqualTo(.15f).Within(.001f));
                if (i==16) {
                    UiOpen(null);
                    for(int frame=0;frame<2;frame++) {
                        actor.GetType().GetField("walkClock").SetValue(actor,frame/6f);
                        animation.Invoke(game,new object[]{actor,0f});
                        Object.Destroy(CaptureFrame("skins-pig-world-frame-"+frame+".png",1440,900));
                    }
                }
            }
            body.linearVelocity = Vector2.zero;
            foreach (string category in new[] { "Armor", "Club", "Necklace" }) {
                var items = ui.Items(category);
                Assert.That(items.Count, Is.EqualTo(36));
                Assert.That(items.Select(x => x.name).Distinct().Count(), Is.EqualTo(36));
                Assert.That(items.All(x => !System.Text.RegularExpressions.Regex.IsMatch(x.name, @"^(일반|고급|희귀|영웅|전설|신화|근원|초월|갓)\s*\d")), Is.True);
            }
            SelectSkinForTest(appearances[16]); yield return null;
            Object.Destroy(CaptureFrame("skins-pig-costume.png", 720, 1520));
            UiScrollBottom(); yield return null;
            Object.Destroy(CaptureFrame("skins-appearance-inventory-bottom.png", 720, 1520));
            SelectSkinForTest(weapons[3]); yield return null;
            Object.Destroy(CaptureFrame("skins-ice-staff.png", 720, 1520));
            SelectSkinForTest(weapons[13]); yield return null;
            Object.Destroy(CaptureFrame("skins-fire-staff.png", 720, 1520));
        }

        [UnityTest]
        public IEnumerator AllCostumesKeepOriginalBodyScaleFaceAndAnimationTimeline()
        {
            game.TogglePause(); var ui = game.Ui;
            LoadServiceSnapshot(saved => { ServiceSetSavedField(saved, "mainStage", 2000); ServiceSetSavedField(saved, "highestMainStage", 2000); });
            var appearances = ui.Skins("Appearance").Where(s => !s.initiallyOwned).ToArray();
            var basic = ui.Skins("Appearance").Single(s => s.initiallyOwned);
            var actor = typeof(DoodleIdleGame).GetField("player", GrowthPrivate).GetValue(game);
            var type = actor.GetType();
            var art = (SpriteRenderer)type.GetField("art").GetValue(actor);
            var body = (Rigidbody2D)type.GetField("body").GetValue(actor);
            var animate = typeof(DoodleIdleGame).GetMethod("AnimateActorFrames", GrowthPrivate);
            Vector3 originalScale = art.transform.localScale;
            var timeline = new System.Collections.Generic.List<int>();
            float[] ticks = { .02f, .10f, .08f, .09f, .07f, .12f, .14f, .03f };
            for (int costume = -1; costume < 20; costume++) {
                if (costume >= 0) ui.TryAcquireSkin(appearances[costume].id);
                ui.EquipSkin(costume < 0 ? basic.id : appearances[costume].id);
                type.GetField("walkClock").SetValue(actor, 0f); type.GetField("phase").SetValue(actor, 0f);
                body.simulated = true; body.linearVelocity = Vector2.right;
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int step = 0; step < ticks.Length; step++) {
                    animate.Invoke(game, new object[] { actor, ticks[step] });
                    int pose = art.sprite.name == "PlayerWalkB" || art.sprite.name.EndsWith("_1") ? 1 : 0;
                    seen.Add(pose);
                    if (costume < 0) timeline.Add(pose); else Assert.That(pose, Is.EqualTo(timeline[step]), appearances[costume].name);
                    Assert.That(art.transform.localScale, Is.EqualTo(originalScale));
                    if (costume < 0) continue;
                    Assert.That(art.sprite, Is.SameAs(DoodlePlayerCostumeArt.Frame(costume, pose)));
                    // Eye pixels are from the original player at the same body-space positions.
                    var reference = DoodlePlayerCostumeArt.BodyFrame(pose);
                    var eyes = pose == 0 ? new[] { new Vector2(199, 1026.5f), new Vector2(280.5f, 1001) }
                        : new[] { new Vector2(555, 631), new Vector2(765.5f, 570) };
                    foreach (var eye in eyes) {
                        Vector2 unit = (eye - reference.rect.center) / reference.pixelsPerUnit;
                        Vector2 pixel = unit * art.sprite.pixelsPerUnit + art.sprite.pivot;
                        var color = art.sprite.texture.GetPixelBilinear(pixel.x / art.sprite.texture.width, pixel.y / art.sprite.texture.height);
                        Assert.That(color.a, Is.GreaterThan(.9f));
                        Assert.That(Mathf.Max(color.r, color.g, color.b), Is.LessThan(.25f), appearances[costume].name + " original eye position");
                    }
                }
                Assert.That(seen.Count, Is.EqualTo(2));
                body.linearVelocity = Vector2.zero;
                animate.Invoke(game, new object[] { actor, .3f });
                Assert.That(art.sprite.name, Is.EqualTo(costume < 0 ? "PlayerWalkA" : "Skin SkinAppearance_" + costume + "_0"));
                Assert.That((float)type.GetField("walkClock").GetValue(actor), Is.Zero);
            }
            foreach (var renderer in game.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled = false;
            var proof = new GameObject("Costume body comparison"); proof.transform.SetParent(game.transform);
            var camera = Camera.main; camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 6.3f;
            var setters = typeof(DoodleIdleGame).GetMethod("SetSpriteArt", GrowthPrivate);
            var portraits = new SpriteRenderer[21];
            for (int i = 0; i <= 20; i++) {
                var go = new GameObject("Costume comparison " + i); go.transform.SetParent(proof.transform);
                go.transform.position = new Vector3(-4.4f + (i % 5) * 2.2f, 4.4f - (i / 5) * 2.2f, 0);
                go.transform.localScale = originalScale;
                portraits[i] = go.AddComponent<SpriteRenderer>();
            }
            for (int pose = 0; pose < 2; pose++) {
                for (int i = 0; i <= 20; i++) setters.Invoke(game, new object[] { portraits[i], i == 0 ? DoodlePlayerCostumeArt.BodyFrame(pose) : DoodlePlayerCostumeArt.Frame(i-1, pose) });
                Object.Destroy(CaptureFrame("costume-body-comparison-" + pose + ".png", 1440, 1440, false));
            }
            Object.Destroy(proof);
            yield return null;
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
