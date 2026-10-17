using System;
using System.Linq;
using CivilGameJam.Disasters;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardSequenceResultsAuthoring
    {
        [MenuItem("Tools/Card Game/Save Sequence Results Panel")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author sequence results in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var sequence = Object.FindFirstObjectByType<MonsterSequenceController>();
            var game = Object.FindFirstObjectByType<CardGameController>();
            var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
            if (font == null) throw new InvalidOperationException("The card font must exist before authoring results.");
            var old = canvas.transform.Find("Sequence_Results");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var overlay = Rect("Sequence_Results", canvas.transform);
            Stretch(overlay, 0f);
            Fill(overlay, new Color(.025f, .055f, .065f, .78f), true);
            var layer = overlay.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true; layer.sortingOrder = 2000;
            overlay.gameObject.AddComponent<GraphicRaycaster>();

            var shadow = Rect("Shadow", overlay);
            Center(shadow, new Vector2(640f, 456f), new Vector2(8f, -8f));
            Fill(shadow, new Color(0f, 0f, 0f, .32f));
            var card = Rect("Card", overlay);
            Center(card, new Vector2(640f, 456f), Vector2.zero);
            var designScale = canvas.GetComponent<CanvasScaler>().referenceResolution.y / 900f;
            shadow.localScale = card.localScale = Vector3.one * designScale;
            Fill(card, new Color(.23f, .38f, .40f, 1f));
            var content = Rect("Content", card); Stretch(content, 3f);
            Fill(content, new Color(.09f, .15f, .17f, 1f));
            var accent = Rect("Accent", content);
            Top(accent, 0f, 0f, 0f, 5f); Fill(accent, new Color(.33f, .81f, .78f, 1f));
            var heading = Label("Title", content, "SEQUENCE COMPLETE", font, 34f, new Color(.95f, .94f, .88f));
            Top(heading.rectTransform, 26f, 28f, 26f, 48f);
            var ap = Label("AP_Label", content, "AP EARNED", font, 24f, new Color(.65f, .78f, .77f));
            Top(ap.rectTransform, 28f, 108f, 28f, 38f);

            var statistic = Rect("House_Statistic", content);
            Top(statistic, 32f, 170f, 32f, 126f);
            Fill(statistic, new Color(.055f, .105f, .12f, 1f));
            var caption = Label("Caption", statistic, "PROTECTED HOUSES / TOTAL", font, 21f, new Color(.72f, .82f, .80f));
            Top(caption.rectTransform, 14f, 14f, 14f, 32f);
            var count = Label("Count", statistic, "0 / 4", font, 54f, new Color(1f, .61f, .31f));
            Top(count.rectTransform, 14f, 50f, 14f, 66f);

            var retryRect = Rect("Retry", content);
            retryRect.anchorMin = new Vector2(0f, 0f); retryRect.anchorMax = new Vector2(1f, 0f);
            retryRect.pivot = new Vector2(.5f, 0f); retryRect.sizeDelta = new Vector2(-64f, 70f);
            retryRect.anchoredPosition = new Vector2(0f, 32f);
            var image = Fill(retryRect, new Color(1f, .61f, .31f), true);
            var retry = retryRect.gameObject.AddComponent<Button>(); retry.targetGraphic = image;
            retry.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = retry.colors; colors.highlightedColor = new Color(1f, .90f, .75f); colors.pressedColor = new Color(.78f, .72f, .65f);
            retry.colors = colors;
            var retryLabel = Label("Label", retryRect, "CONTINUE", font, 30f, new Color(.08f, .13f, .15f));
            Stretch(retryLabel.rectTransform, 10f);

            var results = sequence.GetComponent<MonsterSequenceResults>() ?? sequence.gameObject.AddComponent<MonsterSequenceResults>();
            var data = new SerializedObject(results);
            data.FindProperty("sequence").objectReferenceValue = sequence;
            data.FindProperty("overlay").objectReferenceValue = overlay.gameObject;
            data.FindProperty("destroyedCountLabel").objectReferenceValue = count;
            data.FindProperty("earnedPointsLabel").objectReferenceValue = ap;
            data.FindProperty("retryButton").objectReferenceValue = retry;
            var links = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None).OrderBy(link => link.Building.name).ToArray();
            if (links.Length != 4) throw new InvalidOperationException("Expected the four card house targets.");
            var houses = data.FindProperty("houses"); houses.arraySize = links.Length;
            for (var i = 0; i < links.Length; i++) houses.GetArrayElementAtIndex(i).objectReferenceValue = links[i].Building.GetComponent<BuildingModelGeometry>();
            data.ApplyModifiedPropertiesWithoutUndo();
            data = new SerializedObject(sequence); data.FindProperty("results").objectReferenceValue = results; data.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(retry.onClick, results.Retry);
            overlay.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save sequence results.");
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the sequence result panel, four unique house targets and retry button in 2DCardScene.");
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer; return rect;
        }

        private static Image Fill(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color)
        {
            var rect = Rect(name, parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = size; label.text = text; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true; label.fontSizeMin = 16f; label.fontSizeMax = size;
            return label;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }

        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }

        private static void Top(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(-left - right, height); rect.anchoredPosition = new Vector2((left - right) * .5f, -top);
        }
    }
}
