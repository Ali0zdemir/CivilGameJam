using System.Collections;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [TestCase(UpgradeKind.RailSystem, false)]
    [TestCase(UpgradeKind.RailSystem, true)]
    [TestCase(UpgradeKind.Breakwater, false)]
    [TestCase(UpgradeKind.Breakwater, true)]
    [TestCase(UpgradeKind.Fire, false)]
    [TestCase(UpgradeKind.Fire, true)]
    public void OneResearcherCanDiscoverEveryBlueprint(UpgradeKind kind, bool educated)
    {
        if (kind != UpgradeKind.RailSystem) game.CompleteBlueprint(UpgradeKind.RailSystem);
        var worker = StaffingWorker(educated);
        Assert.That(research.TryAssign(worker), Is.True);
        Assert.That(research.HasActiveWork, Is.True);
        Assert.That(research.GetComponent<BuildingWorkEffects>().IsWorking, Is.True);
        research.AdvanceWork(game.NormalResearchSeconds + .01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        var options = ControlField<CardGameController.BlueprintOption[]>(game, "blueprints");
        foreach (var option in options)
        {
            Assert.That(option.button.transform.Find("Name").GetComponent<TMP_Text>().text, Does.Not.Contain("2 PEOPLE REQUIRED"));
        }
        Assert.That(options[(int)kind - 1].button.interactable, Is.True);
        game.SelectBlueprint((int)kind - 1);
        Assert.That(game.Vault.IsDiscovered(kind), Is.True);
        Assert.That(research.WorkerCount, Is.Zero);
        Assert.That(worker.UsedThisTurn, Is.True);
    }

    [TestCase(UpgradeKind.Breakwater, false, false)]
    [TestCase(UpgradeKind.Breakwater, false, true)]
    [TestCase(UpgradeKind.Breakwater, true, true)]
    [TestCase(UpgradeKind.Fire, false, false)]
    [TestCase(UpgradeKind.Fire, false, true)]
    [TestCase(UpgradeKind.Fire, true, true)]
    public void FireAndTsunamiBlueprintsWaitForTwoFactoryWorkers(UpgradeKind kind, bool firstEducated, bool secondEducated)
    {
        var blueprint = ResearchPlan((int)kind - 1);
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>(); factory.enabled = false;
        var first = StaffingWorker(firstEducated);
        var second = StaffingWorker(secondEducated, first);
        var balance = game.AbilityPoints.CurrentPoints;
        Assert.That(factory.TryInsertBlueprint(blueprint), Is.True);
        Assert.That(factory.RequiredWorkers, Is.EqualTo(2));
        Assert.That(station.TryAssign(first), Is.True);
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.Zero);
        Assert.That(factory.WorkRate, Is.Zero);
        Assert.That(first.UsedThisTurn, Is.False);
        Assert.That(station.GetComponent<BuildingWorkEffects>().IsWorking, Is.False);
        Assert.That(station.TryAssign(second), Is.True);
        Assert.That(factory.HasEnoughWorkers, Is.True);
        Assert.That(station.GetComponent<BuildingWorkEffects>().IsWorking, Is.True);
        factory.AdvanceWork(.5f);
        Assert.That(factory.Progress, Is.GreaterThan(0f).And.LessThan(1f));
        factory.AdvanceWork(100f);
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade && card.UpgradeType == kind), Is.EqualTo(2));
        Assert.That(first.UsedThisTurn && second.UsedThisTurn, Is.True);
        Assert.That(station.WorkerCount, Is.Zero);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance - station.AbilityPointCost(first.Kind) - station.AbilityPointCost(second.Kind)));
        Assert.That(blueprint.StoredInVault, Is.SameAs(game.Vault));
    }

    [Test]
    public void RemovingOneResearcherKeepsResearchRunningAndRefundsOnlyThatPerson()
    {
        game.CompleteBlueprint(UpgradeKind.RailSystem);
        var workers = People().Take(2).ToArray();
        int startingPoints = game.AbilityPoints.CurrentPoints;
        foreach (var worker in workers) Assert.That(research.TryAssign(worker), Is.True);
        int workerCost = research.AbilityPointCost(CardKind.Person);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(startingPoints - 2 * workerCost));
        research.AdvanceWork(game.NormalResearchSeconds * .2f);
        Assert.That(research.ResearchProgress, Is.EqualTo(.4f).Within(.0001f));
        Assert.That(research.ReleaseWorker(1, false), Is.SameAs(workers[1]));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(startingPoints - workerCost));
        Assert.That(research.ResearchProgress, Is.EqualTo(.2f).Within(.0001f));
        Assert.That(research.HasActiveWork, Is.True);
        Assert.That(research.GetComponent<BuildingWorkEffects>().IsWorking, Is.True);
        research.AdvanceWork(game.NormalResearchSeconds * .1f);
        Assert.That(research.ResearchProgress, Is.EqualTo(.3f).Within(.0001f));
        Assert.That(game.IsChoosingBlueprint, Is.False);
        research.AdvanceWork(100f);
        game.SelectBlueprint(1);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.Breakwater), Is.True);
        Assert.That(workers[0].UsedThisTurn, Is.True);
        Assert.That(workers[1].UsedThisTurn, Is.False);
    }

    [Test]
    public void RemovingAFactoryWorkerPausesTwoPersonProductionAndRefundsTheirContribution()
    {
        var blueprint = ResearchPlan(2);
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>(); factory.enabled = false;
        var first = StaffingWorker(false);
        var workers = new[] { first, StaffingWorker(false, first) };
        var balance = game.AbilityPoints.CurrentPoints;
        Assert.That(factory.TryInsertBlueprint(blueprint), Is.True);
        foreach (var worker in workers) Assert.That(station.TryAssign(worker), Is.True);
        factory.AdvanceWork(factory.ProductionSeconds * .2f);
        Assert.That(factory.Progress, Is.EqualTo(.4f).Within(.0001f));
        Assert.That(station.ReleaseWorker(1, false), Is.SameAs(workers[1]));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance - station.AbilityPointCost(CardKind.Person)));
        Assert.That(factory.Progress, Is.EqualTo(.2f).Within(.0001f));
        Assert.That(factory.HasEnoughWorkers, Is.False);
        Assert.That(station.GetComponent<BuildingWorkEffects>().IsWorking, Is.False);
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.EqualTo(.2f).Within(.0001f));
        Assert.That(station.TryAssign(workers[1]), Is.True);
        factory.AdvanceWork(100f);
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(workers.All(worker => worker.UsedThisTurn), Is.True);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance - 2 * station.AbilityPointCost(CardKind.Person)));
    }

    [UnityTest]
    public IEnumerator FactoryKeepsRequestingASecondWorkerForFireBlueprints()
    {
        var blueprint = ResearchPlan(2);
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>(); factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        Assert.That(factory.TryInsertBlueprint(blueprint), Is.True);
        Assert.That(station.TryAssign(StaffingWorker(false)), Is.True);
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        Assert.That(station.TryAssign(StaffingWorker(false)), Is.True);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.False);
    }

    private DraggableCard StaffingWorker(bool educated, DraggableCard excluded = null)
    {
        var person = People().First(card => card.CanStartWork && !card.IsStored && card != excluded);
        if (!educated) return person;
        Assert.That(school.TryAssign(person), Is.True);
        school.AdvanceWork(game.EducationSeconds + .01f);
        return game.Cards.Last(card => card.Kind == CardKind.EducatedPerson && card.CanStartWork && !card.IsStored);
    }
}
