using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        public GameNumber StatAmount(string id)
        {
            InitCollections();
            var stat = Array.Find(collectionTuning.stats, x => x.id == id);
            return stat == null ? 0 : StatAmountAt(stat, StatLevel(id));
        }
        static GameNumber StatAmountAt(UiStatDefinition stat, int level)
        {
            var value = ((GameNumber)stat.initial + (GameNumber)stat.increment * Math.Max(0, level)) * GameNumber.Pow(Math.Max(1, stat.valueGrowth), Math.Max(0, level));
            value = GameNumber.Max(0, value);
            return IsCriticalChance(stat.id) ? GameNumber.Min(100, value) : value;
        }
        public GameNumber StatAmountAfterUpgrades(string id, int count)
        {
            InitCollections(); var stat = Array.Find(collectionTuning.stats, x => x.id == id);
            return stat == null ? 0 : StatAmountAt(stat, (int)Math.Min(int.MaxValue, Math.Max(0L, (long)StatLevel(id) + count)));
        }
        GameNumber ItemEquipAmount(UiItem item)
        {
            if (IsEquipment(item)) {
                GameNumber enhancement = item.rarity == 6 && item.tier == 1 || item.rarity == 8 ? 1 + (GameNumber)Math.Max(0, item.level - 1) * .01 : AbilityEnhancementMultiplier(item);
                return ((1 + (GameNumber)item.equipValue / 100) * enhancement - 1) * 100;
            }
            return (GameNumber)item.equipValue * (item.category == "Skill" || item.category == "Companion" ? AbilityEnhancementMultiplier(item) : 1 + (GameNumber)Math.Max(0, item.level - 1) * .15);
        }
        GameNumber ItemOwnedAmount(UiItem item) => item.category == "Relic" ? (GameNumber)item.level * collectionTuning.relicStepPercent : (GameNumber)item.ownedPercent * (1 + (GameNumber)Math.Max(0, item.level - 1) * .1);
        GameNumber ItemOwnedGoldAmount(UiItem item) => item.category == "Armor" || item.category == "Necklace" ? 0 : (GameNumber)item.ownedGoldPercent * (1 + (GameNumber)Math.Max(0, item.level - 1) * .1);
        GameNumber EquippedAmount(string category)
        {
            if (snapshotPrepared) return snapshotEquipped.TryGetValue(category, out var cached) ? cached : 0;
            InitCollections(); GameNumber value = 0;
            foreach (var item in collectionItems) if (item.category == category && item.equipped) value += ItemEquipAmount(item);
            return value;
        }
        GameNumber EffectAmount(string effect, string category = null, bool includeSkins = true)
        {
            if (snapshotPrepared && category != null) return snapshotEffects.TryGetValue(category + ":" + effect, out var cached) ? cached : 0;
            InitCollections(); GameNumber value = 0;
            foreach (var item in collectionItems) {
                if (!item.discovered || !(category == null || item.category == category || category == "Equipment" && IsEquipment(item))) continue;
                if (item.effect == effect) value += ItemOwnedAmount(item);
                if (effect == "gold") value += ItemOwnedGoldAmount(item);
            }
            return value + (category == null && includeSkins ? SkinOwnedBonus(effect) : 0);
        }
        GameNumber OwnedAmount(string effect, bool includeSkins = true)
        {
            GameNumber value = 1;
            foreach (string category in OwnedEffectCategories) value *= 1 + EffectAmount(effect, category) / 100;
            return value * (includeSkins ? 1 + (GameNumber)SkinOwnedBonus(effect) / 100 : 1);
        }
        GameNumber CollectionDamageAmount(bool includeSkins) => StatAmount("attack") / BaseStatValue("attack")
            * (1 + EquippedAmount("Club") / 100) * (1 + EffectAmount("attack", "Equipment") / 100)
            * (1 + (EffectAmount("attack", "Skill") + EquippedAmount("Skill") * .02) / 100)
            * (1 + (EffectAmount("attack", "Companion") + EquippedAmount("Companion")) / 100)
            * (1 + EffectAmount("attack", "Relic") / 100) * (includeSkins ? 1 + (GameNumber)SkinOwnedBonus("attack") / 100 : 1);
        public GameNumber CombatDamageAmount { get { InitCollections(); return CollectionDamageAmount(true) / GameNumber.Max(.001, starterDamageBaseline); } }
        public GameNumber AttackAmount => combatSnapshot ? snapshotAttack : DoodleAttackPower.ReferenceAttack * CombatDamageAmount * AttackBuffMultiplier;
        public GameNumber MaxHealthAmount => combatSnapshot ? snapshotHealth : GameNumber.Max(1, StatAmount("health") * (1 + EquippedAmount("Armor") / 100) * OwnedAmount("health"));
        GameNumber NecklaceRecovery(GameNumber equipPercent) => StatAmount("health") * OwnedAmount("health") * equipPercent / 100 * .05;
        public GameNumber HealthRegenAmount => combatSnapshot ? snapshotRegen : (StatAmount("healthRegen") + StatAmount("health") * OwnedAmount("health") * EquippedAmount("Necklace") / 100 * .05) * OwnedAmount("healthRegen");
        public GameNumber CriticalBonusAmount => combatSnapshot ? snapshotCriticalBonus : (OwnedAmount("critDamage") - 1) * 100;
        public GameNumber GoldGainAmount => OwnedAmount("gold");
        public GameNumber CategoryDamageAmount(string category) => combatSnapshot ? snapshotCategories[category == "Skill" ? 1 : category == "Companion" ? 2 : 0] : OwnedAmount(category == "Skill" ? "skillAttack" : category == "Companion" ? "companionAttack" : "basicAttack");
        public GameNumber AttackPercentAmount(GameNumber percent, string category) => AttackAmount * percent / 100 * CategoryDamageAmount(category);
        public GameNumber ExpectedCriticalAmount
        {
            get {
                double remaining = 1; GameNumber expected = 0, bonus = 1 + CriticalBonusAmount / 100;
                for (int i = CriticalStatIds.Length - 1; i >= 0; i--) {
                    double chance = CriticalChance(i) / 100d;
                    expected += remaining * chance * CriticalMultiplierAt(i) * bonus; remaining *= 1 - chance;
                }
                return expected + remaining;
            }
        }
        public GameNumber PowerAmount => GameNumber.Round(BaseStatValue("attack") * CombatDamageAmount * ExpectedCriticalAmount * 70 + MaxHealthAmount + HealthRegenAmount * 20);
        public GameNumber ItemHitAmount(UiItem item) => AttackPercentAmount(ItemHitPercentAmount(item), item.category);
        public GameNumber ItemDpsAmount(UiItem item)
        {
            GameNumber total = item.category == "Companion" ? CompanionWeightAmount(item) * item.volleyCount : (GameNumber)DoodleAttackPower.Skill(item.ability).totalWeight * SkillItemAmount(item);
            return AttackAmount * total / DoodleAttackPower.ReferenceAttack * CategoryDamageAmount(item.category) * ExpectedCriticalAmount / Mathf.Max(.01f, ItemAttackInterval(item));
        }

        // A physics-step snapshot avoids scanning a full late-game collection on every hit.
        // End it in finally: live tuning, inventory changes and direct test edits are visible next step.
        bool combatSnapshot, snapshotPrepared;
        readonly Dictionary<string, GameNumber> snapshotEffects = new Dictionary<string, GameNumber>();
        readonly Dictionary<string, GameNumber> snapshotEquipped = new Dictionary<string, GameNumber>();
        readonly GameNumber[] snapshotCategories = new GameNumber[3];
        readonly float[] snapshotChances = new float[CriticalStatIds.Length];
        GameNumber snapshotAttack, snapshotHealth, snapshotRegen, snapshotCriticalBonus;
        public void BeginCombatSnapshot()
        {
            InitCollections(); snapshotEffects.Clear(); snapshotEquipped.Clear();
            foreach (var item in collectionItems) {
                if (item.equipped) AddSnapshot(snapshotEquipped, item.category, ItemEquipAmount(item));
                if (!item.discovered) continue;
                string category = IsEquipment(item) ? "Equipment" : item.category;
                AddSnapshot(snapshotEffects, category + ":" + item.effect, ItemOwnedAmount(item));
                if (item.ownedGoldPercent != 0) AddSnapshot(snapshotEffects, category + ":gold", ItemOwnedGoldAmount(item));
            }
            snapshotPrepared = true;
            // Derived values must be computed before their own cached getters are enabled.
            snapshotAttack = AttackAmount; snapshotHealth = MaxHealthAmount; snapshotRegen = HealthRegenAmount; snapshotCriticalBonus = CriticalBonusAmount;
            snapshotCategories[0] = CategoryDamageAmount("Basic"); snapshotCategories[1] = CategoryDamageAmount("Skill"); snapshotCategories[2] = CategoryDamageAmount("Companion");
            for (int i = 0; i < snapshotChances.Length; i++) snapshotChances[i] = CriticalChance(i);
            combatSnapshot = true;
        }
        static void AddSnapshot(Dictionary<string, GameNumber> data, string key, GameNumber amount) { data.TryGetValue(key, out var previous); data[key] = previous + amount; }
        public void EndCombatSnapshot() { combatSnapshot = snapshotPrepared = false; }
    }
}
