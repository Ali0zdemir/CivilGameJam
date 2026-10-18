using TMPro;
using CivilGameJam.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class BuildingStation : MonoBehaviour
    {
        public const int Capacity = 2;
        private readonly DraggableCard[] workers = new DraggableCard[Capacity];
        [SerializeField] private WorkerSlot[] indicators = new WorkerSlot[Capacity];
        private readonly float[] educationProgress = new float[Capacity];
        private CardGameController game;
        [SerializeField] private DraggableCard card;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform progressRoot;
        [SerializeField] private ProgressBarFeedback progressFeedback;
        [SerializeField] private TextMeshProUGUI status;
        [SerializeField] private Outline dropOutline;
        [Header("Ability points per worker, per operation")]
        [SerializeField, Min(0)] private int normalAbilityPointCost = 4;
        [SerializeField, Min(0)] private int educatedAbilityPointCost = 2;
        [Header("Tsunami: AP per worker, per operation")]
        [SerializeField, Min(0)] private int tsunamiNormalAbilityPointCost = 6;
        [SerializeField, Min(0)] private int tsunamiEducatedAbilityPointCost = 3;
        [Header("Fire: AP per worker, per operation")]
        [SerializeField, Min(0)] private int fireNormalAbilityPointCost = 8;
        [SerializeField, Min(0)] private int fireEducatedAbilityPointCost = 5;
        private int pricingSequence = 1;
        [SerializeField] private TMP_Text normalCostLabel;
        [SerializeField] private TMP_Text educatedCostLabel;
        [SerializeField] private AbilityPointCostFeedback normalCostFeedback;
        [SerializeField] private AbilityPointCostFeedback educatedCostFeedback;
        private readonly int[] reservedCosts = new int[Capacity];
        private readonly float[] researchContributions = new float[Capacity];

        public float ResearchProgress { get; private set; }
        public bool AwaitingBlueprint { get; private set; }
        public int WorkerCount => (workers[0] != null ? 1 : 0) + (workers[1] != null ? 1 : 0);
        public bool IsAnimatingWorkers => indicators != null && System.Array.Exists(indicators, slot => slot != null && slot.IsAnimating);
        public bool HasFolderPresentation => indicators != null && System.Array.Exists(indicators, slot => slot != null && slot.Portrait != null);
        public DraggableCard Card => card;
        public DraggableCard GetWorker(int index) => workers[index];
        public float GetEducationProgress(int index) => educationProgress[index];
        public int AbilityPointCost(CardKind kind)
        {
            bool educated = kind == CardKind.EducatedPerson;
            int cost = pricingSequence == 3 ? educated ? fireEducatedAbilityPointCost : fireNormalAbilityPointCost
                : pricingSequence == 2 ? educated ? tsunamiEducatedAbilityPointCost : tsunamiNormalAbilityPointCost
                : educated ? educatedAbilityPointCost : normalAbilityPointCost;
            return Mathf.Max(0, cost);
        }

        public void UseSequencePrices(int sequenceNumber)
        {
            pricingSequence = Mathf.Clamp(sequenceNumber, 1, 3);
            RefreshAbilityCostLabels();
        }
        public bool HasActiveWork => card != null && !AwaitingBlueprint && WorkerCount > 0 &&
            (card.Kind == CardKind.School || card.Kind == CardKind.ResearchCenter && game != null && game.HasResearchForWorkers(WorkerCount));

        public void Initialize(CardGameController owner, DraggableCard building)
        {
            game = owner;
            card = building;
            if (indicators == null || indicators.Length != Capacity) System.Array.Resize(ref indicators, Capacity);
            foreach (var slot in GetComponentsInChildren<WorkerSlot>(true))
                if (slot.Index >= 0 && slot.Index < Capacity && indicators[slot.Index] == null)
                    indicators[slot.Index] = slot;
            // Resolve existing authored UI when a prefab reference has been cleared.
            // Missing optional presentation must never interrupt education or research.
            if (progressRoot == null) progressRoot = transform.Find("ProgressBar") as RectTransform;
            if (progressFill == null && progressRoot != null)
                progressFill = progressRoot.Find("BlueProgress")?.GetComponent<Image>();
            if (progressFeedback == null && progressRoot != null) progressFeedback = progressRoot.GetComponent<ProgressBarFeedback>();
            if (dropOutline != null) dropOutline.enabled = false;
            RefreshAbilityCostLabels();
            RefreshView();
        }

        public bool CanAccept(DraggableCard worker) => CanAccept(worker, true);

        private bool CanAccept(DraggableCard worker, bool requirePayment)
        {
            return game != null && card != null && card.Kind != CardKind.House && worker != null && worker.Owner == game && worker.CanStartWork && worker.AssignedTo == null &&
                   !game.ActionsLocked && !AwaitingBlueprint && AvailableSlot(worker) >= 0 && (!requirePayment || CanPayWorker(worker)) &&
                   (card.Kind != CardKind.ResearchCenter || game.HasUndiscoveredBlueprints) &&
                   (card.Kind != CardKind.School || worker.Kind == CardKind.Person);
        }

        private int AvailableSlot(DraggableCard worker)
        {
            for (int i = 0; i < Capacity; i++)
            {
                var slot = Indicator(i);
                // A missing interactive slot must not swallow a person who cannot be taken back out.
                if (slot != null && workers[i] == null && (!slot.IsReturning || slot.ReturningWorker == worker)) return i;
            }
            return -1;
        }

        private WorkerSlot Indicator(int index) => indicators != null && index >= 0 && index < indicators.Length ? indicators[index] : null;

        private int AdditionalCost(DraggableCard worker)
        {
            if (card.Kind == CardKind.Factory)
            {
                var factory = GetComponent<FactoryProduction>();
                if (factory == null || factory.PendingBlueprint == null || factory.HasPaidFor(worker)) return 0;
            }
            return AbilityPointCost(worker.Kind);
        }

        private int RequiredAssignmentCost(DraggableCard worker)
        {
            // Idle factory workers are charged when a blueprint arrives, but must still be affordable on entry.
            if (card.Kind == CardKind.Factory && GetComponent<FactoryProduction>()?.PendingBlueprint == null)
                return AbilityPointCost(worker.Kind);
            return AdditionalCost(worker);
        }

        private bool CanPayWorker(DraggableCard worker) => game.AbilityPoints == null || game.AbilityPoints.CanSpend(RequiredAssignmentCost(worker));

        public string RejectionMessage(DraggableCard worker) => game != null && game.ActionsLocked ? "Wait until the disaster finishes." :
            worker != null && worker.UsedThisTurn ? "This person has already worked this turn." :
            worker != null && game != null && !CanPayWorker(worker) ? "Not enough AP: " + RequiredAssignmentCost(worker) + " AP required." :
            "This building holds up to 2 workers.";

        public void ShowInsufficientCost(CardKind workerKind)
        {
            (workerKind == CardKind.EducatedPerson ? educatedCostFeedback : normalCostFeedback)?.PlayDenied();
        }

        public void RefreshAbilityCostLabels()
        {
            if (normalCostLabel != null) normalCostLabel.text = AbilityPointCost(CardKind.Person) + " AP";
            if (educatedCostLabel != null) educatedCostLabel.text = card != null && card.Kind == CardKind.School
                ? "N/A" : AbilityPointCost(CardKind.EducatedPerson) + " AP";
        }

        private void OnValidate() => RefreshAbilityCostLabels();

        public bool TryAssign(DraggableCard worker)
        {
            if (game == null || game.IsChoosingBlueprint) return false;
            if (!CanAccept(worker))
            {
                if (CanAccept(worker, false) && game.AbilityPoints != null && game.AbilityPoints.ReportInsufficient(RequiredAssignmentCost(worker)))
                    ShowInsufficientCost(worker.Kind);
                return false;
            }
            Vector3 droppedPosition = worker.Rect.position;
            game.CloseInspection();
            worker.Rect.position = droppedPosition;
            game.Sidebar.RememberPosition(worker);
            worker.StopFlight();
            for (int i = 0; i < Capacity; i++)
            {
                var slot = Indicator(i);
                if (slot == null || workers[i] != null || slot.IsReturning) continue;
                int cost = AdditionalCost(worker);
                if (game.AbilityPoints != null && !game.AbilityPoints.TrySpend(cost)) return false;
                if (card.Kind == CardKind.Factory) GetComponent<FactoryProduction>()?.RecordPayment(worker, cost);
                else reservedCosts[i] = cost;
                workers[i] = worker;
                educationProgress[i] = 0f;
                if (card.Kind != CardKind.Factory && progressFeedback != null)
                    progressFeedback.Begin(card.Kind == CardKind.School ? Mathf.Max(educationProgress[0], educationProgress[1]) : ResearchProgress);
                var artwork = worker.transform.Find("Artwork") as RectTransform ?? worker.Rect;
                Vector3 origin = artwork.position;
                Vector3 worldScale = artwork.lossyScale;
                var sourceSize = artwork.rect.size;
                worker.AssignedTo = this;
                RefreshView();
                slot.AnimateEntry(worker, origin, worldScale, sourceSize, artwork.rotation);
                worker.gameObject.SetActive(false);
                game.RefreshPopulation();
                return true;
            }
            return false;
        }

        public DraggableCard ReleaseWorker(int index, bool animate = true)
        {
            if (index < 0 || index >= Capacity || game == null || game.IsChoosingBlueprint || game.ActionsLocked) return null;
            var worker = workers[index];
            if (worker == null) return null;
            if (!worker.UsedThisTurn)
            {
                if (card.Kind == CardKind.Factory) GetComponent<FactoryProduction>()?.RefundWorker(worker);
                else if (reservedCosts[index] > 0) game.AbilityPoints?.AddPoints(reservedCosts[index]);
                // An unfinished worker's contribution is cancelled along with their AP charge.
                researchContributions[index] = 0f;
                ResearchProgress = Mathf.Clamp01(researchContributions[0] + researchContributions[1]);
            }
            reservedCosts[index] = 0;
            workers[index] = null;
            educationProgress[index] = 0f;
            worker.AssignedTo = null;
            var slot = Indicator(index);
            var visual = slot != null ? slot.VisibleWorkerRect : null;
            Vector3 origin = visual != null ? visual.position : card.Rect.position;
            float width = visual != null ? visual.rect.width * visual.lossyScale.x : 0f;
            game.ReturnToTable(worker, origin, false);
            if (animate && (slot == null || !slot.AnimateExit(worker)))
            {
                worker.FlyFrom(origin, worker.Rect.position);
                if (width > 0f) worker.MatchArtworkWidth(width);
            }
            RefreshView();
            return worker;
        }

        // Independent school timers preserve each student's own start time. Research combines worker rates.
        public void AdvanceWork(float seconds)
        {
            if (game == null || game.ActionsLocked || card == null || seconds <= 0f || AwaitingBlueprint || card.Kind == CardKind.House) return;
            if (card.Kind == CardKind.School)
            {
                bool graduated = false;
                for (int i = 0; i < Capacity; i++)
                {
                    if (workers[i] == null) continue;
                    educationProgress[i] += seconds / game.EducationSeconds;
                    if (educationProgress[i] < 1f) continue;
                    graduated = true;
                    var student = workers[i];
                    reservedCosts[i] = 0;
                    student.UseForThisTurn();
                    workers[i] = null;
                    educationProgress[i] = 0f;
                    student.AssignedTo = null;
                    var educated = game.Graduate(student, card.Rect.position, false);
                    var slot = Indicator(i);
                    if (slot == null || !slot.AnimateExit(educated)) educated.FlyFrom(card.Rect.position, educated.Rect.position);
                }
                if (graduated)
                {
                    card.GetComponent<CardFeedback>()?.PlayWorkComplete();
                    if (progressFeedback != null) progressFeedback.Complete();
                }
            }
            else if (card.Kind == CardKind.ResearchCenter && WorkerCount > 0 && game.HasResearchForWorkers(WorkerCount))
            {
                for (int i = 0; i < Capacity; i++)
                {
                    if (workers[i] == null) continue;
                    researchContributions[i] += seconds / (workers[i].Kind == CardKind.EducatedPerson
                        ? game.EducatedResearchSeconds : game.NormalResearchSeconds);
                }
                ResearchProgress = Mathf.Clamp01(researchContributions[0] + researchContributions[1]);
                if (ResearchProgress >= 1f)
                {
                    ResearchProgress = 1f;
                    AwaitingBlueprint = true;
                    for (int i = 0; i < Capacity; i++)
                    {
                        if (workers[i] != null) workers[i].UseForThisTurn();
                        reservedCosts[i] = 0;
                    }
                    if (progressFeedback != null) progressFeedback.Complete();
                    game.ShowBlueprintChoice(this);
                }
            }
            RefreshView();
        }

        public void FinishResearch()
        {
            if (!AwaitingBlueprint) return;
            card.GetComponent<CardFeedback>()?.PlayWorkComplete();
            AwaitingBlueprint = false;
            ResearchProgress = 0f;
            System.Array.Clear(researchContributions, 0, Capacity);
            for (int i = 0; i < Capacity; i++) ReleaseWorker(i);
            RefreshView();
        }

        // Turn reset discards old people without creating folder return animations.
        public void ResetForNewTurn()
        {
            AwaitingBlueprint = false;
            ResearchProgress = 0f;
            for (int i = 0; i < Capacity; i++)
            {
                if (reservedCosts[i] > 0) game.AbilityPoints?.AddPoints(reservedCosts[i]);
                reservedCosts[i] = 0;
                researchContributions[i] = 0f;
                if (workers[i] != null) workers[i].AssignedTo = null;
                workers[i] = null;
                educationProgress[i] = 0f;
                Indicator(i)?.ResetForNewTurn();
            }
            RefreshView();
        }

        public void ReleaseAllWorkers()
        {
            for (int i = 0; i < Capacity; i++) ReleaseWorker(i);
        }

        public void PreviewAcceptance(DraggableCard worker)
        {
            if (dropOutline == null) return;
            dropOutline.enabled = worker != null;
            if (worker != null) dropOutline.effectColor = CanAccept(worker)
                ? new Color(0.2f, 0.9f, 0.45f) : new Color(0.95f, 0.25f, 0.2f);
        }

        private void Update() => AdvanceWork(Time.deltaTime);

        private void RefreshView()
        {
            for (int i = 0; i < Capacity; i++)
            {
                var slot = Indicator(i);
                if (slot != null) slot.Refresh(workers[i]);
            }
            if (card == null || card.Kind == CardKind.House) return;
            float progress = card.Kind == CardKind.School
                ? Mathf.Max(educationProgress[0], educationProgress[1]) : ResearchProgress;
            if (card.Kind != CardKind.Factory)
            {
                bool visible = !AwaitingBlueprint && (WorkerCount > 0 || progress > 0);
                if (progressFeedback != null) progressFeedback.SetProgress(progress, visible);
                else
                {
                    if (progressRoot != null) progressRoot.gameObject.SetActive(visible);
                    if (progressFill != null) CardUI.SetProgress(progressFill.rectTransform, progress);
                }
            }
            // Progress is communicated by the saved bar, never an automatic status caption.
            if (status != null) status.text = string.Empty;
        }
    }
}
