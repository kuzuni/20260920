using UnityEngine;
using UnityEngine.UI;
namespace DoodleIdle
{
    [RequireComponent(typeof(Text))]
    public sealed class DoodlePowerCounter : MonoBehaviour
    {
        public GameNumber Displayed { get; private set; }
        public GameNumber Target { get; private set; }
        GameNumber from;
        Text label;
        string prefix, lastText;
        float elapsed;
        bool initialized;
        public void SetTarget(GameNumber value, string caption)
        {
            if (!label) label = GetComponent<Text>();
            prefix = caption;
            if (!initialized) { initialized = true; Displayed = Target = from = value; elapsed = .35f; }
            else if (Target != value) { from = Displayed; Target = value; elapsed = 0; }
            Write();
        }
        void Update()
        {
            if (!initialized || elapsed >= .35f) return;
            elapsed = Mathf.Min(.35f, elapsed + Time.unscaledDeltaTime);
            float t = elapsed / .35f; t = 1 - (1 - t) * (1 - t);
            Displayed = elapsed >= .35f ? Target : from + (Target - from) * t;
            Write();
        }
        void Write()
        {
            string next = prefix + UiNumber.Format(Displayed);
            if (lastText == next) return;
            lastText = next; label.text = next;
        }
    }
}
