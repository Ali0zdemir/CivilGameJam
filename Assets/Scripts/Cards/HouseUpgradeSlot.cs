using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class HouseUpgradeSlot : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private HouseUpgrades house;
        [SerializeField] private UpgradeKind kind;
        [SerializeField] private UpgradeIcon icon;
        [SerializeField] private Image fill;
        private DraggableCard dragged;
        private Coroutine reveal;
        private Vector3 authoredIconScale;
        private bool scaleCached;
        public UpgradeKind Kind => kind;
        public bool Filled => icon.gameObject.activeSelf;
        public void ShowFilled(bool filled)
        {
            if (!scaleCached) { authoredIconScale = icon.rectTransform.localScale; scaleCached = true; }
            icon.gameObject.SetActive(filled);
            fill.color = icon.Artwork != null ? Color.clear : filled ? new Color(0.96f, 0.85f, 0.55f) : new Color(0.67f, 0.48f, 0.19f);
            if (reveal != null) StopCoroutine(reveal);
            reveal = null;
            icon.rectTransform.localScale = authoredIconScale;
            if (filled && isActiveAndEnabled) reveal = StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            const float duration = 0.24f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.72f, 1f, 1f - Mathf.Pow(1f - t, 3f)) + Mathf.Sin(t * Mathf.PI) * 0.08f;
                icon.rectTransform.localScale = authoredIconScale * scale;
                yield return null;
            }
            icon.rectTransform.localScale = authoredIconScale;
            reveal = null;
        }

        private void OnDisable()
        {
            if (reveal != null) StopCoroutine(reveal);
            reveal = null;
            if (icon != null && scaleCached) icon.rectTransform.localScale = authoredIconScale;
        }
        public void OnPointerDown(PointerEventData data) { }
        public void OnPointerClick(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && !data.dragging) house.Release(kind); }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            dragged = house.Release(kind, false);
            if (dragged == null) return;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(dragged.Rect, data.position, data.pressEventCamera, out var point);
            dragged.Rect.position = point;
            dragged.OnBeginDrag(data);
        }
        public void OnDrag(PointerEventData data) => dragged?.OnDrag(data);
        public void OnEndDrag(PointerEventData data) { dragged?.OnEndDrag(data); dragged = null; }
    }
}
