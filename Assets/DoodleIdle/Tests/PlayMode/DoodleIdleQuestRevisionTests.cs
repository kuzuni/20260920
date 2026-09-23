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
                Assert.That(ui.CanClaimQuest(1,QuestIndex(1,"statUpgrade:"+stat)),Is.True,stat);
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
