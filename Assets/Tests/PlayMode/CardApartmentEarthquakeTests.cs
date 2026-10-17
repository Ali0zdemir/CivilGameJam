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
    private static Transform[] EarthquakeBodies(Transform building)
    {
        var model = building.GetComponent<ApartmentUpgradeModel>();
        var root = building.Find("Apartment_Motion/" + (model.IsElevated ? "Apartments_Elevated" : "Apartments_Normal"));
        return Enumerable.Range(1, 6).Select(i => root.Find("Apartment" + i + (model.IsElevated ? "(Elevated)" : ""))).ToArray();
    }

    [UnityTest]
    public IEnumerator ApartmentEarthquakeStaggersMoreThanTwoEligibleHousesAndStillFinishesOnTime()
    {
        var links = CityHouseLinks();
        var animations = links.Select(link => link.Building.GetComponent<ApartmentEarthquakeAnimation>()).ToArray();
        var bodies = links.Select(link => EarthquakeBodies(link.Building)).ToArray();
        var rest = bodies.Select(group => group.Select(body => body.localPosition).ToArray()).ToArray();
        var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        foreach (var count in new[] { 4, 3, 2, 1 })
        {
            for (var i = 0; i < animations.Length; i++) animations[i].enabled = i < count;
            var active = animations.Take(count).ToArray();
            quake.StartEarthquake();
            var duration = quake.TestDuration;
            Assert.That(active.All(animation => animation.IsRunning && animation.Progress == 0f), Is.True);
            InvokeCity(quake, "AdvanceShake", .1f);
            Assert.That(active.Count(animation => animation.Progress > 0f), Is.EqualTo(count > 2 ? 1 : count),
                "Only the first house starts immediately in a larger group; one or two houses still start together.");
            InvokeCity(quake, "AdvanceShake", .4f);
            if (count > 2)
            {
                Assert.That(active.Count(animation => animation.Progress > 0f), Is.InRange(2, count - 1),
                    "The remaining houses start over successive short delays.");
                foreach (var index in Enumerable.Range(0, count).Where(i => animations[i].Progress == 0f))
                    for (var part = 0; part < bodies[index].Length; part++)
                        Assert.That(bodies[index][part].localPosition, Is.EqualTo(rest[index][part]), "Waiting houses retain their saved pose.");
            }
            InvokeCity(quake, "AdvanceShake", duration * .4f - .5f);
            Assert.That(active.Select(animation => animation.Progress).Distinct().Count(), Is.EqualTo(count > 2 ? count : 1),
                "The shaking animations keep different phases after the delays instead of immediately synchronizing again.");
            InvokeCity(quake, "AdvanceShake", duration);
            Assert.That(quake.IsRunning, Is.False);
            Assert.That(quake.TestDuration, Is.EqualTo(duration), "Staggering cannot extend the monster's two-second earthquake tail.");
            Assert.That(active.All(animation => animation.HasCollapsed && !animation.IsRunning && animation.Progress == 1f), Is.True,
                "Even the last delayed unprotected house reaches its final collapse pose before the event ends.");
            quake.StopEarthquake();
            quake.StartEarthquake();
            InvokeCity(quake, "AdvanceShake", .1f);
            Assert.That(active.Count(animation => animation.Progress > 0f), Is.EqualTo(count > 2 ? 1 : count), "Replay starts with fresh delays.");
            quake.StopEarthquake();
            Assert.That(animations.All(animation => !animation.IsRunning && animation.Progress == 0f), Is.True);
            for (var i = 0; i < bodies.Length; i++)
                for (var part = 0; part < bodies[i].Length; part++)
                    Assert.That(bodies[i][part].localPosition, Is.EqualTo(rest[i][part]), "Cancellation also resets houses still waiting for their delay.");
        }
        foreach (var animation in animations) animation.enabled = true;
        yield return null;
    }

    [UnityTest]
    public IEnumerator ApartmentEarthquakeChoosesAuthoredMotionFromTheFourCardProtectionCombinations()
    {
        var links = CityHouseLinks();
        var rails = ProduceCityProducts(UpgradeKind.RailSystem, 2);
        var elevators = ProduceCityProducts(UpgradeKind.Breakwater, 2);
        Assert.That(links[1].House.TryInstall(rails[0]), Is.True);
        Assert.That(links[2].House.TryInstall(rails[1]), Is.True);
        Assert.That(links[2].House.TryInstall(elevators[0]), Is.True);
        Assert.That(links[3].House.TryInstall(elevators[1]), Is.True);
        Assert.That(links[0].House.TryInstall(ProduceCityProducts(UpgradeKind.Fire)[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        var animations = links.Select(link => link.Building.GetComponent<ApartmentEarthquakeAnimation>()).ToArray();
        var bodies = links.Select(link => EarthquakeBodies(link.Building)).ToArray();
        var positions = bodies.Select(group => group.Select(body => body.localPosition).ToArray()).ToArray();
        var rotations = bodies.Select(group => group.Select(body => body.localRotation).ToArray()).ToArray();
        var grounds = links.Select(link => link.Building.Find("Apartment_Ground")).ToArray();
        var groundPositions = grounds.Select(ground => ground.position).ToArray();
        var meshes = links.Select(link => link.Building.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).ToArray()).ToArray();
        var head = links[0].Building.GetComponent<ApartmentUpgradeModel>().SprinklerHead(0);
        var attachment = bodies[0][0].InverseTransformPoint(head.position);
        var support = links[2].Building.Find("Apartment_Motion/Apartments_Elevated/Elevator_Supports/Apartment1_Supports");
        var supportAttachment = bodies[2][0].InverseTransformPoint(support.position);
        var ui = UIIds(game);
        var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        quake.StartEarthquake();
        var expected = new[] { "Apartment_Earthquake_Collapse", "Apartment_Seismic_Default", "Apartment_Seismic_Elevated", "Apartment_Earthquake_Elevated_Collapse" };
        for (var i = 0; i < 4; i++)
        {
            Assert.That(animations[i].IsRunning, Is.True);
            Assert.That(animations[i].ActiveClip.name, Is.EqualTo(expected[i]));
            for (var j = 0; j < 6; j++)
                Assert.That(Vector3.Distance(bodies[i][j].localPosition, positions[i][j]), Is.LessThan(.0001f), "No initial model jump.");
        }
        InvokeCity(quake, "AdvanceShake", quake.TestDuration * .4f);
        foreach (var i in new[] { 1, 2 })
        {
            Assert.That(animations[i].IsProtected, Is.True);
            Assert.That(bodies[i].Where((body, j) => Vector3.Distance(body.localPosition, positions[i][j]) > .0005f).Any(), Is.True,
                "The seismic clips must visibly move, rather than simply disabling collapse.");
        }
        CaptureAuthoredApartmentSprinklers(links[0].Building, "earthquake-unprotected-shaking");
        CaptureAuthoredApartmentSprinklers(links[1].Building, "earthquake-seismic-shaking");
        CaptureAuthoredApartmentSprinklers(links[2].Building, "earthquake-elevated-seismic-shaking");
        InvokeCity(quake, "AdvanceShake", quake.TestDuration * .57f);
        Assert.That(Vector3.Distance(head.position, bodies[0][0].TransformPoint(attachment)), Is.LessThan(.001f));
        Assert.That(Vector3.Distance(support.position, bodies[2][0].TransformPoint(supportAttachment)), Is.LessThan(.001f));
        CaptureAuthoredApartmentSprinklers(links[0].Building, "earthquake-unprotected-collapsing");
        InvokeCity(quake, "AdvanceShake", quake.TestDuration);
        for (var i = 0; i < 4; i++)
        {
            Assert.That(animations[i].HasCollapsed, Is.EqualTo(i == 0 || i == 3), "Only seismic protection prevents earthquake collapse.");
            Assert.That(grounds[i].position, Is.EqualTo(groundPositions[i]));
            Assert.That(links[i].Building.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh), Is.EqualTo(meshes[i]));
            for (var j = 0; j < 6; j++)
            {
                if (i == 0 || i == 3)
                    Assert.That(Vector3.Distance(bodies[i][j].localPosition, positions[i][j]), Is.GreaterThan(.15f));
                else
                {
                    Assert.That(bodies[i][j].localPosition, Is.EqualTo(positions[i][j]));
                    Assert.That(bodies[i][j].localRotation, Is.EqualTo(rotations[i][j]));
                }
            }
        }
        CaptureAuthoredApartmentSprinklers(links[0].Building, "earthquake-unprotected-finished");
        CaptureAuthoredApartmentSprinklers(links[2].Building, "earthquake-protected-finished");
        quake.StopEarthquake();
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 6; j++) Assert.That(bodies[i][j].localPosition, Is.EqualTo(positions[i][j]));
        Assert.That(links[1].RailSystem.IsInstalled && links[2].RailSystem.IsInstalled && links[2].FloodProtection.IsInstalled && links[3].FloodProtection.IsInstalled, Is.True);
        Assert.That(links[0].FireProtection.IsInstalled, Is.True);
        Assert.That(UIIds(game), Is.EqualTo(ui));
    }

    [UnityTest]
    public IEnumerator ApartmentEarthquakeYieldsToTheFloodAndRestartsFromTheSavedPose()
    {
        var link = CityHouseLinks()[0];
        var animation = link.Building.GetComponent<ApartmentEarthquakeAnimation>();
        var collapse = link.Building.GetComponent<ApartmentTsunamiCollapse>();
        var bodies = EarthquakeBodies(link.Building);
        var positions = bodies.Select(body => body.localPosition).ToArray();
        var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        quake.StartEarthquake();
        InvokeCity(quake, "AdvanceShake", quake.TestDuration * .5f);
        Assert.That(animation.IsRunning, Is.True);
        Assert.That(collapse.TryCollapse(), Is.True);
        Assert.That(animation.IsRunning || animation.HasCollapsed, Is.False);
        InvokeCity(collapse, "AdvanceCollapse", collapse.Duration * .6f);
        var floodRotations = bodies.Select(body => body.localRotation).ToArray();
        InvokeCity(quake, "AdvanceShake", quake.TestDuration);
        for (var j = 0; j < 6; j++) Assert.That(bodies[j].localRotation, Is.EqualTo(floodRotations[j]), "The two disasters cannot overwrite each other's transforms.");
        quake.StartEarthquake();
        Assert.That(animation.IsRunning, Is.False, "A house already destroyed by the tsunami must not stand up for the earthquake.");
        quake.StopEarthquake();
        collapse.ResetCollapse();
        quake.StartEarthquake();
        Assert.That(animation.IsRunning, Is.True);
        for (var j = 0; j < 6; j++) Assert.That(Vector3.Distance(bodies[j].localPosition, positions[j]), Is.LessThan(.0001f));
        InvokeCity(quake, "AdvanceShake", quake.TestDuration * .8f);
        quake.StopEarthquake();
        for (var j = 0; j < 6; j++) Assert.That(bodies[j].localPosition, Is.EqualTo(positions[j]));
        quake.StartEarthquake();
        animation.enabled = false;
        Assert.That(animation.IsRunning || animation.HasCollapsed, Is.False);
        quake.StopEarthquake();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ApartmentEarthquakeRespondsToProtectionInstalledDuringShaking()
    {
        var link = CityHouseLinks()[0];
        var rail = ProduceCityProducts(UpgradeKind.RailSystem)[0];
        var elevator = ProduceCityProducts(UpgradeKind.Breakwater)[0];
        var animation = link.Building.GetComponent<ApartmentEarthquakeAnimation>();
        var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        quake.StartEarthquake();
        Assert.That(animation.ActiveClip.name, Is.EqualTo("Apartment_Earthquake_Collapse"));
        Assert.That(link.House.TryInstall(rail), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(animation.ActiveClip.name, Is.EqualTo("Apartment_Seismic_Default"));
        Assert.That(link.House.TryInstall(elevator), Is.True);
        yield return new WaitForSecondsRealtime(1.15f);
        Assert.That(animation.ActiveClip.name, Is.EqualTo("Apartment_Seismic_Elevated"));
        InvokeCity(quake, "AdvanceShake", quake.TestDuration);
        Assert.That(animation.HasCollapsed, Is.False);
        Assert.That(link.Building.GetComponent<ApartmentUpgradeModel>().IsElevated, Is.True);
        Assert.That(link.RailSystem.IsInstalled && link.FloodProtection.IsInstalled, Is.True);
    }
}
