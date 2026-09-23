using System;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        // Return a copy: editing the Odin form does not alter live combat until Apply.
        public ServiceTuning ReadBalanceTuning() => JsonUtility.FromJson<ServiceTuning>(JsonUtility.ToJson(serviceTuning));

        static float BalanceValue(float value, float minimum, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, 1000000);

        public static void CopyBalanceTuning(ServiceTuning source, ServiceTuning destination)
        {
            if (source == null || destination == null) return;
            destination.goldPerEnemy = BalanceValue(source.goldPerEnemy, 0, 10);
            destination.enemyStartingHealth = BalanceValue(source.enemyStartingHealth, .001f, 68);
            destination.enemyStartingDamage = BalanceValue(source.enemyStartingDamage, 0, 0);
            destination.earlyEnemyDamageEndStage = Math.Max(2, source.earlyEnemyDamageEndStage);
            destination.earlyEnemyDamageMax = BalanceValue(source.earlyEnemyDamageMax, 0, 100);
            destination.goldRewardMultiplier = BalanceValue(source.goldRewardMultiplier, 0, 1);
            destination.goldStageGrowth = BalanceValue(source.goldStageGrowth, 0, .02f);
            destination.enemyHealthBaseMultiplier = BalanceValue(source.enemyHealthBaseMultiplier, .001f, 1);
            destination.enemyHealthStageGrowth = BalanceValue(source.enemyHealthStageGrowth, 0, .02f);
            destination.enemyDamageBaseMultiplier = BalanceValue(source.enemyDamageBaseMultiplier, 0, 1);
            destination.enemyDamageStageGrowth = BalanceValue(source.enemyDamageStageGrowth, 0, .02f);
        }

        public void ApplyBalanceTuning(ServiceTuning tuning)
        {
            if (tuning == null || serviceTuning == null) return;
            CreditPendingFieldGold();
            float previousHealth = EnemyHealthMultiplier(CombatDifficultyStage);
            CopyBalanceTuning(tuning, serviceTuning);
            if (game) game.RescaleLivingEnemyHealth(EnemyHealthMultiplier(CombatDifficultyStage) / previousHealth);
            Save(); RefreshHud();
        }

        public int GrantDebugDiamonds(int amount)
        {
            int granted = (int)Math.Min(Math.Max(0L, amount), Math.Max(0L, (long)int.MaxValue - Diamonds));
            if (granted == 0) return 0;
            Diamonds += granted;
            Save(); RefreshHud();
            return granted;
        }
    }

    public sealed partial class DoodleIdleGame
    {
        public void RescaleLivingEnemyHealth(float multiplier)
        {
            foreach (var enemy in enemies)
            {
                if (!Alive(enemy)) continue;
                float fraction = Mathf.Clamp01(enemy.hp / Mathf.Max(.001f, enemy.maxHp));
                enemy.maxHp = (float)Math.Min(1e30, Math.Max(.001, (double)enemy.maxHp * multiplier));
                enemy.hp = enemy.maxHp * fraction;
                RefreshHealthBar(enemy);
            }
        }
    }
}
