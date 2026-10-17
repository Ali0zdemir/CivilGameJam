using CivilGameJam.Cards;
using CivilGameJam.Upgrades;
using UnityEngine;

namespace CivilGameJam
{
    /// <summary>A saved association between one house card and its city building.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class HouseCityUpgradeLink : MonoBehaviour
    {
        [SerializeField] private HouseUpgrades house;
        [SerializeField] private BuildingRailUpgrade railSystem;
        [SerializeField] private HouseColumnUpgrade floodProtection;
        [SerializeField] private HouseSprinklerUpgrade fireProtection;
        [SerializeField] private HouseUpgradePresentation presentation;
        private bool started;

        public HouseUpgrades House => house;
        public Transform Building => railSystem != null ? railSystem.Building : null;
        public BuildingRailUpgrade RailSystem => railSystem;
        public HouseColumnUpgrade FloodProtection => floodProtection;
        public HouseSprinklerUpgrade FireProtection => fireProtection;

        private void OnEnable()
        {
            if (house != null) house.StateChanged += OnHouseChanged;
            if (started) RefreshFromCards();
        }

        private void Start()
        {
            started = true;
            RefreshFromCards();
        }

        private void OnDisable()
        {
            if (presentation != null) presentation.Cancel(this);
            if (house != null) house.StateChanged -= OnHouseChanged;
        }

        private void OnHouseChanged(HouseUpgrades _, UpgradeKind kind)
        {
            if (!started) return;
            if (kind == UpgradeKind.None) RefreshFromCards();
            else
            {
                var product = house.GetInstalled(kind);
                if (product != null && presentation != null && presentation.QueueInstallation(this, kind, product)) return;
                if (presentation != null) presentation.Cancel(this, kind);
                Apply(kind);
            }
        }

        public void RefreshFromCards()
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || house == null) return;
            if (presentation != null) presentation.Cancel(this);
            Apply(UpgradeKind.RailSystem);
            Apply(UpgradeKind.Breakwater);
            Apply(UpgradeKind.Fire);
        }

        internal void ApplyInstalled(UpgradeKind kind, DraggableCard expectedProduct)
        {
            if (isActiveAndEnabled && house != null && house.GetInstalled(kind) == expectedProduct) Apply(kind);
        }

        private void Apply(UpgradeKind kind)
        {
            bool installed = house.GetInstalled(kind) != null;
            switch (kind)
            {
                case UpgradeKind.RailSystem:
                    if (railSystem != null) railSystem.SetInstalled(installed);
                    break;
                case UpgradeKind.Breakwater:
                    // The showcase's per-house flood defence also feeds its shared coastal breakwater.
                    if (floodProtection != null) floodProtection.SetInstalled(installed);
                    break;
                case UpgradeKind.Fire:
                    if (fireProtection != null) fireProtection.SetInstalled(installed);
                    break;
            }
        }
    }
}
