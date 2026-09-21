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
                if (MainBossPending)
                    services.mainStage = (int)Math.Min(int.MaxValue, (long)services.mainStage + 1);
                services.mainStageKillProgress = 0;
                Save();
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
        public int MainMissionReward => 500;
        public int MainMissionNumber => services == null ? 1 : (int)Math.Min(int.MaxValue, services.mainMissionIndex + 1L);
        public bool CanClaimMainMission => services != null && MainMissionProgress >= MainMissionGoal;
        public float MainMissionFraction => services == null ? 0 : Mathf.Clamp01((float)(MainMissionProgress / (double)Math.Max(1, MainMissionGoal)));

        // The first mission preserves the existing attack-level-15 objective. The
        // following four-objective cycle has increasing lifetime goals and no daily
        // lockout: field kills, earned gold, attack upgrades, completed main stages.
        int MainMissionKind => services == null || services.mainMissionIndex == 0 ? -1 : (services.mainMissionIndex - 1) % 4;
        long MainMissionCycle => services == null || services.mainMissionIndex == 0 ? 0 : (services.mainMissionIndex - 1L) / 4 + 1;
        long MainMissionGoal
        {
            get
            {
                switch (MainMissionKind)
                {
                    case 0: return MainMissionCycle * 100;
                    case 1: return MainMissionCycle * 5000;
                    case 2: return Math.Min(int.MaxValue, 15 + MainMissionCycle * 5);
                    case 3: return Math.Min(int.MaxValue, MainMissionCycle * 5);
                    default: return 15;
                }
            }
        }
        long MainMissionProgress
        {
            get
            {
                if (services == null) return 0;
                switch (MainMissionKind)
                {
                    case 0: return services.mainKills;
                    case 1: return services.earnedGold;
                    case 3: return MainStage;
                    default: return AttackStatLevel;
                }
            }
        }
        public string MainMissionText
        {
            get
            {
                string goal = UiNumber.Format(MainMissionGoal);
                string objective;
                switch (MainMissionKind)
                {
                    case 0: objective = "필드 적 " + goal + "마리 처치"; break;
                    case 1: objective = "골드 " + goal + " 획득"; break;
                    case 3: objective = "메인 " + goal + "단계 클리어"; break;
                    default: objective = "공격력 " + goal + "단계 달성"; break;
                }
                return "미션 " + MainMissionNumber + ".\n" + objective + "\n(" + UiNumber.Format(Math.Min(MainMissionProgress, MainMissionGoal)) + "/" + goal + ")";
            }
        }

        public bool ClaimMainMission()
        {
            if (!CanClaimMainMission || services.mainMissionIndex == int.MaxValue) return false;
            // Advance the persisted mission before opening the reward view. Repeated
            // clicks therefore cannot claim the same completed objective twice.
            services.mainMissionIndex++;
            Diamonds += MainMissionReward;
            Save(); RefreshPage();
            ShowRewards("미션 보상 획득!", new List<UiReward> { new UiReward { name = "", icon = "Diamond", amount = MainMissionReward, rarity = 0 } });
            return true;
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

        int DungeonKillsFor(int index) => Math.Max(1, index == 1 ? serviceTuning.diamondDungeonKills : index == 2 ? serviceTuning.relicDungeonKills : serviceTuning.dungeonKills);

        static long SaturatingAdd(long current, int amount) => current > long.MaxValue - amount ? long.MaxValue : current + amount;
    }
}
