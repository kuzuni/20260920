using UnityEngine;

namespace DoodleIdle.CharacterRigs
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class CharacterHitBlood : MonoBehaviour
    {
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
                ? Mathf.RoundToInt(emission.GetBurst(0).count.Evaluate(0, Random.value)) : 0;
            // Cone direction must follow combat facing, not the animated head's
            // rotation (negative rig scale does not reliably mirror world emission).
            var shape = particles.shape;
            var authoredRotation = shape.rotation;
            var direction = new Vector3(facingLeft ? 1 : -1, 1, 0).normalized;
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
