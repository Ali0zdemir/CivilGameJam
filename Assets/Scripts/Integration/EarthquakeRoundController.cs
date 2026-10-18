using System;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam
{
    public enum EarthquakeRoundPhase { Preparation, Earthquake, NextRoundPreparation }

    /// <summary>Playable disaster turns, stage-specific AP rewards and the four-house survival gates.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(300)]
    public sealed class EarthquakeRoundController : MonoBehaviour, IWorkersTutorialRound
    {
        [Serializable]
        private sealed class House
        {
            public HouseCityUpgradeLink link;
            public EarthquakeHouseDamage damage;
            public ApartmentTsunamiCollapse tsunamiDamage;
            [NonSerialized] public bool eligible, protectedAtStart;
        }

        [Header("Saved scene")]
        [SerializeField] private CardGameController game;
        [SerializeField] private AbilityPoints abilityPoints;
        [SerializeField] private MonsterSequenceController sequence;
        [SerializeField] private EarthquakeTestController earthquake;
        [SerializeField] private TsunamiTestController tsunami;
        [SerializeField] private FireTestController fire;
        [SerializeField] private CityCameraController navigation;
        [SerializeField] private HouseUpgradePresentation upgradePresentation;
        [SerializeField] private CardDock dock;
        [SerializeField] private House[] houses = Array.Empty<House>();
        [Header("Turn rewards and transition")]
        [SerializeField, Min(0)] private int pointsPerProtectedHouse = 3;
        [SerializeField, Min(0)] private int tsunamiPointsPerProtectedHouse = 6;
        [SerializeField, Min(0)] private int firePointsPerProtectedHouse = 10;
        [Tooltip("Maximum boss walking time after the earthquake. Debug routes are unaffected.")]
        [SerializeField, Min(.1f)] private float bossDepartureSeconds = 10f;
        [SerializeField, Min(0)] private int nextRoundPoints = 10;
        [Tooltip("Bonus on entering Tsunami and every preparation turn after clearing the earthquake.")]
        [SerializeField, Min(0)] private int afterEarthquakeRoundPoints = 6;
        [Tooltip("Permanent additional normal people per turn after clearing the earthquake.")]
        [SerializeField, Min(0)] private int earthquakePeopleReward = 1;
        [SerializeField, Range(.1f, .85f)] private float damageAtEarthquakeProgress = .45f;
        [Tooltip("Time from the first house collapse until the next preparation turn.")]
        [SerializeField, Min(0f)] private float nextTurnAfterCollapseSeconds = 2.5f;
        [Header("Authored controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private AbilityPointFeedback abilityFeedback;
        [SerializeField] private TMP_Text roundLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button[] debugStartButtons = Array.Empty<Button>();
        private bool[] debugButtonStates;
        private bool damageApplied;
        private float collapseElapsed = -1f;
        private EarthquakeRoundPhase previousPreparation;
        private bool rebuildAfterResults;
        private UpgradeKind upgradesToDiscard;
        private int pendingTransitionPoints;
        private int pendingPeopleReward;
        public int RoundNumber { get; private set; } = 1;
        public int SequenceNumber { get; private set; } = 1;
        public EarthquakeRoundPhase Phase { get; private set; }
        public bool LastRoundSurvived { get; private set; }
        public int LastProtectedHouseCount { get; private set; }
        public int LastSurvivalReward { get; private set; }
        public int LastRoundGrant { get; private set; }
        public float NextTurnAfterCollapseSeconds => Mathf.Max(0f, nextTurnAfterCollapseSeconds);
        public CardGameController Owner => game;
        public Button ReadyButton => readyButton;
        public bool IsRoundActive => Phase == EarthquakeRoundPhase.Earthquake;
        public bool IsResultsVisible => sequence != null && sequence.Results != null && sequence.Results.IsVisible;
        public RectTransform ResultsRewardArea => sequence != null && sequence.Results != null ? sequence.Results.EarnedPointsArea : null;
        public event Action TurnStarted;
        public void ExplainNextRoundResult()
        {
            if (sequence != null && sequence.Results != null) sequence.Results.ExplainNextRoundResult();
        }
        public bool IsUpgradePresentationActive
        {
            get
            {
                if (upgradePresentation != null && upgradePresentation.IsPresenting || navigation != null && navigation.IsEventCameraActive) return true;
                foreach (var house in houses)
                    if (house.link != null && (house.link.RailSystem != null && house.link.RailSystem.IsTransitioning ||
                        house.link.FloodProtection != null && house.link.FloodProtection.IsTransitioning)) return true;
                return false;
            }
        }
        public bool CanReady
        {
            get
            {
                if (rebuildAfterResults || Phase == EarthquakeRoundPhase.Earthquake || game == null || abilityPoints == null || sequence == null || earthquake == null ||
                    (game.IsTutorialActive && !game.TutorialAllowsReady) || game.IsChoosingBlueprint || game.IsDraggingAnyCard || game.HasActiveCardAnimation || sequence.IsRunning || earthquake.IsRunning ||
                    tsunami != null && tsunami.IsRunning || fire != null && fire.IsRunning || navigation != null && navigation.IsEventCameraActive ||
                    upgradePresentation != null && upgradePresentation.IsPresenting) return false;
                foreach (var card in game.Cards)
                {
                    if (card.IsFlying) return false;
                    if (card.Station != null && card.Station.HasActiveWork) return false;
                    var factory = card.GetComponent<FactoryProduction>();
                    if (factory != null && factory.PendingBlueprint != null && factory.WorkRate > 0f) return false;
                }
                foreach (var house in houses)
                    if (house.link != null && (house.link.RailSystem != null && house.link.RailSystem.IsTransitioning ||
                        house.link.FloodProtection != null && house.link.FloodProtection.IsTransitioning)) return false;
                return true;
            }
        }

        private void Start()
        {
            ApplySequencePrices();
            Refresh();
        }

        private void OnEnable()
        {
            if (game != null) game.RestrictResearchTo(SequenceNumber == 1 ? UpgradeKind.RailSystem : UpgradeKind.None);
            if (sequence != null && sequence.Results != null)
            {
                sequence.Results.Dismissed += RestoreCityAfterResults;
                if (rebuildAfterResults && !sequence.Results.IsVisible) RestoreCityAfterResults();
            }
        }

        private UpgradeKind CurrentProtection => SequenceNumber == 1 ? UpgradeKind.RailSystem
            : SequenceNumber == 2 ? UpgradeKind.Breakwater : UpgradeKind.Fire;

        private void ApplySequencePrices()
        {
            if (game == null) return;
            // The first disaster must be cleared before researching other protection.
            game.RestrictResearchTo(SequenceNumber == 1 ? UpgradeKind.RailSystem : UpgradeKind.None);
            foreach (var card in game.Cards) card.Station?.UseSequencePrices(SequenceNumber);
        }

        public void Ready()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !CanReady) return;
            SnapshotHouses();
            game.CloseInspection();
            debugButtonStates = new bool[debugStartButtons.Length];
            for (int i = 0; i < debugStartButtons.Length; i++)
                if (debugStartButtons[i] != null) debugButtonStates[i] = debugStartButtons[i].interactable;
            abilityPoints.SetActionsLocked(true);
            damageApplied = false;
            collapseElapsed = -1f;
            sequence.SelectSequence(SequenceNumber);
            sequence.StartRoundSequence(bossDepartureSeconds, SequenceNumber == 1 && AllHousesProtected());
            if (!sequence.IsRunning) { abilityPoints.SetActionsLocked(false); RestoreDebugButtons(); return; }
            previousPreparation = Phase;
            Phase = EarthquakeRoundPhase.Earthquake;
            TurnStarted?.Invoke();
            if (dock != null) dock.BeginCityPresentation();
            Refresh();
        }

        private void Update() => AdvanceRound(Time.unscaledDeltaTime);

        public void AdvanceRound(float seconds)
        {
            if (Phase == EarthquakeRoundPhase.Earthquake)
            {
                // The existing STOP control cancels a turn without paying rewards or refreshing people.
                if (!sequence.IsRunning && sequence.Stage != MonsterSequenceStage.Complete) CancelRound();
                else if (sequence.Stage == MonsterSequenceStage.AwaitingTsunami && SequenceNumber == 1)
                {
                    if (AllHousesProtected() && DamageFinished()) CompleteRound(true, true);
                    else { sequence.FinishRoundPresentation(); CompleteRound(false); }
                }
                else
                {
                    foreach (var house in houses)
                        if (house.eligible && (house.tsunamiDamage != null && house.tsunamiDamage.HasCollapsed ||
                            house.damage != null && house.damage.UsesAuthoredAnimation && house.damage.IsDestroyed))
                            house.link.House.SetDestroyed(true);
                    if (!damageApplied && (earthquake.IsRunning && earthquake.NormalizedProgress >= damageAtEarthquakeProgress ||
                        sequence.Stage == MonsterSequenceStage.WalkingToSecondPoint || sequence.Stage == MonsterSequenceStage.Complete)) ApplyDamage();
                    bool collapsed = HasCollapsedHouse();
                    if (collapsed)
                    {
                        if (collapseElapsed < 0f) collapseElapsed = 0f;
                        else collapseElapsed += Mathf.Max(0f, seconds);
                        if (collapseElapsed >= NextTurnAfterCollapseSeconds && DamageFinished())
                        {
                            sequence.FinishRoundPresentation();
                            CompleteRound(false);
                        }
                    }
                    else if (!sequence.IsRunning && DamageFinished() && (navigation == null || !navigation.IsEventCameraActive))
                        CompleteRound(AllHousesProtected());
                }
            }
            Refresh();
        }

        private void LateUpdate()
        {
            if (Phase != EarthquakeRoundPhase.Earthquake) return;
            foreach (var button in debugStartButtons) if (button != null) button.interactable = false;
        }

        private void RestoreDebugButtons()
        {
            if (debugButtonStates == null) return;
            for (int i = 0; i < debugStartButtons.Length; i++)
                if (debugStartButtons[i] != null) debugStartButtons[i].interactable = debugButtonStates[i];
            debugButtonStates = null;
        }

        private void ApplyDamage()
        {
            damageApplied = true;
            foreach (var house in houses)
                // Later saved routes also shake the city; they use seismic protection, not the current stage's card.
                if (house.eligible && !house.link.House.IsDestroyed && house.link.House.GetInstalled(UpgradeKind.RailSystem) == null &&
                    house.damage != null && !house.damage.UsesAuthoredAnimation && house.damage.Collapse())
                    house.link.House.SetDestroyed(true);
        }

        private bool HasCollapsedHouse()
        {
            foreach (var house in houses)
                if (house.eligible && house.link.House.IsDestroyed) return true;
            return false;
        }

        private bool AllHousesProtected()
        {
            if (houses.Length == 0) return false;
            foreach (var house in houses)
                if (!house.eligible || !house.protectedAtStart || house.link.House.IsDestroyed) return false;
            return true;
        }

        private void SnapshotHouses()
        {
            foreach (var house in houses)
            {
                house.eligible = house.link != null && house.link.House != null && !house.link.House.IsDestroyed;
                house.protectedAtStart = house.eligible && house.link.House.GetInstalled(CurrentProtection) != null;
            }
        }

        private bool DamageFinished()
        {
            foreach (var house in houses)
                if (house.damage != null && house.damage.IsPlaying || house.tsunamiDamage != null && house.tsunamiDamage.IsPlaying) return false;
            return sequence == null || sequence.PresentationFinished;
        }

        private void ResetHouseDamage()
        {
            foreach (var house in houses)
            {
                if (house.damage != null) house.damage.ResetDamage();
                if (house.tsunamiDamage != null) house.tsunamiDamage.ResetCollapse();
                if (house.link != null && house.link.House != null) house.link.House.SetDestroyed(false);
            }
        }

        private void CompleteRound(bool survived, bool continueToTsunami = false)
        {
            LastRoundSurvived = survived;
            int destroyedHouses = 0, totalHouses = 0;
            foreach (var house in houses)
                if (house.link != null && house.link.House != null)
                {
                    totalHouses++;
                    if (house.link.House.IsDestroyed || sequence.WasHouseBurnt(house.link.Building)) destroyedHouses++;
                }
            LastProtectedHouseCount = 0;
            foreach (var house in houses)
                if (house.eligible && house.protectedAtStart && !house.link.House.IsDestroyed) LastProtectedHouseCount++;
            LastSurvivalReward = LastProtectedHouseCount * Mathf.Max(0,
                SequenceNumber == 3 ? firePointsPerProtectedHouse : SequenceNumber == 2 ? tsunamiPointsPerProtectedHouse : pointsPerProtectedHouse);
            // A failed stage loses all of its manufactured products, preserving earlier protection.
            upgradesToDiscard = !survived ? CurrentProtection : UpgradeKind.None;
            game.EndBlueprintTurn(discardNewDiscoveries: !survived && SequenceNumber == 1);
            if (survived && SequenceNumber == 1)
            {
                int reward = Mathf.Max(0, earthquakePeopleReward);
                game.AwardPeoplePerTurn(reward);
                pendingPeopleReward += reward;
            }
            if (survived && SequenceNumber < 3)
            {
                game.CompleteBlueprint(CurrentProtection);
                SequenceNumber++;
            }
            LastRoundGrant = Mathf.Max(0, SequenceNumber >= 2 ? afterEarthquakeRoundPoints : nextRoundPoints);
            sequence.SelectSequence(SequenceNumber);
            ApplySequencePrices();
            RoundNumber++;
            if (!continueToTsunami) Phase = EarthquakeRoundPhase.NextRoundPreparation;
            var awardedPoints = LastSurvivalReward + LastRoundGrant;
            game.ResetTurnPeople();
            if (continueToTsunami)
            {
                // Seismic survival is recorded now, but its AP waits for the uninterrupted tsunami's report.
                pendingTransitionPoints += awardedPoints;
                awardedPoints = 0;
                ResetHouseDamage();
                if (fire != null) fire.StopFire();
                // This is a new water-stage turn in the same cinematic. STOP preserves the earthquake win.
                previousPreparation = EarthquakeRoundPhase.NextRoundPreparation;
                damageApplied = false;
                collapseElapsed = -1f;
                SnapshotHouses();
                if (sequence.ContinueWithTsunami()) return;
                Phase = EarthquakeRoundPhase.NextRoundPreparation;
            }
            awardedPoints += pendingTransitionPoints;
            pendingTransitionPoints = 0;
            abilityPoints.AddPoints(awardedPoints);
            rebuildAfterResults = true;
            sequence.ShowRoundResults(destroyedHouses, totalHouses, awardedPoints, LastProtectedHouseCount);
            sequence.Results?.ShowPeopleReward(pendingPeopleReward, game.PeoplePerTurn);
            pendingPeopleReward = 0;
            if (sequence.Results == null || !sequence.Results.IsVisible) RestoreCityAfterResults();
            abilityPoints.SetActionsLocked(false);
            RestoreDebugButtons();
            if (dock != null) dock.EndCityPresentation();
        }

        private void CancelRound()
        {
            ResetHouseDamage();
            Phase = previousPreparation;
            damageApplied = false;
            collapseElapsed = -1f;
            abilityPoints.SetActionsLocked(false);
            RestoreDebugButtons();
            if (dock != null) dock.EndCityPresentation();
        }

        private void RestoreCityAfterResults()
        {
            if (!rebuildAfterResults) return;
            rebuildAfterResults = false;
            if (upgradesToDiscard != UpgradeKind.None) game.DiscardUpgrades(upgradesToDiscard);
            upgradesToDiscard = UpgradeKind.None;
            ResetHouseDamage();
            if (fire != null) fire.StopFire();
            Refresh();
        }

        private void OnDisable()
        {
            if (game != null) game.RestrictResearchTo(UpgradeKind.None);
            if (sequence != null && sequence.Results != null) sequence.Results.Dismissed -= RestoreCityAfterResults;
            abilityFeedback?.SetReadyPrompt(false);
            if (Phase != EarthquakeRoundPhase.Earthquake) return;
            if (sequence != null) sequence.StopSequence();
            CancelRound();
        }

        private void Refresh()
        {
            bool canReady = CanReady;
            if (readyButton != null) readyButton.interactable = canReady;
            abilityFeedback?.SetReadyPrompt(canReady && abilityPoints != null && abilityPoints.CurrentPoints == 0);
            if (roundLabel != null) roundLabel.SetText("{0}", RoundNumber);
            if (statusLabel != null) statusLabel.text = Phase == EarthquakeRoundPhase.Earthquake ? string.Empty
                : Phase == EarthquakeRoundPhase.NextRoundPreparation ? "Sequence " + SequenceNumber + "  |  " + LastProtectedHouseCount + " safe houses  +" + LastSurvivalReward + " AP  |  Round bonus  +" + LastRoundGrant + " AP"
                : CanReady ? "Spend AP, protect your houses, then press READY." : string.Empty;
        }
    }
}
