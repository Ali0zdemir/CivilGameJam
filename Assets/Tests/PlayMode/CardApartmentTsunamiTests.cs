using System.Collections;
using System.Linq;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator TsunamiCollapsesOnlyReachedApartmentsWithoutElevatorAndResetKeepsTheirUpgrades()
    {
        var links = CityHouseLinks();
        Assert.That(links[1].House.TryInstall(ProduceCityProducts(UpgradeKind.Breakwater)[0]), Is.True);
        Assert.That(links[0].House.TryInstall(ProduceCityProducts(UpgradeKind.RailSystem)[0]), Is.True);
        Assert.That(links[0].House.TryInstall(ProduceCityProducts(UpgradeKind.Fire)[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.45f);
        var collapses = links.Select(link => link.Building.GetComponent<ApartmentTsunamiCollapse>()).ToArray();
        Assert.That(collapses.All(collapse => collapse != null), Is.True);
        var normal = links[0].Building.Find("Apartment_Motion/Apartments_Normal");
        var bodies = Enumerable.Range(1, 6).Select(i => normal.Find("Apartment" + i)).ToArray();
        var positions = bodies.Select(body => body.localPosition).ToArray();
        var rotations = bodies.Select(body => body.localRotation).ToArray();
        var head = normal.Find("Sprinkler1");
        var headInBody = bodies[0].InverseTransformPoint(head.position);
        var ground = links[0].Building.Find("Apartment_Ground");
        Vector3 groundPosition = ground.position;
        var ui = UIIds(game);
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        PresentationField(tsunami, "sprayAmount", 0f);
        tsunami.StartTsunami();
        InvokeCity(tsunami, "AdvanceTsunami", .1f);
        Assert.That(collapses.Any(collapse => collapse.HasCollapsed), Is.False, "Wait for actual flood contact.");
        for (int step = 0; step < 45 && tsunami.IsRunning; step++) InvokeCity(tsunami, "AdvanceTsunami", .1f);
        Assert.That(collapses[1].HasCollapsed, Is.False, "Elevator protection survives the tsunami.");
        foreach (int i in new[] { 0, 2, 3 }) Assert.That(collapses[i].HasCollapsed, Is.True, "Reached apartment " + i);
        for (int i = 0; i < 6; i++)
            Assert.That(Vector3.Distance(bodies[i].localPosition, positions[i]), Is.LessThan(.0001f), "The animation starts at the displayed pose.");
        InvokeCity(collapses[0], "AdvanceCollapse", collapses[0].Duration * .6f);
        Assert.That(collapses[0].IsPlaying, Is.True);
        Assert.That(bodies.Any(body => Quaternion.Angle(body.localRotation, Quaternion.identity) > 15f), Is.True);
        Assert.That(Vector3.Distance(head.position, bodies[0].TransformPoint(headInBody)), Is.LessThan(.001f), "The native sprinkler follows its falling apartment.");
        CaptureAuthoredApartmentSprinklers(links[0].Building, "tsunami-collapse-moving");
        InvokeCity(collapses[0], "AdvanceCollapse", 10f);
        Assert.That(collapses[0].IsPlaying, Is.False);
        Assert.That(collapses[0].HasCollapsed, Is.True);
        CaptureAuthoredApartmentSprinklers(links[0].Building, "tsunami-collapse-finished");
        InvokeCity(tsunami, "AdvanceTsunami", 100f);
        Assert.That(collapses[0].HasCollapsed, Is.True, "Keep the final pose after the water drains.");
        Assert.That(ground.position, Is.EqualTo(groundPosition));
        tsunami.StopTsunami();
        Assert.That(collapses.Any(collapse => collapse.HasCollapsed), Is.False);
        for (int i = 0; i < 6; i++)
        {
            Assert.That(bodies[i].localPosition, Is.EqualTo(positions[i]));
            Assert.That(bodies[i].localRotation, Is.EqualTo(rotations[i]));
        }
        var model = links[0].Building.GetComponent<ApartmentUpgradeModel>();
        Assert.That(model.HasSeismic && model.HasFireProtection, Is.True);
        Assert.That(links[1].Building.GetComponent<ApartmentUpgradeModel>().IsElevated, Is.True);
        Assert.That(UIIds(game), Is.EqualTo(ui));
        tsunami.StartTsunami();
        Assert.That(collapses.Any(collapse => collapse.HasCollapsed), Is.False);
        tsunami.StopTsunami();
    }

    [UnityTest]
    public IEnumerator ElevatorInstallationProtectsDuringItsJumpAndRemovingItAllowsAuthoredCollapse()
    {
        var link = CityHouseLinks()[0];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        var collapse = link.Building.GetComponent<ApartmentTsunamiCollapse>();
        var elevator = ProduceCityProducts(UpgradeKind.Breakwater)[0];
        link.House.TryInstall(elevator);
        Assert.That(model.IsElevatorProtected, Is.True);
        Assert.That(model.IsElevated, Is.False);
        Assert.That(collapse.TryCollapse(), Is.False);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(collapse.TryCollapse(), Is.False);
        link.House.Release(UpgradeKind.Breakwater, false);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(model.IsElevatorProtected, Is.False);
        Assert.That(collapse.TryCollapse(), Is.True);
        Assert.That(collapse.TryCollapse(), Is.False, "Contact cannot repeatedly restart the animation.");
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(collapse.Progress, Is.GreaterThan(.05f).And.LessThan(1f));
        Assert.That(link.House.TryInstall(elevator), Is.True);
        yield return null;
        yield return null;
        Assert.That(collapse.HasCollapsed, Is.False, "An accepted Elevator installation releases the normal-model animation.");
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(model.IsElevated, Is.True);
        link.House.Release(UpgradeKind.Breakwater, false);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(collapse.TryCollapse(), Is.True);
        collapse.enabled = false;
        Assert.That(collapse.HasCollapsed, Is.False);
    }
}
