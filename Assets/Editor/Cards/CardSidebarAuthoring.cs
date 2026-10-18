using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards.Editor
{
    public static class CardSidebarAuthoring
    {
        private static readonly Color Surface = new Color(0.045f, 0.075f, 0.085f);
        private static readonly Color SlotColor = new Color(0.075f, 0.13f, 0.15f);
        private static readonly Color Muted = new Color(0.52f, 0.65f, 0.67f);
        private static readonly Color Gold = new Color(0.96f, 0.75f, 0.39f);

        [MenuItem("Tools/Card Game/Save Sidebar Upgrade Inventory")]
        public static void SaveUpgradeInventoryForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Save the upgrade inventory in Edit Mode.");
            SaveProductInventoryNames();
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            var sidebar = game.Sidebar;
            sidebar.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            var people = sidebar.PeopleArea;
            var peopleRoot = (RectTransform)people.parent;
            var upgradesRoot = sidebar.transform.Find("Upgrades") as RectTransform;
            var section = upgradesRoot != null ? upgradesRoot : peopleRoot;
            float sectionTop = sidebar.Area.rect.yMax - sidebar.Area.InverseTransformPoint(
                section.TransformPoint(new Vector3(0f, section.rect.yMax))).y;
            float sectionWidth = peopleRoot.rect.width;
            const float header = 48f, peopleHeight = 238f, upgradesHeight = 310f, gap = 16f;

            Place(peopleRoot, Vector2.up, new Vector2(0f, -sectionTop - header - upgradesHeight - gap),
                new Vector2(sectionWidth, header + peopleHeight), Vector2.up);
            Place(people, new Vector2(.5f, 1f), new Vector2(0f, -header),
                new Vector2(sectionWidth - 48f, peopleHeight), new Vector2(.5f, 1f));
            Title(peopleRoot.Find("Title").GetComponent<TextMeshProUGUI>(), "PEOPLE", 0f);
            var peopleCards = people.GetComponentsInChildren<DraggableCard>(true).Where(card => card.IsPerson)
                .OrderBy(card => card.name).ToArray();
            for (int i = 0; i < peopleCards.Length; i++)
            {
                var rect = peopleCards[i].Rect;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(people.rect.width * ((i + .5f) / peopleCards.Length - .5f), 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }

            upgradesRoot = Box("Upgrades", sidebar.transform, Color.clear);
            int peopleIndex = peopleRoot.GetSiblingIndex();
            upgradesRoot.SetSiblingIndex(upgradesRoot.GetSiblingIndex() < peopleIndex ? peopleIndex - 1 : peopleIndex);
            Place(upgradesRoot, Vector2.up, new Vector2(0f, -sectionTop),
                new Vector2(sectionWidth, header + upgradesHeight), Vector2.up);
            Title(Label("Title", upgradesRoot, "UPGRADES", 30f, Gold, FontStyles.Bold), "UPGRADES", 0f);
            var upgrades = game.FactoryOutputArea;
            upgrades.SetParent(upgradesRoot, false);
            upgrades.localScale = Vector3.one;
            upgrades.anchorMin = Vector2.zero;
            upgrades.anchorMax = Vector2.one;
            upgrades.pivot = new Vector2(.5f, .5f);
            upgrades.offsetMin = new Vector2(24f, 0f);
            upgrades.offsetMax = new Vector2(-24f, -header);
            var background = upgrades.GetComponent<Image>() ?? upgrades.gameObject.AddComponent<Image>();
            background.color = people.GetComponent<Image>().color;
            background.raycastTarget = true;
            Frame(upgrades);
            Refs(sidebar, ("upgradesArea", upgrades));
            Refs(game, ("upgradesDropTarget", CardTsunamiTurnAuthoring.ConfigureFeedback(upgrades.gameObject, background)));
            Canvas.ForceUpdateCanvases();
            if (upgrades.rect.height < 300f)
                throw new System.InvalidOperationException("The upgrade inventory needs room for two rows of products.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved UPGRADES between the Vault and compact People area, with free product placement and persistent names.");
        }

        private static void SaveProductInventoryNames()
        {
            foreach (string name in new[] { "RaylıSistemCard", "DalgaKıranCard", "YangınCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var card = root.GetComponent<DraggableCard>();
                    card.Rect.sizeDelta = new Vector2(200f, 280f);
                    var icon = (RectTransform)root.transform.Find("CardSymbol");
                    Place(icon, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * 200f, new Vector2(.5f, .5f));
                    string title = card.UpgradeType == UpgradeKind.RailSystem ? "Rail System"
                        : card.UpgradeType == UpgradeKind.Breakwater ? "Elevator" : "Fire Sprinkler";
                    var label = Label("InventoryName", root.transform, title, 52f, new Color(.85f, .92f, .93f));
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    Place(label.rectTransform, new Vector2(.5f, .5f), new Vector2(0f, 104f),
                        new Vector2(300f, 52f), new Vector2(.5f, 0f));
                    var hoverName = root.transform.Find("HoverDetails/ProductName")?.GetComponent<TMP_Text>();
                    if (hoverName != null)
                    {
                        hoverName.text = title;
                        hoverName.gameObject.SetActive(false);
                    }
                    foreach (string frameName in new[] { "NewItemOutline", "HoverOutline" })
                        if (root.transform.Find(frameName) is RectTransform frame)
                            Place(frame, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * 216f, new Vector2(.5f, .5f));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        public static void BuildForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            Arrange(Object.FindFirstObjectByType<CardGameController>());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved left card sidebar: four Vault spaces above four people spaces, with fixed city framing.");
        }

        public static void MoveGridForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            MoveGridNextToSidebar();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
        }

        [MenuItem("Tools/Civil Game Jam/Move Saved Grid Next To Sidebar")]
        public static void MoveGridNextToSidebar()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Move the saved grid in Edit Mode.");
            var game = Object.FindFirstObjectByType<CardGameController>();
            Canvas.ForceUpdateCanvases();
            var panel = (RectTransform)game.transform.Find("GridPanel");
            float contentWidth = game.Grid.GetCell(CardGrid.Columns - 1).anchoredPosition.x -
                game.Grid.GetCell(0).anchoredPosition.x + game.Grid.CellSize.x;
            panel.anchorMin = new Vector2(0f, panel.anchorMin.y);
            panel.anchorMax = new Vector2(1f, panel.anchorMax.y);
            panel.offsetMin = new Vector2(24f, panel.offsetMin.y);
            panel.offsetMax = new Vector2(24f + contentWidth + 2f - ((RectTransform)game.transform).rect.width, panel.offsetMax.y);
            var slots = game.Grid.rectTransform;
            slots.anchorMin = new Vector2(0f, slots.anchorMin.y);
            slots.anchorMax = new Vector2(1f, slots.anchorMax.y);
            slots.offsetMin = new Vector2(0f, slots.offsetMin.y);
            slots.offsetMax = new Vector2(0f, slots.offsetMax.y);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log("Saved grid beside the left card sidebar, retaining card sizes, row heights and spacing.");
        }

        [MenuItem("Tools/Civil Game Jam/Arrange Saved Left Card Sidebar")]
        public static void ArrangeActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Arrange the saved sidebar in Edit Mode.");
            Arrange(Object.FindFirstObjectByType<CardGameController>());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static void Arrange(CardGameController game)
        {
            if (game == null || game.Vault == null) throw new System.InvalidOperationException("Open the authored card scene first.");
            Canvas.ForceUpdateCanvases();
            var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
            var rect = Box("CardSidebar", game.transform, Surface, true);
            if (!rect.TryGetComponent<CardSidebar>(out var sidebar)) sidebar = rect.gameObject.AddComponent<CardSidebar>();
            Refs(sidebar, ("game", game), ("canvasRoot", canvas.transform));
            Refs(game, ("sidebar", sidebar));
            var dismiss = rect.GetComponent<CardInspectionDismiss>() ?? rect.gameObject.AddComponent<CardInspectionDismiss>();
            Refs(dismiss, ("game", game));
            sidebar.RefreshLayout();
            float width = rect.rect.width;
            const float inset = 24f, gap = 16f, cardHeight = 300f;
            float cellWidth = (width - inset * 2f - gap) / 2f;

            var vault = game.Vault.Area;
            vault.SetParent(rect, false);
            Place(vault, Vector2.up, Vector2.zero, new Vector2(width, 710f), Vector2.up);
            vault.GetComponent<Image>().color = Color.clear;
            if (vault.TryGetComponent<Outline>(out var vaultEdge)) vaultEdge.enabled = false;
            Title(vault.Find("Title").GetComponent<TextMeshProUGUI>(), "VAULT", -12f);
            var description = vault.Find("Description").GetComponent<TextMeshProUGUI>();
            description.text = "Discovered blueprints"; description.alignment = TextAlignmentOptions.MidlineLeft;
            Place(description.rectTransform, Vector2.up, new Vector2(inset, -49f), new Vector2(width - inset * 2f, 22f), Vector2.up);
            for (int i = 0; i < 4; i++)
            {
                var slot = i < 3 ? (RectTransform)game.Vault.Slots[i].transform : Box("BlueprintSlot3", vault, SlotColor);
                slot.SetParent(vault, false);
                Place(slot, Vector2.up, new Vector2(inset + cellWidth / 2f + i % 2 * (cellWidth + gap), -80f - cardHeight / 2f - i / 2 * (cardHeight + gap)),
                    new Vector2(cellWidth, cardHeight), new Vector2(0.5f, 0.5f));
                Frame(slot);
                if (i == 3)
                {
                    var empty = Label("EmptyLabel", slot, "", 18f, Muted);
                    Place(empty.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cellWidth - 20f, 36f), new Vector2(0.5f, 0.5f));
                    continue;
                }
                slot.GetComponent<Image>().color = SlotColor;
                var visual = game.Vault.Slots[i].CardRect;
                Place(visual, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(200f, 300f), new Vector2(0.5f, 0f));
                visual.localScale = Vector3.one;
                var illustration = (RectTransform)visual.Find("Illustration");
                illustration.sizeDelta = new Vector2(164f, 132f);
                foreach (var label in visual.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (label.transform.IsChildOf(visual.Find("HoverDetails"))) continue;
                    float fontSize = label.name == "Name" ? 24f : label.name == "Category" ? 15f : 17f;
                    label.fontSize = label.fontSizeMax = fontSize; label.fontSizeMin = fontSize * 0.75f;
                }
            }

            var peopleArea = Box("People", rect, Color.clear);
            Place(peopleArea, Vector2.up, new Vector2(0f, -746f), new Vector2(width, 692f), Vector2.up);
            var peopleTitle = Label("Title", peopleArea, "PEOPLE", 30f, Gold, FontStyles.Bold);
            Title(peopleTitle, "PEOPLE", 0f);
            var cells = new RectTransform[4];
            for (int i = 0; i < cells.Length; i++)
            {
                var slot = Box("PeopleSlot" + i, peopleArea, SlotColor);
                Place(slot, Vector2.up, new Vector2(inset + cellWidth / 2f + i % 2 * (cellWidth + gap), -60f - cardHeight / 2f - i / 2 * (cardHeight + gap)),
                    new Vector2(cellWidth, cardHeight), new Vector2(0.5f, 0.5f));
                Frame(slot);
                cells[i] = slot;
            }
            var settings = new SerializedObject(sidebar);
            var savedSlots = settings.FindProperty("slots"); savedSlots.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) savedSlots.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            var people = game.GetComponentsInChildren<DraggableCard>().Where(card => card.IsPerson).OrderBy(card => card.name).ToArray();
            for (int i = 0; i < people.Length; i++)
            {
                var card = people[i];
                card.Rect.SetParent(cells[i], false);
                card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
                card.Rect.anchoredPosition = Vector2.zero;
                PrefabUtility.RecordPrefabInstancePropertyModifications(card.Rect);
            }
            Refs(canvas.transform.Find("CityView").GetComponent<CityViewport>(), ("sidebar", rect));
            FitPeopleSlots(game);
            int cardLayerIndex = game.CardLayer.GetSiblingIndex();
            rect.SetSiblingIndex(rect.GetSiblingIndex() < cardLayerIndex ? cardLayerIndex - 1 : cardLayerIndex);
            game.transform.Find("BlueprintChoice").SetAsLastSibling();
            sidebar.RefreshLayout();
            Object.FindFirstObjectByType<CityViewport>().RefreshViewport();
            Canvas.ForceUpdateCanvases();
        }

        internal static void FitPeopleSlots(CardGameController game)
        {
            if (game.Sidebar.PeopleArea != null)
            {
                CardInventoryArtworkAuthoring.ConfigureFreePeopleArea(game);
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PeopleCard.prefab");
            var card = prefab.GetComponent<DraggableCard>();
            Vector2 size = Vector2.Scale(card.Rect.sizeDelta, card.Rect.localScale);
            float height = Mathf.Max(300f, size.y);
            const float gap = 16f;
            float inset = (game.Sidebar.Area.rect.width - size.x * 2f - gap) / 2f;
            for (int i = 0; i < game.Sidebar.Slots.Count; i++)
                Place(game.Sidebar.Slots[i], Vector2.up,
                    new Vector2(inset + size.x / 2f + i % 2 * (size.x + gap), -60f - height / 2f - i / 2 * (height + gap)),
                    new Vector2(size.x, height), new Vector2(0.5f, 0.5f));
        }

        private static void Title(TextMeshProUGUI label, string text, float y)
        {
            var parent = (RectTransform)label.transform.parent;
            label.text = text; label.color = Gold; label.fontSize = label.fontSizeMax = 30f; label.fontSizeMin = 26f;
            label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.MidlineLeft;
            Place(label.rectTransform, Vector2.up, new Vector2(24f, y), new Vector2(parent.rect.width - 48f, 38f), Vector2.up);
        }

        private static void Frame(RectTransform rect)
        {
            if (!rect.TryGetComponent<Outline>(out var outline)) outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.40f, 0.43f, 0.7f); outline.effectDistance = new Vector2(1f, -1f);
        }

        private static RectTransform Box(string name, Transform parent, Color color, bool raycast = false)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            }
            if (!rect.TryGetComponent<Image>(out var image)) image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast;
            return rect;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            }
            if (!rect.TryGetComponent<TextMeshProUGUI>(out var label)) label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset; label.text = text; label.fontSize = size;
            label.fontSizeMax = size; label.fontSizeMin = size * 0.75f; label.enableAutoSizing = true;
            label.color = color; label.fontStyle = style; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            return label;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }

        private static void Refs(Object target, params (string name, Object value)[] refs)
        {
            var data = new SerializedObject(target);
            foreach (var item in refs) data.FindProperty(item.name).objectReferenceValue = item.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
