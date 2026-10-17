using TMPro;
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
        });
        foreach (var controller in new MonoBehaviour[] {
            Object.FindFirstObjectByType<RailSystemTestPanel>(), Object.FindFirstObjectByType<HouseSprinklerTestPanel>(),
            Object.FindFirstObjectByType<HouseColumnsTestPanel>() })
        {
            var panel = ControlField<RectTransform>(controller, "panelRect");
            var header = panel.Find("Collapse_Expand").GetComponent<Button>();
            var body = ControlField<GameObject>(controller, "panelBody");
            yield return CaptureCanvas(Path.ChangeExtension(imagePath, controller.GetType().Name + "-closed.png"),
                afterRender: uiCamera => AssertTopButton(header, uiCamera));
            Assert.That(body.activeSelf, Is.False);
            header.onClick.Invoke();
            Assert.That(body.activeSelf, Is.True);
            // Opening a panel needs a rendered frame before its newly enabled graphics can be raycast.
            yield return CaptureCanvas(Path.ChangeExtension(imagePath, controller.GetType().Name + "-open.png"), afterRender: uiCamera =>
            {
                var fields = (System.Array)controller.GetType().GetField("buildings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                for (int i = 0; i < fields.Length; i++)
                {
                    var row = fields.GetValue(i);
                    var toggle = (Button)row.GetType().GetField("toggleButton").GetValue(row);
                    AssertTopButton(toggle, uiCamera);
                    toggle.onClick.Invoke();
                    Assert.That((int)controller.GetType().GetProperty("InstalledCount").GetValue(controller), Is.EqualTo(i + 1));
                }
                ControlField<Button>(controller, "resetButton").onClick.Invoke();
                Assert.That((int)controller.GetType().GetProperty("InstalledCount").GetValue(controller), Is.Zero);
                header.onClick.Invoke();
                Assert.That(body.activeSelf, Is.False);
            });
        }
        Assert.That(city.GetComponentInParent<Canvas>().GetComponentsInChildren<RectTransform>(true).Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray(),
            Is.EqualTo(uiBefore));
        foreach (var label in city.GetComponentsInChildren<TMP_Text>(true))
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(label.text, "[ıİğĞşŞüÜöÖçÇ]|%|[0-9]+\\s*(seconds?|saniye)"), Is.False, label.text);
    }

    [UnityTest]
    public IEnumerator RestoredTestStopsAndReadyUseCurrentMechanicsWithoutSpendingOrAwardingAP()
    {
        var rig = new SequencePreview();
        var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
        int points = game.AbilityPoints.CurrentPoints;
        var ids = SequenceUIIds();
        foreach (var controller in new MonoBehaviour[] { rig.Quake, rig.Tsunami, rig.Fire })
        {
            var start = ControlField<Button>(controller, "testButton");
            var stop = start.transform.parent.Find("Stop_Test").GetComponent<Button>();
            for (int replay = 0; replay < 2; replay++)
            {
                start.onClick.Invoke();
                Assert.That((bool)controller.GetType().GetProperty("IsRunning").GetValue(controller), Is.True);
                if (controller == rig.Tsunami)
                {
                    var wave = ControlField<AuthoredTsunamiWave>(rig.Tsunami, "authoredWave");
                    Assert.That(wave.transform.parent.name, Is.EqualTo("Map(Guncel1)"));
                    var before = wave.transform.position;
                    InvokeCity(rig.Tsunami, "AdvanceTsunami", rig.Monster.WaveReleaseDelay + .5f);
                    Assert.That(Vector3.Distance(before, wave.transform.position), Is.GreaterThan(.01f));
                }
                stop.onClick.Invoke();
                Assert.That((bool)controller.GetType().GetProperty("IsRunning").GetValue(controller), Is.False);
                InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
                Assert.That(rig.Navigation.InputBlocked, Is.False);
                Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(points));
                Assert.That(round.RoundNumber, Is.EqualTo(1));
            }
        }
        Assert.That(round.CanReady, Is.True);
        round.Ready(); InvokeCity(round, "LateUpdate");
        Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Earthquake));
        foreach (var button in ControlField<Button[]>(round, "debugStartButtons"))
            Assert.That(button.IsInteractable(), Is.False, "Debug starts cannot replace an active playable turn.");
        var sequenceStop = ControlField<Button>(rig.Sequence, "stopButton");
        Assert.That(sequenceStop.IsInteractable(), Is.True);
        sequenceStop.onClick.Invoke(); round.AdvanceRound(0f);
        InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
        Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Preparation));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(points));
        Assert.That(round.RoundNumber, Is.EqualTo(1));
        Assert.That(SequenceUIIds(), Is.EqualTo(ids));
        yield return null;
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
