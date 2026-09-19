using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public bool JoystickActive { get; private set; }
        RectTransform joystickBase, joystickKnob;
        Vector2 joystickInput;
        Pointer joystickPointer;
        readonly List<RaycastResult> joystickUiHits = new List<RaycastResult>();
        const float JoystickRadius = 65;

        void BuildJoystick()
        {
            ReleaseJoystick();
            var root = new GameObject("Touch joystick", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(hudRoot, false);
            joystickBase = root.GetComponent<RectTransform>();
            joystickBase.sizeDelta = Vector2.one * (JoystickRadius * 2);
            var ring = root.GetComponent<Image>(); ring.sprite = summonArt["SoundWave"];
            ring.color = new Color(1, .98f, .92f, .7f); ring.raycastTarget = false;
            var knob = new GameObject("Joystick knob", typeof(RectTransform), typeof(Image));
            knob.transform.SetParent(root.transform, false);
            joystickKnob = knob.GetComponent<RectTransform>(); joystickKnob.sizeDelta = Vector2.one * 48;
            var dot = knob.GetComponent<Image>(); dot.sprite = disc; dot.color = new Color(.3f, .28f, .24f, .85f); dot.raycastTarget = false;
            root.SetActive(false);
        }
        void ReleaseJoystick()
        {
            JoystickActive = false; joystickInput = Vector2.zero; joystickPointer = null;
            if (joystickBase) joystickBase.gameObject.SetActive(false);
        }
        void UpdateJoystick()
        {
            if (paused) { ReleaseJoystick(); return; }
            var pointer = JoystickActive ? joystickPointer : Pointer.current;
            if (pointer == null) return;
            Vector2 screen = pointer.position.ReadValue();
            if (!JoystickActive)
            {
                if (!pointer.press.wasPressedThisFrame) return;
                var events = EventSystem.current;
                if (events)
                {
                    joystickUiHits.Clear();
                    events.RaycastAll(new PointerEventData(events) { position = screen }, joystickUiHits);
                    if (joystickUiHits.Count > 0) return;
                }
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hudRoot, screen, null, out var point);
                joystickBase.anchoredPosition = point;
                joystickBase.SetAsLastSibling(); joystickBase.gameObject.SetActive(true);
                joystickPointer = pointer; JoystickActive = true; dashRemaining = 0;
            }
            if (!pointer.press.isPressed) { ReleaseJoystick(); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickBase, screen, null, out var offset);
            joystickInput = Vector2.ClampMagnitude(offset / JoystickRadius, 1);
            if (joystickInput.sqrMagnitude < .01f) joystickInput = Vector2.zero;
            joystickKnob.anchoredPosition = joystickInput * JoystickRadius;
        }
        void OnApplicationFocus(bool focused) { if (!focused) ReleaseJoystick(); }
    }
}
