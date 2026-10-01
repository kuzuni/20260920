using UnityEngine;
namespace DoodleIdle.CharacterRigs
{
    [DefaultExecutionOrder(200)]
    public sealed class CharacterFootDust : MonoBehaviour
    {
        public ParticleSystem particles;
        [Tooltip("발 본의 애니메이션 대신 실제 캐릭터 루트 이동만 측정합니다.")]
        public Transform movementOrigin;
        [Min(.1f)] public float teleportDistance = 3f;
        public bool Paused { get; set; }
        Vector3 previous, groundOffset;
        bool initialized;
        public void Bind(Transform origin)
        {
            movementOrigin = origin;
            groundOffset = particles ? particles.transform.position - origin.position : Vector3.zero;
            RestartTrail();
        }
        public void ResetTrail()
        {
            previous = (movementOrigin ? movementOrigin : transform).position;
            initialized = true;
        }
        void RestartTrail()
        {
            ResetTrail();
            if (!particles) return;
            particles.transform.position = previous + groundOffset;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Simulate(0, false, true);
            var emission = particles.emission;
            emission.rateOverTime = 0; emission.burstCount = 0; emission.enabled = false;
            particles.Play(false);
        }
        void OnEnable()
        {
            var origin = movementOrigin ? movementOrigin : transform;
            groundOffset = particles ? particles.transform.position - origin.position : Vector3.zero;
            RestartTrail();
        }
        void OnDisable()
        {
            if (particles) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            initialized = false;
        }
        void LateUpdate()
        {
            if (!particles) return;
            Vector3 current = (movementOrigin ? movementOrigin : transform).position;
            var delta = current - previous; delta.z = 0;
            // Pin to translation: turning and walking bones never add emitter distance.
            particles.transform.position = current + groundOffset;
            var emission = particles.emission;
            if (Paused) {
                emission.enabled = false;
                if (!particles.isPaused) particles.Pause(false);
                ResetTrail(); return;
            }
            if (!initialized || delta.sqrMagnitude > teleportDistance * teleportDistance) {
                RestartTrail(); return;
            }
            if (!particles.isPlaying) particles.Play(false);
            emission.enabled = delta.sqrMagnitude > .00000001f;
            previous = current;
        }
    }
}
