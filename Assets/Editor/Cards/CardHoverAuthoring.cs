using CivilGameJam.Cards;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards.Editor
{
    public static class CardHoverAuthoring
    {
        private static readonly string[] CardPrefabNames = { "ResearchCenterCard", "FactoryCard", "SchoolCard", "HouseCard",
            "PeopleCard", "EducatedPeopleCard", "RaylıSistemBlueprint", "DalgaKıranBlueprint", "YangınBlueprint",
            "RaylıSistemCard", "DalgaKıranCard", "YangınCard" };

        public static void ShrinkPeoplePanelsForBatch()
        {
            foreach (string name in new[] { "PeopleCard", "EducatedPeopleCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyDetailsScale(prefab);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true))
            {
                if (!card.IsPerson) continue;
                var details = card.GetComponent<CardHover>().Details;
                ApplyDetailsScale(card.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(details);
            }
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved people info panels at 0.52 display scale, adjusted for card size.");
        }

        public static void TuneForBatch()
        {
            foreach (string name in CardPrefabNames)
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyHoverTuning(prefab);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true))
            {
                ApplyHoverTuning(card.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(card.GetComponent<CardFeedback>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(card.GetComponent<CardHover>().Details);
            }
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved hover tuning: 1.325x cards, 0.24s smooth transition; info panels 0.52x people / 0.8x others.");
        }

        public static void BuildForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            AuthorSavedHoverUI();
            CardSceneAuthoring.VerifySavedAssets();
        }

        [MenuItem("Tools/Civil Game Jam/Create Missing Card Hover Panels")]
        public static void AuthorSavedHoverUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (string name in CardPrefabNames)
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    PreparePrefab(prefab);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            var game = Object.FindFirstObjectByType<CardGameController>();
            var choice = game.transform.Find("BlueprintChoice");
            foreach (var hover in choice.GetComponentsInChildren<CardHover>(true))
            {
                var details = hover.transform.Find("HoverDetails");
                if (details != null) Object.DestroyImmediate(details.gameObject);
                Object.DestroyImmediate(hover);
            }
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        internal static void PreparePrefab(GameObject root)
        {
            var card = root.GetComponent<DraggableCard>();
            var feedback = root.GetComponent<CardFeedback>();
            var hover = root.GetComponent<CardHover>() ?? root.AddComponent<CardHover>();
            var details = root.transform.Find("HoverDetails") as RectTransform;
            if (details == null)
            {
                details = Rect("HoverDetails", root.transform);
                details.anchorMin = details.anchorMax = new Vector2(1f, 0.5f);
                details.pivot = new Vector2(0f, 0.5f);
                details.anchoredPosition = new Vector2(14f, 0f);
                details.sizeDelta = new Vector2(200f, 234f);
                var group = details.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                GetCopy(card.Kind, out string title, out string body, out string extra);
                Panel(details, "Information", 0f, title, body, new Color(0.68f, 0.90f, 0.46f));
                Panel(details, "Properties", -122f, "Details", extra, new Color(1f, 0.79f, 0.32f));
                details.gameObject.SetActive(false);
            }
            var data = new SerializedObject(hover);
            data.FindProperty("card").objectReferenceValue = card;
            data.FindProperty("feedback").objectReferenceValue = feedback;
            data.FindProperty("details").objectReferenceValue = details;
            data.ApplyModifiedPropertiesWithoutUndo();
            ApplyHoverTuning(root);
        }

        private static void ApplyHoverTuning(GameObject root)
        {
            var feedbackData = new SerializedObject(root.GetComponent<CardFeedback>());
            feedbackData.FindProperty("hoverScale").floatValue = 1.21125f;
            feedbackData.FindProperty("hoverSeconds").floatValue = 0.24f;
            feedbackData.ApplyModifiedPropertiesWithoutUndo();
            ApplyDetailsScale(root);
        }

        private static void ApplyDetailsScale(GameObject root)
        {
            var card = root.GetComponent<DraggableCard>();
            float detailsScale = card.IsPerson ? 0.52f / card.Rect.localScale.x : 0.8f;
            root.transform.Find("HoverDetails").localScale = Vector3.one * detailsScale;
        }

        private static void Panel(RectTransform parent, string name, float y, string title, string body, Color accent)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 112f);
            rect.anchoredPosition = new Vector2(0f, y);
            var background = rect.gameObject.AddComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            background.color = new Color(0.025f, 0.033f, 0.04f, 0.98f);
            background.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(3f, -4f);
            Label(rect, "Title", title, 24f, accent, -9f, 29f, FontStyles.Bold);
            Label(rect, "Description", body, 18f, Color.white, -42f, 61f, FontStyles.Normal);
        }

        private static void Label(Transform parent, string name, string value, float size, Color color,
            float y, float height, FontStyles style)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-24f, height);
            rect.anchoredPosition = new Vector2(0f, y);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = value;
            label.color = color;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = size * 0.75f;
            label.fontSizeMax = size;
            label.raycastTarget = false;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void GetCopy(CardKind kind, out string title, out string body, out string extra)
        {
            extra = "Bu açıklamayı\nburadan düzenleyebilirsin.";
            switch (kind)
            {
                case CardKind.ResearchCenter:
                    title = "Research"; body = "Assign people to\nresearch blueprints.";
                    extra = "Up to 2 workers"; break;
                case CardKind.School:
                    title = "Education"; body = "Turns people into\neducated people.";
                    extra = "Up to 2 students"; break;
                case CardKind.Factory:
                    title = "Fabrika"; body = "Çalışan kartlarını\nbu binaya bırakabilirsin.";
                    extra = "En fazla 2 çalışan\nİnsanlar iş sonunda korunur."; break;
                case CardKind.House:
                    title = "Ev"; body = "Kartı sürükleyerek\nboş bir hücreye taşı."; break;
                case CardKind.Person:
                    title = "Person"; body = "Assign to a school, factory\nor research center.";
                    extra = "Can be educated at school."; break;
                case CardKind.EducatedPerson:
                    title = "Educated Person"; body = "Assign to a factory\nor research center.";
                    extra = "Completes research faster."; break;
                default:
                    title = "Blueprint"; body = "Yeni bir yapının\nplan kartı."; break;
            }
        }
    }
}
