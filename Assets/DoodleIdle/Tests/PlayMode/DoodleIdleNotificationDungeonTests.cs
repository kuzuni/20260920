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
        void AssertBadge(Transform target, bool visible)
        {
            var badge=target.GetComponent<DoodleNotificationBadge>();
            Assert.That(badge,Is.Not.Null,target.name);badge.Refresh();
            Assert.That(badge.Visible,Is.EqualTo(visible),target.name);
            var dot=(RectTransform)target.Find("Red notification dot");
            Assert.That(dot.anchorMin,Is.EqualTo(Vector2.one));Assert.That(dot.anchorMax,Is.EqualTo(Vector2.one));
            Assert.That(dot.GetComponent<Image>().raycastTarget,Is.False);
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
            var relic=ui.Items("DungeonRelic").First();ui.AddItem(relic,1);UiOpen("Relics");
            AssertBadge(UiNode("Relics"),true);AssertBadge(UiNode("일괄강화"),true);
            AssertBadge(UiNode("Relic "+relic.id).GetComponentInChildren<Button>().transform,true);
            bool success;Assert.That(ui.TryUpgradeRelic(relic,out success),Is.True);AssertBadge(UiNode("Relics"),false);
            LoadServiceSnapshot(saved=> {ServiceSetSavedField(saved,"spins",5);ServiceSetSavedField(saved,"dungeonUsed",new[]{3,0,3});});
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
        public IEnumerator DungeonRelicsAreExclusiveUniformPersistentAndShareTheirOptions()
        {
            game.TogglePause();var ui=game.Ui;
            var normal=ui.Items("Relic");var dungeon=ui.Items("DungeonRelic");
            Assert.That(normal.Count,Is.EqualTo(8));Assert.That(dungeon.Count,Is.EqualTo(8));
            Assert.That(normal.Intersect(dungeon),Is.Empty);Assert.That(dungeon.All(x=>x.dungeonRelic&&x.rarity==0),Is.True);
            CollectionAssert.AreEquivalent(normal.Select(x=>x.effect),dungeon.Select(x=>x.effect));
            var random=new System.Random(71);
            for(int i=0;i<100;i++){Assert.That(ui.GrantItem("Relic",random).dungeonRelic,Is.False);Assert.That(ui.GrantItem("DungeonRelic",random).dungeonRelic,Is.True);}
            foreach(var item in dungeon)Assert.That(ui.ItemProbability(item),Is.EqualTo(12.5));
            Assert.That(ui.SummonLevel("DungeonRelic"),Is.Zero);Assert.That(ui.CanFreeSummon("DungeonRelic"),Is.False);
            Assert.That(ui.TrySummon("DungeonRelic",10,false),Is.False);Assert.That(ui.TrySummonDungeonRelicTickets(1),Is.False);
            int diamonds=ui.Diamonds,normalCount=normal.Sum(x=>x.count);
            ui.GrantDungeonRelicTickets(10);Assert.That(ui.TrySummonDungeonRelicTickets(10),Is.True);
            Assert.That(ui.DungeonRelicTickets,Is.Zero);Assert.That(dungeon.Sum(x=>x.count),Is.EqualTo(10));
            Assert.That(ui.Diamonds,Is.EqualTo(diamonds));Assert.That(normal.Sum(x=>x.count),Is.EqualTo(normalCount));
            Assert.That(ui.SummonExperience("DungeonRelic"),Is.Zero);
            ui.CloseFullscreen();ReloadPersistedServices();Assert.That(ui.DungeonRelicTickets,Is.Zero);
            foreach(var item in dungeon){item.count=0;item.level=0;item.discovered=false;}
            float gold=ui.GoldGainMultiplier;var goldRelic=dungeon.Single(x=>x.effect=="gold");
            ui.AddItem(goldRelic,1);Assert.That(ui.GoldGainMultiplier,Is.GreaterThan(gold));
            int boosted=ui.GoldForMainKills(50,500);Assert.That(ui.DungeonGoldReward(1),Is.EqualTo(boosted));
            var tuning=(DoodleUi.ServiceTuning)typeof(DoodleUi).GetField("serviceTuning",ServicePrivate).GetValue(ui);
            tuning.goldPerEnemy*=2;Assert.That(ui.DungeonGoldReward(1),Is.EqualTo(boosted*2).Within(1));
            tuning.goldStageGrowth=.01f;Assert.That(ui.DungeonGoldReward(2),Is.EqualTo(ui.GoldForMainKills(100,500)));
            Assert.That(ui.DungeonGoldReward(2),Is.GreaterThan(ui.DungeonGoldReward(1)));
            UiOpen("Shop");var row=UiNode("Summon_DungeonRelic");
            Assert.That(row.GetComponentsInChildren<Button>().Any(x=>x.name=="DungeonRelicTicketSummon1"),Is.True);
            Assert.That(UiNode("Icon: DungeonRelic",row).GetComponent<Image>().sprite,Is.SameAs(UiKit.Art("DungeonPottery")));
            Assert.That(UiNode("Icon: Skill",UiNode("Summon_Skill")).GetComponent<Image>().sprite,Is.SameAs(UiKit.Art("SkillMeteor")));
            Assert.That(UiNode("Icon: Relic",UiNode("Summon_Relic")).GetComponent<Image>().sprite,Is.SameAs(UiKit.Art("NavPottery")));
            var scroll=UiTopScroll();scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
            Object.Destroy(CaptureFrame("dungeon-relic-shop.png",720,1520));
            ui.SaveCollections();var saved=PlayerPrefs.GetString("DoodleUi.Collections.v1");Assert.That(saved,Does.Contain(goldRelic.id));yield return null;
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
                    Assert.That((float)type.GetField("maxHp").GetValue(actor),Is.EqualTo(68*ui.EnemyHealthMultiplier(50)).Within(.01f));
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
