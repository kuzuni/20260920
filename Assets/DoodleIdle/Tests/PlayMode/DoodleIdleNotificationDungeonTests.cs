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
        public IEnumerator DungeonSweepUsesOneKeyAndHighestClearedStageWithoutAdvancingIt()
        {
            game.TogglePause();var ui=game.Ui;
            Assert.That(ui.SweepDungeon(0),Is.False);Assert.That(ui.SweepDungeon(2),Is.False);
            Assert.That(ui.SweepDungeon(-1),Is.False);Assert.That(ui.SweepDungeon(1),Is.False);Assert.That(ui.SweepDungeon(3),Is.False);
            LoadServiceSnapshot(saved=>{ServiceSetSavedField(saved,"dungeonStages",new[]{7,0,3});ServiceSetSavedField(saved,"dungeonUsed",new[]{0,0,0});});
            UiOpen("Dungeons");yield return null;
            AssertBadge(UiNode("SweepDungeon_0"),true);AssertBadge(UiNode("SweepDungeon_2"),true);
            Object.Destroy(CaptureFrame("dungeon-sweep-buttons.png",720,1520));
            var gold=ui.GoldAmount;var reward=ui.DungeonGoldAmount(7);int main=ui.MainStage;
            long clear=ui.CareerProgress("dungeonClear"),entry=ui.CareerProgress("dungeonEnter:0");
            Assert.That(ui.SweepDungeon(0),Is.True);
            Assert.That((double)((ui.GoldAmount-gold)/reward),Is.EqualTo(1).Within(1e-9));
            Assert.That(ServiceStateValue<int[]>("dungeonUsed"),Is.EqualTo(new[]{1,0,0,0,0,0,0,0}));
            Assert.That(ui.GetDungeonStage(0),Is.EqualTo(7));Assert.That(ui.DungeonChallengeStage(0),Is.EqualTo(8));
            Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));Assert.That(ui.MainStage,Is.EqualTo(main));
            Assert.That(ui.CareerProgress("dungeonClear"),Is.EqualTo(clear+1));Assert.That(ui.CareerProgress("dungeonEnter:0"),Is.EqualTo(entry+1));
            Object.Destroy(CaptureFrame("dungeon-sweep-reward.png",720,1520));ui.CloseDetail();
            int tickets=ui.DungeonRelicTickets;
            Assert.That(ui.SweepDungeon(2),Is.True);ui.CloseDetail();
            Assert.That(ui.DungeonRelicTickets,Is.EqualTo(tickets+ui.DungeonRelicReward(3)));
            Assert.That(ui.GetDungeonStage(2),Is.EqualTo(3));Assert.That(ServiceStateValue<int[]>("dungeonUsed"),Is.EqualTo(new[]{1,0,1,0,0,0,0,0}));
            Assert.That(ui.SweepDungeon(0),Is.True);ui.CloseDetail();Assert.That(ui.SweepDungeon(0),Is.True);ui.CloseDetail();
            gold=ui.GoldAmount;Assert.That(ui.SweepDungeon(0),Is.False);Assert.That(ui.GoldAmount,Is.EqualTo(gold));
            ui.Save();ReloadPersistedServices();
            Assert.That(ServiceStateValue<int[]>("dungeonUsed"),Is.EqualTo(new[]{3,0,1,0,0,0,0,0}));
            LoadServiceSnapshot(saved=>ServiceSetSavedField(saved,"activeDungeon",2));
            Assert.That(ui.SweepDungeon(2),Is.False);
            LoadServiceSnapshot(saved=>{ServiceSetSavedField(saved,"activeDungeon",-1);ServiceSetSavedField(saved,"dungeonRelicTickets",int.MaxValue);});
            Assert.That(ui.SweepDungeon(2),Is.False);Assert.That(ServiceStateValue<int[]>("dungeonUsed")[2],Is.EqualTo(1));
            yield return null;
        }

        void AssertBadge(Transform target, bool visible)
        {
            var badge=target.GetComponent<DoodleNotificationBadge>();
            Assert.That(badge,Is.Not.Null,target.name);badge.Refresh();
            Assert.That(badge.Visible,Is.EqualTo(visible),target.name);
            var dot=(RectTransform)target.Find("Red notification dot");
            Assert.That(dot.anchorMin,Is.EqualTo(Vector2.one));Assert.That(dot.anchorMax,Is.EqualTo(Vector2.one));
            Assert.That(dot.GetComponent<Image>().raycastTarget,Is.False);
            Assert.That(dot.GetComponent<Image>().sprite,Is.SameAs(UiKit.NotificationDot));
            Assert.That(dot.GetComponent<LayoutElement>().ignoreLayout,Is.True);
            var level=target.Find("Enhancement level") as RectTransform;
            if(level)Assert.That(UiLocalBounds((RectTransform)target,level).xMax,Is.LessThan(UiLocalBounds((RectTransform)target,dot).xMin-1),"The notification must not cover the enhancement level.");
        }

        [UnityTest]
        public IEnumerator NotificationBadgesFollowClaimsAndAvailableCollectionActions()
        {
            game.TogglePause();var ui=game.Ui;
            foreach(string category in new[]{"Armor","Club","Skill","Companion","Relic","DungeonRelic"})
                foreach(var item in ui.Items(category)){item.discovered=false;item.equipped=false;item.count=0;item.level=0;}
            ui.Gold=0;
            foreach(string page in new[]{"Stats","Equipment","Skills","Companions","Relics","Quests"})AssertBadge(UiNode(page),false);
            foreach(string page in new[]{"Attendance","Roulette","Buffs","Dungeons"})AssertBadge(UiNode(page),true);
            UiOpen("Attendance");AssertBadge(UiNode("Attendance day 1"),true);AssertBadge(UiNode("Attendance day 2"),false);
            UiClick("Attendance day 1");ui.CloseDetail();AssertBadge(UiNode("Attendance"),false);
            AssertBadge(UiNode("Attendance day 2"),false);
            yield return new WaitForSecondsRealtime(1.4f);
            ui.RecordServiceProgress("kills",ServiceTestTuning.weeklyGoals[0]);UiOpen("Quests");
            foreach(string tab in new[]{"일일","반복","주간"}) {
                AssertBadge(UiNode(tab),true);UiClick(tab);
                AssertBadge(UiNode("일괄받기"),true);
                int index=tab=="일일"?0:tab=="반복"?1:2;
                AssertBadge(UiNode("받기",UiNode("Quest "+index+" 0")),true);
                Object.Destroy(CaptureFrame("notification-quest-"+index+".png",720,1520));
                ui.ClaimQuests(-1);ui.CloseDetail();AssertBadge(UiNode(tab),false);AssertBadge(UiNode("일괄받기"),false);
                yield return new WaitForSecondsRealtime(1.4f);
            }
            AssertBadge(UiNode("Quests"),false);
            UiOpen("Buffs");
            AssertBadge(UiNode("버프 활성화",UiNode("Gold buff",UiNode("Panel: 버프"))),true);
            ui.ExtendBuff(false);AssertBadge(UiNode("Buffs"),true);
            ui.ExtendBuff(true);AssertBadge(UiNode("Buffs"),false);
            AssertBadge(UiNode("버프 활성화",UiNode("Gold buff",UiNode("Panel: 버프"))),false);
            ui.Gold=100000;AssertBadge(UiNode("Stats"),true);UiOpen("Stats");
            Assert.That(UiNode("Stat attack").GetComponentsInChildren<DoodleNotificationBadge>().Length,Is.Zero);
            foreach(string category in new[]{"Armor","Skill","Companion"}) {
                var weak=ui.Items(category).First();var strong=ui.Items(category).Last();
                ui.AddItem(weak,1);weak.count=0;ui.AutoEquip(category);
                ui.AddItem(strong,1);strong.count=0;
                if(category=="Armor") {
                    var intermediate=ui.Items(category)[5];ui.AddItem(intermediate,1);intermediate.count=0;
                    Assert.That(ui.CanImproveLoadout(intermediate),Is.False,"Only the best owned armor receives a better-equipment notification.");
                    Assert.That(ui.CanImproveLoadout(strong),Is.True);
                }
                string page=category=="Armor"?"Equipment":category=="Skill"?"Skills":"Companions";
                UiOpen(page);AssertBadge(UiNode(page),true);AssertBadge(UiNode("자동장착"),true);
                AssertBadge(UiNode("Slot: "+strong.name,UiNode("Collection inventory")),true);
                UiClick("Slot: "+strong.name,UiNode("Collection inventory"));
                AssertBadge(UiNode("장착"),true);if(ui.HasOverlay)ui.CloseDetail();
                ui.AutoEquip(category);Assert.That(ui.CanImproveLoadout(strong),Is.False);AssertBadge(UiNode(page),false);
                strong.count=ui.CopiesNeeded(strong);UiOpen(page);AssertBadge(UiNode("일괄강화"),true);
                Assert.That(ui.UpgradeItem(strong),Is.True);AssertBadge(UiNode(page),false);
            }
            var armor=ui.Items("Armor").First();armor.level=100;armor.count=5;
            UiOpen("Equipment");UiClick("Slot: "+armor.name,UiNode("Collection inventory"));
            AssertBadge(UiNode("합성"),true);AssertBadge(UiNode("일괄 합성"),true);
            Object.Destroy(CaptureFrame("notification-equipment-synthesis.png",720,1520));
            var relic=ui.Items("Relic").First();ui.AddItem(relic,1);UiOpen("Relics");
            AssertBadge(UiNode("Relics"),true);AssertBadge(UiNode("일괄강화"),true);
            AssertBadge(UiNode("Relic "+relic.id).GetComponentInChildren<Button>().transform,true);
            bool success;Assert.That(ui.TryUpgradeRelic(relic,out success),Is.True);AssertBadge(UiNode("Relics"),false);
            LoadServiceSnapshot(saved=> {ServiceSetSavedField(saved,"spins",5);ServiceSetSavedField(saved,"dungeonUsed",new[]{3,0,3,3,3,3,3,3});});
            AssertBadge(UiNode("Roulette"),false);AssertBadge(UiNode("Dungeons"),false);
            ui.ClosePage();Object.Destroy(CaptureFrame("notification-main.png",720,1520));yield return null;
        }

        [UnityTest]
        public IEnumerator DungeonEntrySettlesPendingFieldGoldBeforeChangingRewardContext()
        {
            game.TogglePause();var ui=game.Ui;long before=ui.Gold;
            DefeatActualServiceEnemies(1);
            int earned=ui.GoldForMainKills(ui.CombatDifficultyStage,1);
            ui.EnterDungeon(0);
            Assert.That(ui.Gold,Is.EqualTo(before+earned),"The last field kill cannot lose its reward when entry happens before LateUpdate.");
            yield return null;
            Assert.That(ui.Gold,Is.EqualTo(before+earned),"The settled kill cannot pay twice.");
        }

        [UnityTest]
        public IEnumerator RelicDungeonUsesOrdinaryUniformRelicsAndNoExclusiveShop()
        {
            game.TogglePause();var ui=game.Ui;ui.SkipSummonAnimations=true;
            var normal=ui.Items("Relic");Assert.That(normal.Count,Is.EqualTo(8));
            Assert.That(ui.Items("DungeonRelic"),Is.Empty);Assert.That(ui.AllRelics.Count,Is.EqualTo(8));
            foreach(var item in normal){Assert.That(item.dungeonRelic,Is.False);Assert.That(ui.ItemProbability(item),Is.EqualTo(12.5));}
            int diamonds=ui.Diamonds,count=normal.Sum(x=>x.count);
            ui.GrantRelicTickets(10);Assert.That(ui.TrySummonTickets("Relic",10),Is.True);
            Assert.That(ui.RelicTickets,Is.Zero);Assert.That(normal.Sum(x=>x.count),Is.EqualTo(count+10));
            Assert.That(ui.Diamonds,Is.EqualTo(diamonds));Assert.That(ui.SummonLevel("Relic"),Is.Zero);
            ui.CloseFullscreen();UiOpen("Shop");
            Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(x=>x.name=="Summon_DungeonRelic"),Is.False);
            Assert.That(UiRoot.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("던전 유물")),Is.False);
            Assert.That(UiNode("Icon: Relic",UiNode("Summon_Relic")).GetComponent<Image>().sprite,Is.SameAs(UiKit.Art("NavPottery")));
            var scroll=UiTopScroll();scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
            Object.Destroy(CaptureFrame("unified-relic-shop.png",720,1520));
            foreach(int tab in new[]{0,1,2})Assert.That(Enumerable.Range(0,ui.QuestCount(tab)).Select(i=>ui.QuestMetric(tab,i)),Does.Not.Contain("summon:DungeonRelic"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryTicketDungeonGrantsTenThenElevenAndSweepsItsOwnHighestClear()
        {
            game.TogglePause();var ui=game.Ui;
            string[] expected={"Relic","Armor","Club","Necklace","Skill","Companion"};
            Assert.That(DoodleUi.DungeonIndices.Length,Is.EqualTo(7));
            UiOpen("Dungeons");yield return null;
            Object.Destroy(CaptureFrame("expanded-dungeons-top.png",720,1520));
            UiTopScroll().verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
            Object.Destroy(CaptureFrame("expanded-dungeons-bottom.png",720,1520));ui.ClosePage();
            for(int i=0;i<expected.Length;i++) {
                int index=i+2;string category=expected[i];
                Assert.That(DoodleUi.DungeonTicketCategory(index),Is.EqualTo(category));
                int before=ui.SummonTickets(category),main=ui.MainStage;
                Assert.That(ui.SweepDungeon(index),Is.False);
                for(int stage=1;stage<=2;stage++) {
                    ui.EnterDungeon(index);Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(index));
                    DefeatActualServiceEnemies(ui.DungeonKillGoal-1);yield return null;
                    Assert.That(ui.GetDungeonStage(index),Is.EqualTo(stage-1));
                    DefeatActualServiceEnemies(1);yield return null;
                    Assert.That(ui.ActiveDungeonIndex,Is.EqualTo(-1));Assert.That(ui.GetDungeonStage(index),Is.EqualTo(stage));
                    Assert.That(ui.SummonTickets(category),Is.EqualTo(before+(stage==1?10:21)));
                    Assert.That(UiNode("Individual rewards").GetComponentsInChildren<Image>().Any(x=>x.sprite==UiKit.Art(DoodleUi.TicketIcon(category))),Is.True);
                    if(index==5&&stage==1)Object.Destroy(CaptureFrame("necklace-dungeon-ticket-reward.png",720,1520));
                    ui.CloseDetail();
                }
                Assert.That(ui.SweepDungeon(index),Is.True);ui.CloseDetail();
                Assert.That(ui.SummonTickets(category),Is.EqualTo(before+32));Assert.That(ui.GetDungeonStage(index),Is.EqualTo(2));
                Assert.That(ServiceStateValue<int[]>("dungeonUsed")[index],Is.EqualTo(3));
                Assert.That(ui.CanEnterDungeon(index),Is.False);Assert.That(ui.SweepDungeon(index),Is.False);
                Assert.That(ui.MainStage,Is.EqualTo(main));
            }
            Assert.That(ui.CanEnterDungeon(0),Is.True);ui.Save();ReloadPersistedServices();
            foreach(int index in DoodleUi.DungeonIndices.Skip(1))Assert.That(ui.GetDungeonStage(index),Is.EqualTo(2));
            Assert.That(ui.HighestDungeonStage,Is.EqualTo(2));
            LoadServiceSnapshot(saved=>ServiceSetSavedField(saved,"day",System.DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")));
            Assert.That(ServiceStateValue<int[]>("dungeonUsed"),Is.All.Zero);
        }

        [UnityTest]
        public IEnumerator RetiredDungeonRelicsAndTicketsMigrateOnceWithoutLosingProgress()
        {
            game.TogglePause();var ui=game.Ui;
            LoadServiceSnapshot(saved=> {
                ServiceSetSavedField(saved,"dungeonStages",new[]{7,99,4});ServiceSetSavedField(saved,"dungeonUsed",new[]{2,0,1});
                ServiceSetSavedField(saved,"relicTickets",9);ServiceSetSavedField(saved,"dungeonRelicTickets",17);
                ServiceSetSavedField(saved,"questSchemaVersion",3);
                var repeat=new int[26];repeat[22]=3;repeat[23]=7;ServiceSetSavedField(saved,"repeat",repeat);
                var claimed=new bool[11];claimed[10]=true;ServiceSetSavedField(saved,"dailyClaimed",claimed);
            });
            Assert.That(ui.RelicTickets,Is.EqualTo(26));Assert.That(ServiceStateValue<int>("dungeonRelicTickets"),Is.Zero);
            Assert.That(ui.GetDungeonStage(0),Is.EqualTo(7));Assert.That(ui.GetDungeonStage(2),Is.EqualTo(4));Assert.That(ui.HighestDungeonStage,Is.EqualTo(7));
            Assert.That(ServiceStateValue<int[]>("dungeonUsed"),Is.EqualTo(new[]{2,0,1,0,0,0,0,0}));
            Assert.That(ServiceStateValue<int[]>("repeat")[22],Is.EqualTo(10));
            Assert.That(ServiceStateValue<bool[]>("dailyClaimed")[QuestIndex(0,"summon:Relic")],Is.True);
            ReloadPersistedServices();Assert.That(ui.RelicTickets,Is.EqualTo(26));Assert.That(ServiceStateValue<int[]>("repeat")[22],Is.EqualTo(10));
            PlayerPrefs.SetString("DoodleUi.Collections.v1","{\"version\":3,\"items\":[{\"id\":\"dungeon_relic_strength\",\"level\":15000,\"count\":27,\"discovered\":true},{\"id\":\"relic_strength\",\"level\":123,\"count\":5,\"discovered\":true}]}");
            ReloadDungeonCollections();
            var merged=ui.Items("Relic").Single(x=>x.id=="relic_strength");
            Assert.That(merged.level,Is.EqualTo(15123));Assert.That(merged.count,Is.EqualTo(32));Assert.That(merged.discovered,Is.True);
            ui.SaveCollections();Assert.That(PlayerPrefs.GetString("DoodleUi.Collections.v1"),Does.Not.Contain("dungeon_relic_"));
            ReloadDungeonCollections();merged=ui.Items("Relic").Single(x=>x.id=="relic_strength");
            Assert.That(merged.level,Is.EqualTo(15123));Assert.That(merged.count,Is.EqualTo(32));
            LoadServiceSnapshot(saved=>{ServiceSetSavedField(saved,"relicTickets",int.MaxValue-2);ServiceSetSavedField(saved,"dungeonRelicTickets",10);});
            Assert.That(ui.RelicTickets,Is.EqualTo(int.MaxValue));Assert.That(ServiceStateValue<int>("dungeonRelicTickets"),Is.EqualTo(8));
            Assert.That(ui.TrySpendRelicTickets(10),Is.True);Assert.That(ui.RelicTickets,Is.EqualTo(int.MaxValue-2));Assert.That(ServiceStateValue<int>("dungeonRelicTickets"),Is.Zero);
            yield return null;
        }

        void ReloadDungeonCollections()
        {
            typeof(DoodleUi).GetField("collectionTuning",ServicePrivate).SetValue(game.Ui,null);
            ((IList)typeof(DoodleUi).GetField("collectionItems",ServicePrivate).GetValue(game.Ui)).Clear();
            game.Ui.InitCollections();
        }

        [UnityTest]
        public IEnumerator DungeonEntryReplacesActualMapAndActorsAndUsesMainStageDifficulty()
        {
            var ui=game.Ui;game.enemyContactDamage=0;
            foreach(var item in ui.EquippedSkills)item.equipped=false;
            foreach(var item in ui.EquippedCompanions)item.equipped=false;
            foreach(int dungeon in new[]{0,2}) {
                UiOpen("Dungeons");ui.EnterDungeon(dungeon);yield return new WaitForFixedUpdate();yield return null;
                Assert.That(ui.ActivePage,Is.Null);Assert.That(game.CurrentThemeIndex,Is.EqualTo(dungeon==0?8:6));
                var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",ServicePrivate).GetValue(game);
                Assert.That(actors.Count,Is.GreaterThan(0));
                foreach(var actor in actors) {
                    var type=actor.GetType();var root=(GameObject)type.GetField("root").GetValue(actor);
                    string pattern=dungeon==0?"돌 수호자|미라 고양이|황금 풍뎅이":"수정 슬라임|보석 박쥐|수정 거북";
                    Assert.That(root.name,Does.Match(pattern));
                    Assert.That((float)(GameNumber)type.GetField("maxHp").GetValue(actor),Is.EqualTo(68*ui.EnemyHealthMultiplier(50)).Within(.01f));
                }
                game.TogglePause();
                typeof(DoodleIdleGame).GetMethod("ClearDamageNumbers",ServicePrivate).Invoke(game,null);
                yield return new WaitForSecondsRealtime(2.6f);
                Object.Destroy(CaptureFrame("dungeon-theme-"+dungeon+".png",720,1520));
                int coins=game.GoldCoinsEmitted;DefeatActualServiceEnemies(ui.DungeonKillGoal);
                Assert.That(game.GoldCoinsEmitted,Is.EqualTo(coins),"Caves pay their clear reward instead of showing unpaid field coin drops.");
                yield return null;ui.CloseDetail();game.TogglePause();
                yield return new WaitForFixedUpdate();yield return null;
                Assert.That(game.CurrentThemeIndex,Is.EqualTo(DoodleIdleGame.ThemeIndexForStage(ui.MainStage+1)));
            }
        }
    }
}
