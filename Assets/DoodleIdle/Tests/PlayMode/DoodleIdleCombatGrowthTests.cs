using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator GrowthCriticalDamageChangesEnemyHealthWithFourTimesPriority()
        {
            game.TogglePause();
            var ui = game.Ui;
            var enemies = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            Assert.That(enemies.Count, Is.GreaterThan(0));
            var enemy = enemies[0];
            var health = enemy.GetType().GetField("hp");
            var damage = typeof(DoodleIdleGame).GetMethod("Damage", GrowthPrivate);
            Assert.That(health, Is.Not.Null);
            Assert.That(damage, Is.Not.Null);
            int kills = game.Kills;
            int GuaranteedLevel(string id)
            {
                var stat = GrowthTuning.stats.Single(x => x.id == id);
                return Mathf.CeilToInt((100 - stat.initial) / stat.increment);
            }

            // Invoke the real hit entry point, including progression, critical rolls and HP mutation.
            // A large surviving target prevents death/refill side effects from obscuring each result.
            void ExpectHit(float multiplier)
            {
                const float startingHealth = 10000, baseDamage = 100;
                health.SetValue(enemy, startingHealth);
                damage.Invoke(game, new object[] { enemy, baseDamage, Vector2.zero });
                float lost = startingHealth - (float)health.GetValue(enemy);
                Assert.That(lost, Is.EqualTo(baseDamage * ui.UiDamageMultiplier * multiplier).Within(.01f));
                Assert.That(game.Kills, Is.EqualTo(kills));
            }

            GrowthLevels["crit2Chance"] = GrowthLevels["crit4Chance"] = 0;
            Assert.That(ui.CriticalDamageBonus, Is.Zero);
            ExpectHit(1);
            GrowthLevels["crit2Chance"] = GuaranteedLevel("crit2Chance");
            Assert.That(ui.Critical2Chance, Is.EqualTo(100));
            ExpectHit(2);
            GrowthLevels["crit4Chance"] = GuaranteedLevel("crit4Chance");
            Assert.That(ui.Critical4Chance, Is.EqualTo(100));
            ExpectHit(4); // Both guarantees must produce 4x, never 2x or 8x.
            GrowthLevels["crit2Chance"] = 0;
            ExpectHit(4);

            var relic = ui.Items("Relic").Single(x => x.effect == "critDamage");
            relic.discovered = true;
            relic.level = 10;
            float bonus = ui.CriticalDamageBonus;
            Assert.That(bonus, Is.GreaterThan(0));
            ExpectHit(4 * (1 + bonus / 100));
            GrowthLevels["crit4Chance"] = 0;
            GrowthLevels["crit2Chance"] = GuaranteedLevel("crit2Chance");
            ExpectHit(2 * (1 + bonus / 100));
            GrowthLevels["crit2Chance"] = 0;
            ExpectHit(1); // Critical-damage bonuses must not amplify ordinary hits.
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrowthEquippedSkinsUpdateWorldWeaponSpriteAndPlayerTintAndRestoreBasics()
        {
            IsolateSummonTest();
            PlayerBody().simulated = false;
            yield return null;
            var ui = game.Ui;
            var weapon = NamedArt("Floating baseball club").Single();
            var playerArt = NamedArt("Generated head sprite").Single(x => x.transform.parent == PlayerBody().transform);
            var originalWeapon = weapon.sprite;
            var originalPlayerTexture = playerArt.sprite.texture;
            var weaponSkin = ui.Skins("Weapon").Single(x => x.id == "weapon_vine");
            var appearance = ui.Skins("Appearance").Single(x => x.id == "appearance_mint");
            ui.Diamonds = weaponSkin.diamondCost + appearance.diamondCost;
            Assert.That(ui.TryAcquireSkin(weaponSkin.id), Is.True);
            Assert.That(ui.TryAcquireSkin(appearance.id), Is.True);
            Assert.That(ui.EquipSkin(weaponSkin.id), Is.True);
            Assert.That(ui.EquipSkin(appearance.id), Is.True);
            yield return null;
            yield return null;

            var source = UiKit.Art(weaponSkin.icon);
            Assert.That(source, Is.Not.Null);
            Assert.That(weapon.sprite, Is.Not.SameAs(originalWeapon));
            Assert.That(weapon.sprite.texture, Is.SameAs(source.texture));
            Assert.That(weapon.sprite.rect, Is.EqualTo(source.rect));
            Assert.That(weapon.sharedMaterial.mainTexture, Is.SameAs(source.texture), "The rendered material must follow the new sprite atlas.");
            Assert.That(weapon.color, Is.EqualTo(weaponSkin.tint));
            Assert.That(playerArt.color, Is.EqualTo(appearance.tint));
            Assert.That(playerArt.color, Is.Not.EqualTo(Color.white));
            Assert.That(playerArt.sprite.texture, Is.SameAs(originalPlayerTexture), "Cat recolors must retain the live player animation artwork.");

            Assert.That(ui.EquipSkin(ui.Skins("Weapon").Single(x => x.initiallyOwned).id), Is.True);
            Assert.That(ui.EquipSkin(ui.Skins("Appearance").Single(x => x.initiallyOwned).id), Is.True);
            yield return null;
            yield return null;
            Assert.That(weapon.sprite, Is.SameAs(originalWeapon));
            Assert.That(weapon.sharedMaterial.mainTexture, Is.SameAs(originalWeapon.texture));
            Assert.That(weapon.color, Is.EqualTo(Color.white));
            Assert.That(playerArt.color, Is.EqualTo(Color.white));
            Assert.That(playerArt.sprite.texture, Is.SameAs(originalPlayerTexture));
        }
    }
}
