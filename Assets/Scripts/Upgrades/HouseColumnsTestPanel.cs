using System;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Upgrades
{
    [DisallowMultipleComponent]
    public sealed class HouseColumnsTestPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class HouseRow
        {
            public HouseColumnUpgrade upgrade;
            public Button toggleButton;
            public Text buttonLabel;
            public Text buildingLabel;
        }

        [SerializeField] private HouseRow[] buildings = Array.Empty<HouseRow>();
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
            if (panelRect != null) panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, expanded ? 254f : 60f);
            if (headerLabel != null) headerLabel.text = expanded ? "SEL KORUMASI  [-]" : "SEL KORUMASI  [+]";
        }

        private void OnUpgradeChanged(HouseColumnUpgrade _) => Refresh();

        private void Refresh()
        {
            foreach (var row in buildings)
            {
                var installed = row.upgrade != null && row.upgrade.IsInstalled;
                if (row.toggleButton != null)
                {
                    row.toggleButton.interactable = row.upgrade != null && row.upgrade.IsAvailable;
                    if (row.toggleButton.targetGraphic != null)
                        row.toggleButton.targetGraphic.color = installed ? new Color(0.16f, 0.43f, 0.52f) : new Color(0.28f, 0.38f, 0.4f);
                }
                if (row.buttonLabel != null) row.buttonLabel.text = installed ? "GERİ AL" : "UYGULA";
                if (row.buildingLabel != null) row.buildingLabel.color = installed ? new Color(0.56f, 0.85f, 0.95f) : new Color(0.91f, 0.93f, 0.88f);
            }
            if (statusLabel != null) statusLabel.text = InstalledCount + " / " + buildings.Length + " evde alt kat açık";
            if (resetButton != null) resetButton.interactable = InstalledCount > 0;
        }
    }
}
