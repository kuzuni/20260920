using System;
using System.IO;
using System.Linq;
using DoodleIdle;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
[InitializeOnLoad]
public static class EyeHighlightRun
{
    static EyeHighlightRun(){EditorApplication.update+=Check;}
    static void Check()
    {
        const string path="Library/EyeHighlight.command";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(path))return;
        string task=File.ReadAllText(path).Trim();File.Delete(path);
        try
        {
            if(task=="build")
            {
                CharacterFaceSetup.ApplySeparateHighlights();CharacterFaceSetup.RenderPortraits();CharacterFaceSetup.RenderExpressionSamples();Preview();
                File.WriteAllText("Library/EyeHighlight.complete","SUCCESS");
            }
            if(task=="test")
            {
                var api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Result());
                api.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"DoodleIdle.Tests.DoodleCharacterFaceTests","DoodleIdle.Tests.DoodleRigPresentationTests.AllAppearancesPreservePrefabPartAndGroupSorting"}}));
            }
        }catch(Exception e){File.WriteAllText("Library/EyeHighlight.failed",e.ToString());Debug.LogException(e);}
    }
    static void Preview()
    {
        var entry=DoodleCharacterCatalog.Current.entries.Single(e=>e.id=="00_default");var rig=UnityEngine.Object.Instantiate(entry.prefab);
        try
        {
            rig.SetAppearance(entry.appearance);rig.animator.enabled=false;
            rig.animator.runtimeAnimatorController.animationClips.First(c=>c.name=="Idle").SampleAnimation(rig.gameObject,0);rig.face.SyncSorting();
            Directory.CreateDirectory("artifacts/eye-highlight");
            var poses=new[]{Vector2.left,Vector2.up,Vector2.right,new Vector2(1,1).normalized};
            for(int i=0;i<poses.Length;i++)
            {
                foreach(var eye in new[]{rig.face.leftEye,rig.face.rightEye})eye.pupilMotion.localPosition=Vector2.Scale(poses[i],eye.travel);
                rig.face.RefreshHighlights();
                CharacterRigVerifier.RenderPortrait(rig.gameObject,"artifacts/eye-highlight/"+i+".png",entry.center,entry.extent*.55f);
            }
        }finally{UnityEngine.Object.DestroyImmediate(rig.gameObject);}
    }
    sealed class Result:ICallbacks
    {
        public void RunStarted(ITestAdaptor t){} public void TestStarted(ITestAdaptor t){} public void TestFinished(ITestResultAdaptor r){}
        public void RunFinished(ITestResultAdaptor r){TestRunnerApi.SaveResultToFile(r,"artifacts/character-reports/eye-highlight-playmode.xml");File.WriteAllText("Library/EyeHighlight.tests",r.ResultState+" "+r.PassCount+" passed; "+r.FailCount+" failed");}
    }
}
