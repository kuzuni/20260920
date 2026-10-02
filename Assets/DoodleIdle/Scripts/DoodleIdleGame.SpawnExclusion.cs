using UnityEngine;
using DoodleIdle.CharacterRigs;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public CircleCollider2D PlayerSpawnExclusion { get; private set; }
        public CircleCollider2D PlayerSpawnBoundary { get; private set; }
        float spawnBlockedUntil;
        bool spawnWaveIncomplete;
        [InspectorName("적 포위 소환 선호 반경"), Min(0)] public float surroundSpawnRadius = 6.5f;

        bool TryFindSurroundSpawn(int index, int count, float rotation, float enemyRadius, out Vector2 position)
        {
            using var sample = spawnSearchMarker.Auto();
            position = default;
            Vector2 center = ZoneCenter(PlayerSpawnBoundary);
            float outer = ZoneRadius(PlayerSpawnBoundary) - enemyRadius;
            float inner = Mathf.Max(0, ZoneRadius(PlayerSpawnExclusion) + enemyRadius - Vector2.Distance(center, ZoneCenter(PlayerSpawnExclusion)));
            if (outer <= inner) return false;
            float preferred = Mathf.Clamp(surroundSpawnRadius, inner + .001f, outer);
            int availableRings = Mathf.Max(1, Mathf.FloorToInt((outer - preferred) / 1.4f) + 1);
            int capacity = Mathf.Max(8, Mathf.FloorToInt(2 * Mathf.PI * preferred / 1.4f));
            int rings = Mathf.Min(availableRings, Mathf.Max(1, Mathf.CeilToInt(count / (float)capacity)));
            float angle = rotation + index * (2 * Mathf.PI / Mathf.Max(1, count));
            for (int attempt = 0; attempt < 96; attempt++) {
                float candidateAngle = angle + (attempt < 24 ? (attempt / 4) * .055f : (attempt - 23) * 2.399963f);
                float radius = preferred + ((index + attempt) % rings) * 1.4f + (attempt / 32) * 1.4f;
                if (radius > outer) continue;
                var candidate = center + new Vector2(Mathf.Cos(candidateAngle), Mathf.Sin(candidateAngle)) * radius;
                if (!ValidSpawnPosition(candidate, enemyRadius) || SpawnPositionOccupied(candidate)) continue;
                position = candidate; return true;
            }
            // A bounded deterministic ring search fills open arcs without ever crossing A/B.
            for (float radius = inner + .001f; radius <= outer; radius += 1.3f) {
                int slots = Mathf.Max(8, Mathf.FloorToInt(2 * Mathf.PI * radius / 1.4f));
                for (int slot = 0; slot < slots; slot++) {
                    float candidateAngle = rotation + slot * (2 * Mathf.PI / slots);
                    var candidate = center + new Vector2(Mathf.Cos(candidateAngle), Mathf.Sin(candidateAngle)) * radius;
                    if (!ValidSpawnPosition(candidate, enemyRadius) || SpawnPositionOccupied(candidate)) continue;
                    position = candidate; return true;
                }
            }
            return false;
        }

        bool ValidSpawnPosition(Vector2 position, float enemyRadius)
        {
            if (InsidePlayerSpawnExclusion(position, enemyRadius)) return false;
            float radius = ZoneRadius(PlayerSpawnBoundary) - enemyRadius;
            if (radius <= 0 || (position - ZoneCenter(PlayerSpawnBoundary)).sqrMagnitude > radius * radius) return false;
            if (!endlessWorld) {
                var limit = arenaHalfSize - Vector2.one * Mathf.Max(1, enemyRadius + .1f);
                if (Mathf.Abs(position.x) > limit.x || Mathf.Abs(position.y) > limit.y) return false;
            }
            return true;
        }

        void CreatePlayerSpawnExclusion(Transform playerRoot)
        {
            var zones = playerRoot.GetComponentInChildren<CharacterSpawnZones>(true);
            if (!zones) {
                var anchor = new GameObject("SpawnZones"); zones = anchor.AddComponent<CharacterSpawnZones>();
                var old = Resources.Load<CircleCollider2D>("DoodleIdle/PlayerSpawnExclusion");
                zones.exclusion = old ? Instantiate(old, anchor.transform, false) : new GameObject("A_NoSpawn").AddComponent<CircleCollider2D>();
                zones.exclusion.transform.SetParent(anchor.transform, false);
                if (!old) zones.exclusion.radius = 4.5f;
                zones.boundary = new GameObject("B_SpawnBoundary").AddComponent<CircleCollider2D>();
                zones.boundary.transform.SetParent(anchor.transform, false); zones.boundary.radius = 16;
            }
            zones.transform.SetParent(playerRoot, false);
            zones.transform.localPosition = Vector3.zero; zones.transform.localRotation = Quaternion.identity;
            zones.transform.localScale = Vector3.one; zones.ConfigureTriggers();
            PlayerSpawnExclusion = zones.exclusion; PlayerSpawnBoundary = zones.boundary;
            spawnBlockedUntil = 0; spawnWaveIncomplete = false;
        }

        bool InsidePlayerSpawnExclusion(Vector2 position, float enemyRadius)
        {
            float radius = ZoneRadius(PlayerSpawnExclusion) + enemyRadius;
            return (position - ZoneCenter(PlayerSpawnExclusion)).sqrMagnitude <= radius * radius;
        }
    }
}
