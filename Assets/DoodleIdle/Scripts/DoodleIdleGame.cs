using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DoodleIdle
{
    /// <summary>Self-contained, automatic 2D combat sandbox. All tuning is exposed in the Inspector.</summary>
    public sealed class DoodleIdleGame : MonoBehaviour
    {
        [Header("Population")]
        public int targetPopulation = 80;
        public int refillBelow = 20;
        public Vector2 arenaHalfSize = new Vector2(17, 11);
        [Header("Combat")]
        public float moveSpeed = 3.1f;
        public float attackInterval = .65f;
        public float dashInterval = 5f;
        public float stoneInterval = 3.2f;
        public float bananaRadius = 1.9f;
        public bool autoPlay = true;
        public bool paused;

        public int EnemyCount => enemies.Count;
        public int Kills { get; private set; }
        public int Refills { get; private set; }
        public int DashCasts { get; private set; }
        public int StonesLaunched { get; private set; }
        public int BananaHits { get; private set; }
        public int SlashHits { get; private set; }
        public int DashHits { get; private set; }
        public bool Ready { get; private set; }
        public float Elapsed { get; private set; }

        sealed class Actor
        {
            public GameObject root;
            public Rigidbody2D body;
            public SpriteRenderer art;
            public CircleCollider2D collider;
            public float hp = 68, flash, phase;
            public int kind;
            public Vector2 Position => body.position;
        }
        sealed class Shot
        {
            public Transform visual;
            public Vector2 start, end, direction;
            public Actor target;
            public float age, duration;
            public bool stone;
            public HashSet<Actor> hit = new HashSet<Actor>();
        }
        sealed class Fleck
        {
            public Transform visual;
            public Vector3 velocity;
            public float remaining, lifetime;
            public SpriteRenderer sprite;
        }
        readonly List<Actor> enemies = new List<Actor>(80);
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Fleck> flecks = new List<Fleck>();
        readonly List<Actor> nearest = new List<Actor>(80);
        readonly List<Vector2> obstacles = new List<Vector2>();
        readonly HashSet<Actor> dashVictims = new HashSet<Actor>();
        readonly Dictionary<Actor, float> bananaHitTimes = new Dictionary<Actor, float>();
        readonly Transform[] bananas = new Transform[5];
        Sprite[] sprites;
        Sprite disc, slash;
        Actor player;
        Transform world, weapon;
        Camera gameCamera;
        PhysicsMaterial2D frictionless;
        float attackTimer, dashTimer, stoneTimer, dashRemaining, orbitAngle, swing, trailTimer;
        Vector2 dashDirection, facing = Vector2.right, manualInput;
        Text populationText, killsText, timeText, waveText, modeText, pauseText;
        Image dashFill, stoneFill;
        Font uiFont;
        Material spriteMaterial;

        void Start()
        {
            Application.targetFrameRate = 60;
            sprites = LoadAtlas();
            disc = MakeDisc();
            slash = MakeSlash();
            spriteMaterial = new Material(Shader.Find("Sprites/Default"));
            frictionless = new PhysicsMaterial2D("Doodle frictionless") { friction = 0, bounciness = 0 };
            world = new GameObject("Doodle world").transform;
            world.SetParent(transform);
            BuildCamera();
            BuildGround();
            BuildArena();
            BuildHud();
            ResetGame();
            Ready = true;
        }

        Sprite[] LoadAtlas()
        {
            var texture = Resources.Load<Texture2D>("DoodleIdle/Characters");
            if (!texture) throw new InvalidOperationException("Generated character atlas is missing.");
            var result = new Sprite[9];
            int cw = texture.width / 3, ch = texture.height / 3;
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < 9; i++)
            {
                int x0 = i % 3 * cw, y0 = (2 - i / 3) * ch;
                int minX = x0 + cw, minY = y0 + ch, maxX = x0, maxY = y0;
                for (int y = y0; y < y0 + ch; y++)
                    for (int x = x0; x < x0 + cw; x++)
                        if (pixels[y * texture.width + x].a > 32)
                        { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                if (minX > maxX) throw new InvalidOperationException("Empty generated sprite cell: " + i);
                result[i] = Sprite.Create(texture, new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1), new Vector2(.5f, .5f), Mathf.Max(maxX - minX + 1, maxY - minY + 1));
            }
            return result;
        }

        void BuildCamera()
        {
            gameCamera = Camera.main;
            if (!gameCamera) gameCamera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            gameCamera.tag = "MainCamera";
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 8.5f;
            gameCamera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            gameCamera.backgroundColor = new Color(.91f, .83f, .68f);
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        void BuildGround()
        {
            var texture = Resources.Load<Texture2D>("DoodleIdle/Dirt");
            var floor = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, texture.width / 13f);
            for (int y = -2; y <= 2; y++)
                for (int x = -3; x <= 3; x++)
                {
                    var tile = Visual("Generated dirt floor", floor, new Vector2(x * 13, y * 13), Vector2.one, -1000);
                    // Mirroring adjacent tiles makes matching edges exact, even for an imperfect AI tile.
                    tile.flipX = (Mathf.Abs(x) % 2) == 1;
                    tile.flipY = (Mathf.Abs(y) % 2) == 1;
                }
        }

        void BuildArena()
        {
            Wall(new Vector2(-arenaHalfSize.x - .5f, 0), new Vector2(1, arenaHalfSize.y * 2 + 2));
            Wall(new Vector2(arenaHalfSize.x + .5f, 0), new Vector2(1, arenaHalfSize.y * 2 + 2));
            Wall(new Vector2(0, -arenaHalfSize.y - .5f), new Vector2(arenaHalfSize.x * 2, 1));
            Wall(new Vector2(0, arenaHalfSize.y + .5f), new Vector2(arenaHalfSize.x * 2, 1));
            Vector2[] rocks = { new Vector2(-12, 6), new Vector2(11, -6), new Vector2(9, 7), new Vector2(-10, -7), new Vector2(15, 2), new Vector2(-15, -1) };
            foreach (var p in rocks)
            {
                obstacles.Add(p);
                var rock = Visual("Doodle boulder / solid collider", sprites[7], p, Vector2.one * 1.55f, Order(p));
                var collider = rock.gameObject.AddComponent<CircleCollider2D>();
                collider.radius = .43f;
                collider.sharedMaterial = frictionless;
            }
        }

        void Wall(Vector2 position, Vector2 size)
        {
            var wall = new GameObject("Invisible arena boundary");
            wall.transform.SetParent(world);
            wall.transform.position = position;
            wall.AddComponent<BoxCollider2D>().size = size;
        }

        public void ResetGame()
        {
            foreach (var a in enemies) { a.root.SetActive(false); Destroy(a.root); }
            enemies.Clear();
            foreach (var shot in shots) Destroy(shot.visual.gameObject);
            shots.Clear();
            foreach (var f in flecks) Destroy(f.visual.gameObject);
            flecks.Clear();
            if (player != null) { player.root.SetActive(false); Destroy(player.root); }
            foreach (var banana in bananas) if (banana) Destroy(banana.gameObject);
            bananaHitTimes.Clear(); dashVictims.Clear();
            Kills = Refills = DashCasts = StonesLaunched = BananaHits = SlashHits = DashHits = 0;
            Elapsed = orbitAngle = dashRemaining = 0;
            dashTimer = dashInterval; stoneTimer = 1.2f; attackTimer = .3f;
            paused = false;
            player = CreateActor(true, Vector2.zero, 0);
            weapon = Visual("Floating baseball club", sprites[4], Vector2.zero, Vector2.one * .94f, 20).transform;
            weapon.SetParent(player.root.transform, false);
            for (int i = 0; i < 5; i++)
            {
                bananas[i] = Visual("Orbit banana " + (i + 1), sprites[5], Vector2.zero, Vector2.one * .58f, 80).transform;
                var trigger = bananas[i].gameObject.AddComponent<CircleCollider2D>();
                trigger.radius = .4f; trigger.isTrigger = true;
            }
            Refill();
            Refills = 0;
        }

        Actor CreateActor(bool isPlayer, Vector2 p, int kind)
        {
            var root = new GameObject(isPlayer ? "Player - head and club" : "Horned enemy");
            root.transform.SetParent(world);
            root.transform.position = p;
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.linearDamping = 8;
            body.mass = isPlayer ? 5 : 1;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = isPlayer ? .61f : .56f;
            collider.sharedMaterial = frictionless;
            var shadow = Visual("Soft ground shadow", disc, p + new Vector2(0, -.36f), new Vector2(.95f, .28f), Order(p) - 2);
            shadow.color = new Color(.23f, .19f, .15f, .15f);
            shadow.transform.SetParent(root.transform, true);
            var art = Visual("Generated head sprite", sprites[isPlayer ? 0 : kind + 1], p, Vector2.one * (isPlayer ? 1.28f : 1.10f), Order(p));
            art.transform.SetParent(root.transform, true);
            return new Actor { root = root, body = body, art = art, collider = collider, phase = UnityEngine.Random.value * 6.28f, kind = kind };
        }

        void Refill()
        {
            int needed = targetPopulation - enemies.Count;
            for (int n = 0; n < needed; n++)
            {
                Vector2 p = Vector2.zero;
                bool found = false;
                for (int attempt = 0; attempt < 600; attempt++)
                {
                    p = new Vector2(UnityEngine.Random.Range(-arenaHalfSize.x + 1, arenaHalfSize.x - 1), UnityEngine.Random.Range(-arenaHalfSize.y + 1, arenaHalfSize.y - 1));
                    if ((p - player.Position).sqrMagnitude < 10) continue;
                    found = true;
                    foreach (var enemy in enemies) if ((p - enemy.Position).sqrMagnitude < 1.6f) { found = false; break; }
                    foreach (var obstacle in obstacles) if ((p - obstacle).sqrMagnitude < 2.6f) { found = false; break; }
                    if (found) break;
                }
                if (found) enemies.Add(CreateActor(false, p, UnityEngine.Random.Range(0, 3)));
            }
            Refills++;
        }

        void Update()
        {
            if (!Ready) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
                if (keyboard.rKey.wasPressedThisFrame) ResetGame();
                if (keyboard.tabKey.wasPressedThisFrame) autoPlay = !autoPlay;
                manualInput = new Vector2((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0)).normalized;
            }
            UpdateHud();
            if (paused) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            UpdateShots(dt);
            UpdateFlecks(dt);
            swing = Mathf.MoveTowards(swing, 0, dt * 5);
            weapon.localPosition = new Vector3(facing.x < 0 ? -.58f : .58f, -.12f, 0);
            weapon.localRotation = Quaternion.Euler(0, 0, (facing.x < 0 ? 80 : -10) + Mathf.Sin(swing * Mathf.PI) * -105);
            weapon.GetComponent<SpriteRenderer>().sortingOrder = player.art.sortingOrder + 2;
            Animate(player);
            foreach (var enemy in enemies) Animate(enemy);
        }

        void FixedUpdate()
        {
            if (!Ready || paused) return;
            float dt = Time.fixedDeltaTime;
            var target = Closest(player.Position);
            if (target == null) { Refill(); return; }
            Vector2 delta = target.Position - player.Position;
            if (delta.sqrMagnitude > .01f) facing = delta.normalized;
            attackTimer -= dt; dashTimer -= dt; stoneTimer -= dt;
            if (dashTimer <= 0 && dashRemaining <= 0) BeginDash();
            if (dashRemaining > 0)
            {
                dashRemaining -= dt;
                Vector2 from = player.Position;
                Vector2 step = dashDirection * (25 * dt);
                // Sweep damage before the solid body advances: enemies in the path never get tunneled through.
                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = enemies[i];
                    if (!dashVictims.Contains(enemy) && SegmentDistance(enemy.Position, from, from + step) < 1.12f)
                    { dashVictims.Add(enemy); DashHits++; Damage(enemy, 90, dashDirection); }
                }
                player.body.linearVelocity = dashDirection * 25;
                trailTimer -= dt;
                if (trailTimer <= 0) { Trail(); trailTimer = .035f; }
            }
            else
            {
                Vector2 desired = autoPlay ? delta.normalized * (delta.magnitude > 1.35f ? moveSpeed : .45f) : manualInput * moveSpeed;
                // Steer around boulders; the real colliders remain authoritative.
                foreach (var obstacle in obstacles)
                {
                    Vector2 away = player.Position - obstacle;
                    if (away.sqrMagnitude < 3.8f && Vector2.Dot(desired, away) < 0)
                        desired += new Vector2(-away.y, away.x).normalized * moveSpeed + away.normalized * 2;
                }
                player.body.linearVelocity = desired;
            }
            foreach (var enemy in enemies)
            {
                Vector2 toPlayer = player.Position - enemy.Position;
                Vector2 wander = new Vector2(Mathf.Sin(Elapsed * .5f + enemy.phase), Mathf.Cos(Elapsed * .43f + enemy.phase));
                enemy.body.linearVelocity = toPlayer.normalized * .6f + wander * .28f;
            }
            if (attackTimer <= 0 && delta.sqrMagnitude < 24)
            { FireSlash(facing); attackTimer = attackInterval; }
            if (stoneTimer <= 0) { ThrowStones(); stoneTimer = stoneInterval; }
            OrbitBananas(dt);
            if (enemies.Count < refillBelow) Refill();
        }

        void LateUpdate()
        {
            if (!Ready) return;
            Vector3 desired = new Vector3(Mathf.Clamp(player.Position.x * .65f, -6.5f, 6.5f), Mathf.Clamp(player.Position.y * .65f, -3, 3), -10);
            gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, desired, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 4));
        }

        Actor Closest(Vector2 origin)
        {
            Actor best = null; float distance = float.MaxValue;
            foreach (var enemy in enemies)
            {
                float d = (enemy.Position - origin).sqrMagnitude;
                if (d < distance) { distance = d; best = enemy; }
            }
            return best;
        }

        void BeginDash()
        {
            Actor selected = null; float score = float.MaxValue;
            foreach (var enemy in enemies)
            {
                float distance = Vector2.Distance(enemy.Position, player.Position);
                if (distance >= 4 && distance <= 9 && Mathf.Abs(distance - 6) < score)
                { score = Mathf.Abs(distance - 6); selected = enemy; }
            }
            if (selected == null) selected = Closest(player.Position);
            if (selected == null) return;
            dashDirection = (selected.Position - player.Position).normalized;
            dashRemaining = Mathf.Min(Vector2.Distance(selected.Position, player.Position), 8) / 25f;
            dashTimer = dashInterval;
            dashVictims.Clear();
            DashCasts++;
        }

        void FireSlash(Vector2 direction)
        {
            swing = 1;
            var sprite = Visual("Club slash wave", slash, player.Position + direction * .65f, Vector2.one * 1.6f, 500);
            sprite.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
            sprite.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            shots.Add(new Shot { visual = sprite.transform, start = sprite.transform.position, direction = direction, duration = .42f });
        }

        void ThrowStones()
        {
            nearest.Clear(); nearest.AddRange(enemies);
            Vector2 origin = player.Position;
            nearest.Sort((a, b) => (a.Position - origin).sqrMagnitude.CompareTo((b.Position - origin).sqrMagnitude));
            for (int i = 0; i < Mathf.Min(3, nearest.Count); i++)
            {
                var sprite = Visual("Parabolic stone", sprites[6], origin, Vector2.one * .43f, 550);
                sprite.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
                shots.Add(new Shot { visual = sprite.transform, start = origin, end = nearest[i].Position, target = nearest[i], duration = .65f + i * .06f, stone = true });
                StonesLaunched++;
            }
        }

        void OrbitBananas(float dt)
        {
            orbitAngle += dt * 2.1f;
            for (int n = 0; n < bananas.Length; n++)
            {
                float angle = orbitAngle + n * Mathf.PI * 2 / 5;
                Vector2 p = player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * bananaRadius;
                Vector2 previous = bananas[n].position;
                bananas[n].position = p;
                bananas[n].rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 35);
                bananas[n].GetComponent<SpriteRenderer>().sortingOrder = Order(p) + 5;
                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = enemies[i];
                    // A swept trigger avoids missing enemies during a dash. Shared cooldown prevents five instant hits.
                    if (SegmentDistance(enemy.Position, previous, p) > .72f) continue;
                    if (bananaHitTimes.TryGetValue(enemy, out float last) && Elapsed - last < .35f) continue;
                    bananaHitTimes[enemy] = Elapsed; BananaHits++;
                    Damage(enemy, 19, (enemy.Position - player.Position).normalized);
                }
            }
        }

        void UpdateShots(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i]; shot.age += dt;
                float t = Mathf.Clamp01(shot.age / shot.duration);
                if (shot.stone)
                {
                    if (shot.target != null && shot.target.root) shot.end = shot.target.Position;
                    shot.visual.position = Vector2.Lerp(shot.start, shot.end, t) + Vector2.up * (4 * 2.5f * t * (1 - t));
                    shot.visual.Rotate(0, 0, dt * 640);
                    if (t >= 1)
                    {
                        if (shot.target != null && shot.target.root) Damage(shot.target, 42, (shot.end - shot.start).normalized);
                        Burst(shot.end, new Color(.57f, .53f, .46f), 5);
                    }
                }
                else
                {
                    Vector2 old = shot.visual.position;
                    Vector2 next = old + shot.direction * (dt * 11);
                    shot.visual.position = next;
                    shot.visual.localScale = Vector3.one * (1.3f + t * .7f);
                    shot.visual.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 1 - t);
                    for (int n = enemies.Count - 1; n >= 0; n--)
                    {
                        var enemy = enemies[n];
                        if (!shot.hit.Contains(enemy) && SegmentDistance(enemy.Position, old, next) < 1)
                        { shot.hit.Add(enemy); SlashHits++; Damage(enemy, 29, shot.direction); }
                    }
                }
                if (t >= 1) { Destroy(shot.visual.gameObject); shots.RemoveAt(i); }
            }
        }

        void Damage(Actor enemy, float amount, Vector2 push)
        {
            if (enemy.hp <= 0) return;
            enemy.hp -= amount; enemy.flash = .14f;
            enemy.body.AddForce(push * 2, ForceMode2D.Impulse);
            Burst(enemy.Position, new Color(1, .96f, .73f), 2);
            if (enemy.hp > 0) return;
            Kills++;
            Burst(enemy.Position, new Color(.96f, .9f, .7f), 7);
            enemies.Remove(enemy); bananaHitTimes.Remove(enemy);
            // Disable the collider immediately; Destroy is deferred until the end of the frame.
            enemy.root.SetActive(false);
            Destroy(enemy.root);
        }

        void Animate(Actor actor)
        {
            actor.flash = Mathf.Max(0, actor.flash - Time.deltaTime);
            actor.art.color = actor.flash > 0 ? new Color(1, .55f, .42f) : Color.white;
            actor.art.transform.localPosition = new Vector3(0, Mathf.Sin(Elapsed * 7 + actor.phase) * .045f, 0);
            actor.art.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Elapsed * 5 + actor.phase) * 3);
            actor.art.sortingOrder = Order(actor.Position);
            actor.root.transform.GetChild(0).GetComponent<SpriteRenderer>().sortingOrder = actor.art.sortingOrder - 2;
        }

        void Trail()
        {
            var sprite = Visual("Dash afterimage", sprites[0], player.Position, Vector2.one * 1.2f, Order(player.Position) - 3);
            sprite.color = new Color(1, 1, 1, .35f);
            flecks.Add(new Fleck { visual = sprite.transform, sprite = sprite, lifetime = .24f, remaining = .24f });
        }

        void Burst(Vector2 p, Color color, int count)
        {
            for (int n = 0; n < count; n++)
            {
                var sprite = Visual("Hit dust", disc, p, Vector2.one * UnityEngine.Random.Range(.07f, .17f), 600);
                sprite.color = color;
                flecks.Add(new Fleck { visual = sprite.transform, sprite = sprite, velocity = UnityEngine.Random.insideUnitCircle * 3, remaining = .35f, lifetime = .35f });
            }
        }

        void UpdateFlecks(float dt)
        {
            for (int i = flecks.Count - 1; i >= 0; i--)
            {
                var f = flecks[i]; f.remaining -= dt;
                f.visual.position += f.velocity * dt;
                Color color = f.sprite.color; color.a = Mathf.Min(color.a, f.remaining / f.lifetime); f.sprite.color = color;
                if (f.remaining <= 0) { Destroy(f.visual.gameObject); flecks.RemoveAt(i); }
            }
        }

        public void TogglePause()
        {
            paused = !paused;
            player.body.simulated = !paused;
            foreach (var enemy in enemies) enemy.body.simulated = !paused;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.00001f, d.sqrMagnitude)));
        }
        static int Order(Vector2 position) => 100 - Mathf.RoundToInt(position.y * 10);

        SpriteRenderer Visual(string label, Sprite sprite, Vector2 p, Vector2 scale, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(world);
            go.transform.position = p; go.transform.localScale = new Vector3(scale.x, scale.y, 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sortingOrder = order; renderer.sharedMaterial = spriteMaterial;
            return renderer;
        }

        static Sprite MakeDisc()
        {
            var texture = new Texture2D(48, 48, TextureFormat.RGBA32, false);
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((23 - Vector2.Distance(new Vector2(x, y), new Vector2(23.5f, 23.5f))) * 1.5f)));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 48, 48), Vector2.one * .5f, 48);
        }

        static Sprite MakeSlash()
        {
            var texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++)
            {
                float a = Vector2.Distance(new Vector2(x, y), new Vector2(40, 48));
                float b = Vector2.Distance(new Vector2(x, y), new Vector2(28, 48));
                bool inside = a < 43 && b > 42;
                bool edge = a > 40 || b < 45;
                texture.SetPixel(x, y, !inside ? Color.clear : edge ? new Color(.22f, .2f, .16f) : new Color(1, .99f, .88f));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 96, 96), Vector2.one * .5f, 96);
        }

        void BuildHud()
        {
            uiFont = Resources.Load<Font>("DoodleIdle/InterfaceFont");
            if (!uiFont) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Prototype HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1440, 900); scaler.matchWidthOrHeight = .5f;
            if (!FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
                new GameObject("Event System", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(transform);
            var top = Panel(go.transform, "Header", new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -100), new Vector2(-22, -20));
            Label(top, "낙서 원정대", 28, new Vector2(22, 32), new Vector2(250, 40), TextAnchor.MiddleLeft);
            Label(top, "머리 하나, 방망이 하나. 알아서 싸우는 중!", 13, new Vector2(24, 9), new Vector2(360, 26), TextAnchor.MiddleLeft);
            populationText = Label(top, "", 22, new Vector2(-510, 24), new Vector2(180, 35), TextAnchor.MiddleCenter, new Vector2(1, 0));
            killsText = Label(top, "", 22, new Vector2(-320, 24), new Vector2(170, 35), TextAnchor.MiddleCenter, new Vector2(1, 0));
            timeText = Label(top, "", 22, new Vector2(-145, 24), new Vector2(135, 35), TextAnchor.MiddleCenter, new Vector2(1, 0));
            var bottom = Panel(go.transform, "Skill dock", Vector2.zero, new Vector2(1, 0), new Vector2(22, 20), new Vector2(-22, 124));
            SkillCard(bottom, "방망이 검기", "자동 평타", sprites[4], 18, false);
            dashFill = SkillCard(bottom, "쌩! 대시", "5초마다 먼 적에게", sprites[0], 260, true);
            SkillCard(bottom, "빙글 바나나", "5개가 계속 공전", sprites[5], 502, false);
            stoneFill = SkillCard(bottom, "돌멩이 톡톡", "가까운 적 3명 · 포물선", sprites[6], 744, true);
            pauseText = Button(bottom, "일시정지", new Vector2(-204, 54), new Vector2(180, 36), TogglePause);
            Button(bottom, "다시 시작", new Vector2(-204, 12), new Vector2(180, 34), ResetGame);
            modeText = Label(go.transform, "", 14, new Vector2(30, 135), new Vector2(800, 28), TextAnchor.MiddleLeft);
            waveText = Label(go.transform, "", 14, new Vector2(-390, 135), new Vector2(360, 28), TextAnchor.MiddleRight, new Vector2(1, 0));
        }

        RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = min; rect.offsetMax = max;
            go.GetComponent<Image>().color = new Color(1, .98f, .92f, .96f);
            var outline = go.GetComponent<Outline>(); outline.effectColor = new Color(.22f, .2f, .16f, .8f); outline.effectDistance = new Vector2(2, -2);
            return rect;
        }

        Text Label(Transform parent, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor align, Vector2? anchor = null)
        {
            var go = new GameObject(value.Length > 0 ? value : "Counter", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor ?? Vector2.zero; rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var text = go.GetComponent<Text>(); text.font = uiFont; text.text = value; text.fontSize = size; text.alignment = align; text.color = new Color(.22f, .2f, .17f); text.raycastTarget = false;
            return text;
        }

        Image SkillCard(Transform parent, string title, string subtitle, Sprite icon, float x, bool progress)
        {
            var go = new GameObject(title + " icon", typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, 25); rect.sizeDelta = new Vector2(56, 56);
            go.GetComponent<Image>().sprite = icon; go.GetComponent<Image>().preserveAspect = true;
            Label(parent, title, 18, new Vector2(x + 66, 52), new Vector2(178, 30), TextAnchor.MiddleLeft);
            Label(parent, subtitle, 12, new Vector2(x + 66, 31), new Vector2(178, 24), TextAnchor.MiddleLeft);
            if (!progress) return null;
            var bar = new GameObject(title + " cooldown", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(parent, false);
            var r = bar.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero; r.anchoredPosition = new Vector2(x + 66, 21); r.sizeDelta = new Vector2(155, 5);
            var image = bar.GetComponent<Image>(); image.color = new Color(.57f, .65f, .4f); return image;
        }

        Text Button(Transform parent, string value, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var panel = Panel(parent, value, new Vector2(1, 0), new Vector2(1, 0), position, position + size);
            panel.gameObject.AddComponent<Button>().onClick.AddListener(action);
            return Label(panel, value, 15, Vector2.zero, size, TextAnchor.MiddleCenter);
        }

        void UpdateHud()
        {
            populationText.text = "적  " + enemies.Count + " / " + targetPopulation;
            killsText.text = "처치  " + Kills;
            timeText.text = TimeSpan.FromSeconds(Elapsed).ToString(@"mm\:ss");
            waveText.text = "20마리 미만이면 80마리까지 보충  ·  " + Refills + "회";
            modeText.text = paused ? "잠깐 쉬는 중  ·  SPACE로 계속" : (autoPlay ? "● 자동 전투" : "● 직접 이동 · WASD / 방향키") + "    SPACE 일시정지    TAB 이동 모드    R 다시 시작";
            pauseText.text = paused ? "계속하기" : "일시정지";
            dashFill.rectTransform.sizeDelta = new Vector2(155 * Mathf.Clamp01(1 - dashTimer / dashInterval), 5);
            stoneFill.rectTransform.sizeDelta = new Vector2(155 * Mathf.Clamp01(1 - stoneTimer / stoneInterval), 5);
        }

        // Called by the automated editor smoke run after real physics frames.
        public string Diagnostics()
        {
            float penetration = 0;
            for (int i = 0; i < enemies.Count; i++) for (int j = i + 1; j < enemies.Count; j++)
                penetration = Mathf.Max(penetration, 1.12f - Vector2.Distance(enemies[i].Position, enemies[j].Position));
            return JsonUtility.ToJson(new Report { elapsed = Elapsed, enemies = EnemyCount, kills = Kills, refills = Refills, dashes = DashCasts, stones = StonesLaunched, bananaHits = BananaHits, slashHits = SlashHits, dashHits = DashHits, maxEnemyPenetration = penetration, bananas = bananas.Length });
        }
        [Serializable] class Report
        {
            public float elapsed, maxEnemyPenetration;
            public int enemies, kills, refills, dashes, stones, bananaHits, slashHits, dashHits, bananas;
        }

        void OnDestroy()
        {
            if (spriteMaterial) Destroy(spriteMaterial);
            if (frictionless) Destroy(frictionless);
            if (disc) { Destroy(disc.texture); Destroy(disc); }
            if (slash) { Destroy(slash.texture); Destroy(slash); }
            if (sprites != null) foreach (var sprite in sprites) if (sprite) Destroy(sprite);
        }
    }
}
