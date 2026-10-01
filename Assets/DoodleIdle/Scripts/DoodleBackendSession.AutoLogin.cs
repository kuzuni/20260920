using System;
using System.Threading.Tasks;
using BackEnd;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleBackendSession
    {
        const string AutoLoginBlockedKey="DoodleAuth.SignedOut";
        const string GuestAccountKey="DoodleAuth.GuestAccount";
        Task<bool> startupLogin;
        public bool CanRetryAutoLogin {get;private set;}

        public Task<bool> StartAutoLogin()
        {
            // Rebuilding the login UI (e.g. language change) must not start another request.
            if(startupLogin!=null)return startupLogin;
            return startupLogin=TokenLoginAsync();
        }
        public async void RetryAutoLogin() => await TokenLoginAsync();
        public async Task<bool> TokenLoginAsync()
        {
            if(Ready)return true;
            if(Busy || PlayerPrefs.GetInt(AutoLoginBlockedKey,0)!=0)return false;
            Busy=true;CanRetryAutoLogin=false;
            Message(DoodleLanguage.Text("자동 로그인 중…","Signing you in…"));
            try {
                if(!await InitializeBackend()){CanRetryAutoLogin=true;return false;}
                // SDK owns token persistence and refresh. Never store a password or copy tokens.
                var response=await Request(cb=>Backend.BMember.LoginWithTheBackendToken(cb));
                if(!response.IsSuccess()){
                    string code=response.GetStatusCode();
                    if(code=="400" || code=="401" || code=="410")
                        Message(DoodleLanguage.Text("로그인해 주세요.","Please sign in."));
                    else if(code=="403") LoginError(response);
                    else {
                        CanRetryAutoLogin=true;
                        Message(DoodleLanguage.Text("서버 연결이 지연됐어요. 자동 로그인을 다시 시도해 주세요.","Connection failed. Please retry automatic sign-in."));
                    }
                    return false;
                }
                IsGuest=PlayerPrefs.GetString(GuestAccountKey,"")==DoodlePurchaseLedger.Hash(Backend.UserInDate);
                await EnterGame();
                CanRetryAutoLogin=!Ready;
                return Ready;
            }catch(Exception){
                CanRetryAutoLogin=true;
                Message(DoodleLanguage.Text("자동 로그인에 연결하지 못했어요. 다시 시도해 주세요.","Automatic sign-in failed. Please retry."));
                return false;
            }finally{Busy=false;Changed?.Invoke();}
        }
        void RememberAuthenticatedAccount()
        {
            PlayerPrefs.DeleteKey(AutoLoginBlockedKey);
            // Account-bound UI metadata only; this never authorizes access to server data.
            if(IsGuest)PlayerPrefs.SetString(GuestAccountKey,DoodlePurchaseLedger.Hash(Backend.UserInDate));
            else PlayerPrefs.DeleteKey(GuestAccountKey);
            PlayerPrefs.Save();CanRetryAutoLogin=false;
        }
        void StopAutoLoginAfterSignOut()
        {
            PlayerPrefs.SetInt(AutoLoginBlockedKey,1);PlayerPrefs.Save();
            CanRetryAutoLogin=false;
        }
    }
}
