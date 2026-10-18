using System.Collections;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BoardMusicPlayModeTests
{
    private CardGameController game;
    private EarthquakeRoundController round;
    private MonsterSequenceController sequence;
    private WorkersTutorial tutorial;
    private AudioSource intro, loop;
    private AudioClip shortIntro, shortLoop;

    [UnitySetUp]
    public IEnumerator LoadBoard()
    {
        shortIntro = AudioClip.Create("Board test intro", 22050, 1, 44100, false);
        shortLoop = AudioClip.Create("Board test loop", 11025, 1, 44100, false);
        SceneManager.sceneLoaded += ShortenMusic;
        try { yield return SceneManager.LoadSceneAsync("2DCardScene"); }
        finally { SceneManager.sceneLoaded -= ShortenMusic; }
        yield return null;
        game = Object.FindFirstObjectByType<CardGameController>();
        round = Object.FindFirstObjectByType<EarthquakeRoundController>();
        sequence = Object.FindFirstObjectByType<MonsterSequenceController>();
        tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
    }

    private void ShortenMusic(Scene scene, LoadSceneMode mode)
    {
        var tutorial = Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include);
        if (tutorial != null) tutorial.gameObject.SetActive(true);
        var music = Object.FindFirstObjectByType<BoardMusic>();
        Assert.That(music, Is.Not.Null, "The saved gameplay scene must include board music.");
        var voices = music.GetComponents<AudioSource>();
        intro = voices.Single(source => source.clip != null && source.clip.name.StartsWith("normal song start loop"));
        loop = voices.Single(source => source.clip != null && source.clip.name.StartsWith("normal song loop"));
        Assert.That(intro.loop, Is.False);
        Assert.That(loop.loop, Is.True);
        Assert.That(loop.spatialBlend, Is.Zero, "Camera movements must not attenuate board music.");
        Assert.That(voices.All(source => !source.playOnAwake), Is.True);
        intro.clip = shortIntro; loop.clip = shortLoop;
        intro.volume = loop.volume = 0f;
    }

    [TearDown]
    public void RestoreAudioAndTime()
    {
        Time.timeScale = 1f;
        SceneManager.sceneLoaded -= ShortenMusic;
        if (intro != null) intro.Stop();
        if (loop != null) loop.Stop();
        Object.Destroy(shortIntro);
        Object.Destroy(shortLoop);
    }

    [UnityTest]
    public IEnumerator TutorialKeepsLoopingAfterIntroEvenWhenGameTimeIsPaused()
    {
        Assert.That(tutorial.IsOpen && game.ActionsLocked, Is.True);
        round.Ready(); // The tutorial has not allowed READY yet.
        Assert.That(round.IsRoundActive, Is.False);
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(intro.isPlaying, Is.False);
        Assert.That(loop.isPlaying, Is.True);
        Time.timeScale = 0f;
        int previousSample = loop.timeSamples, wraps = 0;
        float until = Time.realtimeSinceStartup + 1.2f;
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
            Assert.That(loop.isPlaying, Is.True);
            if (loop.timeSamples < previousSample) wraps++;
            previousSample = loop.timeSamples;
        }
        Assert.That(wraps, Is.GreaterThanOrEqualTo(2), "The loop must repeat without replaying the intro.");
        Assert.That(intro.isPlaying, Is.False);
        tutorial.DismissImmediate();
        yield return null;
        Assert.That(loop.isPlaying, Is.True);
        Assert.That(intro.isPlaying, Is.False, "Closing the tutorial must not restart preparation music.");
    }

    [UnityTest]
    public IEnumerator ReadyDuringIntroCancelsBothVoicesIncludingTheScheduledLoop()
    {
        tutorial.DismissImmediate();
        Assert.That(intro.isPlaying, Is.True);
        Assert.That(round.CanReady, Is.True);
        round.Ready();
        Assert.That(round.IsRoundActive, Is.True);
        Assert.That(intro.isPlaying || loop.isPlaying, Is.False, "READY must stop music in the same call.");
        yield return new WaitForSecondsRealtime(.9f);
        Assert.That(intro.isPlaying || loop.isPlaying, Is.False, "The pending loop must not start during battle.");
    }

    [UnityTest]
    public IEnumerator ReadyDuringLoopKeepsResultsSilentAndNextPreparationRestartsMusic()
    {
        tutorial.DismissImmediate();
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(loop.isPlaying, Is.True);
        round.Ready();
        Assert.That(round.IsRoundActive, Is.True);
        Assert.That(intro.isPlaying || loop.isPlaying, Is.False);
        float deadline = Time.realtimeSinceStartup + 25f;
        while (!round.IsResultsVisible && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(round.IsResultsVisible, Is.True);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(intro.isPlaying || loop.isPlaying, Is.False);
        sequence.Results.Retry();
        yield return null;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(round.IsRoundActive || round.IsResultsVisible, Is.False);
        Assert.That(intro.isPlaying, Is.True, "A new preparation turn starts with the board intro.");
        game.gameObject.SetActive(false);
        Assert.That(intro.isPlaying || loop.isPlaying, Is.False, "Leaving the board must stop its music.");
    }
}
