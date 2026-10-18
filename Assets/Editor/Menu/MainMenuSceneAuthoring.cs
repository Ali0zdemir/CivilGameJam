using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CivilGameJam.Menu.Editor
{
    /// <summary>Creates normal, editable scene objects; no menu UI is generated in play mode.</summary>
    public static class MainMenuSceneAuthoring
    {
        public const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/2DCardScene.unity";
        private static readonly Color Background = new Color32(119, 119, 119, 255);
        private static readonly Color Ink = new Color32(37, 39, 41, 255);
        private static readonly Color Paper = new Color32(247, 244, 232, 255);
        private static readonly Color Gold = new Color32(237, 188, 97, 255);

        [MenuItem("Tools/Civil Game Jam/Create Main Menu Scene")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create or edit the menu outside Play Mode.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
                ConfigureBuildScenes();
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
        }

        public static void CreateForBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("The menu already exists; edit its saved Hierarchy directly.");
            CreateScene();
        }

        private static void CreateScene()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
            if (font == null) throw new InvalidOperationException("The game's Pixellari font is missing.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            // Assign a sprite here, or add more SpriteRenderers under BackgroundSprites.
            // The UI has no full-screen background Image, so these remain visible behind it.
            var artwork = new GameObject("BackgroundSprites");
            var sprite = new GameObject("BackgroundArt - Assign Sprite", typeof(SpriteRenderer));
            sprite.transform.SetParent(artwork.transform, false);
            sprite.transform.localPosition = new Vector3(2.8f, 0f, 0f);
            sprite.GetComponent<SpriteRenderer>().sortingOrder = -100;

            MainMenuBlueprintAuthoring.Author(camera);

            var canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MainMenuController));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var controller = canvasObject.GetComponent<MainMenuController>();

            var main = Rect("MainPanel", canvas.transform, new Vector2(420f, 610f), Vector2.zero);
            main.anchorMin = main.anchorMax = new Vector2(0.5f, 0.5f);
            var group = main.gameObject.AddComponent<CanvasGroup>();
            Label("Title", main, "CIVIL\nGAME JAM", font, 78f, Paper,
                new Vector2(440f, 200f), new Vector2(0f, 250f), TextAlignmentOptions.Midline);
            var rule = Image("TitleAccent", main, Gold, new Vector2(64f, 5f), new Vector2(0f, 120f));
            rule.raycastTarget = false;
            var play = MenuButton("PlayGameButton", main, "Play Game", font, new Vector2(0f, 4f), true);
            var options = MenuButton("OptionsButton", main, "Options", font, new Vector2(0f, -89f));
            var exit = MenuButton("ExitButton", main, "Exit", font, new Vector2(0f, -182f));
            var status = Label("Status", main, "", font, 20f, Paper,
                new Vector2(420f, 50f), new Vector2(0f, -264f), TextAlignmentOptions.MidlineLeft);
            VerticalNavigation(play, options, exit);

            var optionsRoot = Rect("OptionsPanel", canvas.transform, Vector2.zero, Vector2.zero);
            Stretch(optionsRoot);
            var shade = optionsRoot.gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.56f);
            var dialog = Rect("Dialog", optionsRoot, new Vector2(620f, 410f), Vector2.zero);
            var dialogImage = dialog.gameObject.AddComponent<Image>();
            dialogImage.color = Ink;
            var border = dialog.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(Paper.r, Paper.g, Paper.b, 0.28f);
            border.effectDistance = new Vector2(2f, -2f);
            Label("Heading", dialog, "Options", font, 54f, Paper,
                new Vector2(500f, 70f), new Vector2(0f, 133f), TextAlignmentOptions.MidlineLeft);
            Label("VolumeTitle", dialog, "Master Volume", font, 28f, Paper,
                new Vector2(350f, 45f), new Vector2(-75f, 42f), TextAlignmentOptions.MidlineLeft);
            var volumeValue = Label("VolumeValue", dialog, "100%", font, 28f, Gold,
                new Vector2(130f, 45f), new Vector2(185f, 42f), TextAlignmentOptions.MidlineRight);
            var slider = VolumeSlider(dialog);
            var back = MenuButton("BackButton", dialog, "Back", font, new Vector2(0f, -126f));
            back.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 65f);
            var sliderNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = back, selectOnUp = back };
            slider.navigation = sliderNav;
            back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = slider, selectOnDown = slider };

            var data = new SerializedObject(controller);
            data.FindProperty("gameplayScene").stringValue = GameScenePath;
            Assign(data, "mainPanel", group);
            Assign(data, "optionsPanel", optionsRoot.gameObject);
            Assign(data, "playButton", play);
            Assign(data, "optionsButton", options);
            Assign(data, "playLabel", play.GetComponentInChildren<TMP_Text>());
            Assign(data, "statusLabel", status);
            Assign(data, "masterVolumeSlider", slider);
            Assign(data, "masterVolumeLabel", volumeValue);
            data.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(play.onClick, controller.PlayGame);
            UnityEventTools.AddPersistentListener(options.onClick, controller.OpenOptions);
            UnityEventTools.AddPersistentListener(exit.onClick, controller.ExitGame);
            UnityEventTools.AddPersistentListener(back.onClick, controller.CloseOptions);
            UnityEventTools.AddPersistentListener(slider.onValueChanged, controller.SetMasterVolume);
            optionsRoot.gameObject.SetActive(false);

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<EventSystem>().firstSelectedGameObject = play.gameObject;
            var input = events.GetComponent<InputSystemUIInputModule>();
            const string inputPath = "Assets/InputSystem_Actions.inputactions";
            input.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            var actions = AssetDatabase.LoadAllAssetsAtPath(inputPath).OfType<InputActionReference>().ToArray();
            InputActionReference Action(string name) => actions.First(a => a.action.actionMap.name == "UI" && a.action.name == name);
            input.point = Action("Point");
            input.move = Action("Navigate");
            input.submit = Action("Submit");
            input.cancel = Action("Cancel");
            input.leftClick = Action("Click");
            input.rightClick = Action("RightClick");
            input.middleClick = Action("MiddleClick");
            input.scrollWheel = Action("ScrollWheel");
            input.trackedDevicePosition = Action("TrackedDevicePosition");
            input.trackedDeviceOrientation = Action("TrackedDeviceOrientation");
            input.deselectOnBackgroundClick = false;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the main menu scene.");
            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Main menu saved with editable UI, sprite space, master volume, and gameplay scene connection.");
        }

        private static void ConfigureBuildScenes()
        {
            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != GameScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            }.Concat(others).ToArray();
        }

        private static void Assign(SerializedObject data, string field, UnityEngine.Object value)
            => data.FindProperty(field).objectReferenceValue = value;

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
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

        private static Image Image(string name, Transform parent, Color color, Vector2 size, Vector2 position)
        {
            var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, TMP_FontAsset font,
            float fontSize, Color color, Vector2 size, Vector2 position, TextAlignmentOptions alignment)
        {
            var label = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static Button MenuButton(string name, Transform parent, string caption, TMP_FontAsset font,
            Vector2 position, bool primary = false)
        {
            var image = Image(name, parent, primary ? Gold : Ink, new Vector2(420f, 76f), position);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(1.12f, 1.12f, 1.12f) : new Color(1.4f, 1.4f, 1.4f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            var shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -5f);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = primary ? new Color(1f, 0.89f, 0.67f, 0.8f) : new Color(Paper.r, Paper.g, Paper.b, 0.28f);
            outline.effectDistance = new Vector2(1f, -1f);
            var label = Label("Label", image.transform, caption, font, 36f, primary ? Ink : Paper,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Midline);
            Stretch(label.rectTransform);
            return button;
        }

        private static Slider VolumeSlider(Transform parent)
        {
            var rect = Rect("MasterVolumeSlider", parent, new Vector2(500f, 52f), new Vector2(0f, -19f));
            var slider = rect.gameObject.AddComponent<Slider>();
            var track = Image("Track", rect, new Color32(77, 79, 81, 255), new Vector2(480f, 12f), Vector2.zero);
            var fillArea = Rect("FillArea", rect, Vector2.zero, Vector2.zero);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(10f, 20f);
            fillArea.offsetMax = new Vector2(-10f, -20f);
            var fill = Image("Fill", fillArea, Gold, Vector2.zero, Vector2.zero);
            Stretch(fill.rectTransform);
            var handleArea = Rect("HandleArea", rect, Vector2.zero, Vector2.zero);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 8f);
            handleArea.offsetMax = new Vector2(-10f, -8f);
            var handle = Image("Handle", handleArea, Paper, new Vector2(24f, 0f), Vector2.zero);
            handle.raycastTarget = true;
            var hitArea = rect.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            var colors = slider.colors;
            colors.highlightedColor = Gold;
            colors.selectedColor = Gold;
            slider.colors = colors;
            return slider;
        }

        private static void VerticalNavigation(params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnDown = buttons[(i + 1) % buttons.Length]
                };
        }
    }
}
