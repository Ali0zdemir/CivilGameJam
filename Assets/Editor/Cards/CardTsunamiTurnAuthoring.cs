using System;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    /// <summary>Saves destination outlines and separate tsunami/fire economy settings without rebuilding the board.</summary>
    public static class CardTsunamiTurnAuthoring
    {
        [MenuItem("Tools/Card Game/Save Drag Targets And Tsunami Turn Settings")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save drag targets in Edit Mode.");
            foreach (string name in new[] { "HouseCard", "ResearchCenterCard", "FactoryCard", "SchoolCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var card = root.GetComponent<DraggableCard>();
                    var art = root.transform.Find(card.Kind == CardKind.House
                        ? "FolderArt/CivilBuildingArtwork" : "FolderArt/DışKenar").GetComponent<Graphic>();
                    var feedback = ConfigureFeedback(root, art);
                    var data = new SerializedObject(card);
                    data.FindProperty("dropTargetFeedback").objectReferenceValue = feedback;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    var station = root.GetComponent<BuildingStation>();
                    if (station != null)
                    {
                        var prices = new SerializedObject(station);
                        prices.FindProperty("tsunamiNormalAbilityPointCost").intValue = card.Kind == CardKind.School ? 2
                            : card.Kind == CardKind.Factory ? 12 : 9;
                        prices.FindProperty("tsunamiEducatedAbilityPointCost").intValue = card.Kind == CardKind.School ? 0
                            : card.Kind == CardKind.Factory ? 7 : 3;
                        prices.FindProperty("fireNormalAbilityPointCost").intValue = card.Kind == CardKind.School ? 4
                            : 12;
                        prices.FindProperty("fireEducatedAbilityPointCost").intValue = card.Kind == CardKind.School ? 0
                            : 4;
                        prices.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            var gameData = new SerializedObject(game);
            gameData.FindProperty("educationSeconds").floatValue = 6.25f;
            gameData.FindProperty("vaultDropTarget").objectReferenceValue = ConfigureFeedback(game.Vault.gameObject,
                VaultReturnFrame(game.Vault.Area), false);
            gameData.FindProperty("peopleDropTarget").objectReferenceValue = ConfigureFeedback(game.Sidebar.PeopleArea.gameObject,
                game.Sidebar.PeopleArea.GetComponent<Graphic>());
            gameData.ApplyModifiedPropertiesWithoutUndo();
            var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
            var roundData = new SerializedObject(round);
            roundData.FindProperty("tsunamiPointsPerProtectedHouse").intValue = 6;
            roundData.FindProperty("firePointsPerProtectedHouse").intValue = 10;
            roundData.FindProperty("afterEarthquakeRoundPoints").intValue = 6;
            roundData.FindProperty("bossDepartureSeconds").floatValue = 10f;
            roundData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved native artwork drop outlines, School 6.25s, tsunami educated research 3 AP / factory 5 AP, tsunami normal research 6 AP / factory 7 AP, fire school 4 AP, research 8/5 AP, factory 12/8 AP, fire survival 10 AP, post-earthquake turn bonus 6 AP and boss departure 10s.");
        }

        private static Image VaultReturnFrame(RectTransform area)
        {
            var frame = area.Find("DropTargetFrame") as RectTransform;
            if (frame == null)
            {
                frame = new GameObject("DropTargetFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
                frame.SetParent(area, false);
                frame.gameObject.layer = area.gameObject.layer;
            }
            frame.SetAsFirstSibling();
            frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
            var image = frame.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        internal static CardDropTargetFeedback ConfigureFeedback(GameObject owner, Graphic artwork, bool useArtworkAlpha = true)
        {
            if (artwork == null) throw new InvalidOperationException("Missing saved artwork for " + owner.name);
            var feedback = owner.GetComponent<CardDropTargetFeedback>() ?? owner.AddComponent<CardDropTargetFeedback>();
            var data = new SerializedObject(feedback);
            data.FindProperty("edgeWidth").floatValue = useArtworkAlpha ? 4.5f : 1.8f;
            data.FindProperty("glowWidth").floatValue = useArtworkAlpha ? 9f : 4f;
            data.FindProperty("fadeSeconds").floatValue = .09f;
            data.FindProperty("tint").colorValue = new Color(.5f, 1f, .78f, useArtworkAlpha ? 1f : .95f);
            foreach (string field in new[] { "glowOutline", "edgeOutline" })
            {
                var effect = data.FindProperty(field).objectReferenceValue as Outline;
                if (effect != null && effect.gameObject != artwork.gameObject)
                {
                    Object.DestroyImmediate(effect);
                    effect = null;
                }
                if (effect == null) effect = artwork.gameObject.AddComponent<Outline>();
                effect.useGraphicAlpha = useArtworkAlpha;
                float width = data.FindProperty(field == "edgeOutline" ? "edgeWidth" : "glowWidth").floatValue;
                effect.effectDistance = new Vector2(width, -width);
                effect.effectColor = new Color(.5f, 1f, .78f, 0f);
                effect.enabled = false;
                data.FindProperty(field).objectReferenceValue = effect;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feedback);
            return feedback;
        }
    }
}
