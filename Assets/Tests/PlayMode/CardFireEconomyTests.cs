using System;
using System.Collections;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardAbilityPointsPlayModeTests
{
    private IEnumerator ReachFire()
    {
        yield return ReachTsunami();
        bank.AddPoints(23); // Fund nine AP research and two complete sixteen-AP factory crews.
        PrepareElevators(2);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready();
        Until(() => round.RoundNumber == 3);
        Assert.That(round.SequenceNumber, Is.EqualTo(3));
        Assert.That(bank.CurrentPoints, Is.EqualTo(30));
        AssertFreshPeople(Array.Empty<DraggableCard>());
        sequence.Results.Retry();
    }

    private static string Badge(BuildingStation station, string name) =>
        station.transform.Find("FolderArt/" + name).GetComponentInChildren<TMP_Text>(true).text;

    [UnityTest]
    public IEnumerator FireUsesItsOwnPricesAndRefundsOnlyTheReservedCharges()
    {
        yield return ReachFire();
        Assert.That(school.AbilityPointCost(CardKind.Person), Is.EqualTo(4));
        Assert.That(research.AbilityPointCost(CardKind.Person), Is.EqualTo(12));
        Assert.That(research.AbilityPointCost(CardKind.EducatedPerson), Is.EqualTo(4));
        Assert.That(factoryStation.AbilityPointCost(CardKind.Person), Is.EqualTo(12));
        Assert.That(factoryStation.AbilityPointCost(CardKind.EducatedPerson), Is.EqualTo(4));
        Assert.That(Badge(school, "APNormalPeople"), Is.EqualTo("4 AP"));
        Assert.That(Badge(school, "APEducated"), Is.EqualTo("N/A"));
        Assert.That(Badge(research, "APNormalPeople"), Is.EqualTo("12 AP"));
        Assert.That(Badge(research, "APEducated"), Is.EqualTo("4 AP"));
        Assert.That(Badge(factoryStation, "APNormalPeople"), Is.EqualTo("12 AP"));
        Assert.That(Badge(factoryStation, "APEducated"), Is.EqualTo("4 AP"));

        var graduate = Educate();
        Assert.That(bank.CurrentPoints, Is.EqualTo(26));
        Assert.That(research.TryAssign(graduate), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(22));
        research.AdvanceWork(game.EducatedResearchSeconds * .2f);
        Set(research, "fireEducatedAbilityPointCost", 9);
        research.RefreshAbilityCostLabels();
        research.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(26), "Refund the original four AP, not a later Inspector price.");
        Assert.That(research.ResearchProgress, Is.Zero);
        Set(research, "fireEducatedAbilityPointCost", 4);
        research.RefreshAbilityCostLabels();
        var plan = Research(graduate, UpgradeKind.Fire);
        Assert.That(bank.CurrentPoints, Is.EqualTo(22));
        var maker = Educate();
        Assert.That(bank.CurrentPoints, Is.EqualTo(18));
        Assert.That(factoryStation.TryAssign(maker), Is.True);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(14));
        factory.AdvanceWork(.2f);
        factoryStation.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(18));
        Assert.That(factory.Progress, Is.Zero);
        Assert.That(factoryStation.TryAssign(maker), Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(14));
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.Zero, "One educated worker still waits for a second person.");
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        factory.AdvanceWork(100f);
        Assert.That(maker.UsedThisTurn, Is.True);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.Fire), Is.EqualTo(2));
        yield return new WaitForSecondsRealtime(.65f);
        game.ResetTurnPeople();
        bank.AddPoints(factoryStation.AbilityPointCost(CardKind.Person) - bank.CurrentPoints);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        Assert.That(factory.CurrentOrderCost, Is.EqualTo(24));
        Assert.That(factory.TryInsertBlueprint(plan), Is.False);
        Assert.That(bank.CurrentPoints, Is.EqualTo(12), "An unaffordable two-worker order must not charge either person.");
    }

    private void PrepareFireProtection(int batches)
    {
        var plan = Research(Person(), UpgradeKind.Fire);
        for (int i = 0; i < batches; i++)
        {
            FinishCompletedWorkerReturns();
            // Multi-order fixtures span worker turns while preserving their AP accounting.
            if (game.Cards.Count(card => card.Kind == CardKind.Person && card.CanStartWork && !card.IsStored) < 2)
                game.ResetTurnPeople();
            Assert.That(factoryStation.TryAssign(Person()), Is.True);
            Produce(plan);
        }
        var systems = game.Cards.Where(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.Fire).ToArray();
        for (int i = 0; i < systems.Length; i++) Assert.That(houses[i].House.TryInstall(systems[i]), Is.True);
    }

    [UnityTest]
    public IEnumerator TwoFireProtectedHousesPayTwentyAPAndKeepTheirEarlierSystems()
    {
        yield return ReachFire();
        bank.AddPoints(8); // Twelve AP research, two twelve-AP workers and two AP carried.
        PrepareFireProtection(1);
        Assert.That(bank.CurrentPoints, Is.EqualTo(2));
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready();
        Until(() => round.RoundNumber == 4);
        Assert.That(round.SequenceNumber, Is.EqualTo(3));
        Assert.That(round.LastRoundSurvived, Is.False);
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(2));
        Assert.That(round.LastSurvivalReward, Is.EqualTo(20));
        Assert.That(round.LastRoundGrant, Is.EqualTo(6));
        Assert.That(bank.CurrentPoints, Is.EqualTo(28), "2 carried + 20 survival + 6 turn bonus.");
        Assert.That(houses.All(house => house.House.GetInstalled(UpgradeKind.RailSystem) != null &&
            house.House.GetInstalled(UpgradeKind.Breakwater) != null), Is.True);
        AssertFreshPeople(Array.Empty<DraggableCard>());
    }

    [UnityTest]
    public IEnumerator FourFireProtectedHousesPayFortyAPExactlyOnce()
    {
        yield return ReachFire();
        bank.AddPoints(30); // Fund twelve AP research and two complete twenty-four AP factory crews.
        PrepareFireProtection(2);
        Assert.That(bank.CurrentPoints, Is.Zero);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready();
        Until(() => round.RoundNumber == 4);
        Assert.That(round.LastRoundSurvived, Is.True);
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(4));
        Assert.That(round.LastSurvivalReward, Is.EqualTo(40));
        Assert.That(bank.CurrentPoints, Is.EqualTo(46));
        for (int i = 0; i < 6; i++) round.AdvanceRound(100f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(46), "A completed event cannot pay survival or turn rewards again.");
        AssertFreshPeople(Array.Empty<DraggableCard>());
    }
}
