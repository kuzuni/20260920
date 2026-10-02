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
            [System.NonSerialized] internal CharacterPupilClip clip;
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
        [Tooltip("시선을 좌우 두 방향으로 고정하고 타겟이 없으면 마지막 방향을 유지합니다.")]
        public bool horizontalGazeOnly;
        float horizontalDirection = 1;
        public bool blinking = true;
        public Vector2 blinkInterval = new Vector2(2.5f, 5.5f);
        [Min(.01f)] public float blinkDuration = .13f;
        public Transform target;
        public bool Paused { get; set; }
        public bool UnlitPreview { get; set; }
        public CharacterFaceView View { get; set; }
        public bool IsHurt => hurtRemaining > 0;
        public bool IsBlinking => blinkRemaining > 0 && !IsHurt;
        float hurtRemaining;
        float blinkRemaining;
        float activeBlinkDuration;
        float nextBlink;
        bool showMouth = true, showBrows = true;
        bool tintValid;
        Color previousTint;
        bool suspendingForPool;
        bool highlightsPending;

        public void SetFaceParts(bool mouthVisible, bool browsVisible)
        {
            showMouth = mouthVisible; showBrows = browsVisible;
            SetExpression(IsHurt);
        }
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
        void OnDisable() { target = null; ResetExpressionState(gameObject.activeInHierarchy && !suspendingForPool); }

        public void SuspendForPool()
        {
            // The whole rig is hidden immediately after this call. Defer glint fitting
            // until OnEnable, after its new position and idle bones have been restored.
            suspendingForPool = true;
            enabled = false;
            suspendingForPool = false;
        }

        public void ResetExpression()
        {
            ResetExpressionState(true);
        }

        void ResetExpressionState(bool refreshHighlights)
        {
            hurtRemaining = 0;
            blinkRemaining = 0;
            ScheduleBlink();
            if (leftEye.pupilMotion) leftEye.pupilMotion.localPosition = horizontalGazeOnly ? new Vector3(horizontalDirection * leftEye.travel.x, 0, 0) : Vector3.zero;
            if (rightEye.pupilMotion) rightEye.pupilMotion.localPosition = horizontalGazeOnly ? new Vector3(horizontalDirection * rightEye.travel.x, 0, 0) : Vector3.zero;
            SetExpression(false, refreshHighlights); // Fits after the pupils reach their reset positions.
        }

        public void ShowHit()
        {
            if (!isActiveAndEnabled) return;
            bool alreadyHurt = IsHurt;
            blinkRemaining = 0;
            ScheduleBlink();
            hurtRemaining = hurtDuration;
            // Repeated hits extend the reaction without reassigning the same sprites
            // and fitting two already hidden glints for every damage event.
            if (!alreadyHurt) SetExpression(true);
        }

        public void Blink()
        {
            BeginBlink(true);
        }

        void BeginBlink(bool refreshHighlights)
        {
            if (!isActiveAndEnabled || IsHurt || Paused) return;
            blinkRemaining = Mathf.Max(.01f, blinkDuration);
            activeBlinkDuration = blinkRemaining;
            SetExpression(false, refreshHighlights);
        }

        // Run after Animator evaluation: quad Move animates the head's sorting order.
        public void SyncSorting()
        {
            if (!headRenderer || !sortingGroup) return;
            if(sortingGroup.sortingLayerID!=headRenderer.sortingLayerID)sortingGroup.sortingLayerID=headRenderer.sortingLayerID;
            int order=Mathf.Clamp(headRenderer.sortingOrder+Mathf.Max(1,sortingOffset),-32768,32767);
            if(sortingGroup.sortingOrder!=order)sortingGroup.sortingOrder=order;
        }

        void SetExpression(bool hurt, bool refreshHighlights = true)
        {
            SetEye(leftEye, hurt); SetEye(rightEye, hurt);
            UpdateLids();
            if (mouth) { mouth.enabled = showMouth; mouth.sprite = hurt ? hurtMouth : normalMouth; }
            // Bound world faces get their final pose/camera in LateUpdate. Fitting
            // here as well repeats the work for every pooled respawn in FixedUpdate.
            // Unbound portraits/tools still refresh synchronously.
            if (refreshHighlights && (View == null || Paused || !isActiveAndEnabled)) RefreshHighlights();
            else {
                highlightsPending |= refreshHighlights;
                if (hurt) {
                    if (leftEye.highlight) leftEye.highlight.enabled = false;
                    if (rightEye.highlight) rightEye.highlight.enabled = false;
                }
            }
        }

        void SetEye(Eye eye, bool hurt)
        {
            if (eye.white) eye.white.sprite = hurt ? eye.hurt : eye.normal;
            if (eye.pupil) eye.pupil.enabled = !hurt;
            if (eye.brow) eye.brow.enabled = showBrows && !hurt;
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
            if (eye.lidMotion) { var scale=new Vector3(1,openness,1);if(!eye.lidMotion.localScale.Equals(scale))eye.lidMotion.localScale=scale; }
        }

        public void SetTint(Color color)
        {
            if(tintValid && previousTint==color)return;
            tintValid=true;previousTint=color;
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

        static readonly Unity.Profiling.ProfilerMarker faceMarker = new Unity.Profiling.ProfilerMarker("Doodle/FaceLate");
        void LateUpdate()
        {
            using var sample = faceMarker.Auto();
            SyncSorting();
            if (Paused) {
                // A rig can be restored just before its owner publishes the pause.
                // Finish that one pending visual update without advancing any timers.
                if (highlightsPending) RefreshHighlights();
                return;
            }
            if (hurtRemaining > 0)
            {
                hurtRemaining = Mathf.Max(0, hurtRemaining - Time.deltaTime);
                if (hurtRemaining == 0) SetExpression(false, false);
            }
            else if (!blinking)
            {
                if (blinkRemaining > 0) { blinkRemaining = 0; SetExpression(false, false); }
            }
            else if (blinkRemaining > 0)
            {
                blinkRemaining = Mathf.Max(0, blinkRemaining - Time.deltaTime);
                if (blinkRemaining == 0) { ScheduleBlink(); SetExpression(false, false); }
            }
            else
            {
                nextBlink -= Time.deltaTime;
                if (nextBlink <= 0)
                {
                    BeginBlink(false);
                }
            }
            UpdateLids();
            UpdateEye(leftEye); UpdateEye(rightEye);
            // Timers and gaze continue off screen. Only the expensive visual fit is
            // skipped; the current camera/eye bounds restore it on the first visible frame.
            if (View == null || View.MaySee(leftEye.pupilMask) || View.MaySee(rightEye.pupilMask)) RefreshHighlights();
        }

        public void RefreshHighlights()
        {
            UpdatePupilClip(leftEye); UpdatePupilClip(rightEye);
            UpdateHighlight(leftEye); UpdateHighlight(rightEye);
            highlightsPending = false;
        }

        void UpdatePupilClip(Eye eye)
        {
            // Editor portrait/setup tools must keep the authored prefab mask intact.
            // A hidden pupil gets its updated matrix when its expression reopens.
            if (!Application.isPlaying || !eye.pupil || !eye.pupil.enabled) return;
            if (eye.clip == null) eye.clip = new CharacterPupilClip();
            eye.clip.Apply(eye, UnlitPreview);
        }

        void UpdateHighlight(Eye eye)
        {
            if (!eye.highlight || !eye.highlightMotion || !eye.pupilMask || !eye.pupil) return;
            var motion = eye.highlightMotion;
            var previousOffset = motion.localPosition;
            if (IsHurt || !eye.pupil.enabled) {
                if (!previousOffset.Equals(Vector3.zero)) motion.localPosition = Vector3.zero;
                if (eye.highlight.enabled) eye.highlight.enabled = false;
                return;
            }
            var highlight = eye.highlight;
            var glintBounds = highlight.sprite.bounds;
            var glintMatrix = HighlightSpaceMatrix(highlight.transform);
            var parent = motion.parent;
            // Solve from the unshifted authored position without first moving the
            // live hierarchy back to zero. Rewriting both positions every frame
            // dirties the animated head and its descendant transforms twice.
            var parentMatrix = parent ? HighlightSpaceMatrix(parent) : Matrix4x4.identity;
            var worldOffset = parentMatrix.MultiplyVector(previousOffset);
            glintMatrix.m03 -= worldOffset.x; glintMatrix.m13 -= worldOffset.y; glintMatrix.m23 -= worldOffset.z;
            var mask = new GlintBoundary(HighlightSpaceMatrix(eye.pupilMask.transform), eye.pupilMask.sprite, glintMatrix, glintBounds.extents);
            var pupil = new GlintBoundary(HighlightSpaceMatrix(eye.pupil.transform), eye.pupil.sprite, glintMatrix, glintBounds.extents);
            var original = glintMatrix.MultiplyPoint3x4(glintBounds.center);
            var position = original;
            bool fits = mask.radius > 0 && pupil.radius > 0;
            // Keep the whole glint in the intersection of the visible eye and pupil.
            // It is drawn without stencil clipping, so looking to an edge cannot cut it.
            for (int i = 0; i < 6; i++)
            {
                var before = position;
                position = mask.Fit(position);
                position = pupil.Fit(position);
                if ((position - before).sqrMagnitude < .0000000001f) break;
            }
            var maskPosition = mask.Fit(position);
            // Very narrow lids leave no room for a whole glint; hide it with the blink.
            bool visible = !IsBlinking || fits && (maskPosition - position).sqrMagnitude < .000001f;
            if (eye.highlight.enabled != visible) eye.highlight.enabled = visible;
            var nextOffset = parentMatrix.inverse.MultiplyVector(position - original);
            if (!previousOffset.Equals(nextOffset)) motion.localPosition = nextOffset;
        }

        Matrix4x4 HighlightSpaceMatrix(Transform part)
        {
            if (!UnlitPreview) return part.localToWorldMatrix;
            // Portrait stages live far from gameplay. Fit in face-local coordinates
            // so large world translations cannot toggle tiny glints on/off by rounding.
            var result = Matrix4x4.identity;
            var root = transform.root;
            while (part && part != root) {
                result = Matrix4x4.TRS(part.localPosition, part.localRotation, part.localScale) * result;
                part = part.parent;
            }
            return result;
        }

        readonly struct GlintBoundary
        {
            readonly Matrix4x4 toLocal, toWorld;
            readonly Vector2 center;
            public readonly float radius;
            public GlintBoundary(Matrix4x4 shape, Sprite sprite, Matrix4x4 glintMatrix, Vector3 half)
            {
                // Read native transform/bounds data once per eye, not on every
                // intersection iteration. Still reflects all prefab edits this frame.
                toLocal = shape.inverse; toWorld = shape;
                var bounds = sprite.bounds; center = bounds.center;
                var x = toLocal.MultiplyVector(glintMatrix.MultiplyVector(new Vector3(half.x,0,0)));
                var y = toLocal.MultiplyVector(glintMatrix.MultiplyVector(new Vector3(0,half.y,0)));
                radius = Mathf.Min(bounds.extents.x, bounds.extents.y) * .89f - Mathf.Sqrt(x.sqrMagnitude + y.sqrMagnitude);
            }
            public Vector3 Fit(Vector3 point)
            {
                if (radius <= 0) return point;
                var local = toLocal.MultiplyPoint3x4(point);
                var offset = Vector2.ClampMagnitude((Vector2)local - center, radius);
                local.x = center.x + offset.x; local.y = center.y + offset.y;
                return toWorld.MultiplyPoint3x4(local);
            }
        }

        void UpdateEye(Eye eye)
        {
            if (!eye.pupilMotion) return;
            if (horizontalGazeOnly)
            {
                if (target && target.gameObject.activeInHierarchy)
                {
                    float side = transform.InverseTransformPoint(target.position).x;
                    if (Mathf.Abs(side) > .05f) horizontalDirection = Mathf.Sign(side);
                }
                var horizontalOffset = new Vector3(horizontalDirection * eye.travel.x, 0, 0);
                if (!eye.pupilMotion.localPosition.Equals(horizontalOffset)) eye.pupilMotion.localPosition = horizontalOffset;
                return;
            }
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
            var previousPosition = eye.pupilMotion.localPosition;
            var nextPosition = Vector3.Lerp(previousPosition, offset, blend);
            if (!previousPosition.Equals(nextPosition)) eye.pupilMotion.localPosition = nextPosition;
        }
    }
}
