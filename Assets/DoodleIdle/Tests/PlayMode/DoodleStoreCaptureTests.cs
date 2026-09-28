using System;
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
        public IEnumerator CaptureInternalTestStoreScreenshots()
        {
            if (Environment.GetEnvironmentVariable("DOODLE_STORE_CAPTURES") != "1") Assert.Ignore("Store captures are opt-in.");
            bool previousLanguage = DoodleLanguage.Korean;
            DoodleLanguage.Set(true);
            try {
                // The fixture uses a disposable local profile and renders actual game/UI assets.
                game.companionsEnabled = true;
                game.summonSkillsEnabled = true;
                game.basicSkillsEnabled = game.extraSkillsEnabled = true;
                game.Ui.DebugSetMainStage(800);
                game.Ui.AddItem(game.Ui.Items("Armor").First(x => x.rarity == 6), 1);
                string[] skills = { "banana", "lightning", "arrows", "fire", "cloud", "snake", "dragon", "tornado" };
                string[] companions = { "companion_cat", "companion_slime", "companion_bat", "companion_dragon_friend", "companion_frost" };
                EquipStoreSet("Skill", skills);
                EquipStoreSet("Companion", companions);
                Assert.That(game.Ui.EquippedSkills.Count, Is.EqualTo(8));
                Assert.That(game.Ui.EquippedCompanions.Count, Is.EqualTo(5));
                UiOpen(null);
                if (game.paused) game.TogglePause();
                for (int index = 0; index < 8; index++) {
                    game.Ui.DebugSetMainStage(1 + index * 10);
                    yield return new WaitForSeconds(3.2f + index * .27f);
                    Assert.That(game.ActiveCompanions, Is.EqualTo(5));
                    Assert.That(game.Ui.EquippedSkills.Count, Is.EqualTo(8));
                    UnityEngine.Object.Destroy(CaptureFrame("store-ko-" + (index + 1).ToString("00") + ".png", 1080, 1920));
                }
                Assert.That(game.CompanionAttacks, Is.GreaterThan(0), "Capture actual combat, not an idle setup.");
            }
            finally { DoodleLanguage.Set(previousLanguage); }
        }
        void EquipStoreSet(string category, string[] ids)
        {
            foreach (var item in game.Ui.Items(category)) item.equipped = false;
            for (int i = 0; i < ids.Length; i++) {
                var item = game.Ui.Items(category).Single(x => x.id == ids[i]);
                game.Ui.AddItem(item, 1); item.slot = i; item.equipped = true;
            }
        }
    }
}
