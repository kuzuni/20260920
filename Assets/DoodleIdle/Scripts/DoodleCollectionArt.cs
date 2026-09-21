using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public static class DoodleCollectionArt
    {
        static readonly string[] grades = { "Normal", "Advanced", "Rare", "Epic", "Legendary", "Mythic" };
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (cache.TryGetValue(key, out var value)) return value;
            if (key.StartsWith("SkillThumb_", StringComparison.Ordinal) && int.TryParse(key.Substring(11), out int skill))
                value = Cell("SkillThumbs" + grades[skill / 4], skill % 4, 2, 2);
            else if (key.StartsWith("CompanionMon_", StringComparison.Ordinal) && int.TryParse(key.Substring(13), out int companion))
                return CompanionFrame(companion, 0);
            else if (key.StartsWith("CompanionShot_", StringComparison.Ordinal) && int.TryParse(key.Substring(14), out int shot))
            {
                int special = shot == 2 ? 0 : shot == 3 ? 1 : shot == 10 ? 2 : shot == 18 ? 3 : shot == 19 ? 4 : -1;
                value = special >= 0 ? Cell("CompanionSpecialAttacks", special, 3, 2) : Cell("CompanionAttacks" + (char)('A' + shot / 8), shot % 8, 4, 2);
            }
            if (value) { value.name = key; cache[key] = value; }
            return value;
        }
        public static Sprite CompanionFrame(int index, int frame)
        {
            string key = "CompanionMon_" + index + "_" + frame;
            if (cache.TryGetValue(key, out var value)) return value;
            var texture = Resources.Load<Texture2D>("DoodleIdle/CompanionMons" + grades[index / 4]);
            if (!texture) throw new InvalidOperationException("Missing companion animation: " + index);
            float w = texture.width / 4f, h = texture.height / 2f; int col = index % 4;
            var top = Trim(texture, new Rect(col * w, h, w, h)); top.position -= new Vector2(col * w, h);
            var bottom = Trim(texture, new Rect(col * w, 0, w, h)); bottom.position -= new Vector2(col * w, 0);
            var union = Rect.MinMaxRect(Mathf.Min(top.xMin, bottom.xMin), Mathf.Min(top.yMin, bottom.yMin),
                Mathf.Max(top.xMax, bottom.xMax), Mathf.Max(top.yMax, bottom.yMax));
            var region = union; region.position += new Vector2(col * w, frame == 0 ? h : 0);
            value = Sprite.Create(texture, region, Vector2.one * .5f, Mathf.Max(union.width, union.height));
            value.name = key; cache[key] = value; return value;
        }
        public static int CompanionIndex(string key) => int.Parse(key.Substring(13));
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
