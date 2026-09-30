using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeStage.AntiCheat.ObscuredTypes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    internal static class PvpLoadoutAudit
    {
        internal const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        internal static Dictionary<string,ObscuredInt> Levels(DoodleUi ui)=>(Dictionary<string,ObscuredInt>)typeof(DoodleUi).GetField("statLevels",Private).GetValue(ui);
        internal static void Equip(DoodleUi ui,int variant,int group,bool allOwned=true)
        {
            ui.Items("Skill");
            typeof(DoodleUi).GetField("starterDamageBaseline",Private).SetValue(ui,(GameNumber)1);
            var service=typeof(DoodleUi).GetField("services",Private).GetValue(ui);
            service.GetType().GetProperty("highestMainStage").SetValue(service,1000);
            service.GetType().GetProperty("attackExpiry").SetValue(service,DateTimeOffset.UtcNow.ToUnixTimeSeconds()+3600);
            foreach(var category in new[]{"Armor","Club","Necklace","Relic","Skill","Companion"}) {
                var items=ui.Items(category);
                foreach(var item in items){item.equipped=false;item.discovered=allOwned;item.level=3+variant+items.IndexOf(item)%7;}
                int count=category=="Skill"?8:category=="Companion"?5:category=="Relic"?0:1;
                if(category=="Relic")foreach(var item in items)item.discovered=true;
                for(int i=0;i<count;i++){
                    int index=category=="Skill"?(group*8+i)%items.Count:category=="Companion"?(group*5+i)%items.Count:(variant+2)%items.Count;
                    var item=items[index];item.equipped=item.discovered=true;item.slot=i;
                }
            }
            var levels=Levels(ui);
            foreach(var id in levels.Keys.ToArray())levels[id]=id=="health"?50000:37+variant*11;
            // Exercise an unlocked critical tier and the lock boundary of subsequent tiers.
            levels["crit2Chance"]=4000;levels["crit4Chance"]=250;
            foreach(var skin in ui.Skins("Appearance").Concat(ui.Skins("Weapon")))skin.owned=true;
            Assert.That(ui.EquipSkin(variant==0?"appearance_mint":"appearance_peach"),Is.True);
            Assert.That(ui.EquipSkin(variant==0?"weapon_vine":"weapon_crystal"),Is.True);
        }
        internal static Dictionary<string,string> Values(DoodleUi ui)
        {
            var result=new Dictionary<string,string>();
            foreach(var name in new[]{"AttackAmount","CombatDamageAmount","MaxHealthAmount","HealthRegenAmount","CriticalBonusAmount","ExpectedCriticalAmount","PowerAmount","GoldGainAmount"})
                result[name]=typeof(DoodleUi).GetProperty(name).GetValue(ui).ToString();
            foreach(var pair in Levels(ui)){result["statLevel/"+pair.Key]=((int)pair.Value).ToString();result["stat/"+pair.Key]=ui.StatAmount(pair.Key).ToString();}
            for(int i=0;i<17;i++)result["critical/"+i]=ui.CriticalChance(i).ToString("R");
            foreach(var category in new[]{"Basic","Skill","Companion"})result["category/"+category]=ui.CategoryDamageAmount(category).ToString();
            foreach(var category in new[]{"Armor","Club","Necklace","Relic","Skill","Companion"})foreach(var item in ui.Items(category)){
                result["item/"+item.id]=$"{item.discovered}/{item.equipped}/{item.level}/{item.slot}";
                if(category=="Skill"||category=="Companion"){
                    result["hit/"+item.id]=ui.ItemHitAmount(item).ToString();
                    result["dps/"+item.id]=ui.ItemDpsAmount(item).ToString();
                    result["interval/"+item.id]=ui.ItemAttackInterval(item).ToString("R");
                }
            }
            result["appearance"]=ui.EquippedAppearanceIcon;result["weapon"]=ui.EquippedWeaponIcon;
            result["tint"]=ui.EquippedAppearanceTint.ToString();result["weaponTint"]=ui.EquippedWeaponTint.ToString();
            result["buff"]=ui.AttackBuffMultiplier.ToString("R");
            return result;
        }
        internal static void AssertValues(Dictionary<string,string> expected,DoodleUi actual)
        {
            foreach(var value in Values(actual))Assert.That(value.Value,Is.EqualTo(expected[value.Key]),value.Key);
            actual.BeginCombatSnapshot();
            try{foreach(var value in Values(actual))Assert.That(value.Value,Is.EqualTo(expected[value.Key]),"cached "+value.Key);}
            finally{actual.EndCombatSnapshot();}
        }
    }
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest,Timeout(240000)]
        public IEnumerator PvpAllSkillsAndCompanionsHitWithRestoredFullLoadouts()
        {
            game.TogglePause();game.basicSkillsEnabled=game.extraSkillsEnabled=game.summonSkillsEnabled=game.companionsEnabled=true;
            var report=new List<string>{"PVP full-loadout audit: values before serialization vs restored model and fixed-step cache."};
            var coveredSkills=new HashSet<string>();var coveredCompanions=new HashSet<string>();
            var missing=new List<string>();float originalScale=Time.timeScale;
            try {
                for(int group=0;group<7;group++){
                    DoodleIdleGame left=null,right=null;
                    try {
                        PvpLoadoutAudit.Equip(game.Ui,0,group);
                        var leftValues=PvpLoadoutAudit.Values(game.Ui);var leftSave=DoodlePvpPayload.Pack(game.Ui.CapturePvpLoadout()).Unpack();
                        PvpLoadoutAudit.Equip(game.Ui,1,group);
                        var rightValues=PvpLoadoutAudit.Values(game.Ui);var rightSave=DoodlePvpPayload.Pack(game.Ui.CapturePvpLoadout()).Unpack();
                        left=PvpEngine(game,leftSave,new Vector2(-2,0));right=PvpEngine(game,rightSave,new Vector2(2,0));
                        PvpLoadoutAudit.AssertValues(leftValues,left.Ui);PvpLoadoutAudit.AssertValues(rightValues,right.Ui);
                        Assert.That(left.PlayerMaxHealthAmount,Is.EqualTo(left.Ui.MaxHealthAmount));Assert.That(right.PlayerMaxHealthAmount,Is.EqualTo(right.Ui.MaxHealthAmount));
                        Assert.That(left.Ui.AttackAmount,Is.Not.EqualTo(right.Ui.AttackAmount),"Opponents must retain their own stats.");
                        report.Add($"group {group}: {leftValues.Count*2} fields match; left ATK {left.Ui.AttackAmount}, HP {left.Ui.MaxHealthAmount}, regen {left.Ui.HealthRegenAmount}; right ATK {right.Ui.AttackAmount}, HP {right.Ui.MaxHealthAmount}, regen {right.Ui.HealthRegenAmount}");
                        var hits=new[]{new Dictionary<string,int>(),new Dictionary<string,int>()};
                        int side=0;
                        foreach(var engine in new[]{left,right}){
                            int index=side++;engine.PvpDamageDealt+=(category,id,amount)=>{Assert.That(amount>0,Is.True);hits[index][category+"/"+id]=hits[index].TryGetValue(category+"/"+id,out int n)?n+1:1;};
                            // Numeric equality above uses real HP. This endurance-only phase extends
                            // actor HP so late-casting abilities cannot be skipped by an early KO.
                            var actor=typeof(DoodleIdleGame).GetField("player",PvpLoadoutAudit.Private).GetValue(engine);
                            var hp=engine.Ui.AttackAmount*100000000;
                            actor.GetType().GetField("hp").SetValue(actor,hp);actor.GetType().GetField("maxHp").SetValue(actor,hp);
                        }
                        PvpConnect(left,right);PvpConnect(right,left);yield return null;yield return null;
                        left.TogglePause();right.TogglePause();Time.timeScale=2;
                        while(left.Elapsed<18)yield return null;
                        if(new[]{left,right}.Select((engine,i)=>engine.Ui.EquippedSkills.Any(skill=>!hits[i].ContainsKey("Skill/"+skill.ability))).Any(x=>x)){
                            report.Add($"group {group}: a moving target evaded a path-based attack; repeat with stationary targets six units apart.");
                            int position=0;
                            foreach(var engine in new[]{left,right}){
                                var actor=typeof(DoodleIdleGame).GetField("player",PvpLoadoutAudit.Private).GetValue(engine);
                                var body=(Rigidbody2D)actor.GetType().GetField("body").GetValue(actor);
                                body.position=new Vector2(position++==0?-3:3,0);body.transform.position=body.position;
                                body.linearVelocity=Vector2.zero;body.constraints=RigidbodyConstraints2D.FreezeAll;
                            }
                            Physics2D.SyncTransforms();
                            float until=left.Elapsed+18;while(left.Elapsed<until)yield return null;
                        }
                        Time.timeScale=originalScale;side=0;
                        foreach(var engine in new[]{left,right}){
                            var hit=hits[side++];
                            foreach(var skill in engine.Ui.EquippedSkills){
                                coveredSkills.Add(skill.id);int count=hit.TryGetValue("Skill/"+skill.ability,out var n)?n:0;
                                report.Add($"{group}/{side} skill {skill.ability}: casts={engine.SkillActivationCount(skill.ability)}, hits={count}");
                                if(engine.SkillActivationCount(skill.ability)==0||count==0)missing.Add($"{group}/{side}: {skill.ability}");
                            }
                            foreach(var companion in engine.Ui.EquippedCompanions){
                                coveredCompanions.Add(companion.id);int count=hit.TryGetValue("Companion/"+companion.id,out var n)?n:0;
                                report.Add($"{group}/{side} companion {companion.id}: shots={engine.CompanionShotCount(companion.id)}, hits={count}");
                                if(engine.CompanionShotCount(companion.id)==0||count==0)missing.Add($"{group}/{side}: {companion.id}");
                            }
                            Assert.That(engine.Kills,Is.Zero);
                        }
                    }finally{Time.timeScale=originalScale;if(left)UnityEngine.Object.Destroy(left.gameObject);if(right)UnityEngine.Object.Destroy(right.gameObject);}
                    yield return null;
                }
                Assert.That(coveredSkills.Count,Is.EqualTo(game.Ui.Items("Skill").Count));
                Assert.That(coveredCompanions.Count,Is.EqualTo(game.Ui.Items("Companion").Count));
                Assert.That(missing,Is.Empty,string.Join(", ",missing));
            }finally{
                Time.timeScale=originalScale;Directory.CreateDirectory("artifacts/character-reports");
                File.WriteAllLines("artifacts/character-reports/pvp-full-loadout-audit.txt",report.Concat(new[]{"Missing: "+string.Join(", ",missing)}));
            }
        }

        [UnityTest]
        public IEnumerator PvpAppliedDamageAndIndividualBonusesMatchLoadout()
        {
            game.TogglePause();PvpLoadoutAudit.Equip(game.Ui,0,0);
            DoodleIdleGame attacker=null,defender=null;
            try {
                var saved=DoodlePvpPayload.Pack(game.Ui.CapturePvpLoadout()).Unpack();
                attacker=PvpEngine(game,saved,new Vector2(-2,0));defender=PvpEngine(game,saved,new Vector2(2,0));
                PvpConnect(attacker,defender);PvpConnect(defender,attacker);yield return null;yield return null;
                var ui=attacker.Ui;
                var target=typeof(DoodleIdleGame).GetField("player",PvpLoadoutAudit.Private).GetValue(defender);
                var hpField=target.GetType().GetField("hp");
                var damage=typeof(DoodleIdleGame).GetMethod("DamageAmount",PvpLoadoutAudit.Private);
                foreach(var category in new[]{"Basic","Skill","Companion"}){
                    GameNumber weight=category=="Skill"?35*ui.SkillPowerAmount("Lightning"):category=="Companion"?ui.CompanionWeightAmount(ui.EquippedCompanions.First()):128;
                    var state=UnityEngine.Random.state;var critical=attacker.RollUiCriticalAmount();UnityEngine.Random.state=state;
                    var expected=ui.AttackPercentAmount(weight*100/128,category)*critical;
                    hpField.SetValue(target,expected*10);
                    GameNumber recorded=0;Action<string,string,GameNumber> observe=(c,id,amount)=>recorded=amount;
                    attacker.PvpDamageDealt+=observe;
                    damage.Invoke(attacker,new object[]{target,weight,Vector2.zero,category,"audit"});
                    attacker.PvpDamageDealt-=observe;
                    Assert.That(recorded,Is.EqualTo(expected),category+" applied damage");
                    Assert.That((double)((expected*10-(GameNumber)hpField.GetValue(target))/expected),Is.EqualTo(1).Within(.000001),category+" HP deduction");
                }
                hpField.SetValue(target,defender.Ui.MaxHealthAmount*.1);
                var expectedRegen=GameNumber.Min(defender.Ui.MaxHealthAmount,defender.Ui.MaxHealthAmount*.1+defender.Ui.HealthRegenAmount*.02f);
                typeof(DoodleIdleGame).GetMethod("TickPvpRegeneration",PvpLoadoutAudit.Private).Invoke(defender,new object[]{.02f});
                Assert.That((GameNumber)hpField.GetValue(target),Is.EqualTo(expectedRegen),"actual fixed-step HP recovery");
                var levels=PvpLoadoutAudit.Levels(ui);
                var attack=ui.AttackAmount;levels["attack"]+=100;Assert.That(ui.AttackAmount>attack,Is.True,"attack stat");
                var health=ui.MaxHealthAmount;levels["health"]+=100;Assert.That(ui.MaxHealthAmount>health,Is.True,"health stat");
                var regen=ui.HealthRegenAmount;levels["healthRegen"]+=100;Assert.That(ui.HealthRegenAmount>regen,Is.True,"regeneration stat");
                foreach(var category in new[]{"Club","Armor","Necklace"}){
                    var item=ui.Items(category).Single(x=>x.equipped);
                    var before=category=="Club"?ui.AttackAmount:category=="Armor"?ui.MaxHealthAmount:ui.HealthRegenAmount;
                    item.equipped=false;
                    var after=category=="Club"?ui.AttackAmount:category=="Armor"?ui.MaxHealthAmount:ui.HealthRegenAmount;
                    Assert.That(after<before,Is.True,category+" equipped contribution");item.equipped=true;
                }
                attack=ui.AttackAmount;
                var relics=ui.Items("Relic").Where(x=>x.effect=="attack").ToArray();Assert.That(relics,Is.Not.Empty);
                foreach(var relic in relics)relic.discovered=false;
                Assert.That(ui.AttackAmount<attack,Is.True,"relic contribution");
                attack=ui.AttackAmount;
                foreach(var skin in ui.Skins("Weapon"))skin.owned=false;
                Assert.That(ui.AttackAmount<attack,Is.True,"owned skin contribution");
                foreach(var item in ui.EquippedSkills.Concat(ui.EquippedCompanions)){
                    var before=ui.ItemHitAmount(item);item.level+=10;
                    Assert.That(ui.ItemHitAmount(item)>before,Is.True,item.id+" enhancement damage");
                }
                // Changes to the attacking model cannot leak into the opponent's captured state.
                Assert.That(defender.Ui.CapturePvpLoadout().collections,Is.EqualTo(saved.collections));
            }finally{if(attacker)UnityEngine.Object.Destroy(attacker.gameObject);if(defender)UnityEngine.Object.Destroy(defender.gameObject);}
            yield return null;
        }

        [UnityTest]
        public IEnumerator PvpCandidateShowsScorePowerAndExactReward()
        {
            game.TogglePause();game.Ui.ShowPage("Pvp");
            var method=typeof(DoodleUi).GetMethod("PvpCandidateButton",PvpLoadoutAudit.Private);
            var panel=new GameObject("Candidate test",typeof(RectTransform)).GetComponent<RectTransform>();panel.SetParent(game.Ui.Canvas.transform,false);
            try {
                foreach(int score in new[]{-1000,0,1000}){
                    var button=(Button)method.Invoke(game.Ui,new object[]{panel,"Opponent",0,score,"123456",(Action)(()=>{})});
                    var text=button.GetComponentInChildren<Text>();
                    Assert.That(text.text,Does.Contain("승점 "+score));Assert.That(text.text,Does.Contain("전투력 "+UiNumber.Format((GameNumber)123456)));
                    Assert.That(text.text,Does.Contain("승리 +"+DoodlePvpRules.Delta(0,score,true)+"점"));
                    Assert.That(text.text,Does.Contain("패배 "+DoodlePvpRules.Delta(0,score,false)+"점"));
                    Assert.That(text.supportRichText,Is.False);
                }
            }finally{UnityEngine.Object.Destroy(panel.gameObject);}
            yield return null;
        }
    }
}
