using System;
using System.IO;
using System.Linq;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

public static class CharacterRigPresentation
{
    const string Root = "Assets/DoodleIdle/CharacterRigs/";

    [MenuItem("Doodle Idle/Character Rigs/Apply Sorting And Ground Contacts")]
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
        Debug.Log("CHARACTER_PRESENTATION_UPDATED: sorting groups and ground contacts; animation assets untouched");
    }

    // Never rewrite authored animation clips, controllers, masks or bone hierarchies.
    public static void Configure(CharacterRig rig) => SetGroundContact(rig);

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
