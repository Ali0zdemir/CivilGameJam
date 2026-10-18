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
    public static class CardPauseMenuAuthoring
    {
        private const string GameScenePath = "Assets/2DCardScene.unity";

        [MenuItem("Tools/Civil Game Jam/Center And Enlarge Card Pause Menu")]
        public static void CenterSavedMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit the pause menu outside Play Mode.");
            var original = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().Select(root => root.GetComponent<PauseMenuController>())
                    .First(menu => menu != null);
                CenterAndEnlargeMenu(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the centered pause menu.");
            }
            finally
            {
                if (original.isLoaded) SceneManager.SetActiveScene(original);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static void CenterAndEnlargeMenu(PauseMenuController controller)
        {
            var panel = (RectTransform)controller.transform.Find("PausePanel/MainPanel");
            var accent = panel.Find("TitleAccent");
            if (accent != null) Object.DestroyImmediate(accent.gameObject);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.localScale = Vector3.one;
            panel.sizeDelta = new Vector2(660f, 700f);
            Place("Title", 200f, 1.5f);
            Place("ContinueButton", -25f, 1.5f);
            Place("BackToMainMenuButton", -165f, 1.5f);
            Place("VolumeControl", -296f, 1f);

            void Place(string name, float y, float scale)
            {
                var rect = panel.Find(name) as RectTransform;
                if (rect == null) return;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(0f, y);
                rect.localScale = Vector3.one * scale;
                EditorUtility.SetDirty(rect);
            }
            EditorUtility.SetDirty(panel);
        }

        [MenuItem("Tools/Civil Game Jam/Add Backup Menu As Card Scene Pause Menu")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author the pause menu outside Play Mode.");

            var gameScene = SceneManager.GetSceneByPath(GameScenePath);
            if (!gameScene.isLoaded) gameScene = EditorSceneManager.OpenScene(GameScenePath);
            var existing = gameScene.GetRootGameObjects().FirstOrDefault(root => root.GetComponent<PauseMenuController>() != null);
            if (existing != null)
            {
                Debug.Log("The card scene already contains its editable pause menu.");
                return;
            }

            var sourceScene = SceneManager.GetSceneByPath(MainMenuBackupAuthoring.ScenePath);
            bool openedSource = !sourceScene.isLoaded;
            if (openedSource) sourceScene = EditorSceneManager.OpenScene(MainMenuBackupAuthoring.ScenePath, OpenSceneMode.Additive);
            try
            {
                var source = sourceScene.GetRootGameObjects().Select(root => root.GetComponent<MainMenuController>())
                    .FirstOrDefault(menu => menu != null);
                if (source == null) throw new InvalidOperationException("The backup main menu UI is missing.");

                var root = new GameObject("PauseMenuCanvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PauseMenuController));
                root.layer = LayerMask.NameToLayer("UI");
                SceneManager.MoveGameObjectToScene(root, gameScene);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                EditorUtility.CopySerialized(source.GetComponent<CanvasScaler>(), root.GetComponent<CanvasScaler>());

                var backdrop = new GameObject("PausePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                backdrop.layer = root.layer;
                var backdropRect = (RectTransform)backdrop.transform;
                backdropRect.SetParent(root.transform, false);
                backdropRect.anchorMin = Vector2.zero;
                backdropRect.anchorMax = Vector2.one;
                backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
                var shade = backdrop.GetComponent<Image>();
                shade.color = new Color(0f, 0f, 0f, .846f);
                shade.raycastTarget = true;

                var panel = Object.Instantiate(source.transform.Find("MainPanel").gameObject, backdrop.transform, false);
                panel.name = "MainPanel";
                var panelRect = (RectTransform)panel.transform;
                panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
                panelRect.anchoredPosition = Vector2.zero;
                MenuTitleLogoAuthoring.ApplyToPanel(panel.transform);
                // Keep the saved title and the Play/Exit artwork. Omit the extra grey Options button.
                var options = panel.transform.Find("OptionsButton");
                if (options != null) Object.DestroyImmediate(options.gameObject);
                var status = panel.transform.Find("Status");
                if (status != null) Object.DestroyImmediate(status.gameObject);
                var resume = panel.transform.Find("PlayGameButton").GetComponent<Button>();
                var back = panel.transform.Find("ExitButton").GetComponent<Button>();
                resume.name = "ContinueButton";
                back.name = "BackToMainMenuButton";
                resume.GetComponentInChildren<TMP_Text>().text = "Continue";
                var backLabel = back.GetComponentInChildren<TMP_Text>();
                backLabel.text = "Back to the Main Menu";
                MainMenuButtonArtAuthoring.FitLabelToArtwork(resume);
                MainMenuButtonArtAuthoring.FitLabelToArtwork(back);
                ((RectTransform)back.transform).anchoredPosition = new Vector2(0f, -107f);
                resume.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = back, selectOnDown = back };
                back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = resume, selectOnDown = resume };
                resume.onClick = new Button.ButtonClickedEvent();
                back.onClick = new Button.ButtonClickedEvent();
                var controller = root.GetComponent<PauseMenuController>();
                UnityEventTools.AddPersistentListener(resume.onClick, controller.ContinueGame);
                UnityEventTools.AddPersistentListener(back.onClick, controller.BackToMainMenu);
                var data = new SerializedObject(controller);
                data.FindProperty("pausePanel").objectReferenceValue = backdrop;
                data.FindProperty("continueButton").objectReferenceValue = resume;
                data.FindProperty("mainMenuButton").objectReferenceValue = back;
                data.FindProperty("mainMenuScene").stringValue = MainMenuBackupAuthoring.ScenePath;
                data.ApplyModifiedPropertiesWithoutUndo();
                MenuVolumeSliderAuthoring.ApplyToPauseMenu(controller);
                backdrop.SetActive(false);
                EditorSceneManager.MarkSceneDirty(gameScene);
                if (!EditorSceneManager.SaveScene(gameScene)) throw new InvalidOperationException("Could not save the pause menu.");
                Debug.Log("Card scene pause menu saved with the backup title, Continue and Back to the Main Menu.");
            }
            finally
            {
                if (openedSource) EditorSceneManager.CloseScene(sourceScene, true);
            }
        }
    }
}
