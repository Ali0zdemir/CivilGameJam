using TMPro;
using UnityEngine;

namespace CivilGameJam.Cards
{
    [RequireComponent(typeof(HouseUpgrades))]
    public sealed class HouseUpgradeInformation : MonoBehaviour
    {
        [System.Serializable]
        private struct UpgradeEntry
        {
            public UpgradeKind kind;
            public RectTransform row;
            public TextMeshProUGUI description;
            [TextArea(2, 3)] public string installedDescription;
        }

        [SerializeField] private HouseUpgrades house;
        [SerializeField] private UpgradeEntry[] entries;

        private void OnEnable()
        {
            if (house == null) house = GetComponent<HouseUpgrades>();
            if (house != null) house.StateChanged += OnUpgradeChanged;
            RefreshView();
        }

        private void OnDisable()
        {
            if (house != null) house.StateChanged -= OnUpgradeChanged;
        }

        private void OnUpgradeChanged(HouseUpgrades owner, UpgradeKind kind) => RefreshView();

        private void RefreshView()
        {
            if (entries != null)
                foreach (var entry in entries)
                {
                    bool installed = house != null && house.GetInstalled(entry.kind) != null;
                    if (entry.description != null)
                        entry.description.text = installed ? entry.installedDescription : string.Empty;
                    if (entry.row != null) entry.row.gameObject.SetActive(installed);
                }
        }
    }
}
