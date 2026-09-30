using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

namespace DoodleIdle.Tests
{
    public sealed class DoodleCharacterFaceTests
    {
        [UnityTest]
        public IEnumerator AllCompanionsTrackTargetsAndBlinkWithSeparateMouthAndFriendlyEyes()
        {
            var target = new GameObject("Companion gaze target");
            try
            {
                var entries = DoodleCharacterCatalog.Current.entries.Where(e => e.group == "Companions").ToArray();
                Assert.That(entries.Length, Is.EqualTo(32));
                foreach (var entry in entries)
                {
                    var rig = Object.Instantiate(entry.prefab);
                    try
                    {
                        rig.SetAppearance(entry.appearance); rig.animator.enabled = false;
                        var face = rig.face; face.gazeSpeed = 1000;
                        face.target = target.transform;
                        target.transform.position = face.transform.position + new Vector3(10, 3, 0);
                        yield return null; yield return null;
                        Assert.That(face.isActiveAndEnabled, Is.True, entry.id);
                        Assert.That(face.leftEye.pupilMotion.localPosition.sqrMagnitude, Is.GreaterThan(.001f), entry.id);
                        Assert.That(face.leftEye.highlight.enabled, Is.True, entry.id);
                        face.Blink(); Assert.That(face.IsBlinking, Is.True);
                        face.ShowHit();
                        Assert.That(face.mouth.sprite, Is.SameAs(face.hurtMouth), entry.id);
                        face.ResetExpression();
                        Assert.That(face.mouth.sprite, Is.SameAs(face.normalMouth), entry.id);
                        Assert.That(face.mouth.enabled, Is.True, entry.id);
                        foreach (var eye in new[] {face.leftEye, face.rightEye})
                            if (eye.brow) Assert.That(eye.brow.enabled, Is.False, entry.id);
                    }
                    finally { Object.DestroyImmediate(rig.gameObject); }
                }
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void EyeHighlightsStayWholeAtEveryGazeEdgeAndRespectClosedEyes()
        {
            foreach(var prefab in DoodleCharacterCatalog.Current.entries.Select(e=>e.prefab).Distinct())
            {
                var rig=Object.Instantiate(prefab);
                try
                {
                    rig.face.gameObject.SetActive(true);rig.animator.enabled=false;
                    var face=rig.face;
                    foreach(float side in new[]{1f,-1f})
                    {
                        rig.transform.localScale=new Vector3(side*.7f,1.2f,1);rig.transform.rotation=Quaternion.Euler(0,0,17);
                        rig.transform.position=new Vector3(7,-11,0);
                        for(int i=0;i<16;i++)
                        {
                            var direction=new Vector2(Mathf.Cos(i*Mathf.PI/8),Mathf.Sin(i*Mathf.PI/8));
                            foreach(var eye in new[]{face.leftEye,face.rightEye})eye.pupilMotion.localPosition=Vector2.Scale(direction,eye.travel);
                            face.RefreshHighlights();
                            var beforeLeft=face.leftEye.highlightMotion.position;
                            var beforeRight=face.rightEye.highlightMotion.position;
                            for(int repeat=0;repeat<16;repeat++)face.RefreshHighlights();
                            Assert.That(Vector3.Distance(beforeLeft,face.leftEye.highlightMotion.position),Is.LessThan(.0001f),"Left glint must not drift");
                            Assert.That(Vector3.Distance(beforeRight,face.rightEye.highlightMotion.position),Is.LessThan(.0001f),"Right glint must not drift");
                            foreach(var eye in new[]{face.leftEye,face.rightEye})
                            {
                                Assert.That(eye.highlight.enabled,Is.True,prefab.name+" direction "+i);
                                Assert.That(eye.highlight.maskInteraction,Is.EqualTo(SpriteMaskInteraction.None));
                                var h=eye.highlight;var bounds=h.sprite.bounds;
                                foreach(var shape in new[]{(eye.pupilMask.transform,eye.pupilMask.sprite),(eye.pupil.transform,eye.pupil.sprite)})
                                foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})
                                {
                                    var point=h.transform.TransformPoint(bounds.center+new Vector3(x*bounds.extents.x,y*bounds.extents.y,0));
                                    var local=(Vector2)(shape.Item1.InverseTransformPoint(point)-shape.Item2.bounds.center);
                                    Assert.That(local.magnitude,Is.LessThanOrEqualTo(Mathf.Min(shape.Item2.bounds.extents.x,shape.Item2.bounds.extents.y)*.90f+.001f));
                                }
                            }
                        }
                    }
                    face.SetTint(new Color(1,.4f,.4f,.3f));Assert.That(face.leftEye.highlight.color.a,Is.EqualTo(.3f));
                    face.ShowHit();Assert.That(face.leftEye.highlight.enabled,Is.False);
                    face.ResetExpression();Assert.That(face.leftEye.highlight.enabled,Is.True);
                    face.leftEye.lidMotion.localScale=new Vector3(1,.045f,1);face.RefreshHighlights();
                    Assert.That(face.leftEye.highlight.enabled,Is.False,"A closed eyelid must not have a floating white dot");
                }
                finally{Object.DestroyImmediate(rig.gameObject);}
            }
        }

