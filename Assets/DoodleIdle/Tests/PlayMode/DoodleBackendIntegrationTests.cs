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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    // Explicitly opt in: creates its own temporary account, never reads a player's profile.
    public sealed class DoodleBackendIntegrationTests
    {
        [UnityTest]
        public IEnumerator LoginScreenInitializesBeforeSignupAndCanRelogin()
        {
            if (!File.Exists("Library/BackendInitialization.optin")) Assert.Ignore("Live login regression is opt-in.");
            var errors = new System.Collections.Generic.List<string>();
            Application.LogCallback collect = (message, stack, type) => {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            };
            Application.logMessageReceived += collect;
            LogAssert.ignoreFailingMessages = true;
            try {
                DG.Tweening.DOTween.Init();
                var task = RunLoginInitialization();
                float deadline = Time.realtimeSinceStartup + 180;
                while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(task.IsCompleted, Is.True, "Login regression timed out.");
                if (task.IsFaulted) throw task.Exception.InnerException;
                Assert.That(errors, Is.Empty, string.Join("\n", errors));
            } finally {
                Application.logMessageReceived -= collect;
                LogAssert.ignoreFailingMessages = false;
            }
        }
        static async Task RunLoginInitialization()
        {
            Assert.That(DoodleBackendSession.Instance, Is.Null);
            bool sdkFlagBeforeLogin = Backend.IsInitialized;
            string id = "qa_init_" + Guid.NewGuid().ToString("N").Substring(0, 16);
            string password = Guid.NewGuid().ToString("N") + "aA1!";
            string account = null;
            DoodleBackendSession session = null;
            try {
                SceneManager.LoadScene(DoodleBackendSession.LoginScene);
                await Task.Delay(200);
                session = DoodleBackendSession.Get();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var initialized = typeof(DoodleBackendSession).GetField("backendInitialized", flags);
                Assert.That(initialized.GetValue(session), Is.EqualTo(false), "New session must not inherit SDK's static success flag.");
                string appId = session.Config.clientAppId;
                try {
                    // A stale SDK flag must not bypass this session's config validation.
                    session.Config.clientAppId = "";
                    var attempt = (Task<bool>)typeof(DoodleBackendSession).GetMethod("InitializeBackend", flags).Invoke(session, null);
                    Assert.That(await attempt, Is.False);
                    Assert.That(initialized.GetValue(session), Is.EqualTo(false));
                } finally { session.Config.clientAppId = appId; }
                await SubmitLoginScreen(session, id, password, true);
                account = session.AccountId;
                Assert.That(initialized.GetValue(session), Is.EqualTo(true));
                Assert.That(await session.LeaveAccount(false), Is.True);
                await Task.Delay(200);
                await SubmitLoginScreen(session, id, password, false);
                Assert.That(session.AccountId, Is.EqualTo(account));
                Assert.That(await session.LeaveAccount(true), Is.True);
                account = null;
                Directory.CreateDirectory("artifacts/character-reports");
                File.AppendAllText("artifacts/character-reports/backend-initialization-audit.txt",
                    DateTime.UtcNow.ToString("O") + ": SDK static flag at entry=" + sdkFlagBeforeLogin +
                    "; invalid config rejected; login-screen signup, logout, login, withdrawal passed; no test pre-initialization.\n");
            } finally {
                if (account != null) {
                    var login = await DoodleBackendSession.Request(cb => Backend.BMember.CustomLogin(id, password, cb));
                    if (login.IsSuccess() && Backend.UserInDate == account)
                        Check(await DoodleBackendSession.Request(cb => Backend.BMember.WithdrawAccount(0, cb)), "cleanup QA account");
                }
                DoodlePrefs.UseAccount(null);
                if (session) UnityEngine.Object.Destroy(session.gameObject);
            }
        }
        static async Task SubmitLoginScreen(DoodleBackendSession session, string id, string password, bool create)
        {
            var screen = UnityEngine.Object.FindFirstObjectByType<DoodleLogin>();
            Assert.That(screen, Is.Not.Null);
            screen.GetComponentsInChildren<InputField>().Single(x => x.name == "ID").text = id;
            screen.GetComponentsInChildren<InputField>().Single(x => x.name == "Password").text = password;
            screen.GetComponentInChildren<Toggle>().isOn = true;
            var labels = create ? new[] { "테스트 계정 생성", "Create test account" } : new[] { "테스트 로그인", "Test sign in" };
            screen.GetComponentsInChildren<Button>().Single(x => labels.Contains(x.name)).onClick.Invoke();
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (session.Busy && DateTime.UtcNow < deadline) await Task.Delay(25);
            Assert.That(session.Ready, Is.True, session.Status);
            while (DateTime.UtcNow < deadline) {
                var game = UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
                if (game && game.Ready) return;
                await Task.Delay(25);
            }
            Assert.Fail("Login did not enter the ready game scene.");
        }
        [UnityTest]
        public IEnumerator GuestCanResumeTheSameProgressAndDeleteItsOwnAccount()
        {
            if (Environment.GetEnvironmentVariable("DOODLE_BACKND_GUEST_SMOKE") != "1") Assert.Ignore("Live guest test is opt-in.");
            Assert.That(DoodleBackendSession.Instance, Is.Null, "Use an isolated Editor session.");
            // Do not replace or delete any existing guest credentials on this PC.
            if (!string.IsNullOrEmpty(Backend.BMember.GetGuestID())) Assert.Ignore("An existing guest must be preserved.");
            var session = DoodleBackendSession.Get();
            string createdAccount = null;
            try
            {
                var login = session.GuestLoginAsync();
                while (!login.IsCompleted) yield return null;
                Assert.That(login.Result, Is.True, session.Status);
                createdAccount = session.AccountId;
                Assert.That(session.IsGuest, Is.True);
                string guestId = Backend.BMember.GetGuestID();
                Assert.That(guestId, Does.StartWith("guest-"));
                yield return null;
                var ui = UnityEngine.Object.FindFirstObjectByType<DoodleUi>();
                ui.Diamonds = 12345;
                ui.ShowPage("Settings");
                Assert.That(ui.Canvas.GetComponentsInChildren<Button>().Any(b => b.name == "Google 계정 연동" || b.name == "Link Google account"), Is.True);
                ui.OpenStageLeaderboard();
                Text rankStatus = ui.Canvas.GetComponentsInChildren<Text>().Single(t => t.name == "Stage leaderboard status");
                double rankDeadline = Time.realtimeSinceStartupAsDouble + 90;
                while ((rankStatus.text == "불러오는 중…" || rankStatus.text == "Loading…") && Time.realtimeSinceStartupAsDouble < rankDeadline) yield return null;
                Assert.That(rankStatus.text.Contains("상위 50명") || rankStatus.text.Contains("Top 50"), Is.True, rankStatus.text);
                Assert.That(ui.Canvas.GetComponentsInChildren<Text>().Any(t => t.name.StartsWith("Stage leaderboard entry ")), Is.True, "Server rows must be rendered in the popup.");
                Assert.That(rankStatus.GetComponentInParent<ScrollRect>(), Is.Not.Null);
                ui.CloseDetail();
                var save = session.SaveCloud();
                while (!save.IsCompleted) yield return null;
                Assert.That(save.Result, Is.True);
                var logout = session.LeaveAccount(false);
                while (!logout.IsCompleted) yield return null;
                Assert.That(logout.Result, Is.True);
                Assert.That(Backend.BMember.GetGuestID(), Is.EqualTo(guestId));
                yield return null;
                var resume = session.GuestLoginAsync();
                while (!resume.IsCompleted) yield return null;
                Assert.That(resume.Result, Is.True, session.Status);
                Assert.That(session.AccountId, Is.EqualTo(createdAccount));
                yield return null;
                ui = UnityEngine.Object.FindFirstObjectByType<DoodleUi>();
                Assert.That(ui.Diamonds, Is.EqualTo(12345));
                var delete = session.LeaveAccount(true);
                while (!delete.IsCompleted) yield return null;
                Assert.That(delete.Result, Is.True, session.Status);
                Assert.That(string.IsNullOrEmpty(Backend.BMember.GetGuestID()), Is.True);
                Assert.That(session.IsGuest, Is.False);
                createdAccount = null;
            }
            finally
            {
                if (createdAccount != null && Backend.IsLogin && Backend.UserInDate == createdAccount)
                {
                    var cleanup = Backend.BMember.WithdrawAccount(0);
                    if (cleanup.IsSuccess()) Backend.BMember.DeleteGuestInfo();
                }
                if (session) UnityEngine.Object.Destroy(session.gameObject);
            }
        }
        [UnityTest]
        public IEnumerator SettingsWithdrawalDeletesTemporaryAccountAndReturnsToLogin()
        {
            if (Environment.GetEnvironmentVariable("DOODLE_BACKND_SMOKE") != "1") Assert.Ignore("Live BACKND test is opt-in.");
            Assert.That(DoodleBackendSession.Instance, Is.Null, "Run against an isolated Editor session.");
            bool korean = DoodleLanguage.Korean;
            DoodleLanguage.Set(true);
            var session = DoodleBackendSession.Get();
            string id = "qa_delete_" + Guid.NewGuid().ToString("N").Substring(0, 16);
            string password = Guid.NewGuid().ToString("N") + "aA1!";
            try
            {
                session.EditorLogin(id, password, true);
                double deadline = Time.realtimeSinceStartupAsDouble + 120;
                while (session.Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(session.Ready, Is.True, session.Status);
                yield return null;
                var ui = UnityEngine.Object.FindFirstObjectByType<DoodleUi>();
                Assert.That(ui, Is.Not.Null);
                DoodlePrefs.Flush();
                string savePath = Path.Combine(Application.persistentDataPath, "saves", "DoodleAccount." + DoodlePurchaseLedger.Hash(session.AccountId) + ".json");
                Assert.That(File.Exists(savePath), Is.True);
                ui.ShowPage("Settings");
                var withdraw = ui.Canvas.GetComponentsInChildren<Button>().Single(b => b.name == "회원탈퇴");
                withdraw.onClick.Invoke();
                Assert.That(session.Ready && Backend.IsLogin, Is.True, "Opening confirmation must not delete the account.");
                var confirm = ui.Canvas.GetComponentsInChildren<Button>().Single(b => b.name == "영구 삭제 요청");
                confirm.onClick.Invoke();
                Assert.That(confirm.interactable, Is.False, "Prevent duplicate requests while waiting for the server.");
                deadline = Time.realtimeSinceStartupAsDouble + 90;
                while (session.Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(session.Busy, Is.False, "Withdrawal timed out.");
                Assert.That(session.Ready, Is.False, session.Status);
                Assert.That(session.AccountId, Is.Null);
                Assert.That(Backend.IsLogin, Is.False);
                Assert.That(DoodlePrefs.HasAccount, Is.False);
                Assert.That(File.Exists(savePath), Is.False, "Deleted account's local save must be removed.");
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(DoodleBackendSession.LoginScene));
                var login = DoodleBackendSession.Request(cb => Backend.BMember.CustomLogin(id, password, cb));
                deadline = Time.realtimeSinceStartupAsDouble + 45;
                while (!login.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(login.IsCompleted && !login.IsFaulted, Is.True);
                Assert.That(login.Result.IsSuccess(), Is.False, "Withdrawn account must not sign back in.");
                Assert.That(login.Result.GetStatusCode(), Is.EqualTo("410"));
            }
            finally
            {
                DoodleLanguage.Set(korean);
                if (session) UnityEngine.Object.Destroy(session.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator CustomLoginCanPersistLoadAndLogOutThroughBacknd()
        {
            if (Environment.GetEnvironmentVariable("DOODLE_BACKND_SMOKE") != "1") Assert.Ignore("Live BACKND test is opt-in.");
            var task = Run();
            double deadline = Time.realtimeSinceStartupAsDouble + 180;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "BACKND smoke test timed out.");
            if (task.IsFaulted) throw task.Exception.InnerException;
        }
        static async Task Run()
        {
            var asset = Resources.Load<TextAsset>("DoodleIdle/BackendSettings");
            Assert.That(asset, Is.Not.Null, "Local BACKND settings are required.");
            var settings = JsonUtility.FromJson<DoodleBackendSession.Settings>(asset.text);
            var initialize = await DoodleBackendSession.Request(cb => Backend.InitializeAsync(new BackendCustomSetting {
                clientAppID = settings.clientAppId, signatureKey = settings.signatureKey,
                isSendLogReport = false, useAsyncPoll = false, timeOutSec = 20
            }, cb));
            Check(initialize, "initialize");
            string id = "qa_save_" + Guid.NewGuid().ToString("N").Substring(0, 16);
            string password = Guid.NewGuid().ToString("N") + "aA1!";
            Check(await DoodleBackendSession.Request(cb => Backend.BMember.CustomSignUp(id, password, cb)), "signup");
            string account = Backend.UserInDate;
            try
            {
                DoodlePrefs.UseAccount(account);
                DoodlePrefs.SetInt("DoodleUi.Diamonds", -2000000);
                DoodlePrefs.SetString("DoodleUi.SmokeTest", "local-json-roundtrip");
                DoodlePrefs.Flush();
                var profile = new Param(); profile.Add("save", DoodlePrefs.Export());
                Check(await DoodleBackendSession.Request(cb => Backend.GameData.Insert(settings.profileTable, profile, cb)), "insert profile");
                var stage = new Param(); stage.Add("stage", 0);
                var stageInsert = await DoodleBackendSession.Request(cb => Backend.GameData.Insert(settings.stageTable, stage, cb));
                Check(stageInsert, "insert stage");
                if (!string.IsNullOrEmpty(settings.stageLeaderboardUuid))
                {
                    Check(await DoodleBackendSession.Request(cb => Backend.BMember.CreateNickname("SaveTest" + Guid.NewGuid().ToString("N").Substring(0, 8), cb)), "nickname");
                    var score = new Param(); score.Add("stage", 0);
                    Check(await DoodleBackendSession.Request(cb => Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(settings.stageLeaderboardUuid, settings.stageTable, stageInsert.GetInDate(), score, result => cb(result))), "publish stage ranking");
                    var ranking = new TaskCompletionSource<BackEnd.Leaderboard.BackendUserLeaderboardReturnObject>();
                    Backend.Leaderboard.User.GetLeaderboard(settings.stageLeaderboardUuid, 10, 0, result => ranking.TrySetResult(result));
                    var rankingResult = await ranking.Task;
                    Assert.That(rankingResult.IsSuccess(), Is.True, "load stage ranking");
                }
                Check(await DoodleBackendSession.Request(cb => Backend.BMember.Logout(cb)), "logout");
                Check(await DoodleBackendSession.Request(cb => Backend.BMember.CustomLogin(id, password, cb)), "login");
                var read = await DoodleBackendSession.Request(cb => Backend.GameData.GetMyData(settings.profileTable, new Where(), 1, cb));
                Check(read, "load profile");
                var rows = read.FlattenRows();
                Assert.That(rows.Count, Is.EqualTo(1));
                DoodlePrefs.Import(rows[0]["save"].ToString());
                Assert.That(DoodlePrefs.GetInt("DoodleUi.Diamonds"), Is.EqualTo(-2000000));
                Assert.That(DoodlePrefs.GetString("DoodleUi.SmokeTest"), Is.EqualTo("local-json-roundtrip"));
                DoodlePrefs.SetInt("DoodleUi.Diamonds", -1999999);
                var updated = new Param(); updated.Add("save", DoodlePrefs.Export());
                Check(await DoodleBackendSession.Request(cb => Backend.GameData.UpdateV2(settings.profileTable, rows[0]["inDate"].ToString(), account, updated, cb)), "update profile");
                var reread = await DoodleBackendSession.Request(cb => Backend.GameData.GetMyData(settings.profileTable, new Where(), 1, cb));
                Check(reread, "reload profile");
                DoodlePrefs.Import(reread.FlattenRows()[0]["save"].ToString());
                Assert.That(DoodlePrefs.GetInt("DoodleUi.Diamonds"), Is.EqualTo(-1999999));
            }
            finally
            {
                DoodlePrefs.DeleteAccountCache();
                if (Backend.IsLogin && Backend.UserInDate == account)
                    Check(await DoodleBackendSession.Request(cb => Backend.BMember.WithdrawAccount(0, cb)), "delete temporary QA account");
            }
        }
        static void Check(BackendReturnObject result, string operation)
        {
            // Status/error codes only: never dump tokens, raw responses, or account secrets to CI logs.
            Assert.That(result.IsSuccess(), Is.True, operation + ": " + result.GetStatusCode() + " " + result.GetErrorCode());
        }
    }
}
#endif
