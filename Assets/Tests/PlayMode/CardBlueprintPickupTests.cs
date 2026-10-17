using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator FactoryBlueprintPickupDeliversEndDragAndCanBePlacedRepeatedlyOnThe2DBoard()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>(); factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var plan = ResearchPlan(0);
        yield return new WaitForSecondsRealtime(.65f);
        int balance = game.AbilityPoints.CurrentPoints;
        yield return DragBlueprintViaUI(game.Vault.SlotFor(plan.UpgradeType).CardRect, factory.transform.position, plan);
        Assert.That(factory.PendingBlueprint, Is.SameAs(plan));
        yield return new WaitForSecondsRealtime(.65f);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            Vector3 destination = cycle % 2 == 0 ? game.FactoryOutputArea.TransformPoint(game.FactoryOutputArea.rect.center) : factory.transform.position;
            yield return DragBlueprintViaUI(view.StoredBlueprint.rectTransform, destination, plan);
            Assert.That(game.IsDraggingAnyCard, Is.False, "Removing a stored blueprint must never lock the board in a drag.");
            Assert.That(plan.IsDragging, Is.False);
            yield return new WaitForSecondsRealtime(.65f);
            if (factory.PendingBlueprint == null)
            {
                var slot = game.Vault.SlotFor(plan.UpgradeType);
                Assert.That(slot.IsArriving, Is.False);
                Assert.That(slot.CanShowHover, Is.True);
                yield return DragBlueprintViaUI(slot.CardRect, factory.transform.position, plan);
                yield return new WaitForSecondsRealtime(.65f);
            }
            Assert.That(factory.PendingBlueprint, Is.SameAs(plan));
            Assert.That(view.StoredBlueprint.gameObject.activeSelf, Is.True);
        }
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance));
        Assert.That(game.Cards.Count(card => card.Kind == CardKind.Blueprint), Is.EqualTo(1));
        // A normal person must still be draggable after those blueprint gestures.
        var worker = People().First(person => person.CanStartWork && !person.IsStored);
        Drag(worker, school.Card.Rect.position);
        Assert.That(worker.AssignedTo, Is.SameAs(school));
        Assert.That(game.IsDraggingAnyCard, Is.False);
    }

    [UnityTest]
    public IEnumerator ClickReturnedBlueprintCanBeDraggedFromVaultIntoFactoryAgainThroughUIEvents()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>(); factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var plan = ResearchPlan(0);
        yield return new WaitForSecondsRealtime(.65f);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            yield return DragBlueprintViaUI(game.Vault.SlotFor(plan.UpgradeType).CardRect, factory.transform.position, plan);
            yield return new WaitForSecondsRealtime(.65f);
            var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(view.StoredBlueprint.gameObject);
            Assert.That(receiver, Is.Not.Null);
            var pointer = new PointerEventData(EventSystem.current) {
                pointerId = -1, button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, view.StoredBlueprint.rectTransform.position)
            };
            Assert.That(ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler), Is.True);
            Assert.That(view.IsReturning, Is.True);
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(view.IsReturning, Is.False);
            Assert.That(plan.IsFlying, Is.False);
            Assert.That(game.Vault.SlotFor(plan.UpgradeType).CanShowHover, Is.True);
            Assert.That(game.IsDraggingAnyCard, Is.False);
        }
        yield return DragBlueprintViaUI(game.Vault.SlotFor(plan.UpgradeType).CardRect, factory.transform.position, plan);
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(factory.PendingBlueprint, Is.SameAs(plan));
        Assert.That(view.IsWorkerHintVisible, Is.True);
        game.AbilityPoints.TrySpend(game.AbilityPoints.CurrentPoints - 10);
        string directory = Path.Combine(Application.dataPath, "../Library/CardGameTestResults/BlueprintPickup");
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "larger-worker-prompt.png"));
    }

    private IEnumerator DragBlueprintViaUI(RectTransform visual, Vector3 destination, DraggableCard plan)
    {
        var receiver = ExecuteEvents.GetEventHandler<IBeginDragHandler>(visual.gameObject);
        Assert.That(receiver, Is.Not.Null);
        var pointer = new PointerEventData(EventSystem.current) {
            pointerId = -1, button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, visual.TransformPoint(visual.rect.center)),
            pointerDrag = receiver, useDragThreshold = false
        };
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerDownHandler);
        Assert.That(ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.beginDragHandler), Is.True);
        pointer.dragging = true;
        Assert.That(plan.IsDragging, Is.True);
        Assert.That(game.IsDraggingAnyCard, Is.True);
        yield return null;
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, destination);
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerUpHandler);
        bool delivered = ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.endDragHandler);
        pointer.dragging = false;
        Assert.That(delivered, Is.True, "The event receiver must remain active when its stored image is hidden; otherwise Unity drops the release event and locks the game.");
        Assert.That(game.IsDraggingAnyCard, Is.False);
        Assert.That(plan.IsDragging, Is.False);
    }
}
