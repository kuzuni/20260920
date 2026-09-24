using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        readonly Dictionary<GameObject, System.Action> rewardCloseEffects = new Dictionary<GameObject, System.Action>();

        void FlyReward(Image source, string currency)
        {
            var destination = RewardFlightDestination(currency);
            if (!source || !destination) return;
            StartCoroutine(FlyRewardToWallet(source.sprite, source.rectTransform.TransformPoint(source.rectTransform.rect.center), destination, currency));
        }
        RectTransform RewardFlightDestination(string currency)
        {
            if (currency == "Gold") return walletGold ? walletGold.rectTransform : null;
            if (currency == "Diamond") return walletDiamond ? walletDiamond.rectTransform : null;
            if (string.IsNullOrEmpty(currency) || !(currency.StartsWith("Ticket", System.StringComparison.Ordinal) || currency == "DungeonRelicTicket")) return null;
            // Prefer the visible balance for this exact ticket. Reward cards and
            // mission reward labels are not wallets and must not absorb tickets.
            var sprite = UiKit.Art(currency);
            foreach (var icon in root.GetComponentsInChildren<Image>()) {
                if (icon.sprite != sprite || !icon.transform.parent || !RewardTargetVisible(icon.rectTransform)) continue;
                string parent = icon.transform.parent.name;
                if (parent == "Summon result tickets" || parent.StartsWith("Tickets: ", System.StringComparison.Ordinal)) return icon.rectTransform;
            }
            int shop = System.Array.IndexOf(pages, "Shop");
            return shop >= 0 && shop < navIcons.Count && navIcons[shop] ? navIcons[shop].rectTransform : null;
        }
        bool RewardTargetVisible(RectTransform target)
        {
            if (!target.gameObject.activeInHierarchy) return false;
            var camera = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
            if (!RectTransformUtility.RectangleContainsScreenPoint(root, point, camera)) return false;
            foreach (var mask in target.GetComponentsInParent<RectMask2D>())
                if (mask.isActiveAndEnabled && !RectTransformUtility.RectangleContainsScreenPoint(mask.rectTransform, point, camera)) return false;
            return true;
        }
        IEnumerator FlyRewardToWallet(Sprite sprite, Vector3 origin, RectTransform destination, string currency)
        {
            var layer = UiKit.Rect(root, "Reward flight " + currency); UiKit.Stretch(layer);
            var icons = new Image[8];
            for (int i = 0; i < icons.Length; i++) {
                var rect = UiKit.Rect(layer, "Flying " + currency); rect.sizeDelta = Vector2.one * 108;
                icons[i] = rect.gameObject.AddComponent<Image>(); icons[i].sprite = sprite;
                icons[i].preserveAspect = true; icons[i].raycastTarget = false; rect.position = origin;
            }
            float elapsed = 0;
            while (elapsed < 1.05f && destination) {
                elapsed += Time.unscaledDeltaTime;
                Vector3 end = destination.TransformPoint(destination.rect.center);
                for (int i = 0; i < icons.Length; i++) {
                    float t = Mathf.Clamp01((elapsed - i * .035f) / .7f);
                    float curved = t * t;
                    Vector3 scatter = root.TransformVector(new Vector3((i % 4 - 1.5f) * 55, -75 - i / 4 * 35));
                    icons[i].rectTransform.position = Vector3.Lerp(origin, end, curved) + scatter * Mathf.Sin(t * Mathf.PI);
                    icons[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1, .22f, t);
                    icons[i].color = new Color(1, 1, 1, t < .94f ? 1 : 1 - (t - .94f) / .06f);
                }
                yield return null;
            }
            if (destination) {
                var flash = UiKit.Rect(layer, "Wallet absorption glow"); flash.sizeDelta = Vector2.one * 52;
                var glow = flash.gameObject.AddComponent<Image>(); glow.sprite = UiKit.Circle; glow.raycastTarget = false;
                float time = 0;
                while (time < .2f && destination) {
                    time += Time.unscaledDeltaTime; float t = Mathf.Clamp01(time / .2f);
                    flash.position = destination.TransformPoint(destination.rect.center);
                    flash.localScale = Vector3.one * (1 + t * .6f); glow.color = new Color(1, .88f, .35f, (1 - t) * .45f);
                    yield return null;
                }
            }
            if (layer) Destroy(layer.gameObject);
        }
    }
}
