using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public sealed class DoodleSkillFaceTests
    {
        [Test]
        public void SkillEyeAndMouthAnchorsMatchCurrentPlayerPrefab()
        {
            var player=DoodleCharacterCatalog.Current.Player(-1).prefab.face;
            var catalog=Resources.Load<DoodleSkillFaceCatalog>("DoodleIdle/SkillFaceCatalog");
            foreach(var face in catalog.entries.Select(e=>e.facePrefab).Distinct())
            {
                Assert.That(face.leftEye.pupilMotion.parent.localPosition,Is.EqualTo(player.leftEye.pupilMotion.parent.localPosition),face.name);
                Assert.That(face.rightEye.pupilMotion.parent.localPosition,Is.EqualTo(player.rightEye.pupilMotion.parent.localPosition),face.name);
                Assert.That(face.mouth.transform.localPosition,Is.EqualTo(player.mouth.transform.localPosition),face.name);
                Assert.That(face.mouth.transform.localScale,Is.EqualTo(player.mouth.transform.localScale),face.name);
            }
        }
        [UnityTest]
        public IEnumerator EverySkillFrameUsesEditableMaskedFaceAndPreservesBlinkAcrossFrames()
        {
            var catalog=Resources.Load<DoodleSkillFaceCatalog>("DoodleIdle/SkillFaceCatalog");
            Assert.That(catalog.entries.Length,Is.EqualTo(21));
            foreach(var group in catalog.entries.GroupBy(e=>e.facePrefab))
            {
                var root=new GameObject("Skill face test");var body=root.AddComponent<SpriteRenderer>();
                var visual=root.AddComponent<DoodleSkillFaceVisual>();
                try
                {
                    var first=group.First();body.sprite=visual.Configure(null,body,first.body,first);
                    var face=visual.Face;face.leftEye.pupilMotion.parent.localPosition+=new Vector3(.012f,.023f,0);
                    var authored=face.leftEye.pupilMotion.parent.localPosition;
                    face.Blink();
                    foreach(var entry in group)
                    {
                        body.sprite=visual.Configure(null,body,entry.body,entry);
                        Assert.That(visual.Face,Is.SameAs(face));Assert.That(face.IsBlinking,Is.True);
                        Assert.That(face.leftEye.pupilMotion.parent.localPosition,Is.EqualTo(authored));
                        Assert.That(DoodleSkillFaceCatalog.Find(entry.body),Is.SameAs(entry));
                    }
                    face.ResetExpression();
                    foreach(bool flip in new[]{false,true})
                    {
                        body.flipX=flip;body.sortingOrder=flip?800:15;body.color=new Color(.7f,.5f,.3f,.4f);
                        yield return null;yield return null;
                        Assert.That(visual.Attachment.localScale.x,Is.EqualTo(flip?-1:1));
                        Assert.That(face.sortingGroup.sortingOrder,Is.GreaterThan(body.sortingOrder));
                        Assert.That(face.leftEye.highlight.color.a,Is.EqualTo(.4f));
                        Assert.That(face.leftEye.pupil.maskInteraction,Is.EqualTo(SpriteMaskInteraction.VisibleInsideMask));
                        Assert.That(face.leftEye.highlight.maskInteraction,Is.EqualTo(SpriteMaskInteraction.None));
                    }
                    body.enabled=false;yield return null;Assert.That(face.gameObject.activeInHierarchy,Is.False);
                    body.enabled=true;yield return null;Assert.That(face.gameObject.activeInHierarchy,Is.True);
                    face.ShowHit();Assert.That(face.mouth.sprite,Is.SameAs(face.hurtMouth));
                    root.SetActive(false);root.SetActive(true);Assert.That(face.IsHurt,Is.False);
                }
                finally{Object.DestroyImmediate(root);}
            }
        }
    }

    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator SkillFacesAppearInRealCastsTrackEnemiesAndSurviveVisualPooling()
        {
            var bodies=DurableSkillTargets();
            for(int i=0;i<8;i++)Place(bodies[i],new Vector2(4+i*.5f,0));
            foreach(var skill in new[]{DoodleIdleGame.SummonSkill.WaveSnakes,DoodleIdleGame.SummonSkill.TetherSnake,DoodleIdleGame.SummonSkill.Dragon,DoodleIdleGame.SummonSkill.StormCloud})game.CastSummonSkill(skill);
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Worm);game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Drone);
            foreach(var ability in new[]{"Golem","FireGolem","IceSnakes","RedCloud","MightyDragon","SawSnakes"})Assert.That(CastCatalogSkill(ability),Is.True,ability);
            yield return null;yield return null;
            var faces=game.GetComponentsInChildren<DoodleSkillFaceVisual>().Where(v=>v.Face && v.Face.isActiveAndEnabled).ToArray();
            Assert.That(faces.Length,Is.GreaterThanOrEqualTo(25));
            foreach(var visual in faces)
            {
                Assert.That(visual.Face.target,Is.Not.Null,visual.name);
                Assert.That(DoodleSkillFaceCatalog.Find(visual.GetComponent<SpriteRenderer>().sprite),Is.Not.Null,visual.name);
            }
            var catalog=Resources.Load<DoodleSkillFaceCatalog>("DoodleIdle/SkillFaceCatalog");
            var entry=catalog.entries.First(e=>e.sourceName=="SkillGolem_0");
            var rent=typeof(DoodleIdleGame).GetMethod("RentVisual",BindingFlags.Instance|BindingFlags.NonPublic);
            var release=typeof(DoodleIdleGame).GetMethod("ReleaseVisual",BindingFlags.Instance|BindingFlags.NonPublic);
            object[] args={"Pooled skill face regression",entry.body,Vector2.zero,Vector2.one,501};
            var art=(SpriteRenderer)rent.Invoke(game,args);var face=art.GetComponent<DoodleSkillFaceVisual>().Face;
            face.ShowHit();release.Invoke(game,new object[]{art.gameObject});
            var reused=(SpriteRenderer)rent.Invoke(game,args);Assert.That(reused,Is.SameAs(art));
            Assert.That(reused.GetComponent<DoodleSkillFaceVisual>().Face,Is.SameAs(face));Assert.That(face.IsHurt,Is.False);
            Assert.That(reused.transform.childCount,Is.EqualTo(1));
            release.Invoke(game,new object[]{reused.gameObject});
            args[1]=DoodleExpansionArt.Get("SkillDumbbell");reused=(SpriteRenderer)rent.Invoke(game,args);
            Assert.That(reused.GetComponent<DoodleSkillFaceVisual>().Face.gameObject.activeInHierarchy,Is.False);
            release.Invoke(game,new object[]{reused.gameObject});
        }
    }
}
