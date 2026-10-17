using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Presentation
{
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class ProgressBarFeedback : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private Outline glow;
        [SerializeField] private Color flashColor = Color.white;
        [Min(0.01f)] [SerializeField] private float flashSeconds = 0.14f;
        [Min(0f)] [SerializeField] private float holdSeconds = 0.04f;
        [Min(0.01f)] [SerializeField] private float fadeSeconds = 0.34f;
        [Min(0.01f)] [SerializeField] private float resumeSeconds = 0.12f;
        private Color fillColor, glowColor;
        private bool initialized, completing, resuming, requestedVisible;
        private float age, requestedProgress;

        public bool IsCompleting => completing;
        public float CompletionSeconds => Mathf.Max(0.01f, flashSeconds) + Mathf.Max(0f, holdSeconds) + Mathf.Max(0.01f, fadeSeconds);

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized) return;
            if (visibility == null) visibility = GetComponent<CanvasGroup>();
            if (fill != null) fillColor = fill.color;
            if (glow != null) glowColor = glow.effectColor;
            initialized = true;
        }

        public void Begin(float progress = 0f)
        {
            Initialize();
            completing = resuming = false;
            RestoreColor();
            SetProgress(progress, true);
        }

        public void SetProgress(float progress, bool visible)
        {
            Initialize();
            requestedProgress = Mathf.Clamp01(progress);
            requestedVisible = visible;
            // Work can finish or reset immediately while its saved bar completes the visual outro.
            if (completing) return;
            ApplyFill(requestedProgress);
            if (visible)
            {
                gameObject.SetActive(true);
                if (!resuming && visibility != null) visibility.alpha = 1f;
            }
            else Hide();
        }

        public void Complete()
        {
            Initialize();
            gameObject.SetActive(true);
            completing = true;
            resuming = false;
            requestedVisible = false;
            requestedProgress = 0f;
            age = 0f;
            RestoreColor();
            ApplyFill(1f);
            if (visibility != null) visibility.alpha = 1f;
        }

        public void ResetPresentation()
        {
            Initialize();
            completing = resuming = requestedVisible = false;
            requestedProgress = 0f;
            RestoreColor();
            ApplyFill(0f);
            Hide();
        }

        private void LateUpdate() => AdvancePresentation(Time.unscaledDeltaTime);

        private void AdvancePresentation(float seconds)
        {
            if (!completing && !resuming) return;
            age += Mathf.Max(0f, seconds);
            if (resuming)
            {
                float t = Mathf.Clamp01(age / Mathf.Max(0.01f, resumeSeconds));
                if (visibility != null) visibility.alpha = Mathf.SmoothStep(0f, 1f, t);
                if (t >= 1f) resuming = false;
                return;
            }
            float flash = Mathf.Sin(Mathf.Clamp01(age / Mathf.Max(0.01f, flashSeconds)) * Mathf.PI);
            if (fill != null) fill.color = Color.Lerp(fillColor, new Color(flashColor.r, flashColor.g, flashColor.b, fillColor.a), flash * 0.9f);
            if (glow != null)
            {
                glow.enabled = flash > 0.001f;
                var color = glowColor; color.a *= flash;
                glow.effectColor = color;
            }
            float fade = Mathf.Clamp01((age - Mathf.Max(0.01f, flashSeconds) - Mathf.Max(0f, holdSeconds)) / Mathf.Max(0.01f, fadeSeconds));
            if (visibility != null) visibility.alpha = 1f - Mathf.SmoothStep(0f, 1f, fade);
            if (fade < 1f) return;
            completing = false;
            RestoreColor();
            if (requestedVisible)
            {
                // Another school student can still be working when the previous student graduates.
                ApplyFill(requestedProgress);
                age = 0f;
                resuming = true;
                if (visibility != null) visibility.alpha = 0f;
            }
            else { ApplyFill(0f); Hide(); }
        }

        private void ApplyFill(float amount)
        {
            if (fill == null) return;
            if (fill.type == Image.Type.Filled) fill.fillAmount = amount;
            else
            {
                var rect = fill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(amount, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
        }

        private void Hide()
        {
            resuming = false;
            gameObject.SetActive(false);
            if (visibility != null) visibility.alpha = 0f;
        }

        private void RestoreColor()
        {
            if (fill != null) fill.color = fillColor;
            if (glow != null) { glow.enabled = false; glow.effectColor = glowColor; }
        }

        private void OnDisable()
        {
            completing = resuming = false;
            if (initialized) RestoreColor();
        }
    }
}
