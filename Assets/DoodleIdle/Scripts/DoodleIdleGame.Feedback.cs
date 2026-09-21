using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        const float EnemyMaxHealth = 68;
        const int MaxDamageNumbers = 128;
        Transform damageCanvas;
        readonly List<DamageNumber> damageNumbers = new List<DamageNumber>();
        readonly Stack<DamageNumber> spareDamageNumbers = new Stack<DamageNumber>();
        sealed class DamageNumber { public Text text; public Vector2 origin; public float age, drift; }
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
            actor.healthBack = Visual("Enemy HP background", frame, actor.Position + Vector2.up * .78f, new Vector2(.98f, .14f / frame.bounds.size.y), Order(actor.Position) + 3);
            actor.healthBack.transform.SetParent(actor.root.transform, true);
            actor.healthFill = Visual("Enemy HP fill", fill, actor.Position + Vector2.up * .78f, new Vector2(.9f, .08f / fill.bounds.size.y), Order(actor.Position) + 4);
            actor.healthFill.transform.SetParent(actor.root.transform, true);
            RefreshHealthBar(actor);
        }
        void RefreshHealthBar(Actor actor)
        {
            if (!actor.healthFill) return;
            float fraction = Mathf.Clamp01(actor.hp / EnemyMaxHealth);
            actor.healthFill.transform.localScale = new Vector3(.9f * fraction, .08f / actor.healthFill.sprite.bounds.size.y, 1);
            actor.healthFill.transform.localPosition = new Vector3(-.45f * (1 - fraction), .78f, 0);
            actor.healthFill.color = Color.Lerp(new Color(1, .45f, .45f), Color.white, fraction);
            actor.healthBack.sortingOrder = Order(actor.Position) + 3;
            actor.healthFill.sortingOrder = Order(actor.Position) + 4;
        }
        void ShowDamageNumber(Vector2 position, float amount)
        {
            if (damageNumbers.Count >= MaxDamageNumbers)
            {
                var oldest = damageNumbers[0]; oldest.text.gameObject.SetActive(false);
                spareDamageNumbers.Push(oldest); damageNumbers.RemoveAt(0);
            }
            DamageNumber number;
            if (spareDamageNumbers.Count > 0) number = spareDamageNumbers.Pop();
            else
            {
                var go = new GameObject("Enemy damage number", typeof(RectTransform), typeof(Text), typeof(Outline));
                go.transform.SetParent(damageCanvas, false);
                var text = go.GetComponent<Text>();
                text.font = uiFont; text.fontSize = 84; text.fontStyle = FontStyle.Normal;
                text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
                text.rectTransform.sizeDelta = new Vector2(360, 180);
                var outline = go.GetComponent<Outline>(); outline.effectColor = new Color(.13f, .08f, .06f, .95f); outline.effectDistance = new Vector2(4, -4);
                number = new DamageNumber { text = text };
            }
            number.origin = position + new Vector2(ParticleRandom(-.25f, .25f), 1.05f);
            number.age = 0; number.drift = ParticleRandom(-.45f, .45f);
            number.text.text = UiNumber.Format(System.Math.Ceiling(amount));
            number.text.color = new Color(1, .96f, .76f);
            number.text.rectTransform.localPosition = number.origin * 100;
            number.text.rectTransform.localScale = Vector3.one;
            number.text.gameObject.SetActive(true); damageNumbers.Add(number);
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
                number.text.rectTransform.localPosition = (number.origin + new Vector2(number.drift * number.age, number.age * 1.05f)) * 100;
                number.text.rectTransform.localScale = Vector3.one * (1 + .15f * Mathf.Sin(Mathf.Clamp01(number.age / .2f) * Mathf.PI));
                number.text.color = new Color(1, .96f, .76f, Mathf.Clamp01((.75f - number.age) / .25f));
            }
        }
        void ClearDamageNumbers()
        {
            foreach (var number in damageNumbers) { number.text.gameObject.SetActive(false); spareDamageNumbers.Push(number); }
            damageNumbers.Clear();
        }
    }
}
