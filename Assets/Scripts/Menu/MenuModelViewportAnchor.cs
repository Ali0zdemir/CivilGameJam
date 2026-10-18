using UnityEngine;

namespace CivilGameJam.Menu
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class MenuModelViewportAnchor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Vector2 viewportPoint = new Vector2(0.25f, 0.5f);
        [SerializeField, Min(0.1f)] private float depth = 18f;
        [SerializeField, Min(0.1f)] private float referenceWidth = 5.3f;

        private void LateUpdate() => RefreshPosition();

        public void RefreshPosition()
        {
            if (viewCamera == null) return;
            transform.position = viewCamera.ViewportToWorldPoint(new Vector3(viewportPoint.x, viewportPoint.y, depth));
            // Leave space between the model and menu when the Game view becomes narrower.
            float availableWidth = viewCamera.orthographicSize * 2f * viewCamera.aspect * 0.42f;
            transform.localScale = Vector3.one * Mathf.Min(1f, availableWidth / referenceWidth);
        }
    }
}
