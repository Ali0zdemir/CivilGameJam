using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace CivilGameJam
{
    /// <summary>Pans the camera rig separately from the earthquake's camera-local shake.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class CityCameraController : MonoBehaviour
    {
        private enum DragMouseButton { Left, Middle, Right }

        [Header("Camera")]
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform panRig;

        [Header("Embedded city view (optional)")]
        [SerializeField] private RectTransform inputViewport;
        [SerializeField] private RectTransform[] inputBlockers = new RectTransform[0];
        [SerializeField, Min(0.01f)] private float viewportReferenceAspect = 1.777778f;
        [SerializeField, Range(0f, 1f)] private float viewportVerticalAlignment = 0.9f;

        [Header("Zoom")]
        [Tooltip("En fazla yakından bakılan görünüm. Küçük değer daha fazla yakınlaştırır.")]
        [SerializeField, Min(0.1f)] private float closestZoom = 1.5f;
        [Tooltip("En fazla uzaktan bakılan görünüm. Büyük değer daha fazla uzaklaştırır.")]
        [SerializeField, Min(0.1f)] private float farthestZoom = 7f;
        [SerializeField, Range(0.01f, 0.5f)] private float zoomSpeed = 0.14f;

        [Header("Mouse drag")]
        [SerializeField] private DragMouseButton dragButton = DragMouseButton.Left;
        [SerializeField, Min(0.01f)] private float panSensitivity = 1f;
        [SerializeField, Min(0f)] private float smoothingTime = 0.08f;

        [Header("Movement limits (Hierarchy objects)")]
        [Tooltip("Sol sınır: bu objenin Local Position X değerini ayarla.")]
        [SerializeField] private Transform leftLimit;
        [Tooltip("Sağ sınır: bu objenin Local Position X değerini ayarla.")]
        [SerializeField] private Transform rightLimit;
        [Tooltip("Üst sınır: bu objenin Local Position Y değerini ayarla.")]
        [SerializeField] private Transform topLimit;
        [Tooltip("Alt sınır: bu objenin Local Position Y değerini ayarla.")]
        [SerializeField] private Transform bottomLimit;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData pointerData;
        private EventSystem pointerEventSystem;
        private Vector3 homePosition;
        private float homeZoom;
        private float targetZoom;
        private float currentZoom;
        private Vector2 targetPan;
        private Vector2 currentPan;
        private Vector2 previousPointer;
        private bool dragging;
        private bool initialized;
        private readonly Vector3[] viewportCorners = new Vector3[4];

        public Vector2 PanOffset => currentPan;
        public bool InputBlocked { get; set; }
        public RectTransform InputViewport => inputViewport;
        private float ProjectionScale => inputViewport == null ? 1f :
            Mathf.Max(1f, viewportReferenceAspect / Mathf.Max(0.01f, controlledCamera.aspect));

        private void Awake() => Initialize();

        private bool Initialize()
        {
            if (initialized) return controlledCamera != null && panRig != null;
            if (controlledCamera == null || panRig == null || !controlledCamera.orthographic) return false;
            homePosition = panRig.localPosition;
            homeZoom = controlledCamera.orthographicSize;
            currentZoom = targetZoom = ClampZoom(homeZoom);
            currentPan = targetPan = Vector2.zero;
            initialized = true;
            return true;
        }

        private void Update()
        {
            if (!Initialize()) return;
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused) { dragging = false; return; }
            var button = GetDragButton(mouse);
            ReadPointer(mouse.position.ReadValue(), ReadScrollSteps(mouse), button.wasPressedThisFrame, button.isPressed);
        }

        private static float ReadScrollSteps(Mouse mouse)
        {
            var scroll = mouse.scroll.ReadValue().y;
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor))
                scroll /= 120f;
            return scroll;
        }

        private ButtonControl GetDragButton(Mouse mouse)
        {
            switch (dragButton)
            {
                case DragMouseButton.Middle: return mouse.middleButton;
                case DragMouseButton.Right: return mouse.rightButton;
                default: return mouse.leftButton;
            }
        }

        private void ReadPointer(Vector2 position, float scrollSteps, bool pressed, bool held)
        {
            if (!held) dragging = false;
            Rect inputRect = GetInputRect();
            if (InputBlocked || !inputRect.Contains(position) || IsOverUI(position)) { dragging = false; return; }
            if (pressed)
            {
                dragging = !IsOverUI(position);
                previousPointer = position;
            }
            if (dragging && held)
            {
                // One screen pixel keeps the same apparent grab distance at every zoom level.
                var unitsPerPixel = controlledCamera.orthographicSize * 2f / Mathf.Max(1f, inputRect.height);
                targetPan -= (position - previousPointer) * (unitsPerPixel * Mathf.Max(0.01f, panSensitivity));
                previousPointer = position;
                targetPan = ClampPan(targetPan);
            }
            if (Mathf.Abs(scrollSteps) > 0.0001f && !IsOverUI(position))
                targetZoom = ClampZoom(targetZoom * Mathf.Exp(Mathf.Clamp(-scrollSteps * zoomSpeed, -20f, 20f)));
        }

        private bool IsOverUI(Vector2 position)
        {
            foreach (var blocker in inputBlockers)
                if (blocker != null && blocker.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(blocker, position, UICamera(blocker))) return true;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            if (pointerData == null || pointerEventSystem != eventSystem)
            {
                pointerEventSystem = eventSystem;
                pointerData = new PointerEventData(eventSystem);
            }
            pointerData.Reset();
            pointerData.position = position;
            uiHits.Clear();
            eventSystem.RaycastAll(pointerData, uiHits);
            foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }

        private static Camera UICamera(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            return canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera : null;
        }

        private Rect GetInputRect()
        {
            if (inputViewport == null) return controlledCamera.pixelRect;
            inputViewport.GetWorldCorners(viewportCorners);
            var uiCamera = UICamera(inputViewport);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in viewportCorners)
            {
                Vector2 pixel = RectTransformUtility.WorldToScreenPoint(uiCamera, corner);
                min = Vector2.Min(min, pixel); max = Vector2.Max(max, pixel);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void LateUpdate()
        {
            if (Initialize()) AdvanceNavigation(Time.unscaledDeltaTime);
        }

        private void AdvanceNavigation(float deltaTime)
        {
            targetPan = ClampPan(targetPan);
            targetZoom = ClampZoom(targetZoom);
            var blend = smoothingTime <= 0f ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / smoothingTime);
            currentPan = ClampPan(Vector2.Lerp(currentPan, targetPan, blend));
            currentZoom = ClampZoom(Mathf.Lerp(currentZoom, targetZoom, blend));
            ApplyCameraPose();
        }

        private void ApplyCameraPose()
        {
            float scale = ProjectionScale;
            controlledCamera.orthographicSize = currentZoom * scale;
            // On tall displays keep the city near the top, above the expanded card drawer.
            // This offset depends on the display aspect, never on the drawer's animation.
            float alignment = currentZoom * (scale - 1f) * (2f * viewportVerticalAlignment - 1f);
            panRig.localPosition = homePosition + new Vector3(currentPan.x, currentPan.y - alignment, 0f);
        }

        private float ClampZoom(float value)
        {
            var closest = Mathf.Max(0.1f, closestZoom);
            return Mathf.Clamp(value, closest, Mathf.Max(closest, farthestZoom));
        }

        private Vector2 ClampPan(Vector2 value)
        {
            GetPanBounds(out var minimum, out var maximum);
            return new Vector2(Mathf.Clamp(value.x, minimum.x, maximum.x), Mathf.Clamp(value.y, minimum.y, maximum.y));
        }

        private void GetPanBounds(out Vector2 minimum, out Vector2 maximum)
        {
            var frame = panRig != null && panRig.parent != null ? panRig.parent : transform;
            var left = leftLimit != null ? frame.InverseTransformPoint(leftLimit.position).x : -3f;
            var right = rightLimit != null ? frame.InverseTransformPoint(rightLimit.position).x : 3f;
            var bottom = bottomLimit != null ? frame.InverseTransformPoint(bottomLimit.position).y : -2f;
            var top = topLimit != null ? frame.InverseTransformPoint(topLimit.position).y : 2f;
            minimum = new Vector2(Mathf.Min(left, right), Mathf.Min(bottom, top));
            maximum = new Vector2(Mathf.Max(left, right), Mathf.Max(bottom, top));
        }

        [ContextMenu("Reset camera view (Play Mode)")]
        public void ResetView()
        {
            if (!Application.isPlaying || !Initialize()) return;
            dragging = false;
            currentPan = targetPan = ClampPan(Vector2.zero);
            currentZoom = targetZoom = ClampZoom(homeZoom);
            ApplyCameraPose();
        }

        private void OnDisable() => dragging = false;
        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) dragging = false; }
        private void OnApplicationPause(bool paused) { if (paused) dragging = false; }

        private void OnValidate()
        {
            closestZoom = Mathf.Max(0.1f, closestZoom);
            farthestZoom = Mathf.Max(closestZoom, farthestZoom);
        }

        private void OnDrawGizmosSelected()
        {
            GetPanBounds(out var minimum, out var maximum);
            var frame = panRig != null && panRig.parent != null ? panRig.parent : transform;
            var a = frame.TransformPoint(new Vector3(minimum.x, minimum.y, 0f));
            var b = frame.TransformPoint(new Vector3(maximum.x, minimum.y, 0f));
            var c = frame.TransformPoint(new Vector3(maximum.x, maximum.y, 0f));
            var d = frame.TransformPoint(new Vector3(minimum.x, maximum.y, 0f));
            Gizmos.color = new Color(0.2f, 0.85f, 1f);
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
            if (leftLimit != null) Gizmos.DrawWireSphere(leftLimit.position, 0.08f);
            if (rightLimit != null) Gizmos.DrawWireSphere(rightLimit.position, 0.08f);
            if (topLimit != null) Gizmos.DrawWireSphere(topLimit.position, 0.08f);
            if (bottomLimit != null) Gizmos.DrawWireSphere(bottomLimit.position, 0.08f);
        }
    }
}
