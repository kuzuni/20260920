#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using BackEnd;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    // Run the two phases in separate Play Mode runs to verify persisted SDK tokens.
    public sealed class DoodleTokenLoginTests
    {
        const string Journal="Library/TokenLoginQA.json";
        const string Block="DoodleAuth.SignedOut";
        [Serializable] sealed class Account { public string id,password,account; public int blocked; public string guest; }
        [UnityTest] public IEnumerator PreparePersistedToken() => Run(Prepare);
        [UnityTest] public IEnumerator ResumePersistedTokenAndCheckLogout() => Run(Resume);
        static IEnumerator Run(Func<Task> action)
        {
            if(!File.Exists("Library/TokenLoginQA.optin"))Assert.Ignore("Live token regression is opt-in.");
            DG.Tweening.DOTween.Init();
            var task=action();float deadline=Time.realtimeSinceStartup+180;
            while(!task.IsCompleted && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(task.IsCompleted,Is.True,"Token regression timed out; retain QA journal for cleanup.");
            if(task.IsFaulted)throw task.Exception.InnerException;
        }
        static async Task WaitReady(DoodleBackendSession session)
        {
            var deadline=DateTime.UtcNow.AddSeconds(90);
            while(session.Busy && DateTime.UtcNow<deadline)await Task.Delay(25);
            Assert.That(session.Ready,Is.True,session.Status);
            while(DateTime.UtcNow<deadline){
                var game=UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
                if(game && game.Ready && UnityEngine.Object.FindFirstObjectByType<DoodleUi>())return;
                await Task.Delay(25);
            }
            Assert.Fail("Game did not become ready.");
        }
        static async Task Prepare()
        {
            Assert.That(File.Exists(Journal),Is.False,"Retain and clean up previous QA account first.");
            var a=new Account{id="qa_token_"+Guid.NewGuid().ToString("N").Substring(0,16),password=Guid.NewGuid().ToString("N")+"aA1!",
                blocked=PlayerPrefs.GetInt(Block,0),guest=PlayerPrefs.GetString("DoodleAuth.GuestAccount","")};
            File.WriteAllText(Journal,JsonUtility.ToJson(a));
            PlayerPrefs.SetInt(Block,1);PlayerPrefs.Save();
            SceneManager.LoadScene(DoodleBackendSession.LoginScene);await Task.Delay(200);
            var session=DoodleBackendSession.Get();
            session.EditorLogin(a.id,a.password,true);await WaitReady(session);
            a.account=session.AccountId;File.WriteAllText(Journal,JsonUtility.ToJson(a));
            var ui=UnityEngine.Object.FindFirstObjectByType<DoodleUi>();ui.Diamonds=12345;
            Assert.That(await session.SaveCloud(),Is.True);
            Assert.That(PlayerPrefs.GetInt(Block,0),Is.Zero);
            // End Play Mode without Logout, exactly as application shutdown does.
        }
        static async Task Resume()
        {
            Assert.That(File.Exists(Journal),Is.True,"Run preparation in a separate Play Mode run first.");
            var a=JsonUtility.FromJson<Account>(File.ReadAllText(Journal));
            SceneManager.LoadScene(DoodleBackendSession.LoginScene);await Task.Delay(200);
            var session=DoodleBackendSession.Get();
            Assert.That(await session.StartAutoLogin(),Is.True,session.Status);
            await WaitReady(session);
            Assert.That(session.AccountId==a.account,Is.True,"Automatic login restored a different account.");
            Assert.That(session.IsGuest,Is.False);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<DoodleUi>().Diamonds,Is.EqualTo(12345));
            Assert.That(await session.LeaveAccount(false),Is.True);await Task.Delay(300);
            Assert.That(await session.TokenLoginAsync(),Is.False,"Explicit logout must suppress automatic sign-in.");
            Assert.That(session.Ready,Is.False);
            // SDK tokens were invalidated by logout. Missing/expired token must leave login available.
            PlayerPrefs.DeleteKey(Block);PlayerPrefs.Save();
            Assert.That(await session.TokenLoginAsync(),Is.False);
            Assert.That(session.Busy || session.Ready,Is.False);
            session.EditorLogin(a.id,a.password,false);await WaitReady(session);
            Assert.That(session.AccountId==a.account,Is.True);
            Assert.That(await session.LeaveAccount(true),Is.True,"QA withdrawal failed; retain journal.");
            PlayerPrefs.SetInt(Block,a.blocked);
            if(string.IsNullOrEmpty(a.guest))PlayerPrefs.DeleteKey("DoodleAuth.GuestAccount");
            else PlayerPrefs.SetString("DoodleAuth.GuestAccount",a.guest);
            PlayerPrefs.Save();File.Delete(Journal);
            File.WriteAllText("artifacts/character-reports/token-login-audit.txt",DateTime.UtcNow.ToString("O")+
                ": separate Play Mode runs; startup token login; same account and saved diamonds; explicit logout suppression; invalid token fallback; manual relogin; QA withdrawal passed.\n");
        }
    }
}
#endif
