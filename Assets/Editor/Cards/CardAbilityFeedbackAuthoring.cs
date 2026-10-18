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
    /// <summary>Adds editable AP feedback to the existing HUD and worker-cost badges.</summary>
    public static class CardAbilityFeedbackAuthoring
    {
        [MenuItem("Tools/Card Game/Save AP Feedback")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save AP feedback in Edit Mode.");
            foreach (string name in new[] { "ResearchCenterCard", "SchoolCard", "FactoryCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var station = root.GetComponent<BuildingStation>();
                    var data = new SerializedObject(station);
                    foreach (string kind in new[] { "normal", "educated" })
                    {
                        var label = (TMP_Text)data.FindProperty(kind + "CostLabel").objectReferenceValue;
                        if (label == null) throw new InvalidOperationException("Missing cost badge: " + name);
                        var badge = (RectTransform)label.transform.parent;
                        var feedback = badge.GetComponent<AbilityPointCostFeedback>() ?? badge.gameObject.AddComponent<AbilityPointCostFeedback>();
                        var badgeData = new SerializedObject(feedback);
                        badgeData.FindProperty("badge").objectReferenceValue = badge;
                        badgeData.FindProperty("label").objectReferenceValue = label;
                        badgeData.ApplyModifiedPropertiesWithoutUndo();
                        data.FindProperty(kind + "CostFeedback").objectReferenceValue = feedback;
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var bank = Object.FindFirstObjectByType<AbilityPoints>();
            var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
            var balance = (TMP_Text)new SerializedObject(bank).FindProperty("balanceLabel").objectReferenceValue;
            var counter = (RectTransform)balance.transform.parent;
            var ready = (Button)new SerializedObject(round).FindProperty("readyButton").objectReferenceValue;
            var hud = counter.GetComponent<AbilityPointFeedback>() ?? counter.gameObject.AddComponent<AbilityPointFeedback>();
            var hudData = new SerializedObject(hud);
            hudData.FindProperty("bank").objectReferenceValue = bank;
            hudData.FindProperty("counter").objectReferenceValue = counter;
            hudData.FindProperty("balanceLabel").objectReferenceValue = balance;
            hudData.FindProperty("spendColor").colorValue = new Color(1f, .25f, .2f, 1f);
            hudData.FindProperty("spendSeconds").floatValue = 1f;
            hudData.FindProperty("warningSeconds").floatValue = 1f;
            hudData.FindProperty("popupSeconds").floatValue = 1.3f;
            hudData.FindProperty("counterGlow").objectReferenceValue = Glow("CounterGlow", counter, 2f, true);
            hudData.FindProperty("readyGlow").objectReferenceValue = Glow("ReadyAttentionGlow", (RectTransform)ready.transform, 6f);
            var labels = hudData.FindProperty("spendLabels"); labels.arraySize = 4;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
            for (int i = 0; i < labels.arraySize; i++)
            {
                var rect = EnsureRect("SpendPopup" + (i + 1).ToString("00"), counter);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
                rect.pivot = new Vector2(.5f, 1f);
                rect.sizeDelta = new Vector2(200f, 56f);
                rect.anchoredPosition = new Vector2(0f, -8f - i * 72f);
                var label = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
                label.font = font;
                label.fontSize = label.fontSizeMax = 32f; label.fontSizeMin = 14.4f;
                label.enableAutoSizing = true;
                label.alignment = TextAlignmentOptions.Center;
                label.text = "-4 AP";
                label.color = new Color(1f, .25f, .2f, 1f);
                label.raycastTarget = false; label.maskable = false;
                rect.gameObject.SetActive(false);
                labels.GetArrayElementAtIndex(i).objectReferenceValue = label;
            }
            hudData.ApplyModifiedPropertiesWithoutUndo();
            var roundData = new SerializedObject(round);
            roundData.FindProperty("abilityFeedback").objectReferenceValue = hud;
            roundData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved AP spend popups, counter and worker-cost warnings, and READY attention border without changing prices or layout.");
        }

        private static RectTransform EnsureRect(string name, Transform parent)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect != null) return rect;
            rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        private static Image Glow(string name, RectTransform parent, float padding, bool fitToArtwork = false)
        {
            var rect = EnsureRect(name, parent);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            var artwork = parent.GetComponent<Image>();
            if (fitToArtwork && artwork != null && artwork.sprite != null && artwork.preserveAspect &&
                parent.rect.width > 0f && parent.rect.height > 0f)
            {
                var size = parent.rect.size;
                var spriteSize = artwork.sprite.rect.size;
                var drawn = spriteSize * Mathf.Min(size.x / spriteSize.x, size.y / spriteSize.y);
                var inset = Vector2.Scale(size - drawn, parent.pivot);
                rect.anchorMin = new Vector2(inset.x / size.x, inset.y / size.y);
                rect.anchorMax = rect.anchorMin + new Vector2(drawn.x / size.x, drawn.y / size.y);
            }
            rect.offsetMin = Vector2.one * -padding; rect.offsetMax = Vector2.one * padding;
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced; image.fillCenter = false;
            image.pixelsPerUnitMultiplier = fitToArtwork ? 2f : 1f;
            image.color = Color.clear; image.raycastTarget = false; image.maskable = false;
            rect.gameObject.SetActive(false);
            return image;
        }
    }
}
