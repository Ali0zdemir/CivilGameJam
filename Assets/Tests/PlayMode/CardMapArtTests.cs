using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator TsunamiTraversesBothSavedPointsAndKeepsItsWholeOpaqueMeshInsideWideAndTallGameCameras()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var rig = new SequencePreview();
        var wave = ControlField<AuthoredTsunamiWave>(rig.Tsunami, "authoredWave");
        var camera = ControlField<Camera>(rig.Navigation, "controlledCamera");
        var oldAspect = camera.aspect;
        var originalScale = wave.Surface.transform.lossyScale;
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TsunamiFullRouteWork/Previews"));
        Directory.CreateDirectory(directory);
        PresentationField(wave, "movementSpeed", 8f);
        try
        {
            foreach (var aspect in new[] { 16f / 9f, .75f })
            {
                camera.aspect = aspect;
                var homePan = rig.Navigation.PanOffset;
                var homeZoom = ControlField<float>(rig.Navigation, "currentZoom");
                rig.Tsunami.StartTsunami();
                Assert.That(wave.transform.localPosition, Is.EqualTo(wave.StartPosition));
                AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, rig.Monster.WaveReleaseDelay + .2f);
                var view = aspect < 1f ? "tall" : "wide";
                var capturedMiddle = false;
                var capturedEnd = false;
                while (rig.Tsunami.IsRunning)
                {
                    if (wave.IsVisible)
                    {
                        AssertOpaqueWaveInGameCamera(camera, wave, rig.Navigation.OverviewViewportRect);
                        Assert.That(wave.transform.localPosition.x, Is.EqualTo(-9.536f));
                        Assert.That(wave.transform.localPosition.y, Is.EqualTo(-.32f));
                        Assert.That(wave.Surface.transform.lossyScale, Is.EqualTo(originalScale));
                        Assert.That(rig.Navigation.InputBlocked, Is.True);
                        if (!capturedMiddle && wave.transform.localPosition.z > 20f)
                        {
                            CaptureCenteredFrame(camera, Path.Combine(directory, view + "-middle.png"));
                            capturedMiddle = true;
                            if (aspect > 1f)
                            {
                                yield return CaptureCanvas(Path.Combine(directory, "game-view.png"), afterRender: uiCamera =>
                                {
                                    InvokeCity(rig.Tsunami, "AdvanceTsunami", 0f);
                                    InvokeCity(rig.Navigation, "AdvanceNavigation", .05f);
                                    AssertOpaqueWaveInGameCamera(camera, wave, rig.Navigation.OverviewViewportRect);
                                    camera.Render(); uiCamera.Render();
                                });
                                camera.aspect = aspect;
                            }
                        }
                        if (!capturedEnd && wave.transform.localPosition.z >= 48.49f)
                        {
                            CaptureCenteredFrame(camera, Path.Combine(directory, view + "-end.png"));
                            capturedEnd = true;
                        }
                    }
                    AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, .05f);
                }
                Assert.That(capturedMiddle, Is.True);
                Assert.That(wave.transform.localPosition, Is.EqualTo(wave.EndPosition));
                Assert.That(wave.IsVisible, Is.False);
                InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
                Assert.That(rig.Navigation.PanOffset, Is.EqualTo(homePan));
                Assert.That(ControlField<float>(rig.Navigation, "currentZoom"), Is.EqualTo(homeZoom));
                Assert.That(rig.Navigation.InputBlocked, Is.False);
            }
        }
        finally { rig.Tsunami.StopTsunami(); camera.aspect = oldAspect; }
        yield return null;
    }

    private static void AssertOpaqueWaveInGameCamera(Camera camera, AuthoredTsunamiWave wave, Rect safe)
    {
        var bounds = wave.Surface.localBounds;
        for (var i = 0; i < 8; i++)
        {
            var point = wave.Surface.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
            var view = camera.WorldToViewportPoint(point);
            Assert.That(view.x, Is.InRange(safe.xMin - .001f, safe.xMax + .001f), "The full wave width must fit the actual game camera.");
            Assert.That(view.y, Is.InRange(safe.yMin - .001f, safe.yMax + .001f), "The wave must stay below the HUD and above the sequence menu.");
            Assert.That(view.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane), "No wave faces may disappear at the clipping planes.");
        }
        for (var i = 0; i < wave.Surface.sharedMaterials.Length; i++)
        {
            var block = new MaterialPropertyBlock(); wave.Surface.GetPropertyBlock(block, i);
            Assert.That(block.GetColor("_BaseColor").a, Is.EqualTo(1f), "The wave cannot fade while travelling.");
            Assert.That(wave.Surface.sharedMaterials[i].GetFloat("_Cull"), Is.Zero);
            Assert.That(wave.Surface.sharedMaterials[i].GetFloat("_ZWrite"), Is.EqualTo(1f));
        }
    }

    [UnityTest]
    public IEnumerator AuthoredWaveInspectorSpeedChangesTravelWithoutJumpsOrStartingTheEarthquakeBeforeArrival()
    {
        var rig = new SequencePreview();
        var wave = ControlField<AuthoredTsunamiWave>(rig.Tsunami, "authoredWave");
        var start = ControlField<float>(rig.Tsunami, "startDistance");
        var shore = ControlField<float>(rig.Tsunami, "shorelineDistance");
        var originalScale = wave.Surface.transform.lossyScale;
        PresentationField(wave, "movementSpeed", .25f);
        rig.Sequence.SelectSequence(2);
        rig.Sequence.StartSelectedSequence();
        rig.For(rig.Monster.WaveReleaseDelay + .4f);
        Assert.That(rig.Tsunami.WaveSpeed, Is.EqualTo(.25f));
        Assert.That(wave.transform.localPosition.z - start, Is.EqualTo(.1f).Within(.0001f));
        var wavePosition = wave.transform.localPosition;
        var monsterZ = rig.Tsunami.transform.InverseTransformPoint(rig.Monster.Model.position).z;
        PresentationField(wave, "movementSpeed", .5f);
        InvokeCity(rig.Tsunami, "AdvanceTsunami", 0f);
        Assert.That(wave.transform.localPosition, Is.EqualTo(wavePosition), "Changing the Inspector speed must not teleport the wave.");
        rig.For(.6f);
        var travel = wave.transform.localPosition.z - wavePosition.z;
        Assert.That(travel, Is.EqualTo(.3f).Within(.0001f));
        Assert.That(rig.Tsunami.transform.InverseTransformPoint(rig.Monster.Model.position).z - monsterZ,
            Is.EqualTo(travel).Within(.0001f), "The monster must use the wave's actual distance after a live speed change.");
        rig.For(rig.Monster.RiseDuration + .1f - ControlField<float>(rig.Tsunami, "sequenceElapsed"));
        Assert.That(rig.Monster.EmergenceProgress, Is.EqualTo(1f));
        Assert.That(rig.Monster.HasFinishedEmerging, Is.False);
        Assert.That(rig.Sequence.Stage, Is.EqualTo(MonsterSequenceStage.Emerging));
        Assert.That(rig.Quake.IsRunning, Is.False, "A slow wave cannot release the monster to walk early.");
        Assert.That(rig.Tsunami.FloodCoverage, Is.Zero);
        PresentationField(wave, "movementSpeed", 2f);
        var remaining = (shore - ControlField<float>(rig.Tsunami, "front")) / rig.Tsunami.WaveSpeed;
        rig.For(remaining - .05f);
        Assert.That(rig.Sequence.Stage, Is.EqualTo(MonsterSequenceStage.Emerging));
        Assert.That(rig.Tsunami.FloodCoverage, Is.Zero);
        rig.For(.1f);
        Assert.That(rig.Monster.HasFinishedEmerging, Is.True);
        Assert.That(rig.Quake.IsRunning, Is.True);
        Assert.That(rig.Sequence.Stage, Is.EqualTo(MonsterSequenceStage.WalkingToFirstPoint));
        var arrival = ControlField<float>(rig.Tsunami, "shoreArrivalElapsed");
        var walkingPosition = rig.Monster.Model.position;
        PresentationField(wave, "movementSpeed", .05f);
        InvokeCity(rig.Tsunami, "AdvanceTsunami", .3f);
        Assert.That(ControlField<float>(rig.Tsunami, "shoreArrivalElapsed"), Is.EqualTo(arrival),
            "Changing speed after landfall cannot restart or postpone the city flood.");
        Assert.That(rig.Tsunami.FloodCoverage, Is.GreaterThan(0f));
        Assert.That(rig.Monster.Model.position, Is.EqualTo(walkingPosition));
        Assert.That(wave.Surface.transform.lossyScale, Is.EqualTo(originalScale));
        PresentationField(wave, "movementSpeed", 10f);
        rig.Until(() => !rig.Sequence.IsRunning);
        Assert.That(rig.Sequence.Stage, Is.EqualTo(MonsterSequenceStage.Complete));
        rig.Sequence.StartSelectedSequence();
        Assert.That(rig.Tsunami.WaveSpeed, Is.EqualTo(10f), "Replay keeps the Inspector setting.");
        rig.Sequence.StopSequence();
        Assert.That(wave.IsVisible, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MonsterTravelsWithTheSavedArtistWaveWhileTheNewMapAndExistingFloodRemainIntact()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var wave = ControlField<AuthoredTsunamiWave>(tsunami, "authoredWave");
        var waveId = wave.GetInstanceID();
        var mesh = wave.Surface.GetComponent<MeshFilter>().sharedMesh;
        var originalScale = wave.Surface.transform.lossyScale;
        var originalColors = wave.Surface.sharedMaterials.Select(material => material.GetColor("_BaseColor")).ToArray();
        var scenery = tsunami.transform.Find("Map_Scenery");
        var sceneryMatrix = scenery.localToWorldMatrix;
        var resetPosition = monster.Model.localPosition;
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MapArtIntegrationWork/Previews"));
        Directory.CreateDirectory(directory);
        CaptureCityWorldFrame(Path.Combine(directory, "01-map-idle.png"));
        Assert.That(wave.IsVisible, Is.False);
        tsunami.StartTsunami();
        InvokeCity(tsunami, "AdvanceTsunami", monster.WaveReleaseDelay + .2f);
        Assert.That(wave.IsVisible, Is.True);
        Assert.That(wave.Surface.transform.lossyScale, Is.EqualTo(originalScale), "Emergence must keep the supplied wave dimensions.");
        Assert.That(tsunami.transform.Find("Tsunami_Water").GetComponent<MeshRenderer>().enabled, Is.False,
            "The supplied mesh replaces the procedural incoming crest.");
        var firstWaveZ = wave.transform.localPosition.z;
        var firstMonsterZ = tsunami.transform.InverseTransformPoint(monster.Model.position).z;
        CaptureCityWorldFrame(Path.Combine(directory, "02-map-wave.png"));
        InvokeCity(tsunami, "AdvanceTsunami", .8f);
        var waveTravel = wave.transform.localPosition.z - firstWaveZ;
        var monsterTravel = tsunami.transform.InverseTransformPoint(monster.Model.position).z - firstMonsterZ;
        Assert.That(waveTravel, Is.EqualTo(.8f * tsunami.WaveSpeed).Within(.0001f));
        Assert.That(monsterTravel, Is.EqualTo(waveTravel).Within(.0001f), "The monster approaches with the wave instead of rising at one fixed sea position.");
        Assert.That(wave.Surface.transform.lossyScale, Is.EqualTo(originalScale));
        CaptureCityWorldFrame(Path.Combine(directory, "03-map-wave-travelling.png"));
        var shoreTime = (ControlField<float>(tsunami, "shorelineDistance") - ControlField<float>(tsunami, "startDistance")) / tsunami.WaveSpeed;
        InvokeCity(tsunami, "AdvanceTsunami", shoreTime + .35f - ControlField<float>(tsunami, "elapsed"));
        Assert.That(tsunami.FloodCoverage, Is.GreaterThan(0f));
        Assert.That(wave.transform.localPosition.z, Is.GreaterThan(ControlField<float>(tsunami, "shorelineDistance")),
            "The supplied wave continues across the whole saved route after first touching the city.");
        CaptureCityWorldFrame(Path.Combine(directory, "04-map-city-flood.png"));
        InvokeCity(tsunami, "AdvanceTsunami", ControlField<float>(tsunami, "fadeOutTime") * .5f);
        Assert.That(wave.IsVisible, Is.True);
        Assert.That(wave.Surface.transform.lossyScale, Is.EqualTo(originalScale));
        Assert.That(wave.transform.localPosition.y, Is.EqualTo(wave.StartPosition.y), "The path keeps the height shown in the Inspector.");
        InvokeCity(tsunami, "AdvanceTsunami", tsunami.TestDuration);
        Assert.That(tsunami.IsRunning || wave.IsVisible, Is.False);
        Assert.That(wave.transform.localPosition, Is.EqualTo(wave.EndPosition));
        Assert.That(wave.GetInstanceID(), Is.EqualTo(waveId));
        Assert.That(wave.Surface.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
        Assert.That(wave.Surface.sharedMaterials.Select(material => material.GetColor("_BaseColor")), Is.EqualTo(originalColors));
        Assert.That(scenery.localToWorldMatrix, Is.EqualTo(sceneryMatrix));
        tsunami.StartTsunami();
        Assert.That(monster.Model.localPosition, Is.EqualTo(resetPosition));
        tsunami.StopTsunami();
        Assert.That(wave.IsVisible, Is.False);
        yield return null;
    }
}
