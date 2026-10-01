using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
[InitializeOnLoad]
public static class RemainingPerformanceRun
{
    static RemainingPerformanceRun(){McpUnity.Unity.McpUnitySettings.Instance.AutoStartServer=false;McpUnity.Unity.McpUnityServer.Instance.StopServer();EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="Library/RemainingPerformance.command";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(path))return;
        var command=File.ReadAllText(path).Trim();File.Delete(path);
        try{
            if(command=="build-report"){
                var report=UnityEditor.Build.Reporting.BuildReport.GetLatestReport();
                using(var log=new StreamWriter("artifacts/performance/build-diagnostics.txt")){
                    log.WriteLine(report.summary.result+" "+report.summary.totalErrors+" reported errors");
                    foreach(var step in report.steps)foreach(var message in step.messages)
                        if(message.type==LogType.Error||message.type==LogType.Exception)log.WriteLine(step.name+": "+message.content);
                }return;
            }
            if(command=="build"){
                Directory.CreateDirectory("Builds/Performance");
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/DoodleIdle/DoodleLogin.unity","Assets/DoodleIdle/DoodleIdle.unity"},locationPathName="Builds/Performance/DoodlePerformance.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                File.WriteAllText("Library/RemainingPerformance.build",build.summary.result+" "+build.summary.totalErrors+" errors");return;
            }
            bool editMode=command.StartsWith("edit|");if(editMode)command=command.Substring(5);
            var api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());
            string[] tests=command=="benchmark"?new[]{"DoodleIdle.Tests.DoodleIdlePlayModeTests.MainCombatPerformanceSample"}:File.ReadAllLines(command);
            api.Execute(new ExecutionSettings(new Filter{testMode=editMode?TestMode.EditMode:TestMode.PlayMode,testNames=tests}));
        }catch(Exception e){File.WriteAllText("Library/RemainingPerformance.failed",e.ToString());}
    }
    sealed class Results:ICallbacks {
        public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){File.WriteAllText("Library/RemainingPerformance.progress",t.FullName);}
        public void TestFinished(ITestResultAdaptor t){if(t.ResultState!="Passed")File.AppendAllText("Library/RemainingPerformance.failures",t.FullName+"\n"+t.Message+"\n");}
        public void RunFinished(ITestResultAdaptor r){TestRunnerApi.SaveResultToFile(r,"artifacts/character-reports/remaining-performance-tests.xml");File.WriteAllText("Library/RemainingPerformance.tests",r.ResultState+" "+r.PassCount+" passed; "+r.FailCount+" failed");}
    }
}
