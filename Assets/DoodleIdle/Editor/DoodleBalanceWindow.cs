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
        [SerializeField, HideInInspector] DoodleUi.ServiceTuning draft = new DoodleUi.ServiceTuning();
        [MenuItem("Doodle Idle/밸런스 조절")]
        public static void Open() => GetWindow<DoodleBalanceWindow>("밸런스 조절").Show();
        DoodleIdleGame Game => Object.FindFirstObjectByType<DoodleIdleGame>();
        DoodleUi Ui => EditorApplication.isPlaying && Game && Game.Ready ? Game.Ui : null;
        bool CanApply => Ui;
        protected override void OnEnable() { base.OnEnable(); LoadCurrent(); }

        [InfoBox("8시간 목표 보정 없이 아래 값으로 계산합니다. 증가량은 매 스테이지 기본값에 더하는 비율(%)입니다. 예: 2%이면 51스테이지에 기본값의 2배. 적 데미지는 1스테이지 0에서 70스테이지 100까지 증가하고, 이후 증가량을 적용합니다.")]
        [ShowInInspector, ReadOnly, LabelText("현재 스테이지")]
        int Stage => Ui ? Ui.CombatDifficultyStage : 1;

        [ShowInInspector, BoxGroup("골드"), LabelText("획득 배수"), MinValue(0)]
        float GoldMultiplier { get => draft.goldRewardMultiplier; set => draft.goldRewardMultiplier = value; }
        [ShowInInspector, BoxGroup("골드"), LabelText("스테이지당 증가량 (%)"), MinValue(0)]
        float GoldGrowth { get => draft.goldStageGrowth * 100; set => draft.goldStageGrowth = value / 100; }
        [ShowInInspector, BoxGroup("적 체력"), LabelText("체력 배수"), MinValue(.001)]
        float HealthMultiplier { get => draft.enemyHealthBaseMultiplier; set => draft.enemyHealthBaseMultiplier = value; }
        [ShowInInspector, BoxGroup("적 체력"), LabelText("스테이지당 증가량 (%)"), MinValue(0)]
        float HealthGrowth { get => draft.enemyHealthStageGrowth * 100; set => draft.enemyHealthStageGrowth = value / 100; }
        [ShowInInspector, BoxGroup("적 데미지"), LabelText("데미지 배수"), MinValue(0)]
        float DamageMultiplier { get => draft.enemyDamageBaseMultiplier; set => draft.enemyDamageBaseMultiplier = value; }
        [ShowInInspector, BoxGroup("적 데미지"), LabelText("70 이후 스테이지당 증가량 (%)"), MinValue(0)]
        float DamageGrowth { get => draft.enemyDamageStageGrowth * 100; set => draft.enemyDamageStageGrowth = value / 100; }

        [Button("실행 중인 게임에 적용", ButtonSizes.Large), EnableIf(nameof(CanApply))]
        public void Apply()
        {
            if (!Ui) return;
            Ui.ApplyBalanceTuning(draft); draft = Ui.ReadBalanceTuning();
            ShowNotification(new GUIContent("적 체력 비율과 스테이지 진행을 유지하며 적용했습니다."));
        }

        [InfoBox("실행 중 적용은 이번 플레이에만 반영됩니다. 기본값 저장을 누르면 다음 실행에도 유지됩니다. 골드 동굴도 같은 골드 수치를 사용합니다.")]
        [Button("기본값으로 저장", ButtonSizes.Large)]
        public void SaveDefaults()
        {
            var defaults = JsonUtility.FromJson<DoodleUi.ServiceTuning>(File.ReadAllText(TuningPath));
            DoodleUi.CopyBalanceTuning(draft, defaults);
            File.WriteAllText(TuningPath, JsonUtility.ToJson(defaults, true) + "\n");
            AssetDatabase.ImportAsset(TuningPath);
            if (Ui) Ui.ApplyBalanceTuning(defaults);
            draft = defaults;
            ShowNotification(new GUIContent("밸런스 기본값 저장 완료"));
        }

        [Button("현재 값 다시 불러오기")]
        public void LoadCurrent()
        {
            draft = Ui ? Ui.ReadBalanceTuning() : File.Exists(TuningPath)
                ? JsonUtility.FromJson<DoodleUi.ServiceTuning>(File.ReadAllText(TuningPath)) : new DoodleUi.ServiceTuning();
        }

        [BoxGroup("다이아 디버그"), LabelText("지급량"), MinValue(1)]
        public int diamondAmount = 10000;
        [ShowInInspector, BoxGroup("다이아 디버그"), ReadOnly, LabelText("보유 다이아")]
        int Diamonds => Ui ? Ui.Diamonds : 0;
        [Button("다이아 지급", ButtonSizes.Large), BoxGroup("다이아 디버그"), EnableIf(nameof(CanApply))]
        public void GrantDiamonds()
        {
            if (!Ui) return;
            int granted = Ui.GrantDebugDiamonds(diamondAmount);
            ShowNotification(new GUIContent("다이아 " + granted.ToString("N0") + "개 지급"));
        }
        void OnInspectorUpdate() => Repaint();
    }
}
