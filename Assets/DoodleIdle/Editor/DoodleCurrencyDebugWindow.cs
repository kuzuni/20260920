using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DoodleIdle.Editor
{
    public sealed class DoodleCurrencyDebugWindow : OdinEditorWindow
    {
        [SerializeField, HideInInspector] int currency;
        [SerializeField, HideInInspector] long amount = 10000;
        Vector2 scroll;
        static readonly string[] Currencies = { "골드", "다이아" };
        DoodleIdleGame Game => UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
        DoodleUi Ui => EditorApplication.isPlaying && Game && Game.Ready ? Game.Ui : null;

        [MenuItem("Doodle Idle/화폐 지급 디버그")]
        public static void Open()
        {
            var window = GetWindow<DoodleCurrencyDebugWindow>("화폐 지급 디버그");
            window.minSize = new Vector2(280, 250);
            window.Show();
        }
        protected override void OnEnable() { base.OnEnable(); UseScrollView = false; }
        void OnInspectorUpdate() => Repaint();

        [OnInspectorGUI]
        void DrawControls()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var ui = Ui;
            EditorGUILayout.HelpBox("실행 중인 게임에 화폐를 지급하고 저장합니다. 밸런스 설정이나 일일 무료 보상 횟수는 변경하지 않습니다.", MessageType.Info);
            GUILayout.Label(ui ? "보유 골드: " + ui.Gold.ToString("N0") + "\n보유 다이아: " + ui.Diamonds.ToString("N0") : "게임 실행 후 사용할 수 있습니다.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("지급할 화폐");
            currency = EditorGUILayout.Popup(currency, Currencies);
            GUILayout.Label("지급량");
            amount = Math.Max(1, EditorGUILayout.LongField(amount));
            if (currency == 1) amount = Math.Min(int.MaxValue, amount);
            using (new EditorGUI.DisabledScope(!ui)) if (GUILayout.Button(Currencies[currency] + " 지급", GUILayout.Height(36))) {
                long granted = currency == 0 ? ui.GrantDebugGold(amount) : ui.GrantDebugDiamonds((int)amount);
                ShowNotification(new GUIContent(Currencies[currency] + " " + granted.ToString("N0") + " 지급 완료"));
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
