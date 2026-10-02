using UnityEngine;
namespace DoodleIdle.CharacterRigs
{
    public sealed class CharacterSpawnZones : MonoBehaviour
    {
        [InspectorName("A · 소환 금지 범위")] public CircleCollider2D exclusion;
        [InspectorName("B · 소환 최대 범위")] public CircleCollider2D boundary;
        void Awake() => ConfigureTriggers();
        void OnValidate() => ConfigureTriggers();
        public void ConfigureTriggers()
        {
            Configure(exclusion); Configure(boundary);
        }
        static void Configure(CircleCollider2D zone)
        {
            if (!zone) return;
            zone.isTrigger = true; zone.enabled = true;
            // Geometric spawn sensors need neither collision response nor contact pairs.
            zone.excludeLayers = ~0; zone.callbackLayers = 0; zone.contactCaptureLayers = 0;
        }
    }
}
