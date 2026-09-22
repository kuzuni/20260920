using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        [Header("Player contact damage")]
        [Min(0)] public float enemyContactDamage = 64;
        public const float ContactInvulnerabilityDuration = 1;
        float contactInvulnerability;
        Material playerHitMaterial;
        public float PlayerHealth => player == null ? 0 : player.hp;
        public float PlayerMaxHealth => player == null ? 0 : player.maxHp;
        public bool PlayerInvulnerable => contactInvulnerability > .0001f;
        public int PlayerContactHits { get; private set; }

        void ResetPlayerContactDamage()
        {
            player.hp = player.maxHp = Ui ? Ui.MaxHealth : 1280;
            contactInvulnerability = 0; PlayerContactHits = 0;
            if (!player.healthFill) AddHealthBar(player);
            player.healthBack.name = "Player HP background";
            player.healthFill.name = "Player HP fill";
            UpdatePlayerHealthBar();
        }

        void TickPlayerContactDamage(float dt)
        {
            contactInvulnerability = Mathf.Max(0, contactInvulnerability - dt);
            float maxHealth = Ui ? Ui.MaxHealth : 1280;
            // Preserve missing HP when equipment/stat maximum health changes.
            player.hp = Mathf.Clamp(player.hp + maxHealth - player.maxHp + (Ui ? Ui.HealthRegen * dt : 0), 0, maxHealth);
            player.maxHp = maxHealth;
            if (!PlayerInvulnerable && enemyContactDamage > 0) {
                foreach (var enemy in enemies) {
                    if (!Alive(enemy)) continue;
                    Vector2 relative = enemy.Position - player.Position;
                    Vector2 next = relative + (enemy.body.linearVelocity - player.body.linearVelocity) * dt;
                    float radius = player.collider.radius * Mathf.Abs(player.root.transform.lossyScale.x)
                        + enemy.collider.radius * Mathf.Abs(enemy.root.transform.lossyScale.x);
                    if (SegmentDistance(Vector2.zero, relative, next) > radius + .02f) continue;
                    float damage=enemyContactDamage*(Ui?Ui.EnemyDamageMultiplier(Ui.CombatDifficultyStage):1);
                    player.hp = Mathf.Max(0, player.hp - damage);
                    ShowDamageNumber(player.Position, damage, true);
                    PlayerContactHits++; contactInvulnerability = ContactInvulnerabilityDuration;
                    if (player.hp <= 0) {
                        player.hp = player.maxHp;
                        player.body.position = Vector2.zero;
                        player.body.linearVelocity = Vector2.zero;
                        dashRemaining = 0;
                    }
                    break; // One shared immunity window, including contact with other enemies.
                }
            }
            UpdatePlayerHealthBar();
        }

        Color PlayerInvulnerabilityTint(Color normal)
        {
            if (!PlayerInvulnerable) return normal;
            return PlayerWhiteFlashPhase ? new Color(1, 1, 1, .6f) : new Color(normal.r, normal.g, normal.b, .8f);
        }

        bool PlayerWhiteFlashPhase => PlayerInvulnerable && Mathf.FloorToInt((ContactInvulnerabilityDuration - contactInvulnerability) / .25f) % 2 == 0;

        void ApplyPlayerHitAppearance()
        {
            player.art.color = PlayerInvulnerabilityTint(player.art.color);
            if (PlayerWhiteFlashPhase) {
                if (!playerHitMaterial) playerHitMaterial = new Material(Resources.Load<Shader>("DoodleIdle/DoodlePlayerHit")) { name = "Doodle player white hit flash" };
                playerHitMaterial.mainTexture = player.art.sprite.texture;
                // SpriteRenderer alpha is not provided through vertex color in every URP batching path.
                playerHitMaterial.SetFloat("_Opacity", player.art.color.a);
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
