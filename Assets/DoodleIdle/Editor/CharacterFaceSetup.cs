using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoodleIdle;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class CharacterFaceSetup
{
    const string Root = "Assets/DoodleIdle/CharacterRigs/";
    const string Art = "Assets/DoodleIdle/Art/CharacterSprites/FaceParts/";
    [MenuItem("Doodle Idle/Character Rigs/Install Separated Faces (Keep Animation)")]
    public static void Install()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var path in Directory.GetFiles(Art, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterAppearance", new[] { Root + "Appearances" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var appearance = AssetDatabase.LoadAssetAtPath<CharacterAppearance>(path);
            bool separated = !path.Contains("/Companions/");
            if (appearance.separatedFace == separated) continue;
            appearance.separatedFace = separated;
            EditorUtility.SetDirty(appearance);
        }
        foreach (var path in Directory.GetFiles(Root + "Prefabs", "*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig = root.GetComponent<CharacterRig>();
                // Re-running installation preserves all user-authored face transforms.
                if (!rig.face) CreateFace(rig, path.Contains("Player_Standard"));
                rig.face.gameObject.SetActive(rig.appearance.separatedFace);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("SEPARATED_FACES_INSTALLED: 110 appearances, 6 prefabs; animation assets untouched");
    }

    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
    }
    static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, Vector2 size, int order, int layer)
    {
        var t = Child(parent, name, Vector3.zero);
        var r = t.gameObject.AddComponent<SpriteRenderer>(); r.sprite = sprite;
        t.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        r.sortingOrder = order; r.sortingLayerID = layer; return r;
    }
    static void CreateFace(CharacterRig rig, bool player)
    {
        var head = rig.partRenderers.Single(r => r.name == "머리");
        var bone = rig.skeleton.GetComponentsInChildren<Transform>(true).Single(t => t.name == "머리");
        var face = Child(bone, "Face", Vector3.zero).gameObject.AddComponent<CharacterFace>();
        Vector2 origin = new Vector2(950, 230), center = new Vector2(player ? 968 : 906, player ? 970 : 1000);
        float eyeWidth = player ? .95f : .9f, spacing = player ? 2.2f : 2.3f;
        switch (rig.rigType)
        {
            case "quad": origin = new Vector2(800,230); center = new Vector2(1140,1260); eyeWidth = .9f; spacing = 2.1f; break;
            case "wing": origin = new Vector2(1110,210); center = new Vector2(1120,1080); eyeWidth = 1f; spacing = 2.4f; break;
            case "biped": origin = new Vector2(750,250); center = new Vector2(750,860); eyeWidth = 1.55f; spacing = 3.6f; break;
            case "floating": origin = new Vector2(750,480); center = new Vector2(750,880); eyeWidth = 1.55f; spacing = 3.6f; break;
        }
        var part = rig.appearance.parts.Single(p => p.name == "머리");
        Vector3 local = (Vector3)((center - origin) / 100f) - part.rendererPosition;
        var bind = head.sprite.GetBindPoses()[0];
        face.transform.localPosition = bind.MultiplyPoint3x4(local);
        var right = bind.MultiplyVector(Vector3.right);
        face.transform.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(right.y,right.x)*Mathf.Rad2Deg);
        face.leftEye = CreateEye(face.transform, "LeftEye", -spacing*.5f, eyeWidth, head, "eye_hurt_left");
        face.rightEye = CreateEye(face.transform, "RightEye", spacing*.5f, eyeWidth, head, "eye_hurt_right");
        var mouthAnchor = Child(face.transform, "Mouth", new Vector3(0,-eyeWidth*.65f,0));
        face.normalMouth = Sprite("mouth_normal"); face.hurtMouth = Sprite("mouth_hurt");
        face.mouth = Renderer(mouthAnchor,"Sprite",face.normalMouth,Vector2.one*eyeWidth*.85f,head.sortingOrder+3,head.sortingLayerID);
        rig.face = face;
        ConfigureFaceSorting(rig);
        StyleEye(face.leftEye, !player, true);
        StyleEye(face.rightEye, !player, false);
    }
    static CharacterFace.Eye CreateEye(Transform parent, string name, float x, float width, SpriteRenderer head, string hurt)
    {
        var anchor = Child(parent, name, new Vector3(x,0,0));
        var eye = new CharacterFace.Eye { normal = Sprite("eye_white"), hurt = Sprite(hurt), travel = new Vector2(width*.13f,width*.12f) };
        eye.white = Renderer(anchor,"White",eye.normal,Vector2.one*width,head.sortingOrder+1,head.sortingLayerID);
        eye.pupilMotion = Child(anchor,"PupilMotion",Vector3.zero);
        eye.pupil = Renderer(eye.pupilMotion,"Pupil",Sprite("pupil"),Vector2.one*width*.5f,head.sortingOrder+2,head.sortingLayerID);
        return eye;
    }

    [MenuItem("Doodle Idle/Character Rigs/Apply Round Eyes And Enemy Brows")]
    public static void ApplyRoundEyes()
    {
        Install();
        foreach (var path in Directory.GetFiles(Root + "Prefabs", "*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var face = root.GetComponent<CharacterRig>().face;
                bool enemy = !path.Contains("Player_Standard");
                StyleEye(face.leftEye, enemy, true);
                StyleEye(face.rightEye, enemy, false);
                face.ResetExpression();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    static void StyleEye(CharacterFace.Eye eye, bool enemy, bool left)
    {
        // Change only visual children; never reposition authored face/eye/mouth anchors.
        eye.normal = Sprite("eye_white");
        eye.white.sprite = eye.normal;
        float width = eye.white.transform.localScale.x * eye.normal.bounds.size.x;
        var scale = eye.white.transform.localScale; scale.y = scale.x; eye.white.transform.localScale = scale;
        eye.pupil.sprite = Sprite("pupil");
        eye.pupil.transform.localScale = Vector3.one * (width*(eye.pupilMask ? .62f : .5f) / eye.pupil.sprite.bounds.size.x);
        if (enemy && !eye.brow)
        {
            eye.brow = Renderer(eye.pupilMotion.parent,"Brow",Sprite(left ? "brow_angry_left" : "brow_angry_right"),Vector2.one*width*1.2f,eye.pupil.sortingOrder+1,eye.white.sortingLayerID);
            eye.brow.transform.localPosition = new Vector3(0,width*.43f,0);
        }
        MaskEye(eye);
        RelaxedBlinkEye(eye);
    }

    [MenuItem("Doodle Idle/Character Rigs/Apply Relaxed Blinks")]
    public static void ApplyRelaxedBlinks()
    {
        foreach (var path in Directory.GetFiles(Root + "Prefabs", "*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var face = root.GetComponent<CharacterRig>().face;
                RelaxedBlinkEye(face.leftEye); RelaxedBlinkEye(face.rightEye);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    static void RelaxedBlinkEye(CharacterFace.Eye eye)
    {
        if (eye.lidMotion) return;
        eye.lidMotion = Child(eye.pupilMotion.parent,"EyelidMotion",Vector3.zero);
        // Identity wrapper preserves the White/PupilMask authored transforms.
        eye.white.transform.SetParent(eye.lidMotion,false);
    }

    [MenuItem("Doodle Idle/Character Rigs/Apply Pupil Masks")]
    public static void ApplyPupilMasks()
    {
        foreach (var path in Directory.GetFiles(Root + "Prefabs", "*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig = root.GetComponent<CharacterRig>();
                var face = rig.face;
                ConfigureFaceSorting(rig);
                MaskEye(face.leftEye); MaskEye(face.rightEye);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    static void ConfigureFaceSorting(CharacterRig rig)
    {
        var face = rig.face;
        face.headRenderer = rig.partRenderers.Single(r => r.name == "머리");
        face.sortingGroup = face.GetComponent<SortingGroup>();
        if (!face.sortingGroup) face.sortingGroup = face.gameObject.AddComponent<SortingGroup>();
        face.SyncSorting();
    }

    static void MaskEye(CharacterFace.Eye eye)
    {
        if (eye.pupilMask) { eye.pupilMask.isCustomRangeActive = false; return; } // Preserve subsequent prefab tuning.
        var anchor = eye.pupilMotion.parent;
        // Each eye has its own stencil scope, even when actors or eyes overlap.
        var group = anchor.GetComponent<SortingGroup>();
        if (!group) group = anchor.gameObject.AddComponent<SortingGroup>();
        group.sortingLayerID = eye.white.sortingLayerID;
        group.sortingOrder = eye.white.sortingOrder;
        var mask = Child(eye.white.transform, "PupilMask", Vector3.zero).gameObject.AddComponent<SpriteMask>();
        mask.sprite = eye.normal;
        mask.transform.localScale = new Vector3(.88f, .88f, 1);
        mask.alphaCutoff = .5f;
        mask.isCustomRangeActive = false; // The enclosing eye SortingGroup provides the mask scope.
        mask.backSortingLayerID = mask.frontSortingLayerID = eye.pupil.sortingLayerID;
        mask.backSortingOrder = eye.pupil.sortingOrder - 1;
        mask.frontSortingOrder = eye.pupil.sortingOrder;
        eye.pupilMask = mask;
        eye.pupil.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        float width = eye.white.transform.localScale.x * eye.normal.bounds.size.x;
        eye.pupil.transform.localScale = Vector3.one * (width*.62f / eye.pupil.sprite.bounds.size.x);
        // At full gaze the pupil crosses the inner edge and is cropped by the mask.
        eye.travel = new Vector2(width*.34f, width*.30f);
    }

    [MenuItem("Doodle Idle/Character Rigs/Render Separated Face Portraits")]
    public static void RenderPortraits()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DoodleCharacterCatalog>("Assets/DoodleIdle/Resources/DoodleIdle/CharacterCatalog.asset");
        var limits = new Dictionary<string, Bounds>();
        foreach (var entry in catalog.entries.Where(e => e.appearance.separatedFace))
        {
            var sample = Object.Instantiate(entry.prefab);
            try
            {
                sample.SetAppearance(entry.appearance);
                for (int pose = 0; pose < 2; pose++)
                {
                    Sample(sample, pose);
                    var bounds = CharacterRigVerifier.PreviewBounds(sample.gameObject);
                    string key = entry.group + "/" + entry.appearance.rigType;
                    if (limits.TryGetValue(key, out var previous)) { previous.Encapsulate(bounds); limits[key] = previous; }
                    else limits[key] = bounds;
                }
            }
            finally { Object.DestroyImmediate(sample.gameObject); }
        }
        foreach (var entry in catalog.entries.Where(e => e.appearance.separatedFace))
        {
            var bounds = limits[entry.group + "/" + entry.appearance.rigType];
            entry.center = bounds.center;
            entry.extent = Mathf.Max(bounds.size.x, bounds.size.y) * 1.08f;
            var rig = Object.Instantiate(entry.prefab);
            try
            {
                rig.SetAppearance(entry.appearance);
                for (int pose = 0; pose < 2; pose++)
                {
                    Sample(rig, pose);
                    string path = AssetDatabase.GetAssetPath(entry.portraits[pose]);
                    CharacterRigVerifier.RenderPortrait(rig.gameObject,path,entry.center,entry.extent*.55f);
                }
            }
            finally { Object.DestroyImmediate(rig.gameObject); }
        }
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    static void Sample(CharacterRig rig, int pose)
    {
        var clip = rig.animator.runtimeAnimatorController.animationClips.First(c => c.name == (pose == 0 ? "Idle" : "Move"));
        clip.SampleAnimation(rig.gameObject, pose == 0 ? 0 : clip.length*.25f);
        if (rig.face) rig.face.SyncSorting();
    }

    [MenuItem("Doodle Idle/Character Rigs/Preview Face Expressions")]
    public static void RenderExpressionSamples()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DoodleCharacterCatalog>("Assets/DoodleIdle/Resources/DoodleIdle/CharacterCatalog.asset");
        Directory.CreateDirectory("artifacts/character-faces");
        foreach (var id in new[] { "00_default", "01_meadow_sprout", "e01_quad_moss_wolf", "e04_wing_cave_bat", "e08_float_crystal_eye", "e10_biped_cactus" })
        {
            var entry = catalog.entries.Single(e => e.id == id);
            var rig = Object.Instantiate(entry.prefab);
            try
            {
                rig.SetAppearance(entry.appearance); Sample(rig,0);
                for (int hurt = 0; hurt < 3; hurt++)
                {
                    if (hurt == 1) rig.face.ShowHit();
                    if (hurt == 2)
                    {
                        rig.face.ResetExpression();
                        foreach (var eye in new[] { rig.face.leftEye, rig.face.rightEye })
                            if (eye.lidMotion) eye.lidMotion.localScale = new Vector3(1,.045f,1);
                    }
                    CharacterRigVerifier.RenderPortrait(rig.gameObject,"artifacts/character-faces/"+id+"_"+hurt+".png",entry.center,entry.extent*.55f);
                }
            }
            finally { Object.DestroyImmediate(rig.gameObject); }
        }
    }
}
