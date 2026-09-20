using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        [Serializable]
        public sealed class CommerceTuning
        {
            public int freeCount = 5;
            public int tenCost = 100;
            public int fiftyCost = 450;
            public int[] levelExperience = { 50, 100, 200, 400, 800, 1000 };
            public CurrencyProduct[] products = {
                new CurrencyProduct { amount = 100, priceWon = 1100 },
                new CurrencyProduct { amount = 550, priceWon = 5500 },
                new CurrencyProduct { amount = 1200, priceWon = 11000 },
                new CurrencyProduct { amount = 3000, priceWon = 27500 },
                new CurrencyProduct { amount = 6500, priceWon = 55000 }
            };
        }

        [Serializable]
        public sealed class CurrencyProduct { public int amount, priceWon; }

        [Serializable]
        public sealed class SummonState
        {
            public string category;
            public int level = 1, experience;
            public string freeUsedDay = "";
        }

        readonly string[] commerceCategories = { "Armor", "Club", "Skill", "Companion", "Relic" };
        readonly string[] commerceLabels = { "갑옷", "몽둥이", "스킬", "동료", "유물" };
        readonly string[] commerceGrades = { "일반", "고급", "희귀", "영웅", "전설" };
        readonly Dictionary<string, SummonState> summonStates = new Dictionary<string, SummonState>();
        readonly System.Random commerceRandom = new System.Random();
        CommerceTuning commerceTuning = new CommerceTuning();
        int shopTab;

        void InitCommerce()
        {
            var tuning = Resources.Load<TextAsset>("DoodleIdle/UiCommerce");
            if (tuning != null)
            {
                try { JsonUtility.FromJsonOverwrite(tuning.text, commerceTuning); }
                catch (ArgumentException) { Debug.LogWarning("Invalid UI commerce tuning; using defaults."); }
            }
            commerceTuning.freeCount = Mathf.Clamp(commerceTuning.freeCount, 1, 50);
            commerceTuning.tenCost = Mathf.Max(1, commerceTuning.tenCost);
            commerceTuning.fiftyCost = Mathf.Max(1, commerceTuning.fiftyCost);
            if (commerceTuning.levelExperience == null || commerceTuning.levelExperience.Length == 0)
                commerceTuning.levelExperience = new[] { 50, 100, 200, 400, 800, 1000 };
            for (int i = 0; i < commerceTuning.levelExperience.Length; i++)
                commerceTuning.levelExperience[i] = Mathf.Max(1, commerceTuning.levelExperience[i]);
            if (commerceTuning.products == null) commerceTuning.products = new CurrencyProduct[0];
            foreach (string category in commerceCategories)
            {
                var state = new SummonState { category = category };
                string json = PlayerPrefs.GetString("DoodleUi.Commerce." + category, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try { JsonUtility.FromJsonOverwrite(json, state); }
                    catch (ArgumentException) { Debug.LogWarning("Reset invalid local summon progress: " + category); }
                }
                state.category = category;
                state.level = Mathf.Clamp(state.level, 1, 100000);
                state.experience = Mathf.Clamp(state.experience, 0, CommerceExperienceNeeded(state) - 1);
                summonStates[category] = state;
            }
        }

        void SaveCommerce()
        {
            foreach (var state in summonStates.Values)
                PlayerPrefs.SetString("DoodleUi.Commerce." + state.category, JsonUtility.ToJson(state));
        }

        static string CommerceDay() { return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        int CommerceExperienceNeeded(SummonState state)
        {
            return commerceTuning.levelExperience[Mathf.Min(state.level - 1, commerceTuning.levelExperience.Length - 1)];
        }

        string CommerceLabel(string category)
        {
            int index = Array.IndexOf(commerceCategories, category);
            return index >= 0 ? commerceLabels[index] : category;
        }

        public int SummonLevel(string category) { return summonStates[category].level; }
        public int SummonExperience(string category) { return summonStates[category].experience; }
        public bool CanFreeSummon(string category) { return summonStates[category].freeUsedDay != CommerceDay(); }

        void BuildShop(RectTransform body)
        {
            if (summonStates.Count == 0) InitCommerce();
            body.GetComponent<VerticalLayoutGroup>().spacing = 8;
            var window = body.GetComponentInParent<DoodleUiWindow>();
            if (window) { window.maxWidth = 570; window.maxHeight = shopTab == 0 ? 1010 : 880; }
            var tabs = UiKit.Row(body, "ShopTabs", 64, 2);
            CommerceButtonText(UiKit.Button(tabs, "뽑기", () => { shopTab = 0; RefreshPage(); }, shopTab == 0 ? UiKit.Yellow : UiKit.Paper, 64), 35);
            CommerceButtonText(UiKit.Button(tabs, "재화", () => { shopTab = 1; RefreshPage(); }, shopTab == 1 ? UiKit.Blue : UiKit.Paper, 64), 35);
            if (window)
            {
                tabs.SetParent(window.inner, false);
                var responsive = window.inner.gameObject.AddComponent<DoodleCommerceLayout>();
                responsive.window = window; responsive.tabs = tabs;
                responsive.Reflow();
            }
            if (shopTab == 1) { BuildCurrencyProducts(body); return; }
            foreach (string category in commerceCategories) BuildSummonRow(body, category);
            UiKit.Text(body, "무료 뽑기는 종류별 하루 1회 · 매일 UTC 00:00 초기화", 17, TextAnchor.MiddleCenter, 40);
        }

        RectTransform CommerceFramedRow(Transform parent, string name, float height)
        {
            var row = UiKit.Box(parent, name, UiKit.Paper, height);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        void BuildSummonRow(RectTransform body, string category)
        {
            var state = summonStates[category];
            var row = CommerceFramedRow(body, "Summon_" + category, 176);
            UiKit.Icon(row, category, 142);
            var content = UiKit.Column(row, "SummonInformation", 7, 0);
            UiKit.Flexible(content);
            var title = UiKit.Row(content, "SummonTitle", 40, 5);
            UiKit.Text(title, "Lv. " + state.level + " " + CommerceLabel(category) + " 뽑기", 32, TextAnchor.MiddleLeft, 40);
            var info = UiKit.Button(title, "i", () => ShowSummonProbabilities(category), UiKit.Blue, 36);
            var size = info.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 36;
            size.flexibleWidth = 0;
            int needed = CommerceExperienceNeeded(state);
            UiKit.Gauge(content, state.experience + "/" + needed, (float)state.experience / needed, 28).GetComponentInChildren<Text>().resizeTextMaxSize = 23;
            BuildSummonButtons(content, category);
        }

        void BuildSummonButtons(Transform parent, string category)
        {
            var actions = UiKit.Row(parent, "SummonActions", 68, 5);
            var free = UiKit.Button(actions, "무료 " + commerceTuning.freeCount + "회\n뽑기", () => TrySummon(category, commerceTuning.freeCount, true), UiKit.Green, 68);
            CommerceButtonText(free, 24);
            free.interactable = CanFreeSummon(category);
            PaidSummonButton(actions, category, 10, commerceTuning.tenCost, UiKit.Blue);
            PaidSummonButton(actions, category, 50, commerceTuning.fiftyCost, UiKit.Yellow);
        }

        void PaidSummonButton(Transform parent, string category, int count, int cost, Color color)
        {
            var column = UiKit.Column(parent, "PaidSummon" + count, 1, 0);
            UiKit.Flexible(column);
            CommerceButtonText(UiKit.Button(column, count + "회 뽑기", () => TrySummon(category, count, false), color, 42), 24);
            var price = UiKit.Row(column, "DiamondCost", 24, 2);
            UiKit.Icon(price, "Diamond", 22);
            UiKit.Text(price, cost.ToString("N0"), 17, TextAnchor.MiddleCenter, 24);
        }

        static void CommerceButtonText(Button button, int size)
        {
            var label = button.GetComponentInChildren<Text>();
            label.fontSize = size; label.resizeTextMaxSize = size;
        }

        // The local wallet and collection are changed together before opening results.
        // No store SDK, payment provider, or remote entitlement is simulated here.
        public bool TrySummon(string category, int count, bool free)
        {
            if (!summonStates.ContainsKey(category)) return false;
            if (free && count != commerceTuning.freeCount) return false;
            if (!free && count != 10 && count != 50) return false;
            if (free && !CanFreeSummon(category)) { Toast("오늘 무료 뽑기를 모두 사용했어요."); return false; }
            int cost = free ? 0 : count == 10 ? commerceTuning.tenCost : commerceTuning.fiftyCost;
            if (Diamonds < cost) { Toast("다이아가 부족해요."); return false; }
            var rewards = new List<UiItem>(count);
            for (int i = 0; i < count; i++)
            {
                var item = GrantItem(category, commerceRandom);
                if (item == null) { Toast("뽑기 아이템 데이터를 확인해 주세요."); return false; }
                rewards.Add(item);
            }
            Diamonds -= cost;
            var state = summonStates[category];
            if (free) state.freeUsedDay = CommerceDay();
            foreach (var item in rewards) AddItem(item, 1);
            state.experience += count;
            while (state.experience >= CommerceExperienceNeeded(state))
            {
                state.experience -= CommerceExperienceNeeded(state);
                state.level++;
            }
            RecordServiceProgress("summon", count);
            Save();
            RefreshPage();
            ShowSummonResults(category, rewards);
            return true;
        }

        void ShowSummonResults(string category, List<UiItem> rewards)
        {
            ShowFullscreen("뽑기 결과", body =>
            {
                var subtitle = UiKit.Text(body, CommerceLabel(category) + " " + rewards.Count + "회 뽑기", 38, TextAnchor.MiddleCenter, 58);
                var grid = UiKit.Grid(body, "SummonResultCards", 5, 150);
                for (int i = 0; i < rewards.Count; i++)
                {
                    var item = rewards[i];
                    var slot = UiKit.Slot(grid, item.name, item.icon, item.rarity, item.count, CopiesNeeded(item), item.equipped, false,
                        () => ShowSummonItem(item), 150);
                    slot.gameObject.name = "SummonResult_" + i + "_" + item.id;
                }
                var footer = UiKit.Footer(body, "Summon result footer", 228);
                var state = summonStates[category];
                var summary = UiKit.Row(footer, "Summon progress summary", 46, 10);
                var summaryIcon = UiKit.Icon(summary, category, 46);
                var level = UiKit.Text(summary, CommerceLabel(category) + " 뽑기 Lv. " + state.level, 36, TextAnchor.MiddleCenter, 46);
                int needed = CommerceExperienceNeeded(state);
                var gauge = UiKit.Gauge(footer, state.experience + "/" + needed, (float)state.experience / needed, 32);
                BuildSummonButtons(footer, category);
                var confirmRow = UiKit.Row(footer, "Summon confirmation", 58);
                var confirm = UiKit.Button(confirmRow, "확인", () => { CloseFullscreen(); RefreshPage(); }, UiKit.Yellow, 58);
                var window = body.GetComponentInParent<DoodleUiWindow>();
                if (window)
                {
                    var crest = UiKit.Icon(window.inner, "Player", 108);
                    crest.name = "Result player crest";
                    var responsive = window.inner.gameObject.AddComponent<DoodleCommerceLayout>();
                    responsive.window = window; responsive.resultGrid = grid.GetComponent<GridLayoutGroup>();
                    responsive.resultCount = rewards.Count; responsive.subtitle = subtitle; responsive.crest = crest.rectTransform;
                    responsive.summary = summary; responsive.summaryIcon = summaryIcon.rectTransform; responsive.level = level;
                    responsive.gauge = gauge; responsive.actions = footer.Find("SummonActions") as RectTransform;
                    responsive.confirm = confirm; responsive.confirmRow = confirmRow;
                    responsive.Reflow();
                }
            });
        }

        void ShowSummonItem(UiItem item)
        {
            ShowDetail(item.name, body =>
            {
                UiKit.Slot(body, item.name, item.icon, item.rarity, item.count, CopiesNeeded(item), item.equipped, false, null, 170);
                UiKit.Text(body, "현재 보유 수량 " + item.count + " · Lv. " + item.level, 24, TextAnchor.MiddleCenter, 46);
                UiKit.Text(body, "획득한 아이템은 " + CommerceLabel(item.category) + " 목록에서 확인할 수 있어요.", 20, TextAnchor.MiddleCenter, 60);
                UiKit.Button(body, "확인", CloseDetail, UiKit.Yellow);
            });
        }

        public void ShowSummonProbabilities(string category)
        {
            if (!summonStates.ContainsKey(category)) return;
            ShowDetail("뽑기 확률", body =>
            {
                var window = body.GetComponentInParent<DoodleUiWindow>();
                if (window) { window.maxWidth = 570; window.maxHeight = 1110; }
                body.GetComponent<VerticalLayoutGroup>().spacing = 5;
                var heading = UiKit.Box(body, "Summon probability heading", new Color(.96f, .96f, .94f), 62);
                var headingText = UiKit.Text(heading, "Lv. " + summonStates[category].level + " " + CommerceLabel(category) + " 뽑기", 35, TextAnchor.MiddleCenter, 58);
                UiKit.Stretch(headingText.rectTransform, 5, 2, 5, 2);
                UiKit.Text(body, "1회 뽑기 기준 · 모든 회차 독립 추첨", 24, TextAnchor.MiddleCenter, 34);
                UiKit.Text(body, "등급별 확률", 29, TextAnchor.MiddleLeft, 39);
                var grades = UiKit.Row(body, "GradeProbabilities", 86, 5);
                for (int rarity = 0; rarity < 5; rarity++)
                {
                    int grade = rarity;
                    var card = UiKit.Box(grades, "Grade" + rarity, UiKit.Rarity(rarity), 86);
                    UiKit.Flexible(card);
                    var label = UiKit.Text(card, commerceGrades[grade] + "\n" + GradeProbability(category, grade).ToString("0.####", CultureInfo.InvariantCulture) + "%", 29, TextAnchor.MiddleCenter, 86);
                    var rect = label.rectTransform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(2, 2); rect.offsetMax = new Vector2(-2, -2);
                }
                UiKit.Text(body, "아이템별 확률", 29, TextAnchor.MiddleLeft, 44);
                var all = Items(category);
                for (int rarity = 0; rarity < 5; rarity++)
                {
                    var group = all.FindAll(item => item.rarity == rarity);
                    int grade = rarity;
                    var groupBody = UiKit.Column(body, "Probability grade group " + grade, 0, 0);
                    var header = UiKit.Box(groupBody, "Probability grade header " + grade, UiKit.Rarity(grade), 44);
                    var gradeText = UiKit.Text(header, commerceGrades[grade] + " · 합계 " + GradeProbability(category, grade).ToString("0.####", CultureInfo.InvariantCulture) + "%", 29, TextAnchor.MiddleCenter, 44);
                    UiKit.Stretch(gradeText.rectTransform, 4, 0, 4, 0);
                    foreach (var item in group)
                    {
                        var row = CommerceFramedRow(groupBody, "Probability_" + item.id, 64);
                        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 5, 5);
                        UiKit.Icon(row, item.icon, 50);
                        UiKit.Text(row, item.name, 28, TextAnchor.MiddleLeft, 48);
                        var rate = UiKit.Text(row, CommerceExactRate(category, grade, group.Count), 26, TextAnchor.MiddleRight, 48);
                        var rateSize = rate.GetComponent<LayoutElement>();
                        rateSize.minWidth = rateSize.preferredWidth = 112; rateSize.flexibleWidth = 0;
                    }
                }
                UiKit.Text(body, "등급 확률 합계 100% · 동일 등급 안에서 균등 추첨\n나눗셈 표기는 반올림하지 않은 정확한 확률입니다.", 19, TextAnchor.MiddleCenter, 60);
                var footer = UiKit.Footer(body, "Probability confirmation footer", 64);
                CommerceButtonText(UiKit.Button(footer, "확인", CloseDetail, UiKit.Yellow, 64), 34);
            });
        }

        string CommerceExactRate(string category, int rarity, int itemCount)
        {
            if (itemCount <= 0) return "0%";
            decimal grade = (decimal)GradeProbability(category, rarity);
            decimal rate = grade / itemCount;
            if (decimal.Round(rate, 4) * itemCount == grade)
                return rate.ToString("0.####", CultureInfo.InvariantCulture) + "%";
            return grade.ToString("0.####", CultureInfo.InvariantCulture) + "% ÷ " + itemCount;
        }

        void BuildCurrencyProducts(RectTransform body)
        {
            UiKit.Text(body, "결제 서비스 미연결 · 구매할 수 없습니다", 19, TextAnchor.MiddleCenter, 30);
            foreach (var product in commerceTuning.products)
            {
                if (product == null || product.amount <= 0 || product.priceWon <= 0) continue;
                var row = CommerceFramedRow(body, "CurrencyProduct" + product.amount, 145);
                var art = UiKit.Column(row, "ProductArt", 0, 0);
                UiKit.Flexible(art);
                var artLayout = art.GetComponent<VerticalLayoutGroup>();
                artLayout.childAlignment = TextAnchor.MiddleCenter;
                UiKit.Icon(art, "Diamond", 88);
                UiKit.Text(art, product.amount.ToString("N0"), 32, TextAnchor.MiddleCenter, 34);
                var purchase = UiKit.Column(row, "ProductPrice", 7, 0);
                UiKit.Flexible(purchase);
                CommerceButtonText(UiKit.Button(purchase, "₩" + product.priceWon.ToString("N0"), () => Toast("결제 서비스가 연결되지 않아 구매할 수 없어요."), UiKit.Blue, 72), 35);
                UiKit.Text(purchase, "구매 불가 · 표시용 가격", 17, TextAnchor.MiddleCenter, 28);
            }
        }
    }

    /// <summary>Reference-proportioned commerce layouts; tall screens expand the content, not empty gaps.</summary>
    public sealed class DoodleCommerceLayout : MonoBehaviour
    {
        public DoodleUiWindow window;
        public RectTransform tabs, crest, summary, summaryIcon, gauge, actions, confirmRow;
        public GridLayoutGroup resultGrid;
        public Text subtitle, level;
        public Button confirm;
        public int resultCount;
        bool reflowing;

        void LateUpdate() { Reflow(); }
        void OnRectTransformDimensionsChange() { Reflow(); }

        public void Reflow()
        {
            if (reflowing || !window || !window.inner || !window.content) return;
            reflowing = true;
            try
            {
                var viewport = window.content.parent as RectTransform;
                var rail = window.inner.Find("Scroll position") as RectTransform;
                if (tabs)
                {
                    tabs.anchorMin = tabs.anchorMax = new Vector2(.5f, 1);
                    tabs.pivot = new Vector2(.5f, 1);
                    tabs.anchoredPosition = new Vector2(0, -window.headerHeight);
                    tabs.sizeDelta = new Vector2(window.content.sizeDelta.x, 64);
                    float top = window.headerHeight + 76;
                    if (viewport) viewport.offsetMax = new Vector2(viewport.offsetMax.x, -top);
                    if (rail) rail.offsetMax = new Vector2(rail.offsetMax.x, -top - 2);
                    return;
                }
                if (!resultGrid || !window.footer || !viewport) return;
                // Reference 10 is 720 x 1520 after normalization: cards are ~225 high,
                // result banner sits near y=240, and the actions finish ~100 above the bottom.
                float tall = Mathf.Clamp01((window.inner.rect.height - 720) / 800);
                float header = Mathf.Lerp(Mathf.Max(82, window.headerHeight), 335, tall);
                float footerGap = Mathf.Lerp(8, 26, tall);
                float summaryHeight = Mathf.Lerp(46, 124, tall);
                float gaugeHeight = Mathf.Lerp(32, 36, tall);
                float actionHeight = Mathf.Lerp(68, 106, tall);
                float confirmHeight = Mathf.Lerp(58, 88, tall);
                float footerHeight = summaryHeight + gaugeHeight + actionHeight + confirmHeight + footerGap * 3;
                float bottom = Mathf.Lerp(12, 100, tall);
                window.footer.sizeDelta = new Vector2(window.footer.sizeDelta.x, footerHeight);
                window.footer.anchoredPosition = new Vector2(0, bottom);
                window.footer.GetComponent<VerticalLayoutGroup>().spacing = footerGap;
                viewport.offsetMin = new Vector2(viewport.offsetMin.x, footerHeight + bottom + 12);
                viewport.offsetMax = new Vector2(viewport.offsetMax.x, -header);
                if (rail)
                {
                    rail.offsetMin = new Vector2(rail.offsetMin.x, footerHeight + bottom + 14);
                    rail.offsetMax = new Vector2(rail.offsetMax.x, -header - 2);
                }
                UiKit.Height(summary, summaryHeight); UiKit.Height(level.transform, summaryHeight);
                SetIconSize(summaryIcon, Mathf.Lerp(46, 108, tall));
                SetTextSize(level, Mathf.RoundToInt(Mathf.Lerp(30, 40, tall)));
                UiKit.Height(gauge, gaugeHeight);
                SetTextSize(gauge.GetComponentInChildren<Text>(), Mathf.RoundToInt(Mathf.Lerp(23, 29, tall)));
                UiKit.Height(actions, actionHeight);
                foreach (var button in actions.GetComponentsInChildren<Button>())
                {
                    bool free = button.transform.parent == actions;
                    UiKit.Height(button.transform, free ? actionHeight : actionHeight - 26);
                    SetTextSize(button.GetComponentInChildren<Text>(), Mathf.RoundToInt(Mathf.Lerp(25, 34, tall)));
                }
                UiKit.Height(confirmRow, confirmHeight); UiKit.Height(confirm.transform, confirmHeight);
                var confirmSize = confirm.GetComponent<LayoutElement>();
                confirmSize.minWidth = confirmSize.preferredWidth = Mathf.Lerp(window.footer.rect.width, Mathf.Min(440, window.footer.rect.width), tall);
                confirmSize.flexibleWidth = 0;
                SetTextSize(confirm.GetComponentInChildren<Text>(), Mathf.RoundToInt(Mathf.Lerp(29, 43, tall)));
                SetTextSize(subtitle, Mathf.RoundToInt(Mathf.Lerp(32, 40, tall)));

                float cellHeight = Mathf.Lerp(150, 225, tall);
                resultGrid.spacing = new Vector2(10, Mathf.Lerp(8, 16, tall));
                float gridWidth = ((RectTransform)resultGrid.transform).rect.width;
                float cellWidth = (gridWidth - resultGrid.padding.horizontal - resultGrid.spacing.x * 4) / 5;
                resultGrid.cellSize = new Vector2(Mathf.Max(1, cellWidth), cellHeight);
                foreach (RectTransform card in resultGrid.transform)
                {
                    UiKit.Height(card, cellHeight);
                    foreach (Transform child in card)
                    {
                        var rect = child as RectTransform;
                        if (!rect) continue;
                        if (child.name == "Quantity gauge")
                        {
                            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0);
                            rect.offsetMin = new Vector2(5, 5); rect.offsetMax = new Vector2(-5, 32);
                        }
                        else if (child.name.StartsWith("Icon: ")) UiKit.Stretch(rect, 12, 38, 12, 32);
                        else if (child.GetComponent<Text>())
                        {
                            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
                            rect.offsetMin = new Vector2(5, -31); rect.offsetMax = new Vector2(-5, -3);
                        }
                    }
                }
                int rows = Mathf.CeilToInt(resultCount / 5f);
                float occupied = 58 + 10 + rows * cellHeight + Mathf.Max(0, rows - 1) * resultGrid.spacing.y + 8;
                var bodyLayout = window.content.GetComponent<VerticalLayoutGroup>();
                int topPadding = 4 + Mathf.RoundToInt(Mathf.Max(0, viewport.rect.height - occupied) * .5f);
                if (bodyLayout.padding.top != topPadding)
                    bodyLayout.padding = new RectOffset(bodyLayout.padding.left, bodyLayout.padding.right, topPadding, bodyLayout.padding.bottom);

                if (crest)
                {
                    crest.gameObject.SetActive(tall > .12f);
                    PlaceTop(crest, Mathf.Lerp(40, 111, tall), Vector2.one * Mathf.Lerp(58, 108, tall));
                }
                var banner = window.inner.Find("Golden result banner") as RectTransform;
                float bannerCenter = Mathf.Lerp(43, 239, tall), bannerHeight = Mathf.Lerp(62, 132, tall);
                if (banner) PlaceTop(banner, bannerCenter, new Vector2(Mathf.Min(window.content.rect.width * .8f, 520), bannerHeight));
                foreach (Transform child in window.inner)
                {
                    var title = child.GetComponent<Text>();
                    if (title && title.text == "뽑기 결과")
                    {
                        PlaceTop(title.rectTransform, bannerCenter, new Vector2(Mathf.Min(window.content.rect.width * .78f, 510), bannerHeight - 8));
                        SetTextSize(title, Mathf.RoundToInt(Mathf.Lerp(42, 72, tall)));
                    }
                }
            }
            finally { reflowing = false; }
        }

        static void SetTextSize(Text label, int size) { if (!label) return; label.fontSize = label.resizeTextMaxSize = size; }
        static void SetIconSize(RectTransform icon, float size)
        {
            var element = icon.GetComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = element.minHeight = element.preferredHeight = size;
        }
        static void PlaceTop(RectTransform rect, float center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = new Vector2(0, -center); rect.sizeDelta = size;
        }
    }
}
