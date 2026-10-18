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
    public static class CardImportedFireAuthoring
    {
        public const string SourcePath = "Assets/emreAnimation/Smokes_and_Flames.fbx";
        public const string Folder = "Assets/Art/Effects/Fire/Imported";
        public const string PresetPath = Folder + "/GojiraFire.asset";

        [MenuItem("Tools/Disasters/Use Emre Flames and Smokes")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save the fire in Edit Mode.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new FileNotFoundException("Import Smokes_and_Flames first.", SourcePath);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/Effects/Fire", "Imported");
            var filters = source.GetComponentsInChildren<MeshFilter>();
            var flames = Enumerable.Range(1, 4).Select(index => SaveMesh(filters.Single(part => part.name == "flame" + index), false)).ToArray();
            var smokes = Enumerable.Range(1, 3).Select(index => SaveMesh(filters.Single(part => part.name == "smoke" + index), true)).ToArray();
            var flameMaterial = SaveMaterial("ImportedFlames", .1f, .3f, .2f);
            var smokeMaterial = SaveMaterial("ImportedBlackSmoke", .75f, 0f, .35f);
            var sourceClip = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .Single(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            var animatedFlame = filters.Single(part => part.name == "flame1");
            var animationPrefab = SaveBreathAnimation(animatedFlame, flameMaterial);
            var scaleCurve = AnimationUtility.GetEditorCurve(sourceClip,
                EditorCurveBinding.FloatCurve("flame1", typeof(Transform), "m_LocalScale.y"));
            var maximumScale = scaleCurve.keys.Max(key => key.value);
            var activeKeys = scaleCurve.keys.Where(key => key.value > maximumScale * .025f).ToArray();
            var animationRange = new Vector2(Mathf.Max(0f, activeKeys.First().time - 1f / sourceClip.frameRate),
                Mathf.Min(sourceClip.length, activeKeys.Last().time + 1f / sourceClip.frameRate));
            var preset = AssetDatabase.LoadAssetAtPath<FireVisualPreset>(PresetPath);
            if (preset == null) { preset = ScriptableObject.CreateInstance<FireVisualPreset>(); AssetDatabase.CreateAsset(preset, PresetPath); }
            var data = new SerializedObject(preset);
            SetArray(data, "flameMeshes", flames);
            SetArray(data, "smokeMeshes", smokes);
            data.FindProperty("flameMaterial").objectReferenceValue = flameMaterial;
            data.FindProperty("smokeMaterial").objectReferenceValue = smokeMaterial;
            data.FindProperty("sequenceSeed").longValue = 7351;
            data.FindProperty("breathAnimationPrefab").objectReferenceValue = animationPrefab;
            data.FindProperty("breathAnimation").objectReferenceValue = sourceClip;
            data.FindProperty("breathAnimationRange").vector2Value = animationRange;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset);

            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var fire = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FireTestController>(true)).Single();
            var controller = new SerializedObject(fire);
            controller.FindProperty("visualPreset").objectReferenceValue = preset;
            controller.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the fire preset reference.");
            AssetDatabase.SaveAssets();
            Debug.Log("Saved one repeatable Gojira fire composition using all four flames and all three black smokes.");
        }

        private static GameObject SaveBreathAnimation(MeshFilter source, Material material)
        {
            var root = new GameObject("Authored_Flame_Animation");
            try
            {
                var child = new GameObject("flame1", typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(root.transform, false);
                child.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
                child.transform.localScale = source.transform.localScale;
                child.GetComponent<MeshFilter>().sharedMesh = SaveMesh(source, false, false);
                var renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/GojiraBreathAnimation.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Mesh SaveMesh(MeshFilter filter, bool smoke, bool normalize = true)
        {
            var source = filter.sharedMesh;
            var points = source.vertices;
            var normals = source.normals;
            var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
            var bounds = source.bounds;
            // The FBX lays the variants out in a row, with different exported scales. Use their own bounds.
            var pivot = !normalize ? Vector3.zero : smoke ? bounds.center : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var scale = !normalize ? 1f : 1f / (smoke ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : bounds.size.y);
            var vertices = new List<Vector3>();
            var directions = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();
            for (var submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var color = materials[submesh].GetColor("_BaseColor");
                foreach (var index in source.GetTriangles(submesh))
                {
                    triangles.Add(vertices.Count);
                    vertices.Add((points[index] - pivot) * scale);
                    directions.Add(normals[index]);
                    colors.Add(color);
                }
            }
            var mesh = new Mesh { name = (normalize ? "Imported_" : "Animated_") + filter.name };
            mesh.SetVertices(vertices); mesh.SetNormals(directions); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return SaveAsset(mesh, Folder + "/" + (normalize ? filter.name : "Animated_" + filter.name) + ".asset");
        }

        private static Material SaveMaterial(string name, float lighting, float glow, float softness)
        {
            var material = new Material(Shader.Find("CivilGameJam/LowPolyFire")) { name = name };
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Lighting", lighting); material.SetFloat("_Glow", glow); material.SetFloat("_EdgeSoftness", softness);
            return SaveAsset(material, Folder + "/" + name + ".mat");
        }

        private static T SaveAsset<T>(T value, string path) where T : Object
        {
            var saved = AssetDatabase.LoadAssetAtPath<T>(path);
            if (saved == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, saved); Object.DestroyImmediate(value); EditorUtility.SetDirty(saved); return saved;
        }

        private static void SetArray(SerializedObject data, string field, Mesh[] meshes)
        {
            var array = data.FindProperty(field); array.arraySize = meshes.Length;
            for (var i = 0; i < meshes.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = meshes[i];
        }
    }
}
