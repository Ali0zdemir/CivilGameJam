using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Menu.Editor
{
    /// <summary>Authors the backup menu with Emre's animated model beside the saved menu UI.</summary>
    public static class MainMenuBackupAuthoring
    {
        public const string ScenePath = "Assets/Scenes/MainMenuBackup.unity";
        private const string ModelPath = "Assets/emreAnimation/Main_Menu.fbx";
        private const string AnimationFolder = "Assets/Art/Menu";
        private const string ClipPath = AnimationFolder + "/MainMenu_Earthquake_Loop.anim";
        private const string ControllerPath = AnimationFolder + "/MainMenu_Earthquake.controller";

        [MenuItem("Tools/Civil Game Jam/Create Or Open Backup Main Menu")]
        public static void CreateOrOpen()
        {
            RequireEditMode();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            CreateForBatch();
        }

        public static void CreateForBatch()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("The backup menu already exists; edit its saved scene.");
            if (!AssetDatabase.CopyAsset(MainMenuSceneAuthoring.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy the saved menu UI into the backup scene.");
            ConfigureEarthquakeMenu();
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        }

        [MenuItem("Tools/Civil Game Jam/Use Animated Main Menu Model")]
        public static void ConfigureEarthquakeMenu()
        {
            RequireEditMode();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            var camera = Camera.main;
            if (menu == null || camera == null) throw new InvalidOperationException("The saved menu UI or camera is missing.");
            foreach (var root in scene.GetRootGameObjects())
                if (root != camera.gameObject && root != menu.transform.root.gameObject && root.GetComponent<EventSystem>() == null)
                    Object.DestroyImmediate(root);

            var scaler = menu.GetComponent<CanvasScaler>();
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var main = (RectTransform)menu.transform.Find("MainPanel");
            main.anchorMin = main.anchorMax = new Vector2(0.75f, 0.5f);
            main.anchoredPosition = Vector2.zero;
            Position(main, "Title", 208f);
            Position(main, "TitleAccent", 82f);
            Position(main, "PlayGameButton", -14f);
            Position(main, "OptionsButton", -107f);
            Position(main, "ExitButton", main.Find("VolumeControl") != null ? -107f : -200f);
            Position(main, "Status", -277f);

            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.rotation = Quaternion.Euler(22f, -35f, 0f);
            camera.transform.position = -camera.transform.forward * 18f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Preserve the background color authored on the scene camera in the Inspector.
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7f, 0.72f, 0.76f);

            CreateAnimatedModel(camera);
            var lights = new GameObject("ModelLighting").transform;
            RenderSettings.sun = DirectionalLight("KeyLight", lights, new Vector3(32f, -35f, 0f),
                new Color(1f, 0.96f, 0.9f), 1.15f);
            DirectionalLight("FillLight", lights, new Vector3(20f, 135f, 0f),
                new Color(0.78f, 0.86f, 1f), 0.45f);

            MainMenuButtonArtAuthoring.ApplyToMenu(menu);
            MenuTitleLogoAuthoring.ApplyToPanel(main);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the animated menu.");
            AssetDatabase.SaveAssets();
            Debug.Log("Backup menu saved: animated Main_Menu model on the left, title and working buttons on the right.");
        }

        private static void CreateAnimatedModel(Camera camera)
        {
            var clip = BuildEarthquakeLoop();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("Emre's Main_Menu model is missing.");
            var anchor = new GameObject("MainMenuModel", typeof(MenuModelViewportAnchor)).transform;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, anchor);
            model.name = "Main_Menu";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = BuildController(clip);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            var bounds = ProjectedBounds(model, camera);
            float scale = Mathf.Min(5.6f / Mathf.Max(bounds.size.y, 0.001f), 5.3f / Mathf.Max(bounds.size.x, 0.001f));
            model.transform.localScale *= scale;
            bounds = ProjectedBounds(model, camera);
            model.transform.position -= camera.transform.TransformVector(bounds.center);

            var data = new SerializedObject(anchor.GetComponent<MenuModelViewportAnchor>());
            data.FindProperty("viewCamera").objectReferenceValue = camera;
            data.FindProperty("viewportPoint").vector2Value = new Vector2(0.25f, 0.5f);
            data.FindProperty("depth").floatValue = 18f;
            data.FindProperty("referenceWidth").floatValue = bounds.size.x;
            data.ApplyModifiedPropertiesWithoutUndo();
            anchor.GetComponent<MenuModelViewportAnchor>().RefreshPosition();
        }

        [MenuItem("Tools/Civil Game Jam/Rebuild Menu Earthquake Animation")]
        public static void RebuildEarthquakeAnimation()
        {
            RequireEditMode();
            BuildEarthquakeLoop();
            AssetDatabase.SaveAssets();
            Debug.Log("Menu earthquake animation rebuilt with all six buildings' original movement.");
        }

        private static AnimationClip BuildEarthquakeLoop()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Emre's Main_Menu model is missing.");
            // Key reduction removes the subtle X motion on buildings 2, 4, and 6.
            if (importer.animationCompression != ModelImporterAnimationCompression.Off)
            {
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.SaveAndReimport();
            }
            if (!AssetDatabase.IsValidFolder(AnimationFolder)) AssetDatabase.CreateFolder("Assets/Art", "Menu");
            var loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (loop == null)
            {
                loop = new AnimationClip { name = "MainMenu_Earthquake_Loop", frameRate = 24f };
                AssetDatabase.CreateAsset(loop, ClipPath);
            }
            loop.ClearCurves();
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
            // The FBX exports every Blender action against every object. Use each building's
            // matching seismic action, preserving its authored position and timing.
            for (int i = 1; i <= 6; i++)
            {
                string name = $"Apartment{i}(Default)(Seismic)|Apartment{i}Action.001";
                var source = sourceClips.SingleOrDefault(c => c.name == name);
                if (source == null) throw new InvalidOperationException("The model's earthquake action is missing: " + name);
                foreach (var binding in AnimationUtility.GetCurveBindings(source))
                    AnimationUtility.SetEditorCurve(loop, binding, AnimationUtility.GetEditorCurve(source, binding));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(loop);
            settings.loopTime = true;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(loop, settings);
            EditorUtility.SetDirty(loop);
            return loop;
        }

        private static AnimatorController BuildController(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.FirstOrDefault(s => s.state.name == "Earthquake").state;
            if (state == null) state = machine.AddState("Earthquake");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Bounds ProjectedBounds(GameObject model, Camera camera)
        {
            bool first = true;
            var result = new Bounds();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var point = camera.transform.InverseTransformVector(corner);
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            if (first) throw new InvalidOperationException("The Main_Menu model has no visible meshes.");
            return result;
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit the menu outside Play Mode.");
        }

        private static void Position(Transform panel, string name, float y)
        {
            var rect = (RectTransform)panel.Find(name);
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static Light DirectionalLight(string name, Transform parent, Vector3 angles, Color color, float intensity)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.rotation = Quaternion.Euler(angles);
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }
    }
}
