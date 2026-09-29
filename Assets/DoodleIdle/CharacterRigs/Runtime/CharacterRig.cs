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
        RigAttackMixer attackMixer;

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

        public void SetMoving(bool moving)
        {
            animator.SetBool("Moving", moving);
            attackMixer?.SetMoving(moving);
        }
        public void Attack()
        {
            if (!Application.isPlaying) { animator.SetTrigger("Attack"); return; }
            if (attackMixer == null) attackMixer = RigAttackMixer.Create(animator);
            if (attackMixer != null) attackMixer.Attack();
            else animator.SetTrigger("Attack");
        }
        public void Hit() { if (attackMixer != null) attackMixer.Hit(); else animator.SetTrigger("Hit"); }
        public void Die() { if (attackMixer != null) attackMixer.Die(); else animator.SetTrigger("Die"); }
        void Update() => attackMixer?.Evaluate(Time.deltaTime);
        void OnDisable() { attackMixer?.Dispose(); attackMixer = null; }
        void OnDestroy() { attackMixer?.Dispose(); attackMixer = null; }
    }
}
