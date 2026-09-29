using UnityEngine;
using UnityEngine.Rendering;

namespace DoodleIdle.CharacterRigs
{
    // This lives under the head bone. Authored eye/mouth transforms are never moved;
    // Only dedicated pupil/lid Motion children are animated in eye-local space.
    [DefaultExecutionOrder(100)]
    public sealed class CharacterFace : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Eye
        {
            public SpriteRenderer white;
            public SpriteRenderer pupil;
            public Transform pupilMotion;
            public Sprite normal;
            public Sprite hurt;
            public Transform lidMotion;
            public SpriteRenderer brow;
            public SpriteMask pupilMask;
            public SpriteRenderer highlight;
            public Transform highlightMotion;
            [Tooltip("눈 로컬 좌표 기준 눈동자의 최대 이동 범위")]
            public Vector2 travel = new Vector2(.12f, .08f);
        }

        public Eye leftEye = new Eye();
        public Eye rightEye = new Eye();
        public SpriteRenderer mouth;
        public Sprite normalMouth;
        public Sprite hurtMouth;
        public SpriteRenderer headRenderer;
        public SortingGroup sortingGroup;
        [Min(1)] public int sortingOffset = 1;
        [Min(.01f)] public float hurtDuration = .3f;
        [Min(0)] public float gazeSpeed = 14f;
        [Min(.01f)] public float gazeDistance = 2f;
        public bool blinking = true;
        public Vector2 blinkInterval = new Vector2(2.5f, 5.5f);
        [Min(.01f)] public float blinkDuration = .13f;
        public Transform target;
        public bool Paused { get; set; }
        public bool IsHurt => hurtRemaining > 0;
        public bool IsBlinking => blinkRemaining > 0 && !IsHurt;
        float hurtRemaining;
        float blinkRemaining;
        float activeBlinkDuration;
        float nextBlink;
        // Cosmetic randomness must not consume the combat RNG sequence.
        System.Random blinkRandom;

        void ScheduleBlink()
        {
            if (blinkRandom == null) blinkRandom = new System.Random(GetInstanceID());
            float min = Mathf.Max(.01f, Mathf.Min(blinkInterval.x, blinkInterval.y));
            float max = Mathf.Max(min, Mathf.Max(blinkInterval.x, blinkInterval.y));
            nextBlink = Mathf.Lerp(min, max, (float)blinkRandom.NextDouble());
        }

        void OnEnable() { ResetExpression(); SyncSorting(); }
        void OnDisable() { target = null; ResetExpression(); }

        public void ResetExpression()
        {
            hurtRemaining = 0;
            blinkRemaining = 0;
            ScheduleBlink();
            SetExpression(false);
            if (leftEye.pupilMotion) leftEye.pupilMotion.localPosition = Vector3.zero;
            if (rightEye.pupilMotion) rightEye.pupilMotion.localPosition = Vector3.zero;
            RefreshHighlights();
        }

        public void ShowHit()
        {
            if (!isActiveAndEnabled) return;
            blinkRemaining = 0;
            ScheduleBlink();
            hurtRemaining = hurtDuration;
            SetExpression(true);
        }

        public void Blink()
        {
            if (!isActiveAndEnabled || IsHurt || Paused) return;
            blinkRemaining = Mathf.Max(.01f, blinkDuration);
            activeBlinkDuration = blinkRemaining;
            SetExpression(false);
        }

        // Run after Animator evaluation: quad Move animates the head's sorting order.
        public void SyncSorting()
        {
            if (!headRenderer || !sortingGroup) return;
            sortingGroup.sortingLayerID = headRenderer.sortingLayerID;
            sortingGroup.sortingOrder = Mathf.Clamp(headRenderer.sortingOrder + Mathf.Max(1, sortingOffset), -32768, 32767);
        }

        void SetExpression(bool hurt)
        {
            SetEye(leftEye, hurt); SetEye(rightEye, hurt);
            UpdateLids();
            if (mouth) mouth.sprite = hurt ? hurtMouth : normalMouth;
            RefreshHighlights();
        }

        static void SetEye(Eye eye, bool hurt)
        {
            if (eye.white) eye.white.sprite = hurt ? eye.hurt : eye.normal;
            if (eye.pupil) eye.pupil.enabled = !hurt;
            if (eye.brow) eye.brow.enabled = !hurt;
        }

        void UpdateLids()
        {
            float openness = 1;
            if (IsBlinking)
            {
                float t = 1 - blinkRemaining / activeBlinkDuration;
                // Quick relaxed closure, immediate gentler reopening; no held squint.
                openness = t < .4f
                    ? Mathf.Lerp(1, .045f, Mathf.SmoothStep(0, 1, t / .4f))
                    : Mathf.Lerp(.045f, 1, Mathf.SmoothStep(0, 1, (t - .4f) / .6f));
            }
            SetLid(leftEye, openness); SetLid(rightEye, openness);
        }

