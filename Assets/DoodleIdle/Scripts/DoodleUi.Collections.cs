using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        string equipmentCategory = "Armor", selectedArmor = "armor_4", selectedClub = "club_4", selectedNecklace = "necklace_0";
        float equipmentTabPosition;
        int statBatch = 1;
        bool hideMaxStats;
        bool collectionBulkRunning;
        UiItem pendingEquip;
        readonly Dictionary<string, float> collectionScrollPositions = new Dictionary<string, float>();
        readonly List<Action> statWalletBindings = new List<Action>();
        GameNumber displayedStatGold;

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

        Button CollectionCoinButton(Transform parent, string name, string caption, GameNumber cost, Action click, float height, float width, out Text captionText, out Text priceText)
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
            captionText = UiKit.Text(content, caption, 28, TextAnchor.MiddleCenter, 34);
            var price = UiKit.Row(content, "Coin price", 34, 5);
            UiKit.Icon(price, "Gold", 30);
            priceText = UiKit.Text(price, UiNumber.Format(cost), 27, TextAnchor.MiddleCenter, 34);
            return button;
        }

        void CollectionDivider(RectTransform parent)
        {
            var line = UiKit.Rect(parent, "Collection divider"); UiKit.Height(line, 2); UiKit.Flexible(line);
            var image = line.gameObject.AddComponent<Image>(); image.color = new Color(.72f, .72f, .72f); image.raycastTarget = false;
        }

        void BuildStats(RectTransform body)
        {
            statWalletBindings.Clear(); displayedStatGold = GoldAmount;
            body.GetComponent<VerticalLayoutGroup>().spacing = 12;
            var power = UiKit.Row(body, "Combat power", 126, 16);
            power.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(42, 0, 0, 0);
            UiKit.Icon(power, "Player", 124);
            var powerText = UiKit.Text(power, "전투력 " + UiNumber.Format(PowerAmount), 35, TextAnchor.MiddleCenter, 108);
            CollectionWidth(powerText.transform, 286);
            var batch = UiKit.Row(body, "Stat quantity", 64, 12);
            foreach (int amount in new[] { 1, 10, 100, -1 })
            {
                int selected = amount;
                var mode = UiKit.Button(batch, amount < 0 ? "MAX" : "×" + UiNumber.Format(amount), () => { statBatch = selected; RefreshPage(); }, statBatch == amount ? UiKit.Blue : UiKit.Paper, 64);
                CollectionButtonText(mode, 34);
            }
            var hideMax = UiKit.Button(body, hideMaxStats ? "✓ MAX 숨기기" : "MAX 숨기기", () => {
                hideMaxStats = !hideMaxStats;
                PlayerPrefs.SetInt("DoodleUi.HideMaxStats", hideMaxStats ? 1 : 0); PlayerPrefs.Save(); RefreshPage();
            }, hideMaxStats ? UiKit.Green : UiKit.Paper, 42);
            hideMax.name = "Hide max stats";
            foreach (var definition in collectionTuning.stats)
            {
                var stat = definition;
                if (hideMaxStats && StatLevel(stat.id) >= StatMaxLevel(stat.id)) continue;
                int upgrades;
                GameNumber cost = StatUpgradeQuoteAmount(stat.id, statBatch, out upgrades);
                var frame = CollectionBox(body, "Stat " + stat.id, UiKit.Paper);
                CollectionSoftBorder(frame);
                var row = UiKit.Row(frame, stat.name, 104, 10);
                bool locked = IsCriticalChance(stat.id) && !CriticalUnlocked(stat.id);
                var statArt = UiKit.Icon(row, stat.icon, 86);
                if (locked) statArt.color = Color.gray;
                var criticalBadge = statArt.GetComponentInChildren<DoodleCriticalBadge>();
                if (criticalBadge && locked) criticalBadge.color = Color.gray;
                var text = UiKit.Column(row, "Values", 2, 3);
                CollectionColumnWidth(text, 1.3f);
                UiKit.Text(text, stat.name + " Lv." + StatLevel(stat.id).ToString("N0"), 30, TextAnchor.MiddleLeft, 39);
                GameNumber current = StatAmount(stat.id);
                GameNumber next = StatAmountAfterUpgrades(stat.id,upgrades);
                if (IsCriticalChance(stat.id)) next = GameNumber.Clamp(next, 0, 100);
                var valueText = UiKit.Text(text, locked ? CriticalUnlockText(stat.id) + " MAX 달성 시 해금" : StatNumber(stat.id, current) + " → <color=#216B20>" + StatNumber(stat.id, next) + "</color>", 29, TextAnchor.MiddleLeft, 38);
                if (locked)
                {
                    var lockButton = UiKit.Button(row, CriticalUnlockText(stat.id) + "\nMAX 시 해금", null, Color.gray, 94);
                    CollectionButtonText(lockButton,23);
                    CollectionWidth(lockButton.transform, 164); lockButton.interactable = false;
                    continue;
                }
                Func<bool> purchase = () =>
                {
                    if (!UpgradeStat(stat.id, statBatch)) return false;
                    Save(); RefreshPage(); return true;
                };
                var button = CollectionCoinButton(row, upgrades == 0 ? "최대 단계" : "강화 ×" + UiNumber.Format(upgrades) + "\n골드 " + UiNumber.Format(cost), upgrades == 0 ? "최대 단계" : upgrades == 1 ? "강화" : "강화 ×" + UiNumber.Format(upgrades), cost, () => { if (!purchase()) Toast("강화 골드가 부족하거나 최대 단계입니다."); }, 94, 164, out var captionText, out var priceText);
                button.interactable = upgrades > 0 && GoldAmount >= cost;
                statWalletBindings.Add(() => {
                    if (!button) return;
                    GameNumber liveCost = StatUpgradeQuoteAmount(stat.id, statBatch, out int liveUpgrades);
                    button.interactable = liveUpgrades > 0 && GoldAmount >= liveCost;
                    button.name = liveUpgrades == 0 ? "최대 단계" : "강화 ×" + UiNumber.Format(liveUpgrades) + "\n골드 " + UiNumber.Format(liveCost);
                    captionText.text = liveUpgrades == 0 ? "최대 단계" : liveUpgrades == 1 ? "강화" : "강화 ×" + UiNumber.Format(liveUpgrades);
                    priceText.text = UiNumber.Format(liveCost);
                    valueText.text = StatNumber(stat.id, StatAmount(stat.id)) + " → <color=#216B20>" + StatNumber(stat.id, StatAmountAfterUpgrades(stat.id, liveUpgrades)) + "</color>";
                });
                UiKit.Repeat(button, "stat:" + stat.id, purchase);
            }
        }

        void RefreshStatWallet()
        {
            if (ActivePage != "Stats") { statWalletBindings.Clear(); return; }
            if (displayedStatGold == GoldAmount) return;
            displayedStatGold = GoldAmount;
            // Update existing controls without rebuilding the popup or interrupting scrolling/holds.
            foreach (var refresh in statWalletBindings) refresh();
        }

        static string CriticalUnlockText(string id) => CriticalMultiplierAt(Math.Max(0, Array.IndexOf(CriticalStatIds, id) - 1)) + "배 치명타";
        string StatNumber(string id, GameNumber value) => UiNumber.Format(value, IsCriticalChance(id) ? 2 : 1) + (IsCriticalChance(id) ? "%" : id == "healthRegen" ? "/초" : "");

        public long StatUpgradeQuote(string id, int requested, out int upgrades) => (long)StatUpgradeQuoteAmount(id, requested, out upgrades);
        public GameNumber StatUpgradeQuoteAmount(string id, int requested, out int upgrades)
        {
            InitCollections();
            var stat = Array.Find(collectionTuning.stats, x => x.id == id);
            upgrades = 0;
            if (stat == null || (IsCriticalChance(id) && !CriticalUnlocked(id))) return 0;
            int available = Math.Max(0, StatMaxLevel(id) - StatLevel(id));
            int target = requested < 0 ? available : Math.Min(Math.Max(0, requested), available);
            GameNumber total = 0;
            for (int i = 0; i < target;)
            {
                int level = StatLevel(id) + i;
                GameNumber price = StatUpgradePriceAmount(collectionTuning.statCosts, id, level);
                // Large prices have no representable fractional gold; sum each growth band directly.
                // Flat bands also have one exact, rounded unit price at every level.
                if (!IsCriticalChance(id)) {
                    var tuning = collectionTuning.statCosts;
                    float rate = BalanceValue(tuning.commonGrowth, 0, .004f);
                    int latest = int.MinValue, band = target - i;
                    foreach (var step in tuning.commonGrowthSteps ?? Array.Empty<DoodleGrowthStep>()) {
                        if (step == null) continue;
                        if (step.from <= level + 1 && step.from >= latest) {
                            latest = step.from; rate = BalanceValue(step.growth, 0, 0);
                        } else if (step.from > level + 1) band = Math.Min(band, step.from - level);
                    }
                    if (rate == 0 || price.Exponent >= 15) {
                        GameNumber sum = StatPriceBandSum(price, rate, band);
                        if (requested < 0 && sum > GameNumber.Round(GoldAmount - total)) {
                            int low = 0, high = band;
                            while (low < high) {
                                int mid = low + (high - low + 1) / 2;
                                if (StatPriceBandSum(price, rate, mid) <= GameNumber.Round(GoldAmount - total)) low = mid;
                                else high = mid - 1;
                            }
                            total += StatPriceBandSum(price, rate, low); upgrades += low;
                            break;
                        }
                        total += sum; upgrades += band; i += band;
                        continue;
                    }
                }
                if (requested < 0 && price > GameNumber.Round(GoldAmount - total)) break;
                total += price;
                upgrades++; i++;
            }
            // MAX still presents the next purchase price when the wallet is empty.
            if (requested < 0 && upgrades == 0 && available > 0) return StatUpgradeQuoteAmount(id, 1, out upgrades);
            return GameNumber.Round(total);
        }

        static GameNumber StatPriceBandSum(GameNumber price, float rate, int count)
        {
            if (count == 0) return 0;
            if (rate == 0) return price * count;
            GameNumber factor = 1d + rate, block = 1, prefix = 1, sum = 0;
            // Binary geometric sum avoids cancellation when the configured growth is tiny.
            while (count > 0) {
                if ((count & 1) != 0) { sum += prefix * block; prefix *= factor; }
                count >>= 1;
                if (count > 0) { block *= 1 + factor; factor *= factor; }
            }
            return price * sum;
        }

        public bool UpgradeStat(string id, int requested)
        {
            int count;
            GameNumber cost = StatUpgradeQuoteAmount(id, requested, out count);
            if (count == 0 || cost > GoldAmount) return false;
            GameNumber before = PowerAmount;
            GoldAmount -= cost;
            statLevels[id] += count;
            RecordServiceProgress("statUpgrade", count);
            RecordServiceProgress("statUpgrade:"+id, count);
            NotifyPowerChanged(before, "스탯 강화");
            return true;
        }

        void BuildEquipment(RectTransform body)
        {
            body.GetComponent<VerticalLayoutGroup>().spacing = 8;
            string selectedId = equipmentCategory == "Armor" ? selectedArmor : equipmentCategory == "Club" ? selectedClub : selectedNecklace;
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
            CollectionEffectRow(info, "보유 효과", EffectName(selected.effect) + " +" + UiNumber.Format(ItemOwnedAmount(selected)) + "%" + (ItemOwnedGoldValue(selected) > 0 ? " · 골드 +" + UiNumber.Format(ItemOwnedGoldValue(selected)) + "%" : ""));
            CollectionEffectRow(info, "장착 효과", selected.category == "Necklace" ? "체력 회복 +" + UiNumber.Format(NecklaceRecovery(ItemEquipAmount(selected)), 2) + "/초" : (selected.category == "Armor" ? "체력" : "공격력") + " +" + UiNumber.Format(ItemEquipAmount(selected)) + "%");
            var actions = UiKit.Row(info, "Selected item actions", 46, 12);
            if (selected.discovered)
            {
                bool synthesis = SynthesisTarget(selected) != null && selected.level >= 100;
                var upgrade = UiKit.Button(actions, synthesis ? "합성" : "강화", () => { if (synthesis) SynthesizeFromUi(selected); else UpgradeSelected(selected, false); }, synthesis ? UiKit.Purple : UiKit.Blue, 46);
                upgrade.interactable = synthesis ? selected.count >= 5 : selected.count >= CopiesNeeded(selected);
                Notify(upgrade.transform,()=>synthesis?CanSynthesize(selected):CanUpgradeItem(selected));
                CollectionButtonText(upgrade, 28);
                var equip=UiKit.Button(actions, selected.equipped ? "장착 중" : "장착", () => EquipFromUi(selected, false), UiKit.Green, 46);
                Notify(equip.transform,()=>CanImproveLoadout(selected));CollectionButtonText(equip,28);
            }
            else UiKit.Text(actions, "미획득", 24, TextAnchor.MiddleCenter, 32);
            OwnershipStrip(body, equipmentCategory);
            BuildInventory(body, items, item =>
            {
                if (equipmentCategory == "Armor") selectedArmor = item.id; else if (equipmentCategory == "Club") selectedClub = item.id; else selectedNecklace = item.id;
                RefreshPage();
            }, 5);
            var footer = UiKit.Footer(body, "Equipment footer", 130);
            CollectionActions(footer, equipmentCategory);
            var tabs = UiKit.Box(footer, "Equipment tabs", new Color(.78f,.78f,.76f), 52);
            UiKit.SlidingTabs(tabs, new[] { "갑옷", "몽둥이", "목걸이" }, equipmentCategory == "Armor" ? 0 : equipmentCategory == "Club" ? 1 : 2, equipmentTabPosition,
                index => { equipmentCategory = index == 0 ? "Armor" : index == 1 ? "Club" : "Necklace"; RefreshPage(); }, value => equipmentTabPosition = value, 52);
            Notify(tabs.Find("갑옷"), () => EquipmentCategoryNeedsAttention("Armor"));
            Notify(tabs.Find("몽둥이"), () => EquipmentCategoryNeedsAttention("Club"));
            Notify(tabs.Find("목걸이"), () => EquipmentCategoryNeedsAttention("Necklace"));
            body.gameObject.AddComponent<DoodleCollectionReferenceLayout>().Configure(body, equipmentCategory);
        }

        void BuildSkills(RectTransform body) => BuildLoadout(body, "Skill", "스킬", 8, 8);
        void BuildCompanions(RectTransform body) => BuildLoadout(body, "Companion", "동료", 5, 5);

        void BuildLoadout(RectTransform body, string category, string label, int capacity, int columns)
        {
            var equipped = EquippedItems(category);
            int unlocked = EquipLimit(category);
            bool replacing = pendingEquip != null && pendingEquip.category == category;
            UiKit.Text(body, (replacing ? "교체할 장착 슬롯을 선택하세요" : "장착 슬롯 " + UiNumber.Format(equipped.Count) + "/" + UiNumber.Format(unlocked)), 32, TextAnchor.MiddleLeft, 42);
            const float slotHeight = 100 * 4f / 3;
            var slots = UiKit.Grid(body, "Equipped " + category, columns, slotHeight);
            UiKit.PortraitGrid(slots);
            if (replacing) slots.GetComponent<GridLayoutGroup>().padding.top = 30;
            for (int i = 0; i < capacity; i++)
            {
                if (i >= unlocked) {
                    string requirement = category == "Skill" ? "스테이지 " + SkillSlotUnlockStage(i) + " 도달" : CompanionSlotUnlockRequirement(i);
                    var locked = UiKit.Button(slots, "", () => Toast(requirement + " 시 해금됩니다."), new Color(.55f, .56f, .57f), slotHeight);
                    locked.name = (category == "Skill" ? "Locked skill slot " : "Locked companion slot ") + i;
                    var labelText = locked.GetComponentInChildren<Text>();
                    labelText.text = category == "Skill" ? SkillSlotUnlockStage(i) + "\n스테이지" : GradeNames[i + 2] + "1 이상\n갑옷 획득";
                    labelText.fontSize = labelText.resizeTextMaxSize = 16;
                    labelText.resizeTextMinSize = 10; labelText.color = Color.white;
                    labelText.rectTransform.anchorMin = Vector2.zero; labelText.rectTransform.anchorMax = new Vector2(1, .43f);
                    labelText.rectTransform.offsetMin = new Vector2(2, 3); labelText.rectTransform.offsetMax = new Vector2(-2, 0);
                    var padlock = UiKit.Rect(locked.transform, "Slot unlock padlock");
                    padlock.anchorMin = padlock.anchorMax = new Vector2(.5f, .68f);
                    padlock.sizeDelta = new Vector2(22, 28);
                    padlock.gameObject.AddComponent<DoodleUiPadlock>().raycastTarget = false;
                    continue;
                }
                if (i < equipped.Count)
                {
                    var item = equipped[i];
                    var card = CollectionSlot(slots, item, () => { if (replacing) ReplaceEquippedSlot(item); else ShowCollectionDetail(item); }, slotHeight);
                    if (replacing)
                    {
                        var marker = UiKit.Rect(card.transform, "Replacement arrow");
                        marker.anchorMin = marker.anchorMax = new Vector2(.5f, 1); marker.pivot = new Vector2(.5f, 0);
                        marker.anchoredPosition = new Vector2(0, 3); marker.sizeDelta = new Vector2(28, 28);
                        var image = marker.gameObject.AddComponent<Image>(); image.sprite = UiKit.Art("ReplaceArrow"); image.raycastTarget = false;
                    }
                    card.GetComponentInChildren<Text>().text = category == "Skill" ? UiNumber.Format(i + 1) : UiNumber.Format(i + 1) + " " + GradeNames[item.rarity];
                }
                else UiKit.Slot(slots, "빈 슬롯", "AddSlot", 0, 0, 0, false, false, () => Toast("보유 " + label + "을 선택해 장착하세요."), slotHeight);
            }
            if (replacing) UiKit.Button(body, "교체 취소", () => { pendingEquip = null; RefreshPage(); }, UiKit.Paper, 34);
            OwnershipStrip(body, category);
            CollectionDivider(body);
            UiKit.Text(body, "보유 " + label, 32, TextAnchor.MiddleLeft, 44);
            BuildInventory(body, Items(category), item => { if (replacing) Toast("위의 장착 슬롯 중 교체할 슬롯을 선택하세요."); else ShowCollectionDetail(item); }, category == "Skill" ? 5 : 4);
            CollectionActions(UiKit.Footer(body, category + " footer", 72), category);
            body.gameObject.AddComponent<DoodleCollectionReferenceLayout>().Configure(body, category);
        }

        void OwnershipStrip(RectTransform parent, string category)
        {
            string effect = "공격력 +" + UiNumber.Format(EffectAmount("attack", category)) + "%";
            GameNumber health = EffectAmount("health", category);
            if (health > 0) effect += " · 체력 +" + UiNumber.Format(health) + "%";
            if (IsEquipmentCategory(category))
            {
                var row = UiKit.Row(parent, "Total ownership", 42, 0);
                string equipmentEffect = category == "Club" ? "attack" : category == "Armor" ? "health" : "healthRegen";
                effect = EffectName(equipmentEffect) + " +" + UiNumber.Format(EffectAmount(equipmentEffect, category)) + "%";
                UiKit.Text(row, "총 보유 효과   " + effect, 23, TextAnchor.MiddleCenter, 42);
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
            bool synthesisOnly = item.discovered && item.level >= ItemMaxLevel(item) && SynthesisTarget(item) != null;
            var card = UiKit.Slot(parent, item.name, item.icon, item.rarity, item.count, synthesisOnly ? 5 : CopiesNeeded(item), item.equipped, !item.discovered, click, height);
            card.transform.Find("Quantity gauge/Fill").GetComponent<Image>().color = synthesisOnly ? UiKit.Purple : UiKit.Green;
            if (IsEquipment(item) || item.category == "Skill" || item.category == "Companion")
            {
                card.GetComponentInChildren<Text>().text = GradeNames[item.rarity] + item.tier;
                var level = UiKit.Text(card.transform, "Lv." + item.level.ToString("N0"), 20, TextAnchor.UpperRight, 24);
                level.name = "Enhancement level";
                var rect = level.rectTransform;
                rect.anchorMin = new Vector2(.38f, 1); rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(0, -24); rect.offsetMax = new Vector2(-5, -2);
                level.color = item.discovered ? UiKit.Ink : Color.white;
                card.GetComponent<DoodleUiSlotLayout>().Invalidate();
            }
            Notify(card.transform,()=>ItemNeedsAttention(item));
            return card;
        }

        void SynthesizeFromUi(UiItem item, bool detail = false)
        {
            var target = SynthesisTarget(item);
            int made = SynthesizeItem(item);
            if (detail) CloseDetail();
            RefreshPage();
            if (detail) ShowCollectionDetail(item);
            Toast(made > 0 ? target.name + " 1개 합성 완료" : "100레벨 " + CategoryName(item.category) + "의 남은 조각 5개가 필요합니다.");
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
                bool selected = (category == "Armor" && item.id == selectedArmor) || (category == "Club" && item.id == selectedClub) || (category == "Necklace" && item.id == selectedNecklace);
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
            if (CanSynthesizeCategory(category) && Items(category).Exists(x => x.discovered && x.level >= 100 && SynthesisTarget(x) != null))
            {
                var synthesis = UiKit.Button(row, "일괄 합성", () => { int made = SynthesizeAll(category); RefreshPage(); Toast(UiNumber.Format(made) + "개 합성했습니다."); }, UiKit.Purple, 68);
                synthesis.interactable = !collectionBulkRunning;
                Notify(synthesis.transform,()=>!collectionBulkRunning&&Items(category).Exists(CanSynthesize));
                CollectionButtonText(synthesis, 28);
            }
            var upgrade = UiKit.Button(row, "일괄강화", () => StartCollectionBulk(category), UiKit.Blue, 68);
            upgrade.interactable = !collectionBulkRunning;
            if (CollectionFullyMaxed(category)) {
                var refund = UiKit.Button(row, "일괄 환불", () => {
                    int paid = RefundCollection(category);
                    RefreshPage();
                    Toast(paid > 0 ? paid.ToString("N0") + " 다이아 환불 완료" : "환불 가능한 조각 또는 지갑 공간이 없습니다.");
                }, UiKit.Purple, 68);
                refund.interactable = !collectionBulkRunning && CollectionRefundQuote(category) > 0;
                Notify(refund.transform, () => !collectionBulkRunning && CollectionRefundQuote(category) > 0);
                CollectionButtonText(refund, 28);
            }
            var auto = UiKit.Button(row, "자동장착", () => { AutoEquip(category); Save(); RefreshPage(); Toast((category == "Skill" || category == "Companion" ? "높은 등급의 " : "강한 ") + CategoryName(category) + "부터 장착했습니다."); }, IsEquipmentCategory(category) ? UiKit.Green : UiKit.Yellow, 68);
            Notify(upgrade.transform,()=>!collectionBulkRunning&&CategoryCanUpgrade(category));
            Notify(auto.transform,()=>CategoryCanEquip(category));
            CollectionButtonText(upgrade, 33); CollectionButtonText(auto, 33);
        }

        public void AutoEquip(string category)
        {
            GameNumber before = PowerAmount;
            var owned = Items(category).FindAll(x => x.discovered);
            owned.Sort((a, b) => { int score = CompareEquipPriority(b, a); return score != 0 ? score : string.CompareOrdinal(a.id, b.id); });
            foreach (var item in Items(category)) item.equipped = false;
            for (int i = 0; i < Math.Min(EquipLimit(category), owned.Count); i++) { owned[i].equipped = true; owned[i].slot = i; }
            if(owned.Count>0)RecordMissionAction("equip:"+category);
            NotifyPowerChanged(before, "자동 장착");
        }

        bool UpgradeSelected(UiItem item, bool detail, bool showMessage = true)
        {
            if (!UpgradeItem(item)) { if (showMessage) Toast(item.discovered ? "강화 수량이 부족하거나 최대 단계입니다." : "아직 획득하지 않았습니다."); return false; }
            Save();
            if (detail) CloseDetail();
            RefreshPage();
            if (detail) ShowCollectionDetail(item);
            if (showMessage) Toast(item.name + " 강화 완료 · Lv. " + item.level.ToString("N0"));
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
                float descriptionHeight = Mathf.Max(42, Mathf.CeilToInt(item.description.Length / 17f) * 23);
                var description = UiKit.Box(effects, "Effects", UiKit.Paper, descriptionHeight);
                description.GetComponent<Outline>().enabled = false;
                var descriptionText = UiKit.Text(description, item.description, 19, TextAnchor.MiddleLeft, descriptionHeight);
                UiKit.Stretch(descriptionText.rectTransform, 7, 3, 7, 3);
                var owned = CollectionBox(body, "Ownership effect", new Color(.965f, .943f, .874f));
                owned.GetComponent<Outline>().enabled = false;
                owned.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 3, 3);
                var ownedHeading = UiKit.Row(owned, "Ownership effect label", 26, 0);
                ownedHeading.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                CollectionLabelPill(ownedHeading, "보유 효과", new Color(.79f, .95f, .69f), 106, 26, 22);
                var ownedValue = UiKit.Box(owned, "Ownership value", UiKit.Paper, 26);
                ownedValue.GetComponent<Outline>().enabled = false;
                var ownedText = UiKit.Text(ownedValue, EffectName(item.effect) + " +" + UiNumber.Format(ItemOwnedAmount(item)) + "%", 22, TextAnchor.MiddleLeft, 26);
                UiKit.Stretch(ownedText.rectTransform, 7, 1, 7, 1);
                if (item.category == "Skill" || item.category == "Companion")
                {
                    float interval = ItemAttackInterval(item);
                    var measures = UiKit.Row(body, "Skill measures", 60, 10);
                    var hit = CollectionBox(measures, "Skill potency", UiKit.Paper);
                    hit.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
                    UiKit.Text(hit, "1타 피해 (일반)", 17, TextAnchor.MiddleLeft, 22);
                    UiKit.Text(hit, UiNumber.Format(ItemHitAmount(item), 2), 23, TextAnchor.MiddleLeft, 26);
                    var dps = CollectionBox(measures, "Expected DPS", UiKit.Paper);
                    dps.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
                    UiKit.Text(dps, "예상 총 DPS", 17, TextAnchor.MiddleLeft, 22);
                    UiKit.Text(dps, UiNumber.Format(ItemDpsAmount(item), 2), 23, TextAnchor.MiddleLeft, 26);
                    UiKit.Text(body, "1타 = 공격력의 " + UiNumber.Format(ItemHitPercentAmount(item), 2) + "% · 재사용 " + UiNumber.Format(interval, 2) + "초", 16, TextAnchor.MiddleCenter, 24);
                    if (item.ability == "Molotov" || item.ability == "BlueMolotov")
                        UiKit.Text(body, "화상 1타 = 공격력의 " + UiNumber.Format(DoodleAttackPower.Percent(8), 2) + "%", 16, TextAnchor.MiddleCenter, 22);
                    float splashFraction = ItemSplashFraction(item);
                    if (splashFraction > 0)
                        UiKit.Text(body, "주변 1명당 " + UiNumber.Format(splashFraction * 100) + "% · " + UiNumber.Format(ItemHitAmount(item) * splashFraction, 2) + " 피해\n직격 대상은 중복 피해 없음", 15, TextAnchor.MiddleCenter, 42);
                    string basis = item.category == "Companion" ? item.volleyCount + "발 모두 명중 · 추가 폭발 대상 제외" : DoodleAttackPower.Skill(item.ability).basis;
                    if (item.category == "Skill" && splashFraction > 0) basis += " · 추가 폭발 대상 제외";
                    UiKit.Text(body, basis + "\n현재 공격력·유물·버프 반영 / DPS는 치명타 평균 반영\n전부 명중 가정 · 이동·대상 수에 따라 실제 피해 변동", 14, TextAnchor.MiddleCenter, 60);
                    UiKit.Text(body, "장착 시에만 자동 공격", 15, TextAnchor.MiddleCenter, 22);
                }
                else UiKit.Text(body, "장착 공격력 +" + UiNumber.Format(ItemEquipAmount(item)) + "%", 23, TextAnchor.MiddleCenter, 32);
                if (!item.discovered) UiKit.Text(body, "미획득 · 효과가 적용되지 않습니다.", 18, TextAnchor.MiddleCenter, 26);
                if (item.discovered)
                {
                    var buttons = UiKit.Row(UiKit.Footer(body, "Collection detail footer", 60), "Detail actions", 56);
                    bool maximum = item.level >= ItemMaxLevel(item);
                    bool synthesis = item.level >= 100 && SynthesisTarget(item) != null;
                    bool refund = item.category == "Skill" && maximum && CollectionFullyMaxed("Skill");
                    Action refundAction = () =>
                    {
                        int paid = RefundSkill(item);
                        CloseDetail(); RefreshPage(); ShowCollectionDetail(item);
                        Toast(paid > 0 ? UiNumber.Format(paid) + " 다이아 환불 완료" : "환불 가능한 조각 또는 지갑 공간이 없습니다.");
                    };
                    var upgrade = UiKit.Button(buttons, synthesis ? "합성" : refund ? "환불\n" + SkillRefundQuote(item).ToString("N0") + " 다이아" : maximum ? "최대 레벨" : "강화", () =>
                    {
                        if (synthesis) SynthesizeFromUi(item, true);
                        else if (refund) refundAction();
                        else UpgradeSelected(item, true);
                    }, synthesis || refund ? UiKit.Purple : UiKit.Blue, 56);
                    upgrade.interactable = synthesis ? CanSynthesize(item) : refund ? CanRefundSkill(item) : CanUpgradeItem(item);
                    Notify(upgrade.transform,()=>synthesis?CanSynthesize(item):refund?CanRefundSkill(item):CanUpgradeItem(item));
                    CollectionButtonText(upgrade, 27);
                    if (synthesis && refund) {
                        var refundButton = UiKit.Button(buttons, "환불\n" + SkillRefundQuote(item).ToString("N0") + " 다이아", refundAction, UiKit.Purple, 56);
                        refundButton.interactable = CanRefundSkill(item);
                        CollectionButtonText(refundButton, 22);
                    }
                    var equip=UiKit.Button(buttons, item.equipped ? "장착 해제" : "장착", () => EquipFromUi(item, true), UiKit.Yellow, 56);
                    Notify(equip.transform,()=>CanImproveLoadout(item));CollectionButtonText(equip,27);
                }
            });
        }

        void EquipFromUi(UiItem item, bool detail)
        {
            if (!item.discovered) { Toast("아직 획득하지 않았습니다."); return; }
            GameNumber before = PowerAmount;
            if (item.equipped)
            {
                if (!detail) { Toast("장착 중인 장비입니다."); return; }
                item.equipped = false;
            }
            else
            {
                var equipped = EquippedItems(item.category);
                int limit = EquipLimit(item.category);
                if (limit == 1 && IsEquipment(item)) foreach (var previous in equipped) previous.equipped = false;
                else if (equipped.Count >= limit)
                {
                    pendingEquip = item;
                    if (detail) CloseDetail();
                    RefreshPage();
                    var scroll = pageLayer.GetComponentInChildren<ScrollRect>();
                    if (scroll) scroll.verticalNormalizedPosition = 1;
                    return;
                }
                item.equipped = true; item.slot = limit == 1 ? 0 : equipped.Count;
                RecordMissionAction("equip:"+item.category);
            }
            NormalizeEquipment(item.category);
            NotifyPowerChanged(before, item.equipped ? "장착" : "장착 해제");
            Save(); if (detail) CloseDetail(); RefreshPage();
        }

        void ReplaceEquippedSlot(UiItem previous)
        {
            var item = pendingEquip;
            if (item == null || item.category != previous.category || !previous.equipped || !item.discovered) return;
            GameNumber before = PowerAmount;
            previous.equipped = false; item.equipped = true; item.slot = previous.slot; pendingEquip = null;
            RecordMissionAction("equip:"+item.category);
            NormalizeEquipment(item.category); NotifyPowerChanged(before, "장착 교체"); Save(); RefreshPage();
        }

        void BuildRelics(RectTransform body)
        {
            var note = UiKit.Box(body, "All relics apply", new Color(.94f, .90f, .82f), 56);
            note.GetComponent<Outline>().enabled = false;
            var noteText = UiKit.Text(note, "모든 유물 효과 적용 · 강화 시 같은 유물 1개 소모\n성공 확률 50% · 실패해도 현재 단계 유지", 22, TextAnchor.MiddleCenter, 56);
            UiKit.Stretch(noteText.rectTransform, 4, 2, 4, 2);
            var totals = CollectionBox(body, "Relic effects", new Color(.89f, .985f, .85f));
            totals.GetComponent<Outline>().effectColor = new Color(.27f, .53f, .23f);
            string summary = "총 유물 효과   공격력 +" + UiNumber.Format(EffectAmount("attack", "Relic")) + "% · 체력 +" + UiNumber.Format(EffectAmount("health", "Relic")) + "%\n"
                + "골드 +" + UiNumber.Format(EffectAmount("gold", "Relic")) + "% · 회복 +" + UiNumber.Format(EffectAmount("healthRegen", "Relic")) + "% · 치명 피해 +" + UiNumber.Format(EffectAmount("critDamage", "Relic")) + "%";
            summary += "\n기본 공격 +" + UiNumber.Format(EffectAmount("basicAttack", "Relic")) + "% · 스킬 +" + UiNumber.Format(EffectAmount("skillAttack", "Relic")) + "% · 동료 +" + UiNumber.Format(EffectAmount("companionAttack", "Relic")) + "%";
            UiKit.Text(totals, summary, 21, TextAnchor.MiddleCenter, 88);
            foreach (var entry in AllRelics)
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
                string levelLabel = "Lv. " + item.level.ToString("N0");
                CollectionLabelPill(title, levelLabel, new Color(.94f, .90f, .82f), Mathf.Max(110, levelLabel.Length * 13), 32, 23);
                UiKit.Text(info, EffectName(item.effect) + " +" + UiNumber.Format(ItemOwnedAmount(item)) + "% → <color=#216B20>+" + UiNumber.Format(ItemOwnedAmount(item) + collectionTuning.relicStepPercent) + "%</color>", 24, TextAnchor.MiddleLeft, 34);
                UiKit.Text(info, "성공 확률 50%", 23, TextAnchor.MiddleLeft, 30);
                Func<bool> attempt = () => UpgradeRelicFromUi(item, false);
                string caption = !item.discovered ? "미획득" : item.level >= collectionTuning.maxItemLevel ? "최대 단계" : "강화";
                var button = UiKit.Button(row, caption + "\n" + UiNumber.Format(item.count) + " / 1개", () => UpgradeRelicFromUi(item, true), UiKit.Yellow, 94);
                CollectionWidth(button.transform, 146);
                button.interactable = item.discovered && item.count > 0 && item.level < collectionTuning.maxItemLevel && !collectionBulkRunning;
                UiKit.Repeat(button, "relic:" + item.id, attempt);
                Notify(button.transform,()=>!collectionBulkRunning&&CanUpgradeItem(item));
            }
            var footer = UiKit.Footer(body, "Relic footer", 72);
            var bulk = UiKit.Button(footer, "일괄강화", () => StartCollectionBulk("Relic"), UiKit.Blue, 68);
            bulk.interactable = !collectionBulkRunning;
            Notify(bulk.transform,()=>!collectionBulkRunning&&AllRelics.Exists(CanUpgradeItem));
            CollectionButtonText(bulk, 34);
        }

        bool UpgradeRelicFromUi(UiItem item, bool showMessage)
        {
            if (collectionBulkRunning) return false;
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
            GameNumber before = PowerAmount;
            long attempts = 0, successes = 0;
            long deadline = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency / 250;
            int frameRolls = 0;
            try
            {
                if (category == "Relic" && ActivePage == "Relics")
                    foreach (var button in pageLayer.GetComponentsInChildren<Button>())
                        if (button.name == "일괄강화" || button.name.StartsWith("강화", StringComparison.Ordinal)) button.interactable = false;
                foreach (var item in category=="Relic" ? AllRelics : Items(category))
                {
                    if (category != "Relic") {
                        int upgraded = UpgradeItemBatch(item);
                        attempts += upgraded; successes += upgraded;
                        continue;
                    }
                    while (item.discovered && item.count > 0 && item.level < collectionTuning.maxItemLevel)
                    {
                        int rolls = Math.Min(8192, item.count), used = 0, won = 0;
                        // Preserve the exact per-copy random sequence, failure cost and cap stopping point.
                        while (used < rolls && item.level < collectionTuning.maxItemLevel) {
                            used++;
                            if (collectionRandom.NextDouble() < .5) { item.level++; won++; }
                        }
                        item.count -= used;
                        RecordMissionAction("relicAttempt", used);
                        RecordServiceProgress("relicUpgrade", won);
                        attempts += used; successes += won; frameRolls += used;
                        if (frameRolls < 262144 && System.Diagnostics.Stopwatch.GetTimestamp() < deadline) continue;
                        yield return null;
                        deadline = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency / 250;
                        frameRolls = 0;
                    }
                }
            }
            finally
            {
                collectionBulkRunning = false;
                if (attempts > 0) Save();
                if (successes > 0) NotifyPowerChanged(before, "일괄 강화");
            }
            RefreshCollectionBulkPage(category);
            Toast(attempts == 0 ? "강화 가능한 수량이 없습니다." : category == "Relic"
                ? UiNumber.Format(attempts) + "개 소모 · " + UiNumber.Format(successes) + "회 성공"
                : UiNumber.Format(successes) + "회 강화했습니다.");
        }

        void RefreshCollectionBulkPage(string category)
        {
            string page = IsEquipmentCategory(category) ? "Equipment" : category == "Skill" ? "Skills" : category == "Companion" ? "Companions" : "Relics";
            if (ActivePage == page) RefreshPage();
        }

        public float RelicSuccessChance(UiItem item) => .5f;
        // The upgrade cost is a count of this relic, never a gold price.
        public long RelicUpgradeCost(UiItem item) => 1;
        public bool TryUpgradeRelic(UiItem item, out bool success, bool notifyPower = true)
        {
            success = false;
            if (item == null || item.category != "Relic" || !item.discovered || item.count < 1 || item.level >= collectionTuning.maxItemLevel) return false;
            GameNumber before = notifyPower ? PowerAmount : 0;
            item.count--;
            RecordMissionAction("relicAttempt");
            success = collectionRandom.NextDouble() < .5;
            if (success) { item.level++; RecordServiceProgress("relicUpgrade", 1); }
            if (notifyPower) NotifyPowerChanged(before, "유물 강화");
            return true;
        }

        static string EffectName(string effect)
        {
            switch (effect) { case "basicAttack": return "기본 공격력"; case "skillAttack": return "스킬 공격력"; case "companionAttack": return "동료 공격력"; case "health": return "체력"; case "healthRegen": return "체력 회복"; case "critDamage": return "치명타 피해"; case "crit2Chance": return "2배 치명타 확률"; case "crit4Chance": return "4배 치명타 확률"; case "gold": return "골드 획득"; default: return "공격력"; }
        }
        static string CategoryName(string category)
        {
            switch (category) { case "Armor": return "갑옷"; case "Club": return "몽둥이"; case "Necklace": return "목걸이"; case "Skill": return "스킬"; case "Companion": return "동료"; default: return "유물"; }
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
            bool equipment = DoodleUi.IsEquipmentCategory(category);
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
            Columns(inventory.GetComponent<GridLayoutGroup>(), category == "Skill" ? 5 : 6);
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
            if (card.Find("Slot unlock padlock")) return;
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
