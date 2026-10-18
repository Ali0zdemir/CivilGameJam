using System.Collections;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardAbilityPointsPlayModeTests
{
    [UnityTest]
    public IEnumerator LiveEarthquakeFinishesItsRoarWithTheQuakeAndKeepsRuinsUntilTheReportCloses()
    {
        var source = Field<AudioSource>(sequence, "earthquakeRoar");
        var results = sequence.Results;
        var bodies = houses.Select(house => house.Building.Find("Apartment_Motion/Apartments_Normal/Apartment1")).ToArray();
        var rest = bodies.Select(body => body.localPosition).ToArray();
        var started = Time.realtimeSinceStartup;
        var lastQuake = started; var lastRoar = started;
        var heardRoar = false;
        round.Ready();
        while (!results.IsVisible && Time.realtimeSinceStartup - started < 18f)
        {
            if (source.isPlaying)
            {
                if (!heardRoar) Assert.That(earthquake.ElapsedSeconds, Is.InRange(2f, 2.3f));
                heardRoar = true;
                lastRoar = Time.realtimeSinceStartup;
            }
            if (earthquake.IsRunning) lastQuake = Time.realtimeSinceStartup;
            yield return null;
        }
        Assert.That(results.IsVisible, Is.True, "The real scene must complete without manual time advances.");
        Assert.That(heardRoar, Is.True);
        Assert.That(source.pitch, Is.EqualTo(1f).Within(.00001f));
        Assert.That(Mathf.Abs(lastRoar - lastQuake), Is.LessThan(.3f), "The original-speed roar should finish with the earthquake.");
        Assert.That(source.isPlaying || !sequence.HouseEffectsFinished, Is.False);
        Assert.That(houses.All(house => house.House.IsDestroyed), Is.True);
        var fallen = bodies.Select(body => body.localPosition).ToArray();
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(bodies.Select(body => body.localPosition), Is.EqualTo(fallen));
        results.Retry();
        Assert.That(bodies.Select(body => body.localPosition), Is.EqualTo(rest));
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(round.RoundNumber, Is.EqualTo(2));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20));
    }

    [UnityTest]
    public IEnumerator FourSeismicHousesHearTheRoarSurviveAndContinueStraightIntoTheSynchronizedTsunami()
    {
        bank.AddPoints(4);
        PrepareSeismic(2);
        var rails = houses.Select(house => house.House.GetInstalled(CivilGameJam.Cards.UpgradeKind.RailSystem)).ToArray();
        yield return new WaitForSecondsRealtime(1.6f);
        var source = Field<AudioSource>(sequence, "earthquakeRoar");
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var water = Field<TsunamiTestController>(round, "tsunami");
        var wave = Field<AuthoredTsunamiWave>(water, "authoredWave");
        round.Ready();
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Earthquake));
        Assert.That(source.isPlaying, Is.False);
        Step(2.01f);
        Assert.That(source.isPlaying, Is.True, "The protected version hears the same delayed roar.");
        Assert.That(earthquake.IsRunning, Is.True);
        Assert.That(water.IsRunning || monster.Model.gameObject.activeSelf, Is.False);
        Assert.That(bank.CurrentPoints, Is.Zero);
        Until(() => round.SequenceNumber == 2);
        Assert.That(round.RoundNumber, Is.EqualTo(2));
        Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Earthquake));
        Assert.That(round.LastRoundSurvived, Is.True);
        Assert.That(round.LastProtectedHouseCount, Is.EqualTo(4));
        Assert.That(round.LastSurvivalReward, Is.EqualTo(12));
        Assert.That(round.LastRoundGrant, Is.EqualTo(6));
        Assert.That(bank.CurrentPoints, Is.EqualTo(18));
        Assert.That(bank.ActionsLocked && sequence.IsRunning && water.IsRunning, Is.True);
        Assert.That(round.CanReady || sequence.GetComponent<MonsterSequenceResults>().IsVisible, Is.False);
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Emerging));
        Assert.That(source.isPlaying, Is.False, "There is no second roar during the monster and water entrance.");
        Assert.That(houses.Select(house => house.House.GetInstalled(CivilGameJam.Cards.UpgradeKind.RailSystem)), Is.EqualTo(rails));
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(research.AbilityPointCost(CivilGameJam.Cards.CardKind.Person), Is.EqualTo(6));
        Assert.That(factoryStation.AbilityPointCost(CivilGameJam.Cards.CardKind.Person), Is.EqualTo(7));
        CaptureWorld("roar-earthquake-survived-before-water.png");
        Step(monster.WaveReleaseDelay + .1f);
        Assert.That(monster.Model.gameObject.activeSelf && wave.IsVisible, Is.True);
        Assert.That(monster.EmergenceProgress, Is.InRange(.1f, .99f));
        var waveZ = water.transform.InverseTransformPoint(wave.transform.position).z;
        var monsterZ = water.transform.InverseTransformPoint(monster.Model.position).z;
        Step(.3f);
        var waveTravel = water.transform.InverseTransformPoint(wave.transform.position).z - waveZ;
        var monsterTravel = water.transform.InverseTransformPoint(monster.Model.position).z - monsterZ;
        Assert.That(waveTravel, Is.GreaterThan(0f));
        Assert.That(monsterTravel, Is.EqualTo(waveTravel * .8f).Within(.0001f), "The boss follows behind the faster wave.");
        Assert.That(source.isPlaying, Is.False);
        CaptureWorld("roar-followed-by-monster-and-wave.png");
        Until(() => round.RoundNumber == 3);
        Assert.That(round.SequenceNumber, Is.EqualTo(2), "Losing to water cannot undo the earthquake victory.");
        Assert.That(round.LastRoundSurvived, Is.False);
        Assert.That(round.LastProtectedHouseCount, Is.Zero);
        Assert.That(round.LastSurvivalReward, Is.Zero);
        Assert.That(bank.CurrentPoints, Is.EqualTo(24), "One +18 earthquake award, then the existing +6 water retry bonus.");
        Assert.That(bank.ActionsLocked, Is.False);
        Assert.That(houses.Select(house => house.House.GetInstalled(CivilGameJam.Cards.UpgradeKind.RailSystem)), Is.EqualTo(rails));
        Assert.That(game.CanUseBlueprint(CivilGameJam.Cards.UpgradeKind.RailSystem), Is.False);
        AssertFreshPeople(System.Array.Empty<CivilGameJam.Cards.DraggableCard>());
        Until(() => !navigation.IsEventCameraActive);
        var result = sequence.GetComponent<MonsterSequenceResults>();
        Assert.That(result.IsVisible, Is.True);
        result.Retry();
        round.Ready();
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Emerging));
        Assert.That(water.IsRunning, Is.True);
        Assert.That(source.isPlaying, Is.False, "A water-stage retry starts with the monster, without replaying the earthquake roar.");
        sequence.StopSequence(); round.AdvanceRound(0f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(24));
    }

    [UnityTest]
    public IEnumerator UnprotectedReadyStartsTheQuakeThenPlaysTheQuieterRoarAfterTwoSeconds()
    {
        var source = Field<AudioSource>(sequence, "earthquakeRoar");
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        var water = Field<TsunamiTestController>(round, "tsunami");
        Assert.That(source, Is.Not.Null);
        Assert.That(source.clip.name, Is.EqualTo("Godzilla.v1"));
        Assert.That(source.isPlaying, Is.False);
        var homePan = navigation.PanOffset;
        var homeZoom = Field<float>(navigation, "currentZoom");
        round.Ready();
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Earthquake));
        Assert.That(earthquake.IsRunning, Is.True);
        Assert.That(earthquake.ElapsedSeconds, Is.Zero);
        Assert.That(source.isPlaying, Is.False);
        Assert.That(source.volume, Is.EqualTo(.7f).Within(.00001f));
        Assert.That(bank.ActionsLocked, Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(10));
        Assert.That(navigation.PanOffset, Is.EqualTo(homePan), "City framing must start without a camera cut.");
        var delay = Field<float>(sequence, "earthquakeRoarDelay");
        Assert.That(delay, Is.EqualTo(2f));
        Step(delay - .01f);
        Assert.That(earthquake.IsRunning, Is.True);
        Assert.That(source.isPlaying || water.IsRunning || monster.Model.gameObject.activeSelf, Is.False);
        Assert.That(walk.CompletedSteps, Is.Zero);
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(Field<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
        CaptureWorld("quake-city-before-delayed-roar.png");
        Step(.02f);
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Earthquake));
        Assert.That(earthquake.IsRunning, Is.True);
        Assert.That(earthquake.ElapsedSeconds, Is.EqualTo(2.01f).Within(.0001f));
        Assert.That(source.isPlaying, Is.True, "The quake has already run for two seconds when the roar starts.");
        Assert.That(source.pitch, Is.EqualTo(1f).Within(.00001f), "Play the recording at its original speed.");
        Assert.That(earthquake.TestDuration, Is.EqualTo(delay + source.clip.length).Within(.00001f),
            "Synchronize the offscreen earthquake to the unmodified recording and its start delay.");
        var expectedEnd = earthquake.ElapsedSeconds + source.clip.length / source.pitch;
        Assert.That(expectedEnd, Is.EqualTo(earthquake.TestDuration).Within(.05f));
        Assert.That(monster.Model.gameObject.activeSelf || walk.IsWalkingAfterEvent || water.IsRunning, Is.False);
        Until(() => houses.Any(house => house.House.IsDestroyed));
        CaptureWorld("roar-city-earthquake-collapse.png");
        Assert.That(monster.Model.gameObject.activeSelf || walk.IsWalkingAfterEvent || water.IsRunning, Is.False);
        Until(() => round.RoundNumber == 2);
        Assert.That(round.SequenceNumber, Is.EqualTo(1));
        Assert.That(round.LastRoundSurvived, Is.False);
        Assert.That(round.LastRoundGrant, Is.EqualTo(10));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20), "Carry the unspent AP and award one failed-earthquake turn bonus.");
        Assert.That(source.isPlaying, Is.False);
        Assert.That(bank.ActionsLocked, Is.False);
        AssertFreshPeople(System.Array.Empty<CivilGameJam.Cards.DraggableCard>());
        yield return null;
    }

    [UnityTest]
    public IEnumerator CancellingTheRoarOrHiddenEarthquakeClearsAudioAndCannotTriggerALateDisasterOrReward()
    {
        var source = Field<AudioSource>(sequence, "earthquakeRoar");
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var people = game.Cards.Where(card => card.IsPerson).ToArray();
        var body = houses[0].Building.Find("Apartment_Motion/Apartments_Normal/Apartment1");
        var position = body.localPosition;
        var rotation = body.localRotation;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            round.Ready();
            Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Earthquake));
            Assert.That(earthquake.ElapsedSeconds, Is.Zero);
            Assert.That(source.isPlaying, Is.False);
            Step(attempt == 1 ? 2.2f : .5f);
            Assert.That(source.isPlaying, Is.EqualTo(attempt == 1));
            if (attempt == 2) sequence.enabled = false;
            else sequence.StopSequence();
            round.AdvanceRound(0f);
            Invoke(navigation, "AdvanceNavigation", 2f);
            sequence.enabled = true;
            Step(20f);
            Assert.That(source.isPlaying || sequence.IsRunning || earthquake.IsRunning || monster.Model.gameObject.activeSelf, Is.False);
            Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Preparation));
            Assert.That(round.RoundNumber, Is.EqualTo(1));
            Assert.That(bank.CurrentPoints, Is.EqualTo(10));
            Assert.That(bank.ActionsLocked || navigation.InputBlocked, Is.False);
            Assert.That(game.Cards.Where(card => card.IsPerson), Is.EquivalentTo(people));
            Assert.That(body.localPosition, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(body.localRotation, rotation), Is.LessThan(.001f));
            Assert.That(round.CanReady, Is.True);
        }
        earthquake.StartSequenceEarthquake();
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        Assert.That(earthquake.TestDuration, Is.EqualTo(walk.WalkDuration + Field<float>(earthquake, "afterLastStepDelay")),
            "The audio duration must not replace the visible monster's step timing.");
        earthquake.StopEarthquake();
        earthquake.StartOffscreenEarthquake();
        Assert.That(earthquake.TestDuration, Is.EqualTo(Field<float>(earthquake, "duration")),
            "A standalone earthquake without a recording keeps its Inspector duration.");
        earthquake.StopEarthquake();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ReadyResultsWaitForTheLastHouseAnimationAndItsRemainingDust()
    {
        var results = sequence.GetComponent<MonsterSequenceResults>();
        var feedback = houses.Select(house => house.Building.GetComponent<BuildingCollapseFeedback>()).ToArray();
        var animations = houses.Select(house => house.Building.GetComponent<ApartmentEarthquakeAnimation>()).ToArray();
        round.Ready();
        Until(() => houses.Any(house => house.House.IsDestroyed));
        Assert.That(results.IsVisible, Is.False);
        round.AdvanceRound(round.NextTurnAfterCollapseSeconds + 1f);
        Assert.That(round.RoundNumber, Is.EqualTo(1), "The minimum timer cannot skip the ongoing fall.");
        Assert.That(animations.Any(animation => animation.IsRunning), Is.True);
        Assert.That(results.IsVisible, Is.False);
        Until(() => !earthquake.IsRunning);
        Assert.That(animations.All(animation => !animation.IsRunning), Is.True);
        Assert.That(feedback.Any(item => item.DustParticleCount > 0), Is.True, "The final cloud must dissipate after the pose finishes.");
        Assert.That(results.IsVisible, Is.False);
        Assert.That(bank.ActionsLocked && navigation.InputBlocked, Is.True);
        Step(.25f);
        Assert.That(sequence.IsEarthquakeRoarPlaying, Is.False, "The original-speed roar ends naturally with the quake.");
        Assert.That(results.IsVisible, Is.False, "Ending the audio cannot skip the remaining dust.");
        CaptureWorld("results-wait-for-last-collapse-dust.png");
        Until(() => round.RoundNumber == 2);
        Assert.That(feedback.All(item => !item.IsPlaying), Is.True);
        Assert.That(sequence.IsEarthquakeRoarPlaying, Is.False);
        Assert.That(results.IsVisible, Is.True);
        Assert.That(results.DestroyedHouseCount, Is.EqualTo(4));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20));
        round.AdvanceRound(100f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(20), "Waiting for dust cannot duplicate the AP payout.");
        yield return null;
    }
}