        [UnityTest]
        public IEnumerator QuadMovingHeadNeverCoversFace()
        {
            var entry = DoodleCharacterCatalog.Current.entries.Single(e=>e.id=="e02_quad_sand_boar");
            var rig = Object.Instantiate(entry.prefab);
            try
            {
                rig.SetAppearance(entry.appearance); rig.animator.enabled = false;
                var clip = rig.animator.runtimeAnimatorController.animationClips.First(c=>c.name=="Move");
                int maxOrder = 0;
                foreach (float side in new[] {1f,-1f})
                {
                    rig.transform.localScale = new Vector3(side,1,1);
                    for (int frame=0; frame<12; frame++)
                    {
                        clip.SampleAnimation(rig.gameObject, clip.length*frame/12);
                        int headOrder = rig.face.headRenderer.sortingOrder;
                        maxOrder = Mathf.Max(maxOrder,headOrder);
                        yield return null; yield return null;
                        Assert.That(rig.face.sortingGroup.sortingOrder, Is.GreaterThan(headOrder));
                        Assert.That(rig.face.headRenderer.sortingOrder, Is.EqualTo(headOrder), "The authored animation must not be overridden");
                        Assert.That(rig.face.gameObject.activeInHierarchy, Is.True);
                        Assert.That(rig.face.mouth.enabled, Is.True);
                    }
                }
                Assert.That(maxOrder, Is.GreaterThan(40), "Exercise the previously occluded movement frames");
                rig.face.Paused = true; rig.face.headRenderer.sortingOrder = 90;
                yield return null; yield return null;
                Assert.That(rig.face.sortingGroup.sortingOrder, Is.GreaterThan(90));
                clip.SampleAnimation(rig.gameObject,.26666668f);
                foreach(var t in rig.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                yield return null; yield return null;
                var cameraObject = new GameObject("Quad face capture");
                var target = new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);
                try
                {
                    var camera = cameraObject.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=5;
                    camera.transform.position=rig.face.transform.position+new Vector3(0,0,-20);camera.cullingMask=1<<31;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.targetTexture=target;
                    Color[] Capture(string name)
                    {
                        for(int i=0;i<2;i++)RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                        var previous=RenderTexture.active;RenderTexture.active=target;
                        var image=new Texture2D(256,256,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                        System.IO.Directory.CreateDirectory("artifacts/pupil-masking");System.IO.File.WriteAllBytes("artifacts/pupil-masking/quad-"+name+".png",image.EncodeToPNG());
                        var pixels=image.GetPixels();Object.DestroyImmediate(image);RenderTexture.active=previous;return pixels;
                    }
                    yield return null; var shown=Capture("moving-face");
                    rig.face.gameObject.SetActive(false);yield return null;var hidden=Capture("without-face");
                    int changed=0;for(int i=0;i<shown.Length;i++)if(Mathf.Abs(shown[i].r-hidden[i].r)>.15f)changed++;
                    Assert.That(changed,Is.GreaterThan(100),"The face must actually render in front of the animated head");
                }
                finally{Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(target);}
            }
            finally { Object.DestroyImmediate(rig.gameObject); }
        }

        [UnityTest]
        public IEnumerator PupilPixelsAreClippedAndNeighborMasksCannotRevealThem()
        {
            var prefab = DoodleCharacterCatalog.Current.entries.First(e=>e.group=="Player").prefab;
            var root = new GameObject("Mask pixel test");
            var eye = Object.Instantiate(prefab.face.leftEye.pupilMotion.parent.gameObject,root.transform);
            var cameraObject = new GameObject("Mask test camera");
            var rt = new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);
            try
            {
                eye.transform.localPosition = Vector3.zero; eye.transform.localRotation = Quaternion.identity; eye.transform.localScale = Vector3.one;
                var white = eye.transform.Find("EyelidMotion/White").GetComponent<SpriteRenderer>();
                var pupil = eye.transform.Find("PupilMotion/Pupil").GetComponent<SpriteRenderer>();
                eye.GetComponentsInChildren<SpriteRenderer>().First(r=>r.name=="Highlight").enabled=false;
                float width = white.sprite.bounds.size.x*white.transform.localScale.x;
                eye.transform.Find("PupilMotion").localPosition = new Vector3(width*.34f,0,0);
                var other = Object.Instantiate(eye,root.transform);
                other.transform.localPosition = new Vector3(width*.65f,0,0);
                foreach (var r in other.GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
                var cam = cameraObject.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = width*.8f;
                cam.transform.position = new Vector3(0,0,-10); cam.cullingMask = 1<<31;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear; cam.targetTexture = rt;
                yield return null; yield return null;
                Color[] Capture()
                {
                    for(int i=0;i<2;i++) RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=rt });
                    var previous = RenderTexture.active; RenderTexture.active = rt;
                    var image = new Texture2D(256,256,TextureFormat.RGBA32,false);
                    image.ReadPixels(new Rect(0,0,256,256),0,0); image.Apply(); var pixels = image.GetPixels();
                    Object.DestroyImmediate(image); RenderTexture.active = previous; return pixels;
                }
                pupil.enabled = false; yield return null; var baseline = Capture();
                pupil.enabled = true; yield return null; var masked = Capture();
                pupil.maskInteraction = SpriteMaskInteraction.None; yield return null; var unmasked = Capture();
                System.IO.Directory.CreateDirectory("artifacts/pupil-masking");
                foreach (var frame in new[] { ("baseline",baseline),("masked",masked),("unmasked",unmasked) })
                {
                    var image = new Texture2D(256,256,TextureFormat.RGBA32,false); image.SetPixels(frame.Item2); image.Apply();
                    System.IO.File.WriteAllBytes("artifacts/pupil-masking/test-"+frame.Item1+".png",image.EncodeToPNG()); Object.DestroyImmediate(image);
                }
                int leak = 0, challenge = 0, visible = 0;
                for(int i=0;i<baseline.Length;i++)
                {
                    if(baseline[i].a < .02f) { if(masked[i].a > .1f) leak++; if(unmasked[i].a > .1f) challenge++; }
                    if(baseline[i].a > .9f && baseline[i].r - masked[i].r > .3f) visible++;
                }
                Assert.That(challenge,Is.GreaterThan(100),"Unmasked pupil must extend past the eye for this test");
                Assert.That(visible,Is.GreaterThan(100),"Masked pupil must remain visible inside the eye");
                Assert.That(leak,Is.Zero,"No pupil pixels may escape into a neighboring eye's mask");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(rt); }
        }

