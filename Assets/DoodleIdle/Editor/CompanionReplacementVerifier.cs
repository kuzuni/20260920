using System;
using System.IO;
using System.Linq;
using DoodleIdle;
using UnityEditor;
using UnityEngine;

public static class CompanionReplacementVerifier
{
    const string Folder = "Assets/DoodleIdle/Resources/DoodleIdle/CompanionReplacements/";

    public static void BuildAndVerify()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string path in Directory.GetFiles(Folder, "*.png")) {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        DoodleCharacterCatalogBuild.Build();
        Verify();
    }

    [MenuItem("Doodle Idle/Character Rigs/Verify Companion Replacements")]
    public static void Verify()
    {
        foreach (int index in new[] {18, 21}) {
            var entry = DoodleCharacterCatalog.Current.Companion(index);
            string shotName = index == 18 ? "StoneShard" : "JellyDrop";
            var first = DoodleCollectionArt.CompanionFrame(index, 0);
            var second = DoodleCollectionArt.CompanionFrame(index, 1);
            if (!first || !second || first.texture != entry.portraits[0].texture || second.texture != entry.portraits[1].texture || first.texture == second.texture)
                throw new Exception("Replacement animation missing: " + index);
            if (first.pivot != second.pivot || first.rect.size != second.rect.size)
                throw new Exception("Replacement animation pivot mismatch: " + index);
            if (DoodleCollectionArt.Get("CompanionMon_" + index) != first)
                throw new Exception("Collection icon differs from companion: " + index);
            var shot = DoodleCollectionArt.Get("CompanionShot_" + index);
            if (!shot || shot.texture.name != shotName || DoodleCollectionArt.CompanionImpact(index) != shot)
                throw new Exception("Replacement projectile or impact missing: " + index);
            foreach (var sprite in new[] {first, second, shot}) {
                if (!sprite.texture.GetPixels32().Any(p => p.a > 128))
                    throw new Exception("Empty replacement artwork: " + sprite.name);
            }
        }
        CharacterRigBuilder.WriteArtifact("Library/CompanionReplacement.verified.json",
            System.Text.Encoding.UTF8.GetBytes("{\"companions\":2,\"animationFrames\":4,\"projectiles\":2,\"impactSprites\":2,\"errors\":[]}"));
        Debug.Log("COMPANION_REPLACEMENT_VERIFIED");
    }
}
