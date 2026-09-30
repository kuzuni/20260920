using UnityEngine;
using UnityEngine.UI;
namespace DoodleIdle
{
    public sealed class DoodleStatsPortraitLayout : MonoBehaviour
    {
        public DoodlePortraitSettings settings;
        public RectTransform portrait;
        public Text power;
        void LateUpdate() => Apply();
        public void Apply()
        {
            if (!settings || !portrait || !power) return;
            UiKit.Height(transform, Mathf.Max(settings.statsRowHeight, settings.statsPortraitSize));
            float total = settings.statsPortraitSize + settings.statsPowerGap + settings.statsPowerWidth;
            portrait.anchorMin = portrait.anchorMax = Vector2.one * .5f;
            portrait.sizeDelta = Vector2.one * settings.statsPortraitSize;
            portrait.anchoredPosition = settings.statsGroupOffset + Vector2.right * (-total + settings.statsPortraitSize) * .5f;
            power.rectTransform.anchorMin = power.rectTransform.anchorMax = Vector2.one * .5f;
            power.rectTransform.sizeDelta = new Vector2(settings.statsPowerWidth,108);
            power.rectTransform.anchoredPosition = settings.statsGroupOffset + Vector2.right * (total - settings.statsPowerWidth) * .5f;
            power.alignment = TextAnchor.MiddleLeft; power.resizeTextMaxSize = settings.statsPowerFontSize;
        }
    }
}
