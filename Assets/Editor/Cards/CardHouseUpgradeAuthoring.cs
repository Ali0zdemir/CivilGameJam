using System;
using System.Linq;
using CivilGameJam.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardHouseUpgradeAuthoring
    {
        [MenuItem("Tools/Card Game/Save House Card To City Upgrade Links")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save house-to-city links in Edit Mode.");
            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            Configure(Object.FindFirstObjectByType<CardGameController>(), GameObject.Find("World3D"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved four house card links for rail, flood and fire upgrades in the city.");
        }

        internal static void Configure(CardGameController game, GameObject world)
        {
            var presentation = CardUpgradePresentationAuthoring.ConfigureCity(game, world);
            var houses = game.GetComponentsInChildren<HouseUpgrades>(true)
                .Where(house => house.GetComponent<DraggableCard>().InitialGridCell >= 0)
                .OrderBy(house => house.GetComponent<DraggableCard>().InitialGridCell).ToArray();
            if (houses.Length != 4) throw new InvalidOperationException("Expected the four authored house cards.");
            var rails = world.GetComponentsInChildren<BuildingRailUpgrade>(true);
            var columns = world.GetComponentsInChildren<HouseColumnUpgrade>(true);
            var sprinklers = world.GetComponentsInChildren<HouseSprinklerUpgrade>(true);
            for (int i = 0; i < houses.Length; i++)
            {
                var building = world.transform.Find("Buildings_3D/Buildings/House_" + (i + 1).ToString("00"));
                if (building == null) throw new InvalidOperationException("Missing city house " + (i + 1));
                var link = houses[i].GetComponent<HouseCityUpgradeLink>();
                if (link == null) link = houses[i].gameObject.AddComponent<HouseCityUpgradeLink>();
                var data = new SerializedObject(link);
                data.FindProperty("house").objectReferenceValue = houses[i];
                data.FindProperty("railSystem").objectReferenceValue = rails.Single(upgrade => upgrade.Building == building);
                data.FindProperty("floodProtection").objectReferenceValue = columns.Single(upgrade => upgrade.Building == building);
                data.FindProperty("fireProtection").objectReferenceValue = sprinklers.Single(upgrade => upgrade.Building == building);
                data.FindProperty("presentation").objectReferenceValue = presentation;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(link);
            }
        }
    }
}
