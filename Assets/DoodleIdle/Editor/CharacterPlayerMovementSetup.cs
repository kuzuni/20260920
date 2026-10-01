using System.Linq;
using DoodleIdle;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;

public static class CharacterPlayerMovementSetup
{
    public const string PlayerPath = "Assets/DoodleIdle/CharacterRigs/Prefabs/Player_Standard.prefab";
    [MenuItem("Doodle Idle/Character Rigs/Install Player A B C Triggers")]
    public static void Install()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try {
            var rig = root.GetComponent<CharacterRig>();
            if (rig.movementZones) return;
            var entry = DoodleCharacterCatalog.Current.entries.First(e => e.group == "Player");
            var zones = root.AddComponent<CharacterMovementZones>();
            rig.movementZones = zones;
            var anchor = new GameObject("MovementZones").transform;
            anchor.SetParent(root.transform, false);
            // The gameplay rig aligns this center with the player's Rigidbody origin.
            anchor.localPosition = new Vector3(0, entry.center.y, 0);
            float authorUnits = entry.extent / (2 * 1.28f);
            CircleCollider2D Add(string name, float radius) {
                var child = new GameObject(name); child.transform.SetParent(anchor, false);
                var circle = child.AddComponent<CircleCollider2D>();
                circle.isTrigger = true; circle.radius = radius * authorUnits;
                // Geometric sensors do not need broadphase contact pairs or callbacks.
                circle.excludeLayers = ~0; circle.callbackLayers = 0; circle.contactCaptureLayers = 0;
                return circle;
            }
            zones.stopAndAttack = Add("A_StopAndAttack", 3.2f);
            zones.startRetreat = Add("B_StartRetreat", 1.4f);
            zones.finishRetreat = Add("C_FinishRetreat", 2.5f);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
