using System.Linq;
using CivilGameJam;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardApartmentEarthquakeEditModeTests
{
    [TestCase("Apartment_Earthquake_Collapse", "Apartment(Default)(Earthquake)", "", "")]
    [TestCase("Apartment_Earthquake_Elevated_Collapse", "Apartment(Default)(Earthquake)", "", "(Elevated)")]
    [TestCase("Apartment_Seismic_Default", "Apartment(Default)(Seismic)", "(Default)(Seismic)", "")]
    [TestCase("Apartment_Seismic_Elevated", "Apartment(Elevated)(Seismic)", "(Elevated)(Seismic)", "(Elevated)")]
    public void SavedEarthquakeClipsPreserveTheAuthoredFbxMovement(string asset, string file, string sourceSuffix, string targetSuffix)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Buildings/Animations/" + asset + ".anim");
        var sourcePath = "Assets/emreAnimation/" + file + ".fbx";
        var sourceClip = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().Single(item => !item.name.StartsWith("__preview"));
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.legacy, Is.True, "Directly sampled house clips must also animate in standalone builds without an Animator.");
        Assert.That(clip.length, Is.EqualTo(sourceClip.length).Within(.001f));
        Assert.That(clip.isLooping, Is.False);
        var bindings = AnimationUtility.GetCurveBindings(clip);
        Assert.That(bindings.Length, Is.EqualTo(60));
        Assert.That(bindings.All(binding => binding.path.StartsWith("Apartment") && !binding.path.Contains("Ground") && !binding.path.Contains("Isolator")), Is.True);
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
        var target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Buildings/Prefabs/House.prefab"));
        try
        {
            var root = target.transform.Find("Apartment_Motion/" + (targetSuffix == "" ? "Apartments_Normal" : "Apartments_Elevated"));
            var sources = Enumerable.Range(1, 6).Select(i => source.transform.Find("Apartment" + i + sourceSuffix)).ToArray();
            var targets = Enumerable.Range(1, 6).Select(i => root.Find("Apartment" + i + targetSuffix)).ToArray();
            sourceClip.SampleAnimation(source, 0f);
            var sourcePositions = sources.Select(item => item.localPosition).ToArray();
            var sourceRotations = sources.Select(item => item.localRotation).ToArray();
            var targetPositions = targets.Select(item => item.localPosition).ToArray();
            var targetRotations = targets.Select(item => item.localRotation).ToArray();
            var ground = target.transform.Find("Apartment_Ground");
            var groundPosition = ground.position;
            foreach (var progress in new[] { 0f, .17f, .41f, .8f, 1f })
            {
                sourceClip.SampleAnimation(source, sourceClip.length * progress);
                clip.SampleAnimation(root.gameObject, clip.length * progress);
                for (var i = 0; i < 6; i++)
                {
                    var expectedPosition = targetPositions[i] + sources[i].localPosition - sourcePositions[i];
                    var expectedRotation = targetRotations[i] * Quaternion.Inverse(sourceRotations[i]) * sources[i].localRotation;
                    Assert.That(Vector3.Distance(targets[i].localPosition, expectedPosition), Is.LessThan(.0001f));
                    Assert.That(Quaternion.Angle(targets[i].localRotation, expectedRotation), Is.LessThan(.1f), $"{asset}, apartment {i + 1}, progress {progress}");
                }
                Assert.That(ground.position, Is.EqualTo(groundPosition));
            }
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(target); }
    }

    [Test]
    public void CardSceneSavesTheFourHouseAnimationsAndMonsterExitBeforePlay()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var links = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None);
        var quake = new SerializedObject(Object.FindFirstObjectByType<EarthquakeTestController>());
        var animations = quake.FindProperty("buildingAnimations");
        Assert.That(animations.arraySize, Is.EqualTo(4));
        Assert.That(Enumerable.Range(0, 4).Select(i => animations.GetArrayElementAtIndex(i).objectReferenceValue),
            Is.EquivalentTo(links.Select(link => link.Building.GetComponent<ApartmentEarthquakeAnimation>())));
        foreach (var link in links)
        {
            var data = new SerializedObject(link.Building.GetComponent<ApartmentEarthquakeAnimation>());
            foreach (var variant in new[] { "normal", "elevated" })
            {
                var setting = data.FindProperty(variant);
                foreach (var field in new[] { "root", "collapse", "seismic" }) Assert.That(setting.FindPropertyRelative(field).objectReferenceValue, Is.Not.Null);
                var parts = setting.FindPropertyRelative("parts"); Assert.That(parts.arraySize, Is.EqualTo(6));
                for (var i = 0; i < 6; i++)
                {
                    var part = parts.GetArrayElementAtIndex(i);
                    Assert.That(part.FindPropertyRelative("body").objectReferenceValue, Is.Not.Null);
                    var attachments = part.FindPropertyRelative("attachments");
                    Assert.That(attachments.arraySize, Is.EqualTo(variant == "normal" ? 3 : 4));
                    for (var j = 0; j < attachments.arraySize; j++) Assert.That(attachments.GetArrayElementAtIndex(j).objectReferenceValue, Is.Not.Null);
                }
            }
        }
        var walk = new SerializedObject(Object.FindFirstObjectByType<SeaMonsterWalk>());
        Assert.That(walk.FindProperty("hideAtAfterEventPoint").boolValue, Is.True);
    }
}
