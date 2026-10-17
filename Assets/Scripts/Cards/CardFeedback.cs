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
        private Coroutine feedbackRoutine;
        private float scale = 1f;
        private float lift;
        private bool held;
        private bool hovered;
        private bool initialized;
        private Vector3 layoutScale;

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
            Apply(1f, 0f);
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
            rect.localScale = LayoutScale * scaleFactor;
            shadow.effectColor = new Color(0f, 0f, 0f, Mathf.Lerp(0.12f, 0.24f, shadowLift));
            shadow.effectDistance = Vector2.Lerp(new Vector2(4f, -5f), new Vector2(10f, -14f), shadowLift);
        }

        private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

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
            if (rect != null && shadow != null) Apply(1f, 0f);
        }
    }
}
