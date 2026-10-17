using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator DragTiltKeepsTheGrabbedPointUnderThePointerAndSettlesAfterDrop()
    {
        var person = People()[0];
        person.Rect.SetParent(game.CardLayer, true);
        person.Rect.localPosition = Vector3.zero;
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left, pointerId = -1,
            position = RectTransformUtility.WorldToScreenPoint(null, person.Rect.position) + new Vector2(12f, -8f)
        };
        RectTransformUtility.ScreenPointToWorldPointInRectangle(game.CardLayer, pointer.position, null, out var point);
        Vector3 grip = person.Rect.InverseTransformPoint(point);
        var savedUI = UIIds(game);
        person.OnBeginDrag(pointer);
        for (int i = 0; i < 8; i++)
        {
            pointer.position += new Vector2(12f, 2f);
            person.OnDrag(pointer);
            yield return null;
            Vector2 actualGrip = RectTransformUtility.WorldToScreenPoint(null, person.Rect.TransformPoint(grip));
            Assert.That(Vector2.Distance(actualGrip, pointer.position), Is.LessThan(0.5f), "Rotation and pickup scaling must preserve the grabbed point.");
        }
        float angle = Mathf.DeltaAngle(0f, person.Rect.localEulerAngles.z);
        Assert.That(angle, Is.LessThan(-0.1f).And.GreaterThanOrEqualTo(-4.01f));
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, person.Rect.localEulerAngles.z)), Is.LessThan(0.3f), "A held card should calm down when the pointer stops.");
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, game.Sidebar.PeopleArea.position);
        person.OnDrag(pointer); person.OnEndDrag(pointer);
        yield return new WaitForSecondsRealtime(0.65f);
        Assert.That(Quaternion.Angle(person.Rect.localRotation, Quaternion.identity), Is.LessThan(0.01f));
        Assert.That(person.Rect.localScale, Is.EqualTo(person.LayoutScale));
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        AssertRectInside(person.Rect, game.Sidebar.PeopleArea);
    }

    [UnityTest]
    public IEnumerator DiscoveredBlueprintGlintsAndLeavesASavedThinTrailThenCleansUp()
    {
        research.TryAssign(People()[0]);
        research.AdvanceWork(25.01f);
        game.SelectBlueprint(1);
        var plan = game.Cards.Single(card => card.Kind == CardKind.Blueprint);
        var effects = plan.GetComponent<BlueprintDiscoveryEffects>();
        Assert.That(effects, Is.Not.Null);
        Assert.That(effects.IsRevealing, Is.True);
        Assert.That(plan.GetComponent<Canvas>().overrideSorting, Is.True);
        Assert.That(plan.Rect.localScale.x, Is.LessThan(plan.LayoutScale.x));
        var savedUI = UIIds(game);
        yield return new WaitForSecondsRealtime(0.09f);
        Assert.That(effects.Trail.HasTrail, Is.True);
        Canvas.ForceUpdateCanvases();
        var mesh = effects.Trail.canvasRenderer.GetMesh();
        Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(8), "The light trail must render actual UI geometry.");
        yield return CaptureCanvas(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/card-presentation-blueprint.png"), null, 0.08f);
        yield return new WaitForSecondsRealtime(0.55f);
        Assert.That(plan.gameObject.activeSelf, Is.False);
        Assert.That(effects.IsRevealing, Is.False);
        Assert.That(effects.Trail.HasTrail, Is.False);
        Assert.That(effects.Trail.gameObject.activeSelf, Is.False);
        Assert.That(game.Vault.SlotFor(UpgradeKind.Breakwater).CardRect.gameObject.activeSelf, Is.True);
        Assert.That(UIIds(game), Is.EqualTo(savedUI), "Discovery effects must already exist inside the saved reward prefab.");
        game.Vault.Withdraw(plan);
        Assert.That(plan.GetComponent<Canvas>().overrideSorting, Is.False);
        // Batch mode does not repaint Game view. Raycast after an explicit render has
        // assigned the newly enabled card's actual draw depth, as in normal gameplay.
        yield return CaptureCanvas(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/card-presentation-withdrawn.png"), null, 0.03f,
            camera =>
            {
                var pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(camera, plan.Rect.position) };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.Any(hit => hit.gameObject == plan.gameObject || hit.gameObject.transform.IsChildOf(plan.transform)), Is.True,
                    "The discovery card must receive pointer events after its foreground animation ends. Depth=" + plan.GetComponent<Image>().depth);
            });
    }

    [UnityTest]
    public IEnumerator EveryProductMagnetizesIntoItsOwnHouseSlotThenFlashesAndSettles()
    {
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>();
        factory.enabled = false;
        station.TryAssign(People()[3]);
        for (int type = 0; type < 3; type++)
        {
            factory.TryInsertBlueprint(ResearchPlan(type));
            factory.AdvanceWork(factory.ProductionSeconds + 0.01f);
        }
        yield return null;
        var house = game.Cards.First(card => card.Kind == CardKind.House).GetComponent<HouseUpgrades>();
        var slots = house.GetComponentsInChildren<HouseUpgradeSlot>(true).OrderBy(slot => slot.Kind).ToArray();
        var products = game.Cards.Where(card => card.Kind == CardKind.Upgrade)
            .GroupBy(card => card.UpgradeType).Select(group => group.First()).OrderBy(card => card.UpgradeType).ToArray();
        var savedUI = UIIds(game);
        for (int i = 0; i < 3; i++)
        {
            var product = products[i];
            product.StopFlight();
            product.Rect.position = house.Card.Rect.TransformPoint(new Vector3(125f + i * 20f, 120f));
            var corners = WorldCorners((RectTransform)product.transform.Find("CardSymbol"));
            Assert.That(house.TryInstall(product), Is.True);
            Assert.That(product.gameObject.activeSelf, Is.False);
            Assert.That(slots[i].Filled, Is.True);
            Assert.That(slots[i].IsInstalling, Is.True);
            AssertSameCorners(corners, slots[i].VisibleIcon);
            var duplicate = game.Cards.First(card => card.Kind == CardKind.Upgrade && card.UpgradeType == product.UpgradeType && card != product);
            Assert.That(house.TryInstall(duplicate), Is.False, "The slot is reserved immediately, even during its animation.");
        }
        Vector2 position = house.Card.Rect.anchoredPosition;
        Vector3 scale = house.Card.LayoutScale;
        var localPoints = slots.Select(slot => house.Card.Rect.InverseTransformPoint(slot.VisibleIcon.position)).ToArray();
        house.Card.Rect.anchoredPosition += new Vector2(45f, 60f);
        house.Card.SetLayoutScale(scale * 1.1f);
        for (int i = 0; i < 3; i++)
            Assert.That(Vector3.Distance(house.Card.Rect.InverseTransformPoint(slots[i].VisibleIcon.position), localPoints[i]), Is.LessThan(0.01f));
        var dock = game.GetComponent<CardDock>();
        dock.TickPointer(new Vector2(Screen.width - 1f, Screen.height - 1f), 0.4f);
        Assert.That(dock.IsExpanded, Is.True);
        yield return new WaitForSecondsRealtime(0.14f);
        Assert.That(slots.All(slot => slot.IsInstalling), Is.True);
        yield return CaptureCanvas(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/card-presentation-installing.png"));
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(slots.All(slot => !slot.IsInstalling && slot.Filled), Is.True);
        Assert.That(slots.All(slot => slot.transform.Find("InstallGlow").GetComponent<Image>().enabled), Is.True);
        Assert.That(Mathf.Abs(house.Card.Rect.localScale.x / house.Card.Rect.localScale.y - 1f), Is.GreaterThan(0.0001f));
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(house.Card.Rect.localScale, Is.EqualTo(house.Card.LayoutScale));
        Assert.That(slots.All(slot => !slot.transform.Find("InstallGlow").GetComponent<Image>().enabled), Is.True);
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        house.Card.SetLayoutScale(scale);
        house.Card.Rect.anchoredPosition = position;
        var released = house.Release(UpgradeKind.Fire, false);
        released.Rect.localRotation = Quaternion.Euler(0f, 0f, 4f);
        house.TryInstall(released);
        yield return new WaitForSecondsRealtime(0.08f);
        Vector3 origin = slots[2].VisibleIcon.position;
        float width = slots[2].VisibleIcon.rect.width * slots[2].VisibleIcon.lossyScale.x;
        var visibleCorners = WorldCorners(slots[2].VisibleIcon);
        house.Release(UpgradeKind.Fire);
        Assert.That(slots[2].IsInstalling, Is.False);
        Assert.That(slots[2].Filled, Is.False);
        Assert.That(Vector3.Distance(released.Rect.position, origin), Is.LessThan(0.01f));
        Assert.That(released.Rect.rect.width * released.Rect.lossyScale.x, Is.EqualTo(width).Within(0.01f));
        AssertSameCorners(visibleCorners, (RectTransform)released.transform.Find("CardSymbol"));
        yield return new WaitForSecondsRealtime(0.6f);
        AssertRectInside(released.Rect, game.FactoryOutputArea);
    }

    [UnityTest]
    public IEnumerator BuildingsShowSavedSmokeAndResearchLightOnlyWhileWorking()
    {
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>();
        factory.enabled = false;
        var smoke = station.GetComponent<BuildingWorkEffects>();
        var scan = research.GetComponent<BuildingWorkEffects>();
        Assert.That(smoke, Is.Not.Null); Assert.That(scan, Is.Not.Null);
        Assert.That(smoke.Strength, Is.Zero); Assert.That(scan.Strength, Is.Zero);
        station.TryAssign(People()[3]);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(smoke.Strength, Is.Zero, "Workers without a production order must not emit factory smoke.");
        var blueprint = ResearchPlan(0);
        factory.TryInsertBlueprint(blueprint);
        research.TryAssign(People()[1]);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(smoke.Strength, Is.EqualTo(1f));
        Assert.That(scan.Strength, Is.EqualTo(1f));
        var puffs = station.GetComponentsInChildren<CardSoftGraphic>(true);
        Assert.That(puffs.Length, Is.EqualTo(3));
        Assert.That(puffs.Any(puff => puff.color.a > 0.05f), Is.True);
        var beam = research.transform.Find("WorkEffects/ResearchLightArea/ResearchScan").GetComponent<CardSoftGraphic>();
        float x = beam.rectTransform.anchoredPosition.x;
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(beam.rectTransform.anchoredPosition.x, Is.Not.EqualTo(x));
        var savedUI = UIIds(game);
        yield return CaptureCanvas(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/card-presentation-working-buildings.png"));
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
        factory.ReleaseBlueprint(false);
        research.AdvanceWork(25.01f);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(smoke.Strength, Is.Zero);
        Assert.That(scan.Strength, Is.Zero);
        Assert.That(station.transform.Find("WorkEffects").GetComponent<CanvasGroup>().alpha, Is.Zero);
        Assert.That(research.transform.Find("WorkEffects").GetComponent<CanvasGroup>().alpha, Is.Zero);
        game.SelectBlueprint(1);
    }
}
