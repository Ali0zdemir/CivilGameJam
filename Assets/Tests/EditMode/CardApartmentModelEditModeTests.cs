using System.Collections.Generic;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardApartmentModelEditModeTests
{
    [TestCase("House")]
    [TestCase("Apartment_02")]
    [TestCase("Apartment_03")]
    [TestCase("Apartment_04")]
    public void ApartmentVariantsAndOriginalArtAreAuthoredBeforePlay(string name)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Buildings/Prefabs/" + name + ".prefab");
        var model = root.GetComponent<ApartmentUpgradeModel>();
        Assert.That(model, Is.Not.Null);
        var data = new SerializedObject(model);
        foreach (string field in new[] { "motionRoot", "normalModel", "elevatedModel", "normalIsolators", "elevatedIsolators", "elevatorSupports" })
            Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, name + ": " + field);
        Assert.That(model.IsElevated, Is.False);
        Assert.That(root.transform.Find("Apartment_Ground").gameObject.activeSelf, Is.True);
        Assert.That(((Transform)data.FindProperty("normalIsolators").objectReferenceValue).gameObject.activeSelf, Is.False);
        Assert.That(((Transform)data.FindProperty("elevatedIsolators").objectReferenceValue).gameObject.activeSelf, Is.False);
        Assert.That(data.FindProperty("elevatedSupports").arraySize, Is.EqualTo(24));
        var sprinklers = data.FindProperty("sprinklers");
        Assert.That(sprinklers.arraySize, Is.EqualTo(6));
        for (int index = 0; index < sprinklers.arraySize; index++)
        {
            var pair = sprinklers.GetArrayElementAtIndex(index);
            foreach (string field in new[] { "normalHead", "normalBase", "elevatedHead", "elevatedBase" })
            {
                var part = (Transform)pair.FindPropertyRelative(field).objectReferenceValue;
                Assert.That(part, Is.Not.Null, name + ": " + index + " " + field);
                Assert.That(part.gameObject.activeSelf, Is.False, "Sprinklers stay hidden until a fire card is installed.");
                Assert.That(AssetDatabase.GetAssetPath(part.GetComponent<MeshFilter>().sharedMesh), Is.EqualTo("Assets/EfeArts/EmreArts/Apartments (1).fbx"));
            }
        }
        var geometry = root.GetComponent<BuildingModelGeometry>();
        Assert.That(geometry.IsResidential && geometry.CanOpenGroundFloor, Is.True);
        Assert.That(geometry.Parts.Length, Is.EqualTo(6));
        var renderers = new List<MeshRenderer>(); geometry.AppendRenderers(renderers);
        Assert.That(renderers.Count, Is.EqualTo(6));
        foreach (var renderer in renderers)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo("Assets/EfeArts/EmreArts/Apartments (1).fbx"));
            Assert.That(renderer.sharedMaterials.All(m => m != null && m.shader != null), Is.True);
        }
        var supports = (Transform)data.FindProperty("elevatorSupports").objectReferenceValue;
        Assert.That(supports.childCount, Is.EqualTo(6));
        var originalMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/EfeArts/EmreArts/Apartments(Elevated).fbx").OfType<Mesh>().ToArray();
        foreach (var part in geometry.Parts)
        {
            var burnt = part.GetComponent<ApartmentBurntAppearance>();
            Assert.That(burnt, Is.Not.Null);
            Assert.That(burnt.IsBurnt, Is.False);
            var burntData = new SerializedObject(burnt);
            Assert.That(Quaternion.Angle(burntData.FindProperty("burntTilt").quaternionValue, Quaternion.identity), Is.GreaterThan(.1f));
            var burntVariants = burntData.FindProperty("variants");
            Assert.That(burntVariants.arraySize, Is.EqualTo(2));
            Assert.That(AssetDatabase.GetAssetPath(burntVariants.GetArrayElementAtIndex(0).FindPropertyRelative("burntMesh").objectReferenceValue),
                Is.EqualTo("Assets/emreAnimation/Apartment(Burnt).fbx"));
            for (var variant = 0; variant < 2; variant++)
            {
                var item = burntVariants.GetArrayElementAtIndex(variant);
                Assert.That(item.FindPropertyRelative("body").objectReferenceValue, Is.Not.Null);
                var burntMaterials = item.FindPropertyRelative("burntMaterials");
                Assert.That(burntMaterials.arraySize, Is.EqualTo(3));
                for (var slot = 0; slot < burntMaterials.arraySize; slot++)
                    Assert.That(AssetDatabase.GetAssetPath(burntMaterials.GetArrayElementAtIndex(slot).objectReferenceValue),
                        Is.EqualTo("Assets/emreAnimation/Apartment(Burnt).fbx"));
            }
            var surface = new SerializedObject(part);
            Assert.That(surface.FindProperty("elevatedDefinition").objectReferenceValue, Is.Not.Null);
            var body = (MeshFilter)surface.FindProperty("elevatedMesh").objectReferenceValue;
            var original = originalMeshes.Single(mesh => mesh.name == body.name);
            var lower = supports.GetComponentsInChildren<MeshFilter>(true).Single(filter =>
                filter.name.Replace("_Supports", "(Elevated)") == body.name);
            Assert.That(AssetDatabase.GetAssetPath(body.sharedMesh), Does.EndWith("_ElevatorBody.asset"));
            Assert.That(AssetDatabase.GetAssetPath(lower.sharedMesh), Does.EndWith("_ElevatorSupports.asset"));
            Assert.That(lower.transform.localPosition, Is.EqualTo(body.transform.localPosition));
            Assert.That(lower.transform.localRotation, Is.EqualTo(body.transform.localRotation));
            Assert.That(lower.transform.localScale, Is.EqualTo(body.transform.localScale));
            Assert.That(lower.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(body.GetComponent<MeshRenderer>().sharedMaterials));
            foreach (var mesh in new[] { body.sharedMesh, lower.sharedMesh })
            {
                Assert.That(mesh.vertices, Is.EqualTo(original.vertices));
                Assert.That(mesh.uv, Is.EqualTo(original.uv));
                Assert.That(mesh.normals, Is.EqualTo(original.normals));
                Assert.That(mesh.triangles.Length, Is.GreaterThan(0));
                Assert.That(mesh.subMeshCount, Is.EqualTo(original.subMeshCount));
            }
            for (int index = 0; index < original.subMeshCount; index++)
                Assert.That(Triangles(body.sharedMesh.GetTriangles(index)).Concat(Triangles(lower.sharedMesh.GetTriangles(index))).OrderBy(t => t),
                    Is.EqualTo(Triangles(original.GetTriangles(index)).OrderBy(t => t)),
                    "Separating the supports must preserve every original triangle and material assignment.");
            Assert.That(surface.FindProperty("apartmentModel").objectReferenceValue, Is.SameAs(model));
        }
    }

    private static IEnumerable<string> Triangles(int[] triangles)
    {
        for (int i = 0; i < triangles.Length; i += 3)
            yield return triangles[i] + "," + triangles[i + 1] + "," + triangles[i + 2];
    }

    [Test]
    public void AllFourCardTargetsUseTheSavedApartmentsAndElevatorLabels()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var links = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None);
        Assert.That(links.Length, Is.EqualTo(4));
        foreach (var link in links) Assert.That(link.Building.GetComponent<ApartmentUpgradeModel>(), Is.Not.Null);
        Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(text => text.text.Contains("Breakwater")), Is.False);
        var house = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HouseCard.prefab");
        Assert.That(house.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == "Elevated Floor"), Is.True);
    }
}
