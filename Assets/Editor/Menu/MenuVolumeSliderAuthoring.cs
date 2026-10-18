using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Menu.Editor
{
    public static class MenuVolumeSliderAuthoring
    {
        private const string ArtPath = "Assets/EfeArts/Loading Bar.png";
        private const string GameScenePath = "Assets/2DCardScene.unity";

        [MenuItem("Tools/Civil Game Jam/Apply Loading Bar Volume Sliders")]
        public static void ApplyToSavedMenus()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit the volume sliders outside Play Mode.");

            var original = SceneManager.GetActiveScene();
            var backup = SceneManager.GetSceneByPath(MainMenuBackupAuthoring.ScenePath);
            bool openedBackup = !backup.isLoaded;
            if (openedBackup) backup = EditorSceneManager.OpenScene(MainMenuBackupAuthoring.ScenePath, OpenSceneMode.Additive);
            try
            {
                var menu = backup.GetRootGameObjects().Select(root => root.GetComponent<MainMenuController>())
                    .First(controller => controller != null);
                ApplyToBackupMenu(menu);
                EditorSceneManager.MarkSceneDirty(backup);
                if (!EditorSceneManager.SaveScene(backup)) throw new InvalidOperationException("Could not save the backup menu volume slider.");

                var game = SceneManager.GetSceneByPath(GameScenePath);
                if (!game.isLoaded) game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                var pause = game.GetRootGameObjects().Select(root => root.GetComponent<PauseMenuController>())
                    .First(controller => controller != null);
                ApplyToPauseMenu(pause);
                EditorSceneManager.MarkSceneDirty(game);
                if (!EditorSceneManager.SaveScene(game)) throw new InvalidOperationException("Could not save the pause menu volume slider.");
                AssetDatabase.SaveAssets();
                Debug.Log("Loading Bar volume sliders saved in MainMenuBackup and the centered Esc menu.");
            }
            finally
            {
                if (original.isLoaded) SceneManager.SetActiveScene(original);
                if (openedBackup) EditorSceneManager.CloseScene(backup, true);
            }
        }

        private static void ApplyToBackupMenu(MainMenuController controller)
        {
            var panel = controller.transform.Find("MainPanel");
            var options = panel.Find("OptionsButton");
            var play = panel.Find("PlayGameButton").GetComponent<Button>();
            var exit = panel.Find("ExitButton").GetComponent<Button>();
            ((RectTransform)exit.transform).anchoredPosition = options != null
                ? ((RectTransform)options).anchoredPosition : new Vector2(0f, -107f);
            if (options != null) Object.DestroyImmediate(options.gameObject);

            var slider = BuildVolumeControl(panel, out var label);
            UnityEventTools.AddPersistentListener(slider.onValueChanged, controller.SetMasterVolume);
            var data = new SerializedObject(controller);
            data.FindProperty("optionsButton").objectReferenceValue = null;
            data.FindProperty("optionsPanel").objectReferenceValue = null;
            data.FindProperty("masterVolumeSlider").objectReferenceValue = slider;
            data.FindProperty("masterVolumeLabel").objectReferenceValue = label;
            data.ApplyModifiedPropertiesWithoutUndo();
            var optionsPanel = controller.transform.Find("OptionsPanel");
            if (optionsPanel != null) Object.DestroyImmediate(optionsPanel.gameObject);
            Navigation(play, exit, slider);
        }

        public static void ApplyToPauseMenu(PauseMenuController controller)
        {
            var panel = controller.transform.Find("PausePanel/MainPanel");
            var resume = panel.Find("ContinueButton").GetComponent<Button>();
            var back = panel.Find("BackToMainMenuButton").GetComponent<Button>();
            var slider = BuildVolumeControl(panel, out var label);
            UnityEventTools.AddPersistentListener(slider.onValueChanged, controller.SetMasterVolume);
            var data = new SerializedObject(controller);
            data.FindProperty("masterVolumeSlider").objectReferenceValue = slider;
            data.FindProperty("masterVolumeLabel").objectReferenceValue = label;
            data.ApplyModifiedPropertiesWithoutUndo();
            Navigation(resume, back, slider);
            CardPauseMenuAuthoring.CenterAndEnlargeMenu(controller);
        }

        private static Slider BuildVolumeControl(Transform parent, out TMP_Text valueLabel)
        {
            var old = parent.Find("VolumeControl");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var sprites = LoadBarSprites();
            var font = parent.GetComponentsInChildren<Button>(true)
                .Select(button => button.GetComponentInChildren<TMP_Text>(true)).First(label => label != null).font;
            var root = Rect("VolumeControl", parent, new Vector2(420f, 108f), new Vector2(0f, -222f));
            Label("VolumeTitle", root, "Master Volume", font, new Vector2(290f, 34f), new Vector2(-65f, 32f), TextAlignmentOptions.MidlineLeft);
            valueLabel = Label("VolumeValue", root, Mathf.RoundToInt(GameAudioSettings.MasterVolume * 100f) + "%", font,
                new Vector2(100f, 34f), new Vector2(160f, 32f), TextAlignmentOptions.MidlineRight);

            var sliderRect = Rect("MasterVolumeSlider", root, new Vector2(420f, 52f), new Vector2(0f, -17f));
            var hitArea = sliderRect.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            var track = Image("Track", sliderRect, new Vector2(420f, 35f), Color.white);
            track.sprite = sprites[0];
            track.type = UnityEngine.UI.Image.Type.Sliced;
            Image("Trough", sliderRect, new Vector2(406f, 21f), new Color32(53, 52, 69, 255));
            var fillArea = Rect("FillArea", sliderRect, new Vector2(406f, 21f), Vector2.zero);
            var fill = Image("Fill", fillArea, Vector2.zero, Color.white);
            Stretch(fill.rectTransform);
            fill.sprite = sprites[1];
            fill.type = UnityEngine.UI.Image.Type.Sliced;
            var handleArea = Rect("HandleArea", sliderRect, new Vector2(406f, 45f), Vector2.zero);
            var handle = Image("Handle", handleArea, new Vector2(12f, 0f), new Color32(247, 244, 232, 255));
            var border = handle.gameObject.AddComponent<Outline>();
            border.effectColor = new Color32(49, 46, 59, 255);
            border.effectDistance = new Vector2(2f, -2f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(GameAudioSettings.MasterVolume);
            var colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color32(237, 188, 97, 255);
            colors.pressedColor = new Color32(215, 161, 75, 255);
            colors.fadeDuration = .08f;
            slider.colors = colors;
            return slider;
        }

        private static Sprite[] LoadBarSprites()
        {
            var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Efe's Loading Bar artwork is missing.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // The artwork occupies the top 50 pixels; the rest of the source PNG is transparent.
#pragma warning disable CS0618
            importer.spritesheet = new[]
            {
                new SpriteMetaData { name = "VolumeBarFrame", rect = new Rect(0f, height - 50f, width, 50f),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f), border = new Vector4(10f, 10f, 10f, 10f) },
                new SpriteMetaData { name = "VolumeBarFill", rect = new Rect(10f, height - 40f, width - 20f, 30f),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) }
            };
#pragma warning restore CS0618
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(ArtPath).OfType<Sprite>().ToArray();
            return new[] { sprites.Single(sprite => sprite.name == "VolumeBarFrame"), sprites.Single(sprite => sprite.name == "VolumeBarFill") };
        }

        private static void Navigation(Selectable first, Selectable second, Slider slider)
        {
            first.navigation = new Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = slider, selectOnDown = second };
            second.navigation = new Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = first, selectOnDown = slider };
            slider.navigation = new Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = second, selectOnDown = first };
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image Image(string name, Transform parent, Vector2 size, Color color)
        {
            var image = Rect(name, parent, size, Vector2.zero).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, string text, TMP_FontAsset font, Vector2 size,
            Vector2 position, TextAlignmentOptions alignment)
        {
            var label = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 28f;
            label.color = new Color32(247, 244, 232, 255);
            label.text = text;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }
    }
}
