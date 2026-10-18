using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
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
    public void EveryResultCardAndTutorialWorkBarHasItsOwnSavedBinding()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var view = Object.FindFirstObjectByType<SequenceHouseResultsView>();
        Assert.That(view, Is.Not.Null);
        var data = new SerializedObject(view);
        var cards = data.FindProperty("cards"); Assert.That(cards.arraySize, Is.EqualTo(4));
        var sources = new System.Collections.Generic.List<Object>();
        for (int i = 0; i < cards.arraySize; i++)
        {
            var entry = cards.GetArrayElementAtIndex(i);
            var source = (HouseCityUpgradeLink)entry.FindPropertyRelative("source").objectReferenceValue;
            Assert.That(source.Building, Is.Not.Null); sources.Add(source);
            var icons = entry.FindPropertyRelative("icons"); Assert.That(icons.arraySize, Is.EqualTo(3));
            for (int n = 0; n < icons.arraySize; n++)
                Assert.That(((UpgradeIcon)icons.GetArrayElementAtIndex(n).objectReferenceValue).Kind, Is.EqualTo((UpgradeKind)(n + 1)));
            var report = (TMP_Text)entry.FindPropertyRelative("report").objectReferenceValue;
            Assert.That(report, Is.Not.Null);
            Assert.That(report.font.name, Is.EqualTo("Pixellari SDF"));
            // The designer's result cards are plain UI; they need no draggable component.
            var results = Object.FindFirstObjectByType<MonsterSequenceResults>();
            var overlay = (GameObject)new SerializedObject(results).FindProperty("overlay").objectReferenceValue;
            Assert.That(report.transform.IsChildOf(overlay.transform), Is.True);
            Assert.That(overlay.GetComponentsInChildren<DraggableCard>(true).All(card => !card.enabled), Is.True);
            foreach (var icon in Enumerable.Range(0, icons.arraySize).Select(n => (UpgradeIcon)icons.GetArrayElementAtIndex(n).objectReferenceValue))
                Assert.That(icon.transform.IsChildOf(overlay.transform), Is.True);
            Assert.That(entry.FindPropertyRelative("destroyedMarker").objectReferenceValue, Is.Not.Null);
        }
        Assert.That(sources.Distinct().Count(), Is.EqualTo(4));
        var tutorial = Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include);
        var bars = new SerializedObject(tutorial.Spotlight).FindProperty("progressBars");
        Assert.That(bars.arraySize, Is.EqualTo(3));
        Assert.That(Enumerable.Range(0, 3).Select(i => bars.GetArrayElementAtIndex(i).objectReferenceValue).Where(item => item != null).Distinct().Count(), Is.EqualTo(3));
    }

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
        // The designer can leave the report open in Edit Mode; its Awake closes it at runtime.
        Assert.That(overlay.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(overlay.GetComponent<Canvas>().overrideSorting, Is.True);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
        Assert.That(overlay.GetComponentsInChildren<TMP_Text>(true).All(label => label.font == font), Is.True);
        Assert.That(((TMP_Text)data.FindProperty("earnedPointsLabel").objectReferenceValue).text, Is.EqualTo("AP EARNED"));
        Assert.That(data.FindProperty("earnedPointsCount").objectReferenceValue, Is.Not.Null);
        Assert.That(overlay.transform.Find("Card/Content/House_Statistic/Caption").GetComponent<TMP_Text>().text,
            Is.EqualTo("PROTECTED HOUSES / TOTAL"));
        var retry = (Button)data.FindProperty("retryButton").objectReferenceValue;
        Assert.That(retry.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(retry.onClick.GetPersistentTarget(0), Is.SameAs(results));
        Assert.That(retry.onClick.GetPersistentMethodName(0), Is.EqualTo("Retry"));
        var houses = data.FindProperty("houses"); Assert.That(houses.arraySize, Is.EqualTo(4));
        var saved = Enumerable.Range(0, houses.arraySize).Select(i => houses.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        Assert.That(saved.Distinct().Count(), Is.EqualTo(4));
        Assert.That(saved, Is.EquivalentTo(Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None)
            .Where(link => link.Building != null).Select(link => link.Building.GetComponent<BuildingModelGeometry>())));
    }
}
