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
    public static class CardAvailabilityAuthoring
    {
        [MenuItem("Tools/Card Game/Save Card Availability And Insert Card Guide")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save the card guides in Edit Mode.");
            var unavailable = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/Unavailable UI.png");
            if (unavailable == null) throw new InvalidOperationException("Unavailable UI sprite is missing.");
            foreach (var path in new[] { "Assets/Prefabs/PeopleCard.prefab", "Assets/Prefabs/EducatedPeopleCard.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var art = root.transform.Find("Artwork").GetComponent<Image>();
                    var badge = Picture("UnavailableUI", art.transform, unavailable);
                    float width = Mathf.Min(art.rectTransform.rect.width,
                        art.rectTransform.rect.height * art.sprite.rect.width / art.sprite.rect.height);
                    badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = Vector2.one * .5f;
                    badge.rectTransform.anchoredPosition = Vector2.zero;
                    badge.rectTransform.sizeDelta = Vector2.one * (width * .84f);
                    var group = Ensure<CanvasGroup>(badge.gameObject);
                    group.ignoreParentGroups = true;
                    group.alpha = 1f; group.interactable = false; group.blocksRaycasts = false;
                    var shadow = Ensure<Shadow>(badge.gameObject);
                    shadow.effectColor = new Color(0f, 0f, 0f, .4f);
                    shadow.effectDistance = new Vector2(3f, -3f);
                    badge.gameObject.SetActive(false);
                    var data = new SerializedObject(root.GetComponent<DraggableCard>());
                    data.FindProperty("unavailableOverlay").objectReferenceValue = badge;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            const string factoryPath = "Assets/Prefabs/FactoryCard.prefab";
            var factory = PrefabUtility.LoadPrefabContents(factoryPath);
            try
            {
                CardFactoryFolderAuthoring.ConfigureWorkerHint(factory.transform.Find("FolderArt"), factory.GetComponent<FactoryFolderPresentation>());
                PrefabUtility.SaveAsPrefabAsset(factory, factoryPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(factory); }

            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardGameController>(true)).Single();
            var dataGame = new SerializedObject(game);
            var options = dataGame.FindProperty("blueprints");
            for (int i = 0; i < options.arraySize; i++)
            {
                var entry = options.GetArrayElementAtIndex(i);
                var button = (Button)entry.FindPropertyRelative("button").objectReferenceValue;
                var art = button.GetComponentInChildren<UpgradeIcon>(true);
                var overlay = Picture("UnavailableOverlay", art.transform, art.Artwork);
                overlay.rectTransform.anchorMin = Vector2.zero;
                overlay.rectTransform.anchorMax = Vector2.one;
                overlay.rectTransform.sizeDelta = Vector2.zero;
                overlay.color = new Color(0f, 0f, 0f, .94f);
                overlay.gameObject.SetActive(false);
                entry.FindPropertyRelative("unavailableOverlay").objectReferenceValue = overlay;
            }
            dataGame.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save card availability guides.");
            AssetDatabase.SaveAssets();
            Debug.Log("Saved three masked blueprint overlays, both unavailable people badges and the Insert Card Ui factory guide. Tutorial buttons were not edited.");
        }

        private static Image Picture(string name, Transform parent, Sprite sprite)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
                child.SetParent(parent, false);
            }
            child.gameObject.layer = parent.gameObject.layer;
            var image = child.GetComponent<Image>();
            image.sprite = sprite; image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false;
            image.rectTransform.pivot = Vector2.one * .5f;
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.localScale = Vector3.one;
            image.rectTransform.localRotation = Quaternion.identity;
            child.SetAsLastSibling();
            return image;
        }

        private static T Ensure<T>(GameObject owner) where T : Component
        {
            var component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }
    }
}