        [UnityTest]
        public IEnumerator IdleBlinkPreservesMouthAndAnchorsAndYieldsToDamage()
        {
            var rig = Object.Instantiate(DoodleCharacterCatalog.Current.entries.First(e=>e.group=="Enemies").prefab);
            try
            {
                rig.animator.enabled = false;
                var face = rig.face;
                face.blinkInterval = new Vector2(.03f,.03f); face.blinkDuration = .5f;
                face.ResetExpression();
                var position = face.leftEye.pupilMotion.parent.localPosition;
                yield return new WaitForSeconds(.09f);
                Assert.That(face.IsBlinking, Is.True);
                Assert.That(face.leftEye.white.sprite, Is.SameAs(face.leftEye.normal));
                Assert.That(face.leftEye.pupil.enabled, Is.True);
                Assert.That(face.leftEye.lidMotion.localScale.y, Is.InRange(.045f,.99f));
                Assert.That(face.mouth.sprite, Is.SameAs(face.normalMouth));
                Assert.That(face.leftEye.pupilMotion.parent.localPosition, Is.EqualTo(position));
                var pausedLid = face.leftEye.lidMotion.localScale;
                face.Paused = true;
                yield return new WaitForSeconds(.6f);
                Assert.That(face.IsBlinking, Is.True);
                Assert.That(face.leftEye.lidMotion.localScale, Is.EqualTo(pausedLid));
                face.ShowHit();
                Assert.That(face.IsBlinking, Is.False);
                Assert.That(face.IsHurt, Is.True);
                Assert.That(face.leftEye.white.sprite, Is.SameAs(face.leftEye.hurt));
                Assert.That(face.leftEye.brow.enabled, Is.False);
                Assert.That(face.leftEye.lidMotion.localScale, Is.EqualTo(Vector3.one));
                face.Paused = false; face.blinkInterval = Vector2.one*10;
                face.ResetExpression();
                Assert.That(face.leftEye.pupil.enabled, Is.True);
                Assert.That(face.leftEye.brow.enabled, Is.True);
                face.blinkInterval = Vector2.one*.01f; face.blinkDuration = .04f;
                face.ResetExpression();
                yield return null; yield return null;
                face.blinkInterval = Vector2.one*10;
                yield return new WaitForSeconds(.1f);
                Assert.That(face.IsBlinking, Is.False);
                Assert.That(face.leftEye.pupil.enabled, Is.True);
                Assert.That(face.leftEye.lidMotion.localScale, Is.EqualTo(Vector3.one));
            }
            finally { Object.DestroyImmediate(rig.gameObject); }
        }

