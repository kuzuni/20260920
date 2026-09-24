using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        readonly Stack<Actor> spareEnemyGeometry = new Stack<Actor>();
        public int EnemyObjectsCreated { get; private set; }
        public int EnemyObjectsReused { get; private set; }

        bool TryRentEnemy(Vector2 position, int kind, out Actor actor)
        {
            actor = null;
            Actor previous = null;
            while (spareEnemyGeometry.Count > 0 && (previous == null || !previous.root)) previous = spareEnemyGeometry.Pop();
            if (previous == null || !previous.root) return false;
            // Keep a new combat identity. Projectiles holding the retired Actor must never hit a respawned enemy.
            actor = new Actor { root = previous.root, body = previous.body, collider = previous.collider,
                art = previous.art, shadow = previous.shadow, healthBack = previous.healthBack, healthFill = previous.healthFill,
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
            actor.art.flipX = player.Position.x < position.x;
            actor.shadow.transform.localPosition = new Vector3(0, -.58f, 0);
            actor.shadow.transform.localScale = new Vector3(1.15f, .42f, 1);
            actor.hp = actor.maxHp = Ui ? Ui.EnemyHealthAmount(Ui.CombatDifficultyStage) : EnemyMaxHealth;
            actor.dashCooldown = 2 + actor.phase * .4f;
            RefreshHealthBar(actor);
            actor.root.SetActive(true); EnemyObjectsReused++;
            return true;
        }
        void ReleaseEnemy(Actor actor)
        {
            if (actor == null || !actor.root || actor.returnedToPool) return;
            actor.returnedToPool = true; actor.hp = 0; actor.root.SetActive(false);
            actor.body.linearVelocity = Vector2.zero; actor.body.angularVelocity = 0; actor.body.simulated = false;
            if (spareEnemyGeometry.Count < 512) spareEnemyGeometry.Push(actor);
            else Destroy(actor.root);
        }
    }
}
