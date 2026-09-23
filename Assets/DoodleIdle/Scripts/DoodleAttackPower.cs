using UnityEngine;

namespace DoodleIdle
{
    // Existing combat weights are calibrated against the original 128 attack profile.
    // They are coefficients, never flat damage: every hit uses current player attack.
    public static class DoodleAttackPower
    {
        public const float ReferenceAttack = 128;
        public static float Percent(float weight) => weight * 100 / ReferenceAttack;

        public struct Splash
        {
            public float radius, fraction;
            public int particleIndex;
            public Splash(float radius, float fraction, int particleIndex) { this.radius = radius; this.fraction = fraction; this.particleIndex = particleIndex; }
        }
        // Small secondary impacts supplement direct-hit attacks; existing area attacks keep full damage.
        public static Splash SkillSplash(string ability)
        {
            switch (ability) {
                case "Stone": return new Splash(1.15f, .5f, -1);
                case "Arrows": return new Splash(.6f, .3f, 19);
                case "Fire": return new Splash(1.15f, .5f, 18);
                case "Shotgun": return new Splash(.5f, .3f, 19);
                case "BouncyBall": return new Splash(.65f, .3f, -1);
                case "Durian": return new Splash(.8f, .3f, 16);
                case "Lightning": return new Splash(1f, .5f, 9);
                case "Cloud": return new Splash(.75f, .3f, 10);
                case "RedCloud": return new Splash(.9f, .3f, 10);
                case "DoubleClaw": return new Splash(.8f, .4f, 19);
                case "FireGolem": return new Splash(1.8f, .5f, 23);
                case "SolarVolley": return new Splash(1.4f, .5f, 21);
                case "Golem": return new Splash(1.4f, .5f, -1);
                default: return default;
            }
        }

        public struct Estimate
        {
            public float hitWeight, totalWeight;
            public string basis;
            public Estimate(float hit, float count, string note) { hitWeight = hit; totalWeight = hit * count; basis = note; }
        }
        public static Estimate Skill(string ability)
        {
            switch (ability)
            {
                case "BladeRing": return new Estimate(30, 22, "보라색 칼날 22개 · 0.04초 간격 원형 발사");
                case "FireGolem": return new Estimate(32, 160, "불골렘 10마리 · 10초 공격");
                case "CactusRage": return new Estimate(42, 4, "선인장 4개 관통");
                case "FireTornado": return new Estimate(18, 39, "7초 동안 범위 지속 피해");
                case "RazorShuriken": return new Estimate(45, 18, "칼바람 표창 18개 관통");
                case "MightyDragon": return new Estimate(13, 18, "큰 용 6초 접촉 + 불꽃 공격") { totalWeight = 13 * 18 + 12 * 3 * 19 };
                case "GodHand": return new Estimate(150, 3, "손바닥 3개 · 넓은 범위 타격");
                case "MissileRage": return new Estimate(55, 13, "미사일 13발 · 폭발당 적 1명");
                case "SawSnakes": return new Estimate(8, 170, "톱날뱀 5마리 · 각각 6초 접촉");
                case "SolarVolley": return new Estimate(57.6f, 21, "태양 3개 × 7회 튕김");
                case "Banana": return new Estimate(19, 23, "적 1명에게 8초 접촉 · 약 23타");
                case "Stone": return new Estimate(42, 3, "돌 3개 모두 명중");
                case "Arrows": return new Estimate(22, 10, "화살 10발 모두 명중");
                case "BouncyBall": return new Estimate(24, 7, "총 7회 튕김 명중");
                case "Fire": return new Estimate(38, 3, "유도 불꽃 3발 모두 명중");
                case "Cannon": return new Estimate(46, 12, "10초간 약 12발 · 발당 적 1명");
                case "Shotgun": return new Estimate(15, 20, "산탄 20발 모두 명중");
                case "Sound": return new Estimate(24, 5, "음파 5발 · 발당 적 1명");
                case "TetherSnake": return new Estimate(8, 34, "적 1명에게 6초 접촉 · 약 34타");
                case "Cloud": case "RedCloud": return new Estimate(23, 36, "8초간 약 12회 · 회당 적 3명");
                case "Lightning": return new Estimate(35, 3, "적 3명에게 1타씩");
                case "Molotov": return new Estimate(18, 1, "적 1명 · 충돌 + 4초 화상 12타") { totalWeight = 18 + 8 * 12 };
                case "Dragon": return new Estimate(13, 18, "적 1명에게 6초 접촉 · 약 18타");
                case "FireRing": return new Estimate(19, 24, "불꽃 24개 · 발당 적 1명");
                case "WaveSnakes": return new Estimate(13, 50, "뱀 5마리 · 각각 3.5초 접촉");
                case "BrickVolley": return new Estimate(30, 6, "벽돌 6개 · 발당 적 1명");
                case "Eggplant": return new Estimate(42, 2, "오이 2개 · 발당 적 1명");
                case "Durian": return new Estimate(48, 21, "두리안 3개 × 7회 튕김");
                case "Shuriken": return new Estimate(30, 8, "표창 8개 · 발당 적 1명");
                case "GiantWorm": return new Estimate(30, 17, "적 1명에게 6.5초 접촉 · 약 17타");
                case "IceSnakes": return new Estimate(8, 102, "얼음 뱀 3마리 · 각각 6초 접촉");
                case "PurpleFireArrows": return new Estimate(44, 8, "8방향 순차 관통 · 발당 적 1명 명중");
                case "BlueMolotov": return new Estimate(18, 2, "2병 각각 적 1명 · 충돌 + 4초 화상") { totalWeight = (18 + 8 * 12) * 2 };
                case "RedWave": return new Estimate(22, 5, "검기 5발 · 발당 적 1명");
                case "Tornado": return new Estimate(18, 39, "적 1명에게 7초 접촉 · 약 39타");
                case "DoubleClaw": return new Estimate(38, 10, "적 5명에게 2타씩");
                case "Golem": return new Estimate(32, 80, "골렘 5마리 · 이동 없이 10초 공격");
                case "Dumbbell": return new Estimate(40, 10, "덤벨 10개 · 발당 적 1명");
                case "Meteor": return new Estimate(150, 3, "메테오 3개 · 폭발당 적 1명 기준");
                default: return default;
            }
        }
    }

