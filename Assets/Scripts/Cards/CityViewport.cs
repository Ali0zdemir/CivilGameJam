using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    [ExecuteAlways]
    [DefaultExecutionOrder(300)]
    public sealed class CityViewport : MonoBehaviour
    {
        [SerializeField] private RectTransform sidebar;
        [SerializeField] private CardDock dock;
        [SerializeField] private RawImage cityImage;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform world;
        [SerializeField] private RenderTexture previewTexture;
        [Min(0f)] [SerializeField] private float margin = 24f;
        [Min(1f)] [SerializeField] private float framingPadding = 1.08f;
        [Range(0f, 1f)] [SerializeField] private float verticalAlignment = 0.9f;
        private RenderTexture runtimeTexture;
        private Bounds cityBounds;
        private Vector2 projectedMin, projectedMax;
        private Quaternion boundsRotation;
        private bool hasBounds;
        private readonly Vector3[] corners = new Vector3[4];

        public Camera WorldCamera => worldCamera;
        public RectTransform ViewRect => cityImage.rectTransform;
        public RectTransform Sidebar => sidebar;

        private void Update() => RefreshViewport();

        public void RefreshViewport()
        {
            if (sidebar == null || dock == null || cityImage == null || worldCamera == null || world == null) return;
            var canvas = cityImage.canvas.rootCanvas;
            var root = (RectTransform)canvas.transform;
            Rect area = root.rect;
            Rect sideBounds = BoundsInCanvas(sidebar, root);
            float left = Mathf.Max(area.xMin + margin, sideBounds.xMax);
            var view = cityImage.rectTransform;
            view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
            // Use the saved one-row boundary even while the second row slides over the city.
            view.offsetMin = new Vector2(left - area.xMin, dock.CollapsedCanvasHeight);
            view.offsetMax = Vector2.one * -margin;

            RenderTexture target = previewTexture;
            if (Application.IsPlaying(gameObject))
            {
                var pixels = canvas.pixelRect;
                float reduction = Mathf.Min(1f, 4096f / Mathf.Max(1f, pixels.width, pixels.height));
                int width = Mathf.Max(16, Mathf.RoundToInt(pixels.width * reduction));
                int height = Mathf.Max(16, Mathf.RoundToInt(pixels.height * reduction));
                if (runtimeTexture == null || runtimeTexture.width != width || runtimeTexture.height != height)
                {
                    ReleaseTexture();
                    runtimeTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                    { name = "City view (display)", hideFlags = HideFlags.DontSave };
                    runtimeTexture.Create();
                }
                target = runtimeTexture;
            }
            if (worldCamera.targetTexture != target) worldCamera.targetTexture = target;
            if (cityImage.texture != target) cityImage.texture = target;
            worldCamera.aspect = Mathf.Max(0.01f, view.rect.width / Mathf.Max(1f, view.rect.height));
            if (!hasBounds || !Application.IsPlaying(gameObject) || boundsRotation != worldCamera.transform.rotation) RefreshBounds();
            if (!hasBounds) return;
            Vector3 right = worldCamera.transform.right;
            Vector3 up = worldCamera.transform.up;
            Vector2 half = (projectedMax - projectedMin) * 0.5f;
            Vector2 center = (projectedMin + projectedMax) * 0.5f;
            worldCamera.orthographicSize = Mathf.Max(half.y, half.x / worldCamera.aspect) * framingPadding;
            float spareHeight = Mathf.Max(0f, worldCamera.orthographicSize - half.y);
            Vector3 focus = cityBounds.center + right * (center.x - Vector3.Dot(cityBounds.center, right)) +
                up * (center.y - Vector3.Dot(cityBounds.center, up) - spareHeight * (2f * verticalAlignment - 1f));
            worldCamera.transform.position = focus - worldCamera.transform.forward * 50f;
        }

        private void RefreshBounds()
        {
            hasBounds = false;
            projectedMin = Vector2.positiveInfinity;
            projectedMax = Vector2.negativeInfinity;
            Vector3 right = worldCamera.transform.right, up = worldCamera.transform.up;
            boundsRotation = worldCamera.transform.rotation;
            foreach (var renderer in world.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name == "Water") continue;
                if (!hasBounds) { cityBounds = renderer.bounds; hasBounds = true; }
                else cityBounds.Encapsulate(renderer.bounds);
                // Project each object separately so empty corners of the island's overall box do not shrink the city.
                Bounds bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var projected = new Vector2(Vector3.Dot(point, right), Vector3.Dot(point, up));
                    projectedMin = Vector2.Min(projectedMin, projected);
                    projectedMax = Vector2.Max(projectedMax, projected);
                }
            }
        }

        private Rect BoundsInCanvas(RectTransform rect, RectTransform root)
        {
            rect.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners)
            {
                Vector2 point = root.InverseTransformPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void ReleaseTexture()
        {
            if (runtimeTexture == null) return;
            if (worldCamera != null && worldCamera.targetTexture == runtimeTexture) worldCamera.targetTexture = previewTexture;
            if (cityImage != null && cityImage.texture == runtimeTexture) cityImage.texture = previewTexture;
            runtimeTexture.Release();
            if (Application.isPlaying) Destroy(runtimeTexture); else DestroyImmediate(runtimeTexture);
            runtimeTexture = null;
        }

        private void OnDisable() => ReleaseTexture();
    }
}
