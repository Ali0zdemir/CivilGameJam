using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    /// <summary>Animates the saved AP counter, pooled spend labels and READY border.</summary>
    [DisallowMultipleComponent]
    public sealed class AbilityPointFeedback : MonoBehaviour
    {
        [SerializeField] private AbilityPoints bank;
        [SerializeField] private RectTransform counter;
        [SerializeField] private TMP_Text balanceLabel;
        [SerializeField] private Image counterGlow;
        [SerializeField] private TMP_Text[] spendLabels;
        [SerializeField] private Image readyGlow;
        [Header("Spend and insufficient AP")]
        [SerializeField] private Color spendColor = new Color(1f, .25f, .2f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, .25f, .2f, 1f);
        [SerializeField, Min(.1f)] private float spendSeconds = 1f;
        [SerializeField, Min(.1f)] private float warningSeconds = 1f;
        [SerializeField, Min(.1f)] private float popupSeconds = 1.3f;
        [SerializeField, Min(0f)] private float popupRise = 28f;
        [SerializeField, Min(0f)] private float shakeDistance = 5f;
        [Header("Ready attention")]
        [SerializeField] private Color readyColor = new Color(.55f, 1f, .75f, 1f);
        [SerializeField, Min(.1f)] private float readyPulseSpeed = .75f;
        private Popup[] popups;
        private bool flashing, warning, subscribed;
        private float flashElapsed, readyElapsed, readyVisibility, readyVelocity;
        private Vector2 counterPosition;
        private Vector3 counterScale;
        private Color balanceColor;

        private sealed class Popup
        {
            public TMP_Text label;
            public Vector2 position;
            public Vector3 scale;
            public float elapsed;
            public bool active;
        }

        public bool IsWarning => flashing && warning;
        public bool IsFlashing => flashing;
        public bool IsReadyPromptActive { get; private set; }

        private void Awake()
        {
            popups = new Popup[spendLabels == null ? 0 : spendLabels.Length];
            for (int i = 0; i < popups.Length; i++)
            {
                var label = spendLabels[i];
                if (label == null) continue;
                popups[i] = new Popup { label = label, position = label.rectTransform.anchoredPosition,
                    scale = label.rectTransform.localScale };
                label.gameObject.SetActive(false);
            }
            if (counterGlow != null) counterGlow.gameObject.SetActive(false);
            if (readyGlow != null) readyGlow.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (bank == null || subscribed) return;
            bank.Spent += ShowSpend;
            bank.SpendingRejected += ShowDenied;
            bank.Changed += OnBalanceChanged;
            subscribed = true;
        }

        private void OnBalanceChanged()
        {
            if (bank.ActionsLocked || bank.CurrentPoints != 0) SetReadyPrompt(false);
        }

        private void ShowSpend(int amount)
        {
            StartFlash(false);
            if (popups == null || popups.Length == 0) return;
            Popup chosen = null;
            foreach (var popup in popups)
            {
                if (popup == null) continue;
                if (!popup.active) { chosen = popup; break; }
                if (chosen == null || popup.elapsed > chosen.elapsed) chosen = popup;
            }
            if (chosen == null) return;
            chosen.elapsed = 0f;
            chosen.active = true;
            chosen.label.text = "-" + amount + " AP";
            chosen.label.color = new Color(spendColor.r, spendColor.g, spendColor.b, 0f);
            chosen.label.rectTransform.anchoredPosition = chosen.position;
            chosen.label.rectTransform.localScale = chosen.scale * .92f;
            chosen.label.gameObject.SetActive(true);
        }

        private void ShowDenied(int cost) => StartFlash(true);

        private void StartFlash(bool denied)
        {
            if (counter == null || balanceLabel == null) return;
            if (!flashing)
            {
                counterPosition = counter.anchoredPosition;
                counterScale = counter.localScale;
                balanceColor = balanceLabel.color;
            }
            warning = denied;
            flashElapsed = 0f;
            flashing = true;
            if (counterGlow != null) counterGlow.gameObject.SetActive(true);
        }

        public void SetReadyPrompt(bool value)
        {
            value &= isActiveAndEnabled && bank != null && bank.CurrentPoints == 0 && !bank.ActionsLocked;
            if (IsReadyPromptActive == value) return;
            IsReadyPromptActive = value;
            if (value) readyElapsed = 0f;
        }

        private void Update()
        {
            float seconds = Time.unscaledDeltaTime;
            if (flashing)
            {
                flashElapsed += seconds;
                float t = Mathf.Clamp01(flashElapsed / Mathf.Max(.1f, warning ? warningSeconds : spendSeconds));
                float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .08f)) *
                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.84f, 1f, t)));
                var color = warning ? warningColor : spendColor;
                balanceLabel.color = Color.Lerp(balanceColor, color, envelope);
                counter.localScale = counterScale * (1f + .065f * envelope);
                counter.anchoredPosition = counterPosition + Vector2.right *
                    (Mathf.Sin(t * Mathf.PI * 12f) * shakeDistance * (1f - t) * envelope);
                if (counterGlow != null) counterGlow.color = new Color(color.r, color.g, color.b, .65f * envelope);
                if (t >= 1f) ResetFlash();
            }
            if (popups != null)
                foreach (var popup in popups)
                {
                    if (popup == null || !popup.active) continue;
                    popup.elapsed += seconds;
                    float lifetime = Mathf.Max(popupSeconds, Mathf.Max(.1f, spendSeconds) + .2f);
                    float t = Mathf.Clamp01(popup.elapsed / lifetime);
                    float shakeT = Mathf.Clamp01(popup.elapsed / Mathf.Max(.1f, spendSeconds));
                    float shake = Mathf.Sin(shakeT * Mathf.PI * 12f) * shakeDistance * .7f * (1f - shakeT);
                    float ease = 1f - (1f - t) * (1f - t);
                    popup.label.rectTransform.anchoredPosition = popup.position + Vector2.up * (popupRise * ease) + Vector2.right * shake;
                    popup.label.rectTransform.localScale = popup.scale * Mathf.Lerp(.92f, 1f, Mathf.Clamp01(t * 5f));
                    // Keep the red amount readable for the full shake, then fade it out.
                    float alpha = Mathf.Clamp01(popup.elapsed / .07f) * (1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(Mathf.Max(.1f, spendSeconds), lifetime, popup.elapsed)));
                    popup.label.color = new Color(spendColor.r, spendColor.g, spendColor.b, alpha);
                    if (t < 1f) continue;
                    ResetPopup(popup);
                }
            readyVisibility = Mathf.SmoothDamp(readyVisibility, IsReadyPromptActive ? 1f : 0f,
                ref readyVelocity, .16f, Mathf.Infinity, seconds);
            readyElapsed += seconds;
            if (readyGlow != null)
            {
                float pulse = .5f - .5f * Mathf.Cos(readyElapsed * Mathf.PI * 2f * readyPulseSpeed);
                readyGlow.color = new Color(readyColor.r, readyColor.g, readyColor.b, readyVisibility * Mathf.Lerp(.16f, .55f, pulse));
                readyGlow.gameObject.SetActive(readyVisibility > .005f);
            }
        }

        private void ResetFlash()
        {
            if (!flashing) return;
            if (counter != null) { counter.anchoredPosition = counterPosition; counter.localScale = counterScale; }
            if (balanceLabel != null) balanceLabel.color = balanceColor;
            if (counterGlow != null) counterGlow.gameObject.SetActive(false);
            flashing = false;
        }

        private static void ResetPopup(Popup popup)
        {
            popup.active = false;
            popup.label.gameObject.SetActive(false);
            popup.label.rectTransform.anchoredPosition = popup.position;
            popup.label.rectTransform.localScale = popup.scale;
        }

        private void OnDisable()
        {
            if (bank != null && subscribed)
            {
                bank.Spent -= ShowSpend;
                bank.SpendingRejected -= ShowDenied;
                bank.Changed -= OnBalanceChanged;
            }
            subscribed = false;
            ResetFlash();
            if (popups != null) foreach (var popup in popups) if (popup != null) ResetPopup(popup);
            IsReadyPromptActive = false;
            readyVisibility = readyVelocity = 0f;
            if (readyGlow != null) readyGlow.gameObject.SetActive(false);
        }
    }
}
