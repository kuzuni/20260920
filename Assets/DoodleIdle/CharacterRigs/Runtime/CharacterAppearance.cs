using System;
using UnityEngine;

namespace DoodleIdle.CharacterRigs
{
    [CreateAssetMenu(menuName = "Doodle Idle/Character Appearance")]
    public sealed class CharacterAppearance : ScriptableObject
    {
        public string rigType;
        public string characterId;
        public Part[] parts;
        public Sprite weapon;
        public float weaponScale = 1f;

        [Serializable]
        public struct Part
        {
            public string name;
            public Sprite sprite;
            public Vector3 rendererPosition;
            public string[] boneNames;
            public int sortingOrder;
        }
    }
}
