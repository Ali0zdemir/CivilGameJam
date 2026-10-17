using System.Collections;
using System.IO;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator MonsterSequenceCameraSmoothlyZoomsHoldsTheCityAndReturnsForEveryRouteInCardScene()
    {
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterSequenceCameraWork"));
        Directory.CreateDirectory(directory);
        foreach (var sceneName in new[] { "2DCardScene" })
        {
            yield return SceneManager.LoadSceneAsync(sceneName); yield return null;
            var rig = new SequencePreview();
            var navigation = rig.Navigation;
            var camera = ControlField<Camera>(navigation, "controlledCamera");
            for (var number = 1; number <= 3; number++)
            {
                PresentationField(navigation, "targetPan", new Vector2(0.18f * number, -0.1f));
                PresentationField(navigation, "targetZoom", 5.2f + number * 0.1f);
                InvokeCity(navigation, "AdvanceNavigation", 2f);
                var homePan = navigation.PanOffset;
                var homeZoom = ControlField<float>(navigation, "currentZoom");
                var homeCameraPosition = camera.transform.position;
                void Capture(string stage)
                {
                    if (number != 1) return;
                    var path = Path.Combine(directory, sceneName + "-" + stage + ".png");
                    if (sceneName == "2DCardScene") CaptureCityWorldFrame(path);
                    else CaptureStandaloneMonster(camera, path);
                }
                Capture("01-player-view");
                rig.Sequence.SelectSequence(number);
                rig.Sequence.StartSelectedSequence();
                Assert.That(navigation.PanOffset, Is.EqualTo(homePan), "Starting the cinematic cannot cut to another camera position.");
                Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
                if (number >= 2)
                {
                    var wave = ControlField<AuthoredTsunamiWave>(rig.Tsunami, "authoredWave");
                    rig.For(rig.Monster.WaveReleaseDelay + .2f);
                    AssertOpaqueWaveAtPlayerZoom(camera, wave, navigation);
                    rig.Until(() =>
                    {
                        if (rig.Tsunami.IsRunning && wave.IsVisible)
                            AssertOpaqueWaveAtPlayerZoom(camera, wave, navigation);
                        return !rig.Sequence.IsRunning;
                    });
                    Assert.That(navigation.IsEventCameraActive && navigation.InputBlocked, Is.True);
                    var farZoom = ControlField<float>(navigation, "currentZoom");
                    var seconds = ControlField<float>(rig.Sequence, "cameraReturnSeconds");
                    InvokeCity(navigation, "AdvanceNavigation", seconds * .5f);
                    Assert.That(ControlField<float>(navigation, "currentZoom"),
                        Is.InRange(Mathf.Min(farZoom, homeZoom), Mathf.Max(farZoom, homeZoom)));
                    Assert.That(navigation.InputBlocked, Is.True);
                    InvokeCity(navigation, "AdvanceNavigation", seconds);
                    Assert.That(navigation.IsEventCameraActive || navigation.InputBlocked, Is.False);
                    Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
                    Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
                    Assert.That(Vector3.Distance(camera.transform.position, homeCameraPosition), Is.LessThan(.0001f));
                    continue;
                }
                var cityPan = ControlField<Vector2>(navigation, "targetPan");
                var cityZoom = ControlField<float>(navigation, "targetZoom");
                var zoomSeconds = ControlField<float>(rig.Sequence, "cameraZoomSeconds");
                Assert.That(cityZoom, Is.InRange(homeZoom / 1.3f - 0.0001f, homeZoom));
                rig.For(zoomSeconds * 0.25f);
                Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom), "Centre the city before changing the zoom.");
                Assert.That(Vector2.Distance(navigation.PanOffset, homePan), Is.GreaterThan(0.001f));
                rig.For(zoomSeconds * 0.25f);
                Assert.That(ControlField<float>(navigation, "currentZoom"), Is.GreaterThan(cityZoom).And.LessThan(homeZoom));
                AssertCityViewportCenter(camera, rig.Tsunami);
                rig.For(zoomSeconds * 0.5f + 0.05f);
                Assert.That(navigation.PanOffset, Is.EqualTo(cityPan));
                Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(cityZoom));
                Capture("02-city-zoom");
                var cityDepth = Vector3.Dot(rig.Tsunami.transform.TransformPoint(rig.Tsunami.CityBounds.center) - camera.transform.position, camera.transform.forward);
                var cityAnchor = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, cityDepth));
                rig.Until(() =>
                {
                    if (!rig.Sequence.IsRunning) return true;
                    if (number < 3)
                    {
                        // Collapsing meshes change their bounds; the held camera keeps the original city location centred.
                        var anchored = camera.WorldToViewportPoint(cityAnchor);
                        Assert.That(anchored.x, Is.EqualTo(0.5f).Within(0.025f));
                        Assert.That(anchored.y, Is.EqualTo(0.5f).Within(0.025f));
                        Assert.That(navigation.PanOffset, Is.EqualTo(cityPan), "The sequence holds its city framing until the return.");
                        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(cityZoom));
                    }
                    else AssertCityViewportCenter(camera, rig.Tsunami, 0.025f);
                    return false;
                });
                Assert.That(navigation.IsEventCameraActive && navigation.InputBlocked, Is.True);
                var heldZoom = ControlField<float>(navigation, "currentZoom");
                var returnSeconds = ControlField<float>(rig.Sequence, "cameraReturnSeconds");
                InvokeCity(navigation, "AdvanceNavigation", returnSeconds * 0.5f - ControlField<float>(navigation, "eventElapsed"));
                Assert.That(ControlField<float>(navigation, "currentZoom"), Is.GreaterThan(heldZoom).And.LessThan(homeZoom));
                Assert.That(navigation.InputBlocked, Is.True, "Scroll and drag stay blocked until the smooth return finishes.");
                Capture("03-returning");
                InvokeCity(navigation, "AdvanceNavigation", returnSeconds);
                Assert.That(navigation.IsEventCameraActive || navigation.InputBlocked, Is.False);
                Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
                Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
                Assert.That(Vector3.Distance(camera.transform.position, homeCameraPosition), Is.LessThan(0.0001f));
                Capture("04-restored-player-view");
            }
        }
    }

    [UnityTest]
    public IEnumerator MonsterSequenceCameraRestartsDuringItsReturnAndRestoresTheOriginalViewWhenDisabled()
    {
        var rig = new SequencePreview();
        var navigation = rig.Navigation;
        var camera = ControlField<Camera>(navigation, "controlledCamera");
        var oldAspect = camera.aspect;
        try
        {
            foreach (var aspect in new[] { 16f / 9f, 0.75f })
            {
                camera.aspect = aspect;
                PresentationField(navigation, "targetPan", new Vector2(0.15f, -0.2f));
                PresentationField(navigation, "targetZoom", 5.4f);
                InvokeCity(navigation, "AdvanceNavigation", 2f);
                var homePan = navigation.PanOffset;
                var homeZoom = ControlField<float>(navigation, "currentZoom");
                rig.Sequence.SelectSequence(1); rig.Sequence.StartSelectedSequence();
                rig.For(1f);
                rig.Sequence.StopSequence();
                InvokeCity(navigation, "AdvanceNavigation", 0.35f);
                var interruptedPan = navigation.PanOffset;
                var interruptedZoom = ControlField<float>(navigation, "currentZoom");
                rig.Sequence.SelectSequence(3); rig.Sequence.StartSelectedSequence();
                Assert.That(navigation.PanOffset, Is.EqualTo(interruptedPan));
                Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(interruptedZoom));
                rig.For(3f);
                var wave = ControlField<AuthoredTsunamiWave>(rig.Tsunami, "authoredWave");
                AssertOpaqueWaveAtPlayerZoom(camera, wave, navigation);
                rig.Sequence.enabled = false;
                Assert.That(navigation.IsEventCameraActive && navigation.InputBlocked, Is.True);
                InvokeCity(navigation, "AdvanceNavigation", 2f);
                Assert.That(navigation.IsEventCameraActive || navigation.InputBlocked, Is.False);
                Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
                Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(homeZoom));
                rig.Sequence.enabled = true;
            }
        }
        finally { camera.aspect = oldAspect; InvokeCity(navigation, "AdvanceNavigation", 0.1f); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator MonsterSequenceKeepsTheCityCentredAndTheBreathingMonsterInsideWideAndTallFrames()
    {
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterCenteredCameraWork"));
        Directory.CreateDirectory(directory);
        foreach (var sceneName in new[] { "2DCardScene" })
        {
            yield return SceneManager.LoadSceneAsync(sceneName); yield return null;
            var rig = new SequencePreview();
            var camera = ControlField<Camera>(rig.Navigation, "controlledCamera");
            var oldAspect = camera.aspect;
            var walkPoint = rig.Walk.WalkPoint.position;
            try
            {
                foreach (var aspect in new[] { 16f / 9f, 0.75f })
                {
                    camera.aspect = aspect;
                    // A moved scene marker must still leave room for the boss beside the centred city.
                    rig.Walk.WalkPoint.position = walkPoint - (aspect < 1f ? camera.transform.right * 6f : Vector3.zero);
                    PresentationField(rig.Navigation, "targetPan", new Vector2(0.8f, -0.4f));
                    PresentationField(rig.Navigation, "targetZoom", 1.8f);
                    InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
                    var homePan = rig.Navigation.PanOffset;
                    var homeZoom = ControlField<float>(rig.Navigation, "currentZoom");
                    rig.Sequence.SelectSequence(3); rig.Sequence.StartSelectedSequence();
                    rig.For(ControlField<float>(rig.Sequence, "cameraZoomSeconds") * 0.55f);
                    AssertCityViewportCenter(camera, rig.Tsunami);
                    rig.Until(() => rig.Sequence.Stage == MonsterSequenceStage.Fire);
                    rig.For(rig.Breath.ChargeDuration - 0.1f);
                    var view = aspect < 1f ? "tall" : "wide";
                    CaptureCenteredFrame(camera, Path.Combine(directory, sceneName + "-" + view + "-warming.png"));
                    rig.Until(() =>
                    {
                        AssertCityViewportCenter(camera, rig.Tsunami);
                        AssertSubjectInsideFrame(camera, rig.Tsunami.BuildingsRoot, rig.Tsunami.GroundSurface);
                        AssertSubjectInsideFrame(camera, rig.Monster.Model);
                        return ControlField<float>(rig.Fire, "elapsed") >= rig.Breath.FlameStartTime + 0.8f;
                    });
                    CaptureCenteredFrame(camera, Path.Combine(directory, sceneName + "-" + view + "-breathing.png"));
                    rig.Until(() =>
                    {
                        AssertCityViewportCenter(camera, rig.Tsunami);
                        AssertSubjectInsideFrame(camera, rig.Tsunami.BuildingsRoot, rig.Tsunami.GroundSurface);
                        AssertSubjectInsideFrame(camera, rig.Monster.Model);
                        return ControlField<float>(rig.Fire, "elapsed") >= rig.Breath.BreathEndTime - 0.06f;
                    });
                    Assert.That(rig.Navigation.InputBlocked, Is.True);
                    rig.Sequence.StopSequence();
                    InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
                    Assert.That(rig.Navigation.PanOffset, Is.EqualTo(homePan));
                    Assert.That(ControlField<float>(rig.Navigation, "currentZoom"), Is.EqualTo(homeZoom));
                    Assert.That(rig.Navigation.InputBlocked, Is.False);
                }
            }
            finally
            {
                rig.Sequence.StopSequence();
                camera.aspect = oldAspect;
                rig.Walk.WalkPoint.position = walkPoint;
            }
        }
    }

    private static void AssertCityViewportCenter(Camera camera, TsunamiTestController city, float tolerance = 0.001f)
    {
        var frame = ProjectedSubjectFrame(camera, city.BuildingsRoot, city.GroundSurface);
        Assert.That(frame.center.x, Is.EqualTo(0.5f).Within(tolerance), "Keep the city horizontally centred.");
        Assert.That(frame.center.y, Is.EqualTo(0.5f).Within(tolerance), "Keep the city vertically centred during the zoom.");
    }

    private static void AssertSubjectInsideFrame(Camera camera, params Transform[] subjects)
    {
        var frame = ProjectedSubjectFrame(camera, subjects);
        Assert.That(frame.xMin, Is.GreaterThan(0.035f));
        Assert.That(frame.xMax, Is.LessThan(0.965f));
        Assert.That(frame.yMin, Is.GreaterThan(0.035f));
        Assert.That(frame.yMax, Is.LessThan(0.965f), "The head and the whole city must stay inside the frame throughout the breath.");
    }

    private static Rect ProjectedSubjectFrame(Camera camera, params Transform[] subjects)
    {
        var minimum = Vector2.positiveInfinity;
        var maximum = Vector2.negativeInfinity;
        foreach (var subject in subjects)
        foreach (var mesh in subject.GetComponentsInChildren<MeshFilter>())
        {
            if (!mesh.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled || mesh.sharedMesh == null) continue;
            var bounds = mesh.sharedMesh.bounds;
            foreach (var x in new[] { bounds.min.x, bounds.max.x })
                foreach (var y in new[] { bounds.min.y, bounds.max.y })
                    foreach (var z in new[] { bounds.min.z, bounds.max.z })
                    {
                        Vector2 point = camera.WorldToViewportPoint(mesh.transform.TransformPoint(new Vector3(x, y, z)));
                        minimum = Vector2.Min(minimum, point);
                        maximum = Vector2.Max(maximum, point);
                    }
        }
        return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }

    private static void CaptureCenteredFrame(Camera camera, string path)
    {
        var oldTarget = camera.targetTexture; var oldAspect = camera.aspect; var oldActive = RenderTexture.active;
        var width = oldAspect < 1f ? 720 : 1280;
        var height = Mathf.RoundToInt(width / oldAspect);
        var target = new RenderTexture(width, height, 24);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = oldAspect; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; camera.aspect = oldAspect; RenderTexture.active = oldActive;
            target.Release(); Object.Destroy(target); Object.Destroy(pixels);
        }
    }
}
