using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    private static MeshFilter[] TravellingGrounds(Transform building)
    {
        var apartment = building.GetComponent<ApartmentUpgradeModel>();
        return building.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.name.Contains("Ground") &&
            (apartment == null || apartment.MotionRoot == null || !filter.transform.IsChildOf(apartment.MotionRoot))).ToArray();
    }

    private void AssertCityAtCardCells(CardCityGridPlacement placement)
    {
        var cards = game.Cards.Where(card => card.IsBuilding).ToArray();
        var roots = cards.Select(placement.BuildingFor).ToArray();
        Assert.That(roots.Distinct().Count(), Is.EqualTo(cards.Length));
        for (var i = 0; i < cards.Length; i++)
        {
            var anchor = placement.CellAnchor(game.Grid.IndexOf(cards[i]));
            Assert.That(Vector2.Distance(new Vector2(roots[i].position.x, roots[i].position.z), new Vector2(anchor.position.x, anchor.position.z)), Is.LessThan(.0001f), cards[i].name);
        }
    }

    private static string PlacementCapture(string name)
    {
        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardCityPlacementWork/Previews"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, name + ".png");
    }

    [UnityTest]
    public IEnumerator CityPlacementMovesBuildingsWithTheirGroundIntoTheEmptyCellWithoutMovingTheMap()
    {
        var placement = game.GetComponent<CardCityGridPlacement>();
        var card = research.Card;
        var root = placement.BuildingFor(card);
        var from = root.position;
        var rotation = root.rotation; var scale = root.localScale;
        var identities = root.GetComponentsInChildren<Transform>(true).Select(part => part.GetInstanceID()).ToArray();
        var pads = TravellingGrounds(root);
        Assert.That(pads, Is.Not.Empty);
        var padMeshes = pads.Select(pad => pad.sharedMesh).ToArray();
        var padCenters = pads.Select(pad => root.InverseTransformPoint(pad.GetComponent<MeshRenderer>().bounds.center)).ToArray();
        var ground = Object.FindFirstObjectByType<TsunamiTestController>().GroundSurface;
        var groundMatrix = ground.localToWorldMatrix;
        AssertCityAtCardCells(placement);
        var pointer = BeginGridDrag(card);
        MoveGridDrag(card, pointer, game.Grid.GetCellWorldPosition(InitiallyEmptyCell));
        Assert.That(root.position, Is.EqualTo(from), "Dragging only previews a placement; the accepted drop changes the city.");
        EndGridDrag(card, pointer);
        Assert.That(game.Grid.IndexOf(card), Is.EqualTo(InitiallyEmptyCell));
        Assert.That(Vector3.Distance(card.Rect.position, game.Grid.GetCellWorldPosition(InitiallyEmptyCell)), Is.LessThan(.001f),
            "Keep the current 2D snap into an empty cell while its city building animates.");
        Assert.That(game.Grid.IsMoving(card), Is.False, "Do not add a different 2D empty-slot movement.");
        Assert.That(root.position, Is.EqualTo(from), "The animation starts at the visible position without a cut.");
        Assert.That(placement.IsAnimating && game.HasActiveCardAnimation, Is.True);
        InvokeCity(placement, "AdvancePlacement", .25f);
        for (var i = 0; i < pads.Length; i++)
        {
            var renderer = pads[i].GetComponent<MeshRenderer>();
            Assert.That(renderer.enabled && renderer.gameObject.activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(renderer.bounds.center, root.TransformPoint(padCenters[i])), Is.LessThan(.0001f), "The visible ground follows the same curved movement as its building.");
        }
        Assert.That(Vector3.Distance(root.position, from), Is.GreaterThan(.05f));
        Assert.That(Vector2.Distance(new Vector2(root.position.x, root.position.z), new Vector2(placement.CellAnchor(InitiallyEmptyCell).position.x, placement.CellAnchor(InitiallyEmptyCell).position.z)), Is.GreaterThan(.05f));
        CaptureCityWorldFrame(PlacementCapture("01-empty-cell-moving"));
        InvokeCity(placement, "AdvancePlacement", 2f);
        AssertCityAtCardCells(placement);
        Assert.That(root.rotation, Is.EqualTo(rotation)); Assert.That(root.localScale, Is.EqualTo(scale));
        Assert.That(root.GetComponentsInChildren<Transform>(true).Select(part => part.GetInstanceID()), Is.EqualTo(identities));
        Assert.That(pads.Select(pad => pad.sharedMesh), Is.EqualTo(padMeshes));
        for (var i = 0; i < pads.Length; i++)
            Assert.That(Vector3.Distance(pads[i].GetComponent<MeshRenderer>().bounds.center, root.TransformPoint(padCenters[i])), Is.LessThan(.0001f));
        Assert.That(ground.localToWorldMatrix, Is.EqualTo(groundMatrix));
        yield return new WaitForSecondsRealtime(.4f);
        yield return CaptureCanvas(PlacementCapture("02-empty-cell-finished-game-view"));
    }

    [UnityTest]
    public IEnumerator CityPlacementSwapsOppositeArcsAndKeepsRailsColumnsAndSprinklersWithTheirHouse()
    {
        var placement = game.GetComponent<CardCityGridPlacement>();
        var houses = CityHouseLinks(); var first = houses[0]; var second = houses[1];
        foreach (var kind in new[] { UpgradeKind.RailSystem, UpgradeKind.Breakwater, UpgradeKind.Fire })
            Assert.That(first.House.TryInstall(ProduceCityProducts(kind)[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.6f);
        var a = first.Building; var b = second.Building;
        var model = a.GetComponent<ApartmentUpgradeModel>();
        var head = model.SprinklerHead(0);
        var headOffset = a.InverseTransformPoint(head.position);
        var from = a.position; var otherFrom = b.position;
        var pads = new[] { a, b }.SelectMany(building => TravellingGrounds(building)
            .Select(filter => new { Building = building, Renderer = filter.GetComponent<MeshRenderer>(), Center = building.InverseTransformPoint(filter.GetComponent<MeshRenderer>().bounds.center) })).ToArray();
        Assert.That(pads.Length, Is.GreaterThanOrEqualTo(2));
        var source = game.Grid.IndexOf(first.House.Card); var target = game.Grid.IndexOf(second.House.Card);
        var pointer = BeginGridDrag(first.House.Card);
        MoveGridDrag(first.House.Card, pointer, second.House.Card.Rect.position);
        EndGridDrag(first.House.Card, pointer);
        Assert.That(a.position, Is.EqualTo(from)); Assert.That(b.position, Is.EqualTo(otherFrom));
        InvokeCity(placement, "AdvancePlacement", .3f);
        foreach (var pad in pads)
            Assert.That(Vector3.Distance(pad.Renderer.bounds.center, pad.Building.TransformPoint(pad.Center)), Is.LessThan(.0001f), "Each exchanged building keeps its own original ground.");
        if (pads.Length == 2)
            Assert.That(pads[0].Renderer.bounds.Intersects(pads[1].Renderer.bounds), Is.False, "The restored ground plots also pass beside one another without overlapping.");
        var side = Vector3.Cross(Vector3.up, (otherFrom - from).normalized);
        var center = (from + otherFrom) * .5f;
        Assert.That(Vector3.Dot(a.position - center, side) * Vector3.Dot(b.position - center, side), Is.LessThan(0f), "The swapped buildings pass on opposite sides.");
        Assert.That(Vector3.Distance(head.position, a.TransformPoint(headOffset)), Is.LessThan(.0001f));
        Assert.That(model.IsElevated && model.HasSeismic && model.HasFireProtection, Is.True);
        CaptureCityWorldFrame(PlacementCapture("03-upgraded-house-swap-middle"));
        InvokeCity(placement, "AdvancePlacement", 1f);
        Assert.That(game.Grid.IndexOf(first.House.Card), Is.EqualTo(target));
        Assert.That(game.Grid.IndexOf(second.House.Card), Is.EqualTo(source));
        Assert.That(first.Building, Is.SameAs(a)); Assert.That(second.Building, Is.SameAs(b));
        AssertCityAtCardCells(placement);
        var destination = a.position;
        foreach (var kind in new[] { UpgradeKind.RailSystem, UpgradeKind.Breakwater, UpgradeKind.Fire }) first.House.Release(kind, false);
        yield return new WaitForSecondsRealtime(1.6f);
        Assert.That(a.position, Is.EqualTo(destination), "Removing upgrades at the new cell must not restore the former city position.");
        Assert.That(model.IsElevated || model.HasSeismic || model.HasFireProtection, Is.False);
        AssertCityAtCardCells(placement);
    }

    [UnityTest]
    public IEnumerator CityPlacementRetargetsWithoutJumpsAndUsesUnscaledTimeThroughRepeatedSwaps()
    {
        var placement = game.GetComponent<CardCityGridPlacement>();
        var card = research.Card; var root = placement.BuildingFor(card);
        Assert.That(game.Grid.TryPlaceOrSwap(card, InitiallyEmptyCell), Is.True);
        InvokeCity(placement, "AdvancePlacement", .2f);
        var current = root.position;
        Assert.That(game.Grid.TryPlaceOrSwap(card, game.Grid.IndexOf(school.Card)), Is.True);
        Assert.That(root.position, Is.EqualTo(current), "Retarget from the current animated pose without teleporting.");
        var oldTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(placement.IsAnimating || game.Grid.IsAnimating, Is.False);
            AssertCityAtCardCells(placement);
            for (var i = 0; i < 3; i++)
            {
                Assert.That(game.Grid.TryPlaceOrSwap(card, game.Grid.IndexOf(school.Card)), Is.True);
                yield return new WaitForSecondsRealtime(.8f);
                AssertCityAtCardCells(placement);
            }
        }
        finally { Time.timeScale = oldTimeScale; }
    }

    [UnityTest]
    public IEnumerator CityPlacementSettlesBeforeDisastersCancelsDragsAndLocksEveryEventUntilCameraReturn()
    {
        var placement = game.GetComponent<CardCityGridPlacement>();
        var rig = new SequencePreview();
        var card = research.Card;
        Assert.That(game.Grid.TryPlaceOrSwap(card, InitiallyEmptyCell), Is.True);
        InvokeCity(placement, "AdvancePlacement", .15f);
        var pointer = BeginGridDrag(card);
        MoveGridDrag(card, pointer, school.Card.Rect.position);
        rig.Quake.StartEarthquake();
        Assert.That(card.IsDragging || game.IsDraggingAnyCard || placement.IsAnimating, Is.False, "The existing placement is settled before quake rest poses are sampled.");
        Assert.That(game.Grid.IndexOf(card), Is.EqualTo(InitiallyEmptyCell));
        AssertCityAtCardCells(placement);
        EndGridDrag(card, pointer);
        Assert.That(game.Grid.IndexOf(card), Is.EqualTo(InitiallyEmptyCell));
        rig.Quake.StopEarthquake();
        InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
        for (var kind = 0; kind < 6; kind++)
        {
            if (kind == 0) rig.Tsunami.StartTsunami();
            else if (kind == 1) rig.Quake.StartEarthquake();
            else if (kind == 2) rig.Fire.StartFire();
            else { rig.Sequence.SelectSequence(kind - 2); rig.Sequence.StartSelectedSequence(); }
            Assert.That(placement.IsPlacementLocked && game.Grid.IsPlacementLocked, Is.True, "Immediate lock for individual tests and every sequence.");
            var index = game.Grid.IndexOf(card);
            Assert.That(game.Grid.TryPlaceOrSwap(card, game.Grid.IndexOf(school.Card)), Is.False);
            Assert.That(game.Grid.TryPlace(card, 0), Is.EqualTo(index == 0));
            pointer = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left };
            card.OnPointerDown(pointer);
            card.OnBeginDrag(pointer);
            Assert.That(card.IsDragging, Is.False);
            Assert.That(game.Grid.IndexOf(card), Is.EqualTo(index));
            rig.Sequence.StopSequence(); rig.Tsunami.StopTsunami(); rig.Quake.StopEarthquake(); rig.Fire.StopFire(); rig.Walk.StopAfterEventWalk();
            InvokeCity(rig.Navigation, "AdvanceNavigation", 3f);
            Assert.That(placement.IsPlacementLocked, Is.False, "Placement unlocks when the event and camera return finish.");
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator CityPlacementRefreshesTsunamiImpactsAndSurvivesDisableDuringMotion()
    {
        var placement = game.GetComponent<CardCityGridPlacement>();
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var link = CityHouseLinks()[0];
        Assert.That(game.Grid.TryPlaceOrSwap(link.House.Card, game.Grid.IndexOf(research.Card)), Is.True);
        InvokeCity(placement, "AdvancePlacement", .2f);
        tsunami.StartTsunami();
        AssertCityAtCardCells(placement);
        var sites = ControlField<System.Collections.IList>(tsunami, "impactSites");
        var found = false;
        foreach (var site in sites)
        {
            if ((Transform)site.GetType().GetField("Building").GetValue(site) != link.Building) continue;
            var bounds = (Bounds)site.GetType().GetField("LocalBounds").GetValue(site);
            var expected = tsunami.transform.InverseTransformPoint(link.Building.GetComponent<BuildingModelGeometry>().WorldBounds.center);
            Assert.That(Vector3.Distance(bounds.center, expected), Is.LessThan(.001f), "Wave impacts use the new plot rather than cached startup positions.");
            found = true;
        }
        Assert.That(found, Is.True);
        tsunami.StopTsunami();
        InvokeCity(Object.FindFirstObjectByType<CityCameraController>(), "AdvanceNavigation", 3f);
        Assert.That(game.Grid.TryPlaceOrSwap(link.House.Card, InitiallyEmptyCell), Is.True);
        InvokeCity(placement, "AdvancePlacement", .2f);
        game.Grid.enabled = false;
        Assert.That(placement.IsAnimating || game.Grid.IsAnimating, Is.False);
        AssertCityAtCardCells(placement);
        game.Grid.enabled = true;
        Assert.That(game.Grid.TryPlaceOrSwap(link.House.Card, game.Grid.IndexOf(school.Card)), Is.True);
        placement.enabled = false;
        Assert.That(placement.IsAnimating, Is.False);
        placement.enabled = true;
        AssertCityAtCardCells(placement);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CityPlacementKeepsTheChosenLayoutAfterReadyDestructionAndTheNextTurnRebuild()
    {
        var placement = game.GetComponent<CardCityGridPlacement>();
        var round = game.GetComponent<EarthquakeRoundController>();
        var houses = CityHouseLinks();
        var roots = houses.Select(house => house.Building).ToArray();
        Assert.That(game.Grid.TryPlaceOrSwap(houses[0].House.Card, game.Grid.IndexOf(research.Card)), Is.True);
        Assert.That(round.CanReady, Is.False, "READY waits for both card movement and city movement.");
        yield return new WaitForSecondsRealtime(.8f);
        AssertCityAtCardCells(placement);
        var positions = roots.Select(root => root.position).ToArray();
        var cells = houses.Select(house => game.Grid.IndexOf(house.House.Card)).ToArray();
        var rig = new SequencePreview();
        Assert.That(round.CanReady, Is.True);
        round.Ready();
        Assert.That(placement.IsPlacementLocked, Is.True);
        for (var frame = 0; frame < 2400 && round.RoundNumber == 1; frame++)
        {
            rig.For(.05f);
            round.AdvanceRound(.05f);
        }
        Assert.That(round.RoundNumber, Is.EqualTo(2));
        Assert.That(houses.All(house => house.House.IsDestroyed), Is.True,
            "The current game keeps ruins behind the report until it is dismissed.");
        Assert.That(houses.Select(house => game.Grid.IndexOf(house.House.Card)), Is.EqualTo(cells));
        Assert.That(roots.Select(root => root.position), Is.EqualTo(positions), "Rebuilding earthquake damage restores the models at their chosen cells.");
        AssertCityAtCardCells(placement);
        rig.For(3f);
        Assert.That(rig.Sequence.Results.IsVisible, Is.True);
        rig.Sequence.Results.Retry();
        Assert.That(houses.All(house => !house.House.IsDestroyed), Is.True);
        Assert.That(roots.Select(root => root.position), Is.EqualTo(positions));
        AssertCityAtCardCells(placement);
        Assert.That(placement.IsPlacementLocked, Is.False);
    }
}
