using UnityEngine;
namespace DoodleIdle
{
    [CreateAssetMenu(menuName = "Doodle Idle/Portrait Settings")]
    public sealed class DoodlePortraitSettings : ScriptableObject
    {
        [Header("스탯 팝업 / 크기와 전투력 배치")]
        [Min(80)] public float statsPortraitSize = 248;
        [Min(80)] public float statsRowHeight = 248;
        public Vector2 statsGroupOffset;
        public float statsPowerGap = -28;
        [Min(100)] public float statsPowerWidth = 270;
        [Range(16,60)] public int statsPowerFontSize = 35;
        [Header("스탯 / 확대와 카메라 중심 이동 (프리팹 좌표)")]
        [Min(.1f)] public float statsZoom = 1;
        public Vector2 statsCameraOffset;
        [Header("메인 좌측 상단 / 얼굴 중심 초상화")]
        [Min(24)] public float profileSize = 72;
        [Min(.1f)] public float profileZoom = 1.05f;
        public Vector2 profileCameraOffset;
        [Header("PVP 팝업 / 1~3위 플레이어")]
        [Min(60)] public float pvpFirstSize = 156;
        [Min(60)] public float pvpOtherSize = 140;
        [Min(200)] public float pvpRowHeight = 292;
        [Min(.1f)] public float pvpZoom = 1.35f;
        public Vector2 pvpCameraOffset;
        [Tooltip("시상대 위 발 위치 미세 조절 (UI 좌표)")]
        public float pvpFootOffset;
        static DoodlePortraitSettings current;
        public static DoodlePortraitSettings Current => current ? current : current=Resources.Load<DoodlePortraitSettings>("DoodleIdle/UI/PortraitSettings");
    }
    [System.Serializable]
    public sealed class DoodlePlayerLook
    {
        public string appearanceIcon = "Player", weaponIcon = "Club";
        public Color appearanceTint = Color.white, weaponTint = Color.white;
        public static DoodlePlayerLook From(DoodleUi ui) => new DoodlePlayerLook {
            appearanceIcon = ui.EquippedAppearanceIcon, weaponIcon = ui.EquippedWeaponIcon,
            appearanceTint = ui.EquippedAppearanceTint, weaponTint = ui.EquippedWeaponTint
        };
    }
}
