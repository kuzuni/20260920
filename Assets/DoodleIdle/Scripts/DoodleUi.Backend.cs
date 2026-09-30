using System;
using System.Threading.Tasks;
using BackEnd;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        static string L(string ko, string en) => DoodleLanguage.Text(ko, en);
        void BuildBackendAccount(RectTransform body)
        {
            var session = DoodleBackendSession.Instance;
            var account = ServiceCard(body, "Account connection", UiKit.Paper);
            UiKit.Text(account, L("계정", "Account"), 27, TextAnchor.MiddleLeft, 38);
            UiKit.Text(account, session && session.Ready ? PlayerName : L("에디터 로컬 테스트", "Local editor test"), 23, TextAnchor.MiddleLeft, 36);
            if (session && session.Ready)
            {
                UiKit.Text(account, session.IsGuest ? L("게스트 계정", "Guest account") : L("연결된 계정", "Connected account"), 21, TextAnchor.MiddleLeft, 32);
                if (session.IsGuest)
                    UiKit.Button(account, L("Google 계정 연동", "Link Google account"), LinkGoogleDialog, Color.white, 58);
                UiKit.Button(account, L("로그아웃", "Sign out"), () => AccountDialog(false), UiKit.Blue, 58);
                UiKit.Button(account, L("회원탈퇴", "Delete account"), () => AccountDialog(true), UiKit.Red, 58);
            }
            UiKit.Button(account, L("랭킹", "Rankings"), ()=>OpenRankings(), UiKit.Yellow, 58);
            var links = UiKit.Row(body, "Policy links", 58, 10);
            UiKit.Button(links, L("이용약관", "Terms of Service"), () => Application.OpenURL(DoodleBackendSession.TermsUrl), UiKit.Paper, 58);
            UiKit.Button(links, L("개인정보처리방침", "Privacy Policy"), () => Application.OpenURL(DoodleBackendSession.PrivacyUrl), UiKit.Paper, 58);
            UiKit.Button(body, "English / 한국어", () => { DoodleLanguage.Set(!DoodleLanguage.Korean); RefreshPage(); }, UiKit.Paper, 58);
        }
        void LinkGoogleDialog()
        {
            ShowDetail(L("Google 계정 연동", "Link Google account"), panel =>
            {
                var message = UiKit.Text(panel, L("현재 게스트의 진행 상황과 구매 내역을 Google 계정에 연결합니다. 이미 게임 계정이 있는 Google 계정과는 합칠 수 없습니다.",
                    "Link your current guest progress and purchases to Google. It cannot be merged with an existing game account."), 23, TextAnchor.MiddleCenter, 180);
                Button confirm = null;
                confirm = UiKit.Button(panel, L("Google 계정 선택", "Choose Google account"), async () =>
                {
                    var session = DoodleBackendSession.Instance;
                    if (!session || session.Busy || !session.IsGuest) return;
                    confirm.interactable = false;
                    bool success = await session.LinkGuestToGoogle();
                    if (message) message.text = session.Status;
                    if (confirm)
                    {
                        confirm.interactable = !success && session.IsGuest;
                        confirm.GetComponentInChildren<Text>().text = success
                            ? L("연동 완료", "Account linked")
                            : L("다른 Google 계정 선택", "Choose another Google account");
                    }
                }, UiKit.Blue, 64);
            });
        }
        void AccountDialog(bool delete)
        {
            ShowDetail(delete ? L("회원탈퇴할까요?", "Delete your account?") : L("로그아웃할까요?", "Sign out?"), panel =>
            {
                var message = UiKit.Text(panel, delete ? L("진행 상황과 구매한 재화를 포함한 계정 데이터가 삭제됩니다. 복구할 수 없습니다.\n삭제 완료까지 최대 1시간이 걸릴 수 있습니다.", "Your progress and purchased currency will be deleted permanently. This cannot be undone.\nDeletion can take up to one hour.") : L("이 기기에 진행 상황을 저장하고 로그인 화면으로 돌아갑니다. 최근 서버 저장 이후의 진행은 다른 기기에 아직 반영되지 않았을 수 있습니다.", "Progress will be saved on this device before signing out. Progress since the last server save may not be available on other devices yet."), 23, TextAnchor.MiddleCenter, 180);
                Button confirm = null;
                confirm = UiKit.Button(panel, delete ? L("영구 삭제 요청", "Delete permanently") : L("로그아웃", "Sign out"), async () =>
                {
                    var session = DoodleBackendSession.Instance; if (!session || session.Busy) return;
                    confirm.interactable = false;
                    bool success = await session.LeaveAccount(delete);
                    if (!success && message) { message.text = session.Status; confirm.interactable = true; }
                }, delete ? UiKit.Red : UiKit.Blue, 64);
            });
        }
        public void OpenStageLeaderboard() => OpenRankings(0);
    }
}
