using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        [Header("Overhead HP bar position")]
        [Tooltip("플레이어 머리 위 HP바 위치 (캐릭터 기준). 실행 중에도 조절 가능합니다.")]
        public Vector2 playerHealthBarOffset = new Vector2(0, 1);
        [Tooltip("일반 적 머리 위 HP바 위치. 보스는 머리 위 HP바를 표시하지 않습니다.")]
        public Vector2 enemyHealthBarOffset = new Vector2(0, 1);
        const float EnemyMaxHealth = 68;
        const int MaxDamageNumbers = 128;
        Transform damageCanvas;
        readonly List<DamageNumber> damageNumbers = new List<DamageNumber>();
        readonly Stack<DamageNumber> spareDamageNumbers = new Stack<DamageNumber>();
        sealed class DamageNumber {
            public Text text; public RectTransform rect; public CanvasRenderer renderer;
            public Vector2 origin; public float age, drift, alpha = 1;
        }
        sealed class HealthBarState {
            public Transform back, fill;
            public GameNumber hp, maxHp;
            public Sprite sprite;
            public float fraction, height;
            public bool valid;
        }
        public int ActiveDamageNumbers => damageNumbers.Count;

        void BuildCombatFeedback()
        {
            var go = new GameObject("World damage numbers", typeof(Canvas));
            damageCanvas = go.transform; damageCanvas.SetParent(world, false);
            damageCanvas.localScale = Vector3.one * .01f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = gameCamera;
            canvas.sortingOrder = 1000;
        }
        void AddHealthBar(Actor actor)
        {
            var frame = summonArt["HealthBarFrame"]; var fill = summonArt["HealthBarFill"];
            Vector2 offset = actor.isPlayer ? playerHealthBarOffset : enemyHealthBarOffset;
            actor.healthBack = Visual("Enemy HP background", frame, actor.Position + offset, new Vector2(.98f, .14f / frame.bounds.size.y), Order(actor.Position) + 3);
            actor.healthBack.transform.SetParent(actor.root.transform, true);
            actor.healthFill = Visual("Enemy HP fill", fill, actor.Position + offset, new Vector2(.9f, .08f / fill.bounds.size.y), Order(actor.Position) + 4);
            actor.healthFill.transform.SetParent(actor.root.transform, true);
            actor.healthState = null;
            RefreshHealthBar(actor);
        }
        void RefreshHealthBar(Actor actor)
        {
            if (!actor.healthFill) return;
            bool visible = !actor.isBoss && (actor.hp < actor.maxHp || (actor.isPlayer && PlayerInvulnerable));
            if (actor.healthBack.enabled != visible) actor.healthBack.enabled = visible;
            if (actor.healthFill.enabled != visible) actor.healthFill.enabled = visible;
            var state = actor.healthState;
            if (state == null) actor.healthState = state = new HealthBarState { back = actor.healthBack.transform, fill = actor.healthFill.transform };
            Vector2 offset = actor.isPlayer ? playerHealthBarOffset : enemyHealthBarOffset;
            var backPosition = (Vector3)offset;
            if (!state.back.localPosition.Equals(backPosition)) state.back.localPosition = backPosition;
            if (!visible) {
                // Inspector placement remains live even while the full-health bar is hidden.
                if (!state.fill.localPosition.Equals(backPosition)) state.fill.localPosition = backPosition;
                return;
            }
            bool changed = !state.valid || state.hp != actor.hp || state.maxHp != actor.maxHp;
            if (changed) {
                state.hp = actor.hp; state.maxHp = actor.maxHp;
                state.fraction = (float)GameNumber.Clamp(actor.hp / actor.maxHp, 0, 1); state.valid = true;
                actor.healthFill.color = Color.Lerp(new Color(1, .45f, .45f), Color.white, state.fraction);
            }
            if (state.sprite != actor.healthFill.sprite) { state.sprite = actor.healthFill.sprite; state.height = .08f / state.sprite.bounds.size.y; }
            var scale = new Vector3(.9f * state.fraction, state.height, 1);
            var position = new Vector3(offset.x - .45f * (1 - state.fraction), offset.y, 0);
            if (!state.fill.localScale.Equals(scale)) state.fill.localScale = scale;
            if (!state.fill.localPosition.Equals(position)) state.fill.localPosition = position;
            int order = Order(actor.Position);
            if (actor.healthBack.sortingOrder != order + 3) actor.healthBack.sortingOrder = order + 3;
            if (actor.healthFill.sortingOrder != order + 4) actor.healthFill.sortingOrder = order + 4;
        }
        DamageNumber CreateDamageNumber()
        {
            var go = new GameObject("Enemy damage number", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(damageCanvas, false);
            var text = go.GetComponent<Text>();
            text.font = uiFont; text.fontSize = 84; text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(360, 180);
            var outline = go.GetComponent<Outline>(); outline.effectColor = new Color(.13f, .08f, .06f, .95f); outline.effectDistance = new Vector2(4, -4);
            return new DamageNumber { text = text, rect = text.rectTransform, renderer = text.canvasRenderer };
        }
        void ShowDamageNumber(Vector2 position, GameNumber amount, bool playerHit = false)
        {
            DamageNumber number;
            if (damageNumbers.Count >= MaxDamageNumbers)
            {
                // Replacing the oldest visible number does not need an unregister /
                // register cycle in the Canvas or a different place in draw order.
                number = damageNumbers[0]; damageNumbers.RemoveAt(0);
            }
            else if (spareDamageNumbers.Count > 0) number = spareDamageNumbers.Pop();
            else number = CreateDamageNumber();
            number.origin = position + new Vector2(ParticleRandom(-.25f, .25f), 1.05f);
            number.age = 0; number.drift = ParticleRandom(-.45f, .45f);
            number.text.text = UiNumber.Format(GameNumber.Ceiling(amount));
            number.text.name = playerHit ? "Player damage number" : "Enemy damage number";
            number.text.color = playerHit ? new Color(.62f, .62f, .62f) : new Color(1, .96f, .76f);
            number.rect.localPosition = number.origin * 100;
            number.rect.localScale = Vector3.one;
            number.alpha = 1; number.renderer.SetAlpha(1);
            if (!number.text.gameObject.activeSelf) number.text.gameObject.SetActive(true);
            damageNumbers.Add(number);
        }
        void TickDamageNumbers(float dt)
        {
            for (int i = damageNumbers.Count - 1; i >= 0; i--)
            {
                var number = damageNumbers[i]; number.age += dt;
                if (number.age >= .75f)
                {
                    number.text.gameObject.SetActive(false); spareDamageNumbers.Push(number); damageNumbers.RemoveAt(i); continue;
                }
                number.rect.localPosition = (number.origin + new Vector2(number.drift * number.age, number.age * 1.05f)) * 100;
                var scale = Vector3.one * (1 + .15f * Mathf.Sin(Mathf.Clamp01(number.age / .2f) * Mathf.PI));
                if (!number.rect.localScale.Equals(scale)) number.rect.localScale = scale;
                float alpha = Mathf.Clamp01((.75f - number.age) / .25f);
                // Fade the already generated glyphs and outline together. Text.color
                // would regenerate every outlined glyph mesh on each fading tick.
                if (number.alpha != alpha) { number.alpha = alpha; number.renderer.SetAlpha(alpha); }
            }
        }
        void ClearDamageNumbers()
        {
            foreach (var number in damageNumbers) { number.text.gameObject.SetActive(false); spareDamageNumbers.Push(number); }
            damageNumbers.Clear();
        }
    }
}
