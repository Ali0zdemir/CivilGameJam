using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    // This tool writes the presentation to the scene and prefabs, before Play Mode.
    public static class CardWorkplaceAuthoring
    {
        private const string ScenePath = "Assets/2DCardScene.unity";
        private const float CardScale = 0.64897f * 1.6f * 1.2f;
        private const float BlueprintScale = CardScale * 0.7f;
        private const float SidebarWidth = 648f;
        private const float VaultHeight = 70f + 2f * (400f * BlueprintScale + 42f) + 24f;
        private static readonly Color Ink = new Color(0.16f, 0.20f, 0.19f);

        [MenuItem("Tools/Card Game/Center Saved Blueprint Selection On Screen")]
        public static void CenterBlueprintSelectionForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Center the saved blueprint selection in Edit Mode.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var game = Object.FindFirstObjectByType<CardGameController>();
            var choice = (RectTransform)game.transform.Find("BlueprintChoice");
            foreach (RectTransform child in choice)
                child.anchorMin = child.anchorMax = new Vector2(.5f, .5f);
            var overlay = game.GetComponent<CardCanvasOverlay>() ?? game.gameObject.AddComponent<CardCanvasOverlay>();
            var data = new SerializedObject(overlay);
            data.FindProperty("overlay").objectReferenceValue = choice;
            data.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            overlay.RefreshLayout();
            var root = (RectTransform)game.GetComponentInParent<Canvas>().rootCanvas.transform;
            var window = (RectTransform)choice.Find("SelectionWindow");
            if (Vector3.Distance(window.position, root.TransformPoint(root.rect.center)) > .01f)
                throw new InvalidOperationException("Blueprint selection is not centered on the canvas.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the blueprint selection at the full canvas center with its existing artwork, sizes and callbacks.");
        }

        private static Rect[] workerReferenceBounds;
        private static readonly Dictionary<Sprite, Rect> artworkBounds = new Dictionary<Sprite, Rect>();

        private static void ReadWorkerReference()
        {
            var game = Object.FindFirstObjectByType<CardGameController>();
            var example = game.transform.Find("ReferenceCards/Card") ?? game.Grid.transform.Find("Card");
            if (example == null) throw new InvalidOperationException("The authored Card example is missing.");
            var frame = example.Find("DışKenar").GetComponent<Image>();
            Rect drawing = DrawingRect(frame);
            workerReferenceBounds = new Rect[2];
            for (int i = 0; i < workerReferenceBounds.Length; i++)
            {
                var image = example.Find((i + 1) + ".eleman").GetComponent<Image>();
                Rect bounds = BoundsIn(image, frame.rectTransform);
                workerReferenceBounds[i] = Rect.MinMaxRect((bounds.xMin - drawing.xMin) / drawing.width,
                    (bounds.yMin - drawing.yMin) / drawing.height, (bounds.xMax - drawing.xMin) / drawing.width,
                    (bounds.yMax - drawing.yMin) / drawing.height);
            }
            Debug.Log("Authored Card worker bounds: " + workerReferenceBounds[0] + "; " + workerReferenceBounds[1]);
        }

        private static void MatchWorkerPockets(GameObject root)
        {
            if (workerReferenceBounds == null) ReadWorkerReference();
            var folder = root.transform.Find("FolderArt").GetComponent<RectTransform>();
            var slots = folder.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
            float lip = BoundsIn(folder.Find("Klasör").GetComponent<Image>(), folder).yMax;
            for (int i = 0; i < slots.Length; i++)
            {
                var reference = workerReferenceBounds[i];
                var visible = new Rect(folder.rect.min + Vector2.Scale(reference.min, folder.rect.size),
                    Vector2.Scale(reference.size, folder.rect.size));
                var pocket = (RectTransform)slots[i].transform;
                Place(pocket, new Vector2(0.5f, 0.5f), visible.center, visible.size, new Vector2(0.5f, 0.5f));
                var data = new SerializedObject(slots[i]);
                var sprite = (Sprite)data.FindProperty("normalPortrait").objectReferenceValue;
                Rect pixels = ArtworkBounds(sprite);
                float width = visible.width / pixels.width;
                var size = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
                var center = new Vector2(visible.center.x - (pixels.center.x - 0.5f) * size.x,
                    visible.yMax - (pixels.yMax - 0.5f) * size.y);
                // The supplied Card clips the lower edge of each person at the folder boundary.
                Place(slots[i].Portrait.rectTransform, new Vector2(0.5f, 0.5f), center - visible.center,
                    size, new Vector2(0.5f, 0.5f));
                var mouth = pocket.Find("EntryMouth") as RectTransform;
                if (mouth == null)
                {
                    mouth = new GameObject("EntryMouth", typeof(RectTransform)).GetComponent<RectTransform>();
                    mouth.SetParent(pocket, false); mouth.gameObject.layer = pocket.gameObject.layer;
                }
                Place(mouth, new Vector2(0.5f, 0.5f),
                    new Vector2(center.x, lip + size.y / 2f + 16f) - visible.center, Vector2.zero, new Vector2(0.5f, 0.5f));
                Refs(slots[i], ("entryMouth", mouth));
                PrefabUtility.RecordPrefabInstancePropertyModifications(slots[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pocket);
                PrefabUtility.RecordPrefabInstancePropertyModifications(slots[i].Portrait.rectTransform);
            }
        }

        private static Rect BoundsIn(Image image, RectTransform target)
        {
            Rect drawing = DrawingRect(image), pixels = ArtworkBounds(image.sprite);
            var min = Vector2.positiveInfinity; var max = Vector2.negativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                var uv = new Vector2(i % 2 == 0 ? pixels.xMin : pixels.xMax, i < 2 ? pixels.yMin : pixels.yMax);
                Vector2 point = target.InverseTransformPoint(image.rectTransform.TransformPoint(drawing.min + Vector2.Scale(uv, drawing.size)));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect DrawingRect(Image image)
        {
            Rect rect = image.rectTransform.rect;
            if (!image.preserveAspect || image.sprite == null) return rect;
            float aspect = image.sprite.rect.width / image.sprite.rect.height;
            if (rect.width / rect.height > aspect)
            { float width = rect.height * aspect; rect.x += (rect.width - width) * image.rectTransform.pivot.x; rect.width = width; }
            else
            { float height = rect.width / aspect; rect.y += (rect.height - height) * image.rectTransform.pivot.y; rect.height = height; }
            return rect;
        }

        private static Rect ArtworkBounds(Sprite sprite)
        {
            if (artworkBounds.TryGetValue(sprite, out var bounds)) return bounds;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                var pixels = texture.GetPixels32();
                int left = texture.width, bottom = texture.height, right = -1, top = -1;
                for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                    {
                        if (pixels[y * texture.width + x].a == 0) continue;
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    }
                bounds = new Rect((float)left / texture.width, (float)bottom / texture.height,
                    (float)(right - left + 1) / texture.width, (float)(top - bottom + 1) / texture.height);
                artworkBounds[sprite] = bounds; return bounds;
            }
            finally { Object.DestroyImmediate(texture); }
        }

        public static void RefitForBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            ReadWorkerReference();
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var card = root.GetComponent<DraggableCard>();
                    if (card == null || (!IsWorkplace(card) && card.Kind != CardKind.Blueprint)) continue;
                    if (IsWorkplace(card)) MatchWorkerPockets(root);
                    else SizeBlueprint(card.Rect);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            var game = Object.FindFirstObjectByType<CardGameController>();
            var table = (RectTransform)game.transform;
            Vector2 savedSize = table.sizeDelta;
            Vector2 savedAnchors = table.anchorMax;
            Vector3 savedScale = table.localScale;
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true))
            {
                if (IsWorkplace(card)) MatchWorkerPockets(card.gameObject);
                else if (card.Kind == CardKind.Blueprint) SizeBlueprint(card.Rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(card.Rect);
            }
            ArrangeInventory(game);
            ArrangeChoices(game);
            if (table.sizeDelta != savedSize || table.anchorMax != savedAnchors || table.localScale != savedScale)
                throw new InvalidOperationException("Refitting the sidebar must retain the bottom panel dimensions.");
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Matched both worker pockets to the authored Card, reduced blueprints to 70%, and moved the unchanged bottom panel.");
        }

        public static void ApplyForBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            ReadWorkerReference();
            var housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HouseCard.prefab");
            var templateSource = housePrefab.GetComponentInChildren<WorkerSlot>(true) != null ? housePrefab :
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ResearchCenterCard.prefab");
            var template = Object.Instantiate(templateSource.transform.Find("FolderArt").gameObject);
            template.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                    .Select(AssetDatabase.GUIDToAssetPath))
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var card = root.GetComponent<DraggableCard>();
                        if (card == null) continue;
                        if (IsWorkplace(card)) BuildWorkplace(root, template);
                        if (card.Kind == CardKind.House) RemoveHouseWorkers(root);
                        if (card.Kind == CardKind.Blueprint) SizeBlueprint(card.Rect);
                        TuneFeedback(root.GetComponent<CardFeedback>());
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            finally { Object.DestroyImmediate(template); }
            AssetDatabase.SaveAssets();

            // Reload so scene instances inherit the newly saved prefab children.
            EditorSceneManager.OpenScene(ScenePath);
            var game = Object.FindFirstObjectByType<CardGameController>();
            foreach (var root in game.gameObject.scene.GetRootGameObjects())
                foreach (var card in root.GetComponentsInChildren<DraggableCard>(true))
                {
                    if (card.Kind == CardKind.House) RemoveHouseWorkers(card.gameObject);
                    if (IsWorkplace(card)) BindWorkplace(card.gameObject);
                    if (card.Kind == CardKind.Blueprint) SizeBlueprint(card.Rect);
                    TuneFeedback(card.GetComponent<CardFeedback>());
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card.Rect);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card.GetComponent<CardFeedback>());
                }
            ArrangeInventory(game);
            ArrangeChoices(game);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved folder workplaces, houses without workers, reduced hover and house-sized blueprints.");
        }

        private static bool IsWorkplace(DraggableCard card) => card.Kind == CardKind.ResearchCenter ||
            card.Kind == CardKind.Factory || card.Kind == CardKind.School;

        private static void BuildWorkplace(GameObject root, GameObject template)
        {
            CardBuildingInfoAuthoring.RemoveFactoryInstructions(root);
            foreach (var old in root.GetComponentsInChildren<WorkerSlot>(true)) Object.DestroyImmediate(old.gameObject);
            var oldFolder = root.transform.Find("FolderArt");
            if (oldFolder != null) Object.DestroyImmediate(oldFolder.gameObject);
            var folder = Object.Instantiate(template, root.transform).GetComponent<RectTransform>();
            folder.name = "FolderArt"; folder.gameObject.hideFlags = HideFlags.None;
            Stretch(folder); folder.SetAsFirstSibling();
            foreach (var slot in folder.GetComponentsInChildren<HouseUpgradeSlot>(true)) Object.DestroyImmediate(slot.gameObject);
            foreach (string child in new[] { "HouseIllustration", "BuildingIllustration", "WorkerCaption", "Çerçeveler" })
            {
                var old = folder.Find(child); if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var card = root.GetComponent<DraggableCard>();
            if (card.Kind != CardKind.Factory) BuildIllustration(folder, card.Kind);
            var title = root.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f),
                new Vector2(266f, 28f), new Vector2(0.5f, 1f));
            title.text = card.Kind == CardKind.ResearchCenter ? "RESEARCH CENTER" : card.Kind == CardKind.Factory ? "FACTORY" : "SCHOOL";
            title.color = Ink; title.fontStyle = FontStyles.Bold; Font(title, 23f);
            Place((RectTransform)root.transform.Find("ProgressBar"), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 143f), new Vector2(238f, 10f), new Vector2(0.5f, 0.5f));
            var status = root.transform.Find("WorkStatus").GetComponent<TextMeshProUGUI>();
            Place(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -137f),
                new Vector2(252f, 26f), new Vector2(0.5f, 0.5f));
            status.color = Ink; Font(status, 17f);
            var caption = Text("WorkerCaption", folder, string.Empty, 15f, Ink);
            Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -167f),
                new Vector2(250f, 24f), new Vector2(0.5f, 0.5f));
            caption.transform.SetAsLastSibling();
            root.GetComponent<Image>().color = Color.clear;
            BindWorkplace(root);
            root.transform.Find("HoverDetails").SetAsLastSibling();
        }

        private static void BindWorkplace(GameObject root)
        {
            var station = root.GetComponent<BuildingStation>();
            var folder = root.transform.Find("FolderArt");
            if (folder == null) throw new InvalidOperationException(root.name + " has no saved folder artwork.");
            var slots = folder.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.name).ToArray();
            for (int i = 0; i < slots.Length; i++)
            {
                Refs(slots[i], ("station", station));
                var data = new SerializedObject(slots[i]); data.FindProperty("index").intValue = i; data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(slots[i]);
            }
            MatchWorkerPockets(root);
            var settings = new SerializedObject(station);
            var indicators = settings.FindProperty("indicators"); indicators.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) indicators.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            var frame = folder.Find("DışKenar").gameObject;
            var outline = frame.GetComponent<Outline>(); outline.enabled = false;
            Refs(station, ("card", root.GetComponent<DraggableCard>()), ("dropOutline", outline));
            Refs(root.GetComponent<DraggableCard>(), ("station", station));
            Refs(root.GetComponent<CardFeedback>(), ("shadow", frame.GetComponent<Shadow>()));
            var factory = root.GetComponent<FactoryProduction>();
            if (factory != null) Refs(factory, ("station", station), ("outline", outline));
            foreach (var old in root.GetComponents<Shadow>()) old.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(station);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.GetComponent<Image>());
            if (factory != null) PrefabUtility.RecordPrefabInstancePropertyModifications(factory);
        }

        private static void RemoveHouseWorkers(GameObject root)
        {
            var pockets = root.transform.Find("FolderArt/WorkerPockets");
            if (pockets != null) Object.DestroyImmediate(pockets.gameObject);
            foreach (var slot in root.GetComponentsInChildren<WorkerSlot>(true)) Object.DestroyImmediate(slot.gameObject);
            var station = root.GetComponent<BuildingStation>();
            Refs(root.GetComponent<DraggableCard>(), ("station", null));
            if (station != null) Object.DestroyImmediate(station);
            root.transform.Find("HoverDetails/Information/Description").GetComponent<TextMeshProUGUI>().text =
                "Install manufactured items\nto upgrade this house.";
            root.transform.Find("HoverDetails/Properties/Description").GetComponent<TextMeshProUGUI>().text =
                "Her tür için bir yuva:\nRaylı sistem, dalgakıran\nve yangın koruması.";
        }

        private static void SizeBlueprint(RectTransform rect)
        {
            rect.sizeDelta = new Vector2(300f, 400f);
            rect.localScale = Vector3.one * BlueprintScale;
        }

        private static void TuneFeedback(CardFeedback feedback)
        {
            if (feedback == null) return;
            var data = new SerializedObject(feedback);
            data.FindProperty("hoverScale").floatValue = 1.21125f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ArrangeInventory(CardGameController game)
        {
            var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
            var table = (RectTransform)game.transform;
            float left = 24f + SidebarWidth * table.localScale.x;
            table.anchoredPosition = new Vector2(left, 0f);
            var sidebarSettings = new SerializedObject(game.Sidebar);
            sidebarSettings.FindProperty("width").floatValue = SidebarWidth * table.localScale.x;
            sidebarSettings.ApplyModifiedPropertiesWithoutUndo();
            game.Sidebar.RefreshLayout(); Canvas.ForceUpdateCanvases();
            CardSidebarAuthoring.MoveGridNextToSidebar();

            var vault = game.Vault.Area;
            Place(vault, Vector2.up, Vector2.zero, new Vector2(SidebarWidth, VaultHeight), Vector2.up);
            const float cellWidth = (SidebarWidth - 48f - 24f) / 2f;
            const float cellHeight = 400f * BlueprintScale + 42f;
            for (int i = 0; i < 4; i++)
            {
                var slot = (RectTransform)vault.Find("BlueprintSlot" + i);
                Place(slot, Vector2.up, new Vector2(24f + cellWidth / 2f + i % 2 * (cellWidth + 24f), -70f - i / 2 * (cellHeight + 24f)),
                    new Vector2(cellWidth, cellHeight), new Vector2(0.5f, 1f));
                if (i == 3)
                {
                    var empty = slot.Find("EmptyLabel").GetComponent<TextMeshProUGUI>();
                    Place(empty.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cellWidth - 24f, 48f), new Vector2(0.5f, 0.5f));
                    Font(empty, 24f); continue;
                }
                var vaultSlot = slot.GetComponent<BlueprintVaultSlot>();
                var visual = vaultSlot.CardRect;
                var discovery = visual.Find("Discovery") ?? slot.Find("Discovery");
                var quantity = visual.Find("Quantity") ?? slot.Find("Quantity");
                discovery.SetParent(slot, false); quantity.SetParent(slot, false);
                Place((RectTransform)discovery, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(cellWidth - 24f, 22f), new Vector2(0.5f, 0.5f));
                Place((RectTransform)quantity, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(cellWidth - 24f, 20f), new Vector2(0.5f, 0.5f));
                Font(discovery.GetComponent<TextMeshProUGUI>(), 18f); Font(quantity.GetComponent<TextMeshProUGUI>(), 17f);
                var prefab = BlueprintPrefab(i);
                CopyCardAppearance(prefab, visual);
                Place(visual, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(300f, 400f), new Vector2(0.5f, 1f));
                visual.localScale = Vector3.one * BlueprintScale;
                Refs(vaultSlot, ("background", visual.GetComponent<Image>()), ("icon", visual.GetComponentInChildren<UpgradeIcon>(true)));
                var data = new SerializedObject(vaultSlot); data.FindProperty("preserveArtworkColors").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
                TuneFeedback(slot.GetComponent<CardFeedback>());
            }
            var title = vault.Find("Title").GetComponent<TextMeshProUGUI>();
            title.rectTransform.sizeDelta = new Vector2(SidebarWidth - 48f, 38f);
            var description = vault.Find("Description").GetComponent<TextMeshProUGUI>();
            description.rectTransform.sizeDelta = new Vector2(SidebarWidth - 48f, 22f);
            var peopleArea = (RectTransform)game.Sidebar.transform.Find("People");
            Place(peopleArea, Vector2.up, new Vector2(0f, -VaultHeight - 24f), new Vector2(SidebarWidth, 688f), Vector2.up);
            peopleArea.Find("Title").GetComponent<RectTransform>().sizeDelta = new Vector2(SidebarWidth - 48f, 38f);
            for (int i = 0; i < game.Sidebar.Slots.Count; i++)
                Place(game.Sidebar.Slots[i], Vector2.up, new Vector2(24f + cellWidth / 2f + i % 2 * (cellWidth + 24f), -210f - i / 2 * 316f),
                    new Vector2(240f, 300f), new Vector2(0.5f, 0.5f));
            Object.FindFirstObjectByType<CityViewport>().RefreshViewport();
        }

        private static GameObject BlueprintPrefab(int index) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" +
            new[] { "RaylıSistemBlueprint", "DalgaKıranBlueprint", "YangınBlueprint" }[index] + ".prefab");

        private static void CopyCardAppearance(GameObject prefab, RectTransform visual)
        {
            foreach (Transform child in visual.Cast<Transform>().ToArray())
                if (child.name != "HoverDetails") Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in prefab.transform)
            {
                if (child.name == "HoverDetails") continue;
                var clone = Object.Instantiate(child.gameObject, visual); clone.name = child.name;
                foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
            var source = prefab.GetComponent<Image>(); var background = visual.GetComponent<Image>();
            background.sprite = source.sprite; background.type = source.type; background.color = source.color;
            background.raycastTarget = false;
            visual.Find("HoverDetails")?.SetAsLastSibling();
        }

        private static void ArrangeChoices(CardGameController game)
        {
            var choice = (RectTransform)game.transform.Find("BlueprintChoice");
            var window = (RectTransform)choice.Find("SelectionWindow");
            Place(window, new Vector2(0.5f, 0.54f), Vector2.zero, new Vector2(1050f, 650f), new Vector2(0.5f, 0.5f));
            Place(choice.Find("Title").GetComponent<RectTransform>(), new Vector2(0.5f, 0.54f), new Vector2(0f, 235f),
                new Vector2(990f, 64f), new Vector2(0.5f, 0.5f));
            var description = (RectTransform)window.Find("Description");
            description.anchoredPosition = new Vector2(description.anchoredPosition.x, -120f);
            var footer = (RectTransform)window.Find("Footer");
            Place(footer, new Vector2(0.5f, 0f), new Vector2(0f, 17f), new Vector2(1002f, 26f), new Vector2(0.5f, 0.5f));
            Font(footer.GetComponent<TextMeshProUGUI>(), 17f);
            for (int i = 0; i < 3; i++)
            {
                var button = (RectTransform)choice.Find("BlueprintOption" + i);
                Place(button, new Vector2(0.5f, 0.54f), new Vector2((i - 1) * 334f, -62f),
                    new Vector2(310f, 450f), new Vector2(0.5f, 0.5f));
                var preview = (RectTransform)button.Find("CardPreview");
                // Preview roots are saved graphics only; buttons retain the existing callbacks.
                CopyCardAppearance(BlueprintPrefab(i), preview);
                Place(preview, new Vector2(0.5f, 0.5f), new Vector2(0f, 35f), new Vector2(300f, 400f), new Vector2(0.5f, 0.5f));
                preview.localScale = Vector3.one * BlueprintScale;
                var label = button.Find("Name").GetComponent<TextMeshProUGUI>();
                Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 37f), new Vector2(286f, 56f), new Vector2(0.5f, 0.5f));
                Font(label, 26f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
            }
        }

        private static void BuildIllustration(RectTransform folder, CardKind kind)
        {
            var art = new GameObject("BuildingIllustration", typeof(RectTransform)).GetComponent<RectTransform>();
            art.SetParent(folder, false); art.gameObject.layer = folder.gameObject.layer;
            Place(art, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(218f, 132f), new Vector2(0.5f, 0.5f));
            art.SetSiblingIndex(1);
            var wall = kind == CardKind.School ? new Color(0.80f, 0.64f, 0.41f) : new Color(0.59f, 0.72f, 0.69f);
            var roof = kind == CardKind.School ? new Color(0.62f, 0.30f, 0.22f) : new Color(0.24f, 0.39f, 0.41f);
            Box("Ground", art, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.10f), new Color(0.53f, 0.62f, 0.44f));
            Box("Walls", art, new Vector2(0.16f, 0.10f), new Vector2(0.84f, 0.73f), wall);
            Box("Roof", art, new Vector2(0.10f, 0.73f), new Vector2(0.90f, 0.83f), roof);
            Box("UpperRoof", art, new Vector2(0.23f, 0.83f), new Vector2(0.77f, 0.93f), roof);
            Box("Door", art, new Vector2(0.44f, 0.10f), new Vector2(0.56f, 0.39f), Ink);
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 4; column++)
                    Box("Window" + row + column, art, new Vector2(0.22f + column * 0.15f, 0.46f + row * 0.16f),
                        new Vector2(0.31f + column * 0.15f, 0.56f + row * 0.16f), new Color(0.28f, 0.50f, 0.57f));
            if (kind == CardKind.School)
            {
                Box("FlagPole", art, new Vector2(0.48f, 0.93f), new Vector2(0.50f, 1.10f), Ink);
                Box("Flag", art, new Vector2(0.50f, 1.00f), new Vector2(0.65f, 1.10f), roof);
            }
            else Box("Antenna", art, new Vector2(0.48f, 0.93f), new Vector2(0.52f, 1.10f), roof);
        }

        private static void Box(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        }
        private static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            var label = rect.GetComponent<TextMeshProUGUI>(); label.font = TMP_Settings.defaultFontAsset;
            label.text = text; label.color = color; label.raycastTarget = false; label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold; Font(label, size); return label;
        }
        private static void Font(TextMeshProUGUI label, float size)
        { label.fontSize = label.fontSizeMax = size; label.fontSizeMin = size * 0.75f; label.enableAutoSizing = true; }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; }
        private static void Refs(Object target, params (string name, Object value)[] refs)
        {
            var data = new SerializedObject(target);
            foreach (var item in refs) data.FindProperty(item.name).objectReferenceValue = item.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
