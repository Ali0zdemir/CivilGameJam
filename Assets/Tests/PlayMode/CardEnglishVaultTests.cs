using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator VaultStartsEmptyThenTheDiscoveredBlueprintFliesFromResearchToItsSavedSlot()
    {
        foreach (var slot in game.Vault.Slots)
        {
            Assert.That(slot.CardRect.gameObject.activeSelf, Is.False);
            Hover(slot.gameObject, true);
            yield return null;
            Assert.That(slot.GetComponent<CardHover>().Details.gameObject.activeSelf, Is.False);
            Hover(slot.gameObject, false);
        }
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        yield return CaptureCanvas(Path.Combine(directory, "english-empty-vault.png"));
        var worker = People()[0];
        research.TryAssign(worker);
        research.AdvanceWork(25.01f);
        Vector3 origin = research.Card.Rect.position;
        game.SelectBlueprint(1);
        var plan = game.Cards.Single(card => card.Kind == CardKind.Blueprint);
        var target = game.Vault.SlotFor(UpgradeKind.Breakwater);
        Assert.That(plan.StoredInVault, Is.SameAs(game.Vault));
        Assert.That(plan.IsFlying, Is.True);
        Assert.That(Vector3.Distance(plan.Rect.position, origin), Is.LessThan(0.01f));
        Assert.That(target.CardRect.gameObject.activeSelf, Is.False, "Do not show the destination card before its arrival.");
        Assert.That(target.CanShowHover, Is.False);
        yield return new WaitForSecondsRealtime(0.12f);
        Assert.That(Vector3.Distance(plan.Rect.position, origin), Is.GreaterThan(1f));
        var dock = game.GetComponent<CardDock>();
        dock.SetExpanded(false, true);
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(plan.gameObject.activeSelf, Is.False);
        Assert.That(target.CardRect.gameObject.activeSelf, Is.True);
        Assert.That(target.IsArriving, Is.False);
        Assert.That(game.Vault.AvailableCount(UpgradeKind.Breakwater), Is.EqualTo(1));
        Assert.That(game.Vault.SlotFor(UpgradeKind.Fire).CardRect.gameObject.activeSelf, Is.False);
        dock.SetExpanded(true, true);
        Hover(target.gameObject, true);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(target.GetComponent<CardHover>().Details.gameObject.activeSelf, Is.True);
        Hover(target.gameObject, false);
        yield return CaptureCanvas(Path.Combine(directory, "english-discovered-vault.png"));
    }

    [UnityTest]
    public IEnumerator WorkerEntryStaysAttachedToEachMovingAndResizingBuildingInBothPockets()
    {
        var duration = typeof(WorkerSlot).GetField("entrySeconds", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var kind in new[] { CardKind.ResearchCenter, CardKind.Factory, CardKind.School })
        {
            var station = Station(kind);
            var slots = station.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
            foreach (var slot in slots) duration.SetValue(slot, 2f);
            var people = People().Take(2).ToArray();
            for (int i = 0; i < 2; i++) Drag(people[i], station.Card.Rect.position);
            yield return new WaitForSecondsRealtime(0.06f);
            var points = slots.Select(slot => station.Card.Rect.InverseTransformPoint(slot.EntryImage.rectTransform.position)).ToArray();
            Vector2 position = station.Card.Rect.anchoredPosition;
            Vector3 scale = station.Card.LayoutScale;
            station.Card.Rect.anchoredPosition += new Vector2(90f, 140f);
            station.Card.SetLayoutScale(scale * 1.2f);
            for (int i = 0; i < 2; i++)
                Assert.That(Vector3.Distance(station.Card.Rect.InverseTransformPoint(slots[i].EntryImage.rectTransform.position), points[i]),
                    Is.LessThan(0.01f), kind + ": the entry image must travel with its building immediately.");
            yield return null;
            for (int i = 0; i < 2; i++)
                Assert.That(Vector3.Distance(station.Card.Rect.InverseTransformPoint(slots[i].EntryImage.rectTransform.position), points[i]),
                    Is.LessThan(100f), kind + ": the next animation frame must not jump back to the old coordinate space.");
            var dock = game.GetComponent<CardDock>();
            dock.TickPointer(new Vector2(Screen.width - 1f, Screen.height - 1f), 0.4f);
            Assert.That(dock.IsExpanded, Is.True, "Keep receiving buildings visible until the people have entered.");
            station.ReleaseWorker(0, false); station.ReleaseWorker(1, false);
            station.Card.SetLayoutScale(scale);
            station.Card.Rect.anchoredPosition = position;
            Assert.That(slots.All(slot => !slot.IsEntering && !slot.EntryImage.gameObject.activeSelf), Is.True);
        }
    }

    [UnityTest]
    public IEnumerator RegrabbingARejectedWorkerAndReleasingAnEnteringWorkerNeverSnapsTheirArtwork()
    {
        var people = People();
        school.TryAssign(people[0]); school.TryAssign(people[1]);
        var worker = people[2];
        Drag(worker, school.Card.Rect.position);
        yield return new WaitForSecondsRealtime(0.08f);
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left, pointerId = -1,
            position = RectTransformUtility.WorldToScreenPoint(null, worker.Rect.position)
        };
        Vector3 heldPosition = worker.Rect.position;
        worker.OnPointerDown(pointer); worker.OnBeginDrag(pointer); worker.OnDrag(pointer);
        Assert.That(Vector3.Distance(worker.Rect.position, heldPosition), Is.LessThan(0.01f));
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, school.Card.Rect.position);
        worker.OnDrag(pointer); worker.OnEndDrag(pointer);
        Assert.That(Vector3.Distance(worker.Rect.position, school.Card.Rect.position), Is.LessThan(0.01f));
        yield return new WaitForSecondsRealtime(0.55f);
        AssertRectInside(worker.Rect, game.Sidebar.PeopleArea);
        var entering = people[3];
        Drag(entering, research.Card.Rect.position);
        yield return new WaitForSecondsRealtime(0.1f);
        var slot = research.GetComponentsInChildren<WorkerSlot>(true).Single(item => item.IsEntering);
        var corners = new Vector3[4]; slot.EntryImage.rectTransform.GetWorldCorners(corners);
        research.ReleaseWorker(0);
        var actual = new Vector3[4]; ((RectTransform)entering.transform.Find("Artwork")).GetWorldCorners(actual);
        for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(corners[i], actual[i]), Is.LessThan(0.01f));
        Assert.That(slot.IsEntering, Is.False);
        yield return new WaitForSecondsRealtime(0.55f);
        AssertRectInside(entering.Rect, game.Sidebar.PeopleArea);
    }

    [UnityTest]
    public IEnumerator CancellingAndReturningADiscoveryFlightLeavesOnePlanAndNoGhostCards()
    {
        research.TryAssign(People()[0]); research.AdvanceWork(25.01f); game.SelectBlueprint(2);
        var plan = game.Cards.Single(card => card.Kind == CardKind.Blueprint);
        var slot = game.Vault.SlotFor(UpgradeKind.Fire);
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>(); factory.enabled = false;
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(slot.IsArriving, Is.False);
        Assert.That(plan.gameObject.activeSelf, Is.False);
        factory.ReleaseBlueprint();
        Assert.That(slot.IsArriving, Is.True);
        yield return new WaitForSecondsRealtime(0.55f);
        Assert.That(slot.IsArriving, Is.False);
        Assert.That(slot.CardRect.gameObject.activeSelf, Is.True);
        Assert.That(plan.gameObject.activeSelf, Is.False);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Blueprint), Is.EqualTo(1));
        Assert.That(game.Vault.AvailableCount(UpgradeKind.Fire), Is.EqualTo(1));
    }
}
