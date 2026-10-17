using System.Linq;
using CivilGameJam;
using CivilGameJam.Disasters;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardSequenceResultsEditModeTests
{
    [Test]
    public void CardSceneSavesTheResultModalFontUniqueHouseTargetsAndRetryBinding()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var sequence = Object.FindFirstObjectByType<MonsterSequenceController>();
        var results = sequence.GetComponent<MonsterSequenceResults>();
        Assert.That(results, Is.Not.Null);
        Assert.That(new SerializedObject(sequence).FindProperty("results").objectReferenceValue, Is.SameAs(results));
        var data = new SerializedObject(results);
        Assert.That(data.FindProperty("sequence").objectReferenceValue, Is.SameAs(sequence));
        var overlay = (GameObject)data.FindProperty("overlay").objectReferenceValue;
        Assert.That(overlay.activeSelf, Is.False);
        Assert.That(overlay.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(overlay.GetComponent<Canvas>().overrideSorting, Is.True);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
        Assert.That(overlay.GetComponentsInChildren<TMP_Text>(true).All(label => label.font == font), Is.True);
        Assert.That(overlay.transform.Find("Card/Content/AP_Label").GetComponent<TMP_Text>().text, Is.EqualTo("KAZANILAN AP PUANLARI"));
        var retry = (Button)data.FindProperty("retryButton").objectReferenceValue;
        Assert.That(retry.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(retry.onClick.GetPersistentTarget(0), Is.SameAs(results));
        Assert.That(retry.onClick.GetPersistentMethodName(0), Is.EqualTo("Retry"));
        var houses = data.FindProperty("houses"); Assert.That(houses.arraySize, Is.EqualTo(4));
        var saved = Enumerable.Range(0, houses.arraySize).Select(i => houses.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        Assert.That(saved.Distinct().Count(), Is.EqualTo(4));
        Assert.That(saved, Is.EquivalentTo(Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None)
            .Select(link => link.Building.GetComponent<BuildingModelGeometry>())));
    }
}
