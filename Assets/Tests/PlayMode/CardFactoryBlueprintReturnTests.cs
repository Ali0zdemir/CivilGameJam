using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator BlueprintReturnsThroughTheFolderLipAndUnlocksItsVaultSlotOnlyOnArrival()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>(); factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        typeof(FactoryFolderPresentation).GetField("entrySeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, .9f);
        var plan = ResearchPlan(0);
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        yield return new WaitForSecondsRealtime(1.15f);
        var savedUI = UIIds(game);
        int balance = game.AbilityPoints.CurrentPoints;
        var corners = new Vector3[4]; view.StoredBlueprint.rectTransform.GetWorldCorners(corners);
        var slot = game.Vault.SlotFor(plan.UpgradeType);
        view.StoredBlueprint.GetComponentInParent<FactoryBlueprintSlot>().OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        var actual = new Vector3[4]; view.EntryImage.rectTransform.GetWorldCorners(actual);
        for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(actual[i], corners[i]), Is.LessThan(.02f), "Removing a blueprint must begin at its visible pocket pose.");
        Assert.That(factory.PendingBlueprint, Is.Null);
        Assert.That(view.IsReturning, Is.True);
        Assert.That(view.StoredBlueprint.gameObject.activeSelf, Is.False);
        Assert.That(view.IsWorkerHintVisible, Is.False);
        Assert.That(view.EntryImage.GetComponent<Canvas>().overrideSorting, Is.False);
        Assert.That(plan.StoredInVault, Is.SameAs(game.Vault));
        Assert.That(plan.IsFlying, Is.True);
        Assert.That(plan.GetComponent<CanvasGroup>().alpha, Is.Zero, "Only the saved return preview should render during the transfer.");
        Assert.That(slot.IsArriving, Is.True);
        Assert.That(slot.CanShowHover, Is.False);
        Assert.That(factory.TryInsertBlueprint(plan), Is.False, "An incoming blueprint cannot be reused before the transfer finishes.");
        Assert.That(game.HasActiveCardAnimation, Is.True);
        Vector3 start = view.EntryImage.rectTransform.position;
        float width = view.EntryImage.rectTransform.rect.width * view.EntryImage.rectTransform.lossyScale.x;
        yield return new WaitForSecondsRealtime(.13f);
        Assert.That(view.EntryImage.rectTransform.position.y, Is.GreaterThan(start.y));
        Assert.That(view.EntryImage.rectTransform.rect.width * view.EntryImage.rectTransform.lossyScale.x, Is.EqualTo(width).Within(.1f));
        Assert.That(view.EntryImage.GetComponent<Canvas>().overrideSorting, Is.False);
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(view.IsReturning, Is.True);
        Assert.That(view.EntryImage.GetComponent<Canvas>().overrideSorting, Is.True);
        Assert.That(view.EntryImage.transform.parent, Is.SameAs(factory.transform));
        string directory = Path.Combine(Application.dataPath, "../Library/CardGameTestResults/FactoryReturn");
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "blueprint-returning-to-vault.png"));
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(view.IsReturning, Is.False);
        Assert.That(view.EntryImage.gameObject.activeSelf, Is.False);
        Assert.That(plan.IsFlying, Is.False);
        Assert.That(plan.gameObject.activeSelf, Is.False);
        Assert.That(slot.IsArriving, Is.False);
        Assert.That(slot.CanShowHover, Is.True);
        Assert.That(game.Vault.AvailableCount(plan.UpgradeType), Is.EqualTo(1));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance));
        Assert.That(UIIds(game), Is.EqualTo(savedUI), "Returning must reuse the saved pocket, preview and Vault artwork.");
    }

    [UnityTest]
    public IEnumerator ReturningBlueprintsFollowTheMovingVaultFromSmallWindowsTo4KAndPortrait()
    {
        var factory = Station(CardKind.Factory).GetComponent<FactoryProduction>(); factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var dock = game.GetComponent<CardDock>();
        var canvas = game.GetComponentInParent<Canvas>();
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldDistance = canvas.planeDistance;
        var cameraObject = new GameObject("BlueprintReturnResolutionCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = 450f; camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10f;
        RenderTexture target = null;
        try
        {
            var resolutions = new[] { new Vector2Int(960, 540), new Vector2Int(3840, 2160), new Vector2Int(1080, 1920) };
            for (int i = 0; i < resolutions.Length; i++)
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(resolutions[i].x, resolutions[i].y, 24); camera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); dock.SetExpanded(true, true);
                var plan = ResearchPlan(i);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.That(factory.TryInsertBlueprint(plan), Is.True);
                yield return new WaitForSecondsRealtime(.65f);
                var sprite = view.StoredBlueprint.sprite;
                Assert.That(factory.ReleaseBlueprint(), Is.SameAs(plan));
                yield return new WaitForSecondsRealtime(.05f);
                dock.SetExpanded(false, true);
                yield return new WaitForSecondsRealtime(.12f);
                dock.SetExpanded(true, true);
                yield return new WaitForSecondsRealtime(.6f);
                var slot = game.Vault.SlotFor(plan.UpgradeType);
                Assert.That(view.IsReturning, Is.False, resolutions[i].ToString());
                Assert.That(view.EntryImage.gameObject.activeSelf, Is.False);
                Assert.That(view.EntryImage.sprite, Is.SameAs(sprite));
                Assert.That(plan.IsFlying, Is.False);
                Assert.That(slot.IsArriving, Is.False);
                Assert.That(plan.StoredInVault, Is.SameAs(game.Vault));
                Assert.That(Vector3.Distance(plan.Rect.position, slot.CardRect.TransformPoint(slot.CardRect.rect.center)), Is.LessThan(.02f), "The endpoint must follow the live Vault after panel and resolution changes.");
            }
        }
        finally
        {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject); Canvas.ForceUpdateCanvases();
        }
    }

    [UnityTest]
    public IEnumerator BlueprintReturnsCanReverseAnUnfinishedEntryAndCancelWithoutGhostsOrLostAP()
    {
        var station = Station(CardKind.Factory);
        var factory = station.GetComponent<FactoryProduction>(); factory.enabled = false;
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var plan = ResearchPlan(0);
        yield return new WaitForSecondsRealtime(.65f);
        int balance = game.AbilityPoints.CurrentPoints;
        Assert.That(station.TryAssign(People().First(p => p.CanStartWork && !p.IsStored)), Is.True);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance - station.AbilityPointCost(CardKind.Person)));
        yield return new WaitForSecondsRealtime(.1f);
        var corners = new Vector3[4]; view.EntryImage.rectTransform.GetWorldCorners(corners);
        factory.ReleaseBlueprint();
        var actual = new Vector3[4]; view.EntryImage.rectTransform.GetWorldCorners(actual);
        for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(corners[i], actual[i]), Is.LessThan(.02f), "Reversing an unfinished entry must not jump back to the stored pose.");
        Assert.That(view.IsReturning, Is.True);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(balance), "Unfinished production refunds immediately, independently of cosmetic travel.");
        yield return new WaitForSecondsRealtime(.04f);
        Assert.That(game.Vault.Withdraw(plan), Is.SameAs(plan));
        Assert.That(view.IsReturning, Is.False);
        Assert.That(view.EntryImage.gameObject.activeSelf, Is.False);
        Assert.That(plan.IsFlying, Is.False);
        Assert.That(plan.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        Assert.That(game.Vault.SlotFor(plan.UpgradeType).IsArriving, Is.False);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        yield return new WaitForSecondsRealtime(.65f);
        factory.ReleaseBlueprint();
        factory.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.That(plan.IsFlying, Is.False);
        Assert.That(game.Vault.SlotFor(plan.UpgradeType).IsArriving, Is.False);
        Assert.That(plan.gameObject.activeSelf, Is.False);
        factory.gameObject.SetActive(true);
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        yield return new WaitForSecondsRealtime(.65f);
        factory.ReleaseBlueprint();
        yield return SceneManager.LoadSceneAsync("2DCardScene", LoadSceneMode.Single);
        yield return null;
        var loaded = Object.FindFirstObjectByType<FactoryFolderPresentation>();
        Assert.That(loaded.IsAnimating, Is.False);
        Assert.That(loaded.EntryImage.gameObject.activeSelf, Is.False);
    }
}
