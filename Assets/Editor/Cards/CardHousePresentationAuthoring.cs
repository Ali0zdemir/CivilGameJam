using System;
using System.Linq;
using CivilGameJam.Upgrades;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardHousePresentationAuthoring
    {
        [MenuItem("Tools/Card Game/Match House Titles And Save Rail Dust")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save house presentation in Edit Mode.");
            EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            var reference = HouseTitle();
            const string path = "Assets/Prefabs/HouseCard.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MatchTitle(reference, prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            reference = HouseTitle();
            var game = Object.FindFirstObjectByType<CardGameController>();
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true))
                if (card.Kind == CardKind.House) MatchTitle(reference, card.gameObject);
            foreach (var rail in GameObject.Find("World3D").GetComponentsInChildren<BuildingRailUpgrade>(true))
            {
                var data = new SerializedObject(rail);
                data.FindProperty("dustMultiplier").floatValue = 2.25f;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rail);
                if (PrefabUtility.IsPartOfPrefabInstance(rail)) PrefabUtility.RecordPrefabInstancePropertyModifications(rail);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the authored HouseCard_01 title on every house and increased rail dust by 50%.");
        }

        private static TextMeshProUGUI HouseTitle() => Object.FindFirstObjectByType<CardGameController>()
            .GetComponentsInChildren<DraggableCard>(true)
            .Single(card => card.Kind == CardKind.House && card.name == "HouseCard_01")
            .transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();

        private static void MatchTitle(TextMeshProUGUI reference, GameObject house)
        {
            var title = house.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            CardWorkplaceTextAuthoring.CopyPresentation(reference, title);
            title.text = reference.text;
            EditorUtility.SetDirty(title);
            if (PrefabUtility.IsPartOfPrefabInstance(title)) PrefabUtility.RecordPrefabInstancePropertyModifications(title);
        }
    }
}
