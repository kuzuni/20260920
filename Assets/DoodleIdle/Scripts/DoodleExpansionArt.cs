using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleIdle
{
    public static class DoodleExpansionArt
    {
        static readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { frames.Clear(); }
        public static Sprite Get(string key, int frame = 0)
        {
            int count;
            switch (key)
            {
                case "SkillTornado": case "SkillDoubleClaw": case "SkillLightning": count = 2; break;
                case "SkillGolem": count = 4; break;
                case "SkillDumbbell": case "SkillMeteorRock": case "SkillMeteor": case "SkillMeteorCrater":
                case "NavPottery": case "NavColosseum": count = 1; break;
                default: return null;
            }
            bool valid = frames.TryGetValue(key, out var sprites) && sprites.Length == count;
            if (valid) foreach (var sprite in sprites) if (!sprite || !sprite.texture) { valid = false; break; }
            if (!valid)
            {
                var texture = Resources.Load<Texture2D>("DoodleIdle/" + (key == "SkillGolem" ? "SkillGolemSlam" : key));
                if (!texture) throw new InvalidOperationException("Missing expansion art: " + key);
                int width = texture.width / count, height = texture.height;
                var pixels = texture.GetPixels32();
                int minX = width, minY = height, maxX = -1, maxY = -1;
                // Shared local bounds and PPU preserve body scale/pivot through each animation pose.
                for (int cell = 0; cell < count; cell++)
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                        if (pixels[y * texture.width + cell * width + x].a > 32)
                        { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                if (maxX < minX) throw new InvalidOperationException("Empty expansion art: " + key);
                sprites = new Sprite[count];
                for (int i = 0; i < count; i++)
                {
                    var rect = new Rect(i * width + minX, minY, maxX - minX + 1, maxY - minY + 1);
                    sprites[i] = Sprite.Create(texture, rect, Vector2.one * .5f, Mathf.Max(rect.width, rect.height));
                    sprites[i].name = key + "_" + i;
                }
                frames[key] = sprites;
            }
            return sprites[frame % count];
        }
    }
}
