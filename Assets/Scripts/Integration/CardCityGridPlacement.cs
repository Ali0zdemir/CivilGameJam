using System;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using UnityEngine;

namespace CivilGameJam
{
    /// <summary>The saved card identities share their logical grid cells with the existing city buildings.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(250)]
    public sealed class CardCityGridPlacement : MonoBehaviour
    {
        [Serializable]
        private sealed class BuildingLink
        {
            public DraggableCard card;
            public Transform building;
            public float heightOffset;
            [NonSerialized] public Vector3 from, target, side;
            [NonSerialized] public float elapsed;
            [NonSerialized] public bool moving;
        }

        [SerializeField] private CardGameController game;
        [SerializeField] private CardGrid grid;
        [Tooltip("2D order: top-left to top-right, then bottom-left to bottom-right.")]
        [SerializeField] private Transform[] cellAnchors = new Transform[CardGrid.CellCount];
        [SerializeField] private BuildingLink[] buildings = Array.Empty<BuildingLink>();
        [Header("City movement")]
        [SerializeField, Min(.2f)] private float movementSeconds = .55f;
        [SerializeField, Min(0f)] private float passingArc = 1.35f;
        [SerializeField, Range(0f, .4f)] private float liftHeight = .12f;
        [Header("Placement locks")]
        [SerializeField] private MonsterSequenceController sequence;
        [SerializeField] private EarthquakeTestController earthquake;
        [SerializeField] private TsunamiTestController tsunami;
        [SerializeField] private FireTestController fire;
        [SerializeField] private SeaMonsterWalk monsterWalk;
        [SerializeField] private CityCameraController navigation;
        private bool started, layoutDirty;

        public bool IsAnimating
        {
            get { foreach (var link in buildings) if (link != null && link.moving) return true; return false; }
        }
        public bool IsPlacementLocked => game != null && game.ActionsLocked ||
            sequence != null && sequence.IsRunning || earthquake != null && earthquake.IsRunning ||
            tsunami != null && tsunami.IsRunning || fire != null && fire.IsRunning ||
            monsterWalk != null && monsterWalk.IsWalkingAfterEvent || navigation != null && navigation.IsEventCameraActive;

        public Transform BuildingFor(DraggableCard card)
        {
            foreach (var link in buildings) if (link != null && link.card == card) return link.building;
            return null;
        }
        public Transform CellAnchor(int index) => index >= 0 && index < cellAnchors.Length ? cellAnchors[index] : null;

        private void OnEnable()
        {
            if (grid != null)
            {
                grid.LayoutChanged += OnLayoutChanged;
                grid.PlacementLockRequested += PlacementLocked;
                grid.LayoutAnimationRequested += Animating;
            }
            CityLayoutEvents.PreparingDisaster += PrepareForDisaster;
            if (started) OnLayoutChanged(false);
        }

        private bool PlacementLocked() => isActiveAndEnabled && IsPlacementLocked;
        private bool Animating() => isActiveAndEnabled && IsAnimating;
        private void Start() { started = true; OnLayoutChanged(false); }

        private void OnLayoutChanged(bool animate)
        {
            if (!started || grid == null) return;
            foreach (var link in buildings)
            {
                if (link == null || link.card == null || link.building == null) continue;
                var anchor = CellAnchor(grid.IndexOf(link.card));
                if (anchor == null) continue;
                var target = anchor.position + Vector3.up * link.heightOffset;
                if (animate && link.moving && (link.target - target).sqrMagnitude < .000001f) continue;
                link.target = target;
                link.from = link.building.position;
                link.elapsed = 0f;
                var direction = Vector3.ProjectOnPlane(link.target - link.from, Vector3.up);
                // Opposite journeys use opposite arcs, so exchanged buildings pass beside one another.
                link.side = Vector3.Cross(Vector3.up, direction.normalized) * Mathf.Min(passingArc, direction.magnitude * .6f);
                link.moving = animate && direction.sqrMagnitude > .000001f;
                if (!link.moving) link.building.position = link.target;
            }
            layoutDirty = true;
            if (!IsAnimating) FinishPlacement();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            AdvancePlacement(Time.unscaledDeltaTime);
        }

        private void AdvancePlacement(float seconds)
        {
            foreach (var link in buildings)
            {
                if (link == null || !link.moving) continue;
                if (link.building == null) { link.moving = false; continue; }
                link.elapsed += Mathf.Max(0f, seconds);
                var t = Mathf.Clamp01(link.elapsed / Mathf.Max(.2f, movementSeconds));
                var ease = t * t * t * (t * (t * 6f - 15f) + 10f);
                var arch = 4f * ease * (1f - ease);
                link.building.position = Vector3.LerpUnclamped(link.from, link.target, ease) +
                    link.side * arch + Vector3.up * (liftHeight * arch * arch);
                if (t < 1f) continue;
                link.building.position = link.target;
                link.moving = false;
            }
            if (layoutDirty && !IsAnimating) FinishPlacement();
        }

        private void PrepareForDisaster()
        {
            if (game != null) game.CancelBuildingDrag();
            FinishPlacement();
        }

        private void FinishPlacement()
        {
            foreach (var link in buildings)
            {
                if (link == null || !link.moving) continue;
                if (link.building != null) link.building.position = link.target;
                link.moving = false;
            }
            if (!layoutDirty) return;
            layoutDirty = false;
            if (tsunami != null) tsunami.RefreshCityLayout();
        }

        private void OnDisable()
        {
            if (grid != null)
            {
                grid.LayoutChanged -= OnLayoutChanged;
                grid.PlacementLockRequested -= PlacementLocked;
                grid.LayoutAnimationRequested -= Animating;
            }
            CityLayoutEvents.PreparingDisaster -= PrepareForDisaster;
            if (started) FinishPlacement();
        }
    }
}
