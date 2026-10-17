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
            Assert.That(imported.Find("City_Environment/Street_Lamps"), Is.Null,
                "The card scene must preserve the user's removal of the street lamps.");
            foreach (var mesh in original.GetComponentsInChildren<MeshFilter>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(mesh.transform, original);
                if (path.StartsWith("City_Environment/Street_Lamps/", System.StringComparison.Ordinal)) continue;
                var copy = imported.Find(path).GetComponent<MeshFilter>();
                if (path == "City_Environment/Continuous_Ground")
                {
                    Assert.That(AssetDatabase.GetAssetPath(copy.sharedMesh), Is.EqualTo("Assets/EfeArts/Map(Guncel1).fbx"));
                    continue;
                }
                if (copy.sharedMesh != mesh.sharedMesh)
                {
                    Assert.That(AssetDatabase.GetAssetPath(copy.sharedMesh), Does.StartWith("Assets/Art/Buildings/TileFoliage/"), path);
                    Assert.That(copy.sharedMesh.vertices, Is.EqualTo(mesh.sharedMesh.vertices), path);
                    Assert.That(copy.sharedMesh.uv, Is.EqualTo(mesh.sharedMesh.uv), path);
                    Assert.That(copy.sharedMesh.normals, Is.EqualTo(mesh.sharedMesh.normals), path);
                    Assert.That(copy.sharedMesh.triangles.Length, Is.LessThan(mesh.sharedMesh.triangles.Length), path);
                }
                else Assert.That(copy.sharedMesh, Is.SameAs(mesh.sharedMesh), path);
                Assert.That(copy.transform.rotation, Is.EqualTo(mesh.transform.rotation), path);
                // City groups move as a whole; the models and their internal layout keep their authored size.
                if (mesh.transform.IsChildOf(original.Find("Buildings")))
                {
                    Assert.That(copy.transform.localPosition, Is.EqualTo(mesh.transform.localPosition), path);
                    Assert.That(copy.transform.lossyScale, Is.EqualTo(mesh.transform.lossyScale), path);
                }
                Assert.That(copy.GetComponent<Renderer>().sharedMaterials, Is.EqualTo(mesh.GetComponent<Renderer>().sharedMaterials), path);
            }
            Assert.That(imported.Find("Buildings").childCount, Is.GreaterThanOrEqualTo(original.Find("Buildings").childCount),
                "Keep the original city buildings as well as the user's added models.");
            foreach (Transform group in original.Find("Buildings"))
            {
                var copy = imported.Find("Buildings/" + group.name);
                Assert.That(copy.localPosition.y, Is.EqualTo(group.localPosition.y));
                var cells = world.GetComponentInChildren<TsunamiTestController>().GroundSurface.Find("Tile_Grid_4x2").Cast<Transform>();
                Assert.That(cells.Any(cell => Vector2.Distance(new Vector2(cell.position.x, cell.position.z),
                    new Vector2(copy.position.x, copy.position.z)) < .0001f), Is.True, copy.name + " must sit on an authored tile.");
            }
            Assert.That(world.GetComponentInChildren<TsunamiTestController>().GroundSurface.Find("Tile_Grid_4x2").childCount, Is.EqualTo(8));
            Assert.That(imported.Find("City_Environment/Island_Base").GetComponent<MeshRenderer>().enabled, Is.False);
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
        Assert.That(controls.Length, Is.EqualTo(7), "Three disaster tests, three protection tests and the sequence menu must be saved before Play.");
        var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
        Assert.That(round, Is.Not.Null);
        var roundData = new SerializedObject(round);
        var ready = (UnityEngine.UI.Button)roundData.FindProperty("readyButton").objectReferenceValue;
        Assert.That(ready, Is.Not.Null, "The authored READY button drives the current game's saved sequence.");
        Assert.That(ready.gameObject.activeInHierarchy, Is.True);
        Assert.That(roundData.FindProperty("sequence").objectReferenceValue,
            Is.SameAs(world.GetComponentInChildren<MonsterSequenceController>()));
        var starts = roundData.FindProperty("debugStartButtons");
        Assert.That(starts.arraySize, Is.EqualTo(8));
        for (int i = 0; i < starts.arraySize; i++)
            Assert.That(starts.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null,
                "READY must reserve every restored debug start control during a real round.");
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
