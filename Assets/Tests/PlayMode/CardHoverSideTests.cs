using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    private static Rect HoverRect(RectTransform rect, RectTransform space)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var min = Vector2.positiveInfinity;
        var max = Vector2.negativeInfinity;
        foreach (var corner in corners)
        {
            Vector2 local = space.InverseTransformPoint(corner);
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void AssertHoverSide(DraggableCard card, bool left, float originalX)
    {
        var viewport = (RectTransform)game.GetComponentInParent<Canvas>().rootCanvas.transform;
        var details = card.GetComponent<CardHover>().Details;
        Assert.That(details.gameObject.activeSelf, Is.True);
        var panel = HoverRect(details, viewport);
        var body = HoverRect(card.Rect, viewport);
        if (left) Assert.That(panel.xMax, Is.LessThan(body.xMin), card.name + " must open its panel on the left.");
        else Assert.That(panel.xMin, Is.GreaterThan(body.xMax), card.name + " must use its saved right side when there is room.");
        Assert.That(card.Rect.position.x, Is.EqualTo(originalX).Within(.05f), "Fitting the information must not slide the card away from the mouse.");
        Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(viewport.rect.xMin + 7.9f));
        Assert.That(panel.xMax, Is.LessThanOrEqualTo(viewport.rect.xMax - 7.9f));
    }

    [UnityTest]
    public IEnumerator EveryBuildingUsesLeftInformationInEitherRightmostGridSlotWithoutSlidingAway()
    {
        var cards = new[] { research.Card, Station(CardKind.Factory).Card, school.Card,
            game.Cards.First(card => card.Kind == CardKind.House) };
        var savedUI = UIIds(game);
        foreach (var card in cards)
        {
            foreach (int destination in new[] { 3, 7 })
            {
                Assert.That(game.Grid.TryPlaceOrSwap(card, destination), Is.True);
                yield return new WaitForSecondsRealtime(.45f);
                float x = card.Rect.position.x;
                var hover = card.GetComponent<CardHover>();
                var anchors = hover.Details.anchorMin;
                var pivot = hover.Details.pivot;
                Hover(card, true);
                yield return new WaitForSecondsRealtime(.3f);
                AssertHoverSide(card, true, x);
                yield return new WaitForSecondsRealtime(.2f);
                AssertHoverSide(card, true, x);
                Hover(card, false);
                yield return new WaitForSecondsRealtime(.28f);
                Assert.That(hover.Details.anchorMin, Is.EqualTo(anchors));
                Assert.That(hover.Details.pivot, Is.EqualTo(pivot));
                Assert.That(Vector3.Distance(card.Rect.position, game.Grid.GetCellWorldPosition(destination)), Is.LessThan(.05f));
            }
        }
        Assert.That(UIIds(game), Is.EqualTo(savedUI));
    }

    [UnityTest]
    public IEnumerator InformationSideFollowsTheCurrentColumnAfterAnActualDraggedSwap()
    {
        Assert.That(game.Grid.TryPlace(research.Card, 3), Is.True);
        float x = research.Card.Rect.position.x;
        Hover(research.Card, true);
        yield return new WaitForSecondsRealtime(.3f);
        AssertHoverSide(research.Card, true, x);
        Hover(research.Card, false);
        yield return new WaitForSecondsRealtime(.3f);
        var factory = Station(CardKind.Factory).Card;
        var pointer = BeginGridDrag(research.Card);
        MoveGridDrag(research.Card, pointer, factory.Rect.position);
        EndGridDrag(research.Card, pointer);
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(game.Grid.IndexOf(factory), Is.EqualTo(3));
        x = research.Card.Rect.position.x;
        Hover(research.Card, true);
        yield return new WaitForSecondsRealtime(.3f);
        AssertHoverSide(research.Card, false, x);
        Hover(research.Card, false);
        yield return new WaitForSecondsRealtime(.3f);
        x = factory.Rect.position.x;
        Hover(factory, true);
        yield return new WaitForSecondsRealtime(.3f);
        AssertHoverSide(factory, true, x);
    }

    [UnityTest]
    public IEnumerator RightmostInformationStaysLeftAndStableAt1080p4KPortraitAndUltrawide()
    {
        Assert.That(game.Grid.TryPlace(research.Card, 3), Is.True);
        var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        var cameraObject = new GameObject("HoverSideTestCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 450f;
        camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
        camera.cullingMask = 1 << canvas.gameObject.layer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10f;
        RenderTexture target = null;
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/HoverSide"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160),
                         new Vector2Int(1080, 1920), new Vector2Int(3440, 1440) })
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
                float x = research.Card.Rect.position.x;
                Hover(research.Card, true);
                yield return new WaitForSecondsRealtime(.35f);
                AssertHoverSide(research.Card, true, x);
                var viewport = (RectTransform)canvas.transform;
                Vector3 mouseWorld = game.Grid.GetCell(3).position;
                Assert.That(research.Card.Rect.rect.Contains((Vector2)research.Card.Rect.InverseTransformPoint(mouseWorld)), Is.True);
                WritePanelFrame(camera, target, Path.Combine(directory, "right-edge-" + size.x + "x" + size.y + ".png"));
                Hover(research.Card, false);
                yield return new WaitForSecondsRealtime(.3f);
            }
        }
        finally
        {
            Hover(research.Card, false);
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject);
        }
    }
}
