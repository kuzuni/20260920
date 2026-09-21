using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        string equipmentCategory = "Armor", selectedArmor = "armor_4", selectedClub = "club_4";
        int statBatch = 1;
        bool collectionBulkRunning;
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
            UiKit.Text(price, UiNumber.Format(cost), 27, TextAnchor.MiddleCenter, 34);
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
            var powerText = UiKit.Text(power, "전투력 " + UiNumber.Format(Power), 35, TextAnchor.MiddleCenter, 108);
            CollectionWidth(powerText.transform, 286);
            var batch = UiKit.Row(body, "Stat quantity", 64, 12);
            foreach (int amount in new[] { 1, 10, 100, -1 })
            {
                int selected = amount;
                var mode = UiKit.Button(batch, amount < 0 ? "MAX" : "×" + UiNumber.Format(amount), () => { statBatch = selected; RefreshPage(); }, statBatch == amount ? UiKit.Blue : UiKit.Paper, 64);
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
                bool locked = stat.id == "crit4Chance" && !Critical4Unlocked;
                var statArt = UiKit.Icon(row, stat.icon, 86);
                if (locked) statArt.color = Color.gray;
                var text = UiKit.Column(row, "Values", 2, 3);
                CollectionColumnWidth(text, 1.3f);
                UiKit.Text(text, stat.name, 30, TextAnchor.MiddleLeft, 39);
                float current = StatValue(stat.id);
                float next = current + stat.increment * upgrades;
                if (IsCriticalChance(stat.id)) next = Mathf.Clamp(next, 0, 100);
                UiKit.Text(text, locked ? "2배 치명타 MAX 달성 시 해금" : StatNumber(stat.id, current) + " → <color=#216B20>" + StatNumber(stat.id, next) + "</color>", 29, TextAnchor.MiddleLeft, 38);
                if (locked)
                {
                    var lockButton = UiKit.Button(row, "x2 치명타\nMAX 시 해금", null, Color.gray, 94);
                    CollectionButtonText(lockButton,23);
                    CollectionWidth(lockButton.transform, 164); lockButton.interactable = false;
                    continue;
                }
                Func<bool> purchase = () =>
                {
                    if (!UpgradeStat(stat.id, statBatch)) return false;
                    Save(); RefreshPage(); return true;
                };
                var button = CollectionCoinButton(row, upgrades == 0 ? "최대 단계" : "강화 ×" + UiNumber.Format(upgrades) + "\n골드 " + UiNumber.Format(cost), upgrades == 0 ? "최대 단계" : upgrades == 1 ? "강화" : "강화 ×" + UiNumber.Format(upgrades), cost, () => { if (!purchase()) Toast("강화 골드가 부족하거나 최대 단계입니다."); }, 94, 164);
                button.interactable = upgrades > 0 && Gold >= cost;
                UiKit.Repeat(button, "stat:" + stat.id, purchase);
            }
        }

        string StatNumber(string id, float value) => UiNumber.Format(value, IsCriticalChance(id) ? 2 : 1) + (IsCriticalChance(id) ? "%" : id == "healthRegen" ? "/초" : "");

        public long StatUpgradeQuote(string id, int requested, out int upgrades)
        {
            InitCollections();
            var stat = Array.Find(collectionTuning.stats, x => x.id == id);
            upgrades = 0;
            if (stat == null || (id == "crit4Chance" && !Critical4Unlocked)) return 0;
            int available = Math.Max(0, StatMaxLevel(id) - StatLevel(id));
            int target = requested < 0 ? available : Math.Min(Math.Max(0, requested), available);
            long total = 0;
            for (int i = 0; i < target; i++)
            {
                double raw = stat.baseCost * Math.Pow(collectionTuning.costGrowth, StatLevel(id) + i);
                // Keep every configured level purchasable even after exponential costs exceed the wallet's range.
                long price = double.IsInfinity(raw) || raw >= long.MaxValue ? long.MaxValue : Math.Max(1, (long)Math.Ceiling(raw));
                if (price > long.MaxValue - total) break;
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
            long before = Power;
            Gold -= cost;
            statLevels[id] += count;
            RecordServiceProgress("statUpgrade", count);
            NotifyPowerChanged(before, "스탯 강화");
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
            var card = CollectionSlot(row, selected, () => { }, 128 * 4f / 3);
            var slotLayout = card.GetComponent<LayoutElement>();
            slotLayout.minWidth = 128; slotLayout.preferredWidth = 128; slotLayout.flexibleWidth = 0;
            var info = UiKit.Column(row, "Item information", 4, 1);
            CollectionColumnWidth(info, 1);
            UiKit.Text(info, selected.name + " · <color=#236B25>" + GradeNames[selected.rarity] + "</color>", 27, TextAnchor.MiddleLeft, 38);
            CollectionEffectRow(info, "보유 효과", EffectName(selected.effect) + " +" + UiNumber.Format(ItemOwnedValue(selected)) + "%");
            CollectionEffectRow(info, "장착 효과", (selected.category == "Armor" ? "체력" : "공격력") + " +" + UiNumber.Format(ItemEquipValue(selected)));
            var actions = UiKit.Row(info, "Selected item actions", 46, 12);
            if (selected.discovered)
            {
                bool synthesis = selected.rarity != 6 && selected.level >= 100;
                var upgrade = UiKit.Button(actions, synthesis ? "합성" : "강화", () => { if (synthesis) SynthesizeFromUi(selected); else UpgradeSelected(selected, false); }, synthesis ? UiKit.Purple : UiKit.Blue, 46);
                upgrade.interactable = synthesis ? selected.count >= 5 : selected.count >= CopiesNeeded(selected);
                CollectionButtonText(upgrade, 28);
                CollectionButtonText(UiKit.Button(actions, selected.equipped ? "장착 중" : "장착", () => EquipFromUi(selected, false), UiKit.Green, 46), 28);
            }
            else UiKit.Text(actions, "미획득", 24, TextAnchor.MiddleCenter, 32);
            OwnershipStrip(body, "Equipment");
            BuildInventory(body, items, item =>
            {
                if (equipmentCategory == "Armor") selectedArmor = item.id; else selectedClub = item.id;
                RefreshPage();
            }, 5);
            var footer = UiKit.Footer(body, "Equipment footer", 130);
            CollectionActions(footer, equipmentCategory);
            var tabs = UiKit.Row(footer, "Equipment tabs", 52, 4);
            CollectionButtonText(UiKit.EquipmentTab(tabs, "갑옷", () => { equipmentCategory = "Armor"; RefreshPage(); }, equipmentCategory == "Armor", 52), 31);
            CollectionButtonText(UiKit.EquipmentTab(tabs, "몽둥이", () => { equipmentCategory = "Club"; RefreshPage(); }, equipmentCategory == "Club", 52), 31);
            body.gameObject.AddComponent<DoodleCollectionReferenceLayout>().Configure(body, equipmentCategory);
        }

        void BuildSkills(RectTransform body) => BuildLoadout(body, "Skill", "스킬", 8, 8);
        void BuildCompanions(RectTransform body) => BuildLoadout(body, "Companion", "동료", 5, 5);

        void BuildLoadout(RectTransform body, string category, string label, int capacity, int columns)
        {
            var equipped = EquippedItems(category);
            UiKit.Text(body, "장착 슬롯 " + UiNumber.Format(equipped.Count) + "/" + UiNumber.Format(capacity), 32, TextAnchor.MiddleLeft, 42);
            const float slotHeight = 100 * 4f / 3;
            var slots = UiKit.Grid(body, "Equipped " + category, columns, slotHeight);
            UiKit.PortraitGrid(slots);
            for (int i = 0; i < capacity; i++)
            {
                if (i < equipped.Count)
                {
                    var item = equipped[i];
                    var card = CollectionSlot(slots, item, () => ShowCollectionDetail(item), slotHeight);
                    card.GetComponentInChildren<Text>().text = category == "Skill" ? UiNumber.Format(i + 1) : UiNumber.Format(i + 1) + " " + GradeNames[item.rarity];
                }
                else UiKit.Slot(slots, "빈 슬롯", "", 0, 0, 0, false, false, () => Toast("보유 " + label + "을 선택해 장착하세요."), slotHeight);
            }
            OwnershipStrip(body, category);
            CollectionDivider(body);
            UiKit.Text(body, "보유 " + label, 32, TextAnchor.MiddleLeft, 44);
            BuildInventory(body, Items(category), ShowCollectionDetail, 4);
            CollectionActions(UiKit.Footer(body, category + " footer", 72), category);
            body.gameObject.AddComponent<DoodleCollectionReferenceLayout>().Configure(body, category);
        }

        void OwnershipStrip(RectTransform parent, string category)
        {
            string effect = "공격력 +" + UiNumber.Format(EffectBonus("attack", category)) + "%";
            float health = EffectBonus("health", category);
            if (health > 0) effect += " · 체력 +" + UiNumber.Format(health) + "%";
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
        {
            var card = UiKit.Slot(parent, item.name, item.icon, item.rarity, item.count, IsEquipment(item) && item.level >= 100 && item.rarity != 6 ? 5 : CopiesNeeded(item), item.equipped, !item.discovered, click, height);
            if (IsEquipment(item))
            {
                card.GetComponentInChildren<Text>().text = GradeNames[item.rarity] + item.tier;
                var level = UiKit.Text(card.transform, "+" + item.level, 20, TextAnchor.UpperRight, 24);
                level.name = "Enhancement level";
                var rect = level.rectTransform;
                rect.anchorMin = new Vector2(.48f, 1); rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(0, -24); rect.offsetMax = new Vector2(-5, -2);
                level.color = item.discovered ? UiKit.Ink : Color.white;
                card.GetComponent<DoodleUiSlotLayout>().Invalidate();
            }
            return card;
        }

        void SynthesizeFromUi(UiItem item)
        {
            var target = SynthesisTarget(item);
            int made = SynthesizeItem(item);
            RefreshPage();
            Toast(made > 0 ? target.name + " 1개 합성 완료" : "100강 장비의 남은 조각 5개가 필요합니다.");
        }

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
            const float cellHeight = 100 * 4f / 3;
            var grid = UiKit.Grid(viewport, "Collection inventory", columns, cellHeight);
            UiKit.PortraitGrid(grid);
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
            if ((category == "Armor" || category == "Club") && Items(category).Exists(x => x.discovered && x.level >= 100 && x.rarity != 6))
            {
                var synthesis = UiKit.Button(row, "일괄 합성", () => { int made = SynthesizeAll(category); RefreshPage(); Toast(UiNumber.Format(made) + "개 합성했습니다."); }, UiKit.Purple, 68);
                synthesis.interactable = !collectionBulkRunning;
                CollectionButtonText(synthesis, 28);
            }
            var upgrade = UiKit.Button(row, "일괄강화", () => StartCollectionBulk(category), UiKit.Blue, 68);
            upgrade.interactable = !collectionBulkRunning;
            var auto = UiKit.Button(row, "자동장착", () => { AutoEquip(category); Save(); RefreshPage(); Toast("강한 " + CategoryName(category) + "부터 장착했습니다."); }, category == "Armor" || category == "Club" ? UiKit.Green : UiKit.Yellow, 68);
            CollectionButtonText(upgrade, 33); CollectionButtonText(auto, 33);
        }

        public void AutoEquip(string category)
        {
            long before = Power;
            var owned = Items(category).FindAll(x => x.discovered);
            owned.Sort((a, b) => { int score = ItemEquipValue(b).CompareTo(ItemEquipValue(a)); return score != 0 ? score : string.CompareOrdinal(a.id, b.id); });
            foreach (var item in Items(category)) item.equipped = false;
            for (int i = 0; i < Math.Min(EquipLimit(category), owned.Count); i++) { owned[i].equipped = true; owned[i].slot = i; }
            NotifyPowerChanged(before, "자동 장착");
        }

        bool UpgradeSelected(UiItem item, bool detail, bool showMessage = true)
        {
            if (!UpgradeItem(item)) { if (showMessage) Toast(item.discovered ? "강화 수량이 부족하거나 최대 단계입니다." : "아직 획득하지 않았습니다."); return false; }
            Save();
            if (detail) CloseDetail();
            RefreshPage();
            if (detail) ShowCollectionDetail(item);
            if (showMessage) Toast(item.name + " 강화 완료 · Lv. " + UiNumber.Format(item.level));
            return true;
        }

        void ShowCollectionDetail(UiItem item)
        {
            ShowDetail(item.name, body =>
            {
                var window = body.GetComponentInParent<DoodleUiWindow>();
                if (window)
                {
                    window.maxWidth = 360; window.maxHeight = 550; window.centerFromTop = .645f;
                    window.detailScale = 1.8f; window.centerFromTop = .5f;
                    window.headerHeight = 64; window.titleSize = 33; window.Reflow(safe);
                }
                body.GetComponent<VerticalLayoutGroup>().spacing = 5;
                var grade = UiKit.Row(body, "Detail grade", 24, 0);
                CollectionLabelPill(grade, GradeNames[item.rarity], UiKit.Rarity(item.rarity), 88, 24, 21);
                var display = UiKit.Row(body, "Selected item art", 90, 0);
                var portrait = UiKit.Icon(display, item.icon, 90);
                if (!item.discovered) portrait.color = Color.black;
                var quantity = UiKit.Row(body, "Detail quantity", 26, 0);
                var gauge = UiKit.Gauge(quantity, UiNumber.Format(item.count) + "/" + UiNumber.Format(CopiesNeeded(item)), item.count / (float)CopiesNeeded(item), 26);
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
                var ownedText = UiKit.Text(ownedValue, EffectName(item.effect) + " +" + UiNumber.Format(ItemOwnedValue(item)) + "%", 22, TextAnchor.MiddleLeft, 26);
                UiKit.Stretch(ownedText.rectTransform, 7, 1, 7, 1);
                if (item.category == "Skill")
                {
                    // Read the active combat component, never present catalog mock values
                    // as real cooldowns. Orbit skills use their shared public cycle constant.
                    float interval = DoodleIdleGame.VariantInterval(item.ability);
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
                            case "TetherSnake":interval=game.tetherInterval;break;
                            case "WaveSnakes":interval=game.snakeInterval;break;
                            case "FireRing":interval=game.ringInterval;break;
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
                    UiKit.Text(potency, "위력  Lv. " + UiNumber.Format(item.level), 18, TextAnchor.MiddleLeft, 20);
                    UiKit.Text(potency, UiNumber.Format(ItemEquipValue(item)), 23, TextAnchor.MiddleLeft, 24);
                    var reuse = CollectionBox(measures, "Skill reuse", UiKit.Paper);
                    reuse.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
                    reuse.GetComponent<Outline>().effectColor = new Color(.89f, .81f, .70f);
                    UiKit.Text(reuse, "재사용", 18, TextAnchor.MiddleLeft, 20);
                    UiKit.Text(reuse, interval > 0 ? UiNumber.Format(interval, 2) + "초" : "자동", 23, TextAnchor.MiddleLeft, 24);
                    UiKit.Text(body, "자동 전투 유지 · 장착 공격력 +" + UiNumber.Format(ItemEquipValue(item) * .02f, 2) + "%", 15, TextAnchor.MiddleCenter, 20);
                }
                else UiKit.Text(body, "장착 공격력 +" + UiNumber.Format(ItemEquipValue(item)) + "%", 23, TextAnchor.MiddleCenter, 32);
                if (!item.discovered) UiKit.Text(body, "미획득 · 효과가 적용되지 않습니다.", 18, TextAnchor.MiddleCenter, 26);
                if (item.discovered)
                {
                    var buttons = UiKit.Row(UiKit.Footer(body, "Collection detail footer", 60), "Detail actions", 56);
                    bool refund = item.category == "Skill" && item.level >= ItemMaxLevel(item);
                    var upgrade = UiKit.Button(buttons, refund ? "환불\n" + UiNumber.Format(SkillRefundQuote(item)) + " 다이아" : "강화", () =>
                    {
                        if (!refund) { UpgradeSelected(item, true); return; }
                        int paid = RefundSkill(item);
                        CloseDetail(); RefreshPage(); ShowCollectionDetail(item);
                        Toast(paid > 0 ? UiNumber.Format(paid) + " 다이아 환불 완료" : "환불 가능한 조각 또는 지갑 공간이 없습니다.");
                    }, refund ? UiKit.Purple : UiKit.Blue, 56);
                    upgrade.interactable = refund ? CanRefundSkill(item) : item.count >= CopiesNeeded(item);
                    CollectionButtonText(upgrade, 27);
                    CollectionButtonText(UiKit.Button(buttons, item.equipped ? "장착 해제" : "장착", () => EquipFromUi(item, true), UiKit.Yellow, 56), 27);
                }
            });
        }

        void EquipFromUi(UiItem item, bool detail)
        {
            if (!item.discovered) { Toast("아직 획득하지 않았습니다."); return; }
            long before = Power;
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
                                long replacementBefore = Power;
                                previous.equipped = false; item.equipped = true; item.slot = previous.slot;
                                NormalizeEquipment(item.category);
                                NotifyPowerChanged(replacementBefore, "장착 교체");
                                Save(); CloseDetail(); if (detail) CloseDetail(); RefreshPage();
                            }, UiKit.Green, 54);
                        }
                    });
                    return;
                }
                item.equipped = true; item.slot = limit == 1 ? 0 : equipped.Count;
            }
            NormalizeEquipment(item.category);
            NotifyPowerChanged(before, item.equipped ? "장착" : "장착 해제");
            Save(); if (detail) CloseDetail(); RefreshPage();
        }

        void BuildRelics(RectTransform body)
        {
            var note = UiKit.Box(body, "All relics apply", new Color(.94f, .90f, .82f), 56);
            note.GetComponent<Outline>().enabled = false;
            var noteText = UiKit.Text(note, "모든 유물 효과 적용 · 강화 시 같은 유물 1개 소모\n성공 확률 50% · 실패해도 현재 단계 유지", 22, TextAnchor.MiddleCenter, 56);
            UiKit.Stretch(noteText.rectTransform, 4, 2, 4, 2);
            var totals = CollectionBox(body, "Relic effects", new Color(.89f, .985f, .85f));
            totals.GetComponent<Outline>().effectColor = new Color(.27f, .53f, .23f);
            string summary = "총 유물 효과   공격력 +" + UiNumber.Format(EffectBonus("attack", "Relic")) + "% · 체력 +" + UiNumber.Format(EffectBonus("health", "Relic")) + "%\n"
                + "골드 +" + UiNumber.Format(EffectBonus("gold", "Relic")) + "% · 회복 +" + UiNumber.Format(EffectBonus("healthRegen", "Relic")) + "% · 치명 피해 +" + UiNumber.Format(EffectBonus("critDamage", "Relic")) + "%";
            UiKit.Text(totals, summary, 23, TextAnchor.MiddleCenter, 62);
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
                CollectionLabelPill(title, "Lv. " + UiNumber.Format(item.level), new Color(.94f, .90f, .82f), 64, 32, 23);
                UiKit.Text(info, EffectName(item.effect) + " +" + UiNumber.Format(ItemOwnedValue(item)) + "% → <color=#216B20>+" + UiNumber.Format(ItemOwnedValue(item) + collectionTuning.relicStepPercent) + "%</color>", 24, TextAnchor.MiddleLeft, 34);
                UiKit.Text(info, "성공 확률 50%", 23, TextAnchor.MiddleLeft, 30);
                Func<bool> attempt = () => UpgradeRelicFromUi(item, false);
                string caption = !item.discovered ? "미획득" : item.level >= collectionTuning.maxItemLevel ? "최대 단계" : "강화";
                var button = UiKit.Button(row, caption + "\n" + UiNumber.Format(item.count) + " / 1개", () => UpgradeRelicFromUi(item, true), UiKit.Yellow, 94);
                CollectionWidth(button.transform, 146);
                button.interactable = item.discovered && item.count > 0 && item.level < collectionTuning.maxItemLevel && !collectionBulkRunning;
                UiKit.Repeat(button, "relic:" + item.id, attempt);
            }
            var footer = UiKit.Footer(body, "Relic footer", 72);
            var bulk = UiKit.Button(footer, "일괄강화", () => StartCollectionBulk("Relic"), UiKit.Blue, 68);
            bulk.interactable = !collectionBulkRunning;
            CollectionButtonText(bulk, 34);
        }

        bool UpgradeRelicFromUi(UiItem item, bool showMessage)
        {
            bool success;
            if (!TryUpgradeRelic(item, out success))
            {
                if (showMessage) Toast("강화할 유물이 없거나 최대 단계입니다.");
                return false;
            }
            Save(); RefreshPage();
            if (showMessage) Toast(success ? item.name + " 강화 성공!" : "강화 실패 · 유물 1개 소모, 현재 단계 유지");
            return true;
        }

        void StartCollectionBulk(string category)
        {
            if (collectionBulkRunning) return;
            StartCoroutine(UpgradeCollectionBulk(category));
        }

        IEnumerator UpgradeCollectionBulk(string category)
        {
            collectionBulkRunning = true;
            long before = Power;
            long attempts = 0, successes = 0;
            int chunk = 0;
            try
            {
                foreach (var item in Items(category))
                {
                    while (true)
                    {
                        bool success = true;
                        bool attempted = category == "Relic" ? TryUpgradeRelic(item, out success, false) : UpgradeItem(item, false);
                        if (!attempted) break;
                        attempts++;
                        if (success) successes++;
                        if (++chunk < 64) continue;
                        chunk = 0;
                        Save();
                        RefreshCollectionBulkPage(category);
                        yield return null;
                    }
                }
            }
            finally
            {
                collectionBulkRunning = false;
                Save();
                NotifyPowerChanged(before, "일괄 강화");
            }
            RefreshCollectionBulkPage(category);
            Toast(attempts == 0 ? "강화 가능한 수량이 없습니다." : category == "Relic"
                ? UiNumber.Format(attempts) + "개 소모 · " + UiNumber.Format(successes) + "회 성공"
                : UiNumber.Format(successes) + "회 강화했습니다.");
        }

        void RefreshCollectionBulkPage(string category)
        {
            string page = category == "Armor" || category == "Club" ? "Equipment" : category == "Skill" ? "Skills" : category == "Companion" ? "Companions" : "Relics";
            if (ActivePage == page) RefreshPage();
        }

        public float RelicSuccessChance(UiItem item) => .5f;
        // The upgrade cost is a count of this relic, never a gold price.
        public long RelicUpgradeCost(UiItem item) => 1;
        public bool TryUpgradeRelic(UiItem item, out bool success, bool notifyPower = true)
        {
            success = false;
            if (item == null || item.category != "Relic" || !item.discovered || item.count < 1 || item.level >= collectionTuning.maxItemLevel) return false;
            long before = notifyPower ? Power : 0;
            item.count--;
            success = collectionRandom.NextDouble() < .5;
            if (success) { item.level++; RecordServiceProgress("relicUpgrade", 1); }
            if (notifyPower) NotifyPowerChanged(before, "유물 강화");
            return true;
        }

        static string EffectName(string effect)
        {
            switch (effect) { case "health": return "체력"; case "healthRegen": return "체력 회복"; case "critDamage": return "치명타 피해"; case "crit2Chance": return "2배 치명타 확률"; case "crit4Chance": return "4배 치명타 확률"; case "gold": return "골드 획득"; default: return "공격력"; }
        }
        static string CategoryName(string category)
        {
            switch (category) { case "Armor": return "갑옷"; case "Club": return "몽둥이"; case "Skill": return "스킬"; case "Companion": return "동료"; default: return "유물"; }
        }
    }

    /// <summary>Restores the reference layout after leaving a short viewport.</summary>
    public sealed class DoodleCollectionReferenceLayout : MonoBehaviour
    {
        readonly List<Action<bool>> rules = new List<Action<bool>>();
        RectTransform body, viewport;
        bool? compact;
        float compactViewportGain;

        public void Configure(RectTransform content, string category)
        {
            body = content;
            viewport = body.GetComponentInParent<ScrollRect>().viewport;
            Layout(body.GetComponent<VerticalLayoutGroup>(), 3, new RectOffset(2, 2, 2, 2));
            bool equipment = category == "Armor" || category == "Club";
            if (equipment)
            {
                var spec = body.Find("Selected equipment/Equipment specification");
                Layout(spec.parent.GetComponent<VerticalLayoutGroup>(), 0, new RectOffset(7, 7, 5, 5));
                Height(spec, 128);
                var selected = spec.GetChild(0);
                Height(selected, 128);
                Width(selected, 96);
                var info = spec.Find("Item information");
                Layout(info.GetComponent<VerticalLayoutGroup>(), 3, null);
                Height(info.GetChild(0), 24); Font(info.GetChild(0).GetComponent<Text>(), 24);
                foreach (string effect in new[] { "보유 효과", "장착 효과" })
                {
                    var row = info.Find(effect); Height(row, 22);
                    Height(row.GetChild(0), 22);
                    foreach (var text in row.GetComponentsInChildren<Text>()) { Height(text.transform, 22); Font(text, 20); }
                }
                var actions = info.Find("Selected item actions"); Height(actions, 32);
                foreach (var button in actions.GetComponentsInChildren<Button>())
                { Height(button.transform, 32); Font(button.GetComponentInChildren<Text>(), 23); }
                var ownership = body.Find("Total ownership"); Height(ownership, 32);
                Height(ownership.GetChild(0), 32); Font(ownership.GetComponentInChildren<Text>(), 22);
                CompactFooter(116, 62, 46);
            }
            else
            {
                bool companion = category == "Companion";
                Height(body.GetChild(0), companion ? 24 : 28); Font(body.GetChild(0).GetComponent<Text>(), companion ? 23 : 25);
                var slots = body.Find("Equipped " + category);
                Columns(slots.GetComponent<GridLayoutGroup>(), category == "Skill" ? 8 : 5);
                for (int i = 0; i < slots.childCount; i++) EquippedNumber(slots.GetChild(i), i + 1);
                var ownership = body.Find("Total ownership");
                Layout(ownership.GetComponent<VerticalLayoutGroup>(), 0, new RectOffset(4, 4, 1, 1));
                var badges = ownership.Find("Ownership badges"); Height(badges, companion ? 24 : 30);
                foreach (Transform badge in badges)
                {
                    Height(badge, companion ? 24 : 30);
                    var text = badge.GetComponentInChildren<Text>(); Height(text.transform, companion ? 22 : 28); Font(text, companion ? 21 : 23);
                }
                foreach (Transform child in body)
                {
                    var text = child.GetComponent<Text>();
                    if (text && text.text.StartsWith("보유 ", StringComparison.Ordinal)) { Height(child, companion ? 26 : 28); Font(text, companion ? 24 : 25); }
                }
                if (companion) CompactFooter(60, 56, 0);
            }
            var inventory = body.Find("Collection inventory viewport/Inventory clipping area/Collection inventory");
            Columns(inventory.GetComponent<GridLayoutGroup>(), 6);
            Reflow();
        }

        void CompactFooter(float height, float actionHeight, float tabHeight)
        {
            var window = body.GetComponentInParent<DoodleUiWindow>();
            var footer = window.footer;
            float originalHeight = footer.sizeDelta.y;
            compactViewportGain = originalHeight - height;
            float originalBottom = viewport.offsetMin.y;
            float originalRailBottom = window.rail.offsetMin.y;
            var actions = footer.Find("Collection actions");
            Height(actions, actionHeight);
            foreach (var button in actions.GetComponentsInChildren<Button>()) Height(button.transform, actionHeight);
            var tabs = footer.Find("Equipment tabs");
            if (tabs && tabHeight > 0)
            {
                Height(tabs, tabHeight);
                foreach (var button in tabs.GetComponentsInChildren<Button>()) Height(button.transform, tabHeight);
            }
            rules.Add(shortMode =>
            {
                float shrink = shortMode ? originalHeight - height : 0;
                footer.sizeDelta = new Vector2(footer.sizeDelta.x, shortMode ? height : originalHeight);
                viewport.offsetMin = new Vector2(viewport.offsetMin.x, originalBottom - shrink);
                window.rail.offsetMin = new Vector2(window.rail.offsetMin.x, originalRailBottom - shrink);
            });
        }

        void Height(Transform target, float small)
        {
            var element = target.GetComponent<LayoutElement>();
            if (!element) return;
            float min = element.minHeight, preferred = element.preferredHeight;
            rules.Add(shortMode => { element.minHeight = shortMode ? small : min; element.preferredHeight = shortMode ? small : preferred; });
        }

        void Font(Text text, int size)
        {
            if (!text) return;
            int font = text.fontSize, max = text.resizeTextMaxSize;
            rules.Add(shortMode => { text.fontSize = shortMode ? size : font; text.resizeTextMaxSize = shortMode ? size : max; });
        }

        void Layout(VerticalLayoutGroup layout, float spacing, RectOffset padding)
        {
            float originalSpacing = layout.spacing;
            RectOffset originalPadding = layout.padding;
            rules.Add(shortMode => { layout.spacing = shortMode ? spacing : originalSpacing; layout.padding = shortMode && padding != null ? padding : originalPadding; });
        }

        void Width(Transform target, float small)
        {
            var element = target.GetComponent<LayoutElement>();
            float min = element.minWidth, preferred = element.preferredWidth;
            rules.Add(shortMode => { element.minWidth = shortMode ? small : min; element.preferredWidth = shortMode ? small : preferred; });
        }

        void Columns(GridLayoutGroup grid, int shortColumns)
        {
            int original = grid.constraintCount;
            rules.Add(shortMode =>
            {
                grid.constraintCount = shortMode ? shortColumns : original;
                UiKit.PortraitGrid((RectTransform)grid.transform);
            });
        }

        void EquippedNumber(Transform card, int number)
        {
            var label = card.GetChild(0).GetComponent<Text>();
            string originalText = label.text;
            rules.Add(shortMode => label.text = shortMode ? UiNumber.Format(number) : originalText);
        }

        void LateUpdate() => Reflow();
        public void Reflow()
        {
            if (!body || !viewport || viewport.rect.height <= 0) return;
            // Footer compression adds viewport space; measure the uncompressed height
            // so a viewport near the breakpoint cannot alternate layouts every frame.
            bool shortMode = viewport.rect.height - (compact == true ? compactViewportGain : 0) < 500;
            if (compact == shortMode) return;
            compact = shortMode;
            foreach (var rule in rules) rule(shortMode);
            LayoutRebuilder.ForceRebuildLayoutImmediate(body);
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
            float preferred = available < 500 ? Mathf.Clamp(available - otherHeight, 96, 140)
                : available >= 600 ? Mathf.Max(140, available - otherHeight - 2) : 180;
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
