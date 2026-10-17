using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;

public sealed partial class CardGamePlayModeTests
{
    private FactoryProduction FactoryWithWorkers(int normalWorkers, int educatedWorkers)
    {
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>();
        factory.enabled = false;
        var blueprint = ResearchPlan(0);
        for (int i = 0; i < educatedWorkers; i++)
        {
            Assert.That(school.TryAssign(People().First(person => person.AssignedTo == null)), Is.True);
            school.AdvanceWork(game.EducationSeconds + 0.01f);
        }
        foreach (var worker in People().Where(person => person.AssignedTo == null).Take(normalWorkers).ToArray())
            Assert.That(station.TryAssign(worker), Is.True);
        foreach (var worker in game.Cards.Where(person => person.Kind == CardKind.EducatedPerson).Take(educatedWorkers).ToArray())
            Assert.That(station.TryAssign(worker), Is.True);
        Assert.That(station.WorkerCount, Is.EqualTo(normalWorkers + educatedWorkers));
        Assert.That(factory.TryInsertBlueprint(blueprint), Is.True);
        return factory;
    }

    [TestCase(0, 0, 0f)]
    [TestCase(1, 0, 1f)]
    [TestCase(0, 1, 1.8f)]
    [TestCase(2, 0, 2f)]
    [TestCase(1, 1, 2.8f)]
    [TestCase(0, 2, 3.6f)]
    public void FactoryWorkerConfigurationsContributeAtTheirOwnRates(int normalWorkers, int educatedWorkers, float expectedRate)
    {
        var factory = FactoryWithWorkers(normalWorkers, educatedWorkers);
        var blueprint = factory.PendingBlueprint;
        Assert.That(factory.EducatedWorkerMultiplier, Is.EqualTo(1.8f));
        Assert.That(factory.WorkRate, Is.EqualTo(expectedRate).Within(0.00001f));
        if (expectedRate == 0f)
        {
            factory.AdvanceWork(100f);
            Assert.That(factory.Progress, Is.Zero);
            Assert.That(factory.PendingBlueprint, Is.SameAs(blueprint));
            Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.Zero);
            return;
        }
        float duration = factory.ProductionSeconds / expectedRate;
        factory.AdvanceWork(duration * 0.4f);
        Assert.That(factory.Progress, Is.EqualTo(0.4f).Within(0.00001f));
        factory.AdvanceWork(duration * 0.59f);
        Assert.That(factory.Progress, Is.EqualTo(0.99f).Within(0.00001f));
        Assert.That(factory.PendingBlueprint, Is.SameAs(blueprint), "Production must not finish early.");
        factory.AdvanceWork(duration * 0.011f);
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(blueprint.StoredInVault, Is.SameAs(game.Vault));
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
        Assert.That(Station(CardKind.Factory).WorkerCount, Is.Zero, "Completed workers leave the factory for the rest of the turn.");
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4 - educatedWorkers));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(educatedWorkers));
    }

    [Test]
    public void ChangingFactoryWorkersPreservesProgressAndImmediatelyChangesWorkRate()
    {
        var factory = FactoryWithWorkers(1, 0);
        var station = Station(CardKind.Factory);
        var normal = station.GetWorker(0);
        factory.AdvanceWork(factory.ProductionSeconds * 0.2f);
        Assert.That(factory.Progress, Is.EqualTo(0.2f).Within(0.00001f));
        Assert.That(school.TryAssign(People().First(person => person.AssignedTo == null)), Is.True);
        school.AdvanceWork(game.EducationSeconds + 0.01f);
        Assert.That(station.TryAssign(Educated()), Is.True);
        Assert.That(factory.WorkRate, Is.EqualTo(2.8f).Within(0.00001f));
        factory.AdvanceWork(factory.ProductionSeconds * 0.2f);
        Assert.That(factory.Progress, Is.EqualTo(0.76f).Within(0.00001f));
        station.ReleaseWorker(0, false);
        Assert.That(factory.WorkRate, Is.EqualTo(1.8f));
        factory.AdvanceWork(factory.ProductionSeconds * 0.1f);
        Assert.That(factory.Progress, Is.EqualTo(0.54f).Within(0.00001f));
        station.ReleaseWorker(1, false);
        Assert.That(factory.WorkRate, Is.Zero);
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.Zero, "Withdrawing unfinished workers cancels their refunded effort.");
        Assert.That(station.TryAssign(normal), Is.True);
        factory.AdvanceWork(0f);
        factory.AdvanceWork(-10f);
        Assert.That(factory.Progress, Is.Zero);
        factory.AdvanceWork(factory.ProductionSeconds + .01f);
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(3));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
    }

    [TestCase(0, 1, 1.8f)]
    [TestCase(2, 0, 2f)]
    [TestCase(1, 1, 2.8f)]
    [TestCase(0, 2, 3.6f)]
    public void AcceleratedFactoryBarsAdvanceContinuously(int normalWorkers, int educatedWorkers, float rate)
    {
        var factory = FactoryWithWorkers(normalWorkers, educatedWorkers);
        float stepDuration = factory.ProductionSeconds / rate / 10f;
        AssertContinuousBar(Station(CardKind.Factory).Card, factory.AdvanceWork, stepDuration);
        Assert.That(factory.PendingBlueprint, Is.Not.Null);
        AssertNoTimingText(game.GetComponentInParent<UnityEngine.Canvas>());
        factory.AdvanceWork(stepDuration + 0.001f);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
    }
}
