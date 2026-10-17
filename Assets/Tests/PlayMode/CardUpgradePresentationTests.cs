using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    private static void PresentationField(object owner, string name, object value) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

    private static WorkerSlot[] WorkerSlots(BuildingStation station) => station.GetComponentsInChildren<WorkerSlot>(true)
        .OrderBy(slot => ControlField<int>(slot, "index")).ToArray();

    private IEnumerator FinishHousePresentation(HouseUpgradePresentation presentation)
    {
        float timeout = Time.realtimeSinceStartup + 6f;
        while (presentation.IsPresenting && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(presentation.IsPresenting, Is.False, "The camera and drawer must release their presentation locks.");
    }

    [UnityTest]
    public IEnumerator WorkerPortraitUpperRightCornersReceiveHoverInEveryWorkplace()
    {
        school.TryAssign(People()[0]);
        school.AdvanceWork(25.01f);
        yield return new WaitForSecondsRealtime(.6f);
        var savedUI = UIIds(game);
        foreach (var station in new[] { research, school, Station(CardKind.Factory) })
        {
            var first = station.Card.Kind == CardKind.Factory ? Educated() : People().First(p => p.AssignedTo == null);
            Assert.That(station.TryAssign(first), Is.True);
            var second = station == research ? Educated() : People().First(p => p.AssignedTo == null);
            Assert.That(station.TryAssign(second), Is.True);
            yield return new WaitForSecondsRealtime(.6f);
            foreach (var slot in WorkerSlots(station))
            {
                Vector2 rest = slot.Portrait.rectTransform.anchoredPosition;
                var hit = PortraitUpperRightHit(slot);
                ExecuteEvents.ExecuteHierarchy(hit.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.04f);
                Assert.That(slot.Portrait.rectTransform.anchoredPosition.y, Is.GreaterThan(rest.y).And.LessThan(rest.y + 24f));
                yield return new WaitForSecondsRealtime(.55f);
                Assert.That(slot.Portrait.rectTransform.anchoredPosition.y, Is.EqualTo(rest.y + 24f).Within(.5f));
                PortraitUpperRightHit(slot);
                Hover(slot.gameObject, false);
                yield return new WaitForSecondsRealtime(.55f);
                Assert.That(Vector2.Distance(slot.Portrait.rectTransform.anchoredPosition, rest), Is.LessThan(.3f));
            }
            station.ReleaseWorker(0, false);
            station.ReleaseWorker(1, false);
        }
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }

    private static RaycastResult PortraitUpperRightHit(WorkerSlot slot)
    {
        var portrait = slot.Portrait.rectTransform;
        // The right pocket overlaps the other portrait. Check each visible outer corner,
        // including the upper-right edge of both the normal and educated artwork.
        float edge = slot.Index == 0 ? portrait.rect.xMax - 19f : portrait.rect.xMin + 19f;
        var position = portrait.TransformPoint(new Vector3(edge, portrait.rect.yMax - 35f));
        var pointer = new PointerEventData(EventSystem.current)
        { position = RectTransformUtility.WorldToScreenPoint(null, position) };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty, slot.name);
        Assert.That(hits[0].gameObject.GetComponentInParent<WorkerSlot>(), Is.SameAs(slot),
            slot.transform.parent.parent.name + " / " + slot.name + ": the visible upper-right corner must reach the worker.");
        return hits[0];
    }

    [UnityTest]
    public IEnumerator ElevatorSupportsEmergeFromBelowAndAttachWithoutLosingOtherProtection()
    {
        var link = CityHouseLinks()[0];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        var supports = ControlField<Transform>(model, "elevatorSupports");
        var rest = supports.localPosition;
        var ground = link.Building.Find("Apartment_Ground");
        var groundPosition = ground.position;
        link.RailSystem.SetInstalled(true);
        yield return new WaitForSecondsRealtime(1.4f);
        link.FireProtection.SetInstalled(true);
        var savedUI = UIIds(game);
        model.SetElevated(true);
        yield return null;
        Assert.That(supports.gameObject.activeSelf, Is.False);
        float timeout = Time.realtimeSinceStartup + 2f;
        while (model.ElevatorAttachment < .1f && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(model.IsElevated, Is.True);
        Assert.That(model.ElevatorAttachment, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(supports.gameObject.activeInHierarchy, Is.True);
        float approachingY = supports.position.y - supports.parent.position.y;
        CaptureAuthoredApartmentSprinklers(link.Building, "elevator-magnetic-approach");
        while (model.ElevatorAttachment < .85f && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(supports.position.y - supports.parent.position.y, Is.GreaterThan(approachingY + .05f));
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(model.IsTransitioning, Is.False);
        Assert.That(model.ElevatorAttachment, Is.EqualTo(1f));
        Assert.That(Vector3.Distance(supports.localPosition, rest), Is.LessThan(.0001f));
        Assert.That(model.HasSeismic && model.HasFireProtection, Is.True);
        Assert.That(ground.position, Is.EqualTo(groundPosition));
        CaptureAuthoredApartmentSprinklers(link.Building, "elevator-magnetic-attached");
        model.SetElevated(false);
        yield return new WaitForSecondsRealtime(.2f);
        model.SetElevated(true);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(model.IsElevated && model.HasSeismic && model.HasFireProtection, Is.True);
        Assert.That(Vector3.Distance(supports.localPosition, rest), Is.LessThan(.0001f));
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }

    [UnityTest]
    public IEnumerator GreenAndPurpleWorkersRiseSmoothlyAndReturnWithoutMovingTheirSlots()
    {
        school.TryAssign(People()[0]);
        school.AdvanceWork(25.01f);
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.That(research.TryAssign(People()[0]), Is.True);
        Assert.That(research.TryAssign(Educated()), Is.True);
        yield return new WaitForSecondsRealtime(0.6f);
        var slots = WorkerSlots(research);
        var rest = slots.Select(slot => slot.Portrait.rectTransform.anchoredPosition).ToArray();
        var pockets = slots.Select(slot => ((RectTransform)slot.transform).anchoredPosition).ToArray();
        var savedUI = UIIds(game);
        foreach (var slot in slots) Hover(slot.gameObject, true);
        yield return new WaitForSecondsRealtime(0.04f);
        foreach (var slot in slots)
        {
            int index = ControlField<int>(slot, "index");
            float lift = slot.Portrait.rectTransform.anchoredPosition.y - rest[index].y;
            Assert.That(lift, Is.GreaterThan(0f).And.LessThan(24f), "The portrait must interpolate rather than jump.");
        }
        yield return new WaitForSecondsRealtime(0.5f);
        for (int i = 0; i < slots.Length; i++)
        {
            Assert.That(slots[i].Portrait.rectTransform.anchoredPosition.y - rest[i].y, Is.EqualTo(24f).Within(0.5f));
            Assert.That(((RectTransform)slots[i].transform).anchoredPosition, Is.EqualTo(pockets[i]));
            var rect = (RectTransform)slots[i].transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null,
                    rect.TransformPoint(new Vector2(rect.rect.center.x, rect.rect.yMax + 20f)))
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<WorkerSlot>(), Is.SameAs(slots[i]),
                "The newly exposed top of a raised person must still receive its click.");
        }
        string imagePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/worker-portrait-hover.png"));
        yield return CaptureCanvas(imagePath, slots[0].gameObject);
        foreach (var slot in slots) Hover(slot.gameObject, false);
        yield return new WaitForSecondsRealtime(0.55f);
        for (int i = 0; i < slots.Length; i++)
            Assert.That(Vector2.Distance(slots[i].Portrait.rectTransform.anchoredPosition, rest[i]), Is.LessThan(0.3f));
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }

    [UnityTest]
    public IEnumerator ClickingARaisedSchoolWorkerStartsItsReturnAtTheVisiblePortrait()
    {
        var slot = WorkerSlots(school)[0];
        Vector2 rest = slot.Portrait.rectTransform.anchoredPosition;
        Hover(slot.gameObject, true);
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(slot.Portrait.rectTransform.anchoredPosition, Is.EqualTo(rest), "An empty pocket must not rise.");
        var person = People()[0];
        school.TryAssign(person);
        yield return new WaitForSecondsRealtime(0.6f);
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(slot.Portrait.rectTransform.anchoredPosition.y, Is.GreaterThan(rest.y + 23f));
        Vector3 visible = slot.Portrait.rectTransform.position;
        slot.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        Assert.That(slot.IsReturning, Is.True);
        Assert.That(Vector3.Distance(slot.VisibleWorkerRect.position, visible), Is.LessThan(0.01f));
        Assert.That(slot.Portrait.rectTransform.anchoredPosition, Is.EqualTo(rest));
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.That(person.IsReturningFromFolder, Is.False);
        Assert.That(person.AssignedTo, Is.Null);
        Assert.That(slot.Portrait.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator WorkerHoverYieldsToDraggingAndResumesWithoutChangingFolderEntry()
    {
        var slot = WorkerSlots(research)[0];
        Vector2 rest = slot.Portrait.rectTransform.anchoredPosition;
        research.TryAssign(People()[0]);
        Hover(slot.gameObject, true);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(slot.IsEntering, Is.True);
        Assert.That(slot.Portrait.rectTransform.anchoredPosition, Is.EqualTo(rest));
        yield return new WaitForSecondsRealtime(0.9f);
        var pointer = new PointerEventData(EventSystem.current)
        { button = PointerEventData.InputButton.Left, pointerId = -1, position = Vector2.zero };
        var other = People().First(person => person.AssignedTo == null);
        other.OnBeginDrag(pointer);
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.That(Vector2.Distance(slot.Portrait.rectTransform.anchoredPosition, rest), Is.LessThan(0.3f));
        other.OnEndDrag(pointer);
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.That(slot.Portrait.rectTransform.anchoredPosition.y, Is.GreaterThan(rest.y + 23f));
        Vector3 visible = slot.VisibleWorkerRect.position;
        slot.OnBeginDrag(pointer);
        Assert.That(research.WorkerCount, Is.Zero);
        var worker = game.DraggedCard;
        Assert.That(worker, Is.Not.Null);
        var artwork = worker.transform.Find("Artwork") as RectTransform;
        Assert.That(Vector3.Distance(artwork.position, visible), Is.LessThan(0.05f));
        slot.OnEndDrag(pointer);
    }

    [UnityTest]
    public IEnumerator HouseInstallationWaitsForTheCollapsedDrawerAndSmoothCameraFocus()
    {
        var link = CityHouseLinks()[1];
        var product = ProduceCityProducts(UpgradeKind.RailSystem)[0];
        var presentation = Object.FindFirstObjectByType<HouseUpgradePresentation>();
        var dock = game.GetComponent<CardDock>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var city = Object.FindFirstObjectByType<CityViewport>();
        presentation.enabled = dock.enabled = true;
        dock.SetExpanded(true, true);
        float zoomBefore = city.WorldCamera.orthographicSize;
        Vector2 panBefore = navigation.PanOffset;
        var savedUI = UIIds(game);
        Assert.That(link.House.TryInstall(product), Is.True);
        Assert.That(dock.IsExpanded, Is.False);
        Assert.That(dock.Expansion, Is.EqualTo(1f));
        Assert.That(link.RailSystem.IsInstalled, Is.False);
        bool sawCollapse = false, sawSmoothZoom = false;
        float timeout = Time.realtimeSinceStartup + 4f;
        while (!link.RailSystem.IsInstalled && Time.realtimeSinceStartup < timeout)
        {
            if (dock.Expansion > 0.001f)
            {
                sawCollapse = true;
                Assert.That(navigation.IsPresenting, Is.False, "The camera should wait for the drawer.");
            }
            if (navigation.IsPresenting && !navigation.IsFocusReady)
            {
                Assert.That(dock.Expansion, Is.LessThan(0.001f));
                if (city.WorldCamera.orthographicSize < zoomBefore - 0.001f) sawSmoothZoom = true;
            }
            Assert.That(link.RailSystem.IsTransitioning, Is.False);
            yield return null;
        }
        Assert.That(sawCollapse && sawSmoothZoom, Is.True);
        Assert.That(link.RailSystem.IsInstalled, Is.True);
        Assert.That(navigation.IsFocusReady, Is.True);
        Assert.That(city.WorldCamera.orthographicSize, Is.EqualTo(zoomBefore / 1.12f).Within(0.01f));
        timeout = Time.realtimeSinceStartup + 3f;
        while (link.RailSystem.IsTransitioning && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(link.RailSystem.IsTransitioning, Is.False);
        Assert.That(navigation.IsFocusReady, Is.True, "Keep the camera close until the pistons have seated the house.");
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/house-upgrade-focused.png"));
        yield return CaptureCanvas(path);
        yield return FinishHousePresentation(presentation);
        yield return null;
        Assert.That(navigation.PanOffset, Is.EqualTo(panBefore));
        Assert.That(city.WorldCamera.orthographicSize, Is.EqualTo(zoomBefore).Within(0.01f));
        Assert.That(dock.IsCityPresentationActive, Is.False);
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        dock.enabled = false;
        Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, dock.ActivationArea.TransformPoint(dock.ActivationArea.rect.center));
        dock.BeginCityPresentation();
        dock.TickPointer(inside, 1f);
        dock.EndCityPresentation();
        dock.TickPointer(inside, 1f);
        Assert.That(dock.IsExpanded, Is.True, "A cursor already over the drawer reopens it once the installation presentation ends.");
        dock.TickPointer(new Vector2(-100f, Screen.height + 100f), 1f);
        Assert.That(dock.IsExpanded, Is.False);
        dock.BeginCityPresentation();
        dock.TickPointer(new Vector2(-100f, Screen.height + 100f), 1f);
        dock.EndCityPresentation();
        dock.TickPointer(new Vector2(-100f, Screen.height + 100f), 1f);
        Assert.That(dock.IsExpanded, Is.False, "Finishing an installation must not open the drawer while the cursor is over the city.");
        dock.TickPointer(inside, 1f);
        Assert.That(dock.IsExpanded, Is.True, "A fresh approach restores the normal two-row drawer.");
    }

    [UnityTest]
    public IEnumerator OtherHouseProductsUseTheSameFocusAndPendingRemovalsNeverReappear()
    {
        var links = CityHouseLinks();
        var rail = ProduceCityProducts(UpgradeKind.RailSystem);
        var flood = ProduceCityProducts(UpgradeKind.Breakwater);
        var fire = ProduceCityProducts(UpgradeKind.Fire);
        var presentation = Object.FindFirstObjectByType<HouseUpgradePresentation>();
        var dock = game.GetComponent<CardDock>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        presentation.enabled = dock.enabled = true;
        links[0].House.TryInstall(rail[0]);
        links[0].House.Release(UpgradeKind.RailSystem, false);
        links[1].House.TryInstall(flood[0]);
        links[2].House.TryInstall(fire[0]);
        links[2].House.Release(UpgradeKind.Fire, false);
        links[3].House.TryInstall(fire[1]);
        foreach (var pair in new[] { (link: links[1], kind: UpgradeKind.Breakwater), (link: links[3], kind: UpgradeKind.Fire) })
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (!CityHasUpgrade(pair.link, pair.kind) && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(CityHasUpgrade(pair.link, pair.kind), Is.True);
            Assert.That(navigation.IsFocusReady, Is.True);
            Assert.That(dock.Expansion, Is.LessThan(0.001f));
        }
        yield return FinishHousePresentation(presentation);
        Assert.That(links[0].RailSystem.IsInstalled, Is.False);
        Assert.That(links[2].FireProtection.IsInstalled, Is.False);
        Assert.That(navigation.IsPresenting, Is.False);
    }

    [UnityTest]
    public IEnumerator StartingAnotherDragReleasesThePresentationWithoutLosingAnAcceptedUpgrade()
    {
        var link = CityHouseLinks()[0];
        var product = ProduceCityProducts(UpgradeKind.Fire)[0];
        var presentation = Object.FindFirstObjectByType<HouseUpgradePresentation>();
        var dock = game.GetComponent<CardDock>();
        presentation.enabled = dock.enabled = true;
        link.House.TryInstall(product);
        yield return new WaitForSecondsRealtime(0.45f);
        var person = People().First(worker => worker.AssignedTo == null);
        var pointer = new PointerEventData(EventSystem.current)
        { button = PointerEventData.InputButton.Left, pointerId = -1, position = Vector2.zero };
        person.OnBeginDrag(pointer);
        yield return null;
        Assert.That(link.FireProtection.IsInstalled, Is.True);
        Assert.That(dock.IsExpanded, Is.True);
        person.OnEndDrag(pointer);
        yield return FinishHousePresentation(presentation);
        Assert.That(product.InstalledIn, Is.SameAs(link.House));
        Assert.That(dock.IsCityPresentationActive, Is.False);
        Assert.That(Object.FindFirstObjectByType<CityCameraController>().IsPresenting, Is.False);
    }

    [Test]
    public void HouseFocusStaysAnchoredAndRestoresThePlayerViewAtWideAndTallAspects()
    {
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var camera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
        float oldAspect = camera.aspect;
        try
        {
            foreach (float aspect in new[] { 16f / 9f, 4f / 3f, 0.75f })
            {
                camera.aspect = aspect;
                PresentationField(navigation, "targetPan", new Vector2(0.2f, -0.3f));
                PresentationField(navigation, "targetZoom", 2.5f);
                InvokeCity(navigation, "AdvanceNavigation", 2f);
                Vector2 homePan = navigation.PanOffset;
                float homeZoom = camera.orthographicSize;
                Vector3 target = CityHouseLinks()[1].Building.position + Vector3.up * 0.4f;
                Vector2 screenBefore = camera.WorldToViewportPoint(target);
                Assert.That(navigation.BeginPresentationFocus(target, 1.12f, 0.4f), Is.True);
                InvokeCity(navigation, "AdvanceNavigation", 0.2f);
                Assert.That(camera.orthographicSize, Is.LessThan(homeZoom).And.GreaterThan(homeZoom / 1.12f));
                Assert.That(navigation.IsFocusReady, Is.False);
                InvokeCity(navigation, "AdvanceNavigation", 0.2f);
                Assert.That(navigation.IsFocusReady, Is.True);
                Vector2 screenAfter = camera.WorldToViewportPoint(target);
                Assert.That(Vector2.Distance(screenBefore, screenAfter), Is.LessThan(0.0001f), "Aspect: " + aspect);
                navigation.EndPresentationFocus(0.3f);
                InvokeCity(navigation, "AdvanceNavigation", 0.15f);
                Assert.That(navigation.IsPresenting, Is.True);
                InvokeCity(navigation, "AdvanceNavigation", 0.15f);
                Assert.That(navigation.IsPresenting, Is.False);
                Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
                Assert.That(camera.orthographicSize, Is.EqualTo(homeZoom).Within(0.0001f));
            }
        }
        finally { camera.aspect = oldAspect; }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SeismicIsolatorsAttachAtTheApexAndShareTheSlowerLandingAndLighterDust(bool elevated)
    {
        var link = CityHouseLinks()[0];
        var rail = link.RailSystem;
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        model.SetElevated(elevated, false);
        var isolator = ApartmentIsolators(model)[elevated ? 1 : 0];
        var authoredPosition = isolator.position;
        var ground = link.Building.Find("Apartment_Ground");
        var groundPosition = ground.position;
        PresentationField(rail, "managedExternally", true);
        rail.SetInstalled(true);
        float duration = ControlField<float>(rail, "transitionDuration");
        float landing = ControlField<float>(rail, "settlingDuration");
        Assert.That(isolator.gameObject.activeSelf, Is.False);
        InvokeCity(rail, "AdvanceTransition", duration * .3f);
        Assert.That(model.SeismicAttachment, Is.GreaterThan(0f).And.LessThan(1f));
        var approaching = isolator.position - model.MotionRoot.position;
        CaptureAuthoredApartmentSprinklers(link.Building, elevated ? "elevated-magnetic-approach" : "normal-magnetic-approach");
        InvokeCity(rail, "AdvanceTransition", duration * .3f);
        Assert.That(model.SeismicAttachment, Is.EqualTo(1f));
        float height = ControlField<float>(rail, "liftHeight");
        Assert.That(rail.CurrentLift, Is.EqualTo(height * 2f).Within(0.0001f));
        Assert.That(isolator.gameObject.activeInHierarchy, Is.True);
        Vector3 attachedOffset = isolator.position - model.MotionRoot.position;
        Assert.That(attachedOffset.y - approaching.y, Is.GreaterThan(.05f), "The isolators must rise toward the body before attaching.");
        CaptureAuthoredApartmentSprinklers(link.Building, elevated ? "elevated-magnetic-attached" : "normal-magnetic-attached");
        InvokeCity(rail, "AdvanceTransition", landing * .5f);
        float rest = elevated ? 0f : height;
        Assert.That(rail.CurrentLift, Is.GreaterThan(rest).And.LessThan(height * 2f));
        Assert.That(Vector3.Distance(isolator.position - model.MotionRoot.position, attachedOffset), Is.LessThan(.0001f));
        Assert.That(rail.GetComponentsInChildren<ParticleSystem>().Length, Is.Zero,
            "Dust starts at landing while the apartment and its attached isolators descend as one.");
        Assert.That(landing, Is.GreaterThan(duration * .4f));
        InvokeCity(rail, "AdvanceTransition", landing * .5f + .01f);
        Assert.That(rail.CurrentLift, Is.EqualTo(rest).Within(0.0001f));
        Assert.That(rail.IsTransitioning, Is.False);
        Assert.That(Vector3.Distance(isolator.position, authoredPosition), Is.LessThan(.0001f));
        Assert.That(ground.position, Is.EqualTo(groundPosition));
        var puffs = rail.GetComponentsInChildren<ParticleSystem>();
        Assert.That(puffs.Length, Is.EqualTo(6));
        int enhancedDust = puffs.Sum(puff => puff.particleCount);
        Assert.That(enhancedDust, Is.GreaterThan(0));
        int lavenderParticles = 0;
        foreach (var puff in puffs)
        {
            var particles = new ParticleSystem.Particle[128];
            int count = puff.GetParticles(particles);
            foreach (var particle in particles.Take(count))
            {
                Color tint = particle.startColor;
                Assert.That(tint.r, Is.GreaterThan(.82f).And.LessThan(.94f));
                Assert.That(tint.g, Is.GreaterThan(.78f));
                Assert.That(tint.b, Is.GreaterThan(.85f).And.LessThan(1f));
                if (tint.b > tint.r + .02f) lavenderParticles++;
            }
            puff.Simulate(.1f, false, false, false);
        }
        Assert.That(lavenderParticles, Is.GreaterThan(0), "Light lavender is mixed into the stone-colored dust.");
        CaptureAuthoredApartmentSprinklers(link.Building, elevated ? "elevated-seismic-dust" : "normal-seismic-dust", rail.transform);
        foreach (var puff in puffs) puff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        CaptureAuthoredApartmentSprinklers(link.Building, elevated ? "elevated-seismic-seated" : "normal-seismic-seated");
        rail.SetInstalled(false);
        InvokeCity(rail, "AdvanceTransition", 0.4f);
        PresentationField(rail, "dustMultiplier", 1f);
        rail.SetInstalled(true);
        InvokeCity(rail, "AdvanceTransition", 2f);
        int baseDust = puffs.Sum(puff => puff.particleCount);
        Assert.That((float)enhancedDust / baseDust, Is.EqualTo(2.25f).Within(0.04f));
    }
}
