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
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        public void BeginHitFeedback()
        {
            if (!Rig) return;
            ResetHitFeedback();
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
            whiteActive = true; hitWhiteMaterial.SetFloat(FlashId, 1);
            // Tween a visual multiplier instead of the actor root: colliders stay fixed,
            // while Sync preserves mirroring, costume scale and the actor's visual center.
            hitTween = DG.Tweening.DOTween.Sequence()
                .Append(DG.Tweening.DOTween.To(() => hitScale, v => hitScale = v, 1.18f, .08f).SetEase(Ease.OutQuad))
                .Append(DG.Tweening.DOTween.To(() => hitScale, v => hitScale = v, 1f, .42f).SetEase(Ease.OutCubic))
                .SetAutoKill(false).Pause();
            Sync();
        }

        public void AdvanceHitFeedback(float elapsed)
        {
            if (hitTween == null) return;
            // The combat clock drives Goto, so pausing and PVP don't double-update it.
            hitTween.Goto(Mathf.Clamp(elapsed, 0, DoodleIdleGame.HitStopDuration), false);
            hitWhiteMaterial.SetFloat(FlashId, 1 - Mathf.Clamp01((elapsed - .1f) / .25f));
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
