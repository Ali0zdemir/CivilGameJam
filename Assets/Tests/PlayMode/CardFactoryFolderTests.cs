using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator FactoryBlueprintUsesTheDroppedPoseThenRequestsExactlyOneWorkerWithoutText()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        typeof(FactoryFolderPresentation).GetField("entrySeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 1f);
        var plan = ResearchPlan(0);
        yield return new WaitForSecondsRealtime(.65f);
        game.AbilityPoints.TrySpend(game.AbilityPoints.CurrentPoints - 10);
        int balance = game.AbilityPoints.CurrentPoints;
        var savedUI = UIIds(game);
        var savedText = game.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.GetInstanceID()).OrderBy(id => id).ToArray();
        game.Vault.Withdraw(plan);
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left, pointerId = -1,
            position = RectTransformUtility.WorldToScreenPoint(null, plan.Rect.position)
        };
        plan.OnPointerDown(pointer);
        plan.OnBeginDrag(pointer);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, factory.transform.position);
        plan.OnDrag(pointer);
        var artwork = plan.transform.Find("CardSymbol").GetComponent<UpgradeIcon>();
        var corners = new Vector3[4]; artwork.rectTransform.GetWorldCorners(corners);
        plan.OnEndDrag(pointer);
        var actual = new Vector3[4]; view.EntryImage.rectTransform.GetWorldCorners(actual);
        for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(corners[i], actual[i]), Is.LessThan(.02f), "No jump when the dropped card becomes the saved entry preview.");
        Assert.That(view.EntryImage.sprite, Is.SameAs(artwork.Artwork));
        Assert.That(view.IsEntering, Is.True);
        Assert.That(view.EntryImage.GetComponent<Canvas>().overrideSorting, Is.True);
        Assert.That(game.HasActiveCardAnimation, Is.True);
        Assert.That(factory.GetComponent<DraggableCard>().CanShowHover, Is.False);
        Assert.That(plan.gameObject.activeSelf, Is.False);
        Assert.That(factory.PendingBlueprint, Is.SameAs(plan));
        Assert.That(view.IsWorkerHintVisible, Is.False);
        yield return new WaitForSecondsRealtime(.74f);
        Assert.That(view.IsEntering, Is.True);
        Assert.That(view.EntryImage.GetComponent<Canvas>().overrideSorting, Is.False);
        Assert.That(view.EntryImage.transform.parent, Is.SameAs(view.StoredBlueprint.transform.parent));
        yield return new WaitForSecondsRealtime(.55f);
        Assert.That(view.IsEntering, Is.False);
        Assert.That(view.StoredBlueprint.gameObject.activeSelf, Is.True);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance), "An order waiting for its first worker spends no AP.");
        Assert.That(factory.Progress, Is.Zero);
        Assert.That(UIIds(game), Is.EqualTo(savedUI), "Entry, storage and guidance must reuse the authored hierarchy.");
        Assert.That(game.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.GetInstanceID()).OrderBy(id => id), Is.EqualTo(savedText));
        string directory = Path.Combine(Application.dataPath, "../Library/CardGameTestResults/FactoryFolder");
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "factory-needs-one-worker.png"));
        Assert.That(Station(CardKind.Factory).TryAssign(People().First(p => p.CanStartWork && !p.IsStored)), Is.True);
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(view.IsWorkerHintVisible, Is.False, "One worker is enough; the second pocket remains optional.");
        Assert.That(view.StoredBlueprint.gameObject.activeSelf, Is.True);
        yield return CaptureCanvas(Path.Combine(directory, "factory-blueprint-and-worker.png"));
        Station(CardKind.Factory).ReleaseWorker(0, false);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True, "Removing the last unfinished worker brings the guide back.");
        view.StoredBlueprint.GetComponentInParent<FactoryBlueprintSlot>().OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(view.IsWorkerHintVisible, Is.False);
        Assert.That(view.StoredBlueprint.gameObject.activeSelf, Is.False);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance));
    }

    [UnityTest]
    public IEnumerator FactoryBlueprintEntryCancelsCleanlyOnWithdrawalCompletionAndTurnReset()
    {
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>(); factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var plan = ResearchPlan(0);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.That(factory.ReleaseBlueprint(false), Is.SameAs(plan));
        AssertFactoryPocketCleared(view);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(station.TryAssign(People().First(p => p.CanStartWork && !p.IsStored)), Is.True);
        factory.AdvanceWork(100f);
        AssertFactoryPocketCleared(view);
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Upgrade), Is.EqualTo(2));
        yield return new WaitForSecondsRealtime(.65f);
        AssertFactoryPocketCleared(view);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        factory.ResetForNewTurn();
        AssertFactoryPocketCleared(view);
        Assert.That(plan.StoredInVault, Is.SameAs(game.Vault));
        yield return new WaitForSecondsRealtime(.65f);
        AssertFactoryPocketCleared(view);
    }

    [UnityTest]
    public IEnumerator FactoryBlueprintFollowsTheMovingDrawerAndUsesEachPlansOwnArtwork()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>(); factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var dock = game.GetComponent<CardDock>();
        typeof(FactoryFolderPresentation).GetField("entrySeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, 1f);
        for (int kind = 0; kind < 3; kind++)
        {
            var plan = ResearchPlan(kind);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(factory.TryInsertBlueprint(plan), Is.True);
            yield return new WaitForSecondsRealtime(.08f);
            Vector3 before = game.CardLayer.InverseTransformPoint(view.EntryImage.rectTransform.position);
            dock.SetExpanded(false, true);
            yield return null;
            Vector3 after = game.CardLayer.InverseTransformPoint(view.EntryImage.rectTransform.position);
            Assert.That(Vector3.Distance(before, after), Is.LessThan(100f), "The preview must move with the drawer, without teleporting into the city.");
            dock.SetExpanded(true, true);
            yield return new WaitForSecondsRealtime(1.15f);
            Assert.That(view.StoredBlueprint.sprite, Is.SameAs(plan.transform.Find("CardSymbol").GetComponent<UpgradeIcon>().Artwork));
            Assert.That(view.StoredBlueprint.gameObject.activeSelf, Is.True);
            factory.ReleaseBlueprint(false);
            AssertFactoryPocketCleared(view);
        }
        var active = game.Cards.First(c => c.Kind == CardKind.Blueprint);
        factory.TryInsertBlueprint(active);
        // Exiting the scene in the middle of entry must not reparent a destroyed proxy or leave a coroutine alive.
        yield return SceneManager.LoadSceneAsync("2DCardScene", LoadSceneMode.Single);
        yield return null;
        var loaded = Object.FindFirstObjectByType<FactoryFolderPresentation>();
        Assert.That(loaded, Is.Not.Null);
        AssertFactoryPocketCleared(loaded);
    }

    private static void AssertFactoryPocketCleared(FactoryFolderPresentation view)
    {
        Assert.That(view.IsEntering, Is.False);
        Assert.That(view.EntryImage.gameObject.activeSelf, Is.False);
        Assert.That(view.StoredBlueprint.gameObject.activeSelf, Is.False);
        Assert.That(view.IsWorkerHintVisible, Is.False);
        Assert.That(view.EntryImage.GetComponent<Canvas>().overrideSorting, Is.False);
    }
}
