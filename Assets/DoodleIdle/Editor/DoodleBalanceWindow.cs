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
        static readonly string[] Names = { "골드 보상", "적 체력", "적 데미지", "공격력 · 체력 · 회복 강화 비용", "x2 치명타 강화 비용", "x4 치명타 강화 비용" };
        static readonly string[] CurveNames = { "직선 연결", "부드러운 곡선 (자동)", "곡선 손잡이 (직접 조절)" };
        [SerializeField, HideInInspector] DoodleUi.ServiceTuning draft = new DoodleUi.ServiceTuning(), loaded = new DoodleUi.ServiceTuning();
        [SerializeField, HideInInspector] UiStatCostTuning statDraft = new UiStatCostTuning(), statLoaded = new UiStatCostTuning();
        [SerializeField, HideInInspector] int selected, graphEnd = 300, previewPosition = 70, diamondAmount = 10000;
        [SerializeField, HideInInspector] bool logarithmic = true;
        [SerializeField, HideInInspector] float graphHeadroom = 1.25f;
        [SerializeField, HideInInspector] int selectedPoint = -1;
        Vector2 workspaceScroll;
        float panelWidth;
        int dragPosition = -1, dragControl, dragTangent = -1;
        double dragMaximum;
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

        DoodleGrowthCurve Curve
        {
            get {
                switch (selected) {
                    case 0: return draft.goldCurve ?? (draft.goldCurve = new DoodleGrowthCurve());
                    case 1: return draft.enemyHealthCurve ?? (draft.enemyHealthCurve = new DoodleGrowthCurve());
                    case 2: return draft.enemyDamageCurve ?? (draft.enemyDamageCurve = new DoodleGrowthCurve());
                    case 3: return statDraft.commonCurve ?? (statDraft.commonCurve = new DoodleGrowthCurve());
                    case 4: return statDraft.critical2Curve ?? (statDraft.critical2Curve = new DoodleGrowthCurve());
                    default: return statDraft.critical4Curve ?? (statDraft.critical4Curve = new DoodleGrowthCurve());
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
        double BaseValue(int at)
        {
            if (selected == 2) {
                int end = Math.Max(2, draft.earlyEnemyDamageEndStage);
                return at <= end ? draft.enemyStartingDamage + (draft.earlyEnemyDamageMax - draft.enemyStartingDamage) * Math.Max(0d, (double)at - 1) / (end - 1)
                    : DoodleGrowthCurve.Exponential(draft.earlyEnemyDamageMax, Growth, at - end);
            }
            return DoodleGrowthCurve.Exponential(StartValue, Growth, Math.Max(0, at - Origin));
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
            // One scrollable workspace keeps every control reachable, including docked windows
            // smaller than minSize. Reserve scrollbar/padding space before choosing columns.
            float width = Mathf.Max(320, position.width - 40);
            workspaceScroll = EditorGUILayout.BeginScrollView(workspaceScroll,
                GUILayout.Height(Mathf.Max(60, position.height - 32)), GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            if (width >= 920) {
                EditorGUILayout.BeginHorizontal();
                float settingsWidth = Mathf.Clamp(width * .36f, 340, 440);
                EditorGUILayout.BeginVertical(GUILayout.Width(settingsWidth));
                panelWidth = settingsWidth;
                DrawSettings();
                EditorGUILayout.EndVertical();
                GUILayout.Space(16);
                EditorGUILayout.BeginVertical(GUILayout.Width(width - settingsWidth - 16));
                panelWidth = width - settingsWidth - 16;
                DrawGraphPanel();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            } else {
                panelWidth = width;
                DrawSettings();
                GUILayout.Space(20);
                DrawGraphPanel();
            }
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
            WrappedLabel("성장 공식 · 곡선 편집", true);
            WrappedLabel("설정 항목");
            int choice = EditorGUILayout.Popup(selected, Names);
            if (choice != selected) { selected = choice; selectedPoint = dragPosition = -1; previewPosition = Math.Max(Origin, previewPosition); GUIUtility.hotControl = 0; }
            EditorGUILayout.HelpBox("지수 증가를 기본으로 구간별 곡선을 편집합니다. 모든 게임에 공통인 단일 표준 공식은 없습니다. 그래프 수정은 미리보기이며 적용/저장 버튼을 눌러 반영합니다.", MessageType.Info);
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
            formula += "\nr = 증가율 ÷ 100\n최종값 = 기본값 × C(n)\nC(n): " + (Curve.interpolation == DoodleCurveInterpolation.Linear ? "점 사이 직선 보간" : "점과 양쪽 기울기를 사용하는 3차 곡선") + "\n마지막 점 이후 보정값 일정 · 점이 없으면 C(n) = 1";
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
            EditorGUILayout.HelpBox("실행 중 적용은 이번 플레이에만 반영됩니다. 기본값 저장은 다음 실행에도 유지됩니다. 여섯 항목의 설정과 편집점을 함께 저장합니다.", MessageType.None);
            EditorGUILayout.Space(8);
            WrappedLabel("다이아 디버그", true);
            diamondAmount = Math.Max(1, IntInput("지급량", diamondAmount));
            WrappedLabel("보유 다이아: " + (Ui ? Ui.Diamonds.ToString("N0") : "실행 중 사용 가능"));
            using (new EditorGUI.DisabledScope(!Ui)) if (GUILayout.Button("다이아 지급")) GrantDiamonds();
        }
        void DrawGraphPanel()
        {
            WrappedLabel(Names[selected] + " · 실제 계산 곡선", true);
            WrappedLabel("점을 연결하는 방식");
            int mode = EditorGUILayout.Popup((int)Curve.interpolation, CurveNames);
            if (mode != (int)Curve.interpolation) {
                Record("곡선 연결 방식 변경");
                if (mode != 0 && Curve.points.Length == 0) Curve.SetPoint(Math.Max(Origin + 1, graphEnd), 1, Origin);
                Curve.SetInterpolation((DoodleCurveInterpolation)mode, Origin);
                selectedPoint = Origin; dragPosition = dragTangent = -1; GUIUtility.hotControl = 0;
            }
            graphEnd = Math.Max(Origin + 1, IntInput("표시 끝 " + AxisName, graphEnd));
            logarithmic = GUILayout.Toggle(logarithmic, "로그 세로축");
            WrappedLabel("그래프 위쪽 여유");
            graphHeadroom = EditorGUILayout.Slider(graphHeadroom, 1.1f, 5);
            EditorGUILayout.HelpBox("초록선: 수정값 · 회색선: 기존값 · 파란 점: 시작값\n점 드래그: 단계/수치 변경 · 우클릭: 점 추가/삭제 · Ctrl+Z: 되돌리기\n직접 조절 모드: 점을 선택하고 보라색 손잡이를 위아래로 드래그해 곡선의 휘어짐을 바꿉니다.\n로그 축은 log10(1 + 값)입니다. 표시 범위는 밸런스에 영향을 주지 않습니다.", MessageType.None);
            float graphHeight = Mathf.Clamp((panelWidth - 90) * .65f, 220, 520);
            Rect area = GUILayoutUtility.GetRect(0, graphHeight, GUILayout.ExpandWidth(true));
            DrawGraph(area);
            DrawPointEditor();
        }
        double Scale(double value) => logarithmic ? Math.Log10(1 + Math.Max(0, value)) : Math.Max(0, value);
        double Unscale(double value) => logarithmic ? Math.Pow(10, Math.Max(0, value)) - 1 : Math.Max(0, value);
        Vector2 ToGraph(Rect plot, double at, double value, double maximum) => new Vector2(plot.x + (float)((at - Origin) / (graphEnd - (double)Origin)) * plot.width, plot.yMax - (float)Math.Min(1, Scale(value) / maximum) * plot.height);
        int FromGraphX(Rect plot, float x) => (int)Math.Round(Origin + Mathf.Clamp01((x - plot.x) / plot.width) * (graphEnd - (double)Origin));
        double FromGraphY(Rect plot, float y, double maximum) => Unscale(Mathf.Clamp01((plot.yMax - y) / plot.height) * maximum);
        void DrawGraph(Rect area)
        {
            Rect plot = new Rect(area.x + 75, area.y + 12, Math.Max(50, area.width - 90), Math.Max(50, area.height - 45));
            var current = Ui ? Ui.ReadBalanceTuning() : loaded;
            var currentStats = Ui ? Ui.ReadStatCostTuning() : statLoaded;
            double maximum = 1;
            for (int i = 0; i <= 160; i++) {
                int at = (int)Math.Round(Origin + i / 160d * (graphEnd - (double)Origin));
                maximum = Math.Max(maximum, Math.Max(Scale(DraftValue(at)), Scale(Sample(current, currentStats, at))));
            }
            foreach (var point in Curve.points) if (point.position <= graphEnd) maximum = Math.Max(maximum, Scale(DraftValue(point.position)));
            maximum *= graphHeadroom;
            if (dragPosition >= 0) maximum = dragMaximum;
            if (Event.current.type == EventType.Repaint) {
                EditorGUI.DrawRect(area, new Color(.12f, .13f, .15f));
                var tickStyle = new GUIStyle(EditorStyles.miniLabel);
                tickStyle.normal.textColor = new Color(.85f, .87f, .9f);
                Handles.BeginGUI();
                int divisions = plot.width < 400 ? 2 : 4;
                for (int i = 0; i <= divisions; i++) {
                    float fraction = i / (float)divisions;
                    float y = plot.yMax - plot.height * fraction;
                    Handles.color = new Color(.26f, .28f, .3f); Handles.DrawLine(new Vector3(plot.x, y), new Vector3(plot.xMax, y));
                    double axisValue = Unscale(maximum * fraction);
                    GUI.Label(new Rect(area.x, y - 9, 73, 20), axisValue >= 100000 ? axisValue.ToString("0.##E+0") : Number(axisValue), tickStyle);
                    int x = (int)Math.Round(Origin + fraction * (graphEnd - (double)Origin));
                    float px = plot.x + plot.width * fraction;
                    string tick = x >= 1000000 ? x.ToString("0.##E+0") : x.ToString("N0");
                    GUI.Label(new Rect(Mathf.Clamp(px - 20, area.x, area.xMax - 70), plot.yMax + 5, 70, 20), tick, tickStyle);
                }
                DrawLine(plot, maximum, at => Sample(current, currentStats, at), new Color(.55f, .57f, .6f));
                DrawLine(plot, maximum, DraftValue, new Color(.3f, .95f, .55f));
                DrawDot(ToGraph(plot, Origin, DraftValue(Origin), maximum), new Color(.3f, .65f, 1), selectedPoint == Origin);
                foreach (var point in Curve.points) if (point.position > Origin && point.position <= graphEnd)
                    DrawDot(ToGraph(plot, point.position, DraftValue(point.position), maximum), Color.yellow, selectedPoint == point.position);
                if (Curve.interpolation == DoodleCurveInterpolation.Manual && selectedPoint >= Origin && selectedPoint <= graphEnd) {
                    DrawTangentHandle(plot, maximum, true);
                    DrawTangentHandle(plot, maximum, false);
                }
                Handles.EndGUI();
            }
            HandleGraphInput(plot, maximum);
        }
        void DrawLine(Rect plot, double maximum, Func<int, double> sample, Color color)
        {
            var steps = new SortedSet<int>();
            for (int i = 0; i <= 160; i++) steps.Add((int)Math.Round(Origin + i / 160d * (graphEnd - (double)Origin)));
            int previous = Origin;
            foreach (var point in Curve.points) if (point.position > Origin && point.position <= graphEnd) {
                // Keep short, strongly curved segments visible even in a large stage range.
                for (int i = 1; i <= 8; i++) steps.Add((int)Math.Round(previous + i / 8d * ((double)point.position - previous)));
                previous = point.position;
            }
            var vertices = new Vector3[steps.Count]; int index = 0;
            foreach (int at in steps) vertices[index++] = ToGraph(plot, at, sample(at), maximum);
            Handles.color = color; Handles.DrawAAPolyLine(2, vertices);
        }
        static void DrawDot(Vector2 position, Color color, bool selected)
        {
            Handles.color = color; Handles.DrawSolidDisc(position, Vector3.forward, selected ? 6 : 4);
        }
        bool TangentHandle(bool incoming, out double at, out double value)
        {
            at = value = 0;
            if (selectedPoint < Origin || selectedPoint > graphEnd) return false;
            int neighbor = Curve.Neighbor(selectedPoint, Origin, incoming);
            if (neighbor < 0) return false;
            at = selectedPoint + ((double)neighbor - selectedPoint) / 3;
            if (at < Origin || at > graphEnd) return false;
            double factor = selectedPoint == Origin ? 1 : Curve.FindPoint(selectedPoint)?.factor ?? 1;
            value = BaseValue((int)Math.Round(at)) * Math.Max(0, factor + Curve.GetTangent(selectedPoint, Origin, incoming) * (at - selectedPoint));
            return true;
        }
        void DrawTangentHandle(Rect plot, double maximum, bool incoming)
        {
            if (!TangentHandle(incoming, out double at, out double value)) return;
            var handle = ToGraph(plot, at, value, maximum);
            Handles.color = new Color(.9f, .55f, 1);
            Handles.DrawLine(ToGraph(plot, selectedPoint, DraftValue(selectedPoint), maximum), handle);
            EditorGUI.DrawRect(new Rect(handle.x - 5, handle.y - 5, 10, 10), Handles.color);
        }
        void SetTangentValue(bool incoming, double value)
        {
            if (!TangentHandle(incoming, out double at, out _) || double.IsNaN(value) || double.IsInfinity(value)) return;
            double basis = BaseValue((int)Math.Round(at));
            if (basis <= 0) return;
            double factor = selectedPoint == Origin ? 1 : Curve.FindPoint(selectedPoint)?.factor ?? 1;
            Curve.SetTangent(selectedPoint, Origin, incoming, (Math.Max(0, value) / basis - factor) / (at - selectedPoint));
        }
        int HitPoint(Rect plot, double maximum, Vector2 mouse)
        {
            if (Vector2.Distance(mouse, ToGraph(plot, Origin, DraftValue(Origin), maximum)) < 10) return Origin;
            foreach (var point in Curve.points) if (point.position <= graphEnd && Vector2.Distance(mouse, ToGraph(plot, point.position, DraftValue(point.position), maximum)) < 10) return point.position;
            return -1;
        }
        void HandleGraphInput(Rect plot, double maximum)
        {
            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.ContextClick && plot.Contains(e.mousePosition)) {
                int hit = HitPoint(plot, maximum, e.mousePosition);
                int at = Math.Max(Origin + 1, FromGraphX(plot, e.mousePosition.x));
                double value = FromGraphY(plot, e.mousePosition.y, maximum);
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("이 위치에 변화점 추가"), false, () => { Record("성장 곡선 점 추가"); SetValuePoint(at, value); Repaint(); });
                if (hit > Origin) menu.AddItem(new GUIContent("선택한 점 삭제"), false, () => { Record("성장 곡선 점 삭제"); Curve.RemovePoint(hit); selectedPoint = -1; Repaint(); });
                else menu.AddDisabledItem(new GUIContent("선택한 점 삭제"));
                menu.ShowAsContext(); e.Use();
            }
            if (e.type == EventType.MouseDown && e.button == 0 && plot.Contains(e.mousePosition)) {
                if (Curve.interpolation == DoodleCurveInterpolation.Manual) {
                    for (int side = 0; side < 2; side++) if (TangentHandle(side == 0, out double at, out double value)
                        && Vector2.Distance(e.mousePosition, ToGraph(plot, at, value, maximum)) < 10) {
                        Undo.RegisterCompleteObjectUndo(this, "곡선 손잡이 이동"); dragPosition = selectedPoint; dragTangent = side;
                        dragMaximum = maximum; dragControl = control; GUIUtility.hotControl = control; e.Use(); break;
                    }
                }
            }
            if (e.type == EventType.MouseDown && e.button == 0 && plot.Contains(e.mousePosition)) {
                int hit = HitPoint(plot, maximum, e.mousePosition);
                if (hit >= 0) { Undo.RegisterCompleteObjectUndo(this, "성장 곡선 점 이동"); selectedPoint = dragPosition = hit; dragTangent = -1; dragMaximum = maximum; dragControl = control; GUIUtility.hotControl = control; e.Use(); }
            }
            if (e.type == EventType.MouseDrag && dragPosition >= 0 && GUIUtility.hotControl == dragControl) {
                double value = FromGraphY(plot, e.mousePosition.y, dragMaximum);
                if (dragTangent >= 0) SetTangentValue(dragTangent == 0, value);
                else if (dragPosition == Origin) StartValue = value;
                else {
                    int at = Math.Max(Origin + 1, FromGraphX(plot, e.mousePosition.x));
                    // Do not overwrite a neighboring point while dragging across it.
                    foreach (var point in Curve.points) {
                        if (point.position < dragPosition) at = Math.Max(at, point.position + 1);
                        if (point.position > dragPosition) at = Math.Min(at, point.position - 1);
                    }
                    if (BaseValue(at) > 0) { MoveValuePoint(dragPosition, at, value); dragPosition = at; }
                }
                e.Use(); Repaint();
            }
            if (e.type == EventType.MouseUp && dragPosition >= 0) { dragPosition = dragTangent = -1; GUIUtility.hotControl = 0; e.Use(); Repaint(); }
        }
        void MoveValuePoint(int previous, int at, double value)
        {
            double basis = BaseValue(at);
            if (basis <= 0 || double.IsNaN(value) || double.IsInfinity(value)) return;
            Curve.MovePoint(previous, at, Math.Max(selected >= 3 ? 1 : selected == 1 ? .001 : 0, value) / basis, Origin);
            selectedPoint = at;
        }
        void SetValuePoint(int at, double value)
        {
            double basis = BaseValue(at);
            if (basis <= 0) { ShowNotification(new GUIContent("기본값이 0입니다. 시작값 또는 초반 목표값을 먼저 올려 주세요.")); return; }
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            Curve.SetPoint(at, Math.Max(selected >= 3 ? 1 : selected == 1 ? .001 : 0, value) / basis, Origin);
            selectedPoint = at;
        }
        void DrawPointEditor()
        {
            WrappedLabel("선택한 점 정확히 편집", true);
            if (selectedPoint >= Origin) {
                EditorGUI.BeginChangeCheck();
                int at;
                using (new EditorGUI.DisabledScope(selectedPoint == Origin)) at = IntInput(AxisName, selectedPoint);
                double value = DoubleInput("목표 수치", DraftValue(selectedPoint));
                if (EditorGUI.EndChangeCheck()) {
                    Record("성장 곡선 점 수치 변경");
                    if (selectedPoint == Origin) StartValue = value;
                    else { at = Math.Max(Origin + 1, at); MoveValuePoint(selectedPoint, at, value); }
                }
                if (Curve.interpolation == DoodleCurveInterpolation.Manual) {
                    for (int side = 0; side < 2; side++) if (TangentHandle(side == 0, out _, out double height)) {
                        EditorGUI.BeginChangeCheck();
                        double edited = DoubleInput(side == 0 ? "앞 구간 손잡이 높이" : "뒤 구간 손잡이 높이", height);
                        if (EditorGUI.EndChangeCheck()) { Record("곡선 손잡이 높이 변경"); SetTangentValue(side == 0, edited); }
                        EditorGUI.BeginChangeCheck();
                        double angle = DoubleInput(side == 0 ? "앞 구간 곡선 각도 (°)" : "뒤 구간 곡선 각도 (°)", Curve.GetTangentAngle(selectedPoint, Origin, side == 0));
                        if (EditorGUI.EndChangeCheck()) { Record("곡선 각도 변경"); Curve.SetTangentAngle(selectedPoint, Origin, side == 0, angle); Repaint(); }
                    }
                    WrappedLabel("각도 범위: −89.9° ~ 89.9°. 스테이지/레벨 1당 보정값의 기울기를 각도로 표시합니다. 0°는 보정값이 평평한 상태이며 기본 성장률은 유지됩니다. 화면에서 보이는 각도는 로그 축·확대 비율에 따라 다릅니다.");
                    if (GUILayout.Button("선택점 손잡이 자동 정렬")) {
                        Record("곡선 손잡이 자동 정렬"); var automatic = Curve.Copy(); automatic.SetInterpolation(DoodleCurveInterpolation.Smooth, Origin);
                        Curve.SetTangent(selectedPoint, Origin, true, automatic.GetTangent(selectedPoint, Origin, true));
                        Curve.SetTangent(selectedPoint, Origin, false, automatic.GetTangent(selectedPoint, Origin, false));
                    }
                }
                using (new EditorGUI.DisabledScope(selectedPoint == Origin)) if (GUILayout.Button("선택한 점 삭제")) { Record("성장 곡선 점 삭제"); Curve.RemovePoint(selectedPoint); selectedPoint = -1; }
            } else WrappedLabel("그래프의 점을 클릭하면 단계와 수치를 입력할 수 있습니다.");
            if (GUILayout.Button("편집점 초기화 · 기본 지수 곡선")) { Record("성장 곡선 초기화"); Curve.points = Array.Empty<DoodleGrowthPoint>(); Curve.originOutTangent = 0; selectedPoint = -1; }
        }
        public void Apply()
        {
            if (!Ui) return;
            Ui.ApplyBalanceTuning(draft); Ui.ApplyStatCostTuning(statDraft);
            draft = Ui.ReadBalanceTuning(); statDraft = Ui.ReadStatCostTuning();
            ShowNotification(new GUIContent("수치와 곡선을 적용했습니다. 현재 레벨과 적의 남은 체력 비율은 유지됩니다."));
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
            ShowNotification(new GUIContent("모든 수치와 곡선을 기본값으로 저장했습니다."));
        }
        public void LoadCurrent()
        {
            draft = Ui ? Ui.ReadBalanceTuning() : File.Exists(TuningPath) ? JsonUtility.FromJson<DoodleUi.ServiceTuning>(File.ReadAllText(TuningPath)) : new DoodleUi.ServiceTuning();
            statDraft = Ui ? Ui.ReadStatCostTuning() : File.Exists(CollectionPath) ? JsonUtility.FromJson<UiCollectionTuning>(File.ReadAllText(CollectionPath)).statCosts : new UiStatCostTuning();
            loaded = JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(draft));
            statLoaded = JsonUtility.FromJson<UiStatCostTuning>(JsonUtility.ToJson(statDraft));
            selectedPoint = dragPosition = -1;
        }
        public void GrantDiamonds()
        {
            if (Ui) ShowNotification(new GUIContent("다이아 " + Ui.GrantDebugDiamonds(diamondAmount).ToString("N0") + "개 지급"));
        }
    }
}
