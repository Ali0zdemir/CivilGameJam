using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CivilGameJam.Disasters
{
    /// <summary>Shows a snapshot of the completed sequence using the saved house card targets.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSequenceResults : MonoBehaviour
    {
        [SerializeField] private MonsterSequenceController sequence;
        [SerializeField] private GameObject overlay;
        [InspectorName("Protected House Count Label")]
        [SerializeField] private TMP_Text destroyedCountLabel;
        [SerializeField] private TMP_Text earnedPointsLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private BuildingModelGeometry[] houses = new BuildingModelGeometry[0];

        public bool IsVisible => overlay != null && overlay.activeSelf;
        public int DestroyedHouseCount { get; private set; }
        public int TotalHouseCount { get; private set; }
        public int ProtectedHouseCount { get; private set; }

        private void Awake() => Hide();

        public void Show()
        {
            if (overlay == null || sequence == null || sequence.Stage != MonsterSequenceStage.Complete) return;
            DestroyedHouseCount = TotalHouseCount = 0;
            foreach (var house in houses)
            {
                if (house == null) continue;
                TotalHouseCount++;
                var destroyed = house.TryGetComponent<ApartmentEarthquakeAnimation>(out var quake) && quake.HasCollapsed;
                destroyed |= house.TryGetComponent<ApartmentTsunamiCollapse>(out var flood) && flood.HasCollapsed;
                destroyed |= sequence.WasHouseBurnt(house.transform);
                if (destroyed) DestroyedHouseCount++;
            }
            ProtectedHouseCount = TotalHouseCount - DestroyedHouseCount;
            Open("DISASTER REPORT");
        }

        public void ShowRound(int destroyed, int total, int earnedPoints, int protectedHouses = -1)
        {
            if (!Application.isPlaying || overlay == null || sequence == null || sequence.IsRunning) return;
            TotalHouseCount = Mathf.Max(0, total);
            DestroyedHouseCount = Mathf.Clamp(destroyed, 0, TotalHouseCount);
            ProtectedHouseCount = protectedHouses < 0 ? TotalHouseCount - DestroyedHouseCount : Mathf.Clamp(protectedHouses, 0, TotalHouseCount);
            Open("AP EARNED: +" + Mathf.Max(0, earnedPoints));
        }

        private void Open(string reward)
        {
            if (earnedPointsLabel != null) earnedPointsLabel.text = reward;
            if (destroyedCountLabel != null) destroyedCountLabel.text = ProtectedHouseCount + " / " + TotalHouseCount;
            overlay.SetActive(true);
            if (retryButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
        }

        public void Hide()
        {
            if (EventSystem.current != null && retryButton != null && EventSystem.current.currentSelectedGameObject == retryButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            if (overlay != null) overlay.SetActive(false);
        }

        public void Retry() => Hide();

        private void OnDisable() => Hide();
    }
}
