using UnityEngine;
using UnityEngine.UI;
namespace DoodleIdle
{
    public sealed class DoodleLoadingScreen : MonoBehaviour
    {
        static DoodleLoadingScreen pendingLogin;
        public Canvas Canvas { get; private set; }
        public bool FromLogin { get; private set; }
        Font uiFont;
        Text loadingLabel;
        Image loadingBar;
        DoodleLoadingMotion loadingMotion;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => pendingLogin = null;
        public static DoodleLoadingScreen CreateForLogin()
        {
            if (pendingLogin) return pendingLogin;
            pendingLogin = Create(null);
            pendingLogin.FromLogin = true;
            DontDestroyOnLoad(pendingLogin.gameObject);
            return pendingLogin;
        }
        public static DoodleLoadingScreen TakeForGame(Transform parent)
        {
            if (!pendingLogin) return Create(parent);
            var view = pendingLogin; pendingLogin = null;
            view.transform.SetParent(parent, false);
            return view;
        }
        static DoodleLoadingScreen Create(Transform parent)
        {
            var go = new GameObject("Preparing game", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<DoodleLoadingScreen>(); view.Build(); return view;
        }
        void OnDestroy() { if (pendingLogin == this) pendingLogin = null; }
        void Build()
        {
            var go = gameObject;
            Canvas = go.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 32760;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1520); scaler.matchWidthOrHeight = 1;
            var cover = new GameObject("Loading cover", typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(go.transform, false);
            var rect = (RectTransform)cover.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            cover.GetComponent<Image>().color = new Color(.91f, .83f, .68f);
            var illustration=UiKit.Rect(cover.transform,"Loading battle illustration");
            var art=illustration.gameObject.AddComponent<RawImage>();
            art.texture=Resources.Load<Texture2D>("DoodleIdle/UI/LoadingBattle");art.raycastTarget=false;
            uiFont = Resources.Load<Font>("DoodleIdle/UI/DisplayFont");
            var title=Label(cover.transform,"탕탕탕",82,new Vector2(-310,-170),new Vector2(620,110),TextAnchor.MiddleCenter,new Vector2(.5f,1));
            title.color=new Color(.2f,.13f,.08f);
            Label(cover.transform,"방치형 RPG",27,new Vector2(-260,-218),new Vector2(520,48),TextAnchor.MiddleCenter,new Vector2(.5f,1));
            loadingLabel = Label(cover.transform, "게임 준비 중", 26, new Vector2(-290,120), new Vector2(580,54), TextAnchor.MiddleCenter, new Vector2(.5f,0));
            var track = new GameObject("Loading progress", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(cover.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin=trackRect.anchorMax=new Vector2(.5f,0);
            trackRect.sizeDelta = new Vector2(520, 62); trackRect.anchoredPosition = new Vector2(0, 93);
            track.GetComponent<Image>().sprite=UiKit.Art("HealthBarFrame");
            track.GetComponent<Image>().raycastTarget=false;
            var inset=UiKit.Rect(track.transform,"Gauge inner area");UiKit.Stretch(inset,9,10,9,10);
            var bar = new GameObject("Fill", typeof(RectTransform), typeof(Image),typeof(Mask)); bar.transform.SetParent(inset, false);
            loadingBar = bar.GetComponent<Image>();loadingBar.sprite=UiKit.Art("HealthBarFill");loadingBar.raycastTarget=false;
            var shine=UiKit.Rect(bar.transform,"Gauge moving highlight");shine.sizeDelta=new Vector2(38,100);
            shine.localRotation=Quaternion.Euler(0,0,-18);
            var highlight=shine.gameObject.AddComponent<Image>();highlight.color=new Color(1,1,1,.24f);highlight.raycastTarget=false;
            loadingMotion=cover.AddComponent<DoodleLoadingMotion>();loadingMotion.art=illustration;
            loadingMotion.shine=shine;loadingMotion.fill=loadingBar;
            SetProgress(0, "게임 준비 중");
        }

        public void SetProgress(float progress, string message, bool showPercent = true)
        {
            float value = Mathf.Clamp01(progress);
            loadingLabel.text = showPercent ? message + "  " + Mathf.RoundToInt(value * 100) + "%" : message;
            var rect = loadingBar.rectTransform;
            rect.anchorMin = Vector2.zero;
            loadingMotion.progress=Mathf.Max(loadingMotion.progress,value);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        Text Label(Transform parent, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor align, Vector2? anchor = null)
        {
            var go = new GameObject(value.Length > 0 ? value : "Counter", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor ?? Vector2.zero; rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var text = go.GetComponent<Text>(); text.font = uiFont; text.text = value; text.fontSize = size; text.alignment = align; text.color = new Color(.22f, .2f, .17f); text.raycastTarget = false;
            return text;
        }

    }
}