        [Test]
        public void AllSkinsIncludingCompanionsUsePrefabFaceAnchors()
        {
            foreach (var group in DoodleCharacterCatalog.Current.entries.GroupBy(e => e.prefab))
            {
                var rig = Object.Instantiate(group.Key);
                try
                {
                    Assert.That(rig.face, Is.Not.Null);
                    Assert.That(rig.face.transform.parent.name, Is.EqualTo("머리"));
                    var face = rig.face;
                    Assert.That(face.headRenderer, Is.Not.Null);
                    Assert.That(face.sortingGroup, Is.Not.Null);
                    foreach (var eye in new[] { face.leftEye, face.rightEye })
                    {
                        Assert.That(eye.pupilMask, Is.Not.Null);
                        Assert.That(eye.pupil.sharedMaterial.shader.name, Is.EqualTo("DoodleIdle/Pupil Clip"));
                        Assert.That(eye.pupilMask.enabled, Is.False, "The authored mask is sampled by the pupil shader");
                        Assert.That(eye.white.GetComponentInParent<SortingGroup>(), Is.SameAs(eye.pupil.GetComponentInParent<SortingGroup>()));
                    }
                    face.leftEye.pupilMotion.parent.localPosition += new Vector3(.17f,.23f,0);
                    var positions = face.GetComponentsInChildren<Transform>(true)
                        .Where(t => t != face.leftEye.highlightMotion && t != face.rightEye.highlightMotion)
                        .ToDictionary(t=>t,t=>t.localPosition);
                    var controller = rig.animator.runtimeAnimatorController;
                    foreach (var entry in group)
                    {
                        rig.SetAppearance(entry.appearance);
                        Assert.That(face.gameObject.activeSelf, Is.True, entry.id);
                        Assert.That(face.mouth.enabled, Is.True, entry.id);
                        foreach (var eye in new[] {face.leftEye, face.rightEye})
                            if (eye.brow) Assert.That(eye.brow.enabled, Is.EqualTo(entry.group != "Companions"), entry.id);
                        Assert.That(rig.animator.runtimeAnimatorController, Is.SameAs(controller));
                        foreach (var item in positions)
                            Assert.That(item.Key.localPosition, Is.EqualTo(item.Value), entry.id + "/" + item.Key.name);
                    }
                }
                finally { Object.DestroyImmediate(rig.gameObject); }
            }
        }

