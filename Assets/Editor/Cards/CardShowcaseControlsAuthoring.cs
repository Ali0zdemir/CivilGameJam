using TMPro;
using System;
using System.Linq;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using CivilGameJam.Presentation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
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
            Debug.Log("Restored saved disaster, sequence and protection tests for the current city without changing its camera or cards.");
        }

        internal static void Configure(CardGameController game, CityViewport viewport, GameObject world)
        {
            var quake = world.GetComponentInChildren<EarthquakeTestController>();
            var fire = world.GetComponentInChildren<FireTestController>();
            var tsunami = world.GetComponentInChildren<TsunamiTestController>();
            EnsureDisaster(quake, viewport.ViewRect, "Earthquake", quake.StartEarthquake, quake.StopEarthquake, new Color(1f, .64f, .28f));
            EnsureDisaster(fire, viewport.ViewRect, "Fire", fire.StartFire, fire.StopFire, new Color(1f, .39f, .31f));
            EnsureDisaster(tsunami, viewport.ViewRect, "Tsunami", tsunami.StartTsunami, tsunami.StopTsunami, new Color(.35f, .73f, 1f));
            ConfigureDisaster(quake, viewport.ViewRect, 0, "EARTHQUAKE");
            ConfigureDisaster(fire, viewport.ViewRect, 1, "FIRE");
            ConfigureDisaster(tsunami, viewport.ViewRect, 2, "TSUNAMI");
            foreach (var controller in new MonoBehaviour[] { world.GetComponentInChildren<RailSystemTestPanel>(),
                world.GetComponentInChildren<HouseSprinklerTestPanel>(), world.GetComponentInChildren<HouseColumnsTestPanel>() })
                EnsureUpgrades(controller, viewport.ViewRect);
            ConfigureUpgrades(world.GetComponentInChildren<RailSystemTestPanel>(), viewport.ViewRect, 0, "RAIL SYSTEM");
            ConfigureUpgrades(world.GetComponentInChildren<HouseSprinklerTestPanel>(), viewport.ViewRect, 1, "FIRE PROTECTION");
            ConfigureUpgrades(world.GetComponentInChildren<HouseColumnsTestPanel>(), viewport.ViewRect, 2, "ELEVATOR");
            var individual = new MonoBehaviour[] { quake, tsunami, fire }.Select(controller =>
                (Button)new SerializedObject(controller).FindProperty("testButton").objectReferenceValue).ToArray();
            var sequence = world.GetComponentInChildren<MonsterSequenceController>();
            if (sequence != null) EnsureSequence(sequence, viewport.ViewRect, individual);

            var controls = viewport.GetComponent<CanvasGroup>();
            if (controls == null) controls = viewport.gameObject.AddComponent<CanvasGroup>();
            controls.interactable = true; controls.blocksRaycasts = true;
            var inputGuard = new SerializedObject(world.GetComponent<CardCityNavigation>());
            inputGuard.FindProperty("testControls").objectReferenceValue = controls;
            inputGuard.ApplyModifiedPropertiesWithoutUndo();

            var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
            if (round != null && sequence != null)
            {
                var data = new SerializedObject(round);
                var menu = new SerializedObject(sequence);
                var locked = new[] { (Button)menu.FindProperty("menuButton").objectReferenceValue,
                    (Button)menu.FindProperty("startButton").objectReferenceValue }.Concat(individual)
                    .Concat(Enumerable.Range(0, 3).Select(i => (Button)menu.FindProperty("sequenceButtons").GetArrayElementAtIndex(i).objectReferenceValue)).ToArray();
                AssignArray(data, "debugStartButtons", locked);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // These controls are authored once in Edit Mode and saved as ordinary, editable scene objects.
        private static void EnsureDisaster(MonoBehaviour controller, RectTransform city, string name,
            UnityAction start, UnityAction stop, Color color)
        {
            var data = new SerializedObject(controller);
            if (data.FindProperty("testButton").objectReferenceValue != null) return;
            var canvas = CreateCanvas(name + "_Test_UI", city);
            var panel = CreateRect("Panel", canvas.transform);
            panel.gameObject.AddComponent<Image>();
            CreateLabel("Heading", panel, name.ToUpperInvariant(), 28f);
            var status = CreateLabel("Status", panel, "Ready", 21f);
            var button = CreateButton("Test_Button", panel, "TEST", start);
            CreateButton("Stop_Test", panel, "STOP", stop);
            var track = CreateRect("Progress_Track", panel);
            var background = track.gameObject.AddComponent<Image>();
            background.color = new Color(.12f, .17f, .2f); background.raycastTarget = false;
            var fill = CreateRect("Fill", track).gameObject.AddComponent<Image>();
            Stretch(fill.rectTransform);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0f;
            fill.color = color; fill.raycastTarget = false;
            var glow = fill.gameObject.AddComponent<Outline>();
            glow.effectColor = Color.Lerp(color, Color.white, .6f); glow.effectDistance = new Vector2(1.5f, -1.5f); glow.enabled = false;
            var effect = track.gameObject.AddComponent<ProgressBarFeedback>();
            var visibility = track.GetComponent<CanvasGroup>(); visibility.interactable = visibility.blocksRaycasts = false; visibility.alpha = 0f;
            var feedback = new SerializedObject(effect);
            Assign(feedback, "fill", fill); Assign(feedback, "visibility", visibility); Assign(feedback, "glow", glow);
            feedback.ApplyModifiedPropertiesWithoutUndo();
            track.gameObject.SetActive(false);
            Assign(data, "testButton", button); Assign(data, "statusLabel", status);
            Assign(data, "progressFill", fill); Assign(data, "progressFeedback", effect);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureUpgrades(MonoBehaviour controller, RectTransform city)
        {
            var data = new SerializedObject(controller);
            if (data.FindProperty("panelRect").objectReferenceValue != null) return;
            UnityAction fold, reset; UnityAction<int> toggle;
            if (controller is RailSystemTestPanel rails) { fold = rails.TogglePanel; reset = rails.ResetAll; toggle = rails.ToggleBuilding; }
            else if (controller is HouseSprinklerTestPanel fire) { fold = fire.TogglePanel; reset = fire.ResetAll; toggle = fire.ToggleBuilding; }
            else if (controller is HouseColumnsTestPanel elevator) { fold = elevator.TogglePanel; reset = elevator.ResetAll; toggle = elevator.ToggleBuilding; }
            else throw new InvalidOperationException("Unknown protection test controller.");
            var canvas = CreateCanvas(controller.GetType().Name + "_Test_UI", city);
            var panel = CreateRect("Panel", canvas.transform); panel.gameObject.AddComponent<Image>();
            var header = CreateButton("Collapse_Expand", panel, "", fold);
            Object.DestroyImmediate(header.GetComponentInChildren<TMP_Text>().gameObject);
            var title = CreateLabel("Heading", header.transform, "", 24f);
            var status = CreateLabel("Status", header.transform, "0 / 4 installed", 18f);
            var body = CreateRect("Body", panel);
            var rows = data.FindProperty("buildings");
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var label = CreateLabel("House_" + (i + 1), body, "House " + (i + 1), 22f);
                var button = CreateButton("Toggle_House_" + (i + 1), body, "ADD", null);
                UnityEventTools.AddIntPersistentListener(button.onClick, toggle, i);
                row.FindPropertyRelative("buildingLabel").objectReferenceValue = label;
                row.FindPropertyRelative("toggleButton").objectReferenceValue = button;
                row.FindPropertyRelative("buttonLabel").objectReferenceValue = button.GetComponentInChildren<TMP_Text>();
            }
            var remove = CreateButton("Reset_All", body, "REMOVE ALL", reset);
            Assign(data, "panelRect", panel); Assign(data, "panelBody", body.gameObject);
            Assign(data, "headerLabel", title); Assign(data, "statusLabel", status); Assign(data, "resetButton", remove);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureSequence(MonsterSequenceController sequence, RectTransform city, Button[] individual)
        {
            var data = new SerializedObject(sequence);
            var panel = data.FindProperty("panel").objectReferenceValue as RectTransform;
            if (panel == null)
            {
                var canvas = CreateCanvas("Monster_Sequence_Test_UI", city);
                Embed(canvas, city, 3, 18f, 80f);
                panel = CreateRect("Panel", canvas.transform); Top(panel, 0f, 80f);
                panel.gameObject.AddComponent<Image>().color = new Color(.045f, .07f, .085f, .96f);
                var menu = CreateButton("Choose_Sequence", panel, "SEQUENCE 1 - EARTHQUAKE  [+]", sequence.ToggleMenu);
                Top((RectTransform)menu.transform, 6f, 34f, 10f, 10f);
                var selection = menu.GetComponentInChildren<TMP_Text>(); selection.fontSize = 20f;
                var status = CreateLabel("Status", panel, "Choose a sequence, then press START", 18f);
                Top(status.rectTransform, 46f, 26f, 12f, 186f);
                var start = CreateButton("Start_Sequence", panel, "START", sequence.StartSelectedSequence);
                var stop = CreateButton("Stop_Sequence", panel, "STOP", sequence.StopSequence);
                Right((RectTransform)start.transform, 102f, 46f, 78f, 28f);
                Right((RectTransform)stop.transform, 12f, 46f, 78f, 28f);
                stop.interactable = false;
                var choices = CreateRect("Choices", panel); Top(choices, 82f, 132f, 10f, 10f);
                var titles = new[] { "1 - EARTHQUAKE", "2 - TSUNAMI + EARTHQUAKE", "3 - TSUNAMI + EARTHQUAKE + FIRE" };
                var buttons = new Button[3];
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i] = CreateButton("Sequence_" + (i + 1), choices, titles[i], null);
                    Top((RectTransform)buttons[i].transform, i * 44f, 38f);
                    UnityEventTools.AddIntPersistentListener(buttons[i].onClick, sequence.SelectSequence, i + 1);
                    buttons[i].GetComponentInChildren<TMP_Text>().fontSize = 21f;
                }
                choices.gameObject.SetActive(false);
                Assign(data, "panel", panel); Assign(data, "choices", choices.gameObject);
                Assign(data, "menuButton", menu); Assign(data, "startButton", start); Assign(data, "stopButton", stop);
                Assign(data, "selectionLabel", selection); Assign(data, "statusLabel", status);
                AssignArray(data, "sequenceButtons", buttons);
            }
            AssignArray(data, "individualTestButtons", individual);
            data.ApplyModifiedPropertiesWithoutUndo();
            panel.GetComponentInParent<Canvas>(true).gameObject.SetActive(true);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        private static Canvas CreateCanvas(string name, RectTransform parent)
        {
            var rect = CreateRect(name, parent);
            var canvas = rect.gameObject.AddComponent<Canvas>();
            rect.gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static TMP_Text CreateLabel(string name, Transform parent, string value, float size)
        {
            var label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
            label.text = value; label.fontSize = size; label.color = new Color(.92f, .95f, .98f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        private static Button CreateButton(string name, Transform parent, string value, UnityAction action)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.22f, .32f, .37f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var label = CreateLabel("Label", rect, value, 22f); label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static void Assign(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;

        private static void AssignArray(SerializedObject data, string name, Button[] values)
        {
            var array = data.FindProperty(name); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void Right(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(-right, -top);
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
            var heading = panel.Find("Heading").GetComponent<TMP_Text>();
            heading.text = title; heading.fontSize = 28; heading.alignment = TextAlignmentOptions.TopLeft;
            heading.color = new Color(0.96f, 0.72f, 0.35f);
            Top(heading.rectTransform, 12f, 34f, 16f, 212f);
            var status = (TMP_Text)data.FindProperty("statusLabel").objectReferenceValue;
            status.text = "Ready"; status.fontSize = 21; status.alignment = TextAlignmentOptions.TopLeft;
            Top(status.rectTransform, 50f, 30f, 16f, 212f);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(86f, 64f); rect.anchoredPosition = new Vector2(-108f, -48f);
            rect.localScale = Vector3.one;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = "TEST"; label.fontSize = 26; label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
            var stop = panel.Find("Stop_Test") as RectTransform;
            if (stop != null) Right(stop, 16f, 16f, 80f, 64f);
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
            var heading = (TMP_Text)data.FindProperty("headerLabel").objectReferenceValue;
            heading.text = title + "  [+]"; heading.fontSize = 24;
            Top(heading.rectTransform, 6f, 28f, 16f, 16f);
            var status = (TMP_Text)data.FindProperty("statusLabel").objectReferenceValue;
            status.text = "0 / 4 installed"; status.fontSize = 18;
            Top(status.rectTransform, 35f, 20f, 16f, 16f);
            Top((RectTransform)body.transform, 60f, 194f);
            var rows = data.FindProperty("buildings");
            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var name = (TMP_Text)row.FindPropertyRelative("buildingLabel").objectReferenceValue;
                var button = (Button)row.FindPropertyRelative("toggleButton").objectReferenceValue;
                var action = (TMP_Text)row.FindPropertyRelative("buttonLabel").objectReferenceValue;
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
            var resetLabel = reset.GetComponentInChildren<TMP_Text>(true);
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
            rect.anchorMin = new Vector2(column / 4f, 1f); rect.anchorMax = new Vector2((column + 1f) / 4f, 1f);
            rect.pivot = new Vector2(0.5f, 1f); rect.sizeDelta = new Vector2(-24f, height);
            rect.anchoredPosition = new Vector2(0f, -top); rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = city.gameObject.layer;
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                text.raycastTarget = false;
                text.enableAutoSizing = false;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Truncate;
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
