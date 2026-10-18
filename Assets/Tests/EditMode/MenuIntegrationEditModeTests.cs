using System.Linq;
using CivilGameJam.Menu;
using CivilGameJam.Disasters;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class MenuIntegrationEditModeTests
{
    [Test]
    public void BackupMenuAndPauseHaveSavedBindingsAndOnlyTheBackupIsAddedToBuildScenes()
    {
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
        Assert.That(scenes[0].path, Is.EqualTo("Assets/Scenes/MainMenuBackup.unity"));
        Assert.That(scenes.Select(scene => scene.path), Does.Contain("Assets/2DCardScene.unity"));
        Assert.That(scenes.Select(scene => scene.path), Does.Not.Contain("Assets/Scenes/MainMenu.unity"));
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenuBackup.unity");
        var main = Object.FindFirstObjectByType<MainMenuController>();
        Assert.That(main, Is.Not.Null);
        var data = new SerializedObject(main);
        foreach (string field in new[] { "mainPanel", "playButton", "playLabel", "statusLabel", "masterVolumeSlider", "masterVolumeLabel" })
            Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(data.FindProperty("gameplayScene").stringValue, Is.EqualTo("Assets/2DCardScene.unity"));
        var slider = main.GetComponentInChildren<Slider>(true);
        Assert.That(slider.onValueChanged.GetPersistentTarget(0), Is.SameAs(main));
        Assert.That(slider.onValueChanged.GetPersistentMethodName(0), Is.EqualTo("SetMasterVolume"));
        CheckScene();

        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        Assert.That(pause, Is.Not.Null);
        data = new SerializedObject(pause);
        foreach (string field in new[] { "pausePanel", "continueButton", "mainMenuButton", "masterVolumeSlider", "masterVolumeLabel" })
            Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(data.FindProperty("mainMenuScene").stringValue, Is.EqualTo("Assets/Scenes/MainMenuBackup.unity"));
        Assert.That(pause.transform.Find("PausePanel").gameObject.activeSelf, Is.False);
        Assert.That(pause.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(2100));
        var victory = Object.FindFirstObjectByType<MonsterSequenceResults>();
        Assert.That(new SerializedObject(victory).FindProperty("mainMenuScene").stringValue,
            Is.EqualTo("Assets/Scenes/MainMenuBackup.unity"));
        CheckScene();
    }

    private static void CheckScene()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                Assert.That(source.ignoreListenerVolume, Is.False, "All music and effects must follow the master volume.");
        }
        foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(label => label.GetComponentInParent<MainMenuController>() != null || label.GetComponentInParent<PauseMenuController>() != null))
            Assert.That(label.font, Is.SameAs(font), label.name);
    }
}