        [UnityTest]
        public IEnumerator GazeTracksWorldTargetAcrossMirroringAndHeadRotation()
        {
            var rig = Object.Instantiate(DoodleCharacterCatalog.Current.entries.First(e=>e.group=="Enemies").prefab);
            var target = new GameObject("Face test target");
            try
            {
                rig.animator.enabled = false;
                var face = rig.face; face.gazeSpeed = 1000;
                face.target = target.transform;
                foreach (float side in new[] {1f,-1f})
                {
                    rig.transform.localScale = new Vector3(side,1,1);
                    face.transform.parent.Rotate(0,0,23);
                    var eye = face.leftEye;
                    var origin = eye.pupilMotion.parent;
                    target.transform.position = origin.position + new Vector3(5,2,0);
                    yield return null; yield return null;
                    var worldOffset = eye.pupilMotion.position - origin.position;
                    Assert.That(Vector3.Dot(worldOffset,target.transform.position-origin.position), Is.GreaterThan(0));
                    var offset = eye.pupilMotion.localPosition;
                    Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(eye.travel.x + .001f));
                    Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(eye.travel.y + .001f));
                }
                target.SetActive(false);
                yield return new WaitForSeconds(.08f);
                Assert.That(face.leftEye.pupilMotion.localPosition.magnitude, Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(target); Object.DestroyImmediate(rig.gameObject); }
        }

        [UnityTest]
        public IEnumerator DamageFacePausesRecoversAndResetsWhenPooled()
        {
            var rig = Object.Instantiate(DoodleCharacterCatalog.Current.entries.First(e=>e.group=="Enemies").prefab);
            try
            {
                var face = rig.face; face.hurtDuration = .08f;
                rig.ReactToDamage();
                Assert.That(face.IsHurt, Is.True);
                Assert.That(face.leftEye.pupil.enabled, Is.False);
                Assert.That(face.mouth.sprite, Is.SameAs(face.hurtMouth));
                face.Paused = true;
                yield return new WaitForSeconds(.12f);
                Assert.That(face.IsHurt, Is.True);
                face.Paused = false;
                yield return new WaitForSeconds(.12f);
                Assert.That(face.IsHurt, Is.False);
                Assert.That(face.mouth.sprite, Is.SameAs(face.normalMouth));
                Assert.That(face.leftEye.pupil.enabled, Is.True);
                face.ShowHit(); rig.gameObject.SetActive(false); rig.gameObject.SetActive(true);
                Assert.That(face.IsHurt, Is.False);
                Assert.That(face.target, Is.Null);
            }
            finally { Object.DestroyImmediate(rig.gameObject); }
        }

        [UnityTest]
        public IEnumerator PooledFacesRestoreGlintsBeforeTheirFirstVisibleFrame()
        {
            foreach (var entry in DoodleCharacterCatalog.Current.entries.Where(e => e.group == "Enemies").GroupBy(e => e.appearance.rigType).Select(g => g.First())) {
                var root = new GameObject("Pooled face regression", typeof(SpriteRenderer));
                try {
                    var visual = root.AddComponent<DoodleRigVisual>(); visual.Configure(entry);
                    var face = visual.Rig.face;
                    face.ShowHit(); visual.ParkRig();
                    Assert.That(face.IsHurt, Is.False); Assert.That(face.enabled, Is.False);
                    root.transform.position = new Vector3(12, -7, 0);
                    root.transform.rotation = Quaternion.Euler(0, 0, 23);
                    root.transform.localScale = new Vector3(-1.3f, .8f, 1);
                    visual.RestoreRig();
                    Assert.That(face.enabled, Is.True);
                    Assert.That(face.mouth.sprite, Is.SameAs(face.normalMouth));
                    foreach (var eye in new[] { face.leftEye, face.rightEye }) {
                        Assert.That(eye.highlight.enabled, Is.True, entry.id);
                        var first = eye.highlightMotion.localPosition;
                        face.RefreshHighlights();
                        Assert.That(Vector3.Distance(first, eye.highlightMotion.localPosition), Is.LessThan(.0001f), "No deferred visible-frame correction: " + entry.id);
                    }
                } finally { Object.DestroyImmediate(root); }
            }
            yield return null;
        }
    }
}
