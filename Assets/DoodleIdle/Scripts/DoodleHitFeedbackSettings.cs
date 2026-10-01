using DG.Tweening;
using UnityEngine;

namespace DoodleIdle
{
    [CreateAssetMenu(menuName = "Doodle Idle/Hit Feedback Settings")]
    public sealed class DoodleHitFeedbackSettings : ScriptableObject
    {
        [Header("플레이어 · 적 · PVP 공통 피격 설정")]
        [InspectorName("경직 시간 (초)"), Min(.001f)] public float hitStopDuration = .1f;
        [InspectorName("확대 배율"), Min(1)] public float scaleMultiplier = 1.18f;
        [Tooltip("확대 + 복구 시간이 경직보다 길면 두 시간을 같은 비율로 줄여 경직 안에 끝냅니다.")]
        [InspectorName("커지는 시간 (초)"), Min(.001f)] public float growDuration = .016f;
        [InspectorName("돌아오는 시간 (초)"), Min(.001f)] public float returnDuration = .084f;
        [InspectorName("커질 때 곡선")] public Ease growEase = Ease.OutQuad;
        [InspectorName("돌아올 때 곡선")] public Ease returnEase = Ease.OutCubic;
        [Header("흰색 피격 플래시")]
        [InspectorName("흰색 강도"), Range(0, 1)] public float whiteIntensity = 1;
        [InspectorName("흰색 유지 시간 (초)"), Min(0)] public float whiteHoldDuration = .02f;
        [InspectorName("흰색 사라지는 시간 (초)"), Min(.001f)] public float whiteFadeDuration = .05f;

        static DoodleHitFeedbackSettings shared;
        public static DoodleHitFeedbackSettings Shared {
            get {
                if (!shared) shared = Resources.Load<DoodleHitFeedbackSettings>("DoodleIdle/HitFeedbackSettings");
                if (!shared) { shared = CreateInstance<DoodleHitFeedbackSettings>(); shared.hideFlags = HideFlags.HideAndDontSave; }
                return shared;
            }
        }
        public float Duration => Mathf.Max(.001f, hitStopDuration);
    }
}
