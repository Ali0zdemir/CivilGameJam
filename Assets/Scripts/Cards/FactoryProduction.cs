using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class FactoryProduction : MonoBehaviour
    {
        [Min(0.1f)] [SerializeField] private float productionSeconds = 5f;
        [SerializeField] private BuildingStation station;
        [SerializeField] private TextMeshProUGUI productionStatus;
        [SerializeField] private TextMeshProUGUI blueprintLabel;
        [SerializeField] private UpgradeIcon blueprintIcon;
        [SerializeField] private RectTransform progressRoot;
        [SerializeField] private Image progressFill;
        [SerializeField] private Outline outline;
        private CardGameController game;
        private DraggableCard card;
        public DraggableCard PendingBlueprint { get; private set; }
        public float Progress { get; private set; }
        public float ProductionSeconds => Mathf.Max(0.1f, productionSeconds);

        public void Initialize(CardGameController owner, DraggableCard building)
        { game = owner; card = building; Refresh(); }

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
            if (seconds > 0f && PendingBlueprint != null && station.WorkerCount > 0 && !game.IsChoosingBlueprint)
            {
                Progress = Mathf.Min(1f, Progress + seconds / ProductionSeconds);
                if (Progress >= 1f)
                {
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
            outline.enabled = blueprint != null;
            if (blueprint != null) outline.effectColor = CanAccept(blueprint) ? new Color(0.2f, 0.9f, 0.45f) : Color.red;
        }

        private void Refresh()
        {
            bool pending = PendingBlueprint != null;
            blueprintIcon.gameObject.SetActive(pending);
            if (pending) blueprintIcon.Kind = PendingBlueprint.UpgradeType;
            blueprintLabel.text = pending ? game.RecipeFor(PendingBlueprint.UpgradeType).title + " plan" : "Drop a blueprint";
            productionStatus.text = !pending ? "Requires a blueprint\nand at least 1 worker" : station.WorkerCount == 0
                ? "WORKER REQUIRED\nAssign at least 1 person" : "Producing";
            productionStatus.color = pending && station.WorkerCount == 0 ? new Color(0.95f, 0.4f, 0.24f) : new Color(0.22f, 0.35f, 0.35f);
            progressRoot.gameObject.SetActive(pending);
            CardUI.SetProgress(progressFill.rectTransform, Progress);
        }
    }
}
