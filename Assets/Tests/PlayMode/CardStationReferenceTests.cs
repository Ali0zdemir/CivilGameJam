using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;

public sealed partial class CardGamePlayModeTests
{
    [Test]
    public void ClearedWorkerReferencesReconnectToExistingSavedSlots()
    {
        var saved = school.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.Index).ToArray();
        var ui = UIIds(game);
        PresentationField(school, "indicators", new WorkerSlot[0]);
        school.Initialize(game, school.Card);
        CollectionAssert.AreEqual(saved, ControlField<WorkerSlot[]>(school, "indicators"));
        CollectionAssert.AreEquivalent(ui, UIIds(game), "Recovering references must reuse the authored UI.");
        Assert.That(school.TryAssign(People()[0]), Is.True);
        school.AdvanceWork(game.EducationSeconds + 0.01f);
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
    }

    [Test]
    public void RemovedWorkerIndicatorsDoNotInterruptWorkOrWorkerReturn()
    {
        var worker = People()[0];
        Assert.That(research.TryAssign(worker), Is.True);
        Assert.That(school.TryAssign(People()[1]), Is.True);
        Object.DestroyImmediate(ControlField<WorkerSlot[]>(research, "indicators")[0]);
        Object.DestroyImmediate(ControlField<WorkerSlot[]>(school, "indicators")[0]);
        research.AdvanceWork(1f);
        Assert.That(research.ResearchProgress, Is.GreaterThan(0f));
        Assert.That(research.ReleaseWorker(0, false), Is.SameAs(worker));
        Assert.That(worker.AssignedTo, Is.Null);
        Assert.That(worker.gameObject.activeSelf, Is.True);
        school.AdvanceWork(game.EducationSeconds + 0.01f);
        Assert.That(school.WorkerCount, Is.Zero);
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(3));
        Assert.That(research.TryAssign(worker), Is.True, "The remaining interactive slot must still work.");
        Assert.That(research.GetWorker(1), Is.SameAs(worker));
    }

    [Test]
    public void FactoryProducesWithoutInstructionWidgetsOrProgressBar()
    {
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>();
        factory.enabled = false;
        Assert.That(station.transform.Find("FolderArt/BlueprintInput"), Is.Null);
        Assert.That(station.transform.Find("FolderArt/ProductionStatus"), Is.Null);
        Object.DestroyImmediate(station.transform.Find("ProgressBar").gameObject);
        factory.Initialize(game, station.Card);
        factory.AdvanceWork(0.1f);
        var blueprint = ResearchPlan(0);
        Assert.That(station.TryAssign(People().First(person => person.AssignedTo == null)), Is.True);
        Assert.That(factory.TryInsertBlueprint(blueprint), Is.True);
        factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(blueprint.StoredInVault, Is.SameAs(game.Vault));
        Assert.That(station.WorkerCount, Is.EqualTo(1));
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
    }
}
