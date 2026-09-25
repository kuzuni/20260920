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
        void ReloadPersistedServices()
        {
            // Read the save produced by the action itself. Do not copy current state
            // into PlayerPrefs here, which would conceal a missing production save.
            typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(game.Ui, null);
        }

        [UnityTest]
        public IEnumerator UiMainMissionPaysTicketsOnceAndResumesTheSavedNextObjective()
        {
            game.TogglePause();
            var ui = game.Ui;
            Assert.That(ui.AttackStatLevel, Is.Zero);
            Assert.That(ui.MainMissionNumber, Is.EqualTo(1));
            Assert.That(ui.MainMissionText, Does.Contain("공격력 Lv.15"));
            Assert.That(ui.CanClaimMainMission, Is.False);
            int initialDiamonds = ui.Diamonds;
            Assert.That(ui.ClaimMainMission(), Is.False);
            Assert.That(ui.Diamonds, Is.EqualTo(initialDiamonds));

            // Buy the required upgrades through the actual stat panel buttons.
            UiOpen("Stats"); UiClick("×10");
            var attack = UiNode("Stat attack");
            UiClick(attack.GetComponentsInChildren<Button>().Single().name, attack);
            Assert.That(ui.AttackStatLevel, Is.EqualTo(10));
            UiClick("×1");
            for (int i = 0; i < 4; i++)
            {
                attack = UiNode("Stat attack");
                UiClick(attack.GetComponentsInChildren<Button>().Single().name, attack);
            }
            Assert.That(ui.AttackStatLevel, Is.EqualTo(14));
            Assert.That(ui.CanClaimMainMission, Is.False, "Fourteen upgrades do not meet the fifteen-level objective.");
            attack = UiNode("Stat attack");
            UiClick(attack.GetComponentsInChildren<Button>().Single().name, attack);
            UiOpen(null);
            Assert.That(ui.MainMissionFraction, Is.EqualTo(1));
            UiClick("Claim main mission");
            Assert.That(ui.Diamonds, Is.EqualTo(initialDiamonds + 50));
            Assert.That(ui.SummonTickets("Armor"),Is.EqualTo(20));
            Assert.That(ui.MainMissionNumber, Is.EqualTo(2));
            Assert.That(ui.ClaimMainMission(), Is.False, "The next, unfinished mission cannot duplicate the previous payout.");
            Assert.That(ui.Diamonds, Is.EqualTo(initialDiamonds + 50));
            ReloadPersistedServices();
            Assert.That(ui.MainMissionNumber, Is.EqualTo(2));
            Assert.That(ServiceStateValue<int>("mainMissionIndex"), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("DoodleUi.Diamonds"), Is.EqualTo(initialDiamonds + 50));
            Assert.That(ui.CanClaimMainMission, Is.False);

            ui.CloseDetail();
            Assert.That(ui.TrySummonTickets("Armor",10),Is.True); ui.CloseFullscreen();
            yield return null;
            Assert.That(ui.CanClaimMainMission, Is.True, "The summon mission uses real ticket draw history.");
            ui.RefreshHud(); UiClick("Claim main mission");
            Assert.That(ui.Diamonds, Is.EqualTo(initialDiamonds + 100));
            Assert.That(ui.MainMissionNumber, Is.EqualTo(3));
            ReloadPersistedServices();
            Assert.That(ui.MainMissionNumber, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator UiRealKillsAdvanceMainStagesButActiveDungeonKillsOnlyAdvanceThatDungeon()
        {
            game.TogglePause();
            var ui = game.Ui;
            int initialStage = ui.MainStage;
            int killsToNext = ui.MainStageKillGoal - ui.MainStageKillProgress;
            DefeatActualServiceEnemies(killsToNext - 1);
            yield return null;
            Assert.That(ui.MainStage, Is.EqualTo(initialStage));
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(ui.MainStageKillGoal - 1));
            DefeatActualServiceEnemies(1);
            yield return null;
            Assert.That(ui.MainStage, Is.EqualTo(initialStage), "Clearing the stage kill goal must still require a boss.");
            Assert.That(ui.MainBossPending, Is.True);
            typeof(DoodleIdleGame).GetMethod("Refill", ServicePrivate).Invoke(game, null);
            Assert.That(game.BossActive, Is.True);
            Assert.That(game.EnemyCount, Is.EqualTo(1));
            Assert.That(game.transform.Find("Doodle world/Stage boss").localScale, Is.EqualTo(Vector3.one * 3));
            DefeatActualServiceEnemies(6); // boss, then five enemies from the next wave
            yield return null;
            Assert.That(ui.MainStage, Is.EqualTo(initialStage + 1));
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(5), "Surplus real kills carry into the next main stage.");
            // Ordinary kill progress is batched; app suspension flushes the pending profile.
            typeof(DoodleUi).GetMethod("OnApplicationPause", ServicePrivate).Invoke(ui,new object[]{true});
            ReloadPersistedServices();
            Assert.That(ui.MainStage, Is.EqualTo(initialStage + 1));
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(5));

            ui.EnterDungeon(0);
            DefeatActualServiceEnemies(ui.DungeonKillGoal - 1);
            yield return null;
            Assert.That(ui.GetDungeonStage(0), Is.Zero);
            Assert.That(ui.MainStage, Is.EqualTo(initialStage + 1));
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(5));
            DefeatActualServiceEnemies(1);
            yield return null;
            Assert.That(ui.ActiveDungeonIndex, Is.EqualTo(-1));
            Assert.That(ui.GetDungeonStage(0), Is.EqualTo(1));
            Assert.That(ui.GetDungeonStage(1), Is.Zero);
            Assert.That(ui.GetDungeonStage(2), Is.Zero);
            Assert.That(ui.HighestDungeonStage, Is.EqualTo(1));
            Assert.That(ui.MainStage, Is.EqualTo(initialStage + 1));
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(5));
            ReloadPersistedServices();
            Assert.That(ui.HighestDungeonStage, Is.EqualTo(1));
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(5));
            ui.CloseDetail();
            DefeatActualServiceEnemies(ui.MainStageKillGoal - 5);
            yield return null;
            Assert.That(ui.MainStage, Is.EqualTo(initialStage + 1));
            Assert.That(ui.MainBossPending, Is.True);
            // This fixture pauses FixedUpdate; explicitly perform its pending boss transition.
            typeof(DoodleIdleGame).GetMethod("Refill", ServicePrivate).Invoke(game, null);
            Assert.That(game.BossActive, Is.True);
            DefeatActualServiceEnemies(1); // boss
            yield return null;
            Assert.That(ui.MainStage, Is.EqualTo(initialStage + 2), "Main progression resumes after the dungeon is cleared.");
            Assert.That(ui.MainStageKillProgress, Is.Zero);
        }

        [UnityTest]
        public IEnumerator UiRelicTicketsPersistGrantsExactSpendsAndRejectInvalidOrUnaffordableAmounts()
        {
            game.TogglePause();
            var ui = game.Ui;
            Assert.That(ui.RelicTickets, Is.Zero);
            int diamonds = ui.Diamonds;
            ui.GrantRelicTickets(7);
            ReloadPersistedServices();
            Assert.That(ui.RelicTickets, Is.EqualTo(7));
            Assert.That(ui.TrySpendRelicTickets(3), Is.True);
            ReloadPersistedServices();
            Assert.That(ui.RelicTickets, Is.EqualTo(4));
            Assert.That(ui.TrySpendRelicTickets(5), Is.False);
            Assert.That(ui.TrySpendRelicTickets(0), Is.False);
            Assert.That(ui.TrySpendRelicTickets(-1), Is.False);
            ui.GrantRelicTickets(-5); ui.GrantRelicTickets(0);
            Assert.That(ui.RelicTickets, Is.EqualTo(4));
            Assert.That(ui.TrySpendRelicTickets(4), Is.True);
            ReloadPersistedServices();
            Assert.That(ui.RelicTickets, Is.Zero);
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds), "The ticket wallet is independent of diamonds.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiEveryDailyRepeatAndWeeklyQuestPaysConfiguredSmallRewards()
        {
            game.TogglePause();
            var ui = game.Ui;
            var tuning = ServiceTestTuning;
            Assert.That(tuning.dailyQuestReward, Is.EqualTo(1000));
            Assert.That(tuning.weeklyQuestReward, Is.EqualTo(3000));
            Assert.That(tuning.repeatQuestReward, Is.EqualTo(5));
            Assert.That(tuning.repeatRouletteReward, Is.EqualTo(3));
            int[][] goals = { tuning.dailyGoals, tuning.repeatGoals, tuning.weeklyGoals };
            string[] tabs = { "일일", "반복", "주간" };
            UiOpen("Quests");
            for (int tab = 0; tab < 3; tab++)
            {
                UiClick(tabs[tab], UiNode("Quest tabs"));
                var counters = ServiceStateValue<int[]>(tab == 0 ? "daily" : tab == 1 ? "repeat" : "weekly");
                System.Array.Clear(counters,0,counters.Length);
                Assert.That(ui.QuestCount(tab), Is.EqualTo(tab == 1 ? 17 : 10));
                Assert.That(Enumerable.Range(0,ui.QuestCount(tab)).Select(i=>ui.QuestMetric(tab,i)),Does.Not.Contain("gold"));
                for (int quest = 0; quest < ui.QuestCount(tab); quest++)
                {
                    string metric = ui.QuestMetric(tab,quest);
                    ui.RecordServiceProgress(metric, ui.QuestTarget(tab,quest));
                    int before = ui.Diamonds;
                    UiClick("받기", UiNode("Quest " + tab + " " + quest));
                    int reward = tab == 0 ? 1000 : tab == 2 ? 3000 : metric == "roulette" ? 3 : 5;
                    Assert.That(ui.Diamonds, Is.EqualTo(before + reward), tabs[tab] + " " + metric);
                    AssertSingleDiamondReward();
                    ui.CloseDetail();
                }
                UnityEngine.Object.Destroy(CaptureFrame("quest-expanded-"+tab+".png",720,1560));
            }
            yield return null;
        }
    }
}
