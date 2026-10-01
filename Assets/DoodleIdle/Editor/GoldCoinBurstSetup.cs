using UnityEditor;
using UnityEngine;

public static class GoldCoinBurstSetup
{
    public const string PrefabPath = "Assets/DoodleIdle/Resources/DoodleIdle/GoldCoinBurst.prefab";
    [MenuItem("Doodle Idle/Effects/Create Gold Coin Burst Prefab")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) return;
        const string materialPath = "Assets/DoodleIdle/Resources/DoodleIdle/GoldCoinBurst.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material) {
            material = new Material(Resources.Load<Shader>("DoodleIdle/DoodleParticles"));
            material.mainTexture = Resources.Load<Texture2D>("DoodleIdle/GoldCoin");
            FitArt(material);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var root = new GameObject("GoldCoinBurst");
        try {
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.duration = 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 2048; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSize = new ParticleSystem.MinMaxCurve(.22f, .4f);
            main.startLifetime = 3; // Safety ceiling; landing starts the short fade timer.
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 6);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 9) });
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .001f;
            root.AddComponent<DoodleIdle.DoodleGoldCoinBurst>();
            var spin = ps.rotationOverLifetime; spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-7, 7);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = 620; renderer.maxParticleSize = 1;
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.alignment = ParticleSystemRenderSpace.View;
            ps.useAutoRandomSeed = false; ps.randomSeed = 621;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        } finally { Object.DestroyImmediate(root); }
    }
    public static void FitArt(Material material)
    {
        var texture = (Texture2D)material.mainTexture;
        var pixels = texture.GetPixels32();
        int x0 = texture.width, y0 = texture.height, x1 = -1, y1 = -1;
        for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            if (pixels[y * texture.width + x].a > 32) {
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }
        if (x1 < x0) return;
        material.SetVector("_UvRect", new Vector4((float)x0 / texture.width, (float)y0 / texture.height,
            (float)(x1 - x0 + 1) / texture.width, (float)(y1 - y0 + 1) / texture.height));
        EditorUtility.SetDirty(material);
    }
}
