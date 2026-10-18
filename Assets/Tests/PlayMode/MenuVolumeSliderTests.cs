using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Menu;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class MenuVolumeSliderTests
{
    private bool hadSavedVolume;
    private float savedVolume, listenerVolume;
    private bool audioPaused;

    [UnitySetUp]
    public IEnumerator LoadBackup()
    {
        hadSavedVolume = PlayerPrefs.HasKey(GameAudioSettings.MasterVolumeKey);
        savedVolume = GameAudioSettings.MasterVolume;
        listenerVolume = AudioListener.volume;
        audioPaused = AudioListener.pause;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        GameAudioSettings.SetMasterVolume(.62f);
        yield return SceneManager.LoadSceneAsync("MainMenuBackup", LoadSceneMode.Single);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator RestoreAudio()
    {
        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        if (pause != null && pause.IsPaused) pause.ContinueGame();
        if (hadSavedVolume) PlayerPrefs.SetFloat(GameAudioSettings.MasterVolumeKey, savedVolume);
        else PlayerPrefs.DeleteKey(GameAudioSettings.MasterVolumeKey);
        PlayerPrefs.Save();
        AudioListener.volume = listenerVolume;
        AudioListener.pause = audioPaused;
        Time.timeScale = 1f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator BackupHasTwoButtonsAndTheArtworkSliderCanMuteAndRestoreVolume()
    {
        var menu = Object.FindFirstObjectByType<MainMenuController>();
        Assert.That(menu, Is.Not.Null);
        Assert.That(menu.OptionsOpen, Is.False);
        Assert.That(menu.transform.Find("OptionsPanel"), Is.Null);
        Assert.That(menu.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(2));
        var panel = menu.transform.Find("MainPanel");
        Assert.That(panel.Find("OptionsButton"), Is.Null);
        var exit = (RectTransform)panel.Find("ExitButton");
        var volume = (RectTransform)panel.Find("VolumeControl");
        Assert.That(exit.anchoredPosition.y, Is.EqualTo(-175f), "Keep the backup menu's saved button layout.");
        Assert.That(volume.anchoredPosition.y, Is.LessThan(exit.anchoredPosition.y));
        var slider = volume.GetComponentInChildren<Slider>();
        var frame = slider.transform.Find("Track").GetComponent<Image>().sprite;
        Assert.That(frame.texture.name, Is.EqualTo("Loading Bar"));
        Assert.That(frame.rect.size, Is.EqualTo(new Vector2(600f, 50f)));
        Assert.That(slider.value, Is.EqualTo(.62f).Within(.001f));
        DragSlider(slider, 0f);
        Assert.That(AudioListener.volume, Is.Zero);
        DragSlider(slider, 1f);
        Assert.That(AudioListener.volume, Is.EqualTo(1f));
        DragSlider(slider, .37f);
        Assert.That(AudioListener.volume, Is.EqualTo(.37f).Within(.002f));
        Assert.That(volume.Find("VolumeValue").GetComponent<TMP_Text>().text, Is.EqualTo("37%"));
        yield return null;
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator BothSlidersShareVolumeAcrossPlayContinueAndReturnToMainMenu()
    {
        var main = Object.FindFirstObjectByType<MainMenuController>();
        DragSlider(main.GetComponentInChildren<Slider>(), .34f);
        main.GetComponentsInChildren<Button>().Single(button => button.name == "PlayGameButton").onClick.Invoke();
        yield return WaitForScene("2DCardScene");
        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        Assert.That(pause, Is.Not.Null);
        Assert.That(AudioListener.volume, Is.EqualTo(.34f).Within(.002f));
        pause.PauseGame();
        var slider = pause.GetComponentInChildren<Slider>();
        Assert.That(slider.value, Is.EqualTo(.34f).Within(.002f));
        Assert.That(((RectTransform)pause.transform.Find("PausePanel/MainPanel")).anchorMin.x, Is.EqualTo(.5f));
        DragSlider(slider, 0f);
        Assert.That(AudioListener.volume, Is.Zero);
        DragSlider(slider, 1f);
        Assert.That(AudioListener.volume, Is.EqualTo(1f));
        DragSlider(slider, .52f);
        Assert.That(slider.transform.parent.Find("VolumeValue").GetComponent<TMP_Text>().text, Is.EqualTo("52%"));
        Assert.That(Time.timeScale, Is.Zero);
        yield return null;
        pause.ContinueGame();
        Assert.That(AudioListener.volume, Is.EqualTo(.52f).Within(.002f));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        pause.PauseGame();
        Assert.That(slider.value, Is.EqualTo(.52f).Within(.002f));
        pause.GetComponentsInChildren<Button>().Single(button => button.name == "BackToMainMenuButton").onClick.Invoke();
        yield return WaitForScene("MainMenuBackup");
        main = Object.FindFirstObjectByType<MainMenuController>();
        Assert.That(main.GetComponentInChildren<Slider>().value, Is.EqualTo(.52f).Within(.002f));
        Assert.That(AudioListener.volume, Is.EqualTo(.52f).Within(.002f));
        Assert.That(AudioListener.pause, Is.False);
    }

    private static IEnumerator WaitForScene(string name)
    {
        float deadline = Time.realtimeSinceStartup + 40f;
        while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));
    }

    private static void DragSlider(Slider slider, float value)
    {
        Canvas.ForceUpdateCanvases();
        var area = (RectTransform)slider.handleRect.parent;
        Vector3 world = area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, value), 0f));
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, world)
        };
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
    }
}
