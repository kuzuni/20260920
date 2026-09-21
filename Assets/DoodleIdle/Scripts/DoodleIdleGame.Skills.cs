using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public enum ExtraSkill { Arrows, BouncyBall, Fire, Drone, Worm }
        [Header("Extra automatic skills")]
        public bool extraSkillsEnabled = true;
        public float arrowInterval = 4.5f, ballInterval = 7f, fireInterval = 5.5f;
        public float droneInterval = 6f, wormInterval = 8f;
        public int ArrowsLaunched { get; private set; }
        public int ArrowHits { get; private set; }
        public int BallHits { get; private set; }
        public int BallsCompleted { get; private set; }
        public int LastCompletedBallHits { get; private set; }
        public int FireballsLaunched { get; private set; }
        public int LastFireTargetCount { get; private set; }
        public int FireHits { get; private set; }
        public int MissilesLaunched { get; private set; }
        public int MissileHits { get; private set; }
        public int WormCasts { get; private set; }
        public int WormHits { get; private set; }
        public int ActiveExtraProjectiles => extraShots.Count;
        public event Action<ExtraSkill, float, int> SkillProjectileLaunched;
        public event Action<int, int> BallEnemyHit;

        enum ProjectileKind { Arrow, Ball, Fire, Missile }
        sealed class ExtraShot
        {
            public ProjectileKind kind;
            public SpriteRenderer art;
            public Actor target, previous;
            public Vector2 start, end;
            public float age, duration, trail;
            public int hits;
        }
        sealed class Worm
        {
            public Vector2 origin;
            public float age, angle;
            public readonly List<Vector2> path = new List<Vector2>();
            public readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
            public readonly Dictionary<Actor, float> nextHit = new Dictionary<Actor, float>();
        }
        readonly List<ExtraShot> extraShots = new List<ExtraShot>();
        readonly List<Worm> worms = new List<Worm>();
        readonly List<Actor> skillTargets = new List<Actor>();
        readonly Sprite[] skillArt = new Sprite[7];

        Transform drone;
        Sprite droneFrameB;
        float arrowClock, ballClock, fireClock, droneClock, wormClock;
        float arrowShotClock, missileShotClock;
        int arrowsPending, missilesPending, arrowIndex, missileIndex;

        void LoadSkillArt()
        {
            string[] names = { "Arrow", "BouncyBall", "Fireball", "RobotDroneA", "Missile", "WormHead", "WormSegment" };
            for (int i = 0; i < names.Length; i++)
            {
                var texture = Resources.Load<Texture2D>("DoodleIdle/" + names[i]);
                if (!texture) throw new InvalidOperationException("Missing generated skill art: " + names[i]);
                var pixels = texture.GetPixels32();
                int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
                for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 32)
                    { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                if (maxX < minX) throw new InvalidOperationException("Empty generated skill art: " + names[i]);
                int width = maxX - minX + 1, height = maxY - minY + 1;
                skillArt[i] = Sprite.Create(texture, new Rect(minX, minY, width, height), Vector2.one * .5f, Mathf.Max(width, height));
                skillArt[i].name = names[i];
            }
            // Match both crops so rotor poses do not make the robot body pulse in size.
            var a = skillArt[3].texture;
            var b = ActorTexture("RobotDroneB");
            Rect first = OpaqueBounds(a), second = OpaqueBounds(b);
            Rect union = Rect.MinMaxRect(Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
                Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));
            Destroy(skillArt[3]);
            skillArt[3] = ActorSprite(a, union, "RobotDroneA");
            droneFrameB = ActorSprite(b, union, "RobotDroneB");
        }

        void DisposeSkillArt() { for (int i = 0; i < skillArt.Length; i++) if (i != 3 && skillArt[i]) Destroy(skillArt[i]); }
        void ClearExtraSkills()
        {
            ClearSummons();
            foreach (var shot in extraShots) if (shot.art) Destroy(shot.art.gameObject);
            extraShots.Clear();
            foreach (var worm in worms) foreach (var part in worm.parts) if (part) Destroy(part.gameObject);
            worms.Clear(); skillTargets.Clear();
            if (drone) Destroy(drone.gameObject);
            arrowsPending = missilesPending = 0;
        }

        void ResetExtraSkills()
        {
            ArrowsLaunched = ArrowHits = BallHits = BallsCompleted = LastCompletedBallHits = 0;
            FireballsLaunched = LastFireTargetCount = FireHits = MissilesLaunched = MissileHits = WormCasts = WormHits = 0;
            arrowClock = 2; ballClock = 3; fireClock = 3.8f; droneClock = 1.7f; wormClock = 4;
            arrowShotClock = missileShotClock = 0;
            drone = Visual("Following missile drone", skillArt[3], player.Position + new Vector2(-1.4f, 1.2f), Vector2.one * 1.5f, 450).transform;
            ResetSummons();
        }

        void UpdateExtraVisuals(float dt)
        {
            Vector2 desired = player.Position + new Vector2(-1.4f, 1.2f + Mathf.Sin(Elapsed * 4) * .12f);
            drone.position = Vector2.Lerp(drone.position, desired, 1 - Mathf.Exp(-dt * 12));
            drone.rotation = Quaternion.Euler(0, 0, Mathf.Sin(Elapsed * 5) * 4);
            SetSpriteArt(drone.GetComponent<SpriteRenderer>(), (int)(Elapsed * 8) % 2 == 0 ? skillArt[3] : droneFrameB);
        }

        static bool Alive(Actor actor) => actor != null && actor.hp > 0 && actor.root;
        Actor ClosestExcept(Vector2 origin, Actor excluded)
        {
            Actor best = null; float distance = float.MaxValue;
            foreach (var enemy in enemies)
            {
                if (enemy == excluded) continue;
                float d = (enemy.Position - origin).sqrMagnitude;
                if (d < distance) { best = enemy; distance = d; }
            }
            return best;
        }
        Actor NearbyTarget(Vector2 origin, int index)
        {
            skillTargets.Clear(); skillTargets.AddRange(enemies);
            skillTargets.Sort((a, b) => (a.Position - origin).sqrMagnitude.CompareTo((b.Position - origin).sqrMagnitude));
            return skillTargets.Count == 0 ? null : skillTargets[index % Mathf.Min(10, skillTargets.Count)];
        }

        // Also usable by a debug button or server-side tests; automatic casts use the same path.
        public void CastExtraSkill(ExtraSkill skill)
        {
            if (player == null || enemies.Count == 0) return;
            switch (skill)
            {
                case ExtraSkill.Arrows:
                    if (arrowsPending == 0) { arrowsPending = 10; arrowIndex = 0; arrowShotClock = 0; }
                    break;
                case ExtraSkill.BouncyBall:
                    Launch(ProjectileKind.Ball, Closest(player.Position), player.Position, 0);
                    break;
                case ExtraSkill.Fire:
                    NearbyTarget(player.Position, 0);
                    LastFireTargetCount = Mathf.Min(3, skillTargets.Count);
                    for (int i = 0; i < LastFireTargetCount; i++) Launch(ProjectileKind.Fire, skillTargets[i], player.Position, i);
                    break;
                case ExtraSkill.Drone:
                    if (missilesPending == 0) { missilesPending = 20; missileIndex = 0; missileShotClock = 0; }
                    break;
                case ExtraSkill.Worm:
                    var worm = new Worm { origin = player.Position, angle = Mathf.Atan2(facing.y, facing.x) };
                    worm.path.Add(worm.origin);
                    for (int i = 0; i < 13; i++)
                    {
                        var part = Visual(i == 0 ? "Spiral worm head" : "Spiral worm segment " + i, skillArt[i == 0 ? 5 : 6], worm.origin, Vector2.one * (i == 0 ? .62f : .49f), 430 - i);
                        part.enabled = false; worm.parts.Add(part);
                    }
                    worms.Add(worm); WormCasts++;
                    break;
            }
        }

        void Launch(ProjectileKind kind, Actor target, Vector2 origin, int index)
        {
            if (!Alive(target)) return;
            int artIndex = kind == ProjectileKind.Missile ? 4 : (int)kind;
            float size = kind == ProjectileKind.Arrow ? 1.05f : kind == ProjectileKind.Ball ? .86f : kind == ProjectileKind.Fire ? 1.35f : .86f;
            var art = Visual(kind + " skill projectile", skillArt[artIndex], origin, Vector2.one * size, 510);
            extraShots.Add(new ExtraShot { kind = kind, target = target, start = origin, end = target.Position, art = art, duration = .7f + index % 3 * .08f });
            ExtraSkill skill = ExtraSkill.BouncyBall;
            if (kind == ProjectileKind.Arrow) { ArrowsLaunched++; skill = ExtraSkill.Arrows; }
            if (kind == ProjectileKind.Fire) { FireballsLaunched++; skill = ExtraSkill.Fire; }
            if (kind == ProjectileKind.Missile) { MissilesLaunched++; skill = ExtraSkill.Drone; }
            SkillProjectileLaunched?.Invoke(skill, Time.fixedTime, target.root.GetInstanceID());
        }

        void TickExtraSkills(float dt)
        {
            if (extraSkillsEnabled)
            {
                arrowClock -= dt; ballClock -= dt; fireClock -= dt; droneClock -= dt; wormClock -= dt;
                if (arrowClock <= 0) { CastExtraSkill(ExtraSkill.Arrows); arrowClock = arrowInterval; }
                if (ballClock <= 0) { CastExtraSkill(ExtraSkill.BouncyBall); ballClock = ballInterval; }
                if (fireClock <= 0) { CastExtraSkill(ExtraSkill.Fire); fireClock = fireInterval; }
                if (droneClock <= 0) { CastExtraSkill(ExtraSkill.Drone); droneClock = droneInterval; }
                if (wormClock <= 0) { CastExtraSkill(ExtraSkill.Worm); wormClock = wormInterval; }
            }
            arrowShotClock -= dt; missileShotClock -= dt;
            if (arrowsPending > 0 && arrowShotClock <= 0 && enemies.Count > 0)
            {
                Launch(ProjectileKind.Arrow, NearbyTarget(player.Position, arrowIndex), player.Position, arrowIndex++);
                arrowsPending--; arrowShotClock = .10f;
            }
            if (missilesPending > 0 && missileShotClock <= 0 && enemies.Count > 0)
            {
                Launch(ProjectileKind.Missile, NearbyTarget(drone.position, missileIndex), drone.position, missileIndex++);
                missilesPending--; missileShotClock = .06f;
            }
            UpdateExtraShots(dt);
            UpdateWorms(dt);
        }

        void UpdateExtraShots(float dt)
        {
            for (int i = extraShots.Count - 1; i >= 0; i--)
            {
                var shot = extraShots[i]; shot.age += dt; shot.trail -= dt;
                Vector2 old = shot.art.transform.position;
                bool finished = false;
                if (!Alive(shot.target)) shot.target = ClosestExcept(old, shot.previous);
                if (Alive(shot.target)) shot.end = shot.target.Position;
                Vector2 next;
                if (shot.kind == ProjectileKind.Missile)
                {
                    float t = Mathf.Clamp01(shot.age / shot.duration);
                    next = Vector2.Lerp(shot.start, shot.end, t) + Vector2.up * (4 * 3 * t * (1 - t));
                    if (t >= 1)
                    {
                        if (Alive(shot.target)) { MissileHits++; Damage(shot.target, 18, (shot.end - shot.start).normalized); }
                        Burst(next, new Color(1, .65f, .3f), 4); finished = true;
                    }
                }
                else
                {
                    float speed = shot.kind == ProjectileKind.Ball ? 15 : shot.kind == ProjectileKind.Fire ? 10 : 18;
                    next = Alive(shot.target) ? Vector2.MoveTowards(old, shot.end, speed * dt) : old;
                    // Only enemy circles participate. Floor, walls, player and drone never bounce a ball.
                    Actor collision = null; float closest = float.MaxValue;
                    foreach (var enemy in enemies)
                    {
                        if (enemy == shot.previous) continue;
                        if (shot.kind == ProjectileKind.Fire && enemy != shot.target) continue;
                        if (SegmentDistance(enemy.Position, old, next) > (shot.kind == ProjectileKind.Ball ? .87f : .7f)) continue;
                        float d = (enemy.Position - old).sqrMagnitude;
                        if (d < closest) { collision = enemy; closest = d; }
                    }
                    if (collision != null)
                    {
                        if (shot.kind == ProjectileKind.Ball)
                        {
                            shot.hits++; BallHits++;
                            BallEnemyHit?.Invoke(collision.root.GetInstanceID(), shot.hits);
                            Damage(collision, 24, (next - old).normalized);
                            shot.previous = collision; shot.target = ClosestExcept(next, collision);
                            if (shot.hits == 7) { LastCompletedBallHits = shot.hits; BallsCompleted++; finished = true; }
                        }
                        else
                        {
                            if (shot.kind == ProjectileKind.Fire) FireHits++; else ArrowHits++;
                            Damage(collision, shot.kind == ProjectileKind.Fire ? 38 : 22, (next - old).normalized);
                            finished = true;
                        }
                    }
                    if (shot.kind != ProjectileKind.Ball && shot.age > 5) finished = true;
                }
                shot.art.transform.position = next;
                if (shot.kind == ProjectileKind.Ball && shot.trail <= 0)
                {
                    Echo("Bouncy ball afterimage", shot.art.sprite, old, shot.art.transform.localScale, shot.art.transform.rotation, .25f, .3f, 480);
                    shot.trail = .04f;
                }
                if (shot.kind == ProjectileKind.Ball) shot.art.transform.Rotate(0, 0, 420 * dt);
                else if ((next - old).sqrMagnitude > .00001f) shot.art.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(next.y - old.y, next.x - old.x) * Mathf.Rad2Deg);
                if ((shot.kind == ProjectileKind.Fire || shot.kind == ProjectileKind.Missile) && shot.trail <= 0)
                {
                    var echo = Visual(shot.kind == ProjectileKind.Fire ? "Flame afterimage" : "Missile flame trail", skillArt[2], old, Vector2.one * (shot.kind == ProjectileKind.Fire ? 1.05f : .42f), 490);
                    echo.transform.rotation = shot.art.transform.rotation;
                    echo.color = new Color(1, .85f, .65f, .6f);
                    flecks.Add(new Fleck { visual = echo.transform, sprite = echo, remaining = .28f, lifetime = .28f, velocity = Vector2.up * .25f });
                    shot.trail = .04f;
                }
                if (finished) { Destroy(shot.art.gameObject); extraShots.RemoveAt(i); }
            }
        }

        void UpdateWorms(float dt)
        {
            for (int n = worms.Count - 1; n >= 0; n--)
            {
                var worm = worms[n]; worm.age += dt;
                float angle = worm.angle + worm.age * 2.3f;
                Vector2 head = worm.origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (worm.age * 1.25f);
                worm.path.Insert(0, head);
                // Store a short path so each circular segment follows the head at a constant spacing.
                float travelled = 0; int segment = 0;
                for (int p = 0; p < worm.path.Count - 1 && segment < worm.parts.Count; p++)
                {
                    float length = Vector2.Distance(worm.path[p], worm.path[p + 1]);
                    while (segment < worm.parts.Count && segment * .29f <= travelled + length)
                    {
                        var part = worm.parts[segment];
                        Vector2 old = part.transform.position;
                        Vector2 pos = Vector2.Lerp(worm.path[p], worm.path[p + 1], (segment * .29f - travelled) / Mathf.Max(length, .0001f));
                        bool visible = worm.age >= segment * .065f;
                        if (!part.enabled) old = pos;
                        part.enabled = visible;
                        part.transform.position = pos;
                        part.transform.localScale = Vector3.one * ((segment == 0 ? .62f : .49f) * (1 + Mathf.Sin(worm.age * 15 - segment * .7f) * .08f));
                        part.color = new Color(1, 1, 1, Mathf.Clamp01((6.5f - worm.age) * 2));
                        if (visible)
                        {
                            for (int e = enemies.Count - 1; e >= 0; e--)
                            {
                                var enemy = enemies[e];
                                if (SegmentDistance(enemy.Position, old, pos) > .79f) continue;
                                if (worm.nextHit.TryGetValue(enemy, out float until) && worm.age < until) continue;
                                worm.nextHit[enemy] = worm.age + .4f; WormHits++;
                                Damage(enemy, 15, (enemy.Position - worm.origin).normalized);
                            }
                        }
                        segment++;
                    }
                    travelled += length;
                    if (segment == worm.parts.Count && p + 2 < worm.path.Count) worm.path.RemoveRange(p + 2, worm.path.Count - p - 2);
                }
                if (worm.age >= 6.5f)
                {
                    foreach (var part in worm.parts) Destroy(part.gameObject);
                    worms.RemoveAt(n);
                }
            }
        }

    }
}
