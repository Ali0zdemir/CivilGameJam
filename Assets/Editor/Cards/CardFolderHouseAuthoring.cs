using System;
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
    public static class CardFolderHouseAuthoring
    {
        private const string ArtRoot = "Assets/EfeArts/";
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + name + ".png") ??
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "People/" + name + ".png") ??
            throw new InvalidOperationException("Missing EfeArts sprite: " + name);

        public static void ApplyForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            ApplyToActiveScene();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
        }

        [MenuItem("Tools/Civil Game Jam/Apply Saved Folder Houses And Efe Artwork")]
        public static void ApplyToActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply saved folder artwork in Edit Mode.");
            var game = Object.FindFirstObjectByType<CardGameController>();
            if (game == null) throw new InvalidOperationException("Open 2DCardScene first.");
            var canvas = game.GetComponentInParent<Canvas>();
            var references = game.transform.Find("ReferenceCards");
            var template = game.Grid.transform.Find("Card") ?? references.Find("Card") ??
                game.gameObject.scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Card")?.transform;
            if (template == null) throw new InvalidOperationException("The authored Card example is missing.");
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var card = root.GetComponent<DraggableCard>();
                    if (card == null) continue;
                    if (card.Kind == CardKind.House) BuildHouse(root, template.gameObject);
                    if (card.Kind == CardKind.Upgrade) StyleProduct(root);
                    if (card.Kind == CardKind.Blueprint && card.UpgradeType == UpgradeKind.RailSystem) StyleSeismicPlan(root);
                    foreach (var icon in root.GetComponentsInChildren<UpgradeIcon>(true))
                        ConfigureIcon(icon, card.Kind == CardKind.Blueprint || card.Kind == CardKind.Factory);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            template.SetParent(references, false);
            RestoreHouseRow(game);
            foreach (var root in game.gameObject.scene.GetRootGameObjects())
            {
                foreach (var card in root.GetComponentsInChildren<DraggableCard>(true))
                {
                    if (card.Kind != CardKind.House && card.Kind != CardKind.Upgrade &&
                        !(card.Kind == CardKind.Blueprint && card.UpgradeType == UpgradeKind.RailSystem)) continue;
                    card.GetComponent<Image>().color = Color.clear;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card.GetComponent<Image>());
                    if (card.Kind == CardKind.House)
                    {
                        Refs(card, ("station", card.GetComponent<BuildingStation>()));
                        PrefabUtility.RecordPrefabInstancePropertyModifications(card);
                    }
                }
                foreach (var icon in root.GetComponentsInChildren<UpgradeIcon>(true))
                {
                    bool plan = icon.GetComponentInParent<BlueprintVaultSlot>() != null ||
                        icon.GetComponentInParent<FactoryProduction>() != null ||
                        icon.GetComponentInParent<DraggableCard>()?.Kind == CardKind.Blueprint ||
                        AnimationUtility.CalculateTransformPath(icon.transform, root.transform).Contains("BlueprintOption");
                    ConfigureIcon(icon, plan);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
                }
            }
            var preview = game.transform.Find("BlueprintChoice/BlueprintOption0/CardPreview");
            if (preview != null) StyleSeismicPlan(preview.gameObject);
            var railSlot = game.Vault.SlotFor(UpgradeKind.RailSystem);
            var visual = railSlot.CardRect;
            var illustration = (RectTransform)visual.Find("Illustration");
            illustration.GetComponent<Image>().color = Color.clear;
            At(illustration, new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(160f, 213.3333f), new Vector2(0.5f, 1f));
            Stretch(illustration.GetComponentInChildren<UpgradeIcon>(true).rectTransform);
            var category = visual.Find("Category");
            if (category != null) category.gameObject.SetActive(false);
            At((RectTransform)visual.Find("Name"), new Vector2(0.5f, 1f), new Vector2(0f, -223f), new Vector2(196f, 29f), new Vector2(0.5f, 1f));
            At((RectTransform)visual.Find("Discovery"), new Vector2(0.5f, 1f), new Vector2(0f, -253f), new Vector2(196f, 20f), new Vector2(0.5f, 1f));
            At((RectTransform)visual.Find("Quantity"), new Vector2(0.5f, 1f), new Vector2(0f, -276f), new Vector2(196f, 20f), new Vector2(0.5f, 1f));
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log("Saved folder houses with two empty worker pockets, native entry previews, Efe blueprint art and three manufactured logos.");
        }

        private static void RestoreHouseRow(CardGameController game)
        {
            var first = game.Grid.GetCell(0);
            var second = game.Grid.GetCell(1);
            Vector2 step = second.anchoredPosition - first.anchoredPosition;
            float gap = step.x - first.rect.width;
            var settings = new SerializedObject(game.Grid);
            var cells = settings.FindProperty("cells"); cells.arraySize = CardGrid.CellCount;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HouseCard.prefab");
            for (int i = 0; i < CardGrid.CellCount; i++)
            {
                string name = "Cell_" + (i + 1).ToString("00");
                var cell = game.Grid.GetCell(i) ?? game.Grid.transform.Find(name) as RectTransform;
                if (cell == null)
                {
                    cell = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                    cell.SetParent(game.Grid.transform, false); cell.gameObject.layer = first.gameObject.layer;
                    At(cell, first.anchorMin, first.anchoredPosition + step * (i % CardGrid.Columns) -
                        new Vector2(0f, (first.rect.height + gap) * (i / CardGrid.Columns)), first.rect.size, first.pivot);
                }
                cells.GetArrayElementAtIndex(i).objectReferenceValue = cell;
                if (i < CardGrid.Columns || cell.GetComponentInChildren<DraggableCard>() != null) continue;
                var house = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cell);
                house.name = "HouseCard_" + (i - CardGrid.Columns + 1).ToString("00");
                var rect = (RectTransform)house.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                var card = house.GetComponent<DraggableCard>();
                var data = new SerializedObject(card); data.FindProperty("initialGridCell").intValue = i; data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(card);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildHouse(GameObject root, GameObject template)
        {
            var existingArt = root.transform.Find("GoldFrame/PortraitBorder/Portrait/HouseIllustration") ??
                root.transform.Find("FolderArt/HouseIllustration");
            var houseArt = existingArt != null ? Object.Instantiate(existingArt.gameObject, root.transform) : null;
            var title = root.transform.Find("Text (TMP)");
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name != "HoverDetails" && child != title && (houseArt == null || child.gameObject != houseArt))
                    Object.DestroyImmediate(child.gameObject);
            var folder = Object.Instantiate(template, root.transform).GetComponent<RectTransform>();
            folder.name = "FolderArt";
            Stretch(folder);
            foreach (RectTransform layer in folder.Cast<Transform>().ToArray())
            {
                Stretch(layer);
                layer.GetComponent<Image>().raycastTarget = false;
            }
            if (houseArt != null)
            {
                houseArt.name = "HouseIllustration";
                houseArt.transform.SetParent(folder, false);
                var rect = (RectTransform)houseArt.transform;
                rect.anchorMin = new Vector2(0.12f, 0.40f); rect.anchorMax = new Vector2(0.88f, 0.83f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.SetSiblingIndex(1);
            }
            if (title != null)
            {
                var rect = (RectTransform)title;
                rect.anchorMin = new Vector2(0.08f, 0.89f); rect.anchorMax = new Vector2(0.92f, 0.97f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                title.SetAsLastSibling();
            }
            var station = root.GetComponent<BuildingStation>() ?? root.AddComponent<BuildingStation>();
            var house = root.GetComponent<HouseUpgrades>();
            var card = root.GetComponent<DraggableCard>();
            var outline = root.GetComponent<Outline>();
            outline.enabled = false;
            Refs(station, ("card", card), ("dropOutline", outline));
            Refs(card, ("station", station));
            var pockets = new GameObject("WorkerPockets", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            pockets.SetParent(folder, false); pockets.gameObject.layer = root.layer;
            Stretch(pockets);
            pockets.SetSiblingIndex(folder.Find("2.eleman").GetSiblingIndex());
            var workers = new WorkerSlot[2];
            for (int i = 0; i < workers.Length; i++)
            {
                var layer = (RectTransform)folder.Find((i + 1) + ".eleman");
                Rect bounds = ArtworkBounds(layer.GetComponent<Image>().sprite);
                layer.SetParent(pockets, false);
                PlaceOverArtwork(layer, bounds);
                var background = layer.GetComponent<Image>(); background.sprite = null; background.color = Color.clear; background.raycastTarget = true;
                var slot = layer.gameObject.GetComponent<WorkerSlot>() ?? layer.gameObject.AddComponent<WorkerSlot>();
                float width = layer.rect.width * 320f / 240f;
                float visibleHeight = width * 300f / 320f;
                var portrait = Image("Portrait", layer, Sprite("People Card Small Revised"));
                At(portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, (layer.rect.height - visibleHeight) * 0.5f),
                    Vector2.one * width, new Vector2(0.5f, 0.5f));
                portrait.gameObject.SetActive(false);
                var entry = Image("EntryPreview", layer, Sprite("People Card Small Revised"));
                At(entry.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 320f), new Vector2(0.5f, 0.5f));
                var entryCanvas = entry.gameObject.AddComponent<Canvas>();
                entryCanvas.overrideSorting = true; entryCanvas.sortingOrder = 10;
                entry.gameObject.SetActive(false);
                Refs(slot, ("station", station), ("background", background), ("portrait", portrait), ("entryImage", entry),
                    ("entryCanvas", entryCanvas),
                    ("normalPortrait", Sprite("People Card Small Revised")), ("educatedPortrait", Sprite("Educated People Card Small")));
                var data = new SerializedObject(slot); data.FindProperty("index").intValue = i; data.ApplyModifiedPropertiesWithoutUndo();
                workers[i] = slot;
            }
            workers[1].transform.SetAsFirstSibling();
            Array(station, "indicators", workers);
            var slots = new HouseUpgradeSlot[3];
            string[] names = { "RaylıSistemIcon", "TsunamiSutunları", "YangınSöndürücü" };
            for (int i = 0; i < slots.Length; i++)
            {
                var layer = (RectTransform)folder.Find(names[i]);
                Rect bounds = ArtworkBounds(layer.GetComponent<Image>().sprite);
                PlaceOverArtwork(layer, bounds);
                Vector2 iconSize = layer.sizeDelta;
                layer.sizeDelta += Vector2.one * 10f;
                var background = layer.GetComponent<Image>(); background.sprite = null; background.color = Color.clear; background.raycastTarget = true;
                var iconRect = new GameObject("InstalledIcon", typeof(RectTransform), typeof(UpgradeIcon)).GetComponent<RectTransform>();
                iconRect.SetParent(layer, false); iconRect.gameObject.layer = layer.gameObject.layer;
                At(iconRect, new Vector2(0.5f, 0.5f), Vector2.zero, iconSize, new Vector2(0.5f, 0.5f));
                var icon = iconRect.GetComponent<UpgradeIcon>(); icon.Kind = (UpgradeKind)(i + 1); ConfigureIcon(icon, false);
                icon.raycastTarget = false; icon.gameObject.SetActive(false);
                var slot = layer.gameObject.GetComponent<HouseUpgradeSlot>() ?? layer.gameObject.AddComponent<HouseUpgradeSlot>();
                Refs(slot, ("house", house), ("icon", icon), ("fill", background));
                var data = new SerializedObject(slot); data.FindProperty("kind").enumValueIndex = i + 1; data.ApplyModifiedPropertiesWithoutUndo();
                slots[i] = slot;
            }
            Array(house, "slots", slots); Refs(house, ("outline", outline));
            root.GetComponent<Image>().color = Color.clear;
            var frame = folder.Find("DışKenar").gameObject;
            FeedbackShadow(root, frame);
            var frameOutline = frame.GetComponent<Outline>() ?? frame.AddComponent<Outline>();
            frameOutline.effectDistance = new Vector2(3f, -3f); frameOutline.enabled = false;
            Refs(station, ("dropOutline", frameOutline));
            Refs(house, ("outline", frameOutline));
            root.transform.Find("HoverDetails/Information/Description").GetComponent<TextMeshProUGUI>().text =
                "Assign people to the folder.\nIt holds up to two workers.";
            root.transform.Find("HoverDetails/Properties/Description").GetComponent<TextMeshProUGUI>().text =
                "Install manufactured items\nin the lower slots.\nWorkers can be released.";
        }

        private static void StyleProduct(GameObject root)
        {
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name != "HoverDetails" && child.name != "CardSymbol") Object.DestroyImmediate(child.gameObject);
            ((RectTransform)root.transform).sizeDelta = new Vector2(200f, 200f);
            root.GetComponent<Image>().color = Color.clear;
            var symbol = root.transform.Find("CardSymbol").GetComponent<UpgradeIcon>();
            Stretch(symbol.rectTransform); ConfigureIcon(symbol, false);
            FeedbackShadow(root, symbol.gameObject);
        }

        private static void StyleSeismicPlan(GameObject root)
        {
            foreach (string name in new[] { "TypeBadge", "Text (TMP)", "Usage" })
            {
                var old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            root.GetComponent<Image>().color = Color.clear;
            var icon = root.transform.Find("CardSymbol").GetComponent<UpgradeIcon>();
            Stretch(icon.rectTransform); ConfigureIcon(icon, true);
            FeedbackShadow(root, icon.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.GetComponent<Image>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(icon.rectTransform);
        }

        private static void ConfigureIcon(UpgradeIcon icon, bool blueprint)
        {
            Refs(icon, ("railSystemSprite", Sprite(blueprint ? "Seismic Big Card" : "Siesmic Isolator logo")),
                ("breakwaterSprite", Sprite("Elevated Floor Logo")), ("fireSprite", Sprite("Fire Extingusiher Logo")));
            icon.color = Color.white;
            icon.SetAllDirty();
        }

        private static void FeedbackShadow(GameObject root, GameObject art)
        {
            var shadow = art.GetComponent<Shadow>() ?? art.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.12f); shadow.effectDistance = new Vector2(4f, -5f);
            foreach (var old in root.GetComponents<Shadow>()) if (old.GetType() == typeof(Shadow)) old.enabled = false;
            var feedback = root.GetComponent<CardFeedback>();
            if (feedback != null) Refs(feedback, ("shadow", shadow));
        }

        private static Rect ArtworkBounds(Sprite sprite)
        {
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
                return new Rect((float)left / texture.width, (float)bottom / texture.height,
                    (float)(right - left + 1) / texture.width, (float)(top - bottom + 1) / texture.height);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void PlaceOverArtwork(RectTransform rect, Rect bounds)
        {
            Vector2 size = new Vector2(300f, 400f);
            At(rect, new Vector2(0.5f, 0.5f), Vector2.Scale(bounds.center - Vector2.one * 0.5f, size),
                Vector2.Scale(bounds.size, size), new Vector2(0.5f, 0.5f));
        }

        private static Image Image(string name, Transform parent, Sprite sprite)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            var image = rect.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
        private static void At(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; }
        private static void Refs(Object target, params (string name, Object value)[] refs)
        {
            var data = new SerializedObject(target);
            foreach (var item in refs) data.FindProperty(item.name).objectReferenceValue = item.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Array<T>(Object target, string field, T[] values) where T : Object
        {
            var data = new SerializedObject(target); var array = data.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
