using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    // Read-only tests of the user's original clips through native Animator layers.
    public sealed class DoodleConcurrentAttackTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void AnimatorBlendsAttackArmsWithUninterruptedIdleOrMove(bool moving)
        {
            var source = DoodleCharacterCatalog.Current.Player(-1).prefab;
            var control = Object.Instantiate(source); var combined = Object.Instantiate(source); var sample = Object.Instantiate(source);
            try
            {
                var clip = source.animator.runtimeAnimatorController.animationClips.First(c => c.name == "Attack");
                int upper = combined.animator.GetLayerIndex("Upper Body");
                Assert.That(upper, Is.GreaterThan(0));
                Assert.That(combined.animator.GetLayerWeight(upper), Is.EqualTo(1));
                foreach (var rig in new[] { control, combined })
                {
                    rig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    rig.SetMoving(moving); rig.animator.Update(0);
                }
                for (int i = 0; i < 20; i++) { control.animator.Update(.05f); combined.animator.Update(.05f); }
                combined.Attack();
                for (int i = 0; i < 4; i++) { control.animator.Update(.04f); combined.animator.Update(.04f); }
                Assert.That(combined.animator.GetCurrentAnimatorStateInfo(0).IsName(moving ? "Move" : "Idle"), Is.True);
                Assert.That(combined.animator.GetCurrentAnimatorStateInfo(upper).IsName("Attack"), Is.True);
                clip.SampleAnimation(sample.gameObject, combined.animator.GetCurrentAnimatorStateInfo(upper).normalizedTime * clip.length);
                var attackBones = combined.skeleton.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                var reference = sample.skeleton.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                bool armChanged = false;
                foreach (var bone in control.skeleton.GetComponentsInChildren<Transform>())
                {
                    var mixed = attackBones[bone.name];
                    if (bone.name.StartsWith("다리") || bone.name == "몸통")
                    {
                        Assert.That(Vector3.Distance(bone.localPosition, mixed.localPosition), Is.LessThan(.001f), bone.name);
                        Assert.That(Quaternion.Angle(bone.localRotation, mixed.localRotation), Is.LessThan(.05f), bone.name);
                    }
                    if (bone.name == "팔2")
                    {
                        Assert.That(Quaternion.Angle(reference[bone.name].localRotation, mixed.localRotation), Is.LessThan(.05f), "Attack must drive the actual attacking arm at full weight");
                        armChanged = Quaternion.Angle(bone.localRotation, mixed.localRotation) > 1;
                    }
                }
                Assert.That(armChanged, Is.True);
                control.SetMoving(!moving); combined.SetMoving(!moving);
                for (int i = 0; i < 4; i++) { control.animator.Update(.04f); combined.animator.Update(.04f); }
                foreach (var bone in control.skeleton.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("다리")))
                    Assert.That(Quaternion.Angle(bone.localRotation, attackBones[bone.name].localRotation), Is.LessThan(.05f));
                combined.animator.Update(clip.length + .1f); combined.animator.Update(.01f);
                Assert.That(combined.animator.GetCurrentAnimatorStateInfo(upper).IsName("Locomotion"), Is.True);
            }
            finally { Object.DestroyImmediate(control.gameObject); Object.DestroyImmediate(combined.gameObject); Object.DestroyImmediate(sample.gameObject); }
        }

        [UnityTest]
        public IEnumerator ActualAnimatorFrameSwingsArmWhileLegsMoveAndHonorsPause()
        {
            var rig = Object.Instantiate(DoodleCharacterCatalog.Current.Player(-1).prefab);
            var sample = Object.Instantiate(rig); sample.gameObject.SetActive(false);
            try
            {
                rig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var arm = rig.skeleton.GetComponentsInChildren<Transform>().Single(t => t.name == "팔2");
                var original = arm.localRotation;
                var clip = rig.animator.runtimeAnimatorController.animationClips.First(c => c.name == "Attack");
                int upper = rig.animator.GetLayerIndex("Upper Body");
                rig.SetMoving(true); yield return new WaitForSeconds(.2f); rig.Attack();
                float greatestAngle = 0;
                for (int i = 0; i < 20; i++)
                {
                    yield return new WaitForSeconds(.02f);
                    greatestAngle = Mathf.Max(greatestAngle, Quaternion.Angle(original, arm.localRotation));
                    var state = rig.animator.GetCurrentAnimatorStateInfo(upper);
                    Assert.That(rig.animator.GetCurrentAnimatorStateInfo(0).IsName("Move"), Is.True);
                    if (state.IsName("Attack"))
                    {
                        clip.SampleAnimation(sample.gameObject, state.normalizedTime * clip.length);
                        var reference = sample.skeleton.GetComponentsInChildren<Transform>(true).Single(t => t.name == "팔2");
                        Assert.That(Quaternion.Angle(reference.localRotation, arm.localRotation), Is.LessThan(.1f));
                    }
                }
                Assert.That(greatestAngle, Is.GreaterThan(25), "Attacking arm must visibly swing in actual frame updates");
                rig.Attack(); yield return new WaitForSeconds(.1f);
                rig.animator.speed = 0; yield return null;
                var before = arm.localRotation; yield return new WaitForSeconds(.1f);
                Assert.That(Quaternion.Angle(before, arm.localRotation), Is.LessThan(.05f));
                rig.animator.speed = 1; rig.gameObject.SetActive(false); rig.gameObject.SetActive(true);
                rig.SetMoving(false); rig.Attack(); yield return new WaitForSeconds(.1f);
                Assert.That(rig.animator.GetCurrentAnimatorStateInfo(upper).IsName("Attack"), Is.True);
                rig.Hit(); yield return new WaitForSeconds(.1f);
                Assert.That(rig.animator.GetCurrentAnimatorStateInfo(upper).IsName("Locomotion"), Is.True);
            }
            finally { Object.DestroyImmediate(rig.gameObject); Object.DestroyImmediate(sample.gameObject); }
        }
    }
}
