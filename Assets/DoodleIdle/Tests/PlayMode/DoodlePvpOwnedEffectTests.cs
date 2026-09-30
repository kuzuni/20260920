using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    // Independent double-precision arithmetic over raw catalog data. Never calls the
    // runtime AttackAmount/OwnedAmount/SkillPowerAmount implementations for expected values.
    sealed class PvpDamageOracle
    {
        readonly DoodleUi ui;
        readonly UiCollectionTuning tuning;
        readonly UiItem[] items;
        readonly UiSkin[] skins;
        readonly DoodlePvpLoadout loadout;
        static readonly string[] groups={"Equipment","Skill","Companion","Relic"};
        internal PvpDamageOracle(DoodleUi model)
        {
            ui=model;
            tuning=(UiCollectionTuning)typeof(DoodleUi).GetField("collectionTuning",PvpLoadoutAudit.Private).GetValue(ui);
            loadout=(DoodlePvpLoadout)typeof(DoodleUi).GetField("pvpLoadout",PvpLoadoutAudit.Private).GetValue(ui);
            items=new[]{"Club","Armor","Necklace","Skill","Companion","Relic"}.SelectMany(ui.Items).ToArray();
            skins=ui.Skins("Weapon").Concat(ui.Skins("Appearance")).ToArray();
        }
        static bool Equipment(UiItem item)=>item.category=="Club"||item.category=="Armor"||item.category=="Necklace";
        internal double Stat(string id) {
            var definition=tuning.stats.Single(x=>x.id==id);int level=PvpLoadoutAudit.Levels(ui)[id];
            return (definition.initial+(double)definition.increment*level)*Math.Pow(definition.valueGrowth,level);
        }
        double Skin(string effect)=>skins.Where(x=>x.owned&&(x.effect==effect||x.effect=="healthAndRegen"&&(effect=="health"||effect=="healthRegen"))).Sum(x=>(double)x.ownedBonus);
        double Owned(string group,string effect)=>items.Where(x=>x.discovered&&(Equipment(x)?"Equipment":x.category)==group).Sum(x=>
            (x.effect==effect?(x.category=="Relic"?x.level*(double)tuning.relicStepPercent:x.ownedPercent*(1+Math.Max(0,x.level-1)*.1)):0)
            +(effect=="gold"?x.ownedGoldPercent*(1+Math.Max(0,x.level-1)*.1):0));
        double Multiplier(string effect)=>groups.Aggregate(1.0,(product,group)=>product*(1+Owned(group,effect)/100))*(1+Skin(effect)/100);
        double Enhance(UiItem item)=>1+Math.Min(1,Math.Max(0,item.level-1)/99.0)*tuning.abilityMaxEnhancementBonus;
        double Equipped(string category)=>items.Where(x=>x.equipped&&x.category==category).Sum(x=>Equipment(x)
            ?((1+x.equipValue/100.0)*(x.rarity==8||x.rarity==6&&x.tier==1?1+Math.Max(0,x.level-1)*.01:Enhance(x))-1)*100
            :x.equipValue*Enhance(x));
        internal double Attack=>Stat("attack")*(1+Equipped("Club")/100)*(1+Owned("Equipment","attack")/100)
            *(1+(Owned("Skill","attack")+Equipped("Skill")*.02)/100)
            *(1+(Owned("Companion","attack")+Equipped("Companion"))/100)
            *(1+Owned("Relic","attack")/100)*(1+Skin("attack")/100)*loadout.attackBuff;
        internal double Health=>Stat("health")*(1+Equipped("Armor")/100)*Multiplier("health");
        internal double Regen=>(Stat("healthRegen")+Stat("health")*Multiplier("health")*Equipped("Necklace")/100*.05)*Multiplier("healthRegen");
        internal double Gold=>Multiplier("gold");
        internal double CriticalBonus=>(Multiplier("critDamage")-1)*100;
        internal double Category(string category)=>Multiplier(category=="Basic"?"basicAttack":category=="Skill"?"skillAttack":"companionAttack");
        internal double Critical(int multiplier)=>multiplier==1?1:multiplier*Multiplier("critDamage");
        internal double Hit(string category,double weight,int critical)=>Attack*weight/128*Category(category)*Critical(critical);
        internal double AbilityDps(UiItem item) {
            var grades=item.category=="Skill"?tuning.skillDpsPercentByGrade:tuning.companionDpsPercentByGrade;
            return grades[Math.Min(item.rarity,grades.Length-1)]*item.damageMultiplier*Enhance(item);
        }
        internal double AbilityWeight(UiItem item,float interval)=>128*AbilityDps(item)/100*interval/
            (item.category=="Companion"?item.volleyCount:DoodleAttackPower.Skill(item.ability).totalWeight);
    }
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator OpponentEveryOwnedEffectMatchesIndependentDamageArithmetic()
        {
            game.TogglePause();PvpLoadoutAudit.Equip(game.Ui,1,0);
            // Deterministic critical damage lets ownership effects be compared exactly.
            foreach(var id in DoodleUi.CriticalStatIds)PvpLoadoutAudit.Levels(game.Ui)[id]=0;
            PvpLoadoutAudit.Levels(game.Ui)["crit2Chance"]=4000;
            var saved=DoodlePvpPayload.Pack(game.Ui.CapturePvpLoadout()).Unpack();
            DoodleIdleGame opponent=null,victim=null;
            var report=new List<string>{"Opponent-owned-effect audit: independent raw-catalog arithmetic vs real PVP damage and cached stats."};
            int cases=0,critical=2;
            try {
                opponent=PvpEngine(game,saved,new Vector2(2,0));victim=PvpEngine(game,saved,new Vector2(-2,0));
                PvpConnect(opponent,victim);PvpConnect(victim,opponent);yield return null;yield return null;
                var ui=opponent.Ui;var oracle=new PvpDamageOracle(ui);
                var actor=typeof(DoodleIdleGame).GetField("player",PvpLoadoutAudit.Private).GetValue(victim);
                var health=actor.GetType().GetField("hp");
                var hit=typeof(DoodleIdleGame).GetMethod("DamageAmount",PvpLoadoutAudit.Private);
                Action<string,double,double> same=(label,actual,expected)=>Assert.That(actual,Is.EqualTo(expected).Within(Math.Max(.000001,Math.Abs(expected)*.00002)),label);
                Action<string> check=label=>{
                    ui.BeginCombatSnapshot();
                    try {
                        same(label+" ATK",(double)ui.AttackAmount,oracle.Attack);
                        same(label+" HP",(double)ui.MaxHealthAmount,oracle.Health);
                        same(label+" recovery",(double)ui.HealthRegenAmount,oracle.Regen);
                        same(label+" critical bonus",(double)ui.CriticalBonusAmount,oracle.CriticalBonus);
                        same(label+" gold",(double)ui.GoldGainAmount,oracle.Gold);
                        same(label+" expected critical",(double)ui.ExpectedCriticalAmount,oracle.Critical(critical));
                        foreach(var category in new[]{"Basic","Skill","Companion"}){
                            double expected=oracle.Hit(category,128,critical);
                            health.SetValue(actor,(GameNumber)(expected*10));
                            GameNumber recorded=0;Action<string,string,GameNumber> observer=(c,id,amount)=>recorded=amount;
                            opponent.PvpDamageDealt+=observer;
                            try {hit.Invoke(opponent,new object[]{actor,(GameNumber)128,Vector2.zero,category,"owned-effect-audit"});}
                            finally{opponent.PvpDamageDealt-=observer;}
                            same(label+" "+category+" real damage",(double)recorded,expected);
                            same(label+" "+category+" HP deduction",expected*10-(double)(GameNumber)health.GetValue(actor),expected);
                        }
                    }finally{ui.EndCombatSnapshot();}
                    report.Add(label+": ATK="+ui.AttackAmount+", HP="+ui.MaxHealthAmount+", regen="+ui.HealthRegenAmount+", basic="+oracle.Hit("Basic",128,critical)+", skill="+oracle.Hit("Skill",128,critical)+", companion="+oracle.Hit("Companion",128,critical));cases++;
                };
                Action<UiItem> ability=item=>{
                    float interval=ui.ItemAttackInterval(item);
                    double weight=oracle.AbilityWeight(item,interval);
                    same(item.id+" ability weight",(double)(item.category=="Skill"?ui.SkillItemAmount(item):ui.CompanionWeightAmount(item)),weight);
                    double totalHitWeight=item.category=="Skill"?DoodleAttackPower.Skill(item.ability).hitWeight:1;
                    same(item.id+" one-hit damage",(double)ui.ItemHitAmount(item),oracle.Hit(item.category,weight*totalHitWeight,1));
                    same(item.id+" DPS",(double)ui.ItemDpsAmount(item),oracle.Attack*oracle.AbilityDps(item)/100*oracle.Category(item.category)*oracle.Critical(critical));
                };
                check("baseline opponent");
                foreach(var item in new[]{"Club","Armor","Necklace","Skill","Companion","Relic"}.SelectMany(ui.Items)){
                    item.discovered=false;check("owned off "+item.id);item.discovered=true;
                    item.level++;check("level +1 "+item.id);
                    if(item.category=="Skill"||item.category=="Companion")ability(item);
                    item.level--;
                    if(item.equipped){item.equipped=false;check("unequipped "+item.id);item.equipped=true;}
                }
                foreach(var skin in ui.Skins("Weapon").Concat(ui.Skins("Appearance"))){skin.owned=false;check("skin owned off "+skin.id);skin.owned=true;}
                foreach(var id in new[]{"attack","health","healthRegen"}){PvpLoadoutAudit.Levels(ui)[id]+=10;check("stat +10 "+id);PvpLoadoutAudit.Levels(ui)[id]-=10;}
                var modelSave=(DoodlePvpLoadout)typeof(DoodleUi).GetField("pvpLoadout",PvpLoadoutAudit.Private).GetValue(ui);
                float buff=modelSave.attackBuff;modelSave.attackBuff=1;check("attack buff off");modelSave.attackBuff=buff;
                foreach(var id in DoodleUi.CriticalStatIds)PvpLoadoutAudit.Levels(ui)[id]=0;
                critical=1;check("no critical");
                for(int tier=0;tier<DoodleUi.CriticalStatIds.Length;tier++){
                    string id=DoodleUi.CriticalStatIds[tier];
                    // The half-level boundary exercises probability and the next tier's lock.
                    PvpLoadoutAudit.Levels(ui)[id]=tier==0?2000:1000;
                    same(id+" 50 percent",ui.CriticalChance(tier),50);
                    if(tier<16){
                        string next=DoodleUi.CriticalStatIds[tier+1];PvpLoadoutAudit.Levels(ui)[next]=2000;
                        Assert.That(ui.CriticalChance(tier+1),Is.Zero,id+" next tier locked despite its saved maximum level");
                        PvpLoadoutAudit.Levels(ui)[next]=0;
                    }
                    PvpLoadoutAudit.Levels(ui)[id]=tier==0?4000:2000;critical=1<<(tier+1);check("guaranteed "+id);
                }
                int ownershipIndex=0;
                foreach(var item in new[]{"Club","Armor","Necklace","Skill","Companion","Relic"}.SelectMany(ui.Items))
                    if(!item.equipped&&ownershipIndex++%2==0){item.discovered=false;item.level=item.slot=0;}
                foreach(var skin in ui.Skins("Weapon").Concat(ui.Skins("Appearance")))
                    if(!skin.equipped&&!skin.initiallyOwned&&ownershipIndex++%2==0)skin.owned=false;
                check("mixed ownership opponent");
                var expectedMixed=PvpLoadoutAudit.Values(ui);
                var mixed=PvpEngine(game,DoodlePvpPayload.Pack(ui.CapturePvpLoadout()).Unpack(),new Vector2(5,0));
                try{PvpLoadoutAudit.AssertValues(expectedMixed,mixed.Ui);report.Add("Mixed ownership: all 477 pre-save/restored/cached fields match.");}
                finally{UnityEngine.Object.Destroy(mixed.gameObject);}
                // A completely different opponent model must not mutate the real field's loadout.
                Assert.That(game.Ui.CapturePvpLoadout().collections,Is.EqualTo(saved.collections));
                report.Add("PASS cases="+cases+"; every 188 item ownership/level effect, 82 skins, equipped slots, all 17 critical tiers, attack/HP/regen stats and attack buff covered.");
            }finally{
                Directory.CreateDirectory("artifacts/character-reports");File.WriteAllLines("artifacts/character-reports/pvp-opponent-owned-effects.txt",report);
                if(opponent)UnityEngine.Object.Destroy(opponent.gameObject);if(victim)UnityEngine.Object.Destroy(victim.gameObject);
            }
            yield return null;
        }
    }
}
