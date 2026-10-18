using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private DraggableCard card;
        [SerializeField] private BlueprintVaultSlot vaultSlot;
        [SerializeField] private CardFeedback feedback;
        [SerializeField] private RectTransform details;
        [SerializeField] private Image hoverOutline;
        [Tooltip("Prefer the authored right side when there is room. The last grid column opens left.")]
        [SerializeField] private bool keepDetailsOnRight;
        private RectTransform viewport;
        private Graphic[] detailGraphics;
        private Transform originalParent;
        private Vector2 originalPosition;
        private Vector2 detailMin, detailMax, detailPivot, detailPosition;
        private bool selected;
        private bool showing;
        private int vaultSiblingIndex;
        private RectTransform raisedLayer;
        private int raisedSiblingIndex;
        private readonly Vector3[] corners = new Vector3[4];

        public RectTransform Details => details;
        private CardGameController Owner => card != null ? card.Owner : vaultSlot.Owner;
        private RectTransform Preview => card != null ? card.Rect : vaultSlot.CardRect;
        private bool CanShowHover => card != null ? card.CanShowHover : vaultSlot.CanShowHover;

        private void Awake()
        {
            if ((card == null && vaultSlot == null) || feedback == null || details == null) { enabled = false; return; }
            viewport = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            detailMin = details.anchorMin;
            detailMax = details.anchorMax;
            detailPivot = details.pivot;
            detailPosition = details.anchoredPosition;
            detailGraphics = details.GetComponentsInChildren<Graphic>(true);
            details.gameObject.SetActive(false);
            if (hoverOutline != null) hoverOutline.enabled = false;
        }

        public void OnPointerEnter(PointerEventData eventData) => selected = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Owner != null) Owner.CloseInspection(this);
            else CancelHover();
        }

        public void OpenInspection() => selected = true;

        public void CancelHover()
        {
            selected = false;
            Hide();
        }

        private void LateUpdate()
        {
            if (!selected || !CanShowHover) { Hide(); return; }
            if (!showing)
            {
                Owner.ShowInspection(this);
                showing = true;
                originalParent = Preview.parent;
                originalPosition = Preview.anchoredPosition;
                Preview.SetAsLastSibling();
                if (card != null && card.IsBuilding && Preview.parent.GetComponentInParent<CardGrid>() != null)
                    Preview.parent.SetAsLastSibling();
                if (vaultSlot != null)
                {
                    vaultSiblingIndex = vaultSlot.VaultArea.GetSiblingIndex();
                    vaultSlot.VaultArea.SetAsLastSibling();
                    vaultSlot.transform.SetAsLastSibling();
                }
                var sidebar = GetComponentInParent<CardSidebar>();
                var grid = card != null && card.IsBuilding ? Preview.parent.GetComponentInParent<CardGrid>() : null;
                raisedLayer = sidebar != null ? sidebar.Area : grid != null ? (RectTransform)grid.transform.parent : null;
                if (raisedLayer != null)
                {
                    raisedSiblingIndex = raisedLayer.GetSiblingIndex();
                    raisedLayer.SetAsLastSibling();
                }
                feedback.SetHovered(true);
                details.gameObject.SetActive(true);
                if (hoverOutline != null) hoverOutline.enabled = true;
            }

            // The saved layout stays intact; only the expanded preview is kept inside the display.
            Preview.anchoredPosition = originalPosition;
            KeepInsideViewport(Preview);
            RestoreDetailsPlacement();
            Rect bounds = BoundsInViewport(details);
            Rect area = viewport.rect;
            int cell = card != null && card.IsBuilding && Owner.Grid != null ? Owner.Grid.IndexOf(card) : -1;
            bool rightmostColumn = cell >= 0 && cell % CardGrid.Columns == CardGrid.Columns - 1;
            bool onRight = bounds.center.x > BoundsInViewport(Preview).center.x;
            // Choose the panel's side without moving the card away from the pointer.
            if (rightmostColumn && onRight || bounds.xMax > area.xMax - 8f ||
                !keepDetailsOnRight && bounds.xMin < area.xMin + 8f)
            {
                details.anchorMin = new Vector2(1f - detailMax.x, detailMin.y);
                details.anchorMax = new Vector2(1f - detailMin.x, detailMax.y);
                details.pivot = new Vector2(1f - detailPivot.x, detailPivot.y);
                details.anchoredPosition = new Vector2(-detailPosition.x, detailPosition.y);
            }
            KeepInsideViewport(details);
        }

        private void Hide()
        {
            if (hoverOutline != null) hoverOutline.enabled = false;
            if (!showing) return;
            showing = false;
            if (Preview.parent == originalParent) Preview.anchoredPosition = originalPosition;
            // Unity forbids reordering siblings while their parent is being disabled on scene exit.
            if (vaultSlot != null && vaultSlot.VaultArea.parent.gameObject.activeInHierarchy)
                vaultSlot.VaultArea.SetSiblingIndex(vaultSiblingIndex);
            if (raisedLayer != null && raisedLayer.parent.gameObject.activeInHierarchy)
                raisedLayer.SetSiblingIndex(raisedSiblingIndex);
            raisedLayer = null;
            details.gameObject.SetActive(false);
            RestoreDetailsPlacement();
            feedback.SetHovered(false);
        }

        private void RestoreDetailsPlacement()
        {
            details.anchorMin = detailMin;
            details.anchorMax = detailMax;
            details.pivot = detailPivot;
            details.anchoredPosition = detailPosition;
        }

        private Rect BoundsInViewport(RectTransform rect)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            IncludeBounds(rect, ref min, ref max);
            // Authored information cards can extend beyond their shared root without resizing it.
            if (rect == details && detailGraphics != null)
                foreach (var graphic in detailGraphics)
                    if (graphic != null && graphic.isActiveAndEnabled)
                        IncludeBounds(graphic.rectTransform, ref min, ref max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void IncludeBounds(RectTransform rect, ref Vector2 min, ref Vector2 max)
        {
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 point = viewport.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }

        private void KeepInsideViewport(RectTransform rect)
        {
            Rect bounds = BoundsInViewport(rect);
            Rect area = viewport.rect;
            float x = bounds.xMin < area.xMin + 8f ? area.xMin + 8f - bounds.xMin :
                bounds.xMax > area.xMax - 8f ? area.xMax - 8f - bounds.xMax : 0f;
            float y = bounds.yMin < area.yMin + 8f ? area.yMin + 8f - bounds.yMin :
                bounds.yMax > area.yMax - 8f ? area.yMax - 8f - bounds.yMax : 0f;
            rect.position += viewport.TransformVector(new Vector3(x, y));
        }

        private void OnDisable()
        {
            selected = false;
            Hide();
        }
    }
}
