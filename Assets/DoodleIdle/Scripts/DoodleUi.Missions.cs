using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        [Serializable] public sealed class CareerCounter { public string key; public long value; }
        public sealed class MissionDefinition
        {
            public string key,label,ticket;
            public long goal;
            public int ticketCount,diamonds=50,gold=500;
            public MissionDefinition(string key,string label,long goal,string ticket=null,int count=0) { this.key=key;this.label=label;this.goal=goal;this.ticket=ticket;ticketCount=count; }
        }
        static readonly MissionDefinition[] tutorialMissions={
            new MissionDefinition("stat:attack","공격력 Lv.15 달성",15,"Armor",20),
            new MissionDefinition("summon:Armor","갑옷 5회 뽑기",5,"Club",20),
            new MissionDefinition("equip:Armor","갑옷 장착해 보기",1),
            new MissionDefinition("summon:Club","몽둥이 5회 뽑기",5,"Skill",20),
            new MissionDefinition("equip:Club","몽둥이 장착해 보기",1),
            new MissionDefinition("summon:Skill","스킬 5회 뽑기",5,"Companion",20),
            new MissionDefinition("equip:Skill","스킬 장착해 보기",1),
            new MissionDefinition("summon:Companion","동료 5회 뽑기",5,"Relic",10),
            new MissionDefinition("equip:Companion","동료 장착해 보기",1),
            new MissionDefinition("summon:Relic","유물 5회 뽑기",5),
            new MissionDefinition("relicAttempt","유물 강화 시도해 보기",1,"Relic",5),
            new MissionDefinition("stat:health","체력 Lv.15 달성",15),
            new MissionDefinition("stat:healthRegen","체력 회복 Lv.15 달성",15),
            new MissionDefinition("stat:crit2Chance","x2 치명타 확률 Lv.15 달성",15),
            new MissionDefinition("attendance","출석 보상 받기",1),
            new MissionDefinition("roulette","룰렛 보상 받기",1),
            new MissionDefinition("questClaim","퀘스트 보상 받기",1),
            new MissionDefinition("buff","버프 활성화해 보기",1),
            new MissionDefinition("stage","스테이지 5 클리어",5),
            new MissionDefinition("equipmentUpgrade","장비 강화해 보기",1,"Armor",10),
            new MissionDefinition("dungeon:0","골드 동굴 1회 클리어",1,"Club",10),
            new MissionDefinition("dungeon:2","유물 동굴 1회 클리어",1,"Relic",5)
        };
        public MissionDefinition CurrentMainMission
        {
            get {
                int index=services==null?0:services.mainMissionIndex;
                // Cave stage 1 has field-stage-50 difficulty. Introduce it after the
                // stage-45 cycle so it cannot block the early ticket/gold supply.
                const int earlyTutorials=20, dungeonTutorialIndex=earlyTutorials+9*11;
                if(index<earlyTutorials)return tutorialMissions[index];
                if(index==dungeonTutorialIndex)return tutorialMissions[20];
                if(index==dungeonTutorialIndex+1)return tutorialMissions[21];
                int repeating=index-earlyTutorials-(index>dungeonTutorialIndex+1?2:0);
                long cycle=repeating/11L+1;int kind=repeating%11;
                if(kind<4) {
                    string[] ids={"attack","health","healthRegen","crit2Chance"};string[] labels={"공격력","체력","체력 회복","x2 치명타 확률"};
                    long level=Math.Min(StatMaxLevel(ids[kind]),15+cycle*20);
                    return new MissionDefinition("stat:"+ids[kind],labels[kind]+" Lv."+level+" 달성",level);
                }
                if(kind==4)return new MissionDefinition("stage","스테이지 "+cycle*5+" 클리어",cycle*5);
                if(kind==5)return new MissionDefinition("kills","몬스터 "+UiNumber.Format(cycle*500)+"마리 처치",cycle*500);
                string[] categories={"Armor","Club","Skill","Companion","Relic"};string category=categories[kind-6];
                long draws=cycle*(category=="Relic"?2:8)+5;
                return new MissionDefinition("summon:"+category,CommerceLabel(category)+" 누적 "+UiNumber.Format(draws)+"회 뽑기",draws,category,category=="Relic"?2:8);
            }
        }
        public int MainMissionReward => CurrentMainMission.diamonds;
        public int MainMissionNumber => services==null?1:(int)Math.Min(int.MaxValue,services.mainMissionIndex+1L);
        public long MainMissionGoal => CurrentMainMission.goal;
        public long MainMissionProgress => MissionProgress(CurrentMainMission.key);
        public bool CanClaimMainMission => services!=null&&services.mainMissionIndex<int.MaxValue&&MainMissionProgress>=MainMissionGoal&&Diamonds<=int.MaxValue-MainMissionReward;
        public float MainMissionFraction => Mathf.Clamp01((float)(MainMissionProgress/(double)Math.Max(1,MainMissionGoal)));
        public string MainMissionText => "미션 "+MainMissionNumber+".\n"+CurrentMainMission.label+"\n("+UiNumber.Format(Math.Min(MainMissionProgress,MainMissionGoal))+"/"+UiNumber.Format(MainMissionGoal)+")";
        public long CareerProgress(string key) { if(services==null||services.career==null)return 0;var entry=services.career.Find(x=>x.key==key);return entry==null?0:entry.value; }
        public void RecordMissionAction(string key,int amount=1)
        {
            if(services==null||amount<=0)return;
            if(services.career==null)services.career=new List<CareerCounter>();
            var entry=services.career.Find(x=>x.key==key);
            if(entry==null){entry=new CareerCounter{key=key};services.career.Add(entry);}
            entry.value=entry.value>long.MaxValue-amount?long.MaxValue:entry.value+amount;
        }
        void RememberMissionAction(string key,long value) { long missing=value-CareerProgress(key);if(missing>0)RecordMissionAction(key,(int)Math.Min(int.MaxValue,missing)); }
        public long MissionProgress(string key)
        {
            if(services==null)return 0;
            if(key.StartsWith("stat:"))return StatLevel(key.Substring(5));
            if(key=="stage")return Math.Max(services.highestMainStage,MainStage);
            if(key=="kills")return Math.Max(services.mainKills,CareerProgress("kills"));
            if(key.StartsWith("equip:")&&Items(key.Substring(6)).Exists(x=>x.equipped))return Math.Max(1,CareerProgress(key));
            if(key.StartsWith("summon:")) { string category=key.Substring(7);return Math.Max(CareerProgress(key),summonStates.ContainsKey(category)?summonStates[category].lifetimeDraws:0); }
            return CareerProgress(key);
        }
        void InitMissionHistory()
        {
            services.highestMainStage=Math.Max(services.highestMainStage,services.mainStage);
            if(services.career==null)services.career=new List<CareerCounter>();
            if(services.missionVersion==0){services.mainMissionIndex=0;services.missionVersion=1;}
            if(services.attendanceIndex>0||!string.IsNullOrEmpty(services.attendanceDay))RememberMissionAction("attendance",1);
            if(services.goldExpiry>0||services.attackExpiry>0)RememberMissionAction("buff",1);
            if(Array.Exists(services.dailyClaimed,x=>x)||Array.Exists(services.weeklyClaimed,x=>x))RememberMissionAction("questClaim",1);
            RememberMissionAction("roulette",Math.Max(services.spins,Math.Max(services.weekly[3],services.repeat[3])));
            foreach(int index in new[]{0,2})RememberMissionAction("dungeon:"+index,services.dungeonStages[index]);
            foreach(string category in new[]{"Armor","Club","Skill","Companion"})if(Items(category).Exists(x=>x.equipped))RememberMissionAction("equip:"+category,1);
            if(collectionItems.Exists(x=>IsEquipment(x)&&x.level>1))RememberMissionAction("equipmentUpgrade",1);
            if(AllRelics.Exists(x=>x.level>1))RememberMissionAction("relicAttempt",1);
        }
        public bool ClaimMainMission()
        {
            if(!CanClaimMainMission)return false;
            var reward=CurrentMainMission;
            if(reward.ticketCount>0&&SummonTickets(reward.ticket)>int.MaxValue-reward.ticketCount)return false;
            services.mainMissionIndex++;Diamonds+=reward.diamonds;Gold=SaturatingAdd(Gold,reward.gold);
            var rewards=new List<UiReward>{new UiReward{name="다이아",icon="Diamond",amount=reward.diamonds},new UiReward{name="골드",icon="Gold",amount=reward.gold}};
            if(reward.ticketCount>0){GrantSummonTickets(reward.ticket,reward.ticketCount);rewards.Add(new UiReward{name=CommerceLabel(reward.ticket)+" 뽑기권",icon=TicketIcon(reward.ticket),amount=reward.ticketCount});}
            Save();RefreshPage();ShowRewards("미션 보상 획득!",rewards);return true;
        }
    }
}
