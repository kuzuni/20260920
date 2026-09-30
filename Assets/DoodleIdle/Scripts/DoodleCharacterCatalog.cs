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
            public Sprite appearancePortrait;
            public Vector3 center;
            public float extent;
            [NonSerialized] public Sprite[] cachedPortraits;
        }
        public Entry[] entries;
        public string[] companionIds;
        [NonSerialized] Entry[] playerEntries;
        Dictionary<string,Entry> entriesById;
        Dictionary<int,Entry[]> enemiesByTheme;
        void OnEnable() { playerEntries = null; entriesById = null; enemiesByTheme = null; }
        void EnsureLookup()
        {
            if(playerEntries!=null)return;
            playerEntries=entries.Where(e=>e.group=="Player").OrderBy(e=>e.id,StringComparer.Ordinal).ToArray();
            entriesById=entries.ToDictionary(e=>e.id);
            enemiesByTheme=entries.Where(e=>e.group=="Enemies").GroupBy(e=>e.theme).ToDictionary(g=>g.Key,g=>g.ToArray());
        }
        static DoodleCharacterCatalog current;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { current = null; }
        public static DoodleCharacterCatalog Current => current ? current : current = Resources.Load<DoodleCharacterCatalog>("DoodleIdle/CharacterCatalog")
            ?? throw new InvalidOperationException("Character catalog missing. Run Doodle Idle/Character Rigs/Build Runtime Catalog.");
        public Entry Player(int costume) { EnsureLookup(); return playerEntries[Mathf.Clamp(costume+1,0,playerEntries.Length-1)]; }
        public Entry Companion(int index)
        {
            // Retired icon indices remain loadable for old saves and effect previews.
            string id = index >= 0 && index < companionIds.Length ? companionIds[index] : null;
            EnsureLookup();return id!=null && entriesById.TryGetValue(id,out var entry)?entry:entries.First(e=>e.group=="Companions"&&e.id.StartsWith("01_"));
        }
        public Entry Enemy(int theme, int ordinal) {
            EnsureLookup();var options = enemiesByTheme[theme];
            return options[Math.Abs(ordinal) % options.Length];
        }
        public static Sprite Portrait(Entry entry, int pose = 0) {
            int frame=pose&1;
            if(entry.cachedPortraits==null)entry.cachedPortraits=new Sprite[2];
            var cached=entry.cachedPortraits[frame];
            if(cached && cached.texture)return cached;
            var source = entry.portraits[frame];
            var value = Sprite.Create(source.texture, source.rect, Vector2.one * .5f, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            value.name = "PrefabPortrait " + entry.group + "/" + entry.id + "_" + frame; entry.cachedPortraits[frame] = value; return value;
        }
        public static Sprite PlayerPortrait(int costume = -1, int pose = 0) => Portrait(Current.Player(costume), pose);
        public static Sprite PlayerAppearancePortrait(int costume) => Current.Player(costume).appearancePortrait;
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
