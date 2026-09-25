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
        public IEnumerator CriticalUpgradeQuestCombinesEveryTierAndMigratesUnclaimedProgressOnce()
        {
            game.TogglePause();var ui=game.Ui;
            int quest=QuestIndex(1,"statUpgrade:criticalChance");
            Assert.That(Enumerable.Range(0,ui.QuestCount(1)).Count(i=>ui.QuestMetric(1,i).StartsWith("statUpgrade:crit")),Is.EqualTo(1));
            for(int i=0;i<DoodleUi.CriticalStatIds.Length;i++) {
                for(int p=0;p<i;p++)GrowthLevels[DoodleUi.CriticalStatIds[p]]=DoodleUi.CriticalLevelCap(p);
                string id=DoodleUi.CriticalStatIds[i];GrowthLevels[id]=0;ui.GoldAmount=new GameNumber(1,1000);
                Assert.That(ui.UpgradeStat(id,3),Is.True);
                Assert.That(ServiceStateValue<int[]>("repeat")[25],Is.EqualTo((i+1)*3));
            }
            Assert.That(ui.UpgradeStat("health",5),Is.True);
            Assert.That(ServiceStateValue<int[]>("repeat")[25],Is.EqualTo(51));
            ui.GoldAmount=0;Assert.That(ui.UpgradeStat("crit131072Chance",1),Is.False);
            Assert.That(ServiceStateValue<int[]>("repeat")[25],Is.EqualTo(51));
            LoadServiceSnapshot(saved=> {
                var old=new int[25];old[15]=7;old[16]=8;
                ServiceSetSavedField(saved,"repeat",old);ServiceSetSavedField(saved,"questSchemaVersion",2);
            });
            Assert.That(ServiceStateValue<int[]>("repeat")[25],Is.EqualTo(15));
            Assert.That(ui.CanClaimQuest(1,quest),Is.True);
            UiOpen("Quests");UiClick("반복",UiNode("Quest tabs"));
            Assert.That(UiNode("Quest 1 "+quest).GetComponentsInChildren<UnityEngine.UI.Text>().Any(x=>x.text.Contains("치명타 확률 스탯")),Is.True);
            int wallet=ui.Diamonds;ui.ClaimQuests(quest);ui.CloseDetail();
            Assert.That(ui.Diamonds,Is.EqualTo(wallet+ui.QuestReward(1,quest)));
            Assert.That(ServiceStateValue<int[]>("repeat")[25],Is.EqualTo(5));
            ui.Save();ReloadPersistedServices();
            Assert.That(ServiceStateValue<int[]>("repeat")[25],Is.EqualTo(5));
            Assert.That(ui.CanClaimQuest(1,quest),Is.False);
            ServiceSetSavedField(ServiceStateObject,"mainMissionIndex",13);
            Assert.That(ui.CurrentMainMission.label,Does.Contain("치명타 확률 스탯").And.Not.Contain("x2"));
            Assert.That(ui.MainMissionProgress,Is.EqualTo(DoodleUi.CriticalStatIds.Sum(x=>(long)ui.StatLevel(x))));
            yield return null;
        }

        int QuestIndex(int tab, string metric) => Enumerable.Range(0,game.Ui.QuestCount(tab)).Single(i=>game.Ui.QuestMetric(tab,i)==metric);

        [UnityTest]
        public IEnumerator QuestActionsTrackSeparateSummonsStatsCompanionsEntriesAndClears()
        {
            game.TogglePause();var ui=game.Ui;ui.SkipSummonAnimations=true;
            string[] categories={"Armor","Club","Skill","Companion","Relic","DungeonRelic"};
            foreach(string category in categories) {
                Assert.That(ui.CanClaimQuest(0,QuestIndex(0,"summon:"+category)),Is.False);
                ui.GrantSummonTickets(category,10);
                Assert.That(category=="DungeonRelic"?ui.TrySummonDungeonRelicTickets(10):ui.TrySummon(category,10,false),Is.True);
                ui.CloseFullscreen();
                Assert.That(ui.CanClaimQuest(0,QuestIndex(0,"summon:"+category)),Is.True);
                Assert.That(ui.CanClaimQuest(1,QuestIndex(1,"summon:"+category)),Is.True);
                Assert.That(ui.CanClaimQuest(2,QuestIndex(2,"summon:"+category)),Is.False);
            }
            ui.Gold=long.MaxValue/2;
            foreach(string stat in new[]{"attack","health","healthRegen","crit2Chance","crit4Chance"}) {
                GrowthLevels[stat]=0;
                if(stat=="crit4Chance")GrowthLevels["crit2Chance"]=4000;
                Assert.That(ui.UpgradeStat(stat,10),Is.True,stat);
                Assert.That(ui.CanClaimQuest(1,QuestIndex(1,"statUpgrade:"+(stat.StartsWith("crit")?"criticalChance":stat))),Is.True,stat);
            }
            var companion=ui.Items("Companion").First();companion.discovered=true;companion.level=1;companion.count=10000;
            for(int i=0;i<10;i++)Assert.That(ui.UpgradeItem(companion),Is.True);
            Assert.That(ui.CanClaimQuest(1,QuestIndex(1,"companionUpgrade")),Is.True);
            ui.ClaimAttendance();ui.CloseDetail();
            Assert.That(ui.CanClaimQuest(0,QuestIndex(0,"attendance")),Is.True);
            Assert.That(ui.CanClaimQuest(1,QuestIndex(1,"attendance")),Is.True);
            foreach(int dungeon in new[]{0,2}) {
                ui.EnterDungeon(dungeon);
                Assert.That(ui.CanClaimQuest(0,QuestIndex(0,"dungeonEnter:"+dungeon)),Is.True);
                if(dungeon==0)Assert.That(ui.CanClaimQuest(1,QuestIndex(1,"dungeonClear")),Is.False,"Entry alone is not a repeat clear.");
                DefeatActualServiceEnemies(ui.DungeonKillGoal);yield return null;
                Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));ui.CloseDetail();
                Assert.That(ui.CanClaimQuest(1,QuestIndex(1,"dungeonClear")),Is.True);
            }
            ui.Save();ReloadPersistedServices();
            Assert.That(ui.CanClaimQuest(0,QuestIndex(0,"summon:DungeonRelic")),Is.True);
            Assert.That(ui.CanClaimQuest(1,QuestIndex(1,"companionUpgrade")),Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpandedQuestMigrationKeepsOldProgressWithoutMisassigningClaims()
        {
            game.TogglePause();var ui=game.Ui;
            LoadServiceSnapshot(saved=>{
                ServiceSetSavedField(saved,"questSchemaVersion",0);
                ServiceSetSavedField(saved,"daily",new[]{500,1000,3,1,25,20,0,100});
                ServiceSetSavedField(saved,"weekly",new[]{5000,1000,3,7,25,20,0,100});
                ServiceSetSavedField(saved,"repeat",new[]{1501,100000,3,7,25,20,0,100});
                ServiceSetSavedField(saved,"dailyClaimed",new[]{true,true,true,false});
                ServiceSetSavedField(saved,"weeklyClaimed",new[]{true,true,true,true});
            });
            Assert.That(ServiceStateValue<int[]>("repeat")[0],Is.EqualTo(1501));
            Assert.That(ServiceStateValue<int[]>("repeat")[4],Is.EqualTo(25));
            Assert.That(ServiceStateValue<bool[]>("dailyClaimed").Count(x=>x),Is.EqualTo(1));
            Assert.That(ServiceStateValue<bool[]>("weeklyClaimed").Count(x=>x),Is.EqualTo(1));
            Assert.That(ui.CanClaimQuest(0,QuestIndex(0,"dungeonEnter:0")),Is.False);
            Assert.That(ui.CanClaimQuest(0,QuestIndex(0,"summon:Armor")),Is.False,"Aggregate legacy draws cannot become six category rewards.");
            int pending=ServiceStateValue<int[]>("repeat")[0];
            LoadServiceSnapshot(saved=>{
                ServiceSetSavedField(saved,"day",DateTime.UtcNow.AddDays(-8).ToString("yyyy-MM-dd"));
                ServiceSetSavedField(saved,"week",DateTime.UtcNow.AddDays(-14).ToString("yyyy-MM-dd"));
            });
            Assert.That(ServiceStateValue<int[]>("daily"),Is.All.Zero);
            Assert.That(ServiceStateValue<int[]>("weekly"),Is.All.Zero);
            Assert.That(ServiceStateValue<bool[]>("dailyClaimed"),Is.All.False);
            Assert.That(ServiceStateValue<bool[]>("weeklyClaimed"),Is.All.False);
            Assert.That(ServiceStateValue<int[]>("repeat")[0],Is.EqualTo(pending));
            yield return null;
        }
    }
}
