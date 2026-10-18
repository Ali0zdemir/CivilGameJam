using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator FactoryProductsStayInThe2DPanelAndOutlineUntilTheirOwnFirstLeftClick()
    {
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>();
        factory.enabled = false;
        var dock = game.GetComponent<CardDock>();
        for (int type = 0; type < 3; type++)
        {
            EnsurePresentationWorkers(FactoryProduction.RequiredWorkersFor((UpgradeKind)(type + 1)) + 1);
            var plan = ResearchPlan(type);
            AssignFactoryWorkers(plan.UpgradeType);
            factory.TryInsertBlueprint(plan);
            factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
            // Completing production while the drawer is already moving must not leave
            // the products at obsolete world positions in the city above it.
            dock.SetExpanded(false, true);
            yield return new WaitForSecondsRealtime(0.12f);
            dock.SetExpanded(true, true);
        }
        yield return new WaitForSecondsRealtime(0.5f);
        var products = game.Cards.Where(card => card.Kind == CardKind.Upgrade).ToArray();
        Assert.That(products.Length, Is.EqualTo(6));
        var inventory = game.Sidebar.UpgradesArea;
        foreach (var product in products)
        {
            Assert.That(product.Rect.parent, Is.SameAs(inventory));
            Assert.That(game.Sidebar.Owns(product), Is.True);
            var label = product.transform.Find("InventoryName").GetComponent<TMPro.TMP_Text>();
            string expected = product.UpgradeType == UpgradeKind.RailSystem ? "Rail System"
                : product.UpgradeType == UpgradeKind.Breakwater ? "Elevated Floor" : "Fire Sprinkler";
            Assert.That(label.text, Is.EqualTo(expected));
            Assert.That(label.gameObject.activeInHierarchy, Is.True);
            Assert.That(product.IsNewProduct, Is.True);
            Assert.That(product.transform.Find("NewItemOutline").GetComponent<Image>().enabled, Is.True);
            AssertRectInside(product.Rect, game.FactoryOutputArea);
        }
        dock.SetExpanded(false, true);
        foreach (var product in products)
        {
            AssertRectInside(product.Rect, game.FactoryOutputArea);
            var corners = new Vector3[4]; product.Rect.GetWorldCorners(corners);
            foreach (var point in corners)
                Assert.That(RectTransformUtility.WorldToScreenPoint(null, point).y, Is.GreaterThanOrEqualTo(0f));
        }
        dock.SetExpanded(true, true);
        var savedUI = UIIds(game);
        foreach (var product in products.GroupBy(card => card.UpgradeType).Select(group => group.First()))
        {
            Hover(product, true);
            yield return new WaitForSecondsRealtime(0.32f);
            var details = product.GetComponent<CardHover>().Details;
            var name = details.Find("ProductName").GetComponent<TMPro.TextMeshProUGUI>();
            Assert.That(details.gameObject.activeSelf, Is.True);
            Assert.That(name.gameObject.activeInHierarchy, Is.False, "Hover must not show a second product name.");
            Assert.That(product.GetComponentsInChildren<TMPro.TMP_Text>().Length, Is.EqualTo(1), "Only the persistent inventory name remains visible.");
            Assert.That(name.text, Is.EqualTo(product.transform.Find("InventoryName").GetComponent<TMPro.TMP_Text>().text));
            Assert.That(name.color, Is.EqualTo(Color.white));
            Assert.That(product.transform.Find("HoverOutline").GetComponent<Image>().enabled, Is.True);
            Assert.That(product.IsNewProduct, Is.True);
            Assert.That(details.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(UIIds(game), Is.EqualTo(savedUI), "Hover must reuse the product's saved label and outline.");
            if (product.UpgradeType == UpgradeKind.Fire)
                yield return CaptureCanvas(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/factory-product-name-hover.png"), product.gameObject);
            Hover(product, false);
            Assert.That(details.gameObject.activeSelf, Is.False);
            Assert.That(product.transform.Find("HoverOutline").GetComponent<Image>().enabled, Is.False);
        }
        var first = products[0];
        Hover(first, true);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(first.IsNewProduct, Is.True, "Hover must leave the reward highlighted.");
        Hover(first, false);
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Right, pointerId = -1,
            position = RectTransformUtility.WorldToScreenPoint(null, first.Rect.position)
        };
        first.OnPointerDown(pointer);
        Assert.That(first.IsNewProduct, Is.True);
        pointer.button = PointerEventData.InputButton.Left;
        first.OnPointerDown(pointer);
        Assert.That(first.IsNewProduct, Is.False);
        Assert.That(first.transform.Find("NewItemOutline").GetComponent<Image>().enabled, Is.False);
        first.OnPointerUp(pointer);
        Hover(first, true);
        yield return new WaitForSecondsRealtime(0.32f);
        Assert.That(first.transform.Find("HoverOutline").GetComponent<Image>().enabled, Is.True,
            "Previously acknowledged products still need their white hover outline.");
        Assert.That(first.transform.Find("NewItemOutline").GetComponent<Image>().enabled, Is.False);
        Hover(first, false);
        Assert.That(products.Skip(1).All(card => card.IsNewProduct), Is.True, "A click acknowledges only that product.");
        Vector3 chosenPosition = inventory.TransformPoint(inventory.InverseTransformPoint(first.Rect.position) + Vector3.left * 20f);
        Drag(first, chosenPosition);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(first.Rect.parent, Is.SameAs(inventory));
        Assert.That(Vector3.Distance(first.Rect.position, chosenPosition), Is.LessThan(.01f), "Upgrade drops retain the player's free placement.");
        Drag(first, inventory.TransformPoint(new Vector2(inventory.rect.xMax - 2f, inventory.rect.yMin + 2f)));
        yield return new WaitForSecondsRealtime(.6f);
        AssertRectInside(first.Rect, inventory);
        Drag(first, chosenPosition);
        yield return new WaitForSecondsRealtime(.6f);
        var house = game.Cards.First(card => card.Kind == CardKind.House).GetComponent<HouseUpgrades>();
        Assert.That(Vector3.Distance(first.Rect.position, chosenPosition), Is.LessThan(.01f), "The final free drop must finish at the chosen position before installing.");
        house.TryInstall(first);
        house.Release(first.UpgradeType, false);
        Assert.That(first.IsNewProduct, Is.False, "Taking a used item out of a house must not mark it as newly manufactured.");
        Assert.That(first.Rect.parent, Is.SameAs(inventory));
        Assert.That(Vector3.Distance(first.Rect.position, chosenPosition), Is.LessThan(.01f), "Released upgrades remember their inventory position even in a full tray.");
        yield return new WaitForSecondsRealtime(0.3f);
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        Rect outputViewport = default;
        yield return CaptureCanvas(Path.Combine(directory, "factory-items-outlined.png"), afterRender: camera => outputViewport = OutputViewportRect());
        AssertVisibleProductOutlines(Path.Combine(directory, "factory-items-outlined.png"), outputViewport);
        dock.SetExpanded(false, true);
        yield return CaptureCanvas(Path.Combine(directory, "factory-items-collapsed.png"), afterRender: camera => outputViewport = OutputViewportRect());
        AssertVisibleProductOutlines(Path.Combine(directory, "factory-items-collapsed.png"), outputViewport);
    }

    [UnityTest]
    public IEnumerator PickingUpAnEdgeHoveredPersonDoesNotSnapBeforeTheFolderEntry()
    {
        var worker = People()[0];
        worker.Rect.SetParent(game.CardLayer, true);
        var root = (RectTransform)game.GetComponentInParent<Canvas>().rootCanvas.transform;
        worker.Rect.position = root.TransformPoint(new Vector3(root.rect.xMax - 70f, root.rect.yMax - 70f));
        Hover(worker, true);
        yield return new WaitForSecondsRealtime(0.32f);
        Vector3 visiblePosition = worker.Rect.position;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, visiblePosition) + new Vector2(10f, -5f),
            button = PointerEventData.InputButton.Left, pointerId = -1
        };
        worker.OnPointerDown(pointer);
        worker.OnBeginDrag(pointer);
        Assert.That(Vector3.Distance(worker.Rect.position, visiblePosition), Is.LessThan(0.01f));
        worker.OnDrag(pointer);
        Assert.That(Vector3.Distance(worker.Rect.position, visiblePosition), Is.LessThan(0.01f));
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, school.Card.Rect.position);
        worker.OnDrag(pointer);
        var sourceCorners = new Vector3[4]; ((RectTransform)worker.transform.Find("Artwork")).GetWorldCorners(sourceCorners);
        worker.OnEndDrag(pointer);
        var slot = school.GetComponentsInChildren<WorkerSlot>(true).Single(item => item.IsEntering);
        var entryCorners = new Vector3[4]; slot.EntryImage.rectTransform.GetWorldCorners(entryCorners);
        for (int i = 0; i < 4; i++)
            Assert.That(Vector3.Distance(entryCorners[i], sourceCorners[i]), Is.LessThan(0.01f));
    }

    [UnityTest]
    public IEnumerator BothWorkerPocketsFollowEveryWorkplaceWhileTheDrawerMoves()
    {
        var dock = game.GetComponent<CardDock>();
        var duration = typeof(WorkerSlot).GetField("entrySeconds", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var kind in new[] { CardKind.ResearchCenter, CardKind.Factory, CardKind.School })
        {
            dock.SetExpanded(true, true);
            var station = Station(kind);
            var slots = station.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
            foreach (var slot in slots) duration.SetValue(slot, 2f);
            var workers = People().Take(2).ToArray();
            for (int i = 0; i < 2; i++) Drag(workers[i], station.Card.Rect.position);
            yield return new WaitForSecondsRealtime(0.06f);
            var before = slots.Select(slot => game.CardLayer.InverseTransformPoint(slot.EntryImage.rectTransform.position)).ToArray();
            dock.SetExpanded(false, true);
            yield return null;
            for (int i = 0; i < 2; i++)
            {
                var point = game.CardLayer.InverseTransformPoint(slots[i].EntryImage.rectTransform.position);
                Assert.That(Vector3.Distance(point, before[i]), Is.LessThan(100f), kind + ": moving the panel must not leave the entry animation behind.");
                Assert.That(workers[i].AssignedTo, Is.SameAs(station));
                Assert.That(slots[i].EntryImage.gameObject.activeSelf, Is.True);
            }
            // Releasing during entry must cancel the old proxy before it can finish later.
            station.ReleaseWorker(0, false);
            station.ReleaseWorker(1, false);
            Assert.That(slots.All(slot => !slot.IsEntering && !slot.EntryImage.gameObject.activeSelf), Is.True);
        }
    }

    [UnityTest]
    public IEnumerator ProductsAndReturningWorkersReachTheirMovingTargetsAfterAResolutionChange()
    {
        var canvas = game.GetComponentInParent<Canvas>();
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        var cameraObject = new GameObject("FactoryOutputResolutionCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 450f;
        camera.nearClipPlane = 0.1f; camera.farClipPlane = 100f;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 10f;
        RenderTexture target = null;
        var dock = game.GetComponent<CardDock>();
        try
        {
            var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
            factory.enabled = false;
            Station(CardKind.Factory).TryAssign(People()[3]);
            factory.TryInsertBlueprint(ResearchPlan(0));
            factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
            foreach (var resolution in new[] { new Vector2Int(3440, 1440), new Vector2Int(960, 540), new Vector2Int(3840, 2160), new Vector2Int(1080, 1920) })
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(resolution.x, resolution.y, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                dock.SetExpanded(false, true);
                yield return new WaitForSecondsRealtime(0.55f);
                Canvas.ForceUpdateCanvases();
                foreach (var product in game.Cards.Where(card => card.Kind == CardKind.Upgrade))
                {
                    AssertRectInside(product.Rect, game.FactoryOutputArea);
                    AssertOnScreen(product.Rect, camera, resolution);
                    Assert.That(product.IsNewProduct, Is.True);
                }
                foreach (var person in People().Where(card => !card.IsStored))
                    if (game.Sidebar.Owns(person))
                    {
                        AssertRectInside(person.Rect, game.Sidebar.PeopleArea);
                        Assert.That(Vector2.Distance(person.TablePosition, game.CardLayer.InverseTransformPoint(person.Rect.position)),
                            Is.LessThan(0.01f), "Returned workers must reach their live position in the free people area.");
                    }
                dock.SetExpanded(true, true);
                school.TryAssign(People()[0]);
                school.ReleaseWorker(0);
                // Start another flight immediately before the next resize.
                foreach (var product in game.Cards.Where(card => card.Kind == CardKind.Upgrade))
                    game.ReturnToTable(product, Station(CardKind.Factory).Card.Rect.position);
            }
        }
        finally
        {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject);
            Canvas.ForceUpdateCanvases();
        }
    }

    private static void AssertRectInside(RectTransform card, RectTransform area)
    {
        var corners = new Vector3[4]; card.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            Vector2 point = area.InverseTransformPoint(corner);
            Assert.That(point.x, Is.InRange(area.rect.xMin - 0.01f, area.rect.xMax + 0.01f), card.name);
            Assert.That(point.y, Is.InRange(area.rect.yMin - 0.01f, area.rect.yMax + 0.01f), card.name);
        }
    }

    private Rect OutputViewportRect()
    {
        var canvas = (RectTransform)game.GetComponentInParent<Canvas>().rootCanvas.transform;
        var corners = new Vector3[4]; game.Sidebar.UpgradesArea.GetWorldCorners(corners);
        var min = canvas.InverseTransformPoint(corners[0]);
        var max = canvas.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect((min.x - canvas.rect.xMin) / canvas.rect.width,
            (min.y - canvas.rect.yMin) / canvas.rect.height, (max.x - canvas.rect.xMin) / canvas.rect.width,
            (max.y - canvas.rect.yMin) / canvas.rect.height);
    }

    private void AssertVisibleProductOutlines(string screenshot, Rect viewport)
    {
        var image = new Texture2D(2, 2);
        try
        {
            image.LoadImage(File.ReadAllBytes(screenshot));
            var pixels = image.GetPixels32();
            // The capture restores the original Game view; sample the inventory bounds from the captured viewport.
            int left = Mathf.Clamp(Mathf.FloorToInt(viewport.xMin * image.width), 0, image.width - 1);
            int right = Mathf.Clamp(Mathf.CeilToInt(viewport.xMax * image.width), 0, image.width);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(viewport.yMin * image.height), 0, image.height - 1);
            int top = Mathf.Clamp(Mathf.CeilToInt(viewport.yMax * image.height), 0, image.height);
            int gold = 0;
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                {
                    var pixel = pixels[y * image.width + x];
                    if (pixel.r > 230 && pixel.g > 170 && pixel.g < 235 && pixel.b < 120) gold++;
                }
            Assert.That(gold, Is.GreaterThan(100), "Unclicked products need a clearly visible outline in the 2D output tray.");
        }
        finally { Object.Destroy(image); }
    }
}
