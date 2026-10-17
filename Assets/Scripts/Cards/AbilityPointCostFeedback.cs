using TMPro;
using UnityEngine;

namespace CivilGameJam.Cards
{
    /// <summary>Shakes and flashes one authored worker-cost badge on an unaffordable action.</summary>
    [DisallowMultipleComponent]
    public sealed class AbilityPointCostFeedback : MonoBehaviour
    {
        [SerializeField] private RectTransform badge;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color warningColor = new Color(1f, .25f, .2f, 1f);
        [SerializeField, Min(.1f)] private float warningSeconds = .5f;
        [SerializeField, Min(0f)] private float shakeDistance = 4f;
        private Vector2 restPosition;
        private Color restColor;
        private float elapsed;
        public bool IsPlaying { get; private set; }

        public void PlayDenied()
        {
            if (!isActiveAndEnabled || badge == null || label == null) return;
            if (!IsPlaying)
            {
                restPosition = badge.anchoredPosition;
                restColor = label.color;
            }
            elapsed = 0f;
            IsPlaying = true;
        }

        private void Update()
        {
            if (!IsPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, warningSeconds));
            float envelope = Mathf.Sin(Mathf.PI * t);
            label.color = Color.Lerp(restColor, warningColor, envelope);
            badge.anchoredPosition = restPosition + Vector2.right *
                (Mathf.Sin(t * Mathf.PI * 6f) * shakeDistance * (1f - t));
            if (t >= 1f) ResetFeedback();
        }

        private void ResetFeedback()
        {
            if (!IsPlaying) return;
            if (badge != null) badge.anchoredPosition = restPosition;
            if (label != null) label.color = restColor;
            IsPlaying = false;
        }

        private void OnDisable() => ResetFeedback();
    }
}
