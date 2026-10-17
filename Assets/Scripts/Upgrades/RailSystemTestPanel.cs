using System;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Upgrades
{
    [DisallowMultipleComponent]
    public sealed class RailSystemTestPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class BuildingRow
        {
            public BuildingRailUpgrade upgrade;
            public Button toggleButton;
            public Text buttonLabel;
            public Text buildingLabel;
        }

        [SerializeField] private BuildingRow[] buildings = Array.Empty<BuildingRow>();
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text headerLabel;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private GameObject panelBody;
        [SerializeField] private bool expanded = true;

        public int InstalledCount
        {
            get
            {
                var count = 0;
                foreach (var row in buildings) if (row.upgrade != null && row.upgrade.IsInstalled) count++;
                return count;
            }
        }

        private void OnEnable()
        {
            foreach (var row in buildings) if (row.upgrade != null) row.upgrade.StateChanged += OnUpgradeChanged;
            Refresh();
            ApplyPanelState();
        }

        private void OnDisable()
        {
            foreach (var row in buildings) if (row.upgrade != null) row.upgrade.StateChanged -= OnUpgradeChanged;
        }

        public void ToggleBuilding(int index)
        {
            if (!Application.isPlaying || index < 0 || index >= buildings.Length) return;
            var upgrade = buildings[index].upgrade;
            if (upgrade != null) upgrade.SetInstalled(!upgrade.IsInstalled);
        }

        public void ResetAll()
        {
            if (!Application.isPlaying) return;
            foreach (var row in buildings) if (row.upgrade != null) row.upgrade.ResetUpgrade();
            Refresh();
        }

        public void TogglePanel()
        {
            expanded = !expanded;
            ApplyPanelState();
        }

        private void ApplyPanelState()
        {
            if (panelBody != null) panelBody.SetActive(expanded);
            if (panelRect != null) panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, expanded ? 126f + buildings.Length * 32f : 60f);
            if (headerLabel != null) headerLabel.text = expanded ? "RAIL SYSTEM  [-]" : "RAIL SYSTEM  [+]";
        }

        private void OnUpgradeChanged(BuildingRailUpgrade _) => Refresh();

        private void Refresh()
        {
            foreach (var row in buildings)
            {
                var installed = row.upgrade != null && row.upgrade.IsInstalled;
                if (row.toggleButton != null)
                {
                    row.toggleButton.interactable = row.upgrade != null && row.upgrade.IsAvailable;
                    var image = row.toggleButton.targetGraphic;
                    if (image != null) image.color = installed ? new Color(0.19f, 0.48f, 0.39f) : new Color(0.28f, 0.38f, 0.4f);
                }
                if (row.buttonLabel != null) row.buttonLabel.text = installed ? "REMOVE" : "ADD";
                if (row.buildingLabel != null) row.buildingLabel.color = installed ? new Color(0.56f, 0.91f, 0.73f) : new Color(0.91f, 0.93f, 0.88f);
            }
            if (statusLabel != null) statusLabel.text = InstalledCount + " / " + buildings.Length + " installed";
            if (resetButton != null) resetButton.interactable = InstalledCount > 0;
        }
    }
}
