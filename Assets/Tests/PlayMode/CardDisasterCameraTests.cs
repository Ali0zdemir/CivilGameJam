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
    public IEnumerator TsunamiCameraHoldsPlayerZoomOnTheCityThenSmoothlyRestoresThePlayerView()
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
        Assert.That(navigation.PanOffset, Is.EqualTo(homePan), "Starting the tsunami must not cut the camera.");
        var cityPan = ControlField<Vector2>(navigation, "targetPan");
        AdvanceTsunamiCamera(tsunami, navigation, 0.5f);
        Assert.That(Vector2.Distance(navigation.PanOffset, cityPan), Is.LessThan(Vector2.Distance(homePan, cityPan)),
            "The focus moves smoothly toward the city.");
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
        float lead = ControlField<float>(tsunami, "cameraLeadSeconds");
        AdvanceTsunamiCamera(tsunami, navigation, lead - ControlField<float>(tsunami, "sequenceElapsed"));
        Assert.That(navigation.PanOffset, Is.EqualTo(cityPan));
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom),
            "Showing the wave cannot pull the city farther away than the player's normal camera.");
        Assert.That(tsunami.transform.Find("Tsunami_Water").gameObject.activeSelf, Is.False);
        AdvanceTsunamiCamera(tsunami, navigation, monster.WaveReleaseDelay - 0.06f - ControlField<float>(tsunami, "sequenceElapsed"));
        Vector2 heldCityPan = navigation.PanOffset;
        var oceanWavePosition = wave.transform.position;
        CaptureCityWorldFrame(Path.Combine(directory, "02-before-wave.png"));

        var approach = (tsunami.CityBounds.min.z - ControlField<float>(tsunami, "startDistance")) / tsunami.WaveSpeed;
        AdvanceTsunamiCamera(tsunami, navigation, monster.WaveReleaseDelay + Mathf.Min(0.8f, approach * 0.8f) - ControlField<float>(tsunami, "sequenceElapsed"));
        var waveTravel = wave.transform.position - oceanWavePosition;
        Assert.That(waveTravel.magnitude, Is.GreaterThan(.01f));
        Assert.That(navigation.PanOffset, Is.EqualTo(heldCityPan), "The wave moves while the camera stays focused on the city.");
        AssertOpaqueWaveAtPlayerZoom(camera, wave, navigation);
        CaptureCityWorldFrame(Path.Combine(directory, "03-following-wave.png"));

        float landfall = monster.WaveReleaseDelay +
            (tsunami.CityBounds.min.z - ControlField<float>(tsunami, "startDistance")) / tsunami.WaveSpeed;
        AdvanceTsunamiCamera(tsunami, navigation, landfall - 0.02f - ControlField<float>(tsunami, "sequenceElapsed"));
        Vector2 impactPan = navigation.PanOffset;
        AdvanceTsunamiCamera(tsunami, navigation, landfall + 0.9f - ControlField<float>(tsunami, "sequenceElapsed"));
        Assert.That(navigation.PanOffset, Is.EqualTo(impactPan), "Landfall cannot drag the view away from the city.");
        AssertOpaqueWaveAtPlayerZoom(camera, wave, navigation);
        Assert.That(tsunami.FloodCoverage, Is.GreaterThan(0f));
        CaptureCityWorldFrame(Path.Combine(directory, "04-city-impact.png"));
        AdvanceTsunamiCamera(tsunami, navigation, tsunami.TestDuration - 0.2f - ControlField<float>(tsunami, "sequenceElapsed"));
        Assert.That(tsunami.IsRunning, Is.True);
        AssertOpaqueWaveAtPlayerZoom(camera, wave, navigation);
        Assert.That(tsunami.IsWaterReceding, Is.True);
        Assert.That(wave.FrontDistance, Is.LessThanOrEqualTo(tsunami.WaveEndDistance + .001f),
            "The wave recedes from the city boundary instead of continuing to the old mountain endpoint.");
        Vector2 heldPan = navigation.PanOffset;
        float heldZoom = ControlField<float>(navigation, "currentZoom");
        Assert.That(navigation.InputBlocked, Is.True, "Keep the view locked through the final runoff.");

        InvokeCity(tsunami, "AdvanceTsunami", 1f);
        Assert.That(tsunami.IsRunning, Is.False);
        Assert.That(navigation.PanOffset, Is.EqualTo(heldPan), "The return must start without a cut.");
        InvokeCity(navigation, "AdvanceNavigation", 0.8f);
        Assert.That(navigation.InputBlocked, Is.True);
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
        Assert.That(Vector2.Distance(navigation.PanOffset, homePan), Is.GreaterThan(.001f)
            .And.LessThan(Vector2.Distance(heldPan, homePan)), "Return smoothly before reaching the original view.");
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
