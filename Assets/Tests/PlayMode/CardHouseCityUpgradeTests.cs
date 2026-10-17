using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    private HouseCityUpgradeLink[] CityHouseLinks() => game.GetComponentsInChildren<HouseCityUpgradeLink>()
        .OrderBy(link => link.House.Card.InitialGridCell).ToArray();

    private DraggableCard[] ProduceCityProducts(UpgradeKind kind, int batches = 1)
    {
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>();
        factory.enabled = false;
        if (station.WorkerCount == 0) Assert.That(station.TryAssign(People().First(person => person.AssignedTo == null)), Is.True);
        var plan = ResearchPlan((int)kind - 1);
        for (int i = 0; i < batches; i++)
        {
            Assert.That(factory.TryInsertBlueprint(plan), Is.True);
            factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
        }
        return game.Cards.Where(card => card.Kind == CardKind.Upgrade && card.UpgradeType == kind).ToArray();
    }

    private static bool CityHasUpgrade(HouseCityUpgradeLink link, UpgradeKind kind)
    {
        switch (kind)
        {
            case UpgradeKind.RailSystem: return link.RailSystem.IsInstalled;
            case UpgradeKind.Breakwater: return link.FloodProtection.IsInstalled;
            case UpgradeKind.Fire: return link.FireProtection.IsInstalled;
            default: return false;
        }
    }

    [TestCase(0, UpgradeKind.RailSystem)]
    [TestCase(1, UpgradeKind.RailSystem)]
    [TestCase(2, UpgradeKind.RailSystem)]
    [TestCase(3, UpgradeKind.RailSystem)]
    [TestCase(0, UpgradeKind.Breakwater)]
    [TestCase(1, UpgradeKind.Breakwater)]
    [TestCase(2, UpgradeKind.Breakwater)]
    [TestCase(3, UpgradeKind.Breakwater)]
    [TestCase(0, UpgradeKind.Fire)]
    [TestCase(1, UpgradeKind.Fire)]
    [TestCase(2, UpgradeKind.Fire)]
    [TestCase(3, UpgradeKind.Fire)]
    public void HouseCardsApplyAndRemoveOnlyTheirOwnCityUpgrade(int houseIndex, UpgradeKind kind)
    {
        var links = CityHouseLinks();
        var products = ProduceCityProducts(kind);
        var target = links[houseIndex];
        Assert.That(target.House.TryInstall(products[0]), Is.True);
        for (int i = 0; i < links.Length; i++)
            foreach (var type in new[] { UpgradeKind.RailSystem, UpgradeKind.Breakwater, UpgradeKind.Fire })
                Assert.That(CityHasUpgrade(links[i], type), Is.EqualTo(i == houseIndex && type == kind));
        Assert.That(target.House.TryInstall(products[1]), Is.False);
        Assert.That(target.House.GetInstalled(kind), Is.SameAs(products[0]));
        Assert.That(CityHasUpgrade(target, kind), Is.True);
        Assert.That(target.House.Release(kind, false), Is.SameAs(products[0]));
        Assert.That(CityHasUpgrade(target, kind), Is.False);
        Assert.That(products[0].InstalledIn, Is.Null);
        Assert.That(products[0].gameObject.activeSelf, Is.True);
        Assert.That(target.House.Release(kind, false), Is.Null);
    }

    [UnityTest]
    public IEnumerator MovingHouseCardsAndTransferringProductsKeepTheSavedCityAssociations()
    {
        var links = CityHouseLinks();
        var first = links[0]; var second = links[1];
        var firstBuilding = first.Building;
        Drag(first.House.Card, game.Grid.GetCellWorldPosition(InitiallyEmptyCell));
        Assert.That(first.Building, Is.SameAs(firstBuilding));
        var pointer = new PointerEventData(EventSystem.current)
        { button = PointerEventData.InputButton.Left, pointerId = -1 };
        foreach (var kind in new[] { UpgradeKind.RailSystem, UpgradeKind.Breakwater, UpgradeKind.Fire })
        {
            var product = ProduceCityProducts(kind)[0];
            Drag(product, first.House.Card.Rect.position);
            Assert.That(CityHasUpgrade(first, kind), Is.True);
            var slot = first.House.GetComponentsInChildren<HouseUpgradeSlot>().Single(item => item.Kind == kind);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, slot.VisibleIcon.position);
            slot.OnBeginDrag(pointer);
            Assert.That(CityHasUpgrade(first, kind), Is.False);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, second.House.Card.Rect.position);
            slot.OnDrag(pointer); slot.OnEndDrag(pointer);
            Assert.That(product.InstalledIn, Is.SameAs(second.House));
            Assert.That(CityHasUpgrade(second, kind), Is.True);
            var secondSlot = second.House.GetComponentsInChildren<HouseUpgradeSlot>().Single(item => item.Kind == kind);
            pointer.dragging = false;
            secondSlot.OnPointerClick(pointer);
            Assert.That(CityHasUpgrade(second, kind), Is.False);
            Assert.That(product.InstalledIn, Is.Null);
            yield return new WaitForSecondsRealtime(0.55f);
        }
        Assert.That(first.Building, Is.SameAs(firstBuilding));
    }

    [UnityTest]
    public IEnumerator CombinedCityUpgradesChangeRealModelsAndRestoreThemIndependently()
    {
        var link = CityHouseLinks()[0];
        var models = link.Building.GetComponentsInChildren<BuildingModelGeometry>().Where(model => !model.IsCollection).ToArray();
        var meshes = models.Select(model => ControlField<MeshFilter>(model, "modelMesh").sharedMesh).ToArray();
        var positions = models.Select(model => model.transform.position).ToArray();
        foreach (var kind in new[] { UpgradeKind.RailSystem, UpgradeKind.Breakwater, UpgradeKind.Fire })
            Assert.That(link.House.TryInstall(ProduceCityProducts(kind)[0]), Is.True);
        var savedUI = UIIds(game);
        yield return new WaitForSecondsRealtime(0.45f);
        Assert.That(link.RailSystem.CurrentLift, Is.GreaterThan(0f));
        Assert.That(link.Building.GetComponent<ApartmentUpgradeModel>().HasFireProtection, Is.True);
        AssertApartmentFireVisibility(link.Building.GetComponent<ApartmentUpgradeModel>(), true);
        yield return new WaitForSecondsRealtime(1.05f);
        Assert.That(link.Building.GetComponent<ApartmentUpgradeModel>().IsElevated, Is.True);
        for (int i = 0; i < models.Length; i++)
        {
            Assert.That(models[i].transform.position.y, Is.EqualTo(positions[i].y).Within(.0001f));
            var renderers = new System.Collections.Generic.List<MeshRenderer>(); models[i].AppendRenderers(renderers);
            Assert.That(renderers.Single().GetComponent<MeshFilter>().sharedMesh, Is.Not.SameAs(meshes[i]));
        }
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults"));
        yield return CaptureCanvas(Path.Combine(directory, "house-city-upgrades-installed.png"));
        var fire = Object.FindFirstObjectByType<FireTestController>();
        fire.StartFire();
        yield return null;
        Assert.That(link.FireProtection.IsSpraying, Is.True);
        link.House.Release(UpgradeKind.Fire, false);
        Assert.That(link.FireProtection.IsInstalled || link.FireProtection.IsSpraying, Is.False);
        Assert.That(link.RailSystem.IsInstalled && link.FloodProtection.IsInstalled, Is.True);
        link.House.Release(UpgradeKind.Breakwater, false);
        yield return new WaitForSecondsRealtime(1.15f);
        for (int i = 0; i < models.Length; i++)
            Assert.That(ControlField<MeshFilter>(models[i], "modelMesh").sharedMesh, Is.SameAs(meshes[i]));
        Assert.That(link.RailSystem.IsInstalled, Is.True);
        link.House.Release(UpgradeKind.RailSystem, false);
        yield return new WaitForSecondsRealtime(0.45f);
        for (int i = 0; i < models.Length; i++)
            Assert.That(Vector3.Distance(models[i].transform.position, positions[i]), Is.LessThan(0.0001f));
        Assert.That(link.Building.GetComponent<ApartmentUpgradeModel>().HasFireProtection, Is.False);
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        fire.StopFire();
    }

    [Test]
    public void ElevatorsProtectOnlyTheirApartmentsWithoutCreatingACoastalBreakwater()
    {
        var links = CityHouseLinks();
        var products = ProduceCityProducts(UpgradeKind.Breakwater, 2);
        var breakwater = Object.FindFirstObjectByType<TsunamiBreakwater>();
        for (int i = 0; i < links.Length; i++)
        {
            Assert.That(links[i].House.TryInstall(products[i]), Is.True);
            Assert.That(breakwater.IsActive, Is.False);
        }
        Assert.That(breakwater.Transmission, Is.EqualTo(1f));
        links[2].House.Release(UpgradeKind.Breakwater, false);
        Assert.That(breakwater.IsActive, Is.False);
        Assert.That(breakwater.Transmission, Is.EqualTo(1f));
        Assert.That(links[2].House.TryInstall(products[2]), Is.True);
        Assert.That(breakwater.IsActive, Is.False);
    }

    [Test]
    public void RejectingABlueprintOrDuplicateDoesNotChangeTheCityAndDebugOtherTypesRemainIndependent()
    {
        var link = CityHouseLinks()[0];
        var plan = ResearchPlan(0);
        Assert.That(link.House.TryInstall(plan), Is.False);
        Assert.That(link.RailSystem.IsInstalled || link.FloodProtection.IsInstalled || link.FireProtection.IsInstalled, Is.False);
        link.FireProtection.SetInstalled(true);
        var products = ProduceCityProducts(UpgradeKind.RailSystem);
        link.House.TryInstall(products[0]);
        Assert.That(link.House.TryInstall(products[1]), Is.False);
        Assert.That(link.FireProtection.IsInstalled, Is.True);
        link.House.Release(UpgradeKind.RailSystem, false);
        Assert.That(link.RailSystem.IsInstalled, Is.False);
        Assert.That(link.FireProtection.IsInstalled, Is.True, "A card change must not reset another debug upgrade type.");
        link.FireProtection.ResetUpgrade();
    }

    [Test]
    public void AReenabledSavedLinkResynchronizesCardStateWithoutAddingAnyUI()
    {
        var link = CityHouseLinks()[0];
        var product = ProduceCityProducts(UpgradeKind.Fire)[0];
        var savedUI = UIIds(game);
        link.enabled = false;
        link.House.TryInstall(product);
        Assert.That(link.FireProtection.IsInstalled, Is.False);
        link.enabled = true;
        Assert.That(link.FireProtection.IsInstalled, Is.True);
        link.enabled = false;
        link.House.Release(UpgradeKind.Fire, false);
        Assert.That(link.FireProtection.IsInstalled, Is.True);
        link.enabled = true;
        Assert.That(link.FireProtection.IsInstalled, Is.False);
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }
}
