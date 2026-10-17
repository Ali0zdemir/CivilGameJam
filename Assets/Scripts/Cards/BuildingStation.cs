using TMPro;
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
        [SerializeField] private TextMeshProUGUI status;
        [SerializeField] private Outline dropOutline;

        public float ResearchProgress { get; private set; }
        public bool AwaitingBlueprint { get; private set; }
        public int WorkerCount => (workers[0] != null ? 1 : 0) + (workers[1] != null ? 1 : 0);
        public bool IsReceivingWorker => System.Array.Exists(indicators, slot => slot != null && slot.IsEntering);
        public bool HasFolderPresentation => indicators[0] != null && indicators[0].Portrait != null;
        public DraggableCard Card => card;
        public DraggableCard GetWorker(int index) => workers[index];
        public float GetEducationProgress(int index) => educationProgress[index];

        public void Initialize(CardGameController owner, DraggableCard building)
        {
            game = owner;
            card = building;
            dropOutline.enabled = false;
            RefreshView();
        }

        public bool CanAccept(DraggableCard worker)
        {
            return card.Kind != CardKind.House && worker != null && worker.IsPerson && worker.AssignedTo == null &&
                   !AwaitingBlueprint && WorkerCount < Capacity &&
                   (card.Kind != CardKind.School || worker.Kind == CardKind.Person);
        }

        public bool TryAssign(DraggableCard worker)
        {
            if (!CanAccept(worker) || game.IsChoosingBlueprint) return false;
            Vector3 droppedPosition = worker.Rect.position;
            game.CloseInspection();
            worker.Rect.position = droppedPosition;
            game.Sidebar.RememberPosition(worker);
            for (int i = 0; i < Capacity; i++)
            {
                if (workers[i] != null) continue;
                workers[i] = worker;
                educationProgress[i] = 0f;
                worker.StopFlight();
                var artwork = worker.transform.Find("Artwork") as RectTransform ?? worker.Rect;
                Vector3 origin = artwork.position;
                Vector3 worldScale = artwork.lossyScale;
                var sourceSize = artwork.rect.size;
                worker.AssignedTo = this;
                RefreshView();
                indicators[i].AnimateEntry(worker, origin, worldScale, sourceSize, artwork.rotation);
                worker.gameObject.SetActive(false);
                game.RefreshPopulation();
                return true;
            }
            return false;
        }

        public DraggableCard ReleaseWorker(int index, bool animate = true)
        {
            if (index < 0 || index >= Capacity || game.IsChoosingBlueprint) return null;
            var worker = workers[index];
            if (worker == null) return null;
            workers[index] = null;
            educationProgress[index] = 0f;
            worker.AssignedTo = null;
            var visual = indicators[index].VisibleWorkerRect;
            Vector3 origin = visual != null ? visual.position : card.Rect.position;
            float width = visual != null ? visual.rect.width * visual.lossyScale.x : 0f;
            game.ReturnToTable(worker, origin, animate);
            if (animate && width > 0f) worker.MatchArtworkWidth(width);
            RefreshView();
            return worker;
        }

        // Independent school timers preserve each student's own start time. Research combines worker rates.
        public void AdvanceWork(float seconds)
        {
            if (game == null || seconds <= 0f || AwaitingBlueprint || card.Kind == CardKind.House) return;
            if (card.Kind == CardKind.School)
            {
                for (int i = 0; i < Capacity; i++)
                {
                    if (workers[i] == null) continue;
                    educationProgress[i] += seconds / game.EducationSeconds;
                    if (educationProgress[i] < 1f) continue;
                    var student = workers[i];
                    workers[i] = null;
                    educationProgress[i] = 0f;
                    student.AssignedTo = null;
                    game.Graduate(student, card.Rect.position);
                }
            }
            else if (card.Kind == CardKind.ResearchCenter && WorkerCount > 0)
            {
                for (int i = 0; i < Capacity; i++)
                {
                    if (workers[i] == null) continue;
                    ResearchProgress += seconds / (workers[i].Kind == CardKind.EducatedPerson
                        ? game.EducatedResearchSeconds : game.NormalResearchSeconds);
                }
                if (ResearchProgress >= 1f)
                {
                    ResearchProgress = 1f;
                    AwaitingBlueprint = true;
                    game.ShowBlueprintChoice(this);
                }
            }
            RefreshView();
        }

        public void FinishResearch()
        {
            if (!AwaitingBlueprint) return;
            AwaitingBlueprint = false;
            ResearchProgress = 0f;
            for (int i = 0; i < Capacity; i++) ReleaseWorker(i);
            RefreshView();
        }

        public void PreviewAcceptance(DraggableCard worker)
        {
            dropOutline.enabled = worker != null;
            if (worker != null) dropOutline.effectColor = CanAccept(worker)
                ? new Color(0.2f, 0.9f, 0.45f) : new Color(0.95f, 0.25f, 0.2f);
        }

        private void Update() => AdvanceWork(Time.deltaTime);

        private void RefreshView()
        {
            for (int i = 0; i < Capacity; i++) indicators[i].Refresh(workers[i]);
            if (card.Kind == CardKind.House) return;
            float progress = card.Kind == CardKind.School
                ? Mathf.Max(educationProgress[0], educationProgress[1]) : ResearchProgress;
            if (card.Kind != CardKind.Factory)
            {
                progressRoot.gameObject.SetActive(WorkerCount > 0 || progress > 0);
                CardUI.SetProgress(progressFill.rectTransform, progress);
            }
            if (AwaitingBlueprint) status.text = "Choose a blueprint";
            else if (card.Kind == CardKind.School && WorkerCount > 0)
                status.text = "Educating";
            else if (card.Kind == CardKind.ResearchCenter && WorkerCount > 0)
                status.text = "Researching";
            else status.text = $"{WorkerCount} / {Capacity} workers";
        }
    }
}
