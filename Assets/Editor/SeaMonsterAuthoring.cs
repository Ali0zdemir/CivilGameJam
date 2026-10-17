using System;
using System.Linq;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Authors the monster and the sea into the scene, before entering Play Mode.</summary>
public static class SeaMonsterAuthoring
{
    [MenuItem("Tools/Disasters/Save Monster After Event Point In Both Scenes")]
    public static void SaveAfterEventPointForBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author the route in Edit Mode.");
        var sourcePosition = Vector3.zero;
        var copiedSource = false;
        foreach (var path in new[] { "Assets/Scenes/BuildingShowcase.unity", "Assets/2DCardScene.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var parts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var tsunami = parts.Select(part => part.GetComponent<TsunamiTestController>()).Single(part => part != null);
                var target = parts.SingleOrDefault(part => part.name.Equals("monsterWalkAfterEventPoint", StringComparison.OrdinalIgnoreCase));
                if (!copiedSource)
                {
                    if (target == null) throw new InvalidOperationException("The authored monsterWalkAfterEventPoint is missing from BuildingShowcase.");
                    sourcePosition = tsunami.transform.InverseTransformPoint(target.position);
                    copiedSource = true;
                }
                if (target == null)
                {
                    target = new GameObject("monsterWalkAfterEventPoint").transform;
                    target.SetParent(tsunami.transform, false);
                    target.localPosition = sourcePosition;
                }
                var walk = tsunami.GetComponentInChildren<SeaMonsterWalk>();
                var settings = new SerializedObject(walk);
                settings.FindProperty("afterEventPoint").objectReferenceValue = target;
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
                Debug.Log(path + ": after-event target=" + target.position.ToString("F4"));
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Disasters/Save Monster Walk Point In Both Scenes")]
    public static void SaveWalkPointForBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author the route in Edit Mode.");
        var sourcePoint = Vector3.zero;
        var copiedSource = false;
        foreach (var path in new[] { "Assets/Scenes/BuildingShowcase.unity", "Assets/2DCardScene.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var tsunami = components.Select(part => part.GetComponent<TsunamiTestController>()).Single(part => part != null);
                var target = components.SingleOrDefault(part => part.name.Equals("monsterWalkPoint", StringComparison.OrdinalIgnoreCase));
                if (!copiedSource)
                {
                    if (target == null) throw new InvalidOperationException("The authored monsterWalkPoint is missing from BuildingShowcase.");
                    sourcePoint = tsunami.transform.InverseTransformPoint(target.position); copiedSource = true;
                }
                if (target == null)
                {
                    target = new GameObject("monsterWalkPoint").transform;
                    target.SetParent(tsunami.transform, false); target.localPosition = sourcePoint;
                }
                var monster = tsunami.GetComponentInChildren<SeaMonsterEmergence>();
                var bounds = tsunami.CityBounds;
                monster.transform.localPosition = new Vector3(bounds.center.x, tsunami.SeaLevel - 0.15f, bounds.min.z - 2.8f);
                var settings = new SerializedObject(tsunami);
                settings.FindProperty("startDistance").floatValue = bounds.min.z - 2.45f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                settings = new SerializedObject(monster.GetComponent<SeaMonsterWalk>());
                settings.FindProperty("walkPoint").objectReferenceValue = target; settings.ApplyModifiedPropertiesWithoutUndo();
                ConfigureFirePose(monster.GetComponent<SeaMonsterFireBreath>(), monster.Model);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
                Debug.Log(path + ": emergence=" + monster.transform.position.ToString("F4") + ", walk target=" + target.position.ToString("F4"));
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Disasters/Save Sea Monster In Both Scenes")]
    public static void SaveForBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author the sea monster in Edit Mode.");
        var previousScene = SceneManager.GetActiveScene().path;
        foreach (var path in new[] { "Assets/Scenes/BuildingShowcase.unity", "Assets/2DCardScene.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var tsunami = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TsunamiTestController>(true)).Single();
                Configure(tsunami);
                ConfigureEarthquake(scene, tsunami);
                ConfigureFire(scene, tsunami);
                ConfigureEventCameras(scene);
                FrameCityAndMonster(scene, tsunami);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        var previous = SceneManager.GetSceneByPath(previousScene);
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        if (monster != null) Selection.activeGameObject = monster.gameObject;
        Debug.Log("Saved the cube sea monster, foreground sea and reversed tsunami in both scenes.");
    }

    [MenuItem("Tools/Disasters/Save Monster Fire In Both Scenes")]
    public static void SaveFireForBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author the monster fire in Edit Mode.");
        foreach (var path in new[] { "Assets/Scenes/BuildingShowcase.unity", "Assets/2DCardScene.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var tsunami = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TsunamiTestController>(true)).Single();
                ConfigureFire(scene, tsunami);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Saved the monster mouth, fire animation references and fire test in both scenes.");
    }

    private static void ConfigureFire(Scene scene, TsunamiTestController tsunami)
    {
        var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        var fire = components.OfType<FireTestController>().Single();
        var earthquake = components.OfType<EarthquakeTestController>().Single();
        var monster = components.OfType<SeaMonsterEmergence>().Single();
        var model = monster.Model;
        var muzzle = model.Find("Muzzle");
        var mouth = muzzle.Find("Mouth_Opening");
        if (mouth == null) mouth = Cube("Mouth_Opening", muzzle, new Vector3(0f, -0.05f, 0.51f),
            new Vector3(0.57f, 0.46f, 0.025f), Material("Monster_Mouth", new Color(0.055f, 0.025f, 0.018f)));
        var origin = muzzle.Find("Flame_Origin");
        if (origin == null)
        {
            origin = new GameObject("Flame_Origin").transform;
            origin.SetParent(muzzle, false);
            origin.localPosition = new Vector3(0f, -0.05f, 0.545f);
        }
        var breath = monster.GetComponent<SeaMonsterFireBreath>();
        if (breath == null) breath = monster.gameObject.AddComponent<SeaMonsterFireBreath>();
        var settings = new SerializedObject(breath);
        settings.FindProperty("walk").objectReferenceValue = monster.GetComponent<SeaMonsterWalk>();
        settings.FindProperty("model").objectReferenceValue = model;
        settings.FindProperty("mouth").objectReferenceValue = origin;
        var upper = new[] { "Body", "Belly", "Head", "Muzzle", "Left_Eye", "Right_Eye", "Left_Arm", "Right_Arm" };
        var upperProperty = settings.FindProperty("upperBody"); upperProperty.arraySize = upper.Length;
        for (var i = 0; i < upper.Length; i++) upperProperty.GetArrayElementAtIndex(i).objectReferenceValue = model.Find(upper[i]);
        var skin = model.GetComponentsInChildren<MeshRenderer>().Where(renderer => !renderer.name.Contains("Eye") && renderer.transform != mouth).ToArray();
        var skinProperty = settings.FindProperty("skin"); skinProperty.arraySize = skin.Length;
        for (var i = 0; i < skin.Length; i++) skinProperty.GetArrayElementAtIndex(i).objectReferenceValue = skin[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
        ConfigureFirePose(breath, model);
        settings = new SerializedObject(fire);
        settings.FindProperty("seaMonsterFire").objectReferenceValue = breath;
        settings.FindProperty("earthquake").objectReferenceValue = earthquake;
        settings.FindProperty("tsunami").objectReferenceValue = tsunami;
        settings.FindProperty("flameWidth").floatValue = 0.6f;
        settings.FindProperty("flameRate").floatValue = 32f;
        settings.FindProperty("spreadTime").floatValue = 4.2f;
        settings.FindProperty("smokeAmount").floatValue = 0.75f;
        settings.FindProperty("breathHalfAngle").floatValue = 28f;
        settings.FindProperty("softFireLeadTime").floatValue = 4.2f;
        settings.FindProperty("softFireMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/Fire/SoftStreetFire.mat");
        settings.FindProperty("gasFireMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Effects/Fire/GasFlame.mat");
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach (var controller in new MonoBehaviour[] { earthquake, tsunami })
        {
            settings = new SerializedObject(controller);
            settings.FindProperty("fire").objectReferenceValue = fire;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureFirePose(SeaMonsterFireBreath breath, Transform model)
    {
        var settings = new SerializedObject(breath);
        var names = new[] { "Head", "Muzzle", "Left_Eye", "Right_Eye" };
        var parts = settings.FindProperty("headParts"); parts.arraySize = names.Length;
        for (var i = 0; i < names.Length; i++) parts.GetArrayElementAtIndex(i).objectReferenceValue = model.Find(names[i]);
        settings.FindProperty("breathDownAngle").floatValue = 47.5f;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Configure(TsunamiTestController tsunami)
    {
        var root = tsunami.transform;
        root.localRotation = Quaternion.identity;
        var data = new SerializedObject(tsunami);
        var ground = ((Transform)data.FindProperty("groundSurface").objectReferenceValue).GetComponent<Renderer>().bounds;
        var bounds = new Bounds(root.InverseTransformPoint(ground.center), Vector3.zero);
        for (var corner = 0; corner < 8; corner++)
            bounds.Encapsulate(root.InverseTransformPoint(ground.center + Vector3.Scale(ground.extents,
                new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
        data.FindProperty("shorelineDistance").floatValue = bounds.min.z;
        data.FindProperty("startDistance").floatValue = bounds.min.z - 2.45f;
        data.FindProperty("waveSpeed").floatValue = 4.5f;
        var body = Material("Monster_Body", new Color(0.16f, 0.28f, 0.23f));
        var belly = Material("Monster_Belly", new Color(0.4f, 0.5f, 0.34f));
        var eyes = Material("Monster_Eyes", new Color(1f, 0.68f, 0.17f));
        var water = Material("Sea_Surface", new Color(0.06f, 0.3f, 0.4f));

        var monster = root.Find("Sea_Monster");
        if (monster == null)
        {
            monster = new GameObject("Sea_Monster").transform;
            monster.SetParent(root, false);
            var model = new GameObject("Cube_Model").transform;
            model.SetParent(monster, false);
            Cube("Body", model, new Vector3(0f, 1.85f, 0f), new Vector3(1.5f, 1.6f, 0.95f), body);
            Cube("Belly", model, new Vector3(0f, 1.8f, 0.49f), new Vector3(0.95f, 1.15f, 0.09f), belly);
            Cube("Head", model, new Vector3(0f, 3.04f, 0.14f), new Vector3(1.35f, 0.9f, 1.05f), body);
            Cube("Muzzle", model, new Vector3(0f, 2.89f, 0.7f), new Vector3(1.05f, 0.34f, 0.48f), belly);
            foreach (var side in new[] { -1f, 1f })
            {
                var label = side < 0f ? "Left" : "Right";
                Cube(label + "_Leg", model, new Vector3(side * 0.47f, 0.65f, 0f), new Vector3(0.58f, 1.3f, 0.68f), body);
                Cube(label + "_Foot", model, new Vector3(side * 0.47f, 0.14f, 0.22f), new Vector3(0.68f, 0.28f, 1f), body);
                Cube(label + "_Arm", model, new Vector3(side * 0.99f, 1.85f, 0.12f), new Vector3(0.48f, 1.22f, 0.54f), body);
                Cube(label + "_Eye", model, new Vector3(side * 0.38f, 3.17f, 0.68f), new Vector3(0.23f, 0.2f, 0.08f), eyes);
            }
            var emergence = monster.gameObject.AddComponent<SeaMonsterEmergence>();
            var settings = new SerializedObject(emergence);
            settings.FindProperty("model").objectReferenceValue = model;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        monster.localPosition = new Vector3(bounds.center.x, tsunami.SeaLevel - 0.15f,
            data.FindProperty("startDistance").floatValue - 0.35f);
        monster.localRotation = Quaternion.identity;
        var emergenceSettings = new SerializedObject(monster.GetComponent<SeaMonsterEmergence>());
        emergenceSettings.FindProperty("waveReleaseFraction").floatValue = 0.5f;
        emergenceSettings.ApplyModifiedPropertiesWithoutUndo();
        data.FindProperty("seaMonster").objectReferenceValue = monster.GetComponent<SeaMonsterEmergence>();
        data.ApplyModifiedPropertiesWithoutUndo();

        var sea = root.Find("Sea_Surface");
        if (sea == null) sea = Cube("Sea_Surface", root, Vector3.zero, Vector3.one, water);
        sea.localPosition = new Vector3(bounds.center.x, tsunami.SeaLevel - 0.04f, bounds.min.z - 7f);
        sea.localScale = new Vector3(Mathf.Max(18f, bounds.size.x + 6f), 0.08f, 14f);
    }

    private static void ConfigureEarthquake(Scene scene, TsunamiTestController tsunami)
    {
        var earthquake = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EarthquakeTestController>(true)).Single();
        var monster = tsunami.transform.Find("Sea_Monster");
        var model = monster.Find("Cube_Model");
        var walk = monster.GetComponent<SeaMonsterWalk>();
        if (walk == null) walk = monster.gameObject.AddComponent<SeaMonsterWalk>();
        var settings = new SerializedObject(walk);
        settings.FindProperty("emergence").objectReferenceValue = monster.GetComponent<SeaMonsterEmergence>();
        settings.FindProperty("tsunami").objectReferenceValue = tsunami;
        settings.FindProperty("walkPoint").objectReferenceValue = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .SingleOrDefault(part => part.name.Equals("monsterWalkPoint", StringComparison.OrdinalIgnoreCase));
        settings.FindProperty("stepDuration").floatValue = 1.3f;
        foreach (var side in new[] { "left", "right" })
            foreach (var part in new[] { "Leg", "Foot", "Arm" })
                settings.FindProperty(side + part).objectReferenceValue = model.Find(char.ToUpperInvariant(side[0]) + side.Substring(1) + "_" + part);
        settings.ApplyModifiedPropertiesWithoutUndo();
        settings = new SerializedObject(earthquake);
        settings.FindProperty("seaMonsterWalk").objectReferenceValue = walk;
        settings.FindProperty("afterLastStepDelay").floatValue = 2f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        settings = new SerializedObject(tsunami);
        settings.FindProperty("earthquake").objectReferenceValue = earthquake;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FrameCityAndMonster(Scene scene, TsunamiTestController tsunami)
    {
        var navigation = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CivilGameJam.CityCameraController>(true)).Single();
        var settings = new SerializedObject(navigation);
        var camera = (Camera)settings.FindProperty("controlledCamera").objectReferenceValue;
        var rig = (Transform)settings.FindProperty("panRig").objectReferenceValue;
        var up = camera.transform.up;
        var right = camera.transform.right;
        var minimum = new Vector2(float.MaxValue, float.MaxValue);
        var maximum = new Vector2(float.MinValue, float.MinValue);
        void Include(Renderer renderer)
        {
            var bounds = renderer.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                var projected = new Vector2(Vector3.Dot(point, right), Vector3.Dot(point, up));
                minimum = Vector2.Min(minimum, projected);
                maximum = Vector2.Max(maximum, projected);
            }
        }
        var tsunamiSettings = new SerializedObject(tsunami);
        var buildings = (Transform)tsunamiSettings.FindProperty("buildingsRoot").objectReferenceValue;
        foreach (var renderer in buildings.GetComponentsInChildren<Renderer>()) Include(renderer);
        foreach (var renderer in tsunami.GetComponentInChildren<SeaMonsterEmergence>().Model.GetComponentsInChildren<Renderer>()) Include(renderer);
        Include(((Transform)tsunamiSettings.FindProperty("groundSurface").objectReferenceValue).GetComponent<Renderer>());
        var aspect = Mathf.Max(0.1f, Mathf.Min(camera.aspect, settings.FindProperty("viewportReferenceAspect").floatValue));
        // Leave the upper fifth clear for the saved disaster controls and a small margin below the feet.
        var size = Mathf.Max(camera.orthographicSize, (maximum.y - minimum.y) / (2f * 0.74f),
            (maximum.x - minimum.x) / (2f * aspect * 0.88f));
        var center = (minimum + maximum) * 0.5f;
        rig.position += right * (center.x - Vector3.Dot(camera.transform.position, right)) +
            up * (center.y + size * 0.14f - Vector3.Dot(camera.transform.position, up));
        camera.orthographicSize = size;
    }

    private static void ConfigureEventCameras(Scene scene)
    {
        var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        var navigation = components.OfType<CivilGameJam.CityCameraController>().Single();
        foreach (var controller in components.Where(component => component is TsunamiTestController ||
            component is EarthquakeTestController || component is FireTestController))
        {
            var settings = new SerializedObject(controller);
            settings.FindProperty("navigation").objectReferenceValue = navigation;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Transform Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cube.transform;
    }

    private static Material Material(string name, Color color)
    {
        const string folder = "Assets/Materials/SeaMonster";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials", "SeaMonster");
        var path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.18f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
