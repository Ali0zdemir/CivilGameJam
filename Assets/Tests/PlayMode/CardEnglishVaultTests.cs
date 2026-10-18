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
        research.TryAssign(People()[1]);
        research.AdvanceWork(25.01f);
        Vector3 origin = research.Card.Rect.position;
        game.SelectBlueprint(0);
        var plan = game.Cards.Single(card => card.Kind == CardKind.Blueprint);
        var target = game.Vault.SlotFor(UpgradeKind.RailSystem);
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
        Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        Assert.That(game.Vault.SlotFor(UpgradeKind.Fire).CardRect.gameObject.activeSelf, Is.False);
        Canvas.ForceUpdateCanvases();
        // The moving plan has its own Canvas. Check the saved preview after
        // arrival, where its depth shares the neighbours' Canvas and is comparable.
        var artwork = target.CardRect.GetComponentInChildren<UpgradeIcon>();
        foreach (var backdrop in game.Vault.Area.GetComponentsInChildren<UnityEngine.UI.Image>()
            .Where(image => image.transform.parent == game.Vault.Area && image.name.StartsWith("BlueprintSlot")))
            Assert.That(artwork.depth, Is.GreaterThan(backdrop.depth),
                "The arrived blueprint must remain in front of neighbouring opaque slot backgrounds: " + backdrop.name);
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
        var actual = new Vector3[4]; slot.EntryImage.rectTransform.GetWorldCorners(actual);
        for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(corners[i], actual[i]), Is.LessThan(0.01f));
        Assert.That(slot.IsEntering, Is.False);
        Assert.That(slot.IsReturning, Is.True);
        Assert.That(entering.GetComponent<CanvasGroup>().alpha, Is.Zero);
        yield return new WaitForSecondsRealtime(0.55f);
        AssertRectInside(entering.Rect, game.Sidebar.PeopleArea);
    }

    [UnityTest]
    public IEnumerator BothPocketsReverseTheFolderEntryAndReturnToPeopleInEveryWorkplace()
    {
        var people = People().Take(2).ToArray();
        var savedUI = UIIds(game);
        foreach (var kind in new[] { CardKind.ResearchCenter, CardKind.Factory, CardKind.School })
        {
            var station = Station(kind);
            var slots = station.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
            var home = people.Select(person => person.Rect.localPosition).ToArray();
            foreach (var person in people) Assert.That(station.TryAssign(person), Is.True);
            yield return new WaitForSecondsRealtime(0.6f);
            var corners = slots.Select(slot => WorldCorners(slot.Portrait.rectTransform)).ToArray();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            for (int i = 0; i < 2; i++)
            {
                slots[i].OnPointerClick(pointer);
                AssertSameCorners(corners[i], slots[i].EntryImage.rectTransform);
                Assert.That(slots[i].IsReturning, Is.True);
                Assert.That(slots[i].EntryImage.transform.parent, Is.SameAs(slots[i].transform));
                Assert.That(slots[i].EntryImage.GetComponent<Canvas>().overrideSorting, Is.False);
                Assert.That(people[i].IsFlying, Is.True);
                Assert.That(people[i].GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(people[i].GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(game.Sidebar.Owns(people[i]), Is.True, "Reserve the destination while its saved image performs the exit.");
                slots[i].OnPointerClick(pointer);
            }
            Assert.That(station.WorkerCount, Is.Zero);
            Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
            var dock = game.GetComponent<CardDock>();
            dock.TickPointer(new Vector2(Screen.width - 1f, Screen.height - 1f), 0.4f);
            Assert.That(dock.IsExpanded, Is.True, "Keep the folder visible until both people have returned.");
            yield return new WaitForSecondsRealtime(0.08f);
            foreach (var slot in slots)
                Assert.That(station.Card.Rect.InverseTransformPoint(slot.EntryImage.rectTransform.position).y,
                    Is.GreaterThan(station.Card.Rect.InverseTransformPoint(slot.Portrait.rectTransform.position).y));
            Vector2 position = station.Card.Rect.anchoredPosition;
            Vector3 scale = station.Card.LayoutScale;
            var points = slots.Select(slot => station.Card.Rect.InverseTransformPoint(slot.EntryImage.rectTransform.position)).ToArray();
            station.Card.Rect.anchoredPosition += new Vector2(50f, 75f);
            station.Card.SetLayoutScale(scale * 1.08f);
            for (int i = 0; i < 2; i++)
                Assert.That(Vector3.Distance(station.Card.Rect.InverseTransformPoint(slots[i].EntryImage.rectTransform.position), points[i]), Is.LessThan(0.01f));
            yield return new WaitForSecondsRealtime(0.17f);
            foreach (var slot in slots)
            {
                Assert.That(slot.IsReturning, Is.True);
                Assert.That(slot.EntryImage.transform.parent, Is.SameAs(station.Card.Rect));
                Assert.That(slot.EntryImage.GetComponent<Canvas>().overrideSorting, Is.True, "The person must clear the folder flap before flying home.");
            }
            if (kind == CardKind.Factory)
                yield return CaptureCanvas(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/reverse-worker-exit.png"));
            yield return new WaitForSecondsRealtime(0.4f);
            for (int i = 0; i < 2; i++)
            {
                Assert.That(slots[i].IsAnimating, Is.False);
                Assert.That(slots[i].EntryImage.gameObject.activeSelf, Is.False);
                Assert.That(slots[i].Portrait.gameObject.activeSelf, Is.False);
                Assert.That(people[i].IsFlying, Is.False);
                Assert.That(people[i].CanShowHover, Is.True);
                Assert.That(people[i].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(people[i].GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Assert.That(Vector3.Distance(people[i].Rect.localPosition, home[i]), Is.LessThan(0.01f));
                Assert.That(people[i].Rect.localScale, Is.EqualTo(people[i].LayoutScale));
                AssertRectInside(people[i].Rect, game.Sidebar.PeopleArea);
            }
            station.Card.SetLayoutScale(scale);
            station.Card.Rect.anchoredPosition = position;
            Assert.That(UIIds(game), Is.EqualTo(savedUI), "Both directions must reuse the editor-authored images.");
        }
    }

    [UnityTest]
    public IEnumerator ReversingDuringEitherEntryPhaseKeepsTheExactVisiblePersonPose()
    {
        var worker = People()[0];
        var slot = school.GetComponentsInChildren<WorkerSlot>(true).OrderBy(item => item.name).First();
        foreach (float delay in new[] { 0.07f, 0.4f })
        {
            Drag(worker, school.Card.Rect.position);
            yield return new WaitForSecondsRealtime(delay);
            Assert.That(slot.IsEntering, Is.True);
            var corners = WorldCorners(slot.EntryImage.rectTransform);
            school.ReleaseWorker(0);
            AssertSameCorners(corners, slot.EntryImage.rectTransform);
            Assert.That(slot.IsEntering, Is.False);
            Assert.That(slot.IsReturning, Is.True);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(slot.IsAnimating, Is.False);
            Assert.That(worker.IsReturningFromFolder, Is.False);
            AssertRectInside(worker.Rect, game.Sidebar.PeopleArea);
        }
    }

    [UnityTest]
    public IEnumerator FolderReturnHandlesPauseCancellationAndSceneExitWithoutHiddenWorkers()
    {
        var factory = Station(CardKind.Factory);
        var people = People().Take(2).ToArray();
        var slots = factory.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
        foreach (var person in people) factory.TryAssign(person);
        yield return new WaitForSecondsRealtime(0.6f);
        float timeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            factory.ReleaseWorker(0); factory.ReleaseWorker(1);
            yield return new WaitForSecondsRealtime(0.08f);
            people[0].StopFlight();
            Assert.That(slots[0].IsReturning, Is.False);
            Assert.That(people[0].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            people[1].gameObject.SetActive(false);
            Assert.That(slots[1].IsReturning, Is.False);
            people[1].gameObject.SetActive(true);
            Assert.That(people[1].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            factory.TryAssign(people[0]);
            yield return new WaitForSecondsRealtime(0.6f);
            factory.ReleaseWorker(0);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(people[0].IsReturningFromFolder, Is.False, "Return motion must use unscaled time.");
            AssertRectInside(people[0].Rect, game.Sidebar.PeopleArea);
        }
        finally { Time.timeScale = timeScale; }
        factory.TryAssign(people[0]);
        yield return new WaitForSecondsRealtime(0.6f);
        factory.ReleaseWorker(0);
        factory.Card.gameObject.SetActive(false);
        Assert.That(people[0].IsReturningFromFolder, Is.False);
        Assert.That(people[0].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        factory.Card.gameObject.SetActive(true);
        Assert.That(slots[0].EntryImage.gameObject.activeSelf, Is.False);
        factory.TryAssign(people[0]);
        factory.ReleaseWorker(0);
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("2DCardScene");
        yield return null;
        var loaded = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(loaded.Cards.Count, Is.EqualTo(11));
        Assert.That(loaded.Cards.Any(card => card.IsReturningFromFolder), Is.False);
        Assert.That(loaded.Cards.Where(card => card.IsPerson).All(card => card.GetComponent<CanvasGroup>().alpha == 1f), Is.True);
    }

    [UnityTest]
    public IEnumerator GraduatesAndEducatedResearchersLeaveThroughTheirOwnFolderPockets()
    {
        var people = People().Take(2).ToArray();
        foreach (var person in people) school.TryAssign(person);
        yield return new WaitForSecondsRealtime(0.6f);
        var slots = school.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
        var corners = slots.Select(slot => WorldCorners(slot.Portrait.rectTransform)).ToArray();
        school.AdvanceWork(25.01f);
        var educated = game.Cards.Where(card => card.Kind == CardKind.EducatedPerson).ToArray();
        Assert.That(educated.Length, Is.EqualTo(2));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(2));
        for (int i = 0; i < 2; i++)
        {
            AssertSameCorners(corners[i], slots[i].EntryImage.rectTransform);
            Assert.That(slots[i].IsReturning, Is.True);
            Assert.That(slots[i].EntryImage.sprite, Is.SameAs(educated[i].transform.Find("Artwork").GetComponent<UnityEngine.UI.Image>().sprite));
        }
        yield return new WaitForSecondsRealtime(0.6f);
        foreach (var worker in educated)
        {
            Assert.That(worker.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            AssertRectInside(worker.Rect, game.Sidebar.PeopleArea);
            research.TryAssign(worker);
        }
        yield return new WaitForSecondsRealtime(0.6f);
        research.AdvanceWork(15.01f);
        game.SelectBlueprint(0);
        Assert.That(research.GetComponentsInChildren<WorkerSlot>(true).All(slot => slot.IsReturning), Is.True);
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(2));
        foreach (var worker in educated)
        {
            Assert.That(worker.IsReturningFromFolder, Is.False);
            AssertRectInside(worker.Rect, game.Sidebar.PeopleArea);
        }
    }

    private static Vector3[] WorldCorners(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners;
    }

    private static void AssertSameCorners(Vector3[] expected, RectTransform actual)
    {
        var corners = WorldCorners(actual);
        for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(expected[i], corners[i]), Is.LessThan(0.01f));
    }

    [UnityTest]
    public IEnumerator CancellingAndReturningADiscoveryFlightLeavesOnePlanAndNoGhostCards()
    {
        research.TryAssign(People()[0]); research.TryAssign(People()[1]);
        research.AdvanceWork(25.01f); game.SelectBlueprint(2);
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
