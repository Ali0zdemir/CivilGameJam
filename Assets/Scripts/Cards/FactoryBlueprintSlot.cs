using UnityEngine;
using UnityEngine.EventSystems;

namespace CivilGameJam.Cards
{
    public sealed class FactoryBlueprintSlot : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private FactoryProduction factory;
        private DraggableCard dragged;
        public void OnPointerDown(PointerEventData data) { }
        public void OnPointerUp(PointerEventData data) => dragged?.OnPointerUp(data);
        public void OnPointerClick(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left && !data.dragging) factory.ReleaseBlueprint(); }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || dragged != null) return;
            dragged = factory.ReleaseBlueprint(false);
            if (dragged == null) return;
            dragged.StoredInVault?.Withdraw(dragged);
            RectTransformUtility.ScreenPointToWorldPointInRectangle(dragged.Rect, data.position, data.pressEventCamera, out var point);
            dragged.Rect.position = point;
            dragged.OnBeginDrag(data);
        }
        public void OnDrag(PointerEventData data) => dragged?.OnDrag(data);
        public void OnEndDrag(PointerEventData data)
        {
            var card = dragged;
            dragged = null;
            card?.OnEndDrag(data);
        }
    }
}
