using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DoodleIdle
{
    public sealed class DoodleLogin : MonoBehaviour
    {
        DoodleBackendSession session;
        RectTransform panel;
        Text status;
        Button login, guest, retry;
        Toggle agreement;
        DoodleLoadingScreen startupLoading;
        async void Start()
        {
            session = DoodleBackendSession.Get();
            session.Changed += Refresh;
            await RunAutoLogin(session.StartAutoLogin);
        }
        async System.Threading.Tasks.Task RunAutoLogin(System.Func<System.Threading.Tasks.Task<bool>> attempt)
        {
            if (panel) panel.parent.gameObject.SetActive(false);
            startupLoading = DoodleLoadingScreen.CreateForLogin();
            startupLoading.SetProgress(.05f, L("자동 로그인 중…", "Signing you in…"), false);
            bool entered = await attempt();
            if (!this || entered) return; // The game adopts this same cover before its first render.
            startupLoading.gameObject.SetActive(false); Destroy(startupLoading.gameObject); startupLoading = null;
            Build();
        }
        async void RetryAutoLogin() => await RunAutoLogin(session.TokenLoginAsync);
        static string L(string ko, string en) => DoodleLanguage.Text(ko, en);
        void Build()
        {
            if (panel) Destroy(panel.parent.gameObject);
            UiKit.Font = Resources.Load<Font>("DoodleIdle/UI/DisplayFont");
            var canvas = new GameObject("Login UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = canvas.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(720, 1520); scale.matchWidthOrHeight = .5f;
            var bg = canvas.AddComponent<Image>(); bg.color = new Color(.97f, .96f, .91f);
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("Login events", typeof(EventSystem), typeof(InputSystemUIInputModule));
            panel = UiKit.Column(canvas.transform, "Login", 22, 20);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f); panel.pivot = new Vector2(.5f, .5f); panel.sizeDelta = new Vector2(620, 1380);
            UiKit.Text(panel, L("탕탕탕\n방치형 RPG", "Tang Tang Tang\nIdle RPG"), 56, TextAnchor.MiddleCenter, 155);
            var portrait = UiKit.Icon(panel, "Player", 200); UiKit.Height(portrait.transform, 200); portrait.preserveAspect = true;
            status = UiKit.Text(panel, "", 22, TextAnchor.MiddleCenter, 160);
            retry = UiKit.Button(panel,L("자동 로그인 재시도","Retry automatic sign-in"),RetryAutoLogin,UiKit.Blue,58);
            retry.name="Retry automatic login";
            var terms = UiKit.Row(panel, "Agreement", 72, 12);
            var toggle = UiKit.Box(terms, "Accept terms", Color.white, 46); toggle.sizeDelta = new Vector2(46,46);
            var width = toggle.GetComponent<LayoutElement>(); width.minWidth = width.preferredWidth = 46; width.flexibleWidth = 0;
            agreement = toggle.gameObject.AddComponent<Toggle>(); agreement.targetGraphic = toggle.GetComponent<Image>();
            var mark = UiKit.Text(toggle, "✓", 32, TextAnchor.MiddleCenter, 42); UiKit.Stretch(mark.rectTransform); agreement.graphic = mark;
            agreement.isOn = false; agreement.onValueChanged.AddListener(_ => Refresh());
            UiKit.Text(terms, L("이용약관과 개인정보처리방침에 동의합니다.", "I agree to the Terms of Service and Privacy Policy."), 22, TextAnchor.MiddleLeft, 70);
            var links = UiKit.Row(panel, "Legal links", 56, 16);
            UiKit.Button(links, L("이용약관", "Terms of Service"), () => Application.OpenURL(DoodleBackendSession.TermsUrl), UiKit.Paper, 56);
            UiKit.Button(links, L("개인정보처리방침", "Privacy Policy"), () => Application.OpenURL(DoodleBackendSession.PrivacyUrl), UiKit.Paper, 56);
            login = UiKit.Button(panel, L("Google로 로그인", "Sign in with Google"), session.GoogleLogin, Color.white, 80);
            guest = UiKit.Button(panel, L("게스트로 시작", "Play as guest"), session.GuestLogin, UiKit.Blue, 72);
            UiKit.Text(panel, L("게스트는 앱 삭제·기기 변경 시 복구할 수 없습니다. 설정에서 Google 계정을 연동해 주세요.",
                "Guest progress cannot be recovered after reinstalling or changing devices. Link Google in Settings to keep access."), 18, TextAnchor.MiddleCenter, 78);
#if UNITY_EDITOR
            UiKit.Text(panel, "EDITOR TEST ACCOUNT", 18, TextAnchor.MiddleCenter, 30);
            var id = Input(panel, "ID", false); var password = Input(panel, "Password", true);
            var buttons = UiKit.Row(panel, "Editor login", 58, 12);
            UiKit.Button(buttons, L("테스트 로그인", "Test sign in"), () => { if (agreement.isOn) session.EditorLogin(id.text, password.text, false); }, UiKit.Blue, 58);
            UiKit.Button(buttons, L("테스트 계정 생성", "Create test account"), () => { if (agreement.isOn) session.EditorLogin(id.text, password.text, true); }, UiKit.Paper, 58);
#endif
            UiKit.Button(panel, DoodleLanguage.Korean ? "English" : "한국어", () => { DoodleLanguage.Set(!DoodleLanguage.Korean); Build(); }, UiKit.Paper, 52);
            Refresh();
        }
#if UNITY_EDITOR
        static InputField Input(Transform parent, string label, bool password)
        {
            var box = UiKit.Box(parent, label, Color.white, 58);
            var input = box.gameObject.AddComponent<InputField>();
            var text = UiKit.Text(box, "", 24); UiKit.Stretch(text.rectTransform, 12, 4, 12, 4);
            var placeholder = UiKit.Text(box, label, 24); UiKit.Stretch(placeholder.rectTransform, 12, 4, 12, 4); placeholder.color = Color.gray;
            input.textComponent = text; input.placeholder = placeholder;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.characterLimit = 100; return input;
        }
#endif
        void Refresh()
        {
            if (startupLoading) { startupLoading.SetProgress(.05f, session.Status, false); return; }
            if (!status) return;
            status.text = session.Status;
            login.interactable = agreement.isOn && !session.Busy;
            guest.interactable = agreement.isOn && !session.Busy;
            agreement.interactable = !session.Busy;
            retry.gameObject.SetActive(session.CanRetryAutoLogin);
            retry.interactable = !session.Busy;
        }
        void OnDestroy()
        {
            if (session) session.Changed -= Refresh;
            if (startupLoading && (!session || !session.Ready)) Destroy(startupLoading.gameObject);
        }
    }
}
