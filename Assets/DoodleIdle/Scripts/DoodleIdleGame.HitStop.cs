using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public static float HitStopDuration => DoodleHitFeedbackSettings.Shared.Duration;
        readonly List<Actor> dyingEnemies = new List<Actor>(64);
        bool playerDefeatPending;
        public const float PlayerRespawnDelay = 1f;
        float playerRespawnRemaining;

        void ApplyHitStop(Actor actor)
        {
            // Player damage never interrupts actions. Death cancels the attack but
            // keeps the Animator running so the existing non-looping Death can play.
            if (actor.isPlayer) {
                actor.rigVisual.BeginHitFeedback();
                actor.hitFeedbackRemaining = actor.rigVisual.HitFeedbackDuration;
                ShowHitFeedback(actor);
                if (actor.hp <= 0) {
                    actor.body.linearVelocity = Vector2.zero; actor.body.angularVelocity = 0;
                    actor.body.simulated = false; actor.collider.enabled = false;
                    actor.rigVisual.Rig.Die();
                }
                return;
            }
            actor.hitFeedbackRemaining = 0;
            if (actor.hitStop <= 0) actor.constraintsBeforeHit = actor.body.constraints;
            actor.hitStop = HitStopDuration;
            actor.body.linearVelocity = Vector2.zero;
            actor.body.angularVelocity = 0;
            actor.body.constraints = RigidbodyConstraints2D.FreezeAll;
            actor.dashWindup = actor.enemyDashRemaining = 0;
            actor.rigVisual.HitStopped = true;
            actor.rigVisual.BeginHitFeedback();
            actor.rigVisual.Rig.CancelAttack();
            ShowHitFeedback(actor);
        }

        void ShowHitFeedback(Actor actor)
        {
            actor.rigVisual.ShowHitFace();
            if (actor.rigVisual.Rig.hitBlood) actor.rigVisual.Rig.hitBlood.Burst(actor.art.flipX);
            actor.rigVisual.Sync();
        }

        void TickActorHitStop(Actor actor, float dt)
        {
            if (actor == null) return;
            if (actor.hitFeedbackRemaining > 0) {
                actor.hitFeedbackRemaining = Mathf.Max(0, actor.hitFeedbackRemaining - dt);
                actor.rigVisual.AdvanceHitFeedback(actor.rigVisual.HitFeedbackDuration - actor.hitFeedbackRemaining);
                if (actor.hitFeedbackRemaining <= .0001f) {
                    actor.hitFeedbackRemaining = 0;
                    actor.rigVisual.ResetHitFeedback();
                    actor.rigVisual.Sync();
                }
            }
            if (actor.hitStop <= 0) return;
            actor.hitStop = Mathf.Max(0, actor.hitStop - dt);
            actor.rigVisual.AdvanceHitFeedback(actor.rigVisual.HitFeedbackDuration - actor.hitStop);
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
            if (playerDefeatPending) {
                playerRespawnRemaining = Mathf.Max(0, playerRespawnRemaining - dt);
                if (playerRespawnRemaining > .0001f) return;
                playerDefeatPending = false;
                player.hp = player.maxHp;
                player.body.position = Vector2.zero; player.body.linearVelocity = Vector2.zero;
                player.root.transform.position = Vector3.zero;
                player.rigVisual.Rig.ResetPooledAnimation();
                player.rigVisual.Sync();
                if (Ui) Ui.HandlePlayerDefeat();
                BeginEnemyArrival(player);
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
            dyingEnemies.Clear(); playerDefeatPending = false; playerRespawnRemaining = 0;
        }
    }
}
