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

        public bool TryPlace(DraggableCard card, int index)
        {
            if (card == null || !card.IsBuilding || index < 0 || index >= CellCount ||
                (occupants[index] != null && occupants[index] != card)) return false;
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] == card) occupants[i] = null;
            occupants[index] = card;
            if (cells[index] != null) card.Rect.SetParent(cells[index], true);
            card.Rect.position = GetCellWorldPosition(index);
            SetPreview(-1);
            return true;
        }

        public void SnapToNearest(DraggableCard card)
        {
            TryPlace(card, FindNearestAvailable(card, card.Rect.position));
        }

        public void RefreshPositions()
        {
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] != null && !occupants[i].IsDragging)
                    occupants[i].Rect.position = GetCellWorldPosition(i);
            SetVerticesDirty();
        }

        public void SetPreview(int index)
        {
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
