using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    // Read the actual authored prefab/clips. No prebuild generator or asset writes.
    public sealed class DoodleConcurrentAttackTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OriginalAttackPlaysWithUninterruptedIdleOrMove(bool moving)
        {
            var source = DoodleCharacterCatalog.Current.Player(-1).prefab;
            var control = Object.Instantiate(source); var combined = Object.Instantiate(source); var sample = Object.Instantiate(source);
            var authored = source.animator.runtimeAnimatorController;
            var clip = authored.animationClips.First(c => c.name == "Attack");
            var a = RigAttackMixer.Create(control.animator); var b = RigAttackMixer.Create(combined.animator);
            try
            {
                a.SetMoving(moving); b.SetMoving(moving);
                for (int i = 0; i < 20; i++) { a.Evaluate(.05f); b.Evaluate(.05f); }
                b.Attack();
                for (int i = 0; i < 4; i++) { a.Evaluate(.04f); b.Evaluate(.04f); }
                Assert.That(b.IsAttacking, Is.True);
                clip.SampleAnimation(sample.gameObject, .16f);
                var attackBones = combined.skeleton.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                var reference = sample.skeleton.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                bool upperChanged = false;
                foreach (var bone in control.skeleton.GetComponentsInChildren<Transform>())
                {
                    var mixed = attackBones[bone.name];
                    if (bone.name.StartsWith("다리"))
                    {
                        Assert.That(Vector3.Distance(bone.localPosition, mixed.localPosition), Is.LessThan(.001f), bone.name);
                        Assert.That(Quaternion.Angle(bone.localRotation, mixed.localRotation), Is.LessThan(.05f), bone.name);
                    }
                    if (bone.name == "팔2")
                    {
                        Assert.That(Quaternion.Angle(reference[bone.name].localRotation, mixed.localRotation), Is.LessThan(.05f), "Upper body must play the unchanged Attack curve at full weight");
                        upperChanged = Quaternion.Angle(bone.localRotation, mixed.localRotation) > 1;
                    }
                }
                Assert.That(upperChanged, Is.True);
                // Start/stop moving while the same attack is in progress.
                a.SetMoving(!moving); b.SetMoving(!moving);
                for (int i = 0; i < 4; i++) { a.Evaluate(.04f); b.Evaluate(.04f); }
                foreach (var bone in control.skeleton.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("다리")))
                    Assert.That(Quaternion.Angle(bone.localRotation, attackBones[bone.name].localRotation), Is.LessThan(.05f));
                b.Evaluate(clip.length + .1f);
                Assert.That(b.IsAttacking, Is.False);
                Assert.That(combined.animator.runtimeAnimatorController, Is.SameAs(authored));
            }
            finally
            {
                a.Dispose(); b.Dispose();
                Object.DestroyImmediate(control.gameObject); Object.DestroyImmediate(combined.gameObject); Object.DestroyImmediate(sample.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator GameplayEntryPointPausesRetriggersAndResumesAfterPooling()
        {
            var rig = Object.Instantiate(DoodleCharacterCatalog.Current.Player(-1).prefab);
            try
            {
                rig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                rig.SetMoving(true);
                yield return null;
                rig.Attack();
                yield return new WaitForSeconds(.15f);
                var bones = rig.skeleton.GetComponentsInChildren<Transform>();
                rig.animator.speed = 0;
                yield return null;
                var rotations = bones.Select(t => t.localRotation).ToArray();
                yield return new WaitForSeconds(.12f);
                for (int i = 0; i < bones.Length; i++) Assert.That(Quaternion.Angle(rotations[i], bones[i].localRotation), Is.LessThan(.05f));
                rig.animator.speed = 1; rig.Attack();
                yield return new WaitForSeconds(.15f);
                rig.gameObject.SetActive(false); rig.gameObject.SetActive(true);
                rig.SetMoving(false); rig.Attack();
                yield return new WaitForSeconds(.15f);
                rig.Hit(); rig.Die();
                yield return null;
                Assert.That(rig.animator.runtimeAnimatorController, Is.Not.Null);
            }
            finally { Object.DestroyImmediate(rig.gameObject); }
        }
    }
}
