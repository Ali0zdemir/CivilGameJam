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
    public IEnumerator LiveEarthquakeFinishesItsStartLoopEndAudioWithTheQuakeAndKeepsRuinsUntilTheReportCloses()
    {
        var source = Field<AudioSource>(earthquake, "earthquakeAudio");
        var loop = Field<AudioSource>(earthquake, "loopAudio");
        var ending = Field<AudioSource>(earthquake, "endAudio");
        var results = sequence.Results;
        var bodies = houses.Select(house => house.Building.Find("Apartment_Motion/Apartments_Normal/Apartment1")).ToArray();
        var rest = bodies.Select(body => body.localPosition).ToArray();
        var started = Time.realtimeSinceStartup;
        var lastQuake = started; var lastAudio = started;
        var heardAudio = false; var heardLoop = false; var heardEnding = false;
        round.Ready();
        while (!results.IsVisible && Time.realtimeSinceStartup - started < 18f)
        {
            if (earthquake.IsEarthquakeAudioPlaying)
            {
                if (!heardAudio) Assert.That(earthquake.ElapsedSeconds, Is.InRange(0f, .3f));
                heardAudio = true;
                lastAudio = Time.realtimeSinceStartup;
            }
            heardLoop |= loop.isPlaying && loop.timeSamples > 0;
            heardEnding |= ending.isPlaying && ending.timeSamples > 0;
            if (earthquake.IsRunning) lastQuake = Time.realtimeSinceStartup;
            yield return null;
        }
        Assert.That(results.IsVisible, Is.True, "The real scene must complete without manual time advances.");
        Assert.That(heardAudio && heardLoop && heardEnding, Is.True, "The opening, continuous rumble and ending must all play in the live scene.");
        Assert.That(source.pitch, Is.EqualTo(1f).Within(.00001f));
        Assert.That(Mathf.Abs(lastAudio - lastQuake), Is.LessThan(.3f), "Earthquake audio must finish with the shaking.");
        Assert.That(earthquake.IsEarthquakeAudioPlaying || !sequence.HouseEffectsFinished, Is.False);
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
    public IEnumerator FourSeismicHousesHearTheEarthquakeSurviveAndContinueStraightIntoTheSynchronizedTsunami()
    {
        bank.AddPoints(4);
        PrepareSeismic(2);
        var rails = houses.Select(house => house.House.GetInstalled(CivilGameJam.Cards.UpgradeKind.RailSystem)).ToArray();
        yield return new WaitForSecondsRealtime(1.6f);
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var water = Field<TsunamiTestController>(round, "tsunami");
        var wave = Field<AuthoredTsunamiWave>(water, "authoredWave");
        round.Ready();
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Earthquake));
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.True, "Seismic protection must not silence the earthquake.");
        Step(2.01f);
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.True);
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
        Assert.That(bank.CurrentPoints, Is.Zero, "Earthquake rewards must wait until the uninterrupted tsunami is over.");
        Assert.That(bank.ActionsLocked && sequence.IsRunning && water.IsRunning, Is.True);
        Assert.That(round.CanReady || sequence.GetComponent<MonsterSequenceResults>().IsVisible, Is.False);
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Emerging));
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.False, "Earthquake audio ends before the next stage's entrance.");
        Assert.That(houses.Select(house => house.House.GetInstalled(CivilGameJam.Cards.UpgradeKind.RailSystem)), Is.EqualTo(rails));
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(research.AbilityPointCost(CivilGameJam.Cards.CardKind.Person), Is.EqualTo(9));
        Assert.That(factoryStation.AbilityPointCost(CivilGameJam.Cards.CardKind.Person), Is.EqualTo(10));
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
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.False);
        CaptureWorld("roar-followed-by-monster-and-wave.png");
        Until(() => round.RoundNumber == 3);
        Assert.That(round.SequenceNumber, Is.EqualTo(2), "Losing to water cannot undo the earthquake victory.");
        Assert.That(round.LastRoundSurvived, Is.False);
        Assert.That(round.LastProtectedHouseCount, Is.Zero);
        Assert.That(round.LastSurvivalReward, Is.Zero);
        Assert.That(bank.CurrentPoints, Is.EqualTo(24), "The deferred 18 earthquake AP and 6 tsunami retry AP arrive together at the final report.");
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
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.False, "A water-stage retry starts without an early earthquake sound.");
        sequence.StopSequence(); round.AdvanceRound(0f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(24));
    }

    [UnityTest]
    public IEnumerator EarthquakeRewardsSurviveTsunamiCancellationAndPayOnlyOnceAtTheCompletedReport()
    {
        bank.AddPoints(4);
        PrepareSeismic(2);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready();
        Until(() => round.SequenceNumber == 2);
        Assert.That(bank.CurrentPoints, Is.Zero);
        sequence.StopSequence(); round.AdvanceRound(0f);
        Invoke(navigation, "AdvanceNavigation", 2f);
        Assert.That(bank.CurrentPoints, Is.Zero, "STOP cannot collect an unfinished tsunami's deferred award.");
        Assert.That(sequence.Results.IsVisible, Is.False);

        round.Ready();
        Assert.That(sequence.ActiveSequence, Is.EqualTo(2));
        Until(() => Field<bool>(sequence, "tsunamiAftermathStarted"));
        Step(sequence.TsunamiAftermathSeconds - .1f);
        Assert.That(bank.CurrentPoints, Is.Zero);
        Assert.That(sequence.Results.IsVisible, Is.False);
        Until(() => sequence.Results.IsVisible);
        Assert.That(bank.CurrentPoints, Is.EqualTo(24));
        var earned = Field<TMPro.TMP_Text>(sequence.Results, "earnedPointsCount");
        Assert.That(earned.text, Does.Contain("24"), "The report must include the delayed earthquake award.");
        for (var i = 0; i < 5; i++) round.AdvanceRound(10f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(24));

        sequence.Results.Retry();
        Invoke(navigation, "AdvanceNavigation", 2f);
        round.Ready();
        Until(() => sequence.Results.IsVisible);
        Assert.That(bank.CurrentPoints, Is.EqualTo(30), "Later tsunami retries pay their own six AP without repeating the earthquake award.");
    }

    [UnityTest]
    public IEnumerator ReadyStartsEarthquakeAudioImmediatelyAndKeepsTheConfiguredShakeDuration()
    {
        var source = Field<AudioSource>(earthquake, "earthquakeAudio");
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        var water = Field<TsunamiTestController>(round, "tsunami");
        Assert.That(source, Is.Not.Null);
        Assert.That(source.clip.name, Is.EqualTo("earth quake start loop"));
        Assert.That(source.isPlaying, Is.False);
        var homePan = navigation.PanOffset;
        var homeZoom = Field<float>(navigation, "currentZoom");
        round.Ready();
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Earthquake));
        Assert.That(earthquake.IsRunning, Is.True);
        Assert.That(earthquake.ElapsedSeconds, Is.Zero);
        Assert.That(source.isPlaying && earthquake.IsEarthquakeAudioPlaying, Is.True, "Start audio in the same call as the earthquake.");
        Assert.That(source.volume, Is.EqualTo(.55f).Within(.00001f));
        Assert.That(bank.ActionsLocked, Is.True);
        Assert.That(bank.CurrentPoints, Is.EqualTo(10));
        Assert.That(navigation.PanOffset, Is.EqualTo(homePan), "City framing must start without a camera cut.");
        var loop = Field<AudioSource>(earthquake, "loopAudio");
        var ending = Field<AudioSource>(earthquake, "endAudio");
        Assert.That(loop.clip.name, Is.EqualTo("earth quake loop"));
        Assert.That(ending.clip.name, Is.EqualTo("earth quake end loop"));
        Assert.That(loop.loop && !ending.loop, Is.True);
        Assert.That(loop.clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
        Assert.That(ending.clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
        Step(1.99f);
        Assert.That(earthquake.IsRunning, Is.True);
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.True);
        Assert.That(water.IsRunning || monster.Model.gameObject.activeSelf, Is.False);
        Assert.That(walk.CompletedSteps, Is.Zero);
        Assert.That(houses.Any(house => house.House.IsDestroyed), Is.False);
        Assert.That(Field<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
        CaptureWorld("quake-city-with-earthquake-audio.png");
        Step(.02f);
        Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Earthquake));
        Assert.That(earthquake.IsRunning, Is.True);
        Assert.That(earthquake.ElapsedSeconds, Is.EqualTo(2.01f).Within(.0001f));
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.True);
        Assert.That(source.pitch, Is.EqualTo(1f).Within(.00001f), "Play the recording at its original speed.");
        Assert.That(earthquake.TestDuration, Is.EqualTo(Field<float>(earthquake, "duration")),
            "The 31-second loop must follow the configured shake duration rather than extending the disaster.");
        Assert.That(monster.Model.gameObject.activeSelf || walk.IsWalkingAfterEvent || water.IsRunning, Is.False);
        Until(() => houses.Any(house => house.House.IsDestroyed));
        CaptureWorld("roar-city-earthquake-collapse.png");
        Assert.That(monster.Model.gameObject.activeSelf || walk.IsWalkingAfterEvent || water.IsRunning, Is.False);
        Until(() => round.RoundNumber == 2);
        Assert.That(round.SequenceNumber, Is.EqualTo(1));
        Assert.That(round.LastRoundSurvived, Is.False);
        Assert.That(round.LastRoundGrant, Is.EqualTo(10));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20), "Carry the unspent AP and award one failed-earthquake turn bonus.");
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.False);
        Assert.That(bank.ActionsLocked, Is.False);
        AssertFreshPeople(System.Array.Empty<CivilGameJam.Cards.DraggableCard>());
        yield return null;
    }

    [UnityTest]
    public IEnumerator CancellingTheEarthquakeClearsAllScheduledAudioAndCannotTriggerALateDisasterOrReward()
    {
        var source = Field<AudioSource>(earthquake, "earthquakeAudio");
        var loop = Field<AudioSource>(earthquake, "loopAudio");
        var ending = Field<AudioSource>(earthquake, "endAudio");
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
            Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.True);
            Step(attempt == 1 ? 2.2f : .5f);
            Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.True);
            if (attempt == 2) sequence.enabled = false;
            else sequence.StopSequence();
            Assert.That(source.isPlaying || loop.isPlaying || ending.isPlaying, Is.False, "Stop must also cancel scheduled loop and ending voices.");
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
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.True, "Monster-driven earthquakes use the same audio lifecycle.");
        earthquake.StopEarthquake();
        earthquake.StartOffscreenEarthquake();
        Assert.That(earthquake.TestDuration, Is.EqualTo(Field<float>(earthquake, "duration")),
            "Standalone earthquakes keep their Inspector duration.");
        Assert.That(earthquake.IsEarthquakeAudioPlaying, Is.True);
        earthquake.enabled = false;
        Assert.That(source.isPlaying || loop.isPlaying || ending.isPlaying, Is.False);
        earthquake.enabled = true;
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
        Assert.That(sequence.IsEarthquakeAudioPlaying, Is.False, "The ending recording finishes with the quake.");
        Assert.That(results.IsVisible, Is.False, "Ending the audio cannot skip the remaining dust.");
        CaptureWorld("results-wait-for-last-collapse-dust.png");
        Until(() => round.RoundNumber == 2);
        Assert.That(feedback.All(item => !item.IsPlaying), Is.True);
        Assert.That(sequence.IsEarthquakeAudioPlaying, Is.False);
        Assert.That(results.IsVisible, Is.True);
        Assert.That(results.DestroyedHouseCount, Is.EqualTo(4));
        Assert.That(bank.CurrentPoints, Is.EqualTo(20));
        round.AdvanceRound(100f);
        Assert.That(bank.CurrentPoints, Is.EqualTo(20), "Waiting for dust cannot duplicate the AP payout.");
        yield return null;
    }
}
