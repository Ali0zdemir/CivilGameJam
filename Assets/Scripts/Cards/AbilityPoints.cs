using System;
using TMPro;
using UnityEngine;

namespace CivilGameJam.Cards
{
    /// <summary>The shared, nonnegative AP balance. All card actions debit this one balance.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-300)]
    public sealed class AbilityPoints : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingPoints = 10;
        [SerializeField] private TMP_Text balanceLabel;
        [SerializeField] private int currentPoints = 10;
        private TMP_Text formattedLabel;
        private string balancePrefix, balanceSuffix;
        private bool hasBalanceValue;
        public int CurrentPoints => currentPoints;
        public bool ActionsLocked { get; private set; }
        public event Action Changed;
        public event Action<int> Spent;
        public event Action<int> SpendingRejected;

        private void Awake()
        {
            currentPoints = Mathf.Max(0, startingPoints);
            Refresh();
        }

        public bool CanSpend(int cost) => !ActionsLocked && cost >= 0 && currentPoints >= cost;

        public bool TrySpend(int cost)
        {
            if (!CanSpend(cost)) { ReportInsufficient(cost); return false; }
            currentPoints -= cost;
            Refresh();
            if (cost > 0) Spent?.Invoke(cost);
            return true;
        }

        // Called on an attempted action only; affordability previews remain silent.
        public bool ReportInsufficient(int cost)
        {
            if (ActionsLocked || cost <= currentPoints || cost < 0) return false;
            SpendingRejected?.Invoke(cost);
            return true;
        }

        public void AddPoints(int amount)
        {
            if (amount <= 0) return;
            currentPoints = (int)Math.Min(int.MaxValue, (long)currentPoints + amount);
            Refresh();
        }

        public void SetActionsLocked(bool locked)
        {
            ActionsLocked = locked;
            Changed?.Invoke();
        }

        private void Refresh()
        {
            if (balanceLabel != null)
            {
                if (formattedLabel != balanceLabel) CaptureBalanceFormat();
                if (hasBalanceValue) balanceLabel.text = balancePrefix + currentPoints + balanceSuffix;
            }
            Changed?.Invoke();
        }

        private void CaptureBalanceFormat()
        {
            formattedLabel = balanceLabel;
            hasBalanceValue = false;
            var text = balanceLabel.text ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                // Preserve the authored wording and TMP tags; update only the visible AP number.
                if (text[i] == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end >= 0) { i = end; continue; }
                }
                if (!char.IsDigit(text[i])) continue;
                int last = i + 1;
                while (last < text.Length && char.IsDigit(text[last])) last++;
                balancePrefix = text.Substring(0, i);
                balanceSuffix = text.Substring(last);
                hasBalanceValue = true;
                return;
            }
        }

        private void OnValidate()
        {
            if (!Application.IsPlaying(gameObject))
            {
                currentPoints = Mathf.Max(0, startingPoints);
                return;
            }
            currentPoints = Mathf.Max(0, currentPoints);
            Refresh();
        }
    }
}
