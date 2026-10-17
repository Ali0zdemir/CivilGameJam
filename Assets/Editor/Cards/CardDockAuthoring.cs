using System.Linq;
using CivilGameJam.Cards;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardDockAuthoring
    {
        public static void BuildForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            var panel = (RectTransform)game.transform;
            var canvas = game.GetComponentInParent<Canvas>();
            var dock = game.GetComponent<CardDock>() ?? game.gameObject.AddComponent<CardDock>();
            var buildings = game.GetComponentsInChildren<DraggableCard>().Where(c => c.IsBuilding).ToArray();
            var example = buildings.Single(c => c.Kind == CardKind.ResearchCenter);
            Vector2 size = Vector2.Scale(example.Rect.sizeDelta, example.Rect.localScale);
            const float gap = 27.5472f;
            panel.sizeDelta = new Vector2(panel.rect.width, 964f);
            panel.GetComponent<Image>().color = new Color(0.055f, 0.080f, 0.09f, 0.98f);
            CardResolutionSetup.Configure(game);
            var gridPanel = (RectTransform)panel.Find("GridPanel");
            gridPanel.anchorMin = Vector2.zero;
            gridPanel.anchorMax = Vector2.one;
            gridPanel.offsetMin = new Vector2(504.7095f, 40f);
            gridPanel.offsetMax = new Vector2(-68.0675f, -64f);
            gridPanel.GetComponent<Image>().color = new Color(0.16f, 0.21f, 0.22f, 0.55f);
            var grid = game.Grid.rectTransform;
            grid.anchorMin = Vector2.zero; grid.anchorMax = Vector2.one;
            grid.offsetMin = grid.offsetMax = Vector2.zero;
            var cells = new RectTransform[CardGrid.CellCount];
            float width = CardGrid.Columns * size.x + (CardGrid.Columns - 1) * gap;
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = game.Grid.GetCell(i);
                cells[i].anchorMin = cells[i].anchorMax = new Vector2(0.5f, 1f);
                cells[i].sizeDelta = size;
                cells[i].anchoredPosition = new Vector2(-width / 2f + size.x / 2f +
                    i % CardGrid.Columns * (size.x + gap), -1f - size.y / 2f - i / CardGrid.Columns * (size.y + gap));
            }
            foreach (var building in buildings)
            {
                building.Rect.SetParent(cells[building.InitialGridCell], false);
                building.Rect.anchorMin = building.Rect.anchorMax = new Vector2(0.5f, 0.5f);
                building.Rect.anchoredPosition = Vector2.zero;
                PrefabUtility.RecordPrefabInstancePropertyModifications(building.Rect);
            }
            var gridData = new SerializedObject(game.Grid);
            var savedCells = gridData.FindProperty("cells"); savedCells.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) savedCells.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            gridData.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform child in grid.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Cell_") && !cells.Any(cell => cell == child)) Object.DestroyImmediate(child.gameObject);
            var people = game.GetComponentsInChildren<DraggableCard>().Where(c => c.IsPerson).OrderBy(c => c.name).ToArray();
            for (int i = 0; i < people.Length; i++)
            {
                people[i].Rect.anchorMin = people[i].Rect.anchorMax = new Vector2(0f, 1f);
                people[i].Rect.anchoredPosition = new Vector2(132f + i % 2 * 224f, -240f - i / 2 * 264f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(people[i].Rect);
            }
            game.CardLayer.anchoredPosition = Vector2.zero;
            var activation = canvas.transform.Find("CardDockActivation") as RectTransform;
            if (activation == null) activation = new GameObject("CardDockActivation", typeof(RectTransform)).GetComponent<RectTransform>();
            activation.SetParent(canvas.transform, false);
            activation.gameObject.layer = canvas.gameObject.layer;
            activation.anchorMin = activation.anchorMax = activation.pivot = new Vector2(0.5f, 0f);
            activation.sizeDelta = new Vector2(panel.rect.width, 560f);
            activation.anchoredPosition = Vector2.zero;
            var data = new SerializedObject(dock);
            data.FindProperty("game").objectReferenceValue = game;
            data.FindProperty("panel").objectReferenceValue = panel;
            data.FindProperty("activationArea").objectReferenceValue = activation;
            data.FindProperty("collapsedHeight").floatValue = size.y + 83f;
            data.FindProperty("slideSeconds").floatValue = 0.32f;
            data.ApplyModifiedPropertiesWithoutUndo();
            var population = panel.Find("Population")?.GetComponent<TextMeshProUGUI>();
            if (population != null) population.color = new Color(0.90f, 0.94f, 0.93f);
            CreateWorld();
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved bottom card dock: 4x2 expanded / one row collapsed, four people, editable 3D world.");
        }

        private static void CreateWorld()
        {
            if (GameObject.Find("World3D") != null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/CardWorld")) AssetDatabase.CreateFolder("Assets/Materials", "CardWorld");
            var world = new GameObject("World3D").transform;
            var grass = Material("Grass", new Color(0.36f, 0.51f, 0.37f));
            var stone = Material("Stone", new Color(0.46f, 0.44f, 0.36f));
            var water = Material("Water", new Color(0.14f, 0.32f, 0.38f));
            var road = Material("Road", new Color(0.22f, 0.28f, 0.29f));
            var cream = Material("Walls", new Color(0.85f, 0.77f, 0.55f));
            var roof = Material("Roofs", new Color(0.62f, 0.30f, 0.20f));
            var glass = Material("Glass", new Color(0.27f, 0.62f, 0.69f));
            var blue = Material("Research", new Color(0.32f, 0.49f, 0.62f));
            var gold = Material("School", new Color(0.85f, 0.60f, 0.25f));
            var bark = Material("Trunk", new Color(0.40f, 0.29f, 0.20f));
            var leaves = Material("Leaves", new Color(0.28f, 0.45f, 0.29f));
            Cube("Water", world, new Vector3(0f, -0.9f, 0f), new Vector3(160f, 0.2f, 160f), water);
            Cube("IslandBase", world, new Vector3(0f, -0.35f, 0f), new Vector3(20f, 0.7f, 13f), stone);
            Cube("Ground", world, Vector3.zero, new Vector3(19.8f, 0.15f, 12.8f), grass);
            Cube("MainStreet", world, new Vector3(0f, 0.10f, -0.6f), new Vector3(19.8f, 0.08f, 1.6f), road);
            Cube("CrossStreet", world, new Vector3(1.5f, 0.11f, 0f), new Vector3(1.5f, 0.08f, 12.8f), road);
            for (int i = 0; i < 10; i++)
                Cube("RoadMark_" + i, world, new Vector3(-8.7f + i * 1.9f, 0.16f, -0.6f), new Vector3(0.65f, 0.02f, 0.06f), cream);
            House("House_01", world, new Vector3(-6.7f, 0.15f, -3.5f), cream, roof, glass);
            House("House_02", world, new Vector3(-3.2f, 0.15f, -3.5f), cream, roof, glass);
            House("House_03", world, new Vector3(4.3f, 0.15f, -3.5f), cream, roof, glass);
            House("House_04", world, new Vector3(7.4f, 0.15f, -3.5f), cream, roof, glass);
            var research = new GameObject("ResearchCenter").transform; research.SetParent(world, false);
            research.localPosition = new Vector3(-6f, 0.15f, 3f);
            Cube("Building", research, new Vector3(0f, 1f, 0f), new Vector3(3.1f, 2f, 2.6f), blue);
            Cube("GlassFront", research, new Vector3(0f, 1.25f, -1.31f), new Vector3(2.6f, 0.7f, 0.06f), glass);
            Cube("Roof", research, new Vector3(0f, 2.1f, 0f), new Vector3(3.3f, 0.2f, 2.8f), cream);
            var school = new GameObject("School").transform; school.SetParent(world, false);
            school.localPosition = new Vector3(-1.5f, 0.15f, 3f);
            Cube("Building", school, new Vector3(0f, 0.85f, 0f), new Vector3(3f, 1.7f, 2.5f), gold);
            Cube("Roof", school, new Vector3(0f, 1.8f, 0f), new Vector3(3.3f, 0.2f, 2.8f), roof);
            for (int i = 0; i < 3; i++) Cube("Window_" + i, school, new Vector3(-0.9f + i * 0.9f, 1f, -1.26f), new Vector3(0.45f, 0.5f, 0.04f), glass);
            var factory = new GameObject("Factory").transform; factory.SetParent(world, false);
            factory.localPosition = new Vector3(5.2f, 0.15f, 3f);
            Cube("Building", factory, new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 3f), stone);
            Cube("Roof", factory, new Vector3(0f, 2.1f, 0f), new Vector3(4.2f, 0.2f, 3.2f), road);
            Cube("Door", factory, new Vector3(0f, 0.65f, -1.52f), new Vector3(1.1f, 1.3f, 0.06f), glass);
            Cube("Chimney", factory, new Vector3(1.15f, 2.7f, 0.6f), new Vector3(0.65f, 2.5f, 0.65f), roof);
            foreach (var position in new[] { new Vector3(-8.5f, 0f, 4.8f), new Vector3(-8.8f, 0f, 0.7f), new Vector3(8.8f, 0f, 5f), new Vector3(8.9f, 0f, -5f) })
            {
                Cube("TreeTrunk", world, position + Vector3.up * 0.75f, new Vector3(0.25f, 1.5f, 0.25f), bark);
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere); crown.name = "TreeCrown";
                crown.transform.SetParent(world, false); crown.transform.localPosition = position + Vector3.up * 1.75f;
                crown.transform.localScale = new Vector3(1.6f, 1.8f, 1.6f); crown.GetComponent<Renderer>().sharedMaterial = leaves;
            }
            var camera = Camera.main;
            camera.transform.position = new Vector3(19f, 21f, -26f);
            camera.transform.LookAt(new Vector3(0f, -7f, 0f));
            camera.orthographic = true; camera.orthographicSize = 14f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f, 0.25f, 0.30f);
            var sun = Object.FindFirstObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f); sun.intensity = 1.3f; sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.62f, 0.68f, 0.68f);
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/Materials/CardWorld/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = name; material.color = color; material.SetFloat("_Smoothness", 0.08f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static Transform Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
            cube.transform.SetParent(parent, false); cube.transform.localPosition = position; cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube.transform;
        }

        private static void House(string name, Transform parent, Vector3 position, Material walls, Material roof, Material glass)
        {
            var house = new GameObject(name).transform; house.SetParent(parent, false); house.localPosition = position;
            Cube("Walls", house, new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 1.4f, 1.7f), walls);
            Cube("RoofLeft", house, new Vector3(-0.5f, 1.68f, 0f), new Vector3(1.25f, 0.18f, 2f), roof).localRotation = Quaternion.Euler(0f, 0f, 30f);
            Cube("RoofRight", house, new Vector3(0.5f, 1.68f, 0f), new Vector3(1.25f, 0.18f, 2f), roof).localRotation = Quaternion.Euler(0f, 0f, -30f);
            Cube("Window", house, new Vector3(-0.45f, 0.9f, -0.86f), new Vector3(0.35f, 0.4f, 0.04f), glass);
            Cube("Door", house, new Vector3(0.35f, 0.4f, -0.86f), new Vector3(0.4f, 0.8f, 0.04f), roof);
        }
    }
}
