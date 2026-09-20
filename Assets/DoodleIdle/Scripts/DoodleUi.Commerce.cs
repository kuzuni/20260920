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
            var window = body.GetComponentInParent<DoodleUiWindow>();
            body.GetComponent<VerticalLayoutGroup>().spacing = 8;
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
            var row = CommerceFramedRow(body, "Summon_" + category, category == "Relic" ? 224 : 176);
            UiKit.Icon(row, category, 142);
            var content = UiKit.Column(row, "SummonInformation", 7, 0);
            UiKit.Flexible(content);
            var title = UiKit.Row(content, "SummonTitle", 40, 5);
            UiKit.Text(title, "Lv. " + UiNumber.Format(state.level) + " " + CommerceLabel(category) + " 뽑기", 32, TextAnchor.MiddleLeft, 40);
            var info = UiKit.Button(title, "i", () => ShowSummonProbabilities(category), UiKit.Blue, 36);
            var size = info.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 36;
            size.flexibleWidth = 0;
            int needed = CommerceExperienceNeeded(state);
            UiKit.Gauge(content, UiNumber.Format(state.experience) + "/" + UiNumber.Format(needed), (float)state.experience / needed, 28).GetComponentInChildren<Text>().resizeTextMaxSize = 23;
            BuildSummonButtons(content, category);
            if (category == "Relic")
            {
                var tickets = UiKit.Row(content, "Relic ticket actions", 48, 6);
                UiKit.Text(tickets, "유물 뽑기권 " + UiNumber.Format(RelicTickets) + "장", 21, TextAnchor.MiddleLeft, 48);
                var use = UiKit.Button(tickets, "1장 뽑기", () => TrySummonRelicTicket(), UiKit.Blue, 48);
                use.name = "RelicTicketSummon";
                var width = use.GetComponent<LayoutElement>();
                width.minWidth = width.preferredWidth = 104; width.flexibleWidth = 0;
                CommerceButtonText(use, 22);
                use.interactable = RelicTickets > 0;
            }
        }

        void BuildSummonButtons(Transform parent, string category, bool result = false)
        {
            var actions = UiKit.Row(parent, "SummonActions", 68, 5);
            var free = UiKit.Button(actions, "무료 " + commerceTuning.freeCount + "회\n뽑기", () => TrySummon(category, commerceTuning.freeCount, true), result ? UiKit.Blue : UiKit.Green, 68);
            CommerceButtonText(free, 24);
            free.interactable = CanFreeSummon(category);
            PaidSummonButton(actions, category, 10, commerceTuning.tenCost, result ? UiKit.Yellow : UiKit.Blue);
            PaidSummonButton(actions, category, 50, commerceTuning.fiftyCost, result ? UiKit.Green : UiKit.Yellow);
        }

        void PaidSummonButton(Transform parent, string category, int count, int cost, Color color)
        {
            var column = UiKit.Column(parent, "PaidSummon" + count, 0, 0);
            UiKit.Flexible(column);
            var button = UiKit.Button(column, count + "회 뽑기", () => TrySummon(category, count, false), color, 68);
            CommerceButtonText(button, 24);
            var label = button.GetComponentInChildren<Text>();
            label.rectTransform.anchorMin = new Vector2(0, .44f); label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(3, 0); label.rectTransform.offsetMax = new Vector2(-3, -3);
            var price = UiKit.Row(button.transform, "DiamondCost", 24, 4);
            price.anchorMin = new Vector2(0, 0); price.anchorMax = new Vector2(1, .44f);
            price.offsetMin = new Vector2(4, 3); price.offsetMax = new Vector2(-4, 0);
            UiKit.Icon(price, "Diamond", 20);
            var priceText = UiKit.Text(price, UiNumber.Format(cost), 20, TextAnchor.MiddleCenter, 24);
            var priceSize = priceText.GetComponent<LayoutElement>();
            priceSize.minWidth = priceSize.preferredWidth = cost >= 1000 ? 42 : 34; priceSize.flexibleWidth = 0;
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
            CompleteSummon(category, rewards);
            return true;
        }

        public bool TrySummonRelicTicket()
        {
            if (summonStates.Count == 0) InitCommerce();
            if (RelicTickets < 1) { Toast("유물 뽑기권이 부족해요."); return false; }
            var reward = GrantItem("Relic", commerceRandom);
            if (reward == null) { Toast("유물 데이터를 확인해 주세요."); return false; }
            long before = Power;
            if (!TrySpendRelicTickets(1)) return false;
            CompleteSummon("Relic", new List<UiItem> { reward });
            NotifyPowerChanged(before, "유물 뽑기권 사용");
            return true;
        }

        void CompleteSummon(string category, List<UiItem> rewards)
        {
            var state = summonStates[category];
            foreach (var item in rewards) AddItem(item, 1);
            state.experience += rewards.Count;
            while (state.experience >= CommerceExperienceNeeded(state))
            {
                state.experience -= CommerceExperienceNeeded(state);
                state.level++;
            }
            RecordServiceProgress("summon", rewards.Count);
            Save();
            RefreshPage();
            ShowSummonResults(category, rewards);
        }

        void ShowSummonResults(string category, List<UiItem> rewards)
        {
            ShowFullscreen("뽑기 결과", body =>
            {
                var subtitle = UiKit.Text(body, CommerceLabel(category) + " " + rewards.Count + "회 뽑기", 38, TextAnchor.MiddleCenter, 58);
                var grid = UiKit.Grid(body, "SummonResultCards", 5, 150);
                UiKit.PortraitGrid(grid);
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
                var summaryText = UiKit.Column(summary, "Summon progress text", 4, 0);
                var level = UiKit.Text(summaryText, CommerceLabel(category) + " 뽑기 Lv. " + UiNumber.Format(state.level), 36, TextAnchor.MiddleLeft, 46);
                int needed = CommerceExperienceNeeded(state);
                var experience = UiKit.Text(summaryText, "뽑기 경험치 " + UiNumber.Format(state.experience) + "/" + UiNumber.Format(needed), 29, TextAnchor.MiddleLeft, 34);
                var gauge = UiKit.Gauge(footer, "", (float)state.experience / needed, 32);
                BuildSummonButtons(footer, category, true);
                var confirmRow = UiKit.Row(footer, "Summon confirmation", 58);
                var confirm = UiKit.Button(confirmRow, "확인", () => { CloseFullscreen(); RefreshPage(); }, UiKit.Yellow, 58);
                var window = body.GetComponentInParent<DoodleUiWindow>();
                if (window)
                {
                    var background = window.inner.GetComponent<Image>();
                    if (background) background.color = new Color(1, .982f, .93f);
                    var paper = window.GetComponent<Image>();
                    if (paper) paper.color = new Color(1, .982f, .93f);
                    var decoration = UiKit.Rect(window.inner, "Summon celebration accents");
                    UiKit.Stretch(decoration);
                    decoration.SetAsFirstSibling();
                    decoration.gameObject.AddComponent<DoodleSummonCelebration>().raycastTarget = false;
                    var banner = window.inner.Find("Golden result banner") as RectTransform;
                    if (banner)
                    {
                        banner.GetComponent<Image>().enabled = false;
                        banner.GetComponent<Outline>().enabled = false;
                        var ribbonDrawing = UiKit.Rect(banner, "Ribbon drawing");
                        UiKit.Stretch(ribbonDrawing);
                        ribbonDrawing.gameObject.AddComponent<DoodleSummonRibbon>().raycastTarget = false;
                    }
                    var crest = UiKit.Icon(window.inner, "Player", 108);
                    crest.name = "Result player crest";
                    var responsive = window.inner.gameObject.AddComponent<DoodleCommerceLayout>();
                    responsive.window = window; responsive.resultGrid = grid.GetComponent<GridLayoutGroup>();
                    responsive.resultCount = rewards.Count; responsive.subtitle = subtitle; responsive.crest = crest.rectTransform;
                    responsive.summary = summary; responsive.summaryIcon = summaryIcon.rectTransform; responsive.level = level;
                    responsive.summaryText = summaryText; responsive.experience = experience;
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
                var preview = UiKit.Row(body, "Summon item preview", 128f * 4 / 3);
                var slot = UiKit.Slot(preview, item.name, item.icon, item.rarity, item.count, CopiesNeeded(item), item.equipped, false, null, 128f * 4 / 3);
                var previewSize = slot.GetComponent<LayoutElement>();
                previewSize.minWidth = previewSize.preferredWidth = 128; previewSize.flexibleWidth = 0;
                UiKit.Text(body, "현재 보유 수량 " + UiNumber.Format(item.count) + " · Lv. " + UiNumber.Format(item.level), 24, TextAnchor.MiddleCenter, 46);
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
                var headingText = UiKit.Text(heading, "Lv. " + UiNumber.Format(summonStates[category].level) + " " + CommerceLabel(category) + " 뽑기", 35, TextAnchor.MiddleCenter, 58);
                UiKit.Stretch(headingText.rectTransform, 5, 2, 5, 2);
                var explanation = UiKit.Text(body, "1회 뽑기 기준 · 모든 회차 독립 추첨", 24, TextAnchor.MiddleCenter, 34);
                var gradeTitle = UiKit.Text(body, "등급별 확률", 29, TextAnchor.MiddleLeft, 39);
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
                var itemTitle = UiKit.Text(body, "아이템별 확률", 29, TextAnchor.MiddleLeft, 44);
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
                if (window)
                {
                    var responsive = window.inner.gameObject.AddComponent<DoodleCommerceLayout>();
                    responsive.window = window;
                    responsive.probabilityHeading = heading; responsive.probabilityHeadingText = headingText;
                    responsive.probabilityExplanation = explanation; responsive.probabilityGradeTitle = gradeTitle;
                    responsive.probabilityGrades = grades; responsive.probabilityItemTitle = itemTitle;
                    responsive.Reflow();
                }
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
            string[] productArt = { "DiamondSingle", "DiamondPile", "DiamondBag", "DiamondChest", "DiamondRoyalChest" };
            int productIndex = 0;
            foreach (var product in commerceTuning.products)
            {
                if (product == null || product.amount <= 0 || product.priceWon <= 0) continue;
                var row = CommerceFramedRow(body, "CurrencyProduct" + product.amount, 145);
                row.GetComponent<Image>().color = new Color(1, .977f, .895f);
                var art = UiKit.Column(row, "ProductArt", 0, 0);
                UiKit.Flexible(art);
                var artLayout = art.GetComponent<VerticalLayoutGroup>();
                artLayout.childAlignment = TextAnchor.MiddleCenter;
                UiKit.Icon(art, productArt[Mathf.Min(productIndex++, productArt.Length - 1)], 94);
                UiKit.Text(art, UiNumber.Format(product.amount), 32, TextAnchor.MiddleCenter, 34);
                var purchase = UiKit.Column(row, "ProductPrice", 7, 0);
                UiKit.Flexible(purchase);
                CommerceButtonText(UiKit.Button(purchase, "₩" + product.priceWon.ToString("N0"), () => Toast("결제 서비스가 연결되지 않아 구매할 수 없어요."), UiKit.Blue, 72), 35);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleSummonRibbon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            Vector2 P(float x, float y) { return new Vector2(r.center.x + x * r.width, r.center.y + y * r.height); }
            var gold = new Color(1, .79f, .30f);
            var face = new Color(1, .90f, .43f);
            DoodleCommerceMesh.Polygon(vh, new[] { P(-.39f,.25f), P(-.50f,.12f), P(-.45f,-.18f), P(-.49f,-.61f), P(-.33f,-.48f), P(-.31f,-.10f) }, gold, 3);
            DoodleCommerceMesh.Polygon(vh, new[] { P(.39f,.25f), P(.50f,.12f), P(.45f,-.18f), P(.49f,-.61f), P(.33f,-.48f), P(.31f,-.10f) }, gold, 3);
            DoodleCommerceMesh.Polygon(vh, new[] { P(-.39f,-.36f), P(-.33f,-.48f), P(-.31f,-.10f) }, new Color(.79f,.49f,.12f), 2);
            DoodleCommerceMesh.Polygon(vh, new[] { P(.39f,-.36f), P(.33f,-.48f), P(.31f,-.10f) }, new Color(.79f,.49f,.12f), 2);
            DoodleCommerceMesh.Polygon(vh, new[] { P(-.39f,.34f), P(-.36f,.46f), P(-.12f,.51f), P(.34f,.47f), P(.39f,.37f), P(.38f,-.38f), P(.35f,-.46f), P(-.35f,-.47f), P(-.38f,-.36f) }, face, 4);
            DoodleCommerceMesh.Stroke(vh, P(-.36f,.30f), P(-.35f,-.29f), 3, new Color(1, .97f, .71f));
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleSummonCelebration : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            Vector2 P(float x, float y) { return new Vector2(r.xMin + x * r.width, r.yMin + y * r.height); }
            float tall = Mathf.Clamp01((r.height - 720) / 800);
            if (tall < .12f) return;
            Color[] colors = { new Color(.52f,.85f,.40f), new Color(.99f,.83f,.32f), new Color(.40f,.77f,.98f), new Color(.96f,.54f,.72f) };
            Vector2[] pieces = { P(.10f,.93f), P(.28f,.968f), P(.82f,.944f), P(.94f,.899f), P(.055f,.862f), P(.91f,.755f), P(.10f,.10f), P(.91f,.111f), P(.18f,.045f), P(.82f,.041f) };
            for (int i = 0; i < pieces.Length; i++)
            {
                float angle = (i % 2 == 0 ? -34 : 37) * Mathf.Deg2Rad;
                Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 cross = new Vector2(-axis.y, axis.x);
                Vector2 a = axis * 7, b = cross * 13;
                DoodleCommerceMesh.Polygon(vh, new[] { pieces[i]-a-b, pieces[i]+a-b, pieces[i]+a+b, pieces[i]-a+b }, colors[i%colors.Length], 3);
            }
            var yellow = new Color(1, .77f, .20f);
            foreach (float y in new[] { .815f, .273f, .065f })
            {
                DoodleCommerceMesh.Stroke(vh, P(.045f,y), P(.075f,y-.006f), 5, yellow);
                DoodleCommerceMesh.Stroke(vh, P(.92f,y), P(.946f,y+.014f), 5, yellow);
            }
            DoodleCommerceMesh.Stroke(vh, P(.335f,.94f), P(.353f,.923f), 5, UiKit.Ink);
            DoodleCommerceMesh.Stroke(vh, P(.665f,.94f), P(.647f,.923f), 5, UiKit.Ink);
            DoodleCommerceMesh.Stroke(vh, P(.315f,.908f), P(.339f,.903f), 5, UiKit.Ink);
            DoodleCommerceMesh.Stroke(vh, P(.685f,.908f), P(.661f,.903f), 5, UiKit.Ink);
        }
    }

    static class DoodleCommerceMesh
    {
        public static void Polygon(VertexHelper vh, Vector2[] points, Color fill, float outline)
        {
            int start = vh.currentVertCount;
            Vector2 center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Length;
            vh.AddVert(center, fill, Vector2.zero);
            foreach (var p in points) vh.AddVert(p, fill, Vector2.zero);
            for (int i = 0; i < points.Length; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
            for (int i = 0; i < points.Length; i++) Stroke(vh, points[i], points[(i + 1) % points.Length], outline, UiKit.Ink);
        }
        public static void Stroke(VertexHelper vh, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(from-normal, tint, Vector2.zero); vh.AddVert(from+normal, tint, Vector2.zero);
            vh.AddVert(to+normal, tint, Vector2.zero); vh.AddVert(to-normal, tint, Vector2.zero);
            vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
        }
    }

    /// <summary>Reference-proportioned commerce layouts; tall screens expand the content, not empty gaps.</summary>
    public sealed class DoodleCommerceLayout : MonoBehaviour
    {
        public DoodleUiWindow window;
        public RectTransform tabs, crest, summary, summaryIcon, summaryText, gauge, actions, confirmRow;
        public RectTransform probabilityHeading, probabilityGrades;
        public Text probabilityHeadingText, probabilityExplanation, probabilityGradeTitle, probabilityItemTitle;
        public GridLayoutGroup resultGrid;
        public Text subtitle, level, experience;
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
                if (probabilityHeading && viewport)
                {
                    bool compact = viewport.rect.height < 500;
                    UiKit.Height(probabilityHeading, compact ? 48 : 62);
                    SetTextSize(probabilityHeadingText, compact ? 30 : 35);
                    UiKit.Height(probabilityExplanation.transform, compact ? 24 : 34);
                    SetTextSize(probabilityExplanation, compact ? 20 : 24);
                    UiKit.Height(probabilityGradeTitle.transform, compact ? 28 : 39);
                    SetTextSize(probabilityGradeTitle, compact ? 24 : 29);
                    float gradeHeight = compact ? 62 : 86;
                    UiKit.Height(probabilityGrades, gradeHeight);
                    foreach (RectTransform card in probabilityGrades)
                    {
                        UiKit.Height(card, gradeHeight);
                        SetTextSize(card.GetComponentInChildren<Text>(), compact ? 26 : 29);
                    }
                    UiKit.Height(probabilityItemTitle.transform, compact ? 30 : 44);
                    SetTextSize(probabilityItemTitle, compact ? 24 : 29);
                    return;
                }
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
                // Keep the reference's celebration hierarchy while cards retain a 3:4 ratio.
                float tall = Mathf.Clamp01((window.inner.rect.height - 720) / 800);
                float header = Mathf.Lerp(Mathf.Max(82, window.headerHeight), 335, tall);
                float footerGap = Mathf.Lerp(8, 26, tall);
                float summaryHeight = Mathf.Lerp(52, 124, tall);
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
                UiKit.Height(summary, summaryHeight);
                UiKit.Height(level.transform, Mathf.Lerp(30, 54, tall));
                UiKit.Height(experience.transform, Mathf.Lerp(18, 36, tall));
                summaryText.GetComponent<VerticalLayoutGroup>().spacing = Mathf.Lerp(2, 6, tall);
                float summaryIconSize = Mathf.Lerp(46, 108, tall);
                SetIconSize(summaryIcon, summaryIconSize);
                SetTextSize(level, Mathf.RoundToInt(Mathf.Lerp(26, 40, tall)));
                SetTextSize(experience, Mathf.RoundToInt(Mathf.Lerp(18, 29, tall)));
                var summaryLayout = summary.GetComponent<HorizontalLayoutGroup>();
                summaryLayout.childAlignment = TextAnchor.MiddleCenter;
                summaryLayout.childForceExpandWidth = false;
                var levelSize = summaryText.GetComponent<LayoutElement>();
                float availableLevelWidth = Mathf.Max(1, window.footer.rect.width - summaryIconSize - summaryLayout.spacing);
                levelSize.minWidth = levelSize.preferredWidth = Mathf.Min(Mathf.Max(level.preferredWidth, experience.preferredWidth) + 12, availableLevelWidth);
                levelSize.flexibleWidth = 0;
                UiKit.Height(gauge, gaugeHeight);
                SetTextSize(gauge.GetComponentInChildren<Text>(), Mathf.RoundToInt(Mathf.Lerp(23, 29, tall)));
                UiKit.Height(actions, actionHeight);
                foreach (var button in actions.GetComponentsInChildren<Button>())
                {
                    UiKit.Height(button.transform, actionHeight);
                    SetTextSize(button.GetComponentInChildren<Text>(), Mathf.RoundToInt(Mathf.Lerp(25, 34, tall)));
                    var cost = button.transform.Find("DiamondCost");
                    if (cost)
                    {
                        var costText = cost.GetComponentInChildren<Text>();
                        SetTextSize(costText, Mathf.RoundToInt(Mathf.Lerp(20, 26, tall)));
                        UiKit.Height(costText.transform, Mathf.Lerp(24, 32, tall));
                        var costSize = costText.GetComponent<LayoutElement>();
                        costSize.minWidth = costSize.preferredWidth = costText.preferredWidth + 4;
                        SetIconSize(cost.GetComponentInChildren<Image>().rectTransform, Mathf.Lerp(20, 28, tall));
                    }
                }
                UiKit.Height(confirmRow, confirmHeight); UiKit.Height(confirm.transform, confirmHeight);
                var confirmSize = confirm.GetComponent<LayoutElement>();
                confirmSize.minWidth = confirmSize.preferredWidth = Mathf.Lerp(window.footer.rect.width, Mathf.Min(440, window.footer.rect.width), tall);
                confirmSize.flexibleWidth = 0;
                SetTextSize(confirm.GetComponentInChildren<Text>(), Mathf.RoundToInt(Mathf.Lerp(29, 43, tall)));
                SetTextSize(subtitle, Mathf.RoundToInt(Mathf.Lerp(32, 40, tall)));

                resultGrid.spacing = new Vector2(10, Mathf.Lerp(8, 16, tall));
                var bodyLayout = window.content.GetComponent<VerticalLayoutGroup>();
                float contentWidth = Mathf.Min(760, window.inner.rect.width - 42);
                // Two rows stay entirely visible even on short screens; long draws still scroll.
                float fittingWidth = Mathf.Max(1, (viewport.rect.height - 78 - resultGrid.spacing.y) * .5f) * .75f * 5
                    + resultGrid.spacing.x * 4 + resultGrid.padding.horizontal + bodyLayout.padding.horizontal;
                contentWidth = Mathf.Min(contentWidth, fittingWidth);
                window.content.sizeDelta = new Vector2(contentWidth, window.content.sizeDelta.y);
                float gridWidth = contentWidth - bodyLayout.padding.horizontal;
                float cellWidth = Mathf.Max(1, (gridWidth - resultGrid.padding.horizontal - resultGrid.spacing.x * 4) / 5);
                float cellHeight = cellWidth * 4 / 3;
                resultGrid.cellSize = new Vector2(cellWidth, cellHeight);
                int rows = Mathf.CeilToInt(resultCount / 5f);
                float occupied = 58 + 10 + rows * cellHeight + Mathf.Max(0, rows - 1) * resultGrid.spacing.y + 8;
                // Canvas scale conversion can land on either side of a half-unit rounding tie.
                // Normalize that noise so identical portrait layouts keep identical row placement.
                float spareHeight = Mathf.Round(Mathf.Max(0, viewport.rect.height - occupied) * 100) / 100;
                int topPadding = 4 + Mathf.RoundToInt(spareHeight * .5f);
                if (bodyLayout.padding.top != topPadding)
                    bodyLayout.padding = new RectOffset(bodyLayout.padding.left, bodyLayout.padding.right, topPadding, bodyLayout.padding.bottom);

                var banner = window.inner.Find("Golden result banner") as RectTransform;
                float bannerCenter = Mathf.Lerp(43, 239, tall), bannerHeight = Mathf.Lerp(62, 132, tall);
                if (crest)
                {
                    float crestSize = Mathf.Lerp(58, 122, tall);
                    crest.gameObject.SetActive(tall > .35f);
                    // Overlap the ribbon's upper border without covering its lettering.
                    float crestCenter = bannerCenter - bannerHeight * .5f - crestSize * .30f;
                    PlaceTop(crest, crestCenter, Vector2.one * crestSize);
                }
                float bannerWidth = Mathf.Min(window.inner.rect.width - Mathf.Lerp(130, 65, tall), 650);
                if (banner) PlaceTop(banner, bannerCenter, new Vector2(bannerWidth, bannerHeight));
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
