using UnityEngine;
using UnityEngine.UI;
using CivilGameJam.Presentation;

namespace CivilGameJam.Cards
{
    public sealed class FactoryProduction : MonoBehaviour
    {
        [Tooltip("Production duration with one normal worker. Worker contributions add together.")]
        [Min(0.1f)] [SerializeField] private float productionSeconds = 5f;
        [Tooltip("An educated worker contributes this many normal workers of production effort.")]
        [Min(1f)] [SerializeField] private float educatedWorkerMultiplier = 1.8f;
        [SerializeField] private BuildingStation station;
        [SerializeField] private RectTransform progressRoot;
        [SerializeField] private Image progressFill;
        [SerializeField] private ProgressBarFeedback progressFeedback;
        [SerializeField] private Outline outline;
        private CardGameController game;
        private DraggableCard card;
        public DraggableCard PendingBlueprint { get; private set; }
        public float Progress { get; private set; }
        public float ProductionSeconds => Mathf.Max(0.1f, productionSeconds);
        public float EducatedWorkerMultiplier => Mathf.Max(1f, educatedWorkerMultiplier);
        public float WorkRate
        {
            get
            {
                if (station == null) return 0f;
                float rate = 0f;
                for (int i = 0; i < BuildingStation.Capacity; i++)
                {
                    var worker = station.GetWorker(i);
                    if (worker != null) rate += worker.Kind == CardKind.EducatedPerson ? EducatedWorkerMultiplier : 1f;
                }
                return rate;
            }
        }

        public void Initialize(CardGameController owner, DraggableCard building)
        {
            game = owner;
            card = building;
            if (station == null) station = GetComponent<BuildingStation>();
            if (progressRoot == null) progressRoot = transform.Find("ProgressBar") as RectTransform;
            if (progressFill == null && progressRoot != null) progressFill = progressRoot.Find("BlueProgress")?.GetComponent<Image>();
            if (progressFeedback == null && progressRoot != null) progressFeedback = progressRoot.GetComponent<ProgressBarFeedback>();
            Refresh();
        }

        public bool CanAccept(DraggableCard blueprint) => game != null && !game.IsChoosingBlueprint &&
            PendingBlueprint == null && blueprint != null && blueprint.Kind == CardKind.Blueprint &&
            blueprint.UpgradeType != UpgradeKind.None && blueprint.Owner == game &&
            (!blueprint.IsStored || blueprint.StoredInVault == game.Vault) && game.RecipeFor(blueprint.UpgradeType) != null;

        public bool TryInsertBlueprint(DraggableCard blueprint)
        {
            if (!CanAccept(blueprint)) return false;
            blueprint.StoredInVault?.Withdraw(blueprint);
            blueprint.StopFlight();
            PendingBlueprint = blueprint;
            blueprint.StoredAtFactory = this;
            blueprint.gameObject.SetActive(false);
            Progress = 0f;
            if (progressFeedback != null) progressFeedback.Begin();
            Refresh();
            return true;
        }

        public DraggableCard ReleaseBlueprint(bool animate = true)
        {
            if (PendingBlueprint == null || game.IsChoosingBlueprint) return null;
            var blueprint = PendingBlueprint;
            PendingBlueprint = null;
            Progress = 0f;
            blueprint.StoredAtFactory = null;
            game.ReturnToTable(blueprint, card.Rect.position, animate);
            Refresh();
            return blueprint;
        }

        public void AdvanceWork(float seconds)
        {
            if (game == null) return;
            float workRate = WorkRate;
            if (seconds > 0f && PendingBlueprint != null && workRate > 0f && !game.IsChoosingBlueprint)
            {
                Progress = Mathf.Min(1f, Progress + seconds * workRate / ProductionSeconds);
                if (Progress >= 1f)
                {
                    if (progressFeedback != null) progressFeedback.Complete();
                    var blueprint = PendingBlueprint;
                    PendingBlueprint = null;
                    blueprint.StoredAtFactory = null;
                    Progress = 0f;
                    game.ProduceBlueprint(blueprint, card.Rect.position);
                }
            }
            Refresh();
        }

        private void Update() => AdvanceWork(Time.deltaTime);

        public void Preview(DraggableCard blueprint)
        {
            if (outline == null) return;
            outline.enabled = blueprint != null;
            if (blueprint != null) outline.effectColor = CanAccept(blueprint) ? new Color(0.2f, 0.9f, 0.45f) : Color.red;
        }

        private void Refresh()
        {
            bool pending = PendingBlueprint != null;
            if (progressFeedback != null) progressFeedback.SetProgress(Progress, pending);
            else
            {
                if (progressRoot != null) progressRoot.gameObject.SetActive(pending);
                if (progressFill != null) CardUI.SetProgress(progressFill.rectTransform, Progress);
            }
        }
    }
}
