using System.Collections.Generic;
using UnityEngine;

namespace CivilGameJam.Cards
{
    [ExecuteAlways]
    [DefaultExecutionOrder(250)]
    public sealed class CardSidebar : MonoBehaviour
    {
        [SerializeField] private CardGameController game;
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private RectTransform[] slots;
        [SerializeField] private RectTransform peopleArea;
        [SerializeField] private RectTransform upgradesArea;
        [Min(1f)] [SerializeField] private float width = 560f;
        [Min(0f)] [SerializeField] private float margin = 24f;

        public RectTransform Area => (RectTransform)transform;
        public IReadOnlyList<RectTransform> Slots => slots;
        public RectTransform PeopleArea => peopleArea;
        public RectTransform UpgradesArea => upgradesArea;
        private readonly Dictionary<DraggableCard, Vector2> rememberedPositions = new Dictionary<DraggableCard, Vector2>();
        private RectTransform InventoryFor(DraggableCard card) => card.IsPerson ? peopleArea : card.Kind == CardKind.Upgrade ? upgradesArea : null;
        public bool Owns(DraggableCard card) => InventoryFor(card) is RectTransform area
            ? card.Rect.IsChildOf(area) : peopleArea == null && card.IsPerson && card.Rect.IsChildOf(transform);

        private void Update() => RefreshLayout();

