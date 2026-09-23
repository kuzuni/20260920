using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        [Header("Enemy dash attack")]
        public bool enemyDashEnabled = true;
        [Min(0)] public float enemyDashSpeed = 8;
        public const float EnemyDashWindup = .35f;
        public const float EnemyDashDuration = .3f;
        public const int EnemyDashStartStage = 1000;
        public int EnemyDashCasts { get; private set; }

        void TickEnemyMovement(float dt)
        {
            // A cave uses its equivalent main-stage difficulty, including dash unlocks.
            bool canDash = enemyDashEnabled && Ui && Ui.CombatDifficultyStage >= EnemyDashStartStage;
            foreach (var enemy in enemies) {
                Vector2 toPlayer = player.Position - enemy.Position;
                if (!canDash) enemy.dashWindup = enemy.enemyDashRemaining = 0;
                if (canDash && enemy.enemyDashRemaining > 0) {
                    enemy.enemyDashRemaining = Mathf.Max(0, enemy.enemyDashRemaining - dt);
                    if (enemy.enemyDashRemaining > 0) {
                        enemy.body.linearVelocity = enemy.enemyDashDirection * enemyDashSpeed;
                        enemy.dashTrail -= dt;
                        if (enemy.dashTrail <= 0) { EnemyDashTrail(enemy); enemy.dashTrail += .06f; }
                        continue;
                    }
                }
                if (canDash && enemy.dashWindup > 0) {
                    enemy.dashWindup = Mathf.Max(0, enemy.dashWindup - dt);
                    enemy.body.linearVelocity = Vector2.zero;
                    if (enemy.dashWindup <= .0001f) {
                        enemy.dashWindup = 0;
                        enemy.enemyDashRemaining = EnemyDashDuration;
                        enemy.dashCooldown = 4 + enemy.phase * .35f;
                        enemy.body.linearVelocity = enemy.enemyDashDirection * enemyDashSpeed;
                        enemy.art.flipX = enemy.enemyDashDirection.x < 0;
                        EnemyDashTrail(enemy); enemy.dashTrail = .06f;
                        EnemyDashCasts++;
                        EmitBurst(dustParticles, enemy.Position + Vector2.down * .45f, new Color(.72f, .67f, .58f, .6f), 6, .2f, .4f, 1.8f, .2f, .4f);
                    }
                    continue;
                }
                enemy.dashCooldown = Mathf.Max(0, enemy.dashCooldown - dt);
                if (canDash && enemy.dashCooldown <= 0 && toPlayer.sqrMagnitude <= 64 && toPlayer.sqrMagnitude > .01f) {
                    enemy.dashWindup = EnemyDashWindup;
                    enemy.enemyDashDirection = toPlayer.normalized;
                    enemy.body.linearVelocity = Vector2.zero;
                    continue;
                }
                Vector2 wander = new Vector2(Mathf.Sin(Elapsed * .5f + enemy.phase), Mathf.Cos(Elapsed * .43f + enemy.phase));
                enemy.body.linearVelocity = toPlayer.normalized * .6f + wander * .28f;
            }
        }

        void LimitEnemyCrowdMotion(float dt)
        {
            // Movement and simultaneous skill impulses must not keep compressing a
            // packed crowd against the arena walls. Share the available clearance
            // between both bodies; leave actual contact resolution to Physics2D.
            foreach (var enemy in enemies) {
                Vector2 velocity = enemy.body.linearVelocity;
                float speed = velocity.magnitude;
                if (speed < .0001f) continue;
                Vector2 direction = velocity / speed;
                Vector2 position = enemy.Position;
                float radius = ActorRadius(enemy);
                float travel = speed * dt;
                foreach (var other in enemies) {
                    if (other == enemy) continue;
                    Vector2 delta = other.Position - position;
                    float reach = radius + ActorRadius(other) + .025f;
                    if (delta.sqrMagnitude > (reach + travel * 2) * (reach + travel * 2)) continue;
                    float distance = delta.magnitude;
                    if (distance < .0001f) continue;
                    float closing = Vector2.Dot(direction, delta / distance);
                    if (closing <= 0) continue;
                    travel = Mathf.Min(travel, Mathf.Max(0, distance - reach) * .5f / closing);
                }
                Vector2 limit = arenaHalfSize - Vector2.one * (radius + .025f);
                if (Mathf.Abs(direction.x) > .0001f)
                    travel = Mathf.Min(travel, Mathf.Max(0, limit.x - Mathf.Sign(direction.x) * position.x) / Mathf.Abs(direction.x));
                if (Mathf.Abs(direction.y) > .0001f)
                    travel = Mathf.Min(travel, Mathf.Max(0, limit.y - Mathf.Sign(direction.y) * position.y) / Mathf.Abs(direction.y));
                enemy.body.linearVelocity = direction * (travel / dt);
            }
        }

        void EnemyDashTrail(Actor enemy)
        {
            var afterimage = Visual("Enemy dash afterimage", enemy.art.sprite, enemy.art.transform.position,
                enemy.art.transform.lossyScale, enemy.art.sortingOrder - 1);
            afterimage.transform.rotation = enemy.art.transform.rotation;
            afterimage.flipX = enemy.art.flipX;
            afterimage.color = new Color(1, 1, 1, .28f);
            flecks.Add(new Fleck { visual = afterimage.transform, sprite = afterimage, lifetime = .22f, remaining = .22f });
        }
    }
}
