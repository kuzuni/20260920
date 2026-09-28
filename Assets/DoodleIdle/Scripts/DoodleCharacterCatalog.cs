using System;
using System.Linq;
using System.Collections.Generic;
using DoodleIdle.CharacterRigs;
using UnityEngine;

namespace DoodleIdle
{
    public sealed class DoodleCharacterCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string id, group;
            public int theme;
            public CharacterRig prefab;
            public CharacterAppearance appearance;
            public Sprite[] portraits;
            public Vector3 center;
            public float extent;
        }
        public Entry[] entries;
        public string[] companionIds;
        static DoodleCharacterCatalog current;
        static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { current = null; portraits.Clear(); }
        public static DoodleCharacterCatalog Current => current ? current : current = Resources.Load<DoodleCharacterCatalog>("DoodleIdle/CharacterCatalog")
            ?? throw new InvalidOperationException("Character catalog missing. Run Doodle Idle/Character Rigs/Build Runtime Catalog.");
        public Entry Player(int costume) => entries.Where(e => e.group == "Player").OrderBy(e => e.id, StringComparer.Ordinal).ElementAt(Mathf.Clamp(costume + 1, 0, 40));
        public Entry Companion(int index)
        {
            // Retired icon indices remain loadable for old saves and effect previews.
            string id = index >= 0 && index < companionIds.Length ? companionIds[index] : null;
            return entries.First(e => e.group == "Companions" && (id == null ? e.id.StartsWith("01_") : e.id == id));
        }
        public Entry Enemy(int theme, int ordinal) {
            var options = entries.Where(e => e.group == "Enemies" && e.theme == theme).ToArray();
            return options[Math.Abs(ordinal) % options.Length];
        }
        public static Sprite Portrait(Entry entry, int pose = 0) {
            string key = entry.group + "/" + entry.id + "_" + (pose & 1);
            if (portraits.TryGetValue(key, out var cached) && cached && cached.texture) return cached;
            var source = entry.portraits[pose & 1];
            var value = Sprite.Create(source.texture, source.rect, Vector2.one * .5f, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            value.name = "PrefabPortrait " + key; portraits[key] = value; return value;
        }
        public static Sprite PlayerPortrait(int costume = -1, int pose = 0) => Portrait(Current.Player(costume), pose);
        public static int Costume(string key) => key != null && key.StartsWith("SkinAppearance_", StringComparison.Ordinal) ? int.Parse(key.Split('_')[1]) : -1;
        public static Sprite LegacyPortrait(string key)
        {
            switch (key) {
                case "Player": case "PlayerWalkA": return PlayerPortrait();
                case "PlayerWalkB": return PlayerPortrait(-1, 1);
                case "Companion": return Portrait(Current.Companion(0));
                case "StormCloud": case "StormCloudB": return Portrait(Current.entries.First(e => e.id == "e09_float_storm_cloud"), key.EndsWith("B") ? 1 : 0);
                case "Mushroom": case "MushroomA": case "MushroomB": return Portrait(Current.Enemy(0, 2), key.EndsWith("B") ? 1 : 0);
                case "Bat": case "BatA": case "BatB": return Portrait(Current.Enemy(6, 2), key.EndsWith("B") ? 1 : 0);
                case "Devil": case "DevilA": case "DevilB": return Portrait(Current.Enemy(4, 1), key.EndsWith("B") ? 1 : 0);
                default: return null;
            }
        }
    }
}
