using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    // Explicit atlas gutters keep each hand-drawn silhouette intact.
    public static class DoodleAscensionArt
    {
        // Top-left pixel bounds. The artist's rows are intentionally not a uniform grid.
        static readonly Rect[] regions = {
            new Rect(22,140,199,94), new Rect(232,80,201,196), new Rect(438,94,191,176),
            new Rect(650,89,167,186), new Rect(865,94,161,171), new Rect(1060,95,170,170),
            new Rect(30,305,157,158), new Rect(225,333,189,106), new Rect(438,297,191,184),
            new Rect(656,320,178,156), new Rect(852,312,179,158), new Rect(1046,297,193,187),
            new Rect(23,521,187,160), new Rect(229,503,189,182), new Rect(430,515,181,166),
            new Rect(637,529,180,153), new Rect(853,509,177,177), new Rect(1047,518,196,169),
            new Rect(19,691,193,195), new Rect(232,721,196,159), new Rect(441,696,188,190),
            new Rect(642,700,192,177), new Rect(848,722,190,156), new Rect(1049,736,192,137),
            new Rect(20,921,185,120), new Rect(234,921,183,122), new Rect(445,919,174,126),
            new Rect(659,899,168,154), new Rect(864,900,162,156), new Rect(1076,900,160,156),
            new Rect(42,1054,164,160), new Rect(250,1047,169,165), new Rect(478,1088,109,109),
            new Rect(673,1090,105,107), new Rect(834,1089,212,109), new Rect(1082,1065,132,145)
        };
        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        [Serializable] sealed class CompanionLayout { public CompanionRegion[] frames; }
        [Serializable] sealed class CompanionRegion { public float x, y, width, height, pixelsPerUnit; public string texture; }
        static CompanionLayout companionLayout, revisionLayout, refinementLayout;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { cache.Clear(); companionLayout = revisionLayout = refinementLayout = null; }
        public static Sprite Get(string key)
        {
            if (key == "SkillFireGolem") return FireGolem(0);
            if (key != null && key.StartsWith("AscensionShot_", StringComparison.Ordinal) && int.TryParse(key.Substring(14), out int shot)) return Projectile(shot);
            return key != null && key.StartsWith("Ascension_", StringComparison.Ordinal) &&
                int.TryParse(key.Substring(10), out int cell) ? Cell(cell) : null;
        }
        public static Sprite CompanionFrame(int index, int frame)
        {
            if (index == 5 || index == 7) return Revision((index == 5 ? 0 : 2) + frame % 2);
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
            if (index == 3 || index == 9 || index == 5 || index == 7)
                return Revision(index == 3 ? 4 : index == 9 ? 5 : index == 5 ? 6 : 7);
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
        static Sprite Revision(int cell)
        {
            int key = 400 + cell;
            if (cache.TryGetValue(key, out var sprite) && sprite && sprite.texture) return sprite;
            if (revisionLayout == null) revisionLayout = JsonUtility.FromJson<CompanionLayout>(Resources.Load<TextAsset>("DoodleIdle/AscensionRevisionsLayout").text);
            var region = revisionLayout.frames[cell];
            var texture = Resources.Load<Texture2D>("DoodleIdle/AscensionRevisions");
            sprite = Sprite.Create(texture, new Rect(region.x, region.y, region.width, region.height),
                cell < 4 ? new Vector2(.5f, .08f) : Vector2.one * .5f, region.pixelsPerUnit);
            sprite.name = "AscensionRevision_" + cell; cache[key] = sprite; return sprite;
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
        public static Sprite FireTornado(int frame) => Refined(2 + frame % 2);
        static Sprite Refined(int cell)
        {
            int key = 500 + cell;
            if (cache.TryGetValue(key, out var sprite) && sprite && sprite.texture) return sprite;
            if (refinementLayout == null) refinementLayout = JsonUtility.FromJson<CompanionLayout>(Resources.Load<TextAsset>("DoodleIdle/AscensionSkillRefinementsLayout").text);
            var region = refinementLayout.frames[cell];
            var texture = Resources.Load<Texture2D>("DoodleIdle/" + region.texture);
            sprite = Sprite.Create(texture, new Rect(region.x, region.y, region.width, region.height), Vector2.one * .5f, region.pixelsPerUnit);
            sprite.name = "AscensionRefined_" + cell; cache[key] = sprite; return sprite;
        }
        public static Sprite Cell(int cell)
        {
            if (cell < 0 || cell >= 36) throw new ArgumentOutOfRangeException(nameof(cell));
            switch (cell) {
                case 0: return Refined(6);
                case 2: return DoodleExpansionArt.Get("SkillFoamRoller");
                case 3: return FireTornado(0);
                case 4: return DoodleExpansionArt.Get("SkillCherryShuriken");
                case 7: return DoodleExpansionArt.Get("SkillWhiteMissile");
                case 8: return DoodleCollectionArt.Get("AscensionSkillArt_10");
                case 33: return DoodleCollectionArt.Get("AscensionSkillArt_11");
                case 11: return Refined(4);
            }
            if (cache.TryGetValue(cell, out var sprite) && sprite && sprite.texture) return sprite;
            var texture = Resources.Load<Texture2D>("DoodleIdle/AscensionAtlas");
            if (!texture) throw new InvalidOperationException("Missing ascension atlas.");
            var region = regions[cell];
            int left = Mathf.RoundToInt(region.xMin * texture.width / 1254), right = Mathf.RoundToInt(region.xMax * texture.width / 1254);
            int bottom = Mathf.RoundToInt((1254 - region.yMax) * texture.height / 1254), top = Mathf.RoundToInt((1254 - region.yMin) * texture.height / 1254);
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
