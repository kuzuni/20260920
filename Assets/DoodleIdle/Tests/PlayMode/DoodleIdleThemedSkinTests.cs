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
                Assert.That(first.rect.size, Is.EqualTo(second.rect.size));
                Assert.That(first.rect, Is.Not.EqualTo(second.rect));
                foreach (var sprite in new[] { first, second, UiKit.Art(weapons[i].icon) }) {
                    var r = sprite.rect; int w = (int)r.width, h = (int)r.height;
                    var pixels = sprite.texture.GetPixels((int)r.x, (int)r.y, w, h);
                    Assert.That(pixels.Count(p => p.a > .125f), Is.GreaterThan(w*h/12));
                    for (int x=0; x<w; x++) { Assert.That(pixels[x].a, Is.LessThan(.13f)); Assert.That(pixels[(h-1)*w+x].a, Is.LessThan(.13f)); }
                    for (int y=0; y<h; y++) { Assert.That(pixels[y*w].a, Is.LessThan(.13f)); Assert.That(pixels[y*w+w-1].a, Is.LessThan(.13f)); }
                }
                for (int frame=0; frame<2; frame++) {
                    actor.GetType().GetField("walkClock").SetValue(actor, frame/6f);
                    animation.Invoke(game, new object[] { actor, 0f });
                    Assert.That(art.sprite.name, Is.EqualTo("Skin SkinAppearance_"+i+"_"+frame));
                    Assert.That(art.sharedMaterial.mainTexture, Is.SameAs(art.sprite.texture));
                }
                weaponUpdate.Invoke(game, new object[] { club });
                Assert.That(club.sprite.texture.name, Is.EqualTo("SkinWeapons"));
                Assert.That(club.sprite.rect, Is.EqualTo(UiKit.Art(weapons[i].icon).rect));
                Assert.That(club.sprite.pivot.x / club.sprite.rect.width, Is.EqualTo(.15f).Within(.001f));
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
                    var costume=ui.Skins("Appearance").First(x=>!x.initiallyOwned);
                    Assert.That(ui.TryAcquireSkin(costume.id),Is.True);
                    Assert.That(ui.GoldGainMultiplier/gold,Is.EqualTo(1.5f).Within(.001));
                }
            }
            yield return null;
        }
    }
}
