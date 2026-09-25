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
        public int MainStageKillGoal => BreakthroughMode && MainStage < 99 ? 20 : 50;
        public int MainStageRemaining => Math.Max(0, MainStageKillGoal - MainStageKillProgress);
        public bool BreakthroughMode => services == null || services.breakthroughMode;
        public bool MainBossPending => MainStageKillProgress >= MainStageKillGoal && BreakthroughMode;
        public void ToggleBreakthroughMode()
        {
            bool bossWasPending = MainBossPending;
            services.breakthroughMode = !services.breakthroughMode;
            if (!services.breakthroughMode && bossWasPending)
            {
                services.mainStageKillProgress = 0;
                if (game) game.RequestCombatWaveReset();
            }
            else if (services.breakthroughMode)
                services.mainStageKillProgress = Math.Min(MainStageKillGoal, MainStageKillProgress);
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
                    services.mainStage = (int)Math.Min(int.MaxValue - 1L, (long)services.mainStage + 1);
                services.highestMainStage=Math.Max(services.highestMainStage,services.mainStage);
                services.mainStageKillProgress = 0;
                RequestCombatSave();
                if (UnlockedSkillSlots != previousSkillSlots && ActivePage == "Skills") RefreshPage();
            }
            else
            {
                int previous = services.mainStageKillProgress;
                services.mainStageKillProgress = Math.Min(MainStageKillGoal, services.mainStageKillProgress + 1);
                if (previous < MainStageKillGoal && services.mainStageKillProgress == MainStageKillGoal)
                {
                    if (!BreakthroughMode) services.mainStageKillProgress = 0;
                    RequestCombatSave();
                }
            }
        }
        bool combatSavePending;
        float nextCombatSave;
        void RequestCombatSave() { if (combatSnapshot) combatSavePending = true; else Save(); }
        void FlushCombatSave()
        {
            if (!combatSavePending || Time.unscaledTime < nextCombatSave) return;
            nextCombatSave = Time.unscaledTime + 1;
            Save();
        }
        public int HighestDungeonStage { get { int highest=0;foreach(int index in DungeonIndices)highest=Math.Max(highest,GetDungeonStage(index));return highest; } }
        public int GetDungeonStage(int index) => services == null || !IsDungeon(index) ? 0 : services.dungeonStages[index];
        public int RelicTickets => services == null ? 0 : services.relicTickets;
        // Compatibility adapters for older integrations; all rewards use the ordinary relic wallet.
        public int DungeonRelicTickets => RelicTickets;
        public int DungeonChallengeStage(int index) => (int)Math.Min(int.MaxValue, (long)GetDungeonStage(index) + 1);
        public static int DungeonDifficultyStage(int stage) => (int)Math.Min(int.MaxValue, Math.Max(1L, stage) * 50);
        public int CombatDifficultyStage => ActiveDungeonIndex < 0 ? (int)Math.Min(int.MaxValue, (long)MainStage+1) : DungeonDifficultyStage(DungeonChallengeStage(ActiveDungeonIndex));
        public int DungeonThemeIndex => ActiveDungeonIndex == 0 ? 8 : 6;
        public int HighestMainStage => services==null?0:Math.Max(services.highestMainStage,services.mainStage);
        public int ProjectedStatLevel(int displayStage)
        {
            double completed=Math.Max(0L,(long)displayStage-1);
            double gold=101*serviceTuning.goldPerEnemy*(completed+serviceTuning.goldStageGrowth*completed*(completed-1)/2)*serviceTuning.projectedGoldMultiplier
                +completed*serviceTuning.projectedMissionGoldPerStage;
            // Historical smoke-fixture projection; each cost group now has its own curve.
            double spent=0;
            for (int level=0;level<collectionTuning.maxStatLevel;level++) {
                spent += 3d*StatUpgradePrice(collectionTuning.statCosts,"attack",level)
                    + StatUpgradePrice(collectionTuning.statCosts,"crit2Chance",level);
                if(spent>gold)return level;
            }
            return collectionTuning.maxStatLevel;
        }
        public GameNumber EnemyHealthAmount(int displayStage) => EnemyHealthAmount(serviceTuning, displayStage);
        public static GameNumber EnemyHealthAmount(ServiceTuning tuning, int displayStage) => GameNumber.Max(.001, DoodleGrowthStep.EvaluateAmount(Math.Max(.001, tuning.enemyStartingHealth), tuning.enemyHealthStageGrowth, 1, displayStage, tuning.enemyHealthGrowthSteps));
        public float EnemyHealthMultiplier(int displayStage) => (float)(EnemyHealthAmount(displayStage) / 68);
        public static float EnemyHealthMultiplier(ServiceTuning tuning, int displayStage) => (float)(EnemyHealthAmount(tuning, displayStage) / 68);
        public GameNumber EnemyDamageAmount(int displayStage) => EnemyDamageAmount(serviceTuning, displayStage);
        public static GameNumber EnemyDamageAmount(ServiceTuning tuning, int displayStage)
        {
            int earlyEnd = Math.Max(2, tuning.earlyEnemyDamageEndStage);
            double start = Math.Max(0, tuning.enemyStartingDamage), earlyMax = Math.Max(0, tuning.earlyEnemyDamageMax);
            return displayStage <= earlyEnd ? (GameNumber)(start + (earlyMax - start) * Math.Max(0L, (long)displayStage - 1) / (earlyEnd - 1))
                : DoodleGrowthStep.EvaluateAmount(earlyMax, tuning.enemyDamageStageGrowth, earlyEnd, displayStage, tuning.enemyDamageGrowthSteps);
        }
        public float EnemyDamageMultiplier(int displayStage) => (float)(EnemyDamageAmount(displayStage) / 64);
        public static float EnemyDamageMultiplier(ServiceTuning tuning, int displayStage) => (float)(EnemyDamageAmount(tuning, displayStage) / 64);
        public void HandlePlayerDefeat()
        {
            if(services==null)return;
            CreditPendingFieldGold();
            if(ActiveDungeonIndex>=0){services.activeDungeon=-1;services.dungeonProgress=0;Toast("던전 도전에 실패했어요.");}
            else if (game && game.BossActive) { FailBossChallenge("플레이어 사망"); return; }
            else { Save(); RefreshHud(); return; }
            if(game)game.RequestCombatWaveReset();Save();RefreshHud();
        }
        // Field kills and cave rewards share every gold balance value and live bonus.
        public GameNumber GoldForMainKillsAmount(int displayStage, int count) => GoldForMainKillsAmount(serviceTuning, displayStage, count, GoldGainAmount * GoldBuffMultiplier);
        public static GameNumber GoldForMainKillsAmount(ServiceTuning tuning, int displayStage, int count, GameNumber bonusMultiplier)
        {
            if (count <= 0) return 0;
            var unit = DoodleGrowthStep.EvaluateAmount(Math.Max(0, tuning.goldPerEnemy), tuning.goldStageGrowth, 1, displayStage, tuning.goldGrowthSteps);
            return GameNumber.Round(GameNumber.Max(0, unit * count * bonusMultiplier));
        }
        public int GoldForMainKills(int displayStage, int count) => (int)GoldForMainKillsAmount(displayStage, count);
        public static int GoldForMainKills(ServiceTuning tuning, int displayStage, int count, double bonusMultiplier = 1) => (int)GoldForMainKillsAmount(tuning, displayStage, count, bonusMultiplier);
        public GameNumber DungeonGoldAmount(int stage) => GoldForMainKillsAmount(DungeonDifficultyStage(stage), Math.Max(1, serviceTuning.goldDungeonEnemyCount));
        public int DungeonGoldReward(int stage) => (int)DungeonGoldAmount(stage);
        void CreditPendingFieldGold()
        {
            if (!game) return;
            if (game.Kills < lastKills) lastKills = game.Kills;
            if (game.Kills == lastKills) return;
            GameNumber earned = ActiveDungeonIndex >= 0 ? 0 : GoldForMainKillsAmount(CombatDifficultyStage, game.Kills - lastKills);
            GoldAmount += earned; RecordServiceProgress("gold", (int)earned); lastKills = game.Kills;
        }
        public int DungeonTicketReward(int stage) => (int)Math.Min(int.MaxValue,10L+Math.Max(0L,(long)stage-1));
        public int DungeonRelicReward(int stage) => DungeonTicketReward(stage);
        bool CanReceiveDungeonReward(int index,int stage) => index==0 || (long)SummonTickets(DungeonTicketCategory(index))+DungeonTicketReward(stage)<=int.MaxValue;
        public void GrantDungeonRelicTickets(int amount) => GrantRelicTickets(amount);
        public bool TrySpendDungeonRelicTickets(int amount) => TrySpendRelicTickets(amount);
        void TransferLegacyRelicTickets()
        {
            // Keep any overflow in the retired save field until wallet space is available.
            int transfer=Math.Min(services.dungeonRelicTickets,int.MaxValue-services.relicTickets);
            services.relicTickets+=transfer;services.dungeonRelicTickets-=transfer;
        }
        public bool TrySpendRelicTickets(int amount)
        {
            if (services == null || amount <= 0 || services.relicTickets < amount) return false;
            services.relicTickets -= amount;
            TransferLegacyRelicTickets();
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
