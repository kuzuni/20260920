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
            var tabs = UiKit.Row(body, "ShopTabs", 54);
            UiKit.Button(tabs, "뽑기", () => { shopTab = 0; RefreshPage(); }, shopTab == 0 ? UiKit.Yellow : UiKit.Paper);
            UiKit.Button(tabs, "재화", () => { shopTab = 1; RefreshPage(); }, shopTab == 1 ? UiKit.Blue : UiKit.Paper);
            if (shopTab == 1) { BuildCurrencyProducts(body); return; }
            foreach (string category in commerceCategories) BuildSummonRow(body, category);
            UiKit.Text(body, "무료 뽑기는 종류별 하루 1회 · 매일 UTC 00:00 초기화", 17, TextAnchor.MiddleCenter, 40);
        }

        RectTransform CommerceFramedRow(Transform parent, string name, float height)
        {
            var row = UiKit.Box(parent, name, UiKit.Paper, height);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10;
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
            var row = CommerceFramedRow(body, "Summon_" + category, 214);
            UiKit.Icon(row, category, 112);
            var content = UiKit.Column(row, "SummonInformation", 6, 0);
            UiKit.Flexible(content);
            var title = UiKit.Row(content, "SummonTitle", 39, 5);
            UiKit.Text(title, "Lv. " + state.level + " " + CommerceLabel(category) + " 뽑기", 25, TextAnchor.MiddleLeft, 39);
            var info = UiKit.Button(title, "i", () => ShowSummonProbabilities(category), UiKit.Blue, 36);
            var size = info.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 38;
            size.flexibleWidth = 0;
            int needed = CommerceExperienceNeeded(state);
            UiKit.Gauge(content, state.experience + "/" + needed, (float)state.experience / needed, 28);
            BuildSummonButtons(content, category);
            UiKit.Text(content, CanFreeSummon(category) ? "오늘 무료 뽑기 가능" : "오늘 무료 뽑기 완료", 16, TextAnchor.MiddleCenter, 24);
        }

        void BuildSummonButtons(Transform parent, string category)
        {
            var actions = UiKit.Row(parent, "SummonActions", 68, 5);
            var free = UiKit.Button(actions, "무료 " + commerceTuning.freeCount + "회\n뽑기", () => TrySummon(category, commerceTuning.freeCount, true), UiKit.Green, 68);
            free.interactable = CanFreeSummon(category);
            PaidSummonButton(actions, category, 10, commerceTuning.tenCost, UiKit.Blue);
            PaidSummonButton(actions, category, 50, commerceTuning.fiftyCost, UiKit.Yellow);
        }

        void PaidSummonButton(Transform parent, string category, int count, int cost, Color color)
        {
            var column = UiKit.Column(parent, "PaidSummon" + count, 1, 0);
            UiKit.Flexible(column);
            UiKit.Button(column, count + "회 뽑기", () => TrySummon(category, count, false), color, 42);
            var price = UiKit.Row(column, "DiamondCost", 24, 2);
            UiKit.Icon(price, "Diamond", 22);
            UiKit.Text(price, cost.ToString("N0"), 17, TextAnchor.MiddleCenter, 24);
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
                UiKit.Text(body, CommerceLabel(category) + " " + rewards.Count + "회 뽑기", 32, TextAnchor.MiddleCenter, 58);
                var grid = UiKit.Grid(body, "SummonResultCards", 5, 150);
                for (int i = 0; i < rewards.Count; i++)
                {
                    var item = rewards[i];
                    var slot = UiKit.Slot(grid, item.name, item.icon, item.rarity, item.count, CopiesNeeded(item), item.equipped, false,
                        () => ShowSummonItem(item), 150);
                    slot.gameObject.name = "SummonResult_" + i + "_" + item.id;
                }
                var state = summonStates[category];
                UiKit.Text(body, CommerceLabel(category) + " 뽑기 Lv. " + state.level, 27, TextAnchor.MiddleCenter, 46);
                int needed = CommerceExperienceNeeded(state);
                UiKit.Gauge(body, state.experience + "/" + needed, (float)state.experience / needed, 32);
                BuildSummonButtons(body, category);
                UiKit.Button(body, "확인", () => { CloseFullscreen(); RefreshPage(); }, UiKit.Yellow, 58);
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
                UiKit.Text(body, "Lv. " + summonStates[category].level + " " + CommerceLabel(category) + " 뽑기", 29, TextAnchor.MiddleCenter, 50);
                UiKit.Text(body, "1회 뽑기 기준 · 모든 회차 독립 추첨", 20, TextAnchor.MiddleCenter, 38);
                var grades = UiKit.Row(body, "GradeProbabilities", 72, 5);
                for (int rarity = 0; rarity < 5; rarity++)
                {
                    int grade = rarity;
                    var card = UiKit.Box(grades, "Grade" + rarity, UiKit.Rarity(rarity), 72);
                    UiKit.Flexible(card);
                    var label = UiKit.Text(card, commerceGrades[grade] + "\n" + GradeProbability(category, grade).ToString("0.####", CultureInfo.InvariantCulture) + "%", 21, TextAnchor.MiddleCenter, 72);
                    var rect = label.rectTransform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(2, 2); rect.offsetMax = new Vector2(-2, -2);
                }
                UiKit.Text(body, "아이템별 확률 · 같은 등급 안에서 균등 추첨", 21, TextAnchor.MiddleCenter, 42);
                var all = Items(category);
                for (int rarity = 0; rarity < 5; rarity++)
                {
                    var group = all.FindAll(item => item.rarity == rarity);
                    int grade = rarity;
                    UiKit.Text(body, commerceGrades[grade] + " · 합계 " + GradeProbability(category, grade).ToString("0.####", CultureInfo.InvariantCulture) + "%", 24, TextAnchor.MiddleCenter, 42).color = Color.Lerp(UiKit.Rarity(grade), UiKit.Ink, .72f);
                    foreach (var item in group)
                    {
                        var row = CommerceFramedRow(body, "Probability_" + item.id, 61);
                        UiKit.Icon(row, item.icon, 40);
                        UiKit.Text(row, item.name, 21, TextAnchor.MiddleLeft, 36);
                        UiKit.Text(row, CommerceExactRate(category, grade, group.Count), 20, TextAnchor.MiddleRight, 36);
                    }
                }
                UiKit.Text(body, "등급 확률 합계 100% · 레벨과 관계없이 동일한 확률\n나눗셈으로 표시한 값은 반올림하지 않은 정확한 확률입니다.", 17, TextAnchor.MiddleCenter, 66);
                UiKit.Button(body, "확인", CloseDetail, UiKit.Yellow);
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
            UiKit.Text(body, "재화 상품 · 결제 서비스 미연결", 21, TextAnchor.MiddleCenter, 40);
            foreach (var product in commerceTuning.products)
            {
                if (product == null || product.amount <= 0 || product.priceWon <= 0) continue;
                var row = CommerceFramedRow(body, "CurrencyProduct" + product.amount, 156);
                var art = UiKit.Column(row, "ProductArt", 0, 0);
                UiKit.Flexible(art);
                var artLayout = art.GetComponent<VerticalLayoutGroup>();
                artLayout.childAlignment = TextAnchor.MiddleCenter;
                UiKit.Icon(art, "Diamond", 82);
                UiKit.Text(art, product.amount.ToString("N0"), 27, TextAnchor.MiddleCenter, 36);
                var purchase = UiKit.Column(row, "ProductPrice", 7, 0);
                UiKit.Flexible(purchase);
                UiKit.Button(purchase, "₩" + product.priceWon.ToString("N0"), () => Toast("결제 서비스가 연결되지 않아 구매할 수 없어요."), UiKit.Blue, 64);
                UiKit.Text(purchase, "구매 불가 · 표시용 가격", 17, TextAnchor.MiddleCenter, 28);
            }
        }
    }
}
