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
        private bool presenting;
        private bool returningFromPresentation;
        private Vector2 presentationHomePan;
        private float presentationHomeZoom;
        private Vector3 presentationHomeCameraPosition;
        private Vector2 presentationFromPan;
        private float presentationFromZoom;
        private float presentationSeconds;
        private float presentationElapsed;
        private readonly HashSet<Object> eventInputOwners = new HashSet<Object>();
        private bool externalInputBlocked;
        private Object eventFocusOwner;
        private bool eventReturning;
        private bool eventTracking;
        private Vector2 eventHomePan;
        private float eventHomeZoom;
        private Vector2 eventFromPan;
        private float eventFromZoom;
        private float eventZoom;
        private float eventSeconds;
        private float eventElapsed;
        private bool eventCentered;
        private Vector2 eventCenterPan;
        private float eventCenterSeconds;
        private Transform[] eventOverviewSubjects;
        private readonly Vector3[] viewportCorners = new Vector3[4];

        public Vector2 PanOffset => currentPan;
        public bool InputBlocked
        {
            get => externalInputBlocked || eventInputOwners.Count > 0 || IsEventCameraActive;
            set => externalInputBlocked = value;
        }
        public bool IsEventCameraActive => eventFocusOwner != null;
        public RectTransform InputViewport => inputViewport;
        public Rect OverviewViewportRect => GetOverviewViewportRect();
        public bool IsPresenting => presenting;
        public bool IsFocusReady => presenting && !returningFromPresentation && presentationElapsed >= presentationSeconds;
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
            if (presenting || InputBlocked || !inputRect.Contains(position) || IsOverUI(position)) { dragging = false; return; }
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
            if (IsEventCameraActive)
            {
                AdvanceEventFocus(deltaTime);
                return;
            }
            targetPan = ClampPan(targetPan);
            targetZoom = ClampZoom(targetZoom);
            if (presenting)
            {
                presentationElapsed += Mathf.Max(0f, deltaTime);
                float t = Mathf.Clamp01(presentationElapsed / Mathf.Max(0.001f, presentationSeconds));
                float smooth = t * t * t * (t * (t * 6f - 15f) + 10f);
                currentPan = ClampPan(Vector2.Lerp(presentationFromPan, targetPan, smooth));
                currentZoom = ClampZoom(Mathf.Lerp(presentationFromZoom, targetZoom, smooth));
                ApplyCameraPose();
                if (t >= 1f && returningFromPresentation) presenting = returningFromPresentation = false;
                return;
            }
            var blend = smoothingTime <= 0f ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / smoothingTime);
            currentPan = ClampPan(Vector2.Lerp(currentPan, targetPan, blend));
            currentZoom = ClampZoom(Mathf.Lerp(currentZoom, targetZoom, blend));
            ApplyCameraPose();
        }

        public bool BeginPresentationFocus(Vector3 worldPoint, float zoomFactor, float seconds)
        {
            if (!isActiveAndEnabled || !Initialize() || eventInputOwners.Count > 0 || IsEventCameraActive) return false;
            if (!presenting)
            {
                presentationHomePan = currentPan;
                presentationHomeZoom = currentZoom;
                presentationHomeCameraPosition = controlledCamera.transform.position;
            }
            // Anchor a small zoom on the house, preserving the user's view for the return.
            float zoom = ClampZoom(presentationHomeZoom / Mathf.Max(1f, zoomFactor));
            float fraction = 1f - zoom / Mathf.Max(0.1f, presentationHomeZoom);
            Vector3 offset = worldPoint - presentationHomeCameraPosition;
            Vector3 worldPan = controlledCamera.transform.right * Vector3.Dot(offset, controlledCamera.transform.right) +
                controlledCamera.transform.up * Vector3.Dot(offset, controlledCamera.transform.up);
            Vector3 localPan = panRig.parent != null ? panRig.parent.InverseTransformVector(worldPan) : worldPan;
            targetPan = ClampPan(presentationHomePan + new Vector2(localPan.x, localPan.y) * Mathf.Max(0f, fraction));
            float alignmentChange = (presentationHomeZoom - zoom) * (ProjectionScale - 1f) *
                (2f * viewportVerticalAlignment - 1f);
            targetPan = ClampPan(targetPan - Vector2.up * alignmentChange);
            targetZoom = zoom;
            StartPresentationTween(seconds, false);
            return true;
        }

        public void EndPresentationFocus(float seconds)
        {
            if (!presenting || !Initialize()) return;
            targetPan = ClampPan(presentationHomePan);
            targetZoom = ClampZoom(presentationHomeZoom);
            StartPresentationTween(seconds, true);
            if (seconds <= 0f) AdvanceNavigation(1f);
        }

        private void StartPresentationTween(float seconds, bool returning)
        {
            dragging = false;
            presenting = true;
            returningFromPresentation = returning;
            presentationFromPan = currentPan;
            presentationFromZoom = currentZoom;
            presentationSeconds = Mathf.Max(0.001f, seconds);
            presentationElapsed = 0f;
        }

        public void AcquireEventInputLock(Object owner)
        {
            if (owner == null || !eventInputOwners.Add(owner)) return;
            dragging = false;
            if (!presenting && !IsEventCameraActive && Initialize())
            {
                targetPan = currentPan;
                targetZoom = currentZoom;
            }
        }

        public void ReleaseEventInputLock(Object owner) => eventInputOwners.Remove(owner);

        public bool BeginEventFocus(Object owner, Vector3 worldPoint, float zoomFactor, float seconds)
        {
            if (owner == null || !isActiveAndEnabled || !Initialize()) return false;
            if (!IsEventCameraActive)
            {
                // An interrupted house presentation still returns to the player's original view.
                eventHomePan = presenting ? presentationHomePan : currentPan;
                eventHomeZoom = presenting ? presentationHomeZoom : currentZoom;
            }
            presenting = returningFromPresentation = false;
            eventOverviewSubjects = null;
            eventFocusOwner = owner;
            eventZoom = ClampZoom(eventHomeZoom / Mathf.Max(1f, zoomFactor));
            targetPan = EventFocusPan(worldPoint, eventZoom);
            targetZoom = eventZoom;
            StartEventTween(seconds, false);
            return true;
        }

        public bool BeginEventFrame(Object owner, Transform[] subjects, Transform context, float zoomFactor, float seconds)
        {
            if (subjects == null || !isActiveAndEnabled || !Initialize()) return false;
            var minimum = Vector2.positiveInfinity;
            var maximum = Vector2.negativeInfinity;
            var found = false;
            foreach (var subject in subjects) found |= ProjectSubject(subject, ref minimum, ref maximum);
            if (!found) return false;
            var center = (minimum + maximum) * 0.5f;
            ProjectSubject(context, ref minimum, ref maximum);
            var extent = Vector2.Max(center - minimum, maximum - center);
            // Keep the city centred; extra subjects only widen the view, never pull it sideways.
            const float usableFrame = 0.76f;
            var fitZoom = Mathf.Max(extent.y, extent.x / Mathf.Max(0.01f, controlledCamera.aspect)) /
                (usableFrame * ProjectionScale);
            var cameraTransform = controlledCamera.transform;
            var worldPoint = cameraTransform.position + cameraTransform.right * center.x + cameraTransform.up * center.y;
            if (!BeginEventFocus(owner, worldPoint, zoomFactor, seconds)) return false;
            eventZoom = targetZoom = Mathf.Max(eventZoom, fitZoom);
            targetPan = EventFocusPan(worldPoint, eventZoom, true);
            eventCenterPan = EventFocusPan(worldPoint, eventFromZoom, true);
            // Finish the gentle pan first so the city stays exactly centred throughout the zoom.
            eventCenterSeconds = Vector2.Distance(eventFromPan, eventCenterPan) > 0.001f ? eventSeconds * 0.3f : 0f;
            eventCentered = true;
            return true;
        }

        public bool BeginEventOverview(Object owner, Transform[] subjects, float seconds)
        {
            if (!MeasureOverview(subjects, out var center, out var zoom)) return false;
            controlledCamera.farClipPlane = Mathf.Max(controlledCamera.farClipPlane, 240f);
            if (!BeginEventFocus(owner, center, 1f, seconds)) return false;
            eventOverviewSubjects = subjects;
            eventZoom = targetZoom = zoom;
            targetPan = EventFocusPan(center, zoom, true);
            return true;
        }

        public void TrackEventOverview(Object owner, Transform[] subjects, float responseSeconds)
        {
            if (eventFocusOwner != owner || eventReturning || !MeasureOverview(subjects, out var center, out var zoom)) return;
            eventOverviewSubjects = subjects;
            eventZoom = targetZoom = zoom;
            targetPan = EventFocusPan(center, zoom, true);
            eventTracking = true;
            eventSeconds = Mathf.Max(.001f, responseSeconds);
        }

        private bool MeasureOverview(Transform[] subjects, out Vector3 center, out float zoom)
        {
            center = Vector3.zero; zoom = 0f;
            if (subjects == null || !isActiveAndEnabled || !Initialize()) return false;
            var minimum = Vector2.positiveInfinity; var maximum = Vector2.negativeInfinity;
            var found = false;
            foreach (var subject in subjects) found |= ProjectSubject(subject, ref minimum, ref maximum, true);
            if (!found) return false;
            var half = (maximum - minimum) * .5f;
            var middle = (minimum + maximum) * .5f;
            var safe = GetOverviewViewportRect();
            zoom = Mathf.Max(closestZoom, Mathf.Max(half.y / safe.height,
                half.x / (safe.width * Mathf.Max(.01f, controlledCamera.aspect))) / (.94f * ProjectionScale));
            middle -= Vector2.Scale(safe.center - Vector2.one * .5f,
                new Vector2(2f * zoom * ProjectionScale * controlledCamera.aspect, 2f * zoom * ProjectionScale));
            center = controlledCamera.transform.position + controlledCamera.transform.right * middle.x + controlledCamera.transform.up * middle.y;
            return true;
        }

        private Rect GetOverviewViewportRect()
        {
            var safe = Rect.MinMaxRect(.04f, .06f, .96f, .94f);
            if (inputViewport == null || inputViewport.rect.height <= 1f) return safe;
            var view = inputViewport.rect;
            foreach (Transform child in inputViewport)
            {
                var rect = child as RectTransform;
                if (rect == null || !rect.gameObject.activeInHierarchy || rect.rect.height > view.height * .45f) continue;
                rect.GetWorldCorners(viewportCorners);
                var minimum = Vector2.positiveInfinity; var maximum = Vector2.negativeInfinity;
                foreach (var corner in viewportCorners)
                {
                    var point = (Vector2)inputViewport.InverseTransformPoint(corner);
                    minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
                }
                if (maximum.x - minimum.x < view.width * .12f) continue;
                var bottom = (minimum.y - view.yMin) / view.height;
                var top = (maximum.y - view.yMin) / view.height;
                var middle = (bottom + top) * .5f;
                if (middle > .65f) safe.yMax = Mathf.Min(safe.yMax, bottom - .025f);
                else if (middle < .35f) safe.yMin = Mathf.Max(safe.yMin, top + .025f);
            }
            safe.yMin = Mathf.Clamp(safe.yMin, .06f, .4f);
            safe.yMax = Mathf.Clamp(safe.yMax, .6f, .94f);
            return safe;
        }

        private bool ProjectSubject(Transform subject, ref Vector2 minimum, ref Vector2 maximum, bool includeInactive = false)
        {
            if (subject == null) return false;
            var found = false;
            var cameraTransform = controlledCamera.transform;
            foreach (var renderer in subject.GetComponentsInChildren<MeshRenderer>(includeInactive))
            {
                if (!renderer.enabled) continue;
                var bounds = renderer.localBounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = renderer.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                    var offset = point - cameraTransform.position;
                    var projected = new Vector2(Vector3.Dot(offset, cameraTransform.right), Vector3.Dot(offset, cameraTransform.up));
                    minimum = Vector2.Min(minimum, projected);
                    maximum = Vector2.Max(maximum, projected);
                }
                found = true;
            }
            return found;
        }

        public void TrackEventFocus(Object owner, Vector3 worldPoint, float responseSeconds)
        {
            if (eventFocusOwner != owner || !IsEventCameraActive || eventReturning) return;
            targetPan = EventFocusPan(worldPoint, eventZoom);
            targetZoom = eventZoom;
            eventTracking = true;
            eventSeconds = Mathf.Max(0.001f, responseSeconds);
        }

        public void HoldEventFocus(Object owner)
        {
            if (eventFocusOwner != owner || !IsEventCameraActive || eventReturning) return;
            // Hold the rendered view, including stopping any remaining follow smoothing.
            targetPan = currentPan;
            targetZoom = currentZoom;
            StartEventTween(0f, false);
        }

        public void EndEventFocus(Object owner, float seconds)
        {
            if (eventFocusOwner != owner || !IsEventCameraActive || eventReturning) return;
            targetPan = eventHomePan;
            targetZoom = eventHomeZoom;
            StartEventTween(seconds, true);
            if (seconds <= 0f) AdvanceEventFocus(1f);
        }

        private Vector2 EventFocusPan(Vector3 worldPoint, float zoom, bool centered = false)
        {
            var cameraTransform = controlledCamera.transform;
            var offset = worldPoint - cameraTransform.position;
            if (!centered) offset += cameraTransform.up * (zoom * ProjectionScale * 0.2f);
            var worldPan = cameraTransform.right * Vector3.Dot(offset, cameraTransform.right) +
                cameraTransform.up * Vector3.Dot(offset, cameraTransform.up);
            var localPan = panRig.parent != null ? panRig.parent.InverseTransformVector(worldPan) : worldPan;
            float alignmentChange = (currentZoom - zoom) * (ProjectionScale - 1f) *
                (2f * viewportVerticalAlignment - 1f);
            return currentPan + new Vector2(localPan.x, localPan.y - alignmentChange);
        }

        private void StartEventTween(float seconds, bool returning)
        {
            dragging = false;
            eventReturning = returning;
            eventTracking = false;
            eventCentered = false;
            eventFromPan = currentPan;
            eventFromZoom = currentZoom;
            eventSeconds = Mathf.Max(0.001f, seconds);
            eventElapsed = 0f;
        }

        private void AdvanceEventFocus(float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);
            if (eventTracking)
            {
                float blend = 1f - Mathf.Exp(-step / eventSeconds);
                currentPan = Vector2.Lerp(currentPan, targetPan, blend);
                currentZoom = Mathf.Lerp(currentZoom, targetZoom, blend);
            }
            else
            {
                eventElapsed += step;
                float t = Mathf.Clamp01(eventElapsed / eventSeconds);
                float smooth = SmootherStep(t);
                // Cinematic framing can reach the sea beyond the manual drag limits.
                if (eventCentered)
                {
                    if (eventElapsed < eventCenterSeconds)
                    {
                        currentPan = Vector2.Lerp(eventFromPan, eventCenterPan, SmootherStep(eventElapsed / eventCenterSeconds));
                        currentZoom = eventFromZoom;
                    }
                    else
                    {
                        smooth = SmootherStep(Mathf.Clamp01((eventElapsed - eventCenterSeconds) /
                            Mathf.Max(0.001f, eventSeconds - eventCenterSeconds)));
                        currentPan = Vector2.Lerp(eventCenterPan, targetPan, smooth);
                        currentZoom = Mathf.Lerp(eventFromZoom, targetZoom, smooth);
                    }
                }
                else
                {
                    currentPan = Vector2.Lerp(eventFromPan, targetPan, smooth);
                    currentZoom = Mathf.Lerp(eventFromZoom, targetZoom, smooth);
                }
                if (t >= 1f && eventReturning)
                {
                    currentPan = targetPan = eventHomePan;
                    currentZoom = targetZoom = eventHomeZoom;
                    eventFocusOwner = null;
                    eventReturning = false;
                }
            }
            ApplyCameraPose();
            if (eventTracking && eventOverviewSubjects != null && !eventReturning)
            {
                // Keep fast Inspector speed changes inside the frame while the pan catches up smoothly.
                var minimum = Vector2.positiveInfinity; var maximum = Vector2.negativeInfinity;
                foreach (var subject in eventOverviewSubjects) ProjectSubject(subject, ref minimum, ref maximum, true);
                var safe = GetOverviewViewportRect();
                var vertical = Mathf.Max(maximum.y / (2f * (safe.yMax - .5f)), -minimum.y / (2f * (.5f - safe.yMin)));
                var horizontal = Mathf.Max(maximum.x / (2f * (safe.xMax - .5f)), -minimum.x / (2f * (.5f - safe.xMin))) /
                    Mathf.Max(.01f, controlledCamera.aspect);
                var requiredZoom = Mathf.Max(vertical, horizontal) / ProjectionScale;
                if (requiredZoom > currentZoom)
                {
                    currentPan.y += (requiredZoom - currentZoom) * (ProjectionScale - 1f) * (2f * viewportVerticalAlignment - 1f);
                    currentZoom = requiredZoom; ApplyCameraPose();
                }
            }
        }

        private static float SmootherStep(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

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
            if (!Application.isPlaying || !Initialize() || eventInputOwners.Count > 0 || IsEventCameraActive) return;
            presenting = returningFromPresentation = false;
            dragging = false;
            currentPan = targetPan = ClampPan(Vector2.zero);
            currentZoom = targetZoom = ClampZoom(homeZoom);
            ApplyCameraPose();
        }

        private void OnDisable()
        {
            dragging = false;
            if (presenting) EndPresentationFocus(0f);
            if (IsEventCameraActive)
            {
                eventReturning = false;
                EndEventFocus(eventFocusOwner, 0f);
            }
        }
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
