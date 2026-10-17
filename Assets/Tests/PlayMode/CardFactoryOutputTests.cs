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
            EnsurePresentationWorkers(2);
            var plan = ResearchPlan(type);
            Assert.That(station.TryAssign(People().First(person => person.CanStartWork && !person.IsStored)), Is.True);
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
        foreach (var product in products)
        {
            Assert.That(product.Rect.parent, Is.SameAs(game.CardLayer));
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
            Assert.That(name.text, Is.EqualTo(game.RecipeFor(product.UpgradeType).title));
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
        var house = game.Cards.First(card => card.Kind == CardKind.House).GetComponent<HouseUpgrades>();
        house.TryInstall(first);
        house.Release(first.UpgradeType, false);
        Assert.That(first.IsNewProduct, Is.False, "Taking a used item out of a house must not mark it as newly manufactured.");
        yield return new WaitForSecondsRealtime(0.3f);
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        yield return CaptureCanvas(Path.Combine(directory, "factory-items-outlined.png"));
        AssertVisibleProductOutlines(Path.Combine(directory, "factory-items-outlined.png"));
        dock.SetExpanded(false, true);
        yield return CaptureCanvas(Path.Combine(directory, "factory-items-collapsed.png"));
        AssertVisibleProductOutlines(Path.Combine(directory, "factory-items-collapsed.png"));
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

    private static void AssertVisibleProductOutlines(string screenshot)
    {
        var image = new Texture2D(2, 2);
        try
        {
            image.LoadImage(File.ReadAllBytes(screenshot));
            var pixels = image.GetPixels32();
            int gold = 0;
            for (int y = 0; y < image.height; y++)
                for (int x = image.width * 9 / 10; x < image.width; x++)
                {
                    var pixel = pixels[y * image.width + x];
                    if (pixel.r > 230 && pixel.g > 170 && pixel.g < 235 && pixel.b < 120) gold++;
                }
            Assert.That(gold, Is.GreaterThan(100), "Unclicked products need a clearly visible outline in the 2D output tray.");
        }
        finally { Object.Destroy(image); }
    }
}
