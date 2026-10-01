using UnityEngine;

namespace DoodleIdle
{
    public sealed class DoodleSpawnPortal : MonoBehaviour
    {
        public Transform rotatingCircle;
        public SpriteRenderer art;
        [InspectorName("회전 속도 (도/초)")] public float rotationSpeed = 220;
        Vector3 authoredScale;
        float age, lifetime;
        bool prepared;
        public void Begin(Vector3 position, float size, float duration)
        {
            if (!prepared) { authoredScale = transform.localScale; prepared = true; }
            transform.position = position; transform.localScale = authoredScale * size;
            age = 0; lifetime = Mathf.Max(.01f, duration);
            rotatingCircle.localRotation = Quaternion.identity;
            art.color = new Color(1, 1, 1, 0); gameObject.SetActive(true);
        }
        public bool Simulate(float dt)
        {
            age += dt;
            if (age >= lifetime) return false;
            // Rotate the circular child, then let its parent's Y scale flatten it.
            rotatingCircle.localRotation = Quaternion.Euler(0, 0, -age * rotationSpeed);
            art.color = new Color(1, 1, 1, Mathf.Min(Mathf.Clamp01(age / .06f), Mathf.Clamp01((lifetime - age) / .12f)));
            return true;
        }
    }
}
