using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    private static (PointerEventData pointer, GameObject dragHandler) FolderPointerAt(Vector3 worldPosition)
    {
        var pointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left, pointerId = -1,
            position = RectTransformUtility.WorldToScreenPoint(null, worldPosition)
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty, "The visible card must receive a pointer raycast.");
        pointer.pointerPressRaycast = hits[0];
        return (pointer, ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject));
    }

    [UnityTest]
    public IEnumerator EmptyFolderPocketsAndHouseSocketsLetTheBuildingReceiveActualPointerDrags()
    {
        foreach (var card in game.Cards.Where(card => card.IsBuilding).ToArray())
        {
            var points = new List<Vector3>();
            foreach (var slot in card.GetComponentsInChildren<WorkerSlot>()) points.Add(slot.transform.position);
            foreach (var slot in card.GetComponentsInChildren<FactoryBlueprintSlot>()) points.Add(slot.transform.position);
            foreach (var slot in card.GetComponentsInChildren<HouseUpgradeSlot>()) points.Add(slot.transform.position);
            // The front flap and its stepped top edge in the supplied screenshot.
            foreach (float x in new[] { -.3f, 0f, .3f })
                points.Add(card.Rect.TransformPoint(new Vector3(card.Rect.rect.width * x, -card.Rect.rect.height * .2f)));
            foreach (var point in points)
            {
                var hit = FolderPointerAt(point);
                Assert.That(hit.dragHandler, Is.SameAs(card.gameObject),
                    card.name + ": an empty pocket must not swallow the folder's drag.");
            }
            var press = FolderPointerAt(points[0]);
            Assert.That(ExecuteEvents.Execute(press.dragHandler, press.pointer, ExecuteEvents.pointerDownHandler), Is.True);
            Assert.That(ExecuteEvents.Execute(press.dragHandler, press.pointer, ExecuteEvents.beginDragHandler), Is.True);
            Assert.That(game.DraggedCard, Is.SameAs(card));
            Assert.That(card.IsDragging, Is.True);
            int source = game.Grid.IndexOf(card);
            int destination = Enumerable.Range(0, CardGrid.CellCount).First(i => game.Grid.GetOccupant(i) == null);
            MoveGridDrag(card, press.pointer, game.Grid.GetCellWorldPosition(destination));
            EndGridDrag(card, press.pointer);
            Assert.That(game.Grid.GetOccupant(destination), Is.SameAs(card));
            Assert.That(game.Grid.GetOccupant(source), Is.Null);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Vector3.Distance(card.Rect.position, game.Grid.GetCellWorldPosition(destination)), Is.LessThan(.05f));
        }
    }

    [UnityTest]
    public IEnumerator FilledWorkerBlueprintAndHouseSocketsStillDragTheirContentsInsteadOfTheBuilding()
    {
        var person = People()[0];
        Assert.That(school.TryAssign(person), Is.True);
        yield return new WaitForSecondsRealtime(.6f);
        var slot = WorkerSlots(school)[0];
        var workerHit = PortraitUpperRightHit(slot);
        var workerPointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left, pointerId = -1,
            position = workerHit.screenPosition, pointerPressRaycast = workerHit
        };
        var workerHandler = ExecuteEvents.GetEventHandler<IDragHandler>(workerHit.gameObject);
        Assert.That(workerHandler, Is.SameAs(slot.gameObject));
        ExecuteEvents.Execute(workerHandler, workerPointer, ExecuteEvents.beginDragHandler);
        Assert.That(game.DraggedCard, Is.SameAs(person));
        Assert.That(school.Card.IsDragging, Is.False);
        workerPointer.position = RectTransformUtility.WorldToScreenPoint(null, game.Sidebar.PeopleArea.position);
        ExecuteEvents.Execute(workerHandler, workerPointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(workerHandler, workerPointer, ExecuteEvents.endDragHandler);
        yield return new WaitForSecondsRealtime(.6f);

        var plan = ResearchPlan(0);
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        yield return new WaitForSecondsRealtime(.6f);
        var blueprint = factory.GetComponent<FactoryFolderPresentation>().StoredBlueprint.rectTransform;
        var blueprintPress = FolderPointerAt(blueprint.position);
        Assert.That(blueprintPress.dragHandler.GetComponent<FactoryBlueprintSlot>(), Is.Not.Null);
        ExecuteEvents.Execute(blueprintPress.dragHandler, blueprintPress.pointer, ExecuteEvents.beginDragHandler);
        Assert.That(game.DraggedCard, Is.SameAs(plan));
        Assert.That(factory.GetComponent<DraggableCard>().IsDragging, Is.False);
        blueprintPress.pointer.position = RectTransformUtility.WorldToScreenPoint(null, game.Vault.SlotFor(UpgradeKind.RailSystem).CardRect.position);
        ExecuteEvents.Execute(blueprintPress.dragHandler, blueprintPress.pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(blueprintPress.dragHandler, blueprintPress.pointer, ExecuteEvents.endDragHandler);
        yield return new WaitForSecondsRealtime(.6f);

        var product = ProduceCityProducts(UpgradeKind.Fire)[0];
        var house = CityHouseLinks()[0].House;
        Assert.That(house.TryInstall(product), Is.True);
        yield return new WaitForSecondsRealtime(.6f);
        var socket = house.GetComponentsInChildren<HouseUpgradeSlot>().Single(item => item.Kind == UpgradeKind.Fire);
        var productPress = FolderPointerAt(socket.VisibleIcon.position);
        Assert.That(productPress.dragHandler, Is.SameAs(socket.gameObject));
        ExecuteEvents.Execute(productPress.dragHandler, productPress.pointer, ExecuteEvents.beginDragHandler);
        Assert.That(game.DraggedCard, Is.SameAs(product));
        Assert.That(house.Card.IsDragging, Is.False);
        ExecuteEvents.Execute(productPress.dragHandler, productPress.pointer, ExecuteEvents.endDragHandler);
    }

    [UnityTest]
    public IEnumerator AllBuildingCardsRestAtTheCenterOfTheirSlotsAfterHoverAndAnOccupiedSwap()
    {
        foreach (var card in game.Cards.Where(card => card.IsBuilding).ToArray())
        {
            var cell = game.Grid.GetCell(game.Grid.IndexOf(card));
            var center = cell.TransformPoint(cell.rect.center);
            Assert.That(Vector3.Distance(card.Rect.TransformPoint(card.Rect.rect.center), center), Is.LessThan(.05f), card.name);
            Hover(card, true);
            yield return new WaitForSecondsRealtime(.3f);
            Hover(card, false);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Vector3.Distance(card.Rect.TransformPoint(card.Rect.rect.center), center), Is.LessThan(.05f), card.name);
        }
        var pointer = BeginGridDrag(research.Card);
        MoveGridDrag(research.Card, pointer, school.Card.Rect.position);
        EndGridDrag(research.Card, pointer);
        yield return new WaitForSecondsRealtime(.65f);
        AssertGridCardsAtTheirDestinations();
    }
}
