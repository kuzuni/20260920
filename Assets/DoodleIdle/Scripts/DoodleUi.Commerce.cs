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
            public int fiftyCost = 500;
            public int relicUnitCost = 100, skillUnitCost = 200, companionUnitCost = 200;
            public int[] levelExperience = { 50, 100, 200, 400, 800, 1000 };
            public SummonRateTier[] rates = {
                new SummonRateTier { level=1, basisPoints=new[]{6000,3000,900,100,0,0,0} },
                new SummonRateTier { level=5, basisPoints=new[]{4500,3000,1800,600,100,0,0} },
                new SummonRateTier { level=15, basisPoints=new[]{2000,2000,2500,2500,900,100,0} },
                new SummonRateTier { level=25, basisPoints=new[]{1200,1200,2480,2400,2000,700,20} },
                new SummonRateTier { level=30, basisPoints=new[]{1000,1000,2400,2500,2000,1000,100} }
            };
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
        [Serializable] public sealed class SummonRateTier { public int level; public int[] basisPoints; }
        public const int MaxSummonLevel=30;

        [Serializable]
        public sealed class SummonState
        {
            public string category;
            public int level = 1, experience;
            public string freeUsedDay = "";
            public int freeUsedCount;
        }

        readonly string[] commerceCategories = { "Armor", "Club", "Skill", "Companion", "Relic", "DungeonRelic" };
        readonly string[] commerceLabels = { "갑옷", "몽둥이", "스킬", "동료", "유물", "던전 유물" };
        readonly string[] commerceGrades = GradeNames;
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
            var rates=commerceTuning.rates;
            if(rates==null||rates.Length<2||rates[0].level!=1||rates[rates.Length-1].level!=MaxSummonLevel)
                throw new InvalidOperationException("Summon rate anchors must cover levels 1 through 30.");
            int priorLevel=0;
            foreach(var tier in rates)
            {
                if(tier.level<=priorLevel||tier.basisPoints==null||tier.basisPoints.Length!=7)
                    throw new InvalidOperationException("Summon rate anchors must increase and contain seven grades.");
                int total=0;foreach(int weight in tier.basisPoints){if(weight<0)throw new InvalidOperationException("Negative summon probability.");total+=weight;}
                if(total!=10000||tier.basisPoints[0]<1000||tier.basisPoints[1]<1000)
                    throw new InvalidOperationException("Summon rates must total 100% with at least 10% normal and advanced.");
                priorLevel=tier.level;
            }
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
                if (!json.Contains("freeUsedCount") && state.freeUsedDay == CommerceDay()) state.freeUsedCount = 1;
                state.freeUsedCount = Mathf.Clamp(state.freeUsedCount, 0, 3);
                state.category = category;
                state.level = IsRelicSummon(category)?0:Mathf.Clamp(state.level, 1, MaxSummonLevel);
                state.experience = IsRelicSummon(category) || state.level==MaxSummonLevel ? 0 : Mathf.Clamp(state.experience, 0, CommerceExperienceNeeded(state) - 1);
                summonStates[category] = state;
            }
        }

        void SaveCommerce()
        {
            foreach (var state in summonStates.Values)
                PlayerPrefs.SetString("DoodleUi.Commerce." + state.category, JsonUtility.ToJson(state));
        }

        static bool IsRelicSummon(string category) => category=="Relic"||category=="DungeonRelic";
        static string SummonIcon(string category) => category=="Skill"?"SkillMeteor":category=="Relic"?"NavPottery":category=="DungeonRelic"?"DungeonPottery":category;

        static string CommerceDay() { return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        int CommerceExperienceNeeded(SummonState state)
        {
            if(IsRelicSummon(state.category))return 1;
            return commerceTuning.levelExperience[Mathf.Min(state.level - 1, commerceTuning.levelExperience.Length - 1)];
        }

        string CommerceLabel(string category)
        {
            int index = Array.IndexOf(commerceCategories, category);
            return index >= 0 ? commerceLabels[index] : category;
        }

        public int SummonLevel(string category) { if(summonStates.Count==0)InitCommerce();return summonStates[category].level; }
        public int SummonExperience(string category) { return summonStates[category].experience; }
        public int FreeSummonsRemaining(string category) { if(category=="DungeonRelic")return 0; var state=summonStates[category]; return state.freeUsedDay == CommerceDay() ? Mathf.Max(0,3-state.freeUsedCount) : 3; }
        public bool CanFreeSummon(string category) => FreeSummonsRemaining(category) > 0;

        public int[] SummonWeights(string category) => SummonWeights(category, SummonLevel(category));
        public int[] SummonWeights(string category, int previewLevel)
        {
            int level=Mathf.Clamp(previewLevel, 1, MaxSummonLevel);
            if(IsRelicSummon(category))return new[]{10000,0,0,0,0,0,0};
            var anchors=commerceTuning.rates;var lower=anchors[0];var upper=anchors[anchors.Length-1];
            foreach(var tier in anchors){if(tier.level<=level)lower=tier;if(tier.level>=level){upper=tier;break;}}
            double t=upper.level==lower.level?0:(level-lower.level)/(double)(upper.level-lower.level);
            var weights=new int[7];int sum=0;
            for(int grade=0;grade<7;grade++)
            {
                int value=(int)Math.Round(lower.basisPoints[grade]+(upper.basisPoints[grade]-lower.basisPoints[grade])*t);
                if((grade==4&&level<5)||(grade==5&&level<15)||(grade==6&&level<25))value=0;
                weights[grade]=value;sum+=value;
            }
            weights[0]+=10000-sum;
            if(category=="Skill" || category=="Companion") { weights[5]+=weights[6];weights[6]=0; }
            return weights;
        }

        // Refunds use the same current unit price as skill summons; no bulk discount.
        // Fractional diamonds carry over between refunds and are persisted.
        public decimal SkillRefundUnitPrice => Math.Max(1, commerceTuning.skillUnitCost);
        public bool SkipSummonAnimations { get => PlayerPrefs.GetInt("DoodleUi.SkipSummonAnimations", 0) != 0; set { PlayerPrefs.SetInt("DoodleUi.SkipSummonAnimations", value ? 1 : 0); PlayerPrefs.Save(); } }
        public int SummonCost(string category, int count)
        {
            long unit = category == "Relic" ? commerceTuning.relicUnitCost : category == "Skill" ? commerceTuning.skillUnitCost : category == "Companion" ? commerceTuning.companionUnitCost : Math.Max(1, commerceTuning.tenCost / 10);
            return (int)Math.Min(int.MaxValue, Math.Max(1, unit) * count);
        }
        decimal SkillRefundRemainder => decimal.TryParse(PlayerPrefs.GetString("DoodleUi.SkillRefundRemainder", "0"), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? Math.Max(0, Math.Min(.999999m, value)) : 0;
        int RefundableSkillCopies(UiItem item)
        {
            if (item == null || item.category != "Skill" || !item.discovered || item.level < 100 || item.count <= 0 || !Items("Skill").Contains(item)) return 0;
            decimal room = Math.Max(0L, (long)int.MaxValue - Diamonds);
            return (int)Math.Min(item.count, Math.Max(0, decimal.Floor((room - SkillRefundRemainder) / SkillRefundUnitPrice)));
        }
        public bool CanRefundSkill(UiItem item) => RefundableSkillCopies(item) > 0;
        public int SkillRefundQuote(UiItem item) => (int)decimal.Floor(RefundableSkillCopies(item) * SkillRefundUnitPrice + SkillRefundRemainder);
        public int RefundSkill(UiItem item)
        {
            int copies = RefundableSkillCopies(item);
            if (copies == 0) return 0;
            decimal amount = copies * SkillRefundUnitPrice + SkillRefundRemainder;
            int paid = (int)decimal.Floor(amount);
            item.count -= copies; Diamonds += paid;
            PlayerPrefs.SetString("DoodleUi.SkillRefundRemainder", (amount - paid).ToString(CultureInfo.InvariantCulture));
            Save(); return paid;
        }

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
            UiKit.Text(body, "무료 뽑기는 종류별 하루 3회 · 던전 유물은 전용 뽑기권 사용", 17, TextAnchor.MiddleCenter, 40);
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
            var row = CommerceFramedRow(body, "Summon_" + category, category == "Relic" ? 336 : 264);
            UiKit.Icon(row, SummonIcon(category), 213).name="Icon: "+category;
            var content = UiKit.Column(row, "SummonInformation", 7, 0);
            UiKit.Flexible(content);
            var title = UiKit.Row(content, "SummonTitle", 68, 5);
            bool relic=IsRelicSummon(category);
            UiKit.Text(title, (relic?"":"Lv. " + state.level + (state.level==MaxSummonLevel?" MAX":"")+"\n") + CommerceLabel(category) + " 뽑기", 32, TextAnchor.MiddleLeft, 68);
            var info = UiKit.Button(title, "i", () => ShowSummonProbabilities(category), UiKit.Blue, 36);
            var size = info.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 36;
            size.flexibleWidth = 0;
            int needed = CommerceExperienceNeeded(state);
            if(relic)UiKit.Text(content,"모든 유물 동일 등급 · 각각 " + (100d / Items(category).Count).ToString("0.##") + "%",22,TextAnchor.MiddleLeft,28);
            else UiKit.Gauge(content, state.level==MaxSummonLevel?"MAX":UiNumber.Format(state.experience) + "/" + UiNumber.Format(needed), state.level==MaxSummonLevel?1:(float)state.experience / needed, 28).GetComponentInChildren<Text>().resizeTextMaxSize = 23;
            if(category=="DungeonRelic")UiKit.Text(content,"전용 뽑기권 "+UiNumber.Format(DungeonRelicTickets)+"장",22,TextAnchor.MiddleLeft,28);
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
            if(category=="DungeonRelic") {
                foreach(int count in new[]{1,10,50}) {
                    var ticket=UiKit.Button(actions,count+"회 뽑기\n뽑기권 "+count+"장",()=>TrySummonDungeonRelicTickets(count),UiKit.Yellow,68);
                    ticket.name="DungeonRelicTicketSummon"+count;CommerceButtonText(ticket,22);ticket.interactable=DungeonRelicTickets>=count;
                }
                return;
            }
            var free = UiKit.Button(actions, "무료 " + commerceTuning.freeCount + "회뽑기\n(" + FreeSummonsRemaining(category) + "/3)", () => TrySummon(category, commerceTuning.freeCount, true), result ? UiKit.Blue : UiKit.Green, 68);
            free.name = "무료 " + commerceTuning.freeCount + "회\n뽑기";
            CommerceButtonText(free, 24);
            free.interactable = CanFreeSummon(category);
            PaidSummonButton(actions, category, 10, SummonCost(category, 10), UiKit.Yellow);
            PaidSummonButton(actions, category, 50, SummonCost(category, 50), UiKit.Yellow);
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
            if (category=="DungeonRelic" || !summonStates.ContainsKey(category)) return false;
            if (free && count != commerceTuning.freeCount) return false;
            if (!free && count != 10 && count != 50) return false;
            if (free && !CanFreeSummon(category)) { Toast("오늘 무료 뽑기를 모두 사용했어요."); return false; }
            int cost = free ? 0 : SummonCost(category, count);
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
            if (free) { state.freeUsedCount = state.freeUsedDay == CommerceDay() ? state.freeUsedCount + 1 : 1; state.freeUsedDay = CommerceDay(); }
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

        public bool TrySummonDungeonRelicTickets(int count)
        {
            if(summonStates.Count==0)InitCommerce();
            if((count!=1&&count!=10&&count!=50)||DungeonRelicTickets<count)return false;
            var rewards=new List<UiItem>();
            for(int i=0;i<count;i++){var item=GrantItem("DungeonRelic",commerceRandom);if(item==null)return false;rewards.Add(item);}
            if(!TrySpendDungeonRelicTickets(count))return false;
            long before=Power;CompleteSummon("DungeonRelic",rewards);NotifyPowerChanged(before,"던전 유물 획득");return true;
        }

        void CompleteSummon(string category, List<UiItem> rewards)
        {
            var state = summonStates[category];
            foreach (var item in rewards) AddItem(item, 1);
            if(!IsRelicSummon(category) && state.level<MaxSummonLevel)state.experience += rewards.Count;
            while (!IsRelicSummon(category) && state.level<MaxSummonLevel && state.experience >= CommerceExperienceNeeded(state))
            {
                state.experience -= CommerceExperienceNeeded(state);
                state.level++;
            }
            if(IsRelicSummon(category)||state.level==MaxSummonLevel)state.experience=0;
            RecordServiceProgress("summon", rewards.Count);
            Save();
            RefreshPage();
            ShowSummonResults(category, rewards);
        }

        void ShowSummonResults(string category, List<UiItem> rewards)
        {
            bool animateWindow = fullscreenTitle != "뽑기 결과" && !SkipSummonAnimations;
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
                    slot.GetComponent<DoodleUiSlotLayout>().hideQuantity = true;
                    slot.GetComponent<DoodleUiSlotLayout>().Invalidate();
                    slot.gameObject.name = "SummonResult_" + i + "_" + item.id;
                }
                var footer = UiKit.Footer(body, "Summon result footer", 228);
                var state = summonStates[category];
                var summary = UiKit.Row(footer, "Summon progress summary", 46, 10);
                var summaryIcon = UiKit.Icon(summary, SummonIcon(category), 46);
                var summaryText = UiKit.Column(summary, "Summon progress text", 4, 0);
                bool relic=IsRelicSummon(category);
                var level = UiKit.Text(summaryText, CommerceLabel(category) + (relic?" 뽑기":" 뽑기 Lv. "+state.level+(state.level==MaxSummonLevel?" MAX":"")), 36, TextAnchor.MiddleLeft, 46);
                int needed = CommerceExperienceNeeded(state);
                var experience = UiKit.Text(summaryText, "", 29, TextAnchor.MiddleLeft, 34);
                experience.gameObject.SetActive(false);
                var gauge = UiKit.Gauge(footer, state.level==MaxSummonLevel?"MAX":state.experience+"/"+needed, state.level==MaxSummonLevel?1:(float)state.experience / needed, 32);
                gauge.name="Summon experience gauge"; gauge.gameObject.SetActive(!relic);
                DoodleSummonReveal reveal = null; DoodleSummonCelebration celebration = null;
                DoodleSlidingSelection skipMotion = null; Image skipTrack = null;
                var skip = UiKit.Button(footer, "연출 스킵", () => {
                    SkipSummonAnimations = !SkipSummonAnimations;
                    skipMotion.Slide(SkipSummonAnimations ? 1 : 0);
                    skipTrack.color=SkipSummonAnimations ? UiKit.Green : new Color(.68f,.68f,.68f);
                    if (SkipSummonAnimations) { if(reveal) reveal.Complete(); if(celebration) celebration.Finish(); }
                }, Color.clear, 44);
                skip.name = "Summon animation skip"; skip.GetComponent<Outline>().enabled=false; skip.transition=Selectable.Transition.None;
                var skipLabel=skip.GetComponentInChildren<Text>();
                skipLabel.rectTransform.anchorMin=new Vector2(.25f,0);skipLabel.rectTransform.anchorMax=new Vector2(.62f,1);
                var track=UiKit.Box(skip.transform,"Skip toggle track",SkipSummonAnimations?UiKit.Green:new Color(.68f,.68f,.68f));
                track.anchorMin=track.anchorMax=new Vector2(.65f,.5f);track.sizeDelta=new Vector2(76,40);track.anchoredPosition=Vector2.zero;
                skipTrack=track.GetComponent<Image>();skipTrack.raycastTarget=false;
                var knob=UiKit.Box(track,"Toggle knob",Color.white);knob.GetComponent<Image>().sprite=UiKit.Circle;knob.GetComponent<Image>().type=Image.Type.Simple;knob.GetComponent<Image>().raycastTarget=false;knob.sizeDelta=Vector2.one*34;
                skipMotion=track.gameObject.AddComponent<DoodleSlidingSelection>();skipMotion.Configure(knob,SkipSummonAnimations?1:0,SkipSummonAnimations?1:0,null,true);
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
                    decoration.SetAsLastSibling();
                    celebration = decoration.gameObject.AddComponent<DoodleSummonCelebration>();celebration.raycastTarget = false;
                    if (SkipSummonAnimations) celebration.Finish();
                    reveal = window.gameObject.AddComponent<DoodleSummonReveal>();reveal.Configure(grid, SkipSummonAnimations);
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
                    responsive.noSummonProgress=relic; responsive.skipButton=skip;
                    responsive.gauge = gauge; responsive.actions = footer.Find("SummonActions") as RectTransform;
                    responsive.confirm = confirm; responsive.confirmRow = confirmRow;
                    responsive.Reflow();
                }
            }, animateWindow);
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
            ShowSummonProbabilityPage(category, SummonLevel(category));
        }
        public double PreviewItemProbability(UiItem item, int level)
        {
            var items = Items(item.dungeonRelic?"DungeonRelic":item.category);
            return item.category == "Relic" ? 100d / items.Count : SummonWeights(item.category, level)[item.rarity] / 100d / items.FindAll(x => x.rarity == item.rarity).Count;
        }
        void ShowSummonProbabilityPage(string category, int level, bool animate=true)
        {
            bool relic = IsRelicSummon(category);
            level = relic ? 0 : Mathf.Clamp(level, 1, MaxSummonLevel);
            ShowDetail("뽑기 확률", body =>
            {
                var window = body.GetComponentInParent<DoodleUiWindow>();
                if (window) { window.maxWidth = 570; window.maxHeight = 1110; }
                var heading = UiKit.Row(body, "Probability level pages", 62, 8);
                System.Action<int> page = next => {
                    var old=overlayStack[overlayStack.Count-1];overlayStack.RemoveAt(overlayStack.Count-1);old.SetActive(false);Destroy(old);
                    ShowSummonProbabilityPage(category, next, false);
                };
                if (!relic) {
                    var previous = UiKit.Button(heading, "<", () => page(level - 1), UiKit.Paper, 62);
                    previous.name = "Previous probability level"; previous.interactable = level > 1; FixedWidth(previous.transform, 54);
                }
                var title = UiKit.Text(heading, CommerceLabel(category) + (relic ? " 뽑기" : " 뽑기 Lv. " + level), 30, TextAnchor.MiddleCenter, 62);
                title.name = "Probability level title"; UiKit.Flexible(title.transform);
                if (!relic) {
                    var next = UiKit.Button(heading, ">", () => page(level + 1), UiKit.Paper, 62);
                    next.name = "Next probability level"; next.interactable = level < MaxSummonLevel; FixedWidth(next.transform, 54);
                }
                UiKit.Text(body, "등급별 확률", 27, TextAnchor.MiddleLeft, 42);
                var weights = SummonWeights(category, level);
                for (int grade = 0; grade < 7; grade++) {
                    if (!Items(category).Exists(x => x.rarity == grade)) continue;
                    var row = CommerceFramedRow(body, "Probability_grade_" + grade, 64);
                    row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 5, 5);
                    var badge = UiKit.Box(row, "Grade color", UiKit.Rarity(grade), 40); FixedWidth(badge, 40);
                    UiKit.Text(row, relic ? "유물" : UiKit.GradeName(grade), 27, TextAnchor.MiddleLeft, 48);
                    var rate = UiKit.Text(row, (relic ? 100d : weights[grade] / 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%", 26, TextAnchor.MiddleRight, 48);
                    rate.name = "Grade probability rate";
                    FixedWidth(rate.transform, 100);
                }
                UiKit.Text(body, relic ? "모든 유물은 같은 확률로 등장합니다.\n각 " + (100d / Items(category).Count).ToString("0.##", CultureInfo.InvariantCulture) + "%" : "같은 등급의 아이템은 모두 같은 확률로 등장합니다.", 20, TextAnchor.MiddleCenter, 60);
                UiKit.Text(body, relic ? "모든 유물 동일 확률" : level + " / " + MaxSummonLevel + " · 확률 미리보기", 20, TextAnchor.MiddleCenter, 40);
                var footer = UiKit.Footer(body, "Probability confirmation footer", 64);
                CommerceButtonText(UiKit.Button(footer, "확인", CloseDetail, UiKit.Yellow, 64), 34);
            }, animate);
        }

        void BuildCurrencyProducts(RectTransform body)
        {
            UiKit.Text(body, "결제 서비스 미연결 · 구매할 수 없습니다", 19, TextAnchor.MiddleCenter, 30);
            string[] productArt = { "DiamondSingle", "DiamondPile", "DiamondBag", "DiamondChest", "DiamondRoyalChest" };
            var grid = UiKit.Grid(body, "Currency product cards", 2, 360);
            UiKit.PortraitGrid(grid);
            int productIndex = 0;
            foreach (var product in commerceTuning.products)
            {
                if (product == null || product.amount <= 0 || product.priceWon <= 0) continue;
                var card = UiKit.Box(grid, "CurrencyProduct" + product.amount, new Color(1, .977f, .895f));
                var amount = UiKit.Text(card, UiNumber.Format(product.amount), 36, TextAnchor.MiddleCenter, 48).rectTransform;
                amount.anchorMin = new Vector2(0, .81f); amount.anchorMax = new Vector2(1, .97f); amount.offsetMin = new Vector2(8, 0); amount.offsetMax = new Vector2(-8, 0);
                var art = UiKit.Icon(card, productArt[Mathf.Min(productIndex++, productArt.Length - 1)], 150).rectTransform;
                art.anchorMin = new Vector2(.1f, .24f); art.anchorMax = new Vector2(.9f, .79f); art.offsetMin = art.offsetMax = Vector2.zero;
                var purchase = UiKit.Button(card, "₩" + product.priceWon.ToString("N0"), () => Toast("결제 서비스가 연결되지 않아 구매할 수 없어요."), UiKit.Blue, 64);
                CommerceButtonText(purchase, 30);
                var price = (RectTransform)purchase.transform;
                price.anchorMin = new Vector2(.06f, .04f); price.anchorMax = new Vector2(.94f, .21f); price.offsetMin = price.offsetMax = Vector2.zero;
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
        float age;
        public void Finish() { age = 2.2f; SetVerticesDirty(); enabled = false; }
        void Update() { age += Time.unscaledDeltaTime; SetVerticesDirty(); if (age >= 2.2f) enabled = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (age >= 2.2f) return;
            var r = rectTransform.rect;
            Color[] colors = { new Color(.52f,.85f,.40f), new Color(.99f,.83f,.32f), new Color(.40f,.77f,.98f), new Color(.96f,.54f,.72f) };
            for (int i = 0; i < 128; i++)
            {
                float t = Mathf.Max(0, age - i % 5 * .02f), side = i % 2 == 0 ? -1 : 1;
                Vector2 origin = new Vector2(r.center.x + side * r.width * .51f, r.yMin + r.height * (.08f + i % 7 * .07f));
                Vector2 velocity = new Vector2(-side * r.width * (.4f + i % 11 * .09f), r.height * (1.25f + i % 7 * .1f));
                Vector2 center = origin + velocity * t + Vector2.down * (r.height * 1.35f * t * t);
                float angle = (i * 37 + t * (i % 2 == 0 ? 420 : -560)) * Mathf.Deg2Rad;
                Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float size=Mathf.Clamp(r.width/720, .75f, 1.5f);
                Vector2 a = axis * ((10 + i % 4 * 2)*size), b = new Vector2(-axis.y, axis.x) * ((22+i%3*4)*size);
                Color tint = colors[i % colors.Length]; tint.a = Mathf.Clamp01((2.2f - age)*2);
                DoodleCommerceMesh.Polygon(vh, new[] { center-a-b,center+a-b,center+a+b,center-a+b }, tint, 1.5f);
            }
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
        public bool noSummonProgress;
        public Button skipButton;
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
                    var gradeGrid=probabilityGrades.GetComponent<GridLayoutGroup>();
                    gradeGrid.cellSize=new Vector2(gradeGrid.cellSize.x,gradeHeight);
                    UiKit.Height(probabilityGrades,Mathf.CeilToInt(probabilityGrades.childCount/(float)gradeGrid.constraintCount)*(gradeHeight+gradeGrid.spacing.y)-gradeGrid.spacing.y);
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
                float summaryHeight = Mathf.Lerp(46, 84, tall);
                float gaugeHeight = noSummonProgress?0:Mathf.Lerp(32, 36, tall);
                float actionHeight = Mathf.Lerp(68, 106, tall);
                float confirmHeight = Mathf.Lerp(58, 88, tall);
                float skipHeight=44;
                float footerHeight = summaryHeight + gaugeHeight + skipHeight + actionHeight + confirmHeight + footerGap * (noSummonProgress?3:4);
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
                confirmSize.minWidth = confirmSize.preferredWidth = window.footer.rect.width * .38f;
                if(skipButton) { UiKit.Height(skipButton.transform, skipHeight);SetTextSize(skipButton.GetComponentInChildren<Text>(), 22); }
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
