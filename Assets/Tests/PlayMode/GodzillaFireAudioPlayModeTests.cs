using System.Collections;
using System.Linq;
using System.Reflection;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class GodzillaFireAudioPlayModeTests
{
    private FireTestController fire;
    private SeaMonsterFireBreath breath;
    private AudioSource charge, opening, loop, ending;
    private AudioSource[] voices;

    [UnitySetUp]
    public IEnumerator LoadSavedScene()
    {
        SceneManager.sceneLoaded += DismissTutorial;
        try { yield return SceneManager.LoadSceneAsync("2DCardScene"); }
        finally { SceneManager.sceneLoaded -= DismissTutorial; }
        yield return null;
        fire = Object.FindFirstObjectByType<FireTestController>();
        breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        charge = Field<AudioSource>(fire, "monsterChargeAudio");
        opening = Field<AudioSource>(fire, "monsterFireStartAudio");
        loop = Field<AudioSource>(fire, "monsterFireLoopAudio");
        ending = Field<AudioSource>(fire, "monsterFireEndAudio");
        voices = new[] { charge, opening, loop, ending };
        Assert.That(voices.All(voice => voice != null && voice.clip != null), Is.True,
            "The gameplay scene must bind the GodzillaFire assets without manual setup.");
    }

    private static void DismissTutorial(Scene scene, LoadSceneMode mode)
    {
        var tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
        if (tutorial != null) tutorial.enabled = false;
    }

    [TearDown]
    public void StopAudio()
    {
        Time.timeScale = 1f;
        if (fire != null) fire.StopFire();
        SceneManager.sceneLoaded -= DismissTutorial;
    }

    [UnityTest]
    public IEnumerator SavedFireSoundsArePreloadedAndChargeStartsInTheStartFireCall()
    {
        Assert.That(voices.Select(voice => voice.clip.name), Is.EqualTo(new[] {
            "godzilla fire scream 1", "godzilla fire blaze start loop",
            "godzilla fire blaze loop", "godzilla fire blaze end loop" }));
        foreach (var voice in voices)
        {
            Assert.That(voice.clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            Assert.That(voice.clip.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(voice.playOnAwake, Is.False);
            Assert.That(voice.spatialBlend, Is.Zero);
            Assert.That(voice.dopplerLevel, Is.Zero);
        }
        Assert.That(voices.All(voice => !voice.isPlaying), Is.True);
        Assert.That(loop.loop && !charge.loop && !opening.loop && !ending.loop, Is.True);
        fire.StartFire();
        Assert.That(fire.IsRunning && charge.isPlaying, Is.True,
            "Charge must start immediately, with no scheduled lead-in delay.");
        yield return new WaitForSecondsRealtime(.06f);
        Assert.That(charge.timeSamples, Is.GreaterThan(0));
        Assert.That(opening.timeSamples + loop.timeSamples + ending.timeSamples, Is.Zero,
            "The flame sounds wait for the existing charge and aiming animation.");
    }

    [UnityTest]
    public IEnumerator LiveBreathAudioMatchesTheVisualTimelineAndFinishesWhileTheCityStillBurns()
    {
        fire.StartFire();
        Time.timeScale = 0f; // Fire presentation uses unscaled time, including its sound.
        float firstOpening = -1f, firstLoop = -1f, firstEnding = -1f;
        double deadline = AudioSettings.dspTime + breath.BreathEndTime + ending.clip.length + .5d;
        while (AudioSettings.dspTime < deadline)
        {
            float elapsed = Field<float>(fire, "elapsed");
            if (opening.timeSamples > 0 && firstOpening < 0f) firstOpening = elapsed;
            if (loop.timeSamples > 0 && firstLoop < 0f) firstLoop = elapsed;
            if (ending.timeSamples > 0 && firstEnding < 0f) firstEnding = elapsed;
            yield return null;
        }
        Assert.That(firstOpening, Is.EqualTo(breath.FlameStartTime).Within(.2f),
            "The opening must begin when fire leaves the mouth.");
        Assert.That(firstLoop, Is.EqualTo(breath.FlameStartTime + opening.clip.length).Within(.2f),
            "The continuous blaze must follow the opening without a frame-driven gap.");
        Assert.That(firstEnding, Is.EqualTo(breath.BreathEndTime).Within(.2f),
            "The ending follows the breath, independently of the remaining city fire.");
        Assert.That(voices.Any(voice => voice.isPlaying), Is.False);
        Assert.That(fire.IsRunning, Is.True, "Street and building fire must continue after the breath ends.");
    }

    [UnityTest]
    public IEnumerator CancellingDuringChargeCancelsEveryScheduledVoiceAndRetryRestartsImmediately()
    {
        ShortenBreath();
        fire.StartFire();
        fire.StopFire();
        Assert.That(voices.Any(voice => voice.isPlaying), Is.False);
        yield return new WaitForSecondsRealtime(1f);
        Assert.That(voices.Any(voice => voice.isPlaying), Is.False,
            "Stopping during charge must cancel the pending start, loop and ending.");
        int sourceCount = fire.GetComponents<AudioSource>().Length;
        fire.StartFire();
        Assert.That(charge.isPlaying, Is.True);
        Assert.That(fire.GetComponents<AudioSource>().Length, Is.EqualTo(sourceCount));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisablingDuringTheBlazeStopsItsLoopAndPendingEnding()
    {
        SetField(breath, "chargeDuration", .1f);
        SetField(breath, "leanDuration", .1f);
        SetField(breath, "breathDuration", 2f);
        fire.StartFire();
        yield return new WaitForSecondsRealtime(breath.FlameStartTime + opening.clip.length + .15f);
        Assert.That(loop.isPlaying && loop.timeSamples > 0, Is.True);
        fire.enabled = false;
        Assert.That(voices.Any(voice => voice.isPlaying), Is.False);
        yield return new WaitForSecondsRealtime(1f);
        Assert.That(voices.Any(voice => voice.isPlaying), Is.False,
            "A pending ending must not sound after the disaster was disabled.");
        fire.enabled = true;
        fire.StartFire();
        Assert.That(charge.isPlaying, Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShortBreathCutsOffALongerOpeningAndCompletionCancelsTheEnding()
    {
        ShortenBreath();
        fire.StartFire();
        yield return new WaitForSecondsRealtime(breath.BreathEndTime + .15f);
        Assert.That(opening.isPlaying || loop.isPlaying, Is.False);
        Assert.That(ending.isPlaying && ending.timeSamples > 0, Is.True);
        fire.FinishPresentation();
        Assert.That(fire.IsRunning, Is.False);
        Assert.That(voices.Any(voice => voice.isPlaying), Is.False);
        yield return null;
    }

    private void ShortenBreath()
    {
        SetField(breath, "chargeDuration", .1f);
        SetField(breath, "leanDuration", .1f);
        SetField(breath, "breathDuration", .4f);
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
