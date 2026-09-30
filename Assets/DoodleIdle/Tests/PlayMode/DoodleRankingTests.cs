#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BackEnd;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public sealed class DoodleRankingTests
    {
        [Serializable] sealed class Accounts {public string first,second,password;}
        const string Journal="Library/RankingQA.json";
        [UnityTest] public IEnumerator SeedRankingFields() => Run(Seed);
        [UnityTest] public IEnumerator AllRankingTabsShowServerScoresAndEquippedLooks() => Run(Verify);
        static IEnumerator Run(Func<Task> action)
        {
            if(!File.Exists("Library/RankingLive.optin"))Assert.Ignore("Live ranking verification is opt-in.");
            DG.Tweening.DOTween.Init();
            var errors=new System.Collections.Generic.List<string>();
            Application.LogCallback collect=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
            Application.logMessageReceived+=collect;LogAssert.ignoreFailingMessages=true;
            try{var task=action();float deadline=Time.realtimeSinceStartup+300;
                while(!task.IsCompleted && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(task.IsCompleted,Is.True);if(task.IsFaulted)throw task.Exception.InnerException;
                Assert.That(errors,Is.Empty,string.Join("\n",errors));
            }finally{Application.logMessageReceived-=collect;LogAssert.ignoreFailingMessages=false;}
        }
        static async Task<DoodleUi> Login(DoodleBackendSession session,string id,string password,bool create)
        {
            session.EditorLogin(id,password,create);var deadline=DateTime.UtcNow.AddSeconds(90);
            while(session.Busy && DateTime.UtcNow<deadline)await Task.Delay(50);
            Assert.That(session.Ready,Is.True,session.Status);
            while(DateTime.UtcNow<deadline){var game=UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();if(game && game.Ready)return game.Ui;await Task.Delay(50);}
            throw new TimeoutException("Game initialization");
        }
        static async Task Seed()
        {
            Assert.That(File.Exists(Journal),Is.False,"Preserve any pending QA accounts.");
            var accounts=new Accounts{first="qa_rank_"+Guid.NewGuid().ToString("N").Substring(0,14),password=Guid.NewGuid().ToString("N")+"aA1!"};
            File.WriteAllText(Journal,JsonUtility.ToJson(accounts));
            var session=DoodleBackendSession.Get();var ui=await Login(session,accounts.first,accounts.password,true);
            PvpLoadoutAudit.Equip(ui,0,0);ui.Save();session.SetStage(42);
            Assert.That(await session.PublishStage(),Is.True,"Seed numeric score and compact appearance metadata.");
            Assert.That(await session.SaveCloud(),Is.True);
            Assert.That(await session.LeaveAccount(false),Is.True);
        }
        static async Task Verify()
        {
            if(!File.Exists(Journal))await Seed();
            var accounts=JsonUtility.FromJson<Accounts>(File.ReadAllText(Journal));var session=DoodleBackendSession.Get();
            try{
                var ui=await Login(session,accounts.first,accounts.password,false);
                PvpLoadoutAudit.Equip(ui,0,0);ui.Save();session.SetStage(42);
                var firstLook=DoodlePlayerLook.From(ui);var firstPower=ui.PowerAmount;
                Assert.That(await session.PublishStage(),Is.True);Assert.That(await session.PublishPower(),Is.True);
                string firstAccount=session.AccountId;
                // Register a real PVP snapshot without changing an existing player's data.
                var pending=new DoodlePvpPending{account=firstAccount,matchId=Guid.NewGuid().ToString("N"),finished=true,draw=true,
                    payload=DoodlePvpPayload.Pack(ui.CapturePvpLoadout()),summary=new DoodlePvpSummary{name=ui.PlayerName,power=firstPower.ToString(),lookData=DoodlePvpSummary.EncodeLook(firstLook),day=DateTime.UtcNow.ToString("yyyy-MM-dd"),used=1}};
                session.JournalPvp(pending);await session.FinishPvp(pending,ui);
                Assert.That(await session.LeaveAccount(false),Is.True);
                accounts.second="qa_rank_"+Guid.NewGuid().ToString("N").Substring(0,14);File.WriteAllText(Journal,JsonUtility.ToJson(accounts));
                ui=await Login(session,accounts.second,accounts.password,true);PvpLoadoutAudit.Equip(ui,1,1);ui.Save();session.SetStage(43);
                Assert.That(await session.PublishStage(),Is.True);Assert.That(await session.PublishPower(),Is.True);
                var activities=ui.Canvas.GetComponentsInChildren<Transform>().Single(x=>x.name=="Activities");
                Assert.That(activities.GetChild(0).name,Is.EqualTo("Ranking"));Assert.That(activities.GetChild(1).name,Is.EqualTo("Attendance"));
                await Capture("rankings-main.png");
                ui.OpenRankings();
                for(int tab=0;tab<3;tab++){
                    if(tab>0)ui.Canvas.GetComponentsInChildren<Button>().Single(x=>x.name=="Ranking tab "+tab).onClick.Invoke();
                    var status=ui.Canvas.GetComponentsInChildren<Text>().Single(x=>x.name=="Ranking status");
                    var deadline=DateTime.UtcNow.AddSeconds(60);
                    while(status.text.Contains("불러오는")||status.text.Contains("Loading")){Assert.That(DateTime.UtcNow<deadline,Is.True);await Task.Delay(50);}
                    Assert.That(status.text.Contains("상위 100")||status.text.Contains("Top 100"),Is.True,status.text);
                    var portraits=ui.Canvas.GetComponentsInChildren<DoodleRankingPortrait>();
                    Assert.That(portraits.Any(x=>x.Look.appearanceIcon==firstLook.appearanceIcon && x.Look.weaponIcon==firstLook.weaponIcon),Is.True,"Every tab must restore opponent appearance and weapon.");
                    if(tab==2)Assert.That(ui.Canvas.GetComponentsInChildren<Text>().Any(x=>x.text==UiNumber.Format(firstPower)),Is.True,"Show raw power, never logarithmic sorting key.");
                    await Task.Delay(500);await Capture("rankings-tab-"+tab+".png");
                }
                ui.CloseDetail();
                // Rapid switch/close must not let old asynchronous results populate another tab.
                ui.OpenRankings();ui.Canvas.GetComponentsInChildren<Button>().Single(x=>x.name=="Ranking tab 2").onClick.Invoke();ui.CloseDetail();await Task.Delay(250);
            }finally{
                foreach(var id in new[]{accounts.second,accounts.first}){
                    if(string.IsNullOrEmpty(id))continue;
                    if(session.Ready)await session.LeaveAccount(false);
                    await Login(session,id,accounts.password,false);
                    // Remove only this test account's PVP row, then withdraw its account.
                    var method=typeof(DoodleBackendSession).GetMethod("ConnectPvp",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                    var db=await (Task<BACKND.Database.Client>)method.Invoke(session,null);
                    string account=session.AccountId;await db.From<DoodlePvpProfile>().OfCurrentUser().Where(x=>x.Account==account).Delete();
                    await db.From<DoodleRankProfile>().OfCurrentUser().Where(x=>x.Account==account).Delete();
                    Assert.That(await session.LeaveAccount(true),Is.True);
                }
                File.Delete(Journal);
            }
        }
        static async Task Capture(string name)
        {
            Directory.CreateDirectory("artifacts/screenshots/rankings");
            ScreenCapture.CaptureScreenshot("artifacts/screenshots/rankings/"+name);await Task.Delay(150);
        }
    }
}
#endif
