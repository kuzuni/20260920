using DG.Tweening;
using UnityEngine;

namespace DoodleIdle
{
    public sealed partial class DoodleRigVisual
    {
        float hitScale = 1;
        Sequence hitTween;
        Material hitWhiteMaterial;
        Material[] hitOriginalMaterials;
        Material hitWeaponMaterial;
        bool whiteActive;
        public float HitFeedbackDuration { get; private set; }
        float whiteIntensity, whiteHold, whiteFade;
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        public void BeginHitFeedback()
        {
            if (!Rig) return;
            ResetHitFeedback();
            var settings = DoodleHitFeedbackSettings.Shared;
            HitFeedbackDuration = settings.Duration;
            float grow = Mathf.Max(.001f, settings.growDuration), recover = Mathf.Max(.001f, settings.returnDuration);
            float timeScale = Mathf.Min(1, HitFeedbackDuration / (grow + recover));
            whiteIntensity = Mathf.Clamp01(settings.whiteIntensity);
            whiteHold = Mathf.Max(0, settings.whiteHoldDuration);
            whiteFade = Mathf.Max(.001f, settings.whiteFadeDuration);
            float flashTimeScale = Mathf.Min(1, HitFeedbackDuration / (whiteHold + whiteFade));
            whiteHold *= flashTimeScale; whiteFade *= flashTimeScale;
            if (!hitWhiteMaterial) hitWhiteMaterial = new Material(Resources.Load<Shader>("DoodleIdle/DoodleHitWhite")) {
                name = "Character white hit", hideFlags = HideFlags.DontSave
            };
            if (hitOriginalMaterials == null || hitOriginalMaterials.Length != Rig.partRenderers.Length)
                hitOriginalMaterials = new Material[Rig.partRenderers.Length];
            for (int i = 0; i < Rig.partRenderers.Length; i++) {
                hitOriginalMaterials[i] = Rig.partRenderers[i].sharedMaterial;
                Rig.partRenderers[i].sharedMaterial = hitWhiteMaterial;
            }
            if (Rig.weaponRenderer) {
                hitWeaponMaterial = Rig.weaponRenderer.sharedMaterial;
                Rig.weaponRenderer.sharedMaterial = hitWhiteMaterial;
            }
            whiteActive = true; hitWhiteMaterial.SetFloat(FlashId, whiteIntensity);
            // Tween a visual multiplier instead of the actor root: colliders stay fixed,
            // while Sync preserves mirroring, costume scale and the actor's visual center.
            hitTween = DG.Tweening.DOTween.Sequence()
                .Append(DG.Tweening.DOTween.To(() => hitScale, v => hitScale = v, Mathf.Max(1, settings.scaleMultiplier), grow * timeScale).SetEase(settings.growEase))
                .Append(DG.Tweening.DOTween.To(() => hitScale, v => hitScale = v, 1f, recover * timeScale).SetEase(settings.returnEase))
                .SetAutoKill(false).Pause();
            Sync();
        }

        public void AdvanceHitFeedback(float elapsed)
        {
            if (hitTween == null) return;
            // The combat clock drives Goto, so pausing and PVP don't double-update it.
            hitTween.Goto(Mathf.Clamp(elapsed, 0, HitFeedbackDuration), false);
            hitWhiteMaterial.SetFloat(FlashId, whiteIntensity * (1 - Mathf.Clamp01((elapsed - whiteHold) / whiteFade)));
            Sync();
        }

        public void ResetHitFeedback()
        {
            hitTween?.Kill(); hitTween = null; hitScale = 1;
            if (whiteActive && Rig) {
                for (int i = 0; i < Rig.partRenderers.Length; i++)
                    Rig.partRenderers[i].sharedMaterial = hitOriginalMaterials[i];
                if (Rig.weaponRenderer) Rig.weaponRenderer.sharedMaterial = hitWeaponMaterial;
            }
            whiteActive = false;
        }
        void OnDisable() { ResetHitFeedback(); Sync(); }
        void OnDestroy() { ResetHitFeedback(); if (hitWhiteMaterial) Destroy(hitWhiteMaterial); }
    }
}
