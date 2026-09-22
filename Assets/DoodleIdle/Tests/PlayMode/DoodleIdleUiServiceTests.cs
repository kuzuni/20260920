using System;
using System.Collections;
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
        const BindingFlags ServicePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        object ServiceStateObject => typeof(DoodleUi).GetField("services", ServicePrivate).GetValue(game.Ui);
        T ServiceStateValue<T>(string name) => (T)ServiceStateObject.GetType().GetField(name).GetValue(ServiceStateObject);
        static void ServiceSetSavedField(object state, string name, object value) => state.GetType().GetField(name).SetValue(state, value);
        DoodleUi.ServiceTuning ServiceTestTuning => JsonUtility.FromJson<DoodleUi.ServiceTuning>(Resources.Load<TextAsset>("DoodleIdle/UI/ServicesTuning").text);

        void LoadServiceSnapshot(Action<object> configure)
        {
            // Test a real persisted profile from an earlier session without changing UTC or production goals.
            object saved = JsonUtility.FromJson(JsonUtility.ToJson(ServiceStateObject), ServiceStateObject.GetType());
            configure(saved);
            PlayerPrefs.SetString("DoodleUi.Services.v1", JsonUtility.ToJson(saved));
            typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(game.Ui, null);
        }

        void AssertSingleDiamondReward()
        {
            var icons = UiNode("Individual rewards").GetComponentsInChildren<Image>().Where(i => i.name.StartsWith("Icon: ")).ToArray();
            Assert.That(icons.Length, Is.EqualTo(1));
            Assert.That(icons[0].sprite, Is.SameAs(UiKit.Art("Diamond")), "Attendance, roulette and quests must grant only diamonds.");
        }

        [UnityTest]
        public IEnumerator UiAttendanceClaimsOnceAndRestoresUtcDailyAndWeeklyBoundaries()
        {
            game.TogglePause();
            var ui = game.Ui;
            UiOpen("Attendance");
            Assert.That(UiNode("Attendance days").childCount, Is.EqualTo(6));
            Assert.That(UiNode("Attendance day 7"), Is.Not.Null);
            int wallet = ui.Diamonds;
            long gold = ui.Gold;
            Assert.That(UiRoot.GetComponentsInChildren<Button>().Any(b => b.name == "오늘 보상 받기"), Is.False);
            Assert.That(UiNode("Attendance day 2").GetComponent<Button>().interactable, Is.False);
            Assert.That(UiNode("Attendance day 1").GetComponent<Button>().interactable, Is.True);
            UiClick("Attendance day 1");
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + ServiceTestTuning.attendance[0]));
            Assert.That(ui.Gold, Is.EqualTo(gold));
            AssertSingleDiamondReward();
            ui.ClaimAttendance();
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + ServiceTestTuning.attendance[0]), "Repeated calls cannot duplicate today's claim.");
            Assert.That(ServiceStateValue<int>("attendanceIndex"), Is.EqualTo(1));
            LoadServiceSnapshot(_ => { });
            ui.ClaimAttendance();
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + ServiceTestTuning.attendance[0]), "A restored save must retain the claimed day.");

            string yesterday = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd");
            string previousWeek = DateTime.UtcNow.Date.AddDays(-14).ToString("yyyy-MM-dd");
            LoadServiceSnapshot(saved =>
            {
                ServiceSetSavedField(saved, "day", yesterday);
                ServiceSetSavedField(saved, "week", previousWeek);
                ServiceSetSavedField(saved, "attendanceDay", yesterday);
                ServiceSetSavedField(saved, "spins", 5);
                ServiceSetSavedField(saved, "pvpUsed", 5);
                ServiceSetSavedField(saved, "dungeonUsed", new[] { 3, 2, 1 });
                ServiceSetSavedField(saved, "daily", Enumerable.Repeat(100, 8).ToArray());
                ServiceSetSavedField(saved, "weekly", Enumerable.Repeat(200, 8).ToArray());
                ServiceSetSavedField(saved, "repeat", Enumerable.Repeat(17, 8).ToArray());
                ServiceSetSavedField(saved, "dailyClaimed", Enumerable.Repeat(true, 4).ToArray());
                ServiceSetSavedField(saved, "weeklyClaimed", Enumerable.Repeat(true, 4).ToArray());
            });
            Assert.That(ServiceStateValue<string>("day"), Is.EqualTo(DateTime.UtcNow.ToString("yyyy-MM-dd")));
            Assert.That(ServiceStateValue<int>("spins"), Is.Zero);
            Assert.That(ServiceStateValue<int>("pvpUsed"), Is.Zero);
            Assert.That(ServiceStateValue<int[]>("dungeonUsed"), Is.All.EqualTo(0));
            Assert.That(ServiceStateValue<int[]>("daily"), Is.All.EqualTo(0));
            Assert.That(ServiceStateValue<int[]>("weekly"), Is.All.EqualTo(0));
            Assert.That(ServiceStateValue<bool[]>("dailyClaimed"), Is.All.EqualTo(false));
            Assert.That(ServiceStateValue<bool[]>("weeklyClaimed"), Is.All.EqualTo(false));
            Assert.That(ServiceStateValue<int[]>("repeat"), Is.All.EqualTo(17), "Repeating quest progress must survive calendar boundaries.");
            ui.CloseDetail(); ui.ClaimAttendance();
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + ServiceTestTuning.attendance[0] + ServiceTestTuning.attendance[1]));
            Assert.That(ServiceStateValue<int>("attendanceIndex"), Is.EqualTo(2), "Attendance advances to the next of seven rewards.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiRouletteRunsFiveRealAnimationsAndRejectsDuplicateAndSixthSpins()
        {
            game.TogglePause();
            var ui = game.Ui;
            var tuning = ServiceTestTuning;
            Assert.That(tuning.dailySpins, Is.EqualTo(5));
            for (int spin = 0; spin < 5; spin++)
            {
                UiOpen("Roulette");
                Assert.That(UiNode("Roulette wheel").GetComponent<CanvasRenderer>(), Is.Not.Null, "The custom roulette mesh requires a CanvasRenderer.");
                Assert.That(UiNode("Roulette pointer").GetComponent<CanvasRenderer>(), Is.Not.Null);
                var button = UiNode("돌리기").GetComponent<Button>();
                int before = ui.Diamonds;
                long gold = ui.Gold;
                UiClick("돌리기");
                int awarded = ui.Diamonds - before;
                Assert.That(tuning.roulette, Does.Contain(awarded));
                Assert.That(ServiceStateValue<int>("spins"), Is.EqualTo(spin + 1));
                button.onClick.Invoke(); // The production in-flight guard must reject a second click.
                Assert.That(ui.Diamonds, Is.EqualTo(before + awarded));
                Assert.That(ServiceStateValue<int>("spins"), Is.EqualTo(spin + 1));
                float deadline = Time.realtimeSinceStartup + 5;
                while ((bool)typeof(DoodleUi).GetField("rouletteSpinning", ServicePrivate).GetValue(ui) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That((bool)typeof(DoodleUi).GetField("rouletteSpinning", ServicePrivate).GetValue(ui), Is.False, "The real roulette animation must complete.");
                AssertSingleDiamondReward();
                Assert.That(ui.Gold, Is.EqualTo(gold));
            }
            UiOpen("Roulette");
            var exhausted = UiNode("돌리기").GetComponent<Button>();
            Assert.That(exhausted.interactable, Is.False);
            int finalWallet = ui.Diamonds;
            exhausted.onClick.Invoke();
            yield return null;
            Assert.That(ui.Diamonds, Is.EqualTo(finalWallet));
            Assert.That(ServiceStateValue<int>("spins"), Is.EqualTo(5));
            ui.Save(); LoadServiceSnapshot(_ => { });
            Assert.That(ServiceStateValue<int>("spins"), Is.EqualTo(5), "Reopening the app must not replenish same-day attempts.");
        }

        [UnityTest]
        public IEnumerator UiBuffsRejectActiveRepurchaseRestoreAndReactivateOnlyAfterExpiry()
        {
            game.TogglePause();
            var ui = game.Ui;
            var tuning = ServiceTestTuning;
            UiOpen("Buffs");
            int wallet = ui.Diamonds;
            float baseDamage = ui.UiDamageMultiplier;
            ui.ExtendBuff(true);
            Assert.That(ui.Diamonds, Is.EqualTo(wallet - tuning.buffPrice));
            Assert.That(ui.AttackBuffSeconds, Is.InRange(tuning.buffSeconds - 2, tuning.buffSeconds));
            Assert.That(ui.UiDamageMultiplier, Is.EqualTo(baseDamage * (1 + tuning.attackBuff)).Within(.001f));
            long firstExpiry = ServiceStateValue<long>("attackExpiry");
            ui.ExtendBuff(true);
            Assert.That(ServiceStateValue<long>("attackExpiry"), Is.EqualTo(firstExpiry));
            Assert.That(ui.Diamonds, Is.EqualTo(wallet - tuning.buffPrice), "Active buffs cannot be bought or extended again.");
            ui.ExtendBuff(false);
            Assert.That(ui.Diamonds, Is.EqualTo(wallet - tuning.buffPrice * 2));
            Assert.That(ui.GoldBuffMultiplier, Is.EqualTo(1 + tuning.goldBuff));
            long goldExpiry = ServiceStateValue<long>("goldExpiry");
            typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(ui, null);
            Assert.That(ServiceStateValue<long>("attackExpiry"), Is.EqualTo(firstExpiry));
            Assert.That(ServiceStateValue<long>("goldExpiry"), Is.EqualTo(goldExpiry));
            UiOpen("Buffs");
            foreach (string card in new[] { "Gold buff", "Attack buff" })
            {
                var active = UiNode("버프 활성화", UiNode(card, UiNode("Panel: 버프"))).GetComponent<Button>();
                Assert.That(active.interactable, Is.False);
                Assert.That(active.GetComponentInChildren<Text>().text, Is.EqualTo("활성화 중"));
                active.onClick.Invoke(); // Even a direct callback cannot bypass the production guard.
            }
            Assert.That(ui.Diamonds, Is.EqualTo(wallet - tuning.buffPrice * 2));
            Assert.That(ServiceStateValue<long>("attackExpiry"), Is.EqualTo(firstExpiry));
            Assert.That(ServiceStateValue<long>("goldExpiry"), Is.EqualTo(goldExpiry));
            UnityEngine.Object.Destroy(CaptureFrame("ui-buffs-active-720x1520.png", 720, 1520));
            long expiry = ServiceStateValue<long>("attackExpiry");
            ui.Diamonds = 0; ui.ExtendBuff(true);
            Assert.That(ui.Diamonds, Is.Zero);
            Assert.That(ServiceStateValue<long>("attackExpiry"), Is.EqualTo(expiry));
            LoadServiceSnapshot(saved => ServiceSetSavedField(saved, "goldExpiry", DateTime.UtcNow.AddMinutes(75).Ticks));
            ui.RefreshHud();
            var hudTime = UiNode("Gold buff", UiNode("Timed buffs")).GetComponentInChildren<Text>().text;
            Assert.That(int.Parse(hudTime.Split(':')[0]), Is.InRange(74, 75), "Long buff durations from legacy saves must not wrap at one hour.");
            ServiceSetSavedField(ServiceStateObject, "attackExpiry", DateTime.UtcNow.AddSeconds(-1).Ticks);
            ServiceSetSavedField(ServiceStateObject, "goldExpiry", DateTime.UtcNow.AddSeconds(-1).Ticks);
            RefreshServiceTestBindings(); // Keep the same open panel to verify live expiry, without rebuilding it.
            Assert.That(ui.AttackBuffSeconds, Is.Zero);
            Assert.That(ui.GoldBuffSeconds, Is.Zero);
            Assert.That(ui.AttackBuffMultiplier, Is.EqualTo(1));
            Assert.That(ui.GoldBuffMultiplier, Is.EqualTo(1));
            Assert.That(ui.UiDamageMultiplier, Is.EqualTo(baseDamage).Within(.001f));
            foreach (string card in new[] { "Gold buff", "Attack buff" })
            {
                var expired = UiNode("버프 활성화", UiNode(card, UiNode("Panel: 버프"))).GetComponent<Button>();
                Assert.That(expired.interactable, Is.True);
                Assert.That(expired.GetComponentInChildren<Text>().text, Is.EqualTo("버프 활성화"));
            }
            ui.ExtendBuff(true);
            Assert.That(ui.AttackBuffSeconds, Is.Zero, "An expired buff still requires payment.");
            ui.Diamonds = tuning.buffPrice * 2;
            UiClick("버프 활성화", UiNode("Attack buff", UiNode("Panel: 버프")));
            Assert.That(ui.Diamonds, Is.EqualTo(tuning.buffPrice));
            Assert.That(ui.AttackBuffSeconds, Is.InRange(tuning.buffSeconds - 2, tuning.buffSeconds));
            Assert.That(ui.UiDamageMultiplier, Is.EqualTo(baseDamage * (1 + tuning.attackBuff)).Within(.001f));
            UiClick("버프 활성화", UiNode("Gold buff", UiNode("Panel: 버프")));
            Assert.That(ui.Diamonds, Is.Zero);
            Assert.That(ui.GoldBuffSeconds, Is.InRange(tuning.buffSeconds - 2, tuning.buffSeconds));
            Assert.That(ui.GoldBuffMultiplier, Is.EqualTo(1 + tuning.goldBuff));
            typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(ui, null);
            Assert.That(ui.AttackBuffSeconds, Is.GreaterThan(0));
            Assert.That(ui.GoldBuffSeconds, Is.GreaterThan(0));
            yield return null;
        }

        void RefreshServiceTestBindings()
        {
            typeof(DoodleUi).GetField("nextServiceTick", ServicePrivate).SetValue(game.Ui, -1f);
            typeof(DoodleUi).GetMethod("TickServices", ServicePrivate).Invoke(game.Ui, null);
        }

        [UnityTest]
        public IEnumerator UiRepeatQuestsShowAllCompletedCyclesAndPreserveRemaindersAfterIndividualClaims()
        {
            game.TogglePause();
            var ui = game.Ui;
            var tuning = ServiceTestTuning;
            string[] metrics = { "kills", "equipmentUpgrade", "skillUpgrade", "gold" };
            int[] metricIndices = { 0, 4, 5, 1 };
            Assert.That(tuning.repeatGoals[0], Is.EqualTo(500));
            for (int i = 0; i < 4; i++) ui.RecordServiceProgress(metrics[i], tuning.repeatGoals[i] * 3 + 1);
            ui.Save();
            typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(ui, null);
            UiOpen("Quests"); UiClick("반복", UiNode("Quest tabs"));
            foreach (var size in new[] { new Vector2Int(720, 1520), new Vector2Int(900, 900) })
                UnityEngine.Object.Destroy(CaptureFrame("ui-repeat-1501-" + size.x + "x" + size.y + ".png", size.x, size.y));
            int wallet = ui.Diamonds;
            for (int i = 0; i < 4; i++)
            {
                var card = UiNode("Quest 1 " + i);
                Assert.That(card.GetComponentsInChildren<Text>().Any(t => t.text == "3회 완료 · 미수령"), Is.True);
                Assert.That(card.GetComponentsInChildren<Text>().Any(t => t.text == "1/" + UiNumber.Format(tuning.repeatGoals[i])), Is.True);
                Assert.That(UiNode("받기", card).GetComponentInChildren<Text>().text, Is.EqualTo("3회\n받기"));
                UiClick("받기", card);
                wallet += 3 * tuning.questRewards[i];
                Assert.That(ui.Diamonds, Is.EqualTo(wallet));
                AssertSingleDiamondReward();
                Assert.That(ServiceStateValue<int[]>("repeat")[metricIndices[i]], Is.EqualTo(1));
                ui.CloseDetail();
                ui.ClaimQuests(i);
                Assert.That(ui.Diamonds, Is.EqualTo(wallet), "The paid cycles cannot be claimed twice.");
                typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(ui, null);
                Assert.That(ServiceStateValue<int[]>("repeat")[metricIndices[i]], Is.EqualTo(1), "Claim must persist the remainder without an extra test-side save.");
            }
            RefreshServiceTestBindings();
            for (int i = 0; i < 4; i++)
            {
                var card = UiNode("Quest 1 " + i);
                Assert.That(card.GetComponentsInChildren<Text>().Any(t => t.text == "0회 완료 · 미수령"), Is.True);
                Assert.That(UiNode("받기", card).GetComponentInChildren<Text>().text, Is.EqualTo("진행 중"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiRepeatQuestClaimAllPaysEveryCycleAcrossAllTypesAndKeepsNextProgress()
        {
            game.TogglePause();
            var ui = game.Ui;
            var tuning = ServiceTestTuning;
            string[] metrics = { "kills", "equipmentUpgrade", "skillUpgrade", "gold" };
            int[] metricIndices = { 0, 4, 5, 1 };
            int expectedReward = 0;
            for (int i = 0; i < 4; i++)
            {
                ui.RecordServiceProgress(metrics[i], tuning.repeatGoals[i] * (i + 2) + i + 1);
                expectedReward += tuning.questRewards[i] * (i + 2);
            }
            UiOpen("Quests"); UiClick("반복", UiNode("Quest tabs"));
            int wallet = ui.Diamonds;
            UiClick("일괄받기");
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + expectedReward));
            AssertSingleDiamondReward();
            ui.CloseDetail();
            typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(ui, null);
            for (int i = 0; i < 4; i++) Assert.That(ServiceStateValue<int[]>("repeat")[metricIndices[i]], Is.EqualTo(i + 1));
            UiClick("일괄받기");
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + expectedReward));
            ui.RecordServiceProgress("kills", tuning.repeatGoals[0] - 1);
            UiClick("일괄받기");
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + expectedReward + tuning.questRewards[0]));
            Assert.That(ServiceStateValue<int[]>("repeat")[0], Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiRepeatQuestWalletLimitKeepsUnpaidCyclesWithoutIntegerOverflow()
        {
            game.TogglePause();
            var ui = game.Ui;
            var tuning = ServiceTestTuning;
            string[] metrics = { "kills", "equipmentUpgrade", "skillUpgrade", "gold" };
            int[] metricIndices = { 0, 4, 5, 1 };
            foreach (string metric in metrics) { ui.RecordServiceProgress(metric, int.MaxValue); ui.RecordServiceProgress(metric, 1); }
            ui.Diamonds = int.MaxValue - 250;
            UiOpen("Quests"); UiClick("반복", UiNode("Quest tabs")); UiClick("일괄받기");
            Assert.That(ui.Diamonds, Is.EqualTo(int.MaxValue - 50));
            Assert.That(ServiceStateValue<int[]>("repeat")[0], Is.EqualTo(int.MaxValue - tuning.repeatGoals[0] * 2));
            for (int i = 1; i < 4; i++) Assert.That(ServiceStateValue<int[]>("repeat")[metricIndices[i]], Is.EqualTo(int.MaxValue));
            ui.CloseDetail();
            typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(ui, null);
            int[] before = (int[])ServiceStateValue<int[]>("repeat").Clone();
            UiClick("일괄받기");
            Assert.That(ui.Diamonds, Is.EqualTo(int.MaxValue - 50));
            Assert.That(ServiceStateValue<int[]>("repeat"), Is.EqualTo(before), "No full reward fits, so no pending cycle may be removed.");
            ui.Diamonds = 0;
            UiClick("일괄받기");
            Assert.That(ui.Diamonds, Is.EqualTo(int.MaxValue / 100 * 100));
            long paid = 0;
            for (int i = 0; i < 4; i++)
            {
                int after = ServiceStateValue<int[]>("repeat")[metricIndices[i]];
                Assert.That(after % tuning.repeatGoals[i], Is.EqualTo(before[metricIndices[i]] % tuning.repeatGoals[i]));
                paid += ((long)before[metricIndices[i]] - after) / tuning.repeatGoals[i] * tuning.questRewards[i];
            }
            Assert.That(paid, Is.EqualTo((long)ui.Diamonds), "Every consumed cycle must have a corresponding wallet payout.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiQuestsClaimDailyAndWeeklyOnceAndConsumeOnlyCompletedRepeatCycles()
        {
            game.TogglePause();
            var ui = game.Ui;
            var tuning = ServiceTestTuning;
            UiOpen("Quests");
            int wallet = ui.Diamonds;
            ui.ClaimQuests(-1);
            Assert.That(ui.Diamonds, Is.EqualTo(wallet), "Incomplete quests cannot pay out.");
            ui.RecordServiceProgress("kills", tuning.dailyGoals[0]);
            ui.RecordServiceProgress("gold", tuning.dailyGoals[1]);
            UiClick("일괄받기");
            int dailyReward = tuning.questRewards[0] + tuning.questRewards[1];
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + dailyReward));
            AssertSingleDiamondReward();
            ui.CloseDetail(); ui.ClaimQuests(-1);
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + dailyReward));
            UiClick("반복", UiNode("Quest tabs"));
            ui.RecordServiceProgress("kills", tuning.repeatGoals[0] - tuning.dailyGoals[0]);
            ui.ClaimQuests(0);
            int firstRepeatWallet = ui.Diamonds;
            Assert.That(firstRepeatWallet, Is.EqualTo(wallet + dailyReward + tuning.questRewards[0]));
            Assert.That(ServiceStateValue<int[]>("repeat")[0], Is.Zero);
            ui.CloseDetail(); ui.ClaimQuests(0);
            Assert.That(ui.Diamonds, Is.EqualTo(firstRepeatWallet));
            ui.RecordServiceProgress("kills", tuning.repeatGoals[0] + 7);
            ui.ClaimQuests(0); ui.CloseDetail();
            Assert.That(ui.Diamonds, Is.EqualTo(firstRepeatWallet + tuning.questRewards[0]));
            Assert.That(ServiceStateValue<int[]>("repeat")[0], Is.EqualTo(7), "Unconsumed progress belongs to the next cycle.");
            UiClick("주간", UiNode("Quest tabs"));
            ui.RecordServiceProgress("kills", tuning.weeklyGoals[0]);
            int beforeWeekly = ui.Diamonds;
            ui.ClaimQuests(0); ui.CloseDetail(); ui.ClaimQuests(0);
            Assert.That(ui.Diamonds, Is.EqualTo(beforeWeekly + tuning.questRewards[0]));
            Assert.That(ServiceStateValue<bool[]>("weeklyClaimed")[0], Is.True);
            yield return null;
        }

        void DefeatActualServiceEnemies(int count)
        {
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", ServicePrivate).GetValue(game);
            var damage = typeof(DoodleIdleGame).GetMethod("Damage", ServicePrivate);
            for (int i = 0; i < count; i++)
            {
                if (actors.Count == 0) typeof(DoodleIdleGame).GetMethod("Refill", ServicePrivate).Invoke(game, null);
                damage.Invoke(game, new[] { actors[0], (object)1000000f, Vector2.zero });
            }

        }

        [UnityTest]
        public IEnumerator UiDungeonsSpendIndependentThreeAttemptsAndRewardOnlyRealKillCompletion()
        {
            game.TogglePause();var ui=game.Ui;
            int mainStage=ui.MainStage,mainProgress=ui.MainStageKillProgress,initialKills=game.Kills,requiredKills=0;
            UiOpen("Dungeons");
            var keys=UiRoot.GetComponentsInChildren<DoodleServiceSymbol>().Where(x=>x.kind=="DungeonKey").ToArray();
            Assert.That(keys.Length,Is.EqualTo(2));Assert.That(keys.Select(x=>x.accent).Distinct().Count(),Is.EqualTo(2));
            Assert.That(UiRoot.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("다이아 동굴")),Is.False);
            ui.EnterDungeon(1);Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));
            UnityEngine.Object.Destroy(CaptureFrame("dungeon-new-list.png",720,1520));
            foreach(int dungeon in new[]{0,2})for(int attempt=0;attempt<3;attempt++) {
                UiOpen("Dungeons");Assert.That(ui.DungeonChallengeStage(dungeon),Is.EqualTo(attempt+1));
                long gold=ui.Gold;int diamonds=ui.Diamonds,tickets=ui.DungeonRelicTickets;
                int goldReward=ui.DungeonGoldReward(attempt+1),ticketReward=10+attempt;
                ui.EnterDungeon(dungeon);int goal=ui.DungeonKillGoal;requiredKills+=goal;
                Assert.That(ui.ActivePage,Is.Null);Assert.That(ui.HasOverlay,Is.False);
                Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(dungeon));Assert.That(ui.CombatDifficultyStage,Is.EqualTo((attempt+1)*50));
                Assert.That(ui.Gold,Is.EqualTo(gold));Assert.That(ui.DungeonRelicTickets,Is.EqualTo(tickets));
                Assert.That(ServiceStateValue<int[]>("dungeonUsed")[dungeon],Is.EqualTo(attempt+1));
                ui.EnterDungeon(dungeon==0?2:0);Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(dungeon));
                DefeatActualServiceEnemies(goal-1);yield return null;
                Assert.That(ui.DungeonProgress,Is.EqualTo(goal-1));Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(dungeon));
                Assert.That(ui.Gold,Is.EqualTo(gold));Assert.That(ui.DungeonRelicTickets,Is.EqualTo(tickets));
                DefeatActualServiceEnemies(1);yield return null;
                Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));Assert.That(ui.HasOverlay,Is.True);
                Assert.That(ui.Gold,Is.EqualTo(gold+(dungeon==0?goldReward:0)));
                Assert.That(ui.Diamonds,Is.EqualTo(diamonds));Assert.That(ui.RelicTickets,Is.Zero);
                Assert.That(ui.DungeonRelicTickets,Is.EqualTo(tickets+(dungeon==2?ticketReward:0)));
                Assert.That(ui.GetDungeonStage(dungeon),Is.EqualTo(attempt+1));
                var icon=UiNode("Individual rewards").GetComponentsInChildren<Image>().Single(x=>x.name.StartsWith("Icon: "));
                Assert.That(icon.sprite,Is.SameAs(UiKit.Art(dungeon==0?"Gold":"DungeonPottery")));
                ui.CloseDetail();
            }
            foreach(int dungeon in new[]{0,2}){ui.EnterDungeon(dungeon);Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));}
            Assert.That(game.Kills-initialKills,Is.EqualTo(requiredKills));
            Assert.That(ui.MainStage,Is.EqualTo(mainStage));Assert.That(ui.MainStageKillProgress,Is.EqualTo(mainProgress));
            Assert.That(ServiceStateValue<int[]>("dungeonUsed"),Is.EqualTo(new[]{3,0,3}));
            ReloadPersistedServices();Assert.That(ui.HighestDungeonStage,Is.EqualTo(3));Assert.That(ui.DungeonRelicTickets,Is.EqualTo(33));
        }

        [UnityTest]
        public IEnumerator UiLocalPvpProfilesChatAndSettingsOperateWithoutClaimingServerIntegration()
        {
            game.TogglePause();
            var ui = game.Ui;
            UiOpen("Pvp");
            var ranking = UiNode("Ranking content");
            Assert.That(ranking.childCount, Is.EqualTo(100));
            Assert.That(UiRoot.GetComponentsInChildren<Text>().Any(t => t.text.Contains("서버 미연결")), Is.True);
            for (int rank = 1; rank <= 3; rank++)
            {
                var podium = UiNode("Podium rank " + rank);
                var entry = UiNode("Rank " + rank, ranking);
                var podiumArt = podium.GetComponentsInChildren<Image>().Single(i => i.name.StartsWith("Icon: "));
                var listArt = entry.GetComponentsInChildren<Image>().Single(i => i.name.StartsWith("Icon: "));
                Assert.That(podiumArt.sprite, Is.SameAs(listArt.sprite), "Each podium must show that ranked player's actual listed art.");
            }
            int wallet = ui.Diamonds;
            for (int i = 0; i < 5; i++) { ui.PlayLocalPvp(); Assert.That(ui.HasOverlay, Is.True); ui.CloseDetail(); }
            int points = ServiceStateValue<int>("pvpPoints"); ui.PlayLocalPvp();
            Assert.That(ServiceStateValue<int>("pvpUsed"), Is.EqualTo(5));
            Assert.That(ServiceStateValue<int>("pvpPoints"), Is.EqualTo(points));
            Assert.That(ui.Diamonds, Is.EqualTo(wallet));
            UiOpen("Chat");
            Assert.That(UiNode("Panel: 채팅").GetComponent<DoodleUiWindow>().full, Is.False);
            Assert.That(UiRoot.GetComponentsInChildren<Text>().Any(t => t.text.Contains("네트워크 미연결")), Is.True);
            string message = "<b>로컬 입력 확인</b>";
            UiNode("Chat input").GetComponent<InputField>().text = message;
            UiClick("보내기"); yield return null; yield return null;
            var posted = UiRoot.GetComponentsInChildren<Text>().Single(t => t.text == message);
            Assert.That(posted.supportRichText, Is.False);
            var chatScroll = UiTopScroll();
            if (chatScroll.content.rect.height > chatScroll.viewport.rect.height + 1)
                Assert.That(chatScroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.01f));
            UiOpen("Settings"); UiOpen("Chat");
            Assert.That(UiRoot.GetComponentsInChildren<Text>().Any(t => t.text == message), Is.True, "Local messages survive navigating between pages.");
            UiOpen("Settings");
            Assert.That(UiRoot.GetComponentsInChildren<Button>().Any(b => b.name == "계속하기" || b.name == "일시정지"), Is.False, "Settings must no longer expose pause controls.");
            Assert.That(game.paused, Is.True);
            Assert.That(PlayerBody().simulated, Is.False);
            Assert.That(EnemyBodies().All(b => !b.simulated), Is.True);
            UiClick("절전모드  꺼짐 · 60 FPS");
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            var slider = UiNode("배경음 slider").GetComponent<Slider>(); slider.value = .25f;
            Assert.That(ServiceStateValue<float>("music"), Is.EqualTo(.25f).Within(.001f));
            UiClick("연동하기");
            Assert.That(UiNode("Detail dim: 계정연동").GetComponentsInChildren<Text>().Any(t => t.text.Contains("서버가 연결되지 않았습니다")), Is.True);
            yield return null;
        }
    }
}
