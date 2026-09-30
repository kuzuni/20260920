using System.Collections;
using System.Linq;
using System.Reflection;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public sealed class DoodleFaceVisibilityTests
    {
        static readonly MethodInfo Tick = typeof(CharacterFace).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly int ClipMatrix = Shader.PropertyToID("_PupilToMask");

        [Test]
        public void PooledWorldFacesKeepNativeMasksDisabledAndFitBeforeRendering()
        {
            var cameraObject = new GameObject("Pooled face camera", typeof(Camera));
            try {
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                var view = new CharacterFaceView();
                foreach (var entry in DoodleCharacterCatalog.Current.entries.Where(e => e.group == "Enemies").GroupBy(e => e.appearance.rigType).Select(g => g.First())) {
                    var root = new GameObject("Pooled world face", typeof(SpriteRenderer), typeof(DoodleRigVisual));
                    try {
                        var visual = root.GetComponent<DoodleRigVisual>(); visual.Configure(entry);
                        var face = visual.Rig.face; face.View = view;
                        face.RefreshHighlights();
                        for (int reuse = 0; reuse < 3; reuse++) {
                            face.ShowHit(); visual.ParkRig();
                            root.transform.SetPositionAndRotation(new Vector3(17, 23, 0), Quaternion.Euler(0, 0, reuse * 17));
                            root.transform.localScale = new Vector3(reuse == 1 ? -2 : 1, .8f, 1);
                            visual.RestoreRig();
                            Assert.That(face.leftEye.pupilMask.enabled, Is.False, entry.id);
                            Assert.That(face.rightEye.pupilMask.enabled, Is.False, entry.id);
                            camera.transform.position = face.transform.position + Vector3.back * 10;
                            view.Update(camera); Tick.Invoke(face, null);
                            Assert.That(face.leftEye.highlight.enabled, Is.True, entry.id);
                            var glint = face.leftEye.highlightMotion.localPosition;
                            face.RefreshHighlights();
                            Assert.That(Vector3.Distance(face.leftEye.highlightMotion.localPosition, glint), Is.LessThan(.0001f), entry.id);
                        }
                    } finally { Object.DestroyImmediate(root); }
                }
            } finally { Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void WorldFacesRefreshOnTheFirstVisibleFrameIncludingCameraTeleportsAndMirroring()
        {
            var cameraObject = new GameObject("Face visibility camera", typeof(Camera));
            var target = new GameObject("Face visibility target");
            try {
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 3;
                camera.transform.position = new Vector3(0, 0, -10);
                var view = new CharacterFaceView();
                var properties = new MaterialPropertyBlock();
                var prefabs = DoodleCharacterCatalog.Current.entries.Select(e => e.prefab.face)
                    .Concat(Resources.Load<DoodleSkillFaceCatalog>("DoodleIdle/SkillFaceCatalog").entries.Select(e => e.facePrefab)).Distinct();
                foreach (var prefab in prefabs) {
                    var face = Object.Instantiate(prefab);
                    try {
                        face.transform.SetPositionAndRotation(new Vector3(100, 100, 0), Quaternion.Euler(0, 0, 29));
                        face.transform.localScale = new Vector3(-1.2f, .7f, 1);
                        face.blinking = false; face.horizontalGazeOnly = true;
                        face.ResetExpression(); face.View = view;
                        face.leftEye.pupil.GetPropertyBlock(properties); var before = properties.GetMatrix(ClipMatrix);
                        target.transform.position = face.transform.TransformPoint(new Vector3(-5, 0, 0)); face.target = target.transform;
                        view.Update(camera);
                        Assert.That(view.MaySee(face.leftEye.pupilMask), Is.False, prefab.name);
                        Tick.Invoke(face, null);
                        Assert.That(face.leftEye.pupilMotion.localPosition.x, Is.LessThan(0), "Offscreen gaze keeps tracking");
                        face.leftEye.pupil.GetPropertyBlock(properties);
                        Assert.That(properties.GetMatrix(ClipMatrix), Is.EqualTo(before), "Offscreen clip fit is skipped");
                        camera.transform.position = face.leftEye.pupilMask.transform.position + Vector3.back * 10;
                        view.Update(camera); Tick.Invoke(face, null);
                        face.leftEye.pupil.GetPropertyBlock(properties); var visible = properties.GetMatrix(ClipMatrix);
                        var glint = face.leftEye.highlightMotion.localPosition;
                        Assert.That(visible, Is.Not.EqualTo(before), "Camera teleport updates on this frame");
                        face.RefreshHighlights();
                        face.leftEye.pupil.GetPropertyBlock(properties);
                        Assert.That(properties.GetMatrix(ClipMatrix), Is.EqualTo(visible));
                        Assert.That(Vector3.Distance(face.leftEye.highlightMotion.localPosition, glint), Is.LessThan(.0001f), prefab.name);
                        camera.transform.position = new Vector3(0, 0, -10);
                    } finally { Object.DestroyImmediate(face.gameObject); }
                }
            } finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target); }
        }

        [UnityTest]
        public IEnumerator OffscreenHurtAndBlinkTimersAdvanceAndStaleViewsNeverCull()
        {
            var cameraObject = new GameObject("Offscreen expression camera", typeof(Camera));
            var face = Object.Instantiate(DoodleCharacterCatalog.Current.entries.First().prefab.face);
            try {
                var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
                var view = new CharacterFaceView(); face.View = view;
                face.transform.position = new Vector3(100, 100, 0);
                face.blinkInterval = new Vector2(100, 100); face.hurtDuration = .05f;
                view.Update(camera); Assert.That(view.MaySee(face.leftEye.pupilMask), Is.False);
                face.ShowHit();
                for (float end = Time.time + .09f; Time.time < end;) { view.Update(camera); yield return null; }
                Assert.That(face.IsHurt, Is.False); Assert.That(face.leftEye.pupil.enabled, Is.True);
                yield return null;
                Assert.That(view.MaySee(face.leftEye.pupilMask), Is.True, "An unpublished camera frame cannot suppress updates");
                face.blinkDuration = .06f; face.Blink();
                for (float end = Time.time + .1f; Time.time < end;) { view.Update(camera); yield return null; }
                Assert.That(face.IsBlinking, Is.False);
                Assert.That(face.leftEye.lidMotion.localScale, Is.EqualTo(Vector3.one));
                face.View = null; face.RefreshHighlights();
                Assert.That(face.leftEye.highlight.enabled, Is.True, "Manual/portrait faces stay current");
                face.View = view; face.ShowHit(); face.Paused = true; face.ResetExpression();
                Assert.That(face.leftEye.highlight.enabled, Is.True, "Paused appearance changes refresh synchronously");
                face.Paused = false; face.ShowHit(); face.enabled = false;
                Assert.That(face.leftEye.highlight.enabled, Is.True, "Disabling only the face resets its visible expression");
                face.enabled = true; face.Paused = true; Tick.Invoke(face, null);
                Assert.That(face.leftEye.highlight.enabled, Is.True, "A newly paused rig completes pending geometry");
            } finally { Object.DestroyImmediate(face.gameObject); Object.DestroyImmediate(cameraObject); }
        }
    }
}
