using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    // Costumes contain clothing only. Both poses are composited over the ORIGINAL
    // player frames in body coordinates, never normalized by the hat/cape bounds.
    public static class DoodlePlayerCostumeArt
    {
        [Serializable] sealed class Rig { public Pose[] poses; }
        [Serializable] sealed class Pose
        {
            public string sheet;
            public int x, y, width, height;
            public Vector2 leftEye, rightEye;
        }
        sealed class Pixels
        {
            public int width, height;
            public Color32[] values;
            public Pixels(Texture2D texture) { width = texture.width; height = texture.height; values = texture.GetPixels32(); }
            public Color Sample(float x, float y, Rect bounds)
            {
                if (x < bounds.xMin || y < bounds.yMin || x >= bounds.xMax || y >= bounds.yMax) return Color.clear;
                x -= .5f; y -= .5f;
                int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                Color At(int px, int py) => px < bounds.xMin || px >= bounds.xMax || py < bounds.yMin || py >= bounds.yMax ? Color.clear : (Color)values[py * width + px];
                return Color.Lerp(Color.Lerp(At(ix, iy), At(ix + 1, iy), x - ix), Color.Lerp(At(ix, iy + 1), At(ix + 1, iy + 1), x - ix), y - iy);
            }
        }
        static Rig rig;
        static readonly Sprite[] bodies = new Sprite[2];
        static readonly Dictionary<int, Sprite> frames = new Dictionary<int, Sprite>();
        static readonly Dictionary<Texture2D, Pixels> sourcePixels = new Dictionary<Texture2D, Pixels>();
        static Pixels ReadPixels(Texture2D texture)
        {
            if (!sourcePixels.TryGetValue(texture, out var pixels)) sourcePixels[texture] = pixels = new Pixels(texture);
            return pixels;
        }
        // Source-image pixel anchors, measured on the unchanged original cat frames.
        static readonly Vector2[] leftEyes = { new Vector2(199, 1254 - 227.5f), new Vector2(555, 1254 - 623) };
        static readonly Vector2[] rightEyes = { new Vector2(280.5f, 1254 - 253), new Vector2(765.5f, 1254 - 684) };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            foreach (var sprite in frames.Values) if (sprite) { UnityEngine.Object.Destroy(sprite.texture); UnityEngine.Object.Destroy(sprite); }
            frames.Clear();
            sourcePixels.Clear();
            for (int i = 0; i < bodies.Length; i++) { if (bodies[i]) UnityEngine.Object.Destroy(bodies[i]); bodies[i] = null; }
            rig = null;
        }

        public static Sprite BodyFrame(int pose)
        {
            pose = pose & 1;
            if (bodies[pose]) return bodies[pose];
            var texture = Resources.Load<Texture2D>("DoodleIdle/" + (pose == 0 ? "Characters" : "PlayerWalkB"));
            var pixels = texture.GetPixels32();
            int xMax = pose == 0 ? texture.width / 3 : texture.width;
            int yMin = pose == 0 ? texture.height / 3 * 2 : 0;
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (int y = yMin; y < texture.height; y++) for (int x = 0; x < xMax; x++)
                if (pixels[y * texture.width + x].a > 32) { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            Rect rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            var sprite = Sprite.Create(texture, rect, Vector2.one * .5f, Mathf.Max(rect.width, rect.height));
            sprite.name = pose == 0 ? "PlayerWalkA" : "PlayerWalkB";
            return bodies[pose] = sprite;
        }

        public static Sprite Frame(int costume, int pose)
        {
            int key = costume * 2 + pose;
            if (frames.TryGetValue(key, out var cached) && cached) return cached;
            if (rig == null) rig = JsonUtility.FromJson<Rig>(Resources.Load<TextAsset>("DoodleIdle/UI/PlayerCostumeRig").text);
            var placement = rig.poses[key];
            var body = BodyFrame(pose);
            var clothing = Resources.Load<Texture2D>("DoodleIdle/UI/" + placement.sheet);
            var bodyPixels = ReadPixels(body.texture);
            var coatPixels = ReadPixels(clothing);
            Rect coatRect = new Rect(placement.x, placement.y, placement.width, placement.height);
            Vector2 eye = (leftEyes[pose] + rightEyes[pose]) * .5f;
            Vector2 origin = (eye - body.rect.center) / body.pixelsPerUnit;
            Vector2 targetSpan = (rightEyes[pose] - leftEyes[pose]) / body.pixelsPerUnit;
            Vector2 sourceSpan = placement.rightEye - placement.leftEye;
            Vector2 sourceOrigin = (placement.leftEye + placement.rightEye) * .5f;
            float scale = targetSpan.magnitude / sourceSpan.magnitude;
            float angle = Vector2.SignedAngle(sourceSpan, targetSpan) * Mathf.Deg2Rad;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            Vector2 Forward(Vector2 p) { p = (p - sourceOrigin) * scale; return origin + new Vector2(p.x * c - p.y * s, p.x * s + p.y * c); }
            Vector2 Inverse(Vector2 p) { p = (p - origin) / scale; return sourceOrigin + new Vector2(p.x * c + p.y * s, -p.x * s + p.y * c); }
            Vector2 min = body.bounds.min, max = body.bounds.max;
            foreach (var point in new[] { coatRect.min, coatRect.max, new Vector2(coatRect.xMin, coatRect.yMax), new Vector2(coatRect.xMax, coatRect.yMin) }) {
                var p = Forward(point); min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            // Fixed pixels per BODY unit, including empty padding. Large hats grow outwards.
            const float ppu = 384;
            min = new Vector2(Mathf.Floor(min.x * ppu) - 3, Mathf.Floor(min.y * ppu) - 3) / ppu;
            max = new Vector2(Mathf.Ceil(max.x * ppu) + 3, Mathf.Ceil(max.y * ppu) + 3) / ppu;
            int width = Mathf.RoundToInt((max.x - min.x) * ppu), height = Mathf.RoundToInt((max.y - min.y) * ppu);
            var output = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
                Vector2 p = min + new Vector2(x + .5f, y + .5f) / ppu;
                Vector2 bp = p * body.pixelsPerUnit + body.rect.center;
                Color b = bodyPixels.Sample(bp.x, bp.y, body.rect);
                Vector2 cp = Inverse(p); Color coat = coatPixels.Sample(cp.x, cp.y, coatRect);
                float alpha = coat.a + b.a * (1 - coat.a);
                Color result = alpha > 0 ? (coat * coat.a + b * (b.a * (1 - coat.a))) / alpha : Color.clear;
                result.a = alpha; output[y * width + x] = result;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Costume " + costume + " pose " + pose, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(output); texture.Apply(false, !Application.isEditor);
            var frame = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(-min.x / (max.x - min.x), -min.y / (max.y - min.y)), ppu, 0, SpriteMeshType.FullRect);
            frame.name = "Skin SkinAppearance_" + costume + "_" + pose;
            frames[key] = frame; return frame;
        }
    }
}
