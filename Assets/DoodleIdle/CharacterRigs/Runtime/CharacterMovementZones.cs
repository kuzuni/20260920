using UnityEngine;
namespace DoodleIdle.CharacterRigs
{
    public sealed class CharacterMovementZones : MonoBehaviour
    {
        [Tooltip("A: 적이 들어오면 이동 정지, Idle에서 제자리 공격")]
        public CircleCollider2D stopAndAttack;
        [Tooltip("B: 적이 들어오면 후퇴 시작")]
        public CircleCollider2D startRetreat;
        [Tooltip("C: 모든 적이 나갈 때까지 후퇴 유지 (B보다 크게 설정)")]
        public CircleCollider2D finishRetreat;
        public bool Retreating { get; set; }
        public bool Valid => enabled && stopAndAttack && startRetreat && finishRetreat
            && stopAndAttack.enabled && startRetreat.enabled && finishRetreat.enabled;
    }
}
