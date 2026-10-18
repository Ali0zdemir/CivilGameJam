using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    private const int InitiallyEmptyCell = 3;
    private CardGameController game;
    private BuildingStation school;
    private BuildingStation research;

    [UnitySetUp]
    public IEnumerator LoadBoard()
    {
        yield return SceneManager.LoadSceneAsync("2DCardScene", LoadSceneMode.Single);
        yield return null;
        game = Object.FindFirstObjectByType<CardGameController>();
        // Legacy mechanics/presentation tests have a generous budget; AP-specific tests use the actual starting balance.
        game.AbilityPoints?.AddPoints(10000);
        Assert.That(game, Is.Not.Null);
        Assert.That(game.Grid, Is.Not.Null, "The serialized scene must initialize without manual setup.");
        var dock = game.GetComponent<CardDock>();
        dock.enabled = false;
        dock.SetExpanded(true, true);
        // Mechanics tests advance work directly; presentation timing has its own integration tests.
        var housePresentation = Object.FindFirstObjectByType<CivilGameJam.HouseUpgradePresentation>();
        if (housePresentation != null) housePresentation.enabled = false;
        school = Station(CardKind.School);
        research = Station(CardKind.ResearchCenter);
        // Advance the same work loop explicitly so timing assertions do not depend on machine speed.
        foreach (var card in game.Cards) if (card.Station != null) card.Station.enabled = false;
    }

    private BuildingStation Station(CardKind kind) => game.Cards.Single(c => c.Kind == kind).Station;
    private DraggableCard[] People() => game.Cards.Where(c => c.Kind == CardKind.Person).OrderBy(c => c.UsedThisTurn).ToArray();
    private DraggableCard Educated() => game.Cards.First(c => c.Kind == CardKind.EducatedPerson);

    [UnityTest]
    public IEnumerator StartupReusesSavedUIAndKeepsEditorStyling()
    {
        int[] savedUI = null;
        TextMeshProUGUI savedLabel = null;
        RectTransform savedHouseIcon = null;
        void CaptureBeforeStart(Scene scene, LoadSceneMode mode)
        {
            var board = Object.FindFirstObjectByType<CardGameController>();
            savedLabel = board.Sidebar.PeopleArea.parent.Find("Title").GetComponent<TextMeshProUGUI>();
            savedLabel.color = Color.cyan;
            savedHouseIcon = board.GetComponentsInChildren<HouseUpgradeSlot>().First()
                .GetComponentInChildren<UpgradeIcon>(true).rectTransform;
            savedHouseIcon.localScale = Vector3.one * 0.83f;
            savedUI = UIIds(board);
        }
        SceneManager.sceneLoaded += CaptureBeforeStart;
        try { yield return SceneManager.LoadSceneAsync("2DCardScene", LoadSceneMode.Single); }
        finally { SceneManager.sceneLoaded -= CaptureBeforeStart; }
        yield return null;
        var loaded = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(UIIds(loaded), Is.EqualTo(savedUI), "Start must reuse the authored UI rather than spawn new objects.");
        Assert.That(savedLabel.color, Is.EqualTo(Color.cyan));
        Assert.That(savedHouseIcon.localScale, Is.EqualTo(Vector3.one * 0.83f), "House slots must retain the editor-authored logo scale.");
        Assert.That(loaded.Cards.Count, Is.EqualTo(11));
        AssertNoTimingText(loaded.GetComponentInParent<Canvas>());
    }

    private static int[] UIIds(CardGameController board) => board.GetComponentsInChildren<Component>(true)
        .Where(c => c is Image || c is Button || c is TextMeshProUGUI || c is WorkerSlot || c is DraggableCard || c is CardGrid)
        .Select(c => c.GetInstanceID()).OrderBy(id => id).ToArray();

    private static void Hover(DraggableCard card, bool entered) => Hover(card.gameObject, entered);

    private static void AssertNoTimingText(Canvas canvas)
    {
        foreach (var label in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            Assert.That(label.text, Does.Not.Match(@"(?i)(%|\b\d+(?:[.,]\d+)?\s*(sn|san[iİıI]ye|seconds?)\b)"), label.name);
    }

    [UnityTest]
    public IEnumerator SidebarKeepsFreePeoplePositionsAndReturnsWorkersWithoutCreatingUI()
    {
        var people = People().OrderBy(card => card.name).ToArray();
        var savedUI = UIIds(game);
        var positions = people.Select(card => card.Rect.position).ToArray();
        var vaultPosition = game.Vault.Area.position;
        var dock = game.GetComponent<CardDock>();
        dock.SetExpanded(false, true);
        for (int i = 0; i < people.Length; i++)
            Assert.That(Vector3.Distance(people[i].Rect.position, positions[i]), Is.LessThan(0.01f));
        Assert.That(Vector3.Distance(game.Vault.Area.position, vaultPosition), Is.LessThan(0.01f));
        dock.SetExpanded(true, true);
        Drag(people[0], school.Card.Rect.position);
        Assert.That(people[0].AssignedTo, Is.SameAs(school));
        var area = game.Sidebar.PeopleArea;
        Vector3 freePosition = area.TransformPoint(area.rect.center + new Vector2(45f, 20f));
        Drag(people[1], freePosition);
        Assert.That(people[1].Rect.parent, Is.SameAs(area));
        Assert.That(Vector3.Distance(people[1].Rect.position, freePosition), Is.LessThan(0.01f), "Free placement must not snap to an invisible slot.");
        school.ReleaseWorker(0, false);
        Assert.That(people[0].Rect.parent, Is.SameAs(area));
        Assert.That(Vector3.Distance(people[0].Rect.position, positions[0]), Is.LessThan(0.01f));
        dock.SetExpanded(false, true);
        Assert.That(Vector3.Distance(people[1].Rect.position, freePosition), Is.LessThan(0.01f));
        dock.SetExpanded(true, true);
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        Hover(people[1], true);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(people[1].GetComponent<CardHover>().Details.gameObject.activeSelf, Is.True);
        Assert.That(game.Sidebar.Area.GetSiblingIndex(), Is.GreaterThan(game.CardLayer.GetSiblingIndex()));
        Hover(people[1], false);
    }

    [UnityTest]
    public IEnumerator FreePeopleAreaClampsEdgesAndKeepsTheGraduatesChosenPosition()
    {
        var area = game.Sidebar.PeopleArea;
        var people = People();
        Vector3 chosenPosition = area.TransformPoint(area.InverseTransformPoint(people[0].Rect.position) + Vector3.up * 20f);
        Drag(people[0], chosenPosition);
        Assert.That(Vector3.Distance(people[0].Rect.position, chosenPosition), Is.LessThan(0.01f));
        school.TryAssign(people[0]);
        school.AdvanceWork(game.EducationSeconds + 0.01f);
        yield return new WaitForSecondsRealtime(0.55f);
        var graduate = Educated();
        Assert.That(graduate.Rect.parent, Is.SameAs(area));
        Assert.That(Vector3.Distance(graduate.Rect.position, chosenPosition), Is.LessThan(0.01f),
            "Graduating must retain the player's chosen place instead of introducing a slot.");
        var person = People()[0];
        Drag(person, area.TransformPoint(new Vector2(area.rect.xMin + 2f, area.rect.yMax - 2f)));
        Assert.That(person.Rect.parent, Is.SameAs(area));
        AssertRectInside(person.Rect, area);
        Drag(person, area.TransformPoint(new Vector2(area.rect.xMax - 2f, area.rect.yMin + 2f)));
        AssertRectInside(person.Rect, area);
    }

    [UnityTest]
    public IEnumerator FolderWorkplaceAnimatesBothWorkerKindsAndReleasesOnlyTheirOwnPocket()
    {
        school.TryAssign(People()[0]);
        school.AdvanceWork(25.01f);
        yield return new WaitForSecondsRealtime(0.65f);
        var house = Station(CardKind.Factory);
        var slots = house.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
        Assert.That(slots.Length, Is.EqualTo(2));
        Assert.That(slots.All(slot => !slot.Portrait.gameObject.activeSelf && !slot.EntryImage.gameObject.activeSelf), Is.True);
        var normal = People()[0];
        var educated = Educated();
        var normalSprite = normal.transform.Find("Artwork").GetComponent<Image>().sprite;
        var educatedSprite = educated.transform.Find("Artwork").GetComponent<Image>().sprite;
        var savedUI = UIIds(game);
        Drag(normal, house.Card.Rect.position);
        Assert.That(normal.AssignedTo, Is.SameAs(house));
        Assert.That(slots[0].IsEntering, Is.True);
        Assert.That(slots[0].Portrait.gameObject.activeSelf, Is.False);
        Vector3 origin = slots[0].EntryImage.rectTransform.position;
        float initialWidth = slots[0].EntryImage.rectTransform.lossyScale.x * slots[0].EntryImage.rectTransform.rect.width;
        Drag(educated, house.Card.Rect.position);
        Assert.That(house.WorkerCount, Is.EqualTo(2));
        Assert.That(house.TryAssign(People().First(person => person.AssignedTo == null)), Is.False);
        yield return new WaitForSecondsRealtime(0.24f);
        Assert.That(Vector3.Distance(slots[0].EntryImage.rectTransform.position, origin), Is.GreaterThan(1f));
        float targetWidth = slots[0].Portrait.rectTransform.lossyScale.x * slots[0].Portrait.rectTransform.rect.width;
        float movingWidth = slots[0].EntryImage.rectTransform.lossyScale.x * slots[0].EntryImage.rectTransform.rect.width;
        Assert.That(Mathf.Abs(movingWidth - targetWidth), Is.LessThan(Mathf.Abs(initialWidth - targetWidth)),
            "The person must smoothly approach the authored pocket size from the current table icon size.");
        Assert.That(UIIds(game), Is.EqualTo(savedUI), "Entry animation must reuse the saved preview images.");
        yield return new WaitForSecondsRealtime(0.22f);
        yield return CaptureCanvas(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/station-folder-entering.png"));
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(slots.All(slot => !slot.IsEntering && slot.Portrait.gameObject.activeSelf && !slot.EntryImage.gameObject.activeSelf), Is.True);
        Assert.That(slots[0].Portrait.sprite, Is.SameAs(normalSprite));
        Assert.That(slots[1].Portrait.sprite, Is.SameAs(educatedSprite));
        house.AdvanceWork(100f);
        Assert.That(house.WorkerCount, Is.EqualTo(2));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(3));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        yield return CaptureCanvas(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/station-folder-workers.png"));
        Assert.That(house.ReleaseWorker(0, false), Is.SameAs(normal));
        Assert.That(slots[0].Portrait.gameObject.activeSelf, Is.False);
        Assert.That(slots[1].Portrait.gameObject.activeSelf, Is.True);
        Assert.That(game.Sidebar.Owns(normal), Is.True);
        slots[1].OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        Assert.That(slots[1].Portrait.gameObject.activeSelf, Is.False);
        Assert.That(house.WorkerCount, Is.Zero);
        yield return new WaitForSecondsRealtime(0.6f);
        Drag(educated, research.Card.Rect.position);
        Assert.That(educated.AssignedTo, Is.SameAs(research));
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }

    [UnityTest]
    public IEnumerator FolderEntryCanBeCancelledWithoutGhostWorkersOrSceneExitErrors()
    {
        var house = Station(CardKind.Factory);
        var slots = house.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
        var people = People();
        var savedUI = UIIds(game);
        house.TryAssign(people[0]); house.TryAssign(people[1]);
        yield return new WaitForSecondsRealtime(0.12f);
        house.ReleaseWorker(0, false);
        Assert.That(slots[0].IsEntering, Is.False);
        Assert.That(slots[0].EntryImage.gameObject.activeSelf, Is.False);
        Assert.That(slots[0].Portrait.gameObject.activeSelf, Is.False);
        Assert.That(people[0].gameObject.activeSelf, Is.True);
        Assert.That(people[0].LayoutScale, Is.EqualTo(Vector3.one));
        yield return new WaitForSecondsRealtime(0.55f);
        Assert.That(slots[1].Portrait.gameObject.activeSelf, Is.True);
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        house.TryAssign(people[2]);
        research.TryAssign(people[3]);
        research.AdvanceWork(25.01f);
        yield return null;
        Assert.That(slots[0].EntryImage.color.a, Is.Zero, "Folder entry previews must stay behind blueprint selection.");
        game.SelectBlueprint(0);
        yield return null;
        yield return SceneManager.LoadSceneAsync("2DCardScene", LoadSceneMode.Single);
        yield return null;
        var loaded = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(loaded.Cards.Count, Is.EqualTo(11));
        Assert.That(loaded.NormalPeopleCount, Is.EqualTo(4));
        Assert.That(loaded.Cards.Where(card => card.Kind == CardKind.House).All(card => card.Station == null), Is.True);
    }

    [UnityTest]
    public IEnumerator HousesRejectPeopleAndKeepTheirThreeManufacturedUpgradeSlots()
    {
        var worker = People()[0];
        var origin = worker.Rect.position;
        foreach (var house in game.Cards.Where(card => card.Kind == CardKind.House))
        {
            Drag(worker, house.Rect.position);
            Assert.That(worker.AssignedTo, Is.Null);
            Assert.That(worker.gameObject.activeSelf, Is.True);
            Assert.That(game.Sidebar.Owns(worker), Is.True);
            Assert.That(worker.IsFlying, Is.True);
            Assert.That(Vector3.Distance(worker.Rect.position, house.Rect.position), Is.LessThan(0.01f));
            yield return new WaitForSecondsRealtime(0.55f);
            Assert.That(Vector3.Distance(worker.Rect.position, origin), Is.LessThan(0.01f));
            Assert.That(house.Station, Is.Null);
            Assert.That(house.GetComponentsInChildren<WorkerSlot>(true), Is.Empty);
            Assert.That(house.GetComponentsInChildren<HouseUpgradeSlot>(true).Length, Is.EqualTo(3));
        }
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
    }

    [UnityTest]
    public IEnumerator EveryWorkplaceStartsFolderEntryAtTheExactDroppedArtworkPose()
    {
        var worker = People()[0];
        foreach (var kind in new[] { CardKind.ResearchCenter, CardKind.Factory, CardKind.School })
        {
            var station = Station(kind);
            var slot = station.GetComponentsInChildren<WorkerSlot>(true).OrderBy(item => item.name).First();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, worker.Rect.position),
                button = PointerEventData.InputButton.Left, pointerId = -1
            };
            worker.OnBeginDrag(pointer);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, station.Card.Rect.TransformPoint(new Vector3(60f, 30f)));
            worker.OnDrag(pointer);
            var originalCorners = new Vector3[4];
            worker.transform.Find("Artwork").GetComponent<RectTransform>().GetWorldCorners(originalCorners);
            var nativeUI = UIIds(game);
            worker.OnEndDrag(pointer);
            Assert.That(worker.AssignedTo, Is.SameAs(station));
            Assert.That(worker.gameObject.activeSelf, Is.False);
            Assert.That(slot.EntryImage.gameObject.activeInHierarchy, Is.True);
            var previewCorners = new Vector3[4]; slot.EntryImage.rectTransform.GetWorldCorners(previewCorners);
            for (int i = 0; i < 4; i++)
                Assert.That(Vector3.Distance(originalCorners[i], previewCorners[i]), Is.LessThan(0.01f), kind + ": no snap or resize on handoff.");
            Hover(station.Card, true);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(slot.IsEntering, Is.True);
            Assert.That(station.Card.GetComponent<CardHover>().Details.gameObject.activeSelf, Is.False);
            Assert.That(UIIds(game), Is.EqualTo(nativeUI));
            yield return new WaitForSecondsRealtime(0.36f);
            Assert.That(slot.IsEntering, Is.False);
            Assert.That(slot.Portrait.gameObject.activeSelf, Is.True);
            Assert.That(slot.EntryImage.gameObject.activeSelf, Is.False);
            Hover(station.Card, false);
            station.ReleaseWorker(0, false);
        }
    }

    [Test]
    public void SchoolResearchAndFactoryBarsAdvanceContinuouslyWithoutTimingLabels()
    {
        school.TryAssign(People()[0]);
        AssertContinuousBar(school.Card, school.AdvanceWork, game.EducationSeconds / 10f);
        research.TryAssign(People().First(person => person.AssignedTo == null));
        AssertContinuousBar(research.Card, research.AdvanceWork, game.NormalResearchSeconds / 10f);
        Object.FindFirstObjectByType<CityStatusPanel>()?.Refresh();
        AssertNoTimingText(game.GetComponentInParent<Canvas>());
        research.AdvanceWork(game.NormalResearchSeconds / 10f + 0.01f);
        Assert.That(research.AwaitingBlueprint, Is.True);
        Assert.That(BarProgress(research.transform.Find("ProgressBar/BlueProgress").GetComponent<Image>()), Is.EqualTo(1f));
        game.SelectBlueprint(0);

        var factoryStation = Station(CardKind.Factory);
        var production = factoryStation.GetComponent<FactoryProduction>();
        production.enabled = false;
        factoryStation.TryAssign(People().First(person => person.AssignedTo == null));
        production.TryInsertBlueprint(game.Cards.Last(card => card.Kind == CardKind.Blueprint));
        AssertContinuousBar(factoryStation.Card, production.AdvanceWork, production.ProductionSeconds / 10f);
        Object.FindFirstObjectByType<CityStatusPanel>()?.Refresh();
        AssertNoTimingText(game.GetComponentInParent<Canvas>());
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.Zero);
        production.AdvanceWork(production.ProductionSeconds / 10f + 0.01f);
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Upgrade), Is.EqualTo(2));
        foreach (var product in game.Cards.Where(card => card.Kind == CardKind.Upgrade))
        {
            Assert.That(game.Sidebar.Owns(product), Is.False, "Manufactured cards must leave the four people spaces available.");
            var gridCorners = new Vector3[4]; game.Grid.rectTransform.GetWorldCorners(gridCorners);
            float gridRight = game.CardLayer.InverseTransformPoint(gridCorners[3]).x;
            Assert.That(product.TablePosition.x - product.Rect.rect.width * product.LayoutScale.x / 2f,
                Is.GreaterThan(gridRight), "Manufactured cards should use the open space to the right of the grid.");
        }
        Assert.That(BarProgress(factoryStation.transform.Find("ProgressBar/BlueProgress").GetComponent<Image>()), Is.EqualTo(1f),
            "The completed bar stays full during its visual outro.");
    }

    private static void AssertContinuousBar(DraggableCard card, System.Action<float> advance, float stepDuration)
    {
        var fill = card.transform.Find("ProgressBar/BlueProgress").GetComponent<Image>();
        Assert.That(BarProgress(fill), Is.Zero);
        for (int step = 1; step < 10; step++)
        {
            advance(stepDuration * 0.45f);
            Assert.That(BarProgress(fill), Is.EqualTo((step - 1 + 0.45f) / 10f).Within(0.0001f), card.name + ": advance between former steps");
            advance(stepDuration * 0.55f);
            Assert.That(BarProgress(fill), Is.EqualTo(step / 10f).Within(0.0001f), card.name + ": stay synchronized with work");
        }
    }

    private static float BarProgress(Image fill) => fill.type == Image.Type.Filled ? fill.fillAmount : fill.rectTransform.anchorMax.x;

    private static void Hover(GameObject target, bool entered)
    {
        var pointer = new PointerEventData(EventSystem.current) { pointerId = -1 };
        if (entered) ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerEnterHandler);
        else ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerExitHandler);
    }

    [UnityTest]
    public IEnumerator VaultCardsReuseSavedInformationAndSmoothHoverWhileRemainingDraggable()
    {
        var plan = ResearchPlan(0);
        var slot = game.Vault.SlotFor(UpgradeKind.RailSystem);
        var preview = slot.CardRect;
        var details = slot.GetComponent<CardHover>().Details;
        var label = details.Find("Information/Title").GetComponent<TextMeshProUGUI>();
        label.text = "Özel Vault başlığı";
        var savedUI = UIIds(game);
        var scale = preview.localScale;
        var position = preview.anchoredPosition;
        int sibling = game.Vault.Area.GetSiblingIndex();
        Hover(slot.gameObject, true);
        yield return null;
        yield return new WaitForSecondsRealtime(0.06f);
        Assert.That(preview.localScale.x / scale.x, Is.GreaterThan(1f).And.LessThan(1.21125f));
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(Vector3.Distance(preview.localScale, scale * 1.21125f), Is.LessThan(0.001f));
        Assert.That(details.gameObject.activeSelf, Is.True);
        Assert.That(label.text, Is.EqualTo("Özel Vault başlığı"));
        Assert.That(game.Sidebar.Area.GetSiblingIndex(), Is.GreaterThan(game.CardLayer.GetSiblingIndex()), "Side information must render above the board.");
        Assert.That(UIIds(game), Is.EqualTo(savedUI), "Hover must use the saved cards and information panels.");
        Hover(slot.gameObject, false);
        yield return null;
        yield return new WaitForSecondsRealtime(0.06f);
        Assert.That(preview.localScale.x / scale.x, Is.GreaterThan(1f).And.LessThan(1.21125f));
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(preview.localScale, Is.EqualTo(scale));
        Assert.That(preview.anchoredPosition, Is.EqualTo(position));
        Assert.That(game.Vault.Area.GetSiblingIndex(), Is.EqualTo(sibling));

        Hover(research.Card, true);
        yield return new WaitForSecondsRealtime(0.32f);
        Hover(slot.gameObject, true);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(research.Card.GetComponent<CardHover>().Details.gameObject.activeSelf, Is.False);
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        factory.enabled = false;
        Drag(plan, factory.transform.position);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(details.gameObject.activeSelf, Is.False);
        Assert.That(preview.localScale, Is.EqualTo(scale));
        Assert.That(plan.StoredAtFactory, Is.SameAs(factory));
        Assert.That(game.Vault.AvailableCount(slot.Kind), Is.Zero);
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }

    [UnityTest]
    public IEnumerator HoverUsesSavedPanelsAndReturnsToTheAuthoredScale()
    {
        var card = research.Card;
        var details = card.GetComponent<CardHover>().Details;
        var label = details.Find("Information/Title").GetComponent<TextMeshProUGUI>();
        label.text = "Özel başlık";
        var savedUI = UIIds(game);
        var savedPosition = card.Rect.anchoredPosition;
        var savedScale = card.LayoutScale;
        var savedPanelScale = details.localScale;
        Hover(card, true);
        yield return null;
        yield return new WaitForSecondsRealtime(0.06f);
        float intermediateScale = card.Rect.localScale.x / savedScale.x;
        Assert.That(intermediateScale, Is.GreaterThan(1f).And.LessThan(1.21125f), "Hover should grow through intermediate sizes.");
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(Vector3.Distance(card.Rect.localScale, savedScale * 1.21125f), Is.LessThan(0.001f));
        Assert.That(details.gameObject.activeSelf, Is.True);
        Assert.That(details.localScale, Is.EqualTo(savedPanelScale));
        Assert.That(label.text, Is.EqualTo("Özel başlık"));
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        yield return new WaitForSecondsRealtime(0.25f);
        Assert.That(Vector3.Distance(card.Rect.localScale, savedScale * 1.21125f), Is.LessThan(0.001f));
        Hover(card, false);
        yield return null;
        yield return new WaitForSecondsRealtime(0.06f);
        intermediateScale = card.Rect.localScale.x / savedScale.x;
        Assert.That(intermediateScale, Is.GreaterThan(1f).And.LessThan(1.21125f), "Hover exit should shrink through intermediate sizes.");
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(details.gameObject.activeSelf, Is.False);
        Assert.That(Vector3.Distance(card.Rect.localScale, savedScale), Is.LessThan(0.001f));
        Assert.That(card.Rect.anchoredPosition, Is.EqualTo(savedPosition));
        Assert.That(card.LayoutScale, Is.EqualTo(savedScale));
    }

    [UnityTest]
    public IEnumerator DraggingAndBlueprintChoiceCloseHoverWithoutChangingCardSize()
    {
        var card = school.Card;
        Hover(card, true);
        yield return new WaitForSecondsRealtime(0.32f);
        Drag(card, game.Grid.GetCellWorldPosition(InitiallyEmptyCell));
        yield return new WaitForSecondsRealtime(0.25f);
        Assert.That(card.GetComponent<CardHover>().Details.gameObject.activeSelf, Is.False);
        Assert.That(Vector3.Distance(card.Rect.localScale, card.LayoutScale), Is.LessThan(0.001f));
        Assert.That(game.Grid.GetOccupant(InitiallyEmptyCell), Is.SameAs(card));
        Hover(research.Card, true);
        yield return new WaitForSecondsRealtime(0.32f);
        research.TryAssign(People()[0]);
        research.AdvanceWork(25.01f);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        Assert.That(research.Card.GetComponent<CardHover>().Details.gameObject.activeSelf, Is.False);
        Assert.That(Vector3.Distance(research.Card.Rect.localScale, research.Card.LayoutScale), Is.LessThan(0.001f));
    }

    [UnityTest]
    public IEnumerator HoverInspectsBuildingsButDraggingPeopleOverThemDoesNot()
    {
        var card = school.Card;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(Vector3.Distance(card.Rect.localScale, card.LayoutScale * 1.21125f), Is.LessThan(0.001f));
        Assert.That(card.GetComponent<CardHover>().Details.gameObject.activeSelf, Is.True);
        Hover(card, false);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(card.Rect.localScale, Is.EqualTo(card.LayoutScale));
        var person = People()[0];
        person.OnBeginDrag(pointer);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        pointer.dragging = true;
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(card.GetComponent<CardHover>().Details.gameObject.activeSelf, Is.False);
        Assert.That(card.Rect.localScale, Is.EqualTo(card.LayoutScale));
        person.OnEndDrag(pointer);
        pointer.dragging = false;
        Hover(card, true);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(Vector3.Distance(card.Rect.localScale, card.LayoutScale * 1.21125f), Is.LessThan(0.001f));
    }

    private void EnsurePresentationWorkers(int count)
    {
        // Long presentation scenarios span turns; gameplay economy is covered with the real 10 AP separately.
        // Synchronous setup also skips finished workers' cosmetic returns before filling their saved pockets again.
        foreach (var person in game.Cards.Where(card => card.IsPerson && card.UsedThisTurn && card.IsFlying)) person.StopFlight();
        if (game.Cards.Count(card => card.Kind == CardKind.Person && card.CanStartWork && !card.IsStored) < count) game.ResetTurnPeople();
    }

    private void AssignFactoryWorkers(UpgradeKind kind)
    {
        var station = Station(CardKind.Factory);
        int required = FactoryProduction.RequiredWorkersFor(kind);
        EnsurePresentationWorkers(required);
        while (station.WorkerCount < required)
            Assert.That(station.TryAssign(People().First(person => person.CanStartWork && !person.IsStored)), Is.True);
    }

    private DraggableCard ResearchPlan(int index)
    {
        var existing = game.Cards.FirstOrDefault(c => c.Kind == CardKind.Blueprint && c.UpgradeType == (UpgradeKind)(index + 1));
        if (existing != null) return existing;
        int workerCount = CardGameController.RequiredResearchers((UpgradeKind)(index + 1));
        EnsurePresentationWorkers(workerCount);
        for (int i = 0; i < workerCount; i++)
            Assert.That(research.TryAssign(People().First(p => p.CanStartWork && p.AssignedTo == null)), Is.True);
        research.AdvanceWork(25.01f);
        game.SelectBlueprint(index);
        var plan = game.Cards.Last(c => c.Kind == CardKind.Blueprint);
        // Synchronous mechanics tests skip the cosmetic transfer. Its real timing is
        // exercised separately by the discovery animation regression tests.
        game.Vault.Store(plan);
        return plan;
    }

    [Test]
    public void FireBlueprintNeedsTwoPeopleThenProducesAReusableHouseUpgrade()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        factory.enabled = false;
        var blueprint = ResearchPlan(2);
        Assert.That(blueprint.UpgradeType, Is.EqualTo(UpgradeKind.Fire));
        var house = game.Cards.First(c => c.Kind == CardKind.House).GetComponent<HouseUpgrades>();
        Assert.That(house.TryInstall(blueprint), Is.False, "Houses accept manufactured cards, never blueprints.");
        Drag(blueprint, factory.transform.position);
        Assert.That(factory.PendingBlueprint, Is.SameAs(blueprint));
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.Zero);
        Assert.That(factory.WorkRate, Is.Zero);
        Assert.That(factory.PendingBlueprint, Is.SameAs(blueprint));
        var worker = People()[0];
        Station(CardKind.Factory).TryAssign(worker);
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.Zero);
        Assert.That(factory.PendingBlueprint, Is.SameAs(blueprint));
        var secondWorker = People().First(person => person.CanStartWork && !person.IsStored);
        Assert.That(Station(CardKind.Factory).TryAssign(secondWorker), Is.True);
        factory.AdvanceWork(factory.ProductionSeconds * .5f - .01f);
        Assert.That(factory.PendingBlueprint, Is.SameAs(blueprint));
        factory.AdvanceWork(.02f);
        Assert.That(game.Cards.Contains(blueprint), Is.True);
        Assert.That(blueprint.StoredInVault, Is.SameAs(game.Vault));
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Upgrade), Is.EqualTo(2));
        var product = game.Cards.First(c => c.Kind == CardKind.Upgrade);
        Assert.That(product.UpgradeType, Is.EqualTo(UpgradeKind.Fire));
        Assert.That(product.IsBuilding, Is.False);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
        Assert.That(worker.AssignedTo, Is.Null);
        Assert.That(worker.UsedThisTurn, Is.True);
        Assert.That(secondWorker.UsedThisTurn, Is.True);
        Drag(product, house.transform.position);
        Assert.That(house.GetInstalled(UpgradeKind.Fire), Is.SameAs(product));
        Assert.That(product.gameObject.activeSelf, Is.False);
        Assert.That(house.GetComponentsInChildren<HouseUpgradeSlot>().Single(s => s.Kind == UpgradeKind.Fire).Filled, Is.True);
        house.Release(UpgradeKind.Fire, false);
        Assert.That(product.gameObject.activeSelf, Is.True);
        Assert.That(product.InstalledIn, Is.Null);
        Assert.That(house.GetInstalled(UpgradeKind.Fire), Is.Null);
    }

    [Test]
    public void EveryBlueprintMakesTwoCardsAndBothHousesUseTheirOwnTypeSlots()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        factory.enabled = false;
        var houses = game.Cards.Where(c => c.Kind == CardKind.House).Select(c => c.GetComponent<HouseUpgrades>()).ToArray();
        for (int i = 0; i < 3; i++)
        {
            EnsurePresentationWorkers(FactoryProduction.RequiredWorkersFor((UpgradeKind)(i + 1)) + 1);
            var blueprint = ResearchPlan(i);
            AssignFactoryWorkers(blueprint.UpgradeType);
            Assert.That(factory.TryInsertBlueprint(blueprint), Is.True);
            factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
            var products = game.Cards.Where(c => c.Kind == CardKind.Upgrade && c.UpgradeType == (UpgradeKind)(i + 1)).ToArray();
            Assert.That(products.Length, Is.EqualTo(2));
            Assert.That(houses[0].TryInstall(products[0]), Is.True);
            Assert.That(houses[0].TryInstall(products[1]), Is.False, "A house has one slot for each type.");
            Assert.That(houses[1].TryInstall(products[1]), Is.True);
            Assert.That(game.Vault.IsDiscovered((UpgradeKind)(i + 1)), Is.True);
            Assert.That(game.Vault.AvailableCount((UpgradeKind)(i + 1)), Is.EqualTo(1));
        }
        Assert.That(houses[0].GetComponentsInChildren<HouseUpgradeSlot>().All(s => s.Filled), Is.True);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Upgrade), Is.EqualTo(6));
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Blueprint), Is.EqualTo(3));
    }

    [Test]
    public void VaultRetainsReusableDiscoveriesAndBlocksWithdrawalsDuringChoices()
    {
        var vault = game.Vault;
        int[] SavedVaultUI() => vault.GetComponentsInChildren<Component>(true).Where(c => c.GetComponentInParent<DraggableCard>() == null)
            .Select(c => c.GetInstanceID()).OrderBy(id => id).ToArray();
        int[] savedVaultUI = SavedVaultUI();
        Assert.That(vault.IsDiscovered(UpgradeKind.RailSystem), Is.False);
        var first = ResearchPlan(0);
        var second = ResearchPlan(1);
        Assert.That(vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        Assert.That(first.StoredInVault, Is.SameAs(vault));
        Assert.That(second.gameObject.activeSelf, Is.False);
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>(); factory.enabled = false;
        Assert.That(factory.TryInsertBlueprint(first), Is.True);
        Assert.That(factory.TryInsertBlueprint(second), Is.False, "An occupied factory must not withdraw another plan.");
        Assert.That(vault.AvailableCount(UpgradeKind.RailSystem), Is.Zero);
        Station(CardKind.Factory).TryAssign(People()[0]);
        factory.AdvanceWork(5.01f);
        Assert.That(vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Upgrade), Is.EqualTo(2));
        Drag(second, factory.transform.position);
        Assert.That(factory.PendingBlueprint, Is.SameAs(second));
        Assert.That(vault.AvailableCount(UpgradeKind.Breakwater), Is.Zero);
        factory.ReleaseBlueprint(false);
        Assert.That(vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        EnsurePresentationWorkers(2);
        for (int i = 0; i < 2; i++)
            Assert.That(research.TryAssign(People().First(p => p.CanStartWork && p.AssignedTo == null)), Is.True);
        research.AdvanceWork(25.01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        var knownButton = game.GetComponentsInChildren<Button>().Single(b => b.name == "BlueprintOption0");
        Assert.That(knownButton.interactable, Is.False);
        game.SelectBlueprint(0);
        Assert.That(game.IsChoosingBlueprint, Is.True, "A discovered blueprint cannot consume another research reward.");
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
        ExecuteEvents.Execute(vault.SlotFor(UpgradeKind.RailSystem).gameObject, pointer, ExecuteEvents.beginDragHandler);
        Assert.That(game.IsDraggingAnyCard, Is.False);
        Assert.That(vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        game.SelectBlueprint(2);
        Assert.That(vault.IsDiscovered(UpgradeKind.Fire), Is.True);
        Assert.That(vault.AvailableCount(UpgradeKind.Fire), Is.EqualTo(1));
        Assert.That(SavedVaultUI(), Is.EqualTo(savedVaultUI),
            "Discoveries must update the saved controls rather than create new UI.");
    }

    [Test]
    public void FactoryPausesWithoutWorkersAndPendingBlueprintCanBeTakenBack()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        factory.enabled = false;
        var worker = People()[0];
        var blueprint = ResearchPlan(1);
        factory.TryInsertBlueprint(blueprint);
        Station(CardKind.Factory).TryAssign(worker);
        factory.AdvanceWork(2f);
        Station(CardKind.Factory).ReleaseWorker(0, false);
        float progress = factory.Progress;
        factory.AdvanceWork(100f);
        Assert.That(factory.Progress, Is.EqualTo(progress));
        factory.ReleaseBlueprint(false);
        Assert.That(blueprint.gameObject.activeSelf, Is.False);
        Assert.That(blueprint.StoredAtFactory, Is.Null);
        Assert.That(blueprint.StoredInVault, Is.SameAs(game.Vault));
        Assert.That(game.Vault.AvailableCount(UpgradeKind.Breakwater), Is.EqualTo(1));
        Assert.That(game.Cards.Contains(blueprint), Is.True);
        Assert.That(factory.PendingBlueprint, Is.Null);
    }

    [UnityTest]
    public IEnumerator CaptureFactoryAndFilledHouseSlots()
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        Directory.CreateDirectory(directory);
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        factory.enabled = false;
        Station(CardKind.Factory).TryAssign(People()[0]);
        var house = game.Cards.First(c => c.Kind == CardKind.House).GetComponent<HouseUpgrades>();
        var savedUI = house.GetComponent<CardHover>().Details.GetComponentsInChildren<Graphic>(true)
            .Select(graphic => graphic.GetInstanceID()).ToArray();
        yield return CaptureCanvas(Path.Combine(directory, "house-prevention-empty.png"), house.gameObject);
        Hover(house.gameObject, false);
        for (int i = 0; i < 3; i++)
        {
            EnsurePresentationWorkers(2);
            var blueprint = ResearchPlan(i);
            if (Station(CardKind.Factory).WorkerCount == 0)
                Assert.That(Station(CardKind.Factory).TryAssign(People().First(p => p.CanStartWork && !p.IsStored)), Is.True);
            factory.TryInsertBlueprint(blueprint);
            factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
            house.TryInstall(game.Cards.First(c => c.Kind == CardKind.Upgrade && c.UpgradeType == (UpgradeKind)(i + 1)));
            yield return CaptureCanvas(Path.Combine(directory, "house-prevention-installed-" + (i + 1) + ".png"), house.gameObject);
            Hover(house.gameObject, false);
        }
        Station(CardKind.Factory).ReleaseWorker(0, false);
        game.ResetTurnPeople();
        factory.TryInsertBlueprint(ResearchPlan(2));
        factory.AdvanceWork(0f);
        yield return new WaitForSeconds(0.6f);
        school.TryAssign(People()[0]);
        school.AdvanceWork(25.01f);
        yield return new WaitForSecondsRealtime(0.65f);
        Station(CardKind.Factory).TryAssign(Educated());
        Station(CardKind.Factory).TryAssign(People().First(person => person.AssignedTo == null));
        factory.AdvanceWork(0f);
        yield return new WaitForSecondsRealtime(0.7f);
        yield return CaptureCanvas(Path.Combine(directory, "production-house-board.png"));
        yield return CaptureCanvas(Path.Combine(directory, "upgraded-house-inspection.png"), house.gameObject);
        Hover(house.gameObject, false);
        foreach (var kind in new[] { UpgradeKind.RailSystem, UpgradeKind.Breakwater, UpgradeKind.Fire })
        {
            house.Release(kind, false);
            yield return CaptureCanvas(Path.Combine(directory, "house-prevention-removed-" + kind + ".png"), house.gameObject);
            Hover(house.gameObject, false);
        }
        CollectionAssert.AreEquivalent(savedUI, house.GetComponent<CardHover>().Details.GetComponentsInChildren<Graphic>(true)
            .Select(graphic => graphic.GetInstanceID()).ToArray(), "House prevention information must reuse the saved UI.");
    }

    private static void Drag(DraggableCard card, Vector3 destination)
    {
        var canvas = card.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var target = card.StoredInVault == null ? card.gameObject : card.StoredInVault.SlotFor(card.UpgradeType).gameObject;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(camera, target.transform.position),
            button = PointerEventData.InputButton.Left,
            pointerId = -1,
            pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() }
        };
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.beginDragHandler);
        pointer.position = RectTransformUtility.WorldToScreenPoint(camera, destination);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.endDragHandler);
    }

    [UnityTest]
    public IEnumerator EnteringPlayPreservesAuthoredCardDimensionsAndScale()
    {
        DraggableCard authoredCard = null;
        Vector3 savedScale = Vector3.one * 0.52f;
        Vector2 savedSize = new Vector2(180f, 270f);
        void EditBeforeStart(Scene scene, LoadSceneMode mode)
        {
            var board = Object.FindFirstObjectByType<CardGameController>();
            authoredCard = board.GetComponentsInChildren<DraggableCard>().First(c => c.Kind == CardKind.House);
            authoredCard.Rect.sizeDelta = savedSize;
            authoredCard.SetLayoutScale(savedScale);
        }
        SceneManager.sceneLoaded += EditBeforeStart;
        try { yield return SceneManager.LoadSceneAsync("2DCardScene", LoadSceneMode.Single); }
        finally { SceneManager.sceneLoaded -= EditBeforeStart; }
        yield return null;
        Assert.That(authoredCard.Rect.sizeDelta, Is.EqualTo(savedSize));
        Assert.That(Vector3.Distance(authoredCard.Rect.localScale, savedScale), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(authoredCard.LayoutScale, savedScale), Is.LessThan(0.0001f));
    }

    [UnityTest]
    public IEnumerator BoardAndInteractionsFitFromSmallWindowsTo4KAndPortrait()
    {
        ResearchPlan(2);
        yield return new WaitForSecondsRealtime(0.55f);
        var resolutions = new[]
        {
            new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1366, 768),
            new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
            new Vector2Int(4096, 2160), new Vector2Int(1024, 768), new Vector2Int(1280, 1024),
            new Vector2Int(3440, 1440), new Vector2Int(5120, 1440), new Vector2Int(1080, 1920)
        };
        var canvas = game.GetComponentInParent<Canvas>();
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        var cameraObject = new GameObject("ResolutionTestCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 450f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.cullingMask = 1 << canvas.gameObject.layer;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;
        var baselineScales = game.Cards.ToDictionary(c => c, c => c.LayoutScale);
        var baselineSizes = game.Cards.ToDictionary(c => c, c => c.Rect.rect.size);
        var baselinePeoplePositions = People().ToDictionary(c => c,
            c => game.Sidebar.PeopleArea.InverseTransformPoint(c.Rect.position));
        Vector2 designSize = canvas.GetComponent<CanvasScaler>().referenceResolution;
        var choice = (RectTransform)game.transform.Find("BlueprintChoice");
        RenderTexture target = null;
        try
        {
            foreach (var resolution in resolutions)
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(resolution.x, resolution.y, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                string context = resolution.x + "x" + resolution.y;
                Assert.That(canvas.scaleFactor, Is.EqualTo(Mathf.Min(resolution.x / designSize.x, resolution.y / designSize.y)).Within(0.002f), context);
                var panelCorners = new Vector3[4];
                ((RectTransform)game.transform).GetWorldCorners(panelCorners);
                float panelBottom = RectTransformUtility.WorldToScreenPoint(camera, panelCorners[0]).y;
                float panelTopExpanded = RectTransformUtility.WorldToScreenPoint(camera, panelCorners[1]).y;
                Assert.That(panelBottom, Is.EqualTo(0f).Within(1f), context);
                Assert.That(panelTopExpanded, Is.EqualTo(resolution.y * 0.75f).Within(1f), context);
                Assert.That(RectTransformUtility.WorldToScreenPoint(camera, panelCorners[3]).x,
                    Is.EqualTo(resolution.x).Within(1f), "The saved panel must reach the right screen edge: " + context);
                var gridBackgroundCorners = new Vector3[4];
                game.transform.Find("GridPanel").GetComponent<RectTransform>().GetWorldCorners(gridBackgroundCorners);
                Assert.That(RectTransformUtility.WorldToScreenPoint(camera, gridBackgroundCorners[3]).x,
                    Is.EqualTo(resolution.x).Within(1f), "The grid background must fill the right gutter: " + context);
                AssertOnScreen((RectTransform)game.transform, camera, resolution);
                for (int i = 0; i < CardGrid.CellCount; i++)
                    AssertOnScreen(game.Grid.GetCell(i), camera, resolution);
                var alignedPeople = People().OrderBy(c => c.name).ToArray();
                // The first person is deliberately moved/released below; the untouched cards must keep following their rows.
                for (int i = 1; i < alignedPeople.Length; i++)
                    Assert.That(Vector3.Distance(game.Sidebar.PeopleArea.InverseTransformPoint(alignedPeople[i].Rect.position),
                        baselinePeoplePositions[alignedPeople[i]]),
                        Is.LessThan(0.01f), context);
                var rowStart = game.Grid.rectTransform.InverseTransformPoint(game.Grid.GetCellWorldPosition(0));
                var rowNext = game.Grid.rectTransform.InverseTransformPoint(game.Grid.GetCellWorldPosition(CardGrid.Columns));
                Assert.That(rowStart.y - rowNext.y - game.Grid.CellSize.y, Is.EqualTo(27.5472f).Within(0.01f), context);
                foreach (var card in game.Cards)
                {
                    if (card.IsStored) continue;
                    Assert.That(Vector3.Distance(card.LayoutScale, baselineScales[card]), Is.LessThan(0.0001f), context);
                    Assert.That(Vector2.Distance(card.Rect.rect.size, baselineSizes[card]), Is.LessThan(0.001f), context);
                    AssertOnScreen(card.Rect, camera, resolution);
                    if (card.Station != null)
                        foreach (var slot in card.GetComponentsInChildren<WorkerSlot>(true))
                            AssertInsideCard((RectTransform)slot.transform, card.Rect);
                }
                Assert.That(game.transform.Find("Population"), Is.Null);
                Assert.That(game.transform.Find("CardInstructions"), Is.Null);
                AssertOnScreen(game.Vault.Area, camera, resolution);
                foreach (var slot in game.Vault.Slots) AssertOnScreen((RectTransform)slot.transform, camera, resolution);
                AssertOnScreen(game.Sidebar.PeopleArea, camera, resolution);
                var vaultSlot = game.Vault.SlotFor(UpgradeKind.Fire);
                Hover(vaultSlot.gameObject, true);
                yield return new WaitForSecondsRealtime(0.32f);
                AssertOnScreen(vaultSlot.CardRect, camera, resolution);
                AssertOnScreen(vaultSlot.GetComponent<CardHover>().Details, camera, resolution);
                if (resolution.x == 1920 || resolution.x == 3840)
                    SaveViewport(camera, target, "vault-hover_" + context + ".png");
                Hover(vaultSlot.gameObject, false);
                yield return new WaitForSecondsRealtime(0.32f);

                Drag(school.Card, game.Grid.GetCellWorldPosition(InitiallyEmptyCell));
                Assert.That(game.Grid.GetOccupant(InitiallyEmptyCell), Is.SameAs(school.Card), context);
                Hover(school.Card, true);
                yield return new WaitForSecondsRealtime(0.32f);
                AssertOnScreen(school.Card.Rect, camera, resolution);
                AssertOnScreen(school.Card.GetComponent<CardHover>().Details, camera, resolution);
                Hover(school.Card, false);
                yield return new WaitForSecondsRealtime(0.32f);
                Drag(school.Card, game.Grid.GetCellWorldPosition(2));
                var edgePerson = People()[0];
                Vector2 originalPosition = edgePerson.Rect.anchoredPosition;
                var viewport = (RectTransform)canvas.transform;
                edgePerson.Rect.position = viewport.TransformPoint(new Vector3(viewport.rect.xMax - 58f, 0f));
                Hover(edgePerson, true);
                yield return new WaitForSecondsRealtime(0.32f);
                var edgeDetails = edgePerson.GetComponent<CardHover>().Details;
                AssertOnScreen(edgePerson.Rect, camera, resolution);
                AssertOnScreen(edgeDetails, camera, resolution);
                Assert.That(edgeDetails.pivot.x, Is.EqualTo(1f), "Near the right edge, info must open to the left: " + context);
                Hover(edgePerson, false);
                yield return new WaitForSecondsRealtime(0.32f);
                edgePerson.Rect.anchoredPosition = originalPosition;
                if (resolution.x == 3840)
                {
                    Hover(research.Card, true);
                    yield return new WaitForSecondsRealtime(0.32f);
                    SaveViewport(camera, target, "hover-research_" + context + ".png");
                    Hover(research.Card, false);
                    yield return new WaitForSecondsRealtime(0.32f);
                }
                var person = People()[0];
                Drag(person, school.Card.Rect.position);
                Assert.That(person.AssignedTo, Is.SameAs(school), context);
                school.ReleaseWorker(0, false);
                Assert.That(game.NormalPeopleCount, Is.EqualTo(4), context);

                if (resolution.x == 1920 || resolution.x == 3840 || resolution.x == 3440 || resolution.y == 1920)
                    SaveViewport(camera, target, "resolution_" + context + ".png");
                choice.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
                foreach (var button in choice.GetComponentsInChildren<Button>())
                    AssertOnScreen((RectTransform)button.transform, camera, resolution);
                AssertOnScreen((RectTransform)choice.Find("SelectionWindow"), camera, resolution);
                if (resolution.x == 3840) SaveViewport(camera, target, "blueprint-choice_" + context + ".png");
                choice.gameObject.SetActive(false);
                AssertCityFitsItsArea(camera, resolution);
                var city = Object.FindFirstObjectByType<CityViewport>();
                var fixedViewCorners = new Vector3[4]; city.ViewRect.GetWorldCorners(fixedViewCorners);
                Vector3 fixedCameraPosition = city.WorldCamera.transform.position;
                float fixedCameraSize = city.WorldCamera.orthographicSize;
                float fixedCameraAspect = city.WorldCamera.aspect;
                var dock = game.GetComponent<CardDock>();
                dock.TickPointer(new Vector2(-10000f, -10000f), 0.08f);
                Assert.That(dock.Expansion, Is.GreaterThan(0f).And.LessThan(1f));
                AssertCityUnchanged(city, fixedViewCorners, fixedCameraPosition, fixedCameraSize, fixedCameraAspect);
                dock.SetExpanded(false, true);
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < CardGrid.Columns; i++) AssertOnScreen(game.Grid.GetCell(i), camera, resolution);
                for (int i = CardGrid.Columns; i < CardGrid.CellCount; i++)
                {
                    var rowCorners = new Vector3[4];
                    game.Grid.GetCell(i).GetWorldCorners(rowCorners);
                    Assert.That(rowCorners.All(c => RectTransformUtility.WorldToScreenPoint(camera, c).y < 0f), Is.True,
                        "The second row should be below the screen: " + context);
                }
                int visiblePeople = 0;
                foreach (var loosePerson in People())
                {
                    if (RectTransformUtility.WorldToScreenPoint(camera, loosePerson.Rect.position).y <= 0f) continue;
                    visiblePeople++;
                    AssertOnScreen(loosePerson.Rect, camera, resolution);
                }
                Assert.That(visiblePeople, Is.EqualTo(4), "All sidebar people stay visible while the building drawer closes: " + context);
                AssertCityFitsItsArea(camera, resolution);
                AssertCityUnchanged(city, fixedViewCorners, fixedCameraPosition, fixedCameraSize, fixedCameraAspect);
                if (resolution.x == 1920 || resolution.x == 3840) SaveViewport(camera, target, "dock-collapsed_" + context + ".png");
                dock.SetExpanded(true, true);
            }
        }
        finally
        {
            choice.gameObject.SetActive(false);
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject);
        }
    }

    [UnityTest]
    public IEnumerator DockSlidesWithThePointerAndStaysOpenForDraggingAndBlueprints()
    {
        var dock = game.GetComponent<CardDock>();
        var canvas = game.GetComponentInParent<Canvas>();
        var worldCamera = GameObject.Find("Display Camera").GetComponent<Camera>();
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        var oldTarget = worldCamera.targetTexture;
        var target = new RenderTexture(1920, 1080, 24);
        try
        {
            worldCamera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = worldCamera;
            canvas.planeDistance = 1f;
            dock.SetExpanded(true, true);
            Canvas.ForceUpdateCanvases();
            yield return null;
            var city = Object.FindFirstObjectByType<CityViewport>();
            city.RefreshViewport(); city.WorldCamera.Render();
            SaveViewport(worldCamera, target, "dock-expanded-3d.png");
            dock.SetExpanded(false, true);
            Canvas.ForceUpdateCanvases();
            yield return null;
            city.RefreshViewport(); city.WorldCamera.Render();
            SaveViewport(worldCamera, target, "dock-collapsed-3d.png");
        }
        finally
        {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            worldCamera.targetTexture = oldTarget;
            target.Release(); Object.Destroy(target);
            dock.SetExpanded(true, true);
        }
        Canvas.ForceUpdateCanvases();
        Vector3 originalPerson = People()[0].Rect.position;
        dock.SetExpanded(false, true);
        Assert.That(Vector3.Distance(People()[0].Rect.position, originalPerson),
            Is.LessThan(0.01f), "Sidebar people must stay fixed while the building panel slides.");
        dock.SetExpanded(true, true);
        Vector2 overWorld = new Vector2(Screen.width * 0.5f, Screen.height - 2f);
        dock.TickPointer(overWorld, 0.08f);
        Assert.That(dock.Expansion, Is.GreaterThan(0f).And.LessThan(1f), "The dock should slide through intermediate positions.");
        dock.TickPointer(overWorld, 0.4f);
        Assert.That(dock.IsExpanded, Is.False);
        Assert.That(dock.Expansion, Is.Zero);
        Vector2 overDock = RectTransformUtility.WorldToScreenPoint(null, dock.ActivationArea.TransformPoint(dock.ActivationArea.rect.center));
        dock.TickPointer(overDock, 0.08f);
        Assert.That(dock.Expansion, Is.GreaterThan(0f).And.LessThan(1f));
        dock.TickPointer(overDock, 0.4f);
        Assert.That(dock.Expansion, Is.EqualTo(1f));

        dock.SetExpanded(false, true);
        var person = People()[0];
        var pointer = new PointerEventData(EventSystem.current)
        {
            pointerId = -1, button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, person.Rect.position)
        };
        person.OnBeginDrag(pointer);
        Vector3 heldPosition = person.Rect.position;
        for (int i = 0; i < 8; i++)
        {
            dock.TickPointer(overWorld, 0.05f);
            person.OnDrag(pointer);
            Assert.That(Vector3.Distance(person.Rect.position, heldPosition), Is.LessThan(0.01f), "The drawer must not pull a held card away from the mouse.");
        }
        Assert.That(dock.IsExpanded, Is.True);
        Assert.That(dock.Expansion, Is.EqualTo(1f));
        person.OnEndDrag(pointer);
        research.TryAssign(People()[1]);
        research.AdvanceWork(25.01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        dock.SetExpanded(false, true);
        dock.TickPointer(overWorld, 0.4f);
        Assert.That(dock.Expansion, Is.EqualTo(1f), "Blueprint choices must stay accessible above the drawer.");
    }

    private void AssertCityFitsItsArea(Camera uiCamera, Vector2Int resolution)
    {
        var city = Object.FindFirstObjectByType<CityViewport>();
        city.RefreshViewport();
        AssertOnScreen(city.ViewRect, uiCamera, resolution);
        AssertOnScreen(city.Sidebar, uiCamera, resolution);
        var viewCorners = new Vector3[4]; var sideCorners = new Vector3[4]; var panelCorners = new Vector3[4];
        city.ViewRect.GetWorldCorners(viewCorners);
        city.Sidebar.GetWorldCorners(sideCorners);
        ((RectTransform)game.transform).GetWorldCorners(panelCorners);
        var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
        var root = (RectTransform)canvas.transform;
        float fixedBottom = RectTransformUtility.WorldToScreenPoint(uiCamera, root.TransformPoint(
            new Vector3(0f, root.rect.yMin + game.GetComponent<CardDock>().CollapsedCanvasHeight))).y;
        float viewBottom = RectTransformUtility.WorldToScreenPoint(uiCamera, viewCorners[0]).y;
        float viewTop = RectTransformUtility.WorldToScreenPoint(uiCamera, viewCorners[1]).y;
        Assert.That(viewBottom, Is.EqualTo(fixedBottom).Within(0.1f), "The city begins above the single saved row, regardless of drawer expansion.");
        Assert.That(RectTransformUtility.WorldToScreenPoint(uiCamera, viewCorners[1]).y,
            Is.EqualTo(RectTransformUtility.WorldToScreenPoint(uiCamera, sideCorners[1]).y).Within(0.1f));
        Assert.That(RectTransformUtility.WorldToScreenPoint(uiCamera, viewCorners[0]).x,
            Is.EqualTo(RectTransformUtility.WorldToScreenPoint(uiCamera, sideCorners[2]).x).Within(0.1f));
        Assert.That(RectTransformUtility.WorldToScreenPoint(uiCamera, panelCorners[0]).x,
            Is.EqualTo(RectTransformUtility.WorldToScreenPoint(uiCamera, sideCorners[2]).x).Within(0.1f), "The sidebar and dock must touch.");
        int visibleLandmarks = 0;
        foreach (var landmark in new[] { "ResearchCenter", "School", "Factory" })
            if (city.World.Find("Buildings/" + landmark).GetComponentsInChildren<Renderer>().Any(renderer =>
                Mathf.Lerp(viewBottom, viewTop, city.WorldCamera.WorldToViewportPoint(renderer.bounds.center).y) > resolution.y * 0.77f))
                visibleLandmarks++;
        Assert.That(visibleLandmarks, Is.GreaterThanOrEqualTo(2), "At least two city buildings must remain visible above the expanded drawer: " + resolution);
        foreach (var renderer in city.World.GetComponentsInChildren<Renderer>())
        {
            if (renderer.name == "Water") continue;
            Bounds bounds = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = city.WorldCamera.WorldToViewportPoint(corner);
                Assert.That(point.x, Is.InRange(0f, 1f), renderer.name);
                Assert.That(point.y, Is.InRange(0f, 1f), renderer.name);
                Assert.That(point.z, Is.GreaterThan(0f), renderer.name);
            }
        }
    }

    private static void AssertCityUnchanged(CityViewport city, Vector3[] expectedCorners, Vector3 position, float size, float aspect)
    {
        city.RefreshViewport();
        var corners = new Vector3[4]; city.ViewRect.GetWorldCorners(corners);
        for (int i = 0; i < corners.Length; i++)
            Assert.That(Vector3.Distance(corners[i], expectedCorners[i]), Is.LessThan(0.001f), "The city surface must stay fixed while the drawer slides.");
        Assert.That(Vector3.Distance(city.WorldCamera.transform.position, position), Is.LessThan(0.001f));
        Assert.That(city.WorldCamera.orthographicSize, Is.EqualTo(size).Within(0.0001f), "Hovering must not zoom the city.");
        Assert.That(city.WorldCamera.aspect, Is.EqualTo(aspect).Within(0.0001f));
    }

    private static void AssertInsideCard(RectTransform indicator, RectTransform card)
    {
        var corners = new Vector3[4];
        indicator.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            Vector3 local = card.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(card.rect.xMin - 0.01f, card.rect.xMax + 0.01f), indicator.name);
            Assert.That(local.y, Is.InRange(card.rect.yMin - 0.01f, card.rect.yMax + 0.01f), indicator.name);
        }
    }

    private static void AssertOnScreen(RectTransform rect, Camera camera, Vector2Int resolution)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            Vector2 pixel = RectTransformUtility.WorldToScreenPoint(camera, corner);
            Assert.That(pixel.x, Is.InRange(-1.5f, resolution.x + 1.5f), rect.name + " at " + resolution);
            Assert.That(pixel.y, Is.InRange(-1.5f, resolution.y + 1.5f), rect.name + " at " + resolution);
        }
    }

    private static void SaveViewport(Camera camera, RenderTexture target, string name)
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        pixels.Apply();
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), pixels.EncodeToPNG());
        RenderTexture.active = previous;
        Object.Destroy(pixels);
    }

    [Test]
    public void StartsWithSevenBuildingsAndFourPeopleOutsideTheEightCellGrid()
    {
        Assert.That(CardGrid.Columns, Is.EqualTo(4));
        Assert.That(CardGrid.Rows, Is.EqualTo(2));
        Assert.That(game.Cards.Count, Is.EqualTo(11));
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.House), Is.EqualTo(4));
        Assert.That(game.Cards.Count(c => c.IsBuilding), Is.EqualTo(7));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
        Assert.That(game.EducatedPeopleCount, Is.Zero);
        Assert.That(Enumerable.Range(0, CardGrid.CellCount).Count(i => game.Grid.GetOccupant(i) == null), Is.EqualTo(1));
        foreach (var person in People())
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(game.Grid.rectTransform,
                RectTransformUtility.WorldToScreenPoint(null, person.Rect.position)), Is.False);
    }

    [Test]
    public void BuildingsSnapToNearestFreeCellAndKeepUniqueOccupancy()
    {
        var building = research.Card;
        Drag(building, game.Grid.GetCellWorldPosition(InitiallyEmptyCell));
        Assert.That(game.Grid.GetOccupant(InitiallyEmptyCell), Is.SameAs(building));
        Assert.That(game.Grid.GetOccupant(0), Is.Null);
        Assert.That(Vector3.Distance(building.Rect.position, game.Grid.GetCellWorldPosition(InitiallyEmptyCell)), Is.LessThan(0.1f));
        // Releasing on the same slot still snaps without duplicating the occupant.
        Drag(building, game.Grid.GetCellWorldPosition(InitiallyEmptyCell));
        Assert.That(Enumerable.Range(0, CardGrid.CellCount).Count(i => game.Grid.GetOccupant(i) == building), Is.EqualTo(1));
        Assert.That(Enumerable.Range(0, CardGrid.CellCount).Count(i => game.Grid.GetOccupant(i) != null), Is.EqualTo(7));
    }

    [UnityTest]
    public IEnumerator DropAssignsTwoWorkersAndRejectsTheThirdWithoutConsumingAnyone()
    {
        var people = People();
        Drag(people[0], school.Card.Rect.position);
        Drag(people[1], school.Card.Rect.position);
        Vector3 thirdPosition = people[2].Rect.position;
        Drag(people[2], school.Card.Rect.position);
        Assert.That(school.WorkerCount, Is.EqualTo(2));
        Assert.That(people[0].AssignedTo, Is.SameAs(school));
        Assert.That(people[0].gameObject.activeSelf, Is.False);
        Assert.That(people[2].AssignedTo, Is.Null);
        Assert.That(people[2].IsFlying, Is.True);
        Assert.That(Vector3.Distance(people[2].Rect.position, school.Card.Rect.position), Is.LessThan(0.01f),
            "A rejected drop must start its return at the actual drop position.");
        yield return new WaitForSecondsRealtime(0.55f);
        Assert.That(Vector3.Distance(people[2].Rect.position, thirdPosition), Is.LessThan(0.01f));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
        var slots = school.GetComponentsInChildren<WorkerSlot>();
        Assert.That(slots.Length, Is.EqualTo(2));
        Assert.That(((RectTransform)slots[0].transform).anchorMin.x +
                    ((RectTransform)slots[1].transform).anchorMin.x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(slots[0].Portrait.sprite, Is.EqualTo(slots[1].Portrait.sprite));
    }

    [Test]
    public void SchoolConvertsEachStudentAfterTheirOwnConfiguredDuration()
    {
        var people = People();
        school.TryAssign(people[0]);
        school.AdvanceWork(game.EducationSeconds * .4f);
        school.TryAssign(people[1]);
        school.AdvanceWork(game.EducationSeconds * .6f + .01f);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(3));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        Assert.That(school.WorkerCount, Is.EqualTo(1));
        school.AdvanceWork(game.EducationSeconds * .4f);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(2));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(2));
        Assert.That(school.WorkerCount, Is.Zero);
        Assert.That(school.TryAssign(Educated()), Is.False);
        Assert.That(game.transform.Find("Population"), Is.Null);
        var loosePeople = game.Cards.Where(c => c.IsPerson && !c.IsStored).ToArray();
        foreach (var person in loosePeople)
        {
            Assert.That(person.LayoutScale, Is.EqualTo(Vector3.one), person.name);
            Vector2 size = Vector2.Scale(person.Rect.rect.size, person.LayoutScale);
            var bounds = new Rect(person.TablePosition - size / 2f, size);
            Assert.That(game.Sidebar.Owns(person), Is.True, person.name + " should return to a people slot.");
            foreach (var other in loosePeople)
            {
                if (other == person) continue;
                Vector2 otherSize = Vector2.Scale(other.Rect.rect.size, other.LayoutScale);
                var otherBounds = new Rect(other.TablePosition - otherSize / 2f, otherSize);
                Assert.That(bounds.Overlaps(otherBounds), Is.False, "Graduates should have room beside the larger people cards.");
            }
        }
    }

    [Test]
    public void NormalResearchUsesItsConfiguredDurationAndChoiceGivesExactlyOneBlueprint()
    {
        var worker = People()[0];
        research.TryAssign(worker);
        research.AdvanceWork(game.NormalResearchSeconds - .01f);
        Assert.That(game.IsChoosingBlueprint, Is.False);
        research.AdvanceWork(.02f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Blueprint), Is.Zero);
        research.AdvanceWork(100f);
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Blueprint), Is.Zero);
        var blueprintButton = game.GetComponentsInChildren<Button>().Single(b => b.name == "BlueprintOption0");
        ExecuteEvents.Execute(blueprintButton.gameObject, new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        }, ExecuteEvents.pointerClickHandler);
        game.SelectBlueprint(0);
        Assert.That(game.IsChoosingBlueprint, Is.False);
        Assert.That(game.Cards.Single(c => c.Kind == CardKind.Blueprint).name, Is.EqualTo("RaylıSistemBlueprint"));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
        Assert.That(research.WorkerCount, Is.Zero);
        Assert.That(worker.gameObject.activeSelf, Is.True);
        Assert.That(worker.AssignedTo, Is.Null);
        worker.StopFlight(); // Finish the cosmetic return before filling both research pockets again.
        Assert.That(research.TryAssign(worker), Is.False, "A completed researcher is unavailable until the next turn.");
        Assert.That(research.TryAssign(People().First(p => p.CanStartWork && !p.IsStored)), Is.True);
        Assert.That(research.TryAssign(People().First(p => p.CanStartWork && !p.IsStored)), Is.True);
        research.AdvanceWork(25.01f);
        game.SelectBlueprint(1);
        Assert.That(game.Cards.Count(c => c.Kind == CardKind.Blueprint), Is.EqualTo(2));
        Assert.That(game.Cards.Any(c => c.name == "DalgaKıranBlueprint"), Is.True);
    }

    [Test]
    public void EducatedResearchUsesItsConfiguredDurationAndPreservesTheWorker()
    {
        school.TryAssign(People()[0]);
        school.AdvanceWork(25.01f);
        var worker = Educated();
        research.TryAssign(worker);
        research.AdvanceWork(game.EducatedResearchSeconds - .01f);
        Assert.That(game.IsChoosingBlueprint, Is.False);
        research.AdvanceWork(.02f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        game.SelectBlueprint(0);
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(3));
        Assert.That(worker.AssignedTo, Is.Null);
    }

    [Test]
    public void MixedResearchersCombineSpeedAndUseDifferentIndicatorColors()
    {
        school.TryAssign(People()[0]);
        school.AdvanceWork(25.01f);
        research.TryAssign(People()[0]);
        research.TryAssign(Educated());
        var slots = research.GetComponentsInChildren<WorkerSlot>();
        Assert.That(slots[0].Portrait.sprite, Is.Not.EqualTo(slots[1].Portrait.sprite));
        float duration = 1f / (1f / game.NormalResearchSeconds + 1f / game.EducatedResearchSeconds);
        research.AdvanceWork(duration - .01f);
        Assert.That(game.IsChoosingBlueprint, Is.False);
        research.AdvanceWork(.02f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        game.SelectBlueprint(0);
        Assert.That(game.NormalPeopleCount + game.EducatedPeopleCount, Is.EqualTo(4));
    }

    [Test]
    public void SlotCanReleaseAndDragTheSameWorkerToAnotherBuilding()
    {
        var worker = People()[0];
        school.TryAssign(worker);
        school.AdvanceWork(game.EducationSeconds * .4f);
        var slot = school.GetComponentsInChildren<WorkerSlot>().OrderBy(item => item.name).First();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, slot.transform.position),
            pointerId = -1,
            button = PointerEventData.InputButton.Left
        };
        ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.beginDragHandler);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, research.Card.Rect.position);
        ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.endDragHandler);
        Assert.That(school.WorkerCount, Is.Zero);
        Assert.That(research.WorkerCount, Is.EqualTo(1));
        Assert.That(worker.AssignedTo, Is.SameAs(research));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
        research.AdvanceWork(10f);
        research.ReleaseWorker(0, false);
        float progress = research.ResearchProgress;
        research.AdvanceWork(100f);
        Assert.That(research.ResearchProgress, Is.EqualTo(progress), "Research pauses without a worker.");
        Assert.That(research.TryAssign(worker), Is.True);
        research.AdvanceWork(25.01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
    }

    [Test]
    public void FactoryPreservesBothWorkerTypesAndRejectsOverflow()
    {
        school.TryAssign(People()[0]);
        school.AdvanceWork(25.01f);
        var factory = Station(CardKind.Factory);
        Assert.That(factory.TryAssign(People()[0]), Is.True);
        Assert.That(factory.TryAssign(Educated()), Is.True);
        Assert.That(factory.TryAssign(People()[1]), Is.False);
        factory.AdvanceWork(1000f);
        factory.ReleaseWorker(0, false);
        factory.ReleaseWorker(1, false);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(3));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator ReleasedWorkersAvoidVaultAndInvalidBlueprintDropsReturnThere()
    {
        var people = People();
        research.TryAssign(people[0]);
        research.TryAssign(people[1]);
        research.AdvanceWork(12.51f);
        game.SelectBlueprint(0);
        yield return new WaitForSeconds(0.55f);
        var looseCards = game.Cards.Where(c => !c.IsBuilding && !c.IsStored).ToArray();
        foreach (var card in looseCards)
            Assert.That(game.Vault.OverlapsTableArea(card.TablePosition, Vector2.Scale(card.Rect.rect.size, card.LayoutScale)), Is.False);
        for (int i = 0; i < looseCards.Length; i++)
            for (int j = i + 1; j < looseCards.Length; j++)
            {
                Vector2 size = Vector2.Scale(looseCards[i].Rect.rect.size, looseCards[i].LayoutScale);
                Vector2 otherSize = Vector2.Scale(looseCards[j].Rect.rect.size, looseCards[j].LayoutScale);
                var bounds = new Rect(looseCards[i].TablePosition - size / 2f, size);
                var otherBounds = new Rect(looseCards[j].TablePosition - otherSize / 2f, otherSize);
                Assert.That(bounds.Overlaps(otherBounds), Is.False, "Returned cards need separate space in the compact tray.");
            }
        var blueprint = game.Cards.Single(c => c.Kind == CardKind.Blueprint);
        Vector3 destination = game.Grid.GetCellWorldPosition(InitiallyEmptyCell);
        Drag(blueprint, destination);
        Assert.That(blueprint.StoredInVault, Is.SameAs(game.Vault));
        Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        Assert.That(game.Grid.GetOccupant(InitiallyEmptyCell), Is.Null, "Blueprints do not occupy building slots.");
    }

    [UnityTest]
    public IEnumerator CaptureBoardAndBlueprintChoiceForVisualReview()
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        Directory.CreateDirectory(directory);
        foreach (var kind in new[] { CardKind.ResearchCenter, CardKind.Factory, CardKind.School, CardKind.House })
        {
            var building = game.Cards.Where(card => card.Kind == kind).OrderBy(card => card.InitialGridCell).Last();
            yield return CaptureCanvas(Path.Combine(directory, "building-info-" + kind + ".png"), building.gameObject);
            Hover(building, false);
            yield return new WaitForSecondsRealtime(0.32f);
        }
        school.TryAssign(People()[0]);
        school.AdvanceWork(game.EducationSeconds * .5f);
        research.TryAssign(People()[1]);
        research.AdvanceWork(game.NormalResearchSeconds * .4f);
        yield return CaptureCanvas(Path.Combine(directory, "card-board.png"));
        research.AdvanceWork(game.NormalResearchSeconds * .6f + .01f);
        yield return CaptureCanvas(Path.Combine(directory, "blueprint-choice.png"));
        Assert.That(game.IsChoosingBlueprint, Is.True);
        game.SelectBlueprint(0);
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>(); factory.enabled = false;
        Station(CardKind.Factory).TryAssign(People()[2]);
        factory.TryInsertBlueprint(game.Cards.Last(c => c.Kind == CardKind.Blueprint));
        factory.AdvanceWork(5.01f);
        ResearchPlan(1);
        ResearchPlan(2);
        yield return new WaitForSeconds(0.65f);
        yield return CaptureCanvas(Path.Combine(directory, "vault-discoveries.png"));
        var slot = game.Vault.SlotFor(UpgradeKind.Fire);
        yield return CaptureCanvas(Path.Combine(directory, "vault-hover.png"), slot.gameObject);
        Hover(slot.gameObject, false);
        if (school.WorkerCount == 0)
            Assert.That(school.TryAssign(People().First(person => person.CanStartWork && !person.IsStored)), Is.True);
        school.AdvanceWork(game.EducationSeconds + .01f);
        yield return new WaitForSeconds(0.65f);
        yield return CaptureCanvas(Path.Combine(directory, "efe-people-cards.png"), Educated().gameObject);
    }

    // ScreenCapture waits for a Game view repaint, which does not happen in batch mode.
    private IEnumerator CaptureCanvas(string path, GameObject hoverTarget = null, float settleSeconds = 0f,
        System.Action<Camera> afterRender = null)
    {
        var canvas = game.GetComponentInParent<Canvas>();
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float oldPlaneDistance = canvas.planeDistance;
        var cameraObject = new GameObject("CardTestRenderCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 450f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << canvas.gameObject.layer;
        var target = new RenderTexture(1600, 900, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        // Changing the capture viewport closes inspections; enter after the new layout has settled.
        if (settleSeconds > 0f) yield return new WaitForSecondsRealtime(settleSeconds);
        if (hoverTarget != null)
        {
            Hover(hoverTarget, true);
            yield return new WaitForSecondsRealtime(0.32f);
        }
        var city = Object.FindFirstObjectByType<CityViewport>();
        city.RefreshViewport(); city.WorldCamera.Render();
        Assert.That(game.Grid.canvasRenderer, Is.Not.Null, "Grid guides need a CanvasRenderer to be visible.");
        foreach (var person in People().Where(p => p.AssignedTo == null))
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(game.Grid.rectTransform,
                RectTransformUtility.WorldToScreenPoint(camera, person.Rect.position), camera), Is.False,
                "Resizing must keep the people tray outside the grid: " + Path.GetFileName(path) + " / " + person.name + " / " + person.TablePosition);
        camera.Render();
        afterRender?.Invoke(camera);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        RenderTexture.active = previous;
        canvas.renderMode = oldMode;
        canvas.worldCamera = oldCamera;
        canvas.planeDistance = oldPlaneDistance;
        // Subsequent gameplay must use the restored canvas geometry, not the temporary capture camera's layout.
        Canvas.ForceUpdateCanvases();
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(target);
        Object.Destroy(pixels);
        Object.Destroy(cameraObject);
        Assert.That(new FileInfo(path).Length, Is.GreaterThan(1000));
    }
}
