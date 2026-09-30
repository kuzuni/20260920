using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        readonly Dictionary<string,List<Actor>> spareEnemyGeometry = new Dictionary<string,List<Actor>>();
        int pooledEnemyGeometry;
        public int EnemyObjectsCreated { get; private set; }
        public int EnemyObjectsReused { get; private set; }
        static readonly Unity.Profiling.ProfilerMarker rentEnemyMarker = new Unity.Profiling.ProfilerMarker("Doodle/RentEnemy");
        static readonly Unity.Profiling.ProfilerMarker restoreRigMarker = new Unity.Profiling.ProfilerMarker("Doodle/RestoreRig");
        static readonly Unity.Profiling.ProfilerMarker configureRigMarker = new Unity.Profiling.ProfilerMarker("Doodle/ConfigureRig");
        static readonly Unity.Profiling.ProfilerMarker enemyHealthMarker = new Unity.Profiling.ProfilerMarker("Doodle/EnemyHealth");

        bool TryRentEnemy(Vector2 position, int kind, GameNumber? waveHealth, out Actor actor)
        {
            using var sample = rentEnemyMarker.Auto();
            actor = null;
            var nextEntry=DoodleCharacterCatalog.Current.Enemy(CurrentThemeIndex,enemyAppearanceSequence);
            string rigType = nextEntry.appearance.rigType;
            if (!spareEnemyGeometry.TryGetValue(rigType,out var spare)) return false;
            Actor previous = null;
            while (spare.Count > 0 && (previous == null || !previous.root)) {
                int index=spare.Count-1;
                for(int i=spare.Count-1;i>=0;i--)if(spare[i].root && spare[i].rigVisual.Entry==nextEntry){index=i;break;}
                previous=spare[index];spare.RemoveAt(index);pooledEnemyGeometry--;
            }
            if (previous == null || !previous.root) return false;
            // Keep a new combat identity. Projectiles holding the retired Actor must never hit a respawned enemy.
            actor = new Actor { root = previous.root, body = previous.body, collider = previous.collider,
                art = previous.art, shadow = previous.shadow, healthBack = previous.healthBack, healthFill = previous.healthFill,
                healthState = previous.healthState,
                rigVisual = previous.rigVisual,
                kind = kind, phase = Random.value * 6.28f };
            actor.root.name = "Enemy - " + ThemeEnemies[CurrentThemeIndex][kind];
            actor.root.transform.localScale = Vector3.one;
            actor.root.transform.SetPositionAndRotation(position, Quaternion.identity);
            actor.body.position = position; actor.body.rotation = 0;
            actor.body.linearVelocity = Vector2.zero; actor.body.angularVelocity = 0; actor.body.mass = 1;
            actor.body.simulated = !paused; actor.collider.enabled = true;
            actor.art.transform.localPosition = Vector3.zero; actor.art.color = Color.white; actor.art.enabled = true;
            actor.art.flipY = false; actor.art.transform.localRotation = Quaternion.identity;
            SetSpriteArt(actor.art, enemyWalkFrames[kind][0]); NormalizeEnemyFrame(actor, actor.art.sprite);
            using (restoreRigMarker.Auto()) actor.rigVisual.RestoreRig();
            using (configureRigMarker.Auto()) ConfigureActorRig(actor);
            actor.art.flipX = player.Position.x < position.x;
            actor.shadow.transform.localScale = new Vector3(2.30f, .84f, 1);
            actor.rigVisual.Sync();
            using (enemyHealthMarker.Auto()) actor.hp = actor.maxHp = waveHealth ?? (Ui ? Ui.EnemyHealthAmount(Ui.CombatDifficultyStage) : EnemyMaxHealth);
            actor.dashCooldown = 2 + actor.phase * .4f;
            RefreshHealthBar(actor);
            actor.shadow.enabled = true;
            EnemyObjectsReused++;
            return true;
        }
        void ReleaseEnemy(Actor actor)
        {
            if (actor == null || !actor.root || actor.returnedToPool) return;
            actor.returnedToPool = true; actor.hp = 0;
            // Keep the native skeleton hierarchy registered. Every visible/physical
            // component is disabled; the retired Actor is no longer in combat lists.
            actor.rigVisual.ParkRig();
            actor.root.name = "Pooled enemy";
            actor.collider.enabled = false;
            actor.art.enabled = actor.shadow.enabled = false;
            if (actor.healthBack) actor.healthBack.enabled = false;
            if (actor.healthFill) actor.healthFill.enabled = false;
            actor.body.linearVelocity = Vector2.zero; actor.body.angularVelocity = 0; actor.body.simulated = false;
            if (pooledEnemyGeometry < 512) {
                string type=actor.rigVisual.Rig.rigType;
                if(!spareEnemyGeometry.TryGetValue(type,out var spare))spareEnemyGeometry[type]=spare=new List<Actor>();
                spare.Add(actor);pooledEnemyGeometry++;
            }
            else Destroy(actor.root);
        }
    }
}
