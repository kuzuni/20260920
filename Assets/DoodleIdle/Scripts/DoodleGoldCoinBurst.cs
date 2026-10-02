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
        ParticleSystemRenderer particleRenderer;
        Sprite coinSprite;
        Material coinMaterial;
        Transform rendererRoot;
        readonly List<SpriteRenderer> coinRenderers = new List<SpriteRenderer>(64);
        int visibleCoins;
        int tick;
        void Awake() { Initialize(); }
        void Initialize()
        {
            if (!system) system = GetComponent<ParticleSystem>();
            if (!particleRenderer) particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (buffer == null || buffer.Length < system.main.maxParticles) buffer = new ParticleSystem.Particle[system.main.maxParticles];
        }
        public void PrepareSimulation()
        {
            Initialize(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); Clear();
            system.Simulate(0, false, true, false);
            var emission = system.emission; emission.enabled = false;
            // A particle renderer is one sorting unit, so it cannot interleave individual
            // coins with characters. Keep the authored particle simulation and pool sprites.
            particleRenderer.enabled = false;
            if (!coinSprite) {
                var inheritedPool = transform.Find("Gold Coin Render Pool");
                if (inheritedPool) {
                    inheritedPool.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(inheritedPool.gameObject); else DestroyImmediate(inheritedPool.gameObject);
                }
                rendererRoot = new GameObject("Gold Coin Render Pool").transform;
                rendererRoot.SetParent(transform, false); rendererRoot.gameObject.hideFlags = HideFlags.DontSave;
                var texture = (Texture2D)particleRenderer.sharedMaterial.mainTexture;
                coinSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f,
                    Mathf.Max(texture.width, texture.height), 0, SpriteMeshType.FullRect);
                coinSprite.name = "Gold coin sorting quad";
                // Unity 6 sprites pass renderer tint separately from particle vertex colors.
                coinMaterial = new Material(particleRenderer.sharedMaterial) {
                    shader = Resources.Load<Shader>("DoodleIdle/DoodleGoldCoinSprite"),
                    name = "Gold coin sprite material", hideFlags = HideFlags.DontSave
                };
            }
            EnsureRenderers(Mathf.Min(64, system.main.maxParticles));
        }
        static float RandomRange(ref uint seed, float min, float max)
        {
            seed = seed * 1664525u + 1013904223u;
            return Mathf.Lerp(min, max, (seed & 0x00ffffff) / 16777216f);
        }
        public int EmitBurst(Vector2 position, ref uint seed, bool refreshRenderers = true)
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
            // Gameplay can emit many deaths in one physics step, then sort/render once.
            // Editor previews retain their immediate first-frame feedback.
            if (refreshRenderers) RefreshRenderers(system.GetParticles(buffer));
            return count;
        }
        public void Simulate(float dt)
        {
            Initialize();
            if (system.particleCount == 0) { if (landings.Count > 0 || visibleCoins > 0) Clear(); return; }
            system.Simulate(dt, false, false, false); AfterSimulate(dt);
        }
        public void Clear()
        {
            landings.Clear();
            for (int i = 0; i < visibleCoins; i++) if (coinRenderers[i]) coinRenderers[i].enabled = false;
            visibleCoins = 0;
        }
        void EnsureRenderers(int count)
        {
            while (coinRenderers.Count < count) {
                var go = new GameObject("Y sorted gold coin"); go.transform.SetParent(rendererRoot, false);
                go.hideFlags = HideFlags.DontSave;
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = coinSprite; renderer.sharedMaterial = coinMaterial;
                renderer.sortingLayerID = particleRenderer.sortingLayerID; renderer.enabled = false;
                coinRenderers.Add(renderer);
            }
        }
        void RefreshRenderers(int count)
        {
            if (!coinSprite) return;
            EnsureRenderers(count);
            for (int i = 0; i < count; i++) {
                var coin = buffer[i]; var renderer = coinRenderers[i];
                renderer.transform.position = coin.position;
                renderer.transform.rotation = Quaternion.Euler(0, 0, -coin.rotation);
                var size = coin.GetCurrentSize3D(system); renderer.transform.localScale = new Vector3(size.x, size.y, 1);
                renderer.color = coin.GetCurrentColor(system);
                renderer.sortingOrder = DoodleIdleGame.Order(coin.position); // Same camera-relative Y rule as characters.
                renderer.enabled = true;
            }
            for (int i = count; i < visibleCoins; i++) coinRenderers[i].enabled = false;
            visibleCoins = count;
        }
        void OnDestroy()
        {
            if (Application.isPlaying) { Destroy(coinSprite); Destroy(coinMaterial); }
            else { DestroyImmediate(coinSprite); DestroyImmediate(coinMaterial); }
        }
        // The game owns simulation so pause/reset work exactly like the other pooled effects.
        // Each coin remembers its own ground height; overlapping kills share one particle simulation.
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
            RefreshRenderers(count);
            expired.Clear();
            foreach (var pair in landings) if (pair.Value.seen != tick) expired.Add(pair.Key);
            foreach (uint seed in expired) landings.Remove(seed);
        }
    }
}
