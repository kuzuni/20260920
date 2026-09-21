using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed class DoodleToastMotion : MonoBehaviour
    {
        Sequence sequence;
        CanvasGroup group;
        CanvasGroup Opacity {
            get {
                if(!group)group=GetComponent<CanvasGroup>();
                if(!group)group=gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts=group.interactable=false;return group;
            }
        }
        public void Show(float duration)
        {
            sequence?.Kill();transform.SetAsLastSibling();
            var opacity=Opacity;opacity.alpha=0;transform.localScale=Vector3.one*.93f;
            sequence=DOTween.Sequence().SetUpdate(true);
            sequence.Append(DOTween.To(()=>opacity.alpha,x=>opacity.alpha=x,1,.2f));
            sequence.Join(DOTween.To(()=>transform.localScale,x=>transform.localScale=x,Vector3.one,.24f).SetEase(Ease.OutBack));
            sequence.AppendInterval(Mathf.Max(0,duration-.42f));
            sequence.Append(DOTween.To(()=>opacity.alpha,x=>opacity.alpha=x,0,.18f));
        }
        public void Hide() { sequence?.Kill();Opacity.alpha=0;transform.localScale=Vector3.one; }
        void OnDestroy() { sequence?.Kill(); }
    }

    /// <summary>Unscaled button motion and a repeat binding that survives page rebuilds.</summary>
    public sealed class DoodleButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public string RepeatKey { get; private set; }
        public Func<bool> RepeatAction { get; private set; }
        Tween pressTween;
        Button button;
        DoodleUiRepeatDriver driver;
        public void BindRepeat(string key, Func<bool> action) { RepeatKey = key; RepeatAction = action; }
        Button Control => button ? button : button = GetComponent<Button>();
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !Control || !Control.IsInteractable()) return;
            Animate(.94f, .07f);
            if (RepeatAction == null) return;
            var canvas = GetComponentInParent<Canvas>();
            driver = canvas.GetComponent<DoodleUiRepeatDriver>();
            if (!driver) driver = canvas.gameObject.AddComponent<DoodleUiRepeatDriver>();
            driver.Begin(this, data.position, data.pressEventCamera);
        }
        public void OnPointerUp(PointerEventData data) { Animate(1, .12f); if (driver) driver.Stop(); }
        public void OnPointerExit(PointerEventData data) { Animate(1, .1f); if (driver) driver.Stop(); }
        public void Pulse() { Animate(.94f, .055f, true); }
        public void CompleteMotion() { pressTween?.Kill(); transform.localScale=Vector3.one; }
        void Animate(float scale, float duration, bool rebound = false)
        {
            pressTween?.Kill();
            pressTween = DOTween.To(() => transform.localScale, v => transform.localScale = v, Vector3.one * scale, duration)
                .SetUpdate(true).SetEase(Ease.OutQuad);
            if (rebound) pressTween.OnComplete(() => Animate(1, .1f));
        }
        public bool ConsumeClick()
        {
            var canvas = GetComponentInParent<Canvas>();
            var repeat = canvas ? canvas.GetComponent<DoodleUiRepeatDriver>() : null;
            return repeat && repeat.ConsumeClick(RepeatKey);
        }
        void OnDisable() { pressTween?.Kill(); transform.localScale = Vector3.one; }
        void OnDestroy() { pressTween?.Kill(); }
    }

    public sealed class DoodleUiRepeatDriver : MonoBehaviour
    {
        string key, suppressedKey;
        float nextRepeat, suppressedUntil;
        Camera eventCamera;
        Vector2 pressPosition;
        bool repeated;
        public bool IsRepeating => key != null;
        public void Begin(DoodleButtonMotion source, Vector2 position, Camera camera)
        {
            Stop(); suppressedKey = null; key = source.RepeatKey;
            nextRepeat = Time.unscaledTime + .4f; repeated = false;
            pressPosition = position; eventCamera = camera;
        }
        public void Stop()
        {
            if (repeated && key != null) { suppressedKey = key; suppressedUntil = Time.unscaledTime + .25f; }
            key = null; repeated = false;
        }
        public bool ConsumeClick(string candidate)
        {
            if (candidate != null && key == candidate && repeated) Stop();
            if (candidate == null || candidate != suppressedKey || Time.unscaledTime > suppressedUntil) return false;
            suppressedKey = null; return true;
        }
        void Update()
        {
            if (key == null) return;
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.isPressed) { Stop(); return; }
            var position = pointer.position.ReadValue();
            DoodleButtonMotion target = null;
            foreach (var candidate in GetComponentsInChildren<DoodleButtonMotion>())
                if (candidate.RepeatKey == key && candidate.GetComponent<Button>().IsInteractable()) { target = candidate; break; }
            if (!target || !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)target.transform, position, eventCamera)
                || (position - pressPosition).sqrMagnitude > 30 * 30) { Stop(); return; }
            if (Time.unscaledTime < nextRepeat) return;
            // At most one transaction each frame, even after a slow frame.
            nextRepeat = Time.unscaledTime + .12f; repeated = true;
            target.Pulse();
            bool succeeded=target.RepeatAction();
            if(succeeded && key!=null)
                foreach(var replacement in GetComponentsInChildren<DoodleButtonMotion>())
                    if(replacement.RepeatKey==key && replacement!=target){replacement.Pulse();break;}
            if (!succeeded) Stop();
        }
        void OnDisable() { Stop(); }
    }

    /// <summary>Opening/closing motion uses unscaled time, including while gameplay is paused.</summary>
    public sealed class DoodlePopupMotion : MonoBehaviour
    {
        Sequence sequence;
        CanvasGroup opacity;
        GameObject closingRoot;
        public bool IsClosing => closingRoot;
        public void Open()
        {
            sequence?.Kill();
            opacity = GetComponent<CanvasGroup>();
            if (!opacity) opacity = gameObject.AddComponent<CanvasGroup>();
            opacity.alpha = 0; transform.localScale = Vector3.one * .94f;
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(DOTween.To(() => opacity.alpha, value => opacity.alpha = value, 1, .16f));
            sequence.Join(DOTween.To(() => transform.localScale, value => transform.localScale = value, Vector3.one, .22f).SetEase(Ease.OutCubic));
        }
        public static void Close(GameObject dim, Transform root)
        {
            if (!dim) return;
            var motion = dim.GetComponentInChildren<DoodlePopupMotion>();
            if (!motion) { dim.SetActive(false); Destroy(dim); return; }
            motion.sequence?.Kill(); motion.closingRoot = dim;
            // Logical closure is immediate; the short visual exit cannot accept old UI actions.
            dim.transform.SetParent(root, true);
            foreach (var node in dim.GetComponentsInChildren<Transform>()) node.name = "Closing " + node.name;
            foreach (var selectable in dim.GetComponentsInChildren<Selectable>()) selectable.interactable = false;
            var group = dim.GetComponent<CanvasGroup>();
            if (!group) group = dim.AddComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            motion.sequence = DOTween.Sequence().SetUpdate(true);
            motion.sequence.Join(DOTween.To(() => group.alpha, value => group.alpha = value, 0, .14f));
            motion.sequence.Join(DOTween.To(() => motion.transform.localScale, value => motion.transform.localScale = value, Vector3.one * .96f, .14f));
            motion.sequence.OnComplete(() => { if (dim) { dim.SetActive(false); Destroy(dim); } });
        }
        public static void CompleteAll(Transform root)
        {
            foreach (var motion in root.GetComponentsInChildren<DoodlePopupMotion>()) motion.sequence?.Complete(true);
            foreach (var button in root.GetComponentsInChildren<DoodleButtonMotion>()) button.CompleteMotion();
        }
        void OnDisable() { sequence?.Kill(); }
        void OnDestroy() { sequence?.Kill(); }
    }
}
