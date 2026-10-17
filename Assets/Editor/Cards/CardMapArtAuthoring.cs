using System;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardMapArtAuthoring
    {
        private const string SourcePath = "Assets/emreAnimation/Map+Tsunami+Tiles.fbx";
        private const string WaterSourcePath = "Assets/emreAnimation/Deniz+Tsunami(updated).fbx";

        [MenuItem("Tools/Card Game/Save Authored Map And Tsunami")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save map art in Edit Mode.");
            CardTileMapAuthoring.SaveForBatch();
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var data = new SerializedObject(tsunami);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var waterSource = AssetDatabase.LoadAssetAtPath<GameObject>(WaterSourcePath);
            var sourceTiles = source.transform.Find("Tiles");
            var scenery = tsunami.transform.Find("Map_Scenery");
            if (scenery == null) { scenery = new GameObject("Map_Scenery").transform; scenery.SetParent(tsunami.transform, false); }
            scenery.localPosition = tsunami.transform.InverseTransformPoint(tsunami.GroundSurface.position);
            scenery.localRotation = Quaternion.Euler(0f, 180f, 0f); scenery.localScale = Vector3.one;
            foreach (var name in new[] { "map_coast", "map_forest", "map_forest.001", "map_sea" })
            {
                var partSource = (name == "map_sea" ? waterSource : source).transform.Find(name);
                var part = CopyMesh(partSource, scenery, name);
                part.localPosition = partSource.localPosition - sourceTiles.localPosition;
                if (name == "map_sea")
                {
                    var position = part.localPosition; position.y = tsunami.SeaLevel - scenery.localPosition.y; part.localPosition = position;
                    part.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            // The supplied map already contains the garden props around its tiles.
            foreach (var filter in tsunami.BuildingsRoot.GetComponentsInChildren<MeshFilter>())
                if (filter.name.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) >= 0 && filter.TryGetComponent<MeshRenderer>(out var renderer))
                { renderer.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }

            var existingSea = tsunami.transform.Find("Sea_Surface");
            if (existingSea != null) existingSea.GetComponent<MeshRenderer>().enabled = false;
            const string floodMaterialPath = "Assets/Art/Effects/Tsunami/MapFloodWater.mat";
            var floodMaterial = AssetDatabase.LoadAssetAtPath<Material>(floodMaterialPath);
            if (floodMaterial == null)
            {
                floodMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/Tsunami/TsunamiWater.mat"));
                floodMaterial.name = "MapFloodWater";
                AssetDatabase.CreateAsset(floodMaterial, floodMaterialPath);
            }
            var seaColor = waterSource.transform.Find("map_sea").GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor");
            floodMaterial.SetColor("_DeepColor", new Color(seaColor.r * .65f, seaColor.g * .72f, seaColor.b * .75f, 1f));
            floodMaterial.SetColor("_ShallowColor", seaColor);
            floodMaterial.SetColor("_FoamColor", waterSource.transform.Find("TSUNAMI").GetComponent<MeshRenderer>().sharedMaterials[1].GetColor("_BaseColor"));
            EditorUtility.SetDirty(floodMaterial);
            data.FindProperty("waterMaterial").objectReferenceValue = floodMaterial;
            data.ApplyModifiedPropertiesWithoutUndo();
            var wave = tsunami.transform.Find("Authored_Tsunami_Wave");
            if (wave == null) { wave = new GameObject("Authored_Tsunami_Wave").transform; wave.SetParent(tsunami.transform, false); }
            var waveSource = waterSource.transform.Find("TSUNAMI");
            var geometry = CopyMesh(waveSource, wave, "TSUNAMI");
            var bounds = geometry.GetComponent<MeshFilter>().sharedMesh.bounds;
            var seaInMesh = (waterSource.transform.Find("map_sea").localPosition.y - waveSource.localPosition.y) / waveSource.localScale.y;
            geometry.localRotation = Quaternion.Euler(0f, 180f, 0f);
            geometry.localScale = waveSource.localScale;
            geometry.localPosition = new Vector3(bounds.center.x * waveSource.localScale.x,
                -seaInMesh * waveSource.localScale.y, bounds.min.z * waveSource.localScale.z);
            var surface = geometry.GetComponent<MeshRenderer>(); surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
            var originalMaterials = waveSource.GetComponent<MeshRenderer>().sharedMaterials;
            surface.sharedMaterials = new[]
            {
                OpaqueWaveMaterial(originalMaterials[0], "Assets/Art/Effects/Tsunami/AuthoredWaveWater.mat"),
                OpaqueWaveMaterial(originalMaterials[1], "Assets/Art/Effects/Tsunami/AuthoredWaveFoam.mat")
            };
            var controller = wave.GetComponent<AuthoredTsunamiWave>();
            if (controller == null) controller = wave.gameObject.AddComponent<AuthoredTsunamiWave>();
            var settings = new SerializedObject(controller);
            settings.FindProperty("surface").objectReferenceValue = surface;
            settings.ApplyModifiedPropertiesWithoutUndo();
            wave.localPosition = controller.StartPosition;
            wave.localRotation = Quaternion.identity; wave.localScale = Vector3.one; wave.gameObject.SetActive(false);
            data.FindProperty("authoredWave").objectReferenceValue = controller;
            data.FindProperty("startDistance").floatValue = controller.StartPosition.z;
            data.ApplyModifiedPropertiesWithoutUndo();
            var worldCamera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
            worldCamera.farClipPlane = Mathf.Max(worldCamera.farClipPlane, 240f);
            var monster = (SeaMonsterEmergence)data.FindProperty("seaMonster").objectReferenceValue;
            var approach = data.FindProperty("shorelineDistance").floatValue - data.FindProperty("startDistance").floatValue;
            var monsterData = new SerializedObject(monster);
            monsterData.FindProperty("waveApproachOffset").vector3Value = monster.Model.parent.InverseTransformVector(tsunami.transform.TransformVector(Vector3.back * approach));
            monsterData.FindProperty("waveApproachDuration").floatValue = Mathf.Min(approach / controller.MovementSpeed,
                Mathf.Max(.01f, monster.RiseDuration - monster.WaveReleaseDelay));
            monsterData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(tsunami.gameObject.scene);
            if (!EditorSceneManager.SaveScene(tsunami.gameObject.scene)) throw new InvalidOperationException("Could not save map art.");
            Debug.Log("Saved new map, tiles and tsunami mesh with the existing gameplay objects.");
        }

        private static Material OpaqueWaveMaterial(Material source, string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
            else material.CopyPropertiesFromMaterial(source);
            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AuthoredTsunamiWave.ConfigureOpaqueMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform CopyMesh(Transform source, Transform parent, string name)
        {
            if (source == null) throw new InvalidOperationException("Missing supplied map part: " + name);
            var node = parent.Find(name);
            if (node == null) { node = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform; node.SetParent(parent, false); }
            node.localPosition = source.localPosition; node.localRotation = source.localRotation; node.localScale = source.localScale;
            node.GetComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            node.GetComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            return node;
        }
    }
}
