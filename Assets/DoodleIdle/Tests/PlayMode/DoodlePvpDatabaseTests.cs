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
        public IEnumerator DatabaseAllowsOpponentReadDeniesOpponentWriteAndSavesResultOnce() => RunLive(false);
        [UnityTest]
        public IEnumerator PvpOpponentSkinsAndCompanionsRenderWithoutEquippedSkills() => RunLive(true);
        static IEnumerator RunLive(bool companionsOnly)
        {
            if(!File.Exists("Library/PvpLive.optin"))Assert.Ignore("Live database test must be explicitly enabled.");
            Assert.That(DoodleBackendSession.Instance,Is.Null);
            // Defer log assertions until async cleanup finishes; never abandon created accounts
            // just because the runner observes a scene transition error in an earlier frame.
            var errors=new List<string>();
            Application.LogCallback collect=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
            Application.logMessageReceived+=collect;LogAssert.ignoreFailingMessages=true;
            try {
                DG.Tweening.DOTween.Init();
                var task=Run(companionsOnly);float deadline=Time.realtimeSinceStartup+300;
                while(!task.IsCompleted && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(task.IsCompleted,Is.True,"Live PVP test timeout");
                if(task.IsFaulted)throw task.Exception.InnerException;
                Assert.That(errors,Is.Empty,string.Join("\n",errors));
            }finally{Application.logMessageReceived-=collect;LogAssert.ignoreFailingMessages=false;}
        }
        static async Task Run(bool companionsOnly)
        {
            var session=DoodleBackendSession.Get();
            // Exercise the same initialization path as the actual login screen.
            var idA="qa_pvp_"+Guid.NewGuid().ToString("N").Substring(0,16);
            var idB="qa_pvp_"+Guid.NewGuid().ToString("N").Substring(0,16);
            var password=Guid.NewGuid().ToString("N")+"aA1!";
            string accountA=null,accountB=null;
            string qaName="PvpQA"+idA.Substring(idA.Length-6);
            try {
                await Login(session,idA,password,true);accountA=session.AccountId;
                var ui=UnityEngine.Object.FindFirstObjectByType<DoodleUi>();
                PvpLoadoutAudit.Equip(ui,0,companionsOnly?1:0);
                if(companionsOnly)foreach(var skill in ui.Items("Skill"))skill.equipped=false;
                ui.Save();
                var expectedA=PvpLoadoutAudit.Values(ui);
                var snapshot=ui.CapturePvpLoadout();snapshot.playerName=qaName;
                var pending=new DoodlePvpPending{account=accountA,matchId=Guid.NewGuid().ToString("N"),startScore=0,opponentScore=0,
                    finished=true,won=false,delta=-3,opponentName="QA",payload=DoodlePvpPayload.Pack(snapshot),
                    summary=new DoodlePvpSummary{name=qaName,power=ui.PowerAmount.ToString(),lookData=DoodlePvpSummary.EncodeLook(DoodlePlayerLook.From(ui)),day=DateTime.UtcNow.ToString("yyyy-MM-dd"),used=1}};
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
                PvpLoadoutAudit.Equip(ui,1,0);
                if(companionsOnly)foreach(var skill in ui.Items("Skill"))skill.equipped=false;
                ui.Save();
                var expectedB=PvpLoadoutAudit.Values(ui);var capturedB=ui.CapturePvpLoadout();
                var audit=new List<string>{"Live server PVP: full equipment, "+(companionsOnly?"zero":"eight")+" skills, five companions, relics, both skin categories.","A: ATK "+expectedA["AttackAmount"]+", HP "+expectedA["MaxHealthAmount"]+", regen "+expectedA["HealthRegenAmount"],"B: ATK "+expectedB["AttackAmount"]+", HP "+expectedB["MaxHealthAmount"]+", regen "+expectedB["HealthRegenAmount"]};
                await Task.Delay(3000); // Let the real skin-equipped toast finish before capturing the list.
                ui.ShowPage("Pvp");
                var deadline=DateTime.UtcNow.AddSeconds(30);Button openChallenge=null;
                while(DateTime.UtcNow<deadline){openChallenge=ui.Canvas.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.name=="도전");if(openChallenge)break;await Task.Delay(50);}
                Assert.That(openChallenge,Is.Not.Null,"Ranking popup must load successfully.");openChallenge.onClick.Invoke();
                deadline=DateTime.UtcNow.AddSeconds(30);Button challenge=null;
                while(DateTime.UtcNow<deadline){
                    challenge=ui.Canvas.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.name=="PvpOpponent:"+accountA);
                    if(challenge)break;await Task.Delay(50);
                }
                Assert.That(challenge,Is.Not.Null,"Actual server opponent must appear in challenge popup.");
                Assert.That(GameNumber.TryParse(other.Summary.power,out var shownPower),Is.True);
                var label=challenge.GetComponentInChildren<Text>().text;
                Assert.That(label,Does.Contain("승점 -3"));Assert.That(label,Does.Contain("전투력 "+UiNumber.Format(shownPower)));
                Assert.That(label,Does.Contain("승리 +"+DoodlePvpRules.Delta(0,-3,true)+"점"));
                await Task.Delay(350);Capture("01-opponents",companionsOnly);
                challenge.onClick.Invoke();
                var game=UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
                deadline=DateTime.UtcNow.AddSeconds(30);
                while(!game.PvpSessionActive&&DateTime.UtcNow<deadline)await Task.Delay(25);
                Assert.That(game.PvpSessionActive,Is.True,"Challenge must start real combat.");
                Assert.That(ui.ActivePage,Is.Null.Or.Empty);
                Assert.That(ui.Canvas.enabled,Is.False);
                deadline=DateTime.UtcNow.AddSeconds(30);
                DoodleIdleGame[] fighters;
                do {fighters=UnityEngine.Object.FindObjectsByType<DoodleIdleGame>(FindObjectsSortMode.None).Where(x=>x.IsPvpEngine).OrderBy(x=>x.transform.GetInstanceID()).ToArray();if(fighters.Length<2)await Task.Delay(25);}while(fighters.Length<2&&DateTime.UtcNow<deadline);
                Assert.That(fighters.Length,Is.EqualTo(2));
                var hits=new Dictionary<DoodleIdleGame,Dictionary<string,int>>();
                foreach(var fighter in fighters){
                    bool isA=fighter.Ui.PlayerName==qaName;
                    PvpLoadoutAudit.AssertValues(isA?expectedA:expectedB,fighter.Ui);
                    Assert.That(fighter.PlayerMaxHealthAmount,Is.EqualTo(fighter.Ui.MaxHealthAmount));
                    Assert.That(fighter.Ui.EquippedSkills.Count,Is.EqualTo(companionsOnly?0:8));Assert.That(fighter.ActiveCompanions,Is.EqualTo(5));
                    hits[fighter]=new Dictionary<string,int>();var counts=hits[fighter];
                    fighter.PvpDamageDealt+=(category,id,amount)=>{string key=category+"/"+id;counts[key]=counts.TryGetValue(key,out int n)?n+1:1;};
                    audit.Add((isA?"A":"B")+": "+(isA?expectedA:expectedB).Count+" saved and cached fields match actual combat model.");
                }
                deadline=DateTime.UtcNow.AddSeconds(15);
                while(!UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(x=>x.text=="3"&&x.GetComponentInParent<Canvas>().name=="PVP countdown")&&DateTime.UtcNow<deadline)await Task.Delay(25);
                await Task.Delay(150);Capture("02-countdown-loadouts",companionsOnly);
                await Task.Delay(4500);if(game.PvpSessionActive)Capture("02-combat",companionsOnly);
                deadline=DateTime.UtcNow.AddSeconds(110);
                bool verified=false;
                while((game.PvpSessionActive||session.PendingPvp()!=null)&&DateTime.UtcNow<deadline){
                    if(!verified&&fighters.All(x=>x&&x.Elapsed>=18)){
                        foreach(var fighter in fighters){
                            foreach(var skill in fighter.Ui.EquippedSkills){Assert.That(fighter.SkillActivationCount(skill.ability),Is.GreaterThan(0),skill.ability+" cast");Assert.That(hits[fighter].ContainsKey("Skill/"+skill.ability),Is.True,skill.ability+" actual hit");}
                            foreach(var companion in fighter.Ui.EquippedCompanions){Assert.That(fighter.CompanionShotCount(companion.id),Is.GreaterThan(0),companion.id+" shot");Assert.That(hits[fighter].ContainsKey("Companion/"+companion.id),Is.True,companion.id+" actual hit");}
                            audit.Add(fighter.Ui.PlayerName+" damage sources: "+string.Join(", ",hits[fighter].Select(x=>x.Key+"="+x.Value)));
                        }
                        if(companionsOnly)foreach(var fighter in fighters) {
                            Assert.That(fighter.Ui.Items("Skill").Sum(x=>fighter.SkillActivationCount(x.ability)),Is.Zero,"No equipped skills may cast in companion-only combat.");
                            Assert.That(hits[fighter].Keys.Any(x=>x.StartsWith("Skill/")),Is.False);
                        }
                        verified=true;Capture("03-geared-combat",companionsOnly);
                    }
                    await Task.Delay(50);
                }
                Assert.That(verified,Is.True,"Both full loadouts must be exercised before this match finishes.");
                Assert.That(game.PvpSessionActive,Is.False,"Real combat must finish.");
                Assert.That(session.PendingPvp(),Is.Null,"Battle result must reach the server.");
                var result=await session.ReadMyPvp();Assert.That(Math.Abs(result.Score),Is.EqualTo(3),"Actual outcome must use the displayed point delta.");
                Assert.That(result.Payload.Unpack().collections,Is.EqualTo(capturedB.collections));
                Assert.That(result.Payload.Unpack().skins,Is.EqualTo(capturedB.skins));
                audit.Add("Match completed with real health/regen (no endurance HP override), score "+result.Score+"; result snapshot matches challenger.");
                Directory.CreateDirectory("artifacts/character-reports");File.WriteAllLines(companionsOnly?"artifacts/character-reports/pvp-companions-live-audit.txt":"artifacts/character-reports/pvp-live-loadout-audit.txt",audit);
                await Task.Delay(350);Capture("04-result",companionsOnly);
                Assert.That(ui.Canvas.enabled,Is.True,"Main HUD must return after battle.");
                Assert.That(await session.PvpOwnRank(),Is.GreaterThan(0));
                using(var db=new Client(session.Config.pvpDatabaseUuid)){await db.Initialize();await db.From<DoodlePvpProfile>().OfCurrentUser().Delete();}
                Assert.That(await session.LeaveAccount(true),Is.True);accountB=null;await Task.Delay(100);
                await Login(session,idA,password,false);
                using(var db=new Client(session.Config.pvpDatabaseUuid)){await db.Initialize();await db.From<DoodlePvpProfile>().OfCurrentUser().Delete();}
                Assert.That(await session.LeaveAccount(true),Is.True);accountA=null;
                File.AppendAllText(companionsOnly?"artifacts/character-reports/pvp-companions-live-audit.txt":"artifacts/character-reports/pvp-live-loadout-audit.txt","Both temporary PVP rows deleted and account withdrawals accepted.\n");
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
        static void Capture(string name,bool companionsOnly)
        {
            var camera=Camera.main;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;
            var active=RenderTexture.active;var target=new RenderTexture(720,1520,24,RenderTextureFormat.ARGB32);
            var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x=>x.isRootCanvas&&x.enabled&&x.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var scales=canvases.Select(x=>x.scaleFactor).ToArray();
            var scalers=canvases.Select(x=>x.GetComponent<CanvasScaler>()).ToArray();
            var enabled=scalers.Select(x=>x&&x.enabled).ToArray();
            try {
                camera.targetTexture=target;camera.aspect=720f/1520;
                foreach(var canvas in canvases){var scaler=canvas.GetComponent<CanvasScaler>();if(scaler)scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=1;}
                foreach(var game in UnityEngine.Object.FindObjectsByType<DoodleIdleGame>(FindObjectsSortMode.None).Where(x=>!x.IsPvpEngine))game.RefreshHudLayout();
                foreach(var canvas in canvases){DoodlePopupMotion.CompleteAll(canvas.transform);foreach(var t in canvas.GetComponentsInChildren<Text>())t.SetAllDirty();}
                Canvas.ForceUpdateCanvases();
                foreach(var ui in UnityEngine.Object.FindObjectsByType<DoodleUi>(FindObjectsSortMode.None).Where(x=>x.Canvas&&x.Canvas.enabled))ui.Relayout(true);
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;var frame=new Texture2D(720,1520,TextureFormat.RGB24,false);
                frame.ReadPixels(new Rect(0,0,720,1520),0,0);frame.Apply();
                string folder=companionsOnly?"artifacts/screenshots/pvp-companions":"artifacts/screenshots/pvp-loadout";
                Directory.CreateDirectory(folder);File.WriteAllBytes(folder+"/"+name+".png",frame.EncodeToPNG());UnityEngine.Object.Destroy(frame);
            } finally {
                camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=active;
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].scaleFactor=scales[i];if(scalers[i])scalers[i].enabled=enabled[i];}
                UnityEngine.Object.Destroy(target);
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
