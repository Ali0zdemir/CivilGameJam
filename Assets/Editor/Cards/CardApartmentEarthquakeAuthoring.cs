using System;
using System.Collections.Generic;
using System.Linq;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardApartmentEarthquakeAuthoring
    {
        private const string ClipsFolder = "Assets/Art/Buildings/Animations/";

        [MenuItem("Tools/Card Game/Save Authored Apartment Earthquake Animations")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author earthquake motion in Edit Mode.");
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Buildings/Prefabs/House.prefab");
            var motion = reference.transform.Find("Apartment_Motion");
            var normal = motion.Find("Apartments_Normal");
            var elevated = motion.Find("Apartments_Elevated");
            var collapse = Bake("Apartment(Default)(Earthquake)", normal, "", "", "Apartment_Earthquake_Collapse");
            var elevatedCollapse = Bake("Apartment(Default)(Earthquake)", elevated, "", "(Elevated)", "Apartment_Earthquake_Elevated_Collapse");
            var normalSeismic = Bake("Apartment(Default)(Seismic)", normal, "(Default)(Seismic)", "", "Apartment_Seismic_Default");
            var elevatedSeismic = Bake("Apartment(Elevated)(Seismic)", elevated, "(Elevated)(Seismic)", "(Elevated)", "Apartment_Seismic_Elevated");
            foreach (var name in new[] { "House", "Apartment_02", "Apartment_03", "Apartment_04" })
            {
                var path = "Assets/Art/Buildings/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animation = root.GetComponent<ApartmentEarthquakeAnimation>() ?? root.AddComponent<ApartmentEarthquakeAnimation>();
                    var data = new SerializedObject(animation);
                    data.FindProperty("apartment").objectReferenceValue = root.GetComponent<ApartmentUpgradeModel>();
                    data.FindProperty("tsunamiCollapse").objectReferenceValue = root.GetComponent<ApartmentTsunamiCollapse>();
                    ConfigureVariant(data.FindProperty("normal"), root.transform, false, collapse, normalSeismic);
                    ConfigureVariant(data.FindProperty("elevated"), root.transform, true, elevatedCollapse, elevatedSeismic);
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var roots = scene.GetRootGameObjects();
            var earthquake = roots.SelectMany(root => root.GetComponentsInChildren<EarthquakeTestController>(true)).Single();
            var buildings = roots.SelectMany(root => root.GetComponentsInChildren<HouseCityUpgradeLink>(true))
                .OrderBy(link => link.Building.name).Select(link => link.Building.GetComponent<ApartmentEarthquakeAnimation>()).ToArray();
            if (buildings.Length != 4 || buildings.Any(item => item == null)) throw new InvalidOperationException("Expected the four saved city houses.");
            var settings = new SerializedObject(earthquake);
            var animations = settings.FindProperty("buildingAnimations"); animations.arraySize = buildings.Length;
            for (var i = 0; i < buildings.Length; i++) animations.GetArrayElementAtIndex(i).objectReferenceValue = buildings[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            var walk = roots.SelectMany(root => root.GetComponentsInChildren<SeaMonsterWalk>(true)).Single();
            settings = new SerializedObject(walk);
            settings.FindProperty("hideAtAfterEventPoint").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save 2DCardScene.");
            AssetDatabase.SaveAssets();
            Debug.Log("Saved all three authored earthquake motions and monster departure in 2DCardScene.");
        }

        private static void ConfigureVariant(SerializedProperty variant, Transform building, bool elevated, AnimationClip collapse, AnimationClip seismic)
        {
            var root = building.Find("Apartment_Motion/" + (elevated ? "Apartments_Elevated" : "Apartments_Normal"));
            variant.FindPropertyRelative("root").objectReferenceValue = root;
            variant.FindPropertyRelative("collapse").objectReferenceValue = collapse;
            variant.FindPropertyRelative("seismic").objectReferenceValue = seismic;
            var parts = variant.FindPropertyRelative("parts"); parts.arraySize = 6;
            for (var i = 0; i < 6; i++)
            {
                var part = parts.GetArrayElementAtIndex(i);
                var body = root.Find("Apartment" + (i + 1) + (elevated ? "(Elevated)" : ""));
                part.FindPropertyRelative("body").objectReferenceValue = body;
                var targets = new List<Transform>
                {
                    building.Find("Apartment_Motion/Apartment_Surface_" + (i + 1)),
                    root.Find("Sprinkler" + (i + 1)), root.Find("Sprinkler" + (i + 1) + "(Base)")
                };
                if (elevated) targets.Add(root.Find("Elevator_Supports/Apartment" + (i + 1) + "_Supports"));
                if (body == null || targets.Any(target => target == null)) throw new InvalidOperationException("Missing earthquake target on " + building.name);
                var attachments = part.FindPropertyRelative("attachments"); attachments.arraySize = targets.Count;
                for (var j = 0; j < targets.Count; j++) attachments.GetArrayElementAtIndex(j).objectReferenceValue = targets[j];
            }
        }

        private static AnimationClip Bake(string file, Transform targetRoot, string sourceSuffix, string targetSuffix, string name)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/emreAnimation/" + file + ".fbx").OfType<AnimationClip>()
                .Single(clip => !clip.name.StartsWith("__preview"));
            // These transform clips are sampled directly, without an Animator, in standalone players too.
            var result = new AnimationClip { name = name, legacy = true, frameRate = source.frameRate, wrapMode = WrapMode.ClampForever };
            var bindings = AnimationUtility.GetCurveBindings(source);
            var rotations = SampleRotations(source, file, targetRoot, sourceSuffix, targetSuffix);
            // The original ground and isolators remain fixed; transfer the six animated buildings.
            for (var i = 1; i <= 6; i++)
            {
                var target = targetRoot.Find("Apartment" + i + targetSuffix);
                var path = "Apartment" + i + sourceSuffix;
                var curves = bindings.Where(binding => binding.path == path && binding.type == typeof(Transform))
                    .ToDictionary(binding => binding.propertyName, binding => AnimationUtility.GetEditorCurve(source, binding));
                if (curves.Count != 10 || target == null) throw new InvalidOperationException("Incomplete source animation: " + file + "/" + path);
                foreach (var property in new[] { "m_LocalPosition", "m_LocalScale" })
                    for (var axis = 0; axis < 3; axis++)
                    {
                        var key = property + "." + "xyz"[axis];
                        var curve = curves[key]; var keys = curve.keys;
                        var start = curve.Evaluate(0f);
                        var rest = property == "m_LocalPosition" ? target.localPosition[axis] : target.localScale[axis];
                        var factor = property == "m_LocalScale" && Mathf.Abs(start) > .0001f ? rest / start : 1f;
                        for (var k = 0; k < keys.Length; k++)
                        {
                            keys[k].value = property == "m_LocalPosition" ? keys[k].value + rest - start : keys[k].value * factor;
                            keys[k].inTangent *= factor; keys[k].outTangent *= factor;
                        }
                        AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve(target.name, typeof(Transform), key),
                            new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode });
                    }
                for (var axis = 0; axis < 4; axis++)
                {
                    var key = "m_LocalRotation." + "xyzw"[axis];
                    AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve(target.name, typeof(Transform), key), rotations[i - 1][axis]);
                }
            }
            result.EnsureQuaternionContinuity();
            var assetPath = ClipsFolder + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing == null) { AssetDatabase.CreateAsset(result, assetPath); return result; }
            EditorUtility.CopySerialized(result, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(result);
            return existing;
        }

        private static AnimationCurve[][] SampleRotations(AnimationClip clip, string file, Transform targetRoot, string sourceSuffix, string targetSuffix)
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/emreAnimation/" + file + ".fbx"));
            try
            {
                clip.SampleAnimation(instance, 0f);
                var bodies = Enumerable.Range(1, 6).Select(i => instance.transform.Find("Apartment" + i + sourceSuffix)).ToArray();
                var corrections = Enumerable.Range(0, 6).Select(i => targetRoot.Find("Apartment" + (i + 1) + targetSuffix).localRotation *
                    Quaternion.Inverse(bodies[i].localRotation)).ToArray();
                var frames = Mathf.CeilToInt(clip.length * clip.frameRate * 4f);
                var keys = Enumerable.Range(0, 6).Select(i => Enumerable.Range(0, 4).Select(axis => new Keyframe[frames + 1]).ToArray()).ToArray();
                var previous = new Quaternion[6];
                // FBX rotation interpolation differs from the exposed scalar curves; sample the actual authored pose.
                for (var frame = 0; frame <= frames; frame++)
                {
                    var time = clip.length * frame / frames;
                    clip.SampleAnimation(instance, time);
                    for (var i = 0; i < 6; i++)
                    {
                        var rotation = corrections[i] * bodies[i].localRotation;
                        if (frame > 0 && Quaternion.Dot(previous[i], rotation) < 0f)
                            rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                        previous[i] = rotation;
                        for (var axis = 0; axis < 4; axis++) keys[i][axis][frame] = new Keyframe(time, rotation[axis]);
                    }
                }
                return keys.Select(body => body.Select(channel =>
                {
                    var constant = channel.All(key => key.value == channel[0].value);
                    var curve = new AnimationCurve(constant ? new[] { channel[0], channel[channel.Length - 1] } : channel);
                    for (var frame = 0; frame < curve.length; frame++)
                    {
                        AnimationUtility.SetKeyBroken(curve, frame, true);
                        AnimationUtility.SetKeyLeftTangentMode(curve, frame, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, frame, AnimationUtility.TangentMode.Linear);
                    }
                    return curve;
                }).ToArray()).ToArray();
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
