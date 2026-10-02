using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        readonly List<DoodleSpawnPortal> activeSpawnPortals = new List<DoodleSpawnPortal>(100);
        readonly Stack<DoodleSpawnPortal> spareSpawnPortals = new Stack<DoodleSpawnPortal>(100);
        DoodleSpawnPortal spawnPortalPrefab;
        ParticleSystem spawnSparkParticles;
        void PrepareSpawnPortals()
        {
            spawnPortalPrefab = Resources.Load<DoodleSpawnPortal>("DoodleIdle/EnemySpawnPortal");
            if (!spawnPortalPrefab) return;
            for (int i = 0; i < 100; i++) {
                var portal = Instantiate(spawnPortalPrefab, world, false);
                portal.gameObject.SetActive(false); spareSpawnPortals.Push(portal);
            }
        }
        void BeginEnemyArrival(Actor actor)
        {
            var settings = DoodleHitFeedbackSettings.Shared;
            actor.spawnDelay = Mathf.Max(.001f, settings.spawnPortalDuration);
            actor.spawnGrow = Mathf.Max(.001f, settings.spawnGrowDuration);
            actor.spawnRemaining = actor.spawnDelay + actor.spawnGrow;
            actor.body.linearVelocity = Vector2.zero; actor.body.simulated = false;
            actor.collider.enabled = false; actor.shadow.enabled = false;
            actor.rigVisual.BeginSpawnFeedback();
            if (!spawnPortalPrefab) return;
            DoodleSpawnPortal portal;
            if (spareSpawnPortals.Count > 0) portal = spareSpawnPortals.Pop();
            else {
                // Keep visual capacity bounded even when several waves overlap.
                portal = activeSpawnPortals[0]; activeSpawnPortals.RemoveAt(0);
            }
            float scale = Mathf.Abs(actor.root.transform.localScale.x);
            Vector3 position = actor.rigVisual.Rig.groundContact ? actor.rigVisual.Rig.groundContact.position : actor.root.transform.position;
            portal.Begin(position, scale, actor.spawnRemaining + .12f); activeSpawnPortals.Add(portal);
            EmitBurst(spawnSparkParticles, position, new Color(.7f, .8f, 1), 4, .1f * scale, .18f * scale, .7f * scale, .25f, .45f);
        }
        void TickEnemyArrivals(float dt)
        {
            TickActorArrival(player, dt);
            foreach (var actor in enemies) TickActorArrival(actor, dt);
            for (int i = activeSpawnPortals.Count - 1; i >= 0; i--) {
                var portal = activeSpawnPortals[i];
                if (portal.Simulate(dt)) continue;
                portal.gameObject.SetActive(false); spareSpawnPortals.Push(portal); activeSpawnPortals.RemoveAt(i);
            }
        }
        void TickActorArrival(Actor actor, float dt)
        {
            if (actor == null || actor.spawnRemaining <= 0) return;
            actor.spawnRemaining = Mathf.Max(0, actor.spawnRemaining - dt);
            if (actor.spawnRemaining <= actor.spawnGrow)
                actor.rigVisual.AdvanceSpawnFeedback(1 - actor.spawnRemaining / actor.spawnGrow);
            if (actor.spawnRemaining > 0) return;
            actor.rigVisual.ResetSpawnFeedback(); actor.rigVisual.Sync();
            actor.body.simulated = !paused; actor.collider.enabled = true; actor.shadow.enabled = true;
        }
        void ClearSpawnPortals()
        {
            foreach (var portal in activeSpawnPortals) { portal.gameObject.SetActive(false); spareSpawnPortals.Push(portal); }
            activeSpawnPortals.Clear();
        }
    }
}
