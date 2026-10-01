using System;
using System.Threading.Tasks;
using BackEnd;

namespace DoodleIdle
{
    public sealed partial class DoodleBackendSession
    {
        public bool IsGuest { get; private set; }

        public async void GuestLogin() => await GuestLoginAsync();

        public async Task<bool> GuestLoginAsync()
        {
            if (Busy || Ready) return false;
            Busy = true;
            Message(DoodleLanguage.Text("게스트 계정 연결 중…", "Connecting your guest account…"));
            try
            {
                if (!await InitializeBackend()) return false;
                var response = await Request(cb => Backend.BMember.GuestLogin("Tang Tang Tang guest", cb));
                if (!response.IsSuccess())
                {
                    // Never delete stored credentials on a failed login. A network
                    // error must not silently replace the player's guest account.
                    Message(DoodleLanguage.Text("게스트 로그인 실패\n", "Guest sign-in failed\n") + BackendErrorDetail(response));
                    return false;
                }
                IsGuest = true;
                await EnterGame();
                return Ready;
            }
            catch (Exception error)
            {
                Message(DoodleLanguage.Text("게스트 로그인 오류\n", "Guest sign-in error\n") + RedactAuthError(error.Message));
                return false;
            }
            finally { Busy = false; Changed?.Invoke(); }
        }

        public async Task<bool> LinkGuestToGoogle()
        {
            if (Busy || !Ready || !IsGuest || !Backend.IsLogin || Backend.UserInDate != AccountId) return false;
            var payment = UnityEngine.Object.FindFirstObjectByType<DoodleIapService>();
            if (payment && payment.Busy)
            {
                Message(DoodleLanguage.Text("구매 처리가 끝난 뒤 연동해 주세요.", "Finish the current purchase before linking."));
                return false;
            }
            Busy = true;
            string originalAccount = AccountId;
            Message(DoodleLanguage.Text("Google 계정 연동 중…", "Linking your Google account…"));
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                string token = await RequestGoogleToken(chooseAnotherAccount: true);
                if (string.IsNullOrEmpty(token)) return false;
                var exists = await Request(cb => Backend.BMember.CheckUserInBackend(token, FederationType.Google, cb));
                if (exists.GetStatusCode() != "204")
                {
                    Message(exists.GetStatusCode() == "200"
                        ? DoodleLanguage.Text("이미 게임 계정이 있는 Google 계정입니다. 다른 Google 계정을 선택해 주세요. 현재 게스트 진행은 유지됩니다.",
                            "This Google account already has game progress. Choose another Google account. Your guest progress is unchanged.")
                        : DoodleLanguage.Text("연동 계정 확인 실패\n", "Could not check the Google account\n") + BackendErrorDetail(exists));
                    return false;
                }
                // One exceptional save at account linking prevents a later Google
                // sign-in on another device from opening an older guest snapshot.
                if (!await SaveCloud())
                {
                    Message(DoodleLanguage.Text("진행 상황 저장에 실패해 연동을 중단했어요. 다시 시도해 주세요.",
                        "Could not save your progress. Please retry linking."));
                    return false;
                }
                var result = await Request(cb => Backend.BMember.ChangeCustomToFederation(token, FederationType.Google, cb));
                if (!result.IsSuccess())
                {
                    Message(result.GetStatusCode() == "409"
                        ? DoodleLanguage.Text("이미 가입된 Google 계정이라 연동할 수 없습니다. 다른 Google 계정을 선택해 주세요. 현재 게스트 진행은 유지됩니다.",
                            "This Google account is already registered and cannot be linked. Choose another Google account. Your guest progress is unchanged.")
                        : DoodleLanguage.Text("Google 연동 실패\n", "Google linking failed\n") + BackendErrorDetail(result));
                    return false;
                }
                // This API converts the existing account; it never logs into or
                // merges another account, and keeps the game/profile/receipt IDs.
                if (Backend.UserInDate != originalAccount)
                    throw new InvalidOperationException("Account identity changed during linking.");
                IsGuest = false;
                RememberAuthenticatedAccount();
                Backend.BMember.DeleteGuestInfo();
                Message(DoodleLanguage.Text("Google 계정 연동 완료. 현재 진행 상황이 그대로 유지됩니다.",
                    "Google account linked. Your current progress has been kept."));
                return true;
#else
                Message(DoodleLanguage.Text("Google 계정 연동은 Android에서 사용할 수 있어요.", "Google account linking is available on Android."));
                return false;
#endif
            }
            catch (Exception error)
            {
                Message(DoodleLanguage.Text("계정 연동 오류\n", "Account linking error\n") + RedactAuthError(error.Message));
                return false;
            }
            finally { Busy = false; Changed?.Invoke(); }
        }
    }
}
