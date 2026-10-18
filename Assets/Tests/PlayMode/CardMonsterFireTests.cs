using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator MonsterFireKeepsItsColorWhileLeaningAndSpreadingFromItsMouthAfterTheEarthquake()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var ids = monster.Model.GetComponentsInChildren<Transform>(true).Select(part => part.GetInstanceID()).ToArray();
        quake.StartEarthquake();
        InvokeCity(quake, "AdvanceShake", quake.TestDuration);
        var stance = monster.Model.localPosition;
        var leftFoot = monster.Model.Find("Left_Foot").position;
        var rightFoot = monster.Model.Find("Right_Foot").position;
        var body = monster.Model.GetComponentInChildren<SkinnedMeshRenderer>(true);
        var originalColor = body.sharedMaterial.GetColor("_BaseColor");
        ControlField<Button>(fire, "testButton").onClick.Invoke();
        Assert.That(fire.IsRunning, Is.True);
        Assert.That(navigation.InputBlocked, Is.True);
        Assert.That(monster.Model.localPosition, Is.EqualTo(stance));
        Assert.That(monster.Model.GetComponentsInChildren<Transform>(true).Select(part => part.GetInstanceID()).ToArray(), Is.EqualTo(ids),
            "The fire must animate the saved monster, including its saved mouth.");
        AdvanceMonsterFireTo(fire, breath.ChargeDuration * 0.5f);
        Assert.That(breath.Heat, Is.InRange(0.45f, 0.55f));
        Assert.That(breath.Lean, Is.Zero, "The monster charges before it bends.");
        var properties = new MaterialPropertyBlock(); body.GetPropertyBlock(properties);
        Assert.That(properties.HasProperty("_BaseColor"), Is.False, "Charging must not tint the monster red.");
        Assert.That(body.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(originalColor));
        Assert.That(fire.IgnitedBuildingCount, Is.Zero);
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterFireWork"));
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "01-monster-charging.png"));

        AdvanceMonsterFireTo(fire, breath.FlameStartTime + breath.TravelDuration * 0.45f);
        Assert.That(breath.Lean, Is.GreaterThan(0.95f));
        body.GetPropertyBlock(properties);
        Assert.That(properties.HasProperty("_BaseColor"), Is.False, "Breathing fire must keep the monster's original color.");
        Assert.That(body.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(originalColor));
        Assert.That(Vector3.Distance(monster.Model.Find("Left_Foot").position, leftFoot), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(monster.Model.Find("Right_Foot").position, rightFoot), Is.LessThan(0.0001f));
        Assert.That(fire.IgnitedBuildingCount, Is.Zero, "Buildings cannot ignite before the flame reaches the city.");
        var visuals = ControlField<GameObject>(fire, "visuals");
        var stream = visuals.transform.Find("Mouth_Fire_Stream");
        Assert.That(stream.gameObject.activeSelf, Is.True);
        var coreMesh = stream.GetComponentsInChildren<MeshFilter>().Single();
        Assert.That(coreMesh.sharedMesh.name, Is.EqualTo("Animated_flame1"), "The source animation's flame is surrounded by traveling wisps.");
        var gas = stream.Find("Rolling_Gas_Wisps").GetComponent<ParticleSystem>();
        var gasParticles = new ParticleSystem.Particle[640];
        var gasCount = gas.GetParticles(gasParticles);
        Assert.That(gasCount, Is.GreaterThan(30));
        Assert.That(gasParticles.Take(gasCount).Min(particle => Vector3.Distance(particle.position, breath.Mouth.position)), Is.LessThan(0.3f),
            "The advected flame originates at the mouth.");
        yield return CaptureCanvas(Path.Combine(directory, "02-flame-leaving-mouth.png"));

        AdvanceMonsterFireTo(fire, fire.CityIgnitionTime + 0.9f);
        Assert.That(fire.IgnitedBuildingCount, Is.Zero, "Soft fire must spread between buildings before roof flames begin.");
        var earlyStreetCells = fire.ActiveStreetFireCells;
        Assert.That(earlyStreetCells, Is.GreaterThan(5));
        Assert.That(ControlField<ParticleSystem>(fire, "flames").particleCount, Is.Zero);
        Assert.That(ControlField<ParticleSystem>(fire, "softFlames").particleCount, Is.GreaterThan(0));
        yield return CaptureCanvas(Path.Combine(directory, "02b-soft-fire-between-buildings.png"));
        gasCount = gas.GetParticles(gasParticles);
        var near = gasParticles.Take(gasCount).Where(particle => particle.startLifetime - particle.remainingLifetime < 0.2f).ToArray();
        var far = gasParticles.Take(gasCount).Where(particle => particle.startLifetime - particle.remainingLifetime > 0.55f).ToArray();
        var spreadAxis = Vector3.Cross(Vector3.up, breath.FireDirection).normalized;
        float Width(ParticleSystem.Particle[] group) => group.Max(particle => Vector3.Dot(particle.position, spreadAxis))
            - group.Min(particle => Vector3.Dot(particle.position, spreadAxis));
        Assert.That(Width(far), Is.GreaterThan(Width(near) * 2f), "The gas still spreads into a cone.");
        Assert.That(Width(far), Is.GreaterThan(Vector3.Distance(breath.AimedMouthPosition, ControlField<Vector3>(fire, "ignitionPoint")) * 0.35f),
            "The gas spreads across the landing area even at a steep downward angle.");
        AdvanceMonsterFireTo(fire, fire.BuildingIgnitionTime - 0.15f);
        Assert.That(fire.IgnitedBuildingCount, Is.Zero);
        Assert.That(fire.ActiveStreetFireCells, Is.GreaterThan(earlyStreetCells));
        Assert.That(stream.gameObject.activeSelf, Is.False, "The soft street fire remains visible after the breath clears.");
        yield return CaptureCanvas(Path.Combine(directory, "02c-street-fire-before-roof-flames.png"));
        AdvanceMonsterFireTo(fire, fire.BuildingIgnitionTime + 0.4f);
        var firstCount = fire.IgnitedBuildingCount;
        Assert.That(firstCount, Is.GreaterThan(0));
        AdvanceMonsterFireTo(fire, fire.BuildingIgnitionTime + 2f);
        Assert.That(fire.IgnitedBuildingCount, Is.GreaterThan(firstCount));
        Assert.That(fire.ActiveStreetFireCells, Is.GreaterThan(earlyStreetCells));
        yield return CaptureCanvas(Path.Combine(directory, "03-main-fire-starting.png"));
        AdvanceMonsterFireTo(fire, fire.BuildingIgnitionTime + ControlField<float>(fire, "spreadTime") + 0.9f);
        Assert.That(fire.IgnitedBuildingCount, Is.EqualTo(ControlField<IList>(fire, "sites").Count));
        Assert.That(ControlField<ParticleSystem>(fire, "flames").particleCount, Is.GreaterThan(50));
        yield return CaptureCanvas(Path.Combine(directory, "04-city-fire.png"));
        var departurePosition = monster.Model.localPosition;
        AdvanceMonsterFireTo(fire, fire.TestDuration);
        Assert.That(fire.IsRunning, Is.False);
        Assert.That(navigation.InputBlocked, Is.False);
        Assert.That(fire.ActiveStreetFireCells, Is.Zero);
        Assert.That(ControlField<ParticleSystem>(fire, "softFlames").particleCount, Is.Zero);
        Assert.That(breath.Heat + breath.Lean, Is.Zero);
        Assert.That(monster.Model.localPosition, Is.EqualTo(departurePosition), "The city fire ending cannot move the departing monster back to its firing point.");
        Assert.That(body.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(originalColor), "Shared model materials must remain unchanged.");
        body.GetPropertyBlock(properties);
        Assert.That(properties.HasProperty("_BaseColor"), Is.False, "The monster's colors stay unchanged after fire ends.");
    }

    [UnityTest]
    public IEnumerator MonsterFireWorksAloneAndHandsItsSavedPoseBackToOtherDisasters()
    {
        foreach (var standalone in new[] { false })
        {
            if (standalone) { yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("BuildingShowcase"); yield return null; }
            var fire = Object.FindFirstObjectByType<FireTestController>();
            var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
            var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
            var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
            fire.StartFire();
            Assert.That(walk.CompletedSteps, Is.EqualTo(walk.StepCount));
            Assert.That(monster.EmergenceProgress, Is.EqualTo(1f));
            var finalPosition = monster.Model.localPosition;
            AdvanceMonsterFireTo(fire, breath.FlameStartTime + 0.3f);
            Assert.That(breath.Lean, Is.GreaterThan(0.95f));
            fire.StopFire();
            Assert.That(breath.Heat + breath.Lean, Is.Zero);
            Assert.That(monster.Model.localPosition, Is.EqualTo(finalPosition));
            fire.StartFire();
            Assert.That(monster.Model.localPosition, Is.EqualTo(finalPosition), "Repeating FIRE must not advance the monster again.");
            AdvanceMonsterFireTo(fire, breath.FlameStartTime + 0.3f);
            quake.StartEarthquake();
            Assert.That(fire.IsRunning, Is.False);
            Assert.That(breath.Heat + breath.Lean, Is.Zero);
            InvokeCity(quake, "AdvanceShake", walk.StepDuration * 0.5f);
            fire.StartFire();
            Assert.That(quake.IsRunning, Is.False);
            Assert.That(walk.CompletedSteps, Is.EqualTo(walk.StepCount), "Interrupting a quake for FIRE settles the final stance.");
            Assert.That(monster.Model.localPosition, Is.EqualTo(finalPosition));
            AdvanceMonsterFireTo(fire, breath.FlameStartTime + 0.3f);
            tsunami.StartTsunami();
            Assert.That(fire.IsRunning, Is.False);
            Assert.That(breath.Heat + breath.Lean, Is.Zero);
            Assert.That(walk.CompletedSteps, Is.Zero);
            Assert.That(monster.Model.Find("Head").localRotation, Is.EqualTo(Quaternion.identity));
            tsunami.StopTsunami();
        }
    }

    [UnityTest]
    public IEnumerator SpreadingMonsterFireStillExtinguishesEachProtectedBuilding()
    {
        var link = CityHouseLinks()[0];
        link.FireProtection.SetInstalled(true);
        var fire = Object.FindFirstObjectByType<FireTestController>();
        fire.StartFire();
        var sites = ControlField<IList>(fire, "sites").Cast<object>().ToArray();
        var protectedSites = sites.Where(site => SiteField<HouseSprinklerUpgrade>(site, "Sprinkler") is HouseSprinklerUpgrade sprinkler && sprinkler.IsInstalled).ToArray();
        Assert.That(protectedSites.Length, Is.GreaterThan(1), "Collection upgrades must protect their individual imported buildings.");
        var time = protectedSites.Max(site => SiteField<float>(site, "Delay")) + 2.05f;
        Assert.That(time, Is.LessThan(fire.TestDuration - 1.6f));
        AdvanceMonsterFireTo(fire, time);
        var particles = new ParticleSystem.Particle[1600];
        var count = ControlField<ParticleSystem>(fire, "flames").GetParticles(particles);
        foreach (var site in protectedSites)
        {
            Assert.That(SiteField<bool>(site, "Extinguished"), Is.True);
            var seed = SiteField<uint>(site, "ParticleSeed");
            Assert.That(particles.Take(count).Any(particle => particle.randomSeed == seed), Is.False);
        }
        Assert.That(fire.IgnitedBuildingCount, Is.GreaterThan(0), "Unprotected neighbors keep burning.");
        fire.StopFire();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ImportedMonsterFlamesRollDownwardWithoutMouthSmokeAndFadeAtTheGround()
    {
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        fire.StartFire();
        AdvanceMonsterFireTo(fire, breath.FlameStartTime + 1.1f);
        var stream = ControlField<GameObject>(fire, "visuals").transform.Find("Mouth_Fire_Stream");
        var authoredFlame = stream.Find("Authored_Flame_Animation");
        var preset = ControlField<FireVisualPreset>(fire, "visualPreset");
        Assert.That(preset.BreathAnimation.legacy || authoredFlame.GetComponent<Animator>() != null, Is.True,
            "Imported non-Legacy clips need an Animator to sample the flame in standalone builds.");
        var gas = stream.Find("Rolling_Gas_Wisps").GetComponent<ParticleSystem>();
        foreach (var system in new[] { gas, ControlField<ParticleSystem>(fire, "flames"), ControlField<ParticleSystem>(fire, "softFlames") })
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Mesh));
            Assert.That(renderer.meshCount, Is.EqualTo(4), "The composition combines every imported flame.");
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo("ImportedFlames"));
            Assert.That(system.useAutoRandomSeed, Is.False);
        }
        Assert.That(stream.Find("Breath_Black_Smoke"), Is.Null, "The mouth emits flames without black smoke.");
        Assert.That(stream.GetComponentsInChildren<ParticleSystem>(true), Is.EqualTo(new[] { gas }));
        Assert.That(ControlField<ParticleSystem>(fire, "smoke").GetComponent<ParticleSystemRenderer>().meshCount, Is.EqualTo(3),
            "Burning buildings keep their imported smoke.");
        var forward = (ControlField<Vector3>(fire, "ignitionPoint") - breath.Mouth.position).normalized;
        var innerRollRadius = Vector3.Distance(ControlField<Vector3>(fire, "ignitionPoint"), breath.Mouth.position) * .1f;
        var buffer = new ParticleSystem.Particle[640];
        var flameCount = gas.GetParticles(buffer);
        Assert.That(buffer.Take(flameCount).Select(particle => particle.GetMeshIndex(gas)).Distinct().Count(), Is.EqualTo(4));
        var inward = 0; var turns = 0;
        var previous = new System.Collections.Generic.Dictionary<uint, Vector3>();
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterGasFireWork/Frames"));
        Directory.CreateDirectory(directory);
        for (var frame = 0; frame < 16; frame++)
        {
            AdvanceMonsterFireTo(fire, breath.FlameStartTime + 1.1f + frame * 0.08f);
            var count = gas.GetParticles(buffer);
            Assert.That(count, Is.InRange(100, 640));
            for (var i = 0; i < count; i++)
            {
                var particle = buffer[i]; var offset = particle.position - breath.Mouth.position;
                var radial = offset - forward * Vector3.Dot(offset, forward);
                Assert.That(particle.velocity.y, Is.LessThan(0f), "Rolling must keep the fire traveling down.");
                if (radial.magnitude > innerRollRadius && Vector3.Dot(particle.velocity, radial.normalized) < -0.1f) inward++;
                if (previous.TryGetValue(particle.randomSeed, out var velocity) && Vector3.Angle(velocity, particle.velocity) > 4f) turns++;
                previous[particle.randomSeed] = particle.velocity;
            }
            CaptureCityWorldFrame(Path.Combine(directory, "gas-" + frame.ToString("D3") + ".png"));
        }
        Assert.That(inward, Is.GreaterThan(5), "The moving eddies must actually pull some material back inward.");
        Assert.That(turns, Is.GreaterThan(20), "Existing flame wisps change direction as they advect through the vortex field.");
        AdvanceMonsterFireTo(fire, breath.FlameStartTime + breath.BreathDuration + breath.TravelDuration + 0.05f);
        Assert.That(gas.particleCount, Is.Zero);
        Assert.That(stream.gameObject.activeSelf, Is.False);
        fire.StopFire();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ImportedFireReplaysTheSameFlameComposition()
    {
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        ParticleSystem.Particle[][] first = null;
        int[][] firstMeshes = null;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            fire.StartFire();
            AdvanceMonsterFireTo(fire, fire.CityIgnitionTime + .9f);
            var stream = ControlField<GameObject>(fire, "visuals").transform.Find("Mouth_Fire_Stream");
            var systems = new[]
            {
                stream.Find("Rolling_Gas_Wisps").GetComponent<ParticleSystem>(),
                ControlField<ParticleSystem>(fire, "softFlames")
            };
            var samples = systems.Select(system =>
            {
                var buffer = new ParticleSystem.Particle[system.main.maxParticles];
                return buffer.Take(system.GetParticles(buffer)).ToArray();
            }).ToArray();
            var meshes = systems.Select((system, index) => samples[index].Select(particle => particle.GetMeshIndex(system)).ToArray()).ToArray();
            if (repeat == 0) { first = samples; firstMeshes = meshes; }
            else for (var layer = 0; layer < systems.Length; layer++)
            {
                Assert.That(samples[layer].Length, Is.GreaterThan(0));
                Assert.That(meshes[layer], Is.EqualTo(firstMeshes[layer]), "Restarting must replay the saved mix of models.");
                Assert.That(samples[layer].Select(particle => particle.randomSeed), Is.EqualTo(first[layer].Select(particle => particle.randomSeed)));
                for (var i = 0; i < samples[layer].Length; i++)
                {
                    Assert.That(Vector3.Distance(samples[layer][i].position, first[layer][i].position), Is.LessThan(.0001f));
                    Assert.That(samples[layer][i].startSize3D, Is.EqualTo(first[layer][i].startSize3D));
                    Assert.That(samples[layer][i].rotation3D, Is.EqualTo(first[layer][i].rotation3D));
                    Assert.That(samples[layer][i].startColor, Is.EqualTo(first[layer][i].startColor));
                }
            }
            fire.StopFire();
            Assert.That(systems.All(system => system.particleCount == 0), Is.True);
        }
        yield return null;
    }

    private static void CaptureCityWorldFrame(string path)
    {
        var camera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
        var previousTarget = camera.targetTexture; var previousAspect = camera.aspect;
        var target = new RenderTexture(1280, 640, 24); var previousActive = RenderTexture.active;
        var pixels = new Texture2D(1280, 640, TextureFormat.RGB24, false);
        camera.targetTexture = target; camera.aspect = 2f; camera.Render(); RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1280, 640), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
        camera.targetTexture = previousTarget; camera.aspect = previousAspect; RenderTexture.active = previousActive;
        target.Release(); Object.Destroy(target); Object.Destroy(pixels);
    }

    [UnityTest]
    public IEnumerator StreetFireUsesFewerFlamesWhileKeepingTheSameSpreadTiming()
    {
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var savedDensity = ControlField<float>(fire, "streetFlameDensity");
        var visible = new int[2];
        var activeCells = new int[2];
        for (var run = 0; run < 2; run++)
        {
            PresentationField(fire, "streetFlameDensity", run == 0 ? savedDensity : 1f);
            fire.StartFire();
            AdvanceMonsterFireTo(fire, fire.CityIgnitionTime + 2f);
            visible[run] = ControlField<ParticleSystem>(fire, "softFlames").particleCount;
            activeCells[run] = fire.ActiveStreetFireCells;
            fire.StopFire();
        }
        PresentationField(fire, "streetFlameDensity", savedDensity);
        Assert.That(activeCells[0], Is.EqualTo(activeCells[1]));
        Assert.That(visible[0], Is.GreaterThan(0));
        Assert.That(visible[0], Is.LessThan(visible[1] * .3f), "Ground fire should stay in sparse patches instead of covering the streets.");
        yield return null;
    }

    private static T SiteField<T>(object site, string name) => (T)site.GetType().GetField(name).GetValue(site);

    private static void AdvanceMonsterFireTo(FireTestController fire, float target)
    {
        while (fire.IsRunning && ControlField<float>(fire, "elapsed") < target - 0.00001f)
        {
            var step = Mathf.Min(0.05f, target - ControlField<float>(fire, "elapsed"));
            InvokeCity(fire, "AdvanceFire", step);
            if (!fire.IsRunning) break;
            foreach (var particles in ControlField<GameObject>(fire, "visuals").GetComponentsInChildren<ParticleSystem>())
                particles.Simulate(step, false, false, false);
        }
    }
}
