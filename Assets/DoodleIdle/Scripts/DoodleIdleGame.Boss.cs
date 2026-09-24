using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public const float BossTimeLimit = 10;
        float bossTimeRemaining;
        public float BossTimeRemaining => BossActive ? bossTimeRemaining : 0;
        public float BossHealthFraction {
            get { var boss = enemies.Find(x => x.isBoss); return boss == null ? 0 : (float)GameNumber.Clamp(boss.hp / boss.maxHp, 0, 1); }
        }
        bool TickBossChallenge(float dt)
        {
            if (!BossActive) return false;
            bossTimeRemaining = Mathf.Max(0, bossTimeRemaining - dt);
            if (bossTimeRemaining > .0001f) return false;
            if (Ui) Ui.FailBossChallenge("시간 초과");
            return true;
        }
    }
}
