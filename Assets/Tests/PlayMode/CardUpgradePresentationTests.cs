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

    [Test]
    public void RailPistonsReachTwiceTheLiftThenSeatTheHouseBeforeOneAndAHalfTimesTheDust()
    {
        var rail = CityHouseLinks()[0].RailSystem;
        var parts = ControlField<BuildingRailUpgrade[]>(rail, "partUpgrades");
        PresentationField(rail, "managedExternally", true);
        rail.SetInstalled(true);
        float duration = ControlField<float>(rail, "transitionDuration");
        InvokeCity(rail, "AdvanceTransition", duration * 0.5f);
        float height = ControlField<float>(rail, "liftHeight");
        Assert.That(rail.CurrentLift, Is.EqualTo(height * 2f).Within(0.0001f));
        var rods = parts.SelectMany(part => ControlField<Transform[]>(part, "pistonRods")).ToArray();
        var extendedRods = rods.Select(rod => rod.localScale.y).ToArray();
        var grounds = parts.Select(part => ControlField<GameObject>(part, "railVisuals").transform.position).ToArray();
        Assert.That(parts.All(part => ControlField<ParticleSystem>(part, "installationDust") == null), Is.True);
        InvokeCity(rail, "AdvanceTransition", duration * 0.35f);
        Assert.That(rail.CurrentLift, Is.GreaterThan(height).And.LessThan(height * 2f));
        Assert.That(parts.All(part => ControlField<ParticleSystem>(part, "installationDust") == null), Is.True,
            "No construction dust while the house is still descending.");
        InvokeCity(rail, "AdvanceTransition", duration * 0.2f);
        Assert.That(rail.CurrentLift, Is.EqualTo(height).Within(0.0001f));
        Assert.That(rail.IsTransitioning, Is.False);
        for (int i = 0; i < rods.Length; i++) Assert.That(rods[i].localScale.y, Is.LessThan(extendedRods[i]));
        for (int i = 0; i < parts.Length; i++)
            Assert.That(Vector3.Distance(ControlField<GameObject>(parts[i], "railVisuals").transform.position, grounds[i]), Is.LessThan(0.0001f));
        int enhancedDust = parts.Sum(part => ControlField<ParticleSystem>(part, "installationDust").particleCount);
        Assert.That(enhancedDust, Is.GreaterThan(0));
        rail.SetInstalled(false);
        InvokeCity(rail, "AdvanceTransition", 0.4f);
        foreach (var part in parts) PresentationField(part, "dustMultiplier", 1f);
        rail.SetInstalled(true);
        InvokeCity(rail, "AdvanceTransition", 2f);
        int baseDust = parts.Sum(part => ControlField<ParticleSystem>(part, "installationDust").particleCount);
        Assert.That((float)enhancedDust / baseDust, Is.EqualTo(1.5f).Within(0.04f));
    }
}
