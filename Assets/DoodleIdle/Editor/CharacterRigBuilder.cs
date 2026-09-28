using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Animation;
using UnityEditor.U2D.PSD;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CharacterRigBuilder
{
    const string Art = "Assets/DoodleIdle/Art/CharacterSprites/";
    const string Output = "Assets/DoodleIdle/CharacterRigs/";
    const float Ppu = 100f;
    const string Trigger = "Library/CharacterRig.build";
    static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    [Serializable] public class Row { public string source, psb, type; }
    [Serializable] class Rows { public Row[] items; }
    [Serializable] class Report { public int characters, sprites, prefabs; public List<string> checks = new(); public List<string> errors = new(); }
    sealed class Part
    {
        public string name; public Vector2 start, joint, end, delta; public bool two; public int order;
        public int index; public string[] Names => two ? new[] { name, name + "_끝" } : new[] { name };
    }
    sealed class Definition
    {
        public string type; public Vector2 origin; public List<Part> parts = new(); public SpriteBone[] bones;
    }
    static CharacterRigBuilder() { EditorApplication.delayCall += CheckTrigger; }
    static void CheckTrigger()
    {
        if (!File.Exists(Trigger)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += CheckTrigger; return; }
        File.Delete(Trigger);
        Build();
    }
    static Vector2 V(float x, float y) => new(x, 1700 - y);
    static Quaternion Rotation(Vector2 a, Vector2 b) => Quaternion.Euler(0, 0, Mathf.Atan2(b.y-a.y, b.x-a.x)*Mathf.Rad2Deg);
    static string Id(string s) { using var md5 = MD5.Create(); return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(s))).ToString("N"); }
    static Definition Define(string type)
    {
        var d = new Definition { type = type, origin = V(950,1470) };
        void Add(string n, float x, float y, float ex, float ey, float dx, float dy, int order, bool two = false)
        {
            var a=V(x,y); var b=V(ex,ey);
            d.parts.Add(new Part { name=n,start=a,end=b,joint=Vector2.Lerp(a,b,.52f),delta=new Vector2(dx,-dy),order=order,two=two });
        }
        switch(type)
        {
            case "standard":
                Add("머리",910,890,910,520,0,0,8); Add("몸통",950,1290,950,1040,0,-100,3);
                Add("팔1",270,1420,363,1575,490,-435,1,true); Add("팔2",518,1420,611,1575,562,-435,6,true);
                Add("다리1",847,1430,847,1600,0,-190,2,true); Add("다리2",1113,1430,1113,1600,-60,-190,5,true); break;
            case "wing":
                d.origin=V(1110,1490);
                Add("머리",1120,800,1120,390,0,0,8); Add("몸통",1110,1250,1110,940,0,-100,3);
                Add("날개1",670,535,200,650,280,425,1,true); Add("날개2",180,1090,670,1220,1100,-130,6,true);
                Add("다리1",980,1380,980,1560,0,-230,2,true); Add("다리2",1240,1380,1240,1560,0,-230,5,true); break;
            case "quad":
                d.origin=V(800,1470);
                Add("머리",1120,565,1220,260,0,260,8); Add("몸통",510,870,1130,870,0,0,3);
                Add("다리1",230,1190,230,1550,170,-160,1,true); Add("다리2",580,1190,580,1550,-40,-130,5,true);
                Add("다리3",960,1190,960,1550,-20,-160,2,true); Add("다리4",1290,1190,1290,1550,-190,-130,6,true);
                Add("꼬리",570,470,160,230,-260,400,0,true); break;
            case "biped":
                d.origin=V(750,1450);
                Add("머리",750,1020,750,410,0,0,6); Add("다리1",445,1230,445,1550,0,-170,1,true); Add("다리2",1085,1230,1085,1550,0,-170,3,true); break;
            case "floating":
                d.origin=V(750,1220);
                Add("머리",750,1000,750,400,0,0,6); Add("날개1",540,1360,160,1230,-200,-550,1,true); Add("날개2",980,1360,1340,1230,160,-550,3,true); break;
            default: throw new Exception("Unknown rig: "+type);
        }
        var bones=new List<SpriteBone> { new SpriteBone { name="Root",guid=Id(type+"/Root"),parentId=-1,position=d.origin,rotation=Quaternion.identity,length=100 } };
        foreach(var p in d.parts)
        {
            p.index=bones.Count;var next=p.two?p.joint:p.end;
            bones.Add(new SpriteBone {name=p.name,guid=Id(type+"/"+p.name),parentId=0,position=p.start-d.origin,rotation=Rotation(p.start,next),length=Vector2.Distance(p.start,next)});
            if(p.two) bones.Add(new SpriteBone {name=p.name+"_끝",guid=Id(type+"/"+p.name+"_끝"),parentId=p.index,position=new Vector3(Vector2.Distance(p.start,p.joint),0),rotation=Quaternion.identity,length=Vector2.Distance(p.joint,p.end)});
        }
        d.bones=bones.ToArray();return d;
    }
    static object NewInternal(string name) => Activator.CreateInstance(typeof(ICharacterDataProvider).Assembly.GetType("UnityEditor.U2D.Animation."+name,true),true);
    static object Call(object o,string method,params object[] args) => o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static object Prop(object o,string name) => o.GetType().GetProperty(name,Flags).GetValue(o);
    static void ReleaseMeta(string path)
    {
        AssetDatabase.ReleaseCachedFileHandles();
        // Windows can retain a mapped view after an import. Replace the file entry
        // with identical bytes before Unity serializes it; retain the original.
        string meta=path+".meta";
        if(!File.Exists(meta))return;
        string archive="Library/CharacterRigMetaWriteBackup";Directory.CreateDirectory(archive);
        byte[] bytes=File.ReadAllBytes(meta);
        File.Move(meta,archive+"/"+Guid.NewGuid().ToString("N")+".meta");
        File.WriteAllBytes(meta,bytes);
    }

    public static void WriteReport(string path,string text) => WriteArtifact(path,Encoding.UTF8.GetBytes(text));
    public static void WriteArtifact(string path,byte[] bytes)
    {
        AssetDatabase.ReleaseCachedFileHandles();
        if(File.Exists(path))
        {
            const string archive="Library/CharacterRigWriteBackup";
            Directory.CreateDirectory(archive);
            File.Move(path,archive+"/"+Guid.NewGuid().ToString("N")+Path.GetExtension(path));
        }
        File.WriteAllBytes(path,bytes);
    }

    static void GenerateMesh(ISpriteEditorDataProvider provider, SpriteRect rect, CharacterPart cp, Part part, Report report)
    {
        var mesh=NewInternal("SpriteMeshData");Call(mesh,"SetFrame",rect.rect);
        var controller=NewInternal("SpriteMeshDataController");controller.GetType().GetField("spriteMeshData").SetValue(controller,mesh);
        List<Vector2> vertices=null;int[] indices=null;bool valid=false;
        foreach(var setting in new[]{(.35f,(byte)10),(.6f,(byte)10),(.15f,(byte)32),(.8f,(byte)64),(.4f,(byte)128)})
        {
            Call(controller,"OutlineFromAlpha",NewInternal("OutlineGenerator"),provider.GetDataProvider<ITextureDataProvider>(),setting.Item1,setting.Item2);
            var outline=(Vector2[])Prop(mesh,"vertices");double outlineArea=0;
            foreach(var edge in (Array)Prop(mesh,"edges"))
            {
                int a=(int)edge.GetType().GetField("x").GetValue(edge),b=(int)edge.GetType().GetField("y").GetValue(edge);
                outlineArea+=(double)outline[a].x*outline[b].y-(double)outline[b].x*outline[a].y;
            }
            outlineArea=Math.Abs(outlineArea)*.5;
            Call(controller,"Triangulate",NewInternal("Triangulator"));
            vertices=((Vector2[])Prop(mesh,"vertices")).ToList();indices=(int[])Prop(mesh,"indices");double triangleArea=0;
            for(int i=0;i<indices.Length;i+=3)
            {
                var u=vertices[indices[i+1]]-vertices[indices[i]];var v=vertices[indices[i+2]]-vertices[indices[i]];
                triangleArea+=Math.Abs((double)u.x*v.y-(double)u.y*v.x)*.5;
            }
            // A nonempty triangulation can still contain only a tiny fragment.
            // Retry the alpha outline rather than accepting an invisible limb.
            if(vertices.Count>4 && outlineArea>1 && triangleArea>=outlineArea*.98 && triangleArea<=outlineArea*1.02){valid=true;break;}
        }
        if(!valid)throw new Exception("Auto geometry did not cover its alpha outline: "+rect.name);
        var meshEdges=(Array)Prop(mesh,"edges");var edges=new List<Vector2Int>();
        foreach(var e in meshEdges) edges.Add(new Vector2Int((int)e.GetType().GetField("x").GetValue(e),(int)e.GetType().GetField("y").GetValue(e)));
        // Subdivide the independently generated triangles for smooth joint weights.
        var tris=new List<int>();var midpoint=new Dictionary<(int,int),int>();
        int Mid(int a,int b) { var key=(Math.Min(a,b),Math.Max(a,b));if(midpoint.TryGetValue(key,out var found))return found;var i=vertices.Count;vertices.Add((vertices[a]+vertices[b])*.5f);midpoint[key]=i;return i; }
        for(int i=0;i<indices.Length;i+=3) {int a=indices[i],b=indices[i+1],c=indices[i+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);tris.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}
        var data=new Vertex2DMetaData[vertices.Count];var axis=part.end-part.start;
        for(int i=0;i<data.Length;i++)
        {
            var global=vertices[i]+(Vector2)cp.spritePosition.position;
            float t=Vector2.Dot(global-part.start,axis)/axis.sqrMagnitude;
            float w=part.two?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.72f,t)):0;
            data[i]=new Vertex2DMetaData { position=vertices[i],boneWeight=new BoneWeight {boneIndex0=0,weight0=1-w,boneIndex1=part.two?1:0,weight1=w} };
        }
        var mp=provider.GetDataProvider<ISpriteMeshDataProvider>();mp.SetVertices(rect.spriteID,data);mp.SetIndices(rect.spriteID,tris.ToArray());mp.SetEdges(rect.spriteID,edges.ToArray());report.sprites++;
    }

    static CharacterAppearance Rig(Row row,Definition d,Report report)
    {
        var path=Art+row.psb;
        ReleaseMeta(path);
        var importer=(PSDImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.useMosaicMode=true;importer.useCharacterMode=true;importer.spritePixelsPerUnit=Ppu;importer.mipmapEnabled=false;
        // Re-read changed alpha bounds before rebuilding geometry. Old packed
        // rectangles can point outside the newly packed layer after an art edit.
        var so=new SerializedObject(importer);so.FindProperty("m_ResliceFromLayer").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();importer.SaveAndReimport();
        importer=(PSDImporter)AssetImporter.GetAtPath(path);
        ReleaseMeta(path);
        so=new SerializedObject(importer);so.FindProperty("m_ResliceFromLayer").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();importer.SaveAndReimport();
        importer=(PSDImporter)AssetImporter.GetAtPath(path);
        var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var character=provider.GetDataProvider<ICharacterDataProvider>();var cd=character.GetCharacterData();cd.bones=d.bones;
        var rects=provider.GetSpriteRects();if(rects.Length!=d.parts.Count)throw new Exception(path+" unexpected layer count "+rects.Length);
        var boneProvider=provider.GetDataProvider<ISpriteBoneDataProvider>();
        foreach(var p in d.parts)
        {
            var rect=rects.Single(r=>r.name==p.name);int ci=Array.FindIndex(cd.parts,c=>c.spriteId==rect.spriteID.ToString());var cp=cd.parts[ci];cp.bones=p.two?new[]{p.index,p.index+1}:new[]{p.index};cd.parts[ci]=cp;
            var sb=new List<SpriteBone>();var first=d.bones[p.index];first.parentId=-1;first.position=p.start-(Vector2)cp.spritePosition.position;sb.Add(first);
            if(p.two){var second=d.bones[p.index+1];second.parentId=0;sb.Add(second);}boneProvider.SetBones(rect.spriteID,sb);
            GenerateMesh(provider,rect,cp,p,report);
        }
        character.SetCharacterData(cd);EditorUtility.SetDirty(importer);ReleaseMeta(path);AssetDatabase.WriteImportSettingsIfDirty(path);provider.Apply();AssetDatabase.ReleaseCachedFileHandles();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        var appearance=ScriptableObject.CreateInstance<CharacterAppearance>();appearance.rigType=row.type;appearance.characterId=Path.GetFileNameWithoutExtension(row.source);appearance.name=appearance.characterId;
        appearance.parts=d.parts.Select(p=>
        {
            var sprite=sprites.Single(s=>s.name==p.name);var rect=rects.Single(r=>r.name==p.name);var cp=cd.parts.Single(c=>c.spriteId==rect.spriteID.ToString());
            if(sprite.GetBones().Length!=p.Names.Length || sprite.GetBindPoses().Length!=p.Names.Length)throw new Exception("Bone import failed: "+path+"/"+p.name+" actual bones="+sprite.GetBones().Length+" binds="+sprite.GetBindPoses().Length);
            return new CharacterAppearance.Part {name=p.name,sprite=sprite,boneNames=p.Names,sortingOrder=p.order,rendererPosition=((Vector2)cp.spritePosition.position+sprite.pivot-d.origin)/Ppu};
        }).ToArray();
        if(row.source.StartsWith("Player/"))
        {
            var weapon=appearance.characterId=="00_default"?"05_police":appearance.characterId;
            appearance.weapon=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Player/Weapons/"+weapon+".png");appearance.weaponScale=1f;
        }
        string category=row.source.Split('/')[0];string dir=Output+"Appearances/"+category+"/"+row.type;Directory.CreateDirectory(dir);
        string output=dir+"/"+appearance.characterId+".asset";var existing=AssetDatabase.LoadAssetAtPath<CharacterAppearance>(output);
        if(existing!=null){EditorUtility.CopySerialized(appearance,existing);Object.DestroyImmediate(appearance);appearance=existing;}else AssetDatabase.CreateAsset(appearance,output);
        // Compare reloaded character skeleton, including every position/length/GUID.
        provider=factories.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(path));provider.InitSpriteEditorDataProvider();var actual=provider.GetDataProvider<ICharacterDataProvider>().GetCharacterData().bones;
        if(JsonUtility.ToJson(new BoneList{bones=actual})!=JsonUtility.ToJson(new BoneList{bones=d.bones}))throw new Exception("Common skeleton mismatch: "+path);
        report.characters++;File.WriteAllText("Library/CharacterRig.progress",report.characters+" rebuilt: "+row.source);return appearance;
    }
    [Serializable] class BoneList { public SpriteBone[] bones; }

    static GameObject MakePrefab(Definition d,CharacterAppearance appearance,string name,Report report)
    {
        string appearancePath=AssetDatabase.GetAssetPath(appearance);
        var go=new GameObject(name);go.AddComponent<SortingGroup>();var rig=go.AddComponent<CharacterRig>();rig.rigType=d.type;
        var skeleton=new GameObject("Root").transform;skeleton.SetParent(go.transform,false);rig.skeleton=skeleton;
        foreach(var p in d.parts)
        {
            var t=new GameObject(p.name).transform;t.SetParent(skeleton,false);t.localPosition=(p.start+p.delta-d.origin)/Ppu;t.localRotation=Rotation(p.start,p.two?p.joint:p.end);
            if(p.two){var second=new GameObject(p.name+"_끝").transform;second.SetParent(t,false);second.localPosition=new Vector3(Vector2.Distance(p.start,p.joint)/Ppu,0);}
        }
        var renderers=new List<SpriteRenderer>();
        foreach(var part in appearance.parts){var child=new GameObject(part.name);child.transform.SetParent(go.transform,false);renderers.Add(child.AddComponent<SpriteRenderer>());child.AddComponent<SpriteSkin>();}
        rig.partRenderers=renderers.ToArray();
        if(name=="Player_Standard")
        {
            var wrist=skeleton.Find("팔2/팔2_끝");var weapon=new GameObject("Weapon");weapon.transform.SetParent(wrist,false);
            var p=d.parts.Single(x=>x.name=="팔2");weapon.transform.localPosition=new Vector3(Vector2.Distance(p.joint,p.end)/Ppu,0,0);
            weapon.transform.localRotation=Quaternion.Euler(0,0,45-Rotation(p.start,p.end).eulerAngles.z-45);rig.weaponRenderer=weapon.AddComponent<SpriteRenderer>();rig.weaponRenderer.sortingOrder=5;
        }
        rig.animator=go.AddComponent<Animator>();rig.animator.runtimeAnimatorController=MakeAnimations(go,d,name);rig.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        rig.SetAppearance(AssetDatabase.LoadAssetAtPath<CharacterAppearance>(appearancePath));
        Directory.CreateDirectory(Output+"Prefabs");PrefabUtility.SaveAsPrefabAsset(go,Output+"Prefabs/"+name+".prefab");report.prefabs++;return go;
    }
    static RuntimeAnimatorController MakeAnimations(GameObject go,Definition d,string name)
    {
        string dir=Output+"Animations/"+name;Directory.CreateDirectory(dir);string path=dir+"/"+name+".controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach(var oldObject in AssetDatabase.LoadAllAssetsAtPath(path).Where(x=>x!=controller))Object.DestroyImmediate(oldObject,true);
        controller.layers=new[]{new AnimatorControllerLayer{name="Base Layer",defaultWeight=1,stateMachine=new AnimatorStateMachine()}};
        AssetDatabase.AddObjectToAsset(controller.layers[0].stateMachine,controller);
        controller.parameters=Array.Empty<AnimatorControllerParameter>();controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);
        foreach(var n in new[]{"Attack","Hit","Die"})controller.AddParameter(n,AnimatorControllerParameterType.Trigger);
        var sm=controller.layers[0].stateMachine;var states=new Dictionary<string,AnimatorState>();
        foreach(var state in new[]{"Idle","Move","Attack","Hit","Death"})
        {
            float duration=state=="Attack"?.5f:state=="Hit"?.25f:state=="Death"?.65f:state=="Move"?.65f:1.2f;
            var clip=new AnimationClip{name=state,frameRate=30};
            void Curve(string target,string property,float a,float b,float c){clip.SetCurve(target,typeof(Transform),property,new AnimationCurve(new Keyframe(0,a),new Keyframe(duration*.5f,b),new Keyframe(duration,c)));}
            Curve("Root","m_LocalPosition.y",0,state=="Move"?.09f:state=="Idle"?.035f:0,state=="Death"?-.2f:0);
            Curve("Root","localEulerAnglesRaw.z",0,state=="Hit"?12:state=="Death"?-40:0,state=="Death"?-85:0);
            foreach(var p in d.parts)
            {
                float angle=Rotation(p.start,p.two?p.joint:p.end).eulerAngles.z;float swing=0;
                bool limb=p.name.StartsWith("팔")||p.name.StartsWith("다리")||p.name.StartsWith("날개");
                if(state=="Move"&&limb)swing=(p.name.EndsWith("1")||p.name.EndsWith("4")?1:-1)*(p.name.StartsWith("날개")?20:17);
                if(state=="Idle"&&p.name.StartsWith("날개"))swing=p.name.EndsWith("1")?8:-8;
                if(state=="Attack")swing=p.name=="팔2"?-70:p.name.StartsWith("날개")?(p.name.EndsWith("1")?25:-25):p.name=="머리"?-9:0;
                Curve("Root/"+p.name,"localEulerAnglesRaw.z",angle,angle+swing,angle);
                if(p.two)Curve("Root/"+p.name+"/"+p.name+"_끝","localEulerAnglesRaw.z",0,state=="Move"?Math.Abs(swing)*.45f:state=="Attack"&&p.name=="팔2"?-30:0,0);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=state=="Idle"||state=="Move";AnimationUtility.SetAnimationClipSettings(clip,settings);
            string cp=dir+"/"+state+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(cp);if(old){EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);clip=old;}else AssetDatabase.CreateAsset(clip,cp);
            var s=sm.AddState(state);s.motion=clip;states[state]=s;
        }
        sm.defaultState=states["Idle"];
        var walk=states["Idle"].AddTransition(states["Move"]);walk.hasExitTime=false;walk.duration=.1f;walk.AddCondition(AnimatorConditionMode.If,0,"Moving");
        var stop=states["Move"].AddTransition(states["Idle"]);stop.hasExitTime=false;stop.duration=.1f;stop.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
        foreach(var pair in new[]{("Attack","Attack"),("Hit","Hit"),("Death","Die")})
        {var t=sm.AddAnyStateTransition(states[pair.Item1]);t.hasExitTime=false;t.duration=.06f;t.canTransitionToSelf=false;t.AddCondition(AnimatorConditionMode.If,0,pair.Item2);if(pair.Item1!="Death"){var back=states[pair.Item1].AddTransition(states["Idle"]);back.hasExitTime=true;back.exitTime=1;back.duration=.08f;}}
        EditorUtility.SetDirty(controller);return controller;
    }

    static void ConfigureWeapons()
    {
        foreach(var path in Directory.GetFiles(Art+"Player/Weapons","*.png"))
        {
            AssetDatabase.ReleaseCachedFileHandles();var p=path.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(p);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.1875f,.1875f);importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
    }
    static CharacterAppearance LoadAppearance(Row row)
    {
        return AssetDatabase.LoadAssetAtPath<CharacterAppearance>(Output+"Appearances/"+row.source.Split('/')[0]+"/"+row.type+"/"+Path.GetFileNameWithoutExtension(row.source)+".asset");
    }
    [Serializable] class Selection { public string[] sources; }
    [MenuItem("Doodle Idle/Character Rigs/Rebuild Selected Skins Keep Prefab Poses")]
    public static void RebuildSelectedSkinsKeepPrefabPoses()
    {
        var selection=JsonUtility.FromJson<Selection>(File.ReadAllText("Library/CharacterRig.selection.json"));
        if(selection?.sources==null || selection.sources.Length==0)throw new Exception("No skins selected");
        RebuildSkins(new HashSet<string>(selection.sources));
    }
    [MenuItem("Doodle Idle/Character Rigs/Rebuild Skins Keep Prefab Poses")]
    public static void RebuildSkinsKeepPrefabPoses() => RebuildSkins(null);
    static void RebuildSkins(HashSet<string> selected)
    {
        var report=new Report();Directory.CreateDirectory(Output+"Reports");
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var rows=JsonUtility.FromJson<Rows>("{\"items\":"+File.ReadAllText(Art+"PSB/layer_manifest.json")+"}").items;
            var selectedRows=selected==null?rows:rows.Where(r=>selected.Contains(r.source)).ToArray();
            if(selected!=null && selectedRows.Length!=selected.Count)throw new Exception("Unknown source in selection");
            var defs=selectedRows.Select(r=>r.type).Distinct().ToDictionary(t=>t,Define);
            foreach(var row in selectedRows)Rig(row,defs[row.type],report);
            AssetDatabase.SaveAssets();
            foreach(var prefabPath in Directory.GetFiles(Output+"Prefabs","*.prefab"))
            {
                string path=prefabPath.Replace('\\','/');var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var rig=root.GetComponent<CharacterRig>();
                    if(!defs.ContainsKey(rig.rigType))continue;
                    string appearancePath=AssetDatabase.GetAssetPath(rig.appearance);
                    var pose=rig.skeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
                    var controller=rig.animator.runtimeAnimatorController;
                    var orders=rig.partRenderers.ToDictionary(r=>r,r=>r.sortingOrder);
                    rig.SetAppearance(AssetDatabase.LoadAssetAtPath<CharacterAppearance>(appearancePath));
                    foreach(var kv in orders)kv.Key.sortingOrder=kv.Value;
                    foreach(var kv in pose)
                        if(kv.Key.localPosition!=kv.Value.Item1||kv.Key.localRotation!=kv.Value.Item2||kv.Key.localScale!=kv.Value.Item3)
                            throw new Exception("Prefab pose changed: "+path+"/"+kv.Key.name);
                    if(rig.animator.runtimeAnimatorController!=controller)throw new Exception("Animator changed: "+path);
                    PrefabUtility.SaveAsPrefabAsset(root,path);report.prefabs++;
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
            int expectedParts=selectedRows.Sum(r=>defs[r.type].parts.Count);
            if(report.characters!=selectedRows.Length||report.sprites!=expectedParts)throw new Exception("Unexpected rebuild totals");
            report.checks.Add(report.characters+" common skeletons reloaded and compared; "+report.sprites+" independently regenerated meshes");
            report.checks.Add(report.prefabs+" existing prefab skeleton poses, sorting orders and Animator controllers preserved; sprite references refreshed");
            report.checks.Add("Catalog contains "+rows.Length+" appearances; only selected types refreshed");
            File.WriteAllText("Library/CharacterRig.complete","SUCCESS");Debug.Log("CHARACTER_RIG_BUILD_SUCCESS");
        }
        catch(Exception e){report.errors.Add(e.ToString());File.WriteAllText("Library/CharacterRig.failed",e.ToString());Debug.LogException(e);}
        finally{WriteReport(Output+"Reports/build_report.json",JsonUtility.ToJson(report,true));AssetDatabase.Refresh();}
    }
    static void BuildPrefabs(Row[] rows,Report report)
    {
        foreach(var type in rows.Select(r=>r.type).Distinct())
        {
            var sample=rows.First(r=>r.type==type&&!r.source.StartsWith("Player/"));
            var go=MakePrefab(Define(type),LoadAppearance(sample),"Character_"+type,report);
            foreach(var row in rows.Where(r=>r.type==type))go.GetComponent<CharacterRig>().SetAppearance(LoadAppearance(row));
            Object.DestroyImmediate(go);report.checks.Add(type+": every appearance swap validated");
        }
        var player=rows.First(r=>r.source.EndsWith("00_default.png"));var playerGo=MakePrefab(Define("standard"),LoadAppearance(player),"Player_Standard",report);
        foreach(var row in rows.Where(r=>r.source.StartsWith("Player/")))playerGo.GetComponent<CharacterRig>().SetAppearance(LoadAppearance(row));
        Object.DestroyImmediate(playerGo);AssetDatabase.SaveAssets();
    }
    [MenuItem("Doodle Idle/Character Rigs/Rebuild Prefabs Only")]
    public static void RebuildPrefabs()
    {
        var report=JsonUtility.FromJson<Report>(File.ReadAllText(Output+"Reports/build_report.json"));report.errors.Clear();report.prefabs=0;
        try
        {
            var rows=JsonUtility.FromJson<Rows>("{\"items\":"+File.ReadAllText(Art+"PSB/layer_manifest.json")+"}").items;
            BuildPrefabs(rows,report);
            File.WriteAllText("Library/CharacterRig.complete","SUCCESS");Debug.Log("CHARACTER_RIG_BUILD_SUCCESS");
        }
        catch(Exception e){report.errors.Add(e.ToString());Debug.LogException(e);}
        finally{WriteReport(Output+"Reports/build_report.json",JsonUtility.ToJson(report,true));AssetDatabase.Refresh();}
    }
    [MenuItem("Doodle Idle/Character Rigs/Build All")]
    public static void Build()
    {
        var report=new Report();Directory.CreateDirectory(Output+"Reports");
        try
        {
            // Replace incomplete two-line metadata files with a fresh writable file,
            // preserving Unity's newly assigned GUID and archiving the original.
            AssetDatabase.ReleaseCachedFileHandles();var repairDir="Library/CharacterRigIncompleteMeta";Directory.CreateDirectory(repairDir);
            foreach(var meta in Directory.GetFiles(Art,"*.meta",SearchOption.AllDirectories).Where(p=>new FileInfo(p).Length<70&&(p.EndsWith(".psb.meta")||p.EndsWith(".png.meta"))))
            {
                var bytes=File.ReadAllBytes(meta);File.Move(meta,repairDir+"/"+Guid.NewGuid().ToString("N")+".meta");File.WriteAllBytes(meta,bytes);File.SetAttributes(meta,FileAttributes.Normal);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);ConfigureWeapons();
            var rows=JsonUtility.FromJson<Rows>("{\"items\":"+File.ReadAllText(Art+"PSB/layer_manifest.json")+"}").items;
            var defs=rows.Select(r=>r.type).Distinct().ToDictionary(t=>t,Define);var appearances=new List<(Row,CharacterAppearance)>();
            foreach(var row in rows) appearances.Add((row,Rig(row,defs[row.type],report)));
            AssetDatabase.SaveAssets();
            BuildPrefabs(rows,report);
            if(report.characters!=rows.Length||report.prefabs!=6)throw new Exception("Unexpected output totals");
            report.checks.Add(rows.Length+" characters; per-sprite Unity alpha outline and triangulation; normalized weights; six prefabs; five animation clips each");
            File.WriteAllText("Library/CharacterRig.complete","SUCCESS");Debug.Log("CHARACTER_RIG_BUILD_SUCCESS");
        }
        catch(Exception e){report.errors.Add(e.ToString());File.WriteAllText("Library/CharacterRig.failed",e.ToString());Debug.LogException(e);}
        finally{WriteReport(Output+"Reports/build_report.json",JsonUtility.ToJson(report,true));AssetDatabase.Refresh();}
    }
}
