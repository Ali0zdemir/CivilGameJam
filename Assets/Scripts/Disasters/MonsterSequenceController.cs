using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Disasters
{
    public enum MonsterSequenceStage { Ready, Emerging, WalkingToFirstPoint, Fire, WalkingToSecondPoint, Complete, Roaring, Earthquake, AwaitingTsunami, Settling }

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

        [Header("Offscreen earthquake")]
        [SerializeField] private AudioSource earthquakeRoar;
        [Tooltip("Play the roar this many seconds after the offscreen earthquake starts.")]
        [SerializeField, Min(0f)] private float earthquakeRoarDelay = 2f;
        [Tooltip("Show the boss tsunami result this many seconds after the last affected house finishes collapsing.")]
        [SerializeField, Min(0f)] private float tsunamiAftermathSeconds = 1f;

        [Header("Sequence camera")]
        [SerializeField, Range(1f, 1.6f)] private float cameraZoomFactor = 1.3f;
        [SerializeField, Min(0.1f)] private float cameraZoomSeconds = 1.8f;
        [SerializeField, Min(0.1f)] private float cameraReturnSeconds = 0.8f;

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
        private float departureLimit, departureElapsed;
        private bool monsterDeparted;
        private bool expanded;
        private bool waveCameraActive;
        private bool offscreenEarthquake;
        private bool continueWithTsunami;
        private bool roarPlayed;
        private bool tsunamiAftermathStarted;
        private float tsunamiAftermathElapsed;

        public bool IsRunning { get; private set; }
        public int SelectedSequence => Mathf.Clamp(selectedSequence, 1, 3);
        public int ActiveSequence { get; private set; }
        public MonsterSequenceStage Stage { get; private set; }
        public bool HouseEffectsFinished => results == null || results.HouseEffectsFinished;
        public bool IsEarthquakeRoarPlaying => offscreenEarthquake && roarPlayed && earthquakeRoar != null && earthquakeRoar.isPlaying;
        public float TsunamiAftermathSeconds => Mathf.Max(0f, tsunamiAftermathSeconds);
        private bool HasFinishedTsunamiAftermath => ActiveSequence != 2 || tsunamiAftermathElapsed + .0001f >= TsunamiAftermathSeconds;
        private bool TsunamiCollapsesFinished => tsunami.HasFinishedHouseCollapses && (results == null || results.HouseCollapseAnimationsFinished);
        public bool PresentationFinished => (ActiveSequence == 2 && TsunamiCollapsesFinished || HouseEffectsFinished) &&
            !IsEarthquakeRoarPlaying && HasFinishedTsunamiAftermath && (ActiveSequence <= 2 || !tsunami.IsRunning);
        public MonsterSequenceResults Results => results;

        public bool WasHouseBurnt(Transform house) => fire != null && fire.WasHouseBurnt(house);

        public void ShowRoundResults(int destroyed, int total, int earnedPoints, int protectedHouses = -1)
        {
            if (results != null) results.ShowRound(destroyed, total, earnedPoints, protectedHouses);
        }

        private void Awake()
        {
            if (results != null) results.Dismissed += RestoreDebugCityAfterResults;
            RefreshMenu();
        }

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
        public void StartRoundSequence(float maximumDepartureSeconds, bool tsunamiAfterEarthquake = false) =>
            StartSequence(Mathf.Max(.1f, maximumDepartureSeconds), tsunamiAfterEarthquake);

        private void StartSequence(float maximumDepartureSeconds, bool tsunamiAfterEarthquake = false)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsRunning) return;
            if (monster == null || walk == null || walk.WalkPoint == null || walk.AfterEventPoint == null ||
                tsunami == null || earthquake == null || fire == null) return;
            CityLayoutEvents.PrepareForDisaster();
            if (results != null) results.Hide();
            fire.StopFire();
            earthquake.StopEarthquake();
            tsunami.StopTsunami();
            walk.CancelWalk();
            monster.ResetEmergence();
            if (earthquakeRoar != null) earthquakeRoar.Stop();
            ActiveSequence = SelectedSequence;
            offscreenEarthquake = ActiveSequence == 1;
            continueWithTsunami = offscreenEarthquake && tsunamiAfterEarthquake;
            roarPlayed = false;
            tsunamiAftermathStarted = false;
            tsunamiAftermathElapsed = 0f;
            departureElapsed = 0f;
            departureLimit = maximumDepartureSeconds;
            monsterDeparted = false;
            expanded = false;
            Stage = offscreenEarthquake ? MonsterSequenceStage.Earthquake : MonsterSequenceStage.Emerging;
            IsRunning = true;
            if (navigation != null)
            {
                navigation.AcquireEventInputLock(this);
                FocusCity(null);
            }
            if (offscreenEarthquake)
            {
                // Match the earthquake to the original audio instead of stretching the roar.
                var synchronizedDuration = earthquakeRoar != null && earthquakeRoar.clip != null
                    ? Mathf.Max(0f, earthquakeRoarDelay) + earthquakeRoar.clip.length : 0f;
                earthquake.StartOffscreenEarthquake(synchronizedDuration);
                if (!earthquake.IsRunning) { StopSequence(); return; }
            }
            if (ActiveSequence >= 2) tsunami.StartSequenceTsunami(this);
            RefreshMenu();
            SetIndividualButtons(false);
        }

        private void Update()
        {
            if (Time.timeScale > 0f && IsRunning) AdvanceSequence(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            // Individual events may finish during a sequence; their buttons stay reserved until its end.
            if (!IsRunning) return;
            SetIndividualButtons(false);
            if (tsunami.IsWaveCameraActive) waveCameraActive = true;
            else if (waveCameraActive)
            {
                waveCameraActive = false;
                if (!tsunami.HasSequenceCameraOwner) FocusCity(Stage == MonsterSequenceStage.Fire ? monster.Model : null);
            }
        }

        private void AdvanceSequence(float deltaTime)
        {
            if (!IsRunning) return;
            if (ActiveSequence == 2)
            {
                if (TsunamiCollapsesFinished ||
                    tsunami.HasReachedCityEnd && !earthquake.IsRunning && HouseEffectsFinished)
                {
                    if (tsunamiAftermathStarted) tsunamiAftermathElapsed += Mathf.Max(0f, deltaTime);
                    else
                    {
                        tsunamiAftermathStarted = true;
                        tsunami.BeginSequenceRecession(TsunamiAftermathSeconds);
                    }
                }
                else { tsunamiAftermathStarted = false; tsunamiAftermathElapsed = 0f; }
                // A tsunami report follows the final collapse, independently of the boss's remaining route.
                if (HasFinishedTsunamiAftermath)
                {
                    FinishRoundPresentation();
                    return;
                }
            }
            switch (Stage)
            {
                case MonsterSequenceStage.Earthquake:
                    if (!roarPlayed && earthquake.IsRunning && earthquake.ElapsedSeconds >= Mathf.Max(0f, earthquakeRoarDelay))
                    {
                        roarPlayed = true;
                        if (earthquakeRoar != null && earthquakeRoar.clip != null)
                        {
                            earthquakeRoar.pitch = 1f;
                            earthquakeRoar.Play();
                        }
                    }
                    if (earthquake.IsRunning) return;
                    if (!earthquake.HasCompleted) { StopSequence(); return; }
                    if (!PresentationFinished) return;
                    // The playable round records seismic survival and its AP before water can damage a house.
                    if (continueWithTsunami) { Stage = MonsterSequenceStage.AwaitingTsunami; break; }
                    CompleteSequence();
                    break;
                case MonsterSequenceStage.Emerging:
                    if (!tsunami.IsRunning && !monster.HasFinishedEmerging) { StopSequence(); return; }
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
                    CompleteSequence();
                    break;
                case MonsterSequenceStage.Settling:
                    CompleteSequence();
                    break;
            }
            RefreshMenu();
        }

        public void StopSequence()
        {
            tsunamiAftermathStarted = false;
            tsunamiAftermathElapsed = 0f;
            if (earthquakeRoar != null) earthquakeRoar.Stop();
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

        private void OnDestroy()
        {
            if (results != null) results.Dismissed -= RestoreDebugCityAfterResults;
        }

        private void RestoreDebugCityAfterResults()
        {
            if (IsRunning || departureLimit > 0f) return;
            if (earthquake != null) earthquake.StopEarthquake();
            if (tsunami != null) tsunami.StopTsunami();
            if (fire != null) fire.StopFire();
        }

        public void FinishRoundPresentation()
        {
            if (earthquake != null) earthquake.FinishPresentation();
            if (tsunami != null) tsunami.FinishPresentation();
            if (fire != null) fire.FinishPresentation();
            if (walk != null) walk.CancelWalk();
            if (monster != null) monster.ResetEmergence();
            CompleteSequence();
            RefreshMenu();
        }

        public bool ContinueWithTsunami()
        {
            if (!IsRunning || Stage != MonsterSequenceStage.AwaitingTsunami) return false;
            continueWithTsunami = false;
            offscreenEarthquake = false;
            selectedSequence = ActiveSequence = 2;
            tsunamiAftermathStarted = false;
            tsunamiAftermathElapsed = 0f;
            if (earthquakeRoar != null) earthquakeRoar.Stop();
            Stage = MonsterSequenceStage.Emerging;
            tsunami.StartSequenceTsunami(this);
            if (!tsunami.IsRunning) { StopSequence(); return false; }
            RefreshMenu();
            return true;
        }

        private void CompleteSequence()
        {
            if (!PresentationFinished)
            {
                Stage = MonsterSequenceStage.Settling;
                return;
            }
            IsRunning = false;
            Stage = MonsterSequenceStage.Complete;
            if (earthquakeRoar != null) earthquakeRoar.Stop();
            ReturnCamera();
            SetIndividualButtons(true);
            // Playable rounds snapshot damage and rewards before rebuilding the next turn.
            if (departureLimit <= 0f && results != null) results.Show();
        }

        private void FocusCity(Transform context)
        {
            if (tsunami.IsWaveCameraActive) return;
            waveCameraActive = false;
            if (navigation == null) return;
            if (offscreenEarthquake || ActiveSequence == 2 && tsunami.HasReachedCityEnd)
                navigation.BeginEventFocus(this, tsunami.transform.TransformPoint(tsunami.CityBounds.center), 1f, cameraZoomSeconds);
            else
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
                statusLabel.text = Stage == MonsterSequenceStage.Earthquake ? "Earthquake"
                    : Stage == MonsterSequenceStage.AwaitingTsunami ? "City protected"
                    : Stage == MonsterSequenceStage.Settling ? "City settling"
                    : Stage == MonsterSequenceStage.Emerging ? "Emerging from the sea"
                    : Stage == MonsterSequenceStage.WalkingToFirstPoint ? "Walking to point 1 - earthquake"
                    : Stage == MonsterSequenceStage.Fire ? "Breathing fire at point 1"
                    : Stage == MonsterSequenceStage.WalkingToSecondPoint ? walk.IsWalkingAfterEvent ? "Walking to point 2"
                        : fire.IsRunning ? "City fire fading" : "Disaster fading"
                    : Stage == MonsterSequenceStage.Complete ? "Complete - ready to replay" : "Choose a sequence, then press START";
        }
    }
}
