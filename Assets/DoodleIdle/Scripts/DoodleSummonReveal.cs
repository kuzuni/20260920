using UnityEngine;

namespace DoodleIdle
{
    public sealed class DoodleSummonReveal : MonoBehaviour
    {
        CanvasGroup[] cards;
        float age;
        public int VisibleCards { get; private set; }
        public void Configure(Transform grid, bool skip)
        {
            cards = new CanvasGroup[grid.childCount];
            for (int i = 0; i < cards.Length; i++) {
                cards[i] = grid.GetChild(i).gameObject.AddComponent<CanvasGroup>();
                cards[i].alpha = 0; cards[i].interactable = cards[i].blocksRaycasts = false;
            }
            if (skip) Complete();
        }
        void Update()
        {
            if (cards == null) return;
            age += Time.unscaledDeltaTime;
            for (int i = 0; i < cards.Length; i++) {
                float t = Mathf.Clamp01((age - .14f - i * .045f) / .18f);
                cards[i].alpha = t; cards[i].transform.localScale = Vector3.one * Mathf.Lerp(.72f, 1, 1 - (1-t)*(1-t));
                cards[i].interactable = cards[i].blocksRaycasts = t >= 1;
                if (t >= 1) VisibleCards = i + 1;
            }
            if (VisibleCards == cards.Length) enabled = false;
        }
        public void Complete()
        {
            if (cards == null) return;
            foreach (var card in cards) { card.alpha = 1; card.transform.localScale = Vector3.one; card.interactable = card.blocksRaycasts = true; }
            VisibleCards = cards.Length; enabled = false;
        }
    }
}
