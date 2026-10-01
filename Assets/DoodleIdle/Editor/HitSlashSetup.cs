using UnityEditor;
using UnityEngine;

public static class HitSlashSetup
{
    public const string PrefabPath = "Assets/DoodleIdle/Resources/DoodleIdle/HitSlash.prefab";
    [MenuItem("Doodle Idle/Effects/Create Hit Slash Prefab")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) return;
        const string materialPath = "Assets/DoodleIdle/Resources/DoodleIdle/HitSlash.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material) {
            material = new Material(Resources.Load<Shader>("DoodleIdle/DoodleParticles"));
            material.mainTexture = Resources.Load<Texture2D>("DoodleIdle/RedSlashA");
            GoldCoinBurstSetup.FitArt(material);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var root = new GameObject("HitSlash");
        try {
            var ps = root.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 256; main.startSpeed = 0; main.startLifetime = .16f;
            main.startSize3D = true; main.startSizeX = 1.8f; main.startSizeY = .65f; main.startSizeZ = 1;
            main.startRotation = new ParticleSystem.MinMaxCurve(25 * Mathf.Deg2Rad, 155 * Mathf.Deg2Rad);
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var color = ps.colorOverLifetime; color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .55f), new Keyframe(.2f, 1), new Keyframe(1, 1.15f)));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = 660; renderer.maxParticleSize = 1;
            ps.useAutoRandomSeed = false; ps.randomSeed = 661;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
        } finally { Object.DestroyImmediate(root); }
    }
}
