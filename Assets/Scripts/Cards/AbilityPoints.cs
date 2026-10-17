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
            if (balanceLabel != null) balanceLabel.text = currentPoints + " AP";
            Changed?.Invoke();
        }

        private void OnValidate()
        {
            if (!Application.IsPlaying(gameObject)) currentPoints = Mathf.Max(0, startingPoints);
            else currentPoints = Mathf.Max(0, currentPoints);
            Refresh();
        }
    }
}