    public sealed partial class DoodleUi
    {
        public float CurrentAttackPower => DoodleAttackPower.ReferenceAttack * UiDamageMultiplier;
        public float AttackCategoryMultiplier(string category) => 1 + EffectBonus(category == "Skill" ? "skillAttack" : category == "Companion" ? "companionAttack" : "basicAttack") / 100;
        public float AttackPercentDamage(float percent, string category) => CurrentAttackPower * percent / 100 * AttackCategoryMultiplier(category);
        float AbilityEnhancementMultiplier(UiItem item) => 1 + Mathf.Clamp01(Mathf.Max(0, item.level - 1) / (float)Mathf.Max(1, ItemMaxLevel(item) - 1)) * collectionTuning.abilityMaxEnhancementBonus;
        float AbilityDpsPercent(UiItem item)
        {
            var grades = item.category == "Companion" ? collectionTuning.companionDpsPercentByGrade : collectionTuning.skillDpsPercentByGrade;
            return grades[Mathf.Clamp(item.rarity, 0, grades.Length - 1)] * item.damageMultiplier * AbilityEnhancementMultiplier(item);
        }
        // Allocate the grade's damage-per-second budget over the actual cast interval
        // and hit count. Fast volleys and long summons therefore share the same scale.
        public float SkillItemPower(UiItem item) => DoodleAttackPower.ReferenceAttack * AbilityDpsPercent(item) / 100 * ItemAttackInterval(item) / Mathf.Max(.01f, DoodleAttackPower.Skill(item.ability).totalWeight);
        public float CompanionHitWeight(UiItem item) => DoodleAttackPower.ReferenceAttack * AbilityDpsPercent(item) / 100 * ItemAttackInterval(item) / Mathf.Max(1, item.volleyCount);
        public float SkillPowerMultiplier(string ability)
        {
            foreach (var item in collectionItems) if (item.category == "Skill" && item.ability == ability) return SkillItemPower(item);
            return 1;
        }
        public float ItemHitPercent(UiItem item) => DoodleAttackPower.Percent(item.category == "Companion" ? CompanionHitWeight(item) : DoodleAttackPower.Skill(item.ability).hitWeight * SkillItemPower(item));
        public float ItemHitDamage(UiItem item) => AttackPercentDamage(ItemHitPercent(item), item.category);
        public float ItemSplashFraction(UiItem item) => item.category == "Companion" ? (item.explosionRadius > 0 ? item.splashDamageMultiplier : 0) : DoodleAttackPower.SkillSplash(item.ability).fraction;
        public float ItemAttackInterval(UiItem item) => item.category == "Companion" ? Mathf.Max(.01f, item.attackInterval) : game ? game.SkillInterval(item.ability) : item.cooldown;
        public double ItemExpectedDps(UiItem item)
        {
            float total = item.category == "Companion" ? CompanionHitWeight(item) * item.volleyCount : DoodleAttackPower.Skill(item.ability).totalWeight * SkillItemPower(item);
            return AttackPercentDamage(DoodleAttackPower.Percent(total), item.category) * ExpectedCriticalMultiplier / Mathf.Max(.01f, ItemAttackInterval(item));
        }
    }
}
