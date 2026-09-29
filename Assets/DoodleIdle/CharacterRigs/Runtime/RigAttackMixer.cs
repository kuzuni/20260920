using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DoodleIdle.CharacterRigs
{
    // Runtime graph only: no controller, clip, prefab or mask asset is edited.
    // The existing controller keeps locomotion/Hit/Death. Attack overlays its
    // original upper-body curves without sending the base layer an Attack trigger.
    public sealed class RigAttackMixer : IDisposable
    {
        readonly Animator animator;
        readonly AnimationClip clip;
        readonly AvatarMask upperBody;
        PlayableGraph graph;
        AnimatorControllerPlayable controller;
        AnimationClipPlayable attack;
        AnimationLayerMixerPlayable layers;
        float attackTime;
        bool dead;
        public bool IsAttacking { get; private set; }

        public static RigAttackMixer Create(Animator animator)
        {
            if (!animator || !animator.runtimeAnimatorController) return null;
            var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == "Attack");
            return clip ? new RigAttackMixer(animator, clip) : null;
        }

        RigAttackMixer(Animator animator, AnimationClip clip)
        {
            this.animator = animator; this.clip = clip;
            // Preserve locomotion phase and parameters when the first attack occurs.
            var states = Enumerable.Range(0, animator.layerCount).Select(animator.GetCurrentAnimatorStateInfo).ToArray();
            var parameters = animator.parameters;
            var floats = parameters.Where(p => p.type == AnimatorControllerParameterType.Float).ToDictionary(p => p.nameHash, p => animator.GetFloat(p.nameHash));
            var ints = parameters.Where(p => p.type == AnimatorControllerParameterType.Int).ToDictionary(p => p.nameHash, p => animator.GetInteger(p.nameHash));
            var bools = parameters.Where(p => p.type == AnimatorControllerParameterType.Bool).ToDictionary(p => p.nameHash, p => animator.GetBool(p.nameHash));

            graph = PlayableGraph.Create("Concurrent attack: " + animator.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            controller = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            attack = AnimationClipPlayable.Create(graph, clip);
            attack.SetApplyFootIK(false); attack.SetApplyPlayableIK(false);
            attack.SetSpeed(0); // Evaluate the original clip at the explicit attack clock.
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(controller, 0, layers, 0); graph.Connect(attack, 0, layers, 1);
            layers.SetInputWeight(0, 1); layers.SetInputWeight(1, 0);
            upperBody = new AvatarMask { name = "Runtime upper body", hideFlags = HideFlags.HideAndDontSave };
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            upperBody.transformCount = transforms.Length;
            for (int i = 0; i < transforms.Length; i++)
            {
                var bone = transforms[i];
                string path = "";
                for (var node = bone; node != animator.transform; node = node.parent)
                    path = path.Length == 0 ? node.name : node.name + "/" + path;
                upperBody.SetTransformPath(i, path);
                upperBody.SetTransformActive(i, bone != animator.transform &&
                    (bone.name == "몸통" || bone.name == "머리" || bone.name.StartsWith("팔") || bone.name.StartsWith("날개") || bone.name == "Weapon"));
            }
            layers.SetLayerMaskFromAvatarMask(1, upperBody);
            var output = AnimationPlayableOutput.Create(graph, "Character", animator);
            output.SetSourcePlayable(layers);
            foreach (var p in floats) controller.SetFloat(p.Key, p.Value);
            foreach (var p in ints) controller.SetInteger(p.Key, p.Value);
            foreach (var p in bools) controller.SetBool(p.Key, p.Value);
            for (int i = 0; i < states.Length; i++)
                if (states[i].fullPathHash != 0) controller.Play(states[i].fullPathHash, i, states[i].normalizedTime);
            graph.Play();
        }

        public void SetMoving(bool moving) => controller.SetBool("Moving", moving);

        public void Attack()
        {
            if (dead) return;
            attackTime = 0; IsAttacking = true;
            attack.SetTime(0); attack.SetDone(false);
            layers.SetInputWeight(1, 1);
        }

        public void Hit() { StopAttack(); controller.SetTrigger("Hit"); }
        public void Die() { dead = true; StopAttack(); controller.SetTrigger("Die"); }
        void StopAttack() { IsAttacking = false; layers.SetInputWeight(1, 0); }

        public void Evaluate(float deltaTime)
        {
            if (!graph.IsValid() || !animator.enabled || !animator.gameObject.activeInHierarchy) return;
            float dt = Mathf.Max(0, deltaTime * animator.speed);
            if (IsAttacking)
            {
                attackTime += dt;
                if (attackTime >= clip.length) StopAttack();
                else attack.SetTime(attackTime);
            }
            graph.Evaluate(dt);
        }

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            if (upperBody) UnityEngine.Object.Destroy(upperBody);
        }
    }
}
