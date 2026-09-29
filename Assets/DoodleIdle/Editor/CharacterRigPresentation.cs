using System;
using System.IO;
using System.Linq;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

public static class CharacterRigPresentation
{
    const string Root = "Assets/DoodleIdle/CharacterRigs/";

    [MenuItem("Doodle Idle/Character Rigs/Apply Sorting Upper Body Attack And Ground Contacts")]
    public static void ApplyAll()
    {
        foreach (var path in Directory.GetFiles(Root + "Prefabs", "*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig = root.GetComponent<CharacterRig>();
                if (!rig) continue;
                var group = root.GetComponent<SortingGroup>() ?? root.AddComponent<SortingGroup>();
                group.enabled = true;
                Configure(rig);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_PRESENTATION_UPDATED: six rig prefabs, upper-body attack, ground contacts");
    }

    public static void Configure(CharacterRig rig)
    {
        var controller = rig.animator.runtimeAnimatorController as AnimatorController;
        if (!controller) throw new InvalidOperationException("Missing AnimatorController: " + rig.name);
        var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(controller)).Replace('\\', '/');
        var bones = rig.skeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
        // Legs must not inherit torso attack rotations. Keep their authored world pose.
        foreach (var bone in bones.Values.Where(t => t.name.StartsWith("다리") && !t.parent.name.StartsWith("다리")))
            if (bone.parent != rig.skeleton) bone.SetParent(rig.skeleton, true);
        var idle = controller.animationClips.First(c => c.name == "Idle");
        var restAngles = AnimationUtility.GetCurveBindings(idle)
            .Where(b => b.type == typeof(Transform) && b.propertyName.StartsWith("localEulerAngles"))
            .ToDictionary(b => b.path.Split('/').Last() + "/" + b.propertyName,
                b => AnimationUtility.GetEditorCurve(idle, b).Evaluate(0));
        // Preserve the user's new skeleton hierarchy and local rest pose. Bind existing
        // clips to those bones instead of rebuilding the prefab from a template.
        foreach (var clip in controller.animationClips.Distinct())
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform)) continue;
                var name = binding.path.Split('/').Last();
                if (!bones.TryGetValue(name, out var bone)) continue;
                var path = AnimationUtility.CalculateTransformPath(bone, rig.transform);
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (binding.propertyName.StartsWith("localEulerAngles") && curve.length > 0 &&
                    restAngles.TryGetValue(name + "/" + binding.propertyName, out float oldRest))
                {
                    int axis = binding.propertyName.EndsWith(".x") ? 0 : binding.propertyName.EndsWith(".y") ? 1 : 2;
                    float offset = Mathf.DeltaAngle(oldRest, bone.localEulerAngles[axis]);
                    var keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++) keys[i].value += offset;
                    curve.keys = keys;
                }
                if (path != binding.path) AnimationUtility.SetEditorCurve(clip, binding, null);
                var mapped = binding; mapped.path = path;
                AnimationUtility.SetEditorCurve(clip, mapped, curve);
            }
            if (clip.name == "Attack")
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (!IsUpper(binding.path.Split('/').Last())) AnimationUtility.SetEditorCurve(clip, binding, null);
            EditorUtility.SetDirty(clip);
        }

        var maskPath = dir + "/UpperBody.mask";
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (!mask) { mask = new AvatarMask { name = "UpperBody" }; AssetDatabase.CreateAsset(mask, maskPath); }
        var transforms = rig.GetComponentsInChildren<Transform>(true);
        mask.transformCount = transforms.Length;
        for (int i = 0; i < transforms.Length; i++)
        {
            mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(transforms[i], rig.transform));
            mask.SetTransformActive(i, transforms[i] != rig.transform && IsUpper(transforms[i].name));
        }
        EditorUtility.SetDirty(mask);

        var clips = controller.animationClips.Distinct().ToDictionary(c => c.name);
        // The base layer keeps locomotion running when the attack trigger fires.
        var baseMachine = controller.layers[0].stateMachine;
        foreach (var transition in baseMachine.anyStateTransitions.Where(t => t.conditions.Any(c => c.parameter == "Attack")).ToArray())
            baseMachine.RemoveAnyStateTransition(transition);
        foreach (var state in baseMachine.states.Where(s => s.state.name == "Attack").ToArray()) baseMachine.RemoveState(state.state);

        var layers = controller.layers.ToList();
        var old = layers.FirstOrDefault(l => l.name == "Upper Body");
        if (old != null) layers.Remove(old);
        var upper = new AnimatorStateMachine { name = "Upper Body" };
        AssetDatabase.AddObjectToAsset(upper, controller);
        var states = new System.Collections.Generic.Dictionary<string, AnimatorState>();
        foreach (var name in new[] { "Idle", "Move", "Attack", "Hit", "Death" })
        {
            var state = upper.AddState(name); state.motion = clips[name];
            state.writeDefaultValues = false; states.Add(name, state);
        }
        upper.defaultState = states["Idle"];
        Transition(states["Idle"], states["Move"], false, true);
        Transition(states["Move"], states["Idle"], false, false);
        foreach (var name in new[] { "Attack", "Hit", "Death" })
        {
            var t = upper.AddAnyStateTransition(states[name]);
            t.hasExitTime = false; t.duration = .06f; t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, name == "Death" ? "Die" : name);
            if (name != "Death")
            {
                Transition(states[name], states["Move"], true, true);
                Transition(states[name], states["Idle"], true, false);
            }
        }
        layers.Add(new AnimatorControllerLayer { name = "Upper Body", defaultWeight = 1,
            blendingMode = AnimatorLayerBlendingMode.Override, avatarMask = mask, stateMachine = upper });
        controller.layers = layers.ToArray();
        if (old != null) DestroyMachine(old.stateMachine);
        EditorUtility.SetDirty(controller);
        SetGroundContact(rig);
    }

    static void DestroyMachine(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states)
        {
            foreach (var transition in state.state.transitions) UnityEngine.Object.DestroyImmediate(transition, true);
            UnityEngine.Object.DestroyImmediate(state.state, true);
        }
        foreach (var transition in machine.anyStateTransitions) UnityEngine.Object.DestroyImmediate(transition, true);
        UnityEngine.Object.DestroyImmediate(machine, true);
    }

    static void Transition(AnimatorState from, AnimatorState to, bool exit, bool moving)
    {
        var t = from.AddTransition(to); t.hasExitTime = exit; t.exitTime = 1; t.duration = .08f;
        t.AddCondition(moving ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Moving");
    }

    static bool IsUpper(string name) => name == "머리" || name == "몸통" || name.StartsWith("팔") || name.StartsWith("날개") || name == "Weapon";

    static void SetGroundContact(CharacterRig rig)
    {
        // Calculate the rest-pose feet after skinning, not the transparent atlas bounds.
        var feet = rig.partRenderers.Where(r => r.name.StartsWith("다리")).ToArray();
        var sources = feet.Length > 0 ? feet : rig.partRenderers.Where(r => r.name == "머리").ToArray();
        Vector3 contact = Vector3.zero;
        foreach (var renderer in sources)
        {
            var sprite = renderer.sprite; var skin = renderer.GetComponent<SpriteSkin>();
            var vertices = sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position);
            var weights = sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight);
            var bind = sprite.GetBindPoses();
            var points = new Vector3[vertices.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var w = weights[i]; Vector3 world = Vector3.zero;
                void Add(int index, float weight) { if (weight > 0) world += skin.boneTransforms[index].localToWorldMatrix.MultiplyPoint3x4(bind[index].MultiplyPoint3x4(vertices[i])) * weight; }
                Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                points[i] = rig.transform.InverseTransformPoint(world);
            }
            float bottom = points.Min(p => p.y), top = points.Max(p => p.y);
            var sole = points.Where(p => p.y <= bottom + (top - bottom) * .12f).ToArray();
            contact += new Vector3((sole.Min(p => p.x) + sole.Max(p => p.x)) * .5f, bottom, 0);
        }
        if (sources.Length == 0) throw new InvalidOperationException("Missing ground reference: " + rig.name);
        contact /= sources.Length;
        if (feet.Length == 0) contact.y -= .35f;
        if (!rig.groundContact)
        {
            rig.groundContact = new GameObject("GroundContact").transform;
            rig.groundContact.SetParent(rig.transform, false);
        }
        rig.groundContact.localPosition = contact;
    }
}
