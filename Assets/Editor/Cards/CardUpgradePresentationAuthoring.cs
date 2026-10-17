using System;
using CivilGameJam.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardUpgradePresentationAuthoring
    {
        [MenuItem("Tools/Card Game/Save Worker Hover And House Upgrade Presentation")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save card presentation in Edit Mode.");
            foreach (var name in new[] { "ResearchCenterCard", "SchoolCard", "FactoryCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var slot in root.GetComponentsInChildren<WorkerSlot>(true)) ConfigureWorker(slot);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            var game = Object.FindFirstObjectByType<CardGameController>();
            CardHouseUpgradeAuthoring.Configure(game, GameObject.Find("World3D"));
            foreach (var slot in game.GetComponentsInChildren<WorkerSlot>(true)) ConfigureWorker(slot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved worker hover, house upgrade camera presentation and rail pistons before Play Mode.");
        }

        private static void ConfigureWorker(WorkerSlot slot)
        {
            var data = new SerializedObject(slot);
            data.FindProperty("hoverLift").floatValue = 24f;
            data.FindProperty("hoverSeconds").floatValue = 0.16f;
            data.ApplyModifiedPropertiesWithoutUndo();
            var hitArea = slot.GetComponent<UnityEngine.UI.Image>();
            var padding = hitArea.raycastPadding;
            padding.w = -24f;
            hitArea.raycastPadding = padding;
            EditorUtility.SetDirty(hitArea);
            if (PrefabUtility.IsPartOfPrefabInstance(slot)) PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
            if (PrefabUtility.IsPartOfPrefabInstance(hitArea)) PrefabUtility.RecordPrefabInstancePropertyModifications(hitArea);
        }

        internal static HouseUpgradePresentation ConfigureCity(CardGameController game, GameObject world)
        {
            var presentation = world.GetComponent<HouseUpgradePresentation>();
            if (presentation == null) presentation = world.AddComponent<HouseUpgradePresentation>();
            var data = new SerializedObject(presentation);
            data.FindProperty("game").objectReferenceValue = game;
            data.FindProperty("dock").objectReferenceValue = game.GetComponent<CardDock>();
            data.FindProperty("navigation").objectReferenceValue = world.GetComponentInChildren<CityCameraController>(true);
            data.FindProperty("zoomFactor").floatValue = 1.12f;
            data.FindProperty("zoomSeconds").floatValue = 0.4f;
            data.FindProperty("holdSeconds").floatValue = 0.55f;
            data.FindProperty("returnSeconds").floatValue = 0.35f;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var rail in world.GetComponentsInChildren<BuildingRailUpgrade>(true))
            {
                var settings = new SerializedObject(rail);
                settings.FindProperty("transitionDuration").floatValue = 1.05f;
                settings.FindProperty("peakLiftMultiplier").floatValue = 2f;
                settings.FindProperty("removalDuration").floatValue = 0.35f;
                settings.FindProperty("dustMultiplier").floatValue = 1.5f;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            return presentation;
        }
    }
}
