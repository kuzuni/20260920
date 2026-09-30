using System.IO;
using System.Linq;
using DoodleIdle;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterPopupPortraitSetup
{
    public static void LookRight(CharacterRig rig)
    {
        if (!rig.face) return;
        foreach (var eye in new[] { rig.face.leftEye, rig.face.rightEye })
            if (eye.pupilMotion) eye.pupilMotion.localPosition = new Vector3(eye.travel.x, 0, 0);
        rig.face.SyncSorting(); rig.face.RefreshHighlights();
    }

    [MenuItem("Doodle Idle/Character Rigs/Build Popup Portraits")]
    public static void Build()
    {
        const string root = "Assets/DoodleIdle/Resources/DoodleIdle/PopupPortraits/";
        Directory.CreateDirectory(root); Directory.CreateDirectory("Library/PopupPortraits");
        var catalog = AssetDatabase.LoadAssetAtPath<DoodleCharacterCatalog>("Assets/DoodleIdle/Resources/DoodleIdle/CharacterCatalog.asset");
        foreach (var entry in catalog.entries.Where(e => e.group == "Player"))
        {
            var rig = Object.Instantiate(entry.prefab);
            try
            {
                rig.SetAppearance(entry.appearance); rig.animator.enabled = false;
                var idle = rig.animator.runtimeAnimatorController.animationClips.First(c => c.name == "Idle");
                idle.SampleAnimation(rig.gameObject, 0); LookRight(rig);
                if (rig.weaponRenderer) rig.weaponRenderer.enabled = false;
                var bounds = CharacterRigVerifier.PreviewBounds(rig.gameObject);
                string path = root + entry.id + "_appearance.png";
                CharacterRigVerifier.RenderPortrait(rig.gameObject, path, bounds.center, Mathf.Max(bounds.size.x, bounds.size.y) * .525f);
                Import(path, true); entry.appearancePortrait = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally { Object.DestroyImmediate(rig.gameObject); }
        }
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
    }
    static void Import(string path, bool sprite)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (sprite) { importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 256; }
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048; importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = sprite ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }
}
