using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        string equipmentCategory = "Armor", selectedArmor = "armor_4", selectedClub = "club_4";
        int statBatch = 1;

        RectTransform CollectionBox(Transform parent, string name, Color color)
        {
            var frame = UiKit.Box(parent, name, color);
            var layout = frame.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(7, 7, 7, 7);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return frame;
        }

        // Text preferred widths must not squeeze the adjacent purchase buttons.
        static void CollectionColumnWidth(RectTransform column, float weight)
        {
            var layout = column.GetComponent<LayoutElement>();
            layout.preferredWidth = 0; layout.minWidth = 0; layout.flexibleWidth = weight;
        }

        void BuildStats(RectTransform body)
        {
            var power = UiKit.Row(body, "Combat power", 94);
            UiKit.Icon(power, "Player", 80);
            UiKit.Text(power, "전투력 " + Power.ToString("N0"), 30, TextAnchor.MiddleCenter, 90);
            var batch = UiKit.Row(body, "Stat quantity", 52);
            foreach (int amount in new[] { 1, 10, -1 })
            {
                int selected = amount;
                UiKit.Button(batch, amount < 0 ? "MAX" : "×" + amount, () => { statBatch = selected; RefreshPage(); }, statBatch == amount ? UiKit.Blue : UiKit.Paper);
            }
            foreach (var definition in collectionTuning.stats)
            {
                var stat = definition;
                int upgrades;
                long cost = StatUpgradeQuote(stat.id, statBatch, out upgrades);
                var frame = CollectionBox(body, "Stat " + stat.id, UiKit.Paper);
                var row = UiKit.Row(frame, stat.name, 112);
                UiKit.Icon(row, stat.icon, 56);
                var text = UiKit.Column(row, "Values", 2, 3);
                CollectionColumnWidth(text, 1.3f);
                UiKit.Text(text, stat.name, 24, TextAnchor.MiddleLeft, 34);
                float current = StatValue(stat.id);
                UiKit.Text(text, StatNumber(stat.id, current) + " → " + StatNumber(stat.id, current + stat.increment * upgrades), 21, TextAnchor.MiddleLeft, 32);
                var button = UiKit.Button(row, upgrades == 0 ? "최대 단계" : "강화 ×" + upgrades + "\n골드 " + cost.ToString("N0"), () =>
                {
                    if (!UpgradeStat(stat.id, statBatch)) { Toast("강화에 필요한 골드가 부족합니다."); return; }
                    Save(); RefreshPage();
                }, UiKit.Yellow, 88);
                UiKit.Flexible(button.transform, .9f);
                button.interactable = upgrades > 0 && Gold >= cost;
            }
        }

        string StatNumber(string id, float value) => id == "speed" ? value.ToString("0.00") : value.ToString("N0");

        public long StatUpgradeQuote(string id, int requested, out int upgrades)
        {
            InitCollections();
            var stat = Array.Find(collectionTuning.stats, x => x.id == id);
            upgrades = 0;
            if (stat == null) return 0;
            int available = collectionTuning.maxStatLevel - StatLevel(id);
            int target = requested < 0 ? available : Math.Min(Math.Max(0, requested), available);
            long total = 0;
            for (int i = 0; i < target; i++)
            {
                double raw = stat.baseCost * Math.Pow(collectionTuning.costGrowth, StatLevel(id) + i);
                if (double.IsInfinity(raw) || raw >= long.MaxValue - total) break;
                long price = Math.Max(1, (long)Math.Ceiling(raw));
                if (requested < 0 && price > Gold - total) break;
                total += price;
                upgrades++;
            }
            // MAX still presents the next purchase price when the wallet is empty.
            if (requested < 0 && upgrades == 0 && available > 0) return StatUpgradeQuote(id, 1, out upgrades);
            return total;
        }

        public bool UpgradeStat(string id, int requested)
        {
            int count;
            long cost = StatUpgradeQuote(id, requested, out count);
            if (count == 0 || cost > Gold) return false;
            Gold -= cost;
            statLevels[id] += count;
            RecordServiceProgress("statUpgrade", count);
            return true;
        }

        void BuildEquipment(RectTransform body)
        {
            string selectedId = equipmentCategory == "Armor" ? selectedArmor : selectedClub;
            var items = Items(equipmentCategory);
            var selected = items.Find(x => x.id == selectedId) ?? items[0];
            var frame = CollectionBox(body, "Selected equipment", UiKit.Paper);
            var row = UiKit.Row(frame, "Equipment specification", 168);
            var card = CollectionSlot(row, selected, () => { }, 160);
            var slotLayout = card.GetComponent<LayoutElement>();
            slotLayout.minWidth = 118; slotLayout.preferredWidth = 118; slotLayout.flexibleWidth = 0;
            var info = UiKit.Column(row, "Item information", 3, 4);
            CollectionColumnWidth(info, 1);
            UiKit.Text(info, selected.name + " · " + GradeNames[selected.rarity], 23, TextAnchor.MiddleLeft, 34);
            UiKit.Text(info, "보유 효과   " + EffectName(selected.effect) + " +" + ItemOwnedValue(selected).ToString("0.#") + "%", 20, TextAnchor.MiddleLeft, 30);
            UiKit.Text(info, "장착 효과   " + (selected.category == "Armor" ? "방어력" : "공격력") + " +" + ItemEquipValue(selected).ToString("N0"), 20, TextAnchor.MiddleLeft, 30);
            var actions = UiKit.Row(info, "Selected item actions", 48);
            UiKit.Button(actions, "강화", () => UpgradeSelected(selected, false), UiKit.Blue, 48);
            UiKit.Button(actions, selected.equipped ? "장착 중" : "장착", () => EquipFromUi(selected, false), UiKit.Green, 48);
            OwnershipStrip(body, "Equipment");
            BuildInventory(body, items, item =>
            {
                if (equipmentCategory == "Armor") selectedArmor = item.id; else selectedClub = item.id;
                RefreshPage();
            }, 5);
            CollectionActions(body, equipmentCategory);
            var tabs = UiKit.Row(body, "Equipment tabs", 54);
            UiKit.Button(tabs, "갑옷", () => { equipmentCategory = "Armor"; RefreshPage(); }, equipmentCategory == "Armor" ? UiKit.Yellow : UiKit.Paper);
            UiKit.Button(tabs, "몽둥이", () => { equipmentCategory = "Club"; RefreshPage(); }, equipmentCategory == "Club" ? UiKit.Yellow : UiKit.Paper);
        }

        void BuildSkills(RectTransform body) => BuildLoadout(body, "Skill", "스킬", 8, 4);
        void BuildCompanions(RectTransform body) => BuildLoadout(body, "Companion", "동료", 5, 5);

        void BuildLoadout(RectTransform body, string category, string label, int capacity, int columns)
        {
            var equipped = EquippedItems(category);
            UiKit.Text(body, "장착 슬롯 " + equipped.Count + "/" + capacity, 27, TextAnchor.MiddleLeft, 40);
            var slots = UiKit.Grid(body, "Equipped " + category, columns, 124);
            for (int i = 0; i < capacity; i++)
            {
                if (i < equipped.Count)
                {
                    var item = equipped[i];
                    CollectionSlot(slots, item, () => ShowCollectionDetail(item), 124);
                }
                else UiKit.Slot(slots, "빈 슬롯", "", 0, 0, 0, false, false, () => Toast("보유 " + label + "을 선택해 장착하세요."), 124);
            }
            OwnershipStrip(body, category);
            UiKit.Text(body, "보유 " + label, 27, TextAnchor.MiddleLeft, 40);
            BuildInventory(body, Items(category), ShowCollectionDetail, 4);
            CollectionActions(body, category);
        }

        void OwnershipStrip(RectTransform parent, string category)
        {
            var strip = CollectionBox(parent, "Total ownership", new Color(.96f, .94f, .85f));
            string effect = "공격력 +" + EffectBonus("attack", category).ToString("0.#") + "%";
            float health = EffectBonus("health", category);
            if (health > 0) effect += " · 체력 +" + health.ToString("0.#") + "%";
            UiKit.Text(strip, "총 보유 효과   " + effect, 22, TextAnchor.MiddleCenter, 50);
        }

        Button CollectionSlot(Transform parent, UiItem item, Action click, float height = 112)
            => UiKit.Slot(parent, item.name, item.icon, item.rarity, item.count, CopiesNeeded(item), item.equipped, !item.discovered, click, height);

        void BuildInventory(RectTransform parent, List<UiItem> items, Action<UiItem> click, int columns)
        {
            var grid = UiKit.Grid(parent, "Collection inventory", columns, 122);
            foreach (var entry in items)
            {
                var item = entry;
                CollectionSlot(grid, item, () => click(item), 122);
            }
        }

        void CollectionActions(RectTransform parent, string category)
        {
            var row = UiKit.Row(parent, "Collection actions", 56);
            UiKit.Button(row, "일괄강화", () =>
            {
                int upgrades = 0;
                foreach (var item in Items(category)) while (UpgradeItem(item)) upgrades++;
                Save(); RefreshPage(); Toast(upgrades > 0 ? upgrades + "회 강화했습니다." : "강화할 수 있는 수량이 없습니다.");
            }, UiKit.Blue, 56);
            UiKit.Button(row, "자동장착", () => { AutoEquip(category); Save(); RefreshPage(); Toast("강한 " + CategoryName(category) + "부터 장착했습니다."); }, UiKit.Green, 56);
        }

        public void AutoEquip(string category)
        {
            var owned = Items(category).FindAll(x => x.discovered);
            owned.Sort((a, b) => { int score = ItemEquipValue(b).CompareTo(ItemEquipValue(a)); return score != 0 ? score : string.CompareOrdinal(a.id, b.id); });
            foreach (var item in Items(category)) item.equipped = false;
            for (int i = 0; i < Math.Min(EquipLimit(category), owned.Count); i++) { owned[i].equipped = true; owned[i].slot = i; }
        }

        void UpgradeSelected(UiItem item, bool detail)
        {
            if (!UpgradeItem(item)) { Toast(item.discovered ? "강화 수량이 부족하거나 최대 단계입니다." : "아직 획득하지 않았습니다."); return; }
            Save();
            if (detail) CloseDetail();
            RefreshPage();
            if (detail) ShowCollectionDetail(item);
            Toast(item.name + " 강화 완료 · Lv. " + item.level);
        }

        void ShowCollectionDetail(UiItem item)
        {
            ShowDetail(item.name, body =>
            {
                UiKit.Text(body, GradeNames[item.rarity] + " · Lv. " + item.level, 23, TextAnchor.MiddleCenter, 36);
                if (!item.discovered) UiKit.Text(body, "미획득 · 효과가 적용되지 않습니다.", 20, TextAnchor.MiddleCenter, 34);
                var display = UiKit.Row(body, "Selected item art", 152);
                CollectionSlot(display, item, () => { }, 152);
                var effects = CollectionBox(body, "Equipment effect", new Color(.96f, .94f, .85f));
                var effectBody = UiKit.Column(effects, "Effects", 3, 8);
                UiKit.Text(effectBody, "장착 효과", 23, TextAnchor.MiddleLeft, 32);
                UiKit.Text(effectBody, item.description, 20, TextAnchor.MiddleLeft, 62);
                UiKit.Text(effectBody, item.category == "Companion" ? "공격력 +" + ItemEquipValue(item).ToString("0.#") + "%" : "스킬 위력 " + ItemEquipValue(item).ToString("0.#"), 21, TextAnchor.MiddleLeft, 32);
                UiKit.Text(body, "보유 효과   " + EffectName(item.effect) + " +" + ItemOwnedValue(item).ToString("0.#") + "%", 22, TextAnchor.MiddleLeft, 44);
                if (item.category == "Skill")
                    UiKit.Text(body, "장착 시 전체 공격력 +" + (ItemEquipValue(item) * .02f).ToString("0.##") + "%\n모든 기존 스킬은 계속 자동 사용됩니다.", 18, TextAnchor.MiddleCenter, 52);
                var buttons = UiKit.Row(body, "Detail actions", 56);
                UiKit.Button(buttons, "강화", () => UpgradeSelected(item, true), UiKit.Blue, 56);
                UiKit.Button(buttons, item.equipped ? "장착 해제" : "장착", () => EquipFromUi(item, true), UiKit.Yellow, 56);
            });
        }

        void EquipFromUi(UiItem item, bool detail)
        {
            if (!item.discovered) { Toast("아직 획득하지 않았습니다."); return; }
            if (item.equipped)
            {
                if (!detail) { Toast("장착 중인 장비입니다."); return; }
                item.equipped = false;
            }
            else
            {
                var equipped = EquippedItems(item.category);
                int limit = EquipLimit(item.category);
                if (limit == 1) foreach (var previous in equipped) previous.equipped = false;
                else if (equipped.Count >= limit)
                {
                    ShowDetail("교체할 " + CategoryName(item.category) + " 선택", body =>
                    {
                        UiKit.Text(body, "장착 슬롯이 가득 찼습니다.", 23, TextAnchor.MiddleCenter, 42);
                        foreach (var current in equipped)
                        {
                            var previous = current;
                            UiKit.Button(body, previous.name + " → " + item.name, () =>
                            {
                                previous.equipped = false; item.equipped = true; item.slot = previous.slot;
                                Save(); CloseDetail(); if (detail) CloseDetail(); RefreshPage();
                            }, UiKit.Green, 54);
                        }
                    });
                    return;
                }
                item.equipped = true; item.slot = limit == 1 ? 0 : equipped.Count;
            }
            NormalizeEquipment(item.category);
            Save(); if (detail) CloseDetail(); RefreshPage();
        }

        void BuildRelics(RectTransform body)
        {
            UiKit.Text(body, "모든 보유 유물 효과가 전체 적용됩니다.", 23, TextAnchor.MiddleCenter, 46);
            var totals = CollectionBox(body, "Relic effects", UiKit.Green);
            UiKit.Text(totals, "총 유물 효과   공격력 +" + EffectBonus("attack", "Relic").ToString("0.#") + "% · 체력 +" + EffectBonus("health", "Relic").ToString("0.#") + "%", 22, TextAnchor.MiddleCenter, 54);
            foreach (var entry in Items("Relic"))
            {
                var item = entry;
                var frame = CollectionBox(body, "Relic " + item.id, UiKit.Paper);
                var row = UiKit.Row(frame, "Relic upgrade row", 128);
                UiKit.Icon(row, item.icon, 62);
                var info = UiKit.Column(row, "Relic effects", 1, 4);
                CollectionColumnWidth(info, 1.6f);
                UiKit.Text(info, item.name + "  Lv. " + item.level, 23, TextAnchor.MiddleLeft, 34);
                UiKit.Text(info, EffectName(item.effect) + " +" + ItemOwnedValue(item).ToString("0.#") + "% → +" + (ItemOwnedValue(item) + collectionTuning.relicStepPercent).ToString("0.#") + "%", 20, TextAnchor.MiddleLeft, 36);
                UiKit.Text(info, "성공 확률 " + (RelicSuccessChance(item) * 100).ToString("0") + "%", 20, TextAnchor.MiddleLeft, 32);
                var button = UiKit.Button(row, item.discovered ? "강화\n골드 " + RelicUpgradeCost(item).ToString("N0") : "미획득", () =>
                {
                    bool success;
                    if (!TryUpgradeRelic(item, out success)) { Toast("강화 골드가 부족하거나 최대 단계입니다."); return; }
                    Save(); RefreshPage(); Toast(success ? item.name + " 강화 성공!" : "강화 실패 · 현재 단계는 유지됩니다.");
                }, UiKit.Yellow, 94);
                UiKit.Flexible(button.transform, .8f);
                button.interactable = item.discovered && item.level < collectionTuning.maxItemLevel && Gold >= RelicUpgradeCost(item);
            }
            UiKit.Button(body, "일괄강화", () =>
            {
                int attempted = 0, successes = 0;
                foreach (var item in Items("Relic")) { bool success; if (TryUpgradeRelic(item, out success)) { attempted++; if (success) successes++; } }
                Save(); RefreshPage(); Toast(attempted == 0 ? "강화 가능한 유물이 없습니다." : attempted + "회 시도 · " + successes + "회 성공");
            }, UiKit.Blue, 60);
        }

        public float RelicSuccessChance(UiItem item) => Mathf.Clamp(1 - Math.Max(0, item.level - 1) * collectionTuning.relicChanceLoss, collectionTuning.relicMinimumChance, 1);
        public long RelicUpgradeCost(UiItem item) => (long)Math.Min(long.MaxValue / 2d, Math.Ceiling(collectionTuning.relicBaseCost * (1 + item.rarity * .25) * Math.Pow(collectionTuning.relicCostGrowth, Math.Max(0, item.level - 1))));
        public bool TryUpgradeRelic(UiItem item, out bool success)
        {
            success = false;
            if (item == null || item.category != "Relic" || !item.discovered || item.level >= collectionTuning.maxItemLevel) return false;
            long cost = RelicUpgradeCost(item);
            if (Gold < cost) return false;
            Gold -= cost;
            success = collectionRandom.NextDouble() < RelicSuccessChance(item);
            if (success) { item.level++; RecordServiceProgress("relicUpgrade", 1); }
            return true;
        }

        static string EffectName(string effect)
        {
            switch (effect) { case "health": return "체력"; case "defense": return "방어력"; case "speed": return "공격 속도"; case "gold": return "골드 획득"; default: return "공격력"; }
        }
        static string CategoryName(string category)
        {
            switch (category) { case "Armor": return "갑옷"; case "Club": return "몽둥이"; case "Skill": return "스킬"; case "Companion": return "동료"; default: return "유물"; }
        }
    }
}
