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
        readonly Dictionary<string, float> collectionScrollPositions = new Dictionary<string, float>();

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

        static void CollectionWidth(Transform target, float width)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width; layout.flexibleWidth = 0;
        }

        static void CollectionButtonText(Button button, int size)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label) { label.fontSize = size; label.resizeTextMaxSize = size; }
        }

        static void CollectionSoftBorder(RectTransform frame)
        {
            var outline = frame.GetComponent<Outline>();
            if (outline) { outline.effectColor = new Color(.68f, .68f, .68f); outline.effectDistance = new Vector2(1.2f, -1.2f); }
        }

        RectTransform CollectionLabelPill(Transform parent, string label, Color color, float width, float height = 32, int size = 22)
        {
            var pill = UiKit.Box(parent, label, color, height);
            pill.GetComponent<Outline>().enabled = false;
            CollectionWidth(pill, width);
            var text = UiKit.Text(pill, label, size, TextAnchor.MiddleCenter, height);
            UiKit.Stretch(text.rectTransform, 3, 1, 3, 1);
            return pill;
        }

        void CollectionEffectRow(RectTransform parent, string label, string value)
        {
            var row = UiKit.Row(parent, label, 32, 8);
            CollectionLabelPill(row, label, new Color(.92f, .88f, .80f), 106, 30, 22);
            UiKit.Text(row, value, 24, TextAnchor.MiddleLeft, 32);
        }

        Button CollectionCoinButton(Transform parent, string name, string caption, long cost, Action click, float height, float width)
        {
            var button = UiKit.Button(parent, name, click, UiKit.Yellow, height);
            button.GetComponentInChildren<Text>().gameObject.SetActive(false);
            CollectionWidth(button.transform, width);
            var content = UiKit.Rect(button.transform, "Upgrade cost content");
            UiKit.Stretch(content, 7, 5, 7, 5);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 1;
            UiKit.Text(content, caption, 28, TextAnchor.MiddleCenter, 34);
            var price = UiKit.Row(content, "Coin price", 34, 5);
            UiKit.Icon(price, "Gold", 30);
            UiKit.Text(price, cost.ToString("N0"), 27, TextAnchor.MiddleCenter, 34);
            return button;
        }

        void CollectionDivider(RectTransform parent)
        {
            var line = UiKit.Rect(parent, "Collection divider"); UiKit.Height(line, 2); UiKit.Flexible(line);
            var image = line.gameObject.AddComponent<Image>(); image.color = new Color(.72f, .72f, .72f); image.raycastTarget = false;
        }

        void BuildStats(RectTransform body)
        {
            body.GetComponent<VerticalLayoutGroup>().spacing = 12;
            var power = UiKit.Row(body, "Combat power", 126, 16);
            power.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(42, 0, 0, 0);
            UiKit.Icon(power, "Player", 124);
            var powerText = UiKit.Text(power, "전투력 " + Power.ToString("N0"), 35, TextAnchor.MiddleCenter, 108);
            CollectionWidth(powerText.transform, 286);
            var batch = UiKit.Row(body, "Stat quantity", 64, 12);
            foreach (int amount in new[] { 1, 10, -1 })
            {
                int selected = amount;
                var mode = UiKit.Button(batch, amount < 0 ? "MAX" : "×" + amount, () => { statBatch = selected; RefreshPage(); }, statBatch == amount ? UiKit.Blue : UiKit.Paper, 64);
                CollectionButtonText(mode, 34);
            }
            foreach (var definition in collectionTuning.stats)
            {
                var stat = definition;
                int upgrades;
                long cost = StatUpgradeQuote(stat.id, statBatch, out upgrades);
                var frame = CollectionBox(body, "Stat " + stat.id, UiKit.Paper);
                CollectionSoftBorder(frame);
                var row = UiKit.Row(frame, stat.name, 104, 10);
                UiKit.Icon(row, stat.id == "attack" ? "Pvp" : stat.icon, 86);
                var text = UiKit.Column(row, "Values", 2, 3);
                CollectionColumnWidth(text, 1.3f);
                UiKit.Text(text, stat.name, 30, TextAnchor.MiddleLeft, 39);
                float current = StatValue(stat.id);
                UiKit.Text(text, StatNumber(stat.id, current) + " → <color=#216B20>" + StatNumber(stat.id, current + stat.increment * upgrades) + "</color>", 29, TextAnchor.MiddleLeft, 38);
                var button = CollectionCoinButton(row, upgrades == 0 ? "최대 단계" : "강화 ×" + upgrades + "\n골드 " + cost.ToString("N0"), upgrades == 0 ? "최대 단계" : upgrades == 1 ? "강화" : "강화 ×" + upgrades, cost, () =>
                {
                    if (!UpgradeStat(stat.id, statBatch)) { Toast("강화에 필요한 골드가 부족합니다."); return; }
                    Save(); RefreshPage();
                }, 94, 164);
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
            body.GetComponent<VerticalLayoutGroup>().spacing = 8;
            string selectedId = equipmentCategory == "Armor" ? selectedArmor : selectedClub;
            var items = Items(equipmentCategory);
            var selected = items.Find(x => x.id == selectedId) ?? items[0];
            var frame = CollectionBox(body, "Selected equipment", UiKit.Paper);
            var row = UiKit.Row(frame, "Equipment specification", 172, 14);
            var card = CollectionSlot(row, selected, () => { }, 160);
            var slotLayout = card.GetComponent<LayoutElement>();
            slotLayout.minWidth = 128; slotLayout.preferredWidth = 128; slotLayout.flexibleWidth = 0;
            var info = UiKit.Column(row, "Item information", 4, 1);
            CollectionColumnWidth(info, 1);
            UiKit.Text(info, selected.name + " · <color=#236B25>" + GradeNames[selected.rarity] + "</color>", 27, TextAnchor.MiddleLeft, 38);
            CollectionEffectRow(info, "보유 효과", EffectName(selected.effect) + " +" + ItemOwnedValue(selected).ToString("0.#") + "%");
            CollectionEffectRow(info, "장착 효과", (selected.category == "Armor" ? "방어력" : "공격력") + " +" + ItemEquipValue(selected).ToString("N0"));
            var actions = UiKit.Row(info, "Selected item actions", 46, 12);
            CollectionButtonText(UiKit.Button(actions, "강화", () => UpgradeSelected(selected, false), UiKit.Blue, 46), 28);
            CollectionButtonText(UiKit.Button(actions, selected.equipped ? "장착 중" : "장착", () => EquipFromUi(selected, false), UiKit.Green, 46), 28);
            OwnershipStrip(body, "Equipment");
            BuildInventory(body, items, item =>
            {
                if (equipmentCategory == "Armor") selectedArmor = item.id; else selectedClub = item.id;
                RefreshPage();
            }, 5);
            var footer = UiKit.Footer(body, "Equipment footer", 130);
            CollectionActions(footer, equipmentCategory);
            var tabs = UiKit.Row(footer, "Equipment tabs", 52, 4);
            CollectionButtonText(UiKit.Button(tabs, "갑옷", () => { equipmentCategory = "Armor"; RefreshPage(); }, equipmentCategory == "Armor" ? UiKit.Yellow : UiKit.Paper, 52), 31);
            CollectionButtonText(UiKit.Button(tabs, "몽둥이", () => { equipmentCategory = "Club"; RefreshPage(); }, equipmentCategory == "Club" ? UiKit.Yellow : UiKit.Paper, 52), 31);
        }

        void BuildSkills(RectTransform body) => BuildLoadout(body, "Skill", "스킬", 8, 4);
        void BuildCompanions(RectTransform body) => BuildLoadout(body, "Companion", "동료", 5, 5);

        void BuildLoadout(RectTransform body, string category, string label, int capacity, int columns)
        {
            var equipped = EquippedItems(category);
            UiKit.Text(body, "장착 슬롯 " + equipped.Count + "/" + capacity, 32, TextAnchor.MiddleLeft, 42);
            float slotHeight = category == "Companion" ? 166 : 118;
            var slots = UiKit.Grid(body, "Equipped " + category, columns, slotHeight);
            for (int i = 0; i < capacity; i++)
            {
                if (i < equipped.Count)
                {
                    var item = equipped[i];
                    var card = CollectionSlot(slots, item, () => ShowCollectionDetail(item), slotHeight);
                    card.GetComponentInChildren<Text>().text = (i + 1) + " " + GradeNames[item.rarity];
                }
                else UiKit.Slot(slots, "빈 슬롯", "", 0, 0, 0, false, false, () => Toast("보유 " + label + "을 선택해 장착하세요."), slotHeight);
            }
            OwnershipStrip(body, category);
            CollectionDivider(body);
            UiKit.Text(body, "보유 " + label, 32, TextAnchor.MiddleLeft, 44);
            BuildInventory(body, Items(category), ShowCollectionDetail, 4);
            CollectionActions(UiKit.Footer(body, category + " footer", 72), category);
        }

        void OwnershipStrip(RectTransform parent, string category)
        {
            string effect = "공격력 +" + EffectBonus("attack", category).ToString("0.#") + "%";
            float health = EffectBonus("health", category);
            if (health > 0) effect += " · 체력 +" + health.ToString("0.#") + "%";
            if (category == "Equipment")
            {
                var row = UiKit.Row(parent, "Total ownership", 42, 0);
                UiKit.Text(row, "총 보유 효과   " + effect, 25, TextAnchor.MiddleCenter, 42);
                return;
            }
            var strip = CollectionBox(parent, "Total ownership", new Color(.965f, .943f, .874f));
            strip.GetComponent<Outline>().enabled = false;
            var contents = UiKit.Row(strip, "Ownership badges", 44, 6);
            CollectionLabelPill(contents, "총 보유 효과", new Color(.79f, .95f, .69f), 166, 44, 28);
            var value = CollectionLabelPill(contents, effect, UiKit.Paper, 184, 44, 29);
            var valueLayout = value.GetComponent<LayoutElement>(); valueLayout.minWidth = 150; valueLayout.preferredWidth = 184;
        }

        Button CollectionSlot(Transform parent, UiItem item, Action click, float height = 112)
            => UiKit.Slot(parent, item.name, item.icon, item.rarity, item.count, CopiesNeeded(item), item.equipped, !item.discovered, click, height);

        void BuildInventory(RectTransform parent, List<UiItem> items, Action<UiItem> click, int columns)
        {
            string category = items.Count > 0 ? items[0].category : "Empty";
            var host = UiKit.Rect(parent, "Collection inventory viewport");
            UiKit.Height(host, 180); UiKit.Flexible(host);
            var viewport = UiKit.Rect(host, "Inventory clipping area");
            UiKit.Stretch(viewport, 0, 0, 12, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var surface = viewport.gameObject.AddComponent<Image>(); surface.color = Color.clear;
            var scroll = host.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.viewport = viewport;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 38;
            float cellHeight = category == "Companion" ? 148 : category == "Skill" ? 128 : 132;
            var grid = UiKit.Grid(viewport, "Collection inventory", columns, cellHeight);
            grid.GetComponent<GridLayoutGroup>().padding = new RectOffset(4, 4, 4, 4);
            grid.anchorMin = new Vector2(0, 1); grid.anchorMax = Vector2.one;
            grid.pivot = new Vector2(.5f, 1); grid.anchoredPosition = Vector2.zero; grid.sizeDelta = Vector2.zero;
            scroll.content = grid;
            foreach (var entry in items)
            {
                var item = entry;
                var card = CollectionSlot(grid, item, () => click(item), cellHeight);
                bool selected = (category == "Armor" && item.id == selectedArmor) || (category == "Club" && item.id == selectedClub);
                if (selected)
                {
                    var outline = card.GetComponent<Outline>();
                    outline.effectColor = UiKit.Yellow; outline.effectDistance = new Vector2(3, -3);
                }
            }
            var rail = UiKit.Rect(host, "Inventory scroll position");
            rail.anchorMin = new Vector2(1, 0); rail.anchorMax = Vector2.one;
            rail.offsetMin = new Vector2(-7, 1); rail.offsetMax = new Vector2(0, -1);
            var railImage = rail.gameObject.AddComponent<Image>(); railImage.sprite = UiKit.Frame;
            railImage.type = Image.Type.Sliced; railImage.color = new Color(.83f, .83f, .8f);
            var handle = UiKit.Rect(rail, "Inventory scroll thumb"); UiKit.Stretch(handle);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.sprite = UiKit.Frame;
            handleImage.type = Image.Type.Sliced; handleImage.color = UiKit.Green;
            var scrollbar = rail.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            var adaptive = host.gameObject.AddComponent<DoodleCollectionInventoryViewport>();
            adaptive.body = parent; adaptive.outerViewport = parent.GetComponentInParent<ScrollRect>().viewport;
            adaptive.scroll = scroll; adaptive.grid = grid;
            adaptive.restorePosition = collectionScrollPositions.TryGetValue(category, out float savedPosition) ? savedPosition : 1;
            scroll.onValueChanged.AddListener(position => { if (adaptive.Restored) collectionScrollPositions[category] = scroll.verticalNormalizedPosition; });
        }

        void CollectionActions(RectTransform parent, string category)
        {
            var row = UiKit.Row(parent, "Collection actions", 68, 14);
            var upgrade = UiKit.Button(row, "일괄강화", () =>
            {
                int upgrades = 0;
                foreach (var item in Items(category)) while (UpgradeItem(item)) upgrades++;
                Save(); RefreshPage(); Toast(upgrades > 0 ? upgrades + "회 강화했습니다." : "강화할 수 있는 수량이 없습니다.");
            }, UiKit.Blue, 68);
            var auto = UiKit.Button(row, "자동장착", () => { AutoEquip(category); Save(); RefreshPage(); Toast("강한 " + CategoryName(category) + "부터 장착했습니다."); }, category == "Armor" || category == "Club" ? UiKit.Green : UiKit.Yellow, 68);
            CollectionButtonText(upgrade, 33); CollectionButtonText(auto, 33);
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
                var window = body.GetComponentInParent<DoodleUiWindow>();
                if (window)
                {
                    window.maxWidth = 360; window.maxHeight = 640; window.centerFromTop = .645f;
                    window.headerHeight = 64; window.titleSize = 33; window.Reflow(safe);
                }
                body.GetComponent<VerticalLayoutGroup>().spacing = 5;
                var grade = UiKit.Row(body, "Detail grade", 24, 0);
                CollectionLabelPill(grade, GradeNames[item.rarity], UiKit.Rarity(item.rarity), 88, 24, 21);
                var display = UiKit.Row(body, "Selected item art", 90, 0);
                var portrait = UiKit.Icon(display, item.icon, 90);
                if (!item.discovered) portrait.color = Color.black;
                var quantity = UiKit.Row(body, "Detail quantity", 26, 0);
                var gauge = UiKit.Gauge(quantity, item.count + "/" + CopiesNeeded(item), item.count / (float)CopiesNeeded(item), 26);
                CollectionWidth(gauge, 174);
                var effects = CollectionBox(body, "Equipment effect", new Color(.965f, .943f, .874f));
                effects.GetComponent<Outline>().enabled = false;
                effects.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 3, 3);
                var effectHeading = UiKit.Row(effects, "Equipment effect label", 26, 0);
                effectHeading.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                CollectionLabelPill(effectHeading, "장착 효과", new Color(.79f, .95f, .69f), 106, 26, 22);
                var description = UiKit.Box(effects, "Effects", UiKit.Paper, 42);
                description.GetComponent<Outline>().enabled = false;
                var descriptionText = UiKit.Text(description, item.description, 19, TextAnchor.MiddleLeft, 42);
                UiKit.Stretch(descriptionText.rectTransform, 7, 3, 7, 3);
                var owned = CollectionBox(body, "Ownership effect", new Color(.965f, .943f, .874f));
                owned.GetComponent<Outline>().enabled = false;
                owned.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 3, 3);
                var ownedHeading = UiKit.Row(owned, "Ownership effect label", 26, 0);
                ownedHeading.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                CollectionLabelPill(ownedHeading, "보유 효과", new Color(.79f, .95f, .69f), 106, 26, 22);
                var ownedValue = UiKit.Box(owned, "Ownership value", UiKit.Paper, 26);
                ownedValue.GetComponent<Outline>().enabled = false;
                var ownedText = UiKit.Text(ownedValue, EffectName(item.effect) + " +" + ItemOwnedValue(item).ToString("0.#") + "%", 22, TextAnchor.MiddleLeft, 26);
                UiKit.Stretch(ownedText.rectTransform, 7, 1, 7, 1);
                if (item.category == "Skill")
                {
                    // Read the active combat component, never present catalog mock values
                    // as real cooldowns. Orbit skills use their shared public cycle constant.
                    float interval = -1;
                    if (game)
                    {
                        switch (item.ability)
                        {
                            case "Banana": case "OrbitGun": interval = DoodleIdleGame.OrbitSkillCycle; break;
                            case "Stone": interval = game.stoneInterval; break;
                            case "Arrows": interval = game.arrowInterval; break;
                            case "BouncyBall": interval = game.ballInterval; break;
                            case "Fire": interval = game.fireInterval; break;
                            case "Drone": interval = game.droneInterval; break;
                            case "Worm": interval = game.wormInterval; break;
                            case "Cloud": case "Lightning": interval = game.cloudInterval; break;
                            case "Dragon": interval = game.dragonInterval; break;
                            case "Cannon": interval = game.cannonInterval; break;
                            case "Guardian": interval = game.guardianInterval; break;
                            case "Shotgun": interval = game.shotgunInterval; break;
                            case "Molotov": interval = game.molotovInterval; break;
                            case "Sound": interval = game.soundWaveInterval; break;
                        }
                    }
                    var measures = UiKit.Row(body, "Skill measures", 54, 10);
                    var potency = CollectionBox(measures, "Skill potency", UiKit.Paper);
                    potency.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
                    potency.GetComponent<Outline>().effectColor = new Color(.89f, .81f, .70f);
                    UiKit.Text(potency, "위력  Lv. " + item.level, 18, TextAnchor.MiddleLeft, 20);
                    UiKit.Text(potency, ItemEquipValue(item).ToString("0.#"), 23, TextAnchor.MiddleLeft, 24);
                    var reuse = CollectionBox(measures, "Skill reuse", UiKit.Paper);
                    reuse.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
                    reuse.GetComponent<Outline>().effectColor = new Color(.89f, .81f, .70f);
                    UiKit.Text(reuse, "재사용", 18, TextAnchor.MiddleLeft, 20);
                    UiKit.Text(reuse, interval > 0 ? interval.ToString("0.##") + "초" : "자동", 23, TextAnchor.MiddleLeft, 24);
                    UiKit.Text(body, "자동 전투 유지 · 장착 공격력 +" + (ItemEquipValue(item) * .02f).ToString("0.##") + "%", 15, TextAnchor.MiddleCenter, 20);
                }
                else UiKit.Text(body, "장착 공격력 +" + ItemEquipValue(item).ToString("0.#") + "%", 23, TextAnchor.MiddleCenter, 32);
                if (!item.discovered) UiKit.Text(body, "미획득 · 효과가 적용되지 않습니다.", 18, TextAnchor.MiddleCenter, 26);
                var buttons = UiKit.Row(UiKit.Footer(body, "Collection detail footer", 60), "Detail actions", 56);
                CollectionButtonText(UiKit.Button(buttons, "강화", () => UpgradeSelected(item, true), UiKit.Blue, 56), 27);
                CollectionButtonText(UiKit.Button(buttons, item.equipped ? "장착 해제" : "장착", () => EquipFromUi(item, true), UiKit.Yellow, 56), 27);
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
            var note = UiKit.Box(body, "All relics apply", new Color(.94f, .90f, .82f), 48);
            note.GetComponent<Outline>().enabled = false;
            var noteText = UiKit.Text(note, "모든 유물 효과가 전체 적용됩니다.", 25, TextAnchor.MiddleCenter, 48);
            UiKit.Stretch(noteText.rectTransform, 4, 2, 4, 2);
            var totals = CollectionBox(body, "Relic effects", new Color(.89f, .985f, .85f));
            totals.GetComponent<Outline>().effectColor = new Color(.27f, .53f, .23f);
            UiKit.Text(totals, "총 유물 효과   공격력 +" + EffectBonus("attack", "Relic").ToString("0.#") + "% · 체력 +" + EffectBonus("health", "Relic").ToString("0.#") + "%", 26, TextAnchor.MiddleCenter, 46);
            foreach (var entry in Items("Relic"))
            {
                var item = entry;
                var frame = CollectionBox(body, "Relic " + item.id, UiKit.Paper);
                CollectionSoftBorder(frame);
                var row = UiKit.Row(frame, "Relic upgrade row", 112, 10);
                UiKit.Icon(row, item.icon, 86);
                var info = UiKit.Column(row, "Relic effects", 1, 4);
                CollectionColumnWidth(info, 1.6f);
                var title = UiKit.Row(info, "Relic name and stage", 34, 5);
                UiKit.Text(title, item.name, 27, TextAnchor.MiddleLeft, 34);
                CollectionLabelPill(title, "Lv. " + item.level, new Color(.94f, .90f, .82f), 64, 32, 23);
                UiKit.Text(info, EffectName(item.effect) + " +" + ItemOwnedValue(item).ToString("0.#") + "% → <color=#216B20>+" + (ItemOwnedValue(item) + collectionTuning.relicStepPercent).ToString("0.#") + "%</color>", 24, TextAnchor.MiddleLeft, 34);
                UiKit.Text(info, "성공 확률 " + (RelicSuccessChance(item) * 100).ToString("0") + "%", 23, TextAnchor.MiddleLeft, 30);
                var button = CollectionCoinButton(row, item.discovered ? "강화\n골드 " + RelicUpgradeCost(item).ToString("N0") : "미획득", item.discovered ? "강화" : "미획득", RelicUpgradeCost(item), () =>
                {
                    bool success;
                    if (!TryUpgradeRelic(item, out success)) { Toast("강화 골드가 부족하거나 최대 단계입니다."); return; }
                    Save(); RefreshPage(); Toast(success ? item.name + " 강화 성공!" : "강화 실패 · 현재 단계는 유지됩니다.");
                }, 94, 146);
                button.interactable = item.discovered && item.level < collectionTuning.maxItemLevel && Gold >= RelicUpgradeCost(item);
            }
            var footer = UiKit.Footer(body, "Relic footer", 72);
            var bulk = UiKit.Button(footer, "일괄강화", () =>
            {
                int attempted = 0, successes = 0;
                foreach (var item in Items("Relic")) { bool success; if (TryUpgradeRelic(item, out success)) { attempted++; if (success) successes++; } }
                Save(); RefreshPage(); Toast(attempted == 0 ? "강화 가능한 유물이 없습니다." : attempted + "회 시도 · " + successes + "회 성공");
            }, UiKit.Blue, 68);
            CollectionButtonText(bulk, 34);
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

    /// <summary>Only the item list scrolls in tall windows; short windows can also scroll the header.</summary>
    public sealed class DoodleCollectionInventoryViewport : MonoBehaviour
    {
        public RectTransform body, outerViewport, grid;
        public ScrollRect scroll;
        public float restorePosition = 1;
        public bool Restored { get; private set; }
        int settleFrames;

        void LateUpdate()
        {
            Reflow();
            if (!scroll) return;
            if (!Restored && ++settleFrames >= 3)
            {
                scroll.verticalNormalizedPosition = Mathf.Clamp01(restorePosition);
                Restored = true;
            }
        }

        public void Reflow()
        {
            if (!body || !outerViewport || !grid || !scroll) return;
            var bodyLayout = body.GetComponent<VerticalLayoutGroup>();
            float otherHeight = bodyLayout ? bodyLayout.padding.vertical : 0;
            int childCount = 0;
            foreach (Transform child in body)
            {
                if (!child.gameObject.activeSelf) continue;
                childCount++;
                if (child != transform) otherHeight += LayoutUtility.GetPreferredHeight((RectTransform)child);
            }
            if (bodyLayout) otherHeight += bodyLayout.spacing * Math.Max(0, childCount - 1);
            float available = outerViewport.rect.height;
            float preferred = available >= 600 ? Mathf.Max(140, available - otherHeight - 2) : 180;
            float itemHeight = LayoutUtility.GetPreferredHeight(grid);
            if (itemHeight > 0) preferred = Mathf.Min(preferred, itemHeight + 2);
            var sizing = GetComponent<LayoutElement>();
            if (Mathf.Abs(sizing.preferredHeight - preferred) > .5f)
            {
                sizing.minHeight = sizing.preferredHeight = preferred;
                if (!Restored) settleFrames = 0;
            }
        }
    }
}
