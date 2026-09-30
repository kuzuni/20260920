using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackEnd;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleIdle
{
    public sealed partial class DoodleBackendSession : MonoBehaviour
    {
        [Serializable] public sealed class Settings
        {
            public string clientAppId = "", signatureKey = "", googleWebClientId = "";
            public string profileTable = "PlayerProfile", stageTable = "StageProgress", stageLeaderboardUuid = "";
            public string chatUuid = "";
            public string pvpDatabaseUuid = "", pvpLeaderboardUuid = "";
            public bool paymentsEnabled;
        }
        public const string PrivacyUrl = "https://semobobo.netlify.app/privacy";
        public const string TermsUrl = "https://semobobo.netlify.app/terms";
        public const string LoginScene = "DoodleLogin", GameScene = "DoodleIdle";
        public static DoodleBackendSession Instance { get; private set; }
        public Settings Config { get; private set; }
        public bool Ready { get; private set; }
        public bool Busy { get; private set; }
        public string AccountId { get; private set; }
        public string Status { get; private set; } = "로그인해 주세요.";
        public event Action Changed;
        string profileRow, stageRow;
        int publishedStage = -1, desiredStage;
        Task<bool> saving;
        Task<bool> publishing;
        readonly DoodleSaveSchedule serverSave = new DoodleSaveSchedule(DoodleSaveSchedule.ServerInterval, DoodleSaveSchedule.RetryInterval);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
        public static DoodleBackendSession Get()
        {
            if (!Instance) Instance = new GameObject("BACKND session").AddComponent<DoodleBackendSession>();
            return Instance;
        }
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            var text = Resources.Load<TextAsset>("DoodleIdle/BackendSettings");
            Config = text ? JsonUtility.FromJson<Settings>(text.text) : new Settings();
        }
        void OnDestroy() { pvpDatabase?.Dispose(); if (Instance == this) Instance = null; }
        void Message(string text) { Status = text; Changed?.Invoke(); }
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void AuthDiagnostic(string step) => Debug.Log("[DoodleAuth] " + step);
        public static Task<BackendReturnObject> Request(Action<Backend.BackendCallback> operation)
        {
            var result = new TaskCompletionSource<BackendReturnObject>();
            try { operation(response => result.TrySetResult(response)); }
            catch (Exception e) { result.TrySetException(e); }
            return result.Task;
        }
        async Task<bool> InitializeBackend()
        {
            if (Backend.IsInitialized) return true;
            if (string.IsNullOrEmpty(Config.clientAppId) || string.IsNullOrEmpty(Config.signatureKey))
            { Message("서버 연결 설정을 확인해 주세요."); return false; }
            var response = await Request(cb => Backend.InitializeAsync(new BackendCustomSetting {
                clientAppID = Config.clientAppId, signatureKey = Config.signatureKey, isSendLogReport = false, useAsyncPoll = false, timeOutSec = 30
            }, cb));
            AuthDiagnostic("backnd_initialize_status_" + response.GetStatusCode());
            if (!response.IsSuccess()) Message("뒤끝 초기화 실패\n" + BackendErrorDetail(response));
            return response.IsSuccess();
        }
        public async void GoogleLogin()
        {
            if (Busy || Ready) return;
            Busy = true; Message(DoodleLanguage.Text("Google 계정 연결 중…", "Connecting to Google…"));
            AuthDiagnostic("google_begin");
            try
            {
                if (!await InitializeBackend()) return;
#if UNITY_ANDROID && !UNITY_EDITOR
                string token = await RequestGoogleToken();
                if (string.IsNullOrEmpty(token)) return;
                var response = await Request(cb => Backend.BMember.AuthorizeFederation(token, FederationType.Google, cb));
                AuthDiagnostic("backnd_google_status_" + response.GetStatusCode());
                if (!response.IsSuccess()) { LoginError(response); return; }
                IsGuest = false;
                await EnterGame();
#else
                Message("Google 로그인은 Android에서 사용할 수 있어요.");
#endif
            }
            catch (Exception error) {
                string detail = RedactAuthError(error.GetType().Name + ": " + error.Message);
                AuthDiagnostic("google_login_exception_" + detail);
                Message("Google 로그인 예외\n" + detail);
            }
            finally { Busy = false; Changed?.Invoke(); }
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        bool googleSignInStarted;
        async Task<string> RequestGoogleToken(bool chooseAnotherAccount = false)
        {
                // BACKND's Google SDK reuses the last selected Google account.
                // Only clear Google SDK selection; keep the BACKND guest logged in.
                if (chooseAnotherAccount && googleSignInStarted)
                {
                    var cleared = new TaskCompletionSource<bool>();
                    string resetError = "";
                    TheBackend.ToolKit.GoogleLogin.Android.GoogleSignOut(true, (success, error) =>
                    {
                        resetError = RedactAuthError(error);
                        cleared.TrySetResult(success);
                    });
                    if (await Task.WhenAny(cleared.Task, Task.Delay(15000)) != cleared.Task)
                    {
                        Message(DoodleLanguage.Text("Google 계정 선택 초기화가 지연됐어요. 다시 시도해 주세요. (G-SIGNOUT-TIMEOUT)",
                            "Resetting Google account selection timed out. Please retry. (G-SIGNOUT-TIMEOUT)"));
                        return null;
                    }
                    if (!await cleared.Task)
                    {
                        Message(DoodleLanguage.Text("다른 Google 계정을 선택할 준비에 실패했어요. 다시 시도해 주세요.\n",
                            "Could not reset Google account selection. Please retry.\n") + resetError);
                        return null;
                    }
                }
                if (string.IsNullOrEmpty(Config.googleWebClientId)) { Message("Google 로그인 설정을 확인해 주세요."); return null; }
                var tokenResult = new TaskCompletionSource<string>();
                string googleError = "";
                string googleDetail = "";
                TheBackend.ToolKit.GoogleLogin.Android.GoogleLogin(Config.googleWebClientId, true,
                    (success, error, token) => {
                        if (success) googleSignInStarted = true;
                        googleError = SafeGoogleError(error);
                        googleDetail = RedactAuthError(error);
                        if (!success) GoogleSdkDiagnostic(error);
                        tokenResult.TrySetResult(success ? token : null);
                    });
                if (await Task.WhenAny(tokenResult.Task, Task.Delay(120000)) != tokenResult.Task) {
                    AuthDiagnostic("google_callback_timeout");
                    Message("Google 로그인 응답이 지연됐어요. 다시 시도해 주세요. (G-TIMEOUT)"); return null;
                }
                string token = await tokenResult.Task;
                AuthDiagnostic(string.IsNullOrEmpty(token) ? "google_token_failed_or_cancelled" : "google_token_received");
                if (string.IsNullOrEmpty(token)) {
                    AuthDiagnostic("google_error_" + googleError);
                    Message("Google SDK 로그인 실패 (" + googleError + ")\n" + googleDetail); return null;
                }
            return token;
        }
#endif
        // Only known public error identifiers are surfaced; SDK error strings can
        // contain account details and must never be logged verbatim.
        static string SafeGoogleError(string error)
        {
            if (string.IsNullOrEmpty(error)) return "G-CANCELLED";
            string[] identifiers = { "ExceptionInInitializerError", "ClassNotFoundException", "NoClassDefFoundError", "NoSuchMethodError",
                "NoSuchFieldError", "AndroidJavaException", "MissingMethodException", "NullReferenceException" };
            foreach (string identifier in identifiers)
                if (error.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0) return identifier;
            var reason = System.Text.RegularExpressions.Regex.Match(error, @"Reason\s*:\s*(-?\d{1,6})\s*:");
            if (reason.Success) return "G-" + reason.Groups[1].Value;
            if (error.IndexOf("SDK Exception", StringComparison.OrdinalIgnoreCase) >= 0) return "G-SDK";
            return "G-UNKNOWN";
        }
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void GoogleSdkDiagnostic(string error)
        {
            // Only SDK construction/interop exceptions, never Google responses or tokens.
            if (string.IsNullOrEmpty(error) || !error.StartsWith("SDK Exception", StringComparison.Ordinal)) return;
            AuthDiagnostic("sdk_detail_" + RedactAuthError(error));
        }
        static string RedactAuthError(string error)
        {
            if (string.IsNullOrWhiteSpace(error)) return "SDK returned no error detail.";
            string detail = System.Text.RegularExpressions.Regex.Replace(error,
                @"eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+|GOCSPX-[A-Za-z0-9_-]+|[\w.+-]+@[\w.-]+", "[redacted]");
            detail = System.Text.RegularExpressions.Regex.Replace(detail,
                @"(?i)(access_token|id_token|refresh_token|client_secret|authorization)\s*[:=]\s*[^\s,;]+", "$1=[redacted]");
            detail = detail.Replace('\r', ' ').Replace('\n', ' ');
            return detail.Substring(0, Math.Min(detail.Length, 700));
        }
#if UNITY_EDITOR
        public async void EditorLogin(string id, string password, bool create)
        {
            if (Busy || string.IsNullOrWhiteSpace(id) || string.IsNullOrEmpty(password)) return;
            Busy = true; Message("테스트 계정 연결 중…");
            try
            {
                if (!await InitializeBackend()) return;
                var response = create ? await Request(cb => Backend.BMember.CustomSignUp(id, password, cb)) :
                                        await Request(cb => Backend.BMember.CustomLogin(id, password, cb));
                if (!response.IsSuccess()) { LoginError(response); return; }
                IsGuest = false;
                await EnterGame();
            }
            catch (Exception) { Message("테스트 로그인을 완료하지 못했어요."); }
            finally { Busy = false; Changed?.Invoke(); }
        }
#endif
        static string BackendErrorDetail(BackendReturnObject result) => RedactAuthError(
            "HTTP " + result.GetStatusCode() + " / " + result.GetErrorCode() + " / " + result.GetMessage());
        void LoginError(BackendReturnObject result) => Message(result.GetStatusCode() == "410"
            ? "탈퇴 처리 중인 계정이에요. 최대 1시간 후 다시 이용할 수 있어요.\n" + BackendErrorDetail(result)
            : "뒤끝 계정 인증 실패\n" + BackendErrorDetail(result));
        async Task EnterGame()
        {
            Ready = false;
            AccountId = Backend.UserInDate;
            DoodlePrefs.UseAccount(AccountId);
            Message("저장된 진행 상황을 불러오는 중…");
            var result = await Request(cb => Backend.GameData.GetMyData(Config.profileTable, new Where(), 1, cb));
            if (!result.IsSuccess()) { Message("진행 상황을 불러오지 못했어요. 다시 로그인해 주세요."); return; }
            var rows = result.FlattenRows();
            if (rows.Count > 0)
            {
                profileRow = rows[0]["inDate"].ToString();
                // Unsynced same-device progress survives a network failure or process termination.
                if (!DoodlePrefs.Dirty) DoodlePrefs.Import(rows[0]["save"].ToString());
            }
            else
            {
                var initial = new Param(); initial.Add("save", DoodlePrefs.Export());
                var inserted = await Request(cb => Backend.GameData.Insert(Config.profileTable, initial, cb));
                if (!inserted.IsSuccess()) { Message("저장 공간을 준비하지 못했어요. 다시 시도해 주세요."); return; }
                profileRow = inserted.GetInDate();
            }
            if (string.IsNullOrEmpty(Backend.UserNickName)) {
                var nickname = await Request(cb => Backend.BMember.CreateNickname("탕탕" + DoodlePurchaseLedger.Hash(AccountId).Substring(0, 10), cb));
                if (!nickname.IsSuccess()) { Message("닉네임을 준비하지 못했어요. 다시 로그인해 주세요."); return; }
            }
            Ready = true; stageRow = null; desiredStage = 0; publishedStage = -1;
            serverSave.Reset(Time.realtimeSinceStartupAsDouble);
            SceneManager.LoadScene(GameScene);
            AuthDiagnostic("game_scene_entered");
        }
        public Task<bool> SaveCloud()
        {
            if (DoodleSecurity.Compromised || !Ready || !Backend.IsLogin || Backend.UserInDate != AccountId) return Task.FromResult(false);
            if (saving != null && !saving.IsCompleted) return SaveAfterCurrent();
            saving = SaveNow(); return saving;
        }
        public async Task<bool> ApplyVerifiedVoidedOrders(IReadOnlyList<DoodleRefundReconciler.VoidedOrder> orders)
        {
            if (Busy || !Ready || DoodleSecurity.Compromised || !Backend.IsLogin || Backend.UserInDate != AccountId)
                return false;
            var ui = FindPlayerUi();
            if (!ui) return false;
            Busy = true;
            try
            {
                // Reclaim locally (including coupon debt), persist the local journal,
                // then immediately flush the resulting snapshot to BACKND.
                return await DoodleRefundReconciler.ApplyAndSave(ui, AccountId, orders, SaveCloud);
            }
            finally { Busy = false; Changed?.Invoke(); }
        }
        async Task<bool> SaveAfterCurrent()
        {
            // A purchase arriving during a scheduled save needs its own newer revision persisted.
            // Even when that older request failed, the immediate purchase request must still run.
            await saving;
            return await SaveCloud();
        }
        static DoodleUi FindPlayerUi() => FindObjectsByType<DoodleUi>(FindObjectsSortMode.None).FirstOrDefault(ui=>ui.Canvas);
        async Task<bool> SaveNow()
        {
            serverSave.TryBegin(Time.realtimeSinceStartupAsDouble, true);
            bool success = false;
            string account = AccountId;
            try
            {
                var ui = FindPlayerUi(); if (ui) ui.Save();
                if (!DoodlePrefs.Dirty) return success = true;
                long revision = DoodlePrefs.Revision;
                var param = new Param(); param.Add("save", DoodlePrefs.Export());
                var result = await Request(cb => Backend.GameData.UpdateV2(Config.profileTable, profileRow, account, param, cb));
                success = result.IsSuccess() && Ready && AccountId == account && !DoodleSecurity.Compromised;
                if (success) DoodlePrefs.MarkSynced(revision);
                return success;
            }
            catch (Exception) { return false; }
            finally { serverSave.Complete(Time.realtimeSinceStartupAsDouble, success); }
        }
        async void Update()
        {
            if (DoodleSecurity.Compromised || !Ready || Busy || !serverSave.TryBegin(Time.realtimeSinceStartupAsDouble)) return;
            try { await SaveCloud(); await PublishStage(); }
            catch (Exception) { serverSave.Complete(Time.realtimeSinceStartupAsDouble, false); Message("서버 동기화를 다시 시도하고 있어요."); }
        }
        public void SetStage(int cleared) { desiredStage = Math.Max(desiredStage, cleared); }
        public Task<bool> PublishStage()
        {
            if (publishing != null && !publishing.IsCompleted) return publishing;
            publishing = PublishStageNow(); return publishing;
        }
        async Task<bool> PublishStageNow()
        {
            if (!Ready || string.IsNullOrEmpty(Config.stageLeaderboardUuid)) return false;
            if (desiredStage <= publishedStage) return true;
            if (string.IsNullOrEmpty(stageRow))
            {
                var read = await Request(cb => Backend.GameData.GetMyData(Config.stageTable, new Where(), 1, cb));
                if (!read.IsSuccess()) return false;
                var rows = read.FlattenRows();
                if (rows.Count > 0) { stageRow = rows[0]["inDate"].ToString(); desiredStage = Math.Max(desiredStage, int.Parse(rows[0]["stage"].ToString())); }
                else
                {
                    var initial = new Param(); initial.Add("stage", 0);
                    var write = await Request(cb => Backend.GameData.Insert(Config.stageTable, initial, cb));
                    if (!write.IsSuccess()) return false;
                    stageRow = write.GetInDate();
                }
            }
            int value = desiredStage;
            var param = new Param(); param.Add("stage", value);
            var result = await Request(cb => Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(Config.stageLeaderboardUuid, Config.stageTable, stageRow, param, result => cb(result)));
            if (result.IsSuccess()) publishedStage = value;
            return result.IsSuccess();
        }
        public async Task<bool> LeaveAccount(bool delete)
        {
            if (Busy || !Ready) return false;
            var purchase = FindFirstObjectByType<DoodleIapService>();
            if (purchase && purchase.Busy) { Message("구매 처리가 끝난 뒤 다시 시도해 주세요."); return false; }
            Busy = true;
            try
            {
                var ui = FindPlayerUi(); if (ui) ui.Save();
                if (!delete) DoodlePrefs.Flush();
                if (saving != null && !saving.IsCompleted) await saving;
                var result = delete ? await Request(cb => Backend.BMember.WithdrawAccount(0, cb)) : await Request(cb => Backend.BMember.Logout(cb));
                if (!result.IsSuccess()) { Message("계정 처리를 완료하지 못했어요. 다시 시도해 주세요."); return false; }
                AuthDiagnostic(delete ? "backnd_withdraw_succeeded" : "backnd_logout_succeeded");
                Ready = false;
                pvpDatabase?.Dispose();pvpDatabase=null;pvpAccount=null;
                var chat = GetComponent<DoodleChatService>(); if (chat) { chat.Disconnect(); Destroy(chat); }
                if (ui) { ui.StopSavingForReset(); ui.gameObject.SetActive(false); }
#if UNITY_ANDROID && !UNITY_EDITOR
                var signedOut = new TaskCompletionSource<bool>();
                TheBackend.ToolKit.GoogleLogin.Android.GoogleSignOut(true, (success, error) => signedOut.TrySetResult(success));
                await Task.WhenAny(signedOut.Task, Task.Delay(5000));
                AuthDiagnostic(signedOut.Task.IsCompleted && signedOut.Task.Result ? "google_signout_succeeded" : "google_signout_failed_or_timeout");
#endif
                if (delete) {
                    if (IsGuest) Backend.BMember.DeleteGuestInfo();
                    DoodlePrefs.DeleteAccountCache();
                } else DoodlePrefs.UseAccount(null);
                IsGuest = false;
                AccountId = null; profileRow = stageRow = null; desiredStage = 0;
                SceneManager.LoadScene(LoginScene);
                Message(delete ? "탈퇴 요청이 완료됐어요. 서버 데이터 삭제에 최대 1시간이 걸릴 수 있어요." : "로그아웃했어요.");
                return true;
            }
            catch (Exception) { Message("계정 처리를 완료하지 못했어요. 다시 시도해 주세요."); return false; }
            finally { Busy = false; Changed?.Invoke(); }
        }
    }
}
