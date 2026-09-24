using System;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        // Return a copy: editing the Odin form does not alter live combat until Apply.
        public ServiceTuning ReadBalanceTuning() => JsonUtility.FromJson<ServiceTuning>(JsonUtility.ToJson(serviceTuning));
        public float PlayerKeepDistance => BalanceValue(serviceTuning.playerKeepDistance, 0, .6f);

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
            destination.commonGrowthSteps = DoodleGrowthStep.Copy(source.commonGrowthSteps);
            destination.critical2GrowthSteps = DoodleGrowthStep.Copy(source.critical2GrowthSteps);
            destination.critical4GrowthSteps = DoodleGrowthStep.Copy(source.critical4GrowthSteps);
            destination.commonCurve = source.commonCurve?.Copy() ?? new DoodleGrowthCurve();
            destination.critical2Curve = source.critical2Curve?.Copy() ?? new DoodleGrowthCurve();
            destination.critical4Curve = source.critical4Curve?.Copy() ?? new DoodleGrowthCurve();
        }

        // One price function for the Odin preview, UI quote and actual single/bulk/MAX purchases.
        public static long StatUpgradePrice(UiStatCostTuning tuning, string id, int currentLevel) => (long)StatUpgradePriceAmount(tuning, id, currentLevel);
        public static GameNumber StatUpgradePriceAmount(UiStatCostTuning tuning, string id, int currentLevel)
        {
            int tier = Array.IndexOf(CriticalStatIds, id);
            if (tier > 0) {
                GameNumber start = StatUpgradePriceAmount(tuning, CriticalStatIds[0], CriticalLevelCap(0) - 1);
                float rate = CriticalContinuationGrowth(tuning);
                for (int i = 1; i < tier; i++) start = GrowStatPrice(start, rate, CriticalLevelCap(i) - 1);
                return GrowStatPrice(start, rate, Math.Max(0, currentLevel));
            }
            int cost = tier == 0 ? tuning.critical2BaseCost : tuning.commonBaseCost;
            float growth = BalanceValue(tier == 0 ? tuning.critical2Growth : tuning.commonGrowth, 0, .004f);
            var steps = tier == 0 ? tuning.critical2GrowthSteps : tuning.commonGrowthSteps;
            // Preserve the established integer rounding before normalizing ordinary prices.
            double ordinary = DoodleGrowthStep.Evaluate(Math.Max(1, cost), growth, 0, currentLevel, steps);
            if (ordinary < 1e15) return RoundStatPrice(ordinary);
            return GameNumber.Ceiling(DoodleGrowthStep.EvaluateAmount(Math.Max(1, cost), growth, 0, currentLevel, steps));
        }
        static GameNumber GrowStatPrice(GameNumber start, float rate, int levels)
        {
            if (start.Exponent < 15) {
                double ordinary = DoodleGrowthCurve.Exponential((long)start, rate, levels);
                if (ordinary < 1e15) return RoundStatPrice(ordinary);
            }
            return GameNumber.Ceiling(start * GameNumber.Pow(1d + rate, levels));
        }
        public static float CriticalContinuationGrowth(UiStatCostTuning tuning)
        {
            float rate = BalanceValue(tuning.critical2Growth, 0, .004f); int latest = int.MinValue;
            foreach (var step in tuning.critical2GrowthSteps ?? Array.Empty<DoodleGrowthStep>())
                if (step != null && step.from <= CriticalLevelCap(0) - 1 && step.from >= latest) {
                    latest = step.from; rate = BalanceValue(step.growth, 0, .004f);
                }
            return rate;
        }
        static long RoundStatPrice(double raw)
        {
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
            destination.playerKeepDistance = BalanceValue(source.playerKeepDistance, 0, .6f);
            destination.enemyStartingHealth = BalanceValue(source.enemyStartingHealth, .001f, 68);
            destination.enemyStartingDamage = BalanceValue(source.enemyStartingDamage, 0, 0);
            destination.earlyEnemyDamageEndStage = Math.Max(2, source.earlyEnemyDamageEndStage);
            destination.earlyEnemyDamageMax = BalanceValue(source.earlyEnemyDamageMax, 0, 100);
            destination.goldStageGrowth = BalanceValue(source.goldStageGrowth, 0, .02f);
            destination.enemyHealthStageGrowth = BalanceValue(source.enemyHealthStageGrowth, 0, .02f);
            destination.enemyDamageStageGrowth = BalanceValue(source.enemyDamageStageGrowth, 0, .02f);
            destination.goldGrowthSteps = DoodleGrowthStep.Copy(source.goldGrowthSteps);
            destination.enemyHealthGrowthSteps = DoodleGrowthStep.Copy(source.enemyHealthGrowthSteps);
            destination.enemyDamageGrowthSteps = DoodleGrowthStep.Copy(source.enemyDamageGrowthSteps);
            destination.goldCurve = source.goldCurve?.Copy() ?? new DoodleGrowthCurve();
            destination.enemyHealthCurve = source.enemyHealthCurve?.Copy() ?? new DoodleGrowthCurve();
            destination.enemyDamageCurve = source.enemyDamageCurve?.Copy() ?? new DoodleGrowthCurve();
        }

        public void ApplyBalanceTuning(ServiceTuning tuning)
        {
            if (tuning == null || serviceTuning == null) return;
            CreditPendingFieldGold();
            GameNumber previousHealth = EnemyHealthAmount(CombatDifficultyStage);
            CopyBalanceTuning(tuning, serviceTuning);
            if (game) game.RescaleLivingEnemyHealth(EnemyHealthAmount(CombatDifficultyStage) / previousHealth);
            Save(); RefreshHud();
        }

        public long GrantDebugGold(long amount)
        {
            long granted = Math.Max(0, amount);
            if (granted == 0) return 0;
            GoldAmount += granted;
            Save(); RefreshHud();
            if (!string.IsNullOrEmpty(ActivePage)) RefreshPage();
            return granted;
        }

        public bool DebugSetMainStage(int displayStage)
        {
            if (services == null || !game || !game.Ready) return false;
            // Settle real kills using the old field/dungeon context before switching.
            CreditPendingFieldGold(); TickServices();
            int previousHighest = HighestMainStage;
            services.activeDungeon = -1;
            services.dungeonProgress = 0;
            services.mainStage = Math.Max(1, displayStage) - 1;
            services.highestMainStage = Math.Max(previousHighest, services.mainStage);
            services.mainStageKillProgress = 0;
            lastKills = lastServiceKills = game.Kills;
            StopRepeating(); ClearOverlays();
            game.RestartCombatForStageDebug();
            Save(); RefreshPage();
            return true;
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
        public void RestartCombatForStageDebug()
        {
            if (!Ready) return;
            combatWaveResetRequested = false;
            ReleaseJoystick();
            foreach (var enemy in enemies) { enemy.hp = 0; enemy.root.SetActive(false); ReleaseEnemy(enemy); }
            enemies.Clear(); bananaHitTimes.Clear(); dashVictims.Clear();
            ClearExtraSkills(); ClearParticles(); ClearDamageNumbers();
            foreach (var shot in shots) if (shot.visual) Destroy(shot.visual.gameObject);
            shots.Clear(); stoneVolleys.Clear();
            foreach (var fleck in flecks) if (fleck.visual) Destroy(fleck.visual.gameObject);
            flecks.Clear();
            dashRemaining = swing = bananaCycleAge = 0;
            BananasActive = false;
            foreach (var banana in bananas) if (banana) banana.gameObject.SetActive(false);
            if (player != null) player.body.linearVelocity = Vector2.zero;
            ResetSkillActivation(); ResetExtraSkills();
            // Do not wait for FixedUpdate: the editor action also works while combat is paused.
            Refill();
        }

        public void RescaleLivingEnemyHealth(GameNumber multiplier)
        {
            foreach (var enemy in enemies)
            {
                if (!Alive(enemy)) continue;
                float fraction = (float)GameNumber.Clamp(enemy.hp / GameNumber.Max(.001, enemy.maxHp), 0, 1);
                enemy.maxHp = GameNumber.Max(.001, enemy.maxHp * multiplier);
                enemy.hp = enemy.maxHp * fraction;
                RefreshHealthBar(enemy);
            }
        }
    }
}
