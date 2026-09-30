using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CodeStage.AntiCheat.ObscuredTypes;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator AllDungeonsTimeOutAtThirtySecondsAndRefundExactlyOneKey()
        {
            game.TogglePause();var ui=game.Ui;
            foreach(int index in DoodleUi.DungeonIndices) {
                ui.EnterDungeon(index);
                Assert.That(ui.DungeonTimeRemaining,Is.EqualTo(30));
                var gold=ui.GoldAmount;var tickets=ui.SummonTickets(DoodleUi.DungeonTicketCategory(index));
                Assert.That(ui.TickDungeonChallenge(29.9f),Is.False);
                ui.Save();ReloadPersistedServices();
                Assert.That(ui.DungeonTimeRemaining,Is.EqualTo(.1f).Within(.001f));
                Assert.That(ui.TickDungeonChallenge(.11f),Is.True);
                Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));
                Assert.That(ServiceStateValue<ObscuredInt[]>("dungeonUsed")[index],Is.EqualTo(0));
                Assert.That(ui.GetDungeonStage(index),Is.EqualTo(0));
                Assert.That(ui.GoldAmount,Is.EqualTo(gold));
                Assert.That(ui.SummonTickets(DoodleUi.DungeonTicketCategory(index)),Is.EqualTo(tickets));
                ui.FailDungeonChallenge("중복 콜백");
                Assert.That(ServiceStateValue<ObscuredInt[]>("dungeonUsed")[index],Is.EqualTo(0));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DungeonDeathRefundsButSuccessfulClearStillConsumesOneKey()
        {
            game.TogglePause();var ui=game.Ui;
            ui.EnterDungeon(2);ui.HandlePlayerDefeat();
            Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));
            Assert.That(ServiceStateValue<ObscuredInt[]>("dungeonUsed")[2],Is.EqualTo(0));
            ui.EnterDungeon(2);DefeatActualServiceEnemies(ui.DungeonKillGoal);
            ui.TickDungeonChallenge(30);
            Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));
            Assert.That(ui.GetDungeonStage(2),Is.EqualTo(1));
            Assert.That(ServiceStateValue<ObscuredInt[]>("dungeonUsed")[2],Is.EqualTo(1));
            ui.CloseDetail();yield return null;
        }

        [UnityTest]
        public IEnumerator DungeonPauseDoesNotAdvanceDeadlineAndOldDayRefundDoesNotAddKeys()
        {
            game.TogglePause();var ui=game.Ui;ui.EnterDungeon(0);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ui.DungeonTimeRemaining,Is.EqualTo(30));
            ServiceSetSavedField(ServiceStateObject,"dungeonAttemptDay",DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"));
            // Today's quota may already contain another use after a restored prior-day attempt.
            ServiceSetSavedField(ServiceStateObject,"dungeonUsed",new[]{2,0,0,0,0,0,0,0});
            ui.FailDungeonChallenge("시간 초과");
            Assert.That(ServiceStateValue<ObscuredInt[]>("dungeonUsed")[0],Is.EqualTo(2));
        }
    }
}

