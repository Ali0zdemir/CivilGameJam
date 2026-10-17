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
    public static class CardInventoryArtworkAuthoring
    {
        private static readonly string[] Plans = { "RaylıSistemBlueprint", "DalgaKıranBlueprint", "YangınBlueprint" };
        private static readonly string[] Art = { "Seismic Big Card (1)", "Elevated Floor Big Card", "Detector Big Card" };
        private static readonly string[] Titles = { "Rail System", "Breakwater", "Fire Protection" };

        [MenuItem("Tools/Card Game/Save New Blueprint Artwork and Free People Area")]
        public static void SaveForBatch()
        {
            foreach (string name in Art)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/EfeArts/" + name + ".png");
                if (importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed) continue;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            for (int i = 0; i < Plans.Length; i++)
            {
                int index = i;
                EditPrefab(Plans[i], root => StyleBlueprint(root, index));
            }
            EditPrefab("FactoryCard", root =>
            {
                foreach (var icon in root.GetComponentsInChildren<UpgradeIcon>(true)) ConfigureBlueprintIcon(icon);
            });
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < Plans.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + Plans[i] + ".prefab");
                var slot = game.Vault.SlotFor((UpgradeKind)(i + 1));
                CopyAppearance(prefab, slot.CardRect);
                var icon = slot.CardRect.Find("CardSymbol").GetComponent<UpgradeIcon>();
                icon.color = new Color(0.55f, 0.62f, 0.65f, 0.55f);
                var data = new SerializedObject(slot);
                data.FindProperty("icon").objectReferenceValue = icon;
                data.FindProperty("background").objectReferenceValue = slot.CardRect.GetComponent<Image>();
                data.FindProperty("preserveArtworkColors").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                SetReference(slot.GetComponent<CardFeedback>(), "shadow", icon.GetComponent<Shadow>());
                var preview = (RectTransform)game.transform.Find("BlueprintChoice/BlueprintOption" + i + "/CardPreview");
                CopyAppearance(prefab, preview);
            }
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true))
                if (card.Kind == CardKind.Factory)
                    foreach (var icon in card.GetComponentsInChildren<UpgradeIcon>(true)) ConfigureBlueprintIcon(icon);
            ConfigureFreePeopleArea(game);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved all three new blueprint illustrations and one square, freely arranged people area.");
        }

        internal static void ConfigureFreePeopleArea(CardGameController game)
        {
            var sidebar = game.Sidebar;
            var parent = (RectTransform)sidebar.transform.Find("People");
            var people = sidebar.GetComponentsInChildren<DraggableCard>(true).Where(card => card.IsPerson).ToArray();
            var positions = people.Select(card => card.Rect.position).ToArray();
            var scales = people.Select(card => card.Rect.localScale).ToArray();
            var area = parent.Find("PeopleArea") as RectTransform;
            if (area == null)
            {
                area = new GameObject("PeopleArea", typeof(RectTransform), typeof(Image), typeof(Outline))
                    .GetComponent<RectTransform>();
                area.gameObject.layer = parent.gameObject.layer;
                area.SetParent(parent, false);
                area.anchorMin = area.anchorMax = new Vector2(0.5f, 1f);
                area.pivot = new Vector2(0.5f, 1f);
                float side = sidebar.Area.rect.width - 48f;
                area.sizeDelta = Vector2.one * side;
                area.anchoredPosition = new Vector2(0f, -60f);
                area.GetComponent<Image>().color = new Color(0.075f, 0.13f, 0.15f);
                area.GetComponent<Image>().raycastTarget = true;
                var outline = area.GetComponent<Outline>();
                outline.effectColor = new Color(0.25f, 0.40f, 0.43f, 0.7f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            for (int i = 0; i < people.Length; i++)
            {
                people[i].Rect.SetParent(area, true);
                people[i].Rect.anchorMin = people[i].Rect.anchorMax = new Vector2(0.5f, 0.5f);
                people[i].Rect.localScale = scales[i];
                people[i].Rect.position = positions[i];
                PrefabUtility.RecordPrefabInstancePropertyModifications(people[i].Rect);
            }
            var settings = new SerializedObject(sidebar);
            settings.FindProperty("peopleArea").objectReferenceValue = area;
            settings.FindProperty("slots").arraySize = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform child in parent.Cast<Transform>().ToArray())
                if (child.name.StartsWith("PeopleSlot", StringComparison.Ordinal)) Object.DestroyImmediate(child.gameObject);
        }

        private static void StyleBlueprint(GameObject root, int index)
        {
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name != "HoverDetails" && child.name != "CardSymbol") Object.DestroyImmediate(child.gameObject);
            root.GetComponent<Image>().color = Color.clear;
            var icon = root.transform.Find("CardSymbol").GetComponent<UpgradeIcon>();
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            icon.color = Color.white;
            icon.raycastTarget = false;
            ConfigureBlueprintIcon(icon);
            var shadow = icon.GetComponents<Shadow>().FirstOrDefault(component => !(component is Outline)) ?? icon.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
            shadow.effectDistance = new Vector2(4f, -5f);
            SetReference(root.GetComponent<CardFeedback>(), "shadow", shadow);
            var title = new GameObject("BlueprintTitle", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            title.gameObject.layer = root.layer;
            title.rectTransform.SetParent(root.transform, false);
            title.rectTransform.anchorMin = new Vector2(0.10f, 0.86f);
            title.rectTransform.anchorMax = new Vector2(0.70f, 0.94f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.font = TMP_Settings.defaultFontAsset;
            title.text = Titles[index];
            title.fontSize = title.fontSizeMax = 20f;
            title.fontSizeMin = 13f;
            title.enableAutoSizing = true;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.13f, 0.17f, 0.39f);
            title.raycastTarget = false;
            root.transform.Find("HoverDetails").SetAsLastSibling();
        }

        private static void ConfigureBlueprintIcon(UpgradeIcon icon)
        {
            var settings = new SerializedObject(icon);
            string[] fields = { "railSystemSprite", "breakwaterSprite", "fireSprite" };
            for (int i = 0; i < fields.Length; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/" + Art[i] + ".png");
                if (sprite == null) throw new InvalidOperationException("Missing blueprint sprite: " + Art[i]);
                settings.FindProperty(fields[i]).objectReferenceValue = sprite;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopyAppearance(GameObject prefab, RectTransform target)
        {
            foreach (Transform child in target.Cast<Transform>().ToArray())
                if (child.name != "HoverDetails") Object.DestroyImmediate(child.gameObject);
            foreach (Transform child in prefab.transform)
            {
                if (child.name == "HoverDetails") continue;
                var copy = Object.Instantiate(child.gameObject, target);
                copy.name = child.name;
                foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
            var source = prefab.GetComponent<Image>();
            var image = target.GetComponent<Image>();
            image.sprite = source.sprite;
            image.type = source.type;
            image.color = source.color;
            image.raycastTarget = false;
            target.Find("HoverDetails")?.SetAsLastSibling();
        }

        private static void EditPrefab(string name, Action<GameObject> edit)
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var settings = new SerializedObject(target);
            settings.FindProperty(field).objectReferenceValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
