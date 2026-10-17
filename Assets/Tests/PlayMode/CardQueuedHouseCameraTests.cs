using System.Collections;
using System.IO;
using CivilGameJam;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator SuccessiveProductDragsVisitEachHouseBeforeOneSmoothReturn()
    {
        var links = CityHouseLinks();
        var products = ProduceCityProducts(UpgradeKind.RailSystem, 2);
        var presentation = Object.FindFirstObjectByType<HouseUpgradePresentation>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var camera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
        var dock = game.GetComponent<CardDock>();
        presentation.enabled = dock.enabled = true;
        var homePan = navigation.PanOffset;
        float homeZoom = camera.orthographicSize;
        var savedUI = UIIds(game);

        Drag(products[0], links[0].House.Card.Rect.position);
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!links[0].RailSystem.IsInstalled && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(links[0].RailSystem.IsInstalled, Is.True);
        var pointer = BeginGridDrag(products[1]);
        // Keep the next item in hand beyond the first landing AND its post-animation hold.
        deadline = Time.realtimeSinceStartup + 3.5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            Assert.That(presentation.IsPresenting && navigation.IsPresenting, Is.True);
            Assert.That(ControlField<bool>(navigation, "returningFromPresentation"), Is.False,
                "Dragging another system must keep the tour open rather than return to the city overview.");
            yield return null;
        }
        Assert.That(links[0].RailSystem.IsTransitioning, Is.False);
        Assert.That(links[1].RailSystem.IsInstalled, Is.False);
        MoveGridDrag(products[1], pointer, links[1].House.Card.Rect.position);
        EndGridDrag(products[1], pointer);
        Drag(products[2], links[2].House.Card.Rect.position);
        Assert.That(links[1].House.GetInstalled(UpgradeKind.RailSystem), Is.SameAs(products[1]));
        Assert.That(links[2].House.GetInstalled(UpgradeKind.RailSystem), Is.SameAs(products[2]));

        int installed = 1;
        bool sawTravelBetweenHouses = false;
        Vector2 firstFocus = navigation.PanOffset;
        deadline = Time.realtimeSinceStartup + 12f;
        while (presentation.IsPresenting && Time.realtimeSinceStartup < deadline)
        {
            for (int i = installed; i < 3; i++)
            {
                if (!links[i].RailSystem.IsInstalled) break;
                Assert.That(installed, Is.EqualTo(i), "Accepted upgrades must run in drop order.");
                Assert.That(navigation.IsFocusReady, Is.True);
                Assert.That(links[i - 1].RailSystem.IsTransitioning, Is.False);
                AssertHouseCloseup(navigation, links[i].Building, true);
                installed++;
            }
            if (installed < 3)
            {
                Assert.That(ControlField<bool>(navigation, "returningFromPresentation"), Is.False);
                if (Vector2.Distance(navigation.PanOffset, firstFocus) > .01f && !navigation.IsFocusReady)
                    sawTravelBetweenHouses = true;
            }
            if (ControlField<bool>(navigation, "returningFromPresentation"))
            {
                Assert.That(installed, Is.EqualTo(3));
                Assert.That(links[2].RailSystem.IsTransitioning, Is.False);
            }
            yield return null;
        }
        Assert.That(presentation.IsPresenting, Is.False, "The complete tour must finish.");
        Assert.That(installed, Is.EqualTo(3));
        Assert.That(sawTravelBetweenHouses, Is.True);
        Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
        Assert.That(camera.orthographicSize, Is.EqualTo(homeZoom).Within(.001f));
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }

    [UnityTest]
    public IEnumerator ProductPickedUpDuringReturnHoldsThatPoseAndRetainsTheOriginalHome()
    {
        yield return LateHouseProductDrag(true);
    }

    [UnityTest]
    public IEnumerator UnplacedProductDuringReturnResumesTheSameSmoothReturn()
    {
        yield return LateHouseProductDrag(false);
    }

    private IEnumerator LateHouseProductDrag(bool install)
    {
        var links = CityHouseLinks();
        var products = ProduceCityProducts(UpgradeKind.Fire);
        var presentation = Object.FindFirstObjectByType<HouseUpgradePresentation>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var camera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
        var dock = game.GetComponent<CardDock>();
        presentation.enabled = dock.enabled = true;
        PresentationField(presentation, "holdSeconds", .1f);
        var homePan = navigation.PanOffset;
        float homeZoom = camera.orthographicSize;
        Drag(products[0], links[0].House.Card.Rect.position);
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!ControlField<bool>(navigation, "returningFromPresentation") && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(ControlField<bool>(navigation, "returningFromPresentation"), Is.True);
        yield return new WaitForSecondsRealtime(.2f);
        var pointer = BeginGridDrag(products[1]);
        yield return null;
        yield return null;
        var pausedPan = navigation.PanOffset;
        float pausedZoom = camera.orthographicSize;
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(navigation.IsPresenting, Is.True);
        Assert.That(ControlField<bool>(navigation, "returningFromPresentation"), Is.False);
        Assert.That(Vector2.Distance(navigation.PanOffset, pausedPan), Is.LessThan(.001f));
        Assert.That(camera.orthographicSize, Is.EqualTo(pausedZoom).Within(.001f));
        var destination = install ? links[3].House.Card.Rect.position : game.Grid.GetCellWorldPosition(InitiallyEmptyCell);
        MoveGridDrag(products[1], pointer, destination);
        EndGridDrag(products[1], pointer);
        yield return FinishHousePresentation(presentation);
        Assert.That(links[3].FireProtection.IsInstalled, Is.EqualTo(install));
        if (install) Assert.That(products[1].InstalledIn, Is.SameAs(links[3].House));
        else Assert.That(products[1].InstalledIn, Is.Null);
        Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
        Assert.That(camera.orthographicSize, Is.EqualTo(homeZoom).Within(.001f));
        Assert.That(dock.IsCityPresentationActive, Is.False);
    }
}
