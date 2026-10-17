using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Disasters
{
    public enum MonsterSequenceStage { Ready, Emerging, WalkingToFirstPoint, Fire, WalkingToSecondPoint, Complete }

    /// <summary>Runs the selected test sequence using the monster and controls saved in the scene.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class MonsterSequenceController : MonoBehaviour
    {
        [Header("Saved scene")]
        [SerializeField] private SeaMonsterEmergence monster;
        [SerializeField] private SeaMonsterWalk walk;
        [SerializeField] private TsunamiTestController tsunami;
        [SerializeField] private EarthquakeTestController earthquake;
        [SerializeField] private FireTestController fire;
        [SerializeField] private CityCameraController navigation;
        [SerializeField] private MonsterSequenceResults results;

        [Header("Sequence camera")]
        [SerializeField, Range(1f, 1.6f)] private float cameraZoomFactor = 1.3f;
        [SerializeField, Min(0.1f)] private float cameraZoomSeconds = 1.8f;
        [SerializeField, Min(0.1f)] private float cameraReturnSeconds = 1.8f;

        [Header("Saved test menu")]
        [SerializeField, Range(1, 3)] private int selectedSequence = 1;
        [SerializeField] private RectTransform panel;
        [SerializeField] private GameObject choices;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button[] sequenceButtons = new Button[0];
        [SerializeField] private Button[] individualTestButtons = new Button[0];
        [SerializeField] private TMP_Text selectionLabel;
        [SerializeField] private TMP_Text statusLabel;
        private float riseElapsed;
        private float departureLimit, departureElapsed;
        private bool monsterDeparted;
        private bool expanded;
        private bool waveCameraActive;

        public bool IsRunning { get; private set; }
        public int SelectedSequence => Mathf.Clamp(selectedSequence, 1, 3);
        public int ActiveSequence { get; private set; }
        public MonsterSequenceStage Stage { get; private set; }

        public bool WasHouseBurnt(Transform house) => fire != null && fire.WasHouseBurnt(house);

        public void ShowRoundResults(int destroyed, int total, int earnedPoints, int protectedHouses = -1)
        {
            if (results != null) results.ShowRound(destroyed, total, earnedPoints, protectedHouses);
        }

        private void Awake() => RefreshMenu();

        public void ToggleMenu()
        {
            if (IsRunning) return;
            expanded = !expanded;
            RefreshMenu();
        }

        public void SelectSequence(int number)
        {
            if (IsRunning) return;
            selectedSequence = Mathf.Clamp(number, 1, 3);
            expanded = false;
            RefreshMenu();
        }

        public void StartSelectedSequence() => StartSequence(0f);

        // READY uses a bounded departure; the saved standalone debug routes retain their full paths.
        public void StartRoundSequence(float maximumDepartureSeconds) => StartSequence(Mathf.Max(.1f, maximumDepartureSeconds));

        private void StartSequence(float maximumDepartureSeconds)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsRunning) return;
            if (monster == null || walk == null || walk.WalkPoint == null || walk.AfterEventPoint == null ||
                tsunami == null || earthquake == null || fire == null) return;
            if (results != null) results.Hide();
            fire.StopFire();
            earthquake.StopEarthquake();
            tsunami.StopTsunami();
            walk.CancelWalk();
            monster.ResetEmergence();
            ActiveSequence = SelectedSequence;
            riseElapsed = 0f;
            departureElapsed = 0f;
            departureLimit = maximumDepartureSeconds;
            monsterDeparted = false;
            expanded = false;
            Stage = MonsterSequenceStage.Emerging;
            IsRunning = true;
            if (navigation != null)
            {
                navigation.AcquireEventInputLock(this);
                FocusCity(null);
            }
            if (ActiveSequence >= 2) tsunami.StartSequenceTsunami();
            RefreshMenu();
            SetIndividualButtons(false);
        }

        private void Update()
        {
            if (IsRunning) AdvanceSequence(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            // Individual events may finish during a sequence; their buttons stay reserved until its end.
            if (!IsRunning) return;
            SetIndividualButtons(false);
            if (tsunami.IsWaveCameraActive) waveCameraActive = true;
            else if (waveCameraActive) FocusCity(Stage == MonsterSequenceStage.Fire ? monster.Model : null);
        }

        private void AdvanceSequence(float deltaTime)
        {
            if (!IsRunning) return;
            switch (Stage)
            {
                case MonsterSequenceStage.Emerging:
                    if (ActiveSequence == 1)
                    {
                        riseElapsed += Mathf.Max(0f, deltaTime);
                        monster.SetEmergenceTime(riseElapsed);
                    }
                    else if (!tsunami.IsRunning && !monster.HasFinishedEmerging) { StopSequence(); return; }
                    if (!monster.HasFinishedEmerging) return;
                    earthquake.StartSequenceEarthquake();
                    Stage = MonsterSequenceStage.WalkingToFirstPoint;
                    break;
                case MonsterSequenceStage.WalkingToFirstPoint:
                    if (!walk.HasReachedWalkPoint)
                    {
                        if (!earthquake.IsRunning) StopSequence();
                        return;
                    }
                    if (ActiveSequence == 3)
                    {
                        if (earthquake.IsRunning || tsunami.IsRunning) return;
                        fire.StartFire();
                        FocusCity(monster.Model);
                        Stage = MonsterSequenceStage.Fire;
                    }
                    else
                    {
                        walk.BeginAfterEventWalk();
                        Stage = MonsterSequenceStage.WalkingToSecondPoint;
                    }
                    break;
                case MonsterSequenceStage.Fire:
                    if (!walk.HasStartedAfterEventWalk)
                    {
                        if (!fire.IsRunning) StopSequence();
                        return;
                    }
                    Stage = MonsterSequenceStage.WalkingToSecondPoint;
                    break;
                case MonsterSequenceStage.WalkingToSecondPoint:
                    if (departureLimit > 0f && !monsterDeparted)
                    {
                        departureElapsed += Mathf.Max(0f, deltaTime);
                        if (departureElapsed >= departureLimit || !walk.IsWalkingAfterEvent)
                        {
                            walk.StopAfterEventWalk();
                            monster.ResetEmergence();
                            monsterDeparted = true;
                        }
                    }
                    // Hiding the departing boss must not cut short the remaining water/fire effects.
                    if (walk.IsWalkingAfterEvent || earthquake.IsRunning || fire.IsRunning || tsunami.IsRunning) return;
                    IsRunning = false;
                    Stage = MonsterSequenceStage.Complete;
                    ReturnCamera();
                    SetIndividualButtons(true);
                    // Playable rounds snapshot damage and rewards before rebuilding the next turn.
                    if (departureLimit <= 0f && results != null) results.Show();
                    break;
            }
            RefreshMenu();
        }

        public void StopSequence()
        {
            if (results != null) results.Hide();
            if (!IsRunning) return;
            IsRunning = false;
            if (fire != null) fire.StopFire();
            if (earthquake != null) earthquake.StopEarthquake();
            if (tsunami != null) tsunami.StopTsunami();
            if (walk != null) walk.CancelWalk();
            if (monster != null) monster.ResetEmergence();
            ReturnCamera();
            Stage = MonsterSequenceStage.Ready;
            SetIndividualButtons(true);
            RefreshMenu();
        }

        private void OnDisable() => StopSequence();

        private void FocusCity(Transform context)
        {
            if (tsunami.IsWaveCameraActive) return;
            waveCameraActive = false;
            if (navigation != null)
                navigation.BeginEventFrame(this, new[] { tsunami.BuildingsRoot, tsunami.GroundSurface }, context,
                    cameraZoomFactor, cameraZoomSeconds);
        }

        private void ReturnCamera()
        {
            waveCameraActive = false;
            if (navigation == null) return;
            navigation.EndEventFocus(this, cameraReturnSeconds);
            navigation.ReleaseEventInputLock(this);
        }

        private void SetIndividualButtons(bool interactable)
        {
            foreach (var button in individualTestButtons) if (button != null) button.interactable = interactable;
        }

        private void RefreshMenu()
        {
            if (panel != null) panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, expanded ? 220f : 80f);
            if (choices != null) choices.SetActive(expanded);
            if (menuButton != null) menuButton.interactable = !IsRunning;
            if (startButton != null) startButton.interactable = !IsRunning;
            if (stopButton != null) stopButton.interactable = IsRunning;
            foreach (var button in sequenceButtons) if (button != null) button.interactable = !IsRunning;
            if (selectionLabel != null)
                selectionLabel.text = "SEQUENCE " + SelectedSequence + " - " + (SelectedSequence == 1 ? "EARTHQUAKE"
                    : SelectedSequence == 2 ? "TSUNAMI + EARTHQUAKE" : "TSUNAMI + EARTHQUAKE + FIRE") + (expanded ? "  [-]" : "  [+]");
            if (statusLabel != null)
                statusLabel.text = Stage == MonsterSequenceStage.Emerging ? "Emerging from the sea"
                    : Stage == MonsterSequenceStage.WalkingToFirstPoint ? "Walking to point 1 - earthquake"
                    : Stage == MonsterSequenceStage.Fire ? "Breathing fire at point 1"
                    : Stage == MonsterSequenceStage.WalkingToSecondPoint ? walk.IsWalkingAfterEvent ? "Walking to point 2"
                        : fire.IsRunning ? "City fire fading" : "Disaster fading"
                    : Stage == MonsterSequenceStage.Complete ? "Complete - ready to replay" : "Choose a sequence, then press START";
        }
    }
}
