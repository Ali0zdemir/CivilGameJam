using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class DraggableCard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private CardGameController game;
        private CanvasGroup canvasGroup;
        [SerializeField] private CardKind kind;
        [SerializeField] private CardFeedback feedback;
        [SerializeField] private BuildingStation station;
        [SerializeField] private CardDropTargetFeedback dropTargetFeedback;
        [SerializeField] private UpgradeKind upgradeType;
        [SerializeField] private Image newItemOutline;
        [SerializeField] private Image unavailableOverlay;
        [Tooltip("Starting grid slot, numbered from 0. Use -1 for loose cards or automatic placement.")]
        [SerializeField] private int initialGridCell = -1;
        private RectTransform rect;
        private bool pressed;
        private Vector2 dragPointer;
        private Vector3 dragGrip;
        private Camera dragCamera;
        private Transform dragStartParent;
        private Vector3 dragStartLocal;
        private Coroutine flight;
        private WorkerSlot returningFrom;
        private FactoryFolderPresentation returningBlueprintFrom;
        private Transform flightSpace;
        private Vector3 flightOrigin;
        private Vector3 flightDestination;
        private int pointerId;
        private CardHover hover;
        private BlueprintDiscoveryEffects discoveryEffects;
        private HouseUpgrades houseUpgrades;
        private FactoryProduction factory;

        public CardKind Kind => kind;
        public RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;
        public BuildingStation Station => station;
        public CardDropTargetFeedback DropTargetFeedback => dropTargetFeedback;
        public UpgradeKind UpgradeType => upgradeType;
        public CardGameController Owner => game;
        public FactoryProduction StoredAtFactory { get; internal set; }
        public BlueprintVault StoredInVault { get; internal set; }
        public HouseUpgrades InstalledIn { get; internal set; }
        public bool IsStored => AssignedTo != null || StoredAtFactory != null || InstalledIn != null || StoredInVault != null;
        public int InitialGridCell => initialGridCell;
        public BuildingStation AssignedTo { get; internal set; }
        public bool IsDragging { get; private set; }
        public bool IsHeld => pressed || IsDragging;
        public bool IsMovingInGrid => game != null && game.Grid != null && game.Grid.IsMoving(this);
        public bool IsFlying => flight != null || IsReturningFromFolder || returningBlueprintFrom != null;
        public bool IsReturningFromFolder => returningFrom != null;
        public bool IsNewProduct { get; private set; }
        public bool IsParkedInFactoryOutput { get; internal set; }
        public bool IsInstallingUpgrade => houseUpgrades != null && houseUpgrades.IsInstalling;
        public bool IsStoringBlueprint => factory != null && factory.IsAnimatingBlueprint;
        public bool UsedThisTurn { get; private set; }
        public bool CanStartWork => IsPerson && !UsedThisTurn;

        internal void UseForThisTurn()
        {
            if (!IsPerson) return;
            UsedThisTurn = true;
            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            bool returning = IsReturningFromFolder || returningBlueprintFrom != null;
            if (unavailableOverlay != null)
                unavailableOverlay.gameObject.SetActive(IsPerson && UsedThisTurn && !returning);
            if (canvasGroup == null) return;
            canvasGroup.alpha = returning ? 0f : UsedThisTurn ? .45f : 1f;
            canvasGroup.interactable = !UsedThisTurn;
            canvasGroup.blocksRaycasts = !IsReturningFromFolder && returningBlueprintFrom == null;
        }

        public bool IsPerson => Kind == CardKind.Person || Kind == CardKind.EducatedPerson;
        public bool IsBuilding => Kind == CardKind.House || Kind == CardKind.School ||
            Kind == CardKind.Factory || Kind == CardKind.ResearchCenter;
        public Vector3 LayoutScale => feedback.LayoutScale;
        public bool CanShowHover => game != null && !game.ActionsLocked && !game.IsTutorialActive && !game.IsChoosingBlueprint && !IsDragging &&
            !game.IsDraggingAnyCard && !IsStored && !IsFlying && !IsMovingInGrid && !IsInstallingUpgrade && !IsStoringBlueprint && (station == null || !station.IsAnimatingWorkers);
        public Vector2 TablePosition => game != null
            ? (Vector2)game.CardLayer.InverseTransformPoint(flight != null
                ? flightSpace.TransformPoint(flightDestination) : Rect.position) : Rect.anchoredPosition;

        public void MarkAsNewProduct()
        {
            if (Kind != CardKind.Upgrade) return;
            IsNewProduct = true;
            if (newItemOutline != null) newItemOutline.enabled = true;
        }

        private void AcknowledgeProduct()
        {
            IsNewProduct = false;
            if (newItemOutline != null) newItemOutline.enabled = false;
        }

        public void Initialize(CardGameController owner)
        {
            game = owner;
            canvasGroup = GetComponent<CanvasGroup>();
            hover = GetComponent<CardHover>();
            discoveryEffects = GetComponent<BlueprintDiscoveryEffects>();
            houseUpgrades = GetComponent<HouseUpgrades>();
            // Normalize anchors without moving the editor-authored card on screen.
            Vector3 position = Rect.position;
            Rect.anchorMin = Rect.anchorMax = new Vector2(0.5f, 0.5f);
            Rect.position = position;
            if (station != null) station.Initialize(owner, this);
            factory = GetComponent<FactoryProduction>();
            factory?.Initialize(owner, this);
            GetComponent<HouseUpgrades>()?.Initialize(owner, this);
            RefreshAvailability();
        }

        public void SetLayoutScale(Vector3 scale) => feedback.SetLayoutScale(scale);

        public void MatchArtworkWidth(float worldWidth, Quaternion? worldRotation = null)
        {
            var artwork = transform.Find("Artwork") as RectTransform ?? Rect;
            float width = artwork.rect.width * artwork.lossyScale.x;
            if (width <= 0f) return;
            feedback.SetDisplayedScale(Rect.localScale.x / LayoutScale.x * worldWidth / width);
            if (worldRotation.HasValue) feedback.SetDisplayedRotation(Quaternion.Inverse(Rect.parent.rotation) * worldRotation.Value);
            feedback.Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || pressed || IsDragging ||
                IsStored || UsedThisTurn || IsReturningFromFolder || game.IsChoosingBlueprint || game.ActionsLocked || !game.CanInteractWithCard(this) ||
                IsBuilding && game.BuildingPlacementLocked ||
                houseUpgrades != null && houseUpgrades.IsDestroyed) return;
            pressed = true;
            pointerId = eventData.pointerId;
            AcknowledgeProduct();
            StopFlight();
            Rect.SetAsLastSibling();
            feedback.PickUp();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed || eventData.button != PointerEventData.InputButton.Left ||
                eventData.pointerId != pointerId) return;
            pressed = false;
            // Unity sends pointer-up before end-drag; finish placement before playing the drop effect.
            if (!IsDragging) feedback.Release();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || IsDragging ||
                IsStored || UsedThisTurn || IsReturningFromFolder || game.IsChoosingBlueprint || game.ActionsLocked || !game.CanInteractWithCard(this) ||
                IsBuilding && game.BuildingPlacementLocked ||
                houseUpgrades != null && houseUpgrades.IsDestroyed ||
                (pressed && pointerId != eventData.pointerId)) return;

            // Closing an edge-clamped hover restores its layout position. Keep the visible
            // card under the cursor while remembering that layout position for rejected drops.
            Vector3 visiblePosition = Rect.position;
            dragStartParent = Rect.parent;
            StopFlight();
            game.Grid?.StopMoving(this);
            game.BeginCardDrag(this);
            hover?.CancelHover();
            dragStartLocal = Rect.localPosition;
            game.Sidebar?.RememberPosition(this);
            Rect.position = visiblePosition;
            IsDragging = true;
            IsParkedInFactoryOutput = false;
            pressed = true;
            pointerId = eventData.pointerId;
            AcknowledgeProduct();
            Rect.SetParent(game.CardLayer, true);
            Rect.SetAsLastSibling();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.97f;
            feedback.PickUp();
            dragPointer = eventData.position;
            dragCamera = eventData.pressEventCamera;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(game.CardLayer, dragPointer, dragCamera, out var grip);
            dragGrip = Rect.InverseTransformPoint(grip);
            feedback.BeginDragMotion();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Time.timeScale <= 0f) return;
            if (!IsDragging || eventData.pointerId != pointerId) return;
            Vector3 previous = Rect.position;
            dragPointer = eventData.position;
            KeepDragGrip();
            feedback.TrackDrag(game.CardLayer.InverseTransformVector(Rect.position - previous));
            game.PreviewDrop(this, eventData.position, eventData.pressEventCamera);
        }

        internal void CancelBuildingDrag()
        {
            if (!IsBuilding || !IsDragging) return;
            IsDragging = false;
            pressed = false;
            feedback.EndDragMotion();
            game.EndCardDrag(this);
            RefreshAvailability();
            var cell = game.Grid.IndexOf(this);
            if (cell >= 0) game.Grid.TryPlace(this, cell);
            feedback.Release();
        }

        internal void KeepDragGrip()
        {
            if (!IsDragging || game == null) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(game.CardLayer, dragPointer, dragCamera, out var pointer))
            {
                Rect.position += pointer - Rect.TransformPoint(dragGrip);
                game.ClampToTable(this);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != pointerId) return;
            IsDragging = false;
            feedback.EndDragMotion();
            game.EndCardDrag(this);
            pressed = false;
            RefreshAvailability();
            Vector3 dragStart = dragStartParent != null ? dragStartParent.TransformPoint(dragStartLocal) : Rect.position;
            game.FinishDrop(this, eventData.position, eventData.pressEventCamera, dragStart);
            if (AssignedTo != null)
            {
                if (!AssignedTo.HasFolderPresentation) AssignedTo.Card.PlayDropFeedback();
            }
            else PlayDropFeedback();
        }

        private void PlayDropFeedback()
        {
            if (isActiveAndEnabled && !pressed && !IsDragging) feedback.Release();
        }

        private void OnDisable()
        {
            game?.Grid?.StopMoving(this, true);
            StopFlight();
            pressed = false;
            IsDragging = false;
            game?.EndCardDrag(this);
            if (canvasGroup == null) return;
            RefreshAvailability();
        }

        public void FlyFrom(Vector3 origin, Vector3 destination)
        {
            StopFlight();
            flightSpace = Rect.parent;
            flightOrigin = flightSpace.InverseTransformPoint(origin);
            flightDestination = flightSpace.InverseTransformPoint(destination);
            flight = StartCoroutine(Fly());
        }

        public void SetTablePosition(Vector2 position)
        {
            if (flight != null)
            {
                Vector3 delta = flightSpace.InverseTransformVector(game.CardLayer.TransformVector(position - TablePosition));
                flightOrigin += delta;
                flightDestination += delta;
                Rect.localPosition += delta;
            }
            else Rect.position = game.CardLayer.TransformPoint(position);
        }

        public void StopFlight()
        {
            discoveryEffects?.StopEffects();
            if (returningFrom != null) returningFrom.CancelExit(this);
            if (returningBlueprintFrom != null) returningBlueprintFrom.CancelReturn(this);
            if (flight == null) return;
            StopCoroutine(flight);
            flight = null;
        }

        internal void BeginBlueprintReturn(FactoryFolderPresentation source)
        {
            StopFlight();
            returningBlueprintFrom = source;
            RefreshAvailability();
        }

        internal void EndBlueprintReturn(FactoryFolderPresentation source)
        {
            if (returningBlueprintFrom != source) return;
            returningBlueprintFrom = null;
            RefreshAvailability();
        }

        internal void BeginFolderReturn(WorkerSlot source)
        {
            StopFlight();
            returningFrom = source;
            feedback.SetDisplayedScale(1f);
            RefreshAvailability();
        }

        internal void EndFolderReturn(WorkerSlot source)
        {
            if (returningFrom != source) return;
            returningFrom = null;
            RefreshAvailability();
        }

        private IEnumerator Fly()
        {
            const float duration = 0.45f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / duration;
                float ease = 1f - (1f - t) * (1f - t);
                Rect.localPosition = Vector3.Lerp(flightOrigin, flightDestination, ease) +
                                     Vector3.up * (Mathf.Sin(t * Mathf.PI) * 65f);
                discoveryEffects?.SetFlightProgress(t);
                yield return null;
            }
            Rect.localPosition = flightDestination;
            discoveryEffects?.StopEffects();
            flight = null;
        }
    }
}
