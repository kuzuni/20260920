using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DoodleIdle.CharacterRigs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using Object=UnityEngine.Object;

public static class CharacterRigVerifier
{
    const string Root="Assets/DoodleIdle/CharacterRigs/";
    [Serializable] class Catalog { public CharacterRigBuilder.Row[] items; }
    [Serializable] class Report { public int appearances,parts,swaps,animationSamples,nativeSkinChecks,animatorStates;public List<string> errors=new();public List<string> meshes=new();public List<string> previews=new(); }
    [MenuItem("Doodle Idle/Character Rigs/Verify and Render")]
    public static void Run()
    {
        var report=new Report();Directory.CreateDirectory(Root+"Previews");var previews=new List<GameObject>();
        try
        {
            var assets=AssetDatabase.FindAssets("t:CharacterAppearance",new[]{Root+"Appearances"}).Select(g=>AssetDatabase.LoadAssetAtPath<CharacterAppearance>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(a=>a.characterId).ToArray();
            foreach(var asset in assets)
            {
                foreach(var part in asset.parts)
                {
                    var sprite=part.sprite;var v=sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();var weights=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();var indices=sprite.GetIndices().ToArray();var bones=sprite.GetBones();
                    if(v.Length<=4||indices.Length<3||weights.Length!=v.Length||bones.Length!=part.boneNames.Length)throw new Exception("Invalid mesh "+asset.name+"/"+part.name);
                    for(int i=0;i<v.Length;i++){var w=weights[i];if(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>1e-4f)throw new Exception("Invalid weight");if(!float.IsFinite(v[i].x)||!float.IsFinite(v[i].y))throw new Exception("Nonfinite vertex");}
                    for(int i=0;i<indices.Length;i++)if(indices[i]>=v.Length)throw new Exception("Invalid triangle index");
                    report.parts++;report.meshes.Add(asset.characterId+"/"+part.name+": "+v.Length+" vertices, "+indices.Length/3+" triangles");
                }
                report.appearances++;
            }
            foreach(var path in Directory.GetFiles(Root+"Prefabs","*.prefab").OrderBy(p=>p))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));var go=Object.Instantiate(prefab);previews.Add(go);go.hideFlags=HideFlags.HideAndDontSave;var rig=go.GetComponent<CharacterRig>();
                foreach(var skin in go.GetComponentsInChildren<SpriteSkin>())skin.forceCpuDeformation=true;
                foreach(var asset in assets.Where(a=>a.rigType==rig.rigType))
                {
                    rig.SetAppearance(asset);VerifyNativeSkin(go,report);report.swaps++;
                    bool player=AssetDatabase.GetAssetPath(asset).Contains("/Player/");
                    if(player==(prefab.name=="Player_Standard"))
                    {
                        string dir="Library/CharacterRigPreview/"+(player?"Player":rig.rigType);Directory.CreateDirectory(dir);Render(go,dir+"/"+asset.characterId+".png");
                    }
                }
                rig.SetAppearance(prefab.GetComponent<CharacterRig>().appearance);
                var clips=rig.animator.runtimeAnimatorController.animationClips;
                if(clips.Length!=5||clips.Any(c=>AnimationUtility.GetCurveBindings(c).Length==0))throw new Exception("Missing animation clips: "+path);
                foreach(var clip in clips)for(int frame=0;frame<5;frame++){clip.SampleAnimation(go,clip.length*frame/4);VerifyNativeSkin(go,report);var baked=Bake(go);if(baked.GetComponentsInChildren<MeshFilter>().Length<rig.partRenderers.Length)throw new Exception("Missing rendered part");DestroyBake(baked);report.animationSamples++;}
                var idle=clips.Single(c=>c.name=="Idle");idle.SampleAnimation(go,0);
                var imagePath=Root+"Previews/"+prefab.name+".png";Render(go,imagePath);report.previews.Add(imagePath);
                var attack=clips.Single(c=>c.name=="Attack");attack.SampleAnimation(go,attack.length*.5f);Render(go,Root+"Previews/"+prefab.name+"_attack.png");
                rig.animator.Rebind();rig.animator.Update(0);
                void State(string expected){int layer=expected=="Attack"?Mathf.Max(0,rig.animator.GetLayerIndex("Upper Body")):0;for(int i=0;i<30;i++){rig.animator.Update(.02f);if(rig.animator.GetCurrentAnimatorStateInfo(layer).IsName(expected)){report.animatorStates++;return;}}throw new Exception("Animator transition failed: "+prefab.name+"/"+expected);}
                State("Idle");rig.SetMoving(true);State("Move");rig.SetMoving(false);State("Idle");rig.Attack();State("Attack");rig.Hit();State("Hit");rig.Die();State("Death");
                Object.DestroyImmediate(go);previews.Remove(go);
            }
            var catalog=JsonUtility.FromJson<Catalog>("{\"items\":"+File.ReadAllText("Assets/DoodleIdle/Art/CharacterSprites/PSB/layer_manifest.json")+"}").items;
            if(report.appearances!=catalog.Length)throw new Exception("Wrong appearance count");
            var expected=catalog.Select(r=>r.source.Split('/')[0]+"/"+r.type+"/"+Path.GetFileNameWithoutExtension(r.source)+".asset").OrderBy(p=>p).ToArray();
            var actual=assets.Select(a=>AssetDatabase.GetAssetPath(a).Substring((Root+"Appearances/").Length)).OrderBy(p=>p).ToArray();
            if(!expected.SequenceEqual(actual))throw new Exception("Appearance catalog mismatch");
            File.WriteAllText("Library/CharacterRig.verified","SUCCESS");Debug.Log("CHARACTER_RIG_VERIFY_SUCCESS");
        }
        catch(Exception e){report.errors.Add(e.ToString());Debug.LogException(e);}
        finally {foreach(var go in previews)if(go)Object.DestroyImmediate(go);CharacterRigBuilder.WriteReport(Root+"Reports/verification_report.json",JsonUtility.ToJson(report,true));AssetDatabase.Refresh();}
    }
    static void VerifyNativeSkin(GameObject go,Report report)
    {
        var type=typeof(SpriteSkin).Assembly.GetType("UnityEngine.U2D.Animation.DeformationManager",true);
        var manager=type.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        type.GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(manager,null);
        foreach(var skin in go.GetComponentsInChildren<SpriteSkin>())
        {
            if(!skin.HasCurrentDeformedVertices())throw new Exception("SpriteSkin did not deform: "+skin.name);
            var renderer=skin.GetComponent<SpriteRenderer>();var sprite=renderer.sprite;
            var vertices=skin.GetDeformedVertexPositionData().ToArray();var source=sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();var weights=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();var binds=sprite.GetBindPoses().ToArray();
            if(vertices.Length!=source.Length)throw new Exception("Deformed vertex count mismatch");
            for(int i=0;i<source.Length;i++)
            {
                var w=weights[i];Vector3 expected=Vector3.zero;
                void Add(int index,float weight){if(weight>0)expected+=skin.boneTransforms[index].localToWorldMatrix.MultiplyPoint3x4(binds[index].MultiplyPoint3x4(source[i]))*weight;}
                Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                if(Vector3.Distance(renderer.transform.TransformPoint(vertices[i]),expected)>.002f)throw new Exception("SpriteSkin deformation mismatch: "+skin.name);
            }
            report.nativeSkinChecks++;
        }
    }
    public static Bounds PreviewBounds(GameObject go)
    {
        var baked = Bake(go);
        try { var rs = baked.GetComponentsInChildren<MeshRenderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        finally { DestroyBake(baked); }
    }
    public static void RenderPortrait(GameObject source, string path, Vector3 center, float radius)
    {
        var baked = Bake(source); var preview = new PreviewRenderUtility();
        try {
            preview.AddSingleGO(baked); preview.camera.orthographic = true; preview.camera.orthographicSize = radius;
            preview.camera.transform.position = new Vector3(center.x, center.y, -30); preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 100; preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = Color.clear;
            preview.BeginPreview(new Rect(0, 0, 256, 256), GUIStyle.none); preview.camera.Render(); var texture = preview.EndPreview();
            var rt = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32); Graphics.Blit(texture, rt); var previous = RenderTexture.active; RenderTexture.active = rt;
            var png = new Texture2D(256, 256, TextureFormat.RGBA32, false); png.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); png.Apply(); CharacterRigBuilder.WriteArtifact(path, png.EncodeToPNG()); Object.DestroyImmediate(png); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
        } finally { DestroyBake(baked); preview.Cleanup(); }
    }
    static GameObject Bake(GameObject go)
    {
        var output=new GameObject("BakedRigPreview");output.hideFlags=HideFlags.HideAndDontSave;
        foreach(var renderer in go.GetComponentsInChildren<SpriteRenderer>())
        {
            var sprite=renderer.sprite;if(!sprite||!renderer.enabled)continue;
            var source=sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray();var uv=sprite.GetVertexAttribute<Vector2>(VertexAttribute.TexCoord0).ToArray();var ind=sprite.GetIndices().ToArray();var vertices=new Vector3[source.Length];var uvs=new Vector2[source.Length];
            var skin=renderer.GetComponent<SpriteSkin>();
            if(skin)
            {
                var binds=sprite.GetBindPoses().ToArray();var weights=sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray();
                for(int i=0;i<source.Length;i++)
                {
                    var w=weights[i];Vector3 result=Vector3.zero;
                    void Add(int index,float weight){if(weight>0)result+=skin.boneTransforms[index].localToWorldMatrix.MultiplyPoint3x4(binds[index].MultiplyPoint3x4(source[i]))*weight;}
                    Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);vertices[i]=result;
                }
            }
            else for(int i=0;i<source.Length;i++)vertices[i]=renderer.transform.TransformPoint(source[i]);
            for(int i=0;i<uvs.Length;i++)uvs[i]=uv[i];var triangles=new int[ind.Length];for(int i=0;i<ind.Length;i++)triangles[i]=ind[i];
            var mesh=new Mesh{name=renderer.name};mesh.vertices=vertices;mesh.uv=uvs;mesh.triangles=triangles;mesh.RecalculateBounds();
            var child=new GameObject(renderer.name);child.transform.SetParent(output.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mr=child.AddComponent<MeshRenderer>();mr.sharedMaterial=new Material(Shader.Find("Sprites/Default")){mainTexture=sprite.texture};mr.sortingOrder=renderer.sortingOrder;
        }
        return output;
    }
    static void DestroyBake(GameObject go)
    {
        foreach(var f in go.GetComponentsInChildren<MeshFilter>())Object.DestroyImmediate(f.sharedMesh);
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>())Object.DestroyImmediate(r.sharedMaterial);
        Object.DestroyImmediate(go);
    }
    static void Render(GameObject source,string path)
    {
        var baked=Bake(source);var preview=new PreviewRenderUtility();
        try
        {
            var renderers=baked.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            preview.AddSingleGO(baked);preview.camera.orthographic=true;preview.camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x)*1.15f;
            preview.camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-30);preview.camera.transform.rotation=Quaternion.identity;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.86f,.89f,.91f,1);
            preview.BeginPreview(new Rect(0,0,720,720),GUIStyle.none);preview.camera.Render();var texture=preview.EndPreview();
            var rt=RenderTexture.GetTemporary(720,720,0,RenderTextureFormat.ARGB32);Graphics.Blit(texture,rt);var previous=RenderTexture.active;RenderTexture.active=rt;
            var png=new Texture2D(720,720,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,720,720),0,0);png.Apply();CharacterRigBuilder.WriteArtifact(path,png.EncodeToPNG());Object.DestroyImmediate(png);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
        }
        finally {DestroyBake(baked);preview.Cleanup();}
    }
}
