using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public sealed class DoodleFootDustTests
    {
        [Test]
        public void EveryCharacterPrefabHasEditableWorldSpaceFootDust()
        {
            foreach(var entry in DoodleCharacterCatalog.Current.entries)
            {
                var rig=entry.prefab; var dust=rig.footDust;
                Assert.That(dust,Is.Not.Null,entry.id);
                Assert.That(dust.particles.transform.parent,Is.SameAs(rig.groundContact));
                Assert.That(dust.particles.main.simulationSpace,Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(dust.particles.emission.enabled,Is.True);
                Assert.That(dust.particles.emission.rateOverTime.constantMax,Is.Zero);
                Assert.That(dust.particles.emission.burstCount,Is.Zero);
                var source=DoodleCharacterCatalog.Current.entries.First(e=>e.group=="Player").prefab.footDust.particles;
                Assert.That(dust.particles.emission.rateOverDistance.constantMax,Is.EqualTo(source.emission.rateOverDistance.constantMax));
                Assert.That(dust.particles.main.startSize.constantMax,Is.EqualTo(source.main.startSize.constantMax));
                Assert.That(dust.particles.main.startColor.color,Is.EqualTo(source.main.startColor.color));
                Assert.That(dust.particles.GetComponent<SortingGroup>().sortAtRoot,Is.True);
            }
        }
        [UnityTest]
        public IEnumerator PlayerEnemiesAndCompanionsLeaveDistanceSpacedDustOnlyWhenMoving()
        {
            foreach(string group in new[]{"Player","Enemies","Companions"})
            {
                var entry=DoodleCharacterCatalog.Current.entries.First(e=>e.group==group);
                var root=new GameObject("Dust test "+group,typeof(SpriteRenderer),typeof(DoodleRigVisual));
                try
                {
                    var visual=root.GetComponent<DoodleRigVisual>();visual.Configure(entry);
                    var dust=visual.Rig.footDust;var ps=dust.particles;
                    var main=ps.main;main.startLifetime=3;main.gravityModifier=0;main.startSpeed=0;
                    var emission=ps.emission;emission.rateOverDistance=4;
                    yield return null;yield return null;
                    visual.Moving(true);root.GetComponent<SpriteRenderer>().flipX=true;
                    yield return null;yield return null;
                    Assert.That(ps.particleCount,Is.Zero,"Walking animation and flipping at rest must not emit");
                    for(int i=0;i<16;i++){root.transform.position+=Vector3.right*.125f;yield return null;}
                    yield return null;
                    Assert.That(ps.particleCount,Is.InRange(6,9),group+": native 4 particles/metre over 2 metres");
                    int stoppedCount=ps.particleCount;
                    yield return new WaitForSeconds(.1f);
                    Assert.That(ps.particleCount,Is.EqualTo(stoppedCount),"Idle cannot use time-based emission");
                    visual.Paused=true;visual.Sync();yield return null;
                    root.transform.position+=Vector3.right;
                    yield return null;yield return null;
                    Assert.That(ps.isPaused,Is.True);Assert.That(ps.particleCount,Is.EqualTo(stoppedCount));
                    visual.Paused=false;visual.Sync();yield return null;
                    root.transform.position+=Vector3.right*20;
                    yield return null;yield return null;Assert.That(ps.particleCount,Is.Zero,"Teleports reset the native trail");
                    emission.rateOverDistance=0;
                    for(int i=0;i<8;i++){root.transform.position+=Vector3.right*.125f;yield return null;}
                    yield return null;Assert.That(ps.particleCount,Is.Zero,"Inspector Rate over Distance must be the only emission source");
                    emission.rateOverDistance=4;
                    for(int i=0;i<8;i++){root.transform.position+=Vector3.right*.125f;yield return null;}
                    yield return null;Assert.That(ps.particleCount,Is.GreaterThan(0));
                    root.SetActive(false);Assert.That(ps.particleCount,Is.Zero);
                    root.transform.position=Vector3.zero;root.SetActive(true);
                    yield return null;yield return null;Assert.That(ps.particleCount,Is.Zero,"Pool reactivation starts clean");
                }
                finally{Object.DestroyImmediate(root);}
            }
        }
        [UnityTest]
        public IEnumerator FootDustIsVisibleBelowAllThreeCharacterGroups()
        {
            var stage=new GameObject("Foot dust render stage");
            var cameraObject=new GameObject("Foot dust camera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();
            var target=new RenderTexture(1200,600,24);
            Texture2D Capture()
            {
                camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(1200,600,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,1200,600),0,0);image.Apply();RenderTexture.active=previous;return image;
            }
            try
            {
                stage.transform.position=new Vector3(20000,0,0);
                camera.transform.position=new Vector3(20000,0,-10);
                camera.orthographic=true;camera.orthographicSize=1.4f;camera.aspect=2;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.87f,.78f,.59f);camera.targetTexture=target;
                var actors=new System.Collections.Generic.List<DoodleRigVisual>();
                int index=0;
                foreach(string group in new[]{"Player","Enemies","Companions"})
                {
                    var root=new GameObject(group,typeof(SpriteRenderer),typeof(DoodleRigVisual));root.transform.SetParent(stage.transform,false);
                    root.transform.localPosition=new Vector3(-2.2f+index++*1.8f,0,0);
                    var visual=root.GetComponent<DoodleRigVisual>();visual.Configure(DoodleCharacterCatalog.Current.entries.First(e=>e.group==group));
                    visual.Moving(true);actors.Add(visual);
                }
                yield return null;
                for(int frame=0;frame<12;frame++)
                {
                    foreach(var actor in actors)actor.transform.position+=Vector3.right*.055f;
                    yield return null;
                }
                foreach(var actor in actors){actor.Rig.footDust.Paused=true;Assert.That(actor.Rig.footDust.particles.particleCount,Is.GreaterThan(0));}
                var visible=Capture();
                foreach(var actor in actors)actor.Rig.footDust.particles.GetComponent<ParticleSystemRenderer>().enabled=false;
                var hidden=Capture();
                try
                {
                    var a=visible.GetPixels32();var b=hidden.GetPixels32();int different=0;
                    for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>4)different++;
                    Assert.That(different,Is.GreaterThan(20),"Ground dust must be visibly rendered");
                    System.IO.Directory.CreateDirectory("artifacts/character-reports");
                    System.IO.File.WriteAllBytes("artifacts/character-reports/foot-dust-visible.png",visible.EncodeToPNG());
                }
                finally{Object.DestroyImmediate(visible);Object.DestroyImmediate(hidden);}
            }
            finally{camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(stage);}
        }
        [UnityTest]
        public IEnumerator PlayerAlwaysLooksSidewaysEvenWhenTargetIsAboveBelowOrMissing()
        {
            var rig=Object.Instantiate(DoodleCharacterCatalog.Current.entries.First(e=>e.group=="Player").prefab);
            var target=new GameObject("Horizontal gaze target");
            try
            {
                rig.animator.enabled=false;var face=rig.face;face.blinking=false;
                Assert.That(face.horizontalGazeOnly,Is.True);face.target=target.transform;
                foreach(float mirror in new[]{1f,-1f})
                {
                    rig.transform.localScale=new Vector3(mirror,1,1);
                    foreach(var position in new[]{new Vector3(-10,100,0),new Vector3(10,-100,0),new Vector3(0,100,0)})
                    {
                        target.transform.position=face.transform.TransformPoint(position);
                        yield return null;yield return null;
                        foreach(var eye in new[]{face.leftEye,face.rightEye})
                        {
                            Assert.That(eye.pupilMotion.localPosition.y,Is.Zero);
                            Assert.That(Mathf.Abs(eye.pupilMotion.localPosition.x),Is.EqualTo(eye.travel.x).Within(.0001f));
                            if(position.x!=0)Assert.That(Mathf.Sign(eye.pupilMotion.localPosition.x),Is.EqualTo(Mathf.Sign(position.x)));
                        }
                    }
                }
                var before=face.leftEye.pupilMotion.localPosition;target.SetActive(false);
                yield return null;yield return null;Assert.That(face.leftEye.pupilMotion.localPosition,Is.EqualTo(before));
            }
            finally{Object.DestroyImmediate(target);Object.DestroyImmediate(rig.gameObject);}
        }
    }
}
