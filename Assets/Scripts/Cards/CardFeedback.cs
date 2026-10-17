using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CardFeedback : MonoBehaviour
    {
        private const float PickupScale = 1.07f;
        private const float PickupSeconds = 0.11f;
        private const float SettleSeconds = 0.2f;
        [SerializeField] private RectTransform rect;
        [SerializeField] private Shadow shadow;
        [Tooltip("Card size while hovered, relative to its authored size.")]
        [Min(1f)] [SerializeField] private float hoverScale = 1.21125f;
        [Tooltip("Duration of the smooth hover enlargement and return.")]
        [Min(0.01f)] [SerializeField] private float hoverSeconds = 0.24f;
        [Header("Drag and placement motion")]
        [Range(0f, 8f)] [SerializeField] private float dragTiltDegrees = 4f;
        [Min(0.02f)] [SerializeField] private float tiltSmoothSeconds = 0.11f;
        [Range(0f, 0.08f)] [SerializeField] private float placementSquash = 0.035f;
        [Min(0.1f)] [SerializeField] private float placementSeconds = 0.34f;
        private Coroutine feedbackRoutine;
        private float scale = 1f;
        private float lift;
        private bool held;
        private bool hovered;
        private bool initialized;
        private Vector3 layoutScale;
        private Quaternion layoutRotation;
        private DraggableCard card;
        private bool dragging;
        private float tilt, tiltVelocity, targetTilt, lastDragInput;
        private float placementTime = float.PositiveInfinity;

        public Vector3 LayoutScale => initialized ? layoutScale : rect.localScale;
        private float RestScale => hovered ? hoverScale : 1f;
        private float RestLift => hovered ? 0.55f : 0f;

        public void SetHovered(bool value)
        {
            if (hovered == value) return;
            hovered = value;
            StopAnimation();
            if (isActiveAndEnabled) feedbackRoutine = StartCoroutine(Lift(hoverSeconds, true));
        }

        private void Awake()
        {
            layoutScale = rect.localScale;
            initialized = true;
            layoutRotation = rect.localRotation;
            card = GetComponent<DraggableCard>();
            Apply(1f, 0f);
        }

        public void BeginDragMotion()
        {
            dragging = true;
            targetTilt = 0f;
            lastDragInput = Time.unscaledTime;
        }

        public void TrackDrag(Vector2 canvasDelta)
        {
            float velocity = canvasDelta.x / Mathf.Max(0.008f, Time.unscaledDeltaTime);
            targetTilt = Mathf.Clamp(-velocity * 0.006f, -dragTiltDegrees, dragTiltDegrees);
            lastDragInput = Time.unscaledTime;
        }

        public void EndDragMotion() { dragging = false; targetTilt = 0f; }

        public void SetDisplayedRotation(Quaternion rotation)
        {
            tilt = Mathf.DeltaAngle(0f, (Quaternion.Inverse(layoutRotation) * rotation).eulerAngles.z);
            tiltVelocity = targetTilt = 0f;
            rect.localRotation = rotation;
        }

        public void Punch() => placementTime = 0f;

        public void Reveal(float seconds)
        {
            StopAnimation();
            feedbackRoutine = StartCoroutine(RevealCard(Mathf.Max(0.1f, seconds)));
        }

        private IEnumerator RevealCard(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / seconds);
                float amount = t < 0.65f ? Mathf.Lerp(0.94f, 1.025f, Smooth(t / 0.65f)) :
                    Mathf.Lerp(1.025f, 1f, Smooth((t - 0.65f) / 0.35f));
                Apply(amount, 0.35f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            Apply(1f, 0f);
            feedbackRoutine = null;
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            if (!dragging || Time.unscaledTime - lastDragInput > 0.06f) targetTilt = 0f;
            tilt = Mathf.SmoothDamp(tilt, targetTilt, ref tiltVelocity, tiltSmoothSeconds,
                Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(tilt) < 0.005f && Mathf.Abs(tiltVelocity) < 0.05f) { tilt = 0f; tiltVelocity = 0f; }
            Quaternion rotation = layoutRotation * Quaternion.Euler(0f, 0f, tilt);
            if (!rect.localRotation.Equals(rotation)) rect.localRotation = rotation;
            placementTime += Time.unscaledDeltaTime;
            ApplyScale();
            if (dragging) card?.KeepDragGrip();
        }

        private void ApplyScale()
        {
            float t = Mathf.Clamp01(placementTime / Mathf.Max(0.1f, placementSeconds));
            float squash = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Pow(1f - t, 2f) * placementSquash;
            Vector3 value = Vector3.Scale(LayoutScale * scale, new Vector3(1f + squash, 1f - squash * 0.65f, 1f));
            if (!rect.localScale.Equals(value)) rect.localScale = value;
        }

        public void SetLayoutScale(Vector3 value)
        {
            layoutScale = value;
            if (initialized) Apply(scale, lift);
            else rect.localScale = value;
        }

        public void PickUp()
        {
            if (held) return;
            held = true;
            StopAnimation();
            feedbackRoutine = StartCoroutine(Lift(PickupSeconds, false));
        }

        public void SetDisplayedScale(float value)
        {
            StopAnimation();
            held = false;
            hovered = false;
            Apply(value, 0f);
        }

        public void Release()
        {
            held = false;
            StopAnimation();
            feedbackRoutine = StartCoroutine(Settle());
        }

        private IEnumerator Lift(float seconds, bool smooth)
        {
            float fromScale = scale;
            float fromLift = lift;
            float targetScale = held ? Mathf.Max(PickupScale, RestScale) : RestScale;
            float targetLift = held ? 1f : RestLift;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / seconds);
                t = smooth ? t * t * t * (t * (t * 6f - 15f) + 10f) : EaseOut(t);
                Apply(Mathf.Lerp(fromScale, targetScale, t), Mathf.Lerp(fromLift, targetLift, t));
                yield return null;
            }
            Apply(targetScale, targetLift);
            feedbackRoutine = null;
        }

        private IEnumerator Settle()
        {
            float fromScale = scale;
            float fromLift = lift;
            for (float elapsed = 0f; elapsed < SettleSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / SettleSeconds;
                float nextScale = t < 0.3f
                    ? Mathf.Lerp(fromScale, RestScale * 0.975f, EaseOut(t / 0.3f))
                    : Mathf.Lerp(RestScale * 0.975f, RestScale, EaseOut((t - 0.3f) / 0.7f));
                Apply(nextScale, Mathf.Lerp(fromLift, RestLift, EaseOut(t)));
                yield return null;
            }
            Apply(RestScale, RestLift);
            feedbackRoutine = null;
        }

        private void Apply(float scaleFactor, float shadowLift)
        {
            scale = scaleFactor;
            lift = shadowLift;
            ApplyScale();
            shadow.effectColor = new Color(0f, 0f, 0f, Mathf.Lerp(0.12f, 0.24f, shadowLift));
            shadow.effectDistance = Vector2.Lerp(new Vector2(4f, -5f), new Vector2(10f, -14f), shadowLift);
        }

        private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private void StopAnimation()
        {
            if (feedbackRoutine == null) return;
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            StopAnimation();
            held = false;
            hovered = false;
            dragging = false;
            tilt = tiltVelocity = targetTilt = 0f;
            placementTime = float.PositiveInfinity;
            if (rect != null) rect.localRotation = layoutRotation;
            if (rect != null && shadow != null) Apply(1f, 0f);
        }
    }
}
