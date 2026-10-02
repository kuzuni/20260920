using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        DoodleIdle.CharacterRigs.CharacterMovementZones MovementZones => player?.rigVisual?.Rig ? player.rigVisual.Rig.movementZones : null;
        bool UsesMovementZones => MovementZones && MovementZones.Valid;
        bool KeepsEnemyDistance => autoPlay && !JoystickActive && (UsesMovementZones || (Ui && Ui.PlayerKeepDistance > 0));

        static float ActorRadius(Actor actor) => actor.collider.radius * Mathf.Abs(actor.root.transform.lossyScale.x);

        Vector2 AutomaticMoveVelocity(Actor target, float dt)
        {
            Vector2 delta = target.Position - player.Position;
            if (UsesMovementZones) return ZonedMoveVelocity(target, dt);
            float speedScale = moveSpeed / 3.1f;
            if (!KeepsEnemyDistance) return delta.normalized * (delta.magnitude > 1.35f ? moveSpeed : .45f * speedScale);
            float gap = Ui.PlayerKeepDistance;
            float safe = ActorRadius(player) + ActorRadius(target) + gap;
            Vector2 desired = delta.normalized * Mathf.Clamp((delta.magnitude - safe) * 4 * speedScale, 0, moveSpeed);
            // Keep out of every nearby body, not only the enemy we are attacking.
            Vector2 escape = Vector2.zero;
            foreach (var enemy in enemies) {
                if (enemy.hp <= 0) continue;
                Vector2 away = player.Position - enemy.Position;
                float distance = away.magnitude;
                float clearance = ActorRadius(player) + ActorRadius(enemy) + gap;
                if (distance >= clearance) continue;
                Vector2 direction = distance > .001f ? away / distance : -facing;
                escape += direction * Mathf.Min(moveSpeed, (clearance - distance) * 4 * speedScale);
            }
            desired = Vector2.ClampMagnitude(desired + escape, moveSpeed);
            return LimitAutomaticStep(desired * dt) / dt;
        }

        Vector2 LimitAutomaticStep(Vector2 step)
        {
            if (!KeepsEnemyDistance || step.sqrMagnitude < .0000001f) return step;
            if (!endlessWorld) {
                Vector2 limit = arenaHalfSize - Vector2.one * (ActorRadius(player) + .15f);
                Vector2 end = player.Position + step;
                end.x = Mathf.Clamp(end.x, -limit.x, limit.x);
                end.y = Mathf.Clamp(end.y, -limit.y, limit.y);
                step = end - player.Position;
            }
            // Sweep the complete step, including fast dashes, against enlarged collision circles.
            Vector2 direction = step.normalized;
            float travel = step.magnitude;
            foreach (var enemy in enemies) {
                if (enemy.hp <= 0) continue;
                bool approach = UsesMovementZones && !MovementZones.Retreating;
                Vector2 relative = enemy.Position - (approach ? ZoneCenter(MovementZones.stopAndAttack) : player.Position);
                float radius = approach ? ZoneRadius(MovementZones.stopAndAttack) + ActorRadius(enemy)
                    : ActorRadius(player) + ActorRadius(enemy) + (UsesMovementZones ? .05f : Ui.PlayerKeepDistance);
                float along = Vector2.Dot(relative, direction);
                if (along <= 0) continue; // Always allow retreat from an overlap.
                float acrossSquared = relative.sqrMagnitude - along * along;
                if (acrossSquared >= radius * radius) continue;
                float entry = along - Mathf.Sqrt(Mathf.Max(0, radius * radius - acrossSquared));
                travel = Mathf.Min(travel, Mathf.Max(0, entry + (approach ? .005f : -.01f)));
            }
            return direction * travel;
        }

        Vector2 ZoneCenter(CircleCollider2D zone) => player.Position + (Vector2)(zone.transform.TransformPoint(zone.offset) - player.root.transform.position);
        static float ZoneRadius(CircleCollider2D zone) => zone.radius * Mathf.Max(Mathf.Abs(zone.transform.lossyScale.x), Mathf.Abs(zone.transform.lossyScale.y));
        bool EnemyInsideZone(Actor enemy, CircleCollider2D zone)
        {
            float radius = ZoneRadius(zone) + ActorRadius(enemy);
            return (enemy.Position - ZoneCenter(zone)).sqrMagnitude <= radius * radius;
        }
        Vector2 ZonedMoveVelocity(Actor target, float dt)
        {
            var zones = MovementZones;
            bool insideA = false, insideB = false, insideC = false;
            Vector2 escape = Vector2.zero;
            foreach (var enemy in enemies) {
                if (!Alive(enemy) || enemy.returnedToPool) continue;
                insideA |= EnemyInsideZone(enemy, zones.stopAndAttack);
                insideB |= EnemyInsideZone(enemy, zones.startRetreat);
                if (!EnemyInsideZone(enemy, zones.finishRetreat)) continue;
                insideC = true;
                Vector2 away = player.Position - enemy.Position;
                escape += away.sqrMagnitude > .0001f ? away.normalized / Mathf.Max(.1f, away.magnitude) : -facing;
            }
            if (insideB) zones.Retreating = true;
            else if (!insideC) zones.Retreating = false;
            if (zones.Retreating) return RetreatVelocity(escape, dt);
            if (insideA || dt <= 0) return Vector2.zero;
            return LimitAutomaticStep((target.Position - player.Position).normalized * moveSpeed * dt) / dt;
        }
        Vector2 RetreatVelocity(Vector2 escape, float dt)
        {
            if (dt <= 0) return Vector2.zero;
            Vector2 preferred = escape.sqrMagnitude > .0001f ? escape.normalized : -facing;
            if (preferred.sqrMagnitude < .0001f) preferred = Vector2.right;
            Vector2 best = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            // Sideways exits avoid cancellation by opposing threats and arena walls.
            for (int i = 0; i < 16; i++) {
                float angle = i * (Mathf.PI * 2 / 16);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 step = LimitAutomaticStep(direction * moveSpeed * dt);
                if (step.sqrMagnitude < .0000001f) continue;
                Vector2 end = player.Position + step;
                float clearance = float.PositiveInfinity;
                foreach (var enemy in enemies) {
                    if (!Alive(enemy) || enemy.returnedToPool) continue;
                    clearance = Mathf.Min(clearance, Vector2.Distance(end, enemy.Position) - ActorRadius(enemy));
                }
                float score = clearance + Vector2.Dot(direction, preferred) * .001f;
                if (score > bestScore) { bestScore = score; best = step; }
            }
            return best / dt;
        }
    }
}
