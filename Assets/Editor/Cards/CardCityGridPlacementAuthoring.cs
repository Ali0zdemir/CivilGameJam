using System;
using System.Linq;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardCityGridPlacementAuthoring
    {
        [MenuItem("Tools/Card Game/Save Card and City Grid Placement")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save city placement in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var markers = tsunami.GroundSurface.Find("Tile_Grid_4x2");
            if (markers == null) throw new InvalidOperationException("The map needs its eight saved tile markers.");
            var component = game.GetComponent<CardCityGridPlacement>() ?? game.gameObject.AddComponent<CardCityGridPlacement>();
            var data = new SerializedObject(component);
            data.FindProperty("game").objectReferenceValue = game;
            data.FindProperty("grid").objectReferenceValue = game.Grid;
            data.FindProperty("movementSeconds").floatValue = .55f;
            var anchors = new Transform[CardGrid.CellCount];
            data.FindProperty("cellAnchors").arraySize = anchors.Length;
            for (var i = 0; i < anchors.Length; i++)
            {
                anchors[i] = markers.Find("Tile_R" + (2 - i / CardGrid.Columns) + "_C" + (i % CardGrid.Columns + 1));
                if (anchors[i] == null) throw new InvalidOperationException("Missing tile for card cell " + i);
                data.FindProperty("cellAnchors").GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];
            }
            var cards = game.GetComponentsInChildren<DraggableCard>().Where(card => card.IsBuilding && card.InitialGridCell >= 0)
                .OrderBy(card => card.InitialGridCell).ToArray();
            var groups = tsunami.BuildingsRoot.Cast<Transform>().Where(group => group.gameObject.activeSelf).ToArray();
            var links = data.FindProperty("buildings"); links.arraySize = cards.Length;
            var bound = new System.Collections.Generic.HashSet<Transform>();
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                var house = card.GetComponent<HouseCityUpgradeLink>();
                var key = card.Kind == CardKind.ResearchCenter ? "research" : card.Kind.ToString().ToLowerInvariant();
                var building = house != null ? house.Building : groups.Single(group => group.name.Replace("_", "").ToLowerInvariant().Contains(key));
                if (building == null || !bound.Add(building)) throw new InvalidOperationException("Every card needs its own city building: " + card.name);
                var nearest = anchors.OrderBy(anchor => Vector2.SqrMagnitude(new Vector2(anchor.position.x - building.position.x, anchor.position.z - building.position.z))).First();
                var height = building.position.y - nearest.position.y;
                var entry = links.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("card").objectReferenceValue = card;
                entry.FindPropertyRelative("building").objectReferenceValue = building;
                entry.FindPropertyRelative("heightOffset").floatValue = height;
                building.position = anchors[card.InitialGridCell].position + Vector3.up * height;
                PrefabUtility.RecordPrefabInstancePropertyModifications(building);
                Debug.Log("Card city cell " + card.InitialGridCell + ": " + card.name + " -> " + building.name);
            }
            if (bound.Count != 7 || groups.Length != bound.Count) throw new InvalidOperationException("Expected the four homes and three workplaces.");
            data.FindProperty("tsunami").objectReferenceValue = tsunami;
            data.FindProperty("earthquake").objectReferenceValue = Object.FindFirstObjectByType<EarthquakeTestController>();
            data.FindProperty("fire").objectReferenceValue = Object.FindFirstObjectByType<FireTestController>();
            data.FindProperty("sequence").objectReferenceValue = Object.FindFirstObjectByType<MonsterSequenceController>();
            data.FindProperty("monsterWalk").objectReferenceValue = Object.FindFirstObjectByType<SeaMonsterWalk>();
            data.FindProperty("navigation").objectReferenceValue = Object.FindFirstObjectByType<CityCameraController>();
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the card/city cell links.");
            Debug.Log("Saved seven card-to-building identities, eight city anchors and disaster placement locks.");
        }
    }
}
