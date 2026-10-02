using System.Linq;
using DoodleIdle;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;
public static class CharacterSpawnZonesSetup
{
    [InitializeOnLoadMethod] static void Queue() => EditorApplication.delayCall += Install;
    [MenuItem("Doodle Idle/Character Rigs/Install Spawn A B Triggers")]
    public static void Install()
    {
        const string path = "Assets/DoodleIdle/CharacterRigs/Prefabs/Player_Standard.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            if (root.GetComponentInChildren<CharacterSpawnZones>(true)) return;
            var entry = DoodleCharacterCatalog.Current.entries.First(e => e.group == "Player");
            var anchor = new GameObject("SpawnZones"); anchor.transform.SetParent(root.transform, false);
            // Preview in rig authoring units. Gameplay detaches this anchor at unit scale.
            anchor.transform.localPosition = new Vector3(0, entry.center.y, 0);
            anchor.transform.localScale = Vector3.one * (entry.extent / (2 * 1.28f));
            var zones = anchor.AddComponent<CharacterSpawnZones>();
            CircleCollider2D Add(string name, float radius) {
                var child = new GameObject(name); child.layer = 2; child.transform.SetParent(anchor.transform, false);
                var circle = child.AddComponent<CircleCollider2D>(); circle.radius = radius; return circle;
            }
            var old = Resources.Load<CircleCollider2D>("DoodleIdle/PlayerSpawnExclusion");
            zones.exclusion = Add("A_NoSpawn", old ? old.radius : 4.5f);
            if (old) zones.exclusion.offset = old.offset;
            zones.boundary = Add("B_SpawnBoundary", 16);
            zones.ConfigureTriggers(); PrefabUtility.SaveAsPrefabAsset(root, path);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
