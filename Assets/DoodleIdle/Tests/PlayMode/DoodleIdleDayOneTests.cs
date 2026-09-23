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
        public IEnumerator SmoothGrowthTangentsPreserveKnotsAndDriveActualGold()
        {
            game.TogglePause(); var ui = game.Ui;
            var curve = new DoodleGrowthCurve();
            curve.SetPoint(10, 3, 0); curve.SetPoint(20, 4, 0);
            Assert.That(curve.Evaluate(5,0), Is.EqualTo(2));
            curve.SetInterpolation(DoodleCurveInterpolation.Smooth,0);
            Assert.That(curve.Evaluate(5,0), Is.EqualTo(2.0833333333).Within(.000001));
            double previous = 1;
            for (double x=0;x<=20;x+=.5) {
                double value = curve.Evaluate(x,0);
                Assert.That(value, Is.InRange(previous,4d)); previous=value;
            }
            double smoothMidpoint = curve.Evaluate(5,0);
            curve.SetInterpolation(DoodleCurveInterpolation.Manual,0);
            Assert.That(curve.Evaluate(5,0), Is.EqualTo(smoothMidpoint).Within(.000001), "Enabling handles must preserve the automatic curve initially.");
            curve.SetTangent(10,0,true,0);
            Assert.That(curve.Evaluate(5,0), Is.EqualTo(2.25).Within(.000001));
            Assert.That(curve.Evaluate(10,0), Is.EqualTo(3), "Handles change the curve, not the knot value.");
            curve.SetTangent(10,0,false,-.5);
            Assert.That(curve.Evaluate(15,0), Is.EqualTo(2.75).Within(.000001));
            curve.SetPoint(10,3,0);
            Assert.That(curve.FindPoint(10).outTangent, Is.EqualTo(-.5));
            curve.MovePoint(10,12,3,0);
            Assert.That(curve.FindPoint(12).inTangent, Is.Zero);
            Assert.That(curve.FindPoint(12).outTangent, Is.EqualTo(-.5));
            var copy = curve.Copy(); copy.SetTangent(12,0,false,2);
            Assert.That(curve.FindPoint(12).outTangent, Is.EqualTo(-.5), "Snapshots must own independent handles.");
            var restored = JsonUtility.FromJson<DoodleGrowthCurve>(JsonUtility.ToJson(curve));
            Assert.That(restored.interpolation, Is.EqualTo(DoodleCurveInterpolation.Manual));
            Assert.That(restored.Evaluate(15,0), Is.EqualTo(curve.Evaluate(15,0)).Within(.000001));
            var oldData = JsonUtility.FromJson<DoodleGrowthCurve>("{\"points\":[{\"position\":10,\"factor\":3}]}");
            Assert.That(oldData.Evaluate(5,0), Is.EqualTo(2), "Existing saved linear curves retain their shape.");
            var angles = new DoodleGrowthCurve(); angles.SetPoint(10,3,0);
            angles.SetTangentAngle(0,0,false,45);
            angles.SetTangentAngle(10,0,true,-45);
            Assert.That(angles.interpolation, Is.EqualTo(DoodleCurveInterpolation.Manual));
            Assert.That(angles.GetTangent(0,0,false), Is.EqualTo(1).Within(.000001));
            Assert.That(angles.GetTangent(10,0,true), Is.EqualTo(-1).Within(.000001));
            Assert.That(angles.Evaluate(5,0), Is.EqualTo(4.5).Within(.000001), "Angles must change interpolated rewards while keeping endpoint values.");
            Assert.That(angles.Evaluate(0,0), Is.EqualTo(1));
            Assert.That(angles.Evaluate(10,0), Is.EqualTo(3));
            angles.SetTangentAngle(10,0,true,0);
            Assert.That(angles.GetTangent(10,0,true), Is.Zero);
            angles.SetTangentAngle(10,0,true,-180);
            Assert.That(angles.GetTangentAngle(10,0,true), Is.EqualTo(-89.9).Within(.000001));
            angles.SetTangentAngle(0,0,false,180);
            angles.SetTangentAngle(0,0,false,double.NaN);
            angles.SetTangentAngle(0,0,false,double.PositiveInfinity);
            var restoredAngles = JsonUtility.FromJson<DoodleGrowthCurve>(JsonUtility.ToJson(angles));
            Assert.That(restoredAngles.GetTangentAngle(0,0,false), Is.EqualTo(89.9).Within(.000001));
            Assert.That(restoredAngles.GetTangentAngle(10,0,true), Is.EqualTo(-89.9).Within(.000001));
            curve.SetTangentAngle(12,0,false,-20);
            var tuning = ui.ReadBalanceTuning(); tuning.goldStageGrowth = 0; tuning.goldCurve = curve;
            ui.ApplyBalanceTuning(tuning);
            for (int stage=1;stage<=30;stage++)
                Assert.That(ui.GoldForMainKills(stage,1), Is.EqualTo((int)Math.Round(tuning.goldPerEnemy*curve.Evaluate(stage,1)*ui.GoldGainMultiplier*ui.GoldBuffMultiplier)));
            curve.SetTangent(12,0,false,-1000);
            Assert.That(curve.Evaluate(15,0), Is.Zero, "An extreme handle cannot create negative rewards.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EditableGrowthCurvesDriveRewardsEnemiesAndSharedStatCosts()
        {
            game.TogglePause(); var ui = game.Ui;
            var original = ui.ReadBalanceTuning();
            var tuning = ui.ReadBalanceTuning();
            double normalGold = 10 * Math.Pow(1d + tuning.goldStageGrowth, 50);
            Assert.That(DoodleUi.GoldForMainKills(tuning, 51, 1), Is.EqualTo((int)Math.Round(normalGold)));
            Assert.That(DoodleUi.EnemyHealthMultiplier(tuning, 51) * 68, Is.EqualTo(68*Math.Pow(1d+tuning.enemyHealthStageGrowth,50)).Within(.01));
            tuning.goldCurve.SetPoint(51, 3, 1);
            Assert.That(tuning.goldCurve.Evaluate(26, 1), Is.EqualTo(2));
            Assert.That(tuning.goldCurve.Evaluate(80, 1), Is.EqualTo(3));
            Assert.That(DoodleUi.GoldForMainKills(tuning, 51, 1), Is.EqualTo((int)Math.Round(normalGold*3)));
            Assert.That(ui.GoldForMainKills(51, 1), Is.EqualTo(DoodleUi.GoldForMainKills(original,51,1,(double)ui.GoldGainMultiplier*ui.GoldBuffMultiplier)));
            tuning.enemyHealthCurve.SetPoint(51, 2, 1);
            tuning.enemyDamageCurve.SetPoint(70, .5, 1);
            Assert.That(DoodleUi.EnemyDamageMultiplier(tuning,1), Is.Zero);
            Assert.That(DoodleUi.EnemyDamageMultiplier(tuning,70)*64, Is.EqualTo(50).Within(.001));
            Assert.That(DoodleUi.EnemyDamageMultiplier(tuning,71)*64, Is.EqualTo(51).Within(.001));
            ui.ApplyBalanceTuning(tuning);
            Assert.That(ui.EnemyHealthMultiplier(51)*68, Is.EqualTo(136*Math.Pow(1d+tuning.enemyHealthStageGrowth,50)).Within(.01));
            Assert.That(ui.DungeonGoldReward(1), Is.EqualTo(DoodleUi.GoldForMainKills(tuning,50,500,(double)ui.GoldGainMultiplier*ui.GoldBuffMultiplier)));
            var copied = ui.ReadBalanceTuning();
            copied.goldCurve.SetPoint(51, 9, 1);
            Assert.That(ui.ReadBalanceTuning().goldCurve.Evaluate(51,1), Is.EqualTo(3), "Editing a snapshot must not change the live curve.");
            copied.goldCurve.SetPoint(21, 2, 1);
            copied.goldCurve.SetPoint(51, 4, 1);
            Assert.That(copied.goldCurve.points.Length, Is.EqualTo(2), "Adding at the same step replaces rather than duplicates the point.");
            Assert.That(copied.goldCurve.points[0].position, Is.EqualTo(21));
            copied.goldCurve.RemovePoint(21);
            Assert.That(copied.goldCurve.points.Length, Is.EqualTo(1));
            var roundTrip = JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(tuning));
            Assert.That(DoodleUi.GoldForMainKills(roundTrip,51,500), Is.EqualTo(DoodleUi.GoldForMainKills(tuning,51,500)));
            var costs = ui.ReadStatCostTuning();
            costs.commonCurve.SetPoint(10, 3, 0); costs.critical2Curve.SetPoint(10, 2, 0);
            costs.critical4Curve.SetPoint(10, 5, 0);
            ui.ApplyStatCostTuning(costs);
            foreach (string id in new[] { "attack", "health", "healthRegen", "crit2Chance" }) {
                GrowthLevels[id] = 10;
                Assert.That(ui.StatUpgradeQuote(id,1,out _), Is.EqualTo(DoodleUi.StatUpgradePrice(costs,id,10)));
            }
            Assert.That(ui.StatUpgradeQuote("attack",1,out _), Is.EqualTo(ui.StatUpgradeQuote("healthRegen",1,out _)));
            Assert.That(ui.StatUpgradeQuote("crit2Chance",1,out _), Is.LessThan(ui.StatUpgradeQuote("attack",1,out _)));
            var restoredCosts = JsonUtility.FromJson<UiStatCostTuning>(JsonUtility.ToJson(costs));
            Assert.That(DoodleUi.StatUpgradePrice(restoredCosts,"crit4Chance",10), Is.EqualTo(DoodleUi.StatUpgradePrice(costs,"crit4Chance",10)));
            costs.commonCurve.SetPoint(10, 100d/DoodleGrowthCurve.Exponential(costs.commonBaseCost,costs.commonGrowth,10), 0);
            Assert.That(DoodleUi.StatUpgradePrice(costs,"attack",10), Is.EqualTo(100), "A graph target of 100 must not round floating point dust up to 101.");
            tuning.goldCurve.SetPoint(int.MaxValue, 0, 1);
            Assert.That(DoodleUi.GoldForMainKills(tuning,int.MaxValue,500), Is.Zero);
            tuning.goldCurve.RemovePoint(int.MaxValue);
            Assert.That(DoodleUi.GoldForMainKills(tuning,int.MaxValue,500), Is.EqualTo(int.MaxValue));
            Assert.That(DoodleUi.StatUpgradePrice(costs,"crit4Chance",int.MaxValue), Is.EqualTo(long.MaxValue));
            ui.ApplyBalanceTuning(original);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ManualBalanceControlsApplyToLiveEnemiesGoldCavesAndDiamondWallet()
        {
            game.TogglePause(); var ui = game.Ui;
            int stage = ui.CombatDifficultyStage, progress = ui.MainStageKillProgress;
            var original = ui.ReadBalanceTuning();
            var draft = ui.ReadBalanceTuning();
            float health = ui.EnemyHealthMultiplier(stage), damage = ui.EnemyDamageMultiplier(stage);
            int gold = ui.GoldForMainKills(50, 500);
            draft.goldPerEnemy *= 3;
            draft.enemyStartingHealth *= 4;
            draft.enemyStartingDamage *= 2; draft.earlyEnemyDamageMax *= 2;
            Assert.That(ui.EnemyHealthMultiplier(stage), Is.EqualTo(health), "An editable snapshot must not change the live game.");
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var actor = actors[0]; var type = actor.GetType();
            var maxHp = type.GetField("maxHp"); var hp = type.GetField("hp");
            float originalHp = (float)maxHp.GetValue(actor);
            hp.SetValue(actor, originalHp * .4f);
            ui.ApplyBalanceTuning(draft);
            Assert.That(ui.EnemyHealthMultiplier(stage), Is.EqualTo(health * 4).Within(.001));
            Assert.That(ui.EnemyDamageMultiplier(stage), Is.EqualTo(damage * 2).Within(.001));
            Assert.That((float)maxHp.GetValue(actor), Is.EqualTo(originalHp * 4).Within(.01));
            Assert.That((float)hp.GetValue(actor) / (float)maxHp.GetValue(actor), Is.EqualTo(.4f).Within(.0001));
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(progress));
            Assert.That(ui.GoldForMainKills(50, 500), Is.EqualTo(gold * 3).Within(2));
            Assert.That(ui.DungeonGoldReward(1), Is.EqualTo(ui.GoldForMainKills(50, 500)));
            draft.goldStageGrowth = .1f; draft.enemyHealthStageGrowth = .2f; draft.enemyDamageStageGrowth = .3f;
            ui.ApplyBalanceTuning(draft);
            Assert.That(ui.EnemyHealthMultiplier(51), Is.EqualTo(4*Math.Pow(1d+draft.enemyHealthStageGrowth,50)).Within(2));
            Assert.That(ui.EnemyDamageMultiplier(71) * 64, Is.EqualTo(260).Within(.001));
            Assert.That(ui.EnemyDamageMultiplier(1), Is.Zero);
            draft.enemyStartingDamage = 0; draft.earlyEnemyDamageMax = 0; ui.ApplyBalanceTuning(draft);
            Assert.That(ui.EnemyDamageMultiplier(1000), Is.Zero);
            // Start values must affect live actors/payouts, not just an editor-only preview.
            draft = ui.ReadBalanceTuning();
            draft.goldPerEnemy = 25;
            draft.enemyStartingHealth = 136;
            draft.enemyStartingDamage = 12;
            draft.earlyEnemyDamageEndStage = 11; draft.earlyEnemyDamageMax = 32;
            ui.ApplyBalanceTuning(draft);
            Assert.That(ui.EnemyHealthMultiplier(1) * 68, Is.EqualTo(136).Within(.001));
            Assert.That((float)maxHp.GetValue(actor), Is.EqualTo(68 * DoodleUi.EnemyHealthMultiplier(draft, stage)).Within(.01));
            Assert.That((float)hp.GetValue(actor) / (float)maxHp.GetValue(actor), Is.EqualTo(.4f).Within(.0001));
            Assert.That(ui.EnemyDamageMultiplier(0) * 64, Is.EqualTo(12).Within(.001));
            Assert.That(ui.EnemyDamageMultiplier(1) * 64, Is.EqualTo(12).Within(.001));
            Assert.That(ui.EnemyDamageMultiplier(6) * 64, Is.EqualTo(22).Within(.001));
            Assert.That(ui.EnemyDamageMultiplier(11) * 64, Is.EqualTo(32).Within(.001));
            Assert.That(ui.EnemyDamageMultiplier(12) * 64, Is.EqualTo(41.6).Within(.001));
            Assert.That(ui.ReadBalanceTuning().goldPerEnemy, Is.EqualTo(25));
            foreach (int previewStage in new[] { 1, 6, 11, 12, 70, 300 }) {
                Assert.That(ui.GoldForMainKills(previewStage, 1), Is.EqualTo(DoodleUi.GoldForMainKills(draft, previewStage, 1, (double)ui.GoldGainMultiplier * ui.GoldBuffMultiplier)));
                Assert.That(ui.EnemyDamageMultiplier(previewStage), Is.EqualTo(DoodleUi.EnemyDamageMultiplier(draft, previewStage)));
            }
            Assert.That(ui.DungeonGoldReward(1), Is.EqualTo(DoodleUi.GoldForMainKills(draft, 50, 500, (double)ui.GoldGainMultiplier * ui.GoldBuffMultiplier)));
            var roundTrip = JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(ui.ReadBalanceTuning()));
            Assert.That(roundTrip.enemyStartingHealth, Is.EqualTo(136));
            Assert.That(roundTrip.enemyStartingDamage, Is.EqualTo(12));
            Assert.That(roundTrip.earlyEnemyDamageEndStage, Is.EqualTo(11));
            Assert.That(roundTrip.earlyEnemyDamageMax, Is.EqualTo(32));
            ui.ApplyBalanceTuning(original);
            int wallet = ui.Diamonds;
            Assert.That(ui.GrantDebugDiamonds(10000), Is.EqualTo(10000));
            Assert.That(ui.Diamonds, Is.EqualTo(wallet + 10000));
            Assert.That(PlayerPrefs.GetInt("DoodleUi.Diamonds"), Is.EqualTo(ui.Diamonds));
            Assert.That(ui.GrantDebugDiamonds(-1), Is.Zero);
            ui.Diamonds = int.MaxValue - 3;
            Assert.That(ui.GrantDebugDiamonds(10000), Is.EqualTo(3));
            Assert.That(ui.Diamonds, Is.EqualTo(int.MaxValue));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TierRatiosLinearStatsAndRelicStepsMatchTheManualBalanceRules()
        {
            game.TogglePause(); var ui=game.Ui;
            foreach (var pair in new[] { new[] { "attack", "5" }, new[] { "health", "40" }, new[] { "healthRegen", "1" } }) {
                foreach (int level in new[] { 0, 15, 1200, 5000 }) {
                    GrowthLevels[pair[0]]=level; float before=ui.StatValue(pair[0]);
                    GrowthLevels[pair[0]]=level+1;
                    Assert.That(ui.StatValue(pair[0])-before,Is.EqualTo(int.Parse(pair[1])),pair[0]);
                }
                GrowthLevels[pair[0]]=0;
            }
            foreach (string category in new[] { "Skill", "Companion", "Armor", "Club" }) {
                var items=ui.Items(category).OrderBy(x=>x.rarity).ThenBy(x=>x.tier).ToArray();
                foreach(int level in new[] { 1, 50, 100 }) {
                    foreach(var item in items){item.discovered=true;item.equipped=false;item.level=level;}
                    double previous=0; int previousGrade=-1;
                    foreach(var item in items) {
                        item.equipped=true;
                        double value=category=="Armor"?ui.MaxHealth:category=="Club"?ui.CurrentAttackPower:ui.ItemExpectedDps(item);
                        if(previous>0)Assert.That(value/previous,Is.EqualTo(item.rarity==previousGrade?1.1:2.5).Within(.0002),category+" "+item.name+" Lv."+level);
                        previous=value;previousGrade=item.rarity;item.equipped=false;
                    }
                }
            }
            var effect=typeof(DoodleUi).GetMethod("EffectBonus",GrowthPrivate);
            foreach(var relic in ui.AllRelics) {
                relic.discovered=true;relic.level=10;
                float before=(float)effect.Invoke(ui,new object[]{relic.effect,null,true});
                relic.level++;
                Assert.That((float)effect.Invoke(ui,new object[]{relic.effect,null,true})-before,Is.EqualTo(1).Within(.001),relic.id);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShopBadgesTrackFreeDiamondsAndEveryFreeSummonPool()
        {
            game.TogglePause();var ui=game.Ui;bool skip=ui.SkipSummonAnimations;ui.SkipSummonAnimations=true;
            try {
                UiOpen("Shop");AssertBadge(UiNode("Shop"),true);AssertBadge(UiNode("재화",UiNode("ShopTabs")),true);
                UiClick("재화",UiNode("ShopTabs"));
                AssertBadge(UiNode("Free diamond card").GetComponentInChildren<UnityEngine.UI.Button>().transform,true);
                for(int i=0;i<30;i++){Assert.That(ui.ClaimFreeDiamonds(),Is.True);ui.CloseDetail();}
                AssertBadge(UiNode("Free diamond card").GetComponentInChildren<UnityEngine.UI.Button>().transform,false);
                AssertBadge(UiNode("재화",UiNode("ShopTabs")),false);
                AssertBadge(UiNode("Shop"),true);
                UiClick("뽑기",UiNode("ShopTabs"));
                foreach(string category in new[]{"Armor","Club","Skill","Companion","Relic"}) {
                    AssertBadge(UiNode("무료 5회\n뽑기",UiNode("Summon_"+category)),true);
                    for(int i=0;i<3;i++){Assert.That(ui.TrySummon(category,5,true),Is.True);ui.CloseFullscreen();}
                    AssertBadge(UiNode("무료 5회\n뽑기",UiNode("Summon_"+category)),false);
                    yield return null;
                }
                AssertBadge(UiNode("Shop"),false);
                Object.Destroy(CaptureFrame("shop-free-rewards-exhausted.png",720,1560));
            }
            finally {ui.SkipSummonAnimations=skip;}
        }

        [UnityTest]
        public IEnumerator EveryHigherAbilityGradeOutdamagesThePreviousGradeAtMaximumEnhancement()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Skill", "Companion" }) {
                var items = ui.Items(category);
                foreach (var item in items) { item.discovered = true; item.level = 1; item.equipped = false; }
                for (int grade = 0; grade < 5; grade++) {
                    var lower = items.Where(x => x.rarity == grade).ToArray();
                    var higher = items.Where(x => x.rarity == grade + 1).ToArray();
                    foreach (var item in lower) item.level = ui.ItemMaxLevel(item);
                    foreach (var item in higher) item.level = 1;
                    double strongestLower = 0, weakestHigher = double.MaxValue;
                    foreach (var item in lower) {
                        item.equipped = true;
                        strongestLower = Math.Max(strongestLower, ui.ItemExpectedDps(item));
                        item.equipped = false;
                    }
                    foreach (var item in higher) {
                        item.equipped = true;
                        weakestHigher = Math.Min(weakestHigher, ui.ItemExpectedDps(item));
                        item.equipped = false;
                    }
                    Assert.That(weakestHigher, Is.GreaterThan(strongestLower * 1.05), category + " grade " + (grade + 1) + " Lv.1 must beat every max-level item in grade " + grade);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadoutAutoEquipPrefersRarityThenDpsAndShowsEnhancementLevels()
        {
            game.TogglePause(); var ui = game.Ui;
            DayOneState("mainStage", 0); DayOneState("highestMainStage", 0);
            foreach (var armor in ui.Items("Armor")) { armor.discovered = armor.equipped = false; }
            foreach (string category in new[] { "Skill", "Companion" }) {
                var items = ui.Items(category);
                foreach (var item in items) { item.discovered = item.equipped = false; item.count = 0; }
                var normal = items.First(x => x.rarity == 0);
                var advanced = items.First(x => x.rarity == 1);
                normal.discovered = normal.equipped = true; normal.level = 100;
                advanced.discovered = true; advanced.level = 1;
                Assert.That(ui.CanImproveLoadout(advanced), Is.True);
                ui.AutoEquip(category);
                Assert.That(advanced.equipped, Is.True, "Higher rarity takes priority over a highly enhanced normal item.");
                Assert.That(normal.equipped, Is.False);
                Assert.That(ui.CanImproveLoadout(normal), Is.False, "Notification and auto-equip must agree.");
                foreach (var item in items.Where(x => x.rarity == 1)) { item.discovered = true; item.level = 1; }
                ui.AutoEquip(category);
                var best = items.Where(x => x.discovered && x.rarity == 1).OrderByDescending(ui.ItemExpectedDps).First();
                Assert.That(best.equipped, Is.True, "Within the same rarity use actual estimated skill/companion DPS.");
                ui.ShowPage(category == "Skill" ? "Skills" : "Companions");
                var levels = UiNode("Collection inventory").GetComponentsInChildren<UnityEngine.UI.Text>().Where(x => x.name == "Enhancement level").ToArray();
                Assert.That(levels.Length, Is.EqualTo(items.Count));
                Assert.That(levels.Any(x => x.text == "Lv.100"), Is.True);
                Assert.That(UiNode("Equipped " + category).GetComponentsInChildren<UnityEngine.UI.Text>().Any(x => x.name == "Enhancement level" && x.text == "Lv.1"), Is.True);
                Object.Destroy(CaptureFrame("enhancement-levels-" + category + ".png", 720, 1560));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiPaidSummonsSpendTicketsFirstAndOnlyChargeTheRemainder()
        {
            game.TogglePause(); var ui = game.Ui;
            bool oldSkip = ui.SkipSummonAnimations; ui.SkipSummonAnimations = true;
            try {
                foreach (string category in new[] { "Armor", "Club", "Skill", "Companion", "Relic" }) {
                    Assert.That(ui.SummonTickets(category), Is.Zero);
                    ui.GrantSummonTickets(category, 63); ui.Diamonds = 0;
                    int initial = ui.Items(category).Sum(x => x.count);
                    UiOpen("Shop");
                    UiClick("50회 뽑기", UiNode("Summon_" + category));
                    Assert.That(ui.SummonTickets(category), Is.EqualTo(13), category);
                    Assert.That(ui.Diamonds, Is.Zero);
                    Assert.That(ui.Items(category).Sum(x => x.count), Is.EqualTo(initial + 50));
                    UiClick("10회 뽑기", UiNode("Fullscreen: 뽑기 결과"));
                    Assert.That(ui.SummonTickets(category), Is.EqualTo(3));
                    Assert.That(ui.Diamonds, Is.Zero);

                    var ten = UiNode("PaidSummon10", UiNode("Fullscreen: 뽑기 결과"));
                    Assert.That(UiNode("SummonTicketCost", ten).GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("3장"));
                    int remainder = ui.SummonCost(category, 7);
                    Assert.That(UiNode("SummonDiamondCost", ten).GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo(UiNumber.Format(remainder)));
                    ui.Diamonds = remainder - 1;
                    long progress = ui.CareerProgress("summon:" + category);
                    Assert.That(ui.TrySummon(category, 10, false), Is.False);
                    Assert.That(ui.Diamonds, Is.EqualTo(remainder - 1));
                    Assert.That(ui.SummonTickets(category), Is.EqualTo(3));
                    Assert.That(ui.Items(category).Sum(x => x.count), Is.EqualTo(initial + 60));
                    Assert.That(ui.CareerProgress("summon:" + category), Is.EqualTo(progress));
                    ui.Diamonds = remainder;
                    UiClick("10회 뽑기", UiNode("Fullscreen: 뽑기 결과"));
                    Assert.That(ui.Diamonds, Is.Zero);
                    Assert.That(ui.SummonTickets(category), Is.Zero);
                    Assert.That(ui.Items(category).Sum(x => x.count), Is.EqualTo(initial + 70));
                    Assert.That(ui.CareerProgress("summon:" + category), Is.EqualTo(progress + 10));

                    ui.GrantSummonTickets(category, 7); ui.Diamonds = 12345;
                    Assert.That(ui.TrySummon(category, 5, true), Is.True);
                    Assert.That(ui.Diamonds, Is.EqualTo(12345));
                    Assert.That(ui.SummonTickets(category), Is.EqualTo(7), "Free draws never spend tickets.");
                    ui.CloseFullscreen(); ReloadPersistedServices();
                    typeof(DoodleUi).GetMethod("InitCommerce", ServicePrivate).Invoke(ui, null);
                    Assert.That(ui.SummonTickets(category), Is.EqualTo(7), "Both relic and category ticket wallets persist.");
                }
                UiOpen("Shop");
                Object.Destroy(CaptureFrame("ticket-priority-shop.png", 720, 1560));
                AssertUiGeometry("Ticket and diamond prices");
                Assert.That(UiRoot.GetComponentsInChildren<UnityEngine.UI.Button>().Any(x => x.name.StartsWith("TicketSummon_")), Is.False);
                ui.Diamonds = ui.SummonCost("Skill", 43);
                UiClick("50회 뽑기", UiNode("Summon_Skill"));
                Assert.That(ui.Diamonds, Is.Zero);
                Assert.That(ui.SummonTickets("Skill"), Is.Zero);
                Assert.That(ui.SummonTickets("Companion"), Is.EqualTo(7), "Only matching tickets may pay for a draw.");
                Object.Destroy(CaptureFrame("ticket-priority-results.png", 720, 1560));
                Assert.That(ui.TrySummon("DungeonRelic", 10, false), Is.False, "Dungeon relics keep their exclusive ticket payment.");
                yield return null;
            }
            finally { ui.SkipSummonAnimations = oldSkip; }
        }

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
            yield return new WaitForSecondsRealtime(1.5f);
            UiOpen("Shop"); UiClick("마일리지");
            Object.Destroy(CaptureFrame("dayone-mileage-card.png",720,1560));
            int diamonds=ui.Diamonds;Assert.That(ui.ExchangeMileage(10),Is.True);ui.CloseDetail();
            Assert.That(ui.Diamonds,Is.EqualTo(diamonds+5000000));Assert.That(ui.MileageCoupons,Is.Zero);
            Assert.That(ui.ExchangeMileage(5),Is.False);
            typeof(DoodleUi).GetMethod("InitCommerceExtras",ServicePrivate).Invoke(ui,null);
            Assert.That(ui.MileageCoupons,Is.Zero);Assert.That(ui.GrantConfirmedCurrencyProduct(5,"verified-test-0"),Is.False);
            Assert.That(UiKit.Art("MileageCoupon").texture,Is.Not.SameAs(UiKit.Art("TicketCompanion").texture));
            yield return new WaitForSecondsRealtime(1.5f);
            UiOpen("Shop");UiClick("재화");Object.Destroy(CaptureFrame("dayone-diamond-shop.png",720,1560));
            UiNode("Currency product cards").GetComponentInParent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=0;
            Object.Destroy(CaptureFrame("dayone-diamond-shop-bonus.png",720,1560));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DayOneHistorySurvivesResetPeriodsAndDefeatKeepsUnlocks()
        {
            game.TogglePause();var ui=game.Ui;
            ui.RecordMissionAction("attendance");ui.RecordMissionAction("relicAttempt");ui.RecordMissionAction("equip:Companion");
            DayOneState("mainStage",1199);DayOneState("highestMainStage",1199);DayOneState("mainStageKillProgress",70);
            int slots=ui.UnlockedSkillSlots;ui.HandlePlayerDefeat();
            Assert.That(ui.MainStage,Is.EqualTo(1198));Assert.That(ui.MainStageKillProgress,Is.Zero);
            Assert.That(ui.UnlockedSkillSlots,Is.EqualTo(slots));Assert.That(ui.MissionProgress("stage"),Is.EqualTo(1199));
            DayOneState("day","2000-01-01");ui.Save();ReloadPersistedServices();
            Assert.That(ui.CareerProgress("attendance"),Is.GreaterThanOrEqualTo(1));
            Assert.That(ui.CareerProgress("relicAttempt"),Is.GreaterThanOrEqualTo(1));
            Assert.That(ui.CareerProgress("equip:Companion"),Is.GreaterThanOrEqualTo(1));
            Assert.That(ui.MainStage,Is.EqualTo(1198));
            DayOneState("mainStage",0);ui.HandlePlayerDefeat();Assert.That(ui.MainStage,Is.Zero);
            DayOneState("mainMissionIndex",20);Assert.That(ui.MainMissionGoal,Is.EqualTo(35));
            DayOneState("mainMissionIndex",119);Assert.That(ui.MainMissionText,Does.Contain("골드 동굴"));
            ui.RecordMissionAction("dungeon:0");Assert.That(ui.CanClaimMainMission,Is.True);
            DayOneState("mainMissionIndex",120);Assert.That(ui.MainMissionText,Does.Contain("유물 동굴"));
            DayOneState("mainMissionIndex",121);Assert.That(ui.MainMissionGoal,Is.EqualTo(215));
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
            Assert.That(ui.StatValue("attack"),Is.EqualTo(128+5*1200),"The user now tunes linear stats rather than the older exponential 1c target.");
            long income=0;for(int stage=1;stage<=300;stage++)income+=ui.GoldForMainKills(stage,101);
            for(int stage=1;stage<=3;stage++)income+=ui.DungeonGoldReward(stage);
            income+=(22+59*11)*500;
            long cost=0;foreach(var stat in GrowthTuning.stats.Take(4))for(int level=0;level<1200;level++)cost+=DoodleUi.StatUpgradePrice(GrowthTuning.statCosts,stat.id,level);
            // Historical day-one profile is now only a combat smoke fixture. The user
            // controls difficulty manually; no eight-hour/income target is enforced.
            Assert.That(income,Is.GreaterThan(0));
            Debug.Log("DAYONE budget gold="+income+" cost1200="+cost+" attack="+ui.CurrentAttackPower+" hp="+ui.MaxHealth+" enemyHP="+68*ui.EnemyHealthMultiplier(300));
            ui.ShowPage("Stats");Object.Destroy(CaptureFrame("dayone-stage300-stats.png",720,1560));ui.ClosePage();
            game.summonSkillsEnabled=true;game.companionsEnabled=true;game.autoPlay=true;
            game.RequestCombatWaveReset();game.TogglePause();
            float start=Time.fixedTime;int hits=game.PlayerContactHits;Time.timeScale=8;
            while(ui.MainStage==299 && Time.fixedTime-start<240)yield return new WaitForFixedUpdate();
            Debug.Log("DAYONE stage300 combatSeconds="+(Time.fixedTime-start)+" stage="+ui.MainStage+" contactHits="+(game.PlayerContactHits-hits));
            Assert.That(ui.MainStage,Is.GreaterThanOrEqualTo(300),"A balanced Lv1200/Epic1 profile must clear the stage and its real boss without defeat.");
            game.TogglePause();Object.Destroy(CaptureFrame("dayone-stage300-clear.png",720,1560));
            // Earlier milestones have fewer slots and fewer drops. Use seeded real
            // draws/upgrades rather than borrowing the complete day-one inventory.
            foreach(int milestone in new[]{50,100,200}) {
                SetEarnedMilestoneProfile(milestone);game.ResetGame();
                start=Time.fixedTime;
                while(ui.MainStage==milestone-1 && Time.fixedTime-start<300)yield return new WaitForFixedUpdate();
                Debug.Log("DAYONE milestone="+milestone+" level="+ui.AttackStatLevel+" seconds="+(Time.fixedTime-start)+" reached="+ui.MainStage+" attack="+ui.CurrentAttackPower);
                Assert.That(ui.MainStage,Is.GreaterThanOrEqualTo(milestone),"The seeded ticket/upgrade profile must clear milestone "+milestone+" without dying.");
                game.TogglePause();
            }
        }

        void SetEarnedMilestoneProfile(int stage)
        {
            var ui=game.Ui;
            foreach(var item in GrowthTuning.items){item.equipped=item.discovered=false;item.count=item.level=item.slot=0;}
            DayOneState("mainStage",stage-1);DayOneState("highestMainStage",stage-1);DayOneState("mainStageKillProgress",0);
            foreach(string id in new[]{"attack","health","healthRegen","crit2Chance"})GrowthLevels[id]=ui.ProjectedStatLevel(stage);
            var states=(System.Collections.Generic.Dictionary<string,DoodleUi.SummonState>)typeof(DoodleUi).GetField("summonStates",GrowthPrivate).GetValue(ui);
            var commerce=(DoodleUi.CommerceTuning)typeof(DoodleUi).GetField("commerceTuning",GrowthPrivate).GetValue(ui);
            var rng=new System.Random(20260923+stage);int cycles=(stage-1)/5;
            foreach(string category in new[]{"Armor","Club","Skill","Companion","Relic"}) {
                var state=states[category];state.level=category=="Relic"?0:1;state.experience=0;
                int draws=35+cycles*(category=="Relic"?2:8);
                for(int n=0;n<draws;n++) {
                    ui.AddItem(ui.GrantItem(category,rng),1);
                    if(category!="Relic") {
                        state.experience++;
                        int needed=commerce.levelExperience[Math.Min(state.level-1,commerce.levelExperience.Length-1)];
                        if(state.experience>=needed){state.experience-=needed;state.level++;}
                    }
                }
                foreach(var item in ui.Items(category)) {
                    if(category=="Relic"){item.level+=item.count/2;item.count=0;}
                    else while(ui.UpgradeItem(item,false)) { }
                }
                if(category!="Relic")ui.AutoEquip(category);
            }
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
