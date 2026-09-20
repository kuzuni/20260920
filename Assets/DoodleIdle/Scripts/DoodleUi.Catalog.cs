using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    [Serializable]
    public sealed class UiItem
    {
        public string id, name, icon, category, effect, description, ability;
        public int rarity, count, level, slot;
        public bool equipped, discovered;
        public float ownedPercent, equipValue, cooldown;
    }

    [Serializable]
    public sealed class UiStatDefinition
    {
        public string id, name, icon;
        public float initial, increment;
        public int baseCost;
    }

    [Serializable]
    public sealed class UiCollectionTuning
    {
        public float costGrowth = 1.08f;
        public int maxStatLevel = 10000, maxItemLevel = 1000, copiesPerUpgrade = 5;
        public int relicBaseCost = 600;
        public int[] gradeWeights = { 60, 25, 10, 4, 1 };
        public float relicCostGrowth = 1.16f, relicStepPercent = 2, relicChanceLoss = .035f, relicMinimumChance = .2f;
        public UiStatDefinition[] stats;
        public UiItem[] items;
    }

    public sealed partial class DoodleUi
    {
        const string CollectionsSaveKey = "DoodleUi.Collections.v1";
        public int[] GradeWeights => collectionTuning.gradeWeights;
        public static readonly string[] GradeNames = { "일반", "고급", "희귀", "영웅", "전설" };
        readonly List<UiItem> collectionItems = new List<UiItem>();
        readonly Dictionary<string, int> statLevels = new Dictionary<string, int>();
        UiCollectionTuning collectionTuning;
        float starterDamageBaseline = 1, starterSpeedBaseline = 1;
        readonly System.Random collectionRandom = new System.Random();

        [Serializable] sealed class ItemSave { public string id; public int count, level, slot; public bool equipped, discovered; }
        [Serializable] sealed class StatSave { public string id; public int level; }
        [Serializable] sealed class CollectionSave { public List<ItemSave> items = new List<ItemSave>(); public List<StatSave> stats = new List<StatSave>(); }

        public void InitCollections()
        {
            if (collectionTuning != null) return;
            var asset = Resources.Load<TextAsset>("DoodleIdle/UI/Collections");
            if (asset == null) throw new InvalidOperationException("Missing DoodleIdle/UI/Collections tuning data.");
            collectionTuning = JsonUtility.FromJson<UiCollectionTuning>(asset.text);
            if (collectionTuning == null || collectionTuning.items == null || collectionTuning.stats == null)
                throw new InvalidOperationException("Invalid collection tuning data.");
            if (collectionTuning.gradeWeights == null || collectionTuning.gradeWeights.Length != 5)
                throw new InvalidOperationException("Exactly five grade probabilities are required.");
            int gradeTotal = 0;
            foreach (int weight in GradeWeights) { if (weight < 0) throw new InvalidOperationException("Grade weights cannot be negative."); gradeTotal += weight; }
            if (gradeTotal != 100) throw new InvalidOperationException("Grade probabilities must sum to 100 percent.");
            collectionItems.AddRange(collectionTuning.items);
            foreach (var stat in collectionTuning.stats) statLevels[stat.id] = 0;
            // The original arena is already balanced for the starter profile. Capture the
            // pristine catalog BEFORE restoring saves, so a new profile produces exactly
            // 1x original damage/speed and only progression or loadout changes scale it.
            // This baseline follows tuning data rather than silently rebasing saved upgrades.
            starterDamageBaseline = RawCombatDamageMultiplier;
            starterSpeedBaseline = RawCombatSpeedMultiplier;
            string saved = PlayerPrefs.GetString(CollectionsSaveKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                try
                {
                    var state = JsonUtility.FromJson<CollectionSave>(saved);
                    if (state != null && state.items != null)
                        foreach (var entry in state.items)
                        {
                            var item = collectionItems.Find(x => x.id == entry.id);
                            if (item == null) continue;
                            item.count = Math.Max(0, entry.count);
                            item.discovered = entry.discovered || item.count > 0 || entry.level > 0;
                            item.level = item.discovered ? Mathf.Clamp(entry.level, 1, collectionTuning.maxItemLevel) : 0;
                            item.equipped = entry.equipped && item.discovered && item.category != "Relic";
                            item.slot = Math.Max(0, entry.slot);
                        }
                    if (state != null && state.stats != null)
                        foreach (var entry in state.stats)
                            if (statLevels.ContainsKey(entry.id)) statLevels[entry.id] = Mathf.Clamp(entry.level, 0, collectionTuning.maxStatLevel);
                }
                catch (ArgumentException) { Debug.LogWarning("Collection save could not be read; using local starter data."); }
            }
            foreach (string category in new[] { "Armor", "Club", "Skill", "Companion" }) NormalizeEquipment(category);
        }

        public void SaveCollections()
        {
            if (collectionTuning == null) return;
            var saved = new CollectionSave();
            foreach (var item in collectionItems)
                saved.items.Add(new ItemSave { id = item.id, count = item.count, level = item.level, slot = item.slot, equipped = item.equipped, discovered = item.discovered });
            foreach (var pair in statLevels) saved.stats.Add(new StatSave { id = pair.Key, level = pair.Value });
            PlayerPrefs.SetString(CollectionsSaveKey, JsonUtility.ToJson(saved));
        }

        public List<UiItem> Items(string category)
        {
            InitCollections();
            return collectionItems.FindAll(x => x.category == category);
        }

        // A roll selects a catalog entry only. The caller grants it explicitly after charging currency.
        public UiItem GrantItem(string category, System.Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var items = Items(category);
            if (items.Count == 0) throw new ArgumentException("Unknown collection category", nameof(category));
            int roll = rng.Next(100), grade = 0;
            while (grade < GradeWeights.Length - 1 && roll >= GradeWeights[grade]) { roll -= GradeWeights[grade]; grade++; }
            var choices = items.FindAll(x => x.rarity == grade);
            if (choices.Count == 0) throw new InvalidOperationException("Every draw category must contain every rarity.");
            return choices[rng.Next(choices.Count)];
        }

        public double GradeProbability(string category, int rarity)
        {
            InitCollections();
            return rarity >= 0 && rarity < GradeWeights.Length && Items(category).Exists(x => x.rarity == rarity) ? GradeWeights[rarity] : 0;
        }

        public double ItemProbability(UiItem item)
        {
            if (item == null) return 0;
            int count = Items(item.category).FindAll(x => x.rarity == item.rarity).Count;
            return count == 0 ? 0 : GradeProbability(item.category, item.rarity) / count;
        }

        public void AddItem(UiItem item, int count)
        {
            if (item == null || count <= 0 || !collectionItems.Contains(item)) return;
            item.count = (int)Math.Min(int.MaxValue, (long)item.count + count);
            item.discovered = true;
            item.level = Math.Max(1, item.level);
        }

        public List<UiItem> EquippedSkills => EquippedItems("Skill");
        public List<UiItem> EquippedCompanions => EquippedItems("Companion");
        List<UiItem> EquippedItems(string category)
        {
            var equipped = Items(category).FindAll(x => x.equipped && x.discovered);
            equipped.Sort((a, b) => a.slot.CompareTo(b.slot));
            return equipped;
        }
        int EquipLimit(string category) => category == "Skill" ? 8 : category == "Companion" ? 5 : category == "Relic" ? 0 : 1;
        void NormalizeEquipment(string category)
        {
            var equipped = EquippedItems(category);
            for (int i = 0; i < equipped.Count; i++) { equipped[i].equipped = i < EquipLimit(category); equipped[i].slot = i; }
        }

        public int StatLevel(string id) { InitCollections(); return statLevels.TryGetValue(id, out int level) ? level : 0; }
        public int AttackStatLevel => StatLevel("attack");
        public float StatValue(string id)
        {
            InitCollections();
            foreach (var stat in collectionTuning.stats)
                if (stat.id == id) return stat.initial + stat.increment * StatLevel(id);
            return 0;
        }
        public float OwnedBonus => EffectBonus("attack");
        public float HealthBonus => EffectBonus("health");
        public float DefenseBonus => EffectBonus("defense");
        public float GoldGainMultiplier => 1 + EffectBonus("gold") / 100;
        float RawCombatDamageMultiplier => ((StatValue("attack") + EquippedValue("Club")) / BaseStatValue("attack")) * (1 + (OwnedBonus + EquippedValue("Companion") + EquippedValue("Skill") * .02f) / 100);
        float RawCombatSpeedMultiplier => (StatValue("speed") / BaseStatValue("speed")) * (1 + EffectBonus("speed") / 100);
        public float CombatDamageMultiplier { get { InitCollections(); return RawCombatDamageMultiplier / Mathf.Max(.001f, starterDamageBaseline); } }
        public float CombatAttackSpeedMultiplier { get { InitCollections(); return Mathf.Clamp(RawCombatSpeedMultiplier / Mathf.Max(.001f, starterSpeedBaseline), .25f, 3); } }
        public long Power => (long)Math.Min(long.MaxValue, Math.Round(StatValue("attack") * CombatDamageMultiplier * 70d + StatValue("health") * (1 + HealthBonus / 100) + (StatValue("defense") + EquippedValue("Armor")) * (1 + DefenseBonus / 100) * 5));

        float EquippedValue(string category)
        {
            InitCollections();
            float value = 0;
            // This is queried on every damage event: avoid allocating inventory lists.
            foreach (var item in collectionItems) if (item.category == category && item.equipped) value += ItemEquipValue(item);
            return value;
        }
        float BaseStatValue(string id)
        {
            InitCollections();
            foreach (var definition in collectionTuning.stats)
                if (definition.id == id) return Mathf.Max(.001f, definition.initial);
            return 1;
        }
        float ItemEquipValue(UiItem item) => item.equipValue * (1 + Math.Max(0, item.level - 1) * .15f);
        float ItemOwnedValue(UiItem item) => item.category == "Relic" ? item.level * collectionTuning.relicStepPercent : item.ownedPercent * (1 + Math.Max(0, item.level - 1) * .1f);
        float EffectBonus(string effect, string category = null)
        {
            InitCollections();
            float value = 0;
            foreach (var item in collectionItems)
                if (item.discovered && item.effect == effect && (category == null || item.category == category || (category == "Equipment" && (item.category == "Armor" || item.category == "Club")))) value += ItemOwnedValue(item);
            return value;
        }
        public int CopiesNeeded(UiItem item) => collectionTuning.copiesPerUpgrade + Math.Max(0, item.level - 1) / 10;
        public bool UpgradeItem(UiItem item)
        {
            if (item == null || !item.discovered || item.category == "Relic" || item.level >= collectionTuning.maxItemLevel || item.count < CopiesNeeded(item)) return false;
            item.count -= CopiesNeeded(item);
            item.level++;
            if (item.category == "Armor" || item.category == "Club") RecordServiceProgress("equipmentUpgrade", 1);
            if (item.category == "Skill") RecordServiceProgress("skillUpgrade", 1);
            return true;
        }
    }
}
