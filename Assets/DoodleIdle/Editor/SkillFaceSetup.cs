using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DoodleIdle;
using UnityEditor;
using UnityEngine;
using DoodleIdle.CharacterRigs;

public static class SkillFaceSetup
{
    const string Art="Assets/DoodleIdle/Art/SkillFaces/";
    const string Prefabs="Assets/DoodleIdle/CharacterRigs/SkillFaces/";
    [Serializable] public class Source { public string key, texture, group; public int width, height; public float ppu; public Vector2 pivot; public Vector2 center; public float eyeWidth, spacing; }
    [Serializable] public class Sources { public List<Source> items = new List<Source>(); }
    [MenuItem("Doodle Idle/Character Rigs/Match Skill Faces To Player Layout")]
    public static void MatchPlayerLayout()
    {
        var sources=JsonUtility.FromJson<Sources>(File.ReadAllText(Art+"layout.json")).items;
        var player=AssetDatabase.LoadAssetAtPath<CharacterRig>("Assets/DoodleIdle/CharacterRigs/Prefabs/Player_Standard.prefab").face;
        float eyeWidth=player.leftEye.normal.bounds.size.x*Mathf.Abs(player.leftEye.white.transform.localScale.x*player.leftEye.pupilMotion.parent.localScale.x);
        Vector3 eyeCenter=(player.leftEye.pupilMotion.parent.localPosition+player.rightEye.pupilMotion.parent.localPosition)*.5f;
        foreach(var group in sources.GroupBy(s=>s.group))
        {
            var source=group.First();string path=Prefabs+group.Key+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<CharacterFace>(path);
            var face=UnityEngine.Object.Instantiate(player);
            try
            {
                face.name=existing.name;float scale=source.eyeWidth/Mathf.Max(.001f,eyeWidth);
                face.transform.SetParent(null,false);face.transform.localRotation=Quaternion.identity;
                face.transform.localScale=Vector3.one*scale;face.transform.localPosition=(Vector3)source.center-eyeCenter*scale;
                face.headRenderer=null;face.target=null;face.horizontalGazeOnly=false;
                face.leftEye.travel.y=face.rightEye.travel.y=eyeWidth*.3f;
                face.SetFaceParts(true,false);face.ResetExpression();
                PrefabUtility.SaveAsPrefabAsset(face.gameObject,path);
            }
            finally{UnityEngine.Object.DestroyImmediate(face.gameObject);}
        }
        AssetDatabase.SaveAssets();Build();
    }
    [MenuItem("Doodle Idle/Character Rigs/Build Skill Faces (Keep Animation)")]
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var sources=JsonUtility.FromJson<Sources>(File.ReadAllText(Art+"layout.json")).items;
        Directory.CreateDirectory(Prefabs);
        foreach(var source in sources)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Art+source.key+".png");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=source.ppu;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(source.pivot.x/source.width,source.pivot.y/source.height);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        var prefabByGroup=new Dictionary<string,CharacterFace>();
        var baseline=new Dictionary<string,Vector2>();
        foreach(var group in sources.GroupBy(s=>s.group))
        {
            var source=group.First();baseline[group.Key]=source.center;
            string path=Prefabs+group.Key+".prefab";
            var existing=AssetDatabase.LoadAssetAtPath<CharacterFace>(path);
            if(existing){prefabByGroup[group.Key]=existing;continue;}
            var template=AssetDatabase.LoadAssetAtPath<CharacterRig>("Assets/DoodleIdle/CharacterRigs/Prefabs/Player_Standard.prefab").face;
            var face=UnityEngine.Object.Instantiate(template);face.name=group.Key+" Face";
            try
            {
                face.transform.SetParent(null,false);face.transform.localPosition=source.center;face.transform.localRotation=Quaternion.identity;face.transform.localScale=Vector3.one;
                face.headRenderer=null;face.target=null;face.horizontalGazeOnly=false;face.SetFaceParts(true,false);face.ResetExpression();
                void Eye(CharacterFace.Eye eye,float side)
                {
                    var anchor=eye.pupilMotion.parent;anchor.localPosition=new Vector3(side*source.spacing*.5f,0,0);anchor.localScale=Vector3.one;anchor.localRotation=Quaternion.identity;
                    eye.white.transform.localScale=new Vector3(source.eyeWidth/eye.normal.bounds.size.x,source.eyeWidth/eye.normal.bounds.size.y,1);
                    eye.pupil.transform.localScale=new Vector3(source.eyeWidth*.62f/eye.pupil.sprite.bounds.size.x,source.eyeWidth*.62f/eye.pupil.sprite.bounds.size.y,1);
                    eye.highlight.transform.localScale=new Vector3(source.eyeWidth*.16f/eye.highlight.sprite.bounds.size.x,source.eyeWidth*.16f/eye.highlight.sprite.bounds.size.y,1);
                    eye.highlight.transform.localPosition=new Vector3(source.eyeWidth*.09f,source.eyeWidth*.11f,0);
                    eye.travel=new Vector2(source.eyeWidth*.34f,source.eyeWidth*.30f);
                }
                Eye(face.leftEye,-1);Eye(face.rightEye,1);
                face.mouth.transform.parent.localPosition=new Vector3(0,-source.eyeWidth*.8f,0);face.mouth.transform.parent.localScale=Vector3.one;
                face.mouth.transform.localPosition=Vector3.zero;face.mouth.transform.localRotation=Quaternion.identity;
                face.mouth.transform.localScale=Vector3.one*(source.eyeWidth*.85f/face.normalMouth.bounds.size.x);
                face.RefreshHighlights();PrefabUtility.SaveAsPrefabAsset(face.gameObject,path);
            }
            finally{UnityEngine.Object.DestroyImmediate(face.gameObject);}
            prefabByGroup[group.Key]=AssetDatabase.LoadAssetAtPath<CharacterFace>(path);
        }
        const string catalogPath="Assets/DoodleIdle/Resources/DoodleIdle/SkillFaceCatalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<DoodleSkillFaceCatalog>(catalogPath);
        if(!catalog){catalog=ScriptableObject.CreateInstance<DoodleSkillFaceCatalog>();AssetDatabase.CreateAsset(catalog,catalogPath);}
        catalog.entries=sources.Select(s=>new DoodleSkillFaceCatalog.Entry{sourceName=s.key,sourceTexture=s.texture,body=AssetDatabase.LoadAssetAtPath<Sprite>(Art+s.key+".png"),facePrefab=prefabByGroup[s.group],frameOffset=s.center-baseline[s.group]}).ToArray();
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("artifacts/skill-faces");
        foreach(var entry in catalog.entries)
        {
            var root=new GameObject(entry.sourceName);var body=root.AddComponent<SpriteRenderer>();body.sprite=entry.body;
            try
            {
                var visual=root.AddComponent<DoodleSkillFaceVisual>();visual.Configure(null,body,entry.body,entry);
                var bounds=body.bounds;
                CharacterRigVerifier.RenderPortrait(root,"artifacts/skill-faces/"+entry.sourceName+".png",bounds.center,Mathf.Max(bounds.size.x,bounds.size.y)*.60f);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        File.WriteAllText("Library/SkillFaces.complete",sources.Count+" frames; "+prefabByGroup.Count+" face prefabs");
    }
    public static void ExportSources()
    {
        const string folder="output/skill-faces/sources"; Directory.CreateDirectory(folder);
        var sources=new Sources();
        void Export(Sprite sprite,string group)
        {
            var rect=sprite.rect;
            var image=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGBA32,false);
            image.SetPixels(sprite.texture.GetPixels((int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height));image.Apply();
            File.WriteAllBytes(folder+"/"+sprite.name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            sources.items.Add(new Source{key=sprite.name,texture=sprite.texture.name,group=group,width=(int)rect.width,height=(int)rect.height,ppu=sprite.pixelsPerUnit,pivot=sprite.pivot});
        }
        Rect Bounds(Texture2D texture)
        {
            int x0=texture.width,y0=texture.height,x1=-1,y1=-1;var pixels=texture.GetPixels32();
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>32){x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);}
            return new Rect(x0,y0,x1-x0+1,y1-y0+1);
        }
        foreach(var key in new[]{"SnakeHead","PurpleSnakeHead","DragonHead","WormHead","StormCloud","StormCloudB","RobotDroneA","RobotDroneB"})
        {
            var texture=Resources.Load<Texture2D>("DoodleIdle/"+key);var rect=Bounds(texture);
            if(key.StartsWith("RobotDrone"))
            {
                var other=Bounds(Resources.Load<Texture2D>("DoodleIdle/"+(key.EndsWith("A")?"RobotDroneB":"RobotDroneA")));
                rect=Rect.MinMaxRect(Mathf.Min(rect.xMin,other.xMin),Mathf.Min(rect.yMin,other.yMin),Mathf.Max(rect.xMax,other.xMax),Mathf.Max(rect.yMax,other.yMax));
            }
            var sprite=Sprite.Create(texture,rect,Vector2.one*.5f,Mathf.Max(rect.width,rect.height));sprite.name=key;
            Export(sprite,key.StartsWith("RobotDrone")?"RobotDrone":key=="StormCloudB"?"StormCloud":key);UnityEngine.Object.DestroyImmediate(sprite);
        }
        foreach(var key in new[]{"IceSnakeHead","RedCloud","RedCloudB"})Export(DoodleVariantArt.Get(key),key=="RedCloudB"?"RedCloud":key);
        for(int i=0;i<4;i++){Export(DoodleExpansionArt.Get("SkillGolem",i),"SkillGolem");Export(DoodleAscensionArt.FireGolem(i),"SkillFireGolem");}
        Export(DoodleAscensionArt.Cell(5),"MightyDragon");Export(DoodleAscensionArt.Cell(8),"SawSnake");
        File.WriteAllText("output/skill-faces/sources.json",JsonUtility.ToJson(sources,true));
    }
}
