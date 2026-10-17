using System;
using System.Linq;
using CivilGameJam.Cards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CivilGameJam.Cards.Editor
{
    // Applies the scene's hand-edited examples once, then saves ordinary scene objects and prefabs.
    public static class CardLayoutAuthoring
    {
        public static void ApplySavedExamplesForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            ApplyEditedExamples();
            CardSceneAuthoring.VerifySavedAssets();
        }

        public static void EnlargePeopleForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = UnityEngine.Object.FindFirstObjectByType<CardGameController>();
            Canvas.ForceUpdateCanvases();
            var people = game.GetComponentsInChildren<DraggableCard>().Where(c => c.IsPerson)
                .OrderBy(c => c.name).ToArray();
            var table = (RectTransform)game.transform;
            var gridPanel = (RectTransform)game.transform.Find("GridPanel");
            float currentTrayWidth = table.InverseTransformPoint(gridPanel.position).x +
                gridPanel.rect.xMin - table.rect.xMin;
            const float trayWidth = 504f;
            float extraWidth = Mathf.Ceil(Mathf.Max(0f, trayWidth - currentTrayWidth));
            Vector2 gridOffsetMin = gridPanel.offsetMin;
            Vector2 gridOffsetMax = gridPanel.offsetMax;
            table.sizeDelta += new Vector2(extraWidth, 0f);
            gridPanel.offsetMin = gridOffsetMin + new Vector2(extraWidth, 0f);
            gridPanel.offsetMax = gridOffsetMax;
            CardResolutionSetup.Configure(game);
            for (int i = 0; i < people.Length; i++)
            {
                var person = people[i];
                person.SetLayoutScale(Vector3.one * 2f);
                person.Rect.anchorMin = person.Rect.anchorMax = new Vector2(0f, 1f);
                person.Rect.anchoredPosition = new Vector2(132f + i % 2 * 224f, -220f - i / 2 * 290f);
                var details = person.GetComponent<CardHover>().Details;
                details.localScale = Vector3.one * 0.26f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(person.Rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(details);
            }
            foreach (string name in new[] { "PeopleCard", "EducatedPeopleCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    prefab.GetComponent<DraggableCard>().SetLayoutScale(Vector3.one * 2f);
                    prefab.transform.Find("HoverDetails").localScale = Vector3.one * 0.26f;
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log($"Saved 2x people cards with unchanged info panel size; design area {table.sizeDelta}.");
        }

        [MenuItem("Tools/Civil Game Jam/Apply Edited Card Examples To 4x3 Board")]
        public static void ApplyEditedExamples()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply the saved layout in Edit Mode.");
            var game = UnityEngine.Object.FindFirstObjectByType<CardGameController>();
            if (game == null) throw new InvalidOperationException("Open 2DCardScene first.");
            Canvas.ForceUpdateCanvases();
            var cards = game.GetComponentsInChildren<DraggableCard>();
            var example = cards.Single(c => c.Kind == CardKind.ResearchCenter);
            var factory = cards.Single(c => c.Kind == CardKind.Factory);
            var person = cards.Single(c => c.name == "PeopleCard_02");
            Vector2 buildingSize = example.Rect.sizeDelta;
            Vector3 buildingScale = example.Rect.localScale;
            Vector2 personSize = person.Rect.sizeDelta;
            Vector3 personScale = person.Rect.localScale;
            var gridRect = game.Grid.rectTransform;
            Vector2 visibleSize = Vector2.Scale(buildingSize, buildingScale);
            Vector3 firstCenter = gridRect.InverseTransformPoint(example.Rect.position);
            Vector3 secondCenter = gridRect.InverseTransformPoint(factory.Rect.position);
            float gap = Mathf.Max(0f, secondCenter.x - firstCenter.x - visibleSize.x);
            const float verticalPadding = 36f;
            float requiredHeight = CardGrid.Rows * visibleSize.y + (CardGrid.Rows - 1) * gap + verticalPadding * 2f;

            // Keep the example's rect and scale. Extend the authored area to fit all rows.
            // The GridPanel's hand-edited anchors, offsets and horizontal spacing remain intact.
            var table = (RectTransform)game.transform;
            table.sizeDelta += new Vector2(0f, Mathf.Ceil(Mathf.Max(0f, requiredHeight - gridRect.rect.height)));
            CardResolutionSetup.Configure(game);
            Canvas.ForceUpdateCanvases();
            PinHud((RectTransform)table.Find("Population"), 1f, -28f, 44f);
            PinHud((RectTransform)table.Find("CardInstructions"), 0f, 20f, 28f);
            gridRect.name = "GridSlots";
            var cells = new RectTransform[CardGrid.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                string name = "Cell_" + (i + 1).ToString("00");
                cells[i] = gridRect.Find(name) as RectTransform;
                if (cells[i] == null)
                {
                    cells[i] = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                    cells[i].gameObject.layer = gridRect.gameObject.layer;
                    cells[i].SetParent(gridRect, false);
                }
                var cell = cells[i];
                cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 1f);
                cell.pivot = new Vector2(0.5f, 0.5f);
                cell.localScale = Vector3.one;
                cell.sizeDelta = visibleSize;
                cell.anchoredPosition = new Vector2(firstCenter.x + i % CardGrid.Columns * (visibleSize.x + gap),
                    -verticalPadding - visibleSize.y / 2f - i / CardGrid.Columns * (visibleSize.y + gap));
            }
            var data = new SerializedObject(game.Grid);
            var slots = data.FindProperty("cells");
            slots.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            data.ApplyModifiedPropertiesWithoutUndo();

            foreach (var card in cards)
            {
                if (card.IsBuilding)
                {
                    card.Rect.SetParent(cells[card.InitialGridCell], false);
                    card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
                    card.Rect.anchoredPosition = Vector2.zero;
                    ApplyDimensions(card, buildingSize, buildingScale);
                }
                else if (card.IsPerson) ApplyDimensions(card, personSize, personScale);
                PrefabUtility.RecordPrefabInstancePropertyModifications(card.Rect);
            }

            foreach (string name in new[] { "ResearchCenterCard", "FactoryCard", "SchoolCard", "HouseCard" })
                SavePrefabDimensions(name, buildingSize, buildingScale);
            foreach (string name in new[] { "PeopleCard", "EducatedPeopleCard" })
                SavePrefabDimensions(name, personSize, personScale);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Saved 4x3 board: buildings {buildingSize} at scale {buildingScale.x}, gap {gap}, people {personSize}.");
        }

        public static void EnlargeBuildingsForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = UnityEngine.Object.FindFirstObjectByType<CardGameController>();
            Canvas.ForceUpdateCanvases();
            var buildings = game.GetComponentsInChildren<DraggableCard>().Where(c => c.IsBuilding).ToArray();
            var example = buildings.Single(c => c.Kind == CardKind.ResearchCenter);
            Vector2 nativeSize = example.Rect.sizeDelta;
            Vector3 oldScale = example.Rect.localScale;
            Vector3 enlargedScale = oldScale * 1.6f;
            Vector2 oldVisibleSize = Vector2.Scale(nativeSize, oldScale);
            Vector2 visibleSize = Vector2.Scale(nativeSize, enlargedScale);
            var gridRect = game.Grid.rectTransform;
            var first = game.Grid.GetCell(0);
            Vector2 firstPoint = gridRect.InverseTransformPoint(first.position);
            Vector2 secondPoint = gridRect.InverseTransformPoint(game.Grid.GetCell(1).position);
            Vector2 rowPoint = gridRect.InverseTransformPoint(game.Grid.GetCell(CardGrid.Columns).position);
            Vector2 gap = new Vector2(secondPoint.x - firstPoint.x - oldVisibleSize.x,
                firstPoint.y - rowPoint.y - oldVisibleSize.y);
            Vector2 contentSize = new Vector2(CardGrid.Columns * visibleSize.x + (CardGrid.Columns - 1) * gap.x,
                CardGrid.Rows * visibleSize.y + (CardGrid.Rows - 1) * gap.y);
            var table = (RectTransform)game.transform;
            Vector2 extra = Vector2.Max(Vector2.zero, contentSize + Vector2.one * 72f - gridRect.rect.size);
            table.sizeDelta += new Vector2(Mathf.Ceil(extra.x), Mathf.Ceil(extra.y));
            CardResolutionSetup.Configure(game);
            Canvas.ForceUpdateCanvases();
            var cells = new RectTransform[CardGrid.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = game.Grid.GetCell(i);
                cells[i].anchorMin = cells[i].anchorMax = new Vector2(0.5f, 0.5f);
                cells[i].sizeDelta = visibleSize;
                cells[i].anchoredPosition = new Vector2(-contentSize.x / 2f + visibleSize.x / 2f +
                    i % CardGrid.Columns * (visibleSize.x + gap.x), contentSize.y / 2f - visibleSize.y / 2f -
                    i / CardGrid.Columns * (visibleSize.y + gap.y));
            }
            var occupied = new bool[CardGrid.CellCount];
            foreach (var building in buildings)
            {
                int index = building.InitialGridCell;
                if (index < 0 || index >= cells.Length || occupied[index])
                    index = Array.FindIndex(occupied, value => !value);
                occupied[index] = true;
                building.Rect.SetParent(cells[index], false);
                building.Rect.anchorMin = building.Rect.anchorMax = new Vector2(0.5f, 0.5f);
                building.Rect.anchoredPosition = Vector2.zero;
                building.SetLayoutScale(enlargedScale);
                var cardData = new SerializedObject(building);
                cardData.FindProperty("initialGridCell").intValue = index;
                cardData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(building.Rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(building);
            }
            var data = new SerializedObject(game.Grid);
            var savedCells = data.FindProperty("cells"); savedCells.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) savedCells.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform child in gridRect.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Cell_") && !cells.Any(cell => cell == child)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (string name in new[] { "ResearchCenterCard", "FactoryCard", "SchoolCard", "HouseCard" })
                SavePrefabDimensions(name, nativeSize, enlargedScale);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log($"Saved enlarged 4x3 board: scale {oldScale.x} -> {enlargedScale.x}; " +
                $"card ratio {nativeSize.x}:{nativeSize.y}; design area {table.sizeDelta}; gap {gap}.");
        }

        private static void ApplyDimensions(DraggableCard card, Vector2 size, Vector3 scale)
        {
            card.Rect.pivot = new Vector2(0.5f, 0.5f);
            card.Rect.sizeDelta = size;
            card.SetLayoutScale(scale);
        }

        private static void PinHud(RectTransform rect, float anchorY, float offsetY, float height)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.03f, anchorY);
            rect.anchorMax = new Vector2(0.97f, anchorY);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, offsetY);
        }

        private static void SavePrefabDimensions(string name, Vector2 size, Vector3 scale)
        {
            string path = "Assets/Prefabs/" + name + ".prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyDimensions(prefab.GetComponent<DraggableCard>(), size, scale);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
    }
}
