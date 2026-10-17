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
        [SerializeField] private UpgradeKind upgradeType;
        [SerializeField] private Image newItemOutline;
        [Tooltip("Starting grid slot, numbered from 0. Use -1 for loose cards or automatic placement.")]
        [SerializeField] private int initialGridCell = -1;
        private RectTransform rect;
        private bool pressed;
        private Vector2 dragScreenOffset;
        private Transform dragStartParent;
        private Vector3 dragStartLocal;
        private Coroutine flight;
        private Transform flightSpace;
        private Vector3 flightOrigin;
        private Vector3 flightDestination;
        private int pointerId;
        private CardHover hover;

        public CardKind Kind => kind;
        public RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;
        public BuildingStation Station => station;
        public UpgradeKind UpgradeType => upgradeType;
        public CardGameController Owner => game;
        public FactoryProduction StoredAtFactory { get; internal set; }
        public BlueprintVault StoredInVault { get; internal set; }
        public HouseUpgrades InstalledIn { get; internal set; }
        public bool IsStored => AssignedTo != null || StoredAtFactory != null || InstalledIn != null || StoredInVault != null;
        public int InitialGridCell => initialGridCell;
        public BuildingStation AssignedTo { get; internal set; }
        public bool IsDragging { get; private set; }
        public bool IsFlying => flight != null;
        public bool IsNewProduct { get; private set; }
        public bool IsParkedInFactoryOutput { get; internal set; }
        public bool IsPerson => Kind == CardKind.Person || Kind == CardKind.EducatedPerson;
        public bool IsBuilding => Kind == CardKind.House || Kind == CardKind.School ||
            Kind == CardKind.Factory || Kind == CardKind.ResearchCenter;
        public Vector3 LayoutScale => feedback.LayoutScale;
        public bool CanShowHover => game != null && !game.IsChoosingBlueprint && !IsDragging &&
            !game.IsDraggingAnyCard && !IsStored && flight == null && (station == null || !station.IsReceivingWorker);
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
            // Normalize anchors without moving the editor-authored card on screen.
            Vector3 position = Rect.position;
            Rect.anchorMin = Rect.anchorMax = new Vector2(0.5f, 0.5f);
            Rect.position = position;
            if (station != null) station.Initialize(owner, this);
            GetComponent<FactoryProduction>()?.Initialize(owner, this);
            GetComponent<HouseUpgrades>()?.Initialize(owner, this);
        }

        public void SetLayoutScale(Vector3 scale) => feedback.SetLayoutScale(scale);

        public void MatchArtworkWidth(float worldWidth)
        {
            var artwork = transform.Find("Artwork") as RectTransform ?? Rect;
            float width = artwork.rect.width * artwork.lossyScale.x;
            if (width <= 0f) return;
            feedback.SetDisplayedScale(Rect.localScale.x / LayoutScale.x * worldWidth / width);
            feedback.Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || pressed || IsDragging ||
                IsStored || game.IsChoosingBlueprint) return;
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
                IsStored || game.IsChoosingBlueprint ||
                (pressed && pointerId != eventData.pointerId)) return;

            // Closing an edge-clamped hover restores its layout position. Keep the visible
            // card under the cursor while remembering that layout position for rejected drops.
            Vector3 visiblePosition = Rect.position;
            dragStartParent = Rect.parent;
            StopFlight();
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
            dragScreenOffset = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, Rect.position) - eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != pointerId) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(game.CardLayer, eventData.position + dragScreenOffset,
                    eventData.pressEventCamera, out var pointer))
            {
                Rect.position = pointer;
                game.ClampToTable(this);
            }
            game.PreviewDrop(this, eventData.position, eventData.pressEventCamera);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData.pointerId != pointerId) return;
            IsDragging = false;
            game.EndCardDrag(this);
            pressed = false;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
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
            StopFlight();
            pressed = false;
            IsDragging = false;
            game?.EndCardDrag(this);
            if (canvasGroup == null) return;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
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
            if (flight == null) return;
            StopCoroutine(flight);
            flight = null;
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
                yield return null;
            }
            Rect.localPosition = flightDestination;
            flight = null;
        }
    }
}
