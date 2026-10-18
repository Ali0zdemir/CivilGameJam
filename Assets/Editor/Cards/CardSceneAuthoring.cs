using System;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    // All construction is editor-only. The resulting scene and prefabs are saved as normal Unity assets.
    public static class CardSceneAuthoring
    {
        private const string ScenePath = "Assets/2DCardScene.unity";
        private static readonly Color NormalColor = new Color(0.94f, 0.63f, 0.18f);
        private static readonly Color EducatedColor = new Color(0.18f, 0.63f, 0.94f);
        private static readonly Color EmptyColor = new Color(0.23f, 0.27f, 0.31f);
        private static readonly string[] BlueprintNames = { "Rail System", "Elevated Floor", "Fire Protection" };
        private static readonly string[] BlueprintPaths = { "RaylıSistemBlueprint", "DalgaKıranBlueprint", "YangınBlueprint" };
        private static readonly string[] ProductPaths = { "RaylıSistemCard", "DalgaKıranCard", "YangınCard" };

        [MenuItem("Tools/Civil Game Jam/Create Missing Scene UI")]
        public static void AuthorActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogWarning("Open 2DCardScene in Edit Mode to author its UI.");
                return;
            }
            var controller = Object.FindFirstObjectByType<CardGameController>();
            if (controller != null && controller.CardLayer != null)
            {
                Debug.Log("The scene is already authored. Edit its existing Hierarchy and prefabs directly.");
                return;
            }
            PreparePrefabs();
            AuthorScene();
            CardResolutionSetup.Configure(controller);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        public static void BuildForBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            AuthorActiveScene();
            VerifySavedAssets();
        }

        public static void CaptureEditModePreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var controller = Object.FindFirstObjectByType<CardGameController>();
            var canvas = controller.GetComponentInParent<Canvas>();
            var cameraObject = new GameObject("EditorPreviewCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 450f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            var texture = new RenderTexture(1600, 900, 24);
            camera.targetTexture = texture;
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            var oldTarget = RenderTexture.active;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            Canvas.ForceUpdateCanvases();
            void SavePreview()
            {
                int exitCode = 0;
                try
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in controller.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true, true);
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = texture;
                    var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    pixels.Apply();
                    string directory = Path.GetFullPath("Library/CardGameTestResults");
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, "edit-mode-board.png"), pixels.EncodeToPNG());
                    Object.DestroyImmediate(pixels);
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    exitCode = 1;
                }
                finally
                {
                    canvas.renderMode = oldMode;
                    canvas.worldCamera = oldCamera;
                    canvas.planeDistance = oldDistance;
                    RenderTexture.active = oldTarget;
                    camera.targetTexture = null;
                    Object.DestroyImmediate(texture);
                    Object.DestroyImmediate(cameraObject);
                }
                if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            }
            // Give TMP's editor font/material callbacks time to settle after loading the scene.
            EditorApplication.delayCall += () =>
            {
                EditorApplication.QueuePlayerLoopUpdate();
                EditorApplication.delayCall += SavePreview;
            };
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static GameObject Prefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");

        private static void PreparePrefabs()
        {
            PreparePrefab("ResearchCenterCard", CardKind.ResearchCenter);
            PreparePrefab("FactoryCard", CardKind.Factory);
            PreparePrefab("SchoolCard", CardKind.School);
            PreparePrefab("HouseCard", CardKind.House);
            PreparePrefab("PeopleCard", CardKind.Person);
            PreparePrefab("EducatedPeopleCard", CardKind.EducatedPerson);
            for (int i = 0; i < BlueprintPaths.Length; i++)
            {
                PreparePrefab(BlueprintPaths[i], CardKind.Blueprint, BlueprintNames[i]);
                PreparePrefab(ProductPaths[i], CardKind.Upgrade, BlueprintNames[i]);
            }
        }

        internal static void PreparePrefab(string name, CardKind kind, string title = null)
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                root.GetComponent<Image>().raycastTarget = true;
                var card = root.GetComponent<DraggableCard>() ?? root.AddComponent<DraggableCard>();
                var feedback = root.GetComponent<CardFeedback>() ?? root.AddComponent<CardFeedback>();
                var shadow = root.GetComponents<Shadow>().FirstOrDefault(s => !(s is Outline)) ?? root.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
                shadow.effectDistance = new Vector2(4f, -5f);
                SetObjects(feedback, ("rect", rect), ("shadow", shadow));
                SetObjects(card, ("feedback", feedback));
                SetInt(card, "kind", (int)kind);
                SetInt(card, "initialGridCell", -1);

                bool isPerson = kind == CardKind.Person || kind == CardKind.EducatedPerson;
                var text = root.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
                text.raycastTarget = false;
                Stretch(text.rectTransform, new Vector2(0.06f, 0.29f), new Vector2(0.94f, 0.73f));
                text.enableAutoSizing = true;
                text.fontSizeMin = isPerson ? 14f : 20f;
                text.fontSizeMax = isPerson ? 24f : 30f;
                text.textWrappingMode = TextWrappingModes.Normal;
                if (title != null) text.text = title;

                if (isPerson && rect.Find("PersonType") == null)
                {
                    var accent = Image("PersonType", rect, kind == CardKind.Person ? NormalColor : EducatedColor);
                    Stretch(accent.rectTransform, new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.14f));
                }
                if (kind == CardKind.School || kind == CardKind.ResearchCenter || kind == CardKind.Factory)
                    PrepareStation(root, card);

                CardHoverAuthoring.PreparePrefab(root);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void PrepareStation(GameObject root, DraggableCard card)
        {
            var station = root.GetComponent<BuildingStation>() ?? root.AddComponent<BuildingStation>();
            var outline = root.GetComponent<Outline>() ?? root.AddComponent<Outline>();
            outline.effectDistance = new Vector2(4f, -4f);
            outline.enabled = false;
            var slots = new WorkerSlot[BuildingStation.Capacity];
            for (int i = 0; i < slots.Length; i++)
            {
                string name = "WorkerSlot" + (i + 1);
                var image = root.transform.Find(name)?.GetComponent<Image>() ?? Image(name, root.transform, EmptyColor, true);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(i == 0 ? 0.23f : 0.77f, 0.1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(32f, 26f);
                var label = rect.Find("WorkerType")?.GetComponent<TextMeshProUGUI>() ?? Label("WorkerType", rect, "-", 20f, Color.white);
                slots[i] = image.GetComponent<WorkerSlot>() ?? image.gameObject.AddComponent<WorkerSlot>();
                SetObjects(slots[i], ("station", station), ("background", image), ("label", label));
                SetInt(slots[i], "index", i);
            }
            var track = root.transform.Find("ProgressBar")?.GetComponent<Image>() ??
                Image("ProgressBar", root.transform, new Color(0.1f, 0.15f, 0.2f));
            Stretch(track.rectTransform, new Vector2(0.06f, 0.89f), new Vector2(0.94f, 0.94f));
            var fill = track.transform.Find("BlueProgress")?.GetComponent<Image>() ?? Image("BlueProgress", track.transform, EducatedColor);
            Stretch(fill.rectTransform, Vector2.zero, new Vector2(0f, 1f));
            track.gameObject.SetActive(false);
            var status = root.transform.Find("WorkStatus")?.GetComponent<TextMeshProUGUI>() ??
                Label("WorkStatus", root.transform, string.Empty, 18f, new Color(0.15f, 0.2f, 0.25f));
            Stretch(status.rectTransform, new Vector2(0.03f, 0.16f), new Vector2(0.97f, 0.28f));
            SetObjects(card, ("station", station));
            SetObjects(station, ("card", card), ("progressFill", fill), ("progressRoot", track.rectTransform),
                ("status", status), ("dropOutline", outline));
            SetArray(station, "indicators", slots);
        }

        private static void AuthorScene()
        {
            var controller = Object.FindFirstObjectByType<CardGameController>();
            if (controller == null) throw new InvalidOperationException("CardGameController is missing from the scene.");
            var table = (RectTransform)controller.transform;
            var gridPanel = (RectTransform)table.Find("GridPanel");

            // Preserve the previous hand-placed reference cards in an inactive group.
            var originalCards = table.Cast<Transform>().Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray();
            if (originalCards.Length > 0)
            {
                var references = Rect("ReferenceCards", table);
                Stretch(references, Vector2.zero, Vector2.one);
                foreach (var original in originalCards) original.SetParent(references, true);
                references.gameObject.SetActive(false);
                references.SetAsFirstSibling();
            }

            var gridRect = Rect("GridGuides", gridPanel);
            Stretch(gridRect, Vector2.zero, Vector2.one);
            var grid = gridRect.gameObject.AddComponent<CardGrid>();
            grid.color = new Color(0.22f, 0.17f, 0.10f, 0.55f);
            grid.raycastTarget = false;
            var cells = new RectTransform[CardGrid.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Rect("Cell_" + (i + 1).ToString("00"), gridRect);
                int col = i % CardGrid.Columns;
                int row = i / CardGrid.Columns;
                Stretch(cells[i], new Vector2(col / (float)CardGrid.Columns, 1f - (row + 1) / (float)CardGrid.Rows),
                    new Vector2((col + 1) / (float)CardGrid.Columns, 1f - row / (float)CardGrid.Rows));
            }
            SetArray(grid, "cells", cells);
            var cardLayer = Rect("PlayableCards", table);
            Stretch(cardLayer, Vector2.zero, Vector2.one);
            TextMeshProUGUI population = null;
            TextMeshProUGUI hint = null;

            PlaceBuilding("ResearchCenterCard", cells[0], 0);
            PlaceBuilding("FactoryCard", cells[1], 1);
            PlaceBuilding("SchoolCard", cells[2], 2);
            for (int i = 0; i < 4; i++) PlaceBuilding("HouseCard", cells[4 + i], 4 + i);
            for (int i = 0; i < 4; i++)
            {
                var person = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("PeopleCard"), cardLayer);
                person.name = "PeopleCard_" + (i + 1).ToString("00");
                var rect = (RectTransform)person.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(68f + i % 2 * 112f, -190f - i / 2 * 164f);
            }

            var choice = BuildBlueprintPanel(table, controller, out var buttons);
            SetObjects(controller, ("table", table), ("gridPanel", gridPanel), ("grid", grid),
                ("cardLayer", cardLayer), ("population", population), ("hint", hint), ("choicePanel", choice),
                ("educatedPersonPrefab", Prefab("EducatedPeopleCard")),
                ("normalPersonPrefab", Prefab("PeopleCard")));
            var settings = new SerializedObject(controller);
            var options = settings.FindProperty("blueprints");
            options.arraySize = BlueprintNames.Length;
            for (int i = 0; i < options.arraySize; i++)
            {
                var option = options.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("title").stringValue = BlueprintNames[i];
                option.FindPropertyRelative("prefab").objectReferenceValue = Prefab(BlueprintPaths[i]);
                option.FindPropertyRelative("button").objectReferenceValue = buttons[i];
                option.FindPropertyRelative("productPrefab").objectReferenceValue = Prefab(ProductPaths[i]);
                option.FindPropertyRelative("productCount").intValue = 2;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            controller.FitSceneCardsToGrid();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        private static void PlaceBuilding(string name, RectTransform cell, int index)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(name), cell);
            instance.name = name;
            ((RectTransform)instance.transform).anchoredPosition = Vector2.zero;
            SetInt(instance.GetComponent<DraggableCard>(), "initialGridCell", index);
        }

        private static RectTransform BuildBlueprintPanel(RectTransform table, CardGameController controller, out Button[] buttons)
        {
            var backdrop = Image("BlueprintChoice", table, new Color(0.02f, 0.04f, 0.08f, 0.90f), true);
            var choice = backdrop.rectTransform;
            Stretch(choice, Vector2.zero, Vector2.one);
            var title = Label("Title", choice, "Araştırma tamamlandı · Bir blueprint seç", 36f, Color.white);
            Stretch(title.rectTransform, new Vector2(0.1f, 0.73f), new Vector2(0.9f, 0.85f));
            buttons = new Button[BlueprintNames.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                var image = Image("BlueprintOption" + i, choice, new Color(0.15f, 0.4f, 0.65f), true);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.15f + (i + 0.5f) / buttons.Length * 0.7f, 0.48f);
                rect.sizeDelta = new Vector2(250f, 265f);
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                UnityEventTools.AddIntPersistentListener(button.onClick, controller.SelectBlueprint, i);
                buttons[i] = button;

                var preview = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(BlueprintPaths[i]), rect);
                preview.name = "CardPreview";
                Object.DestroyImmediate(preview.GetComponent<DraggableCard>());
                Object.DestroyImmediate(preview.GetComponent<CardFeedback>());
                Object.DestroyImmediate(preview.GetComponent<CardHover>());
                var hoverDetails = preview.transform.Find("HoverDetails");
                if (hoverDetails != null) Object.DestroyImmediate(hoverDetails.gameObject);
                var previewRect = (RectTransform)preview.transform;
                previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.6f);
                previewRect.anchoredPosition = Vector2.zero;
                previewRect.localScale = Vector3.one * (170f / previewRect.sizeDelta.y);
                foreach (var graphic in preview.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                var label = preview.GetComponentInChildren<TextMeshProUGUI>();
                label.text = BlueprintNames[i];
                Stretch(label.rectTransform, new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.75f));
                label.fontSizeMin = 20f;
                label.fontSizeMax = 32f;
                var caption = Label("Name", rect, BlueprintNames[i], 25f, Color.white);
                Stretch(caption.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.22f));
            }
            choice.gameObject.SetActive(false);
            return choice;
        }

        public static void VerifySavedAssets()
        {
            var controller = Object.FindFirstObjectByType<CardGameController>();
            var cards = controller.GetComponentsInChildren<DraggableCard>();
            if (cards.Length != 11 || controller.Grid == null || controller.CardLayer == null)
                throw new InvalidOperationException("The authored board must contain its 11 starting cards before Play. Found " +
                    cards.Length + ": " + string.Join(", ", cards.Select(card => card.name)));
            if (Enumerable.Range(0, CardGrid.CellCount).Any(i => controller.Grid.GetCell(i) == null))
                throw new InvalidOperationException("All eight grid cells must be saved in the scene.");
            foreach (var root in controller.gameObject.scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script on " + transform.name);
            string directory = Path.GetFullPath("Library/CardGameTestResults");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "authored-hierarchy.txt"),
                string.Join("\n", controller.GetComponentsInChildren<Transform>(true).Select(t =>
                    AnimationUtility.CalculateTransformPath(t, controller.transform) +
                    (t.gameObject.activeSelf ? "" : " [inactive]"))));
            Debug.Log("Editable card scene verified: 11 starting cards, 8 cells, serialized UI and button events.");
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Image(string name, Transform parent, Color color, bool raycast = false)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = size * 0.65f;
            label.fontSizeMax = size;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            return label;
        }

        private static void SetObjects(Object target, params (string name, Object value)[] values)
        {
            var settings = new SerializedObject(target);
            foreach (var value in values) settings.FindProperty(value.name).objectReferenceValue = value.value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string name, int value)
        {
            var settings = new SerializedObject(target);
            settings.FindProperty(name).intValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(Object target, string name, T[] values) where T : Object
        {
            var settings = new SerializedObject(target);
            var array = settings.FindProperty(name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
