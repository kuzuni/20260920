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
        public CharacterFace face;
        public CharacterFootDust footDust;
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
        RuntimeAnimatorController parameterController;
        AnimatorControllerParameter[] parameters;
        int attackLayer, attackTrigger;
        void CacheParameters()
        {
            if (parameters != null && parameterController == animator.runtimeAnimatorController) return;
            parameterController = animator.runtimeAnimatorController;
            parameters = animator.parameters;
            attackLayer = Mathf.Max(0, animator.GetLayerIndex("Upper Body"));
            attackTrigger = Animator.StringToHash("Attack");
            int upper = Animator.StringToHash("UpperAttack");
            foreach (var parameter in parameters)
                if (parameter.nameHash == upper && parameter.type == AnimatorControllerParameterType.Trigger) attackTrigger = upper;
        }

        bool InAttack()
        {
            CacheParameters();
            int layer = attackLayer;
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
            if (animator && animator.isInitialized) {
                CacheParameters();
                foreach (var parameter in parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger
                        && (parameter.name == "Attack" || parameter.name == "UpperAttack"))
                        animator.ResetTrigger(parameter.nameHash);
            }
        }

        // Pooling preserves the hierarchy and bound graph. Reset all parameters so
        // queued Hit/Die transitions and old attack callbacks cannot survive reuse.
        public void ResetPooledAnimation()
        {
            CancelAttack();
            int idle = Animator.StringToHash("Idle");
            if (animator.layerCount != 1 || !animator.HasState(0, idle)) { animator.Rebind(); SetMoving(false); animator.Update(0); return; }
            CacheParameters();
            foreach (var parameter in parameters) {
                switch (parameter.type) {
                    case AnimatorControllerParameterType.Trigger: animator.ResetTrigger(parameter.nameHash); break;
                    case AnimatorControllerParameterType.Bool: animator.SetBool(parameter.nameHash, parameter.defaultBool); break;
                    case AnimatorControllerParameterType.Float: animator.SetFloat(parameter.nameHash, parameter.defaultFloat); break;
                    case AnimatorControllerParameterType.Int: animator.SetInteger(parameter.nameHash, parameter.defaultInt); break;
                }
            }
            animator.Play(idle, 0, 0);
            animator.Update(0);
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
            foreach (var bone in skeleton.GetComponentsInChildren<Transform>(true))
            {
                // Face attachments contain repeated names (White/Pupil), not skin bones.
                if (face && bone.IsChildOf(face.transform)) continue;
                bones.Add(bone.name, bone);
            }
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
                // Re-registering unchanged bones makes the deformation system rebuild
                // native transform arrays on every pooled appearance swap.
                if (skin.rootBone != skeleton) skin.SetRootBone(skeleton);
                bool changed = skin.boneTransforms == null || skin.boneTransforms.Length != transforms.Length;
                if (!changed) for (int i=0;i<transforms.Length;i++) if (skin.boneTransforms[i]!=transforms[i]) { changed=true;break; }
                var state = changed ? skin.SetBoneTransforms(transforms) : SpriteSkinState.Ready;
                // A reused rig can re-enter the camera with last frame's culled
                // deformation buffer. Keep active parts current; pooled parts are
                // still skipped because their renderers are disabled.
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
            if (face)
            {
                face.SetFaceParts(true, !next.friendlyEyes);
                face.gameObject.SetActive(next.separatedFace);
            }
        }

        public void SetMoving(bool moving) => animator.SetBool("Moving", moving);
        public void UpdateOffscreenMeshes(bool update)
        {
            foreach(var renderer in partRenderers) {
                var skin=renderer.GetComponent<SpriteSkin>();
                if(skin)skin.alwaysUpdate=update;
            }
        }
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
            // Only the player has an UpperAttack layer; monsters play the original
            // full-body Attack on their Base Layer.
            CacheParameters();
            animator.SetTrigger(attackTrigger);
            return true;
        }
        public void Hit() { if (face) face.ShowHit(); CancelAttack(); animator.SetTrigger("Hit"); }
        public void ReactToDamage()
        {
            if (face) face.ShowHit();
            // Ordinary damage still flashes/tints the actor, but cannot permanently
            // stunlock attacks under rapid skill/companion hits. Hit() remains the
            // explicit interrupt path; death and pooling still cancel the impact.
            if (!attackQueued && !InAttack()) Hit();
        }
        public void Die() { CancelAttack(); animator.SetTrigger("Die"); }
    }
}
