using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DoodleIdle.Editor
{
    public sealed class DoodleGameInfoWindow : OdinEditorWindow
    {
        [MenuItem("Doodle Idle/게임 정보")]
        public static void Open() => GetWindow<DoodleGameInfoWindow>("게임 정보").Show();
        DoodleIdleGame Game => Object.FindFirstObjectByType<DoodleIdleGame>();
        DoodleUi Ui => Game && Game.Ready ? Game.Ui : null;

        [ShowInInspector, ReadOnly, LabelText("실행 상태")]
        string Status => EditorApplication.isPlaying ? Game && Game.Ready ? "게임 실행 중" : "게임 시작 중" : "편집 모드";
        [ShowInInspector, ReadOnly, LabelText("스테이지")]
        int Stage => Ui ? Ui.MainStage + 1 : 1;
        [ShowInInspector, ReadOnly, LabelText("골드")]
        string Gold => Ui ? UiNumber.Format(Ui.Gold) : PlayerPrefs.GetString("DoodleUi.Gold", "0");
        [ShowInInspector, ReadOnly, LabelText("다이아")]
        int Diamonds => Ui ? Ui.Diamonds : PlayerPrefs.GetInt("DoodleUi.Diamonds", 0);
        [ShowInInspector, ReadOnly, LabelText("플레이어 체력")]
        string Health => Game && Game.Ready ? UiNumber.Format(Game.PlayerHealth) + " / " + UiNumber.Format(Game.PlayerMaxHealth) : "-";
        [ShowInInspector, ReadOnly, LabelText("발견한 아이템")]
        int Discovered => Ui ? new[] { "Armor", "Club", "Skill", "Companion", "Relic" }.Sum(c => Ui.Items(c).Count(x => x.discovered)) : 0;

        bool CanReset => !EditorApplication.isPlayingOrWillChangePlaymode || (EditorApplication.isPlaying && Game && Game.Ready);
        [InfoBox("초기화하면 골드·다이아 0, 장비·스킬·동료·유물 미보유, 장착 슬롯 비움, 스탯 성장 0, 스테이지 1로 돌아갑니다. 뽑기·출석·미션·게임 설정도 초기화하며 실행 중이면 바로 다시 시작합니다.")]
        [Button("게임 정보 초기화", ButtonSizes.Large), GUIColor(1f, .65f, .6f), EnableIf(nameof(CanReset))]
        public void ResetGameInformation()
        {
            if (!CanReset) return;
            if (EditorApplication.isPlaying) DoodleGameData.ResetAndRestart(Game);
            else DoodleGameData.ResetSavedProgress();
            ShowNotification(new GUIContent("초기화 완료: 재화 0 · 보유 아이템 없음"));
            Repaint();
        }
        void OnInspectorUpdate() => Repaint();
    }
}
