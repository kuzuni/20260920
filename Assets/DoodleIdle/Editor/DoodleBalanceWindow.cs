using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DoodleIdle.Editor
{
    public sealed class DoodleBalanceWindow : OdinEditorWindow
    {
        const string TuningPath = "Assets/DoodleIdle/Resources/DoodleIdle/UI/ServicesTuning.json";
        const string CollectionPath = "Assets/DoodleIdle/Resources/DoodleIdle/UI/Collections.json";
        static readonly string[] Names = { "골드 보상", "적 체력", "적 데미지", "공격력 · 체력 · 회복 강화 비용", "x2 치명타 강화 비용", "x4 치명타 강화 비용", "x8 치명타 강화 비용", "x16 치명타 강화 비용", "x32 치명타 강화 비용", "x64 치명타 강화 비용", "x128 치명타 강화 비용" };
        [SerializeField, HideInInspector] DoodleUi.ServiceTuning draft = new DoodleUi.ServiceTuning(), loaded = new DoodleUi.ServiceTuning();
        [SerializeField, HideInInspector] UiStatCostTuning statDraft = new UiStatCostTuning(), statLoaded = new UiStatCostTuning();
        [SerializeField, HideInInspector] int selected, previewPosition = 70;
        string savedDraft, savedStatDraft;
        Vector2 workspaceScroll;
        DoodleIdleGame Game => UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
        DoodleUi Ui => EditorApplication.isPlaying && Game && Game.Ready ? Game.Ui : null;
        int Origin => selected >= 3 ? 0 : 1;
        string AxisName => selected >= 3 ? "강화 전 레벨" : "스테이지";
        string StatId => selected >= 4 ? DoodleUi.CriticalStatIds[selected - 4] : "attack";

        [MenuItem("Doodle Idle/밸런스 조절")]
        public static void Open()
        {
            var window = GetWindow<DoodleBalanceWindow>("밸런스 조절");
            window.minSize = new Vector2(340, 300);
            window.Show();
        }
        protected override void OnEnable()
        {
            base.OnEnable(); UseScrollView = false; minSize = new Vector2(340, 300);
            Undo.undoRedoPerformed -= OnUndoRedo; Undo.undoRedoPerformed += OnUndoRedo;
            LoadCurrent();
        }
        protected override void OnDestroy() { Undo.undoRedoPerformed -= OnUndoRedo; base.OnDestroy(); }
        void OnInspectorUpdate() => Repaint();
        void OnUndoRedo() { AutoSaveChanges(); Repaint(); }
        public void AutoSaveChanges()
        {
            if (!EditorApplication.isPlaying) return;
            if (JsonUtility.ToJson(draft) == savedDraft && JsonUtility.ToJson(statDraft) == savedStatDraft) return;
            SaveDefaults(false);
        }
        void Record(string action) => Undo.RecordObject(this, action);

        DoodleGrowthStep[] GrowthSteps
        {
            get {
                switch (selected) {
                    case 0: return draft.goldGrowthSteps ?? Array.Empty<DoodleGrowthStep>();
                    case 1: return draft.enemyHealthGrowthSteps ?? Array.Empty<DoodleGrowthStep>();
                    case 2: return draft.enemyDamageGrowthSteps ?? Array.Empty<DoodleGrowthStep>();
                    case 3: return statDraft.commonGrowthSteps ?? Array.Empty<DoodleGrowthStep>();
                    case 4: return statDraft.critical2GrowthSteps ?? Array.Empty<DoodleGrowthStep>();
                    default: return statDraft.critical4GrowthSteps ?? Array.Empty<DoodleGrowthStep>();
                }
            }
            set {
                switch (selected) {
                    case 0: draft.goldGrowthSteps = value; break;
                    case 1: draft.enemyHealthGrowthSteps = value; break;
                    case 2: draft.enemyDamageGrowthSteps = value; break;
                    case 3: statDraft.commonGrowthSteps = value; break;
                    case 4: statDraft.critical2GrowthSteps = value; break;
                    default: statDraft.critical4GrowthSteps = value; break;
                }
            }
        }

        double StartValue
        {
            get {
                switch (selected) {
                    case 0: return draft.goldPerEnemy; case 1: return draft.enemyStartingHealth; case 2: return draft.enemyStartingDamage;
                    case 3: return statDraft.commonBaseCost; case 4: return statDraft.critical2BaseCost; default: return statDraft.critical4BaseCost;
                }
            }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                value = Math.Max(selected >= 3 ? 1 : selected == 1 ? .001 : 0, Math.Min(selected >= 3 ? int.MaxValue : 1000000d, value));
                switch (selected) {
                    case 0: draft.goldPerEnemy = (float)value; break; case 1: draft.enemyStartingHealth = (float)value; break; case 2: draft.enemyStartingDamage = (float)value; break;
                    case 3: statDraft.commonBaseCost = (int)Math.Round(value); break; case 4: statDraft.critical2BaseCost = (int)Math.Round(value); break; default: statDraft.critical4BaseCost = (int)Math.Round(value); break;
                }
            }
        }
        float Growth
        {
            get {
                switch (selected) {
                    case 0: return draft.goldStageGrowth; case 1: return draft.enemyHealthStageGrowth; case 2: return draft.enemyDamageStageGrowth;
                    case 3: return statDraft.commonGrowth; case 4: return statDraft.critical2Growth; default: return statDraft.critical4Growth;
                }
            }
            set {
                if (float.IsNaN(value) || float.IsInfinity(value)) return;
                value = Mathf.Clamp(value, 0, 1000000);
                switch (selected) {
                    case 0: draft.goldStageGrowth = value; break; case 1: draft.enemyHealthStageGrowth = value; break; case 2: draft.enemyDamageStageGrowth = value; break;
                    case 3: statDraft.commonGrowth = value; break; case 4: statDraft.critical2Growth = value; break; default: statDraft.critical4Growth = value; break;
                }
            }
        }
        double Sample(DoodleUi.ServiceTuning services, UiStatCostTuning stats, int at)
        {
            switch (selected) {
                case 0: return DoodleUi.GoldForMainKills(services, at, 1);
                case 1: return 68d * DoodleUi.EnemyHealthMultiplier(services, at);
                case 2: return 64d * DoodleUi.EnemyDamageMultiplier(services, at);
                default: return DoodleUi.StatUpgradePrice(stats, StatId, at);
            }
        }
        double DraftValue(int at) => Sample(draft, statDraft, at);
        static string Number(double value) => value >= 1e9 ? value.ToString("0.###E+0") : value.ToString("N2");

        [OnInspectorGUI]
        void DrawWorkspace()
        {
            workspaceScroll = EditorGUILayout.BeginScrollView(workspaceScroll,
                GUILayout.Height(Mathf.Max(60, position.height - 32)), GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(280), GUILayout.ExpandWidth(true));
            DrawSettings();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        static void WrappedLabel(string text, bool bold = false)
        {
            var style = new GUIStyle(EditorStyles.wordWrappedLabel);
            if (bold) style.fontStyle = FontStyle.Bold;
            GUILayout.Label(text, style, GUILayout.ExpandWidth(true));
        }
        static int IntInput(string label, int value) { WrappedLabel(label); return EditorGUILayout.IntField(value); }
        static float FloatInput(string label, float value) { WrappedLabel(label); return EditorGUILayout.FloatField(value); }
        static double DoubleInput(string label, double value) { WrappedLabel(label); return EditorGUILayout.DoubleField(value); }
        void DrawSettings()
        {
            WrappedLabel("밸런스 수치 설정", true);
            WrappedLabel("설정 항목");
            int choice = EditorGUILayout.Popup(selected, Names);
            if (choice != selected) { selected = choice; previewPosition = Math.Max(Origin, previewPosition); GUIUtility.hotControl = 0; }
            EditorGUILayout.HelpBox("시작값과 증가율로 밸런스를 설정합니다. 플레이 중 수정값은 즉시 적용되고 자동 저장됩니다. 플레이 종료 후에도 유지됩니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(selected >= 5);
            double start = DoubleInput(selected >= 3 ? "시작 강화 비용" : selected == 0 ? "시작 골드 / 1마리" : "시작값", (selected >= 5 ? DoodleUi.StatUpgradePrice(statDraft, StatId, 0) : StartValue));
            float growth = FloatInput(selected == 2 ? "초반 목표 이후 증가율 (%)" : "단계당 증가율 (%)", (selected >= 5 ? DoodleUi.CriticalContinuationGrowth(statDraft) : Growth) * 100);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck()) { Record("시작값과 증가율 변경"); StartValue = start; Growth = growth / 100; }
            if (selected == 2) {
                EditorGUI.BeginChangeCheck();
                int earlyStage = IntInput("초반 목표 스테이지", draft.earlyEnemyDamageEndStage);
                float earlyDamage = FloatInput("초반 목표 데미지", draft.earlyEnemyDamageMax);
                if (EditorGUI.EndChangeCheck()) { Record("초반 데미지 변경"); draft.earlyEnemyDamageEndStage = Math.Max(2, earlyStage); draft.earlyEnemyDamageMax = float.IsNaN(earlyDamage) || float.IsInfinity(earlyDamage) ? 100 : Mathf.Clamp(earlyDamage, 0, 1000000); }
            }
            if (selected < 5) DrawGrowthSteps();
            string formula = selected == 2
                ? "n ≤ E: 기본값 = 시작값 + (목표값 − 시작값) × (n − 1) / (E − 1)\nn > E: 기본값 = 목표값 × (1 + r)^(n − E)\nE = 초반 목표 스테이지"
                : "기본값 = 시작값 × (1 + r)^(n − " + Origin + ")";
            if (GrowthSteps.Length > 0) formula = "구간 추가 전 기본 공식\n" + formula;
            formula += "\nr = 증가율 ÷ 100";
            if (GrowthSteps.Length > 0) formula += "\n구간 설정 시: 직전 값 × (1 + 해당 단계 증가율)\n구간별 누적 곱으로 계산하며 시작값을 재설정하지 않습니다.";
            formula += "\n기본값·최종값 계산 상한: 1e30";
            formula += selected >= 3 ? "\n강화 비용: 최소 1골드, 소수점 올림" : selected == 0 ? "\n골드: 소수점 반올림, 실제 지급 시 유물·버프 추가" : selected == 1 ? "\n일반 적 기준, 보스 체력 ×20" : "\n일반 적 접촉 데미지 기준 (기본 접촉값 64)";
            if (selected >= 5) formula = "첫 강화 비용은 앞 치명타 단계의 마지막 강화 비용입니다. 이후 증가율은 x2 만렙 시점의 증가율을 이어받습니다. x2 비용 설정에서 함께 조절합니다.";
            EditorGUILayout.HelpBox(formula, MessageType.None);
            if (selected >= 3) EditorGUILayout.HelpBox("공격력·체력·회복은 같은 비용 곡선입니다. 치명타는 x2 → x4 → x8 → x16 → x32 → x64 → x128 순서로 해금되며 강화 비용이 이어집니다.", MessageType.None);
            WrappedLabel("정확한 수치 비교", true);
            previewPosition = Math.Max(Origin, IntInput(AxisName, previewPosition));
            var current = Ui ? Ui.ReadBalanceTuning() : loaded;
            var currentStats = Ui ? Ui.ReadStatCostTuning() : statLoaded;
            WrappedLabel("기존 → 수정\n" + Number(Sample(current, currentStats, previewPosition)) + " → " + Number(DraftValue(previewPosition)));
            WrappedLabel("시작 단계 → 수정\n" + Number(Sample(current, currentStats, Origin)) + " → " + Number(DraftValue(Origin)));
            if (Ui && selected < 3 && GUILayout.Button("현재 스테이지 수치 보기")) previewPosition = Ui.CombatDifficultyStage;
            EditorGUILayout.Space(10);
            WrappedLabel("플레이어 자동 이동", true);
            EditorGUI.BeginChangeCheck();
            float keepDistance = FloatInput("적과 유지할 거리 (충돌 영역 바깥 여유 거리)", draft.playerKeepDistance);
            if (EditorGUI.EndChangeCheck()) {
                Record("자동 이동 유지 거리 변경");
                draft.playerKeepDistance = float.IsNaN(keepDistance) || float.IsInfinity(keepDistance) ? .6f : Mathf.Clamp(keepDistance, 0, 1000000);
            }
            WrappedLabel("기본 0.6 월드 단위. 커질수록 더 멀리 떨어집니다. 보스 크기도 반영하며 자동 대시도 접근을 멈춥니다. 0이면 기존 접근 이동입니다. 직접 드래그 조작에는 적용하지 않습니다. 유지 거리에 따라 공격 사거리가 늘어나지는 않으므로 너무 크게 설정하면 공격이 닿지 않을 수 있습니다.");
            using (new EditorGUI.DisabledScope(!Ui)) if (GUILayout.Button("실행 중인 게임에 적용", GUILayout.Height(34))) Apply();
            if (GUILayout.Button("기본값으로 저장", GUILayout.Height(34))) SaveDefaults();
            if (GUILayout.Button("현재 값 다시 불러오기")) LoadCurrent();
            EditorGUILayout.HelpBox("플레이 중에는 모든 밸런스 수치와 자동 이동 거리를 자동 저장합니다. 편집 모드에서는 기본값 저장 버튼으로 저장합니다.", MessageType.None);
            AutoSaveChanges();
        }
        void DrawGrowthSteps()
        {
            WrappedLabel("증가율 변경 구간", true);
            WrappedLabel("시작 단계를 추가하면 그 단계로 넘어갈 때부터 새 증가율을 사용합니다. 예: 100부터 3% → 99단계 값 × 1.03이 100단계 값입니다. 구간이 없으면 위의 기본 증가율을 계속 사용합니다.");
            if (selected == 2) WrappedLabel("초반 목표 단계까지는 선형 증가하며, 추가 구간 증가율은 그 이후부터 적용됩니다.");
            var steps = new List<DoodleGrowthStep>(DoodleGrowthStep.Copy(GrowthSteps));
            steps.Sort((a, b) => a.from.CompareTo(b.from));
            WrappedLabel("기본 구간: " + Origin + " ~ " + (steps.Count == 0 ? "이후 계속" : (steps[0].from - 1).ToString("N0")) + " · " + (Growth * 100).ToString("0.###") + "%");
            for (int i = 0; i < steps.Count; i++) {
                var step = steps[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                string end = i + 1 < steps.Count ? (steps[i + 1].from - 1).ToString("N0") : "이후 계속";
                WrappedLabel("구간 " + (i + 1) + " · " + step.from.ToString("N0") + " ~ " + end, true);
                EditorGUI.BeginChangeCheck();
                int from = IntInput("변경 시작 " + AxisName, step.from);
                float percent = FloatInput("이 구간 증가율 (%)", step.growth * 100);
                if (EditorGUI.EndChangeCheck()) {
                    Record("증가율 구간 수정");
                    long lower = i == 0 ? (long)Origin + 1 : (long)steps[i - 1].from + 1;
                    long upper = i + 1 < steps.Count ? (long)steps[i + 1].from - 1 : int.MaxValue;
                    step.from = (int)Math.Max(lower, Math.Min(upper, from));
                    step.growth = float.IsNaN(percent) || float.IsInfinity(percent) ? 0 : Mathf.Clamp(percent / 100, 0, 1000000);
                    GrowthSteps = steps.ToArray();
                }
                WrappedLabel("변경 직전 → 시작 단계 수치\n" + Number(DraftValue(Math.Max(Origin, step.from - 1))) + " → " + Number(DraftValue(step.from)));
                bool remove = GUILayout.Button("이 구간 삭제");
                EditorGUILayout.EndVertical();
                if (remove) { Record("증가율 구간 삭제"); steps.RemoveAt(i); GrowthSteps = steps.ToArray(); break; }
            }
            int last = steps.Count > 0 ? steps[steps.Count - 1].from : selected == 2 ? draft.earlyEnemyDamageEndStage : Origin;
            using (new EditorGUI.DisabledScope(last == int.MaxValue)) if (GUILayout.Button("+ 증가율 변경 구간 추가", GUILayout.Height(30))) {
                Record("증가율 구간 추가");
                steps.Add(new DoodleGrowthStep { from = (int)Math.Min(int.MaxValue, (long)last + (steps.Count == 0 && selected != 2 ? 100 - Origin : 100)), growth = steps.Count > 0 ? steps[steps.Count - 1].growth : Growth });
                GrowthSteps = steps.ToArray();
            }
        }
        public void Apply() => SaveDefaults();
        public void SaveDefaults() => SaveDefaults(true);
        void SaveDefaults(bool notify)
        {
            var services = JsonUtility.FromJson<DoodleUi.ServiceTuning>(File.ReadAllText(TuningPath));
            var collections = JsonUtility.FromJson<UiCollectionTuning>(File.ReadAllText(CollectionPath));
            DoodleUi.CopyBalanceTuning(draft, services); DoodleUi.CopyStatCostTuning(statDraft, collections.statCosts);
            File.WriteAllText(TuningPath, JsonUtility.ToJson(services, true) + "\n");
            File.WriteAllText(CollectionPath, JsonUtility.ToJson(collections, true) + "\n");
            AssetDatabase.ImportAsset(TuningPath); AssetDatabase.ImportAsset(CollectionPath);
            if (Ui) { Ui.ApplyBalanceTuning(services); Ui.ApplyStatCostTuning(collections.statCosts); }
            draft = services; statDraft = collections.statCosts;
            loaded = JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(services));
            statLoaded = JsonUtility.FromJson<UiStatCostTuning>(JsonUtility.ToJson(statDraft));
            savedDraft = JsonUtility.ToJson(draft); savedStatDraft = JsonUtility.ToJson(statDraft);
            if (notify) ShowNotification(new GUIContent("모든 수치를 적용하고 기본값으로 저장했습니다."));
        }
        public void LoadCurrent()
        {
            draft = Ui ? Ui.ReadBalanceTuning() : File.Exists(TuningPath) ? JsonUtility.FromJson<DoodleUi.ServiceTuning>(File.ReadAllText(TuningPath)) : new DoodleUi.ServiceTuning();
            statDraft = Ui ? Ui.ReadStatCostTuning() : File.Exists(CollectionPath) ? JsonUtility.FromJson<UiCollectionTuning>(File.ReadAllText(CollectionPath)).statCosts : new UiStatCostTuning();
            loaded = JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(draft));
            statLoaded = JsonUtility.FromJson<UiStatCostTuning>(JsonUtility.ToJson(statDraft));
            savedDraft = JsonUtility.ToJson(draft); savedStatDraft = JsonUtility.ToJson(statDraft);
        }
    }
}
