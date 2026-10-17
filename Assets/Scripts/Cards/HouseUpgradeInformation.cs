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
        }

        [SerializeField] private HouseUpgrades house;
        [SerializeField] private RectTransform details;
        [SerializeField] private TextMeshProUGUI defaultTitle;
        [SerializeField] private TextMeshProUGUI defaultDescription;
        [SerializeField] private UpgradeEntry[] entries;
        [SerializeField, Min(1f)] private float emptyHeight = 128f;
        [SerializeField, Min(1f)] private float entryHeight = 112f;
        [SerializeField, Min(1f)] private float entrySpacing = 96f;

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
            int visible = 0;
            if (entries != null)
                foreach (var entry in entries)
                {
                    if (entry.row == null) continue;
                    bool installed = house != null && house.GetInstalled(entry.kind) != null;
                    entry.row.gameObject.SetActive(installed);
                    if (installed) entry.row.anchoredPosition = new Vector2(0f, -entrySpacing * visible++);
                }
            if (defaultTitle != null) defaultTitle.gameObject.SetActive(visible == 0);
            if (defaultDescription != null) defaultDescription.gameObject.SetActive(visible == 0);
            if (details != null)
                details.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    visible == 0 ? emptyHeight : entryHeight + entrySpacing * (visible - 1));
        }
    }
}
