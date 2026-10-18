using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    // Uses editor-authored slots for snapping; only the current drop target is highlighted.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardGrid : MaskableGraphic
    {
        public const int Columns = 4;
        public const int Rows = 2;
        public const int CellCount = Columns * Rows;
        [SerializeField] private RectTransform[] cells = new RectTransform[CellCount];
        private readonly DraggableCard[] occupants = new DraggableCard[CellCount];
        private int previewIndex = -1;
        [Header("Occupied slot swap")]
        [Min(0.1f)] [SerializeField] private float swapSeconds = 0.36f;
        [Min(0f)] [SerializeField] private float swapArc = 28f;
        private readonly List<SlotMove> moves = new List<SlotMove>();

        public event System.Action<bool> LayoutChanged;
        public event System.Func<bool> PlacementLockRequested;
        public event System.Func<bool> LayoutAnimationRequested;

        private sealed class SlotMove
        {
            public DraggableCard card;
            public CardFeedback feedback;
            public int cell;
            public Vector3 offset;
            public float elapsed;
        }

        public bool IsAnimating => moves.Count > 0 || Any(LayoutAnimationRequested);
        public bool IsPlacementLocked => Any(PlacementLockRequested);

        private static bool Any(System.Func<bool> checks)
        {
            if (checks == null) return false;
            foreach (System.Func<bool> check in checks.GetInvocationList()) if (check()) return true;
            return false;
        }

        public int IndexOf(DraggableCard card)
        {
            if (card == null) return -1;
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] == card) return i;
            return -1;
        }

        public bool IsMoving(DraggableCard card)
        {
            foreach (var move in moves) if (move.card == card) return true;
            return false;
        }

        public Vector2 CellSize => GetCell(0) != null ? GetCell(0).rect.size :
            new Vector2(rectTransform.rect.width / Columns, rectTransform.rect.height / Rows);
        public DraggableCard GetOccupant(int index) => occupants[index];
        public RectTransform GetCell(int index) => index >= 0 && index < cells.Length ? cells[index] : null;

        public Vector3 GetCellWorldPosition(int index)
        {
            if (GetCell(index) != null) return GetCell(index).position;
            Vector2 size = CellSize;
            Rect rect = rectTransform.rect;
            return rectTransform.TransformPoint(new Vector3(rect.xMin + (index % Columns + 0.5f) * size.x,
                rect.yMax - (index / Columns + 0.5f) * size.y));
        }

        public int FindNearestCell(Vector3 worldPosition)
        {
            int closest = 0;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < CellCount; i++)
            {
                float distance = (GetCellWorldPosition(i) - worldPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                closest = i;
            }
            return closest;
        }

        public int FindNearestAvailable(DraggableCard card, Vector3 worldPosition)
        {
            int closest = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < occupants.Length; i++)
            {
                if (occupants[i] != null && occupants[i] != card) continue;
                float distance = (GetCellWorldPosition(i) - worldPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                closest = i;
            }
            return closest;
        }

        public bool TryPlace(DraggableCard card, int index) => Place(card, index, false);

        private bool Place(DraggableCard card, int index, bool animate)
        {
            if (card == null || !card.IsBuilding || index < 0 || index >= CellCount ||
                (occupants[index] != null && occupants[index] != card)) return false;
            var previous = IndexOf(card);
            if (previous != index && IsPlacementLocked) return false;
            StopMoving(card);
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] == card) occupants[i] = null;
            occupants[index] = card;
            if (animate && previous >= 0 && previous != index) BeginMove(card, index);
            else
            {
                if (cells[index] != null) card.Rect.SetParent(cells[index], true);
                card.Rect.position = GetCellWorldPosition(index);
            }
            SetPreview(-1);
            if (previous != index) LayoutChanged?.Invoke(animate);
            return true;
        }

        public bool TryPlaceOrSwap(DraggableCard card, int index)
        {
            if (card == null || !card.IsBuilding || index < 0 || index >= CellCount) return false;
            var other = occupants[index];
            if (other == null || other == card) return Place(card, index, true);
            int source = IndexOf(card);
            if (source < 0 || IsPlacementLocked || other.IsDragging || !other.isActiveAndEnabled) return false;

            // Reserve both destinations together. Workers, products and city links stay with their card.
            card.GetComponent<CardHover>()?.CancelHover();
            other.GetComponent<CardHover>()?.CancelHover();
            StopMoving(card);
            StopMoving(other);
            occupants[source] = other;
            occupants[index] = card;
            BeginMove(other, source);
            BeginMove(card, index);
            SetPreview(-1);
            LayoutChanged?.Invoke(true);
            return true;
        }

        private void BeginMove(DraggableCard card, int index)
        {
            if (GetCell(index) != null)
            {
                card.Rect.SetParent(GetCell(index), true);
                GetCell(index).SetAsLastSibling();
            }
            moves.Add(new SlotMove { card = card, cell = index, feedback = card.GetComponent<CardFeedback>(),
                offset = rectTransform.InverseTransformVector(card.Rect.position - GetCellWorldPosition(index)) });
        }

        internal void StopMoving(DraggableCard card, bool snap = false)
        {
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                if (moves[i].card != card) continue;
                if (snap && card != null) card.Rect.position = GetCellWorldPosition(moves[i].cell);
                moves.RemoveAt(i);
            }
        }

        private void Update()
        {
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                var move = moves[i];
                if (move.card == null || !move.card.isActiveAndEnabled || move.card.IsDragging)
                {
                    moves.RemoveAt(i);
                    continue;
                }
                move.elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(move.elapsed / Mathf.Max(0.1f, swapSeconds));
                float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
                Vector3 offset = move.offset * (1f - ease) + Vector3.up * (swapArc * 4f * ease * (1f - ease));
                // Use the live cell transform so sliding the dock or resizing the window cannot break the landing.
                move.card.Rect.position = GetCellWorldPosition(move.cell) + rectTransform.TransformVector(offset);
                if (t < 1f) continue;
                moves.RemoveAt(i);
                move.feedback?.Punch();
            }
        }

        protected override void OnDisable()
        {
            foreach (var move in moves)
                if (move.card != null && !move.card.IsDragging)
                    move.card.Rect.position = GetCellWorldPosition(move.cell);
            moves.Clear();
            LayoutChanged?.Invoke(false);
            base.OnDisable();
        }

        public void SnapToNearest(DraggableCard card)
        {
            if (IsPlacementLocked && IndexOf(card) >= 0)
            {
                TryPlace(card, IndexOf(card));
                return;
            }
            TryPlace(card, FindNearestAvailable(card, card.Rect.position));
        }

        public void RefreshPositions()
        {
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] != null && !occupants[i].IsDragging && !IsMoving(occupants[i]))
                    occupants[i].Rect.position = GetCellWorldPosition(i);
            SetVerticesDirty();
        }

        public void SetPreview(int index)
        {
            if (index >= 0 && IsPlacementLocked) index = -1;
            if (previewIndex == index) return;
            previewIndex = index;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var cell = GetCell(previewIndex);
            if (cell == null) return;
            var corners = new Vector3[4];
            cell.GetWorldCorners(corners);
            Vector3 bottomLeft = rectTransform.InverseTransformPoint(corners[0]);
            Vector3 topRight = rectTransform.InverseTransformPoint(corners[2]);
            Quad(mesh, bottomLeft.x, bottomLeft.y, topRight.x - bottomLeft.x, topRight.y - bottomLeft.y,
                new Color(0.2f, 0.75f, 0.45f, 0.2f));
        }

        private static void Quad(VertexHelper mesh, float x, float y, float width, float height, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, y), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x, y + height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x + width, y + height), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x + width, y), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
