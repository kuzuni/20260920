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
            public int buffSeconds = 900, buffPrice = 20, dungeonKills = 30, dungeonGold = 30000;
            public float goldBuff = .5f, attackBuff = .3f;
            public int[] dailyGoals = { 200, 1000, 3, 5 };
            public int[] repeatGoals = { 500, 10, 5, 5000 };
            public int[] weeklyGoals = { 5000, 15, 10, 100 };
            public int[] questRewards = { 100, 150, 200, 300 };
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
        }

        sealed class ServiceBinding
        {
            public Text text;
            public Func<string> value;
            public Image fill;
            public Func<float> fraction;
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
        static readonly string[] DungeonNames = { "골드 동굴", "버섯 소굴", "악마의 틈" };
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
        public int DungeonKillGoal => serviceTuning.dungeonKills;
        public string DungeonMission => ActiveDungeonIndex < 0 ? "" : DungeonNames[ActiveDungeonIndex] + "  " + DungeonProgress + "/" + DungeonKillGoal;
        static int SecondsUntil(long ticks) => (int)Math.Max(0, Math.Min(int.MaxValue, Math.Ceiling((ticks - ServiceNow) / (double)TimeSpan.TicksPerSecond)));
        static string ServiceClock(int seconds) => (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");

        void InitServices()
        {
            var tuningAsset = Resources.Load<TextAsset>("DoodleIdle/UI/ServicesTuning");
            if (tuningAsset) JsonUtility.FromJsonOverwrite(tuningAsset.text, serviceTuning);
            serviceTuning.buffSeconds = Mathf.Max(1, serviceTuning.buffSeconds);
            serviceTuning.dungeonKills = Mathf.Max(1, serviceTuning.dungeonKills);
            services = new ServiceState();
            string json = PlayerPrefs.GetString(ServicesSaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, services); }
                catch (ArgumentException) { services = new ServiceState(); }
            }
            if (services.dungeonUsed == null || services.dungeonUsed.Length != 3) services.dungeonUsed = new int[3];
            if (services.daily == null || services.daily.Length != 8) services.daily = new int[8];
            if (services.weekly == null || services.weekly.Length != 8) services.weekly = new int[8];
            if (services.repeat == null || services.repeat.Length != 8) services.repeat = new int[8];
            if (services.dailyClaimed == null || services.dailyClaimed.Length != 4) services.dailyClaimed = new bool[4];
            if (services.weeklyClaimed == null || services.weeklyClaimed.Length != 4) services.weeklyClaimed = new bool[4];
            services.attendanceIndex = Mathf.Clamp(services.attendanceIndex, 0, 7);
            services.activeDungeon = Mathf.Clamp(services.activeDungeon, -1, 2);
            ResetServicePeriods();
            lastServiceKills = game ? game.Kills : 0;
            Application.targetFrameRate = services.powerSaving ? 30 : 60;
            ApplyServiceAudioSettings();
            localMessages.Add(new LocalMessage { author = "구름발 · 예시", art = "StormCloud", content = "안녕하세요! 이 화면은 로컬 채팅 예시입니다." });
            localMessages.Add(new LocalMessage { author = "버섯대장 · 예시", art = "MushroomA", content = "입력한 메시지는 이 기기에서만 표시돼요." });
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
                    if (services.dungeonProgress >= serviceTuning.dungeonKills) CompleteDungeon();
                }
            }
            if (Time.unscaledTime < nextServiceTick) return;
            nextServiceTick = Time.unscaledTime + .25f;
            if (ResetServicePeriods()) { Save(); if (!string.IsNullOrEmpty(ActivePage)) RefreshPage(); }
            for (int i = serviceBindings.Count - 1; i >= 0; i--)
            {
                var binding = serviceBindings[i];
                if (!binding.text) { serviceBindings.RemoveAt(i); continue; }
                binding.text.text = binding.value();
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
            var grid = UiKit.Grid(body, "Attendance days", 3, 155);
            for (int i = 0; i < 6; i++) AttendanceCard(grid, i);
            AttendanceCard(body, 6);
            var claim = UiKit.Button(body, services.attendanceDay == services.day ? "오늘 보상 받음" : "오늘 보상 받기", ClaimAttendance, UiKit.Blue, 60);
            claim.interactable = services.attendanceDay != services.day && services.attendanceIndex < 7;
        }

        void AttendanceCard(Transform parent, int index)
        {
            bool claimed = index < services.attendanceIndex;
            bool current = index == services.attendanceIndex;
            var card = ServiceCard(parent, "Attendance day " + (index + 1), claimed ? UiKit.Green : current ? UiKit.Yellow : UiKit.Paper);
            UiKit.Text(card, (index + 1) + "일차", 22, TextAnchor.MiddleCenter, 30);
            var line = UiKit.Row(card, "Reward", 48);
            UiKit.Icon(line, "Diamond", 45);
            UiKit.Text(line, serviceTuning.attendance[index].ToString("N0"), 23, TextAnchor.MiddleCenter, 44);
            UiKit.Text(card, claimed ? "받음" : current ? "오늘" : "대기 중", 20, TextAnchor.MiddleCenter, 28);
        }

        public void ClaimAttendance()
        {
            ResetServicePeriods();
            if (services.attendanceDay == services.day || services.attendanceIndex >= 7) return;
            int amount = serviceTuning.attendance[services.attendanceIndex++];
            services.attendanceDay = services.day;
            GrantServiceDiamonds(amount, "출석 보상 획득!");
        }

        void GrantServiceDiamonds(int amount, string title)
        {
            Diamonds += amount; Save(); RefreshPage();
            ShowRewards(title, new List<UiReward> { new UiReward { name = "", icon = "Diamond", amount = amount, rarity = 0 } });
        }

        void BuildRoulette(RectTransform body)
        {
            ServiceText(body, () => "오늘 남은 횟수 " + Math.Max(0, serviceTuning.dailySpins - services.spins) + "/" + serviceTuning.dailySpins, 25, 40);
            UiKit.Text(body, "하루 5회 · 각 칸 확률 12.5%", 18, TextAnchor.MiddleCenter, 28);
            var holder = new GameObject("Roulette area", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            holder.SetParent(body, false); UiKit.Height(holder, 340);
            var wheel = new GameObject("Roulette wheel", typeof(RectTransform), typeof(DoodleRouletteGraphic)).GetComponent<RectTransform>();
            wheel.SetParent(holder, false); wheel.anchorMin = wheel.anchorMax = new Vector2(.5f, .5f); wheel.sizeDelta = new Vector2(320, 320);
            wheel.GetComponent<DoodleRouletteGraphic>().raycastTarget = false;
            for (int i = 0; i < serviceTuning.roulette.Length; i++)
            {
                float angle = (90 - (i + .5f) * 45) * Mathf.Deg2Rad;
                var reward = new GameObject("Wheel reward " + i, typeof(RectTransform)).GetComponent<RectTransform>();
                reward.SetParent(wheel, false); reward.anchorMin = reward.anchorMax = Vector2.one * .5f; reward.sizeDelta = new Vector2(68, 58);
                reward.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 112;
                var icon = UiKit.Icon(reward, "Diamond", 34).rectTransform;
                icon.anchorMin = icon.anchorMax = new Vector2(.5f, .7f); icon.anchoredPosition = Vector2.zero;
                var number = UiKit.Text(reward, serviceTuning.roulette[i].ToString(), 19, TextAnchor.MiddleCenter, 24).rectTransform;
                number.anchorMin = new Vector2(0, 0); number.anchorMax = new Vector2(1, .38f); number.offsetMin = number.offsetMax = Vector2.zero;
            }
            var pointer = new GameObject("Roulette pointer", typeof(RectTransform), typeof(DoodleRoulettePointer)).GetComponent<RectTransform>();
            pointer.SetParent(holder, false);
            pointer.anchorMin = pointer.anchorMax = new Vector2(.5f, 1); pointer.pivot = new Vector2(.5f, 1); pointer.sizeDelta = new Vector2(45, 45); pointer.anchoredPosition = Vector2.zero;
            pointer.GetComponent<DoodleRoulettePointer>().raycastTarget = false;
            var spin = UiKit.Button(body, rouletteSpinning ? "돌리는 중" : "돌리기", () => StartCoroutine(SpinRoulette(wheel)), UiKit.Blue, 60);
            spin.interactable = !rouletteSpinning && services.spins < serviceTuning.dailySpins;
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
            var row = UiKit.Row(card, "Buff", 94);
            UiKit.Icon(row, attack ? "Club" : "Gold", 84);
            var description = UiKit.Column(row, "Buff description", 4, 0);
            UiKit.Text(description, attack ? "공격력 버프" : "골드 버프", 27, TextAnchor.MiddleLeft, 38);
            UiKit.Text(description, (attack ? "공격력 +" : "골드 획득 +") + ((attack ? serviceTuning.attackBuff : serviceTuning.goldBuff) * 100).ToString("0") + "%", 23, TextAnchor.MiddleLeft, 32);
            ServiceText(card, () => (attack ? AttackBuffSeconds : GoldBuffSeconds) > 0 ? "활성화 중 · 남은 시간 " + ServiceClock(attack ? AttackBuffSeconds : GoldBuffSeconds) : "비활성", 22, 34);
            ServiceGauge(card, () => attack ? AttackBuffSeconds : GoldBuffSeconds, () => serviceTuning.buffSeconds, true);
            UiKit.Button(card, (attack ? AttackBuffSeconds : GoldBuffSeconds) > 0 ? "시간 연장" : "버프 활성화", () => ExtendBuff(attack), UiKit.Blue, 54);
            var cost = UiKit.Row(card, "Buff cost", 32);
            UiKit.Icon(cost, "Diamond", 28);
            UiKit.Text(cost, serviceTuning.buffPrice + " · " + (serviceTuning.buffSeconds / 60) + "분", 20, TextAnchor.MiddleCenter, 30);
        }

        public void ExtendBuff(bool attack)
        {
            if (Diamonds < serviceTuning.buffPrice) { Toast("다이아가 부족합니다"); return; }
            long expiry = attack ? services.attackExpiry : services.goldExpiry;
            expiry = Math.Max(expiry, ServiceNow) + TimeSpan.FromSeconds(serviceTuning.buffSeconds).Ticks;
            if (attack) services.attackExpiry = expiry; else services.goldExpiry = expiry;
            Diamonds -= serviceTuning.buffPrice; Save(); RefreshPage();
        }

        void ServiceGauge(Transform parent, Func<int> current, Func<int> maximum, bool clock = false)
        {
            var frame = UiKit.Box(parent, "Live progress gauge", new Color(.82f, .82f, .79f), 26);
            var fill = new GameObject("Progress fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(frame, false); fill.color = UiKit.Green; fill.raycastTarget = false;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            // Filled Image requires a sprite. WhiteTexture provides a plain stretchable fill.
            fill.sprite = ServiceSolidSprite;
            var rect = fill.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * 3; rect.offsetMax = Vector2.one * -3;
            var label = UiKit.Text(frame, "", 18, TextAnchor.MiddleCenter, 26);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            Func<string> value = () => clock ? ServiceClock(current()) : Math.Min(current(), maximum()).ToString("N0") + "/" + maximum().ToString("N0");
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
            var tabs = UiKit.Row(body, "Quest tabs", 50);
            string[] names = { "일일", "반복", "주간" };
            for (int i = 0; i < 3; i++) { int tab = i; UiKit.Button(tabs, names[i], () => { questTab = tab; RefreshPage(); }, questTab == i ? UiKit.Green : UiKit.Paper); }
            for (int i = 0; i < 4; i++) QuestCard(body, i);
            UiKit.Button(body, "일괄받기", () => ClaimQuests(-1), UiKit.Blue, 58);
        }

        string QuestResetLabel()
        {
            if (questTab == 1) return "완료 후 다시 도전";
            DateTime now = DateTime.UtcNow;
            DateTime reset = questTab == 0 ? now.Date.AddDays(1) : now.Date.AddDays(7 - (((int)now.DayOfWeek + 6) % 7));
            TimeSpan remaining = reset - now;
            return (questTab == 0 ? "일일 초기화 " : "주간 초기화 ") + (remaining.Days > 0 ? remaining.Days + "일 " : "") + remaining.Hours.ToString("00") + ":" + remaining.Minutes.ToString("00") + ":" + remaining.Seconds.ToString("00") + " (UTC)";
        }

        int QuestGoal(int tab, int index) => (tab == 0 ? serviceTuning.dailyGoals : tab == 1 ? serviceTuning.repeatGoals : serviceTuning.weeklyGoals)[index];
        int[] QuestCounters(int tab) => tab == 0 ? services.daily : tab == 1 ? services.repeat : services.weekly;
        bool QuestClaimed(int tab, int index) => tab == 0 ? services.dailyClaimed[index] : tab == 2 && services.weeklyClaimed[index];
        void QuestCard(Transform parent, int index)
        {
            int tab = questTab, metric = QuestMetrics[tab][index], goal = QuestGoal(tab, index);
            var card = ServiceCard(parent, "Quest " + tab + " " + index, UiKit.Paper);
            var row = UiKit.Row(card, "Quest summary", 100);
            string[] icons = { "BatA", "Gold", "Dungeon", "Roulette", "Club", "Banana", "Club", "Diamond" };
            UiKit.Icon(row, icons[metric], 54);
            var text = UiKit.Column(row, "Quest text", 5, 0); UiKit.Flexible(text, 3);
            string[] labels = { "적 {0}마리 처치", "골드 {0} 획득", "던전 {0}회 도전", "룰렛 {0}회 돌리기", "장비 {0}회 강화", "스킬 {0}회 강화", "PVP {0}회 도전", "뽑기 {0}회 진행" };
            UiKit.Text(text, string.Format(labels[metric], goal.ToString("N0")), 20, TextAnchor.MiddleLeft, 42);
            ServiceGauge(text, () => QuestCounters(tab)[metric], () => goal);
            var reward = UiKit.Column(row, "Quest reward", 2, 0); UiKit.Flexible(reward, .65f);
            UiKit.Icon(reward, "Diamond", 36);
            UiKit.Text(reward, serviceTuning.questRewards[index].ToString(), 18, TextAnchor.MiddleCenter, 26);
            var claim = UiKit.Button(row, "받기", () => ClaimQuests(index), UiKit.Yellow, 60);
            UiKit.Flexible(claim.transform, .9f);
            var claimText = claim.GetComponentInChildren<Text>();
            serviceBindings.Add(new ServiceBinding { text = claimText, value = () => QuestClaimed(tab, index) ? "받음" : QuestCounters(tab)[metric] >= goal ? "받기" : "진행 중" });
            claimText.text = QuestClaimed(tab, index) ? "받음" : QuestCounters(tab)[metric] >= goal ? "받기" : "진행 중";
        }

        public void ClaimQuests(int selected)
        {
            ResetServicePeriods(); int reward = 0;
            for (int i = 0; i < 4; i++)
            {
                if (selected >= 0 && i != selected) continue;
                int metric = QuestMetrics[questTab][i], goal = QuestGoal(questTab, i);
                if (QuestClaimed(questTab, i) || QuestCounters(questTab)[metric] < goal) continue;
                if (questTab == 1) services.repeat[metric] -= goal;
                else if (questTab == 0) services.dailyClaimed[i] = true;
                else services.weeklyClaimed[i] = true;
                reward += serviceTuning.questRewards[i];
            }
            if (reward == 0) { Toast("받을 수 있는 보상이 없습니다"); return; }
            GrantServiceDiamonds(reward, "퀘스트 보상 획득!");
        }

        void BuildDungeons(RectTransform body)
        {
            UiKit.Text(body, "필드 전투 연계 도전 · 적 " + serviceTuning.dungeonKills + "마리 처치", 19, TextAnchor.MiddleCenter, 34);
            string[] icons = { "Dungeon", "MushroomA", "DevilA" };
            string[] descriptions = { "골드 획득", "장비 획득", "스킬 획득" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var card = ServiceCard(body, DungeonNames[i], i == 0 ? UiKit.Yellow : i == 1 ? UiKit.Green : new Color(.89f, .84f, .96f));
                var row = UiKit.Row(card, "Dungeon", 108);
                UiKit.Icon(row, icons[i], 86);
                var info = UiKit.Column(row, "Dungeon info", 3, 0); UiKit.Flexible(info, 2);
                UiKit.Text(info, DungeonNames[i], 27, TextAnchor.MiddleLeft, 38);
                UiKit.Text(info, descriptions[i], 22, TextAnchor.MiddleLeft, 32);
                var key = UiKit.Row(card, "Independent daily attempts", 40);
                UiKit.Icon(key, "Key", 32);
                ServiceText(key, () => Math.Max(0, serviceTuning.dungeonAttempts - services.dungeonUsed[index]) + "/" + serviceTuning.dungeonAttempts + "  오늘 남은 도전", 21, 36);
                if (services.activeDungeon == i) ServiceGauge(card, () => services.dungeonProgress, () => serviceTuning.dungeonKills);
                var enter = UiKit.Button(card, services.activeDungeon == i ? "도전 진행 중" : "입장", () => EnterDungeon(index), UiKit.Blue, 52);
                enter.interactable = services.activeDungeon < 0 && services.dungeonUsed[i] < serviceTuning.dungeonAttempts;
            }
            UiKit.Text(body, "각 던전은 UTC 00:00에 각각 3회 충전", 17, TextAnchor.MiddleCenter, 28);
        }

        public void EnterDungeon(int index)
        {
            ResetServicePeriods();
            if (index < 0 || index > 2 || services.activeDungeon >= 0 || services.dungeonUsed[index] >= serviceTuning.dungeonAttempts) return;
            services.dungeonUsed[index]++; services.activeDungeon = index; services.dungeonProgress = 0;
            lastServiceKills = game ? game.Kills : 0; RecordServiceProgress("dungeon", 1); Save(); RefreshPage();
            Toast(DungeonNames[index] + " 도전 시작! 필드의 적을 처치하세요");
        }

        void CompleteDungeon()
        {
            int index = services.activeDungeon;
            services.activeDungeon = -1; services.dungeonProgress = 0;
            var rewards = new List<UiReward>();
            if (index == 0)
            {
                Gold += serviceTuning.dungeonGold; RecordServiceProgress("gold", serviceTuning.dungeonGold);
                rewards.Add(new UiReward { name = "", icon = "Gold", amount = serviceTuning.dungeonGold, rarity = 0 });
            }
            else
            {
                string category = index == 1 ? (serviceRandom.Next(2) == 0 ? "Armor" : "Club") : "Skill";
                UiItem item = GrantItem(category, serviceRandom); AddItem(item, 1);
                rewards.Add(new UiReward { name = item.name, icon = item.icon, amount = 1, rarity = item.rarity });
            }
            Save(); if (ActivePage == "Dungeons") RefreshPage();
            ShowRewards("던전 클리어!\n" + DungeonNames[index], rewards);
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
            UiKit.Text(body, "로컬 모의 PVP · 예시 랭킹 / 서버 미연결", 18, TextAnchor.MiddleCenter, 32);
            var ranks = LocalRanking();
            var podium = UiKit.Row(body, "Top three podium", 190);
            foreach (int position in new[] { 1, 0, 2 })
            {
                var rank = ranks[position];
                var card = ServiceCard(podium, "Podium rank " + (position + 1), position == 0 ? UiKit.Yellow : position == 1 ? new Color(.85f, .88f, .91f) : new Color(.87f, .72f, .56f));
                UiKit.Text(card, rank.name, 20, TextAnchor.MiddleCenter, 27);
                var image = UiKit.Icon(card, rank.art, position == 0 ? 78 : 62);
                image.GetComponent<LayoutElement>().flexibleWidth = 1;
                UiKit.Text(card, (position + 1).ToString(), 33, TextAnchor.MiddleCenter, 40);
            }
            int selfIndex = ranks.FindIndex(r => r.self);
            var mine = ServiceCard(body, "My rank", UiKit.Yellow);
            var myRow = UiKit.Row(mine, "My ranking", 72);
            UiKit.Icon(myRow, "Player", 62);
            UiKit.Text(myRow, "내 순위 " + (selfIndex + 1) + "위\n" + PlayerName, 22, TextAnchor.MiddleLeft, 66);
            UiKit.Text(myRow, "승점 " + services.pvpPoints.ToString("N0") + "\n전투력 " + Power.ToString("N0"), 20, TextAnchor.MiddleRight, 66);
            var challenge = UiKit.Button(body, "모의 대전 시작  ·  " + Math.Max(0, serviceTuning.pvpAttempts - services.pvpUsed) + "/" + serviceTuning.pvpAttempts, PlayLocalPvp, UiKit.Blue, 56);
            challenge.interactable = services.pvpUsed < serviceTuning.pvpAttempts;
            UiKit.Text(body, "랭킹 1~100위", 25, TextAnchor.MiddleCenter, 42);
            var header = UiKit.Row(body, "Ranking columns", 32);
            UiKit.Text(header, "순위", 18, TextAnchor.MiddleCenter, 30);
            UiKit.Text(header, "이름", 18, TextAnchor.MiddleCenter, 30);
            UiKit.Text(header, "승점", 18, TextAnchor.MiddleCenter, 30);
            UiKit.Text(header, "전투력", 18, TextAnchor.MiddleCenter, 30);
            var listFrame = UiKit.Box(body, "Scrollable ranking 1 to 100", UiKit.Paper, 330);
            var viewport = new GameObject("Ranking viewport", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
            viewport.SetParent(listFrame, false); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = new Vector2(5, 5); viewport.offsetMax = new Vector2(-5, -5);
            viewport.GetComponent<Image>().color = UiKit.Paper; viewport.GetComponent<Mask>().showMaskGraphic = false;
            var rankingBody = UiKit.Column(viewport, "Ranking content", 6, 3);
            rankingBody.anchorMin = new Vector2(0, 1); rankingBody.anchorMax = Vector2.one; rankingBody.pivot = new Vector2(.5f, 1); rankingBody.sizeDelta = Vector2.zero;
            var rankingScroll = listFrame.gameObject.AddComponent<ScrollRect>(); rankingScroll.viewport = viewport; rankingScroll.content = rankingBody; rankingScroll.horizontal = false;
            rankingScroll.movementType = ScrollRect.MovementType.Clamped; rankingScroll.scrollSensitivity = 35;
            for (int i = 0; i < 100; i++)
            {
                var rank = ranks[i];
                var card = ServiceCard(rankingBody, "Rank " + (i + 1), rank.self ? UiKit.Yellow : UiKit.Paper);
                var row = UiKit.Row(card, "Player rank", 44, 4);
                var number = UiKit.Text(row, (i + 1).ToString(), 22, TextAnchor.MiddleCenter, 42); UiKit.Flexible(number.transform, .45f);
                UiKit.Icon(row, rank.art, 38);
                var name = UiKit.Text(row, rank.name, 18, TextAnchor.MiddleLeft, 42); UiKit.Flexible(name.transform, 1.4f);
                UiKit.Text(row, rank.points.ToString("N0"), 18, TextAnchor.MiddleCenter, 42);
                UiKit.Text(row, rank.power.ToString("N0"), 18, TextAnchor.MiddleCenter, 42);
            }
            mine.SetAsLastSibling(); challenge.transform.SetAsLastSibling();
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
            UiKit.Text(body, "전체 채팅 · 로컬 데모", 27, TextAnchor.MiddleCenter, 48);
            UiKit.Text(body, "네트워크 미연결 · 메시지는 다른 사람에게 전송되지 않습니다", 18, TextAnchor.MiddleCenter, 50);
            foreach (var message in localMessages)
            {
                float messageHeight = Mathf.Max(122, 64 + Mathf.CeilToInt(message.content.Length / 20f) * 29);
                var row = UiKit.Row(body, "Chat message", messageHeight, 10);
                if (!message.self) UiKit.Icon(row, message.art, 62);
                var bubble = ServiceCard(row, "Message bubble", message.self ? UiKit.Yellow : UiKit.Paper); UiKit.Flexible(bubble, 5);
                UiKit.Text(bubble, message.author, 20, message.self ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, 28);
                var text = UiKit.Text(bubble, message.content, 23, TextAnchor.UpperLeft, messageHeight - 62); text.supportRichText = false;
                if (message.self) UiKit.Icon(row, message.art, 62);
            }
            var inputRow = UiKit.Row(body, "Chat composer", 62);
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

            UiKit.Text(body, "메시지는 이 실행 동안만 보관됩니다", 17, TextAnchor.MiddleCenter, 26);
            StartCoroutine(ScrollChatToLatest(body));
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
            UiKit.Text(account, "계정연동 · 연동 안 됨", 25, TextAnchor.MiddleLeft, 40);
            UiKit.Button(account, "연동하기", () => ShowDetail("계정연동", panel => UiKit.Text(panel, "계정 제공자와 서버가 연결되지 않았습니다.\n진행 상황은 현재 기기에만 저장됩니다.", 23, TextAnchor.MiddleCenter, 104)), UiKit.Blue);
            var power = ServiceCard(body, "Power saving", UiKit.Paper);
            UiKit.Button(power, "절전모드  " + (services.powerSaving ? "켜짐 · 30 FPS" : "꺼짐 · 60 FPS"), () =>
            {
                services.powerSaving = !services.powerSaving;
                Application.targetFrameRate = services.powerSaving ? 30 : 60; Save(); RefreshPage();
            }, services.powerSaving ? UiKit.Green : UiKit.Paper);
            ServiceVolume(body, "배경음", true); ServiceVolume(body, "효과음", false);
            UiKit.Button(body, game && game.paused ? "계속하기" : "일시정지", () =>
            {
                if (game) game.TogglePause();
                RefreshPage();
            }, UiKit.Paper, 50);
            if (FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length == 0)
                UiKit.Text(body, "사운드 소스 미등록 · 음량 설정은 저장됩니다", 17, TextAnchor.MiddleCenter, 40);
            UiKit.Button(body, "게임종료", () => ShowDetail("게임을 종료할까요?", panel =>
            {
                UiKit.Text(panel, "현재 진행 상황을 저장합니다", 23, TextAnchor.MiddleCenter, 48);
                UiKit.Button(panel, "게임종료", () => { Save(); PlayerPrefs.Save(); Application.Quit(); }, UiKit.Red, 58);
            }), UiKit.Red, 62);
        }

        void ServiceVolume(Transform parent, string title, bool music)
        {
            var card = ServiceCard(parent, title, UiKit.Paper);
            var label = UiKit.Text(card, title, 25, TextAnchor.MiddleLeft, 35);
            var track = UiKit.Box(card, title + " slider", new Color(.77f, .77f, .74f), 40);
            var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
            var fill = new GameObject("Volume fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); fill.transform.SetParent(track, false); fill.color = UiKit.Green;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one; fill.rectTransform.offsetMin = new Vector2(4, 8); fill.rectTransform.offsetMax = new Vector2(-4, -8);
            var handle = new GameObject("Volume handle", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); handle.transform.SetParent(track, false); handle.color = UiKit.Paper;
            handle.rectTransform.sizeDelta = new Vector2(28, 40); handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, .5f);
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.value = music ? services.music : services.effects;
            label.text = title + "  " + Mathf.RoundToInt(slider.value * 100) + "%";
            slider.onValueChanged.AddListener(value =>
            {
                if (music) services.music = value; else services.effects = value;
                label.text = title + "  " + Mathf.RoundToInt(value * 100) + "%";
                ApplyServiceAudioSettings(); SaveServices();
            });
        }

        public void ApplyServiceAudioSettings()
        {
            if (services == null) return;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) source.volume = source.loop ? services.music : services.effects;
        }
    }

    public sealed class DoodleRoulettePointer : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 a = new Vector2(-21, 19), b = new Vector2(21, 19), c = new Vector2(0, -20);
            vh.AddVert(a, UiKit.Ink, Vector2.zero); vh.AddVert(b, UiKit.Ink, Vector2.zero); vh.AddVert(c, UiKit.Ink, Vector2.zero); vh.AddTriangle(0, 1, 2);
            vh.AddVert(a * .74f, UiKit.Red, Vector2.zero); vh.AddVert(b * .74f, UiKit.Red, Vector2.zero); vh.AddVert(c * .74f, UiKit.Red, Vector2.zero); vh.AddTriangle(3, 4, 5);
        }
    }

    /// <summary>Native uGUI vector mesh; roulette remains sharp at every supported aspect ratio.</summary>
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
                Triangle(helper, Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 33, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 33, UiKit.Ink);
                Triangle(helper, Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 29, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 29, UiKit.Paper);
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
