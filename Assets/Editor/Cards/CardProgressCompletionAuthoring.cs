using System;
using System.Linq;
using CivilGameJam.Disasters;
using CivilGameJam.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardProgressCompletionAuthoring
    {
        [MenuItem("Tools/Card Game/Save Progress Completion Feedback")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save progress feedback in Edit Mode.");
            foreach (string name in new[] { "ResearchCenterCard", "SchoolCard", "FactoryCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { ConfigureCards(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in new[] { "Assets/2DCardScene.unity", "Assets/Scenes/BuildingShowcase.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var root in scene.GetRootGameObjects())
                {
                    ConfigureCards(root);
                    foreach (var owner in root.GetComponentsInChildren<MonoBehaviour>(true)
                        .Where(item => item is EarthquakeTestController || item is FireTestController || item is TsunamiTestController))
                    {
                        var data = new SerializedObject(owner);
                        var fill = data.FindProperty("progressFill").objectReferenceValue as Image;
                        if (fill == null) throw new InvalidOperationException(owner.name + " has no saved progress fill.");
                        var effect = Configure((RectTransform)fill.transform.parent, fill);
                        Assign(owner, effect);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            Debug.Log("Saved editable progress flashes, glow outlines and fading groups for all card and disaster bars.");
        }

        private static void ConfigureCards(GameObject root)
        {
            foreach (var station in root.GetComponentsInChildren<BuildingStation>(true))
            {
                var data = new SerializedObject(station);
                var track = data.FindProperty("progressRoot").objectReferenceValue as RectTransform;
                var fill = data.FindProperty("progressFill").objectReferenceValue as Image;
                if (track == null || fill == null) continue;
                var effect = Configure(track, fill);
                Assign(station, effect);
                var factory = station.GetComponent<FactoryProduction>();
                if (factory != null) Assign(factory, effect);
            }
        }

        private static ProgressBarFeedback Configure(RectTransform track, Image fill)
        {
            var effect = track.GetComponent<ProgressBarFeedback>();
            if (effect == null) effect = track.gameObject.AddComponent<ProgressBarFeedback>();
            var visibility = track.GetComponent<CanvasGroup>();
            visibility.interactable = false; visibility.blocksRaycasts = false;
            var glow = fill.GetComponent<Outline>();
            if (glow == null) glow = fill.gameObject.AddComponent<Outline>();
            var color = Color.Lerp(fill.color, Color.white, 0.6f); color.a = 0.7f;
            glow.effectColor = color; glow.effectDistance = new Vector2(1.5f, -1.5f);
            glow.useGraphicAlpha = true; glow.enabled = false;
            var data = new SerializedObject(effect);
            data.FindProperty("fill").objectReferenceValue = fill;
            data.FindProperty("visibility").objectReferenceValue = visibility;
            data.FindProperty("glow").objectReferenceValue = glow;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(effect)) PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            return effect;
        }

        private static void Assign(MonoBehaviour owner, ProgressBarFeedback effect)
        {
            var data = new SerializedObject(owner);
            data.FindProperty("progressFeedback").objectReferenceValue = effect;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }
    }
}
