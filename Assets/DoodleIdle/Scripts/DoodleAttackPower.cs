using UnityEngine;

namespace DoodleIdle
{
    // Existing combat weights are calibrated against the original 128 attack profile.
    // They are coefficients, never flat damage: every hit uses current player attack.
    public static class DoodleAttackPower
    {
        public const float ReferenceAttack = 128;
        public static float Percent(float weight) => weight * 100 / ReferenceAttack;
        public static float CompanionWeight(UiItem item) => (12 + item.rarity * 4) * (1 + Mathf.Max(0, item.level - 1) * .03f);

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
                case "Eggplant": return new Estimate(42, 3, "가지 3개 · 발당 적 1명");
                case "Durian": return new Estimate(48, 21, "두리안 3개 × 7회 튕김");
                case "Shuriken": return new Estimate(30, 8, "표창 8개 · 발당 적 1명");
                case "GiantWorm": return new Estimate(30, 17, "적 1명에게 6.5초 접촉 · 약 17타");
                case "IceSnakes": return new Estimate(8, 102, "얼음 뱀 3마리 · 각각 6초 접촉");
                case "PurpleFireArrows": return new Estimate(44, 8, "보라 불화살 8발 모두 명중");
                case "BlueMolotov": return new Estimate(18, 2, "2병 각각 적 1명 · 충돌 + 4초 화상") { totalWeight = (18 + 8 * 12) * 2 };
                case "RedWave": return new Estimate(22, 5, "검기 5발 · 발당 적 1명");
                case "Tornado": return new Estimate(18, 39, "적 1명에게 7초 접촉 · 약 39타");
                case "DoubleClaw": return new Estimate(38, 10, "적 5명에게 2타씩");
                case "Golem": return new Estimate(32, 80, "골렘 5마리 · 이동 없이 10초 공격");
                case "Dumbbell": return new Estimate(40, 10, "덤벨 10개 · 발당 적 1명");
                case "Meteor": return new Estimate(150, 1, "폭발 범위 내 적 1명 기준");
                default: return default;
            }
        }
    }

    public sealed partial class DoodleUi
    {
        public float CurrentAttackPower => DoodleAttackPower.ReferenceAttack * UiDamageMultiplier;
        public float AttackCategoryMultiplier(string category) => 1 + EffectBonus(category == "Skill" ? "skillAttack" : category == "Companion" ? "companionAttack" : "basicAttack") / 100;
        public float AttackPercentDamage(float percent, string category) => CurrentAttackPower * percent / 100 * AttackCategoryMultiplier(category);
        public float ItemHitPercent(UiItem item) => DoodleAttackPower.Percent(item.category == "Companion" ? DoodleAttackPower.CompanionWeight(item) : DoodleAttackPower.Skill(item.ability).hitWeight);
        public float ItemHitDamage(UiItem item) => AttackPercentDamage(ItemHitPercent(item), item.category);
        public float ItemAttackInterval(UiItem item) => item.category == "Companion" ? Mathf.Max(.01f, item.attackInterval) : game ? game.SkillInterval(item.ability) : item.cooldown;
        public double ItemExpectedDps(UiItem item)
        {
            float total = item.category == "Companion" ? DoodleAttackPower.CompanionWeight(item) * item.volleyCount : DoodleAttackPower.Skill(item.ability).totalWeight;
            return AttackPercentDamage(DoodleAttackPower.Percent(total), item.category) * ExpectedCriticalMultiplier / Mathf.Max(.01f, ItemAttackInterval(item));
        }
    }
}
