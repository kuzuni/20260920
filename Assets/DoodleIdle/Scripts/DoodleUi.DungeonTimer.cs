using UnityEngine;
namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        public const float DungeonTimeLimit=30;
        public float DungeonTimeRemaining => ActiveDungeonIndex<0?0:Mathf.Clamp(services.dungeonRemaining,0,DungeonTimeLimit);
        public bool TickDungeonChallenge(float dt)
        {
            if(ActiveDungeonIndex<0)return false;
            // Resolve kills from the preceding physics step before checking its deadline.
            TickServices();
            if(ActiveDungeonIndex<0)return true;
            services.dungeonRemaining=Mathf.Max(0,services.dungeonRemaining-Mathf.Max(0,dt));
            serviceProgressDirty=true;
            if(services.dungeonRemaining>0)return false;
            FailDungeonChallenge("시간 초과");return true;
        }
        public void FailDungeonChallenge(string reason)
        {
            int index=ActiveDungeonIndex;if(index<0)return;
            ResetServicePeriods();
            // The reserved key belongs to its entry day. Never credit a new day's quota.
            if(string.IsNullOrEmpty(services.dungeonAttemptDay)||services.dungeonAttemptDay==services.day)
                services.dungeonUsed[index]=Mathf.Max(0,services.dungeonUsed[index]-1);
            services.activeDungeon=-1;services.dungeonProgress=0;services.dungeonRemaining=0;services.dungeonAttemptDay="";
            lastServiceKills=game?game.Kills:lastServiceKills;
            if(game)game.RequestCombatWaveReset();
            Save();RefreshHud();Toast("던전 실패 · "+reason+" · 열쇠는 소모되지 않았어요.");
        }
    }
}
