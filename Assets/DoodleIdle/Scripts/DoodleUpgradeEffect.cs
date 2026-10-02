using UnityEngine;
namespace DoodleIdle
{
    public sealed class DoodleUpgradeEffect : MonoBehaviour
    {
        public DoodleSpawnPortal portal;
        public ParticleSystem glow;
        public float duration = 1.1f;
        float remaining;
        public void Play(Vector3 position, float width)
        {
            transform.position = position; transform.localScale = Vector3.one * width;
            gameObject.SetActive(true); remaining = duration;
            portal.Begin(position, 1, duration);
            glow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            glow.Play();
        }
        void Update()
        {
            remaining -= Time.unscaledDeltaTime;
            portal.Simulate(Time.unscaledDeltaTime);
            var color = portal.art.color; color.r = 1; color.g = .88f; color.b = .3f; portal.art.color = color;
            if (remaining <= 0) gameObject.SetActive(false);
        }
    }
}
