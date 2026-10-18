using System.Collections;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardAbilityPointsPlayModeTests
{
    [UnityTest]
    public IEnumerator FailedFireTurnDiscardsInstalledAndLooseFireItemsButKeepsEarlierProtection()
    {
        yield return ReachFire();
        bank.AddPoints(8); // Cover the revised research price while preserving this retry's carried AP.
        var rails = houses.Select(house => house.House.GetInstalled(UpgradeKind.RailSystem)).ToArray();
        var elevators = houses.Select(house => house.House.GetInstalled(UpgradeKind.Breakwater)).ToArray();
        var plan = Research(Person(), UpgradeKind.Fire);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        Produce(plan);
        var items = game.Cards.Where(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.Fire).ToArray();
        Assert.That(items.Length, Is.EqualTo(2));
        Assert.That(houses[0].House.TryInstall(items[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.6f);
        Assert.That(houses[0].FireProtection.IsInstalled, Is.True);
        Assert.That(items[1].InstalledIn, Is.Null);
        round.Ready();
        Until(() => round.RoundNumber == 4);
        Assert.That(round.SequenceNumber, Is.EqualTo(3));
        Assert.That(round.LastRoundSurvived, Is.False);
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(1));
        Assert.That(round.LastSurvivalReward, Is.EqualTo(10));
        Assert.That(bank.CurrentPoints, Is.EqualTo(28), "2 carried + 10 survival + 16 turn bonus.");
        var fire = Field<CivilGameJam.Disasters.FireTestController>(round, "fire");
        Assert.That(sequence.Results.IsVisible, Is.True);
        Assert.That(houses.Count(house => fire.WasHouseBurnt(house.Building)), Is.EqualTo(3));
        Assert.That(houses[0].House.GetInstalled(UpgradeKind.Fire), Is.SameAs(items[0]));
        var appearances = houses[1].Building.GetComponentsInChildren<CivilGameJam.Disasters.ApartmentBurntAppearance>();
        Assert.That(appearances.All(appearance => appearance.IsBurnt), Is.True);
        var renderer = houses[1].Building.GetComponentsInChildren<MeshRenderer>().First(surface => surface.sharedMaterials.All(material => material.name.StartsWith("burnt")));
        var burntMaterials = renderer.sharedMaterials;
        Step(4f);
        yield return null;
        Assert.That(fire.WasHouseBurnt(houses[1].Building), Is.True, "Keep the burnt city throughout the report.");
        Assert.That(renderer.sharedMaterials, Is.EqualTo(burntMaterials));
        CaptureWorld("held-fire-damage-behind-report.png");
        sequence.Results.Retry();
        Assert.That(houses.Any(house => fire.WasHouseBurnt(house.Building)), Is.False);
        Assert.That(appearances.Any(appearance => appearance.IsBurnt), Is.False);
        Assert.That(renderer.sharedMaterials.Any(material => material.name.StartsWith("burnt")), Is.False);
        CaptureWorld("held-fire-rebuilt-after-close.png");
        Assert.That(game.Cards.Any(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.Fire), Is.False);
        for (int i = 0; i < houses.Length; i++)
        {
            Assert.That(houses[i].House.GetInstalled(UpgradeKind.Fire), Is.Null);
            Assert.That(houses[i].House.GetComponentsInChildren<HouseUpgradeSlot>().Single(slot => slot.Kind == UpgradeKind.Fire).Filled, Is.False);
            Assert.That(houses[i].FireProtection.IsInstalled, Is.False);
            var model = houses[i].Building.GetComponent<ApartmentUpgradeModel>();
            Assert.That(model.HasFireProtection, Is.False);
            Assert.That(houses[i].House.GetInstalled(UpgradeKind.RailSystem), Is.SameAs(rails[i]));
            Assert.That(houses[i].House.GetInstalled(UpgradeKind.Breakwater), Is.SameAs(elevators[i]));
            Assert.That(model.HasSeismic && model.IsElevatorProtected, Is.True);
        }
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.Fire), Is.True, "The researched plan is reusable; only its manufactured products are lost.");
        Assert.That(game.CanUseBlueprint(UpgradeKind.Fire), Is.True);
        AssertFreshPeople(System.Array.Empty<DraggableCard>());
        yield return null;
        Assert.That(items.All(item => item == null), Is.True, "No loose or installed fire item may survive as a hidden card.");
    }

    [UnityTest]
    public IEnumerator StoppingFireKeepsItsSystemsAndCompletingFireRetainsAllFour()
    {
        yield return ReachFire();
        bank.AddPoints(30); // Include twelve AP research and both complete factory crews.
        PrepareFireProtection(2);
        var systems = houses.Select(house => house.House.GetInstalled(UpgradeKind.Fire)).ToArray();
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready();
        Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Earthquake));
        sequence.StopSequence();
        round.AdvanceRound(0f);
        Assert.That(round.RoundNumber, Is.EqualTo(3));
        Assert.That(bank.CurrentPoints, Is.Zero);
        for (int i = 0; i < houses.Length; i++)
            Assert.That(houses[i].House.GetInstalled(UpgradeKind.Fire), Is.SameAs(systems[i]));
        Until(() => !navigation.IsEventCameraActive);
        Assert.That(round.CanReady, Is.True);
        round.Ready();
        Until(() => round.RoundNumber == 4);
        Assert.That(round.LastRoundSurvived, Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(56));
        for (int i = 0; i < houses.Length; i++)
        {
            Assert.That(houses[i].House.GetInstalled(UpgradeKind.Fire), Is.SameAs(systems[i]));
            Assert.That(houses[i].FireProtection.IsInstalled, Is.True);
            Assert.That(houses[i].Building.GetComponent<ApartmentUpgradeModel>().HasFireProtection, Is.True);
        }
    }
}
