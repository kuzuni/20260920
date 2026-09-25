using System;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        // The first eight metric indices are retained for existing saves. Gold/PVP
        // and aggregate summon/entry counters remain history, not quest objectives.
        static readonly string[] ServiceMetrics = {
            "kills", "gold", "dungeon", "roulette", "equipmentUpgrade", "skillUpgrade", "pvp", "summon",
            "attendance", "dungeonEnter:0", "dungeonEnter:2", "companionUpgrade",
            "statUpgrade:attack", "statUpgrade:health", "statUpgrade:healthRegen", "statUpgrade:crit2Chance", "statUpgrade:crit4Chance",
            "relicUpgrade", "summon:Armor", "summon:Club", "summon:Skill", "summon:Companion", "summon:Relic", "summon:DungeonRelic", "dungeonClear", "statUpgrade:criticalChance"
        };
        static readonly int[][] QuestMetrics = {
            new[] { 0, 3, 8, 9, 10, 18, 19, 20, 21, 22 },
            new[] { 0, 4, 5, 11, 12, 13, 14, 25, 17, 18, 19, 20, 21, 22, 24, 3, 8 },
            new[] { 0, 3, 8, 9, 10, 18, 19, 20, 21, 22 }
        };
        static readonly string[] QuestLabels = {
            "적 {0}마리 처치", "골드 {0} 획득", "던전 {0}회 도전", "룰렛 {0}회 돌리기", "장비 {0}회 강화", "스킬 {0}회 강화", "PVP {0}회 도전", "뽑기 {0}회 진행",
            "출석 보상 {0}회 받기", "골드 동굴 {0}회 도전", "유물 동굴 {0}회 도전", "동료 {0}회 강화",
            "공격력 {0}회 강화", "체력 {0}회 강화", "체력 회복 {0}회 강화", "x2 치명타 확률 {0}회 강화", "x4 치명타 확률 {0}회 강화",
            "유물 {0}회 강화", "갑옷 {0}회 뽑기", "몽둥이 {0}회 뽑기", "스킬 {0}회 뽑기", "동료 {0}회 뽑기", "유물 {0}회 뽑기", "던전 유물 {0}회 뽑기", "던전 {0}회 클리어", "치명타 확률 스탯 {0}회 강화"
        };
        static readonly string[] QuestIcons = {
            "MushroomA", "Gold", "Dungeon", "Roulette", "Club", "SkillMeteor", "Pvp", "TicketArmor",
            "Attendance", "Dungeon", "DungeonPottery", "CompanionMon_10",
            "StatAttack", "StatHealth", "StatRegen", "StatCrit2", "StatCrit4", "NavPottery",
            "TicketArmor", "TicketClub", "TicketSkill", "TicketCompanion", "TicketRelic", "DungeonRelicTicket", "Dungeon", "RelicCritical"
        };
        public int QuestCount(int tab) => tab >= 0 && tab < QuestMetrics.Length ? QuestMetrics[tab].Length : 0;
        public string QuestMetric(int tab, int index) => ServiceMetrics[QuestMetrics[tab][index]];
        public int QuestTarget(int tab, int index) => QuestGoal(tab,index);
        public int QuestReward(int tab, int index) => tab == 0 ? serviceTuning.dailyQuestReward : tab == 2 ? serviceTuning.weeklyQuestReward
            : QuestMetric(tab,index) == "roulette" ? serviceTuning.repeatRouletteReward : serviceTuning.repeatQuestReward;

        void NormalizeQuestState()
        {
            Array.Resize(ref services.daily, ServiceMetrics.Length);
            Array.Resize(ref services.weekly, ServiceMetrics.Length);
            Array.Resize(ref services.repeat, ServiceMetrics.Length);
            if (services.questSchemaVersion < 2)
            {
                // A claimed old kill/roulette reward stays claimed. Never map an
                // old gold or aggregate dungeon flag onto an unrelated new quest.
                var daily = services.dailyClaimed;
                var weekly = services.weeklyClaimed;
                services.dailyClaimed = new bool[QuestCount(0)];
                services.weeklyClaimed = new bool[QuestCount(2)];
                if (daily != null && daily.Length > 0) services.dailyClaimed[0] = daily[0];
                if (daily != null && daily.Length > 3) services.dailyClaimed[1] = daily[3];
                if (weekly != null && weekly.Length > 0) services.weeklyClaimed[0] = weekly[0];
                services.questSchemaVersion = 2;
            }
            if (services.questSchemaVersion < 4) {
                foreach (var counters in new[] { services.daily, services.weekly, services.repeat }) {
                    counters[22]=(int)Math.Min(int.MaxValue,(long)counters[22]+counters[23]);counters[23]=0;
                }
                foreach (var claims in new[] { services.dailyClaimed, services.weeklyClaimed })
                    if (claims != null && claims.Length > 10) claims[9] |= claims[10];
            }
            Array.Resize(ref services.dailyClaimed, QuestCount(0));
            Array.Resize(ref services.weeklyClaimed, QuestCount(2));
            if (services.questSchemaVersion < 3) {
                // Merge only unclaimed repeat progress, never lifetime counts that were already paid.
                foreach (var counters in new[] { services.daily, services.weekly, services.repeat })
                    counters[25] = (int)Math.Min(int.MaxValue, (long)counters[15] + counters[16]);
                services.questSchemaVersion = 3;
            }
            if (serviceTuning.repeatGoals != null && serviceTuning.repeatGoals.Length == 19) {
                var merged = new int[18];
                for (int i = 0; i < merged.Length; i++) merged[i] = serviceTuning.repeatGoals[i < 8 ? i : i + 1];
                serviceTuning.repeatGoals = merged;
            }
            services.questSchemaVersion = 4;
            if(serviceTuning.dailyGoals != null && serviceTuning.dailyGoals.Length==11) Array.Resize(ref serviceTuning.dailyGoals,10);
            if(serviceTuning.weeklyGoals != null && serviceTuning.weeklyGoals.Length==11) Array.Resize(ref serviceTuning.weeklyGoals,10);
            if(serviceTuning.repeatGoals != null && serviceTuning.repeatGoals.Length==18) {
                var goals=new int[17];
                for(int i=0;i<goals.Length;i++)goals[i]=serviceTuning.repeatGoals[i<14?i:i+1];
                serviceTuning.repeatGoals=goals;
            }
            var defaults = new ServiceTuning();
            if (serviceTuning.dailyGoals == null || serviceTuning.dailyGoals.Length != QuestCount(0)) serviceTuning.dailyGoals = defaults.dailyGoals;
            if (serviceTuning.repeatGoals == null || serviceTuning.repeatGoals.Length != QuestCount(1)) serviceTuning.repeatGoals = defaults.repeatGoals;
            if (serviceTuning.weeklyGoals == null || serviceTuning.weeklyGoals.Length != QuestCount(2)) serviceTuning.weeklyGoals = defaults.weeklyGoals;
        }
    }
}
