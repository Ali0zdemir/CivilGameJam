using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivilGameJam.Disasters;
using CivilGameJam.Cards;
using CivilGameJam.Menu;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CardPauseMenuTests
{
    private PauseMenuController menu;
    private Keyboard keyboard;
    private bool previousAudioPause;
    private InputSettings.BackgroundBehavior previousBackgroundBehavior;
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

    [UnitySetUp]
    public IEnumerator LoadGame()
    {
        previousAudioPause = AudioListener.pause;
        previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        yield return SceneManager.LoadSceneAsync("2DCardScene", LoadSceneMode.Single);
        yield return null;
        menu = Object.FindFirstObjectByType<PauseMenuController>();
        Assert.That(menu, Is.Not.Null);
        keyboard = InputSystem.AddDevice<Keyboard>();
    }

    [UnityTearDown]
    public IEnumerator RestoreState()
    {
        if (menu != null && menu.IsPaused) menu.ContinueGame();
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        Time.timeScale = 1f;
        AudioListener.pause = previousAudioPause;
        yield return null;
    }

    [UnityTest]
    public IEnumerator EscapeShowsOnlyTwoButtonsAndBlocksTheBoard()
    {
        Assert.That(menu.IsPaused, Is.False);
        Assert.That(menu.transform.Find("PausePanel").gameObject.activeSelf, Is.False);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        yield return null;
        yield return null;
        Assert.That(menu.IsPaused, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(AudioListener.pause, Is.True);
        Assert.That(menu.GetComponentsInChildren<Button>().Length, Is.EqualTo(2));
        Assert.That(Button("ContinueButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Continue"));
        Assert.That(Button("BackToMainMenuButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Back to the Main Menu"));
        Assert.That(menu.transform.Find("PausePanel/MainPanel/OptionsButton"), Is.Null);
        Assert.That(menu.transform.Find("PausePanel/MainPanel/Title"), Is.Not.Null);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("ContinueButton").gameObject));
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Screen.width * .15f, Screen.height * .5f)
        }, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject.name, Is.EqualTo("PausePanel"));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        yield return null;
        yield return null;
        Assert.That(menu.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(AudioListener.pause, Is.False);
    }

    [UnityTest]
    public IEnumerator ContinueResumesTheSameDisasterAndRestoresThePreviousTimeScale()
    {
        var earthquake = Object.FindFirstObjectByType<EarthquakeTestController>();
        Assert.That(earthquake, Is.Not.Null);
        earthquake.StartEarthquake();
        yield return null;
        Time.timeScale = .75f;
        menu.PauseGame();
        float progress = earthquake.NormalizedProgress;
        var camera = Camera.main;
        Vector3 position = camera.transform.position;
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(earthquake.NormalizedProgress, Is.EqualTo(progress).Within(.00001f));
        Assert.That(camera.transform.position, Is.EqualTo(position));
        Click(Button("ContinueButton"));
        Assert.That(menu.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(.75f));
        Assert.That(AudioListener.pause, Is.False);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(earthquake.NormalizedProgress, Is.GreaterThan(progress));
        earthquake.StopEarthquake();
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator BackButtonLoadsBackupMainMenuWithTimeAndAudioRestored()
    {
        menu.PauseGame();
        Click(Button("BackToMainMenuButton"));
        Assert.That(menu.IsLoading, Is.True);
        menu.BackToMainMenu();
        float deadline = Time.realtimeSinceStartup + 45f;
        while (SceneManager.GetActiveScene().name != "MainMenuBackup" && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenuBackup"));
        Assert.That(Object.FindFirstObjectByType<MainMenuController>(), Is.Not.Null);
        Assert.That(Object.FindFirstObjectByType<PauseMenuController>(), Is.Null);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(AudioListener.pause, Is.False);
    }

    private Button Button(string name) => menu.GetComponentsInChildren<Button>(true).Single(button => button.name == name);

    [UnityTest]
    public IEnumerator PauseFreezesSchoolWaterAndFireAndRejectsADropWithoutChargingAP()
    {
        Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include).DismissImmediate();
        var game = Object.FindFirstObjectByType<CardGameController>();
        var school = game.Cards.Single(card => card.Kind == CardKind.School).Station;
        var people = game.Cards.Where(card => card.Kind == CardKind.Person).ToArray();
        Assert.That(school.TryAssign(people[0]), Is.True);
        yield return new WaitForSecondsRealtime(.3f);
        var person = people[1];
        var pointer = new PointerEventData(EventSystem.current)
        {
            pointerId = -1, button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, person.Rect.position)
        };
        person.OnBeginDrag(pointer);
        Assert.That(person.IsDragging, Is.True);
        var water = Object.FindFirstObjectByType<TsunamiTestController>();
        water.StartTsunami(); yield return null;
        menu.PauseGame();
        float waterProgress = water.NormalizedProgress;
        float education = school.GetEducationProgress(0);
        int points = game.AbilityPoints.CurrentPoints;
        Vector3 cardPosition = person.Rect.position;
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, school.transform.position);
        person.OnDrag(pointer);
        Assert.That(person.Rect.position, Is.EqualTo(cardPosition));
        person.OnEndDrag(pointer);
        Assert.That(person.IsDragging, Is.False);
        Assert.That(school.WorkerCount, Is.EqualTo(1));
        school.AdvanceWork(10f);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(water.NormalizedProgress, Is.EqualTo(waterProgress).Within(.00001f));
        Assert.That(school.GetEducationProgress(0), Is.EqualTo(education).Within(.00001f));
        Assert.That(game.AbilityPoints.CurrentPoints, Is.EqualTo(points));
        Assert.That(school.GetWorker(0), Is.SameAs(people[0]));
        menu.ContinueGame();
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(water.NormalizedProgress, Is.GreaterThan(waterProgress));
        Assert.That(school.GetEducationProgress(0), Is.GreaterThan(education));
        water.StopTsunami();

        var fire = Object.FindFirstObjectByType<FireTestController>();
        fire.StartFire(); yield return null;
        menu.PauseGame(); float fireProgress = fire.NormalizedProgress;
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(fire.NormalizedProgress, Is.EqualTo(fireProgress).Within(.00001f));
        menu.ContinueGame(); yield return new WaitForSecondsRealtime(.15f);
        Assert.That(fire.NormalizedProgress, Is.GreaterThan(fireProgress));
        fire.StopFire();
    }

    private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
        ExecuteEvents.pointerClickHandler);
}
