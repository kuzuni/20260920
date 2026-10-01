using System.Collections;
using System.Collections.Generic;
using System.IO;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public sealed class DoodleFootDustSyncTests
    {
        [UnityTest]
        public IEnumerator SavingPlayerDustAutomaticallyPropagatesToOtherPrefabs()
        {
            const string directory="Assets/DoodleIdle/CharacterRigs/Prefabs";
            const string player=directory+"/Player_Standard.prefab";
            var originals=new Dictionary<string,string>();
            foreach(var path in Directory.GetFiles(directory,"*.prefab"))originals[path]=File.ReadAllText(path);
            try {
                var root=PrefabUtility.LoadPrefabContents(player);
                try {
                    var particles=root.GetComponent<CharacterRig>().footDust.particles;
                    var emission=particles.emission; emission.rateOverDistance=3.25f;
                    var main=particles.main;main.startSize=1.125f;
                    PrefabUtility.SaveAsPrefabAsset(root,player);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
                var deadline=EditorApplication.timeSinceStartup+15;
                bool synced=false;
                while(!synced && EditorApplication.timeSinceStartup<deadline) {
                    yield return null;
                    synced=true;
                    foreach(var path in originals.Keys){
                        var ps=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<CharacterRig>().footDust.particles;
                        synced &= Mathf.Abs(ps.emission.rateOverDistance.constantMax-3.25f)<.001f && Mathf.Abs(ps.main.startSize.constantMax-1.125f)<.001f;
                    }
                }
                Assert.That(synced,Is.True,"Saving the player must update all rig prefab particle settings without a manual sync command.");
            }finally{
                foreach(var pair in originals)File.WriteAllText(pair.Key,pair.Value);
                foreach(var path in originals.Keys)AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
