using System.Linq;
using CivilGameJam;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardDisasterFeedbackEditModeTests
{
    [TestCase("House")]
    [TestCase("Apartment_02")]
    [TestCase("Apartment_03")]
    [TestCase("Apartment_04")]
    public void HousePrefabsSaveConstructionDustForBothCollapseAnimations(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Buildings/Prefabs/" + name + ".prefab");
        var feedback = prefab.GetComponent<BuildingCollapseFeedback>();
        Assert.That(feedback, Is.Not.Null);
        var data = new SerializedObject(feedback);
        Assert.That(data.FindProperty("geometry").objectReferenceValue, Is.SameAs(prefab.GetComponent<BuildingModelGeometry>()));
        Assert.That(data.FindProperty("constructionDustMaterial").objectReferenceValue, Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/Construction/ConstructionDust.mat")));
        foreach (var component in new Component[] { prefab.GetComponent<ApartmentEarthquakeAnimation>(), prefab.GetComponent<ApartmentTsunamiCollapse>() })
            Assert.That(new SerializedObject(component).FindProperty("collapseFeedback").objectReferenceValue, Is.SameAs(feedback));
    }

    [Test]
    public void CardSceneSavesMonsterFootfallReferencesAndPreservesItsClosestZoom()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        var feedback = walk.GetComponent<MonsterFootstepFeedback>();
        Assert.That(feedback, Is.Not.Null);
        Assert.That(new SerializedObject(walk).FindProperty("footstepFeedback").objectReferenceValue, Is.SameAs(feedback));
        var data = new SerializedObject(feedback);
        Assert.That(data.FindProperty("tsunami").objectReferenceValue, Is.SameAs(Object.FindFirstObjectByType<TsunamiTestController>()));
        Assert.That(data.FindProperty("seaSurface").objectReferenceValue, Is.Not.Null);
        data = new SerializedObject(Object.FindFirstObjectByType<CityCameraController>());
        Assert.That(data.FindProperty("closestZoom").floatValue, Is.EqualTo(1.5f));
        Assert.That(data.FindProperty("farthestZoom").floatValue, Is.EqualTo(4.9f));
        Assert.That(((Camera)data.FindProperty("controlledCamera").objectReferenceValue).orthographicSize, Is.EqualTo(3.6f));
        var links = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None);
        Assert.That(links.Length, Is.EqualTo(4));
        Assert.That(links.Select(link => link.Building.GetComponent<BuildingCollapseFeedback>()).All(item => item != null), Is.True);
    }
}
