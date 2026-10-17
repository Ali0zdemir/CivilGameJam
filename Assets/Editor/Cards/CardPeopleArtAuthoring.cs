using System;
using System.Linq;
using CivilGameJam.Cards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardPeopleArtAuthoring
    {
        private const float ArtworkScale = 0.55f;

        public static void ExtendPanelAndReducePeopleForBatch()
        {
            ApplyPrefabs();
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            Canvas.ForceUpdateCanvases();
            var cells = Enumerable.Range(0, CardGrid.CellCount).Select(game.Grid.GetCell).ToArray();
            var positions = cells.Select(cell => cell.position).ToArray();
            var sizes = cells.Select(cell => cell.rect.size).ToArray();
            var cards = game.GetComponentsInChildren<DraggableCard>().Where(card => card.IsBuilding).ToArray();
            var cardSizes = cards.Select(card => card.Rect.sizeDelta).ToArray();
            var cardScales = cards.Select(card => card.Rect.localScale).ToArray();
            var gridSlots = game.Grid.rectTransform;
            Vector3 gridPosition = gridSlots.position;
            float gridWidth = gridSlots.rect.width;
            // Fix the card slots in place while both background panels fill the display.
            gridSlots.anchorMin = new Vector2(0f, gridSlots.anchorMin.y);
            gridSlots.anchorMax = new Vector2(0f, gridSlots.anchorMax.y);
            gridSlots.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, gridWidth);
            gridSlots.position = gridPosition;
            var gridPanel = (RectTransform)game.transform.Find("GridPanel");
            gridPanel.anchorMin = new Vector2(0f, gridPanel.anchorMin.y);
            gridPanel.anchorMax = new Vector2(1f, gridPanel.anchorMax.y);
            var panel = (RectTransform)game.transform;
            panel.anchorMax = new Vector2(1f / panel.localScale.x, panel.anchorMax.y);
            panel.sizeDelta = new Vector2(-panel.anchoredPosition.x / panel.localScale.x, panel.sizeDelta.y);
            gridPanel.offsetMax = new Vector2(0f, gridPanel.offsetMax.y);
            // Product placement uses the actual slot area, independently of the wider background.
            var controller = new SerializedObject(game);
            controller.FindProperty("gridPanel").objectReferenceValue = gridSlots;
            controller.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            ApplyScene(false);
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < cells.Length; i++)
                if (Vector3.Distance(cells[i].position, positions[i]) > 0.01f || Vector2.Distance(cells[i].rect.size, sizes[i]) > 0.01f)
                    throw new InvalidOperationException("Extending the panel changed grid geometry: " + cells[i].name +
                        "; position " + positions[i].ToString("F3") + " -> " + cells[i].position.ToString("F3") +
                        "; size " + sizes[i].ToString("F3") + " -> " + cells[i].rect.size.ToString("F3") +
                        "; panel " + panel.rect.size.ToString("F3") + "; grid " + game.Grid.rectTransform.rect.size.ToString("F3"));
            for (int i = 0; i < cards.Length; i++)
                if (cards[i].Rect.sizeDelta != cardSizes[i] || cards[i].Rect.localScale != cardScales[i])
                    throw new InvalidOperationException("Extending the panel resized a building: " + cards[i].name);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved panel stretched to the right screen edge with unchanged grid geometry, and people artwork reduced to 55%.");
        }

        public static void ApplyForBatch()
        {
            ApplyPrefabs();
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            ApplyScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
        }

        [MenuItem("Tools/Civil Game Jam/Apply Saved Efe People Artwork")]
        public static void ApplyToActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply people artwork in Edit Mode.");
            ApplyPrefabs();
            ApplyScene();
            AssetDatabase.SaveAssets();
        }

        private static void ApplyPrefabs()
        {
            foreach (string name in new[] { "PeopleCard", "EducatedPeopleCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    string artName = name == "PeopleCard" ? "People Card Small" : "Educated People Card Small";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/" + artName + ".png");
                    if (sprite == null) throw new InvalidOperationException("Missing EfeArts people sprite: " + artName);
                    var artwork = prefab.transform.Find("Artwork") as RectTransform;
                    if (artwork == null)
                    {
                        artwork = new GameObject("Artwork", typeof(RectTransform), typeof(Image), typeof(Shadow))
                            .GetComponent<RectTransform>();
                        artwork.SetParent(prefab.transform, false);
                        artwork.gameObject.layer = prefab.layer;
                    }
                    artwork.SetAsFirstSibling();
                    artwork.anchorMin = artwork.anchorMax = artwork.pivot = new Vector2(0.5f, 0.5f);
                    artwork.anchoredPosition = Vector2.zero;
                    artwork.localScale = Vector3.one;
                    var image = artwork.GetComponent<Image>();
                    image.sprite = sprite;
                    image.color = Color.white;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    image.SetNativeSize();
                    var shadow = artwork.GetComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
                    shadow.effectDistance = new Vector2(4f, -5f);
                    var feedback = new SerializedObject(prefab.GetComponent<CardFeedback>());
                    feedback.FindProperty("shadow").objectReferenceValue = shadow;
                    feedback.ApplyModifiedPropertiesWithoutUndo();
                    var oldShadow = prefab.GetComponent<Shadow>();
                    if (oldShadow != null) Object.DestroyImmediate(oldShadow);
                    foreach (string childName in new[] { "Text (TMP)", "PersonType" })
                    {
                        var child = prefab.transform.Find(childName);
                        if (child != null) Object.DestroyImmediate(child.gameObject);
                    }
                    ApplyDimensions(prefab.GetComponent<DraggableCard>());
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
        }

        private static void ApplyScene(bool fitSlots = true)
        {
            var game = Object.FindFirstObjectByType<CardGameController>();
            if (game == null) throw new InvalidOperationException("Open 2DCardScene first.");
            foreach (var root in game.gameObject.scene.GetRootGameObjects())
                foreach (var card in root.GetComponentsInChildren<DraggableCard>(true))
                {
                    if (!card.IsPerson) continue;
                    ApplyDimensions(card);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card.Rect);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card.transform.Find("Artwork"));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card.GetComponent<Image>());
                    PrefabUtility.RecordPrefabInstancePropertyModifications(card.GetComponent<CardHover>().Details);
                }
            if (fitSlots) CardSidebarAuthoring.FitPeopleSlots(game);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            Debug.Log("Saved both EfeArts people cards with 55% artwork scale and matching pointer hit areas.");
        }

        private static void ApplyDimensions(DraggableCard card)
        {
            // The supplied 320x320 images contain a 240x300 card and transparent gutters.
            // Retain the source texture dimensions and scale the image and its visible hit area together.
            card.transform.Find("Artwork").localScale = Vector3.one * ArtworkScale;
            card.Rect.sizeDelta = new Vector2(240f, 300f) * ArtworkScale;
            card.Rect.localScale = Vector3.one;
            var hitArea = card.GetComponent<Image>();
            hitArea.sprite = null;
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            card.GetComponent<CardHover>().Details.localScale = Vector3.one * 0.52f;
        }
    }
}
