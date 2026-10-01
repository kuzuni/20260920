using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class DoodleGoldCoinBurst : MonoBehaviour
    {
        [Tooltip("위쪽 기준 좌우로 퍼지는 각도")]
        [Range(0, 85)] public float launchSpread = 40;
        [Tooltip("떨어지는 가속도. 클수록 낮고 빠르게 착지합니다.")]
        [Min(.1f)] public float gravity = 18;
        [Tooltip("바닥에 착지한 후 머무르며 사라지는 시간")]
        [Min(.01f)] public float landedLifetime = .35f;
        struct Landing { public float ground, rotation; public bool landed; public int seen; public Color32 color; }
        readonly Dictionary<uint, Landing> landings = new Dictionary<uint, Landing>(2048);
        readonly List<uint> expired = new List<uint>(2048);
        ParticleSystem system;
        ParticleSystem.Particle[] buffer;
        int tick;
        void Awake() { system = GetComponent<ParticleSystem>(); buffer = new ParticleSystem.Particle[system.main.maxParticles]; }
        public void Track(uint seed, float ground) { landings[seed] = new Landing { ground = ground }; }
        public void Clear() { landings.Clear(); }
        // The game owns simulation so pause/reset work exactly like the other pooled effects.
        // Each coin remembers its own ground height; overlapping kills share one renderer.
        public void AfterSimulate(float dt)
        {
            if (landings.Count == 0) return;
            int count = system.GetParticles(buffer); tick++;
            for (int i = 0; i < count; i++) {
                var coin = buffer[i];
                if (!landings.TryGetValue(coin.randomSeed, out var state)) continue;
                state.seen = tick;
                if (!state.landed && coin.velocity.y <= 0 && coin.position.y <= state.ground) {
                    state.landed = true; state.rotation = coin.rotation; state.color = coin.startColor;
                    coin.startLifetime = coin.remainingLifetime = landedLifetime;
                }
                if (state.landed) {
                    var position = coin.position; position.y = state.ground; coin.position = position;
                    coin.velocity = Vector3.zero; coin.rotation = state.rotation;
                    var color = state.color; color.a = (byte)(state.color.a * Mathf.Clamp01(coin.remainingLifetime / landedLifetime));
                    coin.startColor = color;
                } else coin.velocity += Vector3.down * gravity * dt;
                buffer[i] = coin; landings[coin.randomSeed] = state;
            }
            system.SetParticles(buffer, count);
            expired.Clear();
            foreach (var pair in landings) if (pair.Value.seen != tick) expired.Add(pair.Key);
            foreach (uint seed in expired) landings.Remove(seed);
        }
    }
}
