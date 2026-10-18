using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    private static string FeedbackCapture(string name)
    {
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/DisasterFeedbackTuningWork/Previews"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name + ".png");
    }

    [UnityTest]
    public IEnumerator CollapseSoundFourStartsOnTheDamageFrameOnceForEarthquakeAndTsunami()
    {
        var links = CityHouseLinks();
        var rails = ProduceCityProducts(UpgradeKind.RailSystem, 2);
        var elevators = ProduceCityProducts(UpgradeKind.Breakwater, 2);
        Assert.That(links[1].House.TryInstall(rails[0]), Is.True);
        Assert.That(links[2].House.TryInstall(rails[1]), Is.True);
        Assert.That(links[2].House.TryInstall(elevators[0]), Is.True);
        Assert.That(links[3].House.TryInstall(elevators[1]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        for (var i = 0; i < links.Length; i++)
        {
            var animation = links[i].Building.GetComponent<ApartmentEarthquakeAnimation>();
            var feedback = links[i].Building.GetComponent<BuildingCollapseFeedback>();
            var source = ControlField<AudioSource>(feedback, "collapseAudio");
            Assert.That(source, Is.Not.Null);
            Assert.That(source.clip.name, Is.EqualTo("afet sonucu bina yıkılma sesi 4"));
            Assert.That(source.volume, Is.EqualTo(.048f).Within(.00001f));
            Assert.That(source.clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            Assert.That(source.playOnAwake || source.loop, Is.False);
            Assert.That(source.spatialBlend, Is.Zero, "Camera distance must not delay or mute the collapse.");
            Assert.That(animation.BeginEarthquake(), Is.True);
            animation.SetEarthquakeProgress(.25f);
            Assert.That(source.isPlaying, Is.False, "Shaking alone must remain silent.");
            for (var step = 251; step <= 1000; step++)
            {
                animation.SetEarthquakeProgress(step / 1000f);
                Assert.That(source.isPlaying, Is.EqualTo(feedback.Reported), "Audio must start in the same call that first reports visible collapse.");
                if (feedback.Reported) break;
            }
            if (animation.IsProtected)
            {
                Assert.That(source.isPlaying || feedback.Reported, Is.False, "Seismic protection prevents the destruction sound.");
                animation.ResetEarthquake();
                continue;
            }
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(Mathf.RoundToInt(ControlField<float>(feedback, "collapseSoundStartSeconds") * source.clip.frequency)),
                "Skip any leading silence in the original recording.");
            animation.ResetEarthquake();
            Assert.That(source.isPlaying, Is.False, "Cancelling or rebuilding must stop the old sound.");
            Assert.That(animation.BeginEarthquake(), Is.True);
            animation.SetEarthquakeProgress(.95f);
            Assert.That(source.isPlaying, Is.True, "A new earthquake can play the sound again.");
            source.Stop();
            feedback.FinishCollapse();
            feedback.ReportCollapse();
            Assert.That(source.isPlaying, Is.False, "Repeated dust reports cannot replay the destruction sound.");
            animation.ResetEarthquake();
            Assert.That(animation.BeginEarthquake(), Is.True);
            animation.SetEarthquakeProgress(.95f);
            feedback.enabled = false;
            Assert.That(source.isPlaying, Is.False, "Disabling a house must stop its sound.");
            feedback.enabled = true;
            animation.ResetEarthquake();
            if (links[i].Building.GetComponent<ApartmentUpgradeModel>().IsElevatorProtected) continue;
            var tsunami = links[i].Building.GetComponent<ApartmentTsunamiCollapse>();
            Assert.That(tsunami.TryCollapse(), Is.True);
            InvokeCity(tsunami, "AdvanceCollapse", tsunami.Duration * .95f);
            Assert.That(feedback.Reported, Is.True);
            Assert.That(source.isPlaying, Is.True, "An unprotected tsunami collapse uses the same authored destruction sound.");
            tsunami.ResetCollapse();
        }
    }

    [UnityTest]
    public IEnumerator ApartmentCollapseKeepsConstructionDustWithoutSpawningLossText()
    {
        var links = CityHouseLinks();
        Assert.That(links[1].House.TryInstall(ProduceCityProducts(UpgradeKind.RailSystem)[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        var animations = links.Select(link => link.Building.GetComponent<ApartmentEarthquakeAnimation>()).ToArray();
        var feedback = links.Select(link => link.Building.GetComponent<BuildingCollapseFeedback>()).ToArray();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        navigation.BeginEventFrame(animations[0], links.Select(link => link.Building).ToArray(), null, 1.3f, .1f);
        InvokeCity(navigation, "AdvanceNavigation", 1f);
        foreach (var animation in animations) { Assert.That(animation.BeginEarthquake(), Is.True); animation.SetEarthquakeProgress(.25f); }
        Assert.That(feedback.All(item => !item.Reported), Is.True, "Swaying alone must not produce collapse dust.");
        foreach (var animation in animations) animation.SetEarthquakeProgress(.95f);
        Assert.That(feedback[1].Reported, Is.False, "Seismic protection only shakes, so it produces no collapse dust.");
        Assert.That(feedback[1].DustParticleCount, Is.Zero);
        foreach (var index in new[] { 0, 2, 3 })
        {
            var item = feedback[index];
            Assert.That(item.Reported, Is.True);
            Assert.That(item.DustParticleCount, Is.GreaterThan(30));
            var effect = ControlField<GameObject>(item, "effects");
            Assert.That(effect.GetComponentsInChildren<TMP_Text>(true), Is.Empty, "Destroyed houses no longer create floating loss text.");
            Assert.That(effect.transform.IsChildOf(links[index].Building.parent), Is.False, "Transient dust cannot change cinematic city framing.");
            var puffs = effect.GetComponentsInChildren<ParticleSystem>();
            Assert.That(puffs.Length, Is.EqualTo(6));
            Assert.That(puffs.All(puff => puff.GetComponent<ParticleSystemRenderer>().sharedMaterial.name == "ConstructionDust"), Is.True);
            foreach (var puff in puffs) puff.Simulate(.18f, true, false, true);
            InvokeCity(item, "AdvanceFeedback", .3f);
            Assert.That(item.IsDustEmitting, Is.True);
        }
        CaptureCityWorldFrame(FeedbackCapture("01-collapse-dust-without-loss-text"));
        var effectIdentity = ControlField<GameObject>(feedback[0], "effects").GetInstanceID();
        animations[0].SetEarthquakeProgress(1f);
        Assert.That(feedback[0].IsDustEmitting, Is.False);
        animations[0].ResetEarthquake();
        Assert.That(feedback[0].Reported, Is.False);
        Assert.That(feedback[0].DustParticleCount, Is.Zero);
        animations[0].BeginEarthquake(); animations[0].SetEarthquakeProgress(.95f);
        Assert.That(ControlField<GameObject>(feedback[0], "effects").GetInstanceID(), Is.EqualTo(effectIdentity), "A new test reuses clean dust effects.");
        Assert.That(ControlField<GameObject>(feedback[0], "effects").GetComponentsInChildren<TMP_Text>(true), Is.Empty);
        foreach (var animation in animations) animation.ResetEarthquake();
        navigation.EndEventFocus(animations[0], .1f);
    }

    [UnityTest]
    public IEnumerator ApartmentCollapseDustContinuesThroughoutBothAnimationsThenDissipatesWithoutRestarting()
    {
        var link = CityHouseLinks()[0];
        var quake = link.Building.GetComponent<ApartmentEarthquakeAnimation>();
        var flood = link.Building.GetComponent<ApartmentTsunamiCollapse>();
        var feedback = link.Building.GetComponent<BuildingCollapseFeedback>();
        foreach (var useFlood in new[] { false, true })
        {
            quake.ResetEarthquake(); flood.ResetCollapse();
            if (useFlood) Assert.That(flood.TryCollapse(), Is.True);
            else Assert.That(quake.BeginEarthquake(), Is.True);
            var onset = 0f;
            while (!feedback.Reported && onset < .99f)
            {
                onset += .01f;
                if (useFlood) InvokeCity(flood, "AdvanceCollapse", flood.Duration * .01f);
                else quake.SetEarthquakeProgress(onset);
            }
            Assert.That(feedback.IsDustEmitting, Is.True);
            var puffs = ControlField<GameObject>(feedback, "effects").GetComponentsInChildren<ParticleSystem>();
            var particles = new ParticleSystem.Particle[128];
            var count = puffs[0].GetParticles(particles);
            var originalSeeds = particles.Take(count).Select(particle => particle.randomSeed).ToArray();
            foreach (var puff in puffs) puff.Simulate(.3f, true, false, true);
            InvokeCity(feedback, "AdvanceFeedback", .3f);
            count = puffs[0].GetParticles(particles);
            var appendedSeeds = particles.Take(count).Select(particle => particle.randomSeed).ToArray();
            Assert.That(appendedSeeds.Intersect(originalSeeds).Any(), Is.True, "Adding dust preserves the existing cloud instead of clearing it each pulse.");
            Assert.That(appendedSeeds.Except(originalSeeds).Any(), Is.True, "Fresh particles replenish the cloud.");

            var seconds = useFlood ? flood.Duration * (.99f - onset) : 5f;
            for (var step = 1; step <= 50; step++)
            {
                if (useFlood) InvokeCity(flood, "AdvanceCollapse", seconds / 50f);
                else quake.SetEarthquakeProgress(Mathf.Lerp(onset, .99f, step / 50f));
                foreach (var puff in puffs) puff.Simulate(seconds / 50f, true, false, true);
                InvokeCity(feedback, "AdvanceFeedback", seconds / 50f);
                Assert.That(feedback.DustParticleCount, Is.GreaterThan(0), "The cloud cannot disappear before the animation finishes.");
            }
            Assert.That(feedback.IsDustEmitting, Is.True);
            CaptureCityWorldFrame(FeedbackCapture(useFlood ? "07-flood-continuing-dust" : "06-quake-continuing-dust"));
            if (useFlood) InvokeCity(flood, "AdvanceCollapse", flood.Duration);
            else quake.FinishEarthquake();
            Assert.That(feedback.IsDustEmitting, Is.False);
            Assert.That(feedback.DustParticleCount, Is.GreaterThan(0), "Already emitted dust dissipates naturally at the end.");
            foreach (var puff in puffs) puff.Simulate(2f, true, false, true);
            InvokeCity(feedback, "AdvanceFeedback", 2f);
            if (useFlood) InvokeCity(flood, "AdvanceCollapse", .5f);
            else quake.SetEarthquakeProgress(1f);
            Assert.That(feedback.DustParticleCount, Is.Zero, "A completed or held wreck cannot restart dust emission.");
        }
        flood.ResetCollapse(); quake.ResetEarthquake();
        Assert.That(feedback.IsDustEmitting, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ApartmentFloodDustWaitsForCollapseAndReusesTheEarthquakeCloudWithoutLossText()
    {
        var link = CityHouseLinks()[0];
        var quake = link.Building.GetComponent<ApartmentEarthquakeAnimation>();
        var flood = link.Building.GetComponent<ApartmentTsunamiCollapse>();
        var feedback = link.Building.GetComponent<BuildingCollapseFeedback>();
        Assert.That(flood.TryCollapse(), Is.True);
        Assert.That(feedback.Reported, Is.False);
        InvokeCity(flood, "AdvanceCollapse", flood.Duration * .65f);
        Assert.That(feedback.Reported, Is.True);
        Assert.That(feedback.DustParticleCount, Is.GreaterThan(30));
        var effects = ControlField<GameObject>(feedback, "effects");
        var identity = effects.GetInstanceID();
        Assert.That(effects.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
        InvokeCity(feedback, "AdvanceFeedback", 1f);
        Assert.That(quake.BeginEarthquake(), Is.False);
        Assert.That(feedback.Reported && feedback.IsDustEmitting, Is.True);
        Assert.That(ControlField<GameObject>(feedback, "effects").GetInstanceID(), Is.EqualTo(identity));
        flood.ResetCollapse();
        Assert.That(feedback.Reported, Is.False);
        quake.BeginEarthquake(); quake.SetEarthquakeProgress(.95f);
        InvokeCity(feedback, "AdvanceFeedback", 1.5f);
        Assert.That(flood.TryCollapse(), Is.True);
        InvokeCity(flood, "AdvanceCollapse", flood.Duration * .8f);
        Assert.That(feedback.Reported && feedback.IsDustEmitting, Is.True);
        Assert.That(ControlField<GameObject>(feedback, "effects").GetInstanceID(), Is.EqualTo(identity), "Flood takeover keeps the existing dust effects.");
        Assert.That(effects.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
        flood.ResetCollapse();
        Assert.That(feedback.DustParticleCount, Is.Zero);
        Assert.That(feedback.Reported || feedback.IsDustEmitting, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MonsterFootstepSoundsStartOnContactChooseTheSurfaceAndNeverReplayOldLandings()
    {
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        var feedback = walk.GetComponent<MonsterFootstepFeedback>();
        var voices = ControlField<AudioSource[]>(feedback, "footstepAudio");
        var water = ControlField<AudioClip[]>(feedback, "waterSounds");
        var land = ControlField<AudioClip[]>(feedback, "landSounds");
        Assert.That(voices.Length, Is.EqualTo(8));
        Assert.That(voices.All(source => !source.playOnAwake && !source.loop && source.spatialBlend == 0f && source.pitch == 1f), Is.True);
        Assert.That(water.Concat(land).All(clip => clip.loadState == AudioDataLoadState.Loaded), Is.True);
        walk.BeginWalk();
        var feet = new[] { ControlField<Transform>(walk, "leftFoot"), ControlField<Transform>(walk, "rightFoot") };
        walk.SetWalkTime(walk.StepDuration - .001f);
        Assert.That(voices.Any(source => source.isPlaying), Is.False, "An airborne foot must not make a contact sound.");
        for (var step = 1; step <= walk.StepCount; step++)
        {
            var previousWater = feedback.WaterFootfalls;
            var side = (step - 1) % 2;
            walk.SetWalkTime((step - .1f) * walk.StepDuration);
            var airborneHeight = feet[side].position.y;
            walk.SetWalkTime(step * walk.StepDuration);
            var playCount = ControlField<int>(feedback, "nextFootstepAudio");
            Assert.That(playCount, Is.EqualTo(step), "Each sampled contact must start one sound immediately.");
            var source = voices[(playCount - 1) % voices.Length];
            Assert.That(source.isPlaying, Is.True);
            var onWater = feedback.WaterFootfalls > previousWater;
            var clips = onWater ? water : land;
            Assert.That(clips, Does.Contain(source.clip), "The sound family must match the water ripple or ground dust.");
            var starts = ControlField<int[]>(feedback, onWater ? "waterSoundStarts" : "landSoundStarts");
            Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(starts[System.Array.IndexOf(clips, source.clip)]), "Skip recorded pre-roll before the impact.");
            Assert.That(feet[side].position.y, Is.LessThan(airborneHeight), "The moving foot must descend into its contact pose before playing the impact, including on a sloping route.");
            walk.SetWalkTime(step * walk.StepDuration);
            if (step < walk.StepCount) walk.SetWalkTime((step + .9f) * walk.StepDuration);
            Assert.That(ControlField<int>(feedback, "nextFootstepAudio"), Is.EqualTo(playCount), "Holding a pose or lifting the next foot must not replay the impact.");
        }
        Assert.That(feedback.WaterFootfalls > 0 && feedback.LandFootfalls > 0, Is.True);
        walk.CancelWalk();
        Assert.That(voices.All(source => !source.isPlaying), Is.True);
        walk.BeginWalk();
        walk.SetWalkTime(walk.WalkDuration);
        Assert.That(ControlField<int>(feedback, "nextFootstepAudio"), Is.EqualTo(1), "A skipped frame plays the latest contact without queuing historical sounds late.");
        Assert.That(feedback.WaterFootfalls + feedback.LandFootfalls, Is.EqualTo(walk.StepCount), "Visual footprints still catch up to the walk pose.");
        walk.BeginAfterEventWalk();
        InvokeCity(walk, "AdvanceAfterEventWalk", walk.StepDuration * .99f);
        Assert.That(ControlField<int>(feedback, "nextFootstepAudio"), Is.EqualTo(1));
        InvokeCity(walk, "AdvanceAfterEventWalk", walk.StepDuration * .01f);
        Assert.That(ControlField<int>(feedback, "nextFootstepAudio"), Is.EqualTo(2));
        Assert.That(land, Does.Contain(voices[1].clip), "The departure route must also use the correct contact sound.");
        feedback.enabled = false;
        Assert.That(voices.All(source => !source.isPlaying), Is.True);
        feedback.enabled = true;
        walk.CancelWalk();
        walk.CompleteWalk();
        Assert.That(voices.All(source => !source.isPlaying), Is.True, "Instant fire setup must not synthesize a trail of step sounds.");
        walk.CancelWalk();
        yield return null;
    }

    [UnityTest]
    public IEnumerator MonsterLandingsCreateExpandingSeaRingsThenLighterLandDustWithoutAirborneRepeats()
    {
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        var feedback = walk.GetComponent<MonsterFootstepFeedback>();
        var monster = walk.GetComponent<SeaMonsterEmergence>();
        walk.BeginWalk();
        walk.SetWalkTime(walk.StepDuration * .9f);
        Assert.That(feedback.WaterFootfalls + feedback.LandFootfalls, Is.Zero);
        walk.SetWalkTime(walk.StepDuration);
        Assert.That(feedback.WaterFootfalls, Is.EqualTo(1));
        Assert.That(feedback.DustParticleCount, Is.Zero);
        var effects = ControlField<GameObject>(feedback, "effects");
        var ring = effects.GetComponentsInChildren<LineRenderer>().Single(line => line.enabled);
        var first = ring.GetPosition(0);
        var opposite = ring.GetPosition(ring.positionCount / 2);
        var diameter = Vector3.Distance(first, opposite);
        var alpha = ring.startColor.a;
        Assert.That(first.y, Is.EqualTo(opposite.y).Within(.0001f), "Ripples lie on the water, not vertically in front of the camera.");
        InvokeCity(feedback, "AdvanceEffects", .45f);
        Assert.That(Vector3.Distance(ring.GetPosition(0), ring.GetPosition(ring.positionCount / 2)), Is.GreaterThan(diameter * 2f));
        Assert.That(ring.startColor.a, Is.LessThan(alpha));
        var sea = ControlField<Transform>(feedback, "seaSurface");
        Assert.That(ring.GetPosition(0).y, Is.GreaterThan(sea.GetComponent<MeshRenderer>().bounds.max.y));
        CaptureCityWorldFrame(FeedbackCapture("02-sea-footstep"));
        walk.SetWalkTime(walk.StepDuration);
        Assert.That(feedback.WaterFootfalls, Is.EqualTo(1));
        for (var step = 2; step <= walk.StepCount; step++) walk.SetWalkTime(walk.StepDuration * step);
        Assert.That(feedback.WaterFootfalls, Is.GreaterThan(0));
        Assert.That(feedback.LandFootfalls, Is.GreaterThan(0));
        Assert.That(feedback.WaterFootfalls + feedback.LandFootfalls, Is.EqualTo(walk.StepCount));
        var collapse = CityHouseLinks()[0].Building.GetComponent<BuildingCollapseFeedback>();
        collapse.ReportCollapse();
        Assert.That(feedback.DustParticleCount, Is.GreaterThan(0).And.LessThan(collapse.DustParticleCount));
        collapse.ResetFeedback();
        foreach (var puff in effects.GetComponentsInChildren<ParticleSystem>()) puff.Simulate(.15f, true, false, true);
        CaptureCityWorldFrame(FeedbackCapture("03-land-footstep"));
        InvokeCity(feedback, "AdvanceEffects", 1.4f);
        Assert.That(feedback.ActiveRipples, Is.Zero);
        var previousLandings = feedback.LandFootfalls;
        walk.BeginAfterEventWalk();
        InvokeCity(walk, "AdvanceAfterEventWalk", walk.AfterEventWalkDuration);
        Assert.That(feedback.LandFootfalls, Is.GreaterThan(previousLandings));
        Assert.That(monster.Model.gameObject.activeSelf, Is.False);
        Assert.That(effects.activeInHierarchy, Is.True, "Last foot dust can dissipate after the boss disappears.");
        walk.CancelWalk();
        Assert.That(feedback.DustParticleCount + feedback.ActiveRipples, Is.Zero);
        walk.CompleteWalk();
        Assert.That(feedback.WaterFootfalls + feedback.LandFootfalls, Is.Zero, "Instant fire-test setup must not fabricate a trail of footsteps.");
        walk.CancelWalk();
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisasterCameraHasTighterManualBoundsAndTheSameClosestZoom()
    {
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(3f).Within(.001f));
        CaptureCityWorldFrame(FeedbackCapture("04-closer-default-camera"));
        PresentationField(navigation, "targetPan", new Vector2(100f, 100f));
        PresentationField(navigation, "targetZoom", 1000f);
        InvokeCity(navigation, "AdvanceNavigation", 4f);
        Assert.That(navigation.PanOffset.x, Is.EqualTo(1.8f).Within(.001f));
        Assert.That(navigation.PanOffset.y, Is.EqualTo(1.2f).Within(.001f));
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(3f).Within(.001f));
        PresentationField(navigation, "targetPan", new Vector2(-100f, -100f));
        PresentationField(navigation, "targetZoom", .0001f);
        InvokeCity(navigation, "AdvanceNavigation", 4f);
        Assert.That(navigation.PanOffset.x, Is.EqualTo(-1.8f).Within(.001f));
        Assert.That(navigation.PanOffset.y, Is.EqualTo(-1.2f).Within(.001f));
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(1.5f).Within(.001f));
        navigation.ResetView();
        yield return null;
    }
}
