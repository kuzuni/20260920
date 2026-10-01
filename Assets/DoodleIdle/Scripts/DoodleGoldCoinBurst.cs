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
        [Tooltip("방출 지점 기준 착지 높이 범위(월드 단위). X=최소 Y, Y=최대 Y. 음수는 아래, 양수는 위.")]
        public Vector2 landingYOffset = Vector2.zero;
        struct Landing { public float ground, rotation; public bool landed; public int seen; public Color32 color; }
        readonly Dictionary<uint, Landing> landings = new Dictionary<uint, Landing>(2048);
        readonly List<uint> expired = new List<uint>(2048);
        ParticleSystem system;
        ParticleSystem.Particle[] buffer;
        int tick;
        void Awake() { Initialize(); }
        void Initialize()
        {
            if (!system) system = GetComponent<ParticleSystem>();
            if (buffer == null || buffer.Length < system.main.maxParticles) buffer = new ParticleSystem.Particle[system.main.maxParticles];
        }
        public void PrepareSimulation()
        {
            Initialize(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); Clear();
            system.Simulate(0, false, true, false);
            var emission = system.emission; emission.enabled = false;
        }
        static float RandomRange(ref uint seed, float min, float max)
        {
            seed = seed * 1664525u + 1013904223u;
            return Mathf.Lerp(min, max, (seed & 0x00ffffff) / 16777216f);
        }
        public int EmitBurst(Vector2 position, ref uint seed)
        {
            Initialize();
            var emission = system.emission;
            if (emission.burstCount == 0) return 0;
            int count = Mathf.Max(0, Mathf.RoundToInt(emission.GetBurst(0).count.Evaluate(0, RandomRange(ref seed, 0, 1))));
            var main = system.main;
            for (int i = 0; i < count; i++) {
                float angle = Mathf.PI * .5f + RandomRange(ref seed, -launchSpread, launchSpread) * Mathf.Deg2Rad;
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * main.startSpeed.Evaluate(0, RandomRange(ref seed, 0, 1));
                float offset = RandomRange(ref seed, Mathf.Min(landingYOffset.x, landingYOffset.y), Mathf.Max(landingYOffset.x, landingYOffset.y));
                // A landing plane above the launch point must be reached before descending onto it.
                if (offset > 0) velocity.y = Mathf.Max(velocity.y, Mathf.Sqrt(2 * Mathf.Max(.1f, gravity) * offset) + .25f);
                uint id = ++seed;
                system.Emit(new ParticleSystem.EmitParams { position = position, velocity = velocity, randomSeed = id, applyShapeToPosition = false }, 1);
                landings[id] = new Landing { ground = position.y + offset };
            }
            return count;
        }
        public void Simulate(float dt)
        {
            Initialize(); system.Simulate(dt, false, false, false); AfterSimulate(dt);
        }
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
