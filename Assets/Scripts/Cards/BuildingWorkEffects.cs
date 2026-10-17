using UnityEngine;

namespace CivilGameJam.Cards
{
    public sealed class BuildingWorkEffects : MonoBehaviour
    {
        [SerializeField] private BuildingStation station;
        [SerializeField] private FactoryProduction factory;
        [SerializeField] private CanvasGroup presentation;
        [SerializeField] private RectTransform smokeOrigin;
        [SerializeField] private Vector2 smokeTravel = new Vector2(13f, 54f);
        [SerializeField] private CardSoftGraphic[] smoke;
        [SerializeField] private CardSoftGraphic scan;
        [SerializeField] private CardSoftGraphic windowLight;
        [Min(0.1f)] [SerializeField] private float fadeSeconds = 0.3f;
        [Min(1f)] [SerializeField] private float cycleSeconds = 2.8f;
        private Color[] smokeColors;
        private Color scanColor, windowColor;
        private float clock, strength;
        public float Strength => strength;
        public bool IsWorking => station.Card != null && station.Card.Owner != null && station.WorkerCount > 0 &&
            !station.AwaitingBlueprint && !station.Card.Owner.IsChoosingBlueprint && Time.timeScale > 0f &&
            (factory == null || factory.PendingBlueprint != null);

        private void Awake()
        {
            smokeColors = new Color[smoke.Length];
            for (int i = 0; i < smoke.Length; i++) smokeColors[i] = smoke[i].color;
            if (scan != null) scanColor = scan.color;
            if (windowLight != null) windowColor = windowLight.color;
            Apply();
        }

        private void LateUpdate()
        {
            bool working = IsWorking;
            if (!working && strength <= 0f) return;
            strength = Mathf.MoveTowards(strength, working ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
            clock += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            presentation.alpha = strength;
            for (int i = 0; i < smoke.Length; i++)
            {
                float phase = Mathf.Repeat(clock / cycleSeconds + i / (float)smoke.Length, 1f);
                smoke[i].rectTransform.anchoredPosition = smokeOrigin.anchoredPosition + smokeTravel * phase +
                    Vector2.right * (Mathf.Sin(phase * 3.2f + i) * 5f);
                smoke[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.4f, phase);
                smoke[i].color = Fade(smokeColors[i], Mathf.Sin(phase * Mathf.PI));
            }
            if (scan != null)
            {
                float phase = Mathf.Repeat(clock / cycleSeconds, 1f);
                float width = ((RectTransform)scan.transform.parent).rect.width * 0.67f;
                scan.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-width, width, phase), 0f);
                scan.color = Fade(scanColor, Mathf.Sin(phase * Mathf.PI));
            }
            if (windowLight != null) windowLight.color = Fade(windowColor, 0.7f + 0.3f * Mathf.Sin(clock * 1.4f));
        }

        private static Color Fade(Color color, float alpha) { color.a *= alpha; return color; }
        private void OnDisable() { strength = 0f; if (smokeColors != null) Apply(); }
    }
}