        static void SetLid(Eye eye, float openness)
        {
            if (eye.lidMotion) eye.lidMotion.localScale = new Vector3(1, openness, 1);
        }

        public void SetTint(Color color)
        {
            TintEye(leftEye, color); TintEye(rightEye, color);
            if (mouth) mouth.color = color;
        }

        static void TintEye(Eye eye, Color color)
        {
            if (eye.white) eye.white.color = color;
            if (eye.pupil) eye.pupil.color = color;
            if (eye.brow) eye.brow.color = color;
            if (eye.highlight) eye.highlight.color = color;
        }

        void LateUpdate()
        {
            SyncSorting();
            if (Paused) return;
            if (hurtRemaining > 0)
            {
                hurtRemaining = Mathf.Max(0, hurtRemaining - Time.deltaTime);
                if (hurtRemaining == 0) SetExpression(false);
            }
            else if (!blinking)
            {
                if (blinkRemaining > 0) { blinkRemaining = 0; SetExpression(false); }
            }
            else if (blinkRemaining > 0)
            {
                blinkRemaining = Mathf.Max(0, blinkRemaining - Time.deltaTime);
                if (blinkRemaining == 0) { ScheduleBlink(); SetExpression(false); }
            }
            else
            {
                nextBlink -= Time.deltaTime;
                if (nextBlink <= 0)
                {
                    Blink();
                }
            }
            UpdateLids();
            UpdateEye(leftEye); UpdateEye(rightEye);
            RefreshHighlights();
        }

        public void RefreshHighlights()
        {
            UpdateHighlight(leftEye); UpdateHighlight(rightEye);
        }

        void UpdateHighlight(Eye eye)
        {
            if (!eye.highlight || !eye.highlightMotion || !eye.pupilMask || !eye.pupil) return;
            eye.highlightMotion.localPosition = Vector3.zero;
            if (IsHurt || !eye.pupil.enabled) { eye.highlight.enabled = false; return; }
            var highlight = eye.highlight;
            var original = highlight.transform.TransformPoint(highlight.sprite.bounds.center);
            var position = original;
            bool fits = true;
            // Keep the whole glint in the intersection of the visible eye and pupil.
            // It is drawn without stencil clipping, so looking to an edge cannot cut it.
            for (int i = 0; i < 6; i++)
            {
                var before = position;
                position = FitCircle(position, eye.pupilMask.transform, eye.pupilMask.sprite, highlight, ref fits);
                position = FitCircle(position, eye.pupil.transform, eye.pupil.sprite, highlight, ref fits);
                if ((position - before).sqrMagnitude < .0000000001f) break;
            }
            var maskPosition = FitCircle(position, eye.pupilMask.transform, eye.pupilMask.sprite, highlight, ref fits);
            // Very narrow lids leave no room for a whole glint; hide it with the blink.
            eye.highlight.enabled = fits && (maskPosition - position).sqrMagnitude < .000001f;
            eye.highlightMotion.position += position - original;
        }

        static Vector3 FitCircle(Vector3 point, Transform shape, Sprite sprite, SpriteRenderer glint, ref bool fits)
        {
            var bounds = sprite.bounds;
            var radius = Mathf.Min(bounds.extents.x, bounds.extents.y) * .89f;
            var half = glint.sprite.bounds.extents;
            var x = shape.InverseTransformVector(glint.transform.TransformVector(new Vector3(half.x,0,0)));
            var y = shape.InverseTransformVector(glint.transform.TransformVector(new Vector3(0,half.y,0)));
            // Conservative bound also supports rotated, mirrored and non-uniform prefab scales.
            radius -= Mathf.Sqrt(x.sqrMagnitude + y.sqrMagnitude);
            if (radius <= 0) { fits = false; return point; }
            var local = shape.InverseTransformPoint(point);
            var offset = Vector2.ClampMagnitude((Vector2)local - (Vector2)bounds.center, radius);
            local.x = bounds.center.x + offset.x; local.y = bounds.center.y + offset.y;
            return shape.TransformPoint(local);
        }

        void UpdateEye(Eye eye)
        {
            if (!eye.pupilMotion) return;
            Vector2 direction = Vector2.zero;
            if (target && target.gameObject.activeInHierarchy)
            {
                // InverseTransformVector accounts for both mirroring and animated head rotation.
                var world = target.position - eye.pupilMotion.parent.position;
                var local = eye.pupilMotion.parent.InverseTransformVector(world);
                direction = Vector2.ClampMagnitude(new Vector2(local.x, local.y) / gazeDistance, 1);
            }
            var offset = new Vector3(direction.x * eye.travel.x, direction.y * eye.travel.y, 0);
            float blend = 1 - Mathf.Exp(-gazeSpeed * Time.deltaTime);
            eye.pupilMotion.localPosition = Vector3.Lerp(eye.pupilMotion.localPosition, offset, blend);
        }
    }
}
