using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class WorkerSlot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
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
        private DraggableCard draggedWorker;
        private DraggableCard currentWorker;
        private Coroutine entry;
        public bool IsEntering => entry != null;
        public Image Portrait => portrait;
        public Image EntryImage => entryImage;
        public RectTransform VisibleWorkerRect => IsEntering ? entryImage.rectTransform : portrait != null ? portrait.rectTransform : null;

        public void Refresh(DraggableCard worker)
        {
            if (currentWorker != worker) CancelEntry();
            currentWorker = worker;
            if (portrait != null)
            {
                background.color = Color.clear;
                if (worker != null) portrait.sprite = worker.Kind == CardKind.Person ? normalPortrait : educatedPortrait;
                portrait.gameObject.SetActive(worker != null && !IsEntering);
                return;
            }
            background.color = worker == null ? emptyColor :
                worker.Kind == CardKind.Person ? normalColor : educatedColor;
            label.text = worker == null ? "–" : worker.Kind == CardKind.Person ? "N" : "E";
        }

        public void AnimateEntry(DraggableCard worker, Vector3 origin, Vector3 worldScale, Vector2 sourceSize, Quaternion rotation)
        {
            if (portrait == null || entryImage == null || !isActiveAndEnabled) return;
            CancelEntry();
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
            ResetEntryImage();
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

        private void OnDisable() => CancelEntry();

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
            draggedWorker = station.ReleaseWorker(index, false);
            if (draggedWorker == null) return;
            draggedWorker.Rect.position = origin;
            if (width > 0f) draggedWorker.MatchArtworkWidth(width);
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
