using DoodleIdle;
using UnityEditor;
using UnityEngine;
public static class DoodleUpgradeEffectSetup
{
    [InitializeOnLoadMethod] static void Queue() => EditorApplication.delayCall += Install;
    static void Install()
    {
        const string path = "Assets/DoodleIdle/Resources/DoodleIdle/StatUpgradeEffect.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
        var root = new GameObject("StatUpgradeEffect");
        try {
            var effect = root.AddComponent<DoodleUpgradeEffect>();
            effect.portal = ((GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("DoodleIdle/EnemySpawnPortal"), root.transform)).GetComponent<DoodleSpawnPortal>();
            float size = 1 / effect.portal.art.sprite.bounds.size.x;
            effect.portal.transform.localScale = new Vector3(size, size * .35f, 1);
            effect.portal.art.sortingOrder = -10;
            var go = new GameObject("Upgrade light particles"); go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.glow = ps;
            var main = ps.main; main.loop = false; main.duration = .9f; main.playOnAwake = false; main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .9f); main.startSize = new ParticleSystem.MinMaxCurve(.025f, .07f);
            main.startSpeed = 0; main.startColor = new ParticleSystem.MinMaxGradient(new Color(1,.83f,.2f), Color.white); main.maxParticles = 32;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .4f; shape.scale = new Vector3(1,.3f,1);
            var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[]{new ParticleSystem.Burst(0,18)});
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-.22f,.22f); velocity.y = new ParticleSystem.MinMaxCurve(.3f,.8f); velocity.z = new ParticleSystem.MinMaxCurve(0,0);
            var color = ps.colorOverLifetime; color.enabled = true; var gradient = new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.85f,.25f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}); color.color = gradient;
            const string materialPath = "Assets/DoodleIdle/Resources/DoodleIdle/StatUpgradeGlow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(Resources.Load<Shader>("DoodleIdle/DoodleUpgradeGlow")); material.mainTexture = Texture2D.whiteTexture; AssetDatabase.CreateAsset(material,materialPath); }
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = 20;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        } finally { Object.DestroyImmediate(root); }
    }
}
