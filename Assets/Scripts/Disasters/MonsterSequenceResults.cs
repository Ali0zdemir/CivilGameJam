using System;
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
        [SerializeField] private TMP_Text earnedPointsCount;
        [SerializeField] private Button retryButton;
        [SerializeField] private BuildingModelGeometry[] houses = new BuildingModelGeometry[0];
        [Header("Tutorial outcome")]
        [SerializeField] private TMP_Text tutorialOutcomeLabel;
        [SerializeField, TextArea(1, 3)] private string tutorialOutcomeMessage =
            "Half of your houses survived thanks to the disaster prevention system you installed.";
        private bool explainNextRound;

        public void ExplainNextRoundResult() => explainNextRound = true;

        public bool IsVisible => overlay != null && overlay.activeSelf;
        public RectTransform EarnedPointsArea => earnedPointsCount != null ? earnedPointsCount.transform.parent as RectTransform : earnedPointsLabel?.rectTransform;
        public int DestroyedHouseCount { get; private set; }
        public int TotalHouseCount { get; private set; }
        public int ProtectedHouseCount { get; private set; }
        public event Action Dismissed;
        public event Action Showing;

        public bool IsHouseDestroyed(BuildingModelGeometry house)
        {
            if (house == null) return false;
            return house.TryGetComponent<ApartmentEarthquakeAnimation>(out var quake) && quake.HasCollapsed ||
                house.TryGetComponent<ApartmentTsunamiCollapse>(out var flood) && flood.HasCollapsed ||
                sequence != null && sequence.WasHouseBurnt(house.transform);
        }

        public float ExpectedCollapseDustTail
        {
            get
            {
                var tail = 0f;
                foreach (var house in houses)
                    if (house != null && house.TryGetComponent<ApartmentEarthquakeAnimation>(out var quake) &&
                        quake.IsRunning && !quake.IsProtected && house.TryGetComponent<BuildingCollapseFeedback>(out var feedback))
                        tail = Mathf.Max(tail, feedback.MaximumDustLifetime);
                return tail;
            }
        }

        public bool HouseCollapseAnimationsFinished
        {
            get
            {
                foreach (var house in houses)
                {
                    if (house == null) continue;
                    if (house.TryGetComponent<ApartmentEarthquakeAnimation>(out var quake) && quake.IsRunning && !quake.IsProtected) return false;
                    if (house.TryGetComponent<ApartmentTsunamiCollapse>(out var flood) && flood.IsPlaying) return false;
                }
                return true;
            }
        }

        public bool HouseEffectsFinished
        {
            get
            {
                foreach (var house in houses)
                {
                    if (house == null) continue;
                    if (house.TryGetComponent<ApartmentEarthquakeAnimation>(out var quake) && quake.IsRunning) return false;
                    if (house.TryGetComponent<ApartmentTsunamiCollapse>(out var flood) && flood.IsPlaying) return false;
                    if (house.TryGetComponent<BuildingCollapseFeedback>(out var feedback) && feedback.IsPlaying) return false;
                }
                return true;
            }
        }

        private void Awake() => Hide();

        public void Show()
        {
            if (overlay == null || sequence == null || sequence.Stage != MonsterSequenceStage.Complete) return;
            DestroyedHouseCount = TotalHouseCount = 0;
            foreach (var house in houses)
            {
                if (house == null) continue;
                TotalHouseCount++;
                if (IsHouseDestroyed(house)) DestroyedHouseCount++;
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
            Open("AP EARNED: +" + Mathf.Max(0, earnedPoints), Mathf.Max(0, earnedPoints));
        }

        private void Open(string reward, int earnedPoints = -1)
        {
            if (tutorialOutcomeLabel != null)
            {
                tutorialOutcomeLabel.text = tutorialOutcomeMessage;
                tutorialOutcomeLabel.gameObject.SetActive(explainNextRound && earnedPoints >= 0 && TotalHouseCount == 4 && ProtectedHouseCount == 2);
            }
            explainNextRound = false;
            if (earnedPointsLabel != null) earnedPointsLabel.text = earnedPointsCount != null && earnedPoints >= 0 ? "AP EARNED" : reward;
            if (earnedPointsCount != null) earnedPointsCount.text = earnedPoints >= 0 ? "+" + earnedPoints : "-";
            if (destroyedCountLabel != null) destroyedCountLabel.text = ProtectedHouseCount + " / " + TotalHouseCount;
            Showing?.Invoke();
            overlay.SetActive(true);
            if (retryButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
        }

        public void Hide()
        {
            explainNextRound = false;
            if (tutorialOutcomeLabel != null) tutorialOutcomeLabel.gameObject.SetActive(false);
            if (EventSystem.current != null && retryButton != null && EventSystem.current.currentSelectedGameObject == retryButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            if (overlay != null) overlay.SetActive(false);
        }

        public void Retry()
        {
            var wasVisible = IsVisible;
            Hide();
            if (wasVisible) Dismissed?.Invoke();
        }

        private void OnDisable() => Hide();
    }
}
