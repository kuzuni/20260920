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
        [SerializeField, HideInInspector] DoodleUi.ServiceTuning draft = new DoodleUi.ServiceTuning();
        [SerializeField, HideInInspector] DoodleUi.ServiceTuning loaded = new DoodleUi.ServiceTuning();
        [SerializeField, HideInInspector] UiStatCostTuning statDraft = new UiStatCostTuning();
        [SerializeField, HideInInspector] UiStatCostTuning statLoaded = new UiStatCostTuning();
        [MenuItem("Doodle Idle/밸런스 조절")]
        public static void Open() => GetWindow<DoodleBalanceWindow>("밸런스 조절").Show();
        DoodleIdleGame Game => Object.FindFirstObjectByType<DoodleIdleGame>();
        DoodleUi Ui => EditorApplication.isPlaying && Game && Game.Ready ? Game.Ui : null;
        bool CanApply => Ui;
        protected override void OnEnable() { base.OnEnable(); LoadCurrent(); }

        [InfoBox("시작값은 1스테이지 일반 적 기준입니다. 골드·체력 = 시작값 × [1 + (스테이지 - 1) × 증가율]. 적 데미지는 시작값부터 초반 목표값까지 선형으로 변하고, 이후 목표값을 기준으로 증가합니다. 아래 미리보기는 입력 즉시 갱신되며, 적용/저장을 눌러야 게임에 반영됩니다.")]
        [ShowInInspector, ReadOnly, LabelText("현재 스테이지")]
        int Stage => Ui ? Ui.CombatDifficultyStage : 1;

        [ShowInInspector, BoxGroup("골드"), LabelText("시작 골드 (적 1마리)"), MinValue(0)]
        float StartingGold { get => draft.goldPerEnemy; set => draft.goldPerEnemy = value; }
        [ShowInInspector, BoxGroup("골드"), LabelText("스테이지당 증가량 (%)"), MinValue(0)]
        float GoldGrowth { get => draft.goldStageGrowth * 100; set => draft.goldStageGrowth = value / 100; }
        [ShowInInspector, BoxGroup("적 체력"), LabelText("시작 체력"), MinValue(.001)]
        float StartingHealth { get => draft.enemyStartingHealth; set => draft.enemyStartingHealth = value; }
        [ShowInInspector, BoxGroup("적 체력"), LabelText("스테이지당 증가량 (%)"), MinValue(0)]
        float HealthGrowth { get => draft.enemyHealthStageGrowth * 100; set => draft.enemyHealthStageGrowth = value / 100; }
        [ShowInInspector, BoxGroup("적 데미지"), LabelText("시작 데미지"), MinValue(0)]
        float StartingDamage { get => draft.enemyStartingDamage; set => draft.enemyStartingDamage = value; }
        [ShowInInspector, BoxGroup("적 데미지"), LabelText("초반 목표 스테이지"), MinValue(2)]
        int EarlyEnd { get => draft.earlyEnemyDamageEndStage; set => draft.earlyEnemyDamageEndStage = value; }
        [ShowInInspector, BoxGroup("적 데미지"), LabelText("초반 목표 데미지"), MinValue(0)]
        float EarlyDamage { get => draft.earlyEnemyDamageMax; set => draft.earlyEnemyDamageMax = value; }
        [ShowInInspector, BoxGroup("적 데미지"), LabelText("목표 이후 증가량 (%)"), MinValue(0)]
        float DamageGrowth { get => draft.enemyDamageStageGrowth * 100; set => draft.enemyDamageStageGrowth = value / 100; }

        [BoxGroup("실제 수치 미리보기"), LabelText("비교할 스테이지"), MinValue(1)]
        public int previewStage = 70;
        [Button("현재 스테이지로 비교"), BoxGroup("실제 수치 미리보기")]
        public void PreviewCurrentStage() => previewStage = Stage;

        [System.Serializable]
        public sealed class PreviewRow
        {
            [ReadOnly, LabelText("기준")] public string label;
            [ReadOnly, LabelText("스테이지")] public int stage;
            [ReadOnly, LabelText("골드 / 1마리")] public string gold;
            [ReadOnly, LabelText("적 체력")] public string health;
            [ReadOnly, LabelText("접촉 데미지")] public string damage;
        }

        PreviewRow Preview(string label, DoodleUi.ServiceTuning tuning, int stage)
        {
            // Same formulas as spawning, contact hits and gold payouts. Normalize invalid draft input as Apply does.
            var values = new DoodleUi.ServiceTuning();
            DoodleUi.CopyBalanceTuning(tuning, values);
            double goldBonus = Ui ? (double)Ui.GoldGainMultiplier * Ui.GoldBuffMultiplier : 1;
            return new PreviewRow {
                label = label, stage = stage,
                gold = DoodleUi.GoldForMainKills(values, stage, 1, goldBonus).ToString("N0"),
                health = (68 * DoodleUi.EnemyHealthMultiplier(values, stage)).ToString("N2"),
                damage = ((Game ? Game.enemyContactDamage : 64) * DoodleUi.EnemyDamageMultiplier(values, stage)).ToString("N2")
            };
        }

        [InfoBox("일반 적 기준 · 보스 체력은 ×20. 실행 중 골드는 현재 유물·버프 보너스 포함, 정지 중에는 보너스 제외. 기존=현재 게임 값(정지 중 저장값), 수정=위 입력값. 골드는 정수 반올림됩니다.")]
        [ShowInInspector, BoxGroup("실제 수치 미리보기"), TableList(IsReadOnly = true, AlwaysExpanded = true), HideLabel]
        PreviewRow[] PreviewValues
        {
            get {
                var current = Ui ? Ui.ReadBalanceTuning() : loaded;
                int stage = Mathf.Max(1, previewStage);
                return new[] { Preview("기존", current, 1), Preview("수정", draft, 1),
                    Preview("기존", current, stage), Preview("수정", draft, stage) };
            }
        }

        [InfoBox("강화 비용만 조절합니다. 공격력·체력·체력회복은 같은 레벨에서 같은 비용을 사용합니다. 비용 = 시작 비용 × (1 + 증가율)^현재 레벨, 소수점 올림. 증가율 0%이면 비용이 고정됩니다. 능력치는 기존처럼 레벨마다 일정하게 증가합니다.")]
        [ShowInInspector, BoxGroup("스탯 강화 비용/공격력 · 체력 · 체력회복 공통"), LabelText("시작 비용 (골드)"), MinValue(1)]
        int CommonStatCost { get => statDraft.commonBaseCost; set => statDraft.commonBaseCost = value; }
        [ShowInInspector, BoxGroup("스탯 강화 비용/공격력 · 체력 · 체력회복 공통"), LabelText("레벨당 비용 증가율 (%)"), MinValue(0)]
        float CommonStatGrowth { get => statDraft.commonGrowth * 100; set => statDraft.commonGrowth = value / 100; }
        [ShowInInspector, BoxGroup("스탯 강화 비용/x2 치명타 확률"), LabelText("시작 비용 (골드)"), MinValue(1)]
        int Critical2Cost { get => statDraft.critical2BaseCost; set => statDraft.critical2BaseCost = value; }
        [ShowInInspector, BoxGroup("스탯 강화 비용/x2 치명타 확률"), LabelText("레벨당 비용 증가율 (%)"), MinValue(0)]
        float Critical2Growth { get => statDraft.critical2Growth * 100; set => statDraft.critical2Growth = value / 100; }
        [ShowInInspector, BoxGroup("스탯 강화 비용/x4 치명타 확률"), LabelText("시작 비용 (골드)"), MinValue(1)]
        int Critical4Cost { get => statDraft.critical4BaseCost; set => statDraft.critical4BaseCost = value; }
        [ShowInInspector, BoxGroup("스탯 강화 비용/x4 치명타 확률"), LabelText("레벨당 비용 증가율 (%)"), MinValue(0)]
        float Critical4Growth { get => statDraft.critical4Growth * 100; set => statDraft.critical4Growth = value / 100; }
        [BoxGroup("스탯 강화 비용/미리보기"), LabelText("강화 전 레벨"), MinValue(0)]
        public int previewStatLevel = 0;

        [System.Serializable]
        public sealed class StatCostPreviewRow
        {
            [ReadOnly, LabelText("스탯")] public string stat;
            [ReadOnly, LabelText("기존 비용")] public string current;
            [ReadOnly, LabelText("수정 비용")] public string edited;
        }
        [ShowInInspector, BoxGroup("스탯 강화 비용/미리보기"), TableList(IsReadOnly = true, AlwaysExpanded = true), HideLabel]
        StatCostPreviewRow[] StatCostPreview
        {
            get {
                var current = Ui ? Ui.ReadStatCostTuning() : statLoaded;
                var rows = new StatCostPreviewRow[3];
                string[] ids = { "attack", "crit2Chance", "crit4Chance" };
                string[] names = { "공격력 · 체력 · 회복", "x2 치명타 확률", "x4 치명타 확률" };
                for (int i = 0; i < rows.Length; i++) rows[i] = new StatCostPreviewRow {
                    stat = names[i],
                    current = DoodleUi.StatUpgradePrice(current, ids[i], previewStatLevel).ToString("N0"),
                    edited = DoodleUi.StatUpgradePrice(statDraft, ids[i], previewStatLevel).ToString("N0")
                };
                return rows;
            }
        }

        [Button("실행 중인 게임에 적용", ButtonSizes.Large), EnableIf(nameof(CanApply))]
        public void Apply()
        {
            if (!Ui) return;
            Ui.ApplyBalanceTuning(draft); draft = Ui.ReadBalanceTuning();
            Ui.ApplyStatCostTuning(statDraft); statDraft = Ui.ReadStatCostTuning();
            ShowNotification(new GUIContent("적 체력 비율과 스테이지 진행을 유지하며 적용했습니다."));
        }

        [InfoBox("실행 중 적용은 이번 플레이에만 반영됩니다. 기본값 저장을 누르면 다음 실행에도 유지됩니다. 골드 동굴도 같은 골드 수치를 사용합니다.")]
        [Button("기본값으로 저장", ButtonSizes.Large)]
        public void SaveDefaults()
        {
            var defaults = JsonUtility.FromJson<DoodleUi.ServiceTuning>(File.ReadAllText(TuningPath));
            var collections = JsonUtility.FromJson<UiCollectionTuning>(File.ReadAllText(CollectionPath));
            DoodleUi.CopyBalanceTuning(draft, defaults);
            DoodleUi.CopyStatCostTuning(statDraft, collections.statCosts);
            File.WriteAllText(TuningPath, JsonUtility.ToJson(defaults, true) + "\n");
            File.WriteAllText(CollectionPath, JsonUtility.ToJson(collections, true) + "\n");
            AssetDatabase.ImportAsset(TuningPath);
            AssetDatabase.ImportAsset(CollectionPath);
            if (Ui) { Ui.ApplyBalanceTuning(defaults); Ui.ApplyStatCostTuning(collections.statCosts); }
            draft = defaults;
            loaded = JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(defaults));
            statDraft = collections.statCosts;
            statLoaded = JsonUtility.FromJson<UiStatCostTuning>(JsonUtility.ToJson(statDraft));
            ShowNotification(new GUIContent("밸런스 기본값 저장 완료"));
        }

        [Button("현재 값 다시 불러오기")]
        public void LoadCurrent()
        {
            draft = Ui ? Ui.ReadBalanceTuning() : File.Exists(TuningPath)
                ? JsonUtility.FromJson<DoodleUi.ServiceTuning>(File.ReadAllText(TuningPath)) : new DoodleUi.ServiceTuning();
            loaded = JsonUtility.FromJson<DoodleUi.ServiceTuning>(JsonUtility.ToJson(draft));
            statDraft = Ui ? Ui.ReadStatCostTuning() : File.Exists(CollectionPath)
                ? JsonUtility.FromJson<UiCollectionTuning>(File.ReadAllText(CollectionPath)).statCosts : new UiStatCostTuning();
            statLoaded = JsonUtility.FromJson<UiStatCostTuning>(JsonUtility.ToJson(statDraft));
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
