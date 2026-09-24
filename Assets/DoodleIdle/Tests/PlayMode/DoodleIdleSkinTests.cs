using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        void SelectSkinForTest(UiSkin skin)
        {
            UiOpen("Skins");
            UiClick(skin.category == "Weapon" ? "무기 스킨" : "외형 스킨", UiNode("Skin tabs"));
            UiClick("SkinSlot_" + skin.id, UiNode("Skin inventory"));
        }

        void AssertLockedSkinHasDescriptionAndNoEquip(UiSkin skin)
        {
            var selected = UiNode("Selected skin");
            Assert.That(selected.GetComponentsInChildren<Text>().Any(x => x.text == skin.description), Is.True,
                "An unowned skin must still explain its appearance.");
            Assert.That(selected.GetComponentsInChildren<Text>().Any(x => x.text.Contains("보유 효과")), Is.True);
            Assert.That(selected.GetComponentsInChildren<Button>().Any(x => x.name == "EquipSkin_" + skin.id), Is.False,
                "Locked skins must hide the equip action instead of presenting a nonfunctional equip button.");
        }

        [UnityTest]
        public IEnumerator SkinPurchaseChargesOnceAndOwnershipStatsDoNotDependOnEquipment()
        {
            game.TogglePause();
            var ui = game.Ui;
            var skin = ui.Skins("Weapon").Single(x => x.id == "weapon_vine");
            var basic = ui.Skins("Weapon").Single(x => x.initiallyOwned);
            Assert.That(skin.owned, Is.False);
            Assert.That(skin.effect, Is.EqualTo("attack"));
            LoadServiceSnapshot(saved => { ServiceSetSavedField(saved, "mainStage", 100); ServiceSetSavedField(saved, "highestMainStage", 100); });
            ui.Diamonds = 137;
            long gold = ui.Gold, power = ui.Power;
            float ownership = ui.SkinOwnedBonus("attack"), attack = ui.OwnedBonus, damage = ui.CombatDamageMultiplier;
            SelectSkinForTest(skin);
            AssertLockedSkinHasDescriptionAndNoEquip(skin);
            UiClick("UnlockSkin_" + skin.id);
            Assert.That(ui.Diamonds, Is.EqualTo(137), "The displayed purchase must deduct its exact configured price.");
            Assert.That(ui.Gold, Is.EqualTo(gold));
            Assert.That(ui.IsSkinOwned(skin.id), Is.True);
            Assert.That(ui.SkinOwnedBonus("attack"), Is.EqualTo(ownership + skin.ownedBonus).Within(.0001f));
            Assert.That(ui.OwnedBonus, Is.EqualTo((attack + 100) * 1.5f - 100).Within(.0001f));
            Assert.That(ui.CombatDamageMultiplier, Is.GreaterThan(damage), "The actual combat model must receive ownership effects immediately.");
            Assert.That(ui.Power, Is.GreaterThan(power));
            Assert.That(ui.TryAcquireSkin(skin.id), Is.False, "Buying the same skin twice must be rejected.");
            Assert.That(ui.Diamonds, Is.EqualTo(137));
            long ownedPower = ui.Power;
            float ownedDamage = ui.CombatDamageMultiplier;
            UiClick("EquipSkin_" + skin.id);
            Assert.That(ui.EquippedSkinId("Weapon"), Is.EqualTo(skin.id));
            Assert.That(ui.EquippedWeaponIcon, Is.EqualTo(skin.icon));
            Assert.That(ui.EquippedWeaponTint, Is.EqualTo(skin.tint));
            Assert.That(ui.Power, Is.EqualTo(ownedPower), "Equipping a skin must not grant a second stat bonus.");
            Assert.That(ui.CombatDamageMultiplier, Is.EqualTo(ownedDamage).Within(.0001f));
            Assert.That(ui.EquipSkin(basic.id), Is.True);
            Assert.That(ui.EquippedSkinId("Weapon"), Is.EqualTo(basic.id));
            Assert.That(ui.Power, Is.EqualTo(ownedPower), "Replacing the skin with the default must preserve its ownership bonus.");
            Assert.That(ui.SkinOwnedBonus("attack"), Is.EqualTo(ownership + skin.ownedBonus).Within(.0001f));
            Assert.That(ui.EquipSkin(basic.id), Is.False, "Re-equipping the current appearance is a no-op.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkinPurchaseRejectsInsufficientCurrencyWithoutHidingItsDescription()
        {
            game.TogglePause();
            var ui = game.Ui;
            var skin = ui.Skins("Appearance").Single(x => x.id == "appearance_mint");
            ui.Diamonds = 999999;
            int diamonds = ui.Diamonds;
            long gold = ui.Gold, power = ui.Power;
            float bonus = ui.SkinOwnedBonus(skin.effect);
            SelectSkinForTest(skin);
            AssertLockedSkinHasDescriptionAndNoEquip(skin);
            Assert.That(UiNode("UnlockSkin_" + skin.id).GetComponent<Button>().interactable, Is.False);
            Assert.That(ui.TryAcquireSkin(skin.id), Is.False);
            Assert.That(ui.EquipSkin(skin.id), Is.False);
            Assert.That(ui.TryAcquireSkin("missing_skin"), Is.False);
            Assert.That(ui.EquipSkin("missing_skin"), Is.False);
            Assert.That(ui.IsSkinOwned(skin.id), Is.False);
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds));
            Assert.That(ui.Gold, Is.EqualTo(gold));
            Assert.That(ui.Power, Is.EqualTo(power));
            Assert.That(ui.SkinOwnedBonus(skin.effect), Is.EqualTo(bonus));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkinStageUnlocksRequireOneHundredCompletedStagesAndAreFreeAndSingleUse()
        {
            game.TogglePause();
            var ui = game.Ui;
            var conditions = ui.Skins("Weapon").Concat(ui.Skins("Appearance"))
                .Where(x => x.acquisition == "MainStage" || x.acquisition == "HighestDungeonStage").ToArray();
            Assert.That(conditions.Length, Is.EqualTo(40), "Twenty themes per category, excluding defaults.");
            foreach (var skin in conditions)
            {
                Assert.That(skin.requiredStage % 100, Is.Zero);
                Assert.That(skin.requiredStage, Is.InRange(100, 2000));
                Assert.That(skin.ownedBonus, Is.EqualTo(50));
                LoadServiceSnapshot(saved =>
                {
                    ServiceSetSavedField(saved, "mainStage", skin.requiredStage - 1);
                    ServiceSetSavedField(saved, "highestMainStage", skin.requiredStage - 1);
                    ServiceSetSavedField(saved, "dungeonStages", new[] { 99, 0, 0 });
                });
                int diamonds = ui.Diamonds;
                long gold = ui.Gold;
                float bonus = ui.SkinOwnedBonus(skin.effect);
                SelectSkinForTest(skin);
                AssertLockedSkinHasDescriptionAndNoEquip(skin);
                Assert.That(UiNode("UnlockSkin_" + skin.id).GetComponent<Button>().interactable, Is.False);
                Assert.That(ui.TryAcquireSkin(skin.id), Is.False, "Ninety-nine completed stages must not unlock the one-hundred-stage skin.");
                Assert.That(ui.EquipSkin(skin.id), Is.False);
                Assert.That(ui.IsSkinOwned(skin.id), Is.False);
                LoadServiceSnapshot(saved =>
                {
                    ServiceSetSavedField(saved, "mainStage", skin.requiredStage);
                    ServiceSetSavedField(saved, "highestMainStage", skin.requiredStage);
                });
                Assert.That(skin.owned, Is.False, "Reaching a stage must not automatically grant its skin.");
                SelectSkinForTest(skin);
                UiClick("UnlockSkin_" + skin.id);
                Assert.That(ui.IsSkinOwned(skin.id), Is.True);
                Assert.That(ui.SkinOwnedBonus(skin.effect), Is.EqualTo(bonus + skin.ownedBonus).Within(.0001f));
                Assert.That(UiNode("EquipSkin_" + skin.id).GetComponent<Button>().interactable, Is.True);
                Assert.That(ui.TryAcquireSkin(skin.id), Is.False, "A completed condition may unlock each skin only once.");
                Assert.That(ui.Diamonds, Is.EqualTo(diamonds), "Progression unlocks must not charge diamonds.");
                Assert.That(ui.Gold, Is.EqualTo(gold));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkinOwnershipAndEquippedTintsRestoreFromSavedProfile()
        {
            game.TogglePause();
            var ui = game.Ui;
            var weapon = ui.Skins("Weapon").Single(x => x.id == "weapon_vine");
            var appearance = ui.Skins("Appearance").Single(x => x.id == "appearance_mint");
            LoadServiceSnapshot(saved => { ServiceSetSavedField(saved, "mainStage", 100); ServiceSetSavedField(saved, "highestMainStage", 100); });
            ui.Diamonds = 83;
            Assert.That(ui.TryAcquireSkin(weapon.id), Is.True);
            Assert.That(ui.TryAcquireSkin(appearance.id), Is.True);
            Assert.That(ui.EquipSkin(weapon.id), Is.True);
            Assert.That(ui.EquipSkin(appearance.id), Is.True);
            Assert.That(ui.EquippedAppearanceIcon, Is.EqualTo(appearance.icon), "Appearance skins use the costumed cat art.");
            Assert.That(ui.EquippedAppearanceTint, Is.EqualTo(Color.white));
            long power = ui.Power;
            float attack = ui.SkinOwnedBonus("attack"), health = ui.SkinOwnedBonus("health");
            ui.Save();
            Assert.That(PlayerPrefs.HasKey("DoodleUi.Skins"), Is.True);
            // Clear only the in-memory cache, then enter the actual persisted-catalog loader.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ((IList)typeof(DoodleUi).GetField("skinCatalog", flags).GetValue(ui)).Clear();
            typeof(DoodleUi).GetField("skinsInitialized", flags).SetValue(ui, false);
            var restoredWeapon = ui.Skins("Weapon").Single(x => x.id == weapon.id);
            Assert.That(restoredWeapon, Is.Not.SameAs(weapon), "The check must read a fresh object from the saved profile.");
            Assert.That(ui.IsSkinOwned(weapon.id), Is.True);
            Assert.That(ui.IsSkinOwned(appearance.id), Is.True);
            Assert.That(ui.EquippedSkinId("Weapon"), Is.EqualTo(weapon.id));
            Assert.That(ui.EquippedSkinId("Appearance"), Is.EqualTo(appearance.id));
            Assert.That(ui.EquippedWeaponTint, Is.EqualTo(weapon.tint));
            Assert.That(ui.EquippedAppearanceTint, Is.EqualTo(appearance.tint));
            Assert.That(ui.Skins("Weapon").Count(x => x.equipped), Is.EqualTo(1));
            Assert.That(ui.Skins("Appearance").Count(x => x.equipped), Is.EqualTo(1));
            Assert.That(ui.SkinOwnedBonus("attack"), Is.EqualTo(attack));
            Assert.That(ui.SkinOwnedBonus("health"), Is.EqualTo(health));
            Assert.That(ui.Power, Is.EqualTo(power));
            Assert.That(ui.Diamonds, Is.EqualTo(83));
            Assert.That(ui.TryAcquireSkin(appearance.id), Is.False, "Reloading cannot make an owned skin purchasable again.");
            SelectSkinForTest(restoredWeapon);
            Assert.That(UiNode("EquipSkin_" + weapon.id).GetComponent<Button>().interactable, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkinsAreExcludedFromEveryRandomDrawCatalog()
        {
            game.TogglePause();
            var ui = game.Ui;
            var skinIds = new HashSet<string>(ui.Skins("Weapon").Concat(ui.Skins("Appearance")).Select(x => x.id));
            foreach (string category in new[] { "Armor", "Club", "Necklace", "Skill", "Companion", "Relic" })
                Assert.That(ui.Items(category).Any(item => skinIds.Contains(item.id)), Is.False, category + " must not contain skins.");
            foreach (string category in new[] { "Skin", "Weapon", "Appearance" })
                Assert.Throws<ArgumentException>(() => ui.GrantItem(category, new System.Random(31)), "Skin acquisition must never use the random summon API.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RelicTicketDrawSpendsExactlyOneTicketForOneRelicWithoutWalletCharges()
        {
            game.TogglePause();
            var ui = game.Ui;
            LoadServiceSnapshot(saved => ServiceSetSavedField(saved, "relicTickets", 0));
            ui.GrantRelicTickets(1);
            Assert.That(ui.RelicTickets, Is.EqualTo(1));
            int relics = ui.Items("Relic").Sum(x => x.count);
            int otherItems = new[] { "Armor", "Club", "Skill", "Companion" }.Sum(category => ui.Items(category).Sum(x => x.count));
            int diamonds = ui.Diamonds, xp = ui.SummonExperience("Relic");
            long gold = ui.Gold;
            bool freeAvailable = ui.CanFreeSummon("Relic");
            UiOpen("Shop");
            UiScrollBottom();
            Assert.That(ui.TrySummonRelicTicket(),Is.True);
            Assert.That(ui.RelicTickets, Is.Zero);
            Assert.That(ui.Items("Relic").Sum(x => x.count), Is.EqualTo(relics + 1));
            Assert.That(new[] { "Armor", "Club", "Skill", "Companion" }.Sum(category => ui.Items(category).Sum(x => x.count)), Is.EqualTo(otherItems));
            Assert.That(ui.SummonExperience("Relic"), Is.Zero,"Relics have no summon level or experience.");
            Assert.That(ui.CanFreeSummon("Relic"), Is.EqualTo(freeAvailable), "A ticket must not consume the independent daily free draw.");
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds));
            Assert.That(ui.Gold, Is.EqualTo(gold));
            Assert.That(UiNode("SummonResultCards").childCount, Is.EqualTo(1));
            ui.CloseFullscreen();
            Assert.That(ui.TrySummonRelicTicket(), Is.False, "A zero balance cannot grant another relic.");
            Assert.That(ui.Items("Relic").Sum(x => x.count), Is.EqualTo(relics + 1));
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds));
            Assert.That(ui.Gold, Is.EqualTo(gold));
            UiOpen("Shop");
            UiScrollBottom();
            Assert.That(UiRoot.GetComponentsInChildren<Button>().Any(x=>x.name=="TicketSummon_Relic_1"),Is.False,"Shared diamond buttons now spend tickets first; there is no separate single-ticket button.");
            LoadServiceSnapshot(_ => { });
            Assert.That(ui.RelicTickets, Is.Zero, "The spent ticket balance must survive save restoration.");
            yield return null;
        }
    }
}
