using System.Collections;
using System.Linq;
using System.Reflection;
using CivilGameJam.Cards;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [Test]
    public void ClearedProgressReferencesReconnectToExistingSavedControls()
    {
        var root = research.transform.Find("ProgressBar");
        var controls = UIIds(game);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(BuildingStation).GetField("progressRoot", flags).SetValue(research, null);
        typeof(BuildingStation).GetField("progressFill", flags).SetValue(research, null);
        research.Initialize(game, research.Card);
        Assert.That(typeof(BuildingStation).GetField("progressRoot", flags).GetValue(research), Is.SameAs(root));
        Assert.That(typeof(BuildingStation).GetField("progressFill", flags).GetValue(research), Is.Not.Null);
        Assert.That(UIIds(game), Is.EqualTo(controls));
    }

    [UnityTest]
    public IEnumerator MissingProgressPresentationDoesNotBreakResearchOrEducation()
    {
        Object.DestroyImmediate(research.transform.Find("ProgressBar").gameObject);
        Object.DestroyImmediate(school.transform.Find("ProgressBar").gameObject);
        Assert.That(research.TryAssign(People()[0]), Is.True);
        research.enabled = true;
        yield return null;
        research.enabled = false;
        Assert.That(research.ResearchProgress, Is.GreaterThan(0f), "The real Update loop must survive a missing UI reference.");
        research.AdvanceWork(25.01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        game.SelectBlueprint(0);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        Assert.That(school.TryAssign(People().First(p => p.AssignedTo == null)), Is.True);
        school.AdvanceWork(25.01f);
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator OneDiscoveryReturnsToVaultAndProducesTwoItemsOnEveryReuse()
    {
        var plan = ResearchPlan(0);
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>();
        factory.enabled = false;
        var worker = People().First(p => p.AssignedTo == null);
        Assert.That(station.TryAssign(worker), Is.True);
        int identity = plan.GetInstanceID();
        for (int order = 1; order <= 3; order++)
        {
            Assert.That(factory.TryInsertBlueprint(plan), Is.True);
            Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.Zero);
            factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
            Assert.That(factory.PendingBlueprint, Is.Null);
            Assert.That(plan.GetInstanceID(), Is.EqualTo(identity));
            Assert.That(plan.StoredInVault, Is.SameAs(game.Vault));
            Assert.That(plan.StoredAtFactory, Is.Null);
            Assert.That(plan.IsFlying, Is.True);
            Assert.That(plan.GetComponent<Canvas>().overrideSorting, Is.True, "The return flight must remain visible above the building cards.");
            Assert.That(game.Cards.Count(c => c.Kind == CardKind.Upgrade), Is.EqualTo(order * 2));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(plan.gameObject.activeSelf, Is.False);
            Assert.That(plan.GetComponent<Canvas>().overrideSorting, Is.False);
            var slot = game.Vault.SlotFor(UpgradeKind.RailSystem);
            Assert.That(slot.IsArriving, Is.False);
            Assert.That(slot.CardRect.gameObject.activeSelf, Is.True);
            Assert.That(slot.CanShowHover, Is.True);
            Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        }
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Blueprint), Is.EqualTo(1));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
        Assert.That(worker.AssignedTo, Is.SameAs(station));
        string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        yield return CaptureCanvas(System.IO.Path.Combine(directory, "reusable-blueprint-vault.png"), game.Vault.SlotFor(UpgradeKind.RailSystem).gameObject);
        yield return CaptureCanvas(System.IO.Path.Combine(directory, "people-info-enlarged.png"), People().First(p => p.AssignedTo == null).gameObject);
    }

    [Test]
    public void AllDiscoveriesFinishResearchAndNoDuplicatePlansAreNeeded()
    {
        for (int i = 0; i < 3; i++) ResearchPlan(i);
        Assert.That(game.HasUndiscoveredBlueprints, Is.False);
        Assert.That(research.WorkerCount, Is.Zero);
        Assert.That(research.TryAssign(People().First(p => p.AssignedTo == null)), Is.False);
        research.AdvanceWork(100f);
        Assert.That(game.IsChoosingBlueprint, Is.False);
        Assert.That(research.AwaitingBlueprint, Is.False);
        Assert.That(research.transform.Find("WorkStatus").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("All blueprints discovered"));
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Blueprint), Is.EqualTo(3));
    }
}
