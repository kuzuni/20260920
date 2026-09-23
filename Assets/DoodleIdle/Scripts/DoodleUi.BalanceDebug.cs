using System;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        // Return a copy: editing the Odin form does not alter live combat until Apply.
        public ServiceTuning ReadBalanceTuning() => JsonUtility.FromJson<ServiceTuning>(JsonUtility.ToJson(serviceTuning));

        public UiStatCostTuning ReadStatCostTuning() => JsonUtility.FromJson<UiStatCostTuning>(JsonUtility.ToJson(collectionTuning.statCosts));

        public static void CopyStatCostTuning(UiStatCostTuning source, UiStatCostTuning destination)
        {
            if (source == null || destination == null) return;
            destination.commonBaseCost = Math.Max(1, source.commonBaseCost);
            destination.critical2BaseCost = Math.Max(1, source.critical2BaseCost);
            destination.critical4BaseCost = Math.Max(1, source.critical4BaseCost);
            destination.commonGrowth = BalanceValue(source.commonGrowth, 0, .004f);
            destination.critical2Growth = BalanceValue(source.critical2Growth, 0, .004f);
            destination.critical4Growth = BalanceValue(source.critical4Growth, 0, .004f);
            destination.commonCurve = source.commonCurve?.Copy() ?? new DoodleGrowthCurve();
            destination.critical2Curve = source.critical2Curve?.Copy() ?? new DoodleGrowthCurve();
            destination.critical4Curve = source.critical4Curve?.Copy() ?? new DoodleGrowthCurve();
        }

        // One price function for the Odin preview, UI quote and actual single/bulk/MAX purchases.
        public static long StatUpgradePrice(UiStatCostTuning tuning, string id, int currentLevel)
        {
            int cost = id == "crit2Chance" ? tuning.critical2BaseCost : id == "crit4Chance" ? tuning.critical4BaseCost : tuning.commonBaseCost;
            float growth = id == "crit2Chance" ? tuning.critical2Growth : id == "crit4Chance" ? tuning.critical4Growth : tuning.commonGrowth;
            double raw = DoodleGrowthCurve.Exponential(Math.Max(1, cost), BalanceValue(growth, 0, .004f), currentLevel);
            // Exponentiation can land a few double-precision ULPs above an integer.
            double nearest = Math.Round(raw);
            if (Math.Abs(raw - nearest) <= Math.Max(1, Math.Abs(raw)) * 8.881784197001252e-16) raw = nearest;
            return double.IsInfinity(raw) || raw >= long.MaxValue ? long.MaxValue : Math.Max(1, (long)Math.Ceiling(raw));
        }

        public void ApplyStatCostTuning(UiStatCostTuning tuning)
        {
            if (tuning == null || collectionTuning == null) return;
            CopyStatCostTuning(tuning, collectionTuning.statCosts);
            RefreshHud();
            if (ActivePage == "Stats") RefreshPage();
        }

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
            destination.goldStageGrowth = BalanceValue(source.goldStageGrowth, 0, .02f);
            destination.enemyHealthStageGrowth = BalanceValue(source.enemyHealthStageGrowth, 0, .02f);
            destination.enemyDamageStageGrowth = BalanceValue(source.enemyDamageStageGrowth, 0, .02f);
            destination.goldCurve = source.goldCurve?.Copy() ?? new DoodleGrowthCurve();
            destination.enemyHealthCurve = source.enemyHealthCurve?.Copy() ?? new DoodleGrowthCurve();
            destination.enemyDamageCurve = source.enemyDamageCurve?.Copy() ?? new DoodleGrowthCurve();
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

        public long GrantDebugGold(long amount)
        {
            long granted = Math.Min(Math.Max(0, amount), long.MaxValue - Math.Max(0, Gold));
            if (granted == 0) return 0;
            Gold = Math.Max(0, Gold) + granted;
            Save(); RefreshHud();
            if (!string.IsNullOrEmpty(ActivePage)) RefreshPage();
            return granted;
        }

        public int GrantDebugDiamonds(int amount)
        {
            int granted = (int)Math.Min(Math.Max(0L, amount), Math.Max(0L, (long)int.MaxValue - Diamonds));
            if (granted == 0) return 0;
            Diamonds += granted;
            Save(); RefreshHud();
            if (!string.IsNullOrEmpty(ActivePage)) RefreshPage();
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
