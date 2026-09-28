using UnityEngine;
namespace DoodleIdle
{
    // UI and combat share the same authored prefab appearances.
    public static class DoodlePlayerCostumeArt
    {
        public static Sprite BodyFrame(int pose) => DoodleCharacterCatalog.PlayerPortrait(-1, pose);
        public static Sprite Frame(int costume, int pose) => DoodleCharacterCatalog.PlayerPortrait(costume, pose);
    }
}
