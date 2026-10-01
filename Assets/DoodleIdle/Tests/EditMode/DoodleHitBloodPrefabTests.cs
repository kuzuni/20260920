using System;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public sealed class DoodleHitBloodPrefabTests
    {
        [Test]
        public void EveryRigUsesSharedBloodPrefabAtHead()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DoodleIdle/CharacterRigs/Prefabs" });
            Assert.That(paths.Length, Is.EqualTo(6));
            foreach (var guid in paths) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var rig = prefab.GetComponent<CharacterRig>();
                Assert.That(rig.hitBlood, Is.Not.Null, prefab.name);
                Assert.That(rig.hitBlood.transform.parent, Is.EqualTo(rig.face.transform));
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rig.hitBlood),
                    Is.EqualTo("Assets/DoodleIdle/Resources/DoodleIdle/HitBlood.prefab"));
                Assert.That(rig.hitBlood.transform.localPosition.x, Is.LessThan(0));
            }
        }

        [Test]
        public void PlayerParticleOverridesPropagateThroughSharedPrefabWithoutMovingAnchors()
        {
            string folder = "Assets/HitBloodSyncTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            string common = folder + "/Common.prefab", player = folder + "/Player.prefab", other = folder + "/Other.prefab";
            GameObject editing = null;
            try {
                var root = new GameObject("Shared", typeof(ParticleSystem), typeof(CharacterHitBlood));
                root.GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var shared = PrefabUtility.SaveAsPrefabAsset(root, common); Object.DestroyImmediate(root);
                foreach (var path in new[] { player, other }) {
                    root = new GameObject("Rig");
                    var nested = (GameObject)PrefabUtility.InstantiatePrefab(shared, root.transform);
                    nested.transform.localPosition = path == player ? Vector3.left : Vector3.right;
                    PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
                }
                editing = PrefabUtility.LoadPrefabContents(player);
                var blood = editing.GetComponentInChildren<CharacterHitBlood>();
                var ps = blood.GetComponent<ParticleSystem>(); var main = ps.main;
                main.startSize = .73f; main.startSpeed = 4.2f; blood.rearUpAngle = -67;
                PrefabUtility.RecordPrefabInstancePropertyModifications(blood);
                var emission = ps.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 13) });
                var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sortingOrder = 34;
                PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                var sync = Type.GetType("CharacterHitBloodSync, Assembly-CSharp-Editor");
                Assert.That((bool)sync.GetMethod("PromoteParticleOverrides").Invoke(null, new object[] { blood, common }), Is.True);
                PrefabUtility.SaveAsPrefabAsset(editing, player);
                var target = AssetDatabase.LoadAssetAtPath<GameObject>(other).GetComponentInChildren<ParticleSystem>();
                Assert.That(target.main.startSize.constant, Is.EqualTo(.73f).Within(.001));
                Assert.That(target.main.startSpeed.constant, Is.EqualTo(4.2f).Within(.001));
                Assert.That(target.emission.GetBurst(0).count.constant, Is.EqualTo(13));
                Assert.That(target.GetComponent<ParticleSystemRenderer>().sortingOrder, Is.EqualTo(34));
                Assert.That(target.transform.localPosition, Is.EqualTo(Vector3.right));
                Assert.That(target.GetComponent<CharacterHitBlood>().rearUpAngle, Is.EqualTo(-67));
            } finally {
                if (editing) PrefabUtility.UnloadPrefabContents(editing);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
