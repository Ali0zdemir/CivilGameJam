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
        [Header("Saved installation animation")]
        [SerializeField] private UpgradeIcon installPreview;
        [SerializeField] private Canvas installCanvas;
        [SerializeField] private Image installGlow;
        [Min(0.1f)] [SerializeField] private float installSeconds = 0.3f;
        private DraggableCard dragged;
        private Coroutine reveal;
        private Vector3 authoredIconScale;
        private Color authoredGlowColor;
        private bool scaleCached;
        public UpgradeKind Kind => kind;
        public bool Filled => icon.gameObject.activeSelf;
        public bool IsInstalling => installPreview != null && installPreview.gameObject.activeSelf;
        public bool IsAnimating => reveal != null;
        public RectTransform VisibleIcon => IsInstalling ? installPreview.rectTransform : icon.rectTransform;
        public void ShowFilled(bool filled, bool animate = true)
        {
            if (!scaleCached)
            {
                authoredIconScale = icon.rectTransform.localScale;
                if (installGlow != null) authoredGlowColor = installGlow.color;
                scaleCached = true;
            }
            icon.gameObject.SetActive(filled);
            fill.color = icon.Artwork != null ? Color.clear : filled ? new Color(0.96f, 0.85f, 0.55f) : new Color(0.67f, 0.48f, 0.19f);
            if (reveal != null) StopCoroutine(reveal);
            reveal = null;
            ResetInstallPreview();
            icon.enabled = true;
            if (installGlow != null) installGlow.enabled = false;
            icon.rectTransform.localScale = authoredIconScale;
            if (filled && animate && isActiveAndEnabled) reveal = StartCoroutine(Reveal());
        }

        public void AnimateInstall(DraggableCard upgrade)
        {
            if (installPreview == null || !isActiveAndEnabled) { ShowFilled(true); return; }
            var source = upgrade.transform.Find("CardSymbol") as RectTransform ?? upgrade.Rect;
            Vector3 position = source.position;
            Vector3 scale = source.lossyScale;
            Quaternion rotation = source.rotation;
            ShowFilled(true, false);
            var rect = installPreview.rectTransform;
            var space = house.Card.Rect;
            scale.x *= source.rect.width / rect.rect.width;
            scale.y *= source.rect.height / rect.rect.height;
            icon.enabled = false;
            installPreview.Kind = kind;
            installPreview.color = icon.color;
            rect.SetParent(space, false);
            rect.position = position;
            rect.rotation = rotation;
            SetWorldScale(rect, scale);
            installCanvas.overrideSorting = true;
            installPreview.gameObject.SetActive(true);
            Vector3 localScale = new Vector3(scale.x / space.lossyScale.x,
                scale.y / space.lossyScale.y, scale.z / space.lossyScale.z);
            reveal = StartCoroutine(Install(space, space.InverseTransformPoint(position), localScale,
                Quaternion.Inverse(space.rotation) * rotation));
        }

        private IEnumerator Install(RectTransform space, Vector3 localOrigin, Vector3 localScale, Quaternion localRotation)
        {
            var rect = installPreview.rectTransform;
            for (float elapsed = 0f; elapsed < installSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / installSeconds);
                float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
                Vector3 origin = space.TransformPoint(localOrigin), destination = icon.rectTransform.position;
                Vector3 bend = Vector3.Lerp(origin, destination, 0.5f) + space.up * 25f * space.lossyScale.y;
                rect.position = Vector3.Lerp(Vector3.Lerp(origin, bend, ease), Vector3.Lerp(bend, destination, ease), ease);
                rect.rotation = Quaternion.Slerp(space.rotation * localRotation, icon.rectTransform.rotation, ease);
                Vector3 targetScale = icon.rectTransform.lossyScale;
                targetScale.x *= icon.rectTransform.rect.width / rect.rect.width;
                targetScale.y *= icon.rectTransform.rect.height / rect.rect.height;
                SetWorldScale(rect, Vector3.Lerp(Vector3.Scale(localScale, space.lossyScale), targetScale, ease));
                installPreview.color = house.Card.Owner.IsChoosingBlueprint ? Color.clear : icon.color;
                yield return null;
            }
            ResetInstallPreview();
            icon.enabled = true;
            house.Card.GetComponent<CardFeedback>().Punch();
            yield return Reveal();
            reveal = null;
        }

        private static void SetWorldScale(RectTransform rect, Vector3 scale)
        {
            Vector3 parent = rect.parent.lossyScale;
            rect.localScale = new Vector3(scale.x / parent.x, scale.y / parent.y, scale.z / parent.z);
        }

        private void ResetInstallPreview()
        {
            if (installPreview == null) return;
            installPreview.gameObject.SetActive(false);
            if (!gameObject.activeInHierarchy) return;
            var rect = installPreview.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private IEnumerator Reveal()
        {
            const float duration = 0.24f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float bump = Mathf.Sin(t * Mathf.PI);
                float scale = 1f + bump * bump * 0.06f;
                icon.rectTransform.localScale = authoredIconScale * scale;
                if (installGlow != null)
                {
                    installGlow.enabled = true;
                    var tint = authoredGlowColor;
                    tint.a *= Mathf.Sin(t * Mathf.PI);
                    installGlow.color = tint;
                }
                yield return null;
            }
            icon.rectTransform.localScale = authoredIconScale;
            if (installGlow != null) installGlow.enabled = false;
            reveal = null;
            ResetInstallPreview();
            if (installGlow != null) installGlow.enabled = false;
            if (icon != null) icon.enabled = true;
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
            Vector3 origin = VisibleIcon.position;
            float width = VisibleIcon.rect.width * VisibleIcon.lossyScale.x;
            Quaternion rotation = VisibleIcon.rotation;
            dragged = house.Release(kind, false);
            if (dragged == null) return;
            dragged.Rect.position = origin;
            dragged.MatchArtworkWidth(width, rotation);
            dragged.OnBeginDrag(data);
        }
        public void OnDrag(PointerEventData data) => dragged?.OnDrag(data);
        public void OnEndDrag(PointerEventData data) { dragged?.OnEndDrag(data); dragged = null; }
    }
}
