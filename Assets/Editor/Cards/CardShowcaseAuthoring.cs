using System;
using System.Linq;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardShowcaseAuthoring
    {
        public const string SourceScene = "Assets/Scenes/BuildingShowcase.unity";
        public const string CardScene = "Assets/2DCardScene.unity";

        [MenuItem("Tools/Card Game/Import Building Showcase Into Saved Card Scene")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Import the city in Edit Mode.");

            var target = EditorSceneManager.OpenScene(CardScene);
            var game = Object.FindFirstObjectByType<CardGameController>();
            var viewport = Object.FindFirstObjectByType<CityViewport>();
            viewport.enabled = false;
            var oldCamera = viewport.WorldCamera;
            var oldWorld = GameObject.Find("World3D");
            var oldLights = target.GetRootGameObjects().Where(root => root.GetComponent<Light>() != null).ToArray();
            var source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(source);
            var sourceRoots = source.GetRootGameObjects();
            var navigation = sourceRoots.SelectMany(root => root.GetComponentsInChildren<CityCameraController>(true)).Single();
            var camera = navigation.GetComponentInChildren<Camera>(true);
            var sourceLighting = new LightingSnapshot();
            SceneManager.SetActiveScene(target);

            foreach (var control in viewport.GetComponentsInChildren<Canvas>(true))
                if (control.name.EndsWith("_Test_UI", StringComparison.Ordinal)) Object.DestroyImmediate(control.gameObject);
            if (oldWorld != null) Object.DestroyImmediate(oldWorld);
            if (oldCamera != null) Object.DestroyImmediate(oldCamera.gameObject);
            foreach (var light in oldLights) if (light != null) Object.DestroyImmediate(light);
            var world = new GameObject("World3D");
            foreach (var root in sourceRoots)
            {
                SceneManager.MoveGameObjectToScene(root, target);
                root.transform.SetParent(world.transform, true);
            }
            EditorSceneManager.CloseScene(source, true);
            sourceLighting.Apply();
            CardCitySpacingAuthoring.Configure(world);

            // Retain the complete showcase tools for editing, with one active input system
            // and the card canvas owning the combined game's visible interface.
            foreach (var testCanvas in world.GetComponentsInChildren<Canvas>(true)) testCanvas.gameObject.SetActive(false);
            foreach (var eventSystem in world.GetComponentsInChildren<EventSystem>(true)) eventSystem.gameObject.SetActive(false);
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                listener.enabled = listener.gameObject == camera.gameObject;
            camera.tag = "MainCamera";
            camera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
            camera.depth = -10f;
            camera.enabled = true;

            var data = new SerializedObject(viewport);
            data.FindProperty("worldCamera").objectReferenceValue = camera;
            data.FindProperty("world").objectReferenceValue = world.transform.Find("Buildings_3D");
            data.FindProperty("frameWorldAutomatically").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            Vector3 originalCameraPosition = camera.transform.position;
            Vector3 originalCameraLocalPosition = camera.transform.localPosition;
            viewport.RefreshViewport();
            // Bake the fitted pose into the rig, leaving the camera-local pose for earthquake shake.
            Vector3 fittingOffset = camera.transform.position - originalCameraPosition;
            camera.transform.localPosition = originalCameraLocalPosition;
            navigation.transform.position += fittingOffset;
            data.Update();
            data.FindProperty("frameWorldAutomatically").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            viewport.enabled = true;

            var navData = new SerializedObject(navigation);
            navData.FindProperty("inputViewport").objectReferenceValue = viewport.ViewRect;
            navData.FindProperty("viewportReferenceAspect").floatValue = camera.aspect;
            navData.FindProperty("viewportVerticalAlignment").floatValue = data.FindProperty("verticalAlignment").floatValue;
            navData.FindProperty("farthestZoom").floatValue = Mathf.Max(7f, camera.orthographicSize * 1.6f);
            var blockers = navData.FindProperty("inputBlockers"); blockers.arraySize = 2;
            blockers.GetArrayElementAtIndex(0).objectReferenceValue = game.transform;
            blockers.GetArrayElementAtIndex(1).objectReferenceValue = game.Sidebar.Area;
            navData.ApplyModifiedPropertiesWithoutUndo();
            var guard = world.AddComponent<CardCityNavigation>();
            var guardData = new SerializedObject(guard);
            guardData.FindProperty("game").objectReferenceValue = game;
            guardData.FindProperty("navigation").objectReferenceValue = navigation;
            guardData.ApplyModifiedPropertiesWithoutUndo();

            CardShowcaseControlsAuthoring.Configure(game, viewport, world);
            CardHouseUpgradeAuthoring.Configure(game, world);

            if (!EditorBuildSettings.scenes.Any(scene => scene.path == SourceScene))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(SourceScene, true) }).ToArray();
            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);
            AssetDatabase.SaveAssets();
            VerifySavedCity();
            Debug.Log("Saved BuildingShowcase city, lighting, navigation, disasters and upgrades behind the existing card canvas.");
        }

        private static void VerifySavedCity()
        {
            var game = Object.FindFirstObjectByType<CardGameController>();
            var city = GameObject.Find("World3D");
            if (game == null || city == null || Object.FindFirstObjectByType<CityViewport>().FramesWorldAutomatically)
                throw new InvalidOperationException("The card board and independent city camera must both be saved.");
            foreach (var transform in city.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidOperationException("Missing city script: " + transform.name);
            if (city.GetComponentInChildren<EarthquakeTestController>() == null ||
                city.GetComponentInChildren<FireTestController>() == null || city.GetComponentInChildren<TsunamiTestController>() == null)
                throw new InvalidOperationException("All showcase disaster controllers must be retained.");
        }

        private sealed class LightingSnapshot
        {
            private readonly bool fog = RenderSettings.fog;
            private readonly FogMode fogMode = RenderSettings.fogMode;
            private readonly Color fogColor = RenderSettings.fogColor;
            private readonly float fogDensity = RenderSettings.fogDensity;
            private readonly float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
            private readonly UnityEngine.Rendering.AmbientMode ambientMode = RenderSettings.ambientMode;
            private readonly Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor,
                ground = RenderSettings.ambientGroundColor, light = RenderSettings.ambientLight;
            private readonly float ambientIntensity = RenderSettings.ambientIntensity, reflectionIntensity = RenderSettings.reflectionIntensity;
            private readonly Material skybox = RenderSettings.skybox;
            private readonly Light sun = RenderSettings.sun;

            public void Apply()
            {
                RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientLight = light; RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.reflectionIntensity = reflectionIntensity; RenderSettings.skybox = skybox; RenderSettings.sun = sun;
            }
        }
    }
}
