using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public static class DoodleCollectionArt
    {
        static readonly string[] grades = { "Normal", "Advanced", "Rare", "Epic", "Legendary", "Mythic" };
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        [Serializable] sealed class AnimationLayout { public FrameRegion[] frames; }
        [Serializable] sealed class FrameRegion { public float x, y, width, height, bodyOffsetY; }
        static readonly Dictionary<string, AnimationLayout> layouts = new Dictionary<string, AnimationLayout>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { cache.Clear(); layouts.Clear(); }
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (cache.TryGetValue(key, out var value) && value && value.texture) return value;
            cache.Remove(key);
            if (key.StartsWith("RelicAttack_", StringComparison.Ordinal) && int.TryParse(key.Substring(12), out int relic))
                value = Cell("RelicAttackArtifacts", relic, 3, 1);
            else if (key.StartsWith("SkillThumb_", StringComparison.Ordinal) && int.TryParse(key.Substring(11), out int skill))
                value = skill >= 24 ? Cell("SkillThumbsExpansion", skill - 24, 3, 2) : Cell("SkillThumbs" + grades[skill / 4], skill % 4, 2, 2);
            else if (key.StartsWith("CompanionMon_", StringComparison.Ordinal) && int.TryParse(key.Substring(13), out int companion))
                return CompanionFrame(companion, 0);
            else if (key.StartsWith("CompanionImpact_", StringComparison.Ordinal) && int.TryParse(key.Substring(16), out int impact))
                value = Cell("CompanionImpacts", impact, 4, 4);
            else if (key.StartsWith("CompanionShot_", StringComparison.Ordinal) && int.TryParse(key.Substring(14), out int shot))
            {
                int special = shot == 2 ? 0 : shot == 3 ? 1 : shot == 10 ? 2 : shot == 18 ? 3 : shot == 19 ? 4 : -1;
                value = shot == 12 ? Cell("CompanionHoney", 0, 1, 1) : special >= 0 ? Cell("CompanionSpecialAttacks", special, 3, 2) : Cell("CompanionAttacks" + (char)('A' + shot / 8), shot % 8, 4, 2);
            }
            if (value) { value.name = key; cache[key] = value; }
            return value;
        }
        public static Sprite CompanionFrame(int index, int frame)
        {
            string key = "CompanionMon_" + index + "_" + frame;
            if (cache.TryGetValue(key, out var value) && value && value.texture) return value;
            cache.Remove(key);
            string resource = index == 12 ? "DoodleIdle/CompanionHoneyBee" : "DoodleIdle/CompanionMons" + grades[index / 4];
            var texture = Resources.Load<Texture2D>(resource);
            if (!texture) throw new InvalidOperationException("Missing companion animation: " + index);
            if (!layouts.TryGetValue(resource, out var layout)) {
                layout = JsonUtility.FromJson<AnimationLayout>(Resources.Load<TextAsset>(resource + "Layout").text);
                layouts[resource] = layout;
            }
            // Whole-character bounds exclude neighboring limbs crossing nominal grid boundaries.
            var region = layout.frames[index == 12 ? frame : frame * 4 + index % 4];
            // Keep the face/body at the same height while limbs and wings change pose.
            // Atlas rows have different body baselines even though their crop sizes match.
            value = Sprite.Create(texture, new Rect(region.x, region.y, region.width, region.height),
                new Vector2(.5f, .5f + region.bodyOffsetY / region.height), Mathf.Max(region.width, region.height));
            value.name = key; cache[key] = value; return value;
        }
        public static int CompanionIndex(string key) => int.Parse(key.Substring(13));
        static readonly int[] impactCells = { -1, -1, -1, -1, 0, -1, -1, -1, 1, 2, 3, -1, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        static readonly string[] impactNames = { "잎 파열", "물보라", "전기 스파크", "낙뢰 섬광", "꿀 튐", "금속 스파크", "서리 파편", "마법 파열", "가시 파열", "달빛 섬광", "화염 폭발", "명중 섬광", "성운 소용돌이", "태양 불꽃", "얼음 파열", "붉은 화염" };
        public static Sprite CompanionImpact(int index) => impactCells[index] < 0 ? null : Get("CompanionImpact_" + impactCells[index]);
        public static string CompanionImpactName(int index) => impactCells[index] < 0 ? "폭발 없음 유지" : impactNames[impactCells[index]];
        static Sprite Cell(string resource, int cell, int columns, int rows)
        {
            var texture = Resources.Load<Texture2D>("DoodleIdle/" + resource);
            if (!texture) throw new InvalidOperationException("Missing collection artwork: " + resource);
            float w = texture.width / (float)columns, h = texture.height / (float)rows;
            var region = Trim(texture, new Rect(cell % columns * w, (rows - 1 - cell / columns) * h, w, h));
            return Sprite.Create(texture, region, Vector2.one * .5f, Mathf.Max(region.width, region.height));
        }
        static Rect Trim(Texture2D texture, Rect cell)
        {
            var pixels = texture.GetPixels32(); int x0 = (int)cell.xMax, y0 = (int)cell.yMax, x1 = -1, y1 = -1;
            for (int y = (int)cell.yMin; y < (int)cell.yMax; y++) for (int x = (int)cell.xMin; x < (int)cell.xMax; x++)
                if (pixels[y * texture.width + x].a > 32) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
            if (x1 < x0) throw new InvalidOperationException("Empty collection artwork cell: " + texture.name);
            return new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }
    }
}
