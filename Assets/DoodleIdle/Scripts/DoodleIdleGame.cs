using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

namespace DoodleIdle
{
    /// <summary>Self-contained, automatic 2D combat sandbox. All tuning is exposed in the Inspector.</summary>
    public sealed partial class DoodleIdleGame : MonoBehaviour
    {
        [Header("Population")]
        public int targetPopulation = 200;
        public int refillBelow = 100;
        public Vector2 arenaHalfSize = new Vector2(17, 20);
        public const float SlashSpeed = 11;
        [Header("Combat")]
        public float moveSpeed = 3.1f;
        public float attackInterval = .65f;
        public float dashInterval = 5f;
        public float stoneInterval = 3.2f;
        public float bananaRadius = 1.9f;
        public bool autoPlay = true;
        public bool basicSkillsEnabled = true;
        public bool paused;
        public bool BasicAttackEnabled { get; private set; } = true;
        public void SetBasicAttackEnabled(bool enabled)
        {
            BasicAttackEnabled = enabled;
            if (enabled) return;
            dashRemaining = swing = 0;
            if (player != null) player.body.linearVelocity = Vector2.zero;
            for (int i = shots.Count - 1; i >= 0; i--)
                if (!shots[i].stone) { Destroy(shots[i].visual.gameObject); shots.RemoveAt(i); }
        }

        public int EnemyCount => enemies.Count;
        public int Kills { get; private set; }
        public int Refills { get; private set; }
        public int DashCasts { get; private set; }
        public float FirstDashTime { get; private set; }
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
            public SpriteRenderer healthBack, healthFill;
            public CircleCollider2D collider;
            public float hp = 68, maxHp = 68, flash, phase;
            public float walkClock;
            public float dashCooldown, dashWindup, enemyDashRemaining, dashTrail;
            public Vector2 enemyDashDirection;
            public bool isPlayer, isBoss;
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
        readonly List<Actor> enemies = new List<Actor>(200);
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Fleck> flecks = new List<Fleck>();
        readonly List<Actor> nearest = new List<Actor>(200);
        readonly HashSet<Actor> dashVictims = new HashSet<Actor>();
        readonly Dictionary<Actor, float> bananaHitTimes = new Dictionary<Actor, float>();
        readonly Transform[] bananas = new Transform[5];
        Sprite[] sprites;
        Sprite disc, slash, groundSprite;
        Actor player;
        Transform world, weapon;
        Camera gameCamera;
        PhysicsMaterial2D frictionless;
        float attackTimer, dashTimer, stoneTimer, dashRemaining, orbitAngle, swing, trailTimer;
        float combatStartFixedTime;
        Vector2 dashDirection, facing = Vector2.right, manualInput;
        Text populationText, killsText, timeText, waveText, modeText, pauseText;
        Image dashFill, stoneFill;
        Font uiFont;
        Material spriteMaterial;
        readonly Dictionary<Texture, Material> textureMaterials = new Dictionary<Texture, Material>();
        Transform hudRoot;
        bool portraitHud;
        bool combatWaveResetRequested;
        public bool BossActive => enemies.Exists(x => x.isBoss);
        public void RequestCombatWaveReset() => combatWaveResetRequested = true;

