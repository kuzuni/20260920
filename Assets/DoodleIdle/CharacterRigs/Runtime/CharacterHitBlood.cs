using UnityEngine;

namespace DoodleIdle.CharacterRigs
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class CharacterHitBlood : MonoBehaviour
    {
        [InspectorName("뒤통수 위 분사 각도"), Range(-89, 0)]
        [Tooltip("플레이어 HitBlood의 Z 회전 기준. -54.783은 뒤통수 위 54.783도입니다. 좌우 시선에 따라 자동 반전됩니다.")]
        public float rearUpAngle = -54.783f;
        [InspectorName("Burst 없을 때 방출 시간"), Min(.001f)]
        [Tooltip("Burst 설정이 없으면 Rate over Time × 이 시간만큼 피격 순간에 방출합니다.")]
        public float emissionSampleDuration = .1f;
        public bool Paused { get; set; }
        ParticleSystem particles;
        void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            Clear();
        }
        public void Burst(bool facingLeft = false)
        {
            if (!particles) particles = GetComponent<ParticleSystem>();
            // Emit from the authored head anchor; all size/color/shape/speed modules
            // remain on the shared nested prefab. Manual simulation supports game pause.
            var emission = particles.emission;
            int count = emission.burstCount > 0
                ? Mathf.RoundToInt(emission.GetBurst(0).count.Evaluate(0, Random.value))
                : Mathf.CeilToInt(emission.rateOverTime.Evaluate(0, Random.value) * Mathf.Max(.001f, emissionSampleDuration));
            // Cone direction must follow combat facing, not the animated head's
            // rotation (negative rig scale does not reliably mirror world emission).
            var shape = particles.shape;
            var authoredRotation = shape.rotation;
            float angle = -Mathf.Clamp(rearUpAngle, -89, 0) * Mathf.Deg2Rad;
            var direction = new Vector3((facingLeft ? 1 : -1) * Mathf.Cos(angle), Mathf.Sin(angle), 0);
            var localDirection = transform.InverseTransformDirection(direction);
            shape.rotation = Quaternion.FromToRotation(Vector3.forward, localDirection).eulerAngles;
            particles.Emit(count);
            shape.rotation = authoredRotation;
        }
        public void Clear()
        {
            if (!particles) particles = GetComponent<ParticleSystem>();
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Simulate(0, false, true, false);
            var emission = particles.emission; emission.enabled = false;
        }
        void LateUpdate()
        {
            if (!Paused && particles && particles.particleCount > 0)
                particles.Simulate(Time.deltaTime, false, false, false);
        }
        void OnDisable() => Clear();
    }
}
