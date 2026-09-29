using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleIdle.Editor
{
    public sealed class DoodleHealthBarWindow : EditorWindow
    {
        [MenuItem("Doodle Idle/HP바 위치 설정")]
        static void Open() => GetWindow<DoodleHealthBarWindow>("HP바 위치 설정").Show();

        void OnGUI()
        {
            var game = Object.FindFirstObjectByType<DoodleIdleGame>();
            if (!game)
            {
                EditorGUILayout.HelpBox("DoodleIdle 게임 씬을 열어 주세요.", MessageType.Info);
                return;
            }
            var data = new SerializedObject(game);
            data.Update();
            EditorGUILayout.LabelField("머리 위 HP바 위치", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(data.FindProperty("playerHealthBarOffset"), new GUIContent("플레이어 위치"));
            EditorGUILayout.PropertyField(data.FindProperty("enemyHealthBarOffset"), new GUIContent("일반 적 위치"));
            EditorGUILayout.HelpBox("X: 좌우 / Y: 높이. 보스 머리 위 HP바는 표시하지 않습니다.\nPlay 중 변경은 즉시 보이며, 영구 적용하려면 Play를 끈 상태에서 설정하고 씬을 저장하세요.", MessageType.Info);
            if (data.ApplyModifiedProperties() && !EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            if (GUILayout.Button("게임 Inspector 열기")) Selection.activeGameObject = game.gameObject;
        }
    }
}
