using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator OccupiedGridDropsExchangeBothCardsSmoothlyThroughPointerEvents()
    {
        var incoming = research.Card;
        var displaced = school.Card;
        int source = game.Grid.IndexOf(incoming), target = game.Grid.IndexOf(displaced);
        var savedUI = UIIds(game);
        Vector3 otherStart = displaced.Rect.position;
        var pointer = BeginGridDrag(incoming);
        MoveGridDrag(incoming, pointer, displaced.Rect.position + game.Grid.rectTransform.TransformVector(new Vector3(35f, 8f)));
        Vector3 droppedAt = incoming.Rect.position;
        EndGridDrag(incoming, pointer);

        Assert.That(game.Grid.GetOccupant(source), Is.SameAs(displaced));
        Assert.That(game.Grid.GetOccupant(target), Is.SameAs(incoming));
        Assert.That(incoming.IsMovingInGrid && displaced.IsMovingInGrid, Is.True);
        Assert.That(game.HasActiveCardAnimation, Is.True);
        Assert.That(incoming.CanShowHover || displaced.CanShowHover, Is.False);
        Assert.That(game.IsDraggingAnyCard || incoming.IsDragging, Is.False);
        Assert.That(Vector3.Distance(incoming.Rect.position, droppedAt), Is.LessThan(.01f), "Drop must start at the visible card, without teleporting.");
        Assert.That(Vector3.Distance(displaced.Rect.position, otherStart), Is.LessThan(.01f));
        game.Grid.RefreshPositions();
        Assert.That(Vector3.Distance(displaced.Rect.position, otherStart), Is.LessThan(.01f), "Layout refresh must not snap a moving card.");
        AssertUniqueGridOccupancy();

        yield return new WaitForSecondsRealtime(.11f);
        Assert.That(Vector3.Distance(displaced.Rect.position, otherStart), Is.GreaterThan(1f));
        Assert.That(Vector3.Distance(displaced.Rect.position, game.Grid.GetCellWorldPosition(source)), Is.GreaterThan(1f));
        Assert.That(Vector3.Distance(incoming.Rect.position, droppedAt), Is.GreaterThan(.1f));
        string directory = Path.Combine(Application.dataPath, "../Library/CardGameTestResults/GridSwap");
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "swapping.png"));
        yield return new WaitForSecondsRealtime(.65f);
        AssertGridCardsAtTheirDestinations();
        Assert.That(game.Grid.IsAnimating, Is.False);
        Assert.That(UIIds(game), Is.EqualTo(savedUI), "The animation must reuse the saved cards and UI.");
        yield return CaptureCanvas(Path.Combine(directory, "swapped.png"));
    }

    [UnityTest]
    public IEnumerator MovingGridCardsCanBeGrabbedAndSwappedAgainWithoutJumpingOrDuplicatingSlots()
    {
        var first = research.Card;
        var second = Station(CardKind.Factory).Card;
        var third = game.Cards.First(card => card.Kind == CardKind.House);
        int a = game.Grid.IndexOf(first), b = game.Grid.IndexOf(second), c = game.Grid.IndexOf(third);
        var pointer = BeginGridDrag(first);
        MoveGridDrag(first, pointer, second.Rect.position);
        EndGridDrag(first, pointer);
        yield return new WaitForSecondsRealtime(.14f);

        Vector3 visible = second.Rect.position;
        pointer = BeginGridDrag(second);
        Assert.That(Vector3.Distance(second.Rect.position, visible), Is.LessThan(.01f));
        Assert.That(second.IsMovingInGrid, Is.False, "Picking up a moving card transfers control to the pointer.");
        MoveGridDrag(second, pointer, third.Rect.position);
        EndGridDrag(second, pointer);
        Assert.That(game.Grid.GetOccupant(a), Is.SameAs(third));
        Assert.That(game.Grid.GetOccupant(b), Is.SameAs(first));
        Assert.That(game.Grid.GetOccupant(c), Is.SameAs(second));
        AssertUniqueGridOccupancy();
        yield return new WaitForSecondsRealtime(.7f);
        AssertGridCardsAtTheirDestinations();

        // Repeated swaps must preserve the same seven identities and one empty slot.
        for (int i = 0; i < 3; i++)
        {
            int from = game.Grid.IndexOf(first), to = game.Grid.IndexOf(third);
            pointer = BeginGridDrag(first);
            MoveGridDrag(first, pointer, third.Rect.position);
            EndGridDrag(first, pointer);
            Assert.That(game.Grid.GetOccupant(from), Is.SameAs(third));
            Assert.That(game.Grid.GetOccupant(to), Is.SameAs(first));
            yield return new WaitForSecondsRealtime(.45f);
            AssertUniqueGridOccupancy();
            AssertGridCardsAtTheirDestinations();
        }
    }

    [UnityTest]
    public IEnumerator SwappingWorkingBuildingsPreservesWorkersOrdersProgressAndAP()
    {
        var factory = FactoryWithWorkers(1, 0);
        var station = Station(CardKind.Factory);
        var worker = station.GetWorker(0);
        var plan = factory.PendingBlueprint;
        var student = People().First(person => !person.IsStored && person.CanStartWork);
        Assert.That(school.TryAssign(student), Is.True);
        school.AdvanceWork(game.EducationSeconds * .2f);
        factory.AdvanceWork(factory.ProductionSeconds * .2f);
        float progress = factory.Progress, education = school.GetEducationProgress(0);
        int balance = game.AbilityPoints.CurrentPoints;
        var savedUI = UIIds(game);
        int factoryCell = game.Grid.IndexOf(station.Card), schoolCellBefore = game.Grid.IndexOf(school.Card);

        var pointer = BeginGridDrag(station.Card);
        MoveGridDrag(station.Card, pointer, school.Card.Rect.position);
        EndGridDrag(station.Card, pointer);
        Assert.That(game.Grid.GetOccupant(factoryCell), Is.SameAs(school.Card));
        Assert.That(game.Grid.GetOccupant(schoolCellBefore), Is.SameAs(station.Card));
        yield return new WaitForSecondsRealtime(.75f);
        AssertGridCardsAtTheirDestinations();
        Assert.That(station.GetWorker(0), Is.SameAs(worker));
        Assert.That(worker.AssignedTo, Is.SameAs(station));
        Assert.That(school.GetWorker(0), Is.SameAs(student));
        Assert.That(student.AssignedTo, Is.SameAs(school));
        Assert.That(factory.PendingBlueprint, Is.SameAs(plan));
        Assert.That(plan.StoredAtFactory, Is.SameAs(factory));
        Assert.That(factory.Progress, Is.EqualTo(progress));
        Assert.That(school.GetEducationProgress(0), Is.EqualTo(education));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance));
        Assert.That(factory.GetComponent<FactoryFolderPresentation>().StoredBlueprint.gameObject.activeSelf, Is.True);
        Assert.That(station.GetComponentsInChildren<WorkerSlot>(true).Single(slot => slot.Index == 0).Portrait.gameObject.activeSelf, Is.True);
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        // People still enter the chosen building rather than swapping it.
        var extra = People().First(person => !person.IsStored && person.CanStartWork);
        int schoolCell = game.Grid.IndexOf(school.Card);
        Drag(extra, school.Card.Rect.position);
        Assert.That(extra.AssignedTo, Is.SameAs(school));
        Assert.That(game.Grid.IndexOf(school.Card), Is.EqualTo(schoolCell));
        Assert.That(game.Grid.IsAnimating, Is.False);
    }

    [UnityTest]
    public IEnumerator SwappingUpgradedHouseCardsPreservesTheirOwnCityBuildingsAndSystems()
    {
        var links = CityHouseLinks();
        var first = links[0];
        var second = links[1];
        var product = ProduceCityProducts(UpgradeKind.RailSystem)[0];
        Assert.That(first.House.TryInstall(product), Is.True);
        yield return new WaitForSecondsRealtime(.65f);
        var firstBuilding = first.Building;
        var secondBuilding = second.Building;
        int a = game.Grid.IndexOf(first.House.Card), b = game.Grid.IndexOf(second.House.Card);
        var pointer = BeginGridDrag(first.House.Card);
        MoveGridDrag(first.House.Card, pointer, second.House.Card.Rect.position);
        EndGridDrag(first.House.Card, pointer);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(game.Grid.GetOccupant(a), Is.SameAs(second.House.Card));
        Assert.That(game.Grid.GetOccupant(b), Is.SameAs(first.House.Card));
        Assert.That(first.Building, Is.SameAs(firstBuilding));
        Assert.That(second.Building, Is.SameAs(secondBuilding));
        Assert.That(first.House.GetInstalled(UpgradeKind.RailSystem), Is.SameAs(product));
        Assert.That(product.InstalledIn, Is.SameAs(first.House));
        Assert.That(first.RailSystem.IsInstalled, Is.True);
        Assert.That(second.RailSystem.IsInstalled, Is.False);
        Assert.That(second.House.GetInstalled(UpgradeKind.RailSystem), Is.Null);
        AssertUniqueGridOccupancy();
        AssertGridCardsAtTheirDestinations();
    }

    [UnityTest]
    public IEnumerator GridSwapUsesUnscaledTimeAndFollowsDockAndResolutionChanges()
    {
        var canvas = game.GetComponentInParent<Canvas>();
        var camera = GameObject.Find("Display Camera").GetComponent<Camera>();
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        var oldTarget = camera.targetTexture;
        float oldDistance = canvas.planeDistance, oldTimeScale = Time.timeScale;
        var wide = new RenderTexture(3840, 2160, 24);
        var portrait = new RenderTexture(1080, 1920, 24);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = wide;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Time.timeScale = 0f;
            var pointer = BeginGridDrag(research.Card);
            MoveGridDrag(research.Card, pointer, school.Card.Rect.position);
            EndGridDrag(research.Card, pointer);
            yield return new WaitForSecondsRealtime(.09f);
            Assert.That(game.Grid.IsAnimating, Is.True);
            camera.targetTexture = portrait;
            game.GetComponent<CardDock>().SetExpanded(false, true);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(game.Grid.IsAnimating, Is.False);
            AssertUniqueGridOccupancy();
            AssertGridCardsAtTheirDestinations();
        }
        finally
        {
            Time.timeScale = oldTimeScale;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = oldTarget;
            wide.Release(); portrait.Release();
            Object.Destroy(wide); Object.Destroy(portrait);
            game.GetComponent<CardDock>().SetExpanded(true, true);
            Canvas.ForceUpdateCanvases();
        }
    }

    [UnityTest]
    public IEnumerator DisablingOrReloadingDuringASwapLeavesNoStuckMovementOrMissingReferences()
    {
        Drag(research.Card, school.Card.Rect.position);
        yield return new WaitForSecondsRealtime(.08f);
        game.Grid.enabled = false;
        Assert.That(game.Grid.IsAnimating, Is.False);
        AssertGridCardsAtTheirDestinations();
        game.Grid.enabled = true;
        Drag(research.Card, Station(CardKind.Factory).Card.Rect.position);
        Assert.That(game.Grid.IsAnimating, Is.True);
        yield return SceneManager.LoadSceneAsync("2DCardScene", LoadSceneMode.Single);
        yield return null;
        game = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(game.Grid.IsAnimating, Is.False);
        AssertUniqueGridOccupancy();
        AssertGridCardsAtTheirDestinations();
        LogAssert.NoUnexpectedReceived();
    }

    private PointerEventData BeginGridDrag(DraggableCard card)
    {
        var canvas = card.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var pointer = new PointerEventData(EventSystem.current) {
            pointerId = -1, button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(camera, card.Rect.position),
            pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() }
        };
        Assert.That(ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler), Is.True);
        Assert.That(ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.beginDragHandler), Is.True);
        Assert.That(card.IsDragging, Is.True);
        return pointer;
    }

    private static void MoveGridDrag(DraggableCard card, PointerEventData pointer, Vector3 destination)
    {
        pointer.position = RectTransformUtility.WorldToScreenPoint(pointer.pressEventCamera, destination);
        Assert.That(ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler), Is.True);
    }

    private static void EndGridDrag(DraggableCard card, PointerEventData pointer)
    {
        Assert.That(ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler), Is.True);
        Assert.That(ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler), Is.True);
    }

    private void AssertUniqueGridOccupancy()
    {
        var occupied = Enumerable.Range(0, CardGrid.CellCount).Select(game.Grid.GetOccupant).Where(card => card != null).ToArray();
        Assert.That(occupied.Length, Is.EqualTo(7));
        Assert.That(occupied.Distinct().Count(), Is.EqualTo(7));
        CollectionAssert.AreEquivalent(game.Cards.Where(card => card.IsBuilding), occupied);
    }

    private void AssertGridCardsAtTheirDestinations()
    {
        for (int i = 0; i < CardGrid.CellCount; i++)
        {
            var card = game.Grid.GetOccupant(i);
            if (card == null) continue;
            Assert.That(card.Rect.parent, Is.SameAs(game.Grid.GetCell(i)));
            Assert.That(Vector3.Distance(card.Rect.position, game.Grid.GetCellWorldPosition(i)), Is.LessThan(.05f), card.name);
        }
    }
}
