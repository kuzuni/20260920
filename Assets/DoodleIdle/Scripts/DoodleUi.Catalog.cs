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
        public float ownedPercent, ownedGoldPercent, equipValue, cooldown;
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
    }

    [Serializable]
    public sealed class UiStatCostTuning
    {
        public int commonBaseCost = 20, critical2BaseCost = 20, critical4BaseCost = 40;
        public float commonGrowth = .004f, critical2Growth = .004f, critical4Growth = .004f;
        public DoodleGrowthStep[] commonGrowthSteps = Array.Empty<DoodleGrowthStep>(), critical2GrowthSteps = Array.Empty<DoodleGrowthStep>(), critical4GrowthSteps = Array.Empty<DoodleGrowthStep>();
        public DoodleGrowthCurve commonCurve = new DoodleGrowthCurve(), critical2Curve = new DoodleGrowthCurve(), critical4Curve = new DoodleGrowthCurve();
    }

    [Serializable]
    public sealed class UiCollectionTuning
    {
        public UiStatCostTuning statCosts = new UiStatCostTuning();
        public int maxStatLevel = 10000, maxItemLevel = 1000, copiesPerUpgrade = 5;
        public float relicStepPercent = 1;
        public float[] skillDpsPercentByGrade = { 25.0f, 91.506248f, 334.93576f, 1225.948608f, 4487.27832f, 16424.560547f, 60117.997875f, 220046.901722f };
        public float[] companionDpsPercentByGrade = { 22.0f, 73.205002f, 243.589645f, 810.544495f, 2697.086914f, 8974.556641f, 29862.836558f, 99368.588647f };
        public float abilityMaxEnhancementBonus = .99f;
        public UiStatDefinition[] stats;
        public UiItem[] items;
    }

    public sealed partial class DoodleUi
    {
        const string CollectionsSaveKey = "DoodleUi.Collections.v1";
        public static readonly string[] GradeNames = { "일반", "고급", "희귀", "영웅", "전설", "신화", "근원", "초월", "갓" };
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
                                    statLevels[migrated] = (int)Math.Max(0,Math.Min(StatMaxLevel(migrated),(long)entry.level*(migrated=="crit2Chance"?4:1)));
                            }
                    }
                }
                catch (ArgumentException) { Debug.LogWarning("Collection save could not be read; using local starter data."); }
            }
            foreach (string category in new[] { "Armor", "Club", "Necklace", "Skill", "Companion" }) NormalizeEquipment(category);
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
            int itemRoll=rng.Next(SummonTierWeightTotal(choices.Count)),choice=0;
            while(choice<choices.Count-1&&itemRoll>=SummonTierWeight(choice,choices.Count)){itemRoll-=SummonTierWeight(choice,choices.Count);choice++;}
            return choices[choice];
        }

        static int SummonTierWeight(int index, int count) => count == 1 ? 1 : Math.Max(1, 10 - index);
        static int SummonTierWeightTotal(int count)
        {
            int total = 0;
            for (int i = 0; i < count; i++) total += SummonTierWeight(i, count);
            return total;
        }

        public double GradeProbability(string category, int rarity)
        {
            InitCollections();
            return rarity >= 0 && rarity < GradeNames.Length && Items(category).Exists(x => x.rarity == rarity) ? SummonWeights(category)[rarity]/1000d : 0;
        }

        public double ItemProbability(UiItem item)
        {
            if (item == null) return 0;
            if(item.category=="Relic")return 100d/Items(item.dungeonRelic?"DungeonRelic":"Relic").Count;
            var choices=Items(item.category).FindAll(x=>x.rarity==item.rarity);int index=choices.IndexOf(item);
            if(index<0)return 0;
            return GradeProbability(item.category,item.rarity)*SummonTierWeight(index,choices.Count)/SummonTierWeightTotal(choices.Count);
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
        static readonly int[] skillSlotStages = { 1, 10, 30, 60, 120, 180, 300, 800 };
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
        public float OwnedBonus => (OwnedEffectMultiplier("attack") - 1) * 100;
        public float HealthBonus => (OwnedEffectMultiplier("health") - 1) * 100;
        public float GoldGainMultiplier => OwnedEffectMultiplier("gold");
        public static readonly string[] CriticalStatIds = { "crit2Chance", "crit4Chance", "crit8Chance", "crit16Chance", "crit32Chance", "crit64Chance", "crit128Chance" };
        public static int CriticalMultiplierAt(int tier) => 1 << (tier + 1);
        public static int CriticalLevelCap(int tier) => tier == 0 ? 4000 : 2000;
        public bool CriticalUnlocked(string id)
        {
            int tier = Array.IndexOf(CriticalStatIds, id);
            for (int i = 0; i < tier; i++) if (StatLevel(CriticalStatIds[i]) < StatMaxLevel(CriticalStatIds[i])) return false;
            return true;
        }
        public float CriticalChance(int tier) => CriticalUnlocked(CriticalStatIds[tier]) ? Mathf.Clamp(StatValue(CriticalStatIds[tier]) * OwnedEffectMultiplier(CriticalStatIds[tier]), 0, 100) : 0;
        public float Critical2Chance => CriticalChance(0);
        public bool Critical4Unlocked => CriticalUnlocked("crit4Chance");
        public float Critical4Chance => CriticalChance(1);
        public float CriticalDamageBonus => Mathf.Max(0, (OwnedEffectMultiplier("critDamage") - 1) * 100);
        public float MaxHealth => Mathf.Max(1, LimitedStat((double)StatValue("health") * (1 + EquippedValue("Armor") / 100) * OwnedEffectMultiplier("health")));
        public float HealthRegen => LimitedStat(((double)StatValue("healthRegen") + NecklaceRecovery(EquippedValue("Necklace"))) * OwnedEffectMultiplier("healthRegen"));
        float NecklaceRecovery(float equipPercent) => LimitedStat((double)StatValue("health") * OwnedEffectMultiplier("health") * equipPercent / 100 * .05f);
        float CollectionDamageMultiplier(bool includeSkins) => LimitedStat((double)StatValue("attack") / BaseStatValue("attack")
            * (1 + EquippedValue("Club") / 100) * (1 + EffectBonus("attack", "Equipment") / 100)
            * (1 + (EffectBonus("attack", "Skill") + EquippedValue("Skill") * .02f) / 100)
            * (1 + (EffectBonus("attack", "Companion") + EquippedValue("Companion")) / 100)
            * (1 + EffectBonus("attack", "Relic") / 100) * (includeSkins ? 1 + SkinOwnedBonus("attack") / 100 : 1));
        static float LimitedStat(double value) => (float)Math.Max(0, Math.Min(1e30, value));
        float OwnedEffectMultiplier(string effect, bool includeSkins = true)
        {
            double value = 1;
            foreach (string category in OwnedEffectCategories) value *= 1 + EffectBonus(effect, category) / 100;
            if (includeSkins) value *= 1 + SkinOwnedBonus(effect) / 100;
            return LimitedStat(value);
        }
        static readonly string[] OwnedEffectCategories = { "Equipment", "Skill", "Companion", "Relic" };
        public float CombatDamageMultiplier { get { InitCollections(); return CollectionDamageMultiplier(true) / Mathf.Max(.001f, starterDamageBaseline); } }
        public float CombatAttackSpeedMultiplier => 1;
        public double ExpectedCriticalMultiplier
        {
            get
            {
                double remaining = 1, expected = 0, bonus = 1 + CriticalDamageBonus / 100d;
                for (int i = CriticalStatIds.Length - 1; i >= 0; i--) {
                    double chance = CriticalChance(i) / 100d;
                    expected += remaining * chance * CriticalMultiplierAt(i) * bonus;
                    remaining *= 1 - chance;
                }
                return expected + remaining;
            }
        }
        public long Power => (long)Math.Min(long.MaxValue, Math.Round(BaseStatValue("attack") * CombatDamageMultiplier * ExpectedCriticalMultiplier * 70d + MaxHealth + HealthRegen * 20d));
        static bool IsCriticalChance(string id) => Array.IndexOf(CriticalStatIds, id) >= 0;
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
        float ItemEquipValue(UiItem item)
        {
            if(IsEquipment(item)) {
                float enhancement=(item.rarity==6 && item.tier==1 || item.rarity==8)?1+Math.Max(0,item.level-1)*.01f:AbilityEnhancementMultiplier(item);
                return ((1+item.equipValue/100)*enhancement-1)*100;
            }
            return item.equipValue * (item.category=="Skill"||item.category=="Companion" ? AbilityEnhancementMultiplier(item) : 1 + Math.Max(0, item.level - 1) * .15f);
        }
        int CompareEquipPriority(UiItem a, UiItem b)
        {
            if(a.category=="Skill"||a.category=="Companion") {
                int grade=a.rarity.CompareTo(b.rarity);
                return grade!=0?grade:ItemExpectedDps(a).CompareTo(ItemExpectedDps(b));
            }
            return ItemEquipValue(a).CompareTo(ItemEquipValue(b));
        }
        float ItemOwnedValue(UiItem item) => item.category == "Relic" ? item.level * collectionTuning.relicStepPercent : item.ownedPercent * (1 + Math.Max(0, item.level - 1) * .1f);
        float EffectBonus(string effect, string category = null, bool includeSkins = true)
        {
            InitCollections();
            float value = 0;
            foreach (var item in collectionItems) {
                if (!item.discovered || !(category == null || item.category == category || category == "Equipment" && IsEquipment(item))) continue;
                if (item.effect == effect) value += ItemOwnedValue(item);
                if (effect == "gold") value += ItemOwnedGoldValue(item);
            }
            return value + (category == null && includeSkins ? SkinOwnedBonus(effect) : 0);
        }
        public int CopiesNeeded(UiItem item) => item.category == "Relic" ? 1 : collectionTuning.copiesPerUpgrade + Math.Max(0, item.level - 1) / 10;
        public static bool IsEquipmentCategory(string category) => category == "Armor" || category == "Club" || category == "Necklace";
        public static bool IsEquipment(UiItem item) => item != null && IsEquipmentCategory(item.category);
        float ItemOwnedGoldValue(UiItem item) => item.ownedGoldPercent * (1 + Math.Max(0, item.level - 1) * .1f);
        public int ItemMaxLevel(UiItem item) => IsEquipment(item) ? ((item.rarity == 6 && item.tier == 1 || item.rarity == 8) ? int.MaxValue : 100) : item.category == "Skill" ? 100 : collectionTuning.maxItemLevel;
        long UpgradeCopiesBetween(int from, int to)
        {
            // Sum floor((level - 1) / 10) without a loop over a potentially old high level.
            long Sum(long n) { long q = n / 10, r = n % 10; return 5 * q * (q - 1) + q * r; }
            return (long)(to - from) * collectionTuning.copiesPerUpgrade + Sum(to - 1L) - Sum(from - 1L);
        }
        public UiItem SynthesisTarget(UiItem item)
        {
            if (!IsEquipment(item) || !collectionItems.Contains(item)) return null;
            UiItem next = null;
            foreach (var candidate in Items(item.category)) {
                if (candidate.rarity < item.rarity || candidate.rarity == item.rarity && candidate.tier <= item.tier) continue;
                if (next == null || candidate.rarity < next.rarity || candidate.rarity == next.rarity && candidate.tier < next.tier) next = candidate;
            }
            return next;
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
            if (IsEquipment(item)) RecordServiceProgress("equipmentUpgrade", 1);
            if (item.category == "Skill") RecordServiceProgress("skillUpgrade", 1);
            if (item.category == "Companion") RecordServiceProgress("companionUpgrade", 1);
            if (notifyPower) NotifyPowerChanged(before, "강화");
            return true;
        }
    }
}
