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
    public IEnumerator MonsterFireHeatsThenLeansAndSpreadsFromItsMouthAfterTheEarthquake()
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
        var body = monster.Model.Find("Body").GetComponent<MeshRenderer>();
        var originalColor = body.sharedMaterial.GetColor("_BaseColor");
        ControlField<Button>(fire, "testButton").onClick.Invoke();
        Assert.That(fire.IsRunning, Is.True);
        Assert.That(navigation.InputBlocked, Is.True);
        Assert.That(monster.Model.localPosition, Is.EqualTo(stance));
        Assert.That(monster.Model.GetComponentsInChildren<Transform>(true).Select(part => part.GetInstanceID()).ToArray(), Is.EqualTo(ids),
            "The fire must animate the saved monster, including its saved mouth.");
        AdvanceMonsterFireTo(fire, breath.ChargeDuration * 0.5f);
        Assert.That(breath.Heat, Is.InRange(0.45f, 0.55f));
        Assert.That(breath.Lean, Is.Zero, "The monster heats before it bends.");
        var properties = new MaterialPropertyBlock(); body.GetPropertyBlock(properties);
        var warm = properties.GetColor("_BaseColor");
        Assert.That(warm.r, Is.GreaterThan(originalColor.r));
        Assert.That(warm.r, Is.LessThan(0.8f));
        Assert.That(warm.g, Is.GreaterThan(0.15f));
        Assert.That(fire.IgnitedBuildingCount, Is.Zero);
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterFireWork"));
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "01-monster-warming.png"));

        AdvanceMonsterFireTo(fire, breath.FlameStartTime + breath.TravelDuration * 0.45f);
        Assert.That(breath.Lean, Is.GreaterThan(0.95f));
        Assert.That(Vector3.Distance(monster.Model.Find("Left_Foot").position, leftFoot), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(monster.Model.Find("Right_Foot").position, rightFoot), Is.LessThan(0.0001f));
        Assert.That(fire.IgnitedBuildingCount, Is.Zero, "Buildings cannot ignite before the flame reaches the city.");
        var visuals = ControlField<GameObject>(fire, "visuals");
        var stream = visuals.transform.Find("Mouth_Fire_Stream");
        Assert.That(stream.gameObject.activeSelf, Is.True);
        Assert.That(stream.GetComponentsInChildren<MeshFilter>().Length, Is.Zero, "The breath has no closed, solid cone surface.");
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
        Assert.That(properties.HasProperty("_BaseColor"), Is.False, "The original renderer properties are restored.");
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
    public IEnumerator MonsterFireGasRollsInwardWhileTravelingAndFadesWithoutSolidFlameMeshes()
    {
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        fire.StartFire();
        AdvanceMonsterFireTo(fire, breath.FlameStartTime + 1.1f);
        var stream = ControlField<GameObject>(fire, "visuals").transform.Find("Mouth_Fire_Stream");
        var gas = stream.Find("Rolling_Gas_Wisps").GetComponent<ParticleSystem>();
        foreach (var system in new[] { gas, ControlField<ParticleSystem>(fire, "flames"), ControlField<ParticleSystem>(fire, "softFlames") })
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Billboard));
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("CivilGameJam/GasFlame"));
        }
        var forward = (ControlField<Vector3>(fire, "ignitionPoint") - breath.Mouth.position).normalized;
        var buffer = new ParticleSystem.Particle[640];
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
                if (radial.magnitude > 0.4f && Vector3.Dot(particle.velocity, radial.normalized) < -0.1f) inward++;
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
