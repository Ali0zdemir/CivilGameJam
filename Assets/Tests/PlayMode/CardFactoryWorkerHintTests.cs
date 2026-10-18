using System.Collections;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardAbilityPointsPlayModeTests
{
    [UnityTest]
    public IEnumerator ElevatorAndFireGuidesFollowTheMissingCrewAndDisappearWithoutAP()
    {
        bank.AddPoints(100);
        game.RestrictResearchTo(UpgradeKind.None);
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var originalUI = view.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).OrderBy(id => id).ToArray();
        foreach (var kind in new[] { UpgradeKind.Breakwater, UpgradeKind.Fire })
        {
            game.ResetTurnPeople();
            factoryStation.UseSequencePrices(kind == UpgradeKind.Breakwater ? 2 : 3);
            var plan = Research(Person(), kind);
            Assert.That(factory.TryInsertBlueprint(plan), Is.True);
            Assert.That(view.VisibleWorkerHintCount, Is.Zero, "Wait until the blueprint has entered its folder.");
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(view.VisibleWorkerHintCount, Is.EqualTo(2));
            var pair = Field<UnityEngine.RectTransform[]>(view, "pairedWorkerHints");
            Assert.That(pair.All(hint => hint.gameObject.activeInHierarchy), Is.True);
            Assert.That(pair[0].position.x, Is.LessThan(pair[1].position.x));
            var group = (UnityEngine.RectTransform)Field<UnityEngine.CanvasGroup>(view, "workerHint").transform;
            var savedPosition = group.anchoredPosition; var savedSize = group.sizeDelta; var savedScale = group.localScale;
            int available = bank.CurrentPoints; Assert.That(bank.TrySpend(available), Is.True);
            yield return null;
            Assert.That(view.VisibleWorkerHintCount, Is.Zero);
            bank.AddPoints(available); yield return new WaitForSecondsRealtime(.25f);
            Assert.That(view.VisibleWorkerHintCount, Is.EqualTo(2));
            yield return CaptureAvailabilityGuide("factory-" + kind + "-two-person-guides");
            Assert.That(factoryStation.TryAssign(Person()), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(view.VisibleWorkerHintCount, Is.EqualTo(1));
            Assert.That(factory.WorkRate, Is.Zero);
            Assert.That(pair.All(hint => !hint.gameObject.activeSelf), Is.True);
            Assert.That(factoryStation.TryAssign(Person()), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(view.VisibleWorkerHintCount, Is.Zero);
            Assert.That(factory.WorkRate, Is.EqualTo(2f));
            factoryStation.ReleaseWorker(1, false); yield return new WaitForSecondsRealtime(.25f);
            Assert.That(view.VisibleWorkerHintCount, Is.EqualTo(1), "Removing a worker restores one missing-person guide.");
            Assert.That(factory.ReleaseBlueprint(false), Is.SameAs(plan)); yield return null;
            Assert.That(view.VisibleWorkerHintCount, Is.Zero);
            Assert.That(group.anchoredPosition, Is.EqualTo(savedPosition));
            Assert.That(group.sizeDelta, Is.EqualTo(savedSize)); Assert.That(group.localScale, Is.EqualTo(savedScale));
            Assert.That(view.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).OrderBy(id => id), Is.EqualTo(originalUI),
                "The guide must use saved objects throughout every order.");
        }
    }

    [UnityTest]
    public IEnumerator FactoryWorkerHintFollowsAvailableAPAndTheCurrentSequencePrice()
    {
        var view = factory.GetComponent<FactoryFolderPresentation>();
        Assert.That(view.IsWorkerHintVisible, Is.False, "The editable preview must be hidden before an order exists.");
        var plan = Research(Person()); // 10 - 4 = 6 AP, three normal people remain.
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        Assert.That(bank.TrySpend(2), Is.True); // 4 cannot pay a normal factory worker's 5 AP.
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False);
        bank.AddPoints(1);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        Assert.That(bank.TrySpend(5), Is.True);
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False, "Zero AP must hide the guide immediately.");
        bank.AddPoints(5);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        factoryStation.UseSequencePrices(2); // Tsunami normal factory: 8 AP.
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False);
        bank.AddPoints(7);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        factoryStation.UseSequencePrices(3); // Fire normal factory: 12 AP.
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.True, "Twelve AP still cover a normal factory worker in the fire stage.");
        Assert.That(bank.TrySpend(1), Is.True);
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False);
        bank.AddPoints(1);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(12), "Displaying the guide must never spend AP.");
        Set(factoryStation, "fireNormalAbilityPointCost", 15);
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False, "Inspector price changes must take effect without reloading the order.");
        Set(factoryStation, "fireNormalAbilityPointCost", 0);
        Assert.That(bank.TrySpend(12), Is.True);
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False, "Keep the guide hidden at zero AP even if a custom price is free.");
    }

    [UnityTest]
    public IEnumerator FactoryWorkerHintUsesTheAffordableAvailableWorkerKindAndReturnsAfterARefund()
    {
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var educated = Educate(); // 8 AP remain.
        var plan = Research(Person()); // 4 AP remain: enough for educated (3), too little for normal (5).
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        bank.TrySpend(1);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        bank.TrySpend(1);
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False);
        bank.AddPoints(1);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        Assert.That(factoryStation.TryAssign(educated), Is.True);
        Assert.That(bank.CurrentPoints, Is.Zero);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.False);
        factoryStation.ReleaseWorker(0, false);
        Assert.That(bank.CurrentPoints, Is.EqualTo(3));
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True, "An unfinished worker's refunded AP should make the guide available again.");
    }
}
