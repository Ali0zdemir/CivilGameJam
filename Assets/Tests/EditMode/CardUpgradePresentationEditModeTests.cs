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
        Assert.That(data.FindProperty("zoomFactor").floatValue, Is.EqualTo(2.2f).Within(0.0001f));
        Assert.That(data.FindProperty("closeupMagnification").floatValue, Is.EqualTo(1.5f));
        foreach (var link in Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None))
        {
            Assert.That(new SerializedObject(link).FindProperty("presentation").objectReferenceValue, Is.SameAs(presentation));
            var rail = new SerializedObject(link.RailSystem);
            Assert.That(rail.FindProperty("peakLiftMultiplier").floatValue, Is.EqualTo(2f));
            Assert.That(rail.FindProperty("dustMultiplier").floatValue, Is.EqualTo(2.25f));
            Assert.That(rail.FindProperty("transitionDuration").floatValue, Is.GreaterThan(0.8f));
            Assert.That(rail.FindProperty("settlingDuration").floatValue, Is.EqualTo(.7f).Within(.0001f));
            Assert.That(rail.FindProperty("dustLightening").floatValue, Is.EqualTo(.28f).Within(.0001f));
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
                var padding = slot.GetComponent<UnityEngine.UI.Image>().raycastPadding;
                Assert.That(padding.x, Is.GreaterThan(-10f), "Transparent left margins must not steal the adjacent portrait's hover.");
                Assert.That(padding.z, Is.LessThan(-1f), "Cover the visible right edge outside the original slot.");
                Assert.That(padding.w, Is.LessThanOrEqualTo(-24f), "Keep the raised artwork interactive.");
                Assert.That(padding.y, Is.Zero, "The pocket must not take clicks below its folder flap.");
            }
        }
    }
}
