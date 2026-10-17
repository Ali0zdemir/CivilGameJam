using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    private static Transform[] ApartmentIsolators(ApartmentUpgradeModel model) => new[]
    {
        ControlField<Transform>(model, "normalIsolators"), ControlField<Transform>(model, "elevatedIsolators")
    };

    [UnityTest]
    public IEnumerator ApartmentSeismicThenElevatorKeepsSupportsAndStationaryGround()
    {
        var link = CityHouseLinks()[0];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        var ground = link.Building.Find("Apartment_Ground");
        Vector3 groundPosition = ground.position;
        Vector3 buildingPosition = link.Building.position;
        var isolators = ApartmentIsolators(model);
        Vector3[] supportPositions = isolators.Select(t => t.position).ToArray();
        var railProduct = ProduceCityProducts(UpgradeKind.RailSystem)[0];
        var elevatorProduct = ProduceCityProducts(UpgradeKind.Breakwater)[0];
        var ui = UIIds(game);
        Assert.That(model.IsElevated || model.HasSeismic, Is.False);
        Assert.That(link.House.TryInstall(railProduct), Is.True);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(link.RailSystem.CurrentLift, Is.GreaterThan(0f).And.LessThan(.2f));
        Assert.That(ground.position, Is.EqualTo(groundPosition));
        yield return new WaitForSecondsRealtime(1.2f);
        var puffs = link.RailSystem.GetComponentsInChildren<ParticleSystem>();
        Assert.That(puffs.Length, Is.EqualTo(6), "Keep the existing dust burst on each of the six apartments.");
        Assert.That(puffs.All(puff => puff.particleCount > 0), Is.True);
        Assert.That(model.HasSeismic, Is.True);
        Assert.That(isolators[0].gameObject.activeInHierarchy, Is.True);
        Assert.That(isolators[1].gameObject.activeInHierarchy, Is.False);
        Assert.That(link.House.TryInstall(elevatorProduct), Is.True);
        Assert.That(model.IsElevated, Is.False, "Switch models at the jump apex, not at the instant of the drop.");
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(model.IsElevated && model.IsTransitioning && model.HasSeismic, Is.True);
        yield return new WaitForSecondsRealtime(.55f);
        Assert.That(model.IsTransitioning, Is.False);
        Assert.That(isolators[0].gameObject.activeInHierarchy, Is.False);
        Assert.That(isolators[1].gameObject.activeInHierarchy, Is.True);
        Assert.That(Vector3.Distance(isolators[1].position, supportPositions[1]), Is.LessThan(.0001f));
        Assert.That(model.MotionRoot.localPosition.sqrMagnitude, Is.LessThan(.00001f), "Elevated feet retain their authored height when seismic protection is installed.");
        Assert.That(ground.position, Is.EqualTo(groundPosition));
        Assert.That(link.Building.position, Is.EqualTo(buildingPosition));
        Assert.That(link.Building.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Rail_System" || t.name == "Open_Ground_Floor"), Is.False);
        Assert.That(UIIds(game), Is.EqualTo(ui));
        string directory = Path.GetFullPath("Library/CardGameTestResults/ApartmentVariants");
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "seismic-elevated.png"));
        link.House.Release(UpgradeKind.Breakwater, false);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(model.IsElevated, Is.False);
        Assert.That(model.HasSeismic && isolators[0].gameObject.activeInHierarchy, Is.True);
        Assert.That(Vector3.Distance(isolators[0].position, supportPositions[0]), Is.LessThan(.0001f));
        link.House.Release(UpgradeKind.RailSystem, false);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(model.HasSeismic, Is.False);
        Assert.That(model.MotionRoot.localPosition.sqrMagnitude, Is.LessThan(.00001f));
    }

    [UnityTest]
    public IEnumerator ApartmentElevatorThenSeismicAndIndependentRemovalKeepFireProtection()
    {
        var link = CityHouseLinks()[1];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        Assert.That(link.House.TryInstall(ProduceCityProducts(UpgradeKind.Breakwater)[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(model.IsElevated, Is.True);
        Assert.That(ApartmentIsolators(model).Any(t => t.gameObject.activeInHierarchy), Is.False);
        Assert.That(link.House.TryInstall(ProduceCityProducts(UpgradeKind.Fire)[0]), Is.True);
        Assert.That(link.House.TryInstall(ProduceCityProducts(UpgradeKind.RailSystem)[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.45f);
        Assert.That(model.HasSeismic && model.IsElevated, Is.True);
        Assert.That(model.MotionRoot.localPosition.sqrMagnitude, Is.LessThan(.00001f));
        link.House.Release(UpgradeKind.RailSystem, false);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(model.IsElevated && link.FireProtection.IsInstalled, Is.True);
        Assert.That(ApartmentIsolators(model).Any(t => t.gameObject.activeInHierarchy), Is.False);
        var geometry = link.Building.GetComponent<BuildingModelGeometry>();
        foreach (var part in geometry.Parts)
        {
            var roof = part.SprinklerAnchor;
            Assert.That(part.TryProjectRoof(roof, out var surface, out _), Is.True);
            Assert.That(surface.y, Is.EqualTo(roof.y).Within(.08f));
        }
        link.House.Release(UpgradeKind.Breakwater, false);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(model.IsElevated || model.HasSeismic, Is.False);
        Assert.That(link.FireProtection.IsInstalled, Is.True);
        var fire = Object.FindFirstObjectByType<FireTestController>();
        fire.StartFire();
        AdvanceMonsterFireTo(fire, fire.SprinklerStartTime + .05f);
        yield return null;
        Assert.That(link.FireProtection.IsSpraying, Is.True);
        fire.StopFire();
        link.House.Release(UpgradeKind.Fire, false);
        Assert.That(link.FireProtection.IsInstalled, Is.False);
    }

    [UnityTest]
    public IEnumerator ReversingApartmentElevationDuringInstallationNeverRestoresARemovedUpgrade()
    {
        var link = CityHouseLinks()[2];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        var product = ProduceCityProducts(UpgradeKind.Breakwater)[0];
        link.House.TryInstall(product);
        yield return new WaitForSecondsRealtime(.3f);
        Vector3 before = model.MotionRoot.position;
        link.House.Release(UpgradeKind.Breakwater, false);
        Assert.That(model.MotionRoot.position, Is.EqualTo(before));
        yield return new WaitForSecondsRealtime(.2f);
        link.House.TryInstall(product);
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(model.IsElevated, Is.True);
        link.House.Release(UpgradeKind.Breakwater, false);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(model.IsElevated || model.IsTransitioning || link.FloodProtection.IsInstalled, Is.False);
        Assert.That(model.MotionRoot.localPosition.sqrMagnitude, Is.LessThan(.00001f));
    }

    [UnityTest]
    public IEnumerator ElevatorWaitsForCameraFocusAndTheCameraWaitsForItsLanding()
    {
        var link = CityHouseLinks()[3];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        var presentation = Object.FindFirstObjectByType<HouseUpgradePresentation>();
        var dock = game.GetComponent<CardDock>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        presentation.enabled = dock.enabled = true;
        dock.SetExpanded(true, true);
        link.House.TryInstall(ProduceCityProducts(UpgradeKind.Breakwater)[0]);
        float timeout = Time.realtimeSinceStartup + 5f;
        while (!model.IsTransitioning && Time.realtimeSinceStartup < timeout)
        {
            Assert.That(model.IsElevated, Is.False);
            yield return null;
        }
        Assert.That(model.IsTransitioning && navigation.IsFocusReady, Is.True);
        while (model.IsTransitioning && Time.realtimeSinceStartup < timeout)
        {
            Assert.That(navigation.IsFocusReady, Is.True);
            yield return null;
        }
        Assert.That(model.IsElevated, Is.True);
        yield return FinishHousePresentation(presentation);
        Assert.That(dock.IsCityPresentationActive, Is.False);
    }
}
