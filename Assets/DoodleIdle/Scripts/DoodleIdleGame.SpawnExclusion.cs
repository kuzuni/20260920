using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public CircleCollider2D PlayerSpawnExclusion { get; private set; }
        float spawnBlockedUntil;

        void CreatePlayerSpawnExclusion(Transform playerRoot)
        {
            var prefab = Resources.Load<GameObject>("DoodleIdle/PlayerSpawnExclusion");
            var zone = prefab ? Instantiate(prefab, playerRoot, false) : new GameObject("PlayerSpawnExclusion");
            if (!prefab) zone.transform.SetParent(playerRoot, false);
            zone.name = "PlayerSpawnExclusion";
            PlayerSpawnExclusion = zone.GetComponent<CircleCollider2D>();
            if (!PlayerSpawnExclusion) {
                PlayerSpawnExclusion = zone.AddComponent<CircleCollider2D>();
                PlayerSpawnExclusion.radius = 4.5f;
            }
            // A shape for spawn selection only: no contacts, trigger callbacks or
            // overlap-query pollution. Its radius/offset/scale still define the zone.
            PlayerSpawnExclusion.isTrigger = true;
            PlayerSpawnExclusion.enabled = false;
            spawnBlockedUntil = 0;
        }

        bool InsidePlayerSpawnExclusion(Vector2 position, float enemyRadius)
        {
            var zone = PlayerSpawnExclusion;
            if (!zone) return (position - player.Position).sqrMagnitude < 10;
            // Rigidbody position is authoritative even before transform sync or when paused.
            Vector2 center = player.Position + (Vector2)(zone.transform.TransformPoint(zone.offset) - player.root.transform.position);
            Vector3 scale = zone.transform.lossyScale;
            float radius = zone.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) + enemyRadius;
            return (position - center).sqrMagnitude <= radius * radius;
        }

        bool TryFindEnemySpawn(Vector2 halfSize, float enemyRadius, out Vector2 position)
        {
            using var sample = spawnSearchMarker.Auto();
            position = default;
            bool hasSafeFallback = false;
            for (int attempt = 0; attempt < 600; attempt++) {
                var candidate = new Vector2(Random.Range(-halfSize.x, halfSize.x), Random.Range(-halfSize.y, halfSize.y));
                if (InsidePlayerSpawnExclusion(candidate, enemyRadius)) continue;
                position = candidate; hasSafeFallback = true;
                if (!SpawnPositionOccupied(candidate)) return true;
            }
            // Crowded waves may relax enemy spacing, but never player exclusion.
            if (hasSafeFallback) return true;
            // If the authored zone covers the usual spawn rectangle, use the arena
            // perimeter. A circle covering all four corners leaves no valid location.
            Vector2 limit = arenaHalfSize - Vector2.one * Mathf.Max(1, enemyRadius + .1f);
            for (int i = 0; i < 4; i++) {
                var corner = new Vector2((i & 1) == 0 ? -limit.x : limit.x, (i & 2) == 0 ? -limit.y : limit.y);
                if (InsidePlayerSpawnExclusion(corner, enemyRadius)) continue;
                position = corner; return true;
            }
            return false;
        }
    }
}
