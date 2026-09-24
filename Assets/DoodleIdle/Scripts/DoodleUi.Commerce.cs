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
            public int relicUnitCost = 10, skillUnitCost = 20, companionUnitCost = 20;
            public int[] levelExperience = { 50, 100, 200, 400, 800, 1600, 3200, 6400, 10000 };
            public SummonRateTier[] rates = {
                new SummonRateTier { level=1, basisPoints=new[]{90000,9000,1000,0,0,0,0,0,0} },
                new SummonRateTier { level=2, basisPoints=new[]{86111,12444,1444,1,0,0,0,0,0} },
                new SummonRateTier { level=3, basisPoints=new[]{82217,15889,1889,5,0,0,0,0,0} },
                new SummonRateTier { level=4, basisPoints=new[]{78324,19333,2333,10,0,0,0,0,0} },
                new SummonRateTier { level=5, basisPoints=new[]{74244,22778,2778,200,0,0,0,0,0} },
                new SummonRateTier { level=6, basisPoints=new[]{68778,26222,3222,1777,1,0,0,0,0} },
                new SummonRateTier { level=7, basisPoints=new[]{64330,29667,3667,2333,3,0,0,0,0} },
                new SummonRateTier { level=8, basisPoints=new[]{58881,33111,4111,3888,9,0,0,0,0} },
                new SummonRateTier { level=9, basisPoints=new[]{54434,36556,4556,4444,10,0,0,0,0} },
                new SummonRateTier { level=10, basisPoints=new[]{49901,40000,5000,4999,100,0,0,0,0} },
                new SummonRateTier { level=11, basisPoints=new[]{48000,39000,7500,5299,201,0,0,0,0} },
                new SummonRateTier { level=12, basisPoints=new[]{46000,38000,10000,5599,401,0,0,0,0} },
                new SummonRateTier { level=13, basisPoints=new[]{44001,37000,12500,5899,600,0,0,0,0} },
                new SummonRateTier { level=14, basisPoints=new[]{42001,36000,15000,6199,800,0,0,0,0} },
                new SummonRateTier { level=15, basisPoints=new[]{39999,35000,17500,6500,1000,1,0,0,0} },
                new SummonRateTier { level=16, basisPoints=new[]{37997,34000,20000,6800,1200,3,0,0,0} },
                new SummonRateTier { level=17, basisPoints=new[]{35991,33000,22500,7100,1400,9,0,0,0} },
                new SummonRateTier { level=18, basisPoints=new[]{33986,32000,25000,7400,1599,15,0,0,0} },
                new SummonRateTier { level=19, basisPoints=new[]{31951,31000,27500,7700,1799,50,0,0,0} },
                new SummonRateTier { level=20, basisPoints=new[]{29911,30000,30000,8000,1999,90,0,0,0} },
                new SummonRateTier { level=21, basisPoints=new[]{29000,29000,29000,9200,3599,201,0,0,0} },
                new SummonRateTier { level=22, basisPoints=new[]{28000,28000,28000,10400,5199,401,0,0,0} },
                new SummonRateTier { level=23, basisPoints=new[]{27001,27000,27000,11600,6799,600,0,0,0} },
                new SummonRateTier { level=24, basisPoints=new[]{26001,26000,26000,12800,8399,800,0,0,0} },
                new SummonRateTier { level=25, basisPoints=new[]{24999,25000,25000,14000,10000,1000,1,0,0} },
                new SummonRateTier { level=26, basisPoints=new[]{23992,24000,24000,15200,11600,1200,8,0,0} },
                new SummonRateTier { level=27, basisPoints=new[]{22988,23000,23000,16400,13200,1400,12,0,0} },
                new SummonRateTier { level=28, basisPoints=new[]{21983,22000,22000,17600,14800,1599,18,0,0} },
                new SummonRateTier { level=29, basisPoints=new[]{20982,21000,21000,18800,16400,1799,19,0,0} },
                new SummonRateTier { level=30, basisPoints=new[]{19981,20000,20000,20000,18000,1999,20,0,0} },
                new SummonRateTier { level=31, basisPoints=new[]{20000,20000,20000,20000,18000,1979,21,0,0} },
                new SummonRateTier { level=32, basisPoints=new[]{20000,20000,20000,20000,18000,1959,41,0,0} },
                new SummonRateTier { level=33, basisPoints=new[]{20000,20000,20000,20000,18000,1940,60,0,0} },
                new SummonRateTier { level=34, basisPoints=new[]{20000,20000,20000,20000,18000,1920,80,0,0} },
                new SummonRateTier { level=35, basisPoints=new[]{19999,20000,20000,20000,18000,1900,100,1,0} },
                new SummonRateTier { level=36, basisPoints=new[]{19896,20000,20000,20000,18000,1900,201,3,0} },
                new SummonRateTier { level=37, basisPoints=new[]{19690,20000,20000,20000,18000,1900,401,9,0} },
                new SummonRateTier { level=38, basisPoints=new[]{19485,20000,20000,20000,18000,1900,600,15,0} },
                new SummonRateTier { level=39, basisPoints=new[]{19250,20000,20000,20000,18000,1900,800,50,0} },
                new SummonRateTier { level=40, basisPoints=new[]{19010,20000,20000,20000,18000,1900,1000,90,0} },
                new SummonRateTier { level=41, basisPoints=new[]{18699,20000,20000,20000,18000,1900,1200,201,0} },
                new SummonRateTier { level=42, basisPoints=new[]{18299,20000,20000,20000,18000,1900,1400,401,0} },
                new SummonRateTier { level=43, basisPoints=new[]{17900,20000,20000,20000,18000,1900,1600,600,0} },
                new SummonRateTier { level=44, basisPoints=new[]{17500,20000,20000,20000,18000,1900,1800,800,0} },
                new SummonRateTier { level=45, basisPoints=new[]{17099,20000,20000,20000,18000,1900,2000,1000,1} },
                new SummonRateTier { level=46, basisPoints=new[]{16697,20000,20000,20000,18000,1900,2200,1200,3} },
                new SummonRateTier { level=47, basisPoints=new[]{16291,20000,20000,20000,18000,1900,2400,1400,9} },
                new SummonRateTier { level=48, basisPoints=new[]{15885,20000,20000,20000,18000,1900,2600,1600,15} },
                new SummonRateTier { level=49, basisPoints=new[]{15450,20000,20000,20000,18000,1900,2800,1800,50} },
                new SummonRateTier { level=50, basisPoints=new[]{15000,20000,20000,20000,18000,1900,3000,2000,100} }
            };
            public CurrencyProduct[] products = {
                new CurrencyProduct { amount=10000, priceWon=1100, mileageCoupons=0 },
                new CurrencyProduct { amount=70000, priceWon=5500, mileageCoupons=0 },
                new CurrencyProduct { amount=150000, priceWon=11000, mileageCoupons=0 },
                new CurrencyProduct { amount=500000, priceWon=33000, mileageCoupons=0 },
                new CurrencyProduct { amount=900000, priceWon=55000, mileageCoupons=1 },
                new CurrencyProduct { amount=2000000, priceWon=110000, mileageCoupons=2 }
            };
        }

        [Serializable]
        public sealed class CurrencyProduct { public int amount, priceWon, mileageCoupons; }
        [Serializable] public sealed class SummonRateTier { public int level; public int[] basisPoints; }
        public const int MaxSummonLevel=50, SummonWeightTotal=100000;

        [Serializable]
        public sealed class SummonState
        {
            public string category;
            public int level = 1, experience;
            public string freeUsedDay = "";
            public int freeUsedCount;
            public int tickets;
            public long lifetimeDraws;
        }

        readonly string[] commerceCategories = { "Armor", "Club", "Necklace", "Skill", "Companion", "Relic", "DungeonRelic" };
        readonly string[] commerceLabels = { "갑옷", "몽둥이", "목걸이", "스킬", "동료", "유물", "던전 유물" };
        readonly string[] commerceGrades = GradeNames;
        readonly Dictionary<string, SummonState> summonStates = new Dictionary<string, SummonState>();
        readonly System.Random commerceRandom = new System.Random();
        CommerceTuning commerceTuning = new CommerceTuning();
        int shopTab;

        void InitCommerce()
        {
            InitCommerceExtras();
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
                commerceTuning.levelExperience = new[] { 50, 100, 200, 400, 800, 1600, 3200, 6400, 10000 };
            for (int i = 0; i < commerceTuning.levelExperience.Length; i++)
                commerceTuning.levelExperience[i] = Mathf.Max(1, commerceTuning.levelExperience[i]);
            var rates=commerceTuning.rates;
            if(rates==null||rates.Length<2||rates[0].level!=1||rates[rates.Length-1].level!=MaxSummonLevel)
                throw new InvalidOperationException("Summon rate anchors must cover levels 1 through 50.");
            int priorLevel=0;
            foreach(var tier in rates)
            {
                if(tier.level<=priorLevel||tier.basisPoints==null||tier.basisPoints.Length!=GradeNames.Length)
                    throw new InvalidOperationException("Summon rate anchors must increase and contain all nine grades.");
                int total=0;foreach(int weight in tier.basisPoints){if(weight<0)throw new InvalidOperationException("Negative summon probability.");total+=weight;}
                if(total!=SummonWeightTotal)
                    throw new InvalidOperationException("Summon rates must total 100% at 0.001% precision.");
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
                state.tickets=Math.Max(0,state.tickets);state.lifetimeDraws=Math.Max(0,state.lifetimeDraws);
                if(!json.Contains("lifetimeDraws")&&!IsRelicSummon(category)) {
                    int[] oldThresholds={50,100,200,400,800,1000};
                    for(int prior=1;prior<Math.Min(state.level,31);prior++)state.lifetimeDraws+=oldThresholds[Math.Min(prior-1,oldThresholds.Length-1)];
                    state.lifetimeDraws+=Math.Max(0,state.experience);
                }
                state.level = IsRelicSummon(category)?0:Mathf.Clamp(state.level, 1, MaxSummonLevel);
                state.experience = IsRelicSummon(category) || state.level==MaxSummonLevel ? 0 : Mathf.Clamp(state.experience, 0, CommerceExperienceNeeded(state) - 1);
                summonStates[category] = state;
            }
        }

        void SaveCommerce()
        {
            PlayerPrefs.SetString(CommerceExtrasKey,JsonUtility.ToJson(commerceExtras));
            foreach (var state in summonStates.Values)
                PlayerPrefs.SetString("DoodleUi.Commerce." + state.category, JsonUtility.ToJson(state));
        }

        static bool IsRelicSummon(string category) => category=="Relic"||category=="DungeonRelic";
        static string SummonIcon(string category) => category=="Companion"?"CompanionMon_10":category=="Skill"?"SkillMeteor":category=="Relic"?"NavPottery":category=="DungeonRelic"?"DungeonPottery":category;

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
            if(IsRelicSummon(category))return new[]{SummonWeightTotal,0,0,0,0,0,0,0,0};
            var anchors=commerceTuning.rates;var lower=anchors[0];var upper=anchors[anchors.Length-1];
            foreach(var tier in anchors){if(tier.level<=level)lower=tier;if(tier.level>=level){upper=tier;break;}}
            double t=upper.level==lower.level?0:(level-lower.level)/(double)(upper.level-lower.level);
            var weights=new int[GradeNames.Length];int sum=0;
            for(int grade=0;grade<GradeNames.Length;grade++)
            {
                int value=(int)Math.Round(lower.basisPoints[grade]+(upper.basisPoints[grade]-lower.basisPoints[grade])*t);
                weights[grade]=value;sum+=value;
            }
            weights[0]+=SummonWeightTotal-sum;
            // Skills and companions stop at Transcendent. Equipment keeps God.
            if(category=="Skill" || category=="Companion") { weights[7]+=weights[8];weights[8]=0; }
            if(level<35 && (category=="Skill" || category=="Companion")) { weights[5]+=weights[6];weights[6]=0; }
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
        public int SummonTicketCost(string category, int count) => Math.Min(Math.Max(0, count), SummonTickets(category));
        public int SummonDiamondCost(string category, int count) => SummonCost(category, Math.Max(0, count) - SummonTicketCost(category, count));
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
            var currencyTab = UiKit.Button(tabs, "재화", () => { shopTab = 1; RefreshPage(); }, shopTab == 1 ? UiKit.Blue : UiKit.Paper, 64);
            CommerceButtonText(currencyTab, 35); Notify(currencyTab.transform, () => CanClaimFreeDiamonds);
            CommerceButtonText(UiKit.Button(tabs,"마일리지",()=>{shopTab=2;RefreshPage();},shopTab==2?UiKit.Purple:UiKit.Paper,64),32);
            if (window)
            {
                tabs.SetParent(window.inner, false);
                var responsive = window.inner.gameObject.AddComponent<DoodleCommerceLayout>();
                responsive.window = window; responsive.tabs = tabs;
                responsive.Reflow();
            }
            if (shopTab == 1) { BuildCurrencyProducts(body); return; }
            if(shopTab==2){BuildMileageShop(body);return;}
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
            var row = CommerceFramedRow(body, "Summon_" + category, category == "DungeonRelic" ? 264 : 330);
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
            if(category=="DungeonRelic") {
                var wallet=UiKit.Row(content,"Dungeon relic ticket balance",32,5);
                UiKit.Icon(wallet,"DungeonRelicTicket",32);
                UiKit.Text(wallet,"전용 뽑기권 "+UiNumber.Format(DungeonRelicTickets)+"장",22,TextAnchor.MiddleLeft,32);
            }
            BuildSummonButtons(content, category);
            if(category!="DungeonRelic")BuildTicketBalance(content,category);
        }

        void BuildSummonButtons(Transform parent, string category, bool result = false)
        {
            var actions = UiKit.Row(parent, "SummonActions", 68, 5);
            if(category=="DungeonRelic") {
                foreach(int baseCount in new[]{1,10,50}) {
                    int count = baseCount * (result ? summonResultMultiplier : 1);
                    var ticket=UiKit.Button(actions,count+"회 뽑기",()=>TrySummonDungeonRelicTickets(count),UiKit.Yellow,68);
                    ticket.name="DungeonRelicTicketSummon"+count;CommerceButtonText(ticket,22);ticket.interactable=DungeonRelicTickets>=count;
                    var label=ticket.GetComponentInChildren<Text>();
                    label.rectTransform.anchorMin=new Vector2(0,.44f);label.rectTransform.anchorMax=Vector2.one;
                    label.rectTransform.offsetMin=new Vector2(3,0);label.rectTransform.offsetMax=new Vector2(-3,-3);
                    var price=UiKit.Row(ticket.transform,"Dungeon relic ticket cost",26,3);
                    price.anchorMin=Vector2.zero;price.anchorMax=new Vector2(1,.44f);
                    price.offsetMin=new Vector2(3,2);price.offsetMax=new Vector2(-3,0);
                    UiKit.Icon(price,"DungeonRelicTicket",26);
                    var amount=UiKit.Text(price,count+"장",20,TextAnchor.MiddleCenter,26);
                    var width=amount.GetComponent<LayoutElement>();width.minWidth=width.preferredWidth=44;width.flexibleWidth=0;
                }
                return;
            }
            var free = UiKit.Button(actions, "무료 " + commerceTuning.freeCount + "회뽑기\n(" + FreeSummonsRemaining(category) + "/3)", () => TrySummon(category, commerceTuning.freeCount, true), result ? UiKit.Blue : UiKit.Green, 68);
            free.name = "무료 " + commerceTuning.freeCount + "회\n뽑기";
            CommerceButtonText(free, 24);
            free.interactable = CanFreeSummon(category);
            Notify(free.transform, () => CanFreeSummon(category));
            PaidSummonButton(actions, category, 10 * (result ? summonResultMultiplier : 1), UiKit.Yellow);
            PaidSummonButton(actions, category, 50 * (result ? summonResultMultiplier : 1), UiKit.Yellow);
        }

        void PaidSummonButton(Transform parent, string category, int count, Color color)
        {
            int tickets = SummonTicketCost(category, count), cost = SummonDiamondCost(category, count);
            var column = UiKit.Column(parent, "PaidSummon" + count, 0, 0);
            UiKit.Flexible(column);
            var button = UiKit.Button(column, count + "회 뽑기", () => TrySummon(category, count, false), color, 68);
            CommerceButtonText(button, 24);
            var label = button.GetComponentInChildren<Text>();
            label.rectTransform.anchorMin = new Vector2(0, .44f); label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(3, 0); label.rectTransform.offsetMax = new Vector2(-3, -3);
            var price = UiKit.Row(button.transform, "DiamondCost", 24, 2);
            price.anchorMin = new Vector2(0, 0); price.anchorMax = new Vector2(1, .44f);
            price.offsetMin = new Vector2(4, 3); price.offsetMax = new Vector2(-4, 0);
            void PricePart(string icon, string text, string name, float width) {
                UiKit.Icon(price, icon, 18);
                var amount = UiKit.Text(price, text, 18, TextAnchor.MiddleCenter, 24);
                amount.name = name; amount.resizeTextMinSize = 12;
                var size = amount.GetComponent<LayoutElement>();
                size.minWidth = 12; size.preferredWidth = width; size.flexibleWidth = 0;
            }
            if (tickets > 0) PricePart(TicketIcon(category), tickets + "장", "SummonTicketCost", 30);
            if (cost > 0 || tickets == 0) PricePart("Diamond", cost.ToString("N0", CultureInfo.InvariantCulture), "SummonDiamondCost", Math.Max(34, cost.ToString("N0", CultureInfo.InvariantCulture).Length * 11));
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
            if (!free && !ValidSummonCount(count)) return false;
            if (free && !CanFreeSummon(category)) { Toast("오늘 무료 뽑기를 모두 사용했어요."); return false; }
            int tickets = free ? 0 : SummonTicketCost(category, count);
            int cost = free ? 0 : SummonDiamondCost(category, count);
            if (Diamonds < cost) { Toast("다이아가 부족해요."); return false; }
            var rewards = RollSummonRewards(category, count, commerceRandom);
            Diamonds -= cost;
            var state = summonStates[category];
            if (category == "Relic") services.relicTickets -= tickets;
            else state.tickets -= tickets;
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
            if(!ValidSummonCount(count, true)||DungeonRelicTickets<count)return false;
            var rewards = RollSummonRewards("DungeonRelic", count, commerceRandom);
            if(!TrySpendDungeonRelicTickets(count))return false;
            long before=Power;CompleteSummon("DungeonRelic",rewards);NotifyPowerChanged(before,"던전 유물 획득");return true;
        }

        void CompleteSummon(string category, List<UiItem> rewards)
        {
            var state = summonStates[category];
            foreach (var item in rewards) AddItem(item, 1);
            state.lifetimeDraws=state.lifetimeDraws>long.MaxValue-rewards.Count?long.MaxValue:state.lifetimeDraws+rewards.Count;
            RecordServiceProgress("summon:"+category,rewards.Count);
            AdvanceSummonExperience(state, rewards.Count);
            RecordServiceProgress("summon", rewards.Count);
            Save();
            RefreshPage();
            ShowSummonResults(category, rewards);
        }

        void ShowSummonResults(string category, List<UiItem> rewards)
        {
            var counts = new Dictionary<string, int>(); var unique = new List<UiItem>();
            foreach (var item in rewards) { if (!counts.ContainsKey(item.id)) { counts[item.id] = 0; unique.Add(item); } counts[item.id]++; }
            var catalogOrder = Items(category);
            unique.Sort((a, b) => catalogOrder.IndexOf(a).CompareTo(catalogOrder.IndexOf(b)));
            bool animateWindow = fullscreenTitle != "뽑기 결과" && !SkipSummonAnimations;
            ShowFullscreen("뽑기 결과", body =>
            {
                var subtitle = UiKit.Text(body, CommerceLabel(category) + " " + rewards.Count + "회 뽑기", 38, TextAnchor.MiddleCenter, 58);
                var grid = UiKit.Grid(body, "SummonResultCards", 5, 150);
                UiKit.PortraitGrid(grid);
                for (int i = 0; i < unique.Count; i++)
                {
                    var item = unique[i];
                    var slot = UiKit.Slot(grid, item.name, item.icon, item.rarity, item.count, CopiesNeeded(item), item.equipped, false,
                        () => ShowSummonItem(item), 150);
                    var layout = slot.GetComponent<DoodleUiSlotLayout>();
                    layout.grade.text = SummonGradeLabel(item);
                    layout.gauge.name = "Summon quantity";
                    layout.gauge.GetComponent<Image>().enabled = false;
                    layout.gauge.GetComponent<Outline>().enabled = false;
                    layout.gauge.Find("Fill").gameObject.SetActive(false);
                    var amount = layout.gauge.GetComponentInChildren<Text>(); amount.name = "Draw quantity";
                    amount.text = "×" + counts[item.id].ToString("N0", CultureInfo.InvariantCulture);
                    layout.Invalidate();
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
                var options = UiKit.Row(footer, "Summon result options", 48, 12);
                var skip = UiKit.Button(options, "연출 스킵", () => {
                    SkipSummonAnimations = !SkipSummonAnimations;
                    skipMotion.Slide(SkipSummonAnimations ? 1 : 0);
                    skipTrack.color=SkipSummonAnimations ? UiKit.Green : new Color(.68f,.68f,.68f);
                    if (SkipSummonAnimations) { if(reveal) reveal.Complete(); if(celebration) celebration.Finish(); }
                }, Color.clear, 44);
                CollectionWidth(skip.transform, 188);
                skip.name = "Summon animation skip"; skip.GetComponent<Outline>().enabled=false; skip.transition=Selectable.Transition.None;
                var skipLabel=skip.GetComponentInChildren<Text>();
                skipLabel.rectTransform.anchorMin=new Vector2(0,0);skipLabel.rectTransform.anchorMax=new Vector2(.60f,1);
                var track=UiKit.Box(skip.transform,"Skip toggle track",SkipSummonAnimations?UiKit.Green:new Color(.68f,.68f,.68f));
                track.anchorMin=track.anchorMax=new Vector2(.80f,.5f);track.sizeDelta=new Vector2(62,36);track.anchoredPosition=Vector2.zero;
                skipTrack=track.GetComponent<Image>();skipTrack.raycastTarget=false;
                var knob=UiKit.Box(track,"Toggle knob",Color.white);knob.GetComponent<Image>().sprite=UiKit.Circle;knob.GetComponent<Image>().type=Image.Type.Simple;knob.GetComponent<Image>().raycastTarget=false;knob.sizeDelta=Vector2.one*30;
                skipMotion=track.gameObject.AddComponent<DoodleSlidingSelection>();skipMotion.Configure(knob,SkipSummonAnimations?1:0,SkipSummonAnimations?1:0,null,true);
                var multipliers = UiKit.Row(options, "Summon multipliers", 48, 4);
                UiKit.Flexible(multipliers);
                var multiplierButtons = new List<Button>();
                foreach (int multiplier in summonMultipliers) {
                    var choice = UiKit.Button(multipliers, "×" + multiplier, () => {
                        summonResultMultiplier = multiplier;
                        for (int j = 0; j < multiplierButtons.Count; j++) multiplierButtons[j].GetComponent<Image>().color = summonMultipliers[j] == multiplier ? UiKit.Yellow : UiKit.Paper;
                        var old = footer.Find("SummonActions"); int position = old.GetSiblingIndex();
                        old.name = "Previous summon actions"; old.gameObject.SetActive(false); Destroy(old.gameObject);
                        BuildSummonButtons(footer, category, true);
                        var actions = footer.Find("SummonActions") as RectTransform; actions.SetSiblingIndex(position);
                        var responsive = footer.GetComponentInParent<DoodleUiWindow>().inner.GetComponent<DoodleCommerceLayout>();
                        responsive.actions = actions; responsive.Reflow();
                    }, summonResultMultiplier == multiplier ? UiKit.Yellow : UiKit.Paper, 48);
                    choice.name = "Summon multiplier " + multiplier; CommerceButtonText(choice, 21); multiplierButtons.Add(choice);
                }
                BuildSummonButtons(footer, category, true);
                var confirmRow = UiKit.Row(footer, "Summon confirmation", 58);
                var confirm = UiKit.Button(confirmRow, "확인", () => { CloseFullscreen(); RefreshPage(); }, UiKit.Yellow, 58);
                var window = body.GetComponentInParent<DoodleUiWindow>();
                if (window)
                {
                    var wallet = UiKit.Row(window.inner, "Summon result wallet", 44, 12);
                    var tickets = UiKit.Row(wallet, "Summon result tickets", 44, 5);
                    UiKit.Icon(tickets, TicketIcon(category), 38);
                    var ticketAmount = UiKit.Text(tickets, "", 25, TextAnchor.MiddleLeft, 44);
                    var diamonds = UiKit.Row(wallet, "Summon result diamonds", 44, 5);
                    UiKit.Icon(diamonds, "Diamond", 34);
                    var diamondAmount = UiKit.Text(diamonds, "", 25, TextAnchor.MiddleLeft, 44);
                    wallet.gameObject.AddComponent<DoodleSummonWallet>().Configure(this, category, ticketAmount, diamondAmount);
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
                    responsive.window = window; responsive.resultGrid = grid.GetComponent<GridLayoutGroup>(); responsive.wallet = wallet;
                    responsive.resultCount = unique.Count; responsive.subtitle = subtitle; responsive.crest = crest.rectTransform;
                    responsive.summary = summary; responsive.summaryIcon = summaryIcon.rectTransform; responsive.level = level;
                    responsive.summaryText = summaryText; responsive.experience = experience;
                    responsive.noSummonProgress=relic; responsive.skipButton=skip; responsive.options = options;
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
                UiKit.Text(body, "현재 보유 수량 " + UiNumber.Format(item.count) + " · Lv. " + item.level.ToString("N0"), 24, TextAnchor.MiddleCenter, 46);
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
            if(item.category=="Relic")return 100d/items.Count;
            var choices=items.FindAll(x=>x.rarity==item.rarity);int index=choices.IndexOf(item);
            return index<0?0:SummonWeights(item.category,level)[item.rarity]/1000d*SummonTierWeight(index,choices.Count)/SummonTierWeightTotal(choices.Count);
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
                for (int grade = 0; grade < GradeNames.Length; grade++) {
                    if (!Items(category).Exists(x => x.rarity == grade)) continue;
                    var row = CommerceFramedRow(body, "Probability_grade_" + grade, 64);
                    row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 5, 5);
                    var badge = UiKit.Box(row, "Grade color", UiKit.Rarity(grade), 40); FixedWidth(badge, 40);
                    UiKit.Text(row, relic ? "유물" : UiKit.GradeName(grade), 27, TextAnchor.MiddleLeft, 48);
                    var rate = UiKit.Text(row, (relic ? 100d : weights[grade] / 1000d).ToString("0.###", CultureInfo.InvariantCulture) + "%", 26, TextAnchor.MiddleRight, 48);
                    rate.name = "Grade probability rate";
                    FixedWidth(rate.transform, 100);
                }
                UiKit.Text(body, relic ? "모든 유물은 같은 확률로 등장합니다.\n각 " + (100d / Items(category).Count).ToString("0.##", CultureInfo.InvariantCulture) + "%" : "같은 등급 내 순서별 가중치 10:9:8:7:6\n해당 등급 아이템 수에 맞춰 나눕니다.", 20, TextAnchor.MiddleCenter, 60);
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
            BuildFreeDiamondCard(grid);
            int productIndex = 0;
            foreach (var product in commerceTuning.products)
            {
                if (product == null || product.amount <= 0 || product.priceWon <= 0) continue;
                var card = UiKit.Box(grid, "CurrencyProduct" + product.amount, new Color(1, .977f, .895f));
                var amount = UiKit.Text(card, product.amount.ToString("N0"), 36, TextAnchor.MiddleCenter, 48).rectTransform;
                amount.anchorMin = new Vector2(0, .81f); amount.anchorMax = new Vector2(1, .97f); amount.offsetMin = new Vector2(8, 0); amount.offsetMax = new Vector2(-8, 0);
                var art = UiKit.Icon(card, productArt[Mathf.Min(productIndex++, productArt.Length - 1)], 150).rectTransform;
                art.anchorMin = new Vector2(.1f, .24f); art.anchorMax = new Vector2(.9f, .79f); art.offsetMin = art.offsetMax = Vector2.zero;
                if(product.mileageCoupons>0) {
                    art.anchorMin=new Vector2(.1f,.36f);
                    var coupon=UiKit.Row(card,"Mileage bonus",36,4);coupon.anchorMin=new Vector2(.07f,.23f);coupon.anchorMax=new Vector2(.93f,.36f);coupon.offsetMin=coupon.offsetMax=Vector2.zero;
                    UiKit.Icon(coupon,"MileageCoupon",32);UiKit.Text(coupon,"쿠폰 "+product.mileageCoupons+"개 추가",21,TextAnchor.MiddleCenter,32);
                }
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
        public RectTransform wallet, options;
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
                float header = Mathf.Lerp(Mathf.Max(82, window.headerHeight), 335, tall) + 52;
                if (wallet) PlaceTop(wallet, 28, new Vector2(Mathf.Min(620, window.inner.rect.width - 32), 44));
                float footerGap = Mathf.Lerp(8, 26, tall);
                float summaryHeight = Mathf.Lerp(46, 84, tall);
                float gaugeHeight = noSummonProgress?0:Mathf.Lerp(32, 36, tall);
                float actionHeight = Mathf.Lerp(68, 106, tall);
                float confirmHeight = Mathf.Lerp(58, 88, tall);
                float skipHeight=48;
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
                if (options) UiKit.Height(options, skipHeight);
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
                float bannerCenter = Mathf.Lerp(43, 239, tall) + 52, bannerHeight = Mathf.Lerp(62, 132, tall);
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
