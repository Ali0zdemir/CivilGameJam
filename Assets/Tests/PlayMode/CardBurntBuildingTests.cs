using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator DestroyedApartmentSprinklersStopForTsunamiWithoutLosingTheInstalledCard()
    {
        var links = CityHouseLinks();
        foreach (var link in links.Take(2)) link.FireProtection.SetInstalled(true);
        var fire = Object.FindFirstObjectByType<FireTestController>();
        fire.StartFire();
        AdvanceMonsterFireTo(fire, fire.SprinklerStartTime + .05f);
        yield return null;
        Assert.That(links.Take(2).All(link => link.FireProtection.IsSpraying), Is.True);
        var collapse = links[0].Building.GetComponent<ApartmentTsunamiCollapse>();
        Assert.That(collapse.TryCollapse(), Is.True);
        yield return null;
        yield return null;
        AssertApartmentWaterStopped(links[0].FireProtection);
        Assert.That(links[0].FireProtection.IsInstalled, Is.True);
        Assert.That(links[0].FireProtection.FireMultiplier, Is.EqualTo(1f));
        Assert.That(links[1].FireProtection.IsOperational && links[1].FireProtection.IsSpraying, Is.True);

        var destroyedSites = ControlField<IList>(fire, "sites").Cast<object>()
            .Where(site => SiteField<Transform>(site, "Building").IsChildOf(links[0].Building)).ToArray();
        Assert.That(destroyedSites.Length, Is.EqualTo(6));
        AdvanceMonsterFireTo(fire, destroyedSites.Max(site => SiteField<float>(site, "Delay")) + 2.1f);
        Assert.That(destroyedSites.Any(site => SiteField<bool>(site, "Extinguished")), Is.False,
            "A broken sprinkler cannot extinguish its building just because its card is still installed.");
        collapse.ResetCollapse();
        InvokeCity(links[0].FireProtection, "AdvanceSprinkler", .05f);
        Assert.That(links[0].FireProtection.IsOperational && links[0].FireProtection.IsSpraying, Is.True);
        fire.StopFire();
    }

    [UnityTest]
    public IEnumerator DestroyedApartmentSprinklersStopDuringEarthquakeCollapseButNotProtectedShaking()
    {
        var links = CityHouseLinks();
        foreach (var link in links.Take(2)) link.FireProtection.SetInstalled(true);
        links[1].RailSystem.SetInstalled(true);
        var fire = Object.FindFirstObjectByType<FireTestController>();
        fire.StartFire();
        AdvanceMonsterFireTo(fire, fire.SprinklerStartTime + .05f);
        yield return null;
        var collapse = links[0].Building.GetComponent<ApartmentEarthquakeAnimation>();
        var protectedShake = links[1].Building.GetComponent<ApartmentEarthquakeAnimation>();
        Assert.That(collapse.BeginEarthquake() && protectedShake.BeginEarthquake(), Is.True);
        collapse.SetEarthquakeProgress(.97f);
        protectedShake.SetEarthquakeProgress(.97f);
        Assert.That(links[0].Building.GetComponent<BuildingCollapseFeedback>().Reported, Is.True);
        Assert.That(collapse.HasCollapsed, Is.False, "Disable water during the fall, before the final collapsed pose.");
        yield return null;
        yield return null;
        AssertApartmentWaterStopped(links[0].FireProtection);
        Assert.That(links[1].FireProtection.IsOperational && links[1].FireProtection.IsSpraying, Is.True);
        collapse.FinishEarthquake(); protectedShake.FinishEarthquake();
        yield return null;
        AssertApartmentWaterStopped(links[0].FireProtection);
        Assert.That(links[0].FireProtection.IsInstalled, Is.True);
        collapse.ResetEarthquake();
        InvokeCity(links[0].FireProtection, "AdvanceSprinkler", .05f);
        Assert.That(links[0].FireProtection.IsOperational && links[0].FireProtection.IsSpraying, Is.True);
        fire.StopFire();
    }

    [UnityTest]
    public IEnumerator ImportedBurntApartmentsPersistAcrossElevationAndRestoreTheirOriginalArtOnReset()
    {
        var links = CityHouseLinks();
        links[0].FireProtection.SetInstalled(true);
        var target = links[1].Building;
        var renderers = new List<MeshRenderer>();
        target.GetComponent<BuildingModelGeometry>().AppendRenderers(renderers, true);
        var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
        var originalColors = materials.Select(material => material.GetColor("_BaseColor")).ToArray();
        var bodies = renderers.Select(surface => surface.GetComponent<MeshFilter>()).ToArray();
        var originalMeshes = bodies.Select(body => body.sharedMesh).ToArray();
        var originalRotations = bodies.Select(body => body.transform.localRotation).ToArray();
        var appearances = target.GetComponentsInChildren<ApartmentBurntAppearance>();
        Assert.That(appearances.Length, Is.EqualTo(6));
        var renderer = renderers[0];
        var original = new Color(.64f, .42f, .57f, 1f);
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", original); properties.SetFloat("_PreservedTestValue", .73f);
        renderer.SetPropertyBlock(properties);
        var ground = target.Find("Apartment_Ground").GetComponent<MeshRenderer>();
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var burnt = appearances[0].TargetColor(renderer, 0, Color.magenta);
        Assert.That(burnt.r, Is.InRange(.15f, .3f));
        PresentationField(fire, "burnColorDuration", .6f);
        fire.StartFire();
        var firstSite = ControlField<IList>(fire, "sites").Cast<object>().Single(site =>
            SiteField<Transform>(site, "Building") == target.GetComponent<BuildingModelGeometry>().Parts[0].transform);
        var firstIgnition = SiteField<float>(firstSite, "Delay");
        Assert.That(ColorDistance(ReadBurntSurfaceColor(renderer), original), Is.LessThan(.0001f), "Buildings retain their original colors before ignition.");
        AdvanceMonsterFireTo(fire, firstIgnition + .35f);
        var intermediate = ReadBurntSurfaceColor(renderer);
        Assert.That(ColorDistance(intermediate, burnt), Is.LessThan(ColorDistance(original, burnt)));
        Assert.That(ColorDistance(intermediate, burnt), Is.GreaterThan(.001f), "The burnt tint grows gradually with the fire.");
        AdvanceMonsterFireTo(fire, fire.TestDuration - .1f);
        Assert.That(appearances.All(appearance => appearance.IsBurnt), Is.True);
        Assert.That(renderers.All(surface => surface.sharedMaterials.All(material => material.name.StartsWith("burnt"))), Is.True);
        for (var i = 0; i < bodies.Length; i++)
            if (bodies[i].name.EndsWith("(Elevated)"))
                Assert.That(bodies[i].sharedMesh, Is.SameAs(originalMeshes[i]), "The raised ground floor keeps its authored geometry.");
            else Assert.That(bodies[i].sharedMesh.name, Does.EndWith("(Burnt)"));
        AdvanceMonsterFireTo(fire, fire.TestDuration + .1f);
        Assert.That(fire.IsRunning, Is.False);
        Assert.That(appearances.All(appearance => appearance.IsBurnt), Is.True);
        var protectedBodies = new List<MeshRenderer>();
        links[0].Building.GetComponent<BuildingModelGeometry>().AppendRenderers(protectedBodies);
        Assert.That(protectedBodies.All(surface => !surface.HasPropertyBlock()), Is.True, "Extinguished buildings do not receive the burnt tint.");
        Assert.That(links[0].Building.GetComponentsInChildren<ApartmentBurntAppearance>().Any(appearance => appearance.IsBurnt), Is.False);
        Assert.That(ground.HasPropertyBlock(), Is.False, "Trees and the tile ground keep their original appearance.");
        Assert.That(materials.Select(material => material.GetColor("_BaseColor")), Is.EqualTo(originalColors));
        Assert.That(links[1].House.TryInstall(ProduceCityProducts(UpgradeKind.Breakwater)[0]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(target.GetComponent<ApartmentUpgradeModel>().IsElevated, Is.True);
        Assert.That(renderers.Where(surface => surface.gameObject.activeInHierarchy)
            .All(surface => surface.sharedMaterials.All(material => material.name.StartsWith("burnt"))), Is.True);
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/BurntBuildingsWork/Previews"));
        Directory.CreateDirectory(directory);
        CaptureCityWorldFrame(Path.Combine(directory, "01-burnt-city.png"));
        fire.StopFire();
        Assert.That(appearances.Any(appearance => appearance.IsBurnt), Is.False);
        Assert.That(bodies.Select(body => body.sharedMesh), Is.EqualTo(originalMeshes));
        Assert.That(bodies.Select(body => body.transform.localRotation), Is.EqualTo(originalRotations));
        Assert.That(ColorDistance(ReadBurntSurfaceColor(renderer), original), Is.LessThan(.0001f));
        renderer.GetPropertyBlock(properties);
        Assert.That(properties.GetFloat("_PreservedTestValue"), Is.EqualTo(.73f));
        renderer.GetPropertyBlock(properties, 0);
        Assert.That(properties.isEmpty, Is.True, "Reset restores the pre-fire material overrides exactly.");
        Assert.That(materials.Select(material => material.GetColor("_BaseColor")), Is.EqualTo(originalColors));
        fire.StartFire();
        AdvanceMonsterFireTo(fire, fire.TestDuration + .1f);
        Assert.That(renderers.All(surface => surface.sharedMaterials.All(material => material.name.StartsWith("burnt"))), Is.True,
            "Both apartment variants reuse the imported burnt materials on the next fire.");
        fire.StopFire();
    }

    [UnityTest]
    public IEnumerator NonResidentialBuildingsKeepTheirAppearanceAndStayIntactThroughEveryMonsterSequence()
    {
        var rig = new SequencePreview();
        var otherBuildings = rig.Tsunami.BuildingsRoot.Cast<Transform>()
            .Where(building => !building.GetComponent<BuildingModelGeometry>().IsResidential).ToArray();
        Assert.That(otherBuildings.Length, Is.EqualTo(3), "The school, factory and research center remain protected.");
        var meshes = otherBuildings.SelectMany(building => building.GetComponentsInChildren<MeshFilter>(true)).ToArray();
        var positions = meshes.Select(mesh => mesh.transform.localPosition).ToArray();
        var rotations = meshes.Select(mesh => mesh.transform.localRotation).ToArray();
        var scales = meshes.Select(mesh => mesh.transform.localScale).ToArray();
        var surfaces = otherBuildings.SelectMany(building => building.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var colors = surfaces.Select(ReadBurntSurfaceColor).ToArray();
        void AssertBuildingsIntact()
        {
            Assert.That(meshes.Select(mesh => mesh.transform.localPosition), Is.EqualTo(positions));
            Assert.That(meshes.Select(mesh => mesh.transform.localRotation), Is.EqualTo(rotations));
            Assert.That(meshes.Select(mesh => mesh.transform.localScale), Is.EqualTo(scales));
            for (var i = 0; i < surfaces.Length; i++)
                Assert.That(ColorDistance(ReadBurntSurfaceColor(surfaces[i]), colors[i]), Is.LessThan(.0001f));
            Assert.That(otherBuildings.SelectMany(building => building.GetComponentsInChildren<ApartmentEarthquakeAnimation>(true))
                .Any(animation => animation.HasCollapsed), Is.False);
            Assert.That(otherBuildings.SelectMany(building => building.GetComponentsInChildren<ApartmentTsunamiCollapse>(true))
                .Any(animation => animation.HasCollapsed), Is.False);
        }
        for (var number = 1; number <= 3; number++)
        {
            rig.Sequence.SelectSequence(number); rig.Sequence.StartSelectedSequence();
            if (number == 3)
            {
                rig.Until(() => rig.Sequence.Stage == MonsterSequenceStage.Fire);
                AdvanceMonsterFireTo(rig.Fire, rig.Fire.BuildingIgnitionTime + 6f);
                AssertBuildingsIntact();
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ResidentialDisasterWork/Previews"));
                Directory.CreateDirectory(directory);
                CaptureCityWorldFrame(Path.Combine(directory, "01-residential-fire.png"));
            }
            rig.Until(() => !rig.Sequence.IsRunning);
            AssertBuildingsIntact();
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator NonResidentialGeometryCannotUseTheApartmentCollapseClips()
    {
        var buildings = Object.FindFirstObjectByType<TsunamiTestController>().BuildingsRoot;
        var other = buildings.GetComponentsInChildren<BuildingModelGeometry>(true)
            .First(model => !model.IsCollection && !model.IsResidential);
        var definition = ControlField<BuildingModelDefinition>(other, "definition");
        Assert.That(definition.IsResidential, Is.False);
        var house = CityHouseLinks()[0].Building;
        foreach (var part in house.GetComponent<BuildingModelGeometry>().Parts)
            PresentationField(part, "definition", definition);
        Assert.That(house.GetComponent<BuildingModelGeometry>().IsResidential, Is.False);
        Assert.That(house.GetComponent<ApartmentTsunamiCollapse>().TryCollapse(), Is.False);
        Assert.That(house.GetComponent<ApartmentEarthquakeAnimation>().BeginEarthquake(), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MonsterFireOpensSprinklersOnlyAfterItsFlameHasReachedTheCity()
    {
        var link = CityHouseLinks()[0];
        link.FireProtection.SetInstalled(true);
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        fire.StartFire();
        foreach (var time in new[] { breath.ChargeDuration * .5f, breath.FlameStartTime + .15f, fire.CityIgnitionTime + .1f })
        {
            AdvanceMonsterFireTo(fire, time);
            InvokeCity(link.FireProtection, "AdvanceSprinkler", .05f);
            Assert.That(link.FireProtection.IsInstalled && link.FireProtection.IsOperational, Is.True);
            Assert.That(fire.SprinklersActive || link.FireProtection.IsSpraying, Is.False,
                "An installed sprinkler waits through charging, leaning and the approaching flame.");
            foreach (var part in ControlField<HouseSprinklerUpgrade[]>(link.FireProtection, "partUpgrades"))
                Assert.That(ControlField<GameObject>(part, "waterVisuals").activeSelf, Is.False);
        }
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/FireResultTimingWork/Previews"));
        Directory.CreateDirectory(directory);
        CaptureCityWorldFrame(Path.Combine(directory, "01-fire-before-sprinklers.png"));
        AdvanceMonsterFireTo(fire, fire.SprinklerStartTime + .05f);
        InvokeCity(link.FireProtection, "AdvanceSprinkler", .2f);
        Assert.That(fire.SprinklersActive && link.FireProtection.IsSpraying, Is.True);
        Assert.That(breath.Heat, Is.GreaterThan(.9f), "The monster has actually started breathing fire before water starts.");
        Assert.That(fire.WasHouseBurnt(link.Building), Is.False);
        CaptureCityWorldFrame(Path.Combine(directory, "02-sprinklers-after-fire.png"));
        fire.StopFire();
        InvokeCity(link.FireProtection, "AdvanceSprinkler", .05f);
        Assert.That(link.FireProtection.IsSpraying || fire.SprinklersActive, Is.False);
        yield return null;
    }

    private static void AssertApartmentWaterStopped(HouseSprinklerUpgrade upgrade)
    {
        Assert.That(upgrade.IsOperational || upgrade.IsSpraying, Is.False);
        foreach (var part in ControlField<HouseSprinklerUpgrade[]>(upgrade, "partUpgrades"))
        {
            Assert.That(part.IsOperational || part.IsSpraying, Is.False);
            Assert.That(ControlField<GameObject>(part, "waterVisuals").activeSelf, Is.False);
            Assert.That(ControlField<ParticleSystem>(part, "droplets").particleCount, Is.Zero);
            Assert.That(ControlField<ParticleSystem>(part, "roofRunoff").particleCount, Is.Zero);
        }
    }

    private static Color ReadBurntSurfaceColor(MeshRenderer renderer)
    {
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties, 0);
        if (properties.isEmpty) renderer.GetPropertyBlock(properties);
        return properties.HasColor("_BaseColor") ? properties.GetColor("_BaseColor") : renderer.sharedMaterial.GetColor("_BaseColor");
    }

    private static float ColorDistance(Color a, Color b) => Vector4.Distance(a, b);
}
