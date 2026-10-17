using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    private static Transform[] AuthoredApartmentSprinklers(ApartmentUpgradeModel model, bool elevated) =>
        ControlField<GameObject>(model, elevated ? "elevatedModel" : "normalModel").GetComponentsInChildren<Transform>(true)
            .Where(part => part.name.StartsWith("Sprinkler") && part.GetComponent<MeshFilter>() != null).ToArray();

    private static void AssertApartmentFireVisibility(ApartmentUpgradeModel model, bool installed)
    {
        foreach (bool elevated in new[] { false, true })
        {
            var parts = AuthoredApartmentSprinklers(model, elevated);
            Assert.That(parts.Length, Is.EqualTo(12));
            foreach (var part in parts)
            {
                Assert.That(part.gameObject.activeSelf, Is.EqualTo(installed));
                Assert.That(part.gameObject.activeInHierarchy, Is.EqualTo(installed && elevated == model.IsElevated));
            }
        }
        Assert.That(model.HasFireProtection, Is.EqualTo(installed));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FireCardsOpenOnlyTheirApartmentsOriginalSprinklersAndCloseBothVariants(int index)
    {
        var links = CityHouseLinks();
        var product = ProduceCityProducts(UpgradeKind.Fire)[0];
        var ui = UIIds(game);
        var target = links[index];
        var model = target.Building.GetComponent<ApartmentUpgradeModel>();
        var original = AuthoredApartmentSprinklers(model, false).Concat(AuthoredApartmentSprinklers(model, true)).ToArray();
        var meshes = original.Select(part => part.GetComponent<MeshFilter>().sharedMesh).ToArray();
        Assert.That(target.House.TryInstall(product), Is.True);
        for (int i = 0; i < links.Length; i++) AssertApartmentFireVisibility(links[i].Building.GetComponent<ApartmentUpgradeModel>(), i == index);
        Assert.That(target.Building.GetComponentsInChildren<Transform>(true).Any(part => part.name == "Roof_Sprinkler"), Is.False,
            "The water effect must use the imported sprinkler rather than add another solid sprinkler model.");
        Assert.That(target.House.Release(UpgradeKind.Fire, false), Is.SameAs(product));
        AssertApartmentFireVisibility(model, false);
        Assert.That(target.FireProtection.FireMultiplier, Is.EqualTo(1f));
        Assert.That(original.Select(part => part.GetComponent<MeshFilter>().sharedMesh), Is.EqualTo(meshes));
        Assert.That(UIIds(game), Is.EqualTo(ui));
    }

    [UnityTest]
    public IEnumerator FireSprinklersRemainInstalledAndSprayThroughBothApartmentModelTransitions()
    {
        var link = CityHouseLinks()[0];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        var fireProduct = ProduceCityProducts(UpgradeKind.Fire)[0];
        var elevator = ProduceCityProducts(UpgradeKind.Breakwater)[0];
        link.House.TryInstall(fireProduct);
        var fire = Object.FindFirstObjectByType<FireTestController>();
        fire.StartFire();
        yield return null;
        Assert.That(link.FireProtection.IsSpraying, Is.True);
        foreach (bool elevated in new[] { true, false })
        {
            if (elevated) link.House.TryInstall(elevator);
            else link.House.Release(UpgradeKind.Breakwater, false);
            float timeout = Time.realtimeSinceStartup + 2f;
            while (model.IsTransitioning && Time.realtimeSinceStartup < timeout)
            {
                AssertApartmentFireVisibility(model, true);
                Assert.That(link.FireProtection.FireMultiplier, Is.EqualTo(.25f));
                yield return null;
            }
            Assert.That(model.IsElevated, Is.EqualTo(elevated));
            AssertApartmentFireVisibility(model, true);
            Assert.That(link.FireProtection.IsInstalled && link.FireProtection.IsSpraying, Is.True);
            var parts = ControlField<HouseSprinklerUpgrade[]>(link.FireProtection, "partUpgrades");
            for (int index = 0; index < parts.Length; index++)
            {
                var renderer = model.SprinklerHead(index).GetComponent<MeshRenderer>();
                Bounds bounds = renderer.localBounds;
                var outlet = bounds.center; outlet.y = bounds.max.y;
                var effects = ControlField<GameObject>(parts[index], "sprinklerVisuals");
                Assert.That(Vector3.Distance(effects.transform.position, renderer.transform.TransformPoint(outlet)), Is.LessThan(.0001f));
            }
            CaptureAuthoredApartmentSprinklers(link.Building, elevated ? "elevated" : "normal");
        }
        fire.StopFire();
        link.House.Release(UpgradeKind.Fire, false);
        AssertApartmentFireVisibility(model, false);
        Assert.That(link.FireProtection.IsInstalled || link.FireProtection.IsSpraying, Is.False);
    }

    [UnityTest]
    public IEnumerator ElevatedApartmentCanReceiveFireAndSeismicWithoutResettingEitherProtection()
    {
        var link = CityHouseLinks()[1];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        var elevator = ProduceCityProducts(UpgradeKind.Breakwater)[0];
        var fire = ProduceCityProducts(UpgradeKind.Fire)[0];
        var seismic = ProduceCityProducts(UpgradeKind.RailSystem)[0];
        link.House.TryInstall(elevator);
        yield return new WaitForSecondsRealtime(1.15f);
        link.House.TryInstall(fire);
        link.House.TryInstall(seismic);
        yield return new WaitForSecondsRealtime(1.45f);
        AssertApartmentFireVisibility(model, true);
        Assert.That(model.IsElevated && model.HasSeismic, Is.True);
        link.House.Release(UpgradeKind.Fire, false);
        AssertApartmentFireVisibility(model, false);
        Assert.That(model.IsElevated && model.HasSeismic, Is.True);
        link.House.TryInstall(fire);
        AssertApartmentFireVisibility(model, true);
        Assert.That(link.Building.GetComponentsInChildren<Transform>(true).Count(part => part.name == "Sprinkler_Water_Effects"), Is.EqualTo(6),
            "Reinstalling the card must reuse the existing water effects.");
    }

    [UnityTest]
    public IEnumerator RemovingFireDuringElevationKeepsTheNewApartmentSprinklersHidden()
    {
        var link = CityHouseLinks()[2];
        var model = link.Building.GetComponent<ApartmentUpgradeModel>();
        var fire = ProduceCityProducts(UpgradeKind.Fire)[0];
        link.House.TryInstall(fire);
        link.House.TryInstall(ProduceCityProducts(UpgradeKind.Breakwater)[0]);
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(model.IsElevated, Is.True);
        link.House.Release(UpgradeKind.Fire, false);
        AssertApartmentFireVisibility(model, false);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(model.IsElevated && link.FloodProtection.IsInstalled, Is.True);
        AssertApartmentFireVisibility(model, false);
        link.House.TryInstall(fire);
        AssertApartmentFireVisibility(model, true);
    }

    private static void CaptureAuthoredApartmentSprinklers(Transform building, string variant, Transform extraRoot = null)
    {
        var camera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
        var objects = building.GetComponentsInChildren<Transform>(true)
            .Concat(extraRoot != null ? extraRoot.GetComponentsInChildren<Transform>(true) : new Transform[0])
            .Select(t => t.gameObject).Distinct().ToArray();
        var layers = objects.Select(obj => obj.layer).ToArray();
        int oldMask = camera.cullingMask;
        var oldPosition = camera.transform.position;
        var oldTarget = camera.targetTexture;
        var oldRect = camera.rect;
        float oldSize = camera.orthographicSize, oldAspect = camera.aspect;
        var active = RenderTexture.active;
        var target = new RenderTexture(900, 900, 24);
        var pixels = new Texture2D(900, 900, TextureFormat.RGB24, false);
        try
        {
            foreach (var obj in objects) obj.layer = 30;
            camera.cullingMask = 1 << 30;
            camera.targetTexture = target;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = 1f;
            camera.orthographicSize = 1.6f;
            var center = building.GetComponent<CivilGameJam.BuildingModelGeometry>().WorldBounds.center;
            camera.transform.position = center - camera.transform.forward * 15f;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); pixels.Apply();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/AuthoredSprinklers"));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, variant + "-sprinklers.png"), pixels.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < objects.Length; i++) objects[i].layer = layers[i];
            camera.cullingMask = oldMask;
            camera.targetTexture = oldTarget;
            camera.rect = oldRect;
            camera.aspect = oldAspect;
            camera.orthographicSize = oldSize;
            camera.transform.position = oldPosition;
            RenderTexture.active = active;
            target.Release(); Object.Destroy(target); Object.Destroy(pixels);
        }
    }
}
