using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CivilGameJam.Upgrades;

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
        [Header("Casualty report")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text outcomeLabel;
        [SerializeField] private TMP_Text houseStatisticCaption;
        [SerializeField] private Button retryButton;
        [Header("Victory")]
        [SerializeField] private Button mainMenuButton;
        [SerializeField, Tooltip("Scene name or path included in Build Settings. Assign your main menu here when it is ready.")]
        private string mainMenuScene = "Assets/Scenes/MainMenuBackup.unity";
        [SerializeField] private BuildingModelGeometry[] houses = new BuildingModelGeometry[0];
        [Header("Tutorial outcome")]
        [SerializeField] private TMP_Text tutorialOutcomeLabel;
        [SerializeField, TextArea(1, 3)] private string tutorialOutcomeMessage =
            "Half of your houses survived thanks to the disaster prevention system you installed.";
        private bool explainNextRound;
        private bool returningToMainMenu;
        [Header("Earthquake survival reward")]
        [SerializeField] private TMP_Text peopleRewardLabel;
        [SerializeField, TextArea(1, 3)] private string peopleRewardMessage =
            "Surviving the earthquake earned you +{people} person! New turns start with {total} people.";

        public void ExplainNextRoundResult() => explainNextRound = true;

        public void ShowPeopleReward(int people, int peoplePerTurn)
        {
            if (!IsVisible || people <= 0 || peopleRewardLabel == null) return;
            peopleRewardLabel.text = peopleRewardMessage.Replace("{people}", people.ToString()).Replace("{total}", peoplePerTurn.ToString());
            peopleRewardLabel.gameObject.SetActive(true);
        }

        public bool IsVisible => overlay != null && overlay.activeSelf;
        public RectTransform EarnedPointsArea => earnedPointsCount != null ? earnedPointsCount.transform.parent as RectTransform : earnedPointsLabel?.rectTransform;
        public int DestroyedHouseCount { get; private set; }
        public int TotalHouseCount { get; private set; }
        public int ProtectedHouseCount { get; private set; }
        public int ReportStage { get; private set; }
        public int ClearedStage { get; private set; }
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

        public void ShowRound(int destroyed, int total, int earnedPoints, int protectedHouses = -1, int clearedStage = 0)
        {
            if (!Application.isPlaying || overlay == null || sequence == null || sequence.IsRunning) return;
            TotalHouseCount = Mathf.Max(0, total);
            DestroyedHouseCount = Mathf.Clamp(destroyed, 0, TotalHouseCount);
            ProtectedHouseCount = protectedHouses < 0 ? TotalHouseCount - DestroyedHouseCount : Mathf.Clamp(protectedHouses, 0, TotalHouseCount);
            Open("AP EARNED: +" + Mathf.Max(0, earnedPoints), Mathf.Max(0, earnedPoints), clearedStage);
        }

        private void Open(string reward, int earnedPoints = -1, int clearedStage = 0)
        {
            ReportStage = sequence.ActiveSequence > 0 ? sequence.ActiveSequence : sequence.SelectedSequence;
            ClearedStage = Mathf.Clamp(clearedStage, 0, 3);
            if (ClearedStage == 0 && TotalHouseCount == 4 && ProtectedHouseCount == 4 && DestroyedHouseCount == 0 && HasAllProtection(ReportStage))
                ClearedStage = ReportStage;
            if (titleLabel != null) titleLabel.text = ClearedStage == 3 ? "YOU WON!"
                : ClearedStage > 0 ? Hazard(ClearedStage) + " STAGE CLEARED" : Hazard(ReportStage) + " STAGE NOT CLEARED";
            if (outcomeLabel != null) outcomeLabel.text = ClearedStage == 3 ? "All four houses survived every disaster."
                : ClearedStage > 0 && ClearedStage < ReportStage ? "New threat: " + Hazard(ReportStage) + ". Prepare all four houses."
                : ClearedStage > 0 ? "All four houses are protected." : "Protect all four houses to clear this stage.";
            if (houseStatisticCaption != null) houseStatisticCaption.text = Hazard(ReportStage) + " - PROTECTED HOUSES";
            if (peopleRewardLabel != null) peopleRewardLabel.gameObject.SetActive(false);
            if (tutorialOutcomeLabel != null)
            {
                tutorialOutcomeLabel.text = tutorialOutcomeMessage;
                tutorialOutcomeLabel.gameObject.SetActive(explainNextRound && earnedPoints >= 0 && TotalHouseCount == 4 && ProtectedHouseCount == 2);
            }
            explainNextRound = false;
            if (earnedPointsLabel != null) earnedPointsLabel.text = earnedPointsCount != null && earnedPoints >= 0 ? "AP EARNED" : reward;
            if (earnedPointsCount != null) earnedPointsCount.text = earnedPoints >= 0 ? "+" + earnedPoints : "-";
            if (destroyedCountLabel != null) destroyedCountLabel.text = ProtectedHouseCount + " / " + TotalHouseCount;
            returningToMainMenu = false;
            bool showMainMenu = ClearedStage == 3 && mainMenuButton != null;
            if (retryButton != null) retryButton.gameObject.SetActive(!showMainMenu);
            if (mainMenuButton != null)
            {
                mainMenuButton.interactable = true;
                mainMenuButton.gameObject.SetActive(showMainMenu);
            }
            Showing?.Invoke();
            overlay.SetActive(true);
            var selectedButton = showMainMenu ? mainMenuButton : retryButton;
            if (selectedButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
        }

        private static string Hazard(int stage) => stage == 3 ? "FIRE" : stage == 2 ? "TSUNAMI" : "EARTHQUAKE";

        private bool HasAllProtection(int stage)
        {
            if (houses.Length != 4) return false;
            foreach (var house in houses)
            {
                if (house == null || !house.TryGetComponent<ApartmentUpgradeModel>(out var model)) return false;
                if (!(stage == 3 ? model.HasFireProtection : stage == 2 ? model.IsElevatorProtected : model.HasSeismic)) return false;
            }
            return true;
        }

        public void Hide()
        {
            explainNextRound = false;
            if (peopleRewardLabel != null) peopleRewardLabel.gameObject.SetActive(false);
            if (tutorialOutcomeLabel != null) tutorialOutcomeLabel.gameObject.SetActive(false);
            if (EventSystem.current != null &&
                (retryButton != null && EventSystem.current.currentSelectedGameObject == retryButton.gameObject ||
                 mainMenuButton != null && EventSystem.current.currentSelectedGameObject == mainMenuButton.gameObject))
                EventSystem.current.SetSelectedGameObject(null);
            if (overlay != null) overlay.SetActive(false);
        }

        public void ReturnToMainMenu()
        {
            if (!Application.isPlaying || !IsVisible || ClearedStage != 3 || returningToMainMenu) return;
            if (string.IsNullOrWhiteSpace(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogWarning("Assign the main menu scene on MonsterSequenceResults and add it to Build Settings before using MAIN MENU.", this);
                return;
            }
            returningToMainMenu = true;
            if (mainMenuButton != null) mainMenuButton.interactable = false;
            Time.timeScale = 1f;
            SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single);
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
