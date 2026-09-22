using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        // Stages count actual completed combat waves, starting at zero. Dungeon kills
        // belong only to their active dungeon and cannot also advance the main stage.
        public int MainStage => services == null ? 0 : services.mainStage;
        public int MainStageKillProgress => services == null ? 0 : services.mainStageKillProgress;
        public int MainStageKillGoal => 100;
        public int MainStageRemaining => Math.Max(0, MainStageKillGoal - MainStageKillProgress);
        public bool BreakthroughMode => services == null || services.breakthroughMode;
        public bool MainBossPending => MainStageKillProgress >= MainStageKillGoal && BreakthroughMode;
        public void ToggleBreakthroughMode()
        {
            services.breakthroughMode = !services.breakthroughMode;
            if (!services.breakthroughMode && MainStageKillProgress >= MainStageKillGoal)
            {
                services.mainStageKillProgress = 0;
                if (game) game.RequestCombatWaveReset();
            }
            Save(); RefreshHud();
        }
        public void RecordMainCombatKill(bool boss)
        {
            if (services == null || ActiveDungeonIndex >= 0) return;
            services.mainKills = SaturatingAdd(services.mainKills, 1);
            if (boss)
            {
                int previousSkillSlots = UnlockedSkillSlots;
                if (MainBossPending)
                    services.mainStage = (int)Math.Min(int.MaxValue, (long)services.mainStage + 1);
                services.highestMainStage=Math.Max(services.highestMainStage,services.mainStage);
                services.mainStageKillProgress = 0;
                Save();
                if (UnlockedSkillSlots != previousSkillSlots && ActivePage == "Skills") RefreshPage();
            }
            else
            {
                services.mainStageKillProgress = Math.Min(MainStageKillGoal, services.mainStageKillProgress + 1);
                if (services.mainStageKillProgress == MainStageKillGoal)
                {
                    if (!BreakthroughMode) services.mainStageKillProgress = 0;
                    Save();
                }
            }
        }
        public int HighestDungeonStage => services == null ? 0 : Math.Max(services.dungeonStages[0], Math.Max(services.dungeonStages[1], services.dungeonStages[2]));
        public int GetDungeonStage(int index) => services == null || index < 0 || index >= 3 ? 0 : services.dungeonStages[index];
        public int RelicTickets => services == null ? 0 : services.relicTickets;
        public int DungeonRelicTickets => services == null ? 0 : services.dungeonRelicTickets;
        public int DungeonChallengeStage(int index) => (int)Math.Min(int.MaxValue, (long)GetDungeonStage(index) + 1);
        public static int DungeonDifficultyStage(int stage) => (int)Math.Min(int.MaxValue, Math.Max(1L, stage) * 50);
        public int CombatDifficultyStage => ActiveDungeonIndex < 0 ? (int)Math.Min(int.MaxValue, (long)MainStage+1) : DungeonDifficultyStage(DungeonChallengeStage(ActiveDungeonIndex));
        public int DungeonThemeIndex => ActiveDungeonIndex == 0 ? 8 : 6;
        public int HighestMainStage => services==null?0:Math.Max(services.highestMainStage,services.mainStage);
        public float EnemyHealthMultiplier(int displayStage)
        {
            if(serviceTuning.enemyHealthStageGrowth<=0)return 1;
            double stage=Math.Max(0L,(long)displayStage-1);
            return (float)Math.Min(1e30,(1+stage*serviceTuning.enemyHealthStageGrowth)*Math.Pow(serviceTuning.enemyPowerGrowth,Math.Min(20000,stage*4))*(1+stage*serviceTuning.enemyHealthBudget));
        }
        public float EnemyDamageMultiplier(int displayStage)
        {
            if(serviceTuning.enemyDamageStageGrowth<=0)return 1;
            double stage=Math.Max(0L,(long)displayStage-1);
            return (float)Math.Min(1e30,(1+stage*serviceTuning.enemyDamageStageGrowth)*Math.Pow(serviceTuning.enemyDamagePowerGrowth,Math.Min(20000,stage*4))*(1+stage*.04));
        }
        public void HandlePlayerDefeat()
        {
            if(services==null)return;
            CreditPendingFieldGold();
            if(ActiveDungeonIndex>=0){services.activeDungeon=-1;services.dungeonProgress=0;Toast("던전 도전에 실패했어요.");}
            else {services.highestMainStage=Math.Max(HighestMainStage,MainStage);services.mainStage=Math.Max(0,MainStage-1);services.mainStageKillProgress=0;Toast("패배 · 스테이지 "+(MainStage+1)+"에서 다시 시작합니다.");}
            if(game)game.RequestCombatWaveReset();Save();RefreshHud();
        }
        // Field kills and cave rewards share every gold balance value and live bonus.
        public int GoldForMainKills(int displayStage, int count)
        {
            if(count<=0)return 0;
            double unit=Math.Max(0,serviceTuning.goldPerEnemy)*(1+Math.Max(0L,(long)displayStage-1)*Math.Max(0,serviceTuning.goldStageGrowth));
            return (int)Math.Min(int.MaxValue,Math.Max(0,Math.Round(unit*count*GoldGainMultiplier*GoldBuffMultiplier)));
        }
        public int DungeonGoldReward(int stage) => GoldForMainKills(DungeonDifficultyStage(stage),Math.Max(1,serviceTuning.goldDungeonEnemyCount));
        void CreditPendingFieldGold()
        {
            if(!game)return;
            if(game.Kills<lastKills)lastKills=game.Kills;
            if(game.Kills==lastKills)return;
            int earned=ActiveDungeonIndex>=0?0:GoldForMainKills(CombatDifficultyStage,game.Kills-lastKills);
            Gold=SaturatingAdd(Gold,earned);RecordServiceProgress("gold",earned);lastKills=game.Kills;
        }
        public int DungeonRelicReward(int stage) => (int)Math.Min(int.MaxValue,Math.Max(1L,serviceTuning.dungeonRelicTickets)+Math.Max(0L,(long)stage-1));
        public void GrantDungeonRelicTickets(int amount)
        {
            if(services==null||amount<=0)return;
            services.dungeonRelicTickets=(int)Math.Min(int.MaxValue,(long)services.dungeonRelicTickets+amount);Save();
        }
        public bool TrySpendDungeonRelicTickets(int amount)
        {
            if(services==null||amount<=0||DungeonRelicTickets<amount)return false;
            services.dungeonRelicTickets-=amount;Save();return true;
        }
        public bool TrySpendRelicTickets(int amount)
        {
            if (services == null || amount <= 0 || services.relicTickets < amount) return false;
            services.relicTickets -= amount;
            Save();
            return true;
        }

        public void GrantRelicTickets(int amount)
        {
            if (services == null || amount <= 0) return;
            services.relicTickets = (int)Math.Min(int.MaxValue, (long)services.relicTickets + amount);
            Save();
        }

        int DungeonKillsFor(int index) => Math.Max(1, index == 2 ? serviceTuning.relicDungeonKills : serviceTuning.dungeonKills);

        static long SaturatingAdd(long current, int amount) => current > long.MaxValue - amount ? long.MaxValue : current + amount;
    }
}
