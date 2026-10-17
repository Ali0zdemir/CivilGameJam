using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CardShowcaseEditModeTests
{
    [SetUp]
    public void OpenScene() => EditorSceneManager.OpenScene("Assets/2DCardScene.unity");

    [Test]
    public void FullShowcaseGeometryAndMaterialsAreSavedWithTheCardBoard()
    {
        var world = GameObject.Find("World3D").transform;
        var source = EditorSceneManager.OpenScene("Assets/Scenes/BuildingShowcase.unity", OpenSceneMode.Additive);
        try
        {
            var original = source.GetRootGameObjects().Single(root => root.name == "Buildings_3D").transform;
            var imported = world.Find("Buildings_3D");
            Assert.That(imported.GetComponentsInChildren<Renderer>(true).Length,
                Is.EqualTo(original.GetComponentsInChildren<Renderer>(true).Length));
            foreach (var mesh in original.GetComponentsInChildren<MeshFilter>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(mesh.transform, original);
                var copy = imported.Find(path).GetComponent<MeshFilter>();
                Assert.That(copy.sharedMesh, Is.SameAs(mesh.sharedMesh), path);
                Assert.That(copy.transform.rotation, Is.EqualTo(mesh.transform.rotation), path);
                // City groups move as a whole; the models and their internal layout keep their authored size.
                if (mesh.transform.IsChildOf(original.Find("Buildings")))
                {
                    Assert.That(copy.transform.localPosition, Is.EqualTo(mesh.transform.localPosition), path);
                    Assert.That(copy.transform.lossyScale, Is.EqualTo(mesh.transform.lossyScale), path);
                }
                Assert.That(copy.GetComponent<Renderer>().sharedMaterials, Is.EqualTo(mesh.GetComponent<Renderer>().sharedMaterials), path);
            }
            Assert.That(imported.Find("Buildings").childCount, Is.EqualTo(7));
            foreach (Transform group in original.Find("Buildings"))
            {
                var copy = imported.Find("Buildings/" + group.name);
                Assert.That(copy.localPosition.x, Is.EqualTo(group.localPosition.x));
                Assert.That(copy.localPosition.y, Is.EqualTo(group.localPosition.y));
                Assert.That(copy.localPosition.z, Is.EqualTo(group.localPosition.z * 1.152f).Within(0.0001f));
            }
            float addedDepth = 1.95f * (1.152f - 1f);
            foreach (string name in new[] { "Continuous_Ground", "Island_Base" })
            {
                Assert.That(imported.Find("City_Environment/" + name).localScale.x,
                    Is.EqualTo(original.Find("City_Environment/" + name).localScale.x));
                Assert.That(imported.Find("City_Environment/" + name).localScale.z,
                    Is.EqualTo(original.Find("City_Environment/" + name).localScale.z + addedDepth).Within(0.0001f));
            }
        }
        finally { EditorSceneManager.CloseScene(source, true); }
    }

    [Test]
    public void ImportedScriptsAndAssetReferencesResolveBeforePlay()
    {
        var world = GameObject.Find("World3D");
        foreach (var transform in world.GetComponentsInChildren<Transform>(true))
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
        foreach (var component in world.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var data = new SerializedObject(component);
            var property = data.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceInstanceIDValue != 0)
                    Assert.That(property.objectReferenceValue, Is.Not.Null, component.name + ": " + property.propertyPath);
        }
        foreach (var material in world.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials))
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.Not.Null, material.name);
        }
        Assert.That(world.GetComponentsInChildren<EarthquakeTestController>().Length, Is.EqualTo(1));
        Assert.That(world.GetComponentsInChildren<FireTestController>().Length, Is.EqualTo(1));
        Assert.That(world.GetComponentsInChildren<TsunamiTestController>().Length, Is.EqualTo(1));
        Assert.That(world.GetComponentsInChildren<BuildingRailUpgrade>().Length, Is.EqualTo(4));
        Assert.That(world.GetComponentsInChildren<HouseColumnUpgrade>().Length, Is.EqualTo(4));
        Assert.That(world.GetComponentsInChildren<HouseSprinklerUpgrade>().Length, Is.EqualTo(4));
        Assert.That(world.GetComponentsInChildren<TsunamiBreakwater>().Length, Is.EqualTo(1));
    }

    [Test]
    public void CombinedSceneHasOneInputSystemAndSeparatesCityNavigationFromTheCards()
    {
        var world = GameObject.Find("World3D");
        var view = Object.FindFirstObjectByType<CityViewport>();
        var navigation = world.GetComponentInChildren<CityCameraController>();
        Assert.That(view.FramesWorldAutomatically, Is.False);
        Assert.That(view.WorldCamera, Is.SameAs(navigation.GetComponentInChildren<Camera>()));
        Assert.That(navigation.InputViewport, Is.SameAs(view.ViewRect));
        Assert.That(world.GetComponent<CardCityNavigation>(), Is.Not.Null);
        Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.enabled), Is.EqualTo(1));
        var controls = view.GetComponentsInChildren<Canvas>(true);
        Assert.That(controls.Length, Is.EqualTo(6));
        foreach (var control in controls)
        {
            Assert.That(control.gameObject.activeInHierarchy, Is.True, "Test controls must be visible before Play.");
            Assert.That(control.overrideSorting, Is.False, "Blueprint selection must remain above the test controls.");
            Assert.That(control.rootCanvas, Is.SameAs(view.GetComponentInParent<Canvas>().rootCanvas));
            foreach (var graphic in control.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.gameObject.layer, Is.EqualTo(control.rootCanvas.gameObject.layer),
                    "The display camera must render the test graphics: " + graphic.name);
        }
        Assert.That(view.GetComponent<CanvasGroup>(), Is.Not.Null);
    }
}
