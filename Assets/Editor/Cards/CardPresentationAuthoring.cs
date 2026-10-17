using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardPresentationAuthoring
    {
        [MenuItem("Tools/Card Game/Save Card Presentation Effects")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var card = root.GetComponent<DraggableCard>();
                    if (card == null) continue;
                    var settings = new SerializedObject(root.GetComponent<CardFeedback>());
                    settings.FindProperty("dragTiltDegrees").floatValue = 4f;
                    settings.FindProperty("tiltSmoothSeconds").floatValue = 0.11f;
                    settings.FindProperty("placementSquash").floatValue = 0.035f;
                    settings.FindProperty("placementSeconds").floatValue = 0.34f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    if (card.Kind == CardKind.Blueprint) Discovery(root, card);
                    if (card.Kind == CardKind.House) House(root);
                    if (card.Kind == CardKind.Factory || card.Kind == CardKind.ResearchCenter) Workplace(root, card);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            int workplaces = game.GetComponentsInChildren<BuildingWorkEffects>().Length;
            int houseSlots = game.GetComponentsInChildren<HouseUpgradeSlot>().Length;
            if (workplaces != 2 || houseSlots != 12)
                throw new InvalidOperationException("Saved board presentation: " + workplaces + " workplaces, " + houseSlots + " house slots.");
            Debug.Log("Saved drag motion, blueprint reveals and trails, house installation previews, and working building effects.");
        }

        private static void Discovery(GameObject root, DraggableCard card)
        {
            var size = card.Rect.sizeDelta;
            var canvas = Component<Canvas>(root.transform);
            // World space preserves the isolated prefab's native rect. In the board it
            // inherits the existing UI canvas, including its current capture camera.
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = false;
            canvas.sortingOrder = 0;
            card.Rect.sizeDelta = size;
            var raycaster = Component<GraphicRaycaster>(root.transform);
            var trailRect = Rect("DiscoveryTrail", root.transform, Vector2.zero, card.Rect.rect.size);
            trailRect.SetAsFirstSibling();
            var trail = Component<CardLightTrail>(trailRect);
            trail.color = new Color(0.55f, 0.9f, 1f, 0.9f);
            trail.raycastTarget = false;
            trail.maskable = false;
            trailRect.gameObject.SetActive(false);
            var area = Rect("DiscoveryReveal", root.transform, Vector2.zero, card.Rect.rect.size);
            Component<RectMask2D>(area);
            var flashRect = Rect("Flash", area, Vector2.zero, card.Rect.rect.size);
            var flash = Component<Image>(flashRect);
            flash.color = new Color(0.82f, 0.95f, 1f, 0.14f);
            flash.raycastTarget = false;
            var rim = Frame("Rim", area, -3f, new Color(0.6f, 0.93f, 1f, 0.7f));
            var shine = Soft("Shine", area, Vector2.zero, new Vector2(70f, 650f),
                new Color(0.85f, 0.97f, 1f, 0.36f), true);
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            area.gameObject.SetActive(false);
            var effects = Component<BlueprintDiscoveryEffects>(root.transform);
            Refs(effects, ("card", card), ("feedback", root.GetComponent<CardFeedback>()),
                ("revealArea", area), ("flash", flash), ("rim", rim), ("shine", shine), ("trail", trail),
                ("flightCanvas", canvas), ("flightRaycaster", raycaster));
            root.transform.Find("HoverDetails").SetAsLastSibling();
        }

        private static void House(GameObject root)
        {
            foreach (var slot in root.GetComponentsInChildren<HouseUpgradeSlot>(true))
            {
                var slotData = new SerializedObject(slot);
                var source = (UpgradeIcon)slotData.FindProperty("icon").objectReferenceValue;
                var previewRect = Rect("InstallPreview", slot.transform, Vector2.zero, source.rectTransform.rect.size);
                var preview = Component<UpgradeIcon>(previewRect);
                var iconData = new SerializedObject(preview);
                var sourceData = new SerializedObject(source);
                foreach (string field in new[] { "kind", "railSystemSprite", "breakwaterSprite", "fireSprite" })
                {
                    var destination = iconData.FindProperty(field);
                    if (field == "kind") destination.enumValueIndex = sourceData.FindProperty(field).enumValueIndex;
                    else destination.objectReferenceValue = sourceData.FindProperty(field).objectReferenceValue;
                }
                iconData.ApplyModifiedPropertiesWithoutUndo();
                preview.color = source.color;
                preview.raycastTarget = false;
                preview.maskable = false;
                var canvas = Component<Canvas>(previewRect);
                canvas.overrideSorting = true;
                canvas.sortingOrder = 10;
                previewRect.gameObject.SetActive(false);
                var glow = Frame("InstallGlow", slot.transform, 5f, new Color(0.72f, 0.95f, 1f, 0.85f));
                glow.enabled = false;
                Refs(slot, ("installPreview", preview), ("installCanvas", canvas), ("installGlow", glow));
            }
        }

        private static void Workplace(GameObject root, DraggableCard card)
        {
            var layer = Rect("WorkEffects", root.transform, Vector2.zero, card.Rect.rect.size);
            layer.SetSiblingIndex(1);
            var group = Component<CanvasGroup>(layer);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var effects = Component<BuildingWorkEffects>(root.transform);
            Refs(effects, ("station", root.GetComponent<BuildingStation>()),
                ("factory", root.GetComponent<FactoryProduction>()), ("presentation", group));
            var data = new SerializedObject(effects);
            var smoke = data.FindProperty("smoke");
            smoke.arraySize = card.Kind == CardKind.Factory ? 3 : 0;
            if (card.Kind == CardKind.Factory)
            {
                var origin = Rect("SmokeOrigin", layer, new Vector2(90f, 117f), Vector2.zero);
                Box("Chimney", layer, new Vector2(90f, 105f), new Vector2(10f, 24f), new Color(0.29f, 0.4f, 0.42f));
                Box("ChimneyCap", layer, new Vector2(90f, 117f), new Vector2(16f, 4f), new Color(0.24f, 0.34f, 0.36f));
                Box("ChimneyBase", layer, new Vector2(90f, 92f), new Vector2(25f, 5f), new Color(0.42f, 0.54f, 0.52f));
                data.FindProperty("smokeOrigin").objectReferenceValue = origin;
                for (int i = 0; i < smoke.arraySize; i++)
                    smoke.GetArrayElementAtIndex(i).objectReferenceValue = Soft("Smoke" + i, layer, origin.anchoredPosition,
                        new Vector2(25f, 22f), new Color(0.67f, 0.74f, 0.73f, 0.25f));
            }
            else
            {
                var art = (RectTransform)root.transform.Find("FolderArt/BuildingIllustration");
                var mask = Rect("ResearchLightArea", layer, art.anchoredPosition, art.sizeDelta);
                Component<RectMask2D>(mask);
                var scan = Soft("ResearchScan", mask, Vector2.zero, new Vector2(34f, 210f),
                    new Color(0.44f, 0.83f, 1f, 0.3f), true);
                scan.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                var windows = Soft("WindowLight", mask, new Vector2(0f, 7f), new Vector2(165f, 58f),
                    new Color(0.24f, 0.72f, 1f, 0.15f));
                data.FindProperty("scan").objectReferenceValue = scan;
                data.FindProperty("windowLight").objectReferenceValue = windows;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static bool RepairResearchLight(GameObject root)
        {
            var effects = root.GetComponent<BuildingWorkEffects>();
            if (effects == null) return false;
            var data = new SerializedObject(effects);
            if (data.FindProperty("scan").objectReferenceValue != null) return false;
            var layer = root.transform.Find("WorkEffects");
            var art = root.transform.Find("FolderArt/BuildingIllustration") as RectTransform;
            var area = Rect("ResearchLightArea", layer, art != null ? art.anchoredPosition : new Vector2(0f, 45f),
                art != null ? art.rect.size : new Vector2(218f, 132f));
            Component<RectMask2D>(area);
            var scan = Soft("ResearchScan", area, Vector2.zero, new Vector2(34f, 210f), new Color(0.44f, 0.83f, 1f, 0.3f), true);
            scan.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            var windows = Soft("WindowLight", area, new Vector2(0f, 7f), new Vector2(165f, 58f), new Color(0.24f, 0.72f, 1f, 0.15f));
            data.FindProperty("scan").objectReferenceValue = scan;
            data.FindProperty("windowLight").objectReferenceValue = windows;
            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.gameObject.layer = parent.gameObject.layer;
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static T Component<T>(Transform transform) where T : UnityEngine.Component
        {
            var component = transform.GetComponent<T>();
            return component != null ? component : transform.gameObject.AddComponent<T>();
        }

        private static CardSoftGraphic Soft(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool beam = false)
        {
            var graphic = Component<CardSoftGraphic>(Rect(name, parent, position, size));
            graphic.color = color;
            graphic.raycastTarget = false;
            var data = new SerializedObject(graphic);
            data.FindProperty("shape").enumValueIndex = beam ? 1 : 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            return graphic;
        }

        private static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Component<Image>(Rect(name, parent, position, size));
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image Frame(string name, Transform parent, float outside, Color color)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * -outside;
            rect.offsetMax = Vector2.one * outside;
            var image = Component<Image>(rect);
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Refs(Object target, params (string name, Object value)[] fields)
        {
            var data = new SerializedObject(target);
            foreach (var field in fields) data.FindProperty(field.name).objectReferenceValue = field.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
