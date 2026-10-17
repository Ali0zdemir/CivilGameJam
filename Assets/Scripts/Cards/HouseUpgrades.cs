using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    public sealed class HouseUpgrades : MonoBehaviour
    {
        [SerializeField] private HouseUpgradeSlot[] slots;
        [SerializeField] private Outline outline;
        private readonly DraggableCard[] installed = new DraggableCard[3];
        private CardGameController game;
        private DraggableCard card;
        public DraggableCard GetInstalled(UpgradeKind kind) => kind == UpgradeKind.None ? null : installed[(int)kind - 1];
        public void Initialize(CardGameController owner, DraggableCard building)
        {
            game = owner; card = building;
            for (int i = 0; i < slots.Length; i++) slots[i].ShowFilled(installed[i] != null);
        }
        public bool CanAccept(DraggableCard upgrade) => game != null && !game.IsChoosingBlueprint && upgrade != null &&
            upgrade.Kind == CardKind.Upgrade && upgrade.UpgradeType != UpgradeKind.None && !upgrade.IsStored &&
            GetInstalled(upgrade.UpgradeType) == null;
        public bool TryInstall(DraggableCard upgrade)
        {
            if (!CanAccept(upgrade)) return false;
            int index = (int)upgrade.UpgradeType - 1;
            upgrade.StopFlight();
            installed[index] = upgrade;
            upgrade.InstalledIn = this;
            upgrade.gameObject.SetActive(false);
            slots[index].ShowFilled(true);
            return true;
        }
        public DraggableCard Release(UpgradeKind kind, bool animate = true)
        {
            if (game.IsChoosingBlueprint) return null;
            var upgrade = GetInstalled(kind);
            if (upgrade == null) return null;
            installed[(int)kind - 1] = null;
            upgrade.InstalledIn = null;
            slots[(int)kind - 1].ShowFilled(false);
            game.ReturnToTable(upgrade, card.Rect.position, animate);
            return upgrade;
        }
        public void Preview(DraggableCard upgrade)
        {
            outline.enabled = upgrade != null;
            if (upgrade != null) outline.effectColor = CanAccept(upgrade) ? new Color(0.2f, 0.9f, 0.45f) : Color.red;
        }
    }
}
