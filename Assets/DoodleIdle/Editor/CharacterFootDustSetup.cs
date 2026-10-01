using System.IO;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CharacterFootDustSetup
{
    [MenuItem("Doodle Idle/Character Rigs/Install Foot Dust (Keep Animation)")]
    public static void Install()
    {
        const string materialPath = "Assets/DoodleIdle/CharacterRigs/FootDust.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(Resources.Load<Shader>("DoodleIdle/DoodleParticles"));
            material.mainTexture = Resources.Load<Texture2D>("DoodleIdle/SandPuff");
            material.SetVector("_UvRect", new Vector4(0,0,1,1));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        foreach (var path in Directory.GetFiles("Assets/DoodleIdle/CharacterRigs/Prefabs", "*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig = root.GetComponent<CharacterRig>();
                if (rig.footDust) continue;
                if (!rig.groundContact) throw new System.InvalidOperationException("Missing GroundContact: " + path);
                var child = new GameObject("FootDust"); child.transform.SetParent(rig.groundContact, false);
                var ps = child.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.playOnAwake = false; main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Shape;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.45f,.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(.24f,.38f);
                main.startSpeed = 0; main.startRotation = new ParticleSystem.MinMaxCurve(-.4f,.4f);
                main.startColor = new Color(.86f,.78f,.65f,.65f); main.maxParticles = 64;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0; emission.rateOverDistance = 2;
                var shape = ps.shape; shape.enabled = false;
                var fade = ps.colorOverLifetime; fade.enabled = true;
                var gradient = new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(0,1)}); fade.color = gradient;
                var size = ps.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,.6f,1,1.5f));
                var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
                renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.alignment = ParticleSystemRenderSpace.View; renderer.maxParticleSize = 1;
                var sorting = child.AddComponent<SortingGroup>(); sorting.sortAtRoot = true; sorting.sortingOrder = -850;
                rig.footDust = root.AddComponent<CharacterFootDust>(); rig.footDust.particles = ps; rig.footDust.movementOrigin = root.transform;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        CharacterFootDustSync.Sync();
    }
}
