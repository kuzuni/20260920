using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DoodleIdle.Editor
{
    // Avoid Odin's recursive property tree and editable StringDrawer for a read-only catalog.
    // The reported native allocation failed in that text-field rendering path.
    public sealed class DoodleSkillTestWindow : EditorWindow
    {
        const float RowHeight = 60;
        readonly List<SkillRow> skills = new List<SkillRow>();
        Vector2 scroll;

        [MenuItem("Doodle Idle/스킬 발동 테스트")]
        public static void Open() => GetWindow<DoodleSkillTestWindow>("스킬 발동 테스트").Show();

        DoodleIdleGame Game => Object.FindFirstObjectByType<DoodleIdleGame>();
        public void ToggleBasicAttack()
        {
            var game = Game;
            if (EditorApplication.isPlaying && game && game.Ready)
                game.SetBasicAttackEnabled(!game.BasicAttackEnabled);
        }

        public void RefreshSkills()
        {
            skills.Clear();
            var source = Resources.Load<TextAsset>("DoodleIdle/UI/Collections");
            if (!source) return;
            var items = JsonUtility.FromJson<UiCollectionTuning>(source.text)?.items;
            if (items == null) return;
            foreach (var item in items)
                if (item != null && item.category == "Skill") skills.Add(new SkillRow(item));
            Repaint();
        }
        void OnEnable() => RefreshSkills();
        void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

        void OnGUI()
        {
            var game = Game;
            bool ready = EditorApplication.isPlaying && game && game.Ready;
            EditorGUILayout.LabelField("플레이어 기본 공격", game && !game.BasicAttackEnabled ? "꺼짐" : "켜짐");
            using (new EditorGUI.DisabledScope(!ready))
                if (GUILayout.Button("기본 공격 켜기 / 끄기", GUILayout.Height(28))) ToggleBasicAttack();
            EditorGUILayout.HelpBox("Play 모드에서 미획득·미장착 스킬도 한 번 발동합니다. 장착 상태, 재화, 일반 쿨타임은 변경하지 않습니다. 일시정지 중이거나 적이 없으면 발동할 수 없습니다.", MessageType.Info);
            if (GUILayout.Button("목록 새로고침")) RefreshSkills();
            Rect viewport = GUILayoutUtility.GetRect(0, 100000, 0, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float width = Mathf.Max(1, viewport.width - 18);
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, width, skills.Count * RowHeight));
            try {
                int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
                int end = Mathf.Min(skills.Count, Mathf.CeilToInt((scroll.y + viewport.height) / RowHeight));
                for (int i = first; i < end; i++) {
                    var row = skills[i];
                    float y = i * RowHeight;
                    var sprite = row.Thumbnail;
                    if (sprite) {
                        var r = sprite.rect;
                        float scale = 48 / Mathf.Max(r.width, r.height);
                        GUI.DrawTextureWithTexCoords(new Rect(4 + (48 - r.width * scale) / 2, y + 6 + (48 - r.height * scale) / 2, r.width * scale, r.height * scale),
                            sprite.texture, new Rect(r.x / sprite.texture.width, r.y / sprite.texture.height, r.width / sprite.texture.width, r.height / sprite.texture.height));
                    }
                    GUI.Label(new Rect(60, y + 18, Mathf.Max(0, width - 168), 24), row.Label);
                    using (new EditorGUI.DisabledScope(!ready || !game.CanTestSkill))
                        if (GUI.Button(new Rect(Mathf.Max(60, width - 105), y + 16, 100, 28), "발동 테스트")) game.DebugCastSkill(row.Id);
                }
            }
            finally { GUI.EndScrollView(); }
        }

        public sealed class SkillRow
        {
            public readonly string Id;
            public readonly GUIContent Label;
            readonly string icon;
            Sprite thumbnail;
            public Sprite Thumbnail => thumbnail ? thumbnail : thumbnail = UiKit.Art(icon);
            public SkillRow(UiItem item)
            {
                Id = item.id;
                icon = item.icon;
                string name = item.name ?? "이름 없음";
                if (name.Length > 96) name = name.Substring(0, 96) + "…";
                string grade = item.rarity >= 0 && item.rarity < DoodleUi.GradeNames.Length ? DoodleUi.GradeNames[item.rarity] : "미분류";
                Label = new GUIContent(grade + " · " + name);
            }
            public void Cast()
            {
                var game = Object.FindFirstObjectByType<DoodleIdleGame>();
                if (EditorApplication.isPlaying && game && game.CanTestSkill) game.DebugCastSkill(Id);
            }
        }
    }
}
