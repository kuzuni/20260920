using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    // Explicit atlas gutters keep each hand-drawn silhouette intact.
    public static class DoodleAscensionArt
    {
        static readonly int[] xs = { 0, 222, 432, 639, 843, 1046, 1254 };
        static readonly int[] ys = { 0, 290, 497, 690, 890, 1052, 1254 };
        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        [Serializable] sealed class CompanionLayout { public CompanionRegion[] frames; }
        [Serializable] sealed class CompanionRegion { public float x, y, width, height, pixelsPerUnit; }
        static CompanionLayout companionLayout;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { cache.Clear(); companionLayout = null; }
        public static Sprite Get(string key)
        {
            if (key == "SkillFireGolem") return FireGolem(0);
            if (key != null && key.StartsWith("AscensionShot_", StringComparison.Ordinal) && int.TryParse(key.Substring(14), out int shot)) return Projectile(shot);
            return key != null && key.StartsWith("Ascension_", StringComparison.Ordinal) &&
                int.TryParse(key.Substring(10), out int cell) ? Cell(cell) : null;
        }
        public static Sprite CompanionFrame(int index, int frame)
        {
            int cell = index + (frame % 2) * 10, key = 200 + cell;
            if (cache.TryGetValue(key, out var sprite) && sprite && sprite.texture) return sprite;
            if (companionLayout == null) companionLayout = JsonUtility.FromJson<CompanionLayout>(Resources.Load<TextAsset>("DoodleIdle/AscensionCompanionsLayout").text);
            var region = companionLayout.frames[cell];
            var texture = Resources.Load<Texture2D>("DoodleIdle/AscensionCompanions");
            sprite = Sprite.Create(texture, new Rect(region.x, region.y, region.width, region.height),
                new Vector2(.5f, .08f), region.pixelsPerUnit);
            sprite.name = "CompanionMon_" + (index + 24) + "_" + frame; cache[key] = sprite; return sprite;
        }
        public static Sprite Projectile(int index)
        {
            if (index < 0 || index >= 12) throw new ArgumentOutOfRangeException(nameof(index));
            int key = 300 + index;
            if (cache.TryGetValue(key, out var sprite) && sprite && sprite.texture) return sprite;
            var texture = Resources.Load<Texture2D>("DoodleIdle/AscensionProjectiles");
            int w = texture.width / 4, h = texture.height / 3;
            int left = index % 4 * w, bottom = (2 - index / 4) * h;
            int x0 = left + w, x1 = -1, y0 = bottom + h, y1 = -1;
            var pixels = texture.GetPixels32();
            for (int y = bottom; y < bottom + h; y++) for (int x = left; x < left + w; x++)
                if (pixels[y * texture.width + x].a > 32) {
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
            if (x1 < x0) throw new InvalidOperationException("Empty companion projectile: " + index);
            var rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            sprite = Sprite.Create(texture, rect, Vector2.one * .5f, Mathf.Max(rect.width, rect.height));
            sprite.name = "AscensionShot_" + index; cache[key] = sprite; return sprite;
        }
        public static Sprite FireGolem(int frame)
        {
            int key = 100 + Mathf.Clamp(frame, 0, 3);
            if (cache.TryGetValue(key, out var sprite) && sprite && sprite.texture) return sprite;
            var texture = Resources.Load<Texture2D>("DoodleIdle/SkillFireGolem");
            if (!texture) throw new InvalidOperationException("Missing fire golem animation.");
            float width = texture.width / 4f;
            // Shared cell size and pivot keep the body stable through the four poses.
            sprite = Sprite.Create(texture, new Rect((key - 100) * width, 0, width, texture.height),
                Vector2.one * .5f, width);
            sprite.name = "SkillFireGolem_" + (key - 100); cache[key] = sprite; return sprite;
        }
        public static Sprite Cell(int cell)
        {
            if (cell < 0 || cell >= 36) throw new ArgumentOutOfRangeException(nameof(cell));
            if (cache.TryGetValue(cell, out var sprite) && sprite && sprite.texture) return sprite;
            var texture = Resources.Load<Texture2D>("DoodleIdle/AscensionAtlas");
            if (!texture) throw new InvalidOperationException("Missing ascension atlas.");
            int col = cell % 6, row = cell / 6;
            int left = xs[col] * texture.width / 1254, right = xs[col + 1] * texture.width / 1254;
            int bottom = (1254 - ys[row + 1]) * texture.height / 1254, top = (1254 - ys[row]) * texture.height / 1254;
            int x0 = right, x1 = -1, y0 = top, y1 = -1;
            var pixels = texture.GetPixels32();
            for (int y = bottom; y < top; y++) for (int x = left; x < right; x++)
                if (pixels[y * texture.width + x].a > 32) {
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
            if (x1 < x0) throw new InvalidOperationException("Empty ascension sprite: " + cell);
            x0 = Mathf.Max(left, x0 - 3); y0 = Mathf.Max(bottom, y0 - 3);
            x1 = Mathf.Min(right - 1, x1 + 3); y1 = Mathf.Min(top - 1, y1 + 3);
            var rect = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            sprite = Sprite.Create(texture, rect, Vector2.one * .5f, Mathf.Max(rect.width, rect.height));
            sprite.name = "Ascension_" + cell; cache[cell] = sprite; return sprite;
        }
    }
}
