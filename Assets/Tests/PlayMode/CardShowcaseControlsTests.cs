using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    private static T ControlField<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    private static void AssertTopButton(Button button, Camera uiCamera)
    {
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current)
        { position = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty, button.name);
        Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), button.name + " must receive clicks.");
    }

    [UnityTest]
    public IEnumerator SavedShowcaseControlsReceiveClicksWithoutMovingTheCityOrBuildingRuntimeUI()
    {
        var city = Object.FindFirstObjectByType<CityViewport>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        navigation.enabled = false;
        var uiBefore = city.GetComponentInParent<Canvas>().GetComponentsInChildren<RectTransform>(true).Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray();
        string imagePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/showcase-visible-controls.png"));
        yield return CaptureCanvas(imagePath, afterRender: uiCamera =>
        {
            foreach (var controller in new MonoBehaviour[] {
                Object.FindFirstObjectByType<EarthquakeTestController>(), Object.FindFirstObjectByType<FireTestController>(),
                Object.FindFirstObjectByType<TsunamiTestController>() })
            {
                var button = ControlField<Button>(controller, "testButton");
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
                AssertTopButton(button, uiCamera);
                var rect = (RectTransform)button.transform;
                var point = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
                float zoom = city.WorldCamera.orthographicSize;
                Vector2 pan = navigation.PanOffset;
                InvokeCity(navigation, "ReadPointer", point, 4f, true, true);
                InvokeCity(navigation, "ReadPointer", point + Vector2.right * 15f, 4f, false, true);
                InvokeCity(navigation, "AdvanceNavigation", 1f);
                Assert.That(navigation.PanOffset, Is.EqualTo(pan));
                Assert.That(city.WorldCamera.orthographicSize, Is.EqualTo(zoom).Within(0.001f));
                button.onClick.Invoke();
                Assert.That((bool)controller.GetType().GetProperty("IsRunning").GetValue(controller), Is.True);
            }
            Object.FindFirstObjectByType<EarthquakeTestController>().StopEarthquake();
            Object.FindFirstObjectByType<FireTestController>().StopFire();
            Object.FindFirstObjectByType<TsunamiTestController>().StopTsunami();
            foreach (var controller in new MonoBehaviour[] {
                Object.FindFirstObjectByType<RailSystemTestPanel>(), Object.FindFirstObjectByType<HouseSprinklerTestPanel>(),
                Object.FindFirstObjectByType<HouseColumnsTestPanel>() })
            {
                var panel = ControlField<RectTransform>(controller, "panelRect");
                var header = panel.Find("Collapse_Expand").GetComponent<Button>();
                var body = ControlField<GameObject>(controller, "panelBody");
                AssertTopButton(header, uiCamera);
                Assert.That(body.activeSelf, Is.False);
                header.onClick.Invoke();
                Assert.That(body.activeSelf, Is.True);
                var fields = (System.Array)controller.GetType().GetField("buildings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                var row = fields.GetValue(0);
                var toggle = (Button)row.GetType().GetField("toggleButton").GetValue(row);
                toggle.onClick.Invoke();
                Assert.That((int)controller.GetType().GetProperty("InstalledCount").GetValue(controller), Is.EqualTo(1));
                ControlField<Button>(controller, "resetButton").onClick.Invoke();
                Assert.That((int)controller.GetType().GetProperty("InstalledCount").GetValue(controller), Is.Zero);
                header.onClick.Invoke();
                Assert.That(body.activeSelf, Is.False);
            }
        });
        Assert.That(city.GetComponentInParent<Canvas>().GetComponentsInChildren<RectTransform>(true).Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray(),
            Is.EqualTo(uiBefore));
        foreach (var label in city.GetComponentsInChildren<Text>(true))
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(label.text, "[ıİğĞşŞüÜöÖçÇ]|%|[0-9]+\\s*(seconds?|saniye)"), Is.False, label.text);
    }

    [UnityTest]
    public IEnumerator BlueprintChoiceBlocksTheVisibleShowcaseControls()
    {
        research.TryAssign(People()[0]); research.AdvanceWork(25.01f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        string imagePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/showcase-controls-blueprint-modal.png"));
        yield return CaptureCanvas(imagePath, afterRender: uiCamera =>
        {
            foreach (var controller in new MonoBehaviour[] {
                Object.FindFirstObjectByType<EarthquakeTestController>(), Object.FindFirstObjectByType<FireTestController>(),
                Object.FindFirstObjectByType<TsunamiTestController>() })
            {
                var button = ControlField<Button>(controller, "testButton");
                var rect = (RectTransform)button.transform;
                var pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center)) };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(button.IsInteractable(), Is.False);
                Assert.That(hits.Any(hit => hit.gameObject.GetComponentInParent<Button>() == button), Is.False,
                    "Blueprint selection must block test-button clicks.");
            }
        });
    }
}
