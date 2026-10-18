using UnityEngine;

namespace CivilGameJam.Cards
{
    // Moves the saved board; all cards, hit areas and the 3D world are scene objects.
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class CardDock : MonoBehaviour
    {
        [SerializeField] private CardGameController game;
        [SerializeField] private RectTransform panel;
        [SerializeField] private RectTransform activationArea;
        [SerializeField] private bool startExpanded = true;
        [Min(0.01f)] [SerializeField] private float slideSeconds = 0.32f;
        [Min(1f)] [SerializeField] private float collapsedHeight = 497.112f;
        [Min(0f)] [SerializeField] private float collapsedReferenceHeight;
        private Canvas canvas;
        private Vector2 expandedPosition;
        private float expansion = 1f;
        private float fromExpansion;
        private float elapsed;
        private Vector2 layerSize;
        private bool cityPresentation;

        public bool IsExpanded { get; private set; }
        public float Expansion => expansion;
        public bool IsCityPresentationActive => cityPresentation;
        public RectTransform ActivationArea => activationArea;
        private float PeekHeight => collapsedReferenceHeight > 0f
            ? collapsedHeight * panel.rect.height / collapsedReferenceHeight : collapsedHeight;
        public float CollapsedCanvasHeight => panel == null ? 0f : Mathf.Min(PeekHeight, panel.rect.height) * Mathf.Abs(panel.localScale.y);

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            expandedPosition = panel.anchoredPosition;
            layerSize = game.CardLayer.rect.size;
            SetExpanded(startExpanded, true);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (canvas == null || panel == null || game == null) return;
            Vector2 size = game.CardLayer.rect.size;
            Vector2 change = size - layerSize;
            layerSize = size;
            if (change.sqrMagnitude > 0.0001f && game.Cards.Count > 0)
            {
                game.CloseInspection();
                // The saved grid and output tray use the top-left corner. Flights and
                // loose cards follow that same corner when the display's aspect changes.
                foreach (var card in game.Cards)
                {
                    if (card.IsBuilding || card.IsStored || card.IsDragging || (game.Sidebar != null && game.Sidebar.Owns(card))) continue;
                    var position = card.TablePosition;
                    position += new Vector2(-0.5f * change.x, 0.5f * change.y);
                    card.SetTablePosition(position);
                    game.ClampToTable(card);
                }
            }
            ApplyPosition();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Vector2 pointer = Input.mousePresent ? (Vector2)Input.mousePosition : new Vector2(-1f, Screen.height + 1f);
            TickPointer(pointer, Time.unscaledDeltaTime);
        }

        public void TickPointer(Vector2 screenPosition, float deltaTime)
        {
            bool pointerInside = ContainsPointer(screenPosition);
            bool directInteraction = game.IsDraggingAnyCard || game.IsChoosingBlueprint || game.TutorialKeepsBoardOpen;
            SetExpanded(directInteraction || (!cityPresentation && (game.HasActiveCardAnimation || pointerInside)));
            elapsed += Mathf.Max(0f, deltaTime);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, slideSeconds));
            t = t * t * t * (t * (t * 6f - 15f) + 10f);
            expansion = Mathf.Lerp(fromExpansion, IsExpanded ? 1f : 0f, t);
            ApplyPosition();
        }

        public void BeginCityPresentation()
        {
            cityPresentation = true;
            SetExpanded(false, !isActiveAndEnabled);
        }

        public void EndCityPresentation()
        {
            if (!cityPresentation) return;
            cityPresentation = false;
        }

        public void SetExpanded(bool value, bool immediate = false)
        {
            if (IsExpanded != value)
            {
                IsExpanded = value;
                fromExpansion = expansion;
                elapsed = 0f;
                if (!value) game.CloseInspection();
            }
            if (!immediate) return;
            expansion = fromExpansion = value ? 1f : 0f;
            elapsed = slideSeconds;
            ApplyPosition();
        }

        private bool ContainsPointer(Vector2 point)
        {
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(activationArea, point, camera) ||
                RectTransformUtility.RectangleContainsScreenPoint(panel, point, camera) ||
                (game.Sidebar != null && RectTransformUtility.RectangleContainsScreenPoint(game.Sidebar.Area, point, camera))) return true;
            foreach (var card in game.Cards)
                if (!card.IsStored && card.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(card.Rect, point, camera)) return true;
            return false;
        }

        private void ApplyPosition()
        {
            var dragged = game.DraggedCard;
            Vector3 dragPosition = dragged != null ? dragged.Rect.position : Vector3.zero;
            float offset = Mathf.Min(0f, PeekHeight - panel.rect.height) * panel.localScale.y;
            panel.anchoredPosition = expandedPosition + Vector2.up * (offset * (1f - expansion));
            if (game.Sidebar != null) game.Sidebar.RefreshLayout();
            if (dragged != null) dragged.Rect.position = dragPosition;
        }
    }
}
