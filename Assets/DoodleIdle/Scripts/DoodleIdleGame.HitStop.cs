using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public const float HitStopDuration = .1f;
        readonly List<Actor> dyingEnemies = new List<Actor>(64);
        bool playerDefeatPending;

        void ApplyHitStop(Actor actor)
        {
            if (actor.hitStop <= 0) actor.constraintsBeforeHit = actor.body.constraints;
            actor.hitStop = HitStopDuration;
            actor.body.linearVelocity = Vector2.zero;
            actor.body.angularVelocity = 0;
            actor.body.constraints = RigidbodyConstraints2D.FreezeAll;
            actor.dashWindup = actor.enemyDashRemaining = 0;
            actor.rigVisual.HitStopped = true;
            actor.rigVisual.BeginHitFeedback();
            actor.rigVisual.Rig.CancelAttack();
            actor.rigVisual.ShowHitFace();
            if (actor.rigVisual.Rig.hitBlood) actor.rigVisual.Rig.hitBlood.Burst(actor.art.flipX);
            actor.rigVisual.Sync();
        }

        void TickActorHitStop(Actor actor, float dt)
        {
            if (actor == null || actor.hitStop <= 0) return;
            actor.hitStop = Mathf.Max(0, actor.hitStop - dt);
            actor.rigVisual.AdvanceHitFeedback(HitStopDuration - actor.hitStop);
            actor.body.linearVelocity = Vector2.zero;
            if (actor.hitStop > .0001f) return;
            actor.hitStop = 0;
            actor.body.constraints = actor.constraintsBeforeHit;
            actor.rigVisual.HitStopped = false;
            actor.rigVisual.ResetHitFeedback();
            actor.rigVisual.Sync();
        }

        void TickHitReactions(float dt)
        {
            TickActorHitStop(player, dt);
            if (IsPvpEngine) return; // Each PVP engine owns its player's clock.
            foreach (var enemy in enemies) TickActorHitStop(enemy, dt);
            for (int i = dyingEnemies.Count - 1; i >= 0; i--) {
                var enemy = dyingEnemies[i]; TickActorHitStop(enemy, dt);
                if (enemy.hitStop > 0) continue;
                ReleaseEnemy(enemy); dyingEnemies.RemoveAt(i);
            }
            if (playerDefeatPending && player.hitStop <= 0) {
                playerDefeatPending = false;
                // The defeated pose stays at the hit location for the hit-stop duration;
                // then the respawn starts from a fresh idle pose at the origin.
                player.hp = player.maxHp; player.collider.enabled = true;
                player.body.position = Vector2.zero; player.body.linearVelocity = Vector2.zero;
                player.rigVisual.Rig.ResetPooledAnimation();
                if (Ui) Ui.HandlePlayerDefeat();
                UpdatePlayerHealthBar();
            }
        }

        void HoldEnemyDeath(Actor enemy)
        {
            enemy.collider.enabled = false;
            enemy.body.simulated = false;
            enemy.healthBack.enabled = enemy.healthFill.enabled = false;
            dyingEnemies.Add(enemy);
        }

        void ClearHitReactions()
        {
            foreach (var enemy in dyingEnemies) ReleaseEnemy(enemy);
            dyingEnemies.Clear(); playerDefeatPending = false;
        }
    }
}
