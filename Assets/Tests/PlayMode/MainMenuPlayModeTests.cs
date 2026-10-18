using System.Collections;
using System.Linq;
using CivilGameJam.Cards;
using CivilGameJam.Menu;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class MainMenuPlayModeTests
{
    private MainMenuController menu;
    private bool hadSavedVolume;
    private float previousSavedVolume;
    private float previousListenerVolume;

    [UnitySetUp]
    public IEnumerator LoadMenu()
    {
        hadSavedVolume = PlayerPrefs.HasKey(GameAudioSettings.MasterVolumeKey);
        previousSavedVolume = GameAudioSettings.MasterVolume;
        previousListenerVolume = AudioListener.volume;
        GameAudioSettings.SetMasterVolume(0.65f);
        yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        yield return null;
        menu = Object.FindFirstObjectByType<MainMenuController>();
        Assert.That(menu, Is.Not.Null);
    }

    [UnityTearDown]
    public IEnumerator RestoreAudio()
    {
        if (hadSavedVolume) PlayerPrefs.SetFloat(GameAudioSettings.MasterVolumeKey, previousSavedVolume);
        else PlayerPrefs.DeleteKey(GameAudioSettings.MasterVolumeKey);
        PlayerPrefs.Save();
        AudioListener.volume = previousListenerVolume;
        yield return null;
    }

    [UnityTest]
    public IEnumerator OptionsBlocksMainButtonsAndBackRestoresNavigation()
    {
        Assert.That(menu.OptionsOpen, Is.False);
        Click(Button("OptionsButton"));
        yield return null;
        Assert.That(menu.OptionsOpen, Is.True);
        Assert.That(Button("PlayGameButton").IsInteractable(), Is.False);
        Assert.That(Button("ExitButton").IsInteractable(), Is.False);
        var slider = menu.GetComponentInChildren<Slider>(true);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(slider.gameObject));
        Click(Button("BackButton"));
        yield return null;
        Assert.That(menu.OptionsOpen, Is.False);
        Assert.That(Button("PlayGameButton").IsInteractable(), Is.True);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Button("OptionsButton").gameObject));
    }

    [UnityTest]
    public IEnumerator VolumeUpdatesAudioAndSurvivesSceneReload()
    {
        Click(Button("OptionsButton"));
        var slider = menu.GetComponentInChildren<Slider>(true);
        Assert.That(slider.value, Is.EqualTo(0.65f).Within(0.001f));
        slider.value = 0.37f;
        Assert.That(AudioListener.volume, Is.EqualTo(0.37f).Within(0.001f));
        Assert.That(menu.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "VolumeValue").text, Is.EqualTo("37%"));
        Click(Button("BackButton"));
        yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        yield return null;
        menu = Object.FindFirstObjectByType<MainMenuController>();
        Assert.That(menu.GetComponentInChildren<Slider>(true).value, Is.EqualTo(0.37f).Within(0.001f));
        Assert.That(AudioListener.volume, Is.EqualTo(0.37f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator VolumeSliderCanMuteAndRestoreFullVolume()
    {
        Click(Button("OptionsButton"));
        var slider = menu.GetComponentInChildren<Slider>(true);
        slider.value = 0f;
        Assert.That(AudioListener.volume, Is.Zero);
        Assert.That(GameAudioSettings.MasterVolume, Is.Zero);
        slider.value = 1f;
        Assert.That(AudioListener.volume, Is.EqualTo(1f));
        yield return null;
    }

    [UnityTest, Timeout(90000)]
    public IEnumerator PlayLoadsTheExistingGameOnceAndPreservesVolume()
    {
        menu.SetMasterVolume(0.24f);
        var play = Button("PlayGameButton");
        Click(play);
        Assert.That(menu.IsLoading, Is.True);
        Assert.That(play.IsInteractable(), Is.False);
        menu.PlayGame();
        float deadline = Time.realtimeSinceStartup + 60f;
        while (SceneManager.GetActiveScene().name != "2DCardScene" && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("2DCardScene"));
        Assert.That(Object.FindFirstObjectByType<CardGameController>(), Is.Not.Null);
        Assert.That(Object.FindFirstObjectByType<MainMenuController>(), Is.Null);
        Assert.That(AudioListener.volume, Is.EqualTo(0.24f).Within(0.001f));
    }

    private Button Button(string name) => menu.GetComponentsInChildren<Button>(true).Single(b => b.name == name);

    private static void Click(Button button)
    {
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        }, ExecuteEvents.pointerClickHandler);
    }
}
