using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CivilGameJam.Cards
{
    public sealed class BlueprintVault : MonoBehaviour
    {
        [SerializeField] private CardGameController game;
        [SerializeField] private RectTransform area;
        [SerializeField] private BlueprintVaultSlot[] slots;
        private readonly List<DraggableCard> plans = new List<DraggableCard>();
        private readonly HashSet<UpgradeKind> discoveries = new HashSet<UpgradeKind>();
        private readonly Vector3[] corners = new Vector3[4];
        private readonly Dictionary<DraggableCard, Coroutine> arrivals = new Dictionary<DraggableCard, Coroutine>();

        public CardGameController Owner => game;
        public RectTransform Area => area;
        public IReadOnlyList<BlueprintVaultSlot> Slots => slots;
        public bool CanDrag => game != null && !game.ActionsLocked && !game.IsChoosingBlueprint && !game.IsDraggingAnyCard;
        public bool CanWithdraw(UpgradeKind kind)
        {
            if (!CanDrag) return false;
            foreach (var plan in plans)
                if (plan != null && plan.UpgradeType == kind && !plan.IsFlying && game.CanInteractWithCard(plan)) return true;
            return false;
        }
        public bool IsDiscovered(UpgradeKind kind) => discoveries.Contains(kind);
        public int AvailableCount(UpgradeKind kind)
        {
            int count = 0;
            foreach (var plan in plans) if (plan != null && plan.UpgradeType == kind) count++;
            return count;
        }

        public void Initialize(CardGameController owner)
        {
            game = owner;
            plans.Clear();
            discoveries.Clear();
            Refresh();
        }

        public BlueprintVaultSlot SlotFor(UpgradeKind kind)
        {
            foreach (var slot in slots) if (slot.Kind == kind) return slot;
            return null;
        }

        public bool Store(DraggableCard plan, Vector3? origin = null)
            => Store(plan, origin, null);

        internal bool StoreFromFactory(DraggableCard plan, FactoryFolderPresentation source)
            => source != null && source.CanReturn && Store(plan, null, source);

        private bool Store(DraggableCard plan, Vector3? origin, FactoryFolderPresentation source)
        {
            if (plan == null || plan.Owner != game || plan.Kind != CardKind.Blueprint || SlotFor(plan.UpgradeType) == null ||
                (plan.IsStored && plan.StoredInVault != this)) return false;
            // Capture before closing an edge-clamped building hover moves its folder.
            var pose = source != null ? source.CaptureExitPose() : default;
            game.CloseInspection();
            CancelArrival(plan);
            plan.StopFlight();
            if (!plans.Contains(plan)) plans.Add(plan);
            bool firstDiscovery = discoveries.Add(plan.UpgradeType);
            plan.StoredInVault = this;
            var slot = SlotFor(plan.UpgradeType);
            if (origin.HasValue || source != null)
            {
                slot.BeginArrival(firstDiscovery);
                // The moving plan can cross neighbouring opaque slot backgrounds.
                // Raise its complete slot, just as the saved hover presentation does.
                slot.transform.SetAsLastSibling();
                plan.Rect.SetParent(slot.transform, true);
                plan.Rect.anchorMin = plan.Rect.anchorMax = new Vector2(0.5f, 0.5f);
                plan.gameObject.SetActive(true);
                plan.Rect.SetAsLastSibling();
                Vector3 destination = slot.CardRect.TransformPoint(slot.CardRect.rect.center);
                if (source != null)
                {
                    plan.Rect.position = destination;
                    source.AnimateReturn(plan, slot.CardRect, pose);
                }
                else plan.FlyFrom(origin.Value, destination);
                arrivals.Add(plan, StartCoroutine(FinishArrival(plan, slot)));
            }
            else plan.gameObject.SetActive(false);
            Refresh();
            return true;
        }

        private IEnumerator FinishArrival(DraggableCard plan, BlueprintVaultSlot slot)
        {
            while (plan != null && plan.IsFlying) yield return null;
            arrivals.Remove(plan);
            slot.EndArrival();
            if (plan != null && plan.StoredInVault == this) plan.gameObject.SetActive(false);
            Refresh();
        }

        private void CancelArrival(DraggableCard plan)
        {
            if (!arrivals.TryGetValue(plan, out var routine)) return;
            StopCoroutine(routine);
            arrivals.Remove(plan);
            SlotFor(plan.UpgradeType).EndArrival();
            plan.StopFlight();
        }

        private void OnDisable()
        {
            foreach (var plan in new List<DraggableCard>(arrivals.Keys)) CancelArrival(plan);
        }

        public DraggableCard Withdraw(UpgradeKind kind)
        {
            for (int i = 0; i < plans.Count; i++)
                if (plans[i] != null && plans[i].UpgradeType == kind) return Withdraw(plans[i]);
            return null;
        }

        public DraggableCard Withdraw(DraggableCard plan)
        {
            if (game.IsChoosingBlueprint || game.ActionsLocked || plan == null || !game.CanUseBlueprint(plan.UpgradeType) ||
                !game.CanInteractWithCard(plan) ||
                plan.StoredInVault != this || !plans.Remove(plan)) return null;
            CancelArrival(plan);
            plan.StoredInVault = null;
            plan.Rect.SetParent(game.CardLayer, true);
            plan.Rect.anchorMin = plan.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            var slot = SlotFor(plan.UpgradeType).CardRect;
            plan.Rect.position = slot.TransformPoint(slot.rect.center);
            plan.Rect.SetAsLastSibling();
            plan.gameObject.SetActive(true);
            Refresh();
            return plan;
        }

        public bool OverlapsTableArea(Vector2 position, Vector2 size)
        {
            area.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners)
            {
                Vector2 point = game.CardLayer.InverseTransformPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x - 6f, min.y - 6f, max.x + 6f, max.y + 6f)
                .Overlaps(new Rect(position - size * 0.5f, size));
        }

        internal void Forget(UpgradeKind kind)
        {
            for (int i = plans.Count - 1; i >= 0; i--)
            {
                var plan = plans[i];
                if (plan == null || plan.UpgradeType != kind) continue;
                CancelArrival(plan);
                plan.StoredInVault = null;
                plans.RemoveAt(i);
            }
            discoveries.Remove(kind);
            Refresh();
        }

        internal void Refresh()
        {
            foreach (var slot in slots) slot.Refresh(IsDiscovered(slot.Kind), AvailableCount(slot.Kind));
        }
    }
}
