using System.IO;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;

// Player_Standard/FootDust is the shared authoring source. Saving that prefab
// propagates particle settings while retaining each character's foot anchor.
public sealed class CharacterFootDustSync : AssetPostprocessor
{
    static bool syncing, queued;
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (syncing || queued || System.Array.IndexOf(imported, CharacterPlayerMovementSetup.PlayerPath) < 0) return;
        queued = true;
        EditorApplication.update += SyncWhenReady;
    }
    static void SyncWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= SyncWhenReady; queued = false; Sync();
    }
    [MenuItem("Doodle Idle/Character Rigs/Sync Foot Dust From Player")]
    public static void Sync()
    {
        if (syncing) return;
        syncing = true;
        GameObject source = null;
        try {
            source = PrefabUtility.LoadPrefabContents(CharacterPlayerMovementSetup.PlayerPath);
            var dust = source.GetComponent<CharacterRig>().footDust;
            var particles = dust.particles;
            var emission = particles.emission;
            bool normalize = !emission.enabled || emission.rateOverTime.constantMax != 0 || emission.burstCount != 0
                || particles.main.simulationSpace != ParticleSystemSimulationSpace.World;
            emission.enabled = true; emission.rateOverTime = 0; emission.burstCount = 0;
            var main = particles.main; main.simulationSpace = ParticleSystemSimulationSpace.World;
            if (normalize) PrefabUtility.SaveAsPrefabAsset(source, CharacterPlayerMovementSetup.PlayerPath);
            foreach (var path in Directory.GetFiles("Assets/DoodleIdle/CharacterRigs/Prefabs", "*.prefab")) {
                if (path.Replace('\\','/') == CharacterPlayerMovementSetup.PlayerPath) continue;
                var target = PrefabUtility.LoadPrefabContents(path);
                try {
                    var targetDust = target.GetComponent<CharacterRig>().footDust;
                    if (!targetDust || !targetDust.particles) continue;
                    EditorUtility.CopySerialized(particles, targetDust.particles);
                    EditorUtility.CopySerialized(particles.GetComponent<ParticleSystemRenderer>(), targetDust.particles.GetComponent<ParticleSystemRenderer>());
                    targetDust.teleportDistance = dust.teleportDistance;
                    PrefabUtility.SaveAsPrefabAsset(target, path);
                } finally { PrefabUtility.UnloadPrefabContents(target); }
            }
        } finally {
            if (source) PrefabUtility.UnloadPrefabContents(source);
            syncing = false;
        }
    }
}
