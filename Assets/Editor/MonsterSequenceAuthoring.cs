using System;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MonsterSequenceAuthoring
{
    [MenuItem("Tools/Disasters/Save Sequence City Camera In Both Scenes")]
    public static void SaveCameraForBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author the sequence camera in Edit Mode.");
        foreach (var path in new[] { "Assets/Scenes/BuildingShowcase.unity", "Assets/2DCardScene.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var sequence = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonsterSequenceController>(true)).Single();
                ConfigureCamera(sequence);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
                Debug.Log("Saved smooth city zoom and return for all three sequences in " + path);
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureCamera(MonsterSequenceController sequence)
    {
        var data = new SerializedObject(sequence);
        data.FindProperty("cameraZoomFactor").floatValue = 1.3f;
        data.FindProperty("cameraZoomSeconds").floatValue = 1.8f;
        data.FindProperty("cameraReturnSeconds").floatValue = 1.8f;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Disasters/Save Monster Sequence Menu In Both Scenes")]
    public static void SaveForBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author the sequence menu in Edit Mode.");
        foreach (var path in new[] { "Assets/Scenes/BuildingShowcase.unity", "Assets/2DCardScene.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Configure(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
                Debug.Log("Saved smaller monster and sequence menu in " + path);
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
    }

    private static void Configure(Scene scene)
    {
        var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        var monster = components.OfType<SeaMonsterEmergence>().Single();
        var tsunami = components.OfType<TsunamiTestController>().Single();
        var earthquake = components.OfType<EarthquakeTestController>().Single();
        var fire = components.OfType<FireTestController>().Single();
        monster.Model.localScale = Vector3.one * 0.51f;
        var emergence = new SerializedObject(monster);
        emergence.FindProperty("submergedDepth").floatValue = 3.213f;
        emergence.ApplyModifiedPropertiesWithoutUndo();
        var fireSettings = new SerializedObject(fire);
        fireSettings.FindProperty("smokeAmount").floatValue = 0.75f;
        fireSettings.ApplyModifiedPropertiesWithoutUndo();
        var sequence = monster.GetComponent<MonsterSequenceController>();
        if (sequence == null) sequence = monster.gameObject.AddComponent<MonsterSequenceController>();
        ConfigureCamera(sequence);
        var buttons = new MonoBehaviour[] { earthquake, fire, tsunami }.Select(controller =>
            (Button)new SerializedObject(controller).FindProperty("testButton").objectReferenceValue).ToArray();
        var font = buttons[0].GetComponentInChildren<Text>(true).font;
        var viewport = components.OfType<CityViewport>().SingleOrDefault();
        Transform parent;
        if (viewport != null) parent = viewport.ViewRect;
        else
        {
            var canvas = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "Monster_Sequence_UI");
            if (canvas == null)
            {
                canvas = new GameObject("Monster_Sequence_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvas, scene);
            }
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 40;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            parent = canvas.transform;
        }
        var existing = parent.Find("Monster_Sequence_Menu");
        if (existing != null) return;
        var panel = UI("Monster_Sequence_Menu", parent);
        panel.gameObject.AddComponent<Image>().color = new Color(0.045f, 0.07f, 0.085f, 0.96f);
        panel.anchorMin = viewport != null ? Vector2.zero : Vector2.up;
        panel.anchorMax = viewport != null ? Vector2.right : Vector2.up;
        panel.pivot = viewport != null ? Vector2.zero : Vector2.up;
        panel.sizeDelta = new Vector2(viewport != null ? -24f : 800f, 80f);
        panel.anchoredPosition = new Vector2(viewport != null ? 12f : 24f, viewport != null ? 12f : -450f);
        var menu = Button("Choose_Sequence", panel, font, "SEQUENCE 1 - EARTHQUAKE  [+]", sequence.ToggleMenu);
        Top((RectTransform)menu.transform, 8f, 38f, 12f, 268f);
        menu.GetComponent<Image>().color = new Color(0.12f, 0.19f, 0.21f);
        var start = Button("Start_Sequence", panel, font, "START", sequence.StartSelectedSequence);
        Right((RectTransform)start.transform, 8f, 132f, 112f, 38f);
        start.GetComponent<Image>().color = new Color(0.24f, 0.43f, 0.35f);
        var stop = Button("Stop_Sequence", panel, font, "STOP", sequence.StopSequence);
        Right((RectTransform)stop.transform, 8f, 12f, 108f, 38f);
        stop.GetComponent<Image>().color = new Color(0.46f, 0.24f, 0.19f);
        stop.interactable = false;
        var status = Label("Status", panel, font, "Choose a sequence, then press START", 18);
        Top(status.rectTransform, 50f, 24f, 16f, 16f);
        status.alignment = TextAnchor.MiddleLeft;
        var choices = UI("Sequence_Choices", panel);
        Top(choices, 86f, 124f, 12f, 12f);
        var options = new[] {
            "1   EMERGE > EARTHQUAKE AT POINT 1 > WALK TO POINT 2",
            "2   + TSUNAMI > EARTHQUAKE AT POINT 1 > WALK TO POINT 2",
            "3   + TSUNAMI > EARTHQUAKE > FIRE AT POINT 1 > WALK TO POINT 2" };
        var optionButtons = new Button[3];
        for (var i = 0; i < options.Length; i++)
        {
            var button = Button("Sequence_" + (i + 1), choices, font, options[i], null);
            Top((RectTransform)button.transform, i * 42f, 38f);
            UnityEventTools.AddIntPersistentListener(button.onClick, sequence.SelectSequence, i + 1);
            optionButtons[i] = button;
        }
        choices.gameObject.SetActive(false);
        var data = new SerializedObject(sequence);
        Assign(data, "monster", monster); Assign(data, "walk", monster.GetComponent<SeaMonsterWalk>());
        Assign(data, "tsunami", tsunami); Assign(data, "earthquake", earthquake); Assign(data, "fire", fire);
        Assign(data, "navigation", components.OfType<CityCameraController>().Single());
        Assign(data, "panel", panel); Assign(data, "choices", choices.gameObject);
        Assign(data, "menuButton", menu); Assign(data, "startButton", start); Assign(data, "stopButton", stop);
        Assign(data, "selectionLabel", menu.GetComponentInChildren<Text>()); Assign(data, "statusLabel", status);
        AssignButtons(data, "sequenceButtons", optionButtons); AssignButtons(data, "individualTestButtons", buttons);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Assign(SerializedObject data, string name, UnityEngine.Object value) => data.FindProperty(name).objectReferenceValue = value;

    private static void AssignButtons(SerializedObject data, string name, Button[] buttons)
    {
        var property = data.FindProperty(name); property.arraySize = buttons.Length;
        for (var i = 0; i < buttons.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
    }

    private static RectTransform UI(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        return rect;
    }

    private static Text Label(string name, Transform parent, Font font, string value, int size)
    {
        var rect = UI(name, parent);
        var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.text = value;
        text.fontSize = size; text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size;
        text.alignment = TextAnchor.MiddleCenter; text.color = new Color(0.92f, 0.94f, 0.9f);
        text.raycastTarget = false;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 0f); rect.offsetMax = new Vector2(-10f, 0f);
        return text;
    }

    private static Button Button(string name, Transform parent, Font font, string label, UnityAction action)
    {
        var rect = UI(name, parent);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.16f, 0.25f, 0.28f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
        Label("Label", rect, font, label, 20);
        if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
        return button;
    }

    private static void Top(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
    {
        rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-left - right, height);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
    }

    private static void Right(RectTransform rect, float top, float right, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(-right, -top);
    }
}
