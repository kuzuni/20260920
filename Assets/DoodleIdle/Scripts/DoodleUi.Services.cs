using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        // Local adapters deliberately keep server-owned operations visibly separate.
        // Reward values and goals are editable in Resources/DoodleIdle/UI/ServicesTuning.json.
        [Serializable] public sealed class ServiceTuning
        {
            public int[] attendance = { 100, 150, 200, 250, 300, 400, 700 };
            public int[] roulette = { 20, 50, 100, 30, 200, 50, 500, 100 };
            public int dailySpins = 5, dungeonAttempts = 3, pvpAttempts = 5;
            public int buffSeconds = 900, buffPrice = 20, dungeonKills = 30;
            public int mainStageKills = 100, relicDungeonKills = 50;
            public int dungeonRelicTickets = 10;
            public int goldDungeonEnemyCount = 500;
            public float goldPerEnemy = 10, goldStageGrowth = 0;
            public float enemyHealthStageGrowth = .02f, enemyDamageStageGrowth = .0045f;
            public float enemyHealthAttackRatio=20.75f,projectedGoldMultiplier=1.65f,projectedMissionGoldPerStage=1100;
            public float goldBuff = .5f, attackBuff = .3f;
            public int[] dailyGoals = { 200, 1000, 3, 5 };
            public int[] repeatGoals = { 500, 10, 5, 5000 };
            public int[] weeklyGoals = { 5000, 15, 10, 100 };
            public int[] questRewards = { 500, 500, 500, 500 };
        }

        [Serializable] sealed class ServiceState
        {
            public string day = "", week = "", attendanceDay = "";
            public int attendanceIndex, spins, pvpUsed, pvpPoints = 1240;
            public int[] dungeonUsed = new int[3];
            public int[] daily = new int[8], weekly = new int[8], repeat = new int[8];
            public bool[] dailyClaimed = new bool[4], weeklyClaimed = new bool[4];
            public long goldExpiry, attackExpiry;
            public bool powerSaving;
            public float music = .6f, effects = .8f;
            public int activeDungeon = -1, dungeonProgress;
            public int mainStage, mainStageKillProgress, mainMissionIndex, relicTickets, dungeonRelicTickets;
            public bool breakthroughMode = true;
            public int[] dungeonStages = new int[3];
            public long mainKills, earnedGold;
            public int highestMainStage,missionVersion;
            public List<CareerCounter> career=new List<CareerCounter>();
        }

        sealed class ServiceBinding
        {
            public Text text;
            public Func<string> value;
            public Image fill;
            public Func<float> fraction;
            public Action refresh;
        }

        sealed class LocalRank
        {
            public string name, art;
            public long power;
            public int points;
            public bool self;
        }

        sealed class LocalMessage
        {
            public string author, art, content;
            public bool self;
        }

        const string ServicesSaveKey = "DoodleUi.Services.v1";
        static readonly string[] ServiceMetrics = { "kills", "gold", "dungeon", "roulette", "equipmentUpgrade", "skillUpgrade", "pvp", "summon" };
        static readonly int[][] QuestMetrics = { new[] { 0, 1, 2, 3 }, new[] { 0, 4, 5, 1 }, new[] { 0, 2, 6, 7 } };
        // Index 1 is a retired save slot; preserve indices of earned relic cave progress.
        static readonly string[] DungeonNames = { "골드 동굴", "", "유물 동굴" };
        readonly System.Random serviceRandom = new System.Random();
        readonly List<ServiceBinding> serviceBindings = new List<ServiceBinding>();
        readonly List<LocalMessage> localMessages = new List<LocalMessage>();
        ServiceState services;
        ServiceTuning serviceTuning = new ServiceTuning();
        int lastServiceKills, questTab;
        float nextServiceTick, nextServiceSave;
        bool rouletteSpinning;

        static long ServiceNow => DateTime.UtcNow.Ticks;
        public int GoldBuffSeconds => services == null ? 0 : SecondsUntil(services.goldExpiry);
        public int AttackBuffSeconds => services == null ? 0 : SecondsUntil(services.attackExpiry);
        public float GoldBuffMultiplier => GoldBuffSeconds > 0 ? 1 + serviceTuning.goldBuff : 1;
        public float AttackBuffMultiplier => AttackBuffSeconds > 0 ? 1 + serviceTuning.attackBuff : 1;
        public int ActiveDungeonIndex => services == null ? -1 : services.activeDungeon;
        public int DungeonProgress => services == null ? 0 : services.dungeonProgress;
        public int DungeonKillGoal => DungeonKillsFor(ActiveDungeonIndex);
        public string DungeonMission => ActiveDungeonIndex < 0 ? "" : DungeonNames[ActiveDungeonIndex] + " " + DungeonChallengeStage(ActiveDungeonIndex) + "단계\n" + UiNumber.Format(DungeonProgress) + "/" + UiNumber.Format(DungeonKillGoal);
        static int SecondsUntil(long ticks) => (int)Math.Max(0, Math.Min(int.MaxValue, Math.Ceiling((ticks - ServiceNow) / (double)TimeSpan.TicksPerSecond)));
        static string ServiceClock(int seconds) => (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");

        void InitServices()
        {
            var tuningAsset = Resources.Load<TextAsset>("DoodleIdle/UI/ServicesTuning");
            if (tuningAsset) JsonUtility.FromJsonOverwrite(tuningAsset.text, serviceTuning);
            serviceTuning.buffSeconds = Mathf.Max(1, serviceTuning.buffSeconds);
            serviceTuning.dungeonKills = Mathf.Max(1, serviceTuning.dungeonKills);
            serviceTuning.mainStageKills = Mathf.Max(1, serviceTuning.mainStageKills);
            serviceTuning.relicDungeonKills = Mathf.Max(1, serviceTuning.relicDungeonKills);
            services = new ServiceState();
            string json = PlayerPrefs.GetString(ServicesSaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, services); }
                catch (ArgumentException) { services = new ServiceState(); }
            }
            if (services.dungeonUsed == null || services.dungeonUsed.Length != 3) services.dungeonUsed = new int[3];
            if (services.dungeonStages == null || services.dungeonStages.Length != 3) services.dungeonStages = new int[3];
            services.mainStage = Math.Max(0, services.mainStage);
            if (collectionTuning != null) NormalizeEquipment("Skill");
            services.mainStageKillProgress = Mathf.Clamp(services.mainStageKillProgress, 0, MainStageKillGoal);
            services.mainMissionIndex = Math.Max(0, services.mainMissionIndex);
            services.relicTickets = Math.Max(0, services.relicTickets);
            services.dungeonRelicTickets = Math.Max(0, services.dungeonRelicTickets);
            services.mainKills = Math.Max(0, services.mainKills);
            services.earnedGold = Math.Max(0, services.earnedGold);
            for (int i = 0; i < services.dungeonStages.Length; i++) services.dungeonStages[i] = Math.Max(0, services.dungeonStages[i]);
            if (services.daily == null || services.daily.Length != 8) services.daily = new int[8];
            if (services.weekly == null || services.weekly.Length != 8) services.weekly = new int[8];
            if (services.repeat == null || services.repeat.Length != 8) services.repeat = new int[8];
            if (services.dailyClaimed == null || services.dailyClaimed.Length != 4) services.dailyClaimed = new bool[4];
            if (services.weeklyClaimed == null || services.weeklyClaimed.Length != 4) services.weeklyClaimed = new bool[4];
            services.attendanceIndex = Mathf.Clamp(services.attendanceIndex, 0, 7);
            services.activeDungeon = Mathf.Clamp(services.activeDungeon, -1, 2);
            if (services.activeDungeon == 1) { services.activeDungeon = -1; services.dungeonProgress = 0; }
            InitMissionHistory();
            ResetServicePeriods();
            lastServiceKills = game ? game.Kills : 0;
            Application.targetFrameRate = services.powerSaving ? 30 : 60;
            ApplyServiceAudioSettings();
            localMessages.Add(new LocalMessage { author = "구름발 · 예시", art = "StormCloud", content = "안녕하세요! 이 화면은 로컬 채팅 예시입니다." });
            localMessages.Add(new LocalMessage { author = "내 메시지 · 예시", art = "Player", content = "안녕하세요!", self = true });
            localMessages.Add(new LocalMessage { author = "버섯대장 · 예시", art = "MushroomA", content = "입력한 메시지는 이 기기에서만 표시돼요." });
            localMessages.Add(new LocalMessage { author = "내 메시지 · 예시", art = "Player", content = "직접 입력해 볼게요.", self = true });
            SaveServices();
        }

        void SaveServices()
        {
            if (services != null) PlayerPrefs.SetString(ServicesSaveKey, JsonUtility.ToJson(services));
        }

        bool ResetServicePeriods()
        {
            DateTime now = DateTime.UtcNow;
            string day = now.ToString("yyyy-MM-dd");
            string week = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd");
            bool changed = false;
            // UTC boundary, not device locale. Attendance does not reset if a day is missed.
            if (string.CompareOrdinal(day, services.day) > 0)
            {
                services.day = day; services.spins = 0; services.pvpUsed = 0;
                Array.Clear(services.dungeonUsed, 0, 3);
                Array.Clear(services.daily, 0, 8); Array.Clear(services.dailyClaimed, 0, 4);
                if (services.attendanceIndex == 7 && services.attendanceDay != day) services.attendanceIndex = 0;
                changed = true;
            }
            if (string.CompareOrdinal(week, services.week) > 0)
            {
                services.week = week; Array.Clear(services.weekly, 0, 8); Array.Clear(services.weeklyClaimed, 0, 4); changed = true;
            }
            return changed;
        }

        void TickServices()
        {
            if (services == null) return;
            int kills = game ? game.Kills : lastServiceKills;
            int delta = Math.Max(0, kills - lastServiceKills);
            lastServiceKills = kills;
            if (delta > 0)
            {
                RecordServiceProgress("kills", delta);
                if (services.activeDungeon >= 0)
                {
                    services.dungeonProgress += delta;
                    if (services.dungeonProgress >= DungeonKillGoal) CompleteDungeon();
                }
                // Persist the whole frame's kill progress, including kills after a boss changed the stage.
                SaveServices();
            }
            if (Time.unscaledTime < nextServiceTick) return;
            nextServiceTick = Time.unscaledTime + .25f;
            if (ResetServicePeriods()) { Save(); if (!string.IsNullOrEmpty(ActivePage)) RefreshPage(); }
            for (int i = serviceBindings.Count - 1; i >= 0; i--)
            {
                var binding = serviceBindings[i];
                if (!binding.text) { serviceBindings.RemoveAt(i); continue; }
                binding.text.text = binding.value();
                binding.refresh?.Invoke();
                if (binding.fill && binding.fraction != null) binding.fill.fillAmount = Mathf.Clamp01(binding.fraction());
            }
            if (Time.unscaledTime >= nextServiceSave)
            {
                nextServiceSave = Time.unscaledTime + 15;
                SaveServices(); PlayerPrefs.Save();
                Application.targetFrameRate = services.powerSaving ? 30 : 60;
            }
        }

        public void RecordServiceProgress(string metric, int amount)
        {
            if (services == null || amount <= 0) return;
            RecordMissionAction(metric,amount);
            if (metric == "gold") services.earnedGold = SaturatingAdd(services.earnedGold, amount);
            ResetServicePeriods();
            int index = Array.IndexOf(ServiceMetrics, metric);
            if (index < 0) return;
            services.daily[index] = (int)Math.Min(int.MaxValue, (long)services.daily[index] + amount);
            services.weekly[index] = (int)Math.Min(int.MaxValue, (long)services.weekly[index] + amount);
            services.repeat[index] = (int)Math.Min(int.MaxValue, (long)services.repeat[index] + amount);
        }

        Text ServiceText(Transform parent, Func<string> value, int size = 22, float height = 34)
        {
            var text = UiKit.Text(parent, value(), size, TextAnchor.MiddleCenter, height);
            serviceBindings.Add(new ServiceBinding { text = text, value = value });
            return text;
        }

        RectTransform ServiceCard(Transform parent, string name, Color color)
        {
            var box = UiKit.Box(parent, name, color);
            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10); layout.spacing = 7;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return box;
        }

        void BuildAttendance(RectTransform body)
        {
            UiKit.Text(body, "7일 출석", 27, TextAnchor.MiddleCenter, 38);
            UiKit.Text(body, "매일 UTC 00:00 초기화", 18, TextAnchor.MiddleCenter, 26);
            var grid = UiKit.Grid(body, "Attendance days", 3, 195);
            for (int i = 0; i < 6; i++) AttendanceCard(grid, i);
            AttendanceCard(body, 6);
        }

        void AttendanceCard(Transform parent, int index)
        {
            bool claimed = index < services.attendanceIndex;
            bool current = index == services.attendanceIndex;
            var card = UiKit.Box(parent, "Attendance day " + (index + 1), claimed ? new Color(.91f,.96f,.85f) : current ? new Color(1,.97f,.83f) : new Color(.93f,.93f,.93f), index == 6 ? 182 : 195);
            var claim = card.gameObject.AddComponent<Button>();
            claim.targetGraphic = card.GetComponent<Image>();
            claim.transition = Selectable.Transition.None;
            claim.interactable = current && services.attendanceDay != services.day;
            Notify(card,()=>index==services.attendanceIndex&&CanClaimAttendance);
            claim.onClick.AddListener(() => {
                ResetServicePeriods();
                if (index == services.attendanceIndex) ClaimAttendance();
            });
            var day = UiKit.Text(card, (index + 1) + "일차", 27, TextAnchor.MiddleCenter, 36);
            day.rectTransform.anchorMin = new Vector2(0,1); day.rectTransform.anchorMax = Vector2.one; day.rectTransform.pivot = new Vector2(.5f,1); day.rectTransform.anchoredPosition = new Vector2(0,-8); day.rectTransform.sizeDelta = new Vector2(-12,36);
            var gem = UiKit.Icon(card, "Diamond", index == 6 ? 66 : 70).rectTransform;
            gem.anchorMin = gem.anchorMax = new Vector2(.5f,.57f); gem.anchoredPosition = Vector2.zero;
            if(index == 6) {
                gem.anchoredPosition = new Vector2(0,1);
                foreach(float side in new[]{-1f,1f}) { var extra = UiKit.Icon(card,"Diamond",50).rectTransform; extra.anchorMin=extra.anchorMax=new Vector2(.5f,.57f);extra.anchoredPosition=new Vector2(side*44,-5);extra.localRotation=Quaternion.Euler(0,0,side*-17); }
                gem.SetAsLastSibling();
            }
            var amount = UiKit.Text(card, UiNumber.Format(serviceTuning.attendance[index]), 25, TextAnchor.MiddleCenter, 32);
            amount.rectTransform.anchorMin = new Vector2(0,0); amount.rectTransform.anchorMax = new Vector2(1,0); amount.rectTransform.pivot = new Vector2(.5f,0); amount.rectTransform.anchoredPosition = new Vector2(0,40); amount.rectTransform.sizeDelta = new Vector2(-8,32);
            var strip = UiKit.Box(card,"Attendance status",claimed ? new Color(.77f,.88f,.66f) : current ? UiKit.Yellow : new Color(.82f,.82f,.82f));
            strip.anchorMin=Vector2.zero;strip.anchorMax=new Vector2(1,0);strip.pivot=new Vector2(.5f,0);strip.anchoredPosition=new Vector2(0,3);strip.sizeDelta=new Vector2(-6,36);strip.GetComponent<Outline>().enabled=false;
            var status = UiKit.Text(strip, claimed ? "✓ 받음" : current && services.attendanceDay != services.day ? "눌러서 받기" : "대기 중", 24, TextAnchor.MiddleCenter, 36); UiKit.Stretch(status.rectTransform,3,1,3,1);
        }

        public void ClaimAttendance()
        {
            ResetServicePeriods();
            if (services.attendanceDay == services.day || services.attendanceIndex >= 7) return;
            int amount = serviceTuning.attendance[services.attendanceIndex++];
            services.attendanceDay = services.day;
            RecordMissionAction("attendance");
            GrantServiceDiamonds(amount, "출석 보상 획득!");
        }

        void GrantServiceDiamonds(int amount, string title)
        {
            Diamonds += amount; Save(); RefreshPage();
            ShowRewards(title, new List<UiReward> { new UiReward { name = "", icon = "Diamond", amount = amount, rarity = 0 } });
        }

        void BuildRoulette(RectTransform body)
        {
            DoodleRouletteLayout responsive = null;
            var countBadge=UiKit.Box(body,"Roulette attempts",new Color(1,.97f,.87f),54);
            var remaining = ServiceText(countBadge, () => "오늘 남은 횟수 " + Math.Max(0, serviceTuning.dailySpins - services.spins) + "/" + serviceTuning.dailySpins + (responsive && responsive.Compact ? " · 각 칸 12.5%" : ""), 29, 54);UiKit.Stretch(remaining.rectTransform,6,2,6,2);
            var odds = UiKit.Text(body, "하루 5회 · 각 칸 확률 12.5%", 18, TextAnchor.MiddleCenter, 28);
            var holder = new GameObject("Roulette area", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            holder.SetParent(body, false); UiKit.Height(holder, 340);
            var wheel = new GameObject("Roulette wheel", typeof(RectTransform), typeof(CanvasRenderer), typeof(DoodleRouletteGraphic)).GetComponent<RectTransform>();
            wheel.SetParent(holder, false); wheel.anchorMin = wheel.anchorMax = new Vector2(.5f, .5f); wheel.sizeDelta = new Vector2(480, 480);
            wheel.GetComponent<DoodleRouletteGraphic>().raycastTarget = false;
            for (int i = 0; i < serviceTuning.roulette.Length; i++)
            {
                float angle = (90 - (i + .5f) * 45) * Mathf.Deg2Rad;
                var reward = new GameObject("Wheel reward " + i, typeof(RectTransform)).GetComponent<RectTransform>();
                reward.SetParent(wheel, false); reward.anchorMin = reward.anchorMax = Vector2.one * .5f; reward.sizeDelta = new Vector2(86, 82);
                reward.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 169;
                var icon = UiKit.Icon(reward, "Diamond", 54).rectTransform;
                icon.anchorMin = icon.anchorMax = new Vector2(.5f, .7f); icon.anchoredPosition = Vector2.zero;
                var number = UiKit.Text(reward, UiNumber.Format(serviceTuning.roulette[i]), 27, TextAnchor.MiddleCenter, 32).rectTransform;
                number.anchorMin = new Vector2(0, 0); number.anchorMax = new Vector2(1, .38f); number.offsetMin = number.offsetMax = Vector2.zero;
            }
            var pointer = new GameObject("Roulette pointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(DoodleRoulettePointer)).GetComponent<RectTransform>();
            pointer.SetParent(holder, false);
            pointer.anchorMin = pointer.anchorMax = new Vector2(.5f, 1); pointer.pivot = new Vector2(.5f, 1); pointer.sizeDelta = new Vector2(45, 45); pointer.anchoredPosition = Vector2.zero;
            pointer.GetComponent<DoodleRoulettePointer>().raycastTarget = false;
            var paw = ServiceSymbol(wheel,"Paw",52); paw.anchorMin=paw.anchorMax=Vector2.one*.5f;paw.anchoredPosition=Vector2.zero;
            var spin = UiKit.Button(body, rouletteSpinning ? "돌리는 중" : "돌리기", () => StartCoroutine(SpinRoulette(wheel)), UiKit.Blue, 90);
            spin.interactable = !rouletteSpinning && services.spins < serviceTuning.dailySpins;
            Notify(spin.transform,()=>CanSpinRoulette);
            responsive = holder.gameObject.AddComponent<DoodleRouletteLayout>();
            responsive.viewport = body.parent as RectTransform; responsive.body = body; responsive.wheel = wheel; responsive.pointer = pointer;
            responsive.remaining = remaining; responsive.counter=countBadge; responsive.odds = odds; responsive.spin = spin; responsive.Reflow();
        }

        IEnumerator SpinRoulette(RectTransform wheel)
        {
            ResetServicePeriods();
            if (rouletteSpinning || services.spins >= serviceTuning.dailySpins) yield break;
            rouletteSpinning = true; services.spins++; RecordServiceProgress("roulette", 1);
            int index = serviceRandom.Next(serviceTuning.roulette.Length);
            int amount = serviceTuning.roulette[index];
            Diamonds += amount; Save(); // Commit reward with attempt before animation; quitting cannot lose the reward.
            float elapsed = 0, angle = 1080 + (index + .5f) * 45;
            while (elapsed < 1.6f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (wheel) wheel.localRotation = Quaternion.Euler(0, 0, angle * (1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / 1.6f), 3)));
                yield return null;
            }
            rouletteSpinning = false; RefreshPage();
            ShowRewards("룰렛 보상 획득!", new List<UiReward> { new UiReward { icon = "Diamond", name = "", amount = amount, rarity = 0 } });
        }

        void BuildBuffs(RectTransform body)
        {
            BuildBuffCard(body, false); BuildBuffCard(body, true);
            UiKit.Text(body, "활성 버프는 메인 화면에서도 확인", 19, TextAnchor.MiddleCenter, 30);
        }

        void BuildBuffCard(Transform body, bool attack)
        {
            var card = ServiceCard(body, attack ? "Attack buff" : "Gold buff", UiKit.Paper);
            var row = UiKit.Row(card, "Buff", 183,16);
            UiKit.Icon(row, attack ? "Club" : "Gold", 136);
            var description = UiKit.Column(row, "Buff description", 4, 0);
            UiKit.Text(description, attack ? "공격력 버프" : "골드 버프", 34, TextAnchor.MiddleLeft, 47);
            UiKit.Text(description, (attack ? "공격력 +" : "골드 획득 +") + ((attack ? serviceTuning.attackBuff : serviceTuning.goldBuff) * 100).ToString("0") + "%", 27, TextAnchor.MiddleLeft, 38);
            var badge=UiKit.Box(description,"Buff status",(attack ? AttackBuffSeconds : GoldBuffSeconds)>0?UiKit.Green:new Color(.88f,.88f,.88f),34); badge.GetComponent<Outline>().enabled=false;
            var status=ServiceText(badge,()=> (attack ? AttackBuffSeconds : GoldBuffSeconds)>0?"활성화 중":"비활성",24,34);UiKit.Stretch(status.rectTransform);
            ServiceText(description, () => "남은 시간 " + ServiceClock(attack ? AttackBuffSeconds : GoldBuffSeconds), 24, 34).alignment=TextAnchor.MiddleLeft;
            ServiceGauge(card, () => attack ? AttackBuffSeconds : GoldBuffSeconds, () => serviceTuning.buffSeconds, true, true);
            var activate = UiKit.Button(card, "버프 활성화", () => ExtendBuff(attack), UiKit.Blue, 60);
            Notify(activate.transform,()=> (attack?AttackBuffSeconds:GoldBuffSeconds)==0);
            var activateText = activate.GetComponentInChildren<Text>();
            Func<string> caption = () => (attack ? AttackBuffSeconds : GoldBuffSeconds) > 0 ? "활성화 중" : "버프 활성화";
            Action refresh = () =>
            {
                bool active = (attack ? AttackBuffSeconds : GoldBuffSeconds) > 0;
                activate.interactable = !active;
                badge.GetComponent<Image>().color = active ? UiKit.Green : new Color(.88f,.88f,.88f);
            };
            serviceBindings.Add(new ServiceBinding { text = activateText, value = caption, refresh = refresh });
            activateText.text = caption(); refresh();
            UiKit.Text(card, "다이아 " + UiNumber.Format(serviceTuning.buffPrice) + " · " + (serviceTuning.buffSeconds / 60) + "분", 18, TextAnchor.MiddleCenter, 24);
        }

        public void ExtendBuff(bool attack)
        {
            // Keep the public entry point for existing callers, but an active buff cannot be extended.
            long now = ServiceNow;
            if ((attack ? services.attackExpiry : services.goldExpiry) > now) return;
            if (Diamonds < serviceTuning.buffPrice) { Toast("다이아가 부족합니다"); return; }
            long expiry = now + TimeSpan.FromSeconds(serviceTuning.buffSeconds).Ticks;
            if (attack) services.attackExpiry = expiry; else services.goldExpiry = expiry;
            RecordMissionAction("buff");Diamonds -= serviceTuning.buffPrice; Save(); RefreshPage();
        }

        void ServiceGauge(Transform parent, Func<int> current, Func<int> maximum, bool clock = false, bool separate = false)
        {
            var frame = UiKit.Box(parent, "Live progress gauge", new Color(.82f, .82f, .79f), separate ? 19 : 26);
            var fill = new GameObject("Progress fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(frame, false); fill.color = UiKit.Green; fill.raycastTarget = false;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            // Filled Image requires a sprite. WhiteTexture provides a plain stretchable fill.
            fill.sprite = ServiceSolidSprite;
            var rect = fill.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * 3; rect.offsetMax = Vector2.one * -3;
            var label = UiKit.Text(separate && !clock ? parent : frame, "", separate ? 22 : 18, separate ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, separate ? 27 : 26);
            if(!separate || clock) UiKit.Stretch(label.rectTransform);
            Func<string> value = () => clock ? (separate ? "" : ServiceClock(current())) : UiNumber.Format(Math.Min(current(), maximum())) + "/" + UiNumber.Format(maximum());
            Func<float> fraction = () => current() / (float)Math.Max(1, maximum());
            label.text = value(); fill.fillAmount = Mathf.Clamp01(fraction());
            serviceBindings.Add(new ServiceBinding { text = label, value = value, fill = fill, fraction = fraction });
        }

        static Sprite serviceSolidSprite;
        static Sprite ServiceSolidSprite
        {
            get
            {
                if (!serviceSolidSprite) serviceSolidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f);
                return serviceSolidSprite;
            }
        }

        void BuildQuests(RectTransform body)
        {
            ServiceText(body, QuestResetLabel, 20, 34);
            var tabs = UiKit.Row(body, "Quest tabs", 60);
            string[] names = { "일일", "반복", "주간" };
            for (int i = 0; i < 3; i++) { int tab = i; var button=UiKit.Button(tabs, names[i], () => { questTab = tab; RefreshPage(); }, questTab == i ? UiKit.Green : new Color(.92f,.92f,.92f),60); Notify(button.transform,()=>QuestTabHasReward(tab)); }
            for (int i = 0; i < 4; i++) QuestCard(body, i);
            var bulk=UiKit.Button(body, "일괄받기", () => ClaimQuests(-1), UiKit.Blue, 68);Notify(bulk.transform,()=>QuestTabHasReward(questTab));
        }

        string QuestResetLabel()
        {
            if (questTab == 1) return "완료 횟수 누적 · 한 번에 받기";
            DateTime now = DateTime.UtcNow;
            DateTime reset = questTab == 0 ? now.Date.AddDays(1) : now.Date.AddDays(7 - (((int)now.DayOfWeek + 6) % 7));
            TimeSpan remaining = reset - now;
            return (questTab == 0 ? "일일 초기화 " : "주간 초기화 ") + (remaining.Days > 0 ? remaining.Days + "일 " : "") + remaining.Hours.ToString("00") + ":" + remaining.Minutes.ToString("00") + ":" + remaining.Seconds.ToString("00") + " (UTC)";
        }

        int QuestGoal(int tab, int index) => Math.Max(1, (tab == 0 ? serviceTuning.dailyGoals : tab == 1 ? serviceTuning.repeatGoals : serviceTuning.weeklyGoals)[index]);
        int[] QuestCounters(int tab) => tab == 0 ? services.daily : tab == 1 ? services.repeat : services.weekly;
        bool QuestClaimed(int tab, int index) => tab == 0 ? services.dailyClaimed[index] : tab == 2 && services.weeklyClaimed[index];
        void QuestCard(Transform parent, int index)
        {
            int tab = questTab, metric = QuestMetrics[tab][index], goal = QuestGoal(tab, index);
            var card = ServiceCard(parent, "Quest " + tab + " " + index, UiKit.Paper);
            var row = UiKit.Row(card, "Quest summary", 102,8);
            string[] icons = { "BatA", "Gold", "Dungeon", "Roulette", "Pvp", "Banana", "Pvp", "Roulette" };
            UiKit.Icon(row, icons[metric], 64);
            var text = UiKit.Column(row, "Quest text", 3, 0); UiKit.Flexible(text, 1);
            string[] labels = { "적 {0}마리 처치", "골드 {0} 획득", "던전 {0}회 도전", "룰렛 {0}회 돌리기", "장비 {0}회 강화", "스킬 {0}회 강화", "PVP {0}회 도전", "뽑기 {0}회 진행" };
            UiKit.Text(text, string.Format(labels[metric], UiNumber.Format(goal)), tab == 1 ? 22 : 25, TextAnchor.MiddleLeft, tab == 1 ? 26 : 38);
            if (tab == 1)
                ServiceText(text, () => UiNumber.Format(services.repeat[metric] / goal) + "회 완료 · 미수령", 18, 18).alignment = TextAnchor.MiddleLeft;
            ServiceGauge(text, () => tab == 1 ? services.repeat[metric] % goal : QuestCounters(tab)[metric], () => goal, false, true);
            var reward = UiKit.Column(row, "Quest reward", 2, 0); ServiceWidth(reward,52);
            UiKit.Icon(reward, "Diamond", 46);
            UiKit.Text(reward, UiNumber.Format(serviceTuning.questRewards[index]), 23, TextAnchor.MiddleCenter, 29);
            var claim = UiKit.Button(row, "받기", () => ClaimQuests(index), UiKit.Yellow, 72);
            Notify(claim.transform,()=>CanClaimQuest(tab,index));
            ServiceWidth(claim.transform,94);
            var claimText = claim.GetComponentInChildren<Text>();
            Action refresh=()=>claim.GetComponent<Image>().color=!QuestClaimed(tab,index)&&QuestCounters(tab)[metric]>=goal?UiKit.Yellow:new Color(.89f,.89f,.89f);
            Func<string> caption = () => QuestClaimed(tab, index) ? "받음" : QuestCounters(tab)[metric] < goal ? "진행 중" : tab == 1 ? UiNumber.Format(services.repeat[metric] / goal) + "회\n받기" : "받기";
            serviceBindings.Add(new ServiceBinding { text = claimText, value = caption,refresh=refresh }); refresh();
            claimText.text = caption();
        }

        public void ClaimQuests(int selected)
        {
            ResetServicePeriods(); long reward = 0;
            long capacity = (long)int.MaxValue - Math.Max(0, Diamonds);
            bool walletFull = false;
            for (int i = 0; i < 4; i++)
            {
                if (selected >= 0 && i != selected) continue;
                int metric = QuestMetrics[questTab][i], goal = QuestGoal(questTab, i);
                if (QuestClaimed(questTab, i) || QuestCounters(questTab)[metric] < goal) continue;
                int perCycle = serviceTuning.questRewards[i];
                if (perCycle <= 0) continue;
                long completed = questTab == 1 ? services.repeat[metric] / goal : 1;
                long paidCycles = Math.Min(completed, (capacity - reward) / perCycle);
                walletFull |= paidCycles < completed;
                if (paidCycles == 0) continue;
                // Subtract only paid whole cycles; the saved counter retains unpaid cycles and the remainder.
                if (questTab == 1) services.repeat[metric] -= (int)(paidCycles * goal);
                else if (questTab == 0) services.dailyClaimed[i] = true;
                else services.weeklyClaimed[i] = true;
                reward += paidCycles * perCycle;
            }
            if (reward == 0) { Toast(walletFull ? "다이아 보유 한도입니다 · 미수령 보상은 유지됩니다" : "받을 수 있는 보상이 없습니다"); return; }
            RecordMissionAction("questClaim");
            GrantServiceDiamonds((int)reward, "퀘스트 보상 획득!");
            if (walletFull) Toast("보유 한도로 남은 보상은 다음에 받을 수 있습니다");
        }

        void BuildDungeons(RectTransform body)
        {
            UiKit.Text(body, "동굴별 처치 목표 달성 · 단계별 보상", 19, TextAnchor.MiddleCenter, 34);
            foreach (int index in new[] { 0, 2 })
            {
                int stage = DungeonChallengeStage(index);
                string reward = index == 0 ? "골드 " + UiNumber.Format(DungeonGoldReward(stage)) : "던전 유물 뽑기권 " + DungeonRelicReward(stage) + "장";
                var card = ServiceCard(body, DungeonNames[index], index == 0 ? new Color(1,.97f,.85f) : new Color(.94f,.9f,.98f));
                Notify(card, () => CanEnterDungeon(index));
                var row = UiKit.Row(card, "Dungeon", 216, 8);
                var art = UiKit.Rect(row, "Dungeon illustration"); ServiceWidth(art,170); UiKit.Height(art,174);
                var cave = UiKit.Icon(art,"Dungeon",164).rectTransform; cave.anchorMin=cave.anchorMax=Vector2.one*.5f; cave.anchoredPosition=Vector2.zero;
                var emblem=UiKit.Icon(art,index==0?"Gold":"DungeonPottery",76).rectTransform;
                emblem.anchorMin=emblem.anchorMax=new Vector2(.5f,.76f);emblem.anchoredPosition=Vector2.zero;cave.anchoredPosition=new Vector2(0,-20);
                var info = UiKit.Column(row, "Dungeon info", 3, 0); UiKit.Flexible(info, 2);
                UiKit.Text(info, DungeonNames[index] + " · " + stage + "단계", 31, TextAnchor.MiddleLeft, 46);
                UiKit.Text(info, reward + "\n적 " + DungeonKillsFor(index) + "마리 · 스테이지 " + DungeonDifficultyStage(stage) + " 난이도", 21, TextAnchor.MiddleLeft, 64);
                var actions=UiKit.Row(info,"Dungeon actions",82,8);
                var count=UiKit.Column(actions,"Attempts",2,0);
                var key=UiKit.Row(count,"Independent daily attempts",43,3);
                var keySymbol=ServiceSymbol(key,"DungeonKey",35).GetComponent<DoodleServiceSymbol>();
                keySymbol.accent=index==0?new Color(1,.83f,.2f):new Color(.66f,.36f,.93f);
                ServiceText(key,()=>Math.Max(0,serviceTuning.dungeonAttempts-services.dungeonUsed[index])+"/"+serviceTuning.dungeonAttempts,29,41);
                UiKit.Text(count,index==0?"노랑 열쇠":"보라 열쇠",18,TextAnchor.MiddleCenter,24);
                var enter=UiKit.Button(actions,services.activeDungeon==index?"진행 중":"입장",()=>EnterDungeon(index),UiKit.Blue,76);ServiceWidth(enter.transform,118);
                enter.interactable=CanEnterDungeon(index);Notify(enter.transform,()=>CanEnterDungeon(index));
                if(services.activeDungeon==index)ServiceGauge(card,()=>services.dungeonProgress,()=>DungeonKillsFor(index));
            }
            UiKit.Text(body,"각 던전은 UTC 00:00에 각각 3회 충전",17,TextAnchor.MiddleCenter,28);
        }

        public void EnterDungeon(int index)
        {
            CreditPendingFieldGold(); TickServices(); ResetServicePeriods();
            if (!CanEnterDungeon(index)) return;
            services.dungeonUsed[index]++; services.activeDungeon=index; services.dungeonProgress=0;
            if(game)game.RequestCombatWaveReset();
            lastServiceKills=game?game.Kills:0;lastKills=lastServiceKills;
            RecordServiceProgress("dungeon",1);Save();ClearOverlays();ClosePage();
            Toast(DungeonNames[index]+" "+DungeonChallengeStage(index)+"단계 도전 시작!");
        }

        void CompleteDungeon()
        {
            int index=services.activeDungeon, stage=DungeonChallengeStage(index);
            services.dungeonStages[index]=stage;
            RecordMissionAction("dungeon:"+index);
            services.activeDungeon=-1;services.dungeonProgress=0;
            if(game)game.RequestCombatWaveReset();
            var rewards=new List<UiReward>();
            if(index==0) {
                int amount=DungeonGoldReward(stage);
                Gold=SaturatingAdd(Gold,amount);RecordServiceProgress("gold",amount);
                rewards.Add(new UiReward { name="",icon="Gold",amount=amount,rarity=0 });
            } else {
                int amount=DungeonRelicReward(stage);GrantDungeonRelicTickets(amount);
                rewards.Add(new UiReward { name="던전 유물 뽑기권",icon="DungeonRelicTicket",amount=amount,rarity=0 });
            }
            Save();if(ActivePage=="Dungeons")RefreshPage();
            ShowRewards("던전 클리어!\n"+DungeonNames[index]+" · "+stage+"단계",rewards);
        }

        List<LocalRank> LocalRanking()
        {
            string[] names = { "밤톨왕", "구름발", "버섯대장", "콩알", "밤비", "동실" };
            string[] art = { "Player", "StormCloud", "MushroomA", "DevilA", "BatA", "StormCloudB" };
            var ranks = new List<LocalRank>();
            for (int i = 0; i < 100; i++) ranks.Add(new LocalRank { name = i < names.Length ? names[i] : names[i % names.Length] + (i + 1), art = art[i % art.Length], points = 2840 - i * 17, power = 58200 - i * 460 });
            ranks.Add(new LocalRank { name = PlayerName, art = "Player", points = services.pvpPoints, power = Power, self = true });
            ranks.Sort((a, b) => { int points = b.points.CompareTo(a.points); return points != 0 ? points : b.power.CompareTo(a.power); });
            return ranks;
        }

        void BuildPvp(RectTransform body)
        {
            UiKit.Text(body, "로컬 모의 PVP · 예시 랭킹 / 서버 미연결", 17, TextAnchor.MiddleCenter, 26);
            var ranks = LocalRanking();
            var podium = UiKit.Row(body, "Top three podium", 206);
            foreach (int position in new[] { 1, 0, 2 })
            {
                var rank = ranks[position];
                var card = UiKit.Rect(podium, "Podium rank " + (position + 1));UiKit.Flexible(card);UiKit.Height(card,206);
                float stepHeight=position==0?84:position==1?57:42;
                var step=UiKit.Box(card,"Podium pedestal",position==0?UiKit.Yellow:position==1?new Color(.84f,.85f,.87f):new Color(.87f,.70f,.53f));
                step.GetComponent<Image>().raycastTarget=false;
                step.anchorMin=Vector2.zero;step.anchorMax=new Vector2(1,0);step.pivot=new Vector2(.5f,0);step.anchoredPosition=Vector2.zero;step.sizeDelta=new Vector2(0,stepHeight);
                var number=UiKit.Text(step,(position+1).ToString(),49,TextAnchor.MiddleCenter,stepHeight);UiKit.Stretch(number.rectTransform,8,0,8,0);
                float contactY=stepHeight-2,portraitSize=position==0?78:70;
                var shadow=UiKit.Rect(card,"Podium contact shadow");shadow.anchorMin=shadow.anchorMax=new Vector2(.5f,0);shadow.anchoredPosition=new Vector2(0,contactY+1);shadow.sizeDelta=new Vector2(portraitSize*.62f,7);
                var shade=shadow.gameObject.AddComponent<Image>();shade.sprite=UiKit.Circle;shade.color=new Color(0,0,0,.18f);shade.raycastTarget=false;
                var portrait=UiKit.Icon(card,rank.art,portraitSize);var image=portrait.rectTransform;
                var source=portrait.sprite.rect.size;var drawn=source*(portraitSize/Mathf.Max(source.x,source.y));
                image.sizeDelta=drawn;image.anchorMin=image.anchorMax=new Vector2(.5f,0);image.pivot=new Vector2(.5f,0);image.anchoredPosition=new Vector2(0,contactY);
                var name=UiKit.Text(card,rank.name,23,TextAnchor.MiddleCenter,30).rectTransform;name.anchorMin=new Vector2(0,0);name.anchorMax=new Vector2(1,0);name.pivot=new Vector2(.5f,0);name.sizeDelta=new Vector2(0,30);name.anchoredPosition=new Vector2(0,contactY+drawn.y+5);
            }
            int selfIndex = ranks.FindIndex(r => r.self);
            var mine = ServiceCard(body, "My rank", UiKit.Yellow);
            var myRow = UiKit.Row(mine, "My ranking", 64);
            UiKit.Icon(myRow, "Player", 62);
            UiKit.Text(myRow, "내 순위 " + (selfIndex + 1) + "위\n" + PlayerName, 22, TextAnchor.MiddleLeft, 66);
            UiKit.Text(myRow, "승점 " + UiNumber.Format(services.pvpPoints) + "\n전투력 " + UiNumber.Format(Power), 20, TextAnchor.MiddleRight, 66);
            var actions=UiKit.Row(body,"PVP actions",66,10);
            var challenge = UiKit.Button(actions, "모의 대전 시작", PlayLocalPvp, UiKit.Blue, 66);UiKit.Flexible(challenge.transform,1.6f);
            var attempts=UiKit.Box(actions,"PVP remaining attempts",new Color(.96f,.94f,.9f),66);
            var attemptLabel=UiKit.Text(attempts,"오늘 도전 "+Math.Max(0,serviceTuning.pvpAttempts-services.pvpUsed)+"/"+serviceTuning.pvpAttempts,23,TextAnchor.MiddleCenter,66);UiKit.Stretch(attemptLabel.rectTransform,5,3,5,3);
            challenge.interactable = services.pvpUsed < serviceTuning.pvpAttempts;
            var banner=UiKit.Box(body,"Ranking title",new Color(1,.97f,.85f),42); var title=UiKit.Text(banner,"랭킹 1~100위",29,TextAnchor.MiddleCenter,42);UiKit.Stretch(title.rectTransform);
            var header = UiKit.Row(body, "Ranking columns", 32);
            UiKit.Text(header, "순위", 18, TextAnchor.MiddleCenter, 30);
            UiKit.Text(header, "이름", 18, TextAnchor.MiddleCenter, 30);
            UiKit.Text(header, "승점", 18, TextAnchor.MiddleCenter, 30);
            UiKit.Text(header, "전투력", 18, TextAnchor.MiddleCenter, 30);
            var listFrame = UiKit.Box(body, "Scrollable ranking 1 to 100", UiKit.Paper, 315);
            var viewport = new GameObject("Ranking viewport", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
            viewport.SetParent(listFrame, false); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = new Vector2(5, 5); viewport.offsetMax = new Vector2(-5, -5);
            viewport.GetComponent<Image>().color = UiKit.Paper; viewport.GetComponent<Mask>().showMaskGraphic = false;
            var rankingBody = UiKit.Column(viewport, "Ranking content", 3, 2);
            rankingBody.anchorMin = new Vector2(0, 1); rankingBody.anchorMax = Vector2.one; rankingBody.pivot = new Vector2(.5f, 1); rankingBody.sizeDelta = Vector2.zero;
            var rankingScroll = listFrame.gameObject.AddComponent<ScrollRect>(); rankingScroll.viewport = viewport; rankingScroll.content = rankingBody; rankingScroll.horizontal = false;
            rankingScroll.movementType = ScrollRect.MovementType.Clamped; rankingScroll.scrollSensitivity = 35;
            for (int i = 0; i < 100; i++)
            {
                var rank = ranks[i];
                var card = ServiceCard(rankingBody, "Rank " + (i + 1), rank.self ? UiKit.Yellow : UiKit.Paper);
                card.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(7,7,2,2);
                var row = UiKit.Row(card, "Player rank", 44, 4);
                var number = UiKit.Text(row, (i + 1).ToString(), 22, TextAnchor.MiddleCenter, 42); UiKit.Flexible(number.transform, .45f);
                UiKit.Icon(row, rank.art, 41);
                var name = UiKit.Text(row, rank.name, 21, TextAnchor.MiddleLeft, 42); UiKit.Flexible(name.transform, 1.4f);
                UiKit.Text(row, UiNumber.Format(rank.points), 21, TextAnchor.MiddleCenter, 42);
                UiKit.Text(row, UiNumber.Format(rank.power), 21, TextAnchor.MiddleCenter, 42);
            }
            mine.SetAsLastSibling(); actions.SetAsLastSibling();
        }

        public void PlayLocalPvp()
        {
            ResetServicePeriods(); if (services.pvpUsed >= serviceTuning.pvpAttempts) return;
            var ranks = LocalRanking(); int own = ranks.FindIndex(r => r.self);
            var opponent = ranks[own > 0 ? own - 1 : 1];
            bool won = Power * (0.85 + serviceRandom.NextDouble() * .3) >= opponent.power;
            services.pvpUsed++; services.pvpPoints = Math.Max(0, services.pvpPoints + (won ? 35 : -10));
            RecordServiceProgress("pvp", 1); Save(); RefreshPage();
            ShowDetail("모의 대전 결과", panel =>
            {
                UiKit.Icon(panel, opponent.art, 94);
                UiKit.Text(panel, opponent.name + " 상대 " + (won ? "승리! +35점" : "패배 · -10점"), 26, TextAnchor.MiddleCenter, 52);
                UiKit.Text(panel, "로컬 전투력 비교 시뮬레이션입니다.\n실제 상대나 서버 랭킹에는 영향을 주지 않습니다.", 20, TextAnchor.MiddleCenter, 76);
            });
        }

        void BuildChat(RectTransform body)
        {
            var channel=UiKit.Box(body,"Chat channel",new Color(.91f,.91f,.91f),62);channel.GetComponent<Outline>().enabled=false;
            var channelText=UiKit.Text(channel,"전체 채팅 · 로컬 데모",31,TextAnchor.MiddleCenter,62);UiKit.Stretch(channelText.rectTransform);
            UiKit.Text(body, "네트워크 미연결 · 메시지는 다른 사람에게 전송되지 않습니다", 17, TextAnchor.MiddleCenter, 30);
            foreach (var message in localMessages)
            {
                float messageHeight = Mathf.Max(162, 82 + Mathf.CeilToInt(message.content.Length / 16f) * 31);
                var row = UiKit.Row(body, "Chat message", messageHeight, 10);
                if(message.self) ServiceWidth(UiKit.Rect(row,"Chat opposite margin"),52);else ChatPortrait(row,message.art);
                var column=UiKit.Column(row,"Chat message text",5,0);
                UiKit.Text(column, message.author, 26, message.self ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, 38);
                var bubble = ServiceCard(column, "Message bubble", message.self ? new Color(1,.95f,.7f) : UiKit.Paper);
                var text = UiKit.Text(bubble, message.content, 29, TextAnchor.MiddleLeft, messageHeight - 70); text.supportRichText = false;
                var tail=ServiceSymbol(bubble,message.self?"TailRight":"TailLeft",20);tail.anchorMin=tail.anchorMax=new Vector2(message.self?1:0,.5f);tail.anchoredPosition=new Vector2(message.self?8:-8,0);
                tail.GetComponent<LayoutElement>().ignoreLayout=true;tail.GetComponent<DoodleServiceSymbol>().accent=message.self?new Color(1,.95f,.7f):UiKit.Paper;
                if (message.self) ChatPortrait(row,message.art);else ServiceWidth(UiKit.Rect(row,"Chat opposite margin"),52);
            }
            var footer = UiKit.Footer(body, "Chat footer", 96);
            var inputRow = UiKit.Row(footer, "Chat composer", 62);
            var field = UiKit.Box(inputRow, "Chat input", UiKit.Paper, 60); UiKit.Flexible(field, 4);
            var input = field.gameObject.AddComponent<InputField>(); input.characterLimit = 140;
            var content = UiKit.Text(field, "", 23, TextAnchor.MiddleLeft, 54); content.supportRichText = false;
            content.rectTransform.anchorMin = Vector2.zero; content.rectTransform.anchorMax = Vector2.one; content.rectTransform.offsetMin = new Vector2(12, 5); content.rectTransform.offsetMax = new Vector2(-12, -5);
            input.textComponent = content; input.targetGraphic = field.GetComponent<Image>();
            var placeholder = UiKit.Text(field, "메시지를 입력하세요", 20, TextAnchor.MiddleLeft, 54); placeholder.color = Color.gray;
            placeholder.rectTransform.anchorMin = Vector2.zero; placeholder.rectTransform.anchorMax = Vector2.one; placeholder.rectTransform.offsetMin = new Vector2(12, 5); placeholder.rectTransform.offsetMax = new Vector2(-12, -5);
            input.placeholder = placeholder;
            Action send = () =>
            {
                string message = input.text.Trim(); if (message.Length == 0) return;
                localMessages.Add(new LocalMessage { author = PlayerName, art = "Player", content = message, self = true });
                if (localMessages.Count > 30) localMessages.RemoveAt(0);
                RefreshPage();
            };
            UiKit.Button(inputRow, "보내기", send, UiKit.Blue, 60);

            UiKit.Text(footer, "메시지는 이 실행 동안만 보관됩니다", 17, TextAnchor.MiddleCenter, 26);
            StartCoroutine(ScrollChatToLatest(body));
        }

        void ChatPortrait(Transform parent,string art)
        {
            var circle=UiKit.Box(parent,"Chat portrait",new Color(.91f,.91f,.91f),108);ServiceWidth(circle,108);circle.GetComponent<Image>().sprite=UiKit.Circle;circle.GetComponent<Image>().type=Image.Type.Simple;
            var image=UiKit.Icon(circle,art,88).rectTransform;image.anchorMin=image.anchorMax=Vector2.one*.5f;image.anchoredPosition=Vector2.zero;
        }

        IEnumerator ScrollChatToLatest(RectTransform body)
        {
            yield return null;
            if (!body) yield break;
            var scroll = body.GetComponentInParent<ScrollRect>();
            if (scroll) { UnityEngine.Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0; }
        }

        void BuildSettings(RectTransform body)
        {
            var account = ServiceCard(body, "Account connection", UiKit.Paper);
            var accountRow=UiKit.Row(account,"Account row",88,12);ServiceSymbol(accountRow,"Account",58);
            var accountInfo=UiKit.Column(accountRow,"Account status",2,0);UiKit.Text(accountInfo,"계정연동",27,TextAnchor.MiddleLeft,36);UiKit.Text(accountInfo,"연동 안 됨",23,TextAnchor.MiddleLeft,32).color=new Color(.8f,.12f,.1f);
            var link=UiKit.Button(accountRow, "연동하기", () => ShowDetail("계정연동", panel => UiKit.Text(panel, "계정 제공자와 서버가 연결되지 않았습니다.\n진행 상황은 현재 기기에만 저장됩니다.", 23, TextAnchor.MiddleCenter, 104)), UiKit.Blue,64);ServiceWidth(link.transform,144);
            var power = ServiceCard(body, "Power saving", UiKit.Paper);
            power.name="절전모드  " + (services.powerSaving ? "켜짐 · 30 FPS" : "꺼짐 · 60 FPS");
            var toggle=power.gameObject.AddComponent<Button>();toggle.targetGraphic=power.GetComponent<Image>();toggle.onClick.AddListener(()=>
            {
                services.powerSaving = !services.powerSaving;
                Application.targetFrameRate = services.powerSaving ? 30 : 60; Save(); RefreshPage();
            });
            var powerRow=UiKit.Row(power,"Power saving row",82,12);ServiceSymbol(powerRow,"Leaf",58);
            UiKit.Text(powerRow,"절전모드",27,TextAnchor.MiddleLeft,42);
            var switchTrack=UiKit.Box(powerRow,"Power saving toggle",services.powerSaving?UiKit.Green:new Color(.68f,.68f,.68f),40);ServiceWidth(switchTrack,76);
            var knob=UiKit.Box(switchTrack,"Toggle knob",Color.white);knob.GetComponent<Image>().sprite=UiKit.Circle;knob.GetComponent<Image>().type=Image.Type.Simple;knob.anchorMin=knob.anchorMax=new Vector2(services.powerSaving?1:0,.5f);knob.anchoredPosition=new Vector2(services.powerSaving?-20:20,0);knob.sizeDelta=Vector2.one*34;
            var powerState=UiKit.Text(powerRow,services.powerSaving?"켜짐":"꺼짐",24,TextAnchor.MiddleCenter,40);ServiceWidth(powerState.transform,48);
            ServiceVolume(body, "배경음", true); ServiceVolume(body, "효과음", false);
            if (FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length == 0)
                UiKit.Text(body, "사운드 소스 미등록 · 음량 설정은 저장됩니다", 16, TextAnchor.MiddleCenter, 24);
            UiKit.Button(body, "게임종료", () => ShowDetail("게임을 종료할까요?", panel =>
            {
                UiKit.Text(panel, "현재 진행 상황을 저장합니다", 23, TextAnchor.MiddleCenter, 48);
                UiKit.Button(panel, "게임종료", () => { Save(); PlayerPrefs.Save(); Application.Quit(); }, UiKit.Red, 58);
            }), new Color(1,.56f,.57f), 68);
        }

        void ServiceVolume(Transform parent, string title, bool music)
        {
            var card = ServiceCard(parent, title, UiKit.Paper);
            var row=UiKit.Row(card,title+" setting",84,12);ServiceSymbol(row,music?"Music":"Speaker",58);
            var details=UiKit.Column(row,title+" controls",5,0);UiKit.Text(details,title,27,TextAnchor.MiddleLeft,36);
            var controls=UiKit.Row(details,title+" slider row",34,10);
            var track = UiKit.Box(controls, title + " slider", new Color(.77f, .77f, .74f), 28);
            var label=UiKit.Text(controls,"",24,TextAnchor.MiddleRight,32);ServiceWidth(label.transform,58);
            var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
            var fill = new GameObject("Volume fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); fill.transform.SetParent(track, false); fill.color = UiKit.Green;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one; fill.rectTransform.offsetMin = new Vector2(4, 8); fill.rectTransform.offsetMax = new Vector2(-4, -8);
            var handleArea = new GameObject("Volume handle bounds", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(track, false); handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(14, 1); handleArea.offsetMax = new Vector2(-14, -1);
            var handle = new GameObject("Volume handle", typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<Image>(); handle.transform.SetParent(handleArea, false); handle.color = UiKit.Paper;
            handle.sprite = UiKit.Circle; handle.preserveAspect = true;
            handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = new Vector2(0, 1); handle.rectTransform.sizeDelta = new Vector2(28, 0);
            handle.GetComponent<Outline>().effectColor = UiKit.Ink; handle.GetComponent<Outline>().effectDistance = new Vector2(2, -2);
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.value = music ? services.music : services.effects;
            label.text = Mathf.RoundToInt(slider.value * 100) + "%";
            slider.onValueChanged.AddListener(value =>
            {
                if (music) services.music = value; else services.effects = value;
                label.text = Mathf.RoundToInt(value * 100) + "%";
                ApplyServiceAudioSettings(); SaveServices();
            });
        }

        public void ApplyServiceAudioSettings()
        {
            if (services == null) return;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) source.volume = source.loop ? services.music : services.effects;
        }

        static void ServiceWidth(Transform target,float width)
        {
            var element=target.GetComponent<LayoutElement>()??target.gameObject.AddComponent<LayoutElement>();element.minWidth=element.preferredWidth=width;element.flexibleWidth=0;
        }

        static RectTransform ServiceSymbol(Transform parent,string kind,float size)
        {
            var rect=UiKit.Rect(parent,"Service symbol: "+kind);rect.gameObject.AddComponent<CanvasRenderer>();var symbol=rect.gameObject.AddComponent<DoodleServiceSymbol>();symbol.kind=kind;symbol.raycastTarget=false;rect.sizeDelta=Vector2.one*size;ServiceWidth(rect,size);UiKit.Height(rect,size);return rect;
        }

        public void ReflowServiceLayouts()
        {
            foreach (var roulette in GetComponentsInChildren<DoodleRouletteLayout>()) roulette.Reflow();
        }
    }

    /// <summary>Small native line icons and speech tails; these stay independent of sprite atlases.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleServiceSymbol : MaskableGraphic
    {
        public string kind;
        public Color accent=Color.white;
        Vector2 P(float x,float y)=>rectTransform.rect.center+new Vector2(x*rectTransform.rect.width/64f,y*rectTransform.rect.height/64f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch(kind)
            {
                case "Account":
                    Disc(vh,0,16,13,13,UiKit.Ink);Disc(vh,0,16,9,9,UiKit.Paper);
                    Arc(vh,0,-23,23,0,180,4,UiKit.Ink);Line(vh,-23,-23,-23,-31,4);Line(vh,23,-23,23,-31,4);Line(vh,-23,-31,23,-31,4);break;
                case "Leaf":
                    Poly(vh,new[]{P(-19,-13),P(-20,4),P(-10,21),P(25,30),P(25,8),P(15,-14),P(-3,-23)},UiKit.Ink);
                    Poly(vh,new[]{P(-14,-10),P(-15,3),P(-6,17),P(20,24),P(20,8),P(12,-10),P(-2,-18)},UiKit.Green);
                    Line(vh,-24,-29,12,13,4);break;
                case "Music":
                    Line(vh,-12,-15,-12,23,5);Line(vh,17,-10,17,28,5);Line(vh,-12,23,17,28,6);Line(vh,-12,15,17,20,4);Disc(vh,-19,-17,9,7,UiKit.Ink);Disc(vh,10,-12,9,7,UiKit.Ink);break;
                case "Speaker":
                    Poly(vh,new[]{P(-27,-11),P(-15,-11),P(3,-25),P(3,25),P(-15,11),P(-27,11)},UiKit.Ink);
                    Poly(vh,new[]{P(-23,-7),P(-13,-7),P(-1,-17),P(-1,17),P(-13,7),P(-23,7)},new Color(.65f,.65f,.65f));
                    Arc(vh,2,0,15,-55,55,4,UiKit.Ink);Arc(vh,2,0,26,-55,55,4,UiKit.Ink);break;
                case "Paw":
                    Color pink=new Color(.94f,.56f,.56f);Disc(vh,0,-8,15,12,pink);Disc(vh,-20,6,6,8,pink);Disc(vh,-8,19,6,8,pink);Disc(vh,8,19,6,8,pink);Disc(vh,20,6,6,8,pink);break;
                case "DungeonKey":
                    Stroke(vh,P(-22,-25),P(9,11),11*rectTransform.rect.width/64f,UiKit.Ink);
                    Stroke(vh,P(-22,-25),P(9,11),6*rectTransform.rect.width/64f,accent);
                    Stroke(vh,P(-20,-21),P(-10,-28),8*rectTransform.rect.width/64f,UiKit.Ink);
                    Stroke(vh,P(-20,-21),P(-10,-28),4*rectTransform.rect.width/64f,accent);
                    Disc(vh,13,16,16,16,UiKit.Ink);Disc(vh,13,16,12,12,accent);Disc(vh,13,16,6,6,UiKit.Ink);Disc(vh,13,16,3,3,UiKit.Paper);break;
                case "TailLeft": case "TailRight":
                    float side=kind=="TailLeft"?-1:1;Poly(vh,new[]{P(side*30,0),P(-side*16,24),P(-side*16,-24)},UiKit.Ink);Poly(vh,new[]{P(side*18,0),P(-side*20,16),P(-side*20,-16)},accent);break;
            }
        }
        void Disc(VertexHelper vh,float x,float y,float rx,float ry,Color tint)
        {
            for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Poly(vh,new[]{P(x,y),P(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry),P(x+Mathf.Cos(b)*rx,y+Mathf.Sin(b)*ry)},tint);}
        }
        void Arc(VertexHelper vh,float x,float y,float radius,float from,float to,float width,Color tint)
        {
            for(int i=0;i<28;i++){float a=Mathf.Lerp(from,to,i/28f)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(i+1)/28f)*Mathf.Deg2Rad;Stroke(vh,P(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),P(x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius),width*rectTransform.rect.width/64f,tint);}
        }
        void Line(VertexHelper vh,float x,float y,float xx,float yy,float width)=>Stroke(vh,P(x,y),P(xx,yy),width*rectTransform.rect.width/64f,UiKit.Ink);
        static void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint){Vector2 axis=(b-a).normalized;Vector2 n=new Vector2(-axis.y,axis.x)*width*.5f;Poly(vh,new[]{a-n,a+n,b+n,b-n},tint);}
        static void Poly(VertexHelper vh,Vector2[] points,Color tint){int start=vh.currentVertCount;foreach(var point in points)vh.AddVert(point,tint,Vector2.zero);for(int i=1;i<points.Length-1;i++)vh.AddTriangle(start,start+i,start+i+1);}
    }

    public sealed class DoodleRouletteLayout : MonoBehaviour
    {
        public RectTransform viewport, body, wheel, pointer, counter;
        public Text remaining, odds;
        public Button spin;
        public bool Compact { get; private set; }
        void LateUpdate() => Reflow();
        public void Reflow()
        {
            if (!viewport || !body || !wheel || !pointer || !remaining || !odds || !spin) return;
            var holder = (RectTransform)transform;
            Compact = viewport.rect.height < 500;
            float remainingHeight = Compact ? 30 : 54, oddsHeight = Compact ? 0 : 28, spinHeight = Compact ? 42 : 90;
            var layout = body.GetComponent<VerticalLayoutGroup>();
            float spacing = Compact ? 4 : 10;
            if (layout) layout.spacing = spacing;
            odds.gameObject.SetActive(!Compact);
            remaining.text = remaining.text.Replace(" · 각 칸 12.5%", "") + (Compact ? " · 각 칸 12.5%" : "");
            UiKit.Height(counter ? counter : remaining.transform, remainingHeight); remaining.resizeTextMaxSize = Compact ? 20 : 29;
            UiKit.Height(spin.transform, spinHeight);
            float otherHeight = remainingHeight + oddsHeight + spinHeight + spacing * (Compact ? 2 : 3) + (layout ? layout.padding.vertical : 8);
            float availableHeight = Mathf.Max(36, viewport.rect.height - otherHeight - 2);
            float availableWidth = Mathf.Max(72, viewport.rect.width - 16);
            float scale = Mathf.Min(1, Mathf.Min(availableHeight, availableWidth) / 520f);
            wheel.localScale = pointer.localScale = Vector3.one * scale;
            wheel.anchoredPosition = new Vector2(0, -10 * scale);
            var sizing = holder.GetComponent<LayoutElement>();
            float height = 520 * scale;
            if (Mathf.Abs(sizing.preferredHeight - height) > .1f) sizing.minHeight = sizing.preferredHeight = height;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleRoulettePointer : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 center = rectTransform.rect.center;
            Vector2 a = new Vector2(-21, 19), b = new Vector2(21, 19), c = new Vector2(0, -20);
            vh.AddVert(center + a, UiKit.Ink, Vector2.zero); vh.AddVert(center + b, UiKit.Ink, Vector2.zero); vh.AddVert(center + c, UiKit.Ink, Vector2.zero); vh.AddTriangle(0, 1, 2);
            vh.AddVert(center + a * .74f, UiKit.Red, Vector2.zero); vh.AddVert(center + b * .74f, UiKit.Red, Vector2.zero); vh.AddVert(center + c * .74f, UiKit.Red, Vector2.zero); vh.AddTriangle(3, 4, 5);
        }
    }

    /// <summary>Native uGUI vector mesh; roulette remains sharp at every supported aspect ratio.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DoodleRouletteGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            Color[] colors = { new Color(1, .93f, .64f), new Color(.72f, .88f, 1), new Color(.75f, .94f, .77f), new Color(1, .76f, .8f), new Color(1, .93f, .64f), new Color(.72f, .88f, 1), new Color(.75f, .94f, .77f), new Color(.87f, .78f, .98f) };
            for (int segment = 0; segment < 8; segment++)
            {
                for (int step = 0; step < 12; step++)
                {
                    float a = (90 - segment * 45 - step * 3.75f) * Mathf.Deg2Rad;
                    float b = (90 - segment * 45 - (step + 1) * 3.75f) * Mathf.Deg2Rad;
                    Vector2 first = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); Vector2 second = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                    Triangle(helper, Vector2.zero, first * (radius - 4), second * (radius - 4), colors[segment]);
                    Quad(helper, first * (radius - 4), first * radius, second * radius, second * (radius - 4), UiKit.Ink);
                }
                float angle = (90 - segment * 45) * Mathf.Deg2Rad;
                Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); Vector2 side = new Vector2(-axis.y, axis.x) * 1.5f;
                Quad(helper, side, -side, axis * radius - side, axis * radius + side, UiKit.Ink);
            }
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48, b = (i + 1) * Mathf.PI * 2 / 48;
                Triangle(helper, Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 50, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 50, UiKit.Ink);
                Triangle(helper, Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 46, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 46, UiKit.Paper);
            }
        }
        static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vh.currentVertCount; vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero); vh.AddTriangle(start, start + 1, start + 2);
        }
        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vh.currentVertCount; vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero); vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
