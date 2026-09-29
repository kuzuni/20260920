using System;
using System.Linq;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerAttackLayerSetup
{
    [MenuItem("Doodle Idle/Character Rigs/Add Player Arm Attack Layer")]
    public static void Apply() => ApplyTo("Player_Standard");

    [MenuItem("Doodle Idle/Character Rigs/Add Attack Layers To Armed And Winged Bipeds")]
    public static void ApplySupportedTypes()
    {
        foreach (var name in new[] { "Player_Standard", "Character_standard", "Character_wing" }) ApplyTo(name);
    }

    static void ApplyTo(string prefabName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DoodleIdle/CharacterRigs/Prefabs/" + prefabName + ".prefab");
        var rig = prefab.GetComponent<CharacterRig>();
        var controller = rig.animator.runtimeAnimatorController as AnimatorController;
        if (!controller) throw new InvalidOperationException("Missing Animator controller: " + prefabName);
        if (rig.rigType != "standard" && rig.rigType != "wing")
            throw new InvalidOperationException("Attack limb layers require an armed or winged biped.");
        // Never rebuild a layer that the user may already have edited.
        if (controller.layers.Any(l => l.name == "Upper Body")) return;
        var attackClip = controller.animationClips.First(c => c.name == "Attack");
        string limb = rig.rigType == "wing" ? "날개" : "팔";
        string maskName = rig.rigType == "wing" ? "Wings" : "Arms";
        string maskPath = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(controller)).Replace('\\', '/') + "/" + maskName + ".mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (!mask)
        {
            mask = new AvatarMask { name = maskName };
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            mask.transformCount = transforms.Length;
            for (int i = 0; i < transforms.Length; i++)
            {
                mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(transforms[i], prefab.transform));
                mask.SetTransformActive(i, transforms[i].name.StartsWith(limb) || transforms[i].name == "Weapon");
            }
            AssetDatabase.CreateAsset(mask, maskPath);
        }
        if (!controller.parameters.Any(p => p.name == "UpperAttack"))
            controller.AddParameter("UpperAttack", AnimatorControllerParameterType.Trigger);
        var machine = new AnimatorStateMachine { name = "Upper Body" };
        AssetDatabase.AddObjectToAsset(machine, controller);
        var empty = machine.AddState("Locomotion", new Vector3(180, 0));
        empty.writeDefaultValues = false; // No upper-body override while not attacking.
        var attack = machine.AddState("Attack", new Vector3(430, 0));
        attack.motion = attackClip; attack.writeDefaultValues = false;
        machine.defaultState = empty;
        var start = machine.AddAnyStateTransition(attack);
        start.hasExitTime = false; start.duration = 0; start.canTransitionToSelf = true;
        start.AddCondition(AnimatorConditionMode.If, 0, "UpperAttack");
        var finish = attack.AddTransition(empty);
        finish.hasExitTime = true; finish.exitTime = 1; finish.duration = 0;
        foreach (var trigger in new[] { "Hit", "Die" })
        {
            var cancel = machine.AddAnyStateTransition(empty);
            cancel.hasExitTime = false; cancel.duration = 0; cancel.canTransitionToSelf = false;
            cancel.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }
        controller.AddLayer(new AnimatorControllerLayer { name = "Upper Body", stateMachine = machine,
            defaultWeight = 1, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override });
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        Debug.Log("ATTACK_LIMB_LAYER_READY: " + prefabName + "; existing clips and Base Layer preserved; Upper Body/" + maskName + " added.");
    }
}
