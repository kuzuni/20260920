using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        bool KeepsEnemyDistance => autoPlay && !JoystickActive && Ui && Ui.PlayerKeepDistance > 0;

        static float ActorRadius(Actor actor) => actor.collider.radius * Mathf.Abs(actor.root.transform.lossyScale.x);

        Vector2 AutomaticMoveVelocity(Actor target, float dt)
        {
            Vector2 delta = target.Position - player.Position;
            if (!KeepsEnemyDistance) return delta.normalized * (delta.magnitude > 1.35f ? moveSpeed : .45f);
            float gap = Ui.PlayerKeepDistance;
            float safe = ActorRadius(player) + ActorRadius(target) + gap;
            Vector2 desired = delta.normalized * Mathf.Clamp((delta.magnitude - safe) * 4, 0, moveSpeed);
            // Keep out of every nearby body, not only the enemy we are attacking.
            Vector2 escape = Vector2.zero;
            foreach (var enemy in enemies) {
                if (enemy.hp <= 0) continue;
                Vector2 away = player.Position - enemy.Position;
                float distance = away.magnitude;
                float clearance = ActorRadius(player) + ActorRadius(enemy) + gap;
                if (distance >= clearance) continue;
                Vector2 direction = distance > .001f ? away / distance : -facing;
                escape += direction * Mathf.Min(moveSpeed, (clearance - distance) * 4);
            }
            desired = Vector2.ClampMagnitude(desired + escape, moveSpeed);
            return LimitAutomaticStep(desired * dt) / dt;
        }

        Vector2 LimitAutomaticStep(Vector2 step)
        {
            if (!KeepsEnemyDistance || step.sqrMagnitude < .0000001f) return step;
            Vector2 limit = arenaHalfSize - Vector2.one * (ActorRadius(player) + .15f);
            Vector2 end = player.Position + step;
            end.x = Mathf.Clamp(end.x, -limit.x, limit.x);
            end.y = Mathf.Clamp(end.y, -limit.y, limit.y);
            step = end - player.Position;
            // Sweep the complete step, including fast dashes, against enlarged collision circles.
            Vector2 direction = step.normalized;
            float travel = step.magnitude;
            foreach (var enemy in enemies) {
                if (enemy.hp <= 0) continue;
                Vector2 relative = enemy.Position - player.Position;
                float radius = ActorRadius(player) + ActorRadius(enemy) + Ui.PlayerKeepDistance;
                float along = Vector2.Dot(relative, direction);
                if (along <= 0) continue; // Always allow retreat from an overlap.
                float acrossSquared = relative.sqrMagnitude - along * along;
                if (acrossSquared >= radius * radius) continue;
                float entry = along - Mathf.Sqrt(Mathf.Max(0, radius * radius - acrossSquared));
                travel = Mathf.Min(travel, Mathf.Max(0, entry - .01f));
            }
            return direction * travel;
        }
    }
}
