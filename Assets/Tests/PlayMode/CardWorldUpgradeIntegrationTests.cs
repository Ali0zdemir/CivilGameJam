using System.Collections;
using System.Linq;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardAbilityPointsPlayModeTests
{
    [UnityTest]
    public IEnumerator AuthoredReadyCollapseHasOneAnimationDriverAndKeepsTheFailedTurnResultAfterRebuilding()
    {
        var result = sequence.GetComponent<MonsterSequenceResults>();
        var body = houses[0].Building.Find("Apartment_Motion/Apartments_Normal/Apartment1");
        var position = body.localPosition; var rotation = body.localRotation;
        round.Ready(); Until(() => earthquake.IsRunning);
        var damage = houses[0].Building.GetComponent<EarthquakeHouseDamage>();
        var authored = houses[0].Building.GetComponent<ApartmentEarthquakeAnimation>();
        Assert.That(damage.UsesAuthoredAnimation && authored.IsRunning, Is.True);
        Step(earthquake.TestDuration * .4f);
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False, "Swaying alone must not trigger the next turn.");
        var shakingPosition = body.localPosition; var shakingRotation = body.localRotation;
        var progress = authored.Progress;
        Invoke(damage, "AdvanceCollapse", 1f);
        Assert.That(body.localPosition, Is.EqualTo(shakingPosition));
        Assert.That(body.localRotation, Is.EqualTo(shakingRotation));
        Assert.That(authored.Progress, Is.EqualTo(progress), "The legacy adapter cannot advance the sequence's animation a second time.");
        Until(() => round.RoundNumber == 2);
        Assert.That(result.IsVisible, Is.True);
        Assert.That(result.DestroyedHouseCount, Is.EqualTo(4));
        Assert.That(result.TotalHouseCount, Is.EqualTo(4));
        Assert.That(result.ProtectedHouseCount, Is.Zero);
        Assert.That(Field<TMP_Text>(result, "destroyedCountLabel").text, Is.EqualTo("0 / 4"));
        Assert.That(Field<TMP_Text>(result, "earnedPointsLabel").text, Is.EqualTo("AP EARNED: +10"));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20));
        Assert.That(round.SequenceNumber, Is.EqualTo(1));
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(body.localPosition, Is.EqualTo(position));
        Assert.That(body.localRotation, Is.EqualTo(rotation));
        AssertFreshPeople(System.Array.Empty<DraggableCard>());
        result.Retry(); round.AdvanceRound(100f);
        Assert.That(result.IsVisible, Is.False);
        Assert.That(round.RoundNumber, Is.EqualTo(2));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20), "Closing the report cannot pay rewards twice.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator AuthoredAllSafeRoundKeepsCardsAndAPAndReportsZeroLossesBeforeTheWaterStage()
    {
        bank.AddPoints(4); PrepareSeismic(2);
        var installed = houses.Select(house => house.House.GetInstalled(UpgradeKind.RailSystem)).ToArray();
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready(); Until(() => round.RoundNumber == 2);
        var result = sequence.GetComponent<MonsterSequenceResults>();
        Assert.That(result.IsVisible, Is.True);
        Assert.That(result.DestroyedHouseCount, Is.Zero);
        Assert.That(result.ProtectedHouseCount, Is.EqualTo(4));
        Assert.That(Field<TMP_Text>(result, "destroyedCountLabel").text, Is.EqualTo("4 / 4"));
        Assert.That(Field<TMP_Text>(result, "earnedPointsLabel").text, Is.EqualTo("AP EARNED: +18"));
        Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(bank.CurrentPoints, Is.EqualTo(18));
        Assert.That(houses.Select(house => house.House.GetInstalled(UpgradeKind.RailSystem)), Is.EqualTo(installed));
        Assert.That(houses.All(house => house.Building.GetComponent<ApartmentUpgradeModel>().HasSeismic), Is.True);
        Assert.That(houses.Any(house => house.Building.GetComponent<ApartmentEarthquakeAnimation>().IsRunning), Is.False);
        result.Retry(); round.Ready();
        Assert.That(sequence.IsRunning && sequence.ActiveSequence == 2, Is.True);
        sequence.StopSequence(); round.AdvanceRound(0f);
        Assert.That(result.IsVisible, Is.False, "Cancellation must not show a completed-round report.");
        Assert.That(bank.CurrentPoints, Is.EqualTo(18));
    }

    [UnityTest]
    public IEnumerator AuthoredWaterRetryKeepsSeismicAndElevatorsAndReportsOnlyTheTwoFloodedHouses()
    {
        yield return ReachTsunami();
        PrepareElevators(1);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready(); Until(() => round.RoundNumber == 3);
        var result = sequence.GetComponent<MonsterSequenceResults>();
        Assert.That(result.IsVisible, Is.True);
        Assert.That(result.DestroyedHouseCount, Is.EqualTo(2));
        Assert.That(result.ProtectedHouseCount, Is.EqualTo(2));
        Assert.That(Field<TMP_Text>(result, "destroyedCountLabel").text, Is.EqualTo("2 / 4"));
        Assert.That(Field<TMP_Text>(result, "earnedPointsLabel").text, Is.EqualTo("AP EARNED: +18"));
        Assert.That(bank.CurrentPoints, Is.EqualTo(23));
        Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(houses.All(house => house.Building.GetComponent<ApartmentUpgradeModel>().HasSeismic), Is.True);
        Assert.That(houses.Count(house => house.Building.GetComponent<ApartmentUpgradeModel>().IsElevated), Is.EqualTo(2));
        Assert.That(houses.Any(house => house.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed ||
            house.Building.GetComponent<ApartmentEarthquakeAnimation>().HasCollapsed), Is.False);
        AssertFreshPeople(System.Array.Empty<DraggableCard>());
        result.Retry(); round.AdvanceRound(100f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(23));
    }
}
