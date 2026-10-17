using System;
using System.Linq;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardShowcaseControlsAuthoring
    {
        [MenuItem("Tools/Card Game/Show Saved Showcase Controls")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Arrange the saved showcase controls in Edit Mode.");
            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            var game = Object.FindFirstObjectByType<CardGameController>();
            var viewport = Object.FindFirstObjectByType<CityViewport>();
            Configure(game, viewport, GameObject.Find("World3D"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved visible showcase test controls and a more centered city camera.");
        }

        internal static void Configure(CardGameController game, CityViewport viewport, GameObject world)
        {
            ConfigureDisaster(world.GetComponentInChildren<EarthquakeTestController>(), viewport.ViewRect, 0, "EARTHQUAKE");
            ConfigureDisaster(world.GetComponentInChildren<FireTestController>(), viewport.ViewRect, 1, "FIRE");
            ConfigureDisaster(world.GetComponentInChildren<TsunamiTestController>(), viewport.ViewRect, 2, "TSUNAMI");
            ConfigureUpgrades(world.GetComponentInChildren<RailSystemTestPanel>(), viewport.ViewRect, 0, "RAIL SYSTEM");
            ConfigureUpgrades(world.GetComponentInChildren<HouseSprinklerTestPanel>(), viewport.ViewRect, 1, "FIRE PROTECTION");
            ConfigureUpgrades(world.GetComponentInChildren<HouseColumnsTestPanel>(), viewport.ViewRect, 2, "FLOOD PROTECTION");

            var controls = viewport.GetComponent<CanvasGroup>();
            if (controls == null) controls = viewport.gameObject.AddComponent<CanvasGroup>();
            controls.interactable = true; controls.blocksRaycasts = true;
            var inputGuard = new SerializedObject(world.GetComponent<CardCityNavigation>());
            inputGuard.FindProperty("testControls").objectReferenceValue = controls;
            inputGuard.ApplyModifiedPropertiesWithoutUndo();

            var navigation = world.GetComponentInChildren<CityCameraController>();
            var camera = viewport.WorldCamera;
            var data = new SerializedObject(viewport);
            data.FindProperty("verticalAlignment").floatValue = 0.8f;
            data.FindProperty("frameWorldAutomatically").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            Vector3 position = camera.transform.position, localPosition = camera.transform.localPosition;
            viewport.RefreshViewport();
            Vector3 offset = camera.transform.position - position;
            camera.transform.localPosition = localPosition;
            navigation.transform.position += offset;
            data.Update();
            data.FindProperty("frameWorldAutomatically").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            var navData = new SerializedObject(navigation);
            navData.FindProperty("viewportVerticalAlignment").floatValue = 0.8f;
            navData.FindProperty("viewportReferenceAspect").floatValue = camera.aspect;
            navData.ApplyModifiedPropertiesWithoutUndo();
            viewport.RefreshViewport();
        }

        private static void ConfigureDisaster(MonoBehaviour controller, RectTransform city, int column, string title)
        {
            var data = new SerializedObject(controller);
            var button = (Button)data.FindProperty("testButton").objectReferenceValue;
            var canvas = button.GetComponentInParent<Canvas>(true);
            Embed(canvas, city, column, 18f, 108f);
            var panel = (RectTransform)button.transform.parent;
            Stretch(panel);
            panel.GetComponent<Image>().color = new Color(0.045f, 0.07f, 0.085f, 0.94f);
            var heading = panel.Find("Heading").GetComponent<Text>();
            heading.text = title; heading.fontSize = 28; heading.alignment = TextAnchor.UpperLeft;
            heading.color = new Color(0.96f, 0.72f, 0.35f);
            Top(heading.rectTransform, 12f, 34f, 16f, 172f);
            var status = (Text)data.FindProperty("statusLabel").objectReferenceValue;
            status.text = "Ready"; status.fontSize = 21; status.alignment = TextAnchor.UpperLeft;
            Top(status.rectTransform, 50f, 30f, 16f, 172f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(140f, 64f); rect.anchoredPosition = new Vector2(-16f, -48f);
            rect.localScale = Vector3.one;
            var label = button.GetComponentInChildren<Text>(true);
            label.text = "TEST"; label.fontSize = 26; label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform);
            Top((RectTransform)panel.Find("Progress_Track"), 92f, 5f, 16f, 16f);
            canvas.gameObject.SetActive(true);
        }

        private static void ConfigureUpgrades(MonoBehaviour controller, RectTransform city, int column, string title)
        {
            var data = new SerializedObject(controller);
            var panel = (RectTransform)data.FindProperty("panelRect").objectReferenceValue;
            var body = (GameObject)data.FindProperty("panelBody").objectReferenceValue;
            var canvas = panel.GetComponentInParent<Canvas>(true);
            Embed(canvas, city, column, 138f, 60f);
            Top(panel, 0f, 60f);
            panel.GetComponent<Image>().color = new Color(0.045f, 0.07f, 0.085f, 0.96f);
            data.FindProperty("expanded").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            var header = (RectTransform)panel.Find("Collapse_Expand");
            Top(header, 0f, 60f);
            var heading = (Text)data.FindProperty("headerLabel").objectReferenceValue;
            heading.text = title + "  [+]"; heading.fontSize = 24;
            Top(heading.rectTransform, 6f, 28f, 16f, 16f);
            var status = (Text)data.FindProperty("statusLabel").objectReferenceValue;
            status.text = "0 / 4 installed"; status.fontSize = 18;
            Top(status.rectTransform, 35f, 20f, 16f, 16f);
            Top((RectTransform)body.transform, 60f, 194f);
            var rows = data.FindProperty("buildings");
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var name = (Text)row.FindPropertyRelative("buildingLabel").objectReferenceValue;
                var button = (Button)row.FindPropertyRelative("toggleButton").objectReferenceValue;
                var action = (Text)row.FindPropertyRelative("buttonLabel").objectReferenceValue;
                name.text = "House " + (i + 1); name.fontSize = 22;
                Top(name.rectTransform, 8f + i * 32f, 28f, 16f, 150f);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                rect.sizeDelta = new Vector2(120f, 28f); rect.anchoredPosition = new Vector2(-16f, -8f - i * 32f);
                action.text = "ADD"; action.fontSize = 20;
                Stretch(action.rectTransform);
            }
            var reset = (Button)data.FindProperty("resetButton").objectReferenceValue;
            Top((RectTransform)reset.transform, 146f, 32f, 16f, 16f);
            var resetLabel = reset.GetComponentInChildren<Text>(true);
            resetLabel.text = "REMOVE ALL"; resetLabel.fontSize = 20;
            Stretch(resetLabel.rectTransform);
            body.SetActive(false);
            canvas.gameObject.SetActive(true);
        }

        private static void Embed(Canvas canvas, RectTransform city, int column, float top, float height)
        {
            canvas.gameObject.SetActive(false);
            canvas.transform.SetParent(city, false);
            canvas.overrideSorting = false; canvas.sortingOrder = 0;
            if (canvas.TryGetComponent<CanvasScaler>(out var scaler)) scaler.enabled = false;
            var rect = (RectTransform)canvas.transform;
            rect.anchorMin = new Vector2(column / 3f, 1f); rect.anchorMax = new Vector2((column + 1f) / 3f, 1f);
            rect.pivot = new Vector2(0.5f, 1f); rect.sizeDelta = new Vector2(-24f, height);
            rect.anchoredPosition = new Vector2(0f, -top); rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = city.gameObject.layer;
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            {
                text.raycastTarget = false;
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private static void Top(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
        {
            rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-left - right, height);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top); rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
    }
}
