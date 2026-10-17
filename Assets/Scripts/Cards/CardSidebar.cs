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
        [Min(1f)] [SerializeField] private float width = 560f;
        [Min(0f)] [SerializeField] private float margin = 24f;

        public RectTransform Area => (RectTransform)transform;
        public IReadOnlyList<RectTransform> Slots => slots;
        public RectTransform PeopleArea => peopleArea;
        private readonly Dictionary<DraggableCard, Vector2> rememberedPositions = new Dictionary<DraggableCard, Vector2>();
        public bool Owns(DraggableCard card) => card.Rect.IsChildOf(peopleArea != null ? peopleArea : transform);

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
            if (peopleArea != null)
                return peopleArea.rect.Contains(peopleArea.InverseTransformPoint(position))
                    ? ParkAt(card, peopleArea.InverseTransformPoint(position)) : TryPark(card);
            return TryPark(card, position);
        }

        public bool TryPark(DraggableCard card)
        {
            if (peopleArea == null) return TryPark(card, null);
            if (card.Owner != game || !card.IsPerson || card.IsStored) return false;
            Vector2 size = Vector2.Scale(card.Rect.rect.size, card.LayoutScale);
            Vector2 desired = rememberedPositions.TryGetValue(card, out var previous) ? previous : Vector2.zero;
            desired = ClampPosition(card, desired);
            if (HasRoom(card, desired, size)) return ParkAt(card, desired);
            Vector2 stride = size + Vector2.one * 16f;
            for (float y = peopleArea.rect.yMax - size.y / 2f - 12f; y >= peopleArea.rect.yMin + size.y / 2f + 12f; y -= stride.y)
                for (float x = peopleArea.rect.xMin + size.x / 2f + 12f; x <= peopleArea.rect.xMax - size.x / 2f - 12f; x += stride.x)
                {
                    Vector2 candidate = new Vector2(x, y);
                    if (HasRoom(card, candidate, size)) return ParkAt(card, candidate);
                }
            return ParkAt(card, desired);
        }

        public void RememberPosition(DraggableCard card)
        {
            if (peopleArea != null && card.IsPerson && Owns(card))
            {
                Vector2 point = peopleArea.InverseTransformPoint(card.Rect.position);
                if (peopleArea.rect.Contains(point)) rememberedPositions[card] = point;
            }
        }

        public void TransferRememberedPosition(DraggableCard from, DraggableCard to)
        {
            if (!rememberedPositions.TryGetValue(from, out var position)) return;
            rememberedPositions.Remove(from);
            rememberedPositions[to] = position;
        }

        private bool ParkAt(DraggableCard card, Vector2 position)
        {
            if (card.Owner != game || !card.IsPerson || card.IsStored) return false;
            card.StopFlight();
            card.Rect.SetParent(peopleArea, true);
            card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            // No layout group or slot snapping: the player's drop position is retained.
            position = ClampPosition(card, position);
            card.Rect.position = peopleArea.TransformPoint(position);
            card.Rect.SetAsLastSibling();
            rememberedPositions[card] = position;
            return true;
        }

        private Vector2 ClampPosition(DraggableCard card, Vector2 position)
        {
            Vector2 half = Vector2.Scale(card.Rect.rect.size, card.LayoutScale) * 0.5f;
            Rect bounds = peopleArea.rect;
            position.x = Mathf.Clamp(position.x, bounds.xMin + half.x + 8f, bounds.xMax - half.x - 8f);
            position.y = Mathf.Clamp(position.y, bounds.yMin + half.y + 8f, bounds.yMax - half.y - 8f);
            return position;
        }

        private bool HasRoom(DraggableCard incoming, Vector2 position, Vector2 size)
        {
            foreach (var other in game.Cards)
            {
                if (other == incoming || !other.IsPerson || other.IsStored || !other.gameObject.activeSelf || !Owns(other)) continue;
                Vector2 target = peopleArea.InverseTransformPoint(game.CardLayer.TransformPoint(other.TablePosition));
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
