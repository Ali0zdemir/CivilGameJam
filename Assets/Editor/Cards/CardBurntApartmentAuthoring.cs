using System;
using System.IO;
using System.Linq;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEngine;

namespace CivilGameJam.Cards.Editor
{
    public static class CardBurntApartmentAuthoring
    {
        public const string SourcePath = "Assets/emreAnimation/Apartment(Burnt).fbx";

        [MenuItem("Tools/Disasters/Use Emre Burnt Apartments")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save burnt apartments in Edit Mode.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new FileNotFoundException("Import Apartment(Burnt) first.", SourcePath);
            var sources = source.GetComponentsInChildren<MeshFilter>();
            foreach (var name in new[] { "House", "Apartment_02", "Apartment_03", "Apartment_04" })
            {
                var path = "Assets/Art/Buildings/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var parts = root.GetComponent<BuildingModelGeometry>().Parts;
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var burnt = sources.Single(filter => filter.name == "Apartment" + (i + 1) + "(Burnt)");
                        var appearance = parts[i].GetComponent<ApartmentBurntAppearance>();
                        if (appearance == null) appearance = parts[i].gameObject.AddComponent<ApartmentBurntAppearance>();
                        var geometry = new SerializedObject(parts[i]);
                        var data = new SerializedObject(appearance);
                        data.FindProperty("burntTilt").quaternionValue = burnt.transform.localRotation;
                        var variants = data.FindProperty("variants"); variants.arraySize = 2;
                        var materials = burnt.GetComponent<MeshRenderer>().sharedMaterials;
                        for (var variant = 0; variant < 2; variant++)
                        {
                            var item = variants.GetArrayElementAtIndex(variant);
                            item.FindPropertyRelative("body").objectReferenceValue = geometry.FindProperty(variant == 0 ? "modelMesh" : "elevatedMesh").objectReferenceValue;
                            // Elevated bodies retain their authored open ground floor and separate supports.
                            item.FindPropertyRelative("burntMesh").objectReferenceValue = variant == 0 ? burnt.sharedMesh : null;
                            var slots = item.FindPropertyRelative("burntMaterials"); slots.arraySize = materials.Length;
                            for (var slot = 0; slot < materials.Length; slot++) slots.GetArrayElementAtIndex(slot).objectReferenceValue = materials[slot];
                        }
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the six original burnt apartment meshes, materials and tilts on all four residential prefabs.");
        }
    }
}
