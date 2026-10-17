using CivilGameJam;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardUpgradePresentationEditModeTests
{
    [Test]
    public void HousePresentationReferencesAndRailSettingsAreSavedInTheScene()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var presentation = Object.FindFirstObjectByType<HouseUpgradePresentation>();
        Assert.That(presentation, Is.Not.Null);
        var data = new SerializedObject(presentation);
        Assert.That(data.FindProperty("game").objectReferenceValue, Is.SameAs(Object.FindFirstObjectByType<CardGameController>()));
        Assert.That(data.FindProperty("dock").objectReferenceValue, Is.SameAs(Object.FindFirstObjectByType<CardDock>()));
        Assert.That(data.FindProperty("navigation").objectReferenceValue, Is.SameAs(Object.FindFirstObjectByType<CityCameraController>()));
        Assert.That(data.FindProperty("zoomFactor").floatValue, Is.EqualTo(1.12f).Within(0.0001f));
        foreach (var link in Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None))
        {
            Assert.That(new SerializedObject(link).FindProperty("presentation").objectReferenceValue, Is.SameAs(presentation));
            var rail = new SerializedObject(link.RailSystem);
            Assert.That(rail.FindProperty("peakLiftMultiplier").floatValue, Is.EqualTo(2f));
            Assert.That(rail.FindProperty("dustMultiplier").floatValue, Is.EqualTo(1.5f));
            Assert.That(rail.FindProperty("transitionDuration").floatValue, Is.GreaterThan(0.8f));
        }
    }

    [Test]
    public void EveryWorkplacePrefabHasEditableWorkerHoverBeforePlay()
    {
        foreach (var name in new[] { "ResearchCenterCard", "SchoolCard", "FactoryCard" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
            var slots = prefab.GetComponentsInChildren<WorkerSlot>(true);
            Assert.That(slots.Length, Is.EqualTo(2));
            foreach (var slot in slots)
            {
                var data = new SerializedObject(slot);
                Assert.That(data.FindProperty("hoverLift").floatValue, Is.EqualTo(24f));
                Assert.That(data.FindProperty("hoverSeconds").floatValue, Is.EqualTo(0.16f).Within(0.0001f));
                Assert.That(slot.Portrait, Is.Not.Null);
                Assert.That(slot.EntryImage, Is.Not.Null);
            }
        }
    }
}
