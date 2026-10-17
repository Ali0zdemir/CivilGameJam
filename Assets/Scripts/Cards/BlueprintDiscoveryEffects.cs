using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class BlueprintDiscoveryEffects : MonoBehaviour
    {
        [SerializeField] private DraggableCard card;
        [SerializeField] private CardFeedback feedback;
        [SerializeField] private RectTransform revealArea;
        [SerializeField] private Image flash;
        [SerializeField] private Image rim;
        [SerializeField] private CardSoftGraphic shine;
        [SerializeField] private CardLightTrail trail;
        [SerializeField] private Canvas flightCanvas;
        [SerializeField] private GraphicRaycaster flightRaycaster;
        [Min(0.1f)] [SerializeField] private float revealSeconds = 0.32f;
        private float elapsed;
        private bool revealing;
        private Color flashColor, rimColor, shineColor;
        private Canvas mainCanvas;
        public bool IsRevealing => revealing;
        public CardLightTrail Trail => trail;

        private void Awake()
        {
            mainCanvas = transform.parent.GetComponentInParent<Canvas>().rootCanvas;
            MatchCanvas();
            flashColor = flash.color;
            rimColor = rim.color;
            shineColor = shine.color;
            StopEffects();
        }

        public void BeginDiscovery()
        {
            revealing = true;
            BeginTransfer();
            elapsed = 0f;
            revealArea.gameObject.SetActive(true);
            trail.gameObject.SetActive(true);
            trail.Begin(card.Rect);
            feedback.Reveal(revealSeconds);
            ApplyReveal(0f);
        }

        public void BeginTransfer()
        {
            flightCanvas.overrideSorting = true;
            flightCanvas.sortingOrder = 20;
            flightRaycaster.enabled = false;
        }

        public void SetFlightProgress(float progress)
        {
            if (revealing && progress >= 0.65f) trail.StopEmitting();
        }

        private void LateUpdate()
        {
            MatchCanvas();
            if (flightCanvas.overrideSorting) flightCanvas.sortingOrder = card.Owner.IsChoosingBlueprint ? -1 : 20;
            if (!revealing) return;
            elapsed += Time.unscaledDeltaTime;
            ApplyReveal(Mathf.Clamp01(elapsed / revealSeconds));
        }

        private void ApplyReveal(float t)
        {
            float visibility = card.Owner != null && card.Owner.IsChoosingBlueprint ? 0f : 1f;
            flash.color = Fade(flashColor, (1f - t) * (1f - t) * visibility);
            rim.color = Fade(rimColor, (1f - t) * visibility);
            shine.color = Fade(shineColor, Mathf.Sin(t * Mathf.PI) * visibility);
            shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-260f, 260f, t * t * (3f - 2f * t)), 0f);
        }

        private static Color Fade(Color color, float alpha) { color.a *= alpha; return color; }

        private void MatchCanvas()
        {
            if (mainCanvas == null || flightCanvas == null) return;
            if (flightCanvas.renderMode != mainCanvas.renderMode) flightCanvas.renderMode = mainCanvas.renderMode;
            if (flightCanvas.worldCamera != mainCanvas.worldCamera) flightCanvas.worldCamera = mainCanvas.worldCamera;
        }

        public void StopEffects()
        {
            revealing = false;
            if (flightCanvas != null) { flightCanvas.overrideSorting = false; flightCanvas.sortingOrder = 0; }
            if (flightRaycaster != null) flightRaycaster.enabled = true;
            if (revealArea != null) revealArea.gameObject.SetActive(false);
            if (trail != null) { trail.Clear(); trail.gameObject.SetActive(false); }
        }

        private void OnDisable() => StopEffects();
    }
}
