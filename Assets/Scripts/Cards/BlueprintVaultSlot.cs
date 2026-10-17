using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class BlueprintVaultSlot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private BlueprintVault vault;
        [SerializeField] private UpgradeKind kind;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Image background;
        [SerializeField] private UpgradeIcon icon;
        [SerializeField] private TextMeshProUGUI discovery;
        [SerializeField] private TextMeshProUGUI quantity;
        [SerializeField] private Color unlockedColor = new Color(0.16f, 0.30f, 0.33f);
        [SerializeField] private Color lockedColor = new Color(0.085f, 0.13f, 0.15f);
        [SerializeField] private bool preserveArtworkColors;
        private DraggableCard dragged;
        private bool discovered;
        private int incoming;
        private bool waitingForFirstArrival;

        public UpgradeKind Kind => kind;
        public CardGameController Owner => vault.Owner;
        public RectTransform CardRect => cardRect;
        public RectTransform VaultArea => vault.Area;
        public bool IsArriving => incoming > 0;
        public bool CanShowHover => discovered && cardRect.gameObject.activeSelf && !IsArriving && vault.CanDrag && dragged == null;
        public void BeginArrival(bool firstDiscovery) { incoming++; waitingForFirstArrival |= firstDiscovery; }
        public void EndArrival() { incoming = Mathf.Max(0, incoming - 1); if (incoming == 0) waitingForFirstArrival = false; }
        public void Refresh(bool discovered, int available)
        {
            this.discovered = discovered;
            bool visible = discovered && !waitingForFirstArrival;
            cardRect.gameObject.SetActive(visible);
            discovery.gameObject.SetActive(visible);
            quantity.gameObject.SetActive(visible);
            if (!preserveArtworkColors) background.color = discovered ? unlockedColor : lockedColor;
            icon.color = Color.white;
            discovery.text = discovered ? "DISCOVERED" : string.Empty;
            discovery.color = discovered ? new Color(0.96f, 0.75f, 0.39f) : new Color(0.48f, 0.58f, 0.59f);
            quantity.text = !discovered ? string.Empty : available > 0 ? available + (available == 1 ? " plan · drag" : " plans · drag") : "No plans";
        }

        public void OnPointerDown(PointerEventData data) { }
        public void OnPointerUp(PointerEventData data) => dragged?.OnPointerUp(data);
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || dragged != null || !CanShowHover) return;
            dragged = vault.Withdraw(kind);
            if (dragged == null) return;
            dragged.OnBeginDrag(data);
        }
        public void OnDrag(PointerEventData data) => dragged?.OnDrag(data);
        public void OnEndDrag(PointerEventData data)
        {
            dragged?.OnEndDrag(data);
            dragged = null;
        }
    }
}
