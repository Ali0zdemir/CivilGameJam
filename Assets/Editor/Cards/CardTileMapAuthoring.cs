using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardTileMapAuthoring
    {
        public const string TileAsset = "Assets/EfeArts/Map(Guncel1).fbx";
        private const string Work = "Library/TileMapWork";
        private const string FoliageFolder = "Assets/Art/Buildings/TileFoliage";

        [MenuItem("Tools/Card Game/Save Current 4x2 Map")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author the tile map in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(TileAsset);
            var sourceMesh = source != null ? source.transform.Find("Tiles").GetComponent<MeshFilter>() : null;
            if (sourceMesh == null) throw new InvalidOperationException("Tiles must contain the authored ground mesh.");
            var cells = TileCells(sourceMesh.sharedMesh);
            if (cells.Length != 8 || cells.Select(cell => Mathf.RoundToInt(cell.center.x * 1000f)).Distinct().Count() != 4 ||
                cells.Select(cell => Mathf.RoundToInt(cell.center.z * 1000f)).Distinct().Count() != 2)
                throw new InvalidOperationException("Expected eight authored tiles in four columns and two rows.");

            var previousGround = tsunami.GroundSurface;
            var oldBounds = tsunami.CityBounds;
            var groundPosition = previousGround.position;
            var groundRotation = previousGround.rotation;
            var settings = new SerializedObject(tsunami);
            var approach = settings.FindProperty("startDistance").floatValue - oldBounds.min.z;
            var seaMonster = (SeaMonsterEmergence)settings.FindProperty("seaMonster").objectReferenceValue;
            var monsterShoreOffset = seaMonster.transform.localPosition.z - oldBounds.min.z;
            var walk = seaMonster.GetComponent<SeaMonsterWalk>();
            var departureOffset = walk.AfterEventPoint.position - tsunami.transform.TransformPoint(new Vector3(0f, 0f, oldBounds.max.z));
            var groups = tsunami.BuildingsRoot.Cast<Transform>().Where(group => group.gameObject.activeSelf).ToArray();
            var orderedColumns = groups.Select(group => Mathf.RoundToInt(group.localPosition.x * 1000f)).Distinct().OrderBy(x => x).ToArray();
            if (orderedColumns.Length != 4 || groups.Length > cells.Length) throw new InvalidOperationException("The saved buildings must fit the four-column map.");
            var frontRow = groups.Min(group => group.localPosition.z);
            var foliage = new Dictionary<Mesh, Mesh>();
            if (!AssetDatabase.IsValidFolder(FoliageFolder)) AssetDatabase.CreateFolder("Assets/Art/Buildings", "TileFoliage");

            var mapObject = scene.GetRootGameObjects().SingleOrDefault(root =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == TileAsset);
            if (mapObject == null) mapObject = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            mapObject.name = "Map(Guncel1)";
            var map = mapObject.transform;
            var ground = map.Find("Tiles") ?? map.Find("Continuous_Ground");
            if (ground == null) throw new InvalidOperationException("The main map must contain its own Tiles surface.");
            // Use the real model instance as the main map, aligned with the saved city's existing ground.
            map.localScale = Vector3.one;
            map.rotation = groundRotation * Quaternion.Inverse(ground.localRotation);
            map.position = groundPosition - map.TransformVector(ground.localPosition);
            mapObject.SetActive(true);
            ground.name = "Continuous_Ground";
            ground.gameObject.SetActive(true);
            ground.GetComponent<MeshRenderer>().enabled = true;
            var cityCenter = (groups.Aggregate(Vector3.positiveInfinity, (min, group) => Vector3.Min(min, group.position)) +
                groups.Aggregate(Vector3.negativeInfinity, (max, group) => Vector3.Max(max, group.position))) * .5f;
            var tileCenter = ground.TransformPoint(sourceMesh.sharedMesh.bounds.center);
            map.position += new Vector3(cityCenter.x - tileCenter.x, 0f, cityCenter.z - tileCenter.z);
            cells = cells.OrderBy(cell => Mathf.RoundToInt(tsunami.transform.InverseTransformPoint(ground.TransformPoint(cell.center)).z * 1000f))
                .ThenBy(cell => tsunami.transform.InverseTransformPoint(ground.TransformPoint(cell.center)).x).ToArray();
            if (ground.TryGetComponent<BoxCollider>(out var collider))
            {
                collider.center = sourceMesh.sharedMesh.bounds.center - Vector3.up * .05f;
                collider.size = new Vector3(sourceMesh.sharedMesh.bounds.size.x, .1f, sourceMesh.sharedMesh.bounds.size.z);
            }
            var markers = ground.Find("Tile_Grid_4x2") ?? previousGround.Find("Tile_Grid_4x2") ?? previousGround.Find("Tile_Grid_4x3");
            if (markers == null) { markers = new GameObject("Tile_Grid_4x2").transform; markers.SetParent(ground, false); }
            else if (markers.parent != ground) markers.SetParent(ground, false);
            markers.name = "Tile_Grid_4x2";
            for (var i = 0; i < cells.Length; i++)
            {
                var name = "Tile_R" + (i / 4 + 1) + "_C" + (i % 4 + 1);
                var marker = markers.Find(name);
                if (marker == null) { marker = new GameObject(name).transform; marker.SetParent(markers, false); }
                marker.localPosition = cells[i].center;
            }
            foreach (var stale in markers.Cast<Transform>().Where(marker => marker.name.StartsWith("Tile_R3_", StringComparison.Ordinal)).ToArray())
                Object.DestroyImmediate(stale.gameObject);
            var removedPads = 0;
            foreach (var group in groups)
            {
                var column = Array.IndexOf(orderedColumns, Mathf.RoundToInt(group.localPosition.x * 1000f));
                var row = group.localPosition.z < frontRow + .5f ? 0 : 1;
                var position = tsunami.BuildingsRoot.InverseTransformPoint(ground.TransformPoint(cells[row * 4 + column].center));
                position.y = group.localPosition.y;
                group.localPosition = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                // Remove only the old flat pads; retain the original trees and their materials.
                foreach (var filter in group.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null || filter.name.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var original = filter.sharedMesh;
                    if (AssetDatabase.GetAssetPath(original).StartsWith(FoliageFolder + "/", StringComparison.Ordinal)) { removedPads++; continue; }
                    if (!foliage.TryGetValue(original, out var replacement)) foliage.Add(original, replacement = WithoutGroundPad(original));
                    filter.sharedMesh = replacement; PrefabUtility.RecordPrefabInstancePropertyModifications(filter); removedPads++;
                }
            }
            if (removedPads < groups.Length) throw new InvalidOperationException("Every building must have its original ground pad replaced.");
            var island = previousGround.parent.Find("Island_Base");
            if (island != null) island.GetComponent<MeshRenderer>().enabled = false;
            var bounds = TransformBounds(tsunami.transform.worldToLocalMatrix * ground.localToWorldMatrix, sourceMesh.sharedMesh.bounds);
            settings.FindProperty("groundSurface").objectReferenceValue = ground;
            settings.FindProperty("shorelineDistance").floatValue = bounds.min.z;
            settings.FindProperty("startDistance").floatValue = bounds.min.z + approach;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var monsterPosition = seaMonster.transform.localPosition;
            monsterPosition.z = bounds.min.z + monsterShoreOffset;
            seaMonster.transform.localPosition = monsterPosition;
            walk.AfterEventPoint.position = tsunami.transform.TransformPoint(new Vector3(0f, 0f, bounds.max.z)) + departureOffset;
            var sea = (GameObject)new SerializedObject(seaMonster).FindProperty("seaSurface").objectReferenceValue;
            var seaPosition = sea.transform.localPosition; seaPosition.z = bounds.min.z - sea.transform.localScale.z * .5f;
            sea.transform.localPosition = seaPosition;
            CardMapArtAuthoring.Configure(tsunami, bounds);
            if (previousGround != ground) previousGround.gameObject.SetActive(false);
            foreach (var changed in map.GetComponentsInChildren<Transform>(true))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
                PrefabUtility.RecordPrefabInstancePropertyModifications(changed.gameObject);
            }
            foreach (var renderer in map.GetComponentsInChildren<MeshRenderer>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the tile map.");
            Directory.CreateDirectory(Work);
            File.WriteAllText(Work + "/layout.txt", TileAsset + "\n4 columns x 2 rows\nBuilding roots: " + groups.Length + "\nReplaced pads: " + removedPads + "\nBounds: " + bounds + "\n");
            Debug.Log("Saved the active Map(Guncel1) model as the main map with its own ground and scenery linked to the existing game systems.");
        }

        private static Bounds[] TileCells(Mesh mesh)
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            var parents = Enumerable.Range(0, vertices.Length).ToArray();
            Func<int, int> root = null;
            root = index => parents[index] == index ? index : parents[index] = root(parents[index]);
            Action<int, int> join = (a, b) => parents[root(a)] = root(b);
            var positions = new Dictionary<Vector3Int, int>();
            for (var i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 100000f);
                if (positions.TryGetValue(key, out var previous)) join(i, previous); else positions.Add(key, i);
            }
            for (var i = 0; i < triangles.Length; i += 3) { join(triangles[i], triangles[i + 1]); join(triangles[i], triangles[i + 2]); }
            var components = new Dictionary<int, Bounds>();
            foreach (var index in triangles.Distinct())
            {
                var key = root(index);
                if (!components.TryGetValue(key, out var bounds)) bounds = new Bounds(vertices[index], Vector3.zero);
                bounds.Encapsulate(vertices[index]); components[key] = bounds;
            }
            return components.Values.OrderBy(bounds => Mathf.RoundToInt(bounds.center.z * 1000f)).ThenBy(bounds => bounds.center.x).ToArray();
        }

        private static Mesh WithoutGroundPad(Mesh source)
        {
            var vertices = source.vertices; var levels = new Dictionary<int, float>();
            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                var triangles = source.GetTriangles(sub);
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                    if (Mathf.Max(a.y, b.y, c.y) - Mathf.Min(a.y, b.y, c.y) > .0001f) continue;
                    var key = Mathf.RoundToInt((a.y + b.y + c.y) / 3f * 10000f);
                    var area = Vector3.Cross(b - a, c - a).magnitude * .5f;
                    levels[key] = (levels.TryGetValue(key, out var sum) ? sum : 0f) + area;
                }
            }
            if (levels.Count == 0) throw new InvalidOperationException("No flat ground pad on " + source.name);
            var largestArea = levels.Values.Max();
            var groundY = levels.Where(pair => pair.Value >= largestArea * .9f).Max(pair => pair.Key) / 10000f;
            var mesh = Object.Instantiate(source); mesh.name = source.name + "_Foliage";
            var used = new List<int>();
            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                var original = source.GetTriangles(sub); var keep = new List<int>();
                for (var i = 0; i < original.Length; i += 3)
                    if (Mathf.Max(vertices[original[i]].y, vertices[original[i + 1]].y, vertices[original[i + 2]].y) > groundY + .0001f)
                    { keep.Add(original[i]); keep.Add(original[i + 1]); keep.Add(original[i + 2]); }
                mesh.SetTriangles(keep, sub, false); used.AddRange(keep);
            }
            if (used.Count == 0 || used.Count == source.triangles.Length) throw new InvalidOperationException("Could not separate trees from the ground on " + source.name);
            var remaining = new Bounds(vertices[used[0]], Vector3.zero); foreach (var index in used) remaining.Encapsulate(vertices[index]); mesh.bounds = remaining;
            var file = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source)) + "_" + source.name + "_Foliage";
            file = new string(file.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            var path = FoliageFolder + "/" + file + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh); return saved;
        }

        [Serializable] private sealed class Report
        {
            public List<Node> tiles = new List<Node>();
            public List<Node> city = new List<Node>();
            public List<Node> buildingGroups = new List<Node>();
            public Bounds cityBounds;
            public float groundLevel;
        }

        [Serializable] private sealed class Node
        {
            public string path, mesh;
            public Vector3 position, rotation, scale;
            public Bounds bounds;
            public bool active;
            public string[] materials;
        }

        [MenuItem("Tools/Card Game/Inspect Tile Map Source")]
        public static void InspectForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            Directory.CreateDirectory(Work);
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var report = new Report { cityBounds = tsunami.CityBounds, groundLevel = tsunami.GroundLevel };
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(TileAsset);
            if (source == null) throw new InvalidOperationException("The Tiles model is missing.");
            var instance = Object.Instantiate(source);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var node in instance.GetComponentsInChildren<Transform>(true)) report.tiles.Add(ReadNode(node, instance.transform));
                var root = GameObject.Find("World3D").transform;
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (Depth(node, root) <= 4) report.city.Add(ReadNode(node, tsunami.transform));
                foreach (Transform building in tsunami.BuildingsRoot) report.buildingGroups.Add(ReadNode(building, tsunami.transform, true));
                File.WriteAllText(Work + "/source-report.json", JsonUtility.ToJson(report, true));
                Render(instance.transform, Work + "/tiles-source.png");
            }
            finally { Object.DestroyImmediate(instance); }
            Debug.Log("Tile source and saved city geometry written to " + Work);
        }

        private static int Depth(Transform node, Transform root)
        {
            var depth = 0;
            while (node != null && node != root) { depth++; node = node.parent; }
            return depth;
        }

        private static Node ReadNode(Transform node, Transform frame, bool includeChildren = false)
        {
            var path = node.name; var parent = node.parent;
            while (parent != null && parent != frame) { path = parent.name + "/" + path; parent = parent.parent; }
            var filters = includeChildren ? node.GetComponentsInChildren<MeshFilter>(true) : node.GetComponents<MeshFilter>();
            var found = false; var bounds = new Bounds();
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
                var local = TransformBounds(frame.worldToLocalMatrix * filter.transform.localToWorldMatrix, filter.sharedMesh.bounds);
                if (!found) { found = true; bounds = local; } else bounds.Encapsulate(local);
            }
            var renderer = node.GetComponent<MeshRenderer>();
            return new Node
            {
                path = path, position = frame.InverseTransformPoint(node.position), rotation = node.localEulerAngles, scale = node.localScale,
                bounds = bounds, active = node.gameObject.activeSelf,
                mesh = node.TryGetComponent<MeshFilter>(out var mesh) && mesh.sharedMesh != null ? mesh.sharedMesh.name : "",
                materials = renderer != null ? renderer.sharedMaterials.Select(material => material == null ? "null" : material.name + " :: " + material.shader.name + " :: " + AssetDatabase.GetAssetPath(material)).ToArray() : new string[0]
            };
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds local)
        {
            var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (var i = 0; i < 8; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            return bounds;
        }

        private static void Render(Transform root, string path)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            var bounds = new Bounds(root.position, Vector3.zero); var first = true;
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
                filter.gameObject.layer = 30;
                var box = TransformBounds(filter.transform.localToWorldMatrix, filter.sharedMesh.bounds);
                if (first) { bounds = box; first = false; } else bounds.Encapsulate(box);
            }
            var cameraObject = new GameObject("TilePreviewCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z, bounds.size.y) * .7f;
            camera.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
            camera.transform.position = bounds.center - camera.transform.forward * Mathf.Max(20f, bounds.size.magnitude * 2f);
            camera.farClipPlane = Mathf.Max(100f, bounds.size.magnitude * 5f); camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.79f, .85f, .82f);
            var target = new RenderTexture(1200, 900, 24); camera.targetTexture = target;
            var previous = RenderTexture.active; var image = new Texture2D(1200, 900, TextureFormat.RGB24, false);
            try
            {
                camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; target.Release();
                Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
