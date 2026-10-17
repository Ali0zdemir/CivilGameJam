using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardLightTrail : MaskableGraphic
    {
        [Min(0.03f)] [SerializeField] private float lifetime = 0.14f;
        [Min(0.5f)] [SerializeField] private float width = 5f;
        private readonly Vector2[] points = new Vector2[32];
        private readonly float[] times = new float[32];
        private RectTransform space;
        private Canvas rootCanvas;
        private Vector2 canvasSize;
        private RenderMode renderMode;
        private Camera renderCamera;
        private Transform source;
        private int count;
        private bool emitting;
        public bool HasTrail => count > 1;

        public void Begin(Transform follow)
        {
            source = follow;
            rootCanvas = follow.GetComponent<DraggableCard>().Owner.CardLayer.GetComponentInParent<Canvas>().rootCanvas;
            space = (RectTransform)rootCanvas.transform;
            RememberCanvas();
            count = 0;
            emitting = true;
            Sample();
        }

        public void StopEmitting() => emitting = false;

        public void Clear()
        {
            count = 0;
            emitting = false;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            // Old samples belong to the previous viewport after an aspect or camera change.
            // Discard them rather than drawing a bright line across unrelated coordinates.
            if (space != null && (canvasSize != space.rect.size || renderMode != rootCanvas.renderMode || renderCamera != rootCanvas.worldCamera))
            {
                count = 0;
                RememberCanvas();
            }
            while (count > 0 && Time.unscaledTime - times[0] > lifetime)
            {
                for (int i = 1; i < count; i++) { points[i - 1] = points[i]; times[i - 1] = times[i]; }
                count--;
            }
            if (emitting && source != null) Sample();
            SetVerticesDirty();
        }

        private void RememberCanvas()
        {
            canvasSize = space.rect.size;
            renderMode = rootCanvas.renderMode;
            renderCamera = rootCanvas.worldCamera;
        }

        private void Sample()
        {
            if (space == null || source == null) return;
            Vector2 point = space.InverseTransformPoint(source.position);
            if (count > 0 && Vector2.Distance(points[count - 1], point) < 0.8f) return;
            if (count == points.Length)
            {
                for (int i = 1; i < count; i++) { points[i - 1] = points[i]; times[i - 1] = times[i]; }
                count--;
            }
            points[count] = point;
            times[count++] = Time.unscaledTime;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (space == null || count < 2) return;
            for (int i = 1; i < count; i++)
            {
                Vector2 direction = (points[i] - points[i - 1]).normalized;
                Vector2 normal = new Vector2(-direction.y, direction.x);
                float from = Mathf.Clamp01(1f - (Time.unscaledTime - times[i - 1]) / lifetime);
                float to = Mathf.Clamp01(1f - (Time.unscaledTime - times[i]) / lifetime);
                Segment(mesh, points[i - 1], points[i], normal, width * 2.5f, from, to, 0.12f);
                Segment(mesh, points[i - 1], points[i], normal, width, from, to, 0.8f);
            }
        }

        private void Segment(VertexHelper mesh, Vector2 from, Vector2 to, Vector2 normal,
            float thickness, float fromAge, float toAge, float opacity)
        {
            int start = mesh.currentVertCount;
            var first = color; first.a *= fromAge * fromAge * opacity;
            var last = color; last.a *= toAge * toAge * opacity;
            mesh.AddVert(Local(from - normal * thickness * fromAge * 0.5f), first, Vector2.zero);
            mesh.AddVert(Local(from + normal * thickness * fromAge * 0.5f), first, Vector2.zero);
            mesh.AddVert(Local(to + normal * thickness * toAge * 0.5f), last, Vector2.zero);
            mesh.AddVert(Local(to - normal * thickness * toAge * 0.5f), last, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private Vector3 Local(Vector2 point) => rectTransform.InverseTransformPoint(space.TransformPoint(point));
        protected override void OnDisable() { Clear(); base.OnDisable(); }
    }
}
