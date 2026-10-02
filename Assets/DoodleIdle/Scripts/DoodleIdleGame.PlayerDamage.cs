using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        [Header("Player contact damage")]
        [Min(0)] public float enemyContactDamage = 64;
        [Min(.1f)] public float enemyAttackInterval = 1;
        public const float ContactInvulnerabilityDuration = 1;
        float contactInvulnerability;
        Material playerHitMaterial;
        public float PlayerHealth => player == null ? 0 : (float)player.hp;
        public float PlayerMaxHealth => player == null ? 0 : (float)player.maxHp;
        public GameNumber PlayerHealthAmount => player == null ? 0 : player.hp;
        public GameNumber PlayerMaxHealthAmount => player == null ? 0 : player.maxHp;
        public bool PlayerInvulnerable => contactInvulnerability > .0001f;
        public int PlayerContactHits { get; private set; }

        void ResetPlayerContactDamage()
        {
            player.hp = player.maxHp = (Ui ? Ui.MaxHealthAmount : 1280) * (IsPvpEngine ? 100 : 1);
            contactInvulnerability = 0; PlayerContactHits = 0;
            if (!player.healthFill) AddHealthBar(player);
            player.healthBack.name = "Player HP background";
            player.healthFill.name = "Player HP fill";
            UpdatePlayerHealthBar();
        }

        void TickPlayerContactDamage(float dt)
        {
            if (!Alive(player)) return;
            // Include inspector edits and newly spawned/scaled prefab triggers before querying.
            Physics2D.SyncTransforms();
            contactInvulnerability = Mathf.Max(0, contactInvulnerability - dt);
            GameNumber maxHealth = Ui ? Ui.MaxHealthAmount : 1280;
            // Preserve missing HP when equipment/stat maximum health changes.
            player.hp = GameNumber.Clamp(player.hp + maxHealth - player.maxHp + (Ui ? Ui.HealthRegenAmount * dt : 0), 0, maxHealth);
            player.maxHp = maxHealth;
            foreach (var enemy in enemies)
            {
                if (!Alive(enemy) || enemy.returnedToPool || enemy.hitStop > 0) continue;
                enemy.meleeCooldown = Mathf.Max(0, enemy.meleeCooldown - dt);
                if (enemy.meleeCooldown > 0 || !EnemyInAttackRange(enemy)) continue;
                // Even harmless tutorial enemies play their attack. Damage is resolved
                // only when their clip reaches OnAttackImpact, after the wind-up.
                if (enemy.rigVisual && enemy.rigVisual.TryAttack(enemy.attackImpact ??= CreateEnemyAttackImpact(enemy)))
                    enemy.meleeCooldown = enemyAttackInterval;
            }
            UpdatePlayerHealthBar();
        }

        // Construct the closure only for an enemy that actually attacks. Capturing the
        // foreach variable inline allocates a closure for every enemy on every physics step.
        System.Action CreateEnemyAttackImpact(Actor enemy) => () => ResolveEnemyAttack(enemy);

        bool EnemyInAttackRange(Actor enemy)
        {
            return enemy.rigVisual && enemy.rigVisual.Rig.ContainsAttackTarget(player.collider);
        }

        void ResolveEnemyAttack(Actor enemy)
        {
            Physics2D.SyncTransforms();
            if (!Ready || paused || !Alive(player) || !Alive(enemy) || enemy.returnedToPool || enemy.hitStop > 0
                || !enemy.root.activeInHierarchy || PlayerInvulnerable || !EnemyInAttackRange(enemy)) return;
            GameNumber damage = enemyContactDamage * (Ui ? Ui.EnemyDamageAmount(Ui.CombatDifficultyStage) / 64 : 1);
            // Zero-damage attacks still show the damage number and hit feedback.
            if (damage < 0) return;
            player.hp = GameNumber.Max(0, player.hp - damage);
            ShowDamageNumber(player.Position, damage, true);
            PlayerContactHits++;
            ApplyHitStop(player);
            EmitHitSlash(player.Position);
            contactInvulnerability = ContactInvulnerabilityDuration;
            if (player.hp <= 0)
            {
                dashRemaining = 0;
                playerDefeatPending = true;
                playerRespawnRemaining = PlayerRespawnDelay;
                player.collider.enabled = false;
            }
            UpdatePlayerHealthBar();
        }

        Color PlayerInvulnerabilityTint(Color normal)
        {
            if (!PlayerInvulnerable) return normal;
            return new Color(normal.r, normal.g, normal.b, PlayerFadedHitPhase ? .6f : .8f);
        }

        bool PlayerFadedHitPhase => PlayerInvulnerable && Mathf.FloorToInt((ContactInvulnerabilityDuration - contactInvulnerability) / .25f) % 2 == 0;

        void ApplyPlayerHitAppearance()
        {
            player.art.color = PlayerInvulnerabilityTint(player.art.color);
            if (player.rigVisual) { player.rigVisual.Sync(); return; }
            if (PlayerFadedHitPhase) {
                if (!playerHitMaterial) playerHitMaterial = new Material(Resources.Load<Shader>("DoodleIdle/DoodlePlayerHit")) { name = "Doodle player hit fade" };
                playerHitMaterial.mainTexture = player.art.sprite.texture;
                // SpriteRenderer alpha is not provided through vertex color in every URP batching path.
                playerHitMaterial.SetFloat("_Opacity", player.art.color.a);
                playerHitMaterial.SetColor("_TintColor", new Color(player.art.color.r, player.art.color.g, player.art.color.b, 1));
                player.art.sharedMaterial = playerHitMaterial;
            }
            else if (playerHitMaterial && player.art.sharedMaterial == playerHitMaterial) SetSpriteArt(player.art, player.art.sprite);
        }

        void UpdatePlayerHealthBar()
        {
            bool visible = player.hp < player.maxHp || PlayerInvulnerable;
            player.healthBack.enabled = player.healthFill.enabled = visible;
            RefreshHealthBar(player);
        }
    }
}
