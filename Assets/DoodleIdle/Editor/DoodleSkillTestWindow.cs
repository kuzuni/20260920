using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DoodleIdle.Editor
{
    public sealed class DoodleSkillTestWindow : OdinEditorWindow
    {
        [MenuItem("Doodle Idle/스킬 발동 테스트")]
        public static void Open() => GetWindow<DoodleSkillTestWindow>("스킬 발동 테스트").Show();

        [ShowInInspector, ReadOnly, LabelText("전투 대상")]
        DoodleIdleGame Game => Object.FindFirstObjectByType<DoodleIdleGame>();

        [InfoBox("Play 모드에서 사용합니다. 각 버튼은 미획득·미장착 스킬도 한 번 발동합니다. 장착 상태, 재화, 일반 쿨타임은 변경하지 않습니다. 일시정지 중이거나 적이 없으면 버튼이 비활성화됩니다.")]
        [ShowInInspector, ListDrawerSettings(IsReadOnly = true, ShowIndexLabels = false, Expanded = true), LabelText("전체 스킬")]
        List<SkillRow> skills = new List<SkillRow>();

        [Button("목록 새로고침")]
        public void RefreshSkills()
        {
            skills.Clear();
            var source = Resources.Load<TextAsset>("DoodleIdle/UI/Collections");
            if (!source) return;
            foreach (var item in JsonUtility.FromJson<UiCollectionTuning>(source.text).items)
                if (item.category == "Skill") skills.Add(new SkillRow(item));
        }
        protected override void OnEnable() { base.OnEnable(); RefreshSkills(); }
        void OnInspectorUpdate() => Repaint();

        public sealed class SkillRow
        {
            readonly UiItem item;
            public SkillRow(UiItem item) { this.item = item; }
            [ShowInInspector, PreviewField(52), HideLabel, HorizontalGroup("Row", 60)]
            Sprite Thumbnail => UiKit.Art(item.icon);
            [ShowInInspector, ReadOnly, HideLabel, HorizontalGroup("Row")]
            string Name => DoodleUi.GradeNames[item.rarity] + " · " + item.name;
            bool CanCast
            {
                get { var game = Object.FindFirstObjectByType<DoodleIdleGame>(); return EditorApplication.isPlaying && game && game.CanTestSkill; }
            }
            [Button("발동 테스트", ButtonSizes.Medium), HorizontalGroup("Row", 105), EnableIf(nameof(CanCast))]
            public void Cast()
            {
                var game = Object.FindFirstObjectByType<DoodleIdleGame>();
                if (game) game.DebugCastSkill(item.id);
            }
        }
    }
}
