using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public enum SummonSkill { WaveSnakes, Shotgun, GuardianSword, Cannon, Cucumber, TetherSnake, StormCloud, FireRing, Sand, Dragon, RedWave, Molotov, SoundWave, OrbitGun }
        [Header("Summons and area skills")]
        public bool summonSkillsEnabled = true;
        public bool guardianEnabled = true;
        public float snakeInterval = 7, shotgunInterval = 4.5f, guardianInterval = .7f, cannonInterval = 14;
        public float cucumberInterval = 6, tetherInterval = 9, cloudInterval = 10, ringInterval = 7, sandInterval = 5;
        public float dragonInterval = 11, redWaveInterval = 9;
        public float molotovInterval = 8, soundWaveInterval = 6;
        public const float RedWaveShotGap = .48f;
        public const float CloudMoveSpeed = 2.2f, RedCloudMoveSpeed = 3f;
        public int ShotgunPellets { get; private set; }
        public int CannonShots { get; private set; }
        public int CannonExplosions { get; private set; }
        public float LastCannonLifetime { get; private set; }
        public int TetherRetargets { get; private set; }
        public int DragonFlames { get; private set; }
        public int LightningStrikes { get; private set; }
        public int RedWavesLaunched { get; private set; }
        public event Action<float> RedWaveLaunched;
        public event Action<Vector2, Vector2> CannonProjectileLaunched;
        public int ActiveCannons => turrets.Count;
        public int ActiveStains => stains.Count;
        public int ActiveSummonObjects => movingSkills.Count + snakes.Count + turrets.Count + clouds.Count + bottles.Count + fireZones.Count + soundWaves.Count + (orbitGun ? 1 : 0);
        public int SummonCasts(SummonSkill skill) => summonCasts[(int)skill];
        public int SummonHits(SummonSkill skill) => summonHits[(int)skill];
        public event Action<SummonSkill, int> SummonImpact;
        readonly int[] summonCasts = new int[14], summonHits = new int[14];
        readonly float[] summonClocks = new float[14];
        readonly Dictionary<string, Sprite> summonArt = new Dictionary<string, Sprite>();
        readonly List<MovingSkill> movingSkills = new List<MovingSkill>();
        readonly List<Snake> snakes = new List<Snake>();
        readonly List<Turret> turrets = new List<Turret>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<Stain> stains = new List<Stain>();
        readonly List<RedVolley> redVolleys = new List<RedVolley>();
        Transform guardian;
        float guardianSwing, guardianAim;

        sealed class MovingSkill
        {
            public SummonSkill kind;
            public SpriteRenderer art;
            public Vector2 start, direction, end;
            public Actor target;
            public float age, lifetime, speed, trail, damage, radius;
            public readonly Dictionary<Actor, float> nextHit = new Dictionary<Actor, float>();
        }
        sealed class Snake
        {
            public SummonSkill kind;
            public Vector2 origin, direction, head;
            public Actor target;
            public float age, flameClock;
            public bool ice;
            public readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
            public SpriteRenderer wings;
            public readonly Dictionary<Actor, float> nextHit = new Dictionary<Actor, float>();
        }
        sealed class Turret { public SpriteRenderer art; public Transform muzzle; public Tween recoil; public Vector2 origin; public float age, clock; }
        sealed class Cloud { public SpriteRenderer art; public Actor target; public float age, clock; public bool red; }
        sealed class Stain { public SpriteRenderer art; public float age; }
        sealed class RedVolley { public bool requiresEquipment; public Vector2 direction; public int remaining = 4; public float clock = RedWaveShotGap; }

        void LoadSummonArt()
        {
            string[] names = { "SnakeHead", "SnakeSegment", "GuardianSword", "Cannon", "Cannonball", "Cucumber", "StormCloud", "StormCloudB", "PurpleSnakeHead", "PurpleSnakeSegment", "GoldCoin", "HealthBarFrame", "HealthBarFill", "Lightning", "SandPuff", "InkStain", "Shotgun", "ShotPellet", "Explosion", "DragonHead", "DragonSegment", "DragonWingUp", "DragonWingDown", "RedSlashA", "RedSlashB", "Molotov", "SoundWave", "GroundFlame", "OrbitGun", "OrbitBullet", "MuzzleFlash" };
            foreach (string name in names)
            {
                if (name == "RedSlashA" || name == "RedSlashB") {
                    summonArt.Add(name, Instantiate(DoodleExpansionArt.Get("SkillRedSlash", name == "RedSlashA" ? 0 : 1)));
                    continue;
                }
                var texture = Resources.Load<Texture2D>("DoodleIdle/" + name);
                if (!texture) throw new InvalidOperationException("Missing generated summon art: " + name);
                var pixels = texture.GetPixels32();
                int x0 = texture.width, y0 = texture.height, x1 = -1, y1 = -1;
                for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 32)
                    { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                if (x1 < x0) throw new InvalidOperationException("Empty generated summon art: " + name);
                Vector2 pivot = name == "DragonWingUp" ? new Vector2(.5f, .15f) : name == "DragonWingDown" ? new Vector2(.5f, .34f) : Vector2.one * .5f;
                if (name == "GuardianSword") pivot = new Vector2(.17f, .5f);
                var sprite = Sprite.Create(texture, new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1), pivot, Mathf.Max(x1 - x0 + 1, y1 - y0 + 1));
                sprite.name = name; summonArt.Add(name, sprite);
            }
        }
        void DisposeSummonArt() { foreach (var art in summonArt.Values) if (art) Destroy(art); summonArt.Clear(); }
        void ClearSummons()
        {
            ClearAreaSkills();
            ClearOrbitGun();
            foreach (var shot in movingSkills) if (shot.art) Destroy(shot.art.gameObject);
            foreach (var snake in snakes) { foreach (var part in snake.parts) if (part) Destroy(part.gameObject); if (snake.wings) Destroy(snake.wings.gameObject); }
            foreach (var turret in turrets) { turret.recoil?.Kill(); if (turret.art) Destroy(turret.art.gameObject); }
            foreach (var cloud in clouds) if (cloud.art) Destroy(cloud.art.gameObject);
            foreach (var stain in stains) if (stain.art) Destroy(stain.art.gameObject);
            movingSkills.Clear(); snakes.Clear(); turrets.Clear(); clouds.Clear(); stains.Clear();
            redVolleys.Clear();
            if (guardian) Destroy(guardian.gameObject);
        }
        void ResetSummons()
        {
            Array.Clear(summonCasts, 0, summonCasts.Length); Array.Clear(summonHits, 0, summonHits.Length);
            for (int i = 0; i < summonClocks.Length; i++) summonClocks[i] = 2.5f + i * .6f;
            summonClocks[(int)SummonSkill.GuardianSword] = 1;
            summonClocks[(int)SummonSkill.OrbitGun] = .6f;
            ShotgunPellets = CannonShots = CannonExplosions = TetherRetargets = DragonFlames = LightningStrikes = 0;
            LastCannonLifetime = 0;
            RedWavesLaunched = 0;
            guardianSwing = guardianAim = 0;
            guardian = Visual("Following guardian sword", summonArt["GuardianSword"], player.Position + new Vector2(1.3f, .8f), Vector2.one * 1.45f, 445).transform;
            guardian.GetComponent<SpriteRenderer>().enabled=false;
        }
        float SummonInterval(int i)
        {
            switch ((SummonSkill)i)
            {
                case SummonSkill.WaveSnakes: return snakeInterval;
                case SummonSkill.Shotgun: return shotgunInterval;
                case SummonSkill.GuardianSword: return guardianInterval;
                case SummonSkill.Cannon: return cannonInterval;
                case SummonSkill.Cucumber: return cucumberInterval;
                case SummonSkill.TetherSnake: return tetherInterval;
                case SummonSkill.StormCloud: return cloudInterval;
                case SummonSkill.FireRing: return ringInterval;
                case SummonSkill.Sand: return sandInterval;
                case SummonSkill.Dragon: return dragonInterval;
                case SummonSkill.Molotov: return molotovInterval;
                case SummonSkill.SoundWave: return soundWaveInterval;
                case SummonSkill.OrbitGun: return OrbitSkillCycle;
                default: return redWaveInterval;
            }
        }
        static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        static Quaternion Aim(Vector2 direction) => Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        Actor InRange(Vector2 origin, float range)
        {
            Actor target = Closest(origin);
            return target != null && (target.Position - origin).sqrMagnitude <= range * range ? target : null;
        }
        void Impact(SummonSkill skill, Actor target, float amount, Vector2 direction, string splashAbility = null)
        {
            if (!Alive(target)) return;
            summonHits[(int)skill]++;
            SummonImpact?.Invoke(skill, target.root.GetInstanceID());
            if (splashAbility != null) SkillImpact(target, amount, direction, splashAbility);
            else if (skill == SummonSkill.Shotgun) SkillImpact(target, amount, direction, "Shotgun");
            else SkillDamage(target, amount, direction, skill == SummonSkill.SoundWave ? "Sound" : skill == SummonSkill.Cucumber ? "Eggplant" : skill == SummonSkill.StormCloud ? "Cloud" : skill.ToString());
        }

        SpriteRenderer Echo(string label, Sprite art, Vector2 position, Vector2 scale, Quaternion rotation, float lifetime, float alpha, int order)
        {
            var echo = Visual(label, art, position, scale, order);
            echo.transform.rotation = rotation; echo.color = new Color(1, 1, 1, alpha);
            flecks.Add(new Fleck { visual = echo.transform, sprite = echo, remaining = lifetime, lifetime = lifetime });
            return echo;
        }
        void LeaveStain(Vector2 position)
        {
            if (!summonArt.TryGetValue("InkStain", out var ink)) return;
            if (stains.Count >= 180) { Destroy(stains[0].art.gameObject); stains.RemoveAt(0); }
            // Equal world-space width/height removes the previous flattened oval.
            var art = Visual("Fading black death stain", ink, position, new Vector2(1.15f / ink.bounds.size.x, 1.15f / ink.bounds.size.y), -950);
            art.color = new Color(0, 0, 0, .28f);
            art.transform.rotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360));
            stains.Add(new Stain { art = art });
        }

        public void CastSummonSkill(SummonSkill kind)
        {
            if (player == null || enemies.Count == 0) return;
            Vector2 origin = player.Position;
            Actor target = Closest(origin);
            Vector2 direction = (target.Position - origin).normalized;
            if (kind == SummonSkill.GuardianSword)
            {
                target = InRange(guardian.position, 5);
                if (target == null) return;
                origin = guardian.position; direction = (target.Position - origin).normalized;
                guardian.GetComponent<SpriteRenderer>().enabled=true;
                guardianAim = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                guardianSwing = .34f;
                guardian.rotation = Quaternion.Euler(0, 0, guardianAim - 70);
            }
            summonCasts[(int)kind]++;
            switch (kind)
            {
                case SummonSkill.Molotov:
                    ThrowMolotov(origin, target.Position);
                    break;
                case SummonSkill.SoundWave:
                    StartSoundVolley(direction);
                    break;
                case SummonSkill.OrbitGun:
                    SpawnOrbitGun();
                    break;
                case SummonSkill.WaveSnakes:
                    for (int i = 0; i < 5; i++) SpawnSnake(kind, Direction(Mathf.Atan2(direction.y, direction.x) + i * Mathf.PI * 2 / 5));
                    break;
                case SummonSkill.Shotgun:
                    Echo("Shotgun muzzle", summonArt["Shotgun"], origin + direction * .55f, Vector2.one * 1.2f, Aim(direction), .22f, 1, 470);
                    float angle = Mathf.Atan2(direction.y, direction.x);
                    for (int i = 0; i < 20; i++)
                    {
                        AddMoving(kind, summonArt["ShotPellet"], origin, Direction(angle + Mathf.Lerp(-.61f, .61f, i / 19f)), .44f, 18, .75f, 15, .66f);
                        ShotgunPellets++;
                    }
                    break;
                case SummonSkill.GuardianSword:
                    AddMoving(kind, slash, origin, direction, 1.6f, 12, .5f, 23, 1);
                    break;
                case SummonSkill.Cannon:
                    var cannonArt = Visual("Stationary ten second cannon", summonArt["Cannon"], origin, Vector2.one * 1.8f, Order(origin) + 1);
                    var muzzle = new GameObject("Cannon muzzle").transform;
                    muzzle.SetParent(cannonArt.transform, false);
                    muzzle.localPosition = new Vector3(.385f, .125f, 0);
                    turrets.Add(new Turret { origin = origin, art = cannonArt, muzzle = muzzle });
                    break;
                case SummonSkill.Cucumber:
                    AddMoving(kind, summonArt["Cucumber"], origin, direction, 4.6f, 6, 3.2f, 42, 2.7f);
                    break;
                case SummonSkill.TetherSnake:
                case SummonSkill.Dragon:
                    SpawnSnake(kind, direction);
                    break;
                case SummonSkill.StormCloud:
                    clouds.Add(new Cloud { art = Visual("Drifting storm cloud", summonArt["StormCloud"], origin + Vector2.up * 2, Vector2.one * 2.6f, 650), target = target });
                    break;
                case SummonSkill.FireRing:
                    for (int i = 0; i < 24; i++) AddMoving(kind, skillArt[2], origin, Direction(i * Mathf.PI * 2 / 24), .82f, 4.5f, 1.8f, 19, .78f);
                    break;
                case SummonSkill.Sand:
                    NearbyTarget(origin, 0);
                    for (int i = 0; i < Mathf.Min(3, skillTargets.Count); i++)
                        AddMoving(kind, summonArt["SandPuff"], origin, (skillTargets[i].Position - origin).normalized, 1.2f, 8, .8f, 17, 1.0f);
                    break;
                case SummonSkill.RedWave:
                    LaunchRedWave(direction);
                    redVolleys.Add(new RedVolley { direction = direction, requiresEquipment = castingEquippedSkill });
                    break;
            }
        }
        void LaunchRedWave(Vector2 direction)
        {
            var wave = AddMoving(SummonSkill.RedWave, summonArt["RedSlashA"], player.Position, direction, 4.2f, SlashSpeed, 1.1f, 22, 2.1f);
            // The source crescent opens right; rotate it so the curved cutting edge leads.
            wave.art.transform.rotation = Aim(direction);
            RedWavesLaunched++;
            RedWaveLaunched?.Invoke(Time.fixedTime);
        }
        void TickRedVolleys(float dt)
        {
            for (int i = redVolleys.Count - 1; i >= 0; i--)
            {
                var volley = redVolleys[i];
                if (volley.requiresEquipment && !SkillEquipped("RedWave")) { redVolleys.RemoveAt(i); continue; }
                volley.clock -= dt;
                if (volley.clock > .0001f) continue;
                LaunchRedWave(volley.direction);
                volley.clock += RedWaveShotGap;
                if (--volley.remaining == 0) redVolleys.RemoveAt(i);
            }
        }
        MovingSkill AddMoving(SummonSkill kind, Sprite art, Vector2 origin, Vector2 direction, float size, float speed, float lifetime, float damage, float radius)
        {
            var shot = new MovingSkill { kind = kind, start = origin, direction = direction, speed = speed, lifetime = lifetime, damage = damage, radius = radius,
                art = Visual(kind + " moving skill", art, origin, Vector2.one * size, 505) };
            shot.art.transform.rotation = Aim(direction);
            if(kind==SummonSkill.Cucumber)UpdateRollingVegetable(shot.art,direction,0,size);
            movingSkills.Add(shot); return shot;
        }
        static void UpdateRollingVegetable(SpriteRenderer art,Vector2 direction,float age,float size)
        {
            art.transform.rotation=Aim(direction)*Quaternion.Euler(0,0,90+Mathf.Sin(age*15)*12);
            art.transform.localScale=new Vector3(size,size*(.72f+.28f*Mathf.Abs(Mathf.Cos(age*10))),1);
        }
        void SpawnSnake(SummonSkill kind, Vector2 direction, bool ice=false)
        {
            bool dragon = kind == SummonSkill.Dragon;
            var snake = new Snake { ice=ice,kind = kind, direction = direction, origin = player.Position, head = player.Position, flameClock = .35f };
            int count = kind == SummonSkill.TetherSnake ? 32 : dragon ? 14 : 11;
            for (int i = 0; i < count; i++)
            {
                bool tether = kind == SummonSkill.TetherSnake;
                var art = summonArt[dragon ? (i == 0 ? "DragonHead" : "DragonSegment") : tether ? (i == 0 ? "PurpleSnakeHead" : "PurpleSnakeSegment") : (i == 0 ? "SnakeHead" : "SnakeSegment")];
                if(ice)art=DoodleVariantArt.Get(i==0?"IceSnakeHead":"IceSnakeSegment");
                var part = Visual((ice?"IceSnakes":kind.ToString()) + (i == 0 ? " head" : " segment " + i), art, snake.origin, Vector2.one * (dragon ? .7f : .47f)*(ice?2:1), (tether ? -500 : 445) - i);
                part.enabled = false; snake.parts.Add(part);
            }
            if (dragon) snake.wings = Visual("Animated dragon wings", summonArt["DragonWingUp"], snake.origin, Vector2.one * 2.1f, 450);
            snakes.Add(snake);
        }

        void TickSummons(float dt)
        {
            TickRedVolleys(dt);
            guardian.position = Vector2.Lerp(guardian.position, player.Position + new Vector2(1.3f, .8f + Mathf.Sin(Elapsed * 3) * .12f), 1 - Mathf.Exp(-dt * 12));
            if (guardianSwing > 0)
            {
                guardianSwing = Mathf.Max(0, guardianSwing - dt);
                float progress = 1 - guardianSwing / .34f;
                float angle = progress < .65f ? Mathf.Lerp(-70, 85, Mathf.SmoothStep(0, 1, progress / .65f))
                    : Mathf.Lerp(85, 0, Mathf.SmoothStep(0, 1, (progress - .65f) / .35f));
                guardian.rotation = Quaternion.Euler(0, 0, guardianAim + angle);
            }
            TickTurrets(dt); TickClouds(dt); TickSnakes(dt); TickOrbitGun(dt); TickMovingSkills(dt); TickAreaSkills(dt);
            for (int i = stains.Count - 1; i >= 0; i--)
            {
                var stain = stains[i]; stain.age += dt;
                stain.art.color = new Color(0, 0, 0, .28f * Mathf.Clamp01((6 - stain.age) / 2));
                if (stain.age >= 6) { Destroy(stain.art.gameObject); stains.RemoveAt(i); }
            }
        }
        void TickTurrets(float dt)
        {
            for (int i = turrets.Count - 1; i >= 0; i--)
            {
                var turret = turrets[i]; turret.age += dt; turret.clock -= dt;
                if (turret.recoil != null && turret.recoil.IsActive()) turret.recoil.ManualUpdate(dt, dt);
                if (turret.age >= 10)
                {
                    LastCannonLifetime = turret.age; turret.recoil?.Kill(); Destroy(turret.art.gameObject); turrets.RemoveAt(i); continue;
                }
                var target = InRange(turret.origin, 9);
                if (target == null || turret.clock > 0) continue;
                Vector2 direction = (target.Position - turret.origin).normalized;
                turret.art.flipX = direction.x < 0;
                float side = turret.art.flipX ? -1 : 1;
                // Local coordinates mark the center of the generated artwork's barrel opening.
                // The child inherits the current squash/rotation, and mirrors with the SpriteRenderer.
                turret.muzzle.localPosition = new Vector3(.385f * side, .125f, 0);
                Vector2 launch = turret.muzzle.position;
                direction = (target.Position - launch).normalized;
                var shot = AddMoving(SummonSkill.Cannon, summonArt["Cannonball"], launch, direction, .62f, 0, .8f, 46, 2.2f);
                shot.target = target; shot.end = target.Position;
                CannonProjectileLaunched?.Invoke(launch, target.Position);
                BounceCannon(turret, side);
                turret.clock = .85f; CannonShots++;
            }
        }
        void BounceCannon(Turret turret, float side)
        {
            turret.recoil?.Kill();
            var visual = turret.art.transform;
            visual.localScale = new Vector3(1.8f, 1.8f, 1);
            visual.localRotation = Quaternion.identity;
            // A fixed emplacement with a springy barrel: squash, tilt back, then rebound.
            // Update this sequence manually so the game's pause button also freezes DOTween.
            turret.recoil = DOTween.Sequence()
                .Join(visual.DOPunchScale(new Vector3(-.3f, .4f, 0), .42f, 4, .65f))
                .Join(visual.DOPunchRotation(new Vector3(0, 0, -7 * side), .42f, 4, .65f))
                .SetTarget(visual).SetLink(visual.gameObject).SetUpdate(UpdateType.Manual);
        }
        void KillCannonTweens() { foreach (var turret in turrets) turret.recoil?.Kill(); }
        void TickClouds(float dt)
        {
            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                var cloud = clouds[i]; cloud.age += dt; cloud.clock -= dt;
                SetSpriteArt(cloud.art, cloud.red?DoodleVariantArt.Get((int)(cloud.age*4)%2==0?"RedCloud":"RedCloudB"):summonArt[(int)(cloud.age * 4) % 2 == 0 ? "StormCloud" : "StormCloudB"]);
                Vector2 position=cloud.art.transform.position;
                if(!Alive(cloud.target))cloud.target=Closest(position-Vector2.up*2);
                if(Alive(cloud.target))cloud.art.transform.position=Vector2.MoveTowards(position,cloud.target.Position+Vector2.up*2,(cloud.red?RedCloudMoveSpeed:CloudMoveSpeed)*dt);
                if (cloud.age >= 8) { Destroy(cloud.art.gameObject); clouds.RemoveAt(i); continue; }
                if (cloud.clock > 0) continue;
                cloud.clock = .7f;
                Vector2 origin = cloud.art.transform.position;
                NearbyTarget(origin, 0);
                // Copy the three recipients before Damage can remove enemies from the population.
                var targets = skillTargets.GetRange(0, Mathf.Min(3, skillTargets.Count));
                foreach (var target in targets)
                {
                    if (!Alive(target) || Vector2.Distance(origin, target.Position) > 5.5f) continue;
                    Vector2 delta = target.Position - origin;
                    Echo("Lightning afterimage", cloud.red?DoodleVariantArt.Get("RedLightning"):summonArt["Lightning"], origin + delta * .5f, new Vector2(delta.magnitude, .85f), Aim(delta), .4f, .35f, 570);
                    Echo("Lightning strike", cloud.red?DoodleVariantArt.Get("RedLightning"):summonArt["Lightning"], origin + delta * .5f, new Vector2(delta.magnitude, 1.1f), Aim(delta), .12f, 1, 580);
                    Impact(SummonSkill.StormCloud, target, 23, delta.normalized, cloud.red ? "RedCloud" : "Cloud"); LightningStrikes++;
                }
            }
        }
        void TickMovingSkills(float dt)
        {
            for (int i = movingSkills.Count - 1; i >= 0; i--)
            {
                var shot = movingSkills[i]; shot.age += dt; shot.trail -= dt;
                Vector2 old = shot.art.transform.position;
                Vector2 next = old + shot.direction * (shot.speed * dt);
                bool finished = shot.age >= shot.lifetime;
                if (shot.kind == SummonSkill.Cannon)
                {
                    if (Alive(shot.target)) shot.end = shot.target.Position;
                    float t = Mathf.Clamp01(shot.age / shot.lifetime);
                    next = Vector2.Lerp(shot.start, shot.end, t) + Vector2.up * (4 * 2.3f * t * (1 - t));
                    if (finished)
                    {
                        CannonExplosions++;
                        EmitCannonExplosion(shot.end);
                        for (int e = enemies.Count - 1; e >= 0; e--)
                            if (Vector2.Distance(enemies[e].Position, shot.end) < shot.radius) Impact(shot.kind, enemies[e], shot.damage, (enemies[e].Position - shot.end).normalized);
                    }
                }
                else if (shot.kind == SummonSkill.OrbitGun)
                {
                    Actor first = null; float closest = float.MaxValue;
                    Vector2 step = next - old;
                    foreach (var enemy in enemies)
                    {
                        if (SegmentDistance(enemy.Position, old, next) > shot.radius) continue;
                        float along = Mathf.Clamp01(Vector2.Dot(enemy.Position - old, step) / Mathf.Max(.0001f, step.sqrMagnitude));
                        if (along < closest) { closest = along; first = enemy; }
                    }
                    if (first != null) { Impact(shot.kind, first, shot.damage, shot.direction); finished = true; }
                }
                else
                {
                    for (int e = enemies.Count - 1; e >= 0; e--)
                    {
                        var enemy = enemies[e];
                        if (SegmentDistance(enemy.Position, old, next) > shot.radius) continue;
                        if (shot.nextHit.TryGetValue(enemy, out float until) && shot.age < until) continue;
                        shot.nextHit[enemy] = shot.kind == SummonSkill.Sand ? shot.age + .22f : float.MaxValue;
                        Impact(shot.kind, enemy, shot.damage, shot.direction);
                        if (shot.kind == SummonSkill.Shotgun) { finished = true; break; }
                    }
                }
                shot.art.transform.position = next;
                if (shot.kind == SummonSkill.Cucumber)
                {
                    UpdateRollingVegetable(shot.art,shot.direction,shot.age,4.6f);
                }
                if (shot.kind == SummonSkill.RedWave) SetSpriteArt(shot.art, summonArt[((int)(shot.age * 6) % 2 == 0) ? "RedSlashA" : "RedSlashB"]);
                if (shot.kind == SummonSkill.Sand) shot.art.transform.localScale = Vector3.one * (1.2f + shot.age * 1.2f);
                if (shot.kind == SummonSkill.GuardianSword) shot.art.color = new Color(.7f, .88f, 1, Mathf.Clamp01((shot.lifetime - shot.age) * 4));
                bool trail = shot.kind == SummonSkill.FireRing || shot.kind == SummonSkill.Sand || shot.kind == SummonSkill.Dragon || shot.kind == SummonSkill.RedWave;
                if (trail && shot.trail <= 0)
                {
                    if (shot.kind == SummonSkill.Sand) EmitSand(old, shot.direction);
                    else Echo(shot.kind + " afterimage", shot.art.sprite, old, shot.art.transform.localScale, shot.art.transform.rotation, shot.kind == SummonSkill.RedWave ? .22f : .3f, .3f, 480);
                    shot.trail = shot.kind == SummonSkill.RedWave ? .04f : .06f;
                }
                if (finished) { Destroy(shot.art.gameObject); movingSkills.RemoveAt(i); }
            }
        }

        void TickSnakes(float dt)
        {
            for (int n = snakes.Count - 1; n >= 0; n--)
            {
                var snake = snakes[n]; snake.age += dt;
                bool tether = snake.kind == SummonSkill.TetherSnake, dragon = snake.kind == SummonSkill.Dragon;
                float lifetime = tether || dragon ? 6 : 3.5f;
                if (snake.age >= lifetime)
                {
                    foreach (var part in snake.parts) Destroy(part.gameObject);
                    if (snake.wings) Destroy(snake.wings.gameObject);
                    snakes.RemoveAt(n); continue;
                }
                Vector2 perpendicular = new Vector2(-snake.direction.y, snake.direction.x);
                if (tether)
                {
                    if (!Alive(snake.target) || Vector2.Distance(snake.target.Position, player.Position) > 7)
                    {
                        Actor previous = snake.target; Actor nextTarget = null; float distance = float.MaxValue;
                        foreach (var enemy in enemies)
                        {
                            if (Vector2.Distance(enemy.Position, player.Position) > 7) continue;
                            float d = Vector2.Distance(enemy.Position, snake.head);
                            if(snake.ice && snake.age<1)d+=Mathf.Max(0,1-Vector2.Dot((enemy.Position-player.Position).normalized,snake.direction))*12;
                            if (d < distance) { distance = d; nextTarget = enemy; }
                        }
                        snake.target = nextTarget;
                        if (previous != null && nextTarget != null && previous != nextTarget) TetherRetargets++;
                    }
                    Vector2 destination = Alive(snake.target) ? snake.target.Position : player.Position + facing;
                    snake.head = snake.ice && snake.age<.35f?snake.head+snake.direction*dt*9:Vector2.MoveTowards(snake.head, destination, dt * 9);
                }
                int active = tether ? Mathf.Min(snake.parts.Count, 2 + Mathf.FloorToInt(snake.age / .035f)) : snake.parts.Count;
                for (int p = 0; p < snake.parts.Count; p++)
                {
                    var part = snake.parts[p]; Vector2 old = part.transform.position; Vector2 position;
                    bool visible;
                    if (tether)
                    {
                        // Part zero is the head; the final active circle is anchored to the player every physics tick.
                        visible = p < active;
                        float t = p / (float)(active - 1);
                        Vector2 axis = snake.head - player.Position;
                        var normal = new Vector2(-axis.y, axis.x).normalized;
                        position = Vector2.Lerp(snake.head, player.Position, Mathf.Clamp01(t)) + normal * (Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * 12 - snake.age * 9) * .23f);
                        part.transform.rotation = Aim(axis);
                    }
                    else
                    {
                        float t = snake.age - p * (dragon ? .09f : .065f);
                        visible = t >= 0;
                        t = Mathf.Max(0, t);
                        position = snake.origin + snake.direction * (t * (dragon ? 2.6f : 4.5f)) + perpendicular * (Mathf.Sin(t * 7) * (dragon ? .65f : .46f));
                        part.transform.rotation = Aim(snake.direction + perpendicular * (Mathf.Cos(t * 7) * .55f));
                    }
                    if (!part.enabled) old = position;
                    part.enabled = visible; part.transform.position = position;
                    if (dragon && p == 0)
                    {
                        var mouthTarget = InRange(position, 5);
                        part.transform.rotation = Aim(mouthTarget == null ? snake.direction : (mouthTarget.Position - position).normalized);
                    }
                    float size = (dragon ? .7f : .47f) * (snake.ice?2:1) * (p == 0 ? 1.2f : 1) * (1 + Mathf.Sin(snake.age * 12 - p * .6f) * .06f);
                    part.transform.localScale = Vector3.one * size;
                    if (tether) part.color = new Color(1, 1, 1, Mathf.Clamp01((lifetime - snake.age) * 3));
                    if (!visible) continue;
                    for (int e = enemies.Count - 1; e >= 0; e--)
                    {
                        var enemy = enemies[e];
                        if (SegmentDistance(enemy.Position, old, position) > (dragon ? .92f : .8f)) continue;
                        if (snake.nextHit.TryGetValue(enemy, out float until) && snake.age < until) continue;
                        snake.nextHit[enemy] = snake.age + (tether ? .18f : .35f);
                        Impact(snake.kind, enemy, tether ? 8 : 13, (enemy.Position - player.Position).normalized, snake.ice ? "IceSnakes" : null);
                    }
                }
                if (dragon)
                {
                    snake.wings.transform.position = snake.parts[3].transform.position;
                    snake.wings.transform.rotation = snake.parts[3].transform.rotation * Quaternion.Euler(0, 0, 90);
                    SetSpriteArt(snake.wings, summonArt[(int)(snake.age * 8) % 2 == 0 ? "DragonWingUp" : "DragonWingDown"]);
                    snake.flameClock -= dt;
                    if (snake.flameClock <= 0)
                    {
                        snake.flameClock = .3f;
                        Vector2 mouth = snake.parts[0].transform.position;
                        var target = InRange(mouth, 5);
                        Vector2 aim = target == null ? snake.direction : (target.Position - mouth).normalized;
                        snake.parts[0].transform.rotation = Aim(aim);
                        for (int f = -1; f <= 1; f++)
                        {
                            Vector2 direction = Direction(Mathf.Atan2(aim.y, aim.x) + f * .18f);
                            Vector2 lip = mouth + (Vector2)(Aim(aim) * new Vector3(.34f, -.14f, 0));
                            AddMoving(SummonSkill.Dragon, skillArt[2], lip, direction, .65f, 7.5f, .85f, 12, .72f); DragonFlames++;
                        }
                    }
                }
            }
        }
    }
}

