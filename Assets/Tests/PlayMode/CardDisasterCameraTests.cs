using System.Collections;
using System.IO;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    private static void AdvanceTsunamiCamera(TsunamiTestController tsunami, CityCameraController navigation, float seconds)
    {
        while (seconds > 0.00001f)
        {
            float step = Mathf.Min(seconds, 1f / 60f);
            InvokeCity(tsunami, "AdvanceTsunami", step);
            InvokeCity(navigation, "AdvanceNavigation", step);
            seconds -= step;
        }
    }

    [UnityTest]
    public IEnumerator TsunamiCameraFramesTheWholeMovingWaveThenSmoothlyRestoresThePlayerView()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var wave = ControlField<AuthoredTsunamiWave>(tsunami, "authoredWave");
        var camera = ControlField<Camera>(navigation, "controlledCamera");
        PresentationField(navigation, "targetPan", new Vector2(0.2f, -0.15f));
        PresentationField(navigation, "targetZoom", 5.5f);
        InvokeCity(navigation, "AdvanceNavigation", 2f);
        Vector2 homePan = navigation.PanOffset;
        float homeZoom = ControlField<float>(navigation, "currentZoom");
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TsunamiCameraHoldWork"));
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "01-player-view.png"));

        tsunami.StartTsunami();
        Assert.That(navigation.InputBlocked, Is.True);
        AdvanceTsunamiCamera(tsunami, navigation, 0.5f);
        Assert.That(navigation.PanOffset, Is.EqualTo(homePan), "No camera jump when the test starts.");
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
        float lead = ControlField<float>(tsunami, "cameraLeadSeconds");
        float leadStart = monster.WaveReleaseDelay - lead;
        AdvanceTsunamiCamera(tsunami, navigation, leadStart + lead * 0.5f - ControlField<float>(tsunami, "sequenceElapsed"));
        float halfwayZoom = ControlField<float>(navigation, "currentZoom");
        Assert.That(halfwayZoom, Is.GreaterThan(homeZoom).And.LessThan(ControlField<float>(navigation, "targetZoom")),
            "The cinematic smoothly widens before the full-size wave appears.");
        Assert.That(tsunami.transform.Find("Tsunami_Water").gameObject.activeSelf, Is.False);
        AdvanceTsunamiCamera(tsunami, navigation, monster.WaveReleaseDelay - 0.06f - ControlField<float>(tsunami, "sequenceElapsed"));
        Vector2 oceanPan = navigation.PanOffset;
        CaptureCityWorldFrame(Path.Combine(directory, "02-before-wave.png"));

        var approach = (tsunami.CityBounds.min.z - ControlField<float>(tsunami, "startDistance")) / ControlField<float>(tsunami, "waveSpeed");
        AdvanceTsunamiCamera(tsunami, navigation, monster.WaveReleaseDelay + Mathf.Min(0.8f, approach * 0.8f) - ControlField<float>(tsunami, "sequenceElapsed"));
        Assert.That(Vector2.Distance(navigation.PanOffset, oceanPan), Is.GreaterThan(Mathf.Min(0.4f, approach * ControlField<float>(tsunami, "waveSpeed") * 0.1f)),
            "The camera travels with the wave over the shortened shore approach.");
        AssertOpaqueWaveInGameCamera(camera, wave, navigation.OverviewViewportRect);
        CaptureCityWorldFrame(Path.Combine(directory, "03-following-wave.png"));

        float landfall = monster.WaveReleaseDelay +
            (tsunami.CityBounds.min.z - ControlField<float>(tsunami, "startDistance")) / ControlField<float>(tsunami, "waveSpeed");
        AdvanceTsunamiCamera(tsunami, navigation, landfall - 0.02f - ControlField<float>(tsunami, "sequenceElapsed"));
        Vector2 impactPan = navigation.PanOffset;
        AdvanceTsunamiCamera(tsunami, navigation, landfall + 0.9f - ControlField<float>(tsunami, "sequenceElapsed"));
        Assert.That(Vector2.Distance(navigation.PanOffset, impactPan), Is.GreaterThan(.01f),
            "Tracking continues after first landfall until the mesh reaches the second saved point.");
        AssertOpaqueWaveInGameCamera(camera, wave, navigation.OverviewViewportRect);
        Assert.That(tsunami.FloodCoverage, Is.GreaterThan(0f));
        CaptureCityWorldFrame(Path.Combine(directory, "04-city-impact.png"));
        AdvanceTsunamiCamera(tsunami, navigation, tsunami.TestDuration - 0.2f - ControlField<float>(tsunami, "sequenceElapsed"));
        Assert.That(tsunami.IsRunning, Is.True);
        AssertOpaqueWaveInGameCamera(camera, wave, navigation.OverviewViewportRect);
        Assert.That(wave.transform.localPosition, Is.EqualTo(wave.EndPosition));
        Vector2 heldPan = navigation.PanOffset;
        float heldZoom = ControlField<float>(navigation, "currentZoom");
        Assert.That(navigation.InputBlocked, Is.True, "Keep the view locked through the final runoff.");

        InvokeCity(tsunami, "AdvanceTsunami", 1f);
        Assert.That(tsunami.IsRunning, Is.False);
        Assert.That(navigation.PanOffset, Is.EqualTo(heldPan), "The return must start without a cut.");
        InvokeCity(navigation, "AdvanceNavigation", 0.8f);
        Assert.That(navigation.InputBlocked, Is.True);
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.LessThan(heldZoom).And.GreaterThan(homeZoom));
        InvokeCity(navigation, "AdvanceNavigation", 0.81f);
        Assert.That(navigation.IsEventCameraActive, Is.False);
        Assert.That(navigation.InputBlocked, Is.False);
        Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
        yield return CaptureCanvas(Path.Combine(directory, "05-restored-player-view.png"));
    }

    [UnityTest]
    public IEnumerator DisasterLocksBlockScrollAndDragUntilEveryEventAndTheCameraReturnFinish()
    {
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var guard = Object.FindFirstObjectByType<CardCityNavigation>();
        var city = Object.FindFirstObjectByType<CityViewport>();
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TsunamiCameraWork/input-locks.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        yield return CaptureCanvas(path, afterRender: uiCamera =>
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera,
                city.ViewRect.TransformPoint(new Vector3(city.ViewRect.rect.center.x, city.ViewRect.rect.yMax - 260f)));
            void AssertBlockedInput()
            {
                InvokeCity(guard, "Update");
                Assert.That(navigation.InputBlocked, Is.True, "The card guard cannot clear a disaster's lock.");
                Vector2 pan = ControlField<Vector2>(navigation, "targetPan");
                float zoom = ControlField<float>(navigation, "targetZoom");
                InvokeCity(navigation, "ReadPointer", point, 3f, true, true);
                InvokeCity(navigation, "ReadPointer", point + Vector2.right * 60f, -3f, false, true);
                Assert.That(ControlField<Vector2>(navigation, "targetPan"), Is.EqualTo(pan));
                Assert.That(ControlField<float>(navigation, "targetZoom"), Is.EqualTo(zoom));
                Assert.That(navigation.BeginPresentationFocus(Vector3.zero, 1.12f, 0.4f), Is.False);
            }

            fire.StartFire();
            AssertBlockedInput();
            quake.StartEarthquake();
            fire.StopFire();
            AssertBlockedInput();
            InvokeCity(quake, "AdvanceShake", quake.TestDuration);
            Assert.That(navigation.InputBlocked, Is.False);
            fire.StartFire();
            InvokeCity(fire, "AdvanceFire", fire.TestDuration);
            Assert.That(navigation.InputBlocked, Is.False);
            fire.StartFire();
            fire.enabled = false;
            Assert.That(navigation.InputBlocked, Is.False);
            fire.enabled = true;

            tsunami.StartTsunami();
            AssertBlockedInput();
            AdvanceTsunamiCamera(tsunami, navigation, 2.2f);
            AssertBlockedInput();
            tsunami.StopTsunami();
            AssertBlockedInput();
            InvokeCity(navigation, "AdvanceNavigation", 2f);
            Assert.That(navigation.InputBlocked, Is.False);
            // Holding a drag begun while locked must not move the camera after unlock.
            var expectedPan = ControlField<Vector2>(navigation, "targetPan");
            InvokeCity(navigation, "ReadPointer", point + Vector2.right * 90f, 0f, false, true);
            Assert.That(ControlField<Vector2>(navigation, "targetPan"), Is.EqualTo(expectedPan));
            InvokeCity(navigation, "ReadPointer", point, 0f, true, true);
            InvokeCity(navigation, "ReadPointer", point + Vector2.right * 45f, 1f, false, true);
            Assert.That(ControlField<Vector2>(navigation, "targetPan").x, Is.LessThan(expectedPan.x - 0.01f));
        });
    }

    [UnityTest]
    public IEnumerator TsunamiCameraRestartsAndCancelsAtWideAndTallAspectsWithoutLosingTheOriginalView()
    {
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var camera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
        float originalAspect = camera.aspect;
        try
        {
            foreach (float aspect in new[] { 16f / 9f, 0.75f })
            {
                camera.aspect = aspect;
                PresentationField(navigation, "targetPan", new Vector2(0.2f, -0.3f));
                PresentationField(navigation, "targetZoom", 4.5f);
                InvokeCity(navigation, "AdvanceNavigation", 2f);
                Vector2 homePan = navigation.PanOffset;
                float homeZoom = camera.orthographicSize;
                Assert.That(navigation.BeginPresentationFocus(Vector3.zero, 1.12f, 0.4f), Is.True);
                InvokeCity(navigation, "AdvanceNavigation", 0.2f);
                tsunami.StartTsunami();
                AdvanceTsunamiCamera(tsunami, navigation, 2.6f);
                Assert.That(navigation.IsPresenting, Is.False);
                tsunami.StopTsunami();
                InvokeCity(navigation, "AdvanceNavigation", 0.3f);
                tsunami.StartTsunami();
                AdvanceTsunamiCamera(tsunami, navigation, 2.6f);
                tsunami.enabled = false;
                InvokeCity(navigation, "AdvanceNavigation", 2f);
                Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
                Assert.That(camera.orthographicSize, Is.EqualTo(homeZoom).Within(0.0001f), "Aspect: " + aspect);
                Assert.That(navigation.InputBlocked, Is.False);
                tsunami.enabled = true;
            }
        }
        finally { camera.aspect = originalAspect; }
        yield return null;
    }
}
