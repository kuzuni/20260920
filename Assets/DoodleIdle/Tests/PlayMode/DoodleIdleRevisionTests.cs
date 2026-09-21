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
        public IEnumerator RevisionEquipmentCapsSynthesisChainAndGodUpgrades()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Armor", "Club" })
            {
                var items = ui.Items(category);
                Assert.That(items.Count, Is.EqualTo(31));
                for (int grade = 0; grade < 7; grade++) Assert.That(items.Count(x => x.rarity == grade), Is.EqualTo(grade == 6 ? 1 : 5));
                for (int index = 0; index < items.Count - 1; index++)
                {
                    var item = items[index]; var next = items[index + 1];
                    Assert.That(ui.SynthesisTarget(item), Is.SameAs(next));
                    Assert.That(next.equipValue, Is.GreaterThan(item.equipValue));
                    item.discovered = true; item.level = 99; item.count = 20;
                    Assert.That(ui.SynthesizeItem(item), Is.Zero);
                    Assert.That(ui.UpgradeItem(item), Is.True);
                    Assert.That(item.level, Is.EqualTo(100));
                    Assert.That(ui.UpgradeItem(item), Is.False);
                    item.count = 14; int before = next.count;
                    Assert.That(ui.SynthesizeItem(item, true), Is.EqualTo(2));
                    Assert.That(item.count, Is.EqualTo(4));
                    Assert.That(item.level, Is.EqualTo(100));
                    Assert.That(next.count, Is.EqualTo(before + 2));
                    Assert.That(ui.SynthesizeItem(item), Is.Zero);
                }
                var god = items.Last(); god.level = 1000; god.count = 1000; god.discovered = true;
                Assert.That(ui.UpgradeItem(god), Is.True);
                Assert.That(god.level, Is.EqualTo(1001));
                Assert.That(ui.SynthesisTarget(god), Is.Null);
                Assert.That(ui.SynthesizeItem(god, true), Is.Zero);
            }
            UiOpen("Equipment");
            Assert.That(UiNode("Collection actions").GetChild(0).name, Is.EqualTo("일괄 합성"));
            Assert.That(UiNode("Collection inventory").GetComponentsInChildren<Text>().Count(x => x.name == "Enhancement level"), Is.EqualTo(31));
            Object.Destroy(CaptureFrame("revision-equipment-synthesis.png", 720, 1520));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionLegacyOverCapLevelsReturnCopiesOnlyOnce()
        {
            game.TogglePause();
            string saved = PlayerPrefs.GetString("DoodleUi.Collections.v1", "");
            var host = new GameObject("Migration fixture");
            try
            {
                PlayerPrefs.SetString("DoodleUi.Collections.v1", "{\"version\":2,\"items\":[{\"id\":\"armor_0\",\"count\":7,\"level\":102,\"discovered\":true}],\"stats\":[]}");
                var ui = host.AddComponent<DoodleUi>(); ui.InitCollections();
                var item = ui.Items("Armor").First(x => x.id == "armor_0");
                Assert.That(item.level, Is.EqualTo(100));
                Assert.That(item.count, Is.EqualTo(36), "The old level 100 and 101 upgrades cost 14 and 15 copies.");
                ui.SaveCollections();
                var reload = new GameObject("Migration reload fixture");
                try
                {
                    var restored = reload.AddComponent<DoodleUi>(); restored.InitCollections();
                    Assert.That(restored.Items("Armor").First(x => x.id == "armor_0").count, Is.EqualTo(36));
                }
                finally { Object.DestroyImmediate(reload); }
            }
            finally { Object.DestroyImmediate(host); PlayerPrefs.SetString("DoodleUi.Collections.v1", saved); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionCriticalLockRefundTracksPricesAndSkillSlotsStayInOneRow()
        {
            game.TogglePause(); var ui = game.Ui;
            Assert.That(ui.Critical4Unlocked, Is.False);
            long gold = ui.Gold;
            Assert.That(ui.UpgradeStat("crit4Chance", 1), Is.False);
            Assert.That(ui.Gold, Is.EqualTo(gold));
            UiOpen("Stats");
            Assert.That(UiNode("Stat crit4Chance").GetComponentsInChildren<Button>().Single().interactable, Is.False);
            GrowthLevels["crit2Chance"] = 999;
            ui.Gold = long.MaxValue;
            Assert.That(ui.StatUpgradeQuote("crit2Chance", 100, out int upgrades), Is.EqualTo(long.MaxValue));
            Assert.That(upgrades, Is.EqualTo(1));
            Assert.That(ui.UpgradeStat("crit2Chance", 100), Is.True);
            Assert.That(ui.Critical4Unlocked, Is.True);
            ui.Gold = gold;
            Assert.That(ui.UpgradeStat("crit4Chance", 1), Is.True);
            var skill = ui.Items("Skill")[0]; skill.discovered = true; skill.level = 99; skill.count = 100;
            Assert.That(ui.RefundSkill(skill), Is.Zero);
            Assert.That(ui.UpgradeItem(skill), Is.True);
            Assert.That(ui.UpgradeItem(skill), Is.False);
            var tuning = (DoodleUi.CommerceTuning)typeof(DoodleUi).GetField("commerceTuning", GrowthPrivate).GetValue(ui);
            tuning.tenCost = 200; tuning.fiftyCost = 750;
            skill.count = 7; int diamonds = ui.Diamonds;
            Assert.That(ui.SkillRefundQuote(skill), Is.EqualTo(105));
            Assert.That(ui.RefundSkill(skill), Is.EqualTo(105));
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds + 105));
            Assert.That(skill.count, Is.Zero);
            Assert.That(ui.RefundSkill(skill), Is.Zero);
            tuning.tenCost = 400; tuning.fiftyCost = 1750;
            ui.AddItem(skill, 2);
            Assert.That(ui.SkillRefundQuote(skill), Is.EqualTo(70));
            Assert.That(ui.RefundSkill(skill), Is.EqualTo(70));
            ui.AddItem(skill, 5); ui.Diamonds = int.MaxValue;
            Assert.That(ui.RefundSkill(skill), Is.Zero);
            Assert.That(skill.count, Is.EqualTo(5), "A full wallet cannot destroy unpaid copies.");
            UiOpen("Skills");
            Assert.That(UiNode("Equipped Skill").GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(8));
            Object.Destroy(CaptureFrame("revision-skills-eight-slots.png", 720, 1520));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionFarmingRepeatsWithoutEarlyRefillAndBreakthroughRequiresBoss()
        {
            game.TogglePause(); var ui = game.Ui;
            ui.ToggleBreakthroughMode();
            DefeatActualServiceEnemies(99);
            Assert.That(game.EnemyCount, Is.EqualTo(1));
            typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate).Invoke(game, null);
            Assert.That(game.EnemyCount, Is.EqualTo(1));
            DefeatActualServiceEnemies(1);
            Assert.That(ui.MainStage, Is.Zero);
            Assert.That(ui.MainStageKillProgress, Is.Zero);
            typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate).Invoke(game, null);
            Assert.That(game.EnemyCount, Is.EqualTo(100));
            Assert.That(game.BossActive, Is.False);
            ui.ToggleBreakthroughMode();
            DefeatActualServiceEnemies(100);
            Assert.That(ui.MainStage, Is.Zero);
            typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate).Invoke(game, null);
            Assert.That(game.BossActive, Is.True);
            Assert.That(game.EnemyCount, Is.EqualTo(1));
            DefeatActualServiceEnemies(1);
            Assert.That(ui.MainStage, Is.EqualTo(1));
            Assert.That(ui.MainStageRemaining, Is.EqualTo(100));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionAllTenThemesHaveThreePairedEnemiesAndCycleAfterOneThousand()
        {
            game.TogglePause();
            Assert.That(DoodleIdleGame.ThemeIndexForStage(1), Is.Zero);
            Assert.That(DoodleIdleGame.ThemeIndexForStage(100), Is.Zero);
            Assert.That(DoodleIdleGame.ThemeIndexForStage(101), Is.EqualTo(1));
            Assert.That(DoodleIdleGame.ThemeIndexForStage(1000), Is.EqualTo(9));
            Assert.That(DoodleIdleGame.ThemeIndexForStage(1001), Is.Zero);
            Assert.That(DoodleIdleGame.ThemeIndexForStage(2001), Is.Zero);
            Assert.That(game.GetComponentsInChildren<Transform>().Any(x => x.name == "Doodle boulder / solid collider"), Is.False);
            var state = typeof(DoodleUi).GetField("services", GrowthPrivate).GetValue(game.Ui);
            for (int theme = 0; theme < 10; theme++)
            {
                state.GetType().GetField("mainStage").SetValue(state, theme * 100);
                state.GetType().GetField("mainStageKillProgress").SetValue(state, 0);
                game.RequestCombatWaveReset(); game.paused = false;
                typeof(DoodleIdleGame).GetMethod("FixedUpdate", GrowthPrivate).Invoke(game, null);
                game.TogglePause();
                Assert.That(game.CurrentThemeIndex, Is.EqualTo(theme));
                game.Ui.RefreshHud();
                Assert.That(game.EnemyCount, Is.EqualTo(100));
                var frames = (Sprite[][])typeof(DoodleIdleGame).GetField("enemyWalkFrames", GrowthPrivate).GetValue(game);
                Assert.That(frames.Length, Is.EqualTo(3));
                foreach (var pair in frames)
                {
                    Assert.That(pair.Length, Is.EqualTo(2));
                    Assert.That(pair[0].rect.size, Is.EqualTo(pair[1].rect.size));
                    Assert.That(pair[0].rect, Is.Not.EqualTo(pair[1].rect));
                    Assert.That(pair[0].texture.name, Is.Not.EqualTo("Characters"));
                }
                Object.Destroy(CaptureFrame("revision-theme-" + theme + ".png", 720, 1520));
            }
            yield return null;
        }
    }
}
