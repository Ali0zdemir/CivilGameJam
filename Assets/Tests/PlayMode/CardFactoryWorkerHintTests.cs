using System.Collections;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardAbilityPointsPlayModeTests
{
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
        factoryStation.UseSequencePrices(2); // Tsunami normal factory: 7 AP.
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False);
        bank.AddPoints(2);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        factoryStation.UseSequencePrices(3); // Fire normal factory: 12 AP.
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False);
        bank.AddPoints(5);
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
