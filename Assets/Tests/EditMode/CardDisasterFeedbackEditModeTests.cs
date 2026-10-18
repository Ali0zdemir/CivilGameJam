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
        var sound = (AudioClip)data.FindProperty("collapseSound").objectReferenceValue;
        Assert.That(sound, Is.Not.Null);
        var soundPath = AssetDatabase.GetAssetPath(sound);
        Assert.That(soundPath, Is.EqualTo("Assets/Art/BinaYıkılmaSound/afet sonucu bina yıkılma sesi 4.wav"));
        Assert.That(data.FindProperty("collapseSoundVolume").floatValue, Is.EqualTo(.12f).Within(.00001f));
        var importer = (AudioImporter)AssetImporter.GetAtPath(soundPath);
        Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
        Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
        Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.True);
        Assert.That(importer.loadInBackground, Is.False);
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
        foreach (var surface in new[] { "Water", "Land" })
        {
            var sounds = data.FindProperty(surface.ToLowerInvariant() + "Sounds");
            Assert.That(sounds.arraySize, Is.EqualTo(4));
            for (var i = 0; i < sounds.arraySize; i++)
            {
                var sound = (AudioClip)sounds.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(sound, Is.Not.Null);
                var path = AssetDatabase.GetAssetPath(sound);
                Assert.That(path, Does.StartWith("Assets/Art/GodzillaStep" + surface + "/"));
                var settings = ((AudioImporter)AssetImporter.GetAtPath(path)).defaultSampleSettings;
                Assert.That(settings.preloadAudioData, Is.True);
                Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
                Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
            }
        }
        data = new SerializedObject(Object.FindFirstObjectByType<CityCameraController>());
        Assert.That(data.FindProperty("closestZoom").floatValue, Is.EqualTo(1.5f));
        Assert.That(data.FindProperty("farthestZoom").floatValue, Is.EqualTo(3f));
        Assert.That(((Camera)data.FindProperty("controlledCamera").objectReferenceValue).orthographicSize, Is.EqualTo(3f));
        var links = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None);
        Assert.That(links.Length, Is.EqualTo(4));
        Assert.That(links.Select(link => link.Building.GetComponent<BuildingCollapseFeedback>()).All(item => item != null), Is.True);
    }
}
