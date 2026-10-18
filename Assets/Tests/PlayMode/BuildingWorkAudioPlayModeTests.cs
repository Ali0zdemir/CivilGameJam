using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class BuildingWorkAudioPlayModeTests
{
    private sealed class Voices
    {
        public AudioSource intro, loop, outro;
        public bool IsPlaying => intro.isPlaying || loop.isPlaying || outro.isPlaying;
    }

    private readonly Dictionary<CardKind, Voices> voices = new Dictionary<CardKind, Voices>();
    private CardGameController game;
    private BuildingStation school, research, factoryStation;
    private FactoryProduction factory;
    private AudioClip introClip, loopClip, endClip, tutorialEndClip;

    [UnitySetUp]
    public IEnumerator LoadBoard()
    {
        introClip = AudioClip.Create("Work test intro", 8820, 1, 44100, false);
        loopClip = AudioClip.Create("Work test loop", 6615, 1, 44100, false);
        endClip = AudioClip.Create("Work test outro", 11025, 1, 44100, false);
        voices.Clear();
        SceneManager.sceneLoaded += PrepareScene;
        try { yield return SceneManager.LoadSceneAsync("2DCardScene"); }
        finally { SceneManager.sceneLoaded -= PrepareScene; }
        yield return null;
        game = Object.FindFirstObjectByType<CardGameController>();
        game.AbilityPoints.AddPoints(1000);
        game.RestrictResearchTo(UpgradeKind.None);
        school = Station(CardKind.School);
        research = Station(CardKind.ResearchCenter);
        factoryStation = Station(CardKind.Factory);
        factory = factoryStation.GetComponent<FactoryProduction>();
    }

    private void PrepareScene(Scene scene, LoadSceneMode mode)
    {
        var tutorial = Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include);
        if (tutorial != null) tutorial.enabled = false;
        var buildings = Object.FindObjectsByType<BuildingWorkAudio>(FindObjectsSortMode.None);
        Assert.That(buildings.Length, Is.EqualTo(3), "All three saved card prefabs must supply their own work sounds.");
        foreach (var building in buildings)
        {
            var kind = building.GetComponent<DraggableCard>().Kind;
            string prefix = kind == CardKind.Factory ? "factory" : kind == CardKind.School ? "school" : "searching";
            var sources = building.GetComponents<AudioSource>();
            var sound = new Voices
            {
                intro = sources.Single(source => source.clip != null && source.clip.name == prefix + " start loop"),
                loop = sources.Single(source => source.clip != null && source.clip.name == prefix + " loop"),
                outro = sources.Single(source => source.clip != null && source.clip.name == prefix + " end loop")
            };
            Assert.That(sound.intro.loop || sound.outro.loop, Is.False);
            Assert.That(sound.loop.loop, Is.True);
            Assert.That(sources.All(source => !source.playOnAwake && source.spatialBlend == 0f), Is.True);
            sound.intro.clip = introClip; sound.loop.clip = loopClip; sound.outro.clip = endClip;
            voices.Add(kind, sound);
        }
        foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) source.volume = 0f;
    }

    [TearDown]
    public void Cleanup()
    {
        Time.timeScale = 1f;
        SceneManager.sceneLoaded -= PrepareScene;
        foreach (var sound in voices.Values) { sound.intro.Stop(); sound.loop.Stop(); sound.outro.Stop(); }
        Object.Destroy(introClip); Object.Destroy(loopClip); Object.Destroy(endClip);
        if (tutorialEndClip != null) Object.Destroy(tutorialEndClip);
    }

    private BuildingStation Station(CardKind kind) => game.Cards.Single(card => card.Kind == kind).Station;
    private DraggableCard Person() => game.Cards.First(card => card.Kind == CardKind.Person && card.CanStartWork && !card.IsStored);

    private DraggableCard Discover(UpgradeKind kind)
    {
        if (kind != UpgradeKind.RailSystem) game.CompleteBlueprint(UpgradeKind.RailSystem);
        Assert.That(research.TryAssign(Person()), Is.True);
        research.AdvanceWork(game.NormalResearchSeconds + .01f);
        game.SelectBlueprint((int)kind - 1);
        return game.Cards.Last(card => card.Kind == CardKind.Blueprint && card.UpgradeType == kind);
    }

    private static IEnumerator AssertIntroAndRepeatingLoop(Voices sound)
    {
        Assert.That(sound.intro.isPlaying, Is.True);
        Assert.That(sound.outro.isPlaying, Is.False);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(sound.intro.isPlaying, Is.False);
        Assert.That(sound.loop.isPlaying, Is.True);
        int previousSample = sound.loop.timeSamples, wraps = 0;
        float deadline = Time.realtimeSinceStartup + .5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            Assert.That(sound.loop.isPlaying, Is.True);
            if (sound.loop.timeSamples < previousSample) wraps++;
            previousSample = sound.loop.timeSamples;
        }
        Assert.That(wraps, Is.GreaterThanOrEqualTo(1));
    }

    private static IEnumerator AssertSingleOutro(Voices sound)
    {
        Assert.That(sound.intro.isPlaying || sound.loop.isPlaying, Is.False);
        Assert.That(sound.outro.isPlaying, Is.True);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(sound.IsPlaying, Is.False, "Idle refreshes must not replay the outro.");
    }

    [UnityTest]
    public IEnumerator EachWorkplacePlaysItsOwnIntroLoopAndSingleOutro()
    {
        Assert.That(school.TryAssign(Person()), Is.True);
        yield return AssertIntroAndRepeatingLoop(voices[CardKind.School]);
        school.AdvanceWork(game.EducationSeconds + .01f);
        yield return AssertSingleOutro(voices[CardKind.School]);

        Assert.That(research.TryAssign(Person()), Is.True);
        yield return AssertIntroAndRepeatingLoop(voices[CardKind.ResearchCenter]);
        research.AdvanceWork(game.NormalResearchSeconds + .01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        yield return AssertSingleOutro(voices[CardKind.ResearchCenter]);
        game.SelectBlueprint(0);
        Assert.That(voices[CardKind.ResearchCenter].IsPlaying, Is.False, "Selecting a completed plan must not play a second ending.");

        var blueprint = game.Cards.Single(card => card.Kind == CardKind.Blueprint && card.UpgradeType == UpgradeKind.RailSystem);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        Assert.That(factory.TryInsertBlueprint(blueprint), Is.True);
        yield return AssertIntroAndRepeatingLoop(voices[CardKind.Factory]);
        factory.AdvanceWork(factory.ProductionSeconds + .01f);
        Assert.That(factory.PendingBlueprint, Is.Null);
        yield return AssertSingleOutro(voices[CardKind.Factory]);
    }

    [UnityTest]
    public IEnumerator FactoryWaitsForBlueprintAndRequiredWorkersAndStopsWhenOneLeaves()
    {
        Assert.That(voices.Values.All(sound => !sound.IsPlaying), Is.True);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(voices[CardKind.Factory].IsPlaying, Is.False, "An idle worker must not start factory sound.");
        var blueprint = Discover(UpgradeKind.Breakwater);
        Assert.That(factory.TryInsertBlueprint(blueprint), Is.True);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(factory.Progress, Is.Zero);
        Assert.That(voices[CardKind.Factory].IsPlaying, Is.False, "Two-worker plans must stay silent until staffed.");
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        yield return AssertIntroAndRepeatingLoop(voices[CardKind.Factory]);
        var worker = factoryStation.ReleaseWorker(1, false);
        yield return AssertSingleOutro(voices[CardKind.Factory]);
        Assert.That(factoryStation.TryAssign(worker), Is.True);
        yield return AssertIntroAndRepeatingLoop(voices[CardKind.Factory]);
        Assert.That(factory.ReleaseBlueprint(false), Is.SameAs(blueprint));
        yield return AssertSingleOutro(voices[CardKind.Factory]);
    }

    [UnityTest]
    public IEnumerator SchoolKeepsOneLoopForStaggeredStudentsUntilTheLastStudentFinishes()
    {
        Assert.That(school.TryAssign(Person()), Is.True);
        school.AdvanceWork(game.EducationSeconds * .4f);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That(school.TryAssign(Person()), Is.True);
        Assert.That(voices[CardKind.School].intro.isPlaying, Is.False, "Adding a second student must not replay the intro.");
        school.AdvanceWork(game.EducationSeconds * .6f + .01f);
        Assert.That(school.WorkerCount, Is.EqualTo(1));
        Assert.That(voices[CardKind.School].loop.isPlaying, Is.True);
        Assert.That(voices[CardKind.School].outro.isPlaying, Is.False);
        school.AdvanceWork(game.EducationSeconds);
        yield return AssertSingleOutro(voices[CardKind.School]);
    }

    [UnityTest]
    public IEnumerator CancellingDuringIntroCancelsThePendingLoopAndDisablingTheCardStopsAllVoices()
    {
        var student = Person();
        Assert.That(school.TryAssign(student), Is.True);
        Assert.That(voices[CardKind.School].intro.isPlaying, Is.True);
        school.ReleaseWorker(0, false);
        yield return AssertSingleOutro(voices[CardKind.School]);
        Assert.That(school.TryAssign(student), Is.True);
        yield return AssertIntroAndRepeatingLoop(voices[CardKind.School]);
        school.gameObject.SetActive(false);
        Assert.That(voices[CardKind.School].IsPlaying, Is.False);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(voices[CardKind.School].IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator PauseAndActionLocksSilenceWorkWithoutLeavingScheduledVoicesBehind()
    {
        Assert.That(research.TryAssign(Person()), Is.True);
        yield return AssertIntroAndRepeatingLoop(voices[CardKind.ResearchCenter]);
        Time.timeScale = 0f;
        yield return null;
        Assert.That(voices[CardKind.ResearchCenter].IsPlaying, Is.False);
        Time.timeScale = 1f;
        yield return null;
        game.AbilityPoints.SetActionsLocked(true);
        yield return null;
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(voices[CardKind.ResearchCenter].IsPlaying, Is.False);
        game.AbilityPoints.SetActionsLocked(false);
        yield return null;
        yield return AssertIntroAndRepeatingLoop(voices[CardKind.ResearchCenter]);
    }

    [UnityTest]
    public IEnumerator TutorialCompletionPlaysSchoolOutroEvenWhenTheNextLessonLocksActions()
    {
        var tutorial = Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include);
        tutorial.enabled = true;
        tutorial.gameObject.SetActive(true);
        tutorial.ShowTutorial();
        tutorial.Next();
        Assert.That(tutorial.WaitingForEducation, Is.True);
        tutorialEndClip = AudioClip.Create("Tutorial work outro", 88200, 1, 44100, false);
        voices[CardKind.School].outro.clip = tutorialEndClip;
        Assert.That(school.TryAssign(tutorial.PracticeStudent), Is.True);
        school.AdvanceWork(game.EducationSeconds + .01f);
        Assert.That(voices[CardKind.School].outro.isPlaying, Is.True);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!tutorial.EducationCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(tutorial.EducationCompleted && game.ActionsLocked, Is.True);
        Assert.That(voices[CardKind.School].outro.isPlaying, Is.True, "The next lesson must not cut off the ending.");
        Assert.That(voices[CardKind.School].intro.isPlaying || voices[CardKind.School].loop.isPlaying, Is.False);
        yield return new WaitForSecondsRealtime(2.1f);
        Assert.That(voices[CardKind.School].IsPlaying, Is.False);
    }
}
