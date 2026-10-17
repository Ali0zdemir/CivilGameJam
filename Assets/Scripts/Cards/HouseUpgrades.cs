using UnityEngine;
using TMPro;
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
        [SerializeField] private TMP_Text earthquakeDamageLabel;
        public bool IsDestroyed { get; private set; }
        public void SetDestroyed(bool destroyed)
        {
            IsDestroyed = destroyed;
            if (earthquakeDamageLabel != null) earthquakeDamageLabel.gameObject.SetActive(destroyed);
            var group = GetComponent<CanvasGroup>();
            if (group != null) group.alpha = destroyed ? .48f : 1f;
        }
        public event System.Action<HouseUpgrades, UpgradeKind> StateChanged;
        public DraggableCard Card => card;
        public bool IsInstalling => System.Array.Exists(slots, slot => slot.IsAnimating);
        public DraggableCard GetInstalled(UpgradeKind kind) => kind == UpgradeKind.None ? null : installed[(int)kind - 1];
        public void Initialize(CardGameController owner, DraggableCard building)
        {
            game = owner; card = building;
            for (int i = 0; i < slots.Length; i++) slots[i].ShowFilled(installed[i] != null);
            StateChanged?.Invoke(this, UpgradeKind.None);
        }
        public bool CanAccept(DraggableCard upgrade) => game != null && !IsDestroyed && !game.ActionsLocked && !game.IsChoosingBlueprint && upgrade != null &&
            upgrade.Kind == CardKind.Upgrade && upgrade.UpgradeType != UpgradeKind.None && !upgrade.IsStored &&
            GetInstalled(upgrade.UpgradeType) == null;
        public bool TryInstall(DraggableCard upgrade)
        {
            if (!CanAccept(upgrade)) return false;
            int index = (int)upgrade.UpgradeType - 1;
            upgrade.StopFlight();
            installed[index] = upgrade;
            upgrade.InstalledIn = this;
            slots[index].AnimateInstall(upgrade);
            upgrade.gameObject.SetActive(false);
            StateChanged?.Invoke(this, upgrade.UpgradeType);
            return true;
        }
        public DraggableCard Release(UpgradeKind kind, bool animate = true)
        {
            if (game.IsChoosingBlueprint || game.ActionsLocked) return null;
            var upgrade = GetInstalled(kind);
            if (upgrade == null) return null;
            installed[(int)kind - 1] = null;
            upgrade.InstalledIn = null;
            var visual = slots[(int)kind - 1].VisibleIcon;
            Vector3 origin = visual.position;
            float width = visual.rect.width * visual.lossyScale.x;
            Quaternion rotation = visual.rotation;
            slots[(int)kind - 1].ShowFilled(false);
            game.ReturnToTable(upgrade, origin, animate);
            if (animate) upgrade.MatchArtworkWidth(width, rotation);
            StateChanged?.Invoke(this, kind);
            return upgrade;
        }

        internal void DiscardInstalled(DraggableCard upgrade)
        {
            if (upgrade == null || upgrade.UpgradeType == UpgradeKind.None || GetInstalled(upgrade.UpgradeType) != upgrade) return;
            int index = (int)upgrade.UpgradeType - 1;
            installed[index] = null;
            upgrade.InstalledIn = null;
            slots[index].ShowFilled(false);
            StateChanged?.Invoke(this, upgrade.UpgradeType);
        }

        public void Preview(DraggableCard upgrade)
        {
            outline.enabled = upgrade != null;
            if (upgrade != null) outline.effectColor = CanAccept(upgrade) ? new Color(0.2f, 0.9f, 0.45f) : Color.red;
        }
    }
}
