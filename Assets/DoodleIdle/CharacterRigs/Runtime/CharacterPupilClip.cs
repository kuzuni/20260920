using UnityEngine;
using UnityEngine.Rendering;

namespace DoodleIdle.CharacterRigs
{
    // Keep the SpriteMask as the editable source of shape, cutoff and transform.
    // Sample that shape in the pupil draw instead of extracting stencil ranges for
    // every renderer in the scene. Each eye can only see its own mask texture.
    sealed class CharacterPupilClip
    {
        static Material sharedMaterial;
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int MaskTex = Shader.PropertyToID("_EyeMaskTex");
        static readonly int MaskRect = Shader.PropertyToID("_EyeMaskRect");
        static readonly int MaskShape = Shader.PropertyToID("_EyeMaskShape");
        static readonly int PupilToMask = Shader.PropertyToID("_PupilToMask");
        static readonly int Cutoff = Shader.PropertyToID("_EyeMaskCutoff");
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock nativeProperties = new MaterialPropertyBlock();
        SpriteRenderer boundPupil;
        SpriteMask boundMask;
        Material nativeMaterial;
        SpriteMaskInteraction nativeInteraction;
        bool nativeMaskEnabled;
        Sprite pupilSprite, maskSprite;
        Matrix4x4 previousMatrix;
        float previousCutoff;
        bool valid;

        public void Apply(CharacterFace.Eye eye)
        {
            if (boundPupil != eye.pupil || boundMask != eye.pupilMask) RestoreNative();
            if (!eye.pupil || !eye.pupilMask || !eye.pupil.sprite || !eye.pupilMask.sprite) { RestoreNative(); return; }
            // Rotated/tight atlas packing needs a different UV mapping. Preserve the
            // native mask for such externally authored assets.
            if (eye.pupilMask.sprite.packed) { RestoreNative(); return; }
            if (!sharedMaterial) {
                var candidate = Resources.Load<Material>("DoodleIdle/DoodlePupilClip");
                if (candidate && candidate.shader.isSupported) sharedMaterial = candidate;
            }
            if (!sharedMaterial) { RestoreNative(); return; }
            var pupil = eye.pupil;
            var mask = eye.pupilMask;
            if (!boundPupil) {
                boundPupil = pupil; boundMask = mask;
                nativeMaterial = pupil.sharedMaterial; nativeInteraction = pupil.maskInteraction;
                nativeMaskEnabled = mask.enabled; pupil.GetPropertyBlock(nativeProperties);
            }
            if (pupil.sharedMaterial != sharedMaterial) { pupil.sharedMaterial = sharedMaterial; valid = false; }
            if (pupil.maskInteraction != SpriteMaskInteraction.None) pupil.maskInteraction = SpriteMaskInteraction.None;
            if (mask.enabled) mask.enabled = false;
            var matrix = mask.transform.worldToLocalMatrix * pupil.transform.localToWorldMatrix;
            bool spritesChanged = pupilSprite != pupil.sprite || maskSprite != mask.sprite;
            if (valid && !spritesChanged && previousMatrix.Equals(matrix) && previousCutoff == mask.alphaCutoff) return;
            if (!valid || spritesChanged) {
                pupil.GetPropertyBlock(properties);
                pupilSprite = pupil.sprite; maskSprite = mask.sprite;
                // Unpacked tight sprites still have a trimmed textureRect. Their
                // local vertices and pivot use the full source rect, so sampling
                // through textureRect would enlarge the mask at the eye's edge.
                var rect = maskSprite.rect;
                var texture = maskSprite.texture;
                properties.SetTexture(MainTex, pupilSprite.texture);
                properties.SetTexture(MaskTex, texture);
                properties.SetVector(MaskRect, new Vector4(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height));
                var pivot = maskSprite.pivot;
                var size = maskSprite.rect.size;
                properties.SetVector(MaskShape, new Vector4(maskSprite.pixelsPerUnit / size.x, maskSprite.pixelsPerUnit / size.y, pivot.x / size.x, pivot.y / size.y));
            }
            properties.SetMatrix(PupilToMask, matrix);
            properties.SetFloat(Cutoff, mask.alphaCutoff);
            pupil.SetPropertyBlock(properties);
            previousMatrix = matrix; previousCutoff = mask.alphaCutoff; valid = true;
        }

        void RestoreNative()
        {
            if (boundPupil) {
                boundPupil.sharedMaterial = nativeMaterial; boundPupil.maskInteraction = nativeInteraction;
                boundPupil.SetPropertyBlock(nativeProperties);
            }
            if (boundMask) boundMask.enabled = nativeMaskEnabled;
            boundPupil = null; boundMask = null; valid = false;
        }
    }
}
