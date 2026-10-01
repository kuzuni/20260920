using System.IO;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;

public static class CharacterHitBloodSetup
{
    public const string PrefabPath = "Assets/DoodleIdle/Resources/DoodleIdle/HitBlood.prefab";
    public const string PlayerPath = "Assets/DoodleIdle/CharacterRigs/Prefabs/Player_Standard.prefab";

    [MenuItem("Doodle Idle/Effects/Install Shared Hit Blood")]
    public static void Install()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) Create();
        var shared = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        foreach (var path in Directory.GetFiles("Assets/DoodleIdle/CharacterRigs/Prefabs", "*.prefab")) {
            var root = PrefabUtility.LoadPrefabContents(path);
            try {
                var rig = root.GetComponent<CharacterRig>();
                if (rig.hitBlood) continue;
                var child = (GameObject)PrefabUtility.InstantiatePrefab(shared, rig.face.transform);
                PositionAtBackOfHead(rig, child.transform);
                rig.hitBlood = child.GetComponent<CharacterHitBlood>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    public static void PositionAtBackOfHead(CharacterRig rig, Transform blood)
    {
        var bounds = rig.face.headRenderer.bounds;
        blood.position = new Vector3(bounds.min.x + bounds.size.x * .08f,
            bounds.center.y + bounds.size.y * .08f, rig.face.transform.position.z);
    }

    static void Create()
    {
        const string matPath = "Assets/DoodleIdle/Resources/DoodleIdle/HitBlood.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!material) {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/DoodleIdle/CharacterRigs/FootDust.mat"));
            AssetDatabase.CreateAsset(material, matPath);
        }
        var root = new GameObject("HitBlood");
        try {
            var ps = root.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = .5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(.07f, .16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.65f,.04f,.06f), new Color(.95f,.14f,.13f));
            main.gravityModifier = .3f; main.maxParticles = 64; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0; emission.rateOverDistance = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 7) });
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22; shape.radius = .06f; shape.rotation = new Vector3(0, -90, 0);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.sortingOrder = 20; renderer.maxParticleSize = 1;
            root.AddComponent<CharacterHitBlood>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        } finally { Object.DestroyImmediate(root); }
    }
}

// Saving Particle System settings on the player's nested instance promotes those
// overrides to the shared prefab. Each rig's local emission position stays authored.
public sealed class CharacterHitBloodSync : AssetPostprocessor
{
    static bool queued, syncing;
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (syncing || queued || System.Array.IndexOf(imported, CharacterHitBloodSetup.PlayerPath) < 0) return;
        queued = true; EditorApplication.update += SyncWhenReady;
    }
    static void SyncWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= SyncWhenReady; queued = false; Sync();
    }
    [MenuItem("Doodle Idle/Effects/Sync Hit Blood From Player")]
    public static void Sync()
    {
        if (syncing) return;
        syncing = true; GameObject root = null;
        try {
            root = PrefabUtility.LoadPrefabContents(CharacterHitBloodSetup.PlayerPath);
            var blood = root.GetComponent<CharacterRig>().hitBlood;
            if (!blood || !PrefabUtility.IsPartOfPrefabInstance(blood)) return;
            bool changed = PromoteParticleOverrides(blood, CharacterHitBloodSetup.PrefabPath);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, CharacterHitBloodSetup.PlayerPath);
        } finally {
            if (root) PrefabUtility.UnloadPrefabContents(root);
            syncing = false;
        }
    }
    public static bool PromoteParticleOverrides(CharacterHitBlood blood, string sharedPath)
    {
        bool changed = false;
        foreach (var component in new Component[] { blood.GetComponent<ParticleSystem>(), blood.GetComponent<ParticleSystemRenderer>() }) {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(component);
            var modifications = PrefabUtility.GetPropertyModifications(blood.gameObject);
            if (modifications == null || !System.Array.Exists(modifications, m => m.target == source)) continue;
            PrefabUtility.ApplyObjectOverride(component, sharedPath, InteractionMode.AutomatedAction);
            changed = true;
        }
        return changed;
    }
}

[CustomEditor(typeof(CharacterHitBlood))]
public sealed class CharacterHitBloodEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("플레이어 프리팹의 HitBlood에서 Particle System 설정을 바꾸고 저장하면 공통 원본에 적용되어 모든 리깅 프리팹에 반영됩니다. 위치는 캐릭터별로 조절합니다. 플레이 중 수정은 저장되지 않습니다.", MessageType.Info);
        if (GUILayout.Button("공통 피격 파티클 프리팹 열기"))
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterHitBloodSetup.PrefabPath));
    }
}
