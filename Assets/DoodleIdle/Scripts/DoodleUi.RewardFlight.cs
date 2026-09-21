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
            Text wallet = currency == "Gold" ? walletGold : currency == "Diamond" ? walletDiamond : null;
            if (!source || !wallet) return;
            StartCoroutine(FlyRewardToWallet(source.sprite, source.rectTransform.TransformPoint(source.rectTransform.rect.center), wallet.rectTransform, currency));
        }
        IEnumerator FlyRewardToWallet(Sprite sprite, Vector3 origin, RectTransform destination, string currency)
        {
            var layer = UiKit.Rect(root, "Reward flight " + currency); UiKit.Stretch(layer);
            var icons = new Image[8];
            for (int i = 0; i < icons.Length; i++) {
                var rect = UiKit.Rect(layer, "Flying " + currency); rect.sizeDelta = Vector2.one * 36;
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
