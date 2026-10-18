using System.Collections;
using System.Collections.Generic;
using CivilGameJam.Cards;
using UnityEngine;

namespace CivilGameJam
{
    /// <summary>Shows an accepted house upgrade after the drawer and camera have settled.</summary>
    [DisallowMultipleComponent]
    public sealed class HouseUpgradePresentation : MonoBehaviour
    {
        [SerializeField] private CardGameController game;
        [SerializeField] private CardDock dock;
        [SerializeField] private CityCameraController navigation;
        [SerializeField, Range(1f, 4f)] private float zoomFactor = 2.2f;
        [SerializeField, Range(1f, 2f)] private float closeupMagnification = 1.5f;
        [SerializeField, Min(0.01f)] private float zoomSeconds = 0.9f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.1f;
        [SerializeField, Min(0.01f)] private float returnSeconds = 1.2f;

        private sealed class Installation
        {
            public HouseCityUpgradeLink Link;
            public UpgradeKind Kind;
            public DraggableCard Product;
            public bool Cancelled;
            public bool IsValid => !Cancelled && Link != null && Link.isActiveAndEnabled && Link.House != null && Product != null &&
                Link.House.GetInstalled(Kind) == Product;
        }

        private readonly List<Installation> pending = new List<Installation>();
        private Installation current;
        private Coroutine presentation;
        public bool IsPresenting => presentation != null;
        private bool QueuingUpgrade => game != null && game.DraggedCard != null && game.DraggedCard.Kind == CardKind.Upgrade;
        private bool Interrupted => game == null || game.IsChoosingBlueprint || game.IsDraggingAnyCard && !QueuingUpgrade;

        public bool QueueInstallation(HouseCityUpgradeLink link, UpgradeKind kind, DraggableCard product)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || link == null || product == null) return false;
            Cancel(link, kind);
            pending.Add(new Installation { Link = link, Kind = kind, Product = product });
            if (presentation == null) presentation = StartCoroutine(Present());
            return true;
        }

        public void Cancel(HouseCityUpgradeLink link, UpgradeKind kind = UpgradeKind.None)
        {
            if (current != null && current.Link == link && (kind == UpgradeKind.None || current.Kind == kind))
                current.Cancelled = true;
            pending.RemoveAll(item => item.Link == link && (kind == UpgradeKind.None || item.Kind == kind));
        }

        private IEnumerator Present()
        {
            // The drop callback is still ending its drag in this frame.
            yield return null;
            // Show the item entering its saved socket and the socket's flash before
            // sliding the house row off screen for the city upgrade presentation.
            while (!Interrupted && (QueuingUpgrade || HasPendingCardAnimation())) yield return null;
            if (!Interrupted && dock != null) dock.BeginCityPresentation();
            do
            {
                while (pending.Count > 0 && !Interrupted)
                {
                    current = pending[0];
                    pending.RemoveAt(0);
                    if (!current.IsValid) { current = null; continue; }
                    while (current.IsValid && !Interrupted &&
                           (QueuingUpgrade || current.Link.House.IsInstalling ||
                            dock != null && dock.isActiveAndEnabled && dock.Expansion > 0.001f)) yield return null;
                    if (Interrupted) break;
                    if (!current.IsValid) { current = null; continue; }
                    bool focused = navigation != null && navigation.BeginPresentationFrame(
                        current.Link.Building, zoomFactor, zoomSeconds, current.Kind == UpgradeKind.Breakwater, closeupMagnification);
                    while (focused && navigation != null && navigation.isActiveAndEnabled &&
                           navigation.IsPresenting && !navigation.IsFocusReady && current.IsValid && !Interrupted)
                        yield return null;
                    if (Interrupted) break;
                    if (!current.IsValid) { current = null; continue; }
                    current.Link.ApplyInstalled(current.Kind, current.Product);
                    while (current.IsValid && !Interrupted &&
                           (current.Kind == UpgradeKind.RailSystem && current.Link.RailSystem != null && current.Link.RailSystem.IsTransitioning ||
                            current.Kind == UpgradeKind.Breakwater && current.Link.FloodProtection != null && current.Link.FloodProtection.IsTransitioning))
                        yield return null;
                    for (float age = 0f; age < holdSeconds && current.IsValid && !Interrupted; age += Time.deltaTime)
                        yield return null;
                    if (Interrupted) break;
                    current = null;
                }
                // Other card actions keep their priority. Product drags extend this same camera tour.
                if (Interrupted) ApplyRemaining();
                while (!Interrupted && QueuingUpgrade) yield return null;
                if (pending.Count > 0 && !Interrupted) continue;
                if (navigation != null) navigation.EndPresentationFocus(returnSeconds);
                while (navigation != null && navigation.isActiveAndEnabled && navigation.IsPresenting && pending.Count == 0)
                {
                    // A late product drag may begin during the final return. Keep the original
                    // home view, pause at the rendered pose and continue directly to its house.
                    if (!Interrupted && QueuingUpgrade)
                    {
                        navigation.HoldPresentationFocus();
                        while (!Interrupted && QueuingUpgrade) yield return null;
                        if (pending.Count > 0 && !Interrupted) break;
                        if (Interrupted) ApplyRemaining();
                        navigation.EndPresentationFocus(returnSeconds);
                    }
                    yield return null;
                }
            } while (pending.Count > 0);
            current = null;
            if (dock != null) dock.EndCityPresentation();
            presentation = null;
        }

        private bool HasPendingCardAnimation()
        {
            foreach (var item in pending)
                if (item.IsValid && item.Link.House.IsInstalling) return true;
            return false;
        }

        private void ApplyRemaining()
        {
            if (current != null && current.IsValid) current.Link.ApplyInstalled(current.Kind, current.Product);
            foreach (var item in pending)
                if (item.IsValid) item.Link.ApplyInstalled(item.Kind, item.Product);
            pending.Clear();
            current = null;
        }

        private void OnDisable()
        {
            if (presentation == null) return;
            StopCoroutine(presentation);
            presentation = null;
            ApplyRemaining();
            if (navigation != null) navigation.EndPresentationFocus(0f);
            if (dock != null) dock.EndCityPresentation();
        }
    }
}
