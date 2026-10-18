using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardAbilityFeedbackEditModeTests
{
    [Test]
    public void APFeedbackAndCostWarningsAreAuthoredWithPixellariAndWithoutBlockingInput()
    {
        var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        var hud = all.OfType<AbilityPointFeedback>().Single();
        var data = new SerializedObject(hud);
        var sound = (AudioClip)data.FindProperty("notEnoughSound").objectReferenceValue;
        Assert.That(sound, Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/not enough_.wav")));
        var audioSettings = ((AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sound))).defaultSampleSettings;
        Assert.That(audioSettings.preloadAudioData, Is.True);
        Assert.That(audioSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
        Assert.That(data.FindProperty("bank").objectReferenceValue, Is.SameAs(all.OfType<AbilityPoints>().Single()));
        var round = new SerializedObject(all.OfType<EarthquakeRoundController>().Single());
        Assert.That(round.FindProperty("abilityFeedback").objectReferenceValue, Is.SameAs(hud));
        foreach (string name in new[] { "counterGlow", "readyGlow" })
        {
            var image = (Image)data.FindProperty(name).objectReferenceValue;
            Assert.That(image, Is.Not.Null);
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.gameObject.activeSelf, Is.False);
        }
        var labels = data.FindProperty("spendLabels");
        Assert.That(labels.arraySize, Is.EqualTo(4));
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
        for (int i = 0; i < labels.arraySize; i++)
        {
            var label = (TMP_Text)labels.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(label, Is.Not.Null);
            Assert.That(label.font, Is.SameAs(font));
            Assert.That(label.raycastTarget || label.gameObject.activeSelf, Is.False);
        }
        foreach (string name in new[] { "FactoryCard", "ResearchCenterCard", "SchoolCard" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
            var station = new SerializedObject(root.GetComponent<BuildingStation>());
            foreach (string kind in new[] { "normal", "educated" })
            {
                var feedback = (AbilityPointCostFeedback)station.FindProperty(kind + "CostFeedback").objectReferenceValue;
                Assert.That(feedback, Is.Not.Null);
                var badge = new SerializedObject(feedback);
                Assert.That(badge.FindProperty("label").objectReferenceValue, Is.SameAs(station.FindProperty(kind + "CostLabel").objectReferenceValue));
                Assert.That(badge.FindProperty("badge").objectReferenceValue, Is.SameAs(feedback.transform));
            }
        }
    }
}
