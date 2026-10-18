using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardAbilityPointsPlayModeTests
{
    [UnityTest]
    public IEnumerator SecondEarthquakeRoundOnlyShowsTheSeismicBlueprintInColor()
    {
        PrepareSeismic(1);
        yield return new WaitForSecondsRealtime(1.6f);
        round.Ready(); Until(() => round.RoundNumber == 2);
        Assert.That(round.SequenceNumber, Is.EqualTo(1));
        Assert.That(bank.CurrentPoints, Is.EqualTo(17));
        sequence.Results.Retry(); round.AdvanceRound(.1f);
        Assert.That(game.IsTutorialActive, Is.False);
        Assert.That(research.TryAssign(Person()), Is.True);
        research.AdvanceWork(game.NormalResearchSeconds + .1f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        foreach (var kind in new[] { UpgradeKind.RailSystem, UpgradeKind.Breakwater, UpgradeKind.Fire })
        {
            var option = game.RecipeFor(kind);
            bool seismic = kind == UpgradeKind.RailSystem;
            Assert.That(option.button.interactable, Is.EqualTo(seismic));
            Assert.That(option.unavailableOverlay, Is.Not.Null);
            Assert.That(option.unavailableOverlay.gameObject.activeInHierarchy, Is.EqualTo(!seismic));
            Assert.That(option.unavailableOverlay.color.a, Is.GreaterThan(.9f));
            Assert.That(option.unavailableOverlay.raycastTarget, Is.False);
            Assert.That(option.unavailableOverlay.sprite,
                Is.SameAs(option.button.GetComponentInChildren<UpgradeIcon>(true).Artwork));
        }
        game.SelectBlueprint(1); game.SelectBlueprint(2);
        Assert.That(game.IsChoosingBlueprint, Is.True, "The dark choices remain blocked even when selection is invoked directly.");
        game.GetComponent<CardDock>().SetExpanded(true, true);
        yield return CaptureAvailabilityGuide("second-earthquake-blueprint-choice");
        game.SelectBlueprint(0);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.Breakwater) || game.Vault.IsDiscovered(UpgradeKind.Fire), Is.False);
    }

    [UnityTest]
    public IEnumerator UsedNormalAndEducatedPeopleShowTheirSavedBadgeAfterReturningAndFreshPeopleDoNot()
    {
        var graduate = Educate();
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(AvailabilityBadge(graduate).gameObject.activeSelf, Is.False);
        var plan = Research(graduate);
        Assert.That(graduate.UsedThisTurn, Is.True);
        Assert.That(graduate.IsReturningFromFolder, Is.True);
        Assert.That(AvailabilityBadge(graduate).gameObject.activeSelf, Is.False,
            "The badge cannot appear at the destination ahead of its returning portrait.");
        yield return new WaitForSecondsRealtime(.8f);
        AssertUsedBadge(graduate);

        var maker = Person();
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        Assert.That(factoryStation.TryAssign(maker), Is.True);
        factory.AdvanceWork(100f);
        Assert.That(maker.UsedThisTurn, Is.True);
        Assert.That(maker.IsReturningFromFolder, Is.True);
        Assert.That(AvailabilityBadge(maker).gameObject.activeSelf, Is.False);
        yield return new WaitForSecondsRealtime(.8f);
        AssertUsedBadge(maker);
        Assert.That(game.Cards.Where(person => person.CanStartWork).All(person => !AvailabilityBadge(person).gameObject.activeSelf), Is.True);
        yield return CaptureAvailabilityGuide("used-people-unavailable-badges");

        bank.AddPoints(2);
        var cancelled = Person();
        Assert.That(school.TryAssign(cancelled), Is.True);
        Assert.That(school.ReleaseWorker(0, false), Is.SameAs(cancelled));
        Assert.That(cancelled.UsedThisTurn, Is.False);
        Assert.That(AvailabilityBadge(cancelled).gameObject.activeSelf, Is.False, "A cancelled and refunded job must leave the person available.");
        game.ResetTurnPeople();
        yield return null;
        var fresh = game.Cards.Where(person => person.IsPerson).ToArray();
        Assert.That(fresh.Length, Is.EqualTo(4));
        Assert.That(fresh.All(person => person.CanStartWork && !AvailabilityBadge(person).gameObject.activeSelf), Is.True);
    }

    [UnityTest]
    public IEnumerator FactoryUsesTheAuthoredInsertCardSpriteAndKeepsItsAPAndWorkerVisibilityRules()
    {
        var view = factory.GetComponent<FactoryFolderPresentation>();
        var group = Field<CanvasGroup>(view, "workerHint");
        Assert.That(group.name, Is.EqualTo("InsertCardHint"));
        var guides = group.GetComponentsInChildren<Image>(true);
        Assert.That(guides.Length, Is.EqualTo(1));
        Assert.That(guides[0].sprite.name, Is.EqualTo("Insert Card Ui"));
        Assert.That(guides[0].preserveAspect && !guides[0].raycastTarget, Is.True);
        Assert.That(Field<RectTransform>(view, "personHint"), Is.SameAs(guides[0].rectTransform));
        Assert.That(Field<RectTransform>(view, "arrowHint"), Is.SameAs(guides[0].rectTransform));
        Assert.That(view.IsWorkerHintVisible, Is.False);
        var plan = Research(Person());
        Assert.That(factory.TryInsertBlueprint(plan), Is.True);
        yield return new WaitForSecondsRealtime(.75f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        game.GetComponent<CardDock>().SetExpanded(true, true);
        yield return CaptureAvailabilityGuide("factory-insert-card-ui");
        int balance = bank.CurrentPoints;
        Assert.That(bank.TrySpend(balance), Is.True);
        yield return null;
        Assert.That(view.IsWorkerHintVisible, Is.False);
        bank.AddPoints(balance);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.True);
        Assert.That(factoryStation.TryAssign(Person()), Is.True);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(view.IsWorkerHintVisible, Is.False);
    }

    private static Image AvailabilityBadge(DraggableCard person) => Field<Image>(person, "unavailableOverlay");

    private void AssertUsedBadge(DraggableCard person)
    {
        var badge = AvailabilityBadge(person);
        Assert.That(badge, Is.Not.Null);
        Assert.That(badge.gameObject.activeInHierarchy, Is.True);
        Assert.That(badge.sprite.name, Is.EqualTo("Unavailable UI"));
        Assert.That(badge.color, Is.EqualTo(Color.white));
        Assert.That(badge.GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);
        Assert.That(badge.raycastTarget, Is.False);
        var artwork = person.transform.Find("Artwork").GetComponent<Image>();
        Assert.That(badge.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(badge.rectTransform.rect.width, Is.LessThan(artwork.rectTransform.rect.width));
        Assert.That(badge.rectTransform.rect.height, Is.EqualTo(badge.rectTransform.rect.width));
        Assert.That(person.GetComponent<CanvasGroup>().interactable, Is.False);
        int ap = bank.CurrentPoints;
        person.OnBeginDrag(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 });
        Assert.That(person.IsDragging, Is.False);
        Assert.That(bank.CurrentPoints, Is.EqualTo(ap));
    }

    private IEnumerator CaptureAvailabilityGuide(string name)
    {
        var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
        var mode = canvas.renderMode; var savedCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var cameraObject = new GameObject("AvailabilityGuideCapture", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 450f;
        camera.nearClipPlane = .1f; camera.farClipPlane = 100f; camera.cullingMask = 1 << canvas.gameObject.layer;
        var target = new RenderTexture(1600, 900, 24); camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10f;
        var active = RenderTexture.active; Texture2D pixels = null;
        try
        {
            Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
            var city = Object.FindFirstObjectByType<CityViewport>(); city.RefreshViewport(); city.WorldCamera.Render();
            camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
            var folder = Path.GetFullPath("Library/CardGameTestResults/AvailabilityGuides"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = savedCamera; canvas.planeDistance = distance;
            RenderTexture.active = active; camera.targetTexture = null;
            if (pixels != null) Object.Destroy(pixels);
            Object.Destroy(target); Object.Destroy(cameraObject); Canvas.ForceUpdateCanvases();
        }
    }
}
