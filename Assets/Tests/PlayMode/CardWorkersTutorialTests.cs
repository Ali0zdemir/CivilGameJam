using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class CardWorkersTutorialTests
{
    private WorkersTutorial tutorial;
    private CardGameController game;
    private bool hadPreference;
    private int preference;

    [UnitySetUp]
    public IEnumerator OpenFirstVisit()
    {
        hadPreference = PlayerPrefs.HasKey(WorkersTutorial.CompletionPreferenceKey);
        preference = PlayerPrefs.GetInt(WorkersTutorial.CompletionPreferenceKey, 0);
        PlayerPrefs.DeleteKey(WorkersTutorial.CompletionPreferenceKey);
        yield return SceneManager.LoadSceneAsync("2DCardScene");
        // Exercise the tutorial even when the designer leaves its saved root hidden in Edit Mode.
        Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include).gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(.4f);
        tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
        game = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(tutorial, Is.Not.Null);
        Assert.That(tutorial.IsOpen, Is.True);
    }

    [UnityTearDown]
    public IEnumerator RestoreFirstVisitPreference()
    {
        if (tutorial != null) tutorial.DismissImmediate();
        if (hadPreference) PlayerPrefs.SetInt(WorkersTutorial.CompletionPreferenceKey, preference);
        else PlayerPrefs.DeleteKey(WorkersTutorial.CompletionPreferenceKey);
        PlayerPrefs.Save();
        yield return null;
    }

    private BuildingStation Station(CardKind kind) => game.Cards.Single(card => card.Kind == kind).Station;
    private Button ContinueButton => tutorial.GetComponentsInChildren<Button>(true).Single(button => button.name == "Continue");
    private void ClickContinue()
    {
        var button = ContinueButton;
        Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True);
        Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("CONTINUE"));
        using (var mouse = new MouseDriver()) mouse.Click((RectTransform)button.transform);
    }

    [UnityTest]
    public IEnumerator ProductSpotlightFitsTwoOneAndZeroVisibleCardsWithoutLightingEmptySpace()
    {
        var cards = game.Cards.Where(card => card.Kind == CardKind.Person).ToArray();
        var shown = cards.Take(2).ToArray();
        try
        {
            tutorial.Spotlight.SetHouseProductFocus(null, shown, false);
            foreach (var card in shown) Assert.That(tutorial.Spotlight.IsHighlighted(Center(card.Rect)), Is.True);
            foreach (var card in cards.Skip(2)) Assert.That(tutorial.Spotlight.IsHighlighted(Center(card.Rect)), Is.False);
            shown[1].gameObject.SetActive(false); tutorial.Spotlight.RefreshLayout();
            Assert.That(tutorial.Spotlight.IsHighlighted(Center(shown[0].Rect)), Is.True);
            Assert.That(tutorial.Spotlight.IsHighlighted(Center(shown[1].Rect)), Is.False);
            shown[0].gameObject.SetActive(false); tutorial.Spotlight.RefreshLayout();
            Assert.That(tutorial.Spotlight.material.GetFloat("_ShowPeoplePanel"), Is.Zero);
        }
        finally
        {
            foreach (var card in shown) card.gameObject.SetActive(true);
            tutorial.Spotlight.SetOverviewFocus();
        }
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator CompletedOrSkippedTutorialRestartsFromIntroWheneverTheSceneLoads()
    {
        PlayerPrefs.SetInt(WorkersTutorial.CompletionPreferenceKey, 1);
        PlayerPrefs.Save();
        for (int restart = 0; restart < 2; restart++)
        {
            // Load the saved scene directly, without enabling or opening its UI in the test.
            yield return SceneManager.LoadSceneAsync("2DCardScene");
            yield return new WaitForSecondsRealtime(.4f);
            tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
            game = Object.FindFirstObjectByType<CardGameController>();
            Assert.That(tutorial, Is.Not.Null);
            Assert.That(tutorial.IsOpen && game.IsTutorialActive && game.ActionsLocked, Is.True);
            Assert.That(tutorial.CurrentStep, Is.Zero);
            var overlay = tutorial.transform.Find("Overlay").GetComponent<CanvasGroup>();
            Assert.That(overlay.gameObject.activeInHierarchy && overlay.blocksRaycasts, Is.True);
            Assert.That(overlay.alpha, Is.GreaterThan(.99f));
            AssertStartingResources();
            tutorial.Skip();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(tutorial.IsOpen || game.IsTutorialActive || game.ActionsLocked, Is.False,
                "Skip closes only this session's tutorial.");
            Assert.That(PlayerPrefs.GetInt(WorkersTutorial.CompletionPreferenceKey), Is.EqualTo(1));
        }
    }

    [UnityTest]
    public IEnumerator HouseContoursSurviveLegacyCachesAndCanvasScalerReenable()
    {
        var spotlight = tutorial.Spotlight;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var framesField = typeof(TutorialSpotlight).GetField("frames", flags);
        var cacheField = typeof(TutorialSpotlight).GetField("contourInstances", flags);
        var originalFrames = (Image[])framesField.GetValue(spotlight);
        var originalCache = (Material[])cacheField.GetValue(spotlight);
        var frames = new Image[9]; System.Array.Copy(originalFrames, frames, originalFrames.Length);
        var houses = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(link => link.Building != null && link.House != null)
            .OrderBy(link => link.House.GetComponent<DraggableCard>().InitialGridCell)
            .Select(link => link.House).ToArray();
        Assert.That(houses.Length, Is.EqualTo(4));
        var scaler = game.GetComponentInParent<Canvas>().rootCanvas.GetComponent<CanvasScaler>();
        bool scalerEnabled = scaler.enabled;
        try
        {
            for (int i = 5; i < frames.Length; i++)
            {
                frames[i] = new GameObject("Regression house contour " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                frames[i].transform.SetParent(spotlight.transform.parent, false);
            }
            framesField.SetValue(spotlight, frames);
            foreach (int legacySize in new[] { 0, 4, 5 })
            {
                var legacy = originalCache.Take(legacySize).ToArray(); cacheField.SetValue(spotlight, legacy);
                try
                {
                    spotlight.SetHouseInstallationFocus(houses, game.FactoryOutputArea);
                    scaler.enabled = false; scaler.enabled = true;
                    spotlight.enabled = false; spotlight.enabled = true;
                    Canvas.ForceUpdateCanvases(); spotlight.RefreshLayout();
                    var rebuilt = (Material[])cacheField.GetValue(spotlight);
                    Assert.That(rebuilt.Length, Is.GreaterThanOrEqualTo(9));
                    for (int i = 5; i < 9; i++)
                    {
                        Assert.That(frames[i].gameObject.activeInHierarchy, Is.True);
                        Assert.That(frames[i].sprite, Is.Not.Null);
                        Assert.That(frames[i].material, Is.SameAs(rebuilt[i]));
                    }
                    Assert.That(frames.Skip(5).Select(frame => frame.material).Distinct().Count(), Is.EqualTo(4),
                        "Each house contour keeps its own thickness material.");
                }
                finally
                {
                    var rebuilt = (Material[])cacheField.GetValue(spotlight);
                    cacheField.SetValue(spotlight, originalCache);
                    foreach (var material in rebuilt)
                        if (material != null && !originalCache.Contains(material)) Object.Destroy(material);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            framesField.SetValue(spotlight, originalFrames); cacheField.SetValue(spotlight, originalCache);
            spotlight.SetOverviewFocus(); scaler.enabled = scalerEnabled;
            for (int i = 5; i < frames.Length; i++) if (frames[i] != null) Object.Destroy(frames[i].gameObject);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator WorkingAndCompletingSchoolBarsStayBrightAndLeftClickCannotSkipTraining()
    {
        var panel = (RectTransform)tutorial.transform.Find("Overlay/LessonPanel");
        ClickContinue();
        Assert.That(ContinueButton.gameObject.activeSelf, Is.False);
        var school = Station(CardKind.School); school.enabled = false;
        using (var mouse = new MouseDriver()) mouse.Drag(tutorial.PracticeStudent.Rect, school.Card.Rect);
        Assert.That(tutorial.PracticeStudent.AssignedTo, Is.SameAs(school));
        school.AdvanceWork(game.EducationSeconds * .55f);
        yield return new WaitForSecondsRealtime(.5f);
        var progress = school.GetComponentsInChildren<ProgressBarFeedback>(true).Single();
        Assert.That(progress.gameObject.activeInHierarchy, Is.True);
        tutorial.Spotlight.RefreshLayout();
        Assert.That(tutorial.Spotlight.IsHighlighted(Center((RectTransform)progress.transform)), Is.True);
        using (var mouse = new MouseDriver()) mouse.Click(panel);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
        yield return Capture("school-working-bar-bright");
        school.AdvanceWork(game.EducationSeconds);
        Assert.That(progress.IsCompleting, Is.True);
        tutorial.Spotlight.RefreshLayout();
        Assert.That(tutorial.Spotlight.IsHighlighted(Center((RectTransform)progress.transform)), Is.True);
        using (var mouse = new MouseDriver()) mouse.Click(panel);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
        yield return Capture("school-completion-bar-bright");
        yield return new WaitForSecondsRealtime(.9f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(2));
        using (var mouse = new MouseDriver()) mouse.Click(panel);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(2), "Background clicks cannot advance the graduate explanation.");
        ClickContinue();
        Assert.That(tutorial.CurrentStep, Is.EqualTo(3), "CONTINUE advances the explanation after the real graduate has returned.");
        Assert.That(ContinueButton.gameObject.activeSelf, Is.False);
    }
    private void AssertStartingResources()
    {
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(10));
        Assert.That(game.NormalPeopleCount, Is.EqualTo(4));
        Assert.That(game.EducatedPeopleCount, Is.Zero);
        Assert.That(game.Cards.Where(card => card.Station != null).All(card => card.Station.WorkerCount == 0), Is.True);
    }

    [UnityTest]
    public IEnumerator IntroHighlightsAllThreeBuildingsAndPeopleAndOnlyContinueEntersThePlayableLesson()
    {
        AssertStartingResources();
        Assert.That(tutorial.CurrentStep, Is.Zero);
        Assert.That(game.AbilityPoints.ActionsLocked, Is.True);
        Assert.That(game.GetComponent<CardDock>().IsExpanded, Is.True);
        foreach (var kind in new[] { CardKind.School, CardKind.Factory, CardKind.ResearchCenter })
            Assert.That(tutorial.Spotlight.IsHighlighted(Center(Station(kind).Card.Rect)), Is.True);
        foreach (var person in game.Cards.Where(card => card.IsPerson))
            Assert.That(tutorial.Spotlight.IsHighlighted(Center(person.Rect)), Is.True, "Light the whole People section.");
        Assert.That(tutorial.Spotlight.IsHighlighted(Center((RectTransform)game.Sidebar.PeopleArea.parent)), Is.True);
        Assert.That(tutorial.Spotlight.IsPersonOutlineVisible, Is.False, "The intro highlights the section, not a single person.");
        yield return Capture("01-intro-three-workplaces-and-people");
        yield return null;
        Canvas.ForceUpdateCanvases(); tutorial.Spotlight.RefreshLayout();
        var module = EventSystem.current.currentInputModule as InputSystemUIInputModule;
        Assert.That(module, Is.Not.Null, "Exercise the input module saved in the player's scene.");
        var background = InputSystem.settings.backgroundBehavior;
        var updateMode = InputSystem.settings.updateMode;
#if UNITY_EDITOR
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
        var mouse = InputSystem.AddDevice<Mouse>("TutorialTestMouse");
        var student = tutorial.PracticeStudent;
        var school = Station(CardKind.School);
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
#if UNITY_EDITOR
            // The isolated batch Game View has no OS focus. Route the test device to
            // the game instead of allowing the editor to consume its mouse events.
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.EnableDevice(mouse);
            var introPoint = Center(game.Cards.Last(card => card.IsPerson && card != student).Rect);
            SendMouse(mouse, module, introPoint, true, MouseButton.Right);
            SendMouse(mouse, module, introPoint, false, MouseButton.Right);
            Assert.That(tutorial.CurrentStep, Is.Zero, "Background clicks cannot advance the introduction.");
            SendMouse(mouse, module, introPoint, true);
            Assert.That(tutorial.CurrentStep, Is.Zero, "Wait for release so the same click cannot pick up a card.");
            SendMouse(mouse, module, introPoint, false);
            Assert.That(tutorial.CurrentStep, Is.Zero, "Releasing a background click must leave the introduction open.");
            var panelPoint = Center((RectTransform)tutorial.transform.Find("Overlay/LessonPanel"));
            SendMouse(mouse, module, panelPoint, true); SendMouse(mouse, module, panelPoint, false);
            Assert.That(tutorial.CurrentStep, Is.Zero, "Clicking the lesson panel must leave the introduction open.");
            Assert.That(ContinueButton.gameObject.activeInHierarchy, Is.True);
            var continuePoint = Center((RectTransform)ContinueButton.transform);
            SendMouse(mouse, module, continuePoint, true); SendMouse(mouse, module, continuePoint, false);
            Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
            Assert.That(ContinueButton.gameObject.activeSelf, Is.False, "Hide CONTINUE while the person must be dragged and educated.");
            Assert.That(tutorial.WaitingForEducation, Is.True);
            Assert.That(game.AbilityPoints.ActionsLocked, Is.False);
            Assert.That(game.Cards.All(card => !card.IsHeld && !card.IsDragging), Is.True);
            AssertStartingResources();
            AssertPracticeInput(); AssertCannotAdvance();
            var start = Center(student.Rect);
            SendMouse(mouse, module, start, false);
            SendMouse(mouse, module, start, true);
            Assert.That(mouse.leftButton.isPressed, Is.True, "The mouse state event must reach the Input System.");
            Assert.That(module.leftClick.action.ReadValue<float>(), Is.EqualTo(1f), "The scene's click action must receive the press.");
            Assert.That(student.IsHeld, Is.True, "The actual raycast/press path must pick up the person.");
            tutorial.Spotlight.RefreshLayout();
            Assert.That(tutorial.Spotlight.IsPersonOutlineVisible, Is.False);
            SendMouse(mouse, module, start + new Vector2(24f, 0f), true);
            Assert.That(student.IsDragging, Is.True, "Moving past the drag threshold must begin a real drag.");
            var end = Center(school.Card.Rect);
            for (int i = 1; i <= 12; i++)
            {
                SendMouse(mouse, module, Vector2.Lerp(start, end, i / 12f), true);
                tutorial.Spotlight.RefreshLayout();
            }
            SendMouse(mouse, module, end, false);
            Assert.That(student.IsDragging, Is.False);
            Assert.That(student.AssignedTo, Is.SameAs(school));
            Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(8));
            AssertCannotAdvance();
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = updateMode;
            InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        }
        yield return new WaitForSecondsRealtime(.5f);
        yield return Capture("01b-student-in-school-after-intro");
    }

    private sealed class MouseDriver : System.IDisposable
    {
        private readonly Mouse mouse;
        private readonly InputSystemUIInputModule module;
        private readonly InputSettings.BackgroundBehavior background;
        private readonly InputSettings.UpdateMode mode;
#if UNITY_EDITOR
        private readonly InputSettings.EditorInputBehaviorInPlayMode editor;
#endif
        public MouseDriver()
        {
            module = (InputSystemUIInputModule)EventSystem.current.currentInputModule;
            background = InputSystem.settings.backgroundBehavior; mode = InputSystem.settings.updateMode;
#if UNITY_EDITOR
            editor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            mouse = InputSystem.AddDevice<Mouse>("ResearchTutorialTestMouse"); InputSystem.EnableDevice(mouse);
        }
        public void Click(RectTransform target)
        {
            var point = Center(target); SendMouse(mouse, module, point, false);
            SendMouse(mouse, module, point, true); SendMouse(mouse, module, point, false);
        }
        public void Drag(RectTransform source, RectTransform target)
        {
            var start = Center(source); var end = Center(target);
            SendMouse(mouse, module, start, false); SendMouse(mouse, module, start, true);
            for (int i = 1; i <= 16; i++) SendMouse(mouse, module, Vector2.Lerp(start, end, i / 16f), true);
            SendMouse(mouse, module, end, false);
        }
        public void Dispose()
        {
            InputSystem.RemoveDevice(mouse); InputSystem.settings.updateMode = mode;
            InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = editor;
#endif
        }
    }

    [UnityTest]
    public IEnumerator EducatedWorkerResearchesForcedSeismicBlueprintAndFactoryNeedsThePlanAndAFreshWorker()
    {
        var panel = (RectTransform)tutorial.transform.Find("Overlay/LessonPanel");
        var position = panel.anchoredPosition; var size = panel.sizeDelta;
        var uiIds = tutorial.GetComponentsInChildren<Component>(true).Select(item => item.GetInstanceID()).OrderBy(id => id).ToArray();
        tutorial.Next();
        using (var mouse = new MouseDriver()) mouse.Drag(tutorial.PracticeStudent.Rect, Station(CardKind.School).Card.Rect);
        Assert.That(tutorial.PracticeStudent.AssignedTo, Is.SameAs(Station(CardKind.School)));
        yield return new WaitForSecondsRealtime(game.EducationSeconds + .9f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(2));
        Assert.That(tutorial.DisplayedDescription, Does.Contain("Research Center").And.Contain("less AP").And.Not.Contain("Factory"));
        Assert.That(tutorial.Spotlight.IsHighlighted(Center(Station(CardKind.Factory).Card.Rect)), Is.False);
        using (var mouse = new MouseDriver()) mouse.Click(panel);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(2));
        ClickContinue();
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(3));
        Assert.That(tutorial.DisplayedDescription, Does.Contain("blueprints").And.Contain("disaster protection"));
        Assert.That(Station(CardKind.Factory).TryAssign(tutorial.PracticeGraduate), Is.False);
        Assert.That(Station(CardKind.ResearchCenter).TryAssign(game.Cards.First(card => card.Kind == CardKind.Person)), Is.False);
        AssertRequiredStep(3);
        yield return Capture("06-research-disaster-protection");
        using (var mouse = new MouseDriver()) mouse.Drag(tutorial.PracticeGraduate.Rect, Station(CardKind.ResearchCenter).Card.Rect);
        Assert.That(tutorial.PracticeGraduate.AssignedTo, Is.SameAs(Station(CardKind.ResearchCenter)));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(6));
        yield return new WaitForSecondsRealtime(.7f);
        AssertRequiredStep(3);
        yield return Capture("07-educated-worker-researching");
        yield return new WaitForSecondsRealtime(game.EducatedResearchSeconds + .25f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(4)); Assert.That(game.IsChoosingBlueprint, Is.True);
        Assert.That(panel.gameObject.activeSelf, Is.False, "The saved lesson panel must not cover the choice cards.");
        var seismic = game.RecipeFor(UpgradeKind.RailSystem).button;
        Assert.That(seismic.interactable, Is.True);
        Assert.That(game.RecipeFor(UpgradeKind.Breakwater).button.interactable, Is.False);
        Assert.That(game.RecipeFor(UpgradeKind.Fire).button.interactable, Is.False);
        game.SelectBlueprint(1); game.SelectBlueprint(2); tutorial.Next(); tutorial.Skip();
        Assert.That(game.IsChoosingBlueprint, Is.True, "Neither other choices nor tutorial navigation may bypass the seismic choice.");
        using (var mouse = new MouseDriver()) mouse.Click((RectTransform)game.RecipeFor(UpgradeKind.Fire).button.transform);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        yield return Capture("08-forced-seismic-choice");
        using (var mouse = new MouseDriver()) mouse.Click((RectTransform)seismic.transform);
        Assert.That(game.IsChoosingBlueprint, Is.False);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.Fire) || game.Vault.IsDiscovered(UpgradeKind.Breakwater), Is.False);
        Assert.That(tutorial.PracticeGraduate.UsedThisTurn, Is.True);
        Assert.That(Station(CardKind.ResearchCenter).WorkerCount, Is.Zero, "Finishing research must still release its worker.");
        yield return new WaitForSecondsRealtime(.9f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(5)); Assert.That(panel.gameObject.activeSelf, Is.True);
        Assert.That(tutorial.PracticeBlueprint.IsFlying, Is.False);
        AssertRequiredStep(5);
        yield return Capture("09-blueprint-vault-to-factory");
        var production = Station(CardKind.Factory).GetComponent<FactoryProduction>();
        var vaultSlot = game.Vault.SlotFor(UpgradeKind.RailSystem);
        using (var mouse = new MouseDriver()) mouse.Drag(vaultSlot.CardRect, Station(CardKind.Factory).Card.Rect);
        Assert.That(production.PendingBlueprint, Is.SameAs(tutorial.PracticeBlueprint));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(6), "A blueprint alone does not spend worker AP.");
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(6));
        Assert.That(production.Progress, Is.Zero, "Factory cannot produce from a blueprint without a person.");
        Assert.That(tutorial.FactoryWorker.Kind, Is.EqualTo(CardKind.Person));
        Assert.That(tutorial.FactoryWorker.CanStartWork, Is.True);
        Assert.That(Station(CardKind.Factory).TryAssign(tutorial.PracticeGraduate), Is.False, "The researcher has already worked this turn.");
        AssertRequiredStep(6);
        yield return Capture("10-factory-needs-a-fresh-person");
        using (var mouse = new MouseDriver()) mouse.Drag(tutorial.FactoryWorker.Rect, Station(CardKind.Factory).Card.Rect);
        Assert.That(tutorial.FactoryWorker.AssignedTo, Is.SameAs(Station(CardKind.Factory)));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.7f);
        AssertRequiredStep(6);
        yield return Capture("11-producing-physical-upgrades");
        yield return new WaitForSecondsRealtime(production.ProductionSeconds + 1f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(7)); Assert.That(tutorial.ProductionCompleted, Is.True);
        var products = game.Cards.Where(card => card.Kind == CardKind.Upgrade && card.UpgradeType == UpgradeKind.RailSystem).ToArray();
        Assert.That(products.Length, Is.EqualTo(2));
        Assert.That(products.All(card => !card.IsFlying && card.IsNewProduct), Is.True);
        tutorial.Spotlight.RefreshLayout();
        var unusedOutput = game.FactoryOutputArea.TransformPoint(new Vector2(game.FactoryOutputArea.rect.center.x, game.FactoryOutputArea.rect.yMin + 4f));
        var areaContains = typeof(TutorialSpotlight).GetMethod("AreaContains", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That((bool)areaContains.Invoke(tutorial.Spotlight, new object[] { (Vector2)tutorial.Spotlight.rectTransform.InverseTransformPoint(unusedOutput) }), Is.False,
            "The product highlight must not extend through unused factory output slots.");
        Assert.That(production.PendingBlueprint, Is.Null);
        Assert.That(game.Vault.AvailableCount(UpgradeKind.RailSystem), Is.EqualTo(1));
        Assert.That(tutorial.FactoryWorker.UsedThisTurn, Is.True);
        Assert.That(Station(CardKind.Factory).WorkerCount, Is.Zero);
        Assert.That(panel.anchoredPosition, Is.EqualTo(position)); Assert.That(panel.sizeDelta, Is.EqualTo(size));
        Assert.That(tutorial.GetComponentsInChildren<Component>(true).Select(item => item.GetInstanceID()).OrderBy(id => id), Is.EqualTo(uiIds));
        yield return Capture("12-two-seismic-upgrades-ready");
        ClickContinue();
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(8));
        Assert.That(tutorial.IsOpen, Is.True);
        Assert.That(panel.gameObject.activeSelf, Is.False, "Expose all four houses instead of covering them with the lesson panel.");
        AssertRequiredStep(8);
        yield return Capture("13-choose-any-two-houses");
        var houses = game.Cards.Where(card => card.Kind == CardKind.House).Select(card => card.GetComponent<HouseUpgrades>()).ToArray();
        Assert.That(houses.Length, Is.EqualTo(4));
        foreach (var house in houses)
        {
            Assert.That(house.CanAccept(products[0]), Is.True, "The player can choose any of the four houses.");
            Assert.That(tutorial.Spotlight.IsHighlighted(Center(house.Card.Rect)), Is.True);
        }
        var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
        var drawer = game.GetComponent<CardDock>();
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var cityPresentation = Object.FindFirstObjectByType<HouseUpgradePresentation>();
        Assert.That(round.CanReady, Is.False);
        var productPosition = products[0].Rect.position;
        using (var mouse = new MouseDriver()) mouse.Drag(products[0].Rect, (RectTransform)round.ReadyButton.transform);
        Assert.That(products[0].InstalledIn, Is.Null);
        yield return new WaitForSecondsRealtime(.9f);
        Assert.That(Vector3.Distance(products[0].Rect.position, productPosition), Is.LessThan(.1f),
            "An empty-area drop returns the practice item instead of stranding it behind the dimmer.");
        Assert.That(tutorial.Spotlight.IsHighlighted(Center(products[0].Rect)), Is.True);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(8));
        using (var mouse = new MouseDriver()) mouse.Drag(products[0].Rect, houses[3].Card.Rect);
        Assert.That(products[0].InstalledIn, Is.SameAs(houses[3]));
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(tutorial.IsWatchingCity, Is.True);
        Assert.That(tutorial.GetComponentInChildren<CanvasGroup>().alpha, Is.LessThan(.03f));
        Assert.That(round.CanReady, Is.False);
        using (var mouse = new MouseDriver()) mouse.Click((RectTransform)round.ReadyButton.transform);
        Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Preparation));
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!navigation.IsFocusReady && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(navigation.IsFocusReady, Is.True, "Show the existing close camera focus before installation.");
        Assert.That(drawer.Expansion, Is.LessThan(.02f), "The native drawer must collapse smoothly for the city presentation.");
        yield return Capture("14-city-zoom-first-chosen-house");
        yield return WaitForTutorialCityReturn(round, drawer);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(8), "One protected house is not enough to finish the installation lesson.");
        Assert.That(houses[3].CanAccept(products[1]), Is.False, "The second item goes to a different house.");
        var firstLink = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None).Single(link => link.House == houses[3]);
        Assert.That(firstLink.RailSystem.IsInstalled, Is.True);
        using (var mouse = new MouseDriver()) mouse.Drag(products[1].Rect, houses[1].Card.Rect);
        Assert.That(products[1].InstalledIn, Is.SameAs(houses[1]));
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(tutorial.IsWatchingCity, Is.True);
        Assert.That(round.CanReady, Is.False);
        yield return WaitForTutorialCityReturn(round, drawer);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(9));
        Assert.That(tutorial.DisplayedDescription, Does.Contain("run out of AP").And.Contain("feel ready").And.Contain("end your turn").And.Contain("READY"));
        Assert.That(panel.gameObject.activeSelf, Is.True);
        Assert.That(cityPresentation.IsPresenting || navigation.IsPresenting, Is.False);
        Assert.That(round.CanReady, Is.True);
        Assert.That(houses.Count(house => house.GetInstalled(UpgradeKind.RailSystem) != null), Is.EqualTo(2));
        Assert.That(tutorial.Spotlight.IsHighlighted(Center((RectTransform)round.ReadyButton.transform)), Is.True);
        Assert.That(PlayerPrefs.GetInt(WorkersTutorial.CompletionPreferenceKey, 0), Is.Zero, "Completion requires the actual READY click.");
        yield return Capture("15-insufficient-ap-press-ready");
        Assert.That(panel.anchoredPosition, Is.EqualTo(position)); Assert.That(panel.sizeDelta, Is.EqualTo(size));
        using (var mouse = new MouseDriver()) mouse.Click((RectTransform)round.ReadyButton.transform);
        Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Earthquake));
        Assert.That(tutorial.IsOpen || game.IsTutorialActive, Is.False);
        Assert.That(game.ActionsLocked, Is.True, "Tutorial cleanup must preserve the disaster's action lock.");
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(1));
        Assert.That(PlayerPrefs.GetInt(WorkersTutorial.CompletionPreferenceKey), Is.EqualTo(1));
        var results = Object.FindFirstObjectByType<MonsterSequenceResults>();
        deadline = Time.realtimeSinceStartup + 90f;
        while (!results.IsVisible && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(results.IsVisible, Is.True, "READY must reach the real first-earthquake report.");
        Assert.That(results.TotalHouseCount, Is.EqualTo(4));
        Assert.That(results.ProtectedHouseCount, Is.EqualTo(2));
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(tutorial.IsOpen && game.IsTutorialActive, Is.True);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(10));
        Assert.That(tutorial.DisplayedDescription, Does.Contain("Protected houses earn 4 AP each.").And.Contain("2 houses: +8 AP").And.Contain("Round bonus: +10 AP").And.Contain("+18 AP"));
        Assert.That(tutorial.DisplayedDescription, Does.Not.Contain("{"));
        Assert.That(tutorial.Spotlight.IsHighlighted(Center(results.EarnedPointsArea)), Is.True);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(19), "Only the real round pays AP; the explanation must never pay twice.");
        var outcome = (TMP_Text)typeof(MonsterSequenceResults).GetField("tutorialOutcomeLabel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(results);
        if (outcome != null)
        {
            Assert.That(outcome.gameObject.activeInHierarchy, Is.True);
            Assert.That(outcome.text, Is.EqualTo("Half of your houses survived thanks to the disaster prevention system you installed."));
        }
        yield return Capture("16-tutorial-earthquake-outcome");
        if (outcome != null) { outcome.ForceMeshUpdate(); Assert.That(outcome.isTextOverflowing, Is.False); }
        using (var mouse = new MouseDriver()) mouse.Click(panel);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(10));
        ClickContinue();
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(11));
        Assert.That(tutorial.DisplayedDescription, Does.Contain("all four houses earthquake-resistant").And.Contain("next disaster"));
        Assert.That(results.IsVisible && game.ActionsLocked, Is.True);
        yield return Capture("17-protect-all-houses-before-next-disaster");
        using (var mouse = new MouseDriver()) mouse.Click(panel);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(11));
        ClickContinue();
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(tutorial.IsOpen || game.IsTutorialActive || game.ActionsLocked, Is.False);
        Assert.That(results.IsVisible, Is.True, "Keep the report and ruins in place until its own Continue button is pressed.");
        Assert.That(results.DestroyedHouseCount, Is.EqualTo(2));
        Assert.That(tutorial.GetComponentsInChildren<Component>(true).Select(item => item.GetInstanceID()).OrderBy(id => id), Is.EqualTo(uiIds));
        results.Retry();
        if (outcome != null) Assert.That(outcome.gameObject.activeSelf, Is.False);
        results.ShowRound(2, 4, 18, 2);
        if (outcome != null) Assert.That(outcome.gameObject.activeSelf, Is.False, "The tutorial explanation must not repeat on later reports.");
        results.Retry();
        Assert.That(round.SequenceNumber, Is.EqualTo(1));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(19));
        var retryResearch = Station(CardKind.ResearchCenter); retryResearch.enabled = false;
        var retryWorker = game.Cards.First(card => card.Kind == CardKind.Person && card.CanStartWork);
        Assert.That(retryResearch.TryAssign(retryWorker), Is.True);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(15));
        retryResearch.AdvanceWork(100f);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        Assert.That(game.RecipeFor(UpgradeKind.RailSystem).button.interactable, Is.True);
        Assert.That(game.RecipeFor(UpgradeKind.Breakwater).button.interactable || game.RecipeFor(UpgradeKind.Fire).button.interactable, Is.False);
        game.SelectBlueprint(1); game.SelectBlueprint(2);
        Assert.That(game.IsChoosingBlueprint, Is.True, "Other blueprints cannot bypass the stage gate through SelectBlueprint either.");
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.Breakwater) || game.Vault.IsDiscovered(UpgradeKind.Fire), Is.False);
        game.SelectBlueprint(0);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        Assert.That(game.HasUndiscoveredBlueprints, Is.False);
        Assert.That(retryResearch.TryAssign(game.Cards.First(card => card.Kind == CardKind.Person && card.CanStartWork)), Is.False);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(15), "There is no AP charge when only locked or already-discovered plans remain.");
    }

    private IEnumerator WaitForTutorialCityReturn(EarthquakeRoundController round, CardDock drawer)
    {
        float deadline = Time.realtimeSinceStartup + 25f;
        while ((round.IsUpgradePresentationActive || tutorial.IsWatchingCity) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(round.IsUpgradePresentationActive, Is.False, "Finish the model animation, hold and smooth camera return.");
        while (drawer.Expansion < .999f && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(drawer.Expansion, Is.GreaterThan(.99f));
    }

    [UnityTest]
    public IEnumerator DisablingTutorialKeepsTheEarthquakeBlueprintRestrictionAndTheAuthoredPanel()
    {
        tutorial.Next(); Station(CardKind.School).enabled = false;
        DropStudentAtSchool(); Station(CardKind.School).AdvanceWork(game.EducationSeconds + .01f);
        yield return new WaitForSecondsRealtime(.9f);
        tutorial.Next(); Assert.That(tutorial.CurrentStep, Is.EqualTo(3));
        Assert.That(Station(CardKind.ResearchCenter).TryAssign(tutorial.PracticeGraduate), Is.True);
        Station(CardKind.ResearchCenter).AdvanceWork(game.EducatedResearchSeconds + .1f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(4));
        Assert.That(game.RecipeFor(UpgradeKind.Fire).button.interactable, Is.False);
        tutorial.enabled = false;
        Assert.That(game.ActionsLocked || game.IsTutorialActive, Is.False);
        Assert.That(tutorial.transform.Find("Overlay/LessonPanel").gameObject.activeSelf, Is.True);
        Assert.That(game.RecipeFor(UpgradeKind.Fire).button.interactable, Is.False);
        Assert.That(game.RecipeFor(UpgradeKind.Breakwater).button.interactable, Is.False);
        game.SelectBlueprint(2);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.Fire), Is.False);
        Assert.That(game.IsChoosingBlueprint, Is.True);
        game.SelectBlueprint(0);
        Assert.That(game.Vault.IsDiscovered(UpgradeKind.RailSystem), Is.True);
        yield return null;
    }

    private void AssertRequiredStep(int step)
    {
        Assert.That(tutorial.CurrentStep, Is.EqualTo(step));
        var next = tutorial.GetComponentsInChildren<Button>(true).Single(button => button.name == "Continue");
        Assert.That(next.interactable, Is.False);
        Assert.That(next.gameObject.activeSelf, Is.False, "Hide CONTINUE for assignments, blueprint selection, installations and READY.");
        tutorial.Next(); tutorial.Back();
        Assert.That(tutorial.CurrentStep, Is.EqualTo(step));
        Assert.That(Object.FindFirstObjectByType<EarthquakeRoundController>().CanReady, Is.False);
    }

    private static void SendMouse(Mouse mouse, InputSystemUIInputModule module, Vector2 point, bool pressed,
        MouseButton button = MouseButton.Left)
    {
        // Dispatch via Input Actions, the scene's UI input module and its raycasters.
        // Do not call any DraggableCard callbacks or ExecuteEvents directly here.
        InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(button, pressed));
        InputSystem.Update();
        module.Process();
    }

    [UnityTest]
    public IEnumerator PlayerDragsTheGlowingPersonAndOnlyRealEducationAndItsExitAnimationAdvanceTheLesson()
    {
        var ids = tutorial.GetComponentsInChildren<Component>(true).Select(item => item.GetInstanceID()).OrderBy(id => id).ToArray();
        Assert.That(tutorial.StepCount, Is.EqualTo(12));
        var next = tutorial.GetComponentsInChildren<Button>(true).Single(button => button.name == "Continue");
        Assert.That(next.onClick.GetPersistentTarget(0), Is.SameAs(tutorial));
        next.onClick.Invoke(); yield return new WaitForSecondsRealtime(.25f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
        Assert.That(tutorial.DisplayedDescription, Does.Contain("Drag the glowing normal person").And.Contain("2 AP"));
        Assert.That(tutorial.DisplayedDescription, Does.Not.Contain("{"));
        var student = tutorial.PracticeStudent;
        Assert.That(student.Kind, Is.EqualTo(CardKind.Person));
        AssertPracticeInput();
        yield return Capture("02-drag-to-school");
        AssertCannotAdvance();
        var other = game.Cards.First(card => card.Kind == CardKind.Person && card != student);
        Assert.That(Station(CardKind.School).TryAssign(other), Is.False);
        Assert.That(Station(CardKind.Factory).TryAssign(student), Is.False);
        Assert.That(Station(CardKind.ResearchCenter).TryAssign(student), Is.False);
        BeginDrag(other);
        Assert.That(other.IsDragging, Is.False);
        var pointer = BeginDrag(student);
        Assert.That(student.IsDragging, Is.True);
        pointer.position = Center(Station(CardKind.School).Card.Rect);
        student.OnDrag(pointer);
        Assert.That(Station(CardKind.School).Card.DropTargetFeedback.IsAvailable, Is.True);
        Assert.That(Station(CardKind.Factory).Card.DropTargetFeedback.IsAvailable, Is.False);
        Assert.That(Station(CardKind.ResearchCenter).Card.DropTargetFeedback.IsAvailable, Is.False);
        student.OnPointerUp(pointer); student.OnEndDrag(pointer);
        Assert.That(student.AssignedTo, Is.SameAs(Station(CardKind.School)));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(8));
        yield return new WaitForSecondsRealtime(.6f);
        AssertCannotAdvance();
        Assert.That(tutorial.DisplayedDescription, Does.Contain("educating this person"));
        yield return Capture("03-training");
        // Let the real School.Update timer run; the tutorial never fabricates a graduate.
        yield return new WaitForSecondsRealtime(game.EducationSeconds + .8f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(2));
        Assert.That(tutorial.EducationCompleted, Is.True);
        Assert.That(tutorial.PracticeGraduate.IsFlying, Is.False);
        Assert.That(game.NormalPeopleCount, Is.EqualTo(3));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(8));
        Assert.That(tutorial.DisplayedDescription, Does.Contain("less AP").And.Contain("Research Center").And.Not.Contain("Factory"));
        Assert.That(tutorial.GetComponentsInChildren<Component>(true).Select(item => item.GetInstanceID()).OrderBy(id => id), Is.EqualTo(ids));
        yield return Capture("04-educated-worker-returned");
        tutorial.Back(); yield return new WaitForSecondsRealtime(.2f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
        Assert.That(tutorial.DisplayedDescription, Does.Contain("School educated"));
        yield return Capture("05-school-review");
        tutorial.Next();
        next.onClick.Invoke();
        Assert.That(tutorial.CurrentStep, Is.EqualTo(3), "Education leads into the real Research lesson.");
        tutorial.Skip(); yield return new WaitForSecondsRealtime(.4f);
        Assert.That(tutorial.IsOpen, Is.False); Assert.That(game.ActionsLocked, Is.False);
        Assert.That(game.IsTutorialActive, Is.False);
        var graduate = game.Cards.Single(card => card.Kind == CardKind.EducatedPerson);
        Assert.That(Station(CardKind.ResearchCenter).TryAssign(graduate), Is.True);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(6));
    }

    [UnityTest]
    public IEnumerator ContinueCannotBypassPracticeAndUnfinishedLessonsAreNotRemembered()
    {
        Assert.That(tutorial.IsOpen, Is.True);
        tutorial.Next(); AssertCannotAdvance();
        var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
        Assert.That(round.CanReady, Is.False);
        round.Ready(); Assert.That(round.Phase, Is.EqualTo(EarthquakeRoundPhase.Preparation));
        Assert.That(PlayerPrefs.GetInt(WorkersTutorial.CompletionPreferenceKey, 0), Is.Zero);
        yield return SceneManager.LoadSceneAsync("2DCardScene"); yield return new WaitForSecondsRealtime(.4f);
        tutorial = Object.FindFirstObjectByType<WorkersTutorial>(); game = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(tutorial.IsOpen, Is.True);
        tutorial.Next(); var school = Station(CardKind.School); school.enabled = false;
        DropStudentAtSchool(); school.AdvanceWork(game.EducationSeconds + .01f);
        AssertCannotAdvance();
        yield return new WaitForSecondsRealtime(.85f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(2));
        Assert.That(PlayerPrefs.GetInt(WorkersTutorial.CompletionPreferenceKey, 0), Is.Zero);
        tutorial.Skip(); yield return new WaitForSecondsRealtime(.4f);
        Assert.That(tutorial.IsOpen || game.ActionsLocked || game.IsTutorialActive, Is.False);
        Assert.That(PlayerPrefs.GetInt(WorkersTutorial.CompletionPreferenceKey), Is.EqualTo(1));
        yield return SceneManager.LoadSceneAsync("2DCardScene"); yield return new WaitForSecondsRealtime(.35f);
        tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
        game = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(tutorial.IsOpen && game.ActionsLocked, Is.True);
        Assert.That(tutorial.CurrentStep, Is.Zero);
        AssertStartingResources();
    }

    [UnityTest]
    public IEnumerator TheSavedSkipButtonCanDismissTheIntroWithoutTrainingOrSpendingAP()
    {
        var skip = tutorial.GetComponentsInChildren<Button>(true).Single(button => button.name == "Skip");
        Assert.That(tutorial.CurrentStep, Is.Zero);
        Assert.That(skip.interactable, Is.True);
        var module = (InputSystemUIInputModule)EventSystem.current.currentInputModule;
        var background = InputSystem.settings.backgroundBehavior;
        var updateMode = InputSystem.settings.updateMode;
#if UNITY_EDITOR
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
#endif
        var mouse = InputSystem.AddDevice<Mouse>("TutorialSkipTestMouse");
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.EnableDevice(mouse);
            var point = Center((RectTransform)skip.transform);
            SendMouse(mouse, module, point, false);
            SendMouse(mouse, module, point, true);
            SendMouse(mouse, module, point, false);
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = updateMode;
            InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        }
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(tutorial.IsOpen || game.IsTutorialActive || game.ActionsLocked, Is.False);
        Assert.That(tutorial.EducationCompleted, Is.False);
        AssertStartingResources();
        Assert.That(PlayerPrefs.GetInt(WorkersTutorial.CompletionPreferenceKey), Is.EqualTo(1));
        yield return SceneManager.LoadSceneAsync("2DCardScene");
        yield return new WaitForSecondsRealtime(.4f);
        tutorial = Object.FindFirstObjectByType<WorkersTutorial>(); game = Object.FindFirstObjectByType<CardGameController>();
        Assert.That(tutorial.IsOpen, Is.True);
        Assert.That(tutorial.CurrentStep, Is.Zero);
        AssertStartingResources();
    }

    [UnityTest]
    public IEnumerator WrongDropsAndCancelledTrainingKeepTheLessonOpenAndRefundOnlyTheUnfinishedJob()
    {
        tutorial.Next(); var school = Station(CardKind.School); school.enabled = false;
        var student = tutorial.PracticeStudent;
        var pointer = BeginDrag(student);
        pointer.position = Center(Station(CardKind.ResearchCenter).Card.Rect);
        student.OnDrag(pointer); student.OnPointerUp(pointer); student.OnEndDrag(pointer);
        Assert.That(student.AssignedTo, Is.Null);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(10));
        yield return new WaitForSecondsRealtime(.8f);
        AssertCannotAdvance();
        DropStudentAtSchool(); school.AdvanceWork(game.EducationSeconds * .5f);
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(8));
        Assert.That(school.ReleaseWorker(0), Is.SameAs(student));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(10));
        yield return new WaitForSecondsRealtime(.8f);
        AssertCannotAdvance(); AssertPracticeInput();
        DropStudentAtSchool();
        school.AdvanceWork(game.EducationSeconds + .01f);
        Assert.That(tutorial.PracticeGraduate, Is.Not.Null);
        Assert.That(tutorial.PracticeGraduate.IsFlying, Is.True);
        AssertCannotAdvance();
        yield return new WaitForSecondsRealtime(.08f);
        yield return Capture("03b-graduate-returning");
        yield return new WaitForSecondsRealtime(.85f);
        Assert.That(tutorial.CurrentStep, Is.EqualTo(2));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(8));
        Assert.That(game.EducatedPeopleCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator AuthoredPanelNeedsNoDeletedDecorationsAndTheSilhouetteOutlineStopsOnPointerDown()
    {
        var panel = tutorial.transform.Find("Overlay/LessonPanel");
        var names = panel.GetComponentsInChildren<Transform>(true).Select(item => item.name).ToArray();
        foreach (string removed in new[] { "Chapter", "Note", "Showcase", "Back", "CostComparison", "Step1", "Step2", "Step3" })
            Assert.That(names, Does.Not.Contain(removed), "Use the player's edited lesson panel.");
        var position = ((RectTransform)panel).anchoredPosition;
        var size = ((RectTransform)panel).sizeDelta;
        tutorial.Next(); yield return new WaitForSecondsRealtime(.25f);
        var frame = tutorial.GetComponentsInChildren<Image>(true).Single(image => image.name == "PersonFocus");
        Assert.That(frame.sprite.name, Is.EqualTo("PersonSilhouette"));
        Assert.That(frame.material.shader.name, Is.EqualTo("CivilGameJam/UI/Tutorial Contour"));
        Assert.That(tutorial.Spotlight.IsPersonOutlineVisible, Is.True);
        var rect = frame.rectTransform.rect;
        var transparentCorner = frame.rectTransform.TransformPoint(new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, .97f),
            Mathf.Lerp(rect.yMin, rect.yMax, .97f)));
        Assert.That(tutorial.Spotlight.IsHighlighted(RectTransformUtility.WorldToScreenPoint(null, transparentCorner)), Is.False,
            "Transparent corners must stay dim instead of opening a rectangular hitbox.");
        var student = tutorial.PracticeStudent;
        var artwork = student.transform.Find("Artwork").GetComponent<Image>();
        var padding = UnityEngine.Sprites.DataUtility.GetPadding(artwork.sprite);
        if (padding.x > 1f)
        {
            var artRect = artwork.rectTransform.rect;
            var margin = artwork.rectTransform.TransformPoint(new Vector2(artRect.xMin +
                artRect.width * padding.x / artwork.sprite.rect.width * .5f, artRect.center.y));
            Assert.That(tutorial.Spotlight.IsHighlighted(RectTransformUtility.WorldToScreenPoint(null, margin)), Is.False,
                "Restore trimmed transparent margins; the contour must hug the actual artwork.");
        }
        var pointer = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left,
            position = Center(student.Rect), pressPosition = Center(student.Rect) };
        student.OnPointerDown(pointer); tutorial.Spotlight.RefreshLayout();
        Assert.That(student.IsHeld, Is.True);
        Assert.That(tutorial.Spotlight.IsPersonOutlineVisible, Is.False, "Hide as soon as the player picks the card up.");
        yield return Capture("02b-person-held");
        student.OnPointerUp(pointer); yield return null;
        Assert.That(((RectTransform)panel).anchoredPosition, Is.EqualTo(position));
        Assert.That(((RectTransform)panel).sizeDelta, Is.EqualTo(size));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(10));
    }

    private void AssertCannotAdvance()
    {
        Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
        foreach (var button in tutorial.GetComponentsInChildren<Button>(true))
            Assert.That(button.interactable, Is.EqualTo(button.name == "Skip"), button.name);
        tutorial.Next(); tutorial.Back();
        Assert.That(tutorial.CurrentStep, Is.EqualTo(1));
        Assert.That(tutorial.IsOpen, Is.True);
        Assert.That(tutorial.EducationCompleted, Is.False);
        Assert.That(Object.FindFirstObjectByType<EarthquakeRoundController>().CanReady, Is.False);
    }

    private static Vector2 Center(RectTransform rect, Camera camera = null) =>
        RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));

    private PointerEventData BeginDrag(DraggableCard card)
    {
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            pointerId = -1, position = Center(card.Rect), pressPosition = Center(card.Rect) };
        card.OnPointerDown(pointer); card.OnBeginDrag(pointer);
        return pointer;
    }

    private void DropStudentAtSchool()
    {
        var student = tutorial.PracticeStudent;
        var pointer = BeginDrag(student);
        Assert.That(student.IsDragging, Is.True);
        pointer.position = Center(Station(CardKind.School).Card.Rect);
        student.OnDrag(pointer); student.OnPointerUp(pointer); student.OnEndDrag(pointer);
        Assert.That(student.AssignedTo, Is.SameAs(Station(CardKind.School)));
    }

    private void AssertPracticeInput(Camera camera = null)
    {
        var personPoint = Center(tutorial.PracticeStudent.Rect, camera);
        Assert.That(tutorial.Spotlight.IsHighlighted(personPoint, camera), Is.True);
        Assert.That(tutorial.Spotlight.IsHighlighted(Center(Station(CardKind.School).Card.Rect, camera), camera), Is.True);
        Assert.That(tutorial.Spotlight.IsHighlighted(Center(Station(CardKind.Factory).Card.Rect, camera), camera), Is.False);
        Assert.That(tutorial.Spotlight.IsHighlighted(Center(Station(CardKind.ResearchCenter).Card.Rect, camera), camera), Is.False);
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = personPoint }, results);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results[0].gameObject.GetComponentInParent<DraggableCard>(), Is.SameAs(tutorial.PracticeStudent),
            "The spotlight and existing lesson panel must leave the real person accessible.");
    }

    [UnityTest]
    public IEnumerator DisablingTheTutorialReleasesInputAndTheDockWithoutChangingAPOrCardPoses()
    {
        var rects = game.Cards.Select(card => card.Rect).ToArray();
        var poses = rects.Select(rect => rect.anchoredPosition).ToArray();
        var scales = rects.Select(rect => rect.localScale).ToArray();
        tutorial.enabled = false;
        Assert.That(tutorial.IsOpen || game.ActionsLocked || game.IsTutorialActive, Is.False);
        Assert.That(game.GetComponent<CardDock>().enabled, Is.True);
        AssertStartingResources();
        for (int i = 0; i < rects.Length; i++)
        {
            Assert.That(Vector2.Distance(rects[i].anchoredPosition, poses[i]), Is.LessThan(.01f));
            Assert.That(rects[i].localScale, Is.EqualTo(scales[i]));
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator TutorialFitsAt1080p4KPortraitAndUltrawideWithoutClippingText()
    {
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160),
                     new Vector2Int(1080, 1920), new Vector2Int(3440, 1440) })
            yield return Capture("responsive-" + size.x + "x" + size.y, size.x, size.y);
        tutorial.Next(); yield return new WaitForSecondsRealtime(.25f);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(3840, 2160),
                     new Vector2Int(1080, 1920), new Vector2Int(3440, 1440) })
            yield return Capture("practice-" + size.x + "x" + size.y, size.x, size.y);
    }

    private IEnumerator Capture(string name, int width = 1600, int height = 900)
    {
        var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var cameraObject = new GameObject("TutorialCaptureCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = 450f; camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
        camera.cullingMask = 1 << canvas.gameObject.layer;
        var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10f;
        var previousActive = RenderTexture.active;
        Texture2D pixels = null;
        try
        {
            Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
            tutorial.Spotlight.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            if (tutorial.WaitingForEducation && tutorial.PracticeStudent != null && !tutorial.PracticeStudent.IsStored)
                AssertPracticeInput(camera);
            var panel = tutorial.transform.Find("Overlay/LessonPanel").GetComponent<RectTransform>();
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var screen = camera.WorldToScreenPoint(corner);
                Assert.That(screen.x, Is.InRange(0f, (float)width)); Assert.That(screen.y, Is.InRange(0f, (float)height));
            }
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>())
            {
                var preferred = label.GetPreferredValues(label.text, label.rectTransform.rect.width, Mathf.Infinity);
                Assert.That(preferred.y, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f), label.name + " must fit.");
            }
            var city = Object.FindFirstObjectByType<CityViewport>(); city.RefreshViewport(); city.WorldCamera.Render();
            camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            if (tutorial.IsOpen && !tutorial.IsWatchingCity) AssertLessonDimming(camera, target, pixels);
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/CardGameTestResults/InstallTutorial"));
            Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance;
            RenderTexture.active = previousActive;
            if (pixels != null) Object.Destroy(pixels);
            Canvas.ForceUpdateCanvases(); camera.targetTexture = null; target.Release();
            Object.Destroy(target); Object.Destroy(cameraObject);
        }
    }

    private void AssertLessonDimming(Camera camera, RenderTexture target, Texture2D shaded)
    {
        var samples = new List<(string name, Vector2 point, bool lit)>();
        if (tutorial.CurrentStep >= 10)
        {
            var result = Object.FindFirstObjectByType<MonsterSequenceResults>();
            var apArea = result.EarnedPointsArea;
            // Sample the light statistic plate; its dark centered digits are deliberately below the brightness threshold.
            var apBackground = apArea.rect.center + Vector2.left * apArea.rect.width * .38f;
            samples.Add(("Earned AP", RectTransformUtility.WorldToScreenPoint(camera, apArea.TransformPoint(apBackground)), true));
            var heading = result.EarnedPointsArea.parent.Find("Title").GetComponent<TMP_Text>();
            heading.ForceMeshUpdate();
            var glyph = heading.textInfo.characterInfo.First(character => character.isVisible);
            var headingPoint = RectTransformUtility.WorldToScreenPoint(camera, heading.rectTransform.TransformPoint((glyph.topLeft + glyph.bottomRight) * .5f));
            samples.Add(("Result heading", headingPoint, tutorial.CurrentStep == 11));
            foreach (var card in result.EarnedPointsArea.parent.GetComponentsInChildren<DraggableCard>(true))
                samples.Add(("Result house", Center(card.Rect, camera), tutorial.CurrentStep == 11));
            samples.Add(("Outside the report", new Vector2(12f, target.height - 12f), false));
        }
        else if (game.IsChoosingBlueprint)
        {
            foreach (var kind in new[] { UpgradeKind.RailSystem, UpgradeKind.Breakwater, UpgradeKind.Fire })
                samples.Add((kind + " choice", Center((RectTransform)game.RecipeFor(kind).button.transform, camera), kind == UpgradeKind.RailSystem));
        }
        else
        {
        foreach (var progress in Object.FindObjectsByType<ProgressBarFeedback>(FindObjectsSortMode.None))
            samples.Add((progress.name + " working/completing bar", Center((RectTransform)progress.transform, camera), true));
        foreach (var kind in new[] { CardKind.School, CardKind.ResearchCenter, CardKind.Factory })
            samples.Add((kind.ToString(), Center(Station(kind).Card.Rect, camera), tutorial.CurrentStep == 0 ||
                (tutorial.CurrentStep == 1 ? kind == CardKind.School : tutorial.CurrentStep == 2 || tutorial.CurrentStep == 3 ? kind == CardKind.ResearchCenter :
                    tutorial.CurrentStep >= 5 && tutorial.CurrentStep <= 6 && kind == CardKind.Factory)));
        foreach (var person in game.Cards.Where(card => card.IsPerson && !card.IsStored && !card.IsFlying && card.gameObject.activeInHierarchy))
            samples.Add((person.name, Center(person.Rect, camera), tutorial.CurrentStep == 0 || person ==
                (tutorial.CurrentStep == 1 ? tutorial.EducationCompleted ? tutorial.PracticeGraduate : tutorial.PracticeStudent : tutorial.CurrentStep == 2 || tutorial.CurrentStep == 3 ? tutorial.PracticeGraduate :
                    tutorial.CurrentStep == 6 ? tutorial.FactoryWorker : null)));
        foreach (var kind in new[] { CardKind.School, CardKind.ResearchCenter, CardKind.Factory })
        foreach (var slot in Station(kind).GetComponentsInChildren<WorkerSlot>())
            if (slot.VisibleWorkerRect != null && slot.VisibleWorkerRect.gameObject.activeInHierarchy &&
                (slot.Portrait.gameObject.activeInHierarchy || slot.IsAnimating))
                samples.Add(("Worker pocket animation", Center(slot.VisibleWorkerRect, camera),
                    tutorial.CurrentStep == 1 && kind == CardKind.School || tutorial.CurrentStep == 3 && kind == CardKind.ResearchCenter ||
                    tutorial.CurrentStep == 6 && kind == CardKind.Factory));
        var targets = (RectTransform[])typeof(TutorialSpotlight).GetField("targets", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(tutorial.Spotlight);
        samples.Add(("AP counter", Center(targets[3], camera), tutorial.CurrentStep > 0 && tutorial.CurrentStep < 7 || tutorial.CurrentStep == 9));
        foreach (var house in game.Cards.Where(card => card.Kind == CardKind.House))
            samples.Add((house.name, Center(house.Rect, camera), tutorial.CurrentStep == 7 || tutorial.CurrentStep == 8));
        if (tutorial.CurrentStep == 7 || tutorial.CurrentStep == 8)
            foreach (var product in tutorial.PracticeProducts.Where(card => !card.IsStored))
                samples.Add(("Seismic item", Center(product.Rect, camera), true));
        if (tutorial.CurrentStep == 9)
            samples.Add(("READY button", Center((RectTransform)Object.FindFirstObjectByType<EarthquakeRoundController>().ReadyButton.transform, camera), true));
        if (tutorial.CurrentStep == 5)
            samples.Add(("Seismic Vault card", Center(game.Vault.SlotFor(UpgradeKind.RailSystem).CardRect, camera), true));
        }

        var reference = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        float alpha = tutorial.Spotlight.canvasRenderer.GetAlpha();
        try
        {
            tutorial.Spotlight.canvasRenderer.SetAlpha(0f);
            camera.Render();
            reference.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); reference.Apply();
            var panel = (RectTransform)tutorial.transform.Find("Overlay/LessonPanel");
            int brightChecked = 0, darkChecked = 0;
            foreach (var sample in samples)
            {
                Assert.That(tutorial.Spotlight.IsHighlighted(sample.point, camera), Is.EqualTo(sample.lit),
                    sample.name + " must match the current lesson.");
                if (RectTransformUtility.RectangleContainsScreenPoint(panel, sample.point, camera) ||
                    sample.point.x < 3 || sample.point.x >= target.width - 3 || sample.point.y < 3 || sample.point.y >= target.height - 3) continue;
                float original = Brightness(reference, sample.point);
                if (original < .08f) continue;
                float ratio = Brightness(shaded, sample.point) / original;
                Assert.That(ratio, sample.lit ? Is.InRange(.96f, 1.04f) : Is.InRange(.07f, .24f),
                    sample.name + " must render " + (sample.lit ? "at its original brightness." : "visibly darkened."));
                if (!sample.lit)
                    Assert.That(Brightness(shaded, sample.point, false) / Brightness(reference, sample.point, false),
                        Is.LessThan(.53f), sample.name + " must look clearly darker in the rendered sRGB image too.");
                if (sample.lit) brightChecked++; else darkChecked++;
            }
            Assert.That(brightChecked, Is.GreaterThan(0), "Check actual rendered focus pixels.");
            if (tutorial.CurrentStep < 11)
                Assert.That(darkChecked, Is.GreaterThan(0), "Check actual rendered dimmed pixels.");
        }
        finally
        {
            tutorial.Spotlight.canvasRenderer.SetAlpha(alpha);
            Object.Destroy(reference);
        }
    }

    private static float Brightness(Texture2D pixels, Vector2 point, bool linear = true)
    {
        float value = 0f;
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
            {
                // Rendered PNG pixels are sRGB; compare light in linear space, where the UI's
                // alpha blend is performed, rather than treating gamma-encoded values as light.
                var color = pixels.GetPixel(Mathf.RoundToInt(point.x) + x, Mathf.RoundToInt(point.y) + y);
                if (linear) color = color.linear;
                value += (color.r + color.g + color.b) / 3f;
            }
        return value / 25f;
    }
}
