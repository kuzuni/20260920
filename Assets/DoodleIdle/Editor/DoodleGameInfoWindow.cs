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
        string Gold => Ui ? UiNumber.Format(Ui.Gold) : PlayerPrefs.GetString("DoodleUi.Gold", "125480");
        [ShowInInspector, ReadOnly, LabelText("다이아")]
        int Diamonds => Ui ? Ui.Diamonds : PlayerPrefs.GetInt("DoodleUi.Diamonds", 1250);
        [ShowInInspector, ReadOnly, LabelText("플레이어 체력")]
        string Health => Game && Game.Ready ? UiNumber.Format(Game.PlayerHealth) + " / " + UiNumber.Format(Game.PlayerMaxHealth) : "-";
        [ShowInInspector, ReadOnly, LabelText("발견한 아이템")]
        int Discovered => Ui ? new[] { "Armor", "Club", "Skill", "Companion", "Relic" }.Sum(c => Ui.Items(c).Count(x => x.discovered)) : 0;

        bool CanReset => !EditorApplication.isPlayingOrWillChangePlaymode || (EditorApplication.isPlaying && Game && Game.Ready);
        [InfoBox("초기화 버튼을 누르면 재화, 스탯, 장비·스킬·동료·유물, 뽑기, 스테이지, 출석·미션과 게임 설정을 처음 시작한 상태로 되돌립니다. 실행 중이면 게임을 바로 다시 시작합니다.")]
        [Button("게임 정보 초기화", ButtonSizes.Large), GUIColor(1f, .65f, .6f), EnableIf(nameof(CanReset))]
        public void ResetGameInformation()
        {
            if (!CanReset) return;
            if (EditorApplication.isPlaying) DoodleGameData.ResetAndRestart(Game);
            else DoodleGameData.ResetSavedProgress();
            Repaint();
        }
        void OnInspectorUpdate() => Repaint();
    }
}
