#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Threading.Tasks;
using BACKND.Database;
using BackEnd;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace DoodleIdle.Tests
{
    public sealed class DoodlePvpDatabaseTests
    {
        [UnityTest]
        public IEnumerator DatabaseAllowsOpponentReadDeniesOpponentWriteAndSavesResultOnce()
        {
            if(!File.Exists("Library/PvpLive.optin"))Assert.Ignore("Live database test must be explicitly enabled.");
            Assert.That(DoodleBackendSession.Instance,Is.Null);
            var task=Run();float deadline=Time.realtimeSinceStartup+240;
            while(!task.IsCompleted && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(task.IsCompleted,Is.True,"Live PVP test timeout");
            if(task.IsFaulted)throw task.Exception.InnerException;
        }
        static async Task Run()
        {
            var session=DoodleBackendSession.Get();
            Check(await DoodleBackendSession.Request(cb=>Backend.InitializeAsync(new BackendCustomSetting {
                clientAppID=session.Config.clientAppId,signatureKey=session.Config.signatureKey,
                isSendLogReport=false,useAsyncPoll=false,timeOutSec=20
            },cb)),"initialize test SDK");
            var idA="qa_pvp_"+Guid.NewGuid().ToString("N").Substring(0,16);
            var idB="qa_pvp_"+Guid.NewGuid().ToString("N").Substring(0,16);
            var password=Guid.NewGuid().ToString("N")+"aA1!";
            string accountA=null,accountB=null;
            string qaName="PvpQA"+idA.Substring(idA.Length-6);
            try {
                await Login(session,idA,password,true);accountA=session.AccountId;
                var ui=UnityEngine.Object.FindFirstObjectByType<DoodleUi>();
                var snapshot=ui.CapturePvpLoadout();
                var pending=new DoodlePvpPending{account=accountA,matchId=Guid.NewGuid().ToString("N"),startScore=0,opponentScore=0,
                    finished=true,won=false,delta=-3,opponentName="QA",payload=DoodlePvpPayload.Pack(snapshot),
                    summary=new DoodlePvpSummary{name=qaName,power="128",lookData=DoodlePvpSummary.EncodeLook(DoodlePlayerLook.From(ui)),day=DateTime.UtcNow.ToString("yyyy-MM-dd"),used=1}};
                session.JournalPvp(pending);
                await session.FinishPvp(pending,ui);await session.FinishPvp(pending,ui);
                var saved=await session.ReadMyPvp();
                Assert.That(saved.Score,Is.EqualTo(-3),"Retry must not charge the score twice.");
                Assert.That(saved.Payload.Unpack().collections,Is.EqualTo(snapshot.collections));
                Assert.That(saved.Summary.lastMatch,Is.EqualTo(pending.matchId));
                Assert.That(session.PendingPvp(),Is.Null);
                Assert.That(await session.PvpOwnRank(),Is.GreaterThan(0));
                var cloud=await DoodleBackendSession.Request(cb=>Backend.GameData.GetMyData(session.Config.profileTable,new Where(),1,cb));
                Check(cloud,"read cloud save after match");
                Assert.That(cloud.FlattenRows()[0]["save"].ToString(),Does.Contain("DoodleUi.Services.v1"));
                Assert.That(await session.LeaveAccount(false),Is.True);await Task.Delay(100);
                await Login(session,idB,password,true);accountB=session.AccountId;
                var candidates=await session.PvpCandidates(0);
                Assert.That(candidates.Any(x=>x.Account==accountA),Is.True,"Score-index query must find registered opponents.");
                var other=await session.ReadPvpOpponent(accountA);
                Assert.That(other.Score,Is.EqualTo(-3));
                using(var db=new Client(session.Config.pvpDatabaseUuid)){
                    await db.Initialize();
                    bool denied=false;
                    try {
                        var attempt=await db.From<DoodlePvpProfile>().Where(x=>x.Account==accountA).Set(x=>x.Score,999999).Update();
                        denied=attempt.AffectedRows==0;
                    }catch(Exception error) when(error.Message.Contains("permission denied")){denied=true;}
                    Assert.That(denied,Is.True,"Server must deny another owner's update.");
                }
                Assert.That((await session.ReadPvpOpponent(accountA)).Score,Is.EqualTo(-3));
                Assert.That(await session.ReadMyPvp(),Is.Null,"Opponent reads must not create own snapshot.");
                // Use the actual candidate popup and challenge button for the second account.
                ui=UnityEngine.Object.FindFirstObjectByType<DoodleUi>();
                var levels=(Dictionary<string,CodeStage.AntiCheat.ObscuredTypes.ObscuredInt>)typeof(DoodleUi).GetField("statLevels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
                levels["attack"]=1000;ui.Save();
                ui.ShowPage("Pvp");
                var deadline=DateTime.UtcNow.AddSeconds(30);Button openChallenge=null;
                while(DateTime.UtcNow<deadline){openChallenge=ui.Canvas.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.name=="도전");if(openChallenge)break;await Task.Delay(50);}
                Assert.That(openChallenge,Is.Not.Null,"Ranking popup must load successfully.");openChallenge.onClick.Invoke();
                deadline=DateTime.UtcNow.AddSeconds(30);Button challenge=null;
                while(DateTime.UtcNow<deadline){
                    challenge=ui.Canvas.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.name==qaName+" · -3점");
                    if(challenge)break;await Task.Delay(50);
                }
                Assert.That(challenge,Is.Not.Null,"Actual server opponent must appear in challenge popup.");
                challenge.onClick.Invoke();
                var game=UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
                deadline=DateTime.UtcNow.AddSeconds(30);
                while(!game.PvpSessionActive&&DateTime.UtcNow<deadline)await Task.Delay(25);
                Assert.That(game.PvpSessionActive,Is.True,"Challenge must start real combat.");
                Assert.That(ui.ActivePage,Is.Null.Or.Empty);
                Assert.That(ui.Canvas.enabled,Is.False);
                deadline=DateTime.UtcNow.AddSeconds(110);
                while((game.PvpSessionActive||session.PendingPvp()!=null)&&DateTime.UtcNow<deadline)await Task.Delay(50);
                Assert.That(game.PvpSessionActive,Is.False,"Real combat must finish.");
                Assert.That(session.PendingPvp(),Is.Null,"Battle result must reach the server.");
                var result=await session.ReadMyPvp();Assert.That(result.Score,Is.InRange(1,5),"Stronger challenger must win this real match.");
                Assert.That(result.Payload.Unpack().collections,Does.Contain("1000"));
                Assert.That(ui.Canvas.enabled,Is.True,"Main HUD must return after battle.");
                Assert.That(await session.PvpOwnRank(),Is.GreaterThan(0));
                using(var db=new Client(session.Config.pvpDatabaseUuid)){await db.Initialize();await db.From<DoodlePvpProfile>().OfCurrentUser().Delete();}
                Assert.That(await session.LeaveAccount(true),Is.True);accountB=null;await Task.Delay(100);
                await Login(session,idA,password,false);
                using(var db=new Client(session.Config.pvpDatabaseUuid)){await db.Initialize();await db.From<DoodlePvpProfile>().OfCurrentUser().Delete();}
                Assert.That(await session.LeaveAccount(true),Is.True);accountA=null;
            }finally {
                // Retry cleanup for both temporary accounts even if an assertion failed while B was signed in.
                foreach(var entry in new[]{(id:idA,account:accountA),(id:idB,account:accountB)}){
                    if(entry.account==null)continue;
                    try {
                        var login=await DoodleBackendSession.Request(cb=>Backend.BMember.CustomLogin(entry.id,password,cb));
                        if(!login.IsSuccess()||Backend.UserInDate!=entry.account)continue;
                        using(var db=new Client(session.Config.pvpDatabaseUuid)){await db.Initialize();await db.From<DoodlePvpProfile>().OfCurrentUser().Delete();}
                        await DoodleBackendSession.Request(cb=>Backend.BMember.WithdrawAccount(0,cb));
                    }catch(Exception error){Debug.LogWarning("PVP QA account cleanup: "+error.GetType().Name);}
                }
                DoodlePrefs.UseAccount(null);
                if(session)UnityEngine.Object.Destroy(session.gameObject);
            }
        }
        static async Task Login(DoodleBackendSession session,string id,string password,bool signup)
        {
            session.EditorLogin(id,password,signup);
            var deadline=DateTime.UtcNow.AddSeconds(90);
            while(session.Busy&&DateTime.UtcNow<deadline)await Task.Delay(50);
            Assert.That(session.Ready,Is.True,session.Status);
            deadline=DateTime.UtcNow.AddSeconds(60);
            while(DateTime.UtcNow<deadline){var game=UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();if(game&&game.Ready)return;await Task.Delay(50);}
            Assert.Fail("Game did not become ready.");
        }
        static void Check(BackendReturnObject result,string step)=>Assert.That(result.IsSuccess(),Is.True,step+": "+result.GetStatusCode()+" "+result.GetErrorCode());
    }
}
#endif
