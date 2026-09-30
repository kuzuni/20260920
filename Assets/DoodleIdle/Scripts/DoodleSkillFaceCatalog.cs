using System;
using System.Collections.Generic;
using DoodleIdle.CharacterRigs;
using UnityEngine;

namespace DoodleIdle
{
    [CreateAssetMenu(menuName="Doodle Idle/Skill Face Catalog")]
    public sealed class DoodleSkillFaceCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string sourceName, sourceTexture;
            public Sprite body;
            public CharacterFace facePrefab;
            public Vector2 frameOffset;
        }
        public Entry[] entries;
        static DoodleSkillFaceCatalog current;
        Dictionary<string, Entry> lookup;
        readonly Dictionary<Sprite,Entry> sprites = new Dictionary<Sprite,Entry>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { current = null; }
        public static Entry Find(Sprite source)
        {
            if (!source) return null;
            if (!current) current=Resources.Load<DoodleSkillFaceCatalog>("DoodleIdle/SkillFaceCatalog");
            if (!current) return null;
            if(current.sprites.TryGetValue(source,out var known))return known;
            if (current.lookup == null)
            {
                current.lookup = new Dictionary<string, Entry>();
                foreach(var entry in current.entries)
                {
                    current.lookup[entry.sourceTexture+"/"+entry.sourceName]=entry;
                    if(entry.body)current.lookup[entry.body.texture.name+"/"+entry.body.name]=entry;
                }
            }
            current.lookup.TryGetValue(source.texture.name+"/"+source.name,out var result);
            current.sprites[source]=result;return result;
        }
    }
}
