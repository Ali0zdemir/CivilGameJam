using System;
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
    public static class CardProductionAuthoring
    {
        private static readonly string[] Plans = { "RaylıSistemBlueprint", "DalgaKıranBlueprint", "YangınBlueprint" };
        private static readonly string[] Products = { "RaylıSistemCard", "DalgaKıranCard", "YangınCard" };
        private static readonly string[] Titles = { "Rail System", "Elevator", "Fire Protection" };
        private static readonly Color Ink = new Color(0.22f, 0.19f, 0.14f);
        private static readonly Color Gold = new Color(0.85f, 0.64f, 0.26f);
        private const string FactoryWorkerInfo = "Workers combine effort.\nEducated workers are faster.\nWorkers are kept.";
        private const string EducatedWorkerInfo = "Works faster in research\nand factory production.";

        [MenuItem("Tools/Card Game/Save Factory Worker Rates")]
        public static void SaveWorkerRatesForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Edit("FactoryCard", root =>
            {
                SetWorkerRate(root.GetComponent<FactoryProduction>());
                SetWorkerInfo(root, FactoryWorkerInfo);
            });
            Edit("EducatedPeopleCard", root => SetWorkerInfo(root, EducatedWorkerInfo));
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true))
            {
                if (card.Kind == CardKind.Factory)
                {
                    SetWorkerRate(card.GetComponent<FactoryProduction>());
                    SetWorkerInfo(card.gameObject, FactoryWorkerInfo);
                }
                else if (card.Kind == CardKind.EducatedPerson) SetWorkerInfo(card.gameObject, EducatedWorkerInfo);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved additive factory worker rates: normal 1, educated 1.8, and existing English information panels.");
        }

        private static void SetWorkerRate(FactoryProduction factory)
        {
            var data = new SerializedObject(factory);
            data.FindProperty("educatedWorkerMultiplier").floatValue = 1.8f;
            if (data.ApplyModifiedPropertiesWithoutUndo() && PrefabUtility.IsPartOfPrefabInstance(factory))
                PrefabUtility.RecordPrefabInstancePropertyModifications(factory);
        }

        private static void SetWorkerInfo(GameObject root, string text)
        {
            var label = root.transform.Find("HoverDetails/Properties/Description").GetComponent<TextMeshProUGUI>();
            label.text = text;
            if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }

        public static void BuildForBatch()
        {
            CloneIfMissing(Plans[0], Plans[2]);
            CloneIfMissing(Products[1], Products[2]);
            for (int i = 0; i < 3; i++)
            {
                CardSceneAuthoring.PreparePrefab(Plans[i], CardKind.Blueprint, Titles[i]);
                CardSceneAuthoring.PreparePrefab(Products[i], CardKind.Upgrade, Titles[i]);
                int index = i;
                Edit(Plans[i], root => StyleItem(root, index, true));
                Edit(Products[i], root => StyleItem(root, index, false));
            }
            Edit("FactoryCard", StyleFactory);
            Edit("HouseCard", StyleHouse);
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            var dismissal = game.GetComponent<CardInspectionDismiss>() ?? game.gameObject.AddComponent<CardInspectionDismiss>();
            Refs(dismissal, ("game", game));
            BuildChoices(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
        }

        private static void CloneIfMissing(string source, string target)
        {
            string path = "Assets/Prefabs/" + target + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null &&
                !AssetDatabase.CopyAsset("Assets/Prefabs/" + source + ".prefab", path))
                throw new InvalidOperationException("Cannot create " + path);
        }

        private static void Edit(string name, Action<GameObject> action)
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { root.name = name; action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void StyleItem(GameObject root, int index, bool plan)
        {
            var kind = (UpgradeKind)(index + 1);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(200f, 300f);
            root.GetComponent<DraggableCard>().SetLayoutScale(Vector3.one * 0.5f);
            var data = new SerializedObject(root.GetComponent<DraggableCard>());
            data.FindProperty("upgradeType").enumValueIndex = index + 1;
            data.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<Image>().color = plan ? new Color(0.87f, 0.93f, 0.93f) : new Color(0.97f, 0.91f, 0.73f);
            var badge = Box("TypeBadge", rect, plan ? new Color(0.18f, 0.37f, 0.47f) : new Color(0.28f, 0.42f, 0.35f),
                new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.98f));
            Text("BadgeText", badge, plan ? "BLUEPRINT" : "ÜRETİLMİŞ KART", 15f, Color.white);
            var icon = Icon("CardSymbol", rect, kind, new Vector2(0.2f, 0.48f), new Vector2(0.8f, 0.83f));
            icon.raycastTarget = false;
            var label = root.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            Stretch(label.rectTransform, new Vector2(0.06f, 0.27f), new Vector2(0.94f, 0.45f));
            label.text = Titles[index]; label.color = Ink; label.fontStyle = FontStyles.Bold;
            label.fontSizeMin = 18f; label.fontSizeMax = 25f;
            var footer = Text("Usage", rect, plan ? "Fabrikaya bırak" : "Ev yuvasına tak", 17f, Ink);
            Stretch(footer.rectTransform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.22f));
            SetInfo(root, Titles[index], plan ? "A plan to manufacture\nat a staffed factory." : "Drop this manufactured item\ninto its matching house slot.",
                plan ? "1 plan -> 2 items" : "Manufactured item.\nOne slot per type in each house.");
        }

        private static void StyleFactory(GameObject root)
        {
            var factory = root.GetComponent<FactoryProduction>() ?? root.AddComponent<FactoryProduction>();
            var station = root.GetComponent<BuildingStation>();
            var title = root.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            Stretch(title.rectTransform, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.86f));
            title.text = "FACTORY"; title.fontStyle = FontStyles.Bold;
            CardBuildingInfoAuthoring.RemoveFactoryInstructions(root);
            Refs(factory, ("station", station), ("progressRoot", root.transform.Find("ProgressBar")),
                ("progressFill", root.transform.Find("ProgressBar/BlueProgress").GetComponent<Image>()), ("outline", root.GetComponent<Outline>()));
            SetInfo(root, "Production", "Requires a blueprint\nand at least one worker.", FactoryWorkerInfo);
        }

        private static void StyleHouse(GameObject root)
        {
            var house = root.GetComponent<HouseUpgrades>() ?? root.AddComponent<HouseUpgrades>();
            var outline = root.GetComponent<Outline>() ?? root.AddComponent<Outline>();
            outline.enabled = false; outline.effectDistance = new Vector2(4f, -4f);
            root.GetComponent<Image>().color = Ink;
            var frame = Box("GoldFrame", root.transform, Gold, new Vector2(0.015f, 0.012f), new Vector2(0.985f, 0.988f));
            frame.SetAsFirstSibling();
            Box("FrameHighlight", frame, new Color(0.98f, 0.79f, 0.38f), new Vector2(0.01f, 0.97f), new Vector2(0.99f, 0.99f));
            var border = Box("PortraitBorder", frame, Ink, new Vector2(0.035f, 0.255f), new Vector2(0.965f, 0.885f));
            var paper = Box("Portrait", border, new Color(0.93f, 0.93f, 0.85f), new Vector2(0.015f, 0.018f), new Vector2(0.985f, 0.982f));
            var art = Rect("HouseIllustration", paper);
            Stretch(art, new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.84f));
            Box("Ground", art, new Color(0.63f, 0.69f, 0.48f), new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.13f));
            Box("Walls", art, new Color(0.82f, 0.69f, 0.46f), new Vector2(0.20f, 0.13f), new Vector2(0.80f, 0.61f));
            Box("Chimney", art, new Color(0.49f, 0.45f, 0.37f), new Vector2(0.68f, 0.67f), new Vector2(0.77f, 0.95f));
            var roof = new Color(0.61f, 0.31f, 0.20f);
            Box("RoofLower", art, roof, new Vector2(0.10f, 0.59f), new Vector2(0.90f, 0.70f));
            Box("RoofMiddle", art, roof, new Vector2(0.22f, 0.70f), new Vector2(0.78f, 0.81f));
            Box("RoofTop", art, roof, new Vector2(0.35f, 0.81f), new Vector2(0.65f, 0.91f));
            var blue = new Color(0.36f, 0.58f, 0.63f);
            Box("LeftWindow", art, blue, new Vector2(0.28f, 0.36f), new Vector2(0.41f, 0.52f));
            Box("RightWindow", art, blue, new Vector2(0.59f, 0.36f), new Vector2(0.72f, 0.52f));
            Box("Door", art, Ink, new Vector2(0.45f, 0.13f), new Vector2(0.55f, 0.37f));
            var title = root.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            title.text = "HOUSE"; title.color = Ink; title.fontStyle = FontStyles.Bold;
            Stretch(title.rectTransform, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f));
            var slots = new HouseUpgradeSlot[3];
            for (int i = 0; i < 3; i++)
            {
                string name = new[] { "RailSystemSlot", "BreakwaterSlot", "FireSlot" }[i];
                var slot = Box(name, root.transform, Ink, Vector2.zero, Vector2.one, true);
                slot.anchorMin = slot.anchorMax = new Vector2(0.19f + i * 0.31f, 0.14f);
                slot.sizeDelta = new Vector2(68f, 58f);
                slot.anchoredPosition = Vector2.zero;
                var fill = Box("Fill", slot, new Color(0.67f, 0.48f, 0.19f), new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.92f));
                var icon = Icon("InstalledIcon", fill, (UpgradeKind)(i + 1), new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.92f));
                icon.gameObject.SetActive(false);
                slots[i] = slot.GetComponent<HouseUpgradeSlot>() ?? slot.gameObject.AddComponent<HouseUpgradeSlot>();
                Refs(slots[i], ("house", house), ("icon", icon), ("fill", fill.GetComponent<Image>()));
                var data = new SerializedObject(slots[i]); data.FindProperty("kind").enumValueIndex = i + 1; data.ApplyModifiedPropertiesWithoutUndo();
                var caption = Text(name + "Caption", root.transform, new[] { "RAYLI", "DALGAKIRAN", "YANGIN" }[i], 10f, Ink);
                Stretch(caption.rectTransform, new Vector2(0.06f + i * 0.31f, 0.035f), new Vector2(0.32f + i * 0.31f, 0.06f));
            }
            Box("BottomRule", root.transform, Ink, new Vector2(0.06f, 0.022f), new Vector2(0.94f, 0.03f));
            Refs(house, ("outline", outline));
            var settings = new SerializedObject(house);
            var array = settings.FindProperty("slots"); array.arraySize = 3;
            for (int i = 0; i < 3; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            SetInfo(root, "House upgrades", "Fabrikada üretilmiş\nkartları bu eve tak.", "3 yuva: raylı sistem,\ndalgakıran ve yangın.\nBlueprint takılamaz.");
        }

        private static void BuildChoices(CardGameController game)
        {
            var panel = game.transform.Find("BlueprintChoice");
            var third = panel.Find("BlueprintOption2");
            if (third == null)
            {
                third = Object.Instantiate(panel.Find("BlueprintOption0").gameObject, panel).transform;
                third.name = "BlueprintOption2";
            }
            var data = new SerializedObject(game);
            var options = data.FindProperty("blueprints"); options.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var button = panel.Find("BlueprintOption" + i).GetComponent<Button>();
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.20f + i * 0.30f, 0.48f);
                rect.sizeDelta = new Vector2(250f, 290f); rect.anchoredPosition = Vector2.zero;
                while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
                UnityEventTools.AddIntPersistentListener(button.onClick, game.SelectBlueprint, i);
                var old = button.transform.Find("CardPreview"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var plan = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + Plans[i] + ".prefab");
                var product = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + Products[i] + ".prefab");
                var preview = (GameObject)PrefabUtility.InstantiatePrefab(plan, button.transform);
                preview.name = "CardPreview";
                Object.DestroyImmediate(preview.GetComponent<CardHover>());
                Object.DestroyImmediate(preview.GetComponent<DraggableCard>());
                Object.DestroyImmediate(preview.GetComponent<CardFeedback>());
                Object.DestroyImmediate(preview.transform.Find("HoverDetails").gameObject);
                var previewRect = (RectTransform)preview.transform;
                previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.62f);
                previewRect.anchoredPosition = Vector2.zero; previewRect.localScale = Vector3.one * 0.57f;
                foreach (var graphic in preview.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                button.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = Titles[i] + " · 2 kart";
                var option = options.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("title").stringValue = Titles[i];
                option.FindPropertyRelative("prefab").objectReferenceValue = plan;
                option.FindPropertyRelative("productPrefab").objectReferenceValue = product;
                option.FindPropertyRelative("productCount").intValue = 2;
                option.FindPropertyRelative("button").objectReferenceValue = button;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInfo(GameObject root, string title, string body, string extra)
        {
            root.transform.Find("HoverDetails/Information/Title").GetComponent<TextMeshProUGUI>().text = title;
            root.transform.Find("HoverDetails/Information/Description").GetComponent<TextMeshProUGUI>().text = body;
            root.transform.Find("HoverDetails/Properties/Description").GetComponent<TextMeshProUGUI>().text = extra;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect != null) return rect;
            rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer; rect.SetParent(parent, false); return rect;
        }
        private static RectTransform Box(string name, Transform parent, Color color, Vector2 min, Vector2 max, bool raycast = false)
        {
            var rect = Rect(name, parent); Stretch(rect, min, max);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast; return rect;
        }
        private static UpgradeIcon Icon(string name, Transform parent, UpgradeKind kind, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent); Stretch(rect, min, max);
            var icon = rect.GetComponent<UpgradeIcon>() ?? rect.gameObject.AddComponent<UpgradeIcon>();
            var data = new SerializedObject(icon); data.FindProperty("kind").enumValueIndex = (int)kind; data.ApplyModifiedPropertiesWithoutUndo();
            icon.raycastTarget = false; return icon;
        }
        private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color)
        {
            var rect = Rect(name, parent); Stretch(rect, Vector2.zero, Vector2.one);
            var label = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset; label.text = value; label.color = color;
            label.fontSize = size; label.enableAutoSizing = true; label.fontSizeMin = size * 0.7f; label.fontSizeMax = size;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Refs(Object target, params (string name, Object value)[] values)
        {
            var data = new SerializedObject(target);
            foreach (var value in values) data.FindProperty(value.name).objectReferenceValue = value.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