        void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
            sprites = LoadAtlas();
            LoadSkillArt();
            LoadSummonArt();
            LoadActorAnimations();
            disc = MakeDisc();
            slash = MakeSlash();
            // The legacy sprite shader can reuse the floor texture in URP's 2D batching path.
            // Use the pipeline's sprite shader so each renderer binds its own texture/color.
            string shaderName = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? "Universal Render Pipeline/2D/Sprite-Unlit-Default" : "Sprites/Default";
            spriteMaterial = new Material(Shader.Find(shaderName));
            frictionless = new PhysicsMaterial2D("Doodle frictionless") { friction = 0, bounciness = 0 };
            world = new GameObject("Doodle world").transform;
            world.SetParent(transform);
            BuildParticles();
            BuildCamera();
            BuildGround();
            BuildArena();
            BuildHud();
            BuildCombatFeedback();
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
                Vector2 pivot = i == 4 ? new Vector2(.15f, .12f) : Vector2.one * .5f;
                result[i] = Sprite.Create(texture, new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1), pivot, Mathf.Max(maxX - minX + 1, maxY - minY + 1));
                if (i == 0) result[i].name = "PlayerWalkA";
            }
            return result;
        }

        void BuildCamera()
        {
            gameCamera = Camera.main;
            if (!gameCamera)
            {
                gameCamera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                gameCamera.transform.SetParent(transform);
            }
            gameCamera.tag = "MainCamera";
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 8.5f;
            gameCamera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            gameCamera.backgroundColor = new Color(.91f, .83f, .68f);
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        void BuildGround()
        {
            var texture = Texture2D.whiteTexture;
            groundSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, texture.width, 0, SpriteMeshType.FullRect);
            // One opaque surface avoids gaps, tight sprite meshes and camera-dependent tile edges.
            var floor = Visual("Generated dirt floor", groundSprite, Vector2.zero, (arenaHalfSize + Vector2.one * 40) * 2, -1000);
            groundMaterial = new Material(Resources.Load<Shader>("DoodleIdle/DoodleTerrain"));
            groundMaterial.mainTexture = texture;
            groundMaterial.SetTexture("_AtlasTex", Resources.Load<Texture2D>("DoodleIdle/Themes/Grounds"));
            groundMaterial.SetFloat("_TileSize", 3f);
            floor.sharedMaterial = groundMaterial;
            groundTiles.Add(floor);
        }

        void BuildArena()
        {
            Wall(new Vector2(-arenaHalfSize.x - .5f, 0), new Vector2(1, arenaHalfSize.y * 2 + 2));
            Wall(new Vector2(arenaHalfSize.x + .5f, 0), new Vector2(1, arenaHalfSize.y * 2 + 2));
            Wall(new Vector2(0, -arenaHalfSize.y - .5f), new Vector2(arenaHalfSize.x * 2, 1));
            Wall(new Vector2(0, arenaHalfSize.y + .5f), new Vector2(arenaHalfSize.x * 2, 1));

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
            combatWaveResetRequested = false;
            ReleaseJoystick();
            ClearParticles();
            ClearDamageNumbers();
            ClearExtraSkills();
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
            EnemyDashCasts = 0;
            Elapsed = orbitAngle = dashRemaining = 0;
            ResetSkillActivation();
            bananaCycleAge = 0; BananasActive = SkillEquipped("Banana");
            FirstDashTime = 0;
            combatStartFixedTime = Time.fixedTime;
            dashTimer = dashInterval; stoneTimer = 1.2f; attackTimer = .3f;
            paused = false;
            player = CreateActor(true, Vector2.zero, 0);
            ResetPlayerContactDamage();
            weapon = Visual("Floating baseball club", sprites[4], Vector2.zero, Vector2.one * .94f, 20).transform;
            weapon.SetParent(player.root.transform, false);
            for (int i = 0; i < 5; i++)
            {
                bananas[i] = Visual("Orbit banana " + (i + 1), sprites[5], Vector2.zero, Vector2.one * 1.16f, 80).transform;
                var trigger = bananas[i].gameObject.AddComponent<CircleCollider2D>();
                trigger.radius = .4f; trigger.isTrigger = true;
                bananas[i].gameObject.SetActive(BananasActive);
            }
            Refill();
            Refills = 0;
            ResetExtraSkills();
        }

        Actor CreateActor(bool isPlayer, Vector2 p, int kind)
        {
            var root = new GameObject(isPlayer ? "Player - head and club" : "Enemy - " + ThemeEnemies[CurrentThemeIndex][kind]);
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
            var shadow = Visual("Soft ground shadow", disc, p + new Vector2(0, -.58f), new Vector2(1.15f, .42f), -900);
            shadow.color = new Color(.08f, .07f, .06f, .32f);
            shadow.transform.SetParent(root.transform, true);
            var art = Visual("Generated head sprite", isPlayer ? sprites[0] : enemyWalkFrames[kind][0], p, Vector2.one * (isPlayer ? 1.28f : 1.10f), Order(p));
            art.transform.SetParent(root.transform, true);
            var actor = new Actor { root = root, body = body, art = art, collider = collider, phase = UnityEngine.Random.value * 6.28f, kind = kind, isPlayer = isPlayer };
            if (!isPlayer)
            {
                actor.hp = actor.maxHp = EnemyMaxHealth * (Ui ? Ui.EnemyHealthMultiplier(Ui.CombatDifficultyStage) : 1);
                actor.dashCooldown = 2 + actor.phase * .4f;
                NormalizeEnemyFrame(actor,art.sprite);
                actor.art.flipX = player.Position.x < p.x;
            }
            if (!isPlayer) AddHealthBar(actor);
            return actor;
        }

        void Refill()
        {
            ApplyStageTheme();
            if (Ui && Ui.ActiveDungeonIndex < 0 && Ui.MainBossPending)
            {
                if (BossActive) return;
                // A hundred credited kills starts the challenge, regardless of surviving field enemies.
                foreach (var enemy in enemies) { enemy.hp = 0; enemy.root.SetActive(false); Destroy(enemy.root); }
                enemies.Clear(); bananaHitTimes.Clear(); dashVictims.Clear();
                var boss = CreateActor(false, new Vector2(Mathf.Clamp(player.Position.x + 5, -arenaHalfSize.x + 3, arenaHalfSize.x - 3), Mathf.Clamp(player.Position.y, -arenaHalfSize.y + 3, arenaHalfSize.y - 3)), 2);
                boss.isBoss = true; boss.hp = boss.maxHp = boss.maxHp * 20;
                boss.root.name = "Stage boss";
                boss.root.transform.localScale = Vector3.one * 3;
                boss.body.mass = 9;
                RefreshHealthBar(boss); enemies.Add(boss);
                return;
            }
            if (BossActive) return;
            bool dungeon = Ui && Ui.ActiveDungeonIndex >= 0;
            int population = dungeon ? 100 : targetPopulation;
            if (enemies.Count > 0 && (dungeon || enemies.Count >= refillBelow)) return;
            int needed = Mathf.Max(0, population - enemies.Count);
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
                    if (found) break;
                }
                // The wide arena normally finds a free position. Still create every
                // member of the wave so the 100-kill objective cannot get stranded.
                enemies.Add(CreateActor(false, p, UnityEngine.Random.Range(0, 3)));
            }
            Refills++;
        }

        void Update()
        {
            if (!Ready) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && (!Ui || !Ui.BlocksGameplay))
            {
                if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
                if (keyboard.rKey.wasPressedThisFrame) ResetGame();
                if (keyboard.tabKey.wasPressedThisFrame) autoPlay = !autoPlay;
                manualInput = new Vector2((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0)).normalized;
            }
            RefreshHudLayout();
            UpdateJoystick();
            UpdateHud();
            if (paused) return;
            float dt = Time.deltaTime;
            UpdateFlecks(dt);
            UpdateExtraVisuals(dt);
            swing = Mathf.MoveTowards(swing, 0, dt * 5);
            Animate(player);
            UpdateHeldClub();
            foreach (var enemy in enemies) Animate(enemy);
        }

        void FixedUpdate()
        {
            if (!Ready || paused) return;
            if (combatWaveResetRequested)
            {
                combatWaveResetRequested = false;
                foreach (var enemy in enemies) { enemy.hp = 0; enemy.root.SetActive(false); Destroy(enemy.root); }
                enemies.Clear(); bananaHitTimes.Clear(); dashVictims.Clear();
                Refill();
            }
            float dt = Time.fixedDeltaTime;
            Elapsed += dt;
            var target = Closest(player.Position);
            if (target == null) { Refill(); return; }
            Vector2 delta = target.Position - player.Position;
            if (delta.sqrMagnitude > .01f) facing = delta.normalized;
            attackTimer -= dt; dashTimer -= dt; stoneTimer -= dt;
            if (basicSkillsEnabled && BasicAttackEnabled && !JoystickActive && dashTimer <= 0 && dashRemaining <= 0) BeginDash();
            if (dashRemaining > 0)
            {
                dashRemaining -= dt;
                Vector2 from = player.Position;
                Vector2 step = dashDirection * (25 * dt);
                Vector2 safeStep = LimitAutomaticStep(step);
                if (safeStep.sqrMagnitude < step.sqrMagnitude - .000001f) dashRemaining = 0;
                step = safeStep;
                // Sweep damage before the solid body advances: enemies in the path never get tunneled through.
                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = enemies[i];
                    float reach = KeepsEnemyDistance ? ActorRadius(player) + ActorRadius(enemy) + .75f : 1.12f;
                    if (!dashVictims.Contains(enemy) && SegmentDistance(enemy.Position, from, from + step) < reach)
                    { dashVictims.Add(enemy); DashHits++; Damage(enemy, 90, dashDirection); }
                }
                player.body.linearVelocity = step / dt;
                trailTimer -= dt;
                if (trailTimer <= 0) { Trail(); trailTimer = .035f; }
            }
            else
            {
                Vector2 desired = JoystickActive ? joystickInput * moveSpeed : autoPlay ? AutomaticMoveVelocity(target, dt) : manualInput * moveSpeed;
                player.body.linearVelocity = desired;
            }
            TickEnemyMovement(dt);
            TickPlayerContactDamage(dt);
            if (basicSkillsEnabled && BasicAttackEnabled && attackTimer <= 0 && delta.sqrMagnitude < 24)
            { FireSlash(facing); attackTimer = attackInterval / (Ui ? Mathf.Max(1, Ui.UiSpeedMultiplier) : 1); }
            TickEquippedSkills(dt);
            OrbitBananas(dt);
            TickCompanions(dt);
            TickExpansionSkills(dt);
            TickVariants(dt);
            TickExtraSkills(dt);
            TickSummons(dt);
            TickParticles(dt);
            TickDamageNumbers(dt);
            // Damage-bearing projectiles share the physics clock with actors and skills.
            // A slow render frame must not extend a slash beyond its intended lifetime/range.
            UpdateShots(dt);
            if (enemies.Count < refillBelow || (Ui && Ui.MainBossPending)) Refill();
        }

        void LateUpdate()
        {
            if (!Ready) return;
            Vector3 desired = new Vector3(player.Position.x, player.Position.y, -10);
            gameCamera.transform.position = Vector3.Lerp(gameCamera.transform.position, desired, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 9));
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
            if (DashCasts == 0) FirstDashTime = Time.fixedTime - combatStartFixedTime;
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
            LaunchStone(0);
            stoneVolleys.Add(new StoneVolley { requiresEquipment = castingEquippedSkill });
        }

        void OrbitBananas(float dt)
        {
            bool equipped = SkillEquipped("Banana");
            bool debugActive = debugBananaRemaining > 0;
            debugBananaRemaining = Mathf.Max(0, debugBananaRemaining - dt);
            if (equipped) bananaCycleAge += dt; else bananaCycleAge = 0;
            if (bananaCycleAge >= OrbitSkillCycle) bananaCycleAge -= OrbitSkillCycle;
            bool wasActive = BananasActive;
            BananasActive = debugActive || (equipped && bananaCycleAge < OrbitSkillLifetime);
            if (equipped && !wasActive && BananasActive)
                skillActivationCounts["Banana"] = SkillActivationCount("Banana") + 1;
            orbitAngle += dt * 2.1f;
            for (int n = 0; n < bananas.Length; n++)
            {
                float angle = orbitAngle + n * Mathf.PI * 2 / 5;
                Vector2 p = player.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * bananaRadius;
                Vector2 previous = bananas[n].position;
                bananas[n].gameObject.SetActive(BananasActive);
                bananas[n].position = p;
                bananas[n].rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 35);
                bananas[n].GetComponent<SpriteRenderer>().sortingOrder = Order(p) + 5;
                if (!BananasActive || (!basicSkillsEnabled && !debugActive)) continue;
                if (!wasActive) previous = p;
                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = enemies[i];
                    // A swept trigger avoids missing enemies during a dash. Shared cooldown prevents five instant hits.
                    if (SegmentDistance(enemy.Position, previous, p) > 1.02f) continue;
                    if (bananaHitTimes.TryGetValue(enemy, out float last) && Elapsed - last < .35f) continue;
                    bananaHitTimes[enemy] = Elapsed; BananaHits++;
                    SkillDamage(enemy, 19, (enemy.Position - player.Position).normalized, "Banana");
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
                        if (Alive(shot.target)) SkillImpact(shot.target, 42, (shot.end - shot.start).normalized, "Stone");
                        Burst(shot.end, new Color(.57f, .53f, .46f), 5);
                    }
                }
                else
                {
                    Vector2 old = shot.visual.position;
                    Vector2 next = old + shot.direction * (dt * SlashSpeed);
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
            => DamageByCategory(enemy, amount, push, "Basic");

        void SkillDamage(Actor enemy, float weight, Vector2 push, string ability = null)
            => DamageByCategory(enemy, weight * (Ui && ability != null ? Ui.SkillPowerMultiplier(ability) : 1), push, "Skill");

        void SkillImpact(Actor target, float weight, Vector2 push, string ability)
        {
            if (!Alive(target)) return;
            Vector2 center = target.Position;
            var splash = DoodleAttackPower.SkillSplash(ability);
            SkillDamage(target, weight, push, ability);
            if (splash.radius <= 0) return;
            skillSplashCounts[ability] = SkillSplashCount(ability) + 1;
            if (splash.particleIndex < 0)
                EmitBurst(dustParticles, center, new Color(.74f,.70f,.62f,.85f), 9, .16f, .36f, splash.radius * 2.5f, .22f, .4f);
            else if (ability == "RedCloud")
                EmitBurst(companionImpactParticles[splash.particleIndex], center, new Color(1,.25f,.25f), 9, .16f, .32f, 3, .18f, .3f);
            else EmitCompanionImpact(splash.particleIndex, center, splash.radius);
            for (int i = enemies.Count - 1; i >= 0; i--) {
                var enemy = enemies[i];
                float contact = enemy.collider.radius * Mathf.Abs(enemy.root.transform.lossyScale.x);
                if (enemy != target && Vector2.Distance(enemy.Position, center) <= splash.radius + contact)
                    SkillDamage(enemy, weight * splash.fraction, (enemy.Position - center).normalized, ability);
            }
        }

        void DamageByCategory(Actor enemy, float weight, Vector2 push, string category)
        {
            if (enemy.hp <= 0) return;
            float amount = (Ui ? Ui.AttackPercentDamage(DoodleAttackPower.Percent(weight), category) : weight) * RollUiCriticalMultiplier();
            enemy.hp -= amount; enemy.flash = .14f;
            RefreshHealthBar(enemy);
            ShowDamageNumber(enemy.Position, amount);
            enemy.body.AddForce(push * 2, ForceMode2D.Impulse);
            Burst(enemy.Position, new Color(1, .96f, .73f), 2);
            if (enemy.hp > 0) return;
            Kills++;
            if (Ui) Ui.RecordMainCombatKill(enemy.isBoss);
            LeaveStain(enemy.Position);
            if(!Ui || Ui.ActiveDungeonIndex<0)EmitGold(enemy.Position);
            Burst(enemy.Position, new Color(.96f, .9f, .7f), 7);
            enemies.Remove(enemy); bananaHitTimes.Remove(enemy);
            // Disable the collider immediately; Destroy is deferred until the end of the frame.
            enemy.root.SetActive(false);
            Destroy(enemy.root);
        }

        void Animate(Actor actor)
        {
            AnimateActorFrames(actor, Time.deltaTime);
            actor.flash = Mathf.Max(0, actor.flash - Time.deltaTime);
            actor.art.color = actor.flash > 0 ? new Color(1, .55f, .42f) : actor.isPlayer && Ui ? Ui.EquippedAppearanceTint : Color.white;
            if (!actor.isPlayer && actor.dashWindup > 0 && actor.flash <= 0) actor.art.color = new Color(1, .75f, .65f);
            if (actor.isPlayer) ApplyPlayerHitAppearance();
            actor.art.transform.localPosition = new Vector3(0, Mathf.Sin(Elapsed * 7 + actor.phase) * .045f, 0);
            actor.art.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Elapsed * 5 + actor.phase) * 3);
            actor.art.sortingOrder = Order(actor.Position);
            RefreshHealthBar(actor);
            actor.root.transform.GetChild(0).GetComponent<SpriteRenderer>().sortingOrder = -900;
        }

        void UpdateHeldClub()
        {
            float side = facing.x < 0 ? -1 : 1;
            // Sprite pivot is inside the grip; the hand contact follows the head's bob and tilt.
            weapon.position = player.art.transform.TransformPoint(new Vector3(.4f * side, -.18f, 0));
            var art = weapon.GetComponent<SpriteRenderer>();
            ApplyWeaponSkin(art);
            art.flipX = side < 0;
            weapon.localRotation = Quaternion.Euler(0, 0, side * (-10 - Mathf.Sin(swing * Mathf.PI) * 105));
            art.sortingOrder = player.art.sortingOrder + 2;
        }

        void Trail()
        {
            var sprite = Visual("Dash afterimage", player.art.sprite, player.Position, Vector2.one * 1.2f, Order(player.Position) - 3);
            sprite.color = new Color(1, 1, 1, .35f);
            flecks.Add(new Fleck { visual = sprite.transform, sprite = sprite, lifetime = .24f, remaining = .24f });
        }

        void Burst(Vector2 p, Color color, int count)
        {
            EmitBurst(dustParticles, p, color, count, .28f, .68f, 2.8f, .35f, .55f);
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
            if (paused) ReleaseJoystick();
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
            renderer.sortingOrder = order;
            SetSpriteArt(renderer, sprite);
            return renderer;
        }

        void SetSpriteArt(SpriteRenderer renderer, Sprite sprite)
        {
            renderer.sprite = sprite;
            // Explicit bindings keep atlas, floor and effect textures in separate material batches.
            if (!textureMaterials.TryGetValue(sprite.texture, out var material))
            {
                material = new Material(spriteMaterial) { mainTexture = sprite.texture, name = "Doodle / " + sprite.texture.name };
                textureMaterials.Add(sprite.texture, material);
            }
            renderer.sharedMaterial = material;
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

        public DoodleUi Ui { get; private set; }
        void BuildHud()
        {
            uiFont = Resources.Load<Font>("DoodleIdle/InterfaceFont");
            var go = new GameObject("Prototype HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform);
            hudRoot = go.transform;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1520);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 1;
            if (!FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
                new GameObject("Event System", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(transform);
            Ui = gameObject.AddComponent<DoodleUi>(); Ui.Initialize(this, hudRoot);
            BuildJoystick();
        }
        public void RefreshHudLayout() { if (Ui) Ui.Relayout(); }
        public void CancelUiPointer() { ReleaseJoystick(); manualInput = Vector2.zero; }
        public float UiCooldown(string ability)
        {
            if (ability == "Banana") return SkillEquipped(ability) ? Mathf.Clamp01((OrbitSkillCycle - bananaCycleAge) / OrbitSkillCycle) : 0;
            return equippedSkillClocks.TryGetValue(ability, out var clock) ? Mathf.Clamp01(clock / Mathf.Max(.1f, SkillInterval(ability))) : 0;
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

        Image SkillCard(Transform parent, string title, string subtitle, Sprite icon, float x, bool progress, float y = 0)
        {
            var go = new GameObject(title + " icon", typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y + 25); rect.sizeDelta = new Vector2(56, 56);
            go.GetComponent<Image>().sprite = icon; go.GetComponent<Image>().preserveAspect = true;
            Label(parent, title, portraitHud ? 26 : 18, new Vector2(x + 66, y + 52), new Vector2(230, 32), TextAnchor.MiddleLeft);
            Label(parent, subtitle, portraitHud ? 20 : 12, new Vector2(x + 66, y + 26), new Vector2(230, 27), TextAnchor.MiddleLeft);
            if (!progress) return null;
            var bar = new GameObject(title + " cooldown", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(parent, false);
            var r = bar.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = Vector2.zero; r.pivot = Vector2.zero; r.anchoredPosition = new Vector2(x + 66, y + 16); r.sizeDelta = new Vector2(155, 5);
            var image = bar.GetComponent<Image>(); image.color = new Color(.57f, .65f, .4f); return image;
        }

        Text Button(Transform parent, string value, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var panel = Panel(parent, value, new Vector2(1, 0), new Vector2(1, 0), position, position + size);
            panel.gameObject.AddComponent<Button>().onClick.AddListener(action);
            return Label(panel, value, portraitHud ? 25 : 15, Vector2.zero, size, TextAnchor.MiddleCenter);
        }

        void UpdateHud() { if (Ui) Ui.RefreshHud(); }

        // Used by server-side PlayMode tests after real physics frames.
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
            orbitGunRecoil?.Kill();
            KillCannonTweens();
            DisposeParticleMaterials();
            if (spriteMaterial) Destroy(spriteMaterial);
            if (playerHitMaterial) Destroy(playerHitMaterial);
            foreach (var material in textureMaterials.Values) if (material) Destroy(material);
            textureMaterials.Clear();
            if (frictionless) Destroy(frictionless);
            if (disc) { Destroy(disc.texture); Destroy(disc); }
            if (slash) { Destroy(slash.texture); Destroy(slash); }
            if (groundSprite) Destroy(groundSprite);
            DisposeCompanionArt();
            DisposeSkillArt();
            DisposeSummonArt();
            DisposeActorAnimations();
            DisposeThemes();
            DisposeWorldSkins();
            if (sprites != null) foreach (var sprite in sprites) if (sprite) Destroy(sprite);
        }
    }
}
