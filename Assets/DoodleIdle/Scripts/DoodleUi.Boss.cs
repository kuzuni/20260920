using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleUi
    {
        RectTransform bossHud, bossHealthFill, bossTimerFill;
        Text bossHealthLabel, bossTimerLabel;
        void BuildBossHud()
        {
            bossHud = UiKit.Rect(stageInfo, "Boss challenge HUD");
            bossHealthFill = BuildBossBar("Boss health bar", -20, new Color(.94f,.53f,.47f), out bossHealthLabel);
            bossTimerFill = BuildBossBar("Boss timer bar", -62, new Color(.48f,.77f,1), out bossTimerLabel);
            bossHud.gameObject.SetActive(false);
        }
        RectTransform BuildBossBar(string name, float y, Color color, out Text label)
        {
            var bar = UiKit.Rect(bossHud, name);
            var frame = bar.gameObject.AddComponent<Image>();
            frame.sprite = DoodleExpansionArt.Get("HealthBarFrame"); frame.raycastTarget = false;
            bar.anchorMin = new Vector2(0,1); bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(.5f,.5f); bar.sizeDelta = new Vector2(0,34); bar.anchoredPosition = new Vector2(0,y);
            var inner = UiKit.Rect(bar, "Fill area"); UiKit.Stretch(inner,9,6,9,6);
            var fill = UiKit.Rect(inner, name + " fill"); UiKit.Stretch(fill);
            var image = fill.gameObject.AddComponent<Image>(); image.sprite = DoodleExpansionArt.Get("BossGaugeFill");
            image.color = color; image.raycastTarget = false;
            label = UiKit.Text(bar, "", 22, TextAnchor.MiddleCenter, 30); UiKit.Stretch(label.rectTransform,4,0,4,0);
            label.raycastTarget = false;
            return fill;
        }
        void RefreshBossHud()
        {
            bool active = game && game.BossActive && MainBossPending && BreakthroughMode && ActiveDungeonIndex < 0;
            bossHud.gameObject.SetActive(active);
            stageInfo.sizeDelta = new Vector2(active ? 280 : 230, active ? 224 : 140);
            stageInfo.anchoredPosition = new Vector2(0, active ? -233 : -191);
            UiKit.Stretch(stageLabel.rectTransform,0,active ? 130 : 44,0,0);
            if (!active) return;
            bossHud.anchorMin = new Vector2(0,1); bossHud.anchorMax = Vector2.one; bossHud.pivot = new Vector2(.5f,1);
            bossHud.offsetMin = new Vector2(0,-180); bossHud.offsetMax = new Vector2(0,-96);
            bossHealthFill.anchorMax = new Vector2(game.BossHealthFraction,1);
            bossTimerFill.anchorMax = new Vector2(game.BossTimeRemaining / DoodleIdleGame.BossTimeLimit,1);
            bossHealthLabel.text = "보스 체력  " + (game.BossHealthFraction * 100).ToString("0") + "%";
            bossTimerLabel.text = "남은 시간  " + game.BossTimeRemaining.ToString("0.0") + "초";
        }
        public void FailBossChallenge(string reason)
        {
            if (services == null || ActiveDungeonIndex >= 0) return;
            CreditPendingFieldGold();
            services.breakthroughMode = true; services.mainStageKillProgress = 0;
            if (game) game.RequestCombatWaveReset();
            Save(); RefreshHud(); Toast("보스 도전 실패 · " + reason + " · 같은 스테이지에서 다시 시작합니다.");
        }
    }

    public sealed class DoodleBreakthroughPulse : MonoBehaviour
    {
        Image surface;
        bool active;
        float phase;
        public void SetActive(bool value)
        {
            if (!surface) surface=GetComponent<Image>();
            if(active!=value) { active=value; phase=0; }
            if(!active) surface.color=Color.gray;
        }
        void LateUpdate()
        {
            if(!active || !surface)return;
            phase+=Time.unscaledDeltaTime*3;
            surface.color=Color.Lerp(UiKit.Green,new Color(.91f,1,.66f),(.5f+.5f*Mathf.Sin(phase))*.7f);
        }
        void OnDisable() { if(surface)surface.color=active?UiKit.Green:Color.gray; }
    }
}
