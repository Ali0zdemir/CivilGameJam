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
        private const string SourcePath = CardTileMapAuthoring.TileAsset;

        [MenuItem("Tools/Card Game/Save Authored Map And Tsunami")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save map art in Edit Mode.");
            CardTileMapAuthoring.SaveForBatch();
        }

        internal static void Configure(TsunamiTestController tsunami, Bounds cityBounds)
        {
            var data = new SerializedObject(tsunami);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var waterSource = source;
            var map = tsunami.GroundSurface.parent;
            if (map.name != "Map(Guncel1)" || !map.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The real Map(Guncel1) instance must be the active main map.");
            var previousScenery = tsunami.transform.Find("Map_Scenery");
            if (previousScenery != null) previousScenery.gameObject.SetActive(false);
            foreach (var name in new[] { "map_coast", "map_forest", "map_forest.001", "map_sea" })
            {
                var part = map.Find(name);
                if (part == null) throw new InvalidOperationException("Missing main-map part: " + name);
                part.gameObject.SetActive(true);
                if (name == "map_sea")
                {
                    var position = tsunami.transform.InverseTransformPoint(part.position);
                    position.y = tsunami.SeaLevel; part.position = tsunami.transform.TransformPoint(position);
                    part.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            if (map.Find("Gojira") != null) map.Find("Gojira").gameObject.SetActive(false);
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
            var waveSource = waterSource.transform.Find("TSUNAMI");
            var wave = map.Find("TSUNAMI");
            if (wave == null) throw new InvalidOperationException("The main map must contain its own TSUNAMI object.");
            var surface = wave.GetComponent<MeshRenderer>();
            surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
            var originalMaterials = waveSource.GetComponent<MeshRenderer>().sharedMaterials;
            surface.sharedMaterials = new[]
            {
                OpaqueWaveMaterial(originalMaterials[0], "Assets/Art/Effects/Tsunami/AuthoredWaveWater.mat"),
                OpaqueWaveMaterial(originalMaterials[1], "Assets/Art/Effects/Tsunami/AuthoredWaveFoam.mat")
            };
            var controller = wave.GetComponent<AuthoredTsunamiWave>();
            var firstSetup = controller == null;
            if (firstSetup) controller = wave.gameObject.AddComponent<AuthoredTsunamiWave>();
            var previous = (AuthoredTsunamiWave)data.FindProperty("authoredWave").objectReferenceValue;
            if (previous != null && previous != controller) previous.gameObject.SetActive(false);
            var settings = new SerializedObject(controller);
            settings.FindProperty("surface").objectReferenceValue = surface;
            settings.FindProperty("simulationFrame").objectReferenceValue = tsunami.transform;
            if (firstSetup)
            {
                // The scene object was moved to demonstrate the destination; retain the asset's offshore start.
                settings.FindProperty("startPosition").vector3Value = waveSource.localPosition;
                settings.FindProperty("movementSpeed").floatValue = 1f;
            }
            var start = settings.FindProperty("startPosition").vector3Value;
            settings.FindProperty("endPosition").vector3Value = new Vector3(-21.3f, start.y, -26.53f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            wave.localPosition = controller.StartPosition;
            wave.gameObject.SetActive(false);
            data.FindProperty("authoredWave").objectReferenceValue = controller;
            data.FindProperty("startDistance").floatValue = controller.StartDistance;
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
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
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
