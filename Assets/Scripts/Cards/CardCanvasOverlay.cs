using UnityEngine;

namespace CivilGameJam.Cards
{
    // Keeps an authored overlay in canvas space while its card dock moves beneath it.
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(500)]
    public sealed class CardCanvasOverlay : MonoBehaviour
    {
        [SerializeField] private RectTransform overlay;

        private void OnEnable() => RefreshLayout();
        private void LateUpdate() => RefreshLayout();
        private void OnRectTransformDimensionsChange() => RefreshLayout();

        public void RefreshLayout()
        {
            if (overlay == null || !(overlay.parent is RectTransform parent)) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = (RectTransform)canvas.rootCanvas.transform;
            if (root.rect.width <= 0f || root.rect.height <= 0f) return;
            Vector2 min = parent.InverseTransformPoint(root.TransformPoint(root.rect.min));
            Vector2 max = parent.InverseTransformPoint(root.TransformPoint(root.rect.max));
            overlay.anchorMin = overlay.anchorMax = overlay.pivot = new Vector2(.5f, .5f);
            overlay.sizeDelta = max - min;
            overlay.position = root.TransformPoint(root.rect.center);
        }
    }
}
