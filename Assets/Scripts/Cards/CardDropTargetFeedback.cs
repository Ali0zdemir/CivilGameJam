using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    /// <summary>Pulses the saved artwork outlines while a compatible card is being dragged.</summary>
    [DisallowMultipleComponent]
    public sealed class CardDropTargetFeedback : MonoBehaviour
    {
        [SerializeField] private Outline edgeOutline;
        [SerializeField] private Outline glowOutline;
        [SerializeField] private Color tint = new Color(.5f, 1f, .78f, 1f);
        [SerializeField, Min(.01f)] private float fadeSeconds = .09f;
        [SerializeField, Min(.1f)] private float pulseSpeed = 1.25f;
        [SerializeField, Min(.5f)] private float edgeWidth = 4.5f;
        [SerializeField, Min(1f)] private float glowWidth = 9f;
        private float visibility, velocity, elapsed, focus, focusVelocity;

        public bool IsAvailable { get; private set; }
        public bool IsFocused { get; private set; }
        public float Visibility => visibility;

        public void SetAvailable(bool available, bool focused = false)
        {
            IsAvailable = available;
            IsFocused = available && focused;
        }

        private void Awake() => ResetFeedback();
        private void Update() => AdvanceFeedback(Time.unscaledDeltaTime);

        private void AdvanceFeedback(float seconds)
        {
            if (seconds <= 0f) return;
            elapsed += seconds;
            visibility = Mathf.SmoothDamp(visibility, IsAvailable ? IsFocused ? 1f : .9f : 0f,
                ref velocity, Mathf.Max(.01f, fadeSeconds), Mathf.Infinity, seconds);
            focus = Mathf.SmoothDamp(focus, IsFocused ? 1f : 0f, ref focusVelocity,
                Mathf.Max(.01f, fadeSeconds), Mathf.Infinity, seconds);
            // Keep valid destinations bright between pulses; emphasize the one under the pointer.
            float pulse = .94f + .06f * Mathf.Sin(elapsed * Mathf.PI * 2f * pulseSpeed);
            Paint(edgeOutline, visibility * pulse * .98f, edgeWidth * (1f + .2f * focus));
            Paint(glowOutline, visibility * pulse * .42f, glowWidth * (1f + .15f * focus));
        }

        private void Paint(Outline outline, float alpha, float width)
        {
            if (outline == null) return;
            outline.enabled = alpha > .005f;
            outline.effectColor = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
            outline.effectDistance = new Vector2(width, -width);
        }

        private void ResetFeedback()
        {
            IsAvailable = IsFocused = false;
            visibility = velocity = elapsed = focus = focusVelocity = 0f;
            Paint(edgeOutline, 0f, edgeWidth);
            Paint(glowOutline, 0f, glowWidth);
        }

        private void OnDisable() => ResetFeedback();
    }
}
