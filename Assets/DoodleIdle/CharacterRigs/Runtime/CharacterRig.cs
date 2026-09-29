using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D.Animation;

namespace DoodleIdle.CharacterRigs
{
    [RequireComponent(typeof(SortingGroup))]
    public sealed class CharacterRig : MonoBehaviour
    {
        public string rigType;
        public CharacterAppearance appearance;
        public Transform skeleton;
        public Transform groundContact;
        public SpriteRenderer weaponRenderer;
        public SpriteRenderer[] partRenderers;
        public Animator animator;
        [Tooltip("적 공격 범위. AttackRange 자식의 Trigger Collider2D 크기와 위치로 조절합니다.")]
        public Collider2D attackRange;

        public bool ContainsAttackTarget(Collider2D target)
        {
            if (!attackRange || !attackRange.enabled || !attackRange.isTrigger
                || !attackRange.gameObject.activeInHierarchy || !target || !target.enabled
                || !target.gameObject.activeInHierarchy) return false;
            var distance = attackRange.Distance(target);
            return distance.isValid && distance.isOverlapped;
        }
        Action attackImpact;
        bool attackQueued, attackObserved;

        bool InAttack()
        {
            int layer = animator.GetLayerIndex("Upper Body");
            if (layer < 0) layer = 0;
            return animator.GetCurrentAnimatorStateInfo(layer).IsName("Attack")
                || (animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).IsName("Attack"));
        }

        void LateUpdate()
        {
            if (!attackQueued) return;
            if (InAttack()) attackObserved = true;
            else if (attackObserved) CancelAttack();
        }

        public void CancelAttack()
        {
            attackImpact = null;
            attackQueued = attackObserved = false;
            if (animator && animator.isInitialized)
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger
                        && (parameter.name == "Attack" || parameter.name == "UpperAttack"))
                        animator.ResetTrigger(parameter.nameHash);
        }

        void OnDisable() => CancelAttack();

        // Called only by the authored Attack clip's impact event. Consume before invoking:
        // a second event or an outgoing animation blend must never deal damage twice.
        public void OnAttackImpact()
        {
            if (!attackQueued || !isActiveAndEnabled) return;
            attackObserved = true;
            var impact = attackImpact;
            attackImpact = null;
            impact?.Invoke();
        }

        public void SetAppearance(CharacterAppearance next)
        {
            if (next == null || next.rigType != rigType)
                throw new ArgumentException("Appearance must use rig type " + rigType + "; received " + (next ? next.rigType : "null") + ".", nameof(next));
            var bones = new Dictionary<string, Transform>();
            foreach (var bone in skeleton.GetComponentsInChildren<Transform>(true)) bones.Add(bone.name, bone);
            if (next.parts.Length != partRenderers.Length) throw new InvalidOperationException("Part count mismatch.");
            foreach (var part in next.parts)
            {
                var renderer = Array.Find(partRenderers, r => r.name == part.name);
                if (renderer == null || part.sprite == null) throw new InvalidOperationException("Missing part: " + part.name);
                var transforms = new Transform[part.boneNames.Length];
                for (var i = 0; i < transforms.Length; i++) transforms[i] = bones[part.boneNames[i]];
                var skin = renderer.GetComponent<SpriteSkin>();
                renderer.sprite = part.sprite;
                renderer.transform.localPosition = part.rendererPosition;
                // Layer/order belong to the rig prefab, not the interchangeable skin.
                skin.SetRootBone(skeleton);
                var state = skin.SetBoneTransforms(transforms);
                skin.alwaysUpdate = true;
                if (state != SpriteSkinState.Ready) throw new InvalidOperationException("Invalid skin: " + part.name + " (" + state + ")");
            }
            if (weaponRenderer != null)
            {
                weaponRenderer.sprite = next.weapon;
                weaponRenderer.enabled = next.weapon != null;
                weaponRenderer.transform.localScale = Vector3.one * next.weaponScale;
            }
            appearance = next;
        }

        public void SetMoving(bool moving) => animator.SetBool("Moving", moving);
        public void Attack() => TryAttack(null);

        public bool TryAttack(Action impact)
        {
            LateUpdate();
            if (!isActiveAndEnabled || attackQueued || InAttack()) return false;
            attackQueued = true;
            attackObserved = false;
            attackImpact = impact;
            // Damage may have queued Hit earlier in this frame. It must not consume
            // the new attack before the Animator has evaluated its trigger.
            animator.ResetTrigger("Hit");
            // Armed/winged biped Animators blend the existing Attack on their limb layer.
            // Other authored controllers retain their original Attack trigger.
            int upperAttack = Animator.StringToHash("UpperAttack");
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == upperAttack && parameter.type == AnimatorControllerParameterType.Trigger)
                { animator.SetTrigger(upperAttack); return true; }
            animator.SetTrigger("Attack");
            return true;
        }
        public void Hit() { CancelAttack(); animator.SetTrigger("Hit"); }
        public void ReactToDamage()
        {
            // Ordinary damage still flashes/tints the actor, but cannot permanently
            // stunlock attacks under rapid skill/companion hits. Hit() remains the
            // explicit interrupt path; death and pooling still cancel the impact.
            if (!attackQueued && !InAttack()) Hit();
        }
        public void Die() { CancelAttack(); animator.SetTrigger("Die"); }
    }
}
