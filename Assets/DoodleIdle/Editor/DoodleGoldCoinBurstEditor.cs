using DoodleIdle;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DoodleGoldCoinBurst))]
public sealed class DoodleGoldCoinBurstEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("launchSpread"), new GUIContent("좌우 퍼짐 각도"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gravity"), new GUIContent("낙하 가속도"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("landedLifetime"), new GUIContent("착지 후 유지 시간"));
        var range = serializedObject.FindProperty("landingYOffset");
        var value = range.vector2Value;
        value.x = EditorGUILayout.FloatField("착지 Y 최소", value.x);
        value.y = EditorGUILayout.FloatField("착지 Y 최대", value.y);
        range.vector2Value = value;
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("착지 Y는 방출 지점 기준입니다. 음수=아래 / 양수=위. 예: 최소 -0.3, 최대 0.1\n크기: Particle System → Start Size\n기본 파티클 재생에는 착지 코드가 없으므로 아래 실제 동작 미리보기를 사용하세요.", MessageType.Info);
        if (GUILayout.Button("실제 동작 미리보기")) DoodleGoldCoinPreview.Open((DoodleGoldCoinBurst)target);
        if (GUILayout.Button("원본 GoldCoinBurst 프리팹 선택")) Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(GoldCoinBurstSetup.PrefabPath);
        if (Application.isPlaying) EditorGUILayout.HelpBox("플레이 중 인스턴스 변경은 종료 시 사라집니다. 영구 설정은 원본 프리팹을 저장하세요.", MessageType.Info);
    }
}

public sealed class DoodleGoldCoinPreview : EditorWindow
{
    DoodleGoldCoinBurst source, clone;
    PreviewRenderUtility preview;
    double previousTime;
    float elapsed, accumulator;
    bool playing = true;
    public static void Open(DoodleGoldCoinBurst source)
    {
        var window = GetWindow<DoodleGoldCoinPreview>("골드 실제 동작");
        window.source = source; window.minSize = new Vector2(420, 360); window.Restart(); window.Show();
    }
    [MenuItem("Doodle Idle/Effects/Gold Coin Preview")]
    public static void OpenDefault() => Open(AssetDatabase.LoadAssetAtPath<GameObject>(GoldCoinBurstSetup.PrefabPath).GetComponent<DoodleGoldCoinBurst>());
    void OnEnable() { EditorApplication.update += Tick; }
    void OnDisable() { EditorApplication.update -= Tick; Cleanup(); }
    void Cleanup() { if (preview != null) { preview.Cleanup(); preview = null; } clone = null; }
    public void Restart()
    {
        Cleanup();
        if (!source) return;
        preview = new PreviewRenderUtility();
        var root = Instantiate(source.gameObject); root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;
        preview.AddSingleGO(root);
        clone = root.GetComponent<DoodleGoldCoinBurst>(); clone.PrepareSimulation();
        uint seed = 1; clone.EmitBurst(Vector2.zero, ref seed);
        var camera = preview.camera; camera.orthographic = true; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.68f, .67f, .52f);
        float low = Mathf.Min(source.landingYOffset.x, source.landingYOffset.y);
        float high = Mathf.Max(source.landingYOffset.x, source.landingYOffset.y);
        float speed = source.GetComponent<ParticleSystem>().main.startSpeed.constantMax;
        float top = Mathf.Max(speed * speed / (2 * Mathf.Max(.1f, source.gravity)), high) + 1;
        float bottom = Mathf.Min(0, low) - 1;
        camera.orthographicSize = Mathf.Max(2.5f, (top - bottom) * .65f);
        camera.transform.position = new Vector3(0, (top + bottom) * .5f, -10); camera.transform.rotation = Quaternion.identity;
        elapsed = accumulator = 0; previousTime = EditorApplication.timeSinceStartup; Repaint();
    }
    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (!clone || !playing) { previousTime = now; return; }
        accumulator += Mathf.Min(.1f, (float)(now - previousTime)); previousTime = now;
        while (accumulator >= .02f) { clone.Simulate(.02f); accumulator -= .02f; elapsed += .02f; }
        Repaint();
    }
    public void PreviewAt(float time)
    {
        Restart(); playing = false;
        if (!clone) return;
        for (int i = 0; i < Mathf.RoundToInt(time / .02f); i++) { clone.Simulate(.02f); elapsed += .02f; }
        Repaint();
    }
    void OnGUI()
    {
        EditorGUILayout.LabelField("게임과 같은 방출 · 중력 · 착지 로직", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("원본 프리팹 값을 바꾼 뒤 ‘다시 재생’을 누르세요. 이 창은 원본을 수정하지 않는 복제본입니다.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope()) {
            if (GUILayout.Button("다시 재생")) { playing = true; Restart(); }
            if (GUILayout.Button(playing ? "일시정지" : "계속")) playing = !playing;
        }
        if (preview == null || !clone) return;
        EditorGUI.BeginChangeCheck();
        float time = EditorGUILayout.Slider("시간", Mathf.Min(elapsed, 3), 0, 3);
        if (EditorGUI.EndChangeCheck()) PreviewAt(time);
        EditorGUILayout.LabelField($"{elapsed:0.00}초 / 동전 {clone.GetComponent<ParticleSystem>().particleCount}개");
        var rect = GUILayoutUtility.GetRect(100, 10000, 180, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        if (Event.current.type != EventType.Repaint) return;
        preview.BeginPreview(rect, GUIStyle.none); preview.Render(true); var texture = preview.EndPreview();
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
    }
}
