using System.Linq;
using System.Collections;
using System.IO;
using System.Reflection;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class CardGamePlayModeTests
{
    private static ProgressBarFeedback CompletionFor(BuildingStation station)
    {
        var effect = station.transform.Find("ProgressBar").GetComponent<ProgressBarFeedback>();
        effect.enabled = false; // Drive the same presentation clock deterministically, independently of frame stalls.
        return effect;
    }

    private static void TickCompletion(ProgressBarFeedback effect, float seconds) =>
        typeof(ProgressBarFeedback).GetMethod("AdvancePresentation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, new object[] { seconds });

    private static void AssertCompletionFlashAndFade(ProgressBarFeedback effect)
    {
        var group = effect.GetComponent<CanvasGroup>();
        var fill = (Image)typeof(ProgressBarFeedback).GetField("fill", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);
        var originalColor = fill.color;
        var ids = effect.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray();
        Assert.That(effect.IsCompleting, Is.True);
        Assert.That(effect.gameObject.activeSelf, Is.True);
        Assert.That(fill.type == Image.Type.Filled ? fill.fillAmount : fill.rectTransform.anchorMax.x, Is.EqualTo(1f));
        TickCompletion(effect, 0.07f);
        Assert.That(fill.color.grayscale, Is.GreaterThan(originalColor.grayscale));
        Assert.That(fill.GetComponent<Outline>().enabled, Is.True);
        Assert.That(group.alpha, Is.EqualTo(1f));
        TickCompletion(effect, 0.23f);
        Assert.That(group.alpha, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(effect.gameObject.activeSelf, Is.True);
        TickCompletion(effect, effect.CompletionSeconds);
        Assert.That(effect.IsCompleting, Is.False);
        Assert.That(effect.gameObject.activeSelf, Is.False);
        Assert.That(group.alpha, Is.Zero);
        Assert.That(fill.color, Is.EqualTo(originalColor));
        Assert.That(fill.GetComponent<Outline>().enabled, Is.False);
        Assert.That(effect.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()), Is.EqualTo(ids));
    }

    [Test]
    public void SchoolGraduatesImmediatelyWhileItsSavedBarFlashesThenFades()
    {
        var effect = CompletionFor(school);
        school.TryAssign(People()[0]);
        school.AdvanceWork(game.EducationSeconds + 0.01f);
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        Assert.That(school.WorkerCount, Is.Zero);
        AssertCompletionFlashAndFade(effect);
    }

    [Test]
    public void ResearchChoiceRemainsAvailableAfterItsCompletedBarFades()
    {
        var effect = CompletionFor(research);
        research.TryAssign(People()[0]);
        research.AdvanceWork(game.NormalResearchSeconds + 0.01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        AssertCompletionFlashAndFade(effect);
        Assert.That(research.AwaitingBlueprint, Is.True);
        game.SelectBlueprint(0);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
    }

    [Test]
    public void FactoryProducesImmediatelyAndNewOrdersCancelThePreviousOutro()
    {
        var factory = FactoryWithWorkers(1, 0);
        var effect = CompletionFor(Station(CardKind.Factory));
        var fill = factory.transform.Find("ProgressBar/BlueProgress").GetComponent<Image>();
        factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
        Assert.That(effect.IsCompleting, Is.True);
        TickCompletion(effect, 0.3f);
        Assert.That(effect.GetComponent<CanvasGroup>().alpha, Is.LessThan(1f));
        var nextPlan = ResearchPlan(1);
        Assert.That(Station(CardKind.Factory).TryAssign(People().First(person => person.CanStartWork && !person.IsStored)), Is.True);
        Assert.That(factory.TryInsertBlueprint(nextPlan), Is.True);
        Assert.That(effect.IsCompleting, Is.False);
        Assert.That(effect.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        Assert.That(BarProgress(fill), Is.Zero);
        factory.AdvanceWork(factory.ProductionSeconds * 0.027f);
        Assert.That(BarProgress(fill), Is.EqualTo(0.027f).Within(0.00001f));
        factory.AdvanceWork(factory.ProductionSeconds);
        AssertCompletionFlashAndFade(effect);
    }

    [Test]
    public void SchoolResumesTheRemainingStudentsActualProgressAfterTheOutro()
    {
        var effect = CompletionFor(school);
        school.TryAssign(People()[0]);
        school.AdvanceWork(game.EducationSeconds * .4f);
        school.TryAssign(People().First(person => person.AssignedTo == null));
        school.AdvanceWork(game.EducationSeconds * .6f + .01f);
        Assert.That(school.WorkerCount, Is.EqualTo(1));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        var fill = school.transform.Find("ProgressBar/BlueProgress").GetComponent<Image>();
        Assert.That(BarProgress(fill), Is.EqualTo(1f));
        TickCompletion(effect, effect.CompletionSeconds + 0.01f);
        Assert.That(effect.gameObject.activeSelf, Is.True);
        Assert.That(BarProgress(fill), Is.EqualTo(school.GetEducationProgress(1)).Within(0.00001f));
        Assert.That(effect.GetComponent<CanvasGroup>().alpha, Is.Zero);
        TickCompletion(effect, 0.06f);
        Assert.That(effect.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f).And.LessThan(1f));
        TickCompletion(effect, 0.1f);
        Assert.That(effect.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
    }

    [Test]
    public void EveryDisasterCompletesItsWorkBeforeTheSavedBarFinishesFading()
    {
        foreach (var owner in new MonoBehaviour[] { Object.FindFirstObjectByType<EarthquakeTestController>(),
            Object.FindFirstObjectByType<FireTestController>(), Object.FindFirstObjectByType<TsunamiTestController>() })
        {
            var effect = ControlField<ProgressBarFeedback>(owner, "progressFeedback");
            effect.enabled = false;
            if (owner is EarthquakeTestController quake) { quake.StartEarthquake(); InvokeCity(quake, "AdvanceShake", 100f); }
            if (owner is FireTestController fire) { fire.StartFire(); InvokeCity(fire, "AdvanceFire", 100f); }
            if (owner is TsunamiTestController tsunami) { tsunami.StartTsunami(); InvokeCity(tsunami, "AdvanceTsunami", 100f); }
            Assert.That((bool)owner.GetType().GetProperty("IsRunning").GetValue(owner), Is.False);
            Assert.That(ControlField<Button>(owner, "testButton").interactable, Is.True);
            AssertCompletionFlashAndFade(effect);
        }
    }

    [UnityTest]
    public IEnumerator CaptureProgressCompletionFlashAndFade()
    {
        var factory = FactoryWithWorkers(1, 0);
        var station = Station(CardKind.Factory);
        var effect = CompletionFor(station);
        factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        TickCompletion(effect, 0.07f);
        yield return CaptureCanvas(Path.Combine(directory, "progress-completion-flash.png"), station.gameObject);
        TickCompletion(effect, 0.23f);
        yield return CaptureCanvas(Path.Combine(directory, "progress-completion-fade.png"));
        TickCompletion(effect, effect.CompletionSeconds);
        yield return CaptureCanvas(Path.Combine(directory, "progress-completion-hidden.png"));
    }
}
