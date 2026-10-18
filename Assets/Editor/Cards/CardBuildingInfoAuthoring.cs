using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardBuildingInfoAuthoring
    {
        // Shared building palette. The resulting colors and text remain editable on the saved UI.
        private static readonly Color Research = new Color32(83, 185, 255, 255);
        private static readonly Color Factory = new Color32(188, 151, 255, 255);
        private static readonly Color School = new Color32(120, 222, 165, 255);
        private static readonly Color House = new Color32(247, 199, 112, 255);
        private static readonly Color[] ProtectionColors =
        {
            new Color32(255, 170, 85, 255),
            new Color32(83, 185, 255, 255),
            new Color32(255, 103, 103, 255)
        };
        private static readonly string[] ProtectionRows =
            { "RailSystemInformation", "FloodProtectionInformation", "FireProtectionInformation" };

        [MenuItem("Tools/Card Game/Save System Colors And Remove Factory Instructions")]
        public static void SaveSystemColorsAndFactoryForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author system colors in Edit Mode.");
            foreach (string name in new[] { "HouseCard", "FactoryCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (name == "HouseCard") TintProtectionHeadings(root);
                    else RemoveFactoryInstructions(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            foreach (var card in Object.FindFirstObjectByType<CardGameController>().GetComponentsInChildren<DraggableCard>(true))
            {
                if (card.Kind == CardKind.House) TintProtectionHeadings(card.gameObject);
                else if (card.Kind == CardKind.Factory) RemoveFactoryInstructions(card.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved orange rail, blue flood and red fire headings, and removed the obsolete factory instruction widgets.");
        }

        private static void TintProtectionHeadings(GameObject root)
        {
            for (int i = 0; i < ProtectionRows.Length; i++)
            {
                var title = root.GetComponent<CardHover>().Details.Find("Information/" + ProtectionRows[i] + "/Title")
                    .GetComponent<TextMeshProUGUI>();
                title.color = ProtectionColors[i];
                SaveObject(title);
            }
        }

        internal static void RemoveFactoryInstructions(GameObject root)
        {
            foreach (string path in new[] { "FolderArt/BlueprintInput", "FolderArt/ProductionStatus", "BlueprintInput", "ProductionStatus" })
            {
                var widget = root.transform.Find(path);
                if (widget != null) Object.DestroyImmediate(widget.gameObject);
            }
        }

        [MenuItem("Tools/Card Game/Save House Prevention Information")]
        public static void SaveHouseForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author house information in Edit Mode.");
            const string path = "Assets/Prefabs/HouseCard.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureHouseInformation(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            foreach (var card in Object.FindFirstObjectByType<CardGameController>()
                         .GetComponentsInChildren<DraggableCard>(true).Where(card => card.Kind == CardKind.House))
                ConfigureHouseInformation(card.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the short House description and editable prevention entries for installed products before Play Mode.");
        }

        [MenuItem("Tools/Card Game/Save Building Information And Repair Worker Slots")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author building information in Edit Mode.");
            foreach (string name in new[] { "ResearchCenterCard", "FactoryCard", "SchoolCard", "HouseCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureInformation(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            var game = Object.FindFirstObjectByType<CardGameController>();
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true).Where(card => card.IsBuilding))
            {
                ConfigureInformation(card.gameObject);
                RepairWorkerSlots(card.gameObject);
            }
            // Keep the user's city without street lamps; never re-import or rebuild the world here.
            var world = GameObject.Find("World3D");
            if (world != null)
                foreach (var lamps in world.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Street_Lamps").ToArray())
                    Object.DestroyImmediate(lamps.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved one right-side information panel per building, repaired native factory controls and kept the city without street lamps.");
        }

        private static void RepairWorkerSlots(GameObject root)
        {
            var station = root.GetComponent<BuildingStation>();
            if (station == null) return;
            if (root.GetComponent<DraggableCard>().Kind == CardKind.Factory && PrefabUtility.IsPartOfPrefabInstance(root))
                foreach (var removed in PrefabUtility.GetRemovedGameObjects(root).ToArray())
                    if (removed.assetGameObject != null &&
                        removed.assetGameObject.name == "WorkerPockets")
                        removed.Revert(InteractionMode.AutomatedAction);

            var slots = root.GetComponentsInChildren<WorkerSlot>(true).OrderBy(slot => slot.Index).ToArray();
            if (slots.Length != BuildingStation.Capacity || slots[0].Index != 0 || slots[1].Index != 1)
                throw new InvalidOperationException(root.name + " needs its two saved interactive worker slots.");
            var data = new SerializedObject(station);
            var indicators = data.FindProperty("indicators");
            indicators.arraySize = BuildingStation.Capacity;
            for (int i = 0; i < slots.Length; i++) indicators.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            SaveObject(station);
        }

        private static void ConfigureInformation(GameObject root)
        {
            var card = root.GetComponent<DraggableCard>();
            if (card.Kind == CardKind.Factory) RemoveFactoryInstructions(root);
            var hover = root.GetComponent<CardHover>();
            var details = hover.Details;
            var secondary = details.Find("Properties");
            if (secondary != null) Object.DestroyImmediate(secondary.gameObject);
            details.anchorMin = details.anchorMax = new Vector2(1f, 0.5f);
            details.pivot = new Vector2(0f, 0.5f);
            details.anchoredPosition = new Vector2(18f, 10f);
            details.sizeDelta = new Vector2(282f, 244f);
            details.localScale = Vector3.one * 0.8f;
            var panel = (RectTransform)details.Find("Information");
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = panel.anchoredPosition = Vector2.zero;
            GetCopy(card.Kind, out string title, out string description, out Color accent);
            var background = panel.GetComponent<Image>();
            background.color = new Color32(9, 12, 17, 250);
            background.raycastTarget = false;
            var shadow = panel.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
            shadow.effectDistance = new Vector2(3f, -5f);
            var border = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(accent.r, accent.g, accent.b, 0.28f);
            border.effectDistance = new Vector2(1f, -1f);
            var heading = panel.Find("Title").GetComponent<TextMeshProUGUI>();
            Label(heading, title, 26f, accent, 18f, 36f, FontStyles.Bold);
            var body = panel.Find("Description").GetComponent<TextMeshProUGUI>();
            Label(body, description, 19f, new Color32(225, 233, 242, 255), 66f, 158f, FontStyles.Normal);
            body.enableAutoSizing = true;
            body.fontSizeMin = 18f;
            body.fontSizeMax = 19f;
            body.lineSpacing = 2f;
            var settings = new SerializedObject(hover);
            settings.FindProperty("keepDetailsOnRight").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            details.gameObject.SetActive(false);
            foreach (var item in new Object[] { details, panel, background, shadow, border, heading, heading.rectTransform,
                         body, body.rectTransform, hover }) SaveObject(item);
            if (card.Kind == CardKind.House) ConfigureHouseInformation(root);
        }

        private static void ConfigureHouseInformation(GameObject root)
        {
            var details = root.GetComponent<CardHover>().Details;
            var display = root.GetComponent<HouseUpgradeInformation>() ?? root.AddComponent<HouseUpgradeInformation>();
            var data = new SerializedObject(display);
            data.FindProperty("house").objectReferenceValue = root.GetComponent<HouseUpgrades>();
            var entries = data.FindProperty("entries");
            entries.arraySize = 3;
            string[] messages =
            {
                "<color=#FFAA55>Rail System</color>\nEarthquake resistant.",
                "<color=#53B9FF>Elevator</color>\nFlood resistant.",
                "<color=#FF6767>Fire Protection</color>\nFire resistant."
            };
            for (int i = 0; i < entries.arraySize; i++)
            {
                var label = details.Find("Information (" + (i + 1) + ")/Description")?.GetComponent<TextMeshProUGUI>();
                if (label == null) throw new InvalidOperationException(root.name + " needs its three authored protection labels.");
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("kind").enumValueIndex = i + 1;
                entry.FindPropertyRelative("row").objectReferenceValue = label.rectTransform.parent;
                entry.FindPropertyRelative("description").objectReferenceValue = label;
                entry.FindPropertyRelative("installedDescription").stringValue = messages[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            SaveObject(display);
        }

        private static void Label(TextMeshProUGUI label, string text, float size, Color color, float top, float height, FontStyles style)
        {
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-36f, height);
            rect.anchoredPosition = new Vector2(0f, -top);
            label.text = text;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
        }

        private static void GetCopy(CardKind kind, out string title, out string description, out Color accent)
        {
            switch (kind)
            {
                case CardKind.ResearchCenter:
                    title = "Research Center"; accent = Research;
                    description = "Assign 1 or 2 researchers to discover any blueprint. Educated people research faster. Discoveries go to the Vault."; break;
                case CardKind.Factory:
                    title = "Factory"; accent = Factory;
                    description = "Rail System needs 1 worker. Elevator and Fire Sprinkler need 2. Produce 2 items per order. Educated people work faster. Blueprints are kept."; break;
                case CardKind.School:
                    title = "School"; accent = School;
                    description = "Assign up to 2 people to train them as educated workers. Each student trains independently, then returns to the People area."; break;
                case CardKind.House:
                    title = "House"; accent = House;
                    description = "Can be upgraded with prevention systems."; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void SaveObject(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
