using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivilGameJam.Disasters;
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

    [UnitySetUp]
    public IEnumerator LoadGame()
    {
        previousAudioPause = AudioListener.pause;
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
        Directory.CreateDirectory("Library/PauseMenuWork");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Library/PauseMenuWork/pause-menu.png");
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

    private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
        ExecuteEvents.pointerClickHandler);
}
