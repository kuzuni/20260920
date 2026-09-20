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
            UiClick("오늘 보상 받기");
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
        public IEnumerator UiBuffsChargeExtendPersistAndExpireWithoutFreeMultiplier()
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
            Assert.That(ServiceStateValue<long>("attackExpiry") - firstExpiry, Is.EqualTo(TimeSpan.FromSeconds(tuning.buffSeconds).Ticks));
            ui.ExtendBuff(false);
            Assert.That(ui.Diamonds, Is.EqualTo(wallet - tuning.buffPrice * 3));
            Assert.That(ui.GoldBuffMultiplier, Is.EqualTo(1 + tuning.goldBuff));
            LoadServiceSnapshot(_ => { });
            Assert.That(ui.AttackBuffSeconds, Is.GreaterThan(tuning.buffSeconds));
            long expiry = ServiceStateValue<long>("attackExpiry");
            ui.Diamonds = 0; ui.ExtendBuff(true);
            Assert.That(ui.Diamonds, Is.Zero);
            Assert.That(ServiceStateValue<long>("attackExpiry"), Is.EqualTo(expiry));
            LoadServiceSnapshot(saved => ServiceSetSavedField(saved, "goldExpiry", DateTime.UtcNow.AddMinutes(75).Ticks));
            ui.RefreshHud();
            var hudTime = UiNode("Gold buff", UiNode("Timed buffs")).GetComponentInChildren<Text>().text;
            Assert.That(int.Parse(hudTime.Split(':')[0]), Is.InRange(74, 75), "The main buff clock must not wrap at one hour after repeated extensions.");
            LoadServiceSnapshot(saved =>
            {
                ServiceSetSavedField(saved, "attackExpiry", DateTime.UtcNow.AddSeconds(-1).Ticks);
                ServiceSetSavedField(saved, "goldExpiry", DateTime.UtcNow.AddSeconds(-1).Ticks);
            });
            Assert.That(ui.AttackBuffSeconds, Is.Zero);
            Assert.That(ui.GoldBuffSeconds, Is.Zero);
            Assert.That(ui.AttackBuffMultiplier, Is.EqualTo(1));
            Assert.That(ui.GoldBuffMultiplier, Is.EqualTo(1));
            Assert.That(ui.UiDamageMultiplier, Is.EqualTo(baseDamage).Within(.001f));
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
            if (actors.Count < count)
            {
                typeof(DoodleIdleGame).GetMethod("Refill", ServicePrivate).Invoke(game, null);
                game.TogglePause(); game.TogglePause(); // Include newly spawned bodies in the existing pause mechanism.
            }
            var damage = typeof(DoodleIdleGame).GetMethod("Damage", ServicePrivate);
            for (int i = 0; i < count; i++) damage.Invoke(game, new[] { actors[0], (object)1000000f, Vector2.zero });
        }

        [UnityTest]
        public IEnumerator UiDungeonsSpendIndependentThreeAttemptsAndRewardOnlyRealKillCompletion()
        {
            game.TogglePause();
            var ui = game.Ui;
            var tuning = ServiceTestTuning;
            Assert.That(tuning.dungeonAttempts, Is.EqualTo(3));
            int equipment = ui.Items("Armor").Sum(x => x.count) + ui.Items("Club").Sum(x => x.count);
            int skills = ui.Items("Skill").Sum(x => x.count);
            int initialKills = game.Kills;
            for (int dungeon = 0; dungeon < 3; dungeon++)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    UiOpen("Dungeons");
                    long gold = ui.Gold;
                    ui.EnterDungeon(dungeon);
                    Assert.That(ui.ActiveDungeonIndex, Is.EqualTo(dungeon));
                    Assert.That(ui.Gold, Is.EqualTo(gold), "Entering cannot grant a clear reward.");
                    Assert.That(ServiceStateValue<int[]>("dungeonUsed")[dungeon], Is.EqualTo(attempt + 1));
                    if (dungeon < 2) Assert.That(ServiceStateValue<int[]>("dungeonUsed")[dungeon + 1], Is.Zero);
                    ui.EnterDungeon((dungeon + 1) % 3);
                    Assert.That(ui.ActiveDungeonIndex, Is.EqualTo(dungeon), "Only one field challenge may run at once.");
                    DefeatActualServiceEnemies(ui.DungeonKillGoal - 1);
                    yield return null;
                    Assert.That(ui.ActiveDungeonIndex, Is.EqualTo(dungeon));
                    Assert.That(ui.DungeonProgress, Is.EqualTo(ui.DungeonKillGoal - 1));
                    DefeatActualServiceEnemies(1);
                    yield return null;
                    Assert.That(ui.ActiveDungeonIndex, Is.EqualTo(-1));
                    Assert.That(ui.HasOverlay, Is.True, "A real completion must display its reward overlay.");
                    if (dungeon == 0) Assert.That(ui.Gold, Is.GreaterThanOrEqualTo(gold + tuning.dungeonGold));
                    ui.CloseDetail();
                }
                ui.EnterDungeon(dungeon);
                Assert.That(ui.ActiveDungeonIndex, Is.EqualTo(-1), "A fourth entry must be rejected for that dungeon.");
                Assert.That(ServiceStateValue<int[]>("dungeonUsed")[dungeon], Is.EqualTo(3));
            }
            Assert.That(game.Kills - initialKills, Is.EqualTo(ui.DungeonKillGoal * 9));
            Assert.That(ui.Items("Armor").Sum(x => x.count) + ui.Items("Club").Sum(x => x.count), Is.EqualTo(equipment + 3));
            Assert.That(ui.Items("Skill").Sum(x => x.count), Is.EqualTo(skills + 3));
            Assert.That(ServiceStateValue<int[]>("dungeonUsed"), Is.All.EqualTo(3));
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
            Assert.That(UiNode("Panel: 채팅").GetComponent<DoodleUiWindow>().full, Is.True);
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
            UiClick("계속하기");
            Assert.That(game.paused, Is.False);
            Assert.That(PlayerBody().simulated, Is.True);
            UiClick("일시정지");
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
