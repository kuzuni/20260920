using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        void DayOneState(string key, object value) => ServiceStateObject.GetType().GetField(key).SetValue(ServiceStateObject, value);

        [UnityTest]
        public IEnumerator DayOneCurrencyClaimsMileageTransactionsAndCardPersist()
        {
            game.TogglePause(); var ui=game.Ui; int initial=ui.Diamonds;
            for(int i=0;i<30;i++) { int before=ui.Diamonds; Assert.That(ui.ClaimFreeDiamonds(),Is.True); Assert.That(ui.Diamonds-before,Is.InRange(200,1000)); ui.CloseDetail(); }
            Assert.That(ui.FreeDiamondClaimsRemaining,Is.Zero); Assert.That(ui.ClaimFreeDiamonds(),Is.False);
            Assert.That(ui.Diamonds-initial,Is.InRange(6000,30000));
            typeof(DoodleUi).GetMethod("InitCommerceExtras",ServicePrivate).Invoke(ui,null);
            Assert.That(ui.FreeDiamondClaimsRemaining,Is.Zero);
            for(int i=0;i<5;i++)Assert.That(ui.GrantConfirmedCurrencyProduct(5,"verified-test-"+i),Is.True);
            Assert.That(ui.MileageCoupons,Is.EqualTo(10));
            Assert.That(ui.GrantConfirmedCurrencyProduct(5,"verified-test-0"),Is.False);
            ui.ShowPage("Shop"); UiClick("마일리지");
            Object.Destroy(CaptureFrame("dayone-mileage-card.png",720,1560));
            int diamonds=ui.Diamonds;Assert.That(ui.ExchangeMileage(10),Is.True);ui.CloseDetail();
            Assert.That(ui.Diamonds,Is.EqualTo(diamonds+5000000));Assert.That(ui.MileageCoupons,Is.Zero);
            Assert.That(ui.ExchangeMileage(5),Is.False);
            typeof(DoodleUi).GetMethod("InitCommerceExtras",ServicePrivate).Invoke(ui,null);
            Assert.That(ui.MileageCoupons,Is.Zero);Assert.That(ui.GrantConfirmedCurrencyProduct(5,"verified-test-0"),Is.False);
            Assert.That(UiKit.Art("MileageCoupon").texture,Is.Not.SameAs(UiKit.Art("TicketCompanion").texture));
            ui.ShowPage("Shop");UiClick("재화");Object.Destroy(CaptureFrame("dayone-diamond-shop.png",720,1560));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DayOneHistorySurvivesResetPeriodsAndDefeatKeepsUnlocks()
        {
            game.TogglePause();var ui=game.Ui;
            ui.RecordMissionAction("attendance");ui.RecordMissionAction("relicAttempt");ui.RecordMissionAction("equip:Companion");
            DayOneState("mainStage",1200);DayOneState("highestMainStage",1200);DayOneState("mainStageKillProgress",70);
            int slots=ui.UnlockedSkillSlots;ui.HandlePlayerDefeat();
            Assert.That(ui.MainStage,Is.EqualTo(1199));Assert.That(ui.MainStageKillProgress,Is.Zero);
            Assert.That(ui.UnlockedSkillSlots,Is.EqualTo(slots));Assert.That(ui.MissionProgress("stage"),Is.EqualTo(1200));
            DayOneState("day","2000-01-01");ui.Save();ReloadPersistedServices();
            Assert.That(ui.CareerProgress("attendance"),Is.GreaterThanOrEqualTo(1));
            Assert.That(ui.CareerProgress("relicAttempt"),Is.GreaterThanOrEqualTo(1));
            Assert.That(ui.CareerProgress("equip:Companion"),Is.GreaterThanOrEqualTo(1));
            Assert.That(ui.MainStage,Is.EqualTo(1199));
            yield return null;
        }

        void SetDayOneProfile(int stage)
        {
            var ui=game.Ui;
            foreach(var item in GrowthTuning.items) { item.equipped=false;item.discovered=false;item.count=0;item.level=0; }
            foreach(string id in new[]{"attack","health","healthRegen","crit2Chance"})GrowthLevels[id]=stage*4;
            GrowthLevels["crit4Chance"]=0;
            foreach(var category in new[]{"Armor","Club","Skill","Companion"}) {
                var items=ui.Items(category);
                foreach(var item in items.Where(x=>x.rarity<3)) { item.discovered=true;item.level=item.rarity==0?8:item.rarity==1?3:1; }
                var epic=items.First(x=>x.rarity==3);epic.discovered=true;epic.level=1;
                if(category=="Armor"||category=="Club")epic.equipped=true;
            }
            int slot=0;
            foreach(string ability in new[]{"TetherSnake","Dumbbell","Cloud","Stone"}) {
                var skill=ui.Items("Skill").Single(x=>x.ability==ability);skill.equipped=true;skill.slot=slot++;
            }
            slot=0;foreach(var item in ui.Items("Companion").Where(x=>x.discovered).OrderByDescending(x=>x.rarity).Take(2)){item.equipped=true;item.slot=slot++;}
            foreach(var relic in ui.Items("Relic")){relic.discovered=true;relic.level=5;}
            DayOneState("mainStage",stage-1);DayOneState("highestMainStage",stage-1);DayOneState("mainStageKillProgress",0);
            DayOneState("goldExpiry",DateTime.UtcNow.AddHours(9).Ticks);DayOneState("attackExpiry",DateTime.UtcNow.AddHours(9).Ticks);
            typeof(DoodleUi).GetField("starterDamageBaseline",GrowthPrivate).SetValue(ui,1f);
        }

        [UnityTest]
        public IEnumerator DayOneGrowthBudgetAndLiveStage300Combat()
        {
            game.TogglePause();var ui=game.Ui;SetDayOneProfile(300);
            Assert.That(ui.Critical2Chance,Is.EqualTo(30).Within(.01));
            Assert.That(ui.CurrentAttackPower,Is.InRange(500000000f,2000000000f));
            long income=0;for(int stage=1;stage<=300;stage++)income+=ui.GoldForMainKills(stage,101);
            for(int stage=1;stage<=3;stage++)income+=ui.DungeonGoldReward(stage);
            income+=(22+59*11)*500;
            long cost=0;foreach(var stat in GrowthTuning.stats.Take(4))for(int level=0;level<1200;level++)cost+=(long)Math.Ceiling(stat.baseCost*Math.Pow(GrowthTuning.costGrowth,level));
            Assert.That(income/(double)cost,Is.InRange(.9,1.4),"Stage kills, cave gold and small mission rewards must fund about Lv1200, not many times that budget.");
            Debug.Log("DAYONE budget gold="+income+" cost1200="+cost+" attack="+ui.CurrentAttackPower+" hp="+ui.MaxHealth+" enemyHP="+68*ui.EnemyHealthMultiplier(300));
            Object.Destroy(CaptureFrame("dayone-stage300-stats.png",720,1560));
            game.summonSkillsEnabled=true;game.companionsEnabled=true;game.autoPlay=true;
            game.RequestCombatWaveReset();game.TogglePause();
            float start=Time.fixedTime;int hits=game.PlayerContactHits;Time.timeScale=8;
            while(ui.MainStage==299 && Time.fixedTime-start<150)yield return new WaitForFixedUpdate();
            Debug.Log("DAYONE stage300 combatSeconds="+(Time.fixedTime-start)+" stage="+ui.MainStage+" contactHits="+(game.PlayerContactHits-hits));
            Assert.That(ui.MainStage,Is.GreaterThanOrEqualTo(300),"A balanced Lv1200/Epic1 profile must clear the stage and its real boss without defeat.");
            game.TogglePause();Object.Destroy(CaptureFrame("dayone-stage300-clear.png",720,1560));
        }

        [UnityTest]
        public IEnumerator DayOneSkillLevelAndDisplayedHitUseTheSameDamageCoefficient()
        {
            game.TogglePause();var ui=game.Ui;
            GrowthLevels["crit2Chance"]=GrowthLevels["crit4Chance"]=0;
            var enemies=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
            var target=enemies[0];var hp=target.GetType().GetField("hp");
            var hit=typeof(DoodleIdleGame).GetMethod("SkillDamage",GrowthPrivate);
            foreach(var item in ui.Items("Skill")) {
                item.level=10;float before=1000000;hp.SetValue(target,before);
                hit.Invoke(game,new object[]{target,DoodleAttackPower.Skill(item.ability).hitWeight,Vector2.zero,item.ability});
                Assert.That(before-(float)hp.GetValue(target),Is.EqualTo(ui.ItemHitDamage(item)).Within(.15f),item.ability);
                float damage=ui.ItemHitDamage(item);item.level=11;Assert.That(ui.ItemHitDamage(item),Is.GreaterThan(damage));
            }
            yield return null;
        }
    }
}
