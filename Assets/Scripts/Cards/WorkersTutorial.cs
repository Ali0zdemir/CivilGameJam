using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public interface IWorkersTutorialRound
    {
        CardGameController Owner { get; }
        Button ReadyButton { get; }
        bool IsUpgradePresentationActive { get; }
        bool IsRoundActive { get; }
        bool IsResultsVisible { get; }
        RectTransform ResultsRewardArea { get; }
        int LastProtectedHouseCount { get; }
        int LastSurvivalReward { get; }
        int LastRoundGrant { get; }
        void ExplainNextRoundResult();
        event Action TurnStarted;
    }

    /// <summary>New-game lessons, using only the UI already saved under Canvas/WorkersTutorial.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(350)]
    public sealed class WorkersTutorial : MonoBehaviour, IPointerClickHandler
    {
        public const string CompletionPreferenceKey = "CivilGameJam.Tutorial.Workers.v5";

        [Serializable]
        private sealed class Lesson
        {
            public string chapter;
            public string title;
            [TextArea(3, 6)] public string description;
            [TextArea(1, 3)] public string note;
        }

        [Header("Startup")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private bool rememberCompletion = true;
        [SerializeField, Min(.05f)] private float fadeSeconds = .28f;
        [Header("Existing game")]
        [SerializeField] private CardGameController game;
        [SerializeField] private CardDock dock;
        [SerializeField] private BuildingStation school;
        [SerializeField] private BuildingStation research;
        [SerializeField] private BuildingStation factory;
        [Header("Saved tutorial UI")]
        [SerializeField] private CanvasGroup layer;
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup copy;
        [SerializeField] private TutorialSpotlight spotlight;
        [SerializeField] private TMP_Text chapter;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text note;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private TMP_Text budget;
        [SerializeField] private TMP_Text schoolPrice;
        [SerializeField] private TMP_Text comparison;
        [SerializeField] private string comparisonTemplate = "RESEARCH   {normalResearch} AP  >  {educatedResearch} AP\nFACTORY     {normalFactory} AP  >  {educatedFactory} AP";
        [SerializeField] private Button next;
        [SerializeField] private Button back;
        [SerializeField] private Button skip;
        [SerializeField] private GameObject[] showcases;
        [SerializeField] private Image[] dots;
        [SerializeField] private Image schoolFrame;
        [SerializeField] private RectTransform trainingSpark;
        [SerializeField] private Lesson[] lessons;
        [Header("Hands-on school lesson")]
        [SerializeField, TextArea(2, 4)] private string trainingDescription =
            "This person is learning at School.\nWait for the educated person to return. This lesson will continue automatically.";
        [SerializeField] private string trainingNote = "No extra AP is spent while training.";
        private Color schoolFrameColor;
        private float visibility, copyVisibility, elapsed;
        private bool closing, ownsGameLock, ownsDockState, dockWasEnabled, dockWasExpanded, awaitingResults;
        private GameObject previousSelection;
        private DraggableCard practiceStudent, practiceGraduate;
        private WorkerSlot[][] stationSlots;
        private BuildingStation[] stations;
        private FactoryProduction production;
        private DraggableCard practiceBlueprint, factoryWorker;
        private bool producedBlueprint;
        [Header("Research and production lessons")]
        [SerializeField, TextArea(2, 4)] private string researchingDescription =
            "Research is creating a blueprint.\nWait for the blueprint choices to appear.";
        [SerializeField, TextArea(2, 4)] private string blueprintArrivalDescription =
            "Your seismic blueprint is moving to the Vault.\nWait for it to arrive, then drag it to Factory.";
        [SerializeField, TextArea(2, 4)] private string producingDescription =
            "Factory is producing the physical seismic upgrades.\nWait for your two items to arrive.";
        private string authoredTitle, authoredDescription;
        private IWorkersTutorialRound round;
        private readonly List<HouseUpgrades> houses = new List<HouseUpgrades>();
        private readonly List<DraggableCard> practiceProducts = new List<DraggableCard>();
        private float boardVisibility = 1f;


        public bool IsOpen { get; private set; }
        public int CurrentStep { get; private set; }
        public int StepCount => lessons == null ? 0 : lessons.Length;
        public TutorialSpotlight Spotlight => spotlight;
        public string DisplayedDescription => description != null ? description.text : string.Empty;
        public bool EducationCompleted { get; private set; }
        public bool ProductionCompleted { get; private set; }
        public bool WaitingForEducation => IsOpen && !closing && CurrentStep == 1 && !EducationCompleted;
        public bool WaitingForResearch => IsOpen && !closing && CurrentStep == 3;
        public DraggableCard PracticeStudent => practiceStudent;
        public DraggableCard PracticeGraduate => practiceGraduate;
        public DraggableCard PracticeBlueprint => practiceBlueprint;
        public DraggableCard FactoryWorker => factoryWorker;
        public IReadOnlyList<DraggableCard> PracticeProducts => practiceProducts;
        public bool IsWatchingCity => IsOpen && CurrentStep == 8 && UpgradeAnimationsActive;
        public bool AllowsReady => IsOpen && !closing && CurrentStep == 9 && AllProductsInstalled && !UpgradeAnimationsActive;
        public bool KeepsBoardOpen => IsOpen && !closing && (CurrentStep < 8 || CurrentStep == 8 && !IsWatchingCity || CurrentStep == 9);
        private bool RequiredAction => WaitingForEducation || CurrentStep >= 3 && CurrentStep <= 6 || CurrentStep == 8 || CurrentStep == 9;
        private bool UpgradeAnimationsActive
        {
            get
            {
                if (round != null && round.IsUpgradePresentationActive) return true;
                foreach (var house in houses) if (house.IsInstalling) return true;
                return false;
            }
        }
        private bool AllProductsInstalled
        {
            get
            {
                if (practiceProducts.Count < 2) return false;
                foreach (var product in practiceProducts)
                    if (product == null || product.InstalledIn == null || !houses.Contains(product.InstalledIn)) return false;
                return true;
            }
        }

        public bool AllowsCardInteraction(DraggableCard card)
        {
            if (!IsOpen || closing || card == null || card.IsFlying) return false;
            if (CurrentStep == 8) return !IsWatchingCity && practiceProducts.Contains(card) && card.InstalledIn == null;
            if (CurrentStep == 5) return card == practiceBlueprint;
            var worker = WaitingForEducation ? practiceStudent : CurrentStep == 3 ? practiceGraduate : CurrentStep == 6 ? factoryWorker : null;
            return card == worker && worker.CanStartWork && worker.AssignedTo == null;
        }

        public bool AllowsAssignment(BuildingStation station, DraggableCard worker) =>
            AllowsCardInteraction(worker) && (WaitingForEducation && station == school ||
                CurrentStep == 3 && station == research || CurrentStep == 6 && station == factory && production.PendingBlueprint == practiceBlueprint);

        public bool AllowsBlueprintSelection(UpgradeKind kind) => IsOpen && !closing && CurrentStep == 4 && kind == UpgradeKind.RailSystem;
        public bool AllowsBlueprintInsertion(FactoryProduction target, DraggableCard blueprint) =>
            IsOpen && !closing && CurrentStep == 5 && target == production && blueprint != null && blueprint == practiceBlueprint;
        public bool AllowsUpgradeInstallation(HouseUpgrades house, DraggableCard upgrade) =>
            IsOpen && !closing && CurrentStep == 8 && !IsWatchingCity && houses.Contains(house) && practiceProducts.Contains(upgrade);

        private void Awake()
        {
            authoredTitle = title != null ? title.text : string.Empty;
            authoredDescription = description != null ? description.text : string.Empty;
            if (schoolFrame != null) schoolFrameColor = schoolFrame.color;
            stations = new[] { school, research, factory };
            stationSlots = new WorkerSlot[stations.Length][];
            for (int i = 0; i < stations.Length; i++) stationSlots[i] = stations[i] != null
                ? stations[i].GetComponentsInChildren<WorkerSlot>(true) : Array.Empty<WorkerSlot>();
            if (factory != null) production = factory.GetComponent<FactoryProduction>();
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour is IWorkersTutorialRound service && service.Owner == game) { round = service; break; }
            if (layer != null) { layer.alpha = 0f; layer.gameObject.SetActive(false); }
        }

        private void Start()
        {
            // Completing or skipping only ends this session's guide. A new game
            // always starts the authored tutorial again, even with an older save.
            if (showOnStart) ShowTutorial();
        }

        public void ShowTutorial()
        {
            if (IsOpen || game == null || game.AbilityPoints == null || game.ActionsLocked || game.IsChoosingBlueprint ||
                game.IsDraggingAnyCard || layer == null || spotlight == null || StepCount < 10 || school == null || research == null || factory == null ||
                production == null || panel == null || next == null || round == null || round.ReadyButton == null) return;
            game.CloseInspection();
            ownsGameLock = true;
            game.AbilityPoints.SetActionsLocked(true);
            if (dock != null)
            {
                ownsDockState = true;
                dockWasEnabled = dock.enabled; dockWasExpanded = dock.IsExpanded;
                dock.enabled = false; dock.SetExpanded(true, true);
            }
            IsOpen = true; closing = false; visibility = elapsed = 0f;
            EducationCompleted = ProductionCompleted = producedBlueprint = false;
            practiceProducts.Clear(); houses.Clear(); boardVisibility = 1f;
            foreach (var card in game.Cards)
                if (card.Kind == CardKind.House && card.TryGetComponent<HouseUpgrades>(out var house)) houses.Add(house);
            practiceStudent = practiceGraduate = practiceBlueprint = factoryWorker = null;
            game.SetWorkersTutorial(this);
            game.PersonEducated += OnPersonEducated;
            game.BlueprintChoiceOpened += OnBlueprintChoiceOpened;
            game.BlueprintDiscovered += OnBlueprintDiscovered;
            game.BlueprintProduced += OnBlueprintProduced;
            round.TurnStarted += OnTurnStarted;
            practiceStudent = FindStudent();
            layer.gameObject.SetActive(true); layer.interactable = layer.blocksRaycasts = true;
            if (EventSystem.current != null) previousSelection = EventSystem.current.currentSelectedGameObject;
            ShowStep(0);
        }

        private void ShowStep(int index)
        {
            CurrentStep = Mathf.Clamp(index, 0, StepCount - 1);
            var lesson = lessons[CurrentStep];
            if (chapter != null) chapter.text = (CurrentStep + 1).ToString("00") + " / " + StepCount.ToString("00") + "   " + lesson.chapter;
            if (title != null) title.text = CurrentStep == 0 ? authoredTitle : lesson.title;
            if (description != null) description.text = Format(CurrentStep == 0 ? authoredDescription : lesson.description);
            if (note != null) note.text = Format(lesson.note);
            if (CurrentStep == 1 && EducationCompleted && description != null)
                description.text = Format("School educated this person for {school} AP.\nThey are ready for one job this turn.");
            if (nextLabel != null) nextLabel.text = "CONTINUE";
            if (back != null) { back.gameObject.SetActive(CurrentStep == 2); back.interactable = CurrentStep == 2; }
            next.interactable = !RequiredAction;
            if (skip != null) skip.interactable = true;
            if (budget != null) budget.text = game.AbilityPoints.CurrentPoints + " AP";
            if (schoolPrice != null) schoolPrice.text = "SCHOOL  -" + school.AbilityPointCost(CardKind.Person) + " AP";
            if (comparison != null) comparison.text = Format(comparisonTemplate);
            if (showcases != null) for (int i = 0; i < showcases.Length; i++) if (showcases[i] != null) showcases[i].SetActive(i == CurrentStep);
            if (dots != null) for (int i = 0; i < dots.Length; i++) if (dots[i] != null) dots[i].color = i == CurrentStep
                ? new Color32(244, 197, 121, 255) : i < CurrentStep ? new Color32(126, 181, 155, 255) : new Color32(62, 72, 84, 255);
            copyVisibility = 0f;
            if (WaitingForEducation && practiceStudent == null) practiceStudent = FindStudent();
            if (CurrentStep == 6 && (factoryWorker == null || !factoryWorker.CanStartWork)) factoryWorker = FindStudent();
            if (dock != null && CurrentStep >= 8 && CurrentStep <= 9) dock.enabled = true;
            // Choice already blocks gameplay; keep work unlocked until FinishResearch can
            // return its worker. The tutorial's per-card and per-building gates own input.
            game.AbilityPoints.SetActionsLocked(!RequiredAction);
            UpdatePractice();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(next.interactable ? next.gameObject : null);
        }

        private DraggableCard FindStudent()
        {
            DraggableCard best = null; Vector3 bestPoint = Vector3.zero;
            foreach (var card in game.Cards)
            {
                if (card == null || card.Kind != CardKind.Person || !card.CanStartWork || card.IsStored || card.IsFlying) continue;
                var point = game.Sidebar.PeopleArea.InverseTransformPoint(card.Rect.position);
                if (best == null || point.y > bestPoint.y + 1f || Mathf.Abs(point.y - bestPoint.y) <= 1f && point.x < bestPoint.x)
                { best = card; bestPoint = point; }
            }
            return best;
        }

        private void OnPersonEducated(DraggableCard student, DraggableCard graduate)
        {
            if (WaitingForEducation && student == practiceStudent) practiceGraduate = graduate;
        }
        private void OnBlueprintChoiceOpened(BuildingStation station)
        {
            if (WaitingForResearch && station == research) ShowStep(4);
        }
        private void OnBlueprintDiscovered(DraggableCard blueprint)
        {
            if (IsOpen && !closing && CurrentStep == 4 && blueprint.UpgradeType == UpgradeKind.RailSystem) practiceBlueprint = blueprint;
        }
        private void OnBlueprintProduced(DraggableCard blueprint)
        {
            if (!IsOpen || closing || CurrentStep != 6 || blueprint != practiceBlueprint) return;
            producedBlueprint = true;
            foreach (var card in game.Cards)
                if (card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.RailSystem && !card.IsStored) practiceProducts.Add(card);
        }

        private void OnTurnStarted()
        {
            if (!IsOpen || closing || CurrentStep != 9) return;
            round.ExplainNextRoundResult();
            if (rememberCompletion) { PlayerPrefs.SetInt(CompletionPreferenceKey, 1); PlayerPrefs.Save(); }
            // READY already locked actions for the real disaster; cleanup must keep that lock.
            DismissImmediate(true);
            awaitingResults = StepCount >= 12;
        }

        private void ShowResultsLessons()
        {
            awaitingResults = false;
            ownsGameLock = true;
            game.SetWorkersTutorial(this);
            IsOpen = true; closing = false; visibility = elapsed = 0f; boardVisibility = 1f;
            if (EventSystem.current != null) previousSelection = EventSystem.current.currentSelectedGameObject;
            layer.gameObject.SetActive(true); layer.interactable = layer.blocksRaycasts = true;
            ShowStep(10);
        }

        private void SetInstruction(string value)
        {
            if (description != null && description.text != value) { description.text = value; copyVisibility = 0f; }
        }

        private void FocusWorker(BuildingStation station, TutorialSpotlight.Workplaces place, DraggableCard worker)
        {
            bool free = worker != null && !worker.IsStored && !worker.IsFlying && worker.CanStartWork;
            spotlight.SetPracticeTargets(free ? worker.Rect : null, station.Card.Rect);
            spotlight.SetLessonFocus(place, true, VisiblePerson(worker), free && !worker.IsHeld);
        }

        private void UpdatePractice()
        {
            if (budget != null) budget.text = game.AbilityPoints.CurrentPoints + " AP";
            // The authored choice cards need their own click area. Temporarily hide the
            // saved LessonPanel while the native choice screen is open; never move it.
            panel.gameObject.SetActive(CurrentStep != 8 && (CurrentStep != 4 || !game.IsChoosingBlueprint));
            spotlight.SetPracticeTargets(null, null);
            switch (CurrentStep)
            {
                case 0:
                    spotlight.SetOverviewFocus();
                    break;
                case 1:
                    FocusWorker(school, TutorialSpotlight.Workplaces.School, practiceGraduate != null ? practiceGraduate : practiceStudent);
                    if (!WaitingForEducation) break;
                    bool training = practiceGraduate != null || practiceStudent != null && practiceStudent.AssignedTo == school;
                    SetInstruction(training ? trainingDescription : Format(lessons[1].description));
                    if (practiceGraduate != null && !practiceGraduate.IsFlying && !school.IsAnimatingWorkers)
                    { EducationCompleted = true; ShowStep(2); }
                    break;
                case 2:
                    spotlight.SetLessonFocus(TutorialSpotlight.Workplaces.ResearchCenter, true, VisiblePerson(practiceGraduate));
                    break;
                case 3:
                    FocusWorker(research, TutorialSpotlight.Workplaces.ResearchCenter, practiceGraduate);
                    bool researching = practiceGraduate != null && practiceGraduate.AssignedTo == research;
                    SetInstruction(researching ? researchingDescription : Format(lessons[3].description));
                    break;
                case 4:
                    if (game.IsChoosingBlueprint)
                    {
                        var button = game.RecipeFor(UpgradeKind.RailSystem).button;
                        spotlight.SetAreaFocus(0, false, null, (RectTransform)button.transform, true);
                    }
                    else if (practiceBlueprint != null)
                    {
                        SetInstruction(blueprintArrivalDescription);
                        spotlight.SetAreaFocus(0, true, null, practiceBlueprint.Rect);
                        var slot = game.Vault.SlotFor(UpgradeKind.RailSystem);
                        if (!practiceBlueprint.IsFlying && !slot.IsArriving && !research.IsAnimatingWorkers) ShowStep(5);
                    }
                    break;
                case 5:
                    if (production.PendingBlueprint == practiceBlueprint)
                    {
                        spotlight.SetLessonFocus(TutorialSpotlight.Workplaces.Factory, true, null);
                        if (!production.IsAnimatingBlueprint) ShowStep(6);
                    }
                    else
                    {
                        var slot = game.Vault.SlotFor(UpgradeKind.RailSystem);
                        var area = practiceBlueprint != null && practiceBlueprint.gameObject.activeInHierarchy ? practiceBlueprint.Rect : slot.CardRect;
                        spotlight.SetPracticeTargets(null, factory.Card.Rect);
                        spotlight.SetAreaFocus(TutorialSpotlight.Workplaces.Factory, true, null, area,
                            practiceBlueprint != null && !practiceBlueprint.IsFlying && !slot.IsArriving);
                        SetInstruction(Format(lessons[5].description));
                    }
                    break;
                case 6:
                    if (!producedBlueprint && production.PendingBlueprint != practiceBlueprint) { ShowStep(5); break; }
                    FocusWorker(factory, TutorialSpotlight.Workplaces.Factory, factoryWorker);
                    bool producing = producedBlueprint || factoryWorker != null && factoryWorker.AssignedTo == factory;
                    SetInstruction(producing ? producingDescription : Format(lessons[6].description));
                    if (producedBlueprint && ProductionHasSettled()) { ProductionCompleted = true; ShowStep(7); }
                    break;
                case 7:
                    spotlight.SetHouseProductFocus(houses, practiceProducts, false);
                    break;
                case 8:
                    if (IsWatchingCity) spotlight.SetLessonFocus(0, false, null);
                    else if (AllProductsInstalled) ShowStep(9);
                    else
                    {
                        spotlight.SetHouseProductFocus(houses, practiceProducts);
                    }
                    break;
                case 9:
                    spotlight.SetAreaFocus(0, true, null, (RectTransform)round.ReadyButton.transform, true);
                    SetInstruction(Format(lessons[9].description));
                    break;
                case 10:
                    spotlight.SetAreaFocus(0, false, null, round.ResultsRewardArea);
                    break;
                case 11:
                    spotlight.SetAreaFocus(0, false, null, round.ResultsRewardArea != null ? round.ResultsRewardArea.parent as RectTransform : null);
                    break;
            }
        }

        private bool ProductionHasSettled()
        {
            if (factory.IsAnimatingWorkers || production.IsAnimatingBlueprint || practiceBlueprint.IsFlying ||
                game.Vault.SlotFor(UpgradeKind.RailSystem).IsArriving) return false;
            foreach (var card in game.Cards)
                if (card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.RailSystem && card.IsFlying) return false;
            return true;
        }

        private RectTransform VisiblePerson(DraggableCard person)
        {
            if (person == null) return null;
            for (int i = 0; i < stations.Length; i++)
                foreach (var slot in stationSlots[i])
                    if ((stations[i].GetWorker(slot.Index) == person || slot.ReturningWorker == person) &&
                        slot.VisibleWorkerRect != null && slot.VisibleWorkerRect.gameObject.activeInHierarchy) return slot.VisibleWorkerRect;
            return person.gameObject.activeInHierarchy ? person.Rect : null;
        }

        private string Format(string value) => (value ?? string.Empty)
            .Replace("{school}", school.AbilityPointCost(CardKind.Person).ToString())
            .Replace("{normalResearch}", research.AbilityPointCost(CardKind.Person).ToString())
            .Replace("{educatedResearch}", research.AbilityPointCost(CardKind.EducatedPerson).ToString())
            .Replace("{normalFactory}", factory.AbilityPointCost(CardKind.Person).ToString())
            .Replace("{educatedFactory}", factory.AbilityPointCost(CardKind.EducatedPerson).ToString())
            .Replace("{ap}", game.AbilityPoints.CurrentPoints.ToString())
            .Replace("{safeHouses}", (round?.LastProtectedHouseCount ?? 0).ToString())
            .Replace("{survivalReward}", (round?.LastSurvivalReward ?? 0).ToString())
            .Replace("{houseReward}", round != null && round.LastProtectedHouseCount > 0 ? (round.LastSurvivalReward / round.LastProtectedHouseCount).ToString() : "0")
            .Replace("{roundBonus}", (round?.LastRoundGrant ?? 0).ToString())
            .Replace("{earnedAP}", ((round?.LastSurvivalReward ?? 0) + (round?.LastRoundGrant ?? 0)).ToString());

        public void Next()
        {
            if (!IsOpen || closing || RequiredAction) return;
            if (CurrentStep < StepCount - 1) ShowStep(CurrentStep + 1);
            else Close();
        }
        public void Back() { if (IsOpen && !closing && CurrentStep == 2) ShowStep(1); }
        public void Skip() { if (IsOpen && !closing && !game.IsChoosingBlueprint) Close(true); }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Next();
        }
        private void Close(bool skipped = false)
        {
            if (!ProductionCompleted && !skipped) return;
            closing = true; layer.interactable = false;
            if (rememberCompletion) { PlayerPrefs.SetInt(CompletionPreferenceKey, 1); PlayerPrefs.Save(); }
        }

        private void Update()
        {
            if (awaitingResults)
            {
                if (round.IsResultsVisible) ShowResultsLessons();
                else if (!round.IsRoundActive) awaitingResults = false;
            }
            if (!IsOpen) return;
            if (CurrentStep >= 10 && !round.IsResultsVisible) { DismissImmediate(); return; }
            if (!closing) UpdatePractice();
            float seconds = Time.unscaledDeltaTime;
            visibility = Mathf.MoveTowards(visibility, closing ? 0f : 1f, seconds / Mathf.Max(.05f, fadeSeconds));
            boardVisibility = Mathf.MoveTowards(boardVisibility, IsWatchingCity ? 0f : 1f, seconds / .2f);
            layer.alpha = Mathf.SmoothStep(0f, 1f, visibility) * Mathf.Max(.001f, Mathf.SmoothStep(0f, 1f, boardVisibility));
            copyVisibility = Mathf.MoveTowards(copyVisibility, 1f, seconds / .18f);
            if (copy != null) copy.alpha = Mathf.SmoothStep(0f, 1f, copyVisibility);
            elapsed += seconds;
            if (schoolFrame != null)
            {
                var color = schoolFrameColor;
                color.a = RequiredAction ? Mathf.Lerp(.55f, 1f, .5f - .5f * Mathf.Cos(elapsed * Mathf.PI * 1.6f)) : .7f;
                schoolFrame.color = color; spotlight.PulsePerson(color.a);
            }
            if (trainingSpark != null && CurrentStep == 2)
                trainingSpark.anchoredPosition = new Vector2(Mathf.Lerp(-80f, 80f, Mathf.SmoothStep(0f, 1f, Mathf.Repeat(elapsed * .55f, 1f))), 0f);
            if (closing && visibility <= 0f) DismissImmediate();
        }

        public void DismissImmediate() { awaitingResults = false; DismissImmediate(false); }

        private void DismissImmediate(bool preserveActionLock)
        {
            IsOpen = closing = false;
            if (panel != null) panel.gameObject.SetActive(true);
            if (layer != null) { layer.alpha = 0f; layer.blocksRaycasts = false; layer.gameObject.SetActive(false); }
            if (schoolFrame != null) schoolFrame.color = schoolFrameColor;
            if (spotlight != null)
            {
                spotlight.SetPracticeTargets(null, null);
                spotlight.SetLessonFocus(TutorialSpotlight.Workplaces.All, false, null);
            }
            if (!ownsGameLock) return;
            ownsGameLock = false;
            if (round != null) round.TurnStarted -= OnTurnStarted;
            if (game != null)
            {
                game.PersonEducated -= OnPersonEducated;
                game.BlueprintChoiceOpened -= OnBlueprintChoiceOpened;
                game.BlueprintDiscovered -= OnBlueprintDiscovered;
                game.BlueprintProduced -= OnBlueprintProduced;
                game.SetWorkersTutorial(null);
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
            if (!preserveActionLock && game != null && game.AbilityPoints != null) game.AbilityPoints.SetActionsLocked(false);
            if (ownsDockState && dock != null && dock.gameObject.activeInHierarchy)
            { dock.SetExpanded(dockWasExpanded, true); dock.enabled = dockWasEnabled; }
            ownsDockState = false;
        }

        [ContextMenu("Reset And Replay Tutorial")]
        public void ResetAndReplay()
        {
            PlayerPrefs.DeleteKey(CompletionPreferenceKey); PlayerPrefs.Save();
            if (Application.IsPlaying(gameObject)) { DismissImmediate(); ShowTutorial(); }
        }
        private void OnDisable() { if (Application.IsPlaying(gameObject)) DismissImmediate(); }
    }
}
