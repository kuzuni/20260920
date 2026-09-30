using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public sealed class DoodlePupilClipTests
    {
        [UnityTest]
        public IEnumerator DirectClippingMatchesNativeMaskPixelsAcrossGazeBlinkAndMirroring()
        {
            var cameraObject = new GameObject("Pupil clipping comparison camera");
            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            try {
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.targetTexture = rt;
                var rows = new System.Collections.Generic.List<string> { "face,pose,differentPixels,meanDifference" };
                var faces = DoodleCharacterCatalog.Current.entries.Select(e => e.prefab.face)
                    .Concat(Resources.Load<DoodleSkillFaceCatalog>("DoodleIdle/SkillFaceCatalog").entries.Select(e => e.facePrefab)).Distinct();
                foreach (var prefab in faces) {
                    var root = new GameObject("Pupil pixel comparison");
                    try {
                        var source = prefab.leftEye;
                        var eyeRoot = Object.Instantiate(source.pupilMotion.parent.gameObject, root.transform).transform;
                        eyeRoot.localPosition = Vector3.zero; eyeRoot.localRotation = Quaternion.identity; eyeRoot.localScale = Vector3.one;
                        var eye = new CharacterFace.Eye {
                            white = eyeRoot.Find("EyelidMotion/White").GetComponent<SpriteRenderer>(),
                            pupil = eyeRoot.Find("PupilMotion/Pupil").GetComponent<SpriteRenderer>(),
                            pupilMotion = eyeRoot.Find("PupilMotion"),
                            pupilMask = eyeRoot.GetComponentInChildren<SpriteMask>(),
                            normal = source.normal, hurt = source.hurt,
                            lidMotion = eyeRoot.Find("EyelidMotion")
                        };
                        foreach (var renderer in eyeRoot.GetComponentsInChildren<SpriteRenderer>())
                            if (renderer != eye.white && renderer != eye.pupil) renderer.enabled = false;
                        // A nearby mask must never enlarge this pupil's clipping region.
                        var neighbor = Object.Instantiate(eyeRoot.gameObject, root.transform);
                        float width = eye.white.sprite.bounds.size.x * eye.white.transform.localScale.x;
                        neighbor.transform.localPosition = new Vector3(width * .65f, 0, 0);
                        foreach (var renderer in neighbor.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled = false;
                        foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
                        var face = root.AddComponent<CharacterFace>(); face.enabled = false;
                        face.leftEye = eye; face.blinking = false;
                        camera.orthographicSize = width * 1.1f;
                        for (int pose = 0; pose < 5; pose++) {
                            root.transform.rotation = Quaternion.Euler(0, 0, pose * 17);
                            root.transform.localScale = pose >= 2 ? new Vector3(-1.2f, .8f, 1) : Vector3.one;
                            eye.pupilMotion.localPosition = new Vector3(width * (pose % 2 == 0 ? .34f : -.34f), pose == 1 ? width * .25f : 0, 0);
                            eye.lidMotion.localScale = new Vector3(1, pose == 3 ? .16f : 1, 1);
                            eye.pupil.color = pose == 4 ? new Color(.4f, .8f, 1, .35f) : Color.white;
                            eye.pupil.sharedMaterial = source.pupil.sharedMaterial; eye.pupil.SetPropertyBlock(null);
                            eye.pupil.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; eye.pupilMask.enabled = true;
                            yield return null; yield return null;
                            var native = Capture(camera, rt);
                            face.RefreshHighlights();
                            Assert.That(eye.pupilMask.enabled, Is.False);
                            Assert.That(eye.pupil.sharedMaterial.shader.name, Is.EqualTo("DoodleIdle/Pupil Clip"));
                            yield return null; yield return null;
                            var direct = Capture(camera, rt);
                            int changed = 0; float total = 0;
                            for (int i = 0; i < native.Length; i++) {
                                float difference = Mathf.Max(Mathf.Abs(native[i].r - direct[i].r), Mathf.Abs(native[i].g - direct[i].g), Mathf.Abs(native[i].b - direct[i].b), Mathf.Abs(native[i].a - direct[i].a));
                                total += difference; if (difference > .06f) changed++;
                            }
                            rows.Add(prefab.name + "," + pose + "," + changed + "," + total / native.Length);
                            if (changed >= 20) {
                                System.IO.Directory.CreateDirectory("artifacts/pupil-direct");
                                foreach (var pair in new[] { ("native", native), ("direct", direct) }) {
                                    var image = new Texture2D(256, 256, TextureFormat.RGBA32, false); image.SetPixels(pair.Item2); image.Apply();
                                    System.IO.File.WriteAllBytes("artifacts/pupil-direct/" + prefab.name + "-" + pose + "-" + pair.Item1 + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
                                }
                            }
                            Assert.That(changed, Is.LessThan(20), prefab.name + " pose " + pose + " different pixels; mean " + total / native.Length);
                        }
                    } finally { Object.DestroyImmediate(root); }
                }
                System.IO.Directory.CreateDirectory("artifacts/pupil-direct");
                System.IO.File.WriteAllLines("artifacts/pupil-direct/pixel-comparison.csv", rows);
            } finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(rt); }
        }

        static Color[] Capture(Camera camera, RenderTexture rt)
        {
            for (int i = 0; i < 2; i++) RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            var pixels = texture.GetPixels(); Object.DestroyImmediate(texture); RenderTexture.active = previous;
            return pixels;
        }
    }
}
