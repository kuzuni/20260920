using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    [Serializable]
    public sealed class UiItem
    {
        public string id, name, icon, category, effect, description, ability;
        public int rarity, count, level, slot, tier;
        public bool equipped, discovered, dungeonRelic;
        public float ownedPercent, equipValue, cooldown;
        public string projectile, trajectory;
        public int volleyCount;
        public float volleyGap, attackInterval, projectileSpeed, explosionRadius;
        public float splashDamageMultiplier = 1;
        public float damageMultiplier = 1;
    }

    [Serializable]
    public sealed class UiStatDefinition
    {
        public string id, name, icon;
        public float initial, increment;
        public float valueGrowth = 1;
        public int baseCost;
    }

    [Serializable]
    public sealed class UiCollectionTuning
    {
        public float costGrowth = 1.08f;
        public int maxStatLevel = 10000, maxItemLevel = 1000, copiesPerUpgrade = 5;
        public float relicStepPercent = 2;
        public UiStatDefinition[] stats;
        public UiItem[] items;
    }

    public sealed partial class DoodleUi
    {
        const string CollectionsSaveKey = "DoodleUi.Collections.v1";
        public static readonly string[] GradeNames = { "일반", "고급", "희귀", "영웅", "전설", "신화", "갓" };
        readonly List<UiItem> collectionItems = new List<UiItem>();
        readonly Dictionary<string, int> statLevels = new Dictionary<string, int>();
        UiCollectionTuning collectionTuning;
        float starterDamageBaseline = 1;
        readonly System.Random collectionRandom = new System.Random();

        [Serializable] sealed class ItemSave { public string id; public int count, level, slot; public bool equipped, discovered; }
        [Serializable] sealed class StatSave { public string id; public int level; }
        [Serializable] sealed class CollectionSave { public int version; public List<ItemSave> items = new List<ItemSave>(); public List<StatSave> stats = new List<StatSave>(); }

        public void InitCollections()
        {
            if (collectionTuning != null) return;
            var asset = Resources.Load<TextAsset>("DoodleIdle/UI/Collections");
            if (asset == null) throw new InvalidOperationException("Missing DoodleIdle/UI/Collections tuning data.");
            collectionTuning = JsonUtility.FromJson<UiCollectionTuning>(asset.text);
            if (collectionTuning == null || collectionTuning.items == null || collectionTuning.stats == null)
                throw new InvalidOperationException("Invalid collection tuning data.");
            collectionItems.AddRange(collectionTuning.items);
            // Catalog entries describe items; only a saved profile or an actual grant owns them.
            foreach (var item in collectionItems) {
                item.count = item.level = item.slot = 0;
                item.discovered = item.equipped = false;
            }
            foreach (var stat in collectionTuning.stats) statLevels[stat.id] = 0;
            // Use the empty new-game profile as the baseline before restoring earned upgrades.
            starterDamageBaseline = CollectionDamageMultiplier(false);
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
                            item.level = item.discovered ? Mathf.Clamp(entry.level, 1, ItemMaxLevel(item)) : 0;
                            // Return copies invested above the new cap to the inventory.
                            if (entry.level > item.level && item.discovered)
                                item.count = (int)Math.Min(int.MaxValue, item.count + UpgradeCopiesBetween(item.level, entry.level));
                            item.equipped = entry.equipped && item.discovered && item.category != "Relic";
                            item.slot = Math.Max(0, entry.slot);
                        }
                    if (state != null && state.stats != null)
                    {
                        foreach (var entry in state.stats)
                            if (statLevels.ContainsKey(entry.id)) statLevels[entry.id] = (int)Math.Min(StatMaxLevel(entry.id),Math.Max(0L,(long)entry.level*(state.version<3&&entry.id=="crit2Chance"?4:1)));
                        if (state.version < 2)
                            foreach (var entry in state.stats)
                            {
                                string migrated = entry.id == "defense" ? "healthRegen" : entry.id == "speed" ? "crit2Chance" : null;
                                if (migrated != null && !state.stats.Exists(x => x.id == migrated))
                                    statLevels[migrated] = Mathf.Clamp(entry.level, 0, StatMaxLevel(migrated));
                            }
                    }
                }
                catch (ArgumentException) { Debug.LogWarning("Collection save could not be read; using local starter data."); }
            }
            foreach (string category in new[] { "Armor", "Club", "Skill", "Companion" }) NormalizeEquipment(category);
        }

        public void SaveCollections()
        {
            if (collectionTuning == null) return;
            var saved = new CollectionSave { version = 3 };
            foreach (var item in collectionItems)
                saved.items.Add(new ItemSave { id = item.id, count = item.count, level = item.level, slot = item.slot, equipped = item.equipped, discovered = item.discovered });
            foreach (var pair in statLevels) saved.stats.Add(new StatSave { id = pair.Key, level = pair.Value });
            PlayerPrefs.SetString(CollectionsSaveKey, JsonUtility.ToJson(saved));
        }

        public List<UiItem> Items(string category)
        {
            InitCollections();
            return collectionItems.FindAll(x => category == "DungeonRelic" ? x.category == "Relic" && x.dungeonRelic : x.category == category && (category != "Relic" || !x.dungeonRelic));
        }

        public List<UiItem> AllRelics { get { InitCollections();return collectionItems.FindAll(x=>x.category=="Relic"); } }

        // A roll selects a catalog entry only. The caller grants it explicitly after charging currency.
        public UiItem GrantItem(string category, System.Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var items = Items(category);
            if (items.Count == 0) throw new ArgumentException("Unknown collection category", nameof(category));
            if(category=="Relic"||category=="DungeonRelic")return items[rng.Next(items.Count)];
            var weights=SummonWeights(category);
            int roll = rng.Next(SummonWeightTotal), grade = 0;
            while (grade < weights.Length - 1 && roll >= weights[grade]) { roll -= weights[grade]; grade++; }
            var choices = items.FindAll(x => x.rarity == grade);
            if (choices.Count == 0) throw new InvalidOperationException("Every draw category must contain every rarity.");
            int itemRoll=rng.Next(100),choice=0;
            var tierWeights=choices.Count==4?new[]{64,25,9,2}:choices.Count==5?new[]{60,25,10,4,1}:new[]{100};
            while(choice<choices.Count-1&&itemRoll>=tierWeights[choice]){itemRoll-=tierWeights[choice];choice++;}
            return choices[choice];
        }

        public double GradeProbability(string category, int rarity)
        {
            InitCollections();
            return rarity >= 0 && rarity < 7 && Items(category).Exists(x => x.rarity == rarity) ? SummonWeights(category)[rarity]/1000d : 0;
        }

        public double ItemProbability(UiItem item)
        {
            if (item == null) return 0;
            if(item.category=="Relic")return 100d/Items(item.dungeonRelic?"DungeonRelic":"Relic").Count;
            var choices=Items(item.category).FindAll(x=>x.rarity==item.rarity);int index=choices.IndexOf(item);
            if(index<0)return 0;
            var weights=choices.Count==4?new[]{64,25,9,2}:choices.Count==5?new[]{60,25,10,4,1}:new[]{100};
            return GradeProbability(item.category,item.rarity)*weights[index]/100;
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
        static readonly int[] skillSlotStages = { 1, 50, 100, 200, 350, 500, 800, 1200 };
        public int SkillSlotUnlockStage(int slot) => skillSlotStages[slot];
        public int UnlockedSkillSlots {
            get {
                int count = 1;
                while (count < skillSlotStages.Length && HighestMainStage >= skillSlotStages[count] - 1) count++;
                return count;
            }
        }
        public int UnlockedCompanionSlots {
            get {
                InitCollections();
                int highest = -1;
                foreach (var item in collectionItems)
                    if (item.category == "Armor" && item.discovered) highest = Math.Max(highest, item.rarity);
                return Mathf.Clamp(highest - 1, 1, 5);
            }
        }
        public string CompanionSlotUnlockRequirement(int slot) => GradeNames[slot + 2] + "1 이상 갑옷 획득";
        List<UiItem> EquippedItems(string category)
        {
            int limit = EquipLimit(category);
            var equipped = Items(category).FindAll(x => x.equipped && x.discovered && x.slot < limit);
            equipped.Sort((a, b) => a.slot.CompareTo(b.slot));
            return equipped;
        }
        int EquipLimit(string category) => category == "Skill" ? UnlockedSkillSlots : category == "Companion" ? UnlockedCompanionSlots : category == "Relic" ? 0 : 1;
        void NormalizeEquipment(string category)
        {
            var equipped = Items(category).FindAll(x => x.equipped && x.discovered);
            equipped.Sort((a, b) => a.slot.CompareTo(b.slot));
            // Collections load before service progress. Defer the skill cap until its saved stage is known.
            int limit = category == "Skill" && services == null ? skillSlotStages.Length : EquipLimit(category);
            for (int i = 0; i < equipped.Count; i++) { equipped[i].equipped = i < limit; equipped[i].slot = i; }
        }

        public int StatLevel(string id) { InitCollections(); return statLevels.TryGetValue(id, out int level) ? level : 0; }
        public int AttackStatLevel => StatLevel("attack");
        public float StatValue(string id)
        {
            InitCollections();
            foreach (var stat in collectionTuning.stats)
                if (stat.id == id)
                {
                    float value = StatValueAtLevel(stat,StatLevel(id));
                    return IsCriticalChance(id) ? Mathf.Clamp(value, 0, 100) : value;
                }
            return 0;
        }
        static float StatValueAtLevel(UiStatDefinition stat,int level) => (float)Math.Min(1e30,(stat.initial+stat.increment*(double)level)*Math.Pow(Math.Max(1,stat.valueGrowth),Math.Max(0,level)));
        public float StatValueAfterUpgrades(string id,int count) { var stat=Array.Find(collectionTuning.stats,x=>x.id==id);return stat==null?0:Mathf.Min(IsCriticalChance(id)?100:1e30f,StatValueAtLevel(stat,(int)Math.Min(int.MaxValue,(long)StatLevel(id)+count))); }
        public float OwnedBonus => EffectBonus("attack");
        public float HealthBonus => EffectBonus("health");
        public float GoldGainMultiplier => 1 + EffectBonus("gold") / 100;
        public float Critical2Chance => Mathf.Clamp(StatValue("crit2Chance") + EffectBonus("crit2Chance"), 0, 100);
        public bool Critical4Unlocked => StatLevel("crit2Chance") >= StatMaxLevel("crit2Chance");
        public float Critical4Chance => Critical4Unlocked ? Mathf.Clamp(StatValue("crit4Chance") + EffectBonus("crit4Chance"), 0, 100) : 0;
        public float CriticalDamageBonus => Mathf.Max(0, EffectBonus("critDamage"));
        public float MaxHealth => Mathf.Max(1, (StatValue("health") * (1 + EquippedValue("Armor") / 100)) * (1 + HealthBonus / 100));
        public float HealthRegen => Mathf.Max(0, StatValue("healthRegen") * (1 + EffectBonus("healthRegen") / 100));
        float CollectionDamageMultiplier(bool includeSkins) => (StatValue("attack") * (1 + EquippedValue("Club") / 100) / BaseStatValue("attack")) * (1 + (EffectBonus("attack", null, includeSkins) + EquippedValue("Companion") + EquippedValue("Skill") * .02f) / 100);
        public float CombatDamageMultiplier { get { InitCollections(); return CollectionDamageMultiplier(true) / Mathf.Max(.001f, starterDamageBaseline); } }
        public float CombatAttackSpeedMultiplier => 1;
        public double ExpectedCriticalMultiplier
        {
            get
            {
                double p2 = Critical2Chance / 100d, p4 = Critical4Chance / 100d, bonus = 1 + CriticalDamageBonus / 100d;
                return (1 - p4) * (1 - p2) + (1 - p4) * p2 * 2 * bonus + p4 * 4 * bonus;
            }
        }
        public long Power => (long)Math.Min(long.MaxValue, Math.Round(BaseStatValue("attack") * CombatDamageMultiplier * ExpectedCriticalMultiplier * 70d + MaxHealth + HealthRegen * 20d));
        static bool IsCriticalChance(string id) => id == "crit2Chance" || id == "crit4Chance";
        int StatMaxLevel(string id)
        {
            var stat = Array.Find(collectionTuning.stats, x => x.id == id);
            if (stat == null) return 0;
            return IsCriticalChance(id) && stat.increment > 0
                ? Math.Min(collectionTuning.maxStatLevel, Mathf.CeilToInt((100 - stat.initial) / stat.increment)) : collectionTuning.maxStatLevel;
        }

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
        float EffectBonus(string effect, string category = null, bool includeSkins = true)
        {
            InitCollections();
            float value = 0;
            foreach (var item in collectionItems)
                if (item.discovered && item.effect == effect && (category == null || item.category == category || (category == "Equipment" && (item.category == "Armor" || item.category == "Club")))) value += ItemOwnedValue(item);
            return value + (category == null && includeSkins ? SkinOwnedBonus(effect) : 0);
        }
        public int CopiesNeeded(UiItem item) => item.category == "Relic" ? 1 : collectionTuning.copiesPerUpgrade + Math.Max(0, item.level - 1) / 10;
        public static bool IsEquipment(UiItem item) => item != null && (item.category == "Armor" || item.category == "Club");
        public int ItemMaxLevel(UiItem item) => IsEquipment(item) ? (item.rarity == 6 ? int.MaxValue : 100) : item.category == "Skill" ? 100 : collectionTuning.maxItemLevel;
        long UpgradeCopiesBetween(int from, int to)
        {
            // Sum floor((level - 1) / 10) without a loop over a potentially old high level.
            long Sum(long n) { long q = n / 10, r = n % 10; return 5 * q * (q - 1) + q * r; }
            return (long)(to - from) * collectionTuning.copiesPerUpgrade + Sum(to - 1L) - Sum(from - 1L);
        }
        public UiItem SynthesisTarget(UiItem item)
        {
            if (!IsEquipment(item) || item.rarity == 6 || !collectionItems.Contains(item)) return null;
            int grade = item.tier == 5 ? item.rarity + 1 : item.rarity;
            int tier = item.tier == 5 ? 1 : item.tier + 1;
            return Items(item.category).Find(x => x.rarity == grade && x.tier == tier);
        }
        public int SynthesizeItem(UiItem item, bool all = false)
        {
            var next = SynthesisTarget(item);
            if (next == null || !item.discovered || item.level < 100 || item.count < 5) return 0;
            int amount = Math.Min(all ? item.count / 5 : 1, int.MaxValue - next.count);
            if (amount <= 0) return 0;
            long before = Power;
            item.count -= amount * 5;
            AddItem(next, amount);
            NotifyPowerChanged(before, "장비 합성");
            Save();
            return amount;
        }
        public int SynthesizeAll(string category)
        {
            long total = 0;
            foreach (var item in Items(category)) total += SynthesizeItem(item, true);
            return (int)Math.Min(int.MaxValue, total);
        }
        public bool UpgradeItem(UiItem item, bool notifyPower = true)
        {
            if (item == null || !collectionItems.Contains(item) || !item.discovered || item.category == "Relic" || item.level >= ItemMaxLevel(item) || item.count < CopiesNeeded(item)) return false;
            long before = notifyPower ? Power : 0;
            item.count -= CopiesNeeded(item);
            item.level++;
            if (item.category == "Armor" || item.category == "Club") RecordServiceProgress("equipmentUpgrade", 1);
            if (item.category == "Skill") RecordServiceProgress("skillUpgrade", 1);
            if (notifyPower) NotifyPowerChanged(before, "강화");
            return true;
        }
    }
}
