using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DoodleIdle.Editor
{
    public sealed class DoodleStageDebugWindow : OdinEditorWindow
    {
        [SerializeField, HideInInspector] int targetStage = 1;
        Vector2 scroll;
        DoodleIdleGame Game => UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
        DoodleUi Ui => EditorApplication.isPlaying && Game && Game.Ready ? Game.Ui : null;

        [MenuItem("Doodle Idle/스테이지 디버그")]
        public static void Open()
        {
            var window = GetWindow<DoodleStageDebugWindow>("스테이지 디버그");
            window.minSize = new Vector2(300, 330);
            window.Show();
        }
        protected override void OnEnable() { base.OnEnable(); UseScrollView = false; }
        void OnInspectorUpdate() => Repaint();

        [OnInspectorGUI]
        void DrawControls()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var ui = Ui;
            GUILayout.Label(ui ? "현재 메인 스테이지: " + ((long)ui.MainStage + 1).ToString("N0") + "\n최고 도달 스테이지: " + ((long)ui.HighestMainStage + 1).ToString("N0") + (ui.ActiveDungeonIndex >= 0 ? "\n현재 던전 진행 중" : "\n현재 테마: " + Game.CurrentThemeName) : "게임 실행 후 사용할 수 있습니다.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);
            GUILayout.Label("이동할 스테이지 (1부터)");
            targetStage = Math.Max(1, EditorGUILayout.IntField(targetStage));
            GUILayout.Label("이동할 테마: " + DoodleIdleGame.ThemeNames[DoodleIdleGame.ThemeIndexForStage(targetStage)], EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox("입력한 메인 스테이지로 즉시 이동하고 저장합니다. 처치 진행도는 0부터 시작하며 맵·적·보스·공격 효과를 새로 준비합니다. 일시정지 중에도 적용됩니다.\n던전 진행 중이면 던전을 종료합니다. 낮은 단계로 이동해도 최고 도달 기록과 이미 열린 슬롯은 유지됩니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!ui)) {
                if (GUILayout.Button("현재 스테이지 가져오기")) targetStage = (int)Math.Min(int.MaxValue, (long)ui.MainStage + 1);
                if (GUILayout.Button("스테이지 즉시 이동", GUILayout.Height(40)) && ui.DebugSetMainStage(targetStage))
                    ShowNotification(new GUIContent("스테이지 " + targetStage.ToString("N0") + " 이동 완료"));
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
