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
        Assert.That(Field<TMP_Text>(result, "earnedPointsCount").text, Is.EqualTo("+10"));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20));
        Assert.That(round.SequenceNumber, Is.EqualTo(1));
        Assert.That(houses.All(house => house.House.IsDestroyed), Is.True);
        Assert.That(authored.HasCollapsed, Is.True);
        Assert.That(Vector3.Distance(body.localPosition, position), Is.GreaterThan(.1f));
        var fallenPosition = body.localPosition; var fallenRotation = body.localRotation;
        Step(4f);
        yield return null;
        Assert.That(body.localPosition, Is.EqualTo(fallenPosition));
        Assert.That(body.localRotation, Is.EqualTo(fallenRotation));
        Assert.That(result.IsVisible, Is.True);
        Assert.That(round.CanReady, Is.False);
        round.Ready();
        Assert.That(sequence.IsRunning, Is.False, "A report cannot be skipped by another READY.");
        CaptureWorld("held-earthquake-ruins-behind-report.png");
        AssertFreshPeople(System.Array.Empty<DraggableCard>());
        result.Retry(); round.AdvanceRound(100f);
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(body.localPosition, Is.EqualTo(position));
        Assert.That(body.localRotation, Is.EqualTo(rotation));
        Assert.That(authored.HasCollapsed, Is.False);
        CaptureWorld("held-earthquake-rebuilt-after-close.png");
        result.Retry();
        Assert.That(result.IsVisible, Is.False);
        Assert.That(round.RoundNumber, Is.EqualTo(2));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20), "Closing the report cannot pay rewards twice.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator AuthoredAllSafeRoundKeepsCardsAndAPAndStartsWaterWithoutAnIntermediateResultsPanel()
    {
        bank.AddPoints(4); PrepareSeismic(2);
        var installed = houses.Select(house => house.House.GetInstalled(UpgradeKind.RailSystem)).ToArray();
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready(); Until(() => round.RoundNumber == 2);
        var result = sequence.GetComponent<MonsterSequenceResults>();
        Assert.That(result.IsVisible, Is.False, "The earthquake success cannot interrupt the continuous monster entrance.");
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(4));
        Assert.That(round.LastSurvivalReward + round.LastRoundGrant, Is.EqualTo(18));
        Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(bank.CurrentPoints, Is.Zero, "No AP is paid at the earthquake-to-tsunami handoff.");
        Assert.That(houses.Select(house => house.House.GetInstalled(UpgradeKind.RailSystem)), Is.EqualTo(installed));
        Assert.That(houses.All(house => house.Building.GetComponent<ApartmentUpgradeModel>().HasSeismic), Is.True);
        Assert.That(houses.Any(house => house.Building.GetComponent<ApartmentEarthquakeAnimation>().IsRunning), Is.False);
        Assert.That(sequence.IsRunning && sequence.ActiveSequence == 2, Is.True);
        sequence.StopSequence(); round.AdvanceRound(0f);
        Assert.That(result.IsVisible, Is.False, "Cancellation must not show a completed-round report.");
        Assert.That(bank.CurrentPoints, Is.Zero, "Cancelling the tsunami cannot collect the pending earthquake award.");
    }

    [UnityTest]
    public IEnumerator AuthoredWaterRetryKeepsSeismicAndElevatorsAndReportsOnlyTheTwoFloodedHouses()
    {
        yield return ReachTsunami();
        bank.AddPoints(20); // Nine AP research, two twelve-AP workers and five AP carried.
        PrepareElevators(1);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready(); Until(() => round.RoundNumber == 3);
        var result = sequence.GetComponent<MonsterSequenceResults>();
        Assert.That(result.IsVisible, Is.True);
        Assert.That(result.DestroyedHouseCount, Is.EqualTo(2));
        Assert.That(result.ProtectedHouseCount, Is.EqualTo(2));
        Assert.That(Field<TMP_Text>(result, "destroyedCountLabel").text, Is.EqualTo("2 / 4"));
        Assert.That(Field<TMP_Text>(result, "earnedPointsCount").text, Is.EqualTo("+18"));
        Assert.That(bank.CurrentPoints, Is.EqualTo(23));
        Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(houses.All(house => house.Building.GetComponent<ApartmentUpgradeModel>().HasSeismic), Is.True);
        Assert.That(houses.Count(house => house.Building.GetComponent<ApartmentUpgradeModel>().IsElevated), Is.EqualTo(2));
        var flooded = houses.Where(house => house.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed).ToArray();
        Assert.That(flooded.Length, Is.EqualTo(2));
        AssertVisibleTsunamiRuins();
        var bodies = flooded.SelectMany(house => Enumerable.Range(1, 6)
            .Select(i => house.Building.Find("Apartment_Motion/Apartments_Normal/Apartment" + i))).ToArray();
        var collapsedPositions = bodies.Select(body => body.localPosition).ToArray();
        var collapsedRotations = bodies.Select(body => body.localRotation).ToArray();
        Step(4f);
        yield return null;
        AssertVisibleTsunamiRuins();
        Assert.That(bodies.Select(body => body.localPosition), Is.EqualTo(collapsedPositions));
        Assert.That(bodies.Select(body => body.localRotation), Is.EqualTo(collapsedRotations));
        CaptureWorld("held-tsunami-damage-behind-report.png");
        AssertFreshPeople(System.Array.Empty<DraggableCard>());
        result.Retry(); round.AdvanceRound(100f);
        Assert.That(houses.Any(house => house.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed ||
            house.Building.GetComponent<ApartmentEarthquakeAnimation>().HasCollapsed), Is.False);
        Assert.That(flooded.Any(house => house.Building.GetComponentsInChildren<MeshRenderer>(true).Any(renderer => renderer.forceRenderingOff)), Is.False);
        CaptureWorld("held-tsunami-rebuilt-after-close.png");
        Assert.That(bank.CurrentPoints, Is.EqualTo(23));
    }
}
