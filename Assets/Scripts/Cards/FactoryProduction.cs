using UnityEngine;
using System.Collections.Generic;
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
        [SerializeField] private FactoryFolderPresentation folderPresentation;
        private CardGameController game;
        private DraggableCard card;
        private readonly Dictionary<DraggableCard, int> paidWorkers = new Dictionary<DraggableCard, int>();
        private readonly Dictionary<DraggableCard, float> contributions = new Dictionary<DraggableCard, float>();
        public bool HasPaidFor(DraggableCard worker) => paidWorkers.ContainsKey(worker);
        public void RecordPayment(DraggableCard worker, int cost)
        {
            if (PendingBlueprint != null) paidWorkers[worker] = cost;
        }

        public void RefundWorker(DraggableCard worker)
        {
            if (paidWorkers.TryGetValue(worker, out int cost))
            {
                paidWorkers.Remove(worker);
                game.AbilityPoints?.AddPoints(cost);
            }
            if (contributions.TryGetValue(worker, out float contribution))
            {
                contributions.Remove(worker);
                Progress = Mathf.Max(0f, Progress - contribution);
            }
            Refresh();
        }
        public int CurrentOrderCost
        {
            get
            {
                int cost = 0;
                if (station != null)
                    for (int i = 0; i < BuildingStation.Capacity; i++)
                    {
                        var worker = station.GetWorker(i);
                        if (worker != null) cost += station.AbilityPointCost(worker.Kind);
                    }
                return cost;
            }
        }
        public string RejectionMessage => game != null && game.ActionsLocked ? "Wait until the disaster finishes." :
            game != null && game.AbilityPoints != null && !game.AbilityPoints.CanSpend(CurrentOrderCost)
                ? "Not enough AP: " + CurrentOrderCost + " AP required for this production order." : "The factory already has a blueprint.";
        public string RejectionMessageFor(DraggableCard blueprint) => blueprint != null && game != null && !game.CanUseBlueprint(blueprint.UpgradeType)
            ? "This blueprint is complete and cannot start another production order." : RejectionMessage;
        public DraggableCard PendingBlueprint { get; private set; }
        public float Progress { get; private set; }
        public bool IsAnimatingBlueprint => folderPresentation != null && folderPresentation.IsAnimating;
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
            if (folderPresentation == null) folderPresentation = GetComponent<FactoryFolderPresentation>();
            if (folderPresentation != null) folderPresentation.Initialize(owner, building);
            Refresh();
        }

        public bool CanAccept(DraggableCard blueprint) => CanAccept(blueprint, true);

        private bool CanAccept(DraggableCard blueprint, bool requirePayment) => game != null && !game.IsChoosingBlueprint && !game.ActionsLocked &&
            (!requirePayment || game.AbilityPoints == null || game.AbilityPoints.CanSpend(CurrentOrderCost)) &&
            PendingBlueprint == null && !IsAnimatingBlueprint && blueprint != null && blueprint.Kind == CardKind.Blueprint &&
            blueprint.UpgradeType != UpgradeKind.None && blueprint.Owner == game &&
            game.CanUseBlueprint(blueprint.UpgradeType) &&
            (!blueprint.IsStored || blueprint.StoredInVault == game.Vault) && game.RecipeFor(blueprint.UpgradeType) != null;

        public bool TryInsertBlueprint(DraggableCard blueprint)
        {
            if (!CanAccept(blueprint))
            {
                if (CanAccept(blueprint, false) && game.AbilityPoints != null && game.AbilityPoints.ReportInsufficient(CurrentOrderCost))
                    for (int i = 0; i < BuildingStation.Capacity; i++)
                        if (station.GetWorker(i) != null) station.ShowInsufficientCost(station.GetWorker(i).Kind);
                return false;
            }
            if (game.AbilityPoints != null && !game.AbilityPoints.TrySpend(CurrentOrderCost)) return false;
            paidWorkers.Clear();
            contributions.Clear();
            for (int i = 0; i < BuildingStation.Capacity; i++)
                if (station.GetWorker(i) != null)
                {
                    var worker = station.GetWorker(i);
                    paidWorkers[worker] = station.AbilityPointCost(worker.Kind);
                }
            PendingBlueprint = blueprint;
            blueprint.StoredAtFactory = this;
            // Snapshot the actual dropped artwork before withdrawing or hiding its card.
            if (folderPresentation != null) folderPresentation.Insert(blueprint);
            blueprint.StoredInVault?.Withdraw(blueprint);
            blueprint.StopFlight();
            blueprint.gameObject.SetActive(false);
            Progress = 0f;
            if (progressFeedback != null) progressFeedback.Begin();
            Refresh();
            return true;
        }

        public DraggableCard ReleaseBlueprint(bool animate = true)
        {
            if (PendingBlueprint == null || game.IsChoosingBlueprint || game.ActionsLocked) return null;
            var blueprint = PendingBlueprint;
            PendingBlueprint = null;
            Progress = 0f;
            blueprint.StoredAtFactory = null;
            bool returning = animate && folderPresentation != null && game.Vault.StoreFromFactory(blueprint, folderPresentation);
            if (!returning)
            {
                if (folderPresentation != null) folderPresentation.ClearBlueprint();
                game.ReturnToTable(blueprint, card.Rect.position, animate);
            }
            station.ReleaseAllWorkers();
            paidWorkers.Clear();
            contributions.Clear();
            Refresh();
            return blueprint;
        }

        public void AdvanceWork(float seconds)
        {
            if (game == null || game.ActionsLocked) return;
            float workRate = WorkRate;
            if (seconds > 0f && PendingBlueprint != null && workRate > 0f && !game.IsChoosingBlueprint)
            {
                for (int i = 0; i < BuildingStation.Capacity; i++)
                {
                    var worker = station.GetWorker(i);
                    if (worker == null) continue;
                    float contribution = seconds * (worker.Kind == CardKind.EducatedPerson ? EducatedWorkerMultiplier : 1f) / ProductionSeconds;
                    contributions.TryGetValue(worker, out float previous);
                    contributions[worker] = previous + contribution;
                    Progress += contribution;
                }
                Progress = Mathf.Min(1f, Progress);
                if (Progress >= 1f)
                {
                    if (progressFeedback != null) progressFeedback.Complete();
                    var blueprint = PendingBlueprint;
                    PendingBlueprint = null;
                    if (folderPresentation != null) folderPresentation.ClearBlueprint();
                    for (int i = 0; i < BuildingStation.Capacity; i++) station.GetWorker(i)?.UseForThisTurn();
                    paidWorkers.Clear();
                    contributions.Clear();
                    blueprint.StoredAtFactory = null;
                    Progress = 0f;
                    game.ProduceBlueprint(blueprint, card.Rect.position);
                    station.ReleaseAllWorkers();
                }
            }
            Refresh();
        }

        public void ResetForNewTurn()
        {
            if (folderPresentation != null) folderPresentation.ClearBlueprint();
            // Cancellation refunds each outstanding worker exactly once.
            if (station != null)
                for (int i = 0; i < BuildingStation.Capacity; i++)
                    if (station.GetWorker(i) != null) RefundWorker(station.GetWorker(i));
            if (PendingBlueprint != null)
            {
                var blueprint = PendingBlueprint;
                PendingBlueprint = null;
                if (folderPresentation != null) folderPresentation.ClearBlueprint();
                blueprint.StoredAtFactory = null;
                game.ReturnToTable(blueprint, card.Rect.position, false);
            }
            paidWorkers.Clear();
            contributions.Clear();
            Progress = 0f;
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
