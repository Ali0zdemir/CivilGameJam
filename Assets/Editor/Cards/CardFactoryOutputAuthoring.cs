using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardFactoryOutputAuthoring
    {
        [MenuItem("Tools/Card Game/Save Factory Output Area and New Item Outlines")]
        public static void SaveForBatch()
        {
            foreach (string name in new[] { "RaylıSistemCard", "DalgaKıranCard", "YangınCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var icon = root.transform.Find("CardSymbol").GetComponent<UpgradeIcon>();
                    var tintedOutline = icon.GetComponent<Outline>();
                    if (tintedOutline != null) Object.DestroyImmediate(tintedOutline);
                    var frame = root.transform.Find("NewItemOutline") as RectTransform;
                    if (frame == null)
                    {
                        frame = new GameObject("NewItemOutline", typeof(RectTransform)).GetComponent<RectTransform>();
                        frame.gameObject.layer = root.layer;
                        frame.SetParent(root.transform, false);
                        frame.SetAsFirstSibling();
                        frame.anchorMin = Vector2.zero;
                        frame.anchorMax = Vector2.one;
                        frame.offsetMin = Vector2.one * -8f;
                        frame.offsetMax = Vector2.one * 8f;
                    }
                    var outline = frame.GetComponent<Image>() ?? frame.gameObject.AddComponent<Image>();
                    outline.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    outline.type = Image.Type.Sliced;
                    outline.fillCenter = false;
                    outline.pixelsPerUnitMultiplier = 0.75f;
                    outline.color = new Color(1f, 0.83f, 0.28f, 1f);
                    outline.raycastTarget = false;
                    outline.enabled = false;
                    var data = new SerializedObject(root.GetComponent<DraggableCard>());
                    data.FindProperty("newItemOutline").objectReferenceValue = outline;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            Canvas.ForceUpdateCanvases();
            var layer = game.CardLayer;
            var area = layer.Find("FactoryOutputArea") as RectTransform;
            if (area == null)
            {
                area = new GameObject("FactoryOutputArea", typeof(RectTransform)).GetComponent<RectTransform>();
                area.gameObject.layer = layer.gameObject.layer;
                area.SetParent(layer, false);
                var corners = new Vector3[4];
                game.Grid.rectTransform.GetWorldCorners(corners);
                float gridRight = layer.InverseTransformPoint(corners[3]).x;
                area.anchorMin = new Vector2(0f, 1f);
                area.anchorMax = Vector2.one;
                area.pivot = Vector2.up;
                area.offsetMin = new Vector2(gridRight - layer.rect.xMin + 12f, -64f - game.Grid.CellSize.y);
                area.offsetMax = new Vector2(-12f, -64f);
            }
            var settings = new SerializedObject(game);
            settings.FindProperty("factoryOutputArea").objectReferenceValue = area;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            if (area.rect.width < 100f || area.rect.height < 100f)
                throw new InvalidOperationException("The saved FactoryOutputArea must fit a manufactured item.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved factory output area " + area.rect.size + " and three product outlines.");
        }
    }
}
