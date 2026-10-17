using System;
using System.Linq;
using CivilGameJam.Disasters;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CivilGameJam.Cards.Editor
{
    public static class CardTurnPersonnelAuthoring
    {
        [MenuItem("Tools/Card Game/Save Turn Personnel And Remove Work Captions")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save turn settings in Edit Mode.");
            foreach (var name in new[] { "ResearchCenterCard", "FactoryCard", "SchoolCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { RemoveCaption(root.transform); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var game = components.OfType<CardGameController>().Single();
            var gameData = new SerializedObject(game);
            gameData.FindProperty("normalPersonPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PeopleCard.prefab");
            gameData.FindProperty("normalPeoplePerTurn").intValue = 4;
            gameData.ApplyModifiedPropertiesWithoutUndo();
            foreach (var station in components.OfType<BuildingStation>()) RemoveCaption(station.transform);
            var round = components.OfType<EarthquakeRoundController>().Single();
            var data = new SerializedObject(round);
            data.FindProperty("nextTurnAfterCollapseSeconds").floatValue = 2.5f;
            var houses = data.FindProperty("houses");
            for (int i = 0; i < houses.arraySize; i++)
            {
                var house = houses.GetArrayElementAtIndex(i);
                var link = (HouseCityUpgradeLink)house.FindPropertyRelative("link").objectReferenceValue;
                house.FindPropertyRelative("tsunamiDamage").objectReferenceValue = link.Building.GetComponent<ApartmentTsunamiCollapse>();
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var heading = (TMP_Text)data.FindProperty("roundLabel").objectReferenceValue;
            heading.text = "ROUND 1";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved four normal people per turn, collapse transition and empty, inactive workplace captions. Existing city and card layouts are retained.");
        }

        private static void RemoveCaption(Transform root)
        {
            var caption = root.Find("WorkStatus");
            if (caption == null) return;
            caption.GetComponent<TMP_Text>().text = string.Empty;
            caption.gameObject.SetActive(false);
            EditorUtility.SetDirty(caption.gameObject);
            if (PrefabUtility.IsPartOfPrefabInstance(caption))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(caption.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(caption.GetComponent<TMP_Text>());
            }
        }
    }
}
