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
    public static class CardVaultAuthoring
    {
        private static readonly string[] Titles = { "Rail System", "Elevator", "Fire Protection" };
        private static readonly Color Ink = new Color(0.055f, 0.10f, 0.12f);
        private static readonly Color Paper = new Color(0.89f, 0.95f, 0.93f);
        private static readonly Color Muted = new Color(0.57f, 0.72f, 0.73f);
        private static readonly Color Gold = new Color(0.96f, 0.75f, 0.39f);

        public static void BuildForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            Configure(Object.FindFirstObjectByType<CardGameController>());
        }

        public static void BuildSavedVaultCardsForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            BuildVault(game);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved larger Vault cards, smooth hover feedback and editable side information panels.");
        }

        [MenuItem("Tools/Civil Game Jam/Build Saved Blueprint Vault And Choices")]
        public static void BuildActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Build the saved Vault in Edit Mode.");
            Configure(Object.FindFirstObjectByType<CardGameController>());
        }

        private static void Configure(CardGameController game)
        {
            if (game == null) throw new System.InvalidOperationException("Open 2DCardScene first.");
            BuildVault(game);
            BuildChoices(game);
            foreach (string name in new[] { "RaylıSistemBlueprint", "DalgaKıranBlueprint", "YangınBlueprint", "FactoryCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var label = prefab.transform.Find("HoverDetails/Properties/Description").GetComponent<TextMeshProUGUI>();
                    label.text = name == "FactoryCard"
                        ? "Each blueprint: 2 items\nWorkers are kept."
                        : "1 plan -> 2 items";
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved blueprint discovery Vault, larger grouped choices, translucent backdrop and two products for every plan.");
        }

        private static void BuildVault(CardGameController game)
        {
            Canvas.ForceUpdateCanvases();
            var people = game.GetComponentsInChildren<DraggableCard>().Where(card => card.IsPerson).OrderBy(card => card.name).ToArray();
            for (int i = 0; game.Sidebar == null && i < people.Length; i++)
            {
                float x = people[i].Rect.anchoredPosition.x;
                people[i].Rect.position = game.Grid.GetCellWorldPosition(i / 2 * CardGrid.Columns);
                people[i].Rect.anchoredPosition = new Vector2(x, people[i].Rect.anchoredPosition.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(people[i].Rect);
            }
            var area = game.Vault != null ? game.Vault.Area : Box("Vault", game.transform, Ink);
            Place(area, Vector2.zero, new Vector2(18f, 8f), new Vector2(480f, 244f), Vector2.zero);
            var edge = area.GetComponent<Outline>() ?? area.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.36f, 0.56f, 0.57f, 0.75f); edge.effectDistance = new Vector2(1f, -1f);
            var title = Text("Title", area, "VAULT", 24f, Gold, FontStyles.Bold);
            Place(title.rectTransform, Vector2.up, new Vector2(14f, -5f), new Vector2(100f, 27f), Vector2.up);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var description = Text("Description", area, "Discovered blueprints", 14f, Muted);
            Place(description.rectTransform, Vector2.one, new Vector2(-14f, -6f), new Vector2(320f, 26f), Vector2.one);
            description.alignment = TextAlignmentOptions.MidlineRight;
            var vault = area.GetComponent<BlueprintVault>() ?? area.gameObject.AddComponent<BlueprintVault>();
            var slots = new BlueprintVaultSlot[3];
            const float width = (480f - 24f - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                // A fixed hit area prevents the enlargement itself from toggling the hover.
                var rect = Box("BlueprintSlot" + i, area, Color.clear, true);
                Place(rect, Vector2.up, new Vector2(12f + i * (width + 8f), -34f), new Vector2(width, 204f), Vector2.up);
                var visual = Box("CardVisual", rect, new Color(0.085f, 0.13f, 0.15f));
                Place(visual, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(136f, 204f), new Vector2(0.5f, 0f));
                visual.localScale = Vector3.one;
                foreach (string oldName in new[] { "Icon", "Name", "Discovery", "Quantity" })
                {
                    var old = rect.Find(oldName);
                    if (old != null) old.SetParent(visual, false);
                }
                var accent = UpgradeIcon.Accent((UpgradeKind)(i + 1));
                var border = visual.GetComponent<Outline>() ?? visual.gameObject.AddComponent<Outline>();
                border.effectColor = Color.Lerp(accent, Paper, 0.35f); border.effectDistance = new Vector2(1.5f, -1.5f);
                var shadow = visual.GetComponents<Shadow>().FirstOrDefault(component => !(component is Outline)) ?? visual.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.12f); shadow.effectDistance = new Vector2(4f, -5f);
                var category = Text("Category", visual, "BLUEPRINT", 12f, Gold, FontStyles.Bold);
                AtTop(category, 4f, -6f, -4f, 19f);
                var illustration = Box("Illustration", visual, new Color(0.72f, 0.83f, 0.83f));
                Place(illustration, new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(112f, 88f), new Vector2(0.5f, 0.5f));
                var iconRect = visual.Find("Icon") as RectTransform ?? Rect("Icon", illustration);
                iconRect.SetParent(illustration, false);
                Stretch(iconRect, new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.92f));
                var icon = iconRect.GetComponent<UpgradeIcon>() ?? iconRect.gameObject.AddComponent<UpgradeIcon>();
                icon.Kind = (UpgradeKind)(i + 1); icon.raycastTarget = false;
                icon.color = new Color(0.35f, 0.46f, 0.48f, 0.5f);
                var name = Text("Name", visual, Titles[i], 18f, Paper, FontStyles.Bold);
                Stretch(name.rectTransform, new Vector2(0.025f, 0.29f), new Vector2(0.975f, 0.47f));
                var discovery = Text("Discovery", visual, "", 13f, Muted, FontStyles.Bold);
                Stretch(discovery.rectTransform, new Vector2(0.025f, 0.14f), new Vector2(0.975f, 0.26f));
                var quantity = Text("Quantity", visual, "", 13f, Muted);
                Stretch(quantity.rectTransform, new Vector2(0.025f, 0.015f), new Vector2(0.975f, 0.13f));
                slots[i] = rect.GetComponent<BlueprintVaultSlot>() ?? rect.gameObject.AddComponent<BlueprintVaultSlot>();
                Refs(slots[i], ("vault", vault), ("cardRect", visual), ("background", visual.GetComponent<Image>()), ("icon", icon),
                    ("discovery", discovery), ("quantity", quantity));
                var data = new SerializedObject(slots[i]);
                data.FindProperty("kind").enumValueIndex = i + 1;
                data.ApplyModifiedPropertiesWithoutUndo();
                BuildVaultHover(slots[i], visual, shadow, i);
            }
            Refs(vault, ("game", game), ("area", area));
            var vaultData = new SerializedObject(vault);
            var savedSlots = vaultData.FindProperty("slots"); savedSlots.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) savedSlots.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            vaultData.ApplyModifiedPropertiesWithoutUndo();
            Refs(game, ("vault", vault));
            if (game.Sidebar != null)
            {
                CardSidebarAuthoring.Arrange(game);
                return;
            }
            // Lifted cards render above their saved inventory controls.
            int layerIndex = game.CardLayer.GetSiblingIndex();
            area.SetSiblingIndex(area.GetSiblingIndex() < layerIndex ? layerIndex - 1 : layerIndex);
        }

        private static void BuildVaultHover(BlueprintVaultSlot slot, RectTransform visual, Shadow shadow, int index)
        {
            var feedback = slot.GetComponent<CardFeedback>() ?? slot.gameObject.AddComponent<CardFeedback>();
            Refs(feedback, ("rect", visual), ("shadow", shadow));
            var tuning = new SerializedObject(feedback);
            tuning.FindProperty("hoverScale").floatValue = 1.21125f;
            tuning.FindProperty("hoverSeconds").floatValue = 0.24f;
            tuning.ApplyModifiedPropertiesWithoutUndo();
            var details = Rect("HoverDetails", visual);
            Place(details, new Vector2(1f, 0.5f), new Vector2(14f, 0f), new Vector2(240f, 234f), new Vector2(0f, 0.5f));
            details.localScale = Vector3.one * 0.8f;
            if (!details.TryGetComponent<CanvasGroup>(out var group)) group = details.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            BuildInfoPanel(details, "Information", 0f, Titles[index],
                "Drag to the factory.\nProduction needs\nat least one worker.", new Color(0.68f, 0.90f, 0.46f));
            BuildInfoPanel(details, "Properties", -122f, "PRODUCES 2 ITEMS",
                "Ürünleri evdeki " + Titles[index] + " yuvasına tak.\nKeşif Vault'ta kalır.", Gold);
            details.gameObject.SetActive(false);
            var hover = slot.GetComponent<CardHover>() ?? slot.gameObject.AddComponent<CardHover>();
            Refs(hover, ("card", null), ("vaultSlot", slot), ("feedback", feedback), ("details", details));
        }

        private static void BuildInfoPanel(RectTransform details, string name, float y, string title, string body, Color accent)
        {
            var panel = Box(name, details, new Color(0.025f, 0.033f, 0.04f, 0.98f));
            panel.GetComponent<Image>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            panel.GetComponent<Image>().type = Image.Type.Sliced;
            panel.anchorMin = Vector2.up; panel.anchorMax = Vector2.one; panel.pivot = new Vector2(0.5f, 1f);
            panel.sizeDelta = new Vector2(0f, 112f); panel.anchoredPosition = new Vector2(0f, y);
            var heading = Text("Title", panel, title, 24f, accent, FontStyles.Bold);
            AtTop(heading, 12f, -9f, -12f, 29f);
            var description = Text("Description", panel, body, 18f, Color.white);
            AtTop(description, 12f, -42f, -12f, 61f);
        }

        private static void BuildChoices(CardGameController game)
        {
            var panel = (RectTransform)game.transform.Find("BlueprintChoice");
            panel.GetComponent<Image>().color = new Color(0.10f, 0.20f, 0.25f, 0.38f);
            var window = Box("SelectionWindow", panel, new Color(0.09f, 0.16f, 0.20f, 0.96f));
            Place(window, new Vector2(0.5f, 0.54f), Vector2.zero, new Vector2(1240f, 840f), new Vector2(0.5f, 0.5f));
            window.SetAsFirstSibling();
            var edge = window.GetComponent<Outline>() ?? window.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.47f, 0.68f, 0.69f, 0.85f); edge.effectDistance = new Vector2(2f, -2f);
            var eyebrow = Text("Eyebrow", window, "RESEARCH COMPLETE", 21f, Gold, FontStyles.Bold);
            AtTop(eyebrow, 24f, -30f, -24f, 30f);
            var title = panel.Find("Title").GetComponent<TextMeshProUGUI>();
            title.text = "Discover a blueprint"; title.fontSize = title.fontSizeMax = 48f; title.fontSizeMin = 36f;
            title.color = Paper; title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(0.5f, 0.54f), new Vector2(0f, 310f), new Vector2(1160f, 72f), new Vector2(0.5f, 0.5f));
            var description = Text("Description", window, "Your plan goes to the Vault · Produces 2 items at the factory", 23f, Muted);
            AtTop(description, 24f, -144f, -24f, 38f);
            var footer = Text("Footer", window, "Your discovery stays in the Vault after you use the plan.", 20f, Muted);
            Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(1160f, 40f), new Vector2(0.5f, 0.5f));
            var settings = new SerializedObject(game);
            var options = settings.FindProperty("blueprints");
            for (int i = 0; i < options.arraySize; i++)
            {
                var option = options.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("productCount").intValue = 2;
                var button = (Button)option.FindPropertyRelative("button").objectReferenceValue;
                var rect = (RectTransform)button.transform;
                Place(rect, new Vector2(0.5f, 0.54f), new Vector2((i - 1) * 374f, -50f), new Vector2(350f, 480f), new Vector2(0.5f, 0.5f));
                var accent = i == 2 ? new Color(0.94f, 0.58f, 0.32f) : new Color(0.40f, 0.72f, 0.78f);
                button.GetComponent<Image>().color = new Color(0.13f, 0.26f, 0.30f);
                var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
                outline.effectColor = accent; outline.effectDistance = new Vector2(2f, -2f);
                var preview = (RectTransform)button.transform.Find("CardPreview");
                Place(preview, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(200f, 300f), new Vector2(0.5f, 0.5f));
                preview.localScale = Vector3.one * 1.05f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
                var label = button.transform.Find("Name").GetComponent<TextMeshProUGUI>();
                label.text = Titles[i] + "\n2 KART"; label.fontStyle = FontStyles.Bold;
                label.fontSize = label.fontSizeMax = 30f; label.fontSizeMin = 24f; label.color = Paper;
                Stretch(label.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.22f));
                button.transform.SetAsLastSibling();
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            title.transform.SetAsLastSibling();
            panel.SetAsLastSibling();
            panel.gameObject.SetActive(false);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect != null) return rect;
            rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }
        private static RectTransform Box(string name, Transform parent, Color color, bool raycast = false)
        {
            var rect = Rect(name, parent);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast;
            return rect;
        }
        private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var rect = Rect(name, parent);
            var label = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset; label.text = value; label.fontSize = size; label.color = color;
            label.fontStyle = style; label.raycastTarget = false; label.enableAutoSizing = true;
            label.fontSizeMin = size * 0.75f; label.fontSizeMax = size;
            label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void AtTop(TextMeshProUGUI label, float left, float y, float right, float height)
        {
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, y - height); rect.offsetMax = new Vector2(right, y);
        }
        private static void Refs(Object target, params (string name, Object value)[] refs)
        {
            var data = new SerializedObject(target);
            foreach (var item in refs) data.FindProperty(item.name).objectReferenceValue = item.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
