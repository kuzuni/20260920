using System;
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
        static readonly string[] Names = { "골드 보상", "적 체력", "적 데미지", "공격력 · 체력 · 회복 강화 비용", "x2 치명타 강화 비용", "x4 치명타 강화 비용" };
        [SerializeField, HideInInspector] DoodleUi.ServiceTuning draft = new DoodleUi.ServiceTuning(), loaded = new DoodleUi.ServiceTuning();
        [SerializeField, HideInInspector] UiStatCostTuning statDraft = new UiStatCostTuning(), statLoaded = new UiStatCostTuning();
        [SerializeField, HideInInspector] int selected, previewPosition = 70;
        Vector2 workspaceScroll;
        DoodleIdleGame Game => UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
        DoodleUi Ui => EditorApplication.isPlaying && Game && Game.Ready ? Game.Ui : null;
        int Origin => selected >= 3 ? 0 : 1;
        string AxisName => selected >= 3 ? "강화 전 레벨" : "스테이지";
        string StatId => selected == 4 ? "crit2Chance" : selected == 5 ? "crit4Chance" : "attack";

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
            Undo.undoRedoPerformed -= Repaint; Undo.undoRedoPerformed += Repaint;
            LoadCurrent();
        }
        protected override void OnDestroy() { Undo.undoRedoPerformed -= Repaint; base.OnDestroy(); }
        void OnInspectorUpdate() => Repaint();
        void Record(string action) => Undo.RecordObject(this, action);

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
            EditorGUILayout.HelpBox("시작값과 증가율로 밸런스를 설정합니다. 수정값은 미리보기이며 적용/저장 버튼을 눌러 반영합니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            double start = DoubleInput(selected >= 3 ? "시작 강화 비용" : selected == 0 ? "시작 골드 / 1마리" : "시작값", StartValue);
            float growth = FloatInput(selected == 2 ? "초반 목표 이후 증가율 (%)" : "단계당 증가율 (%)", Growth * 100);
            if (EditorGUI.EndChangeCheck()) { Record("시작값과 증가율 변경"); StartValue = start; Growth = growth / 100; }
            if (selected == 2) {
                EditorGUI.BeginChangeCheck();
                int earlyStage = IntInput("초반 목표 스테이지", draft.earlyEnemyDamageEndStage);
                float earlyDamage = FloatInput("초반 목표 데미지", draft.earlyEnemyDamageMax);
                if (EditorGUI.EndChangeCheck()) { Record("초반 데미지 변경"); draft.earlyEnemyDamageEndStage = Math.Max(2, earlyStage); draft.earlyEnemyDamageMax = float.IsNaN(earlyDamage) || float.IsInfinity(earlyDamage) ? 100 : Mathf.Clamp(earlyDamage, 0, 1000000); }
            }
            string formula = selected == 2
                ? "n ≤ E: 기본값 = 시작값 + (목표값 − 시작값) × (n − 1) / (E − 1)\nn > E: 기본값 = 목표값 × (1 + r)^(n − E)\nE = 초반 목표 스테이지"
                : "기본값 = 시작값 × (1 + r)^(n − " + Origin + ")";
            formula += "\nr = 증가율 ÷ 100";
            formula += "\n기본값·최종값 계산 상한: 1e30";
            formula += selected >= 3 ? "\n강화 비용: 최소 1골드, 소수점 올림" : selected == 0 ? "\n골드: 소수점 반올림, 실제 지급 시 유물·버프 추가" : selected == 1 ? "\n일반 적 기준, 보스 체력 ×20" : "\n일반 적 접촉 데미지 기준 (기본 접촉값 64)";
            EditorGUILayout.HelpBox(formula, MessageType.None);
            if (selected >= 3) EditorGUILayout.HelpBox("공격력·체력·회복은 같은 비용 곡선입니다. x2·x4는 각각 독립적입니다. 능력치는 일정 증가, x4 해금 조건은 유지됩니다.", MessageType.None);
            WrappedLabel("정확한 수치 비교", true);
            previewPosition = Math.Max(Origin, IntInput(AxisName, previewPosition));
            var current = Ui ? Ui.ReadBalanceTuning() : loaded;
            var currentStats = Ui ? Ui.ReadStatCostTuning() : statLoaded;
            WrappedLabel("기존 → 수정\n" + Number(Sample(current, currentStats, previewPosition)) + " → " + Number(DraftValue(previewPosition)));
            WrappedLabel("시작 단계 → 수정\n" + Number(Sample(current, currentStats, Origin)) + " → " + Number(DraftValue(Origin)));
            if (Ui && selected < 3 && GUILayout.Button("현재 스테이지 수치 보기")) previewPosition = Ui.CombatDifficultyStage;
            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(!Ui)) if (GUILayout.Button("실행 중인 게임에 적용", GUILayout.Height(34))) Apply();
            if (GUILayout.Button("기본값으로 저장", GUILayout.Height(34))) SaveDefaults();
            if (GUILayout.Button("현재 값 다시 불러오기")) LoadCurrent();
            EditorGUILayout.HelpBox("실행 중 적용은 이번 플레이에만 반영됩니다. 기본값 저장은 다음 실행에도 유지됩니다. 여섯 항목의 수치 설정을 함께 저장합니다.", MessageType.None);
        }
        public void Apply()
        {
            if (!Ui) return;
            Ui.ApplyBalanceTuning(draft); Ui.ApplyStatCostTuning(statDraft);
            draft = Ui.ReadBalanceTuning(); statDraft = Ui.ReadStatCostTuning();
            ShowNotification(new GUIContent("수치를 적용했습니다. 현재 레벨과 적의 남은 체력 비율은 유지됩니다."));
        }
        public void SaveDefaults()
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
            ShowNotification(new GUIContent("모든 수치를 기본값으로 저장했습니다."));
        }
        public void LoadCurrent()
        {
            draft = Ui ? Ui.ReadBalanceTuning() : File.Exists(TuningPath) ? JsonUtility.FromJson<DoodleUi.ServiceTuning>(File.ReadAllText(TuningPath)) : new DoodleUi.ServiceTuning();
            statDraft = Ui ? Ui.ReadStatCostTuning() : File.Exists(CollectionPath) ? JsonUtility.FromJson<UiCollectionTuning>(File.ReadAllText(CollectionPath)).statCosts : new UiStatCostTuning();
            loaded = JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(draft));
            statLoaded = JsonUtility.FromJson<UiStatCostTuning>(JsonUtility.ToJson(statDraft));
        }
    }
}
