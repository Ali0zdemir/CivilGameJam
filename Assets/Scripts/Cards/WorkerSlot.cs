using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class WorkerSlot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private BuildingStation station;
        [SerializeField] private int index;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Color normalColor = CardUI.NormalWorker;
        [SerializeField] private Color educatedColor = CardUI.EducatedWorker;
        [SerializeField] private Color emptyColor = CardUI.EmptySlot;
        [Header("Saved folder artwork")]
        [SerializeField] private Image portrait;
        [SerializeField] private Image entryImage;
        [SerializeField] private Canvas entryCanvas;
        [SerializeField] private RectTransform entryMouth;
        [SerializeField] private Sprite normalPortrait;
        [SerializeField] private Sprite educatedPortrait;
        [Min(0.1f)] [SerializeField] private float entrySeconds = 0.5f;
        [Header("Occupied worker hover")]
        [Min(0f)] [SerializeField] private float hoverLift = 24f;
        [Min(0.01f)] [SerializeField] private float hoverSeconds = 0.16f;
        private Vector2 portraitRestPosition;
        private float hoverOffset;
        private float hoverVelocity;
        private bool portraitPositionSaved;
        private bool pointerInside;
        private DraggableCard draggedWorker;
        private DraggableCard currentWorker;
        private Coroutine entry;
        private Coroutine exit;
        private DraggableCard returningWorker;
        private float entryProgress;
        public int Index => index;
        public bool IsEntering => entry != null;
        public bool IsReturning => exit != null;
        public bool IsAnimating => IsEntering || IsReturning;
        internal DraggableCard ReturningWorker => returningWorker;
        public Image Portrait => portrait;
        public Image EntryImage => entryImage;
        public RectTransform VisibleWorkerRect => IsAnimating ? entryImage.rectTransform : portrait != null ? portrait.rectTransform : null;

        private void SavePortraitPosition()
        {
            if (portraitPositionSaved || portrait == null) return;
            portraitRestPosition = portrait.rectTransform.anchoredPosition;
            portraitPositionSaved = true;
        }

        private void LateUpdate()
        {
            SavePortraitPosition();
            if (!portraitPositionSaved) return;
            var game = station != null && station.Card != null ? station.Card.Owner : null;
            bool canHover = pointerInside && currentWorker != null && !IsAnimating && draggedWorker == null &&
                game != null && !game.IsDraggingAnyCard && !game.IsChoosingBlueprint;
            hoverOffset = Mathf.SmoothDamp(hoverOffset, canHover ? hoverLift : 0f, ref hoverVelocity,
                hoverSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
            portrait.rectTransform.anchoredPosition = portraitRestPosition + Vector2.up * hoverOffset;
        }

        private void ResetHover()
        {
            SavePortraitPosition();
            hoverOffset = hoverVelocity = 0f;
            if (portraitPositionSaved && portrait != null) portrait.rectTransform.anchoredPosition = portraitRestPosition;
        }

        public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;
        public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

        public void Refresh(DraggableCard worker)
        {
            if (currentWorker != worker) { CancelEntry(); ResetHover(); }
            currentWorker = worker;
            if (portrait != null)
            {
                background.color = Color.clear;
                if (worker != null) portrait.sprite = worker.Kind == CardKind.Person ? normalPortrait : educatedPortrait;
                portrait.gameObject.SetActive(worker != null && !IsAnimating);
                return;
            }
            background.color = worker == null ? emptyColor :
                worker.Kind == CardKind.Person ? normalColor : educatedColor;
            label.text = worker == null ? "-" : worker.Kind == CardKind.Person ? "N" : "E";
        }

        public void AnimateEntry(DraggableCard worker, Vector3 origin, Vector3 worldScale, Vector2 sourceSize, Quaternion rotation)
        {
            if (portrait == null || entryImage == null || !isActiveAndEnabled) return;
            CancelEntry();
            ResetHover();
            entryProgress = 0f;
            portrait.gameObject.SetActive(false);
            entryImage.sprite = portrait.sprite;
            entryImage.color = Color.white;
            if (entryCanvas != null) entryCanvas.overrideSorting = true;
            var rect = entryImage.rectTransform;
            rect.SetParent(station.Card.Rect, false);
            worldScale.x *= sourceSize.x / rect.rect.width;
            worldScale.y *= sourceSize.y / rect.rect.height;
            rect.position = origin;
            rect.rotation = rotation;
            SetWorldScale(rect, worldScale);
            entryImage.gameObject.SetActive(true);
            var space = station.Card.Rect;
            Vector3 localScale = new Vector3(worldScale.x / space.lossyScale.x,
                worldScale.y / space.lossyScale.y, worldScale.z / space.lossyScale.z);
            entry = StartCoroutine(EnterFolder(space, space.InverseTransformPoint(origin), localScale,
                Quaternion.Inverse(space.rotation) * rotation));
        }

        private IEnumerator EnterFolder(RectTransform space, Vector3 localOrigin, Vector3 localScale, Quaternion localRotation)
        {
            var rect = entryImage.rectTransform;
            var target = portrait.rectTransform;
            bool behindFlap = false;
            for (float elapsed = 0f; elapsed < entrySeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / entrySeconds);
                entryProgress = t;
                Vector3 origin = space.TransformPoint(localOrigin);
                Vector3 fromScale = Vector3.Scale(localScale, space.lossyScale);
                Quaternion rotation = space.rotation * localRotation;
                entryImage.color = station.Card.Owner.IsChoosingBlueprint ? Color.clear : Color.white;
                Vector3 destination = target.position;
                // Shrink above the folder lip first. Switching layers while the card is
                // already below the flap would abruptly cut off the person's lower half.
                Vector3 above = entryMouth != null ? entryMouth.position :
                    destination + target.up * (target.rect.height * target.lossyScale.y * 1.05f);
                Vector3 toScale = new Vector3(target.lossyScale.x * target.rect.width / rect.rect.width,
                    target.lossyScale.y * target.rect.height / rect.rect.height, target.lossyScale.z);
                if (t < 0.62f)
                {
                    float approach = Smooth(t / 0.62f);
                    Vector3 bend = Vector3.Lerp(origin, above, 0.5f) + target.up * (target.rect.height * target.lossyScale.y * 0.2f);
                    rect.position = Vector3.Lerp(Vector3.Lerp(origin, bend, approach), Vector3.Lerp(bend, above, approach), approach);
                    rect.rotation = Quaternion.Slerp(rotation, target.rotation, approach);
                    SetWorldScale(rect, Vector3.Lerp(fromScale, toScale, approach));
                }
                else
                {
                    if (!behindFlap)
                    {
                        if (entryCanvas != null) entryCanvas.overrideSorting = false;
                        rect.SetParent(transform, true); behindFlap = true;
                    }
                    rect.position = Vector3.Lerp(above, destination, Smooth((t - 0.62f) / 0.38f));
                    rect.rotation = target.rotation;
                    SetWorldScale(rect, toScale);
                }
                yield return null;
            }
            entry = null;
            ResetEntryImage();
            portrait.gameObject.SetActive(currentWorker != null);
        }

        public bool AnimateExit(DraggableCard worker)
        {
            if (portrait == null || entryImage == null || !isActiveAndEnabled || IsReturning) return false;
            var source = VisibleWorkerRect;
            var rect = entryImage.rectTransform;
            var space = station.Card.Rect;
            Vector3 origin = source.position;
            Vector3 scale = source.lossyScale;
            scale.x *= source.rect.width / rect.rect.width;
            scale.y *= source.rect.height / rect.rect.height;
            Quaternion rotation = source.rotation;
            float slideSeconds = (IsEntering ? Mathf.Max(0f, entryProgress - 0.62f) : 0.38f) * entrySeconds;
            CancelEntry();
            // Capture the raised portrait first so clicking it never snaps back into the folder.
            ResetHover();
            currentWorker = null;
            returningWorker = worker;
            portrait.gameObject.SetActive(false);
            entryImage.sprite = worker.Kind == CardKind.Person ? normalPortrait : educatedPortrait;
            entryImage.color = Color.white;
            if (entryCanvas != null) entryCanvas.overrideSorting = slideSeconds <= 0f;
            rect.SetParent(slideSeconds > 0f ? transform : space, false);
            rect.position = origin;
            rect.rotation = rotation;
            SetWorldScale(rect, scale);
            entryImage.gameObject.SetActive(true);
            worker.BeginFolderReturn(this);
            Vector3 localScale = new Vector3(scale.x / space.lossyScale.x,
                scale.y / space.lossyScale.y, scale.z / space.lossyScale.z);
            exit = StartCoroutine(ExitFolder(space, space.InverseTransformPoint(origin), localScale,
                Quaternion.Inverse(space.rotation) * rotation, slideSeconds));
            return true;
        }

        private IEnumerator ExitFolder(RectTransform space, Vector3 localOrigin, Vector3 localScale,
            Quaternion localRotation, float slideSeconds)
        {
            var rect = entryImage.rectTransform;
            var target = returningWorker.transform.Find("Artwork") as RectTransform ?? returningWorker.Rect;
            // Reverse the entry: first slide out behind the flap, then fly above it.
            for (float elapsed = 0f; elapsed < slideSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Smooth(Mathf.Clamp01(elapsed / slideSeconds));
                rect.position = Vector3.Lerp(space.TransformPoint(localOrigin), MouthPosition(), t);
                rect.rotation = Quaternion.Slerp(space.rotation * localRotation, portrait.rectTransform.rotation, t);
                SetWorldScale(rect, Vector3.Scale(localScale, space.lossyScale));
                entryImage.color = station.Card.Owner.IsChoosingBlueprint ? Color.clear : Color.white;
                yield return null;
            }
            if (slideSeconds > 0f)
            {
                localOrigin = space.InverseTransformPoint(MouthPosition());
                localRotation = Quaternion.Inverse(space.rotation) * portrait.rectTransform.rotation;
            }
            if (entryCanvas != null) entryCanvas.overrideSorting = true;
            rect.SetParent(space, true);
            float travelSeconds = entrySeconds * 0.62f;
            for (float elapsed = 0f; elapsed < travelSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Smooth(Mathf.Clamp01(elapsed / travelSeconds));
                Vector3 from = space.TransformPoint(localOrigin);
                Vector3 destination = target.position;
                Vector3 bend = Vector3.Lerp(from, destination, 0.5f) + space.up *
                    (portrait.rectTransform.rect.height * portrait.rectTransform.lossyScale.y * 0.2f);
                rect.position = Vector3.Lerp(Vector3.Lerp(from, bend, t), Vector3.Lerp(bend, destination, t), t);
                rect.rotation = Quaternion.Slerp(space.rotation * localRotation, target.rotation, t);
                Vector3 targetScale = target.lossyScale;
                targetScale.x *= target.rect.width / rect.rect.width;
                targetScale.y *= target.rect.height / rect.rect.height;
                SetWorldScale(rect, Vector3.Lerp(Vector3.Scale(localScale, space.lossyScale), targetScale, t));
                entryImage.color = station.Card.Owner.IsChoosingBlueprint ? Color.clear : Color.white;
                yield return null;
            }
            var worker = returningWorker;
            returningWorker = null;
            exit = null;
            ResetEntryImage();
            if (worker != null) worker.EndFolderReturn(this);
        }

        private Vector3 MouthPosition() => entryMouth != null ? entryMouth.position :
            portrait.rectTransform.position + portrait.rectTransform.up *
            (portrait.rectTransform.rect.height * portrait.rectTransform.lossyScale.y * 1.05f);

        internal void CancelExit(DraggableCard worker)
        {
            if (returningWorker != worker) return;
            if (exit != null) StopCoroutine(exit);
            exit = null;
            returningWorker = null;
            ResetEntryImage();
            if (worker != null) worker.EndFolderReturn(this);
        }

        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static void SetWorldScale(RectTransform rect, Vector3 scale)
        {
            Vector3 parentScale = rect.parent.lossyScale;
            rect.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        }

        private void CancelEntry()
        {
            if (entry != null) StopCoroutine(entry);
            entry = null;
            if (exit == null) ResetEntryImage();
        }

        private void ResetEntryImage()
        {
            if (entryImage == null) return;
            entryImage.gameObject.SetActive(false);
            if (entryCanvas != null) entryCanvas.overrideSorting = true;
            if (!gameObject.activeInHierarchy) return;
            var rect = entryImage.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void OnDisable()
        {
            pointerInside = false;
            ResetHover();
            CancelEntry();
            CancelExit(returningWorker);
        }

        // Consume slot presses so taking a worker does not lift its building as well.
        public void OnPointerDown(PointerEventData eventData) { }

        public void OnPointerUp(PointerEventData eventData) => draggedWorker?.OnPointerUp(eventData);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
                station.ReleaseWorker(index);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Vector3 origin = VisibleWorkerRect != null ? VisibleWorkerRect.position : transform.position;
            float width = VisibleWorkerRect != null ? VisibleWorkerRect.rect.width * VisibleWorkerRect.lossyScale.x : 0f;
            Quaternion rotation = VisibleWorkerRect != null ? VisibleWorkerRect.rotation : Quaternion.identity;
            draggedWorker = station.ReleaseWorker(index, false);
            if (draggedWorker == null) return;
            draggedWorker.Rect.position = origin;
            if (width > 0f) draggedWorker.MatchArtworkWidth(width, rotation);
            draggedWorker.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData) => draggedWorker?.OnDrag(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            draggedWorker?.OnEndDrag(eventData);
            draggedWorker = null;
        }
    }
}
