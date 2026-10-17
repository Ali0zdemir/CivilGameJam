using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardSceneEditModeTests
{
    private CardGameController game;

    private static Sprite FindArtwork(string name)
    {
        string path = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/EfeArts" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Single(asset => System.IO.Path.GetFileNameWithoutExtension(asset) == name);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [SetUp]
    public void OpenSavedScene()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        game = Object.FindFirstObjectByType<CardGameController>();
    }

    [Test]
    public void SavedCanvasFitsTheSameDesignAreaToEveryDisplay()
    {
        var canvas = game.GetComponentInParent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
        Assert.That(scaler.referenceResolution.x, Is.GreaterThanOrEqualTo(1600f));
        Assert.That(scaler.referenceResolution.y, Is.GreaterThanOrEqualTo(900f));
        var table = (RectTransform)game.transform;
        Assert.That(table.sizeDelta.x * table.localScale.x, Is.LessThan(scaler.referenceResolution.x));
        Assert.That(table.sizeDelta.y, Is.Zero);
        Assert.That(table.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(table.anchorMax.x * table.localScale.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(table.sizeDelta.x * table.localScale.x + table.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
        Assert.That(table.anchorMax.y * table.localScale.y, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(table.localScale, Is.EqualTo(Vector3.one * 1.1f));
        Assert.That(canvas.transform.Find("ScreenBackground").gameObject.activeSelf, Is.True);
        Assert.That(canvas.transform.Find("CitySidebar"), Is.Null, "The deleted statistics panel must stay deleted.");
        Assert.That(game.Sidebar, Is.Not.Null);
        Assert.That(game.Sidebar.Slots, Is.Empty);
        Assert.That(game.Sidebar.PeopleArea, Is.Not.Null);
        Assert.That(game.Sidebar.PeopleArea.rect.width, Is.EqualTo(game.Sidebar.PeopleArea.rect.height).Within(0.01f));
        Assert.That(game.Sidebar.PeopleArea.rect.width, Is.GreaterThanOrEqualTo(500f));
        Assert.That(game.Sidebar.GetComponentsInChildren<Transform>(true).Any(child => child.name.StartsWith("PeopleSlot")), Is.False);
        Assert.That(game.Sidebar.PeopleArea.GetComponent<LayoutGroup>(), Is.Null);
        Assert.That(game.Vault.Area.Cast<Transform>().Count(child => child.name.StartsWith("BlueprintSlot")), Is.EqualTo(4));
        var viewport = canvas.transform.Find("CityView").GetComponent<CityViewport>();
        Assert.That(viewport, Is.Not.Null);
        Assert.That(viewport.WorldCamera.targetTexture, Is.Not.Null);
        Assert.That(viewport.ViewRect.GetComponent<RawImage>().texture, Is.Not.Null);
        Assert.That(viewport.Sidebar, Is.SameAs(game.Sidebar.Area));
        Assert.That(viewport.Sidebar.rect.width * ((RectTransform)game.transform).localScale.x, Is.EqualTo(712.8f).Within(0.01f));
        Assert.That(new SerializedObject(viewport).FindProperty("dock").objectReferenceValue, Is.SameAs(game.GetComponent<CardDock>()));
        Assert.That(game.GetComponent<CardDock>(), Is.Not.Null);
        Assert.That(game.GetComponent<CardDock>().ActivationArea, Is.Not.Null);
        Assert.That(GameObject.Find("World3D").GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(10));
        Assert.That(System.Attribute.IsDefined(typeof(CardGameController), typeof(ExecuteAlways)), Is.False,
            "Inspecting the scene must not silently resize the authored cards.");
    }

    [Test]
    public void SavedSceneContainsTheBoardWithoutEnteringPlayMode()
    {
        Assert.That(EditorApplication.isPlaying, Is.False);
        Assert.That(game.Grid, Is.Not.Null);
        Assert.That(game.CardLayer, Is.Not.Null);
        Assert.That(game.FactoryOutputArea, Is.Not.Null);
        Assert.That(game.FactoryOutputArea.parent, Is.SameAs(game.CardLayer));
        Assert.That(game.FactoryOutputArea.rect.width, Is.GreaterThanOrEqualTo(100f));
        var cards = game.GetComponentsInChildren<DraggableCard>();
        Assert.That(cards.Length, Is.EqualTo(11));
        Assert.That(cards.Count(c => c.Kind == CardKind.Person), Is.EqualTo(4));
        Assert.That(cards.Count(c => c.Kind == CardKind.House), Is.EqualTo(4));
        Assert.That(cards.Count(c => c.Station != null), Is.EqualTo(3));
        foreach (var slot in game.GetComponentsInChildren<WorkerSlot>(true))
            if (slot.Portrait == null)
                Assert.That(((RectTransform)slot.transform).anchoredPosition, Is.EqualTo(Vector2.zero),
                    "Worker indicators must retain their prefab anchors inside the card.");
        for (int i = 0; i < CardGrid.CellCount; i++)
        {
            Assert.That(game.Grid.GetCell(i), Is.Not.Null);
            var building = cards.FirstOrDefault(c => c.InitialGridCell == i);
            if (building != null) Assert.That(building.Rect.parent, Is.SameAs(game.Grid.GetCell(i)));
        }
        Assert.That(game.transform.Find("Population"), Is.Null);
        Assert.That(game.transform.Find("CardInstructions"), Is.Null);
    }

    [TestCase("ResearchCenterCard", true)]
    [TestCase("FactoryCard", true)]
    [TestCase("SchoolCard", true)]
    [TestCase("HouseCard", false)]
    [TestCase("PeopleCard", false)]
    [TestCase("EducatedPeopleCard", false)]
    [TestCase("RaylıSistemBlueprint", false)]
    [TestCase("DalgaKıranBlueprint", false)]
    [TestCase("YangınBlueprint", false)]
    [TestCase("RaylıSistemCard", false)]
    [TestCase("DalgaKıranCard", false)]
    [TestCase("YangınCard", false)]
    public void PrefabsContainTheirSavedControlsAndFeedback(string name, bool acceptsPeople)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
        var card = prefab.GetComponent<DraggableCard>();
        Assert.That(card, Is.Not.Null);
        if (card.IsBuilding || card.Kind == CardKind.Blueprint)
        {
            Assert.That(card.Rect.sizeDelta, Is.EqualTo(new Vector2(300f, 400f)));
            Assert.That(card.Rect.localScale.x, Is.EqualTo(0.64897f * 1.6f * 1.2f *
                (card.Kind == CardKind.Blueprint ? 0.7f : 1f)).Within(0.00001f));
            Assert.That(card.Rect.localScale.y, Is.EqualTo(card.Rect.localScale.x));
        }
        Assert.That(prefab.GetComponent<CanvasGroup>(), Is.Not.Null);
        var feedback = new SerializedObject(prefab.GetComponent<CardFeedback>());
        Assert.That(feedback.FindProperty("rect").objectReferenceValue, Is.Not.Null);
        Assert.That(feedback.FindProperty("shadow").objectReferenceValue, Is.Not.Null);
        Assert.That(feedback.FindProperty("hoverScale").floatValue, Is.EqualTo(1.21125f));
        Assert.That(feedback.FindProperty("hoverSeconds").floatValue, Is.EqualTo(0.24f));
        var hover = prefab.GetComponent<CardHover>();
        Assert.That(hover, Is.Not.Null);
        Assert.That(hover.Details, Is.Not.Null);
        bool product = card.Kind == CardKind.Upgrade;
        Assert.That(hover.Details.localScale, Is.EqualTo(Vector3.one * (product ? 1f : card.IsPerson ? 0.7176f : 0.8f)));
        Assert.That(hover.Details.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(product ? 0 : 2));
        Assert.That(hover.Details.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Length, Is.EqualTo(product ? 1 : 4));
        Assert.That(hover.Details.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        var motion = new SerializedObject(prefab.GetComponent<CardFeedback>());
        Assert.That(motion.FindProperty("dragTiltDegrees").floatValue, Is.EqualTo(4f));
        Assert.That(motion.FindProperty("tiltSmoothSeconds").floatValue, Is.GreaterThan(0f));
        foreach (var graphic in hover.Details.GetComponentsInChildren<Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False, name + ": info panels must not intercept dragging.");
        if (name == "HouseCard")
        {
            Assert.That(prefab.GetComponent<HouseUpgrades>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<HouseUpgradeSlot>(true).Length, Is.EqualTo(3));
            Assert.That(prefab.transform.Find("FolderArt/HouseIllustration"), Is.Not.Null);
            Assert.That(prefab.transform.Find("FolderArt/Klasör"), Is.Not.Null);
            Assert.That(prefab.transform.Find("FolderArt/WorkerPockets"), Is.Null);
            Assert.That(prefab.GetComponentsInChildren<WorkerSlot>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<HouseUpgradeSlot>(true).All(slot => !slot.Filled), Is.True);
            foreach (var slot in prefab.GetComponentsInChildren<HouseUpgradeSlot>(true))
            {
                var presentation = new SerializedObject(slot);
                foreach (string field in new[] { "installPreview", "installCanvas", "installGlow" })
                    Assert.That(presentation.FindProperty(field).objectReferenceValue, Is.Not.Null);
                Assert.That(slot.transform.Find("InstallPreview").gameObject.activeSelf, Is.False);
                Assert.That(slot.transform.Find("InstallGlow").GetComponent<Image>().enabled, Is.False);
            }
        }
        if (name == "FactoryCard")
        {
            Assert.That(prefab.GetComponent<FactoryProduction>(), Is.Not.Null);
            var production = new SerializedObject(prefab.GetComponent<FactoryProduction>());
            Assert.That(production.FindProperty("educatedWorkerMultiplier").floatValue, Is.EqualTo(1.8f));
            var folder = prefab.transform.Find("FolderArt");
            Assert.That(folder.Find("ProductionStatus"), Is.Not.Null);
            Assert.That(folder.Find("BlueprintInput"), Is.Not.Null);
            Assert.That(folder.Find("ProductionStatus").GetSiblingIndex(), Is.LessThan(folder.Find("WorkerPockets").GetSiblingIndex()));
            Assert.That(folder.Find("BlueprintInput").GetSiblingIndex(), Is.LessThan(folder.Find("WorkerPockets").GetSiblingIndex()));
        }
        if (card.Kind == CardKind.Factory || card.Kind == CardKind.ResearchCenter)
        {
            Assert.That(prefab.GetComponent<BuildingWorkEffects>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("WorkEffects").GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(prefab.transform.Find("WorkEffects").GetComponentsInChildren<CardSoftGraphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
        }
        if (card.Kind == CardKind.Blueprint)
        {
            var effects = prefab.GetComponent<BlueprintDiscoveryEffects>();
            Assert.That(effects, Is.Not.Null);
            Assert.That(effects.Trail.gameObject.activeSelf, Is.False);
            Assert.That(effects.Trail.GetComponent<CanvasRenderer>(), Is.Not.Null);
            var presentation = new SerializedObject(effects);
            foreach (string field in new[] { "card", "feedback", "revealArea", "flash", "rim", "shine", "trail", "flightCanvas", "flightRaycaster" })
                Assert.That(presentation.FindProperty(field).objectReferenceValue, Is.Not.Null);
        }
        if (acceptsPeople)
        {
            Assert.That(card.Station, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<WorkerSlot>(true).Length, Is.EqualTo(2));
            if (name != "HouseCard")
            {
                Assert.That(prefab.transform.Find("ProgressBar/BlueProgress"), Is.Not.Null);
                Assert.That(prefab.transform.Find("WorkStatus"), Is.Not.Null);
                var station = new SerializedObject(card.Station);
                Assert.That(station.FindProperty("progressRoot").objectReferenceValue,
                    Is.SameAs(prefab.transform.Find("ProgressBar")), name + ": progress root must be saved in the prefab.");
                Assert.That(station.FindProperty("progressFill").objectReferenceValue,
                    Is.SameAs(prefab.transform.Find("ProgressBar/BlueProgress").GetComponent<Image>()));
            }
            foreach (var slot in prefab.GetComponentsInChildren<WorkerSlot>(true))
            {
                var data = new SerializedObject(slot);
                foreach (var field in new[] { "station", "background", "portrait", "entryImage", "entryMouth", "normalPortrait", "educatedPortrait" })
                    Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, name + ": " + field);
                Assert.That(slot.Portrait.gameObject.activeSelf, Is.False);
                Assert.That(slot.EntryImage.gameObject.activeSelf, Is.False);
                Assert.That(data.FindProperty("station").objectReferenceValue, Is.SameAs(card.Station));
            }
            Assert.That(prefab.transform.Find("FolderArt/WorkerPockets").GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("FolderArt/Klasör"), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<HouseUpgradeSlot>(true), Is.Empty);
        }
        else Assert.That(card.Station, Is.Null);
        if (card.Kind == CardKind.Upgrade)
        {
            var title = hover.Details.Find("ProductName").GetComponent<TMPro.TextMeshProUGUI>();
            Assert.That(title.color, Is.EqualTo(Color.white));
            Assert.That(title.fontSharedMaterial.GetFloat(TMPro.ShaderUtilities.ID_OutlineWidth), Is.GreaterThan(0f));
            Assert.That(hover.Details.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(hover.Details.pivot, Is.EqualTo(new Vector2(0.5f, 0f)));
            var whiteOutline = prefab.transform.Find("HoverOutline").GetComponent<Image>();
            Assert.That(whiteOutline.enabled, Is.False);
            Assert.That(whiteOutline.color, Is.EqualTo(Color.white));
            Assert.That(whiteOutline.fillCenter, Is.False);
            Assert.That(whiteOutline.raycastTarget, Is.False);
            Assert.That(new SerializedObject(hover).FindProperty("hoverOutline").objectReferenceValue, Is.SameAs(whiteOutline));
            var outline = prefab.transform.Find("NewItemOutline").GetComponent<Image>();
            Assert.That(outline, Is.Not.Null);
            Assert.That(outline.enabled, Is.False);
            Assert.That(outline.raycastTarget, Is.False);
            Assert.That(outline.fillCenter, Is.False);
            Assert.That(outline.sprite, Is.Not.Null);
            Assert.That(new SerializedObject(card).FindProperty("newItemOutline").objectReferenceValue, Is.SameAs(outline));
            Assert.That(card.Rect.sizeDelta, Is.EqualTo(new Vector2(200f, 200f)));
            var artwork = prefab.GetComponentInChildren<UpgradeIcon>(true).Artwork;
            string spriteName = card.UpgradeType == UpgradeKind.RailSystem ? "Siesmic Isolator logo" :
                card.UpgradeType == UpgradeKind.Breakwater ? "Elevated Floor Logo" : "Fire Extingusiher Logo";
            Assert.That(artwork, Is.SameAs(FindArtwork(spriteName)));
        }
        if (card.Kind == CardKind.Blueprint)
        {
            string art = new[] { "Seismic Big Card (1)", "Elevated Floor Big Card", "Detector Big Card" }[(int)card.UpgradeType - 1];
            Assert.That(prefab.GetComponentInChildren<UpgradeIcon>(true).Artwork,
                Is.SameAs(FindArtwork(art)));
            Assert.That(prefab.transform.Find("BlueprintTitle").GetComponent<TMPro.TextMeshProUGUI>().raycastTarget, Is.False);
        }
        if (card.IsPerson)
        {
            string artName = card.Kind == CardKind.Person ? "People Card Small" : "Educated People Card Small";
            var sprite = FindArtwork(artName);
            var artwork = prefab.transform.Find("Artwork").GetComponent<Image>();
            Assert.That(artwork.sprite, Is.SameAs(sprite));
            Assert.That(artwork.rectTransform.sizeDelta, Is.EqualTo(sprite.rect.size), "Use the supplied artwork at native size.");
            Assert.That(artwork.transform.localScale, Is.EqualTo(Vector3.one * 0.55f));
            Assert.That(artwork.raycastTarget, Is.False, "Transparent gutters must not intercept the adjacent card.");
            Assert.That(prefab.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(prefab.transform.Find("Text (TMP)"), Is.Null);
            Assert.That(prefab.transform.Find("PersonType"), Is.Null);
        }
        foreach (var label in prefab.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            Assert.That(label.text, Does.Not.Match(@"(?i)(%|\b\d+(?:[.,]\d+)?\s*(sn|san[iİıI]ye|seconds?)\b)"),
                name + ": saved card information must not show seconds or percentages.");
    }

    [Test]
    public void BlueprintButtonsAndCallbacksAreSavedInTheHierarchy()
    {
        var choice = game.transform.Find("BlueprintChoice");
        Assert.That(choice, Is.Not.Null);
        Assert.That(choice.gameObject.activeSelf, Is.False);
        var buttons = choice.GetComponentsInChildren<Button>(true);
        Assert.That(buttons.Length, Is.EqualTo(3));
        foreach (var button in buttons)
        {
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(game));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("SelectBlueprint"));
        }
        Assert.That(choice.GetComponentsInChildren<DraggableCard>(true), Is.Empty);
        Assert.That(choice.GetComponentsInChildren<CardHover>(true), Is.Empty);
    }

    [Test]
    public void VaultAndLargerBlueprintChoicesAreSavedWithTwoProductRecipes()
    {
        Canvas.ForceUpdateCanvases();
        Assert.That(EditorApplication.isPlaying, Is.False);
        Assert.That(game.Vault, Is.Not.Null);
        Assert.That(game.Vault.Area.parent, Is.SameAs(game.Sidebar.transform));
        Assert.That(game.Vault.Slots.Count, Is.EqualTo(3));
        Assert.That(game.Vault.Area.rect.width, Is.GreaterThan(456f));
        Assert.That(game.Vault.Area.rect.height, Is.GreaterThan(206f));
        Assert.That(game.Vault.GetComponentsInChildren<DraggableCard>(true), Is.Empty);
        var settings = new SerializedObject(game);
        var options = settings.FindProperty("blueprints");
        var choice = game.transform.Find("BlueprintChoice");
        Assert.That(choice.GetComponent<Image>().color.a, Is.LessThan(0.6f));
        Assert.That(((RectTransform)choice.Find("SelectionWindow")).rect.width, Is.GreaterThanOrEqualTo(1050f));
        for (int i = 0; i < 3; i++)
        {
            Assert.That(options.GetArrayElementAtIndex(i).FindPropertyRelative("productCount").intValue, Is.EqualTo(2));
            var slot = game.Vault.SlotFor((UpgradeKind)(i + 1));
            Assert.That(slot, Is.Not.Null);
            string art = new[] { "Seismic Big Card (1)", "Elevated Floor Big Card", "Detector Big Card" }[i];
            var sprite = FindArtwork(art);
            Assert.That(slot.CardRect.Find("CardSymbol").GetComponent<UpgradeIcon>().Artwork, Is.SameAs(sprite));
            Assert.That(choice.Find("BlueprintOption" + i + "/CardPreview/CardSymbol").GetComponent<UpgradeIcon>().Artwork, Is.SameAs(sprite));
            var data = new SerializedObject(slot);
            foreach (string field in new[] { "vault", "cardRect", "background", "icon", "discovery", "quantity" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(slot.CardRect.rect.height, Is.GreaterThan(190f));
            Assert.That(slot.CardRect.rect.size, Is.EqualTo(new Vector2(300f, 400f)));
            Assert.That(Vector3.Distance(slot.CardRect.localScale,
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HouseCard.prefab").transform.localScale * 0.7f), Is.LessThan(0.00001f));
            var hover = slot.GetComponent<CardHover>();
            Assert.That(hover, Is.Not.Null);
            Assert.That(new SerializedObject(hover).FindProperty("vaultSlot").objectReferenceValue, Is.SameAs(slot));
            Assert.That(slot.GetComponent<CardFeedback>(), Is.Not.Null);
            Assert.That(hover.Details.parent, Is.SameAs(slot.CardRect));
            Assert.That(hover.Details.gameObject.activeSelf, Is.False);
            Assert.That(hover.Details.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            foreach (string path in new[] { "Information/Title", "Information/Description", "Properties/Title", "Properties/Description" })
                Assert.That(hover.Details.Find(path), Is.Not.Null, "Vault information must be saved in the Hierarchy: " + path);
            var button = (RectTransform)choice.Find("BlueprintOption" + i);
            Assert.That(button.rect.width, Is.EqualTo(310f));
            Assert.That(button.rect.height, Is.EqualTo(450f));
            var preview = button.Find("CardPreview").GetComponent<RectTransform>();
            Assert.That(preview.rect.size, Is.EqualTo(slot.CardRect.rect.size));
            Assert.That(preview.localScale, Is.EqualTo(slot.CardRect.localScale));
            if (i > 0)
                Assert.That(button.anchoredPosition.x - ((RectTransform)choice.Find("BlueprintOption" + (i - 1))).anchoredPosition.x - button.rect.width,
                    Is.EqualTo(24f).Within(0.01f));
        }
        var vaultCorners = new Vector3[4]; game.Vault.Area.GetWorldCorners(vaultCorners);
        foreach (var corner in vaultCorners)
        {
            Vector2 point = game.Sidebar.Area.InverseTransformPoint(corner);
            Assert.That(point.x, Is.InRange(game.Sidebar.Area.rect.xMin - 0.01f, game.Sidebar.Area.rect.xMax + 0.01f));
            Assert.That(point.y, Is.InRange(game.Sidebar.Area.rect.yMin - 0.01f, game.Sidebar.Area.rect.yMax + 0.01f));
        }
        foreach (var person in game.GetComponentsInChildren<DraggableCard>().Where(c => c.IsPerson))
        {
            var personCorners = new Vector3[4]; person.Rect.GetWorldCorners(personCorners);
            Assert.That(vaultCorners[0].y, Is.GreaterThan(personCorners[1].y), "The Vault must sit above the people.");
        }
    }

    [Test]
    public void SavedUIIsEnglishAndUndiscoveredVaultSlotsAreCompletelyEmpty()
    {
        foreach (var root in game.gameObject.scene.GetRootGameObjects())
            foreach (var label in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                Assert.That(label.text, Does.Not.Match(@"[çğıöşüÇĞİÖŞÜ]|\b(EV|KART|kişi)\b"), label.name);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            foreach (var label in AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid))
                .GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                Assert.That(label.text, Does.Not.Match(@"[çğıöşüÇĞİÖŞÜ]|\b(EV|KART|kişi)\b"), label.name);
        Assert.That(game.transform.Find("Population"), Is.Null);
        Assert.That(game.transform.Find("CardInstructions"), Is.Null);
        foreach (var slot in game.Vault.Slots)
        {
            Assert.That(slot.CardRect.gameObject.activeSelf, Is.False);
            var data = new SerializedObject(slot);
            foreach (string field in new[] { "discovery", "quantity" })
            {
                var label = (TMPro.TextMeshProUGUI)data.FindProperty(field).objectReferenceValue;
                Assert.That(label.gameObject.activeSelf, Is.False);
                Assert.That(label.text, Is.Empty);
            }
        }
    }

    [Test]
    public void GridHasNoDividerLinesAndSceneHasNoMissingScripts()
    {
        Canvas.ForceUpdateCanvases();
        var mesh = game.Grid.canvasRenderer.GetMesh();
        Assert.That(mesh == null || mesh.vertexCount == 0, Is.True, "The saved board must have no divider lines.");
        game.Grid.SetPreview(CardGrid.CellCount - 1);
        Canvas.ForceUpdateCanvases();
        mesh = game.Grid.canvasRenderer.GetMesh();
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh.vertexCount, Is.EqualTo(4), "Only the hovered drop target should be highlighted.");
        game.Grid.SetPreview(-1);
        foreach (var root in game.gameObject.scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
    }

    [Test]
    public void SavedCardSizesAndAllEightSlotsMatchTheAuthoredLayout()
    {
        Canvas.ForceUpdateCanvases();
        var cards = game.GetComponentsInChildren<DraggableCard>();
        var example = cards.Single(c => c.Kind == CardKind.ResearchCenter);
        Assert.That(example.Rect.sizeDelta, Is.EqualTo(new Vector2(300f, 400f)));
        Assert.That(example.Rect.localScale.x, Is.EqualTo(0.64897f * 1.6f * 1.2f).Within(0.00001f));
        Assert.That(example.Rect.localScale.y, Is.EqualTo(example.Rect.localScale.x));
        Assert.That(game.Grid.transform.Cast<Transform>().Count(t => t.name.StartsWith("Cell_")), Is.EqualTo(8));
        foreach (var card in cards.Where(c => c.IsBuilding))
        {
            Assert.That(card.Rect.sizeDelta, Is.EqualTo(example.Rect.sizeDelta), card.name);
            Assert.That(card.Rect.localScale, Is.EqualTo(example.Rect.localScale), card.name);
            Assert.That(card.Rect.anchoredPosition, Is.EqualTo(Vector2.zero), card.name);
        }
        foreach (var card in cards.Where(c => c.IsPerson))
        {
            Assert.That(card.Rect.sizeDelta, Is.EqualTo(new Vector2(240f, 300f) * 0.55f), card.name);
            Assert.That(card.Rect.localScale, Is.EqualTo(Vector3.one), card.name);
        }
        var first = game.Grid.GetCellWorldPosition(0);
        var second = game.Grid.GetCellWorldPosition(1);
        var nextRow = game.Grid.GetCellWorldPosition(CardGrid.Columns);
        var firstLocal = game.Grid.rectTransform.InverseTransformPoint(first);
        var secondLocal = game.Grid.rectTransform.InverseTransformPoint(second);
        var nextLocal = game.Grid.rectTransform.InverseTransformPoint(nextRow);
        float gapX = secondLocal.x - firstLocal.x - game.Grid.CellSize.x;
        float gapY = firstLocal.y - nextLocal.y - game.Grid.CellSize.y;
        Assert.That(gapX, Is.EqualTo(27.5472f).Within(0.01f));
        Assert.That(gapY, Is.EqualTo(gapX).Within(0.01f), "Row spacing should be as compact as column spacing.");
        var people = cards.Where(c => c.IsPerson).OrderBy(c => c.name).ToArray();
        for (int i = 0; i < people.Length; i++)
        {
            Assert.That(people[i].Rect.parent, Is.SameAs(game.Sidebar.PeopleArea), people[i].name);
            Assert.That(game.Sidebar.PeopleArea.rect.Contains(game.Sidebar.PeopleArea.InverseTransformPoint(people[i].Rect.position)), Is.True);
        }
        var corners = new Vector3[4];
        game.Grid.GetCell(0).GetWorldCorners(corners);
        var sideCorners = new Vector3[4]; game.Sidebar.Area.GetWorldCorners(sideCorners);
        var canvasRect = (RectTransform)game.GetComponentInParent<Canvas>().rootCanvas.transform;
        float sidebarGap = canvasRect.InverseTransformPoint(corners[0]).x - canvasRect.InverseTransformPoint(sideCorners[3]).x;
        Assert.That(sidebarGap, Is.GreaterThanOrEqualTo(20f), "The authored building grid must not overlap the left card sidebar.");
        for (int i = 0; i < CardGrid.CellCount; i++)
        {
            var cell = game.Grid.GetCell(i);
            Assert.That(cell, Is.Not.Null);
            Vector3 expected = first + (second - first) * (i % CardGrid.Columns) +
                (nextRow - first) * (i / CardGrid.Columns);
            Assert.That(Vector3.Distance(cell.position, expected), Is.LessThan(0.01f), cell.name);
            cell.GetWorldCorners(corners);
            foreach (var corner in corners)
                Assert.That(game.Grid.rectTransform.rect.Contains(game.Grid.rectTransform.InverseTransformPoint(corner)),
                    Is.True, cell.name + " must fit inside GridPanel.");
        }
    }
}
