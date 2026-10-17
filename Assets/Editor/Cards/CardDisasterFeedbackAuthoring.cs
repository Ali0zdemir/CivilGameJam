using System;
using System.Linq;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CivilGameJam.Cards.Editor
{
    public static class CardDisasterFeedbackAuthoring
    {
        [MenuItem("Tools/Card Game/Save Disaster Feedback")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save disaster feedback in Edit Mode.");
            var dust = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/Construction/ConstructionDust.mat");
            if (dust == null) throw new InvalidOperationException("Missing construction dust material.");
            foreach (var name in new[] { "House", "Apartment_02", "Apartment_03", "Apartment_04" })
            {
                var path = "Assets/Art/Buildings/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var feedback = root.GetComponent<BuildingCollapseFeedback>() ?? root.AddComponent<BuildingCollapseFeedback>();
                    var settings = new SerializedObject(feedback);
                    settings.FindProperty("geometry").objectReferenceValue = root.GetComponent<BuildingModelGeometry>();
                    settings.FindProperty("constructionDustMaterial").objectReferenceValue = dust;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var collapse in new Component[] { root.GetComponent<ApartmentEarthquakeAnimation>(), root.GetComponent<ApartmentTsunamiCollapse>() })
                    {
                        settings = new SerializedObject(collapse);
                        settings.FindProperty("collapseFeedback").objectReferenceValue = feedback;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var roots = scene.GetRootGameObjects();
            var walk = roots.SelectMany(root => root.GetComponentsInChildren<SeaMonsterWalk>(true)).Single();
            var tsunami = roots.SelectMany(root => root.GetComponentsInChildren<TsunamiTestController>(true)).Single();
            var emergence = walk.GetComponent<SeaMonsterEmergence>();
            var sea = (GameObject)new SerializedObject(emergence).FindProperty("seaSurface").objectReferenceValue;
            var footsteps = walk.GetComponent<MonsterFootstepFeedback>() ?? walk.gameObject.AddComponent<MonsterFootstepFeedback>();
            var data = new SerializedObject(footsteps);
            data.FindProperty("tsunami").objectReferenceValue = tsunami;
            data.FindProperty("seaSurface").objectReferenceValue = sea != null ? sea.transform : null;
            data.FindProperty("constructionDustMaterial").objectReferenceValue = dust;
            data.ApplyModifiedPropertiesWithoutUndo();
            data = new SerializedObject(walk);
            data.FindProperty("footstepFeedback").objectReferenceValue = footsteps;
            data.ApplyModifiedPropertiesWithoutUndo();

            var camera = roots.SelectMany(root => root.GetComponentsInChildren<CityCameraController>(true)).Single();
            data = new SerializedObject(camera);
            data.FindProperty("farthestZoom").floatValue = 4.9f;
            ((Camera)data.FindProperty("controlledCamera").objectReferenceValue).orthographicSize = 3.6f;
            foreach (var field in new[] { "leftLimit", "rightLimit", "topLimit", "bottomLimit" })
            {
                var marker = (Transform)data.FindProperty(field).objectReferenceValue;
                var position = marker.localPosition;
                if (field == "leftLimit") position.x = -2.4f;
                if (field == "rightLimit") position.x = 2.4f;
                if (field == "topLimit") position.y = 1.5f;
                if (field == "bottomLimit") position.y = -1.5f;
                marker.localPosition = position;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save 2DCardScene.");
            AssetDatabase.SaveAssets();
            Debug.Log("Saved collapse feedback, monster footfalls and tighter manual camera limits in 2DCardScene.");
        }
    }
}
