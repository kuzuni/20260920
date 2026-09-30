using UnityEngine;

namespace DoodleIdle.CharacterRigs
{
    [DefaultExecutionOrder(200)]
    public sealed class CharacterFootDust : MonoBehaviour
    {
        public ParticleSystem particles;
        [Tooltip("실제 캐릭터 이동을 측정할 루트. 발 본의 애니메이션은 이동으로 취급하지 않습니다.")]
        public Transform movementOrigin;
        [Min(.02f)] public float spacing = .22f;
        [Min(.1f)] public float teleportDistance = 3f;
        public Vector2 scatter = new Vector2(.055f, .025f);
        public bool Paused { get; set; }
        Vector3 previous;
        float distanceRemainder;
        bool initialized;
        uint noise = 1;

        public void Bind(Transform origin)
        {
            movementOrigin = origin;
            ResetTrail();
        }
        public void ResetTrail()
        {
            previous = (movementOrigin ? movementOrigin : transform).position;
            distanceRemainder = 0;
            initialized = true;
        }
        void OnEnable()
        {
            ResetTrail();
            noise = unchecked((uint)GetInstanceID()) | 1u;
            if (particles) { particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); particles.Play(false); }
        }
        void OnDisable()
        {
            if (particles) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            initialized = false;
            distanceRemainder = 0;
        }
        float RandomSigned()
        {
            noise = noise * 1664525u + 1013904223u;
            return (noise & 0xffffff) / 8388608f - 1;
        }
        void LateUpdate()
        {
            if (!particles) return;
            if (Paused)
            {
                if (!particles.isPaused) particles.Pause(false);
                ResetTrail();
                return;
            }
            if (!particles.isPlaying) particles.Play(false);
            var current = (movementOrigin ? movementOrigin : transform).position;
            if (!initialized) { ResetTrail(); return; }
            var delta = current - previous; delta.z = 0;
            previous = current;
            float distance = delta.magnitude;
            if (distance > teleportDistance) { distanceRemainder = 0; return; }
            if (distance < .0001f) return;
            float step = Mathf.Max(.02f, spacing);
            // Place puffs along the travelled segment, not one burst at the new position.
            // Use the current foot offset so turning in place cannot spray a false trail.
            var end = particles.transform.position;
            for (float along = step - distanceRemainder; along <= distance; along += step)
            {
                var position = end - delta * (1 - along / distance);
                position += new Vector3(RandomSigned() * scatter.x, RandomSigned() * scatter.y, 0);
                particles.Emit(new ParticleSystem.EmitParams { position = position, velocity = Vector3.zero }, 1);
            }
            distanceRemainder = (distanceRemainder + distance) % step;
        }
    }
}
