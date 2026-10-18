using System;
using System.Collections.Generic;
using System.Linq;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardBuildingGroundAuthoring
    {
        [MenuItem("Tools/Card Game/Restore Travelling Building Grounds")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Restore building grounds in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var count = 0;
            foreach (Transform building in tsunami.BuildingsRoot)
                if (building.gameObject.activeSelf) count += RestoreGroup(building, tsunami.GroundSurface);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save restored building grounds.");
            Debug.Log("Restored " + count + " original ground meshes as children of their travelling buildings.");
        }

        internal static int RestoreGroup(Transform building, Transform mapGround)
        {
            var mapTop = mapGround.GetComponent<MeshRenderer>().bounds.max.y;
            var count = 0;
            var apartment = building.GetComponent<ApartmentUpgradeModel>();
            foreach (var filter in building.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) < 0) continue;
                // Imported normal/elevated variants retain hidden source grounds. The shared exterior is the travelling ground.
                if (apartment != null && apartment.MotionRoot != null && filter.transform.IsChildOf(apartment.MotionRoot)) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(filter);
                if (source == null || source.sharedMesh == null) throw new InvalidOperationException("Missing original ground mesh for " + building.name + "/" + filter.name);
                filter.sharedMesh = source.sharedMesh;
                var surface = GroundSurfaceHeight(filter);
                // Keep the original lawn visible above the map tile without changing the building or upgrades.
                var lift = Mathf.Max(0f, mapTop + .004f - surface);
                filter.transform.position += Vector3.up * lift;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) throw new InvalidOperationException("Missing ground renderer for " + building.name);
                renderer.enabled = true;
                if (!filter.gameObject.activeSelf) filter.gameObject.SetActive(true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter.gameObject);
                count++;
                Debug.Log("Travelling ground " + building.name + "/" + filter.name + ": " +
                    filter.sharedMesh.name + ", surface " + surface.ToString("R") + ", lift " + lift.ToString("R") +
                    ", width " + renderer.bounds.size.x.ToString("R") + ", depth " + renderer.bounds.size.z.ToString("R"));
            }
            if (count == 0) throw new InvalidOperationException("No building ground found on " + building.name);
            return count;
        }

        private static float GroundSurfaceHeight(MeshFilter filter)
        {
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var levels = new Dictionary<int, float>();
            foreach (var submesh in Enumerable.Range(0, mesh.subMeshCount))
            {
                var triangles = mesh.GetTriangles(submesh);
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = filter.transform.TransformPoint(vertices[triangles[i]]);
                    var b = filter.transform.TransformPoint(vertices[triangles[i + 1]]);
                    var c = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                    if (Mathf.Max(a.y, b.y, c.y) - Mathf.Min(a.y, b.y, c.y) > .0002f) continue;
                    var area = Vector3.Cross(b - a, c - a).magnitude * .5f;
                    var level = Mathf.RoundToInt((a.y + b.y + c.y) / 3f * 10000f);
                    levels[level] = (levels.TryGetValue(level, out var sum) ? sum : 0f) + area;
                }
            }
            if (levels.Count == 0) throw new InvalidOperationException("No flat ground surface on " + filter.name);
            var largest = levels.Values.Max();
            return levels.Where(pair => pair.Value >= largest * .9f).Max(pair => pair.Key) / 10000f;
        }
    }
}
