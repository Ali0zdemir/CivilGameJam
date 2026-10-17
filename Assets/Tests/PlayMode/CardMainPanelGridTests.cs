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
    [UnityTest]
    public IEnumerator SavedGridFollowsTheEightPanelRecessesAndKeepsItsFirstRowVisibleAtEveryAspect()
    {
        var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
        var panel = (RectTransform)game.transform;
        var dock = game.GetComponent<CardDock>();
        var originalMode = canvas.renderMode;
        var originalCamera = canvas.worldCamera;
        float originalDistance = canvas.planeDistance;
        var cameraObject = new GameObject("PanelAlignmentTestCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 450f;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100f;
        camera.cullingMask = 1 << canvas.gameObject.layer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;
        var sizes = game.Cards.Where(card => card.IsBuilding).ToDictionary(card => card, card => card.Rect.sizeDelta);
        var scales = sizes.Keys.ToDictionary(card => card, card => card.Rect.localScale);
        var savedUI = UIIds(game);
        RenderTexture target = null;
        var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/QueueGrid"));
        Directory.CreateDirectory(output);
        try
        {
            foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160),
                         new Vector2Int(1024, 768), new Vector2Int(3440, 1440), new Vector2Int(1080, 1920) })
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(resolution.x, resolution.y, 24);
                camera.targetTexture = target;
                dock.SetExpanded(true, true);
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < CardGrid.CellCount; i++)
                {
                    var cell = game.Grid.GetCell(i);
                    Vector2 local = panel.InverseTransformPoint(cell.position);
                    var normalized = new Vector2((local.x - panel.rect.xMin) / panel.rect.width,
                        (local.y - panel.rect.yMin) / panel.rect.height);
                    // These are the centers of the eight recessed areas in Deck Ui (2),
                    // measured in the artwork, independent of screen or Canvas dimensions.
                    Assert.That(normalized.x, Is.EqualTo(.135f + .245f * (i % 4)).Within(.002f), cell.name);
                    Assert.That(normalized.y, Is.EqualTo(i < 4 ? .725f : .235f).Within(.002f), cell.name);
                    AssertOnScreen(cell, camera, resolution);
                }
                foreach (var card in sizes.Keys)
                {
                    Assert.That(card.Rect.sizeDelta, Is.EqualTo(sizes[card]));
                    Assert.That(card.Rect.localScale, Is.EqualTo(scales[card]));
                }
                if (resolution.x == 1920 || resolution.x == 3840)
                    WritePanelFrame(camera, target, Path.Combine(output, "panel-" + resolution.x + "-two-rows.png"));
                dock.SetExpanded(false, true);
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < CardGrid.Columns; i++) AssertOnScreen(game.Grid.GetCell(i), camera, resolution);
                var corners = new Vector3[4];
                game.Grid.GetCell(4).GetWorldCorners(corners);
                Assert.That(RectTransformUtility.WorldToScreenPoint(camera, corners[1]).y, Is.LessThan(1f),
                    "The collapsed dock must show only the first row: " + resolution);
                if (resolution.x == 1920 || resolution.x == 3840)
                    WritePanelFrame(camera, target, Path.Combine(output, "panel-" + resolution.x + "-one-row.png"));
                Assert.That(UIIds(game), Is.EqualTo(savedUI));
            }
        }
        finally
        {
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = originalDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject);
            dock.SetExpanded(true, true);
        }
    }

    private static void WritePanelFrame(Camera camera, RenderTexture target, string path)
    {
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.Destroy(texture); }
    }
}
