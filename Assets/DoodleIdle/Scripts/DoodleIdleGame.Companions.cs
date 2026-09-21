using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public bool companionsEnabled = true;
        sealed class CompanionActor
        {
            public UiItem item; public SpriteRenderer art; public float clock, frameClock, shotClock;
            public int pending, shotIndex; public bool facingLeft;
        }
        sealed class CompanionShot
        {
            public SpriteRenderer art; public string owner; public Vector2 start, end, direction;
            public float age, duration, speed, damage, explosionRadius; public bool arc;
        }
        sealed class CompanionBurst { public SpriteRenderer art; public Vector2 start, direction; public float age, radius; }
        readonly Dictionary<string, CompanionActor> companions = new Dictionary<string, CompanionActor>();
        readonly Dictionary<string, Sprite> companionSprites = new Dictionary<string, Sprite>();
        readonly List<CompanionShot> companionShots = new List<CompanionShot>();
        readonly List<CompanionBurst> companionBursts = new List<CompanionBurst>();
        readonly Dictionary<string, int> companionShotCounts = new Dictionary<string, int>();
        public int ActiveCompanions => companions.Count;
        public int CompanionAttacks { get; private set; }
        public int CompanionShotsLaunched { get; private set; }
        public int CompanionExplosions { get; private set; }
        public int CompanionHits { get; private set; }
        public event System.Action<string, float, bool, float> CompanionShotLaunched;
        public int CompanionShotCount(string id) => companionShotCounts.TryGetValue(id, out var n) ? n : 0;
        Sprite WorldIcon(string key)
        {
            var collection = DoodleCollectionArt.Get(key); if (collection) return collection;
            var variant = DoodleVariantArt.Get(key); if (variant) return variant;
            if (companionSprites.TryGetValue(key, out var cached)) return cached;
            var source = UiKit.Art(key); var rect = source.rect;
            var sprite = Sprite.Create(source.texture, rect, Vector2.one * .5f, Mathf.Max(rect.width, rect.height)); sprite.name = key;
            companionSprites[key] = sprite; return sprite;
        }
        void TickCompanions(float dt)
        {
            var equipped = Ui.EquippedCompanions;
            var removed = new List<string>();
            foreach (var pair in companions) if (!companionsEnabled || !equipped.Exists(x => x.id == pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) { Destroy(companions[id].art.gameObject); companions.Remove(id); }
            if (companionsEnabled) for (int i = 0; i < equipped.Count; i++)
            {
                var item = equipped[i];
                if (!companions.TryGetValue(item.id, out var companion))
                {
                    companion = new CompanionActor { item = item, clock = .5f + i * .15f,
                        art = Visual("Companion: " + item.id, WorldIcon(item.icon), player.Position, Vector2.one * .95f, 450) };
                    companions[item.id] = companion;
                }
                int index = DoodleCollectionArt.CompanionIndex(item.icon);
                float angle = i * Mathf.PI * 2 / Mathf.Max(1, equipped.Count) + Mathf.PI * .5f;
                Vector2 home = player.Position + Direction(angle) * 1.75f;
                Vector2 old = companion.art.transform.position;
                companion.art.transform.position = Vector2.Lerp(old, home, 1 - Mathf.Exp(-dt * 14));
                if (Mathf.Abs(home.x - old.x) > .01f) companion.facingLeft = home.x < old.x;
                companion.frameClock += dt;
                var frame = DoodleCollectionArt.CompanionFrame(index, (int)(companion.frameClock * 6) % 2);
                if (companion.art.sprite != frame) SetSpriteArt(companion.art, frame);
                companion.art.flipX = companion.facingLeft;
                companion.art.sortingOrder = Order(companion.art.transform.position) + 2;
                companion.clock -= dt; companion.shotClock -= dt;
                if (companion.pending > 0 && companion.shotClock <= .0001f)
                {
                    FireCompanionShot(companion, companion.shotIndex++, false);
                    companion.pending--; companion.shotClock += item.volleyGap;
                }
                Vector2 origin = companion.art.transform.position;
                if (companion.clock > 0 || companion.pending > 0 || InRange(origin, 12) == null) continue;
                CompanionAttacks++; companion.clock = item.attackInterval;
                if (item.volleyGap > 0)
                {
                    companion.pending = item.volleyCount - 1; companion.shotIndex = 1; companion.shotClock = item.volleyGap;
                    FireCompanionShot(companion, 0, false);
                }
                else for (int n = 0; n < item.volleyCount; n++) FireCompanionShot(companion, n, item.volleyCount > 1);
            }
            TickCompanionShots(dt);
        }
        void FireCompanionShot(CompanionActor companion, int shotIndex, bool spread)
        {
            var item = companion.item; Vector2 origin = companion.art.transform.position;
            var target = NearbyTarget(origin, shotIndex); if (!Alive(target)) return;
            var direction = (target.Position - origin).normalized;
            if (spread) direction = Rotate(direction, (shotIndex - (item.volleyCount - 1) * .5f) * 10);
            companion.facingLeft = direction.x < 0; companion.art.flipX = companion.facingLeft;
            float damage = (12 + item.rarity * 4) * (1 + Mathf.Max(0, item.level - 1) * .03f);
            var sprite = WorldIcon(item.projectile);
            if (item.trajectory == "Lightning")
            {
                Vector2 start = target.Position + Vector2.up * 2.7f;
                Echo("Companion cloud lightning", sprite, target.Position + Vector2.up * 1.35f,
                    new Vector2(2.7f, .7f), Aim(Vector2.down), .22f, 1, 570);
                CompanionDamage(target, damage, Vector2.down);
                if (item.explosionRadius > 0) CompanionExplosion(item.id, sprite, target.Position, item.explosionRadius, damage, target);
            }
            else
            {
                bool arc = item.trajectory == "Arc";
                var art = Visual("Companion shot: " + item.id, sprite, origin, Vector2.one * (item.rarity >= 4 ? .7f : .5f), 515);
                art.transform.rotation = Aim(direction);
                companionShots.Add(new CompanionShot { owner = item.id, art = art, start = origin,
                    end = target.Position, direction = direction, arc = arc, duration = arc ? .7f + shotIndex * .025f : 2,
                    speed = item.projectileSpeed, damage = damage, explosionRadius = item.explosionRadius });
            }
            CompanionShotsLaunched++; companionShotCounts[item.id] = CompanionShotCount(item.id) + 1;
            CompanionShotLaunched?.Invoke(item.id, Time.fixedTime, item.trajectory == "Arc", item.explosionRadius);
        }
        void CompanionDamage(Actor enemy, float damage, Vector2 direction)
        { if (Alive(enemy)) { CompanionHits++; Damage(enemy, damage, direction); } }
        void TickCompanionShots(float dt)
        {
            for (int i = companionShots.Count - 1; i >= 0; i--)
            {
                var shot = companionShots[i]; shot.age += dt; Vector2 old = shot.art.transform.position;
                float t = Mathf.Clamp01(shot.age / shot.duration);
                Vector2 next = shot.arc ? Vector2.Lerp(shot.start, shot.end, t) + Vector2.up * (8 * t * (1 - t))
                    : old + shot.direction * (shot.speed * dt);
                shot.art.transform.position = next;
                if (shot.arc && (next - old).sqrMagnitude > .0001f) shot.art.transform.rotation = Aim(next - old);
                Actor victim = null;
                if (!shot.arc || t >= 1) foreach (var enemy in enemies)
                    if ((shot.arc ? Vector2.Distance(enemy.Position, next) : SegmentDistance(enemy.Position, old, next)) <= .78f) { victim = enemy; break; }
                bool landed = shot.arc && t >= 1;
                if (victim != null || landed)
                {
                    if (victim != null) CompanionDamage(victim, shot.damage, shot.direction);
                    if (shot.explosionRadius > 0) CompanionExplosion(shot.owner, shot.art.sprite, next, shot.explosionRadius, shot.damage, victim);
                }
                if (victim != null || landed || shot.age >= shot.duration) { Destroy(shot.art.gameObject); companionShots.RemoveAt(i); }
            }
            for (int i = companionBursts.Count - 1; i >= 0; i--)
            {
                var burst = companionBursts[i]; burst.age += dt; float t = burst.age / .3f;
                burst.art.transform.position = burst.start + burst.direction * burst.radius * t;
                burst.art.transform.localScale = Vector3.one * Mathf.Lerp(.5f, .12f, t);
                burst.art.color = new Color(1, 1, 1, Mathf.Clamp01(1 - t));
                if (t >= 1) { Destroy(burst.art.gameObject); companionBursts.RemoveAt(i); }
            }
        }
        void CompanionExplosion(string id, Sprite art, Vector2 position, float radius, float damage, Actor directVictim)
        {
            CompanionExplosions++;
            // Every fragment uses this companion's new attack art, not an unrelated skill effect.
            for (int i = 0; i < 8; i++)
            {
                Vector2 direction = Direction(i * Mathf.PI / 4);
                var fragment = Visual("Companion explosion: " + id, art, position, Vector2.one * .5f, 565);
                fragment.transform.rotation = Aim(direction);
                companionBursts.Add(new CompanionBurst { art = fragment, start = position, direction = direction, radius = radius });
            }
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (enemy != directVictim && Vector2.Distance(enemy.Position, position) <= radius + .56f)
                    CompanionDamage(enemy, damage, (enemy.Position - position).normalized);
            }
        }
        void ClearCompanions()
        {
            foreach (var companion in companions.Values) if (companion.art) Destroy(companion.art.gameObject);
            foreach (var shot in companionShots) if (shot.art) Destroy(shot.art.gameObject);
            foreach (var burst in companionBursts) if (burst.art) Destroy(burst.art.gameObject);
            companions.Clear(); companionShots.Clear(); companionBursts.Clear(); companionShotCounts.Clear();
            CompanionAttacks = CompanionShotsLaunched = CompanionExplosions = CompanionHits = 0;
        }
        void DisposeCompanionArt() { foreach (var sprite in companionSprites.Values) if (sprite) Destroy(sprite); companionSprites.Clear(); }
    }
}
