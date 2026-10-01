using DoodleIdle;
using UnityEditor;
using UnityEngine;

public static class DoodleSpawnPortalSetup
{
    const string Root = "Assets/DoodleIdle/Resources/DoodleIdle/";
    [InitializeOnLoadMethod]
    static void Queue() => EditorApplication.delayCall += Ensure;
    [MenuItem("Doodle Idle/Effects/Create Missing Spawn Portal Prefab")]
    public static void Ensure()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "EnemySpawnPortal.prefab")) return;
        var importer = AssetImporter.GetAtPath(Root + "EnemySpawnCircle.png") as TextureImporter;
        if (!importer) return;
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 320; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.isReadable = false; importer.maxTextureSize = 512; importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "EnemySpawnCircle.png");
        var root = new GameObject("EnemySpawnPortal");
        try {
            root.transform.localScale = new Vector3(1, .35f, 1);
            var circle = new GameObject("RotatingCircle"); circle.transform.SetParent(root.transform, false);
            var art = circle.AddComponent<SpriteRenderer>(); art.sprite = sprite; art.sortingOrder = -890;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "DoodleGoldCoinSprite.shader");
            var material = new Material(shader) { name = "EnemySpawnCircle" };
            material.SetTexture("_MainTex", sprite.texture); material.SetVector("_UvRect", new Vector4(0, 0, 1, 1));
            AssetDatabase.CreateAsset(material, Root + "EnemySpawnCircle.mat"); art.sharedMaterial = material;
            var portal = root.AddComponent<DoodleSpawnPortal>(); portal.rotatingCircle = circle.transform; portal.art = art;
            PrefabUtility.SaveAsPrefabAsset(root, Root + "EnemySpawnPortal.prefab");
        } finally { Object.DestroyImmediate(root); }
    }
}
