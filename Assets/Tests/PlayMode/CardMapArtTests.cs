using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator CityWaterRecedesGraduallyTowardTheSeaBeforeTheFloodIsCleared()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var rig = new SequencePreview();
        var wave = ControlField<AuthoredTsunamiWave>(rig.Tsunami, "authoredWave");
        var ids = UIIds(game);
        rig.Tsunami.StartTsunami();
        var limit = 0;
        while (!rig.Tsunami.IsWaterReceding && limit++ < 1800)
            AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, .05f);
        Assert.That(rig.Tsunami.IsWaterReceding, Is.True);
        var flood = ControlField<object>(rig.Tsunami, "flood");
        float MeanDepth() => (float)flood.GetType().GetProperty("MeanDepth").GetValue(flood);
        var surface = rig.Tsunami.transform.Find("Tsunami_Water/City_Flood_Flow").GetComponent<MeshFilter>();
        var initialDepth = MeanDepth();
        var initialCoverage = rig.Tsunami.FloodCoverage;
        Assert.That(initialDepth, Is.GreaterThan(.001f));
        var directory = Path.GetFullPath("Library/CardGameTestResults/TsunamiRecession/Previews");
        Directory.CreateDirectory(directory);
        CaptureCityWorldFrame(Path.Combine(directory, "water-recession-start.png"));
        var previousDepth = initialDepth;
        var previousCoverage = initialCoverage;
        foreach (var progress in new[] { .1f, .3f, .5f, .7f, .9f })
        {
            while (rig.Tsunami.WaterRecessionProgress < progress && limit++ < 2400)
                AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, .05f);
            Assert.That(rig.Tsunami.IsRunning, Is.True, "Keep rendering the water throughout its recession.");
            Assert.That(MeanDepth(), Is.LessThanOrEqualTo(previousDepth + .00001f));
            Assert.That(rig.Tsunami.FloodCoverage, Is.LessThanOrEqualTo(previousCoverage + .00001f));
            if (progress <= .5f) Assert.That(surface.sharedMesh.vertexCount, Is.GreaterThan(0));
            previousDepth = MeanDepth(); previousCoverage = rig.Tsunami.FloodCoverage;
            if (progress == .5f)
            {
                Assert.That(MeanDepth(), Is.GreaterThan(0f).And.LessThan(initialDepth));
                Assert.That(rig.Tsunami.FloodCoverage, Is.LessThan(initialCoverage));
                var depths = ControlField<float[]>(flood, "depth");
                var startDepths = ControlField<float[]>(flood, "recessionDepth");
                var rows = ControlField<int>(flood, "rows");
                var columns = ControlField<int>(flood, "columns");
                Assert.That(depths.Skip(columns * (rows * 3 / 4)).All(depth => depth < .00001f), Is.True,
                    "The inland quarter should dry before the shoreline.");
                Assert.That(depths.Take(columns * (rows / 4)).Where((depth, i) => startDepths[i] > .001f).Any(depth => depth > .0001f), Is.True,
                    "The last water remains by the sea while inland tiles dry.");
                CaptureCityWorldFrame(Path.Combine(directory, "water-recession-middle.png"));
            }
        }
        CaptureCityWorldFrame(Path.Combine(directory, "water-recession-ending.png"));
        while (rig.Tsunami.IsRunning && limit++ < 3000)
            AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, .05f);
        Assert.That(rig.Tsunami.IsRunning, Is.False);
        Assert.That(rig.Tsunami.WaterRecessionProgress, Is.GreaterThan(.999f));
        Assert.That(surface.sharedMesh.vertexCount, Is.Zero);
        Assert.That(UIIds(game), Is.EqualTo(ids));
        rig.Tsunami.StartTsunami();
        Assert.That(rig.Tsunami.WaterRecessionProgress, Is.Zero, "Replay must clear the recession state.");
        rig.Tsunami.StopTsunami();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ResizedAuthoredWaveKeepsItsSavedPoseAndOnlyEmitsSprayAtWaterContact()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var rig = new SequencePreview();
        var wave = ControlField<AuthoredTsunamiWave>(rig.Tsunami, "authoredWave");
        var position = wave.transform.localPosition;
        var scale = wave.transform.localScale;
        var rotation = wave.transform.localRotation;
        var ui = UIIds(game);
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TsunamiSizeSprayWork/Previews"));
        Directory.CreateDirectory(directory);
        Assert.That(position, Is.EqualTo(wave.StartPosition), "The saved pose must be the actual starting pose.");
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                rig.Tsunami.StartTsunami();
                Assert.That(wave.transform.localPosition, Is.EqualTo(position), "Starting or replaying must not teleport the edited wave.");
                var spray = ControlField<ParticleSystem>(rig.Tsunami, "spray");
                Assert.That(spray, Is.Not.Null, "Keep the existing contact splash effect available.");
                Assert.That(spray.particleCount, Is.Zero);
                var approach = (rig.Tsunami.CityBounds.min.z - wave.StartDistance) / rig.Tsunami.WaveSpeed;
                AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, rig.Monster.WaveReleaseDelay + approach - .1f);
                Assert.That(wave.IsVisible, Is.True);
                Assert.That(wave.FrontDistance, Is.LessThan(rig.Tsunami.CityBounds.min.z));
                Assert.That(rig.Tsunami.FloodCoverage, Is.Zero);
                Assert.That(spray.particleCount, Is.Zero, "The invisible procedural crest must not shed floating droplets in front of the artist mesh.");
                CaptureCityWorldFrame(Path.Combine(directory, attempt + "-approach.png"));
                AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, 1.1f);
                Assert.That(rig.Tsunami.FloodCoverage, Is.GreaterThan(0f));
                Assert.That(spray.particleCount, Is.GreaterThan(0), "Real building contact must still produce splashes.");
                Assert.That(CityHouseLinks().Any(link => link.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed), Is.True,
                    "Removing detached crest particles cannot disable flood damage.");
                Assert.That(wave.transform.localScale, Is.EqualTo(scale));
                Assert.That(wave.transform.localRotation, Is.EqualTo(rotation));
                Assert.That(wave.transform.localPosition.y, Is.EqualTo(position.y));
                CaptureCityWorldFrame(Path.Combine(directory, attempt + "-contact.png"));
                rig.Tsunami.StopTsunami();
                Assert.That(spray.particleCount, Is.Zero, "Stop must clear splash particles before replay.");
                Assert.That(wave.IsVisible, Is.False);
            }
            // Other scenes still use the procedural wave, which needs its own visible crest foam.
            PresentationField(rig.Tsunami, "authoredWave", null);
            rig.Tsunami.StartTsunami();
            AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, rig.Monster.WaveReleaseDelay + .5f);
            Assert.That(ControlField<ParticleSystem>(rig.Tsunami, "spray").particleCount, Is.GreaterThan(0));
            Assert.That(UIIds(game), Is.EqualTo(ui));
        }
        finally
        {
            rig.Tsunami.StopTsunami();
            PresentationField(rig.Tsunami, "authoredWave", wave);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator TsunamiStopsAtCityEdgeAndSmoothlyDissipatesAtPlayerZoomInWideAndTallGameCameras()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var rig = new SequencePreview();
        var wave = ControlField<AuthoredTsunamiWave>(rig.Tsunami, "authoredWave");
        var camera = ControlField<Camera>(rig.Navigation, "controlledCamera");
        var oldAspect = camera.aspect;
        var originalScale = wave.transform.localScale;
        var originalEnd = wave.EndPosition;
        var originalMesh = wave.Surface.GetComponent<MeshFilter>().sharedMesh;
        var directory = Path.GetFullPath("Library/CardGameTestResults/TsunamiAftermath/Previews");
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
                Assert.That(wave.transform.localScale, Is.EqualTo(originalScale), "Replay restores the artist's complete crest.");
                var endDistance = rig.Tsunami.WaveEndDistance;
                Assert.That(endDistance, Is.LessThan(wave.EndDistance), "The city edge must stop the original route before the mountains.");
                Assert.That(endDistance, Is.EqualTo(rig.Tsunami.CityBounds.max.z).Within(.0001f));
                AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, rig.Monster.WaveReleaseDelay + .2f);
                var view = aspect < 1f ? "tall" : "wide";
                var capturedMiddle = false;
                var capturedEnd = false;
                var capturedDissipation = false;
                var previousStrength = 1f;
                while (rig.Tsunami.IsRunning)
                {
                    if (wave.IsVisible)
                    {
                        AssertOpaqueWaveAtPlayerZoom(camera, wave, rig.Navigation);
                        Assert.That(wave.FrontDistance, Is.LessThanOrEqualTo(endDistance + .0001f));
                        var routeProgress = Mathf.InverseLerp(wave.StartDistance, endDistance, wave.FrontDistance);
                        var fullPosition = Vector3.Lerp(wave.StartPosition, wave.EndPosition,
                            Mathf.InverseLerp(wave.StartDistance, wave.EndDistance, wave.FrontDistance));
                        Assert.That(wave.transform.localPosition.x, Is.EqualTo(fullPosition.x).Within(.0001f));
                        Assert.That(wave.transform.localPosition.z, Is.EqualTo(fullPosition.z).Within(.0001f));
                        Assert.That(wave.transform.localScale.x, Is.EqualTo(originalScale.x));
                        Assert.That(wave.transform.localScale.z, Is.EqualTo(originalScale.z));
                        Assert.That(wave.transform.localScale.y, Is.EqualTo(originalScale.y * wave.Strength).Within(.00001f));
                        Assert.That(rig.Navigation.InputBlocked, Is.True);
                        if (!capturedMiddle && routeProgress >= .5f)
                        {
                            CaptureCenteredFrame(camera, Path.Combine(directory, view + "-crossing.png"));
                            capturedMiddle = true;
                        }
                        if (rig.Tsunami.HasReachedCityEnd)
                        {
                            Assert.That(wave.FrontDistance, Is.EqualTo(endDistance).Within(.0001f));
                            Assert.That(wave.Strength, Is.LessThanOrEqualTo(previousStrength + .00001f));
                            previousStrength = wave.Strength;
                            if (!capturedEnd)
                            {
                                CaptureCenteredFrame(camera, Path.Combine(directory, view + "-city-edge.png"));
                                capturedEnd = true;
                            }
                            if (!capturedDissipation && wave.Strength < .4f && wave.Strength > .05f)
                            {
                                CaptureCenteredFrame(camera, Path.Combine(directory, view + "-dissipating.png"));
                                capturedDissipation = true;
                            }
                        }
                    }
                    AdvanceTsunamiCamera(rig.Tsunami, rig.Navigation, .05f);
                }
                Assert.That(capturedMiddle && capturedEnd && capturedDissipation, Is.True);
                Assert.That(wave.FrontDistance, Is.EqualTo(endDistance).Within(.0001f));
                Assert.That(wave.IsVisible, Is.False);
                Assert.That(wave.transform.localScale, Is.EqualTo(originalScale));
                Assert.That(wave.EndPosition, Is.EqualTo(originalEnd));
                Assert.That(wave.Surface.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
                InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
                Assert.That(rig.Navigation.PanOffset, Is.EqualTo(homePan));
                Assert.That(ControlField<float>(rig.Navigation, "currentZoom"), Is.EqualTo(homeZoom));
                Assert.That(rig.Navigation.InputBlocked, Is.False);
            }
        }
        finally { rig.Tsunami.StopTsunami(); camera.aspect = oldAspect; }
        yield return null;
    }

    private static void AssertOpaqueWaveAtPlayerZoom(Camera camera, AuthoredTsunamiWave wave, CityCameraController navigation)
    {
        Assert.That(ControlField<float>(navigation, "currentZoom"), Is.EqualTo(ControlField<float>(navigation, "eventHomeZoom")).Within(.0001f),
            "Tsunami framing must retain the player's normal zoom even when the wave is wider than the screen.");
        var bounds = wave.Surface.localBounds;
        for (var i = 0; i < 8; i++)
        {
            var point = wave.Surface.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
            var view = camera.WorldToViewportPoint(point);
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
        Assert.That(wave.FrontDistance - start, Is.EqualTo(.1f).Within(.0001f));
        var wavePosition = wave.transform.localPosition;
        var waveWorldZ = rig.Tsunami.transform.InverseTransformPoint(wave.transform.position).z;
        var monsterZ = rig.Tsunami.transform.InverseTransformPoint(rig.Monster.Model.position).z;
        PresentationField(wave, "movementSpeed", .5f);
        InvokeCity(rig.Tsunami, "AdvanceTsunami", 0f);
        Assert.That(wave.transform.localPosition, Is.EqualTo(wavePosition), "Changing the Inspector speed must not teleport the wave.");
        rig.For(.6f);
        var travel = rig.Tsunami.transform.InverseTransformPoint(wave.transform.position).z - waveWorldZ;
        Assert.That(travel, Is.EqualTo(.3f).Within(.0001f));
        Assert.That(rig.Tsunami.transform.InverseTransformPoint(rig.Monster.Model.position).z - monsterZ,
            Is.EqualTo(travel * .8f).Within(.0001f), "The boss stays slower than the wave after a live speed change.");
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
        Assert.That(rig.Monster.HasFinishedEmerging, Is.False, "Landfall must not teleport the trailing boss to shore.");
        Assert.That(rig.Quake.IsRunning, Is.False);
        Assert.That(rig.Tsunami.FloodCoverage, Is.GreaterThan(0f));
        rig.Until(() => rig.Sequence.Stage == MonsterSequenceStage.WalkingToFirstPoint);
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
    public IEnumerator MonsterTrailsTheFasterSavedArtistWaveWhileTheNewMapAndExistingFloodRemainIntact()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var wave = ControlField<AuthoredTsunamiWave>(tsunami, "authoredWave");
        var waveId = wave.GetInstanceID();
        var mesh = wave.Surface.GetComponent<MeshFilter>().sharedMesh;
        var originalScale = wave.Surface.transform.lossyScale;
        var originalColors = wave.Surface.sharedMaterials.Select(material => material.GetColor("_BaseColor")).ToArray();
        var scenery = tsunami.GroundSurface.parent;
        Assert.That(scenery.name, Is.EqualTo("Map(Guncel1)"));
        Assert.That(scenery.gameObject.activeInHierarchy, Is.True);
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
        var firstWaveZ = tsunami.transform.InverseTransformPoint(wave.transform.position).z;
        var firstMonsterZ = tsunami.transform.InverseTransformPoint(monster.Model.position).z;
        CaptureCityWorldFrame(Path.Combine(directory, "02-map-wave.png"));
        InvokeCity(tsunami, "AdvanceTsunami", .8f);
        var waveTravel = tsunami.transform.InverseTransformPoint(wave.transform.position).z - firstWaveZ;
        var monsterTravel = tsunami.transform.InverseTransformPoint(monster.Model.position).z - firstMonsterZ;
        Assert.That(waveTravel, Is.EqualTo(.8f * tsunami.WaveSpeed).Within(.0001f));
        Assert.That(monsterTravel, Is.EqualTo(waveTravel * .8f).Within(.0001f), "The moving boss must fall behind the faster wave.");
        var initialSeparation = wave.StartDistance - (tsunami.transform.InverseTransformPoint(monster.EmergedWorldPosition).z - monster.ApproachDistance);
        Assert.That(initialSeparation, Is.GreaterThanOrEqualTo(4f), "The crest starts ahead of the boss.");
        Assert.That(wave.FrontDistance - tsunami.transform.InverseTransformPoint(monster.Model.position).z,
            Is.GreaterThan(initialSeparation), "The gap must grow during the approach.");
        Assert.That(wave.Surface.transform.lossyScale, Is.EqualTo(originalScale));
        CaptureCityWorldFrame(Path.Combine(directory, "03-map-wave-travelling.png"));
        var shoreTime = (ControlField<float>(tsunami, "shorelineDistance") - ControlField<float>(tsunami, "startDistance")) / tsunami.WaveSpeed;
        InvokeCity(tsunami, "AdvanceTsunami", shoreTime + .35f - ControlField<float>(tsunami, "elapsed"));
        Assert.That(tsunami.FloodCoverage, Is.GreaterThan(0f));
        Assert.That(wave.FrontDistance, Is.GreaterThan(ControlField<float>(tsunami, "shorelineDistance")),
            "The supplied wave continues across the whole saved route after first touching the city.");
        CaptureCityWorldFrame(Path.Combine(directory, "04-map-city-flood.png"));
        InvokeCity(tsunami, "AdvanceTsunami", ControlField<float>(tsunami, "fadeOutTime") * .5f);
        Assert.That(wave.IsVisible, Is.True);
        Assert.That(wave.Surface.transform.lossyScale, Is.EqualTo(originalScale));
        Assert.That(wave.transform.localPosition.y, Is.EqualTo(wave.StartPosition.y), "The path keeps the height shown in the Inspector.");
        InvokeCity(tsunami, "AdvanceTsunami", tsunami.TestDuration);
        Assert.That(tsunami.IsRunning || wave.IsVisible, Is.False);
        Assert.That(wave.FrontDistance, Is.EqualTo(tsunami.WaveEndDistance).Within(.0001f));
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