        public void RefreshLayout()
        {
            if (canvasRoot == null || !(transform.parent is RectTransform parent) || canvasRoot.rect.height <= 0f) return;
            var topLeft = parent.InverseTransformPoint(canvasRoot.TransformPoint(
                new Vector3(canvasRoot.rect.xMin + margin, canvasRoot.rect.yMax - margin)));
            var bottomRight = parent.InverseTransformPoint(canvasRoot.TransformPoint(
                new Vector3(canvasRoot.rect.xMin + margin + width, canvasRoot.rect.yMin + margin)));
            Area.anchorMin = Area.anchorMax = Area.pivot = Vector2.up;
            Area.sizeDelta = new Vector2(bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
            Area.anchoredPosition = (Vector2)topLeft - new Vector2(parent.rect.xMin, parent.rect.yMax);
        }

        public bool TrySnap(DraggableCard card, Vector3 position)
        {
            if (!Area.rect.Contains(Area.InverseTransformPoint(position))) return false;
            var area = InventoryFor(card);
            if (area != null)
                return area.rect.Contains(area.InverseTransformPoint(position))
                    ? ParkAt(card, area, area.InverseTransformPoint(position)) : TryPark(card);
            return TryPark(card, position);
        }

        public bool TryPark(DraggableCard card)
        {
            var area = InventoryFor(card);
            if (area == null) return TryPark(card, null);
            if (card.Owner != game || card.IsStored) return false;
            Vector2 size = Vector2.Scale(card.Rect.rect.size, card.LayoutScale);
            bool upgrade = area == upgradesArea;
            float inset = upgrade ? 8f : 12f;
            Vector2 stride = size + Vector2.one * (upgrade ? 8f : 16f);
            float firstX = area.rect.xMin + size.x / 2f + inset;
            if (upgrade)
            {
                int columns = Mathf.Clamp(Mathf.FloorToInt((area.rect.width - inset * 2f) / (size.x + 8f)), 1, 3);
                stride.x = (area.rect.width - inset * 2f) / columns;
                firstX = area.rect.xMin + inset + stride.x / 2f;
            }
            float firstY = area.rect.yMax - size.y / 2f - inset;
            Vector2 desired = rememberedPositions.TryGetValue(card, out var previous) ? previous
                : upgrade ? new Vector2(firstX, firstY) : Vector2.zero;
            desired = ClampPosition(card, area, desired);
            if (HasRoom(card, area, desired, size)) return ParkAt(card, area, desired);
            for (float y = firstY; y >= area.rect.yMin + size.y / 2f + inset; y -= stride.y)
                for (float x = firstX; x <= area.rect.xMax - size.x / 2f - inset; x += stride.x)
                {
                    Vector2 candidate = new Vector2(x, y);
                    if (HasRoom(card, area, candidate, size)) return ParkAt(card, area, candidate);
                }
            if (upgrade)
            {
                int count = 0;
                foreach (var other in game.Cards)
                    if (other != card && other.Kind == CardKind.Upgrade && !other.IsStored && Owns(other)) count++;
                float offset = 10f * (1 + count % 4);
                desired += new Vector2(offset, -offset);
            }
            return ParkAt(card, area, desired);
        }

        public void RememberPosition(DraggableCard card)
        {
            var area = InventoryFor(card);
            if (area != null && Owns(card))
            {
                Vector2 point = area.InverseTransformPoint(card.Rect.position);
                if (area.rect.Contains(point)) rememberedPositions[card] = point;
            }
        }

        internal void ForgetPosition(DraggableCard card) => rememberedPositions.Remove(card);

        public void TransferRememberedPosition(DraggableCard from, DraggableCard to)
        {
            if (!rememberedPositions.TryGetValue(from, out var position)) return;
            rememberedPositions.Remove(from);
            rememberedPositions[to] = position;
        }

        private bool ParkAt(DraggableCard card, RectTransform area, Vector2 position)
        {
            if (card.Owner != game || InventoryFor(card) != area || card.IsStored) return false;
            card.StopFlight();
            card.Rect.SetParent(area, true);
            card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            // No layout group or slot snapping: the player's drop position is retained.
            position = ClampPosition(card, area, position);
            card.Rect.position = area.TransformPoint(position);
            card.IsParkedInFactoryOutput = card.Kind == CardKind.Upgrade;
            card.Rect.SetAsLastSibling();
            rememberedPositions[card] = position;
            return true;
        }

        private Vector2 ClampPosition(DraggableCard card, RectTransform area, Vector2 position)
        {
            Vector2 half = Vector2.Scale(card.Rect.rect.size, card.LayoutScale) * 0.5f;
            Rect bounds = area.rect;
            position.x = Mathf.Clamp(position.x, bounds.xMin + half.x + 8f, bounds.xMax - half.x - 8f);
            position.y = Mathf.Clamp(position.y, bounds.yMin + half.y + 8f, bounds.yMax - half.y - 8f);
            return position;
        }

        private bool HasRoom(DraggableCard incoming, RectTransform area, Vector2 position, Vector2 size)
        {
            foreach (var other in game.Cards)
            {
                if (other == incoming || InventoryFor(other) != area || other.IsStored || !other.gameObject.activeSelf || !Owns(other)) continue;
                Vector2 target = area.InverseTransformPoint(game.CardLayer.TransformPoint(other.TablePosition));
                Vector2 separation = (size + Vector2.Scale(other.Rect.rect.size, other.LayoutScale)) / 2f + Vector2.one * 8f;
                if (Mathf.Abs(target.x - position.x) < separation.x && Mathf.Abs(target.y - position.y) < separation.y) return false;
            }
            return true;
        }

        private bool TryPark(DraggableCard card, Vector3? preferredPosition)
        {
            if (card.Owner != game || !card.IsPerson || card.IsStored) return false;
            RectTransform target = null;
            float distance = float.PositiveInfinity;
            foreach (var slot in slots)
            {
                bool occupied = false;
                foreach (var other in slot.GetComponentsInChildren<DraggableCard>())
                    if (other != card && !other.IsStored) { occupied = true; break; }
                if (occupied) continue;
                float nextDistance = preferredPosition.HasValue ? (slot.position - preferredPosition.Value).sqrMagnitude : 0f;
                if (target != null && nextDistance >= distance) continue;
                target = slot; distance = nextDistance;
            }
            if (target == null) return false;
            card.Rect.SetParent(target, true);
            card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            card.Rect.anchoredPosition = Vector2.zero;
            card.Rect.SetAsLastSibling();
            return true;
        }
    }
}
