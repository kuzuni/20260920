using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoodleIdle;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class DoodleCharacterCatalogBuild : IPrebuildSetup
{
    const string Root = "Assets/DoodleIdle/CharacterRigs/";
    const string ResourcesRoot = "Assets/DoodleIdle/Resources/DoodleIdle/";
    [Serializable] sealed class CatalogRows { public CharacterRigBuilder.Row[] items; }
    [Serializable] sealed class Items { public Item[] items; }
    [Serializable] sealed class Item { public string id, icon; }
    public void Setup() => Build();

    [MenuItem("Doodle Idle/Character Rigs/Build Runtime Catalog")]
    public static void Build()
    {
        Directory.CreateDirectory("Library");
        File.WriteAllText("Library/CharacterRig.selection.json", "{\"sources\":[\"Companions/머리날개형/19_companion_brick.png\"]}");
        CharacterRigBuilder.RebuildSelectedSkinsKeepPrefabPoses();
        var rows = JsonUtility.FromJson<CatalogRows>("{\"items\":" + File.ReadAllText("Assets/DoodleIdle/Art/CharacterSprites/PSB/layer_manifest.json") + "}").items;
        var catalogPath = ResourcesRoot + "CharacterCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<DoodleCharacterCatalog>(catalogPath);
        if (!catalog) { catalog = ScriptableObject.CreateInstance<DoodleCharacterCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
        var entries = new List<DoodleCharacterCatalog.Entry>();
        var limits = new Dictionary<string, Bounds>();
        foreach (var row in rows) {
            string group = row.source.Split('/')[0], id = Path.GetFileNameWithoutExtension(row.source);
            var appearance = AssetDatabase.LoadAssetAtPath<CharacterAppearance>(Root + "Appearances/" + group + "/" + row.type + "/" + id + ".asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + (group == "Player" ? "Player_Standard" : "Character_" + row.type) + ".prefab").GetComponent<CharacterRig>();
            if (!appearance || !prefab) throw new Exception("Missing rig: " + row.source);
            var entry = new DoodleCharacterCatalog.Entry { id = id, group = group, appearance = appearance, prefab = prefab, theme = Theme(id), portraits = new Sprite[2] };
            entries.Add(entry);
            var go = Object.Instantiate(prefab.gameObject);
            try {
                var rig = go.GetComponent<CharacterRig>(); rig.SetAppearance(appearance);
                for (int pose = 0; pose < 2; pose++) {
                    Sample(rig, pose);
                    var bounds = CharacterRigVerifier.PreviewBounds(go);
                    string key = group + "/" + row.type;
                    if (limits.TryGetValue(key, out var existing)) { existing.Encapsulate(bounds); limits[key] = existing; } else limits[key] = bounds;
                }
            } finally { Object.DestroyImmediate(go); }
        }
        foreach (var entry in entries) {
            var bounds = limits[entry.group + "/" + entry.appearance.rigType];
            entry.center = bounds.center; entry.extent = Mathf.Max(bounds.size.x, bounds.size.y) * 1.08f;
            var go = Object.Instantiate(entry.prefab.gameObject);
            try {
                var rig = go.GetComponent<CharacterRig>(); rig.SetAppearance(entry.appearance);
                for (int pose = 0; pose < 2; pose++) {
                    Sample(rig, pose);
                    string path = ResourcesRoot + "RigPortraits/" + entry.group + "/" + entry.id + "_" + pose + ".png";
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    CharacterRigVerifier.RenderPortrait(go, path, entry.center, entry.extent * .55f);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 256; importer.alphaIsTransparency = true; importer.isReadable = true;
                    importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport(); entry.portraits[pose] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            } finally { Object.DestroyImmediate(go); }
        }
        catalog.entries = entries.ToArray(); catalog.companionIds = new string[34];
        var items = JsonUtility.FromJson<Items>(File.ReadAllText(ResourcesRoot + "UI/Collections.json")).items;
        foreach (var item in items.Where(i => i.icon != null && i.icon.StartsWith("CompanionMon_"))) {
            int index = int.Parse(item.icon.Substring(13));
            catalog.companionIds[index] = entries.Single(e => e.group == "Companions" && e.id.Substring(3) == item.id).id;
        }
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CharacterRigVerifier.Run();
        // Cloud-generated assets are downloaded into the checkout after validation.
        var generated = Directory.GetFiles(ResourcesRoot + "RigPortraits", "*", SearchOption.AllDirectories).Concat(new[] {catalogPath, catalogPath + ".meta", ResourcesRoot + "RigPortraits.meta", "Assets/DoodleIdle/Art/CharacterSprites/PSB/Companions/머리날개형/19_companion_brick.psb.meta", Root + "Appearances/Companions/floating/19_companion_brick.asset", Root + "Prefabs/Character_floating.prefab"});
        foreach (string path in generated.Where(File.Exists)) {
            string output = "artifacts/character-assets/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.Copy(path, output, true);
        }
        Debug.Log("CHARACTER_RUNTIME_CATALOG_BUILT " + entries.Count);
    }
    static void Sample(CharacterRig rig, int pose) {
        rig.animator.enabled = false;
        rig.animator.runtimeAnimatorController.animationClips.First(c => c.name == (pose == 0 ? "Idle" : "Move")).SampleAnimation(rig.gameObject, pose == 0 ? 0 : .25f);
    }
    static int Theme(string id) {
        string[] names = {"meadow", "desert", "forest", "swamp", "volcano", "coast", "crystal", "twilight", "ruins", "glacier"};
        for (int i = 0; i < names.Length; i++) if (id.Contains(names[i])) return i;
        if (id.Contains("snow") || id.Contains("ice")) return 9;
        if (id.Contains("sand") || id.Contains("cactus")) return 1;
        if (id.Contains("ember") || id.Contains("ash") || id.Contains("lava") || id.Contains("coal")) return 4;
        if (id.Contains("coral") || id.Contains("pearl") || id.Contains("storm")) return 5;
        if (id.Contains("moon") || id.Contains("night") || id.Contains("haunted")) return 7;
        if (id.Contains("clock") || id.Contains("chest") || id.Contains("bell") || id.Contains("tomb") || id.Contains("dice")) return 8;
        if (id.Contains("acid") || id.Contains("moss")) return 3;
        if (id.Contains("acorn") || id.Contains("thorn") || id.Contains("honey") || id.Contains("deer")) return 2;
        if (id.Contains("cave") || id.Contains("thunder")) return 6;
        return 0;
    }
}
