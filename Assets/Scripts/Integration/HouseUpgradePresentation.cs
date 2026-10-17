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
        [SerializeField, Range(1f, 1.3f)] private float zoomFactor = 1.12f;
        [SerializeField, Min(0.01f)] private float zoomSeconds = 0.4f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.55f;
        [SerializeField, Min(0.01f)] private float returnSeconds = 0.35f;

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
        private bool Interrupted => game == null || game.IsDraggingAnyCard || game.IsChoosingBlueprint;

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
            while (!Interrupted && HasPendingCardAnimation()) yield return null;
            if (!Interrupted && dock != null) dock.BeginCityPresentation();
            do
            {
                while (pending.Count > 0 && !Interrupted)
                {
                    current = pending[0];
                    pending.RemoveAt(0);
                    if (!current.IsValid) { current = null; continue; }
                    while (dock != null && dock.isActiveAndEnabled && dock.Expansion > 0.001f &&
                           current.IsValid && !Interrupted) yield return null;
                    if (Interrupted) break;
                    if (!current.IsValid) { current = null; continue; }
                    bool focused = navigation != null && navigation.BeginPresentationFocus(
                        BuildingCenter(current.Link.Building), zoomFactor, zoomSeconds);
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
                    for (float age = 0f; age < holdSeconds && current.IsValid && !Interrupted; age += Time.unscaledDeltaTime)
                        yield return null;
                    if (Interrupted) break;
                    current = null;
                }
                // A fresh player action takes priority, but an accepted product must still be applied.
                if (Interrupted) ApplyRemaining();
                if (navigation != null) navigation.EndPresentationFocus(returnSeconds);
                while (navigation != null && navigation.isActiveAndEnabled && navigation.IsPresenting && pending.Count == 0)
                    yield return null;
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

        private static Vector3 BuildingCenter(Transform building)
        {
            if (building == null) return Vector3.zero;
            var renderers = building.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return building.position;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
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
