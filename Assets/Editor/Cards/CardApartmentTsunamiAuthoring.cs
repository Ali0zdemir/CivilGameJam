using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardApartmentTsunamiAuthoring
    {
        private const string AnimationPath = "Assets/EfeArts/EmreArts/Animasyon/Apartment(Tsunami).fbx";
        private const string ClipPath = "Assets/Art/Buildings/Animations/Apartment_Tsunami_Collapse.anim";

        [MenuItem("Tools/Card Game/Save Authored Apartment Tsunami Animation")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save tsunami animation in Edit Mode.");
            if (!AssetDatabase.IsValidFolder("Assets/Art/Buildings/Animations")) AssetDatabase.CreateFolder("Assets/Art/Buildings", "Animations");
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Buildings/Prefabs/House.prefab");
            var clip = BakeClip(reference.transform.Find("Apartment_Motion/Apartments_Normal"));
            foreach (string name in new[] { "House", "Apartment_02", "Apartment_03", "Apartment_04" })
            {
                string path = "Assets/Art/Buildings/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animationRoot = root.transform.Find("Apartment_Motion/Apartments_Normal");
                    var collapse = root.GetComponent<ApartmentTsunamiCollapse>() ?? root.AddComponent<ApartmentTsunamiCollapse>();
                    var data = new SerializedObject(collapse);
                    data.FindProperty("apartment").objectReferenceValue = root.GetComponent<ApartmentUpgradeModel>();
                    data.FindProperty("animationRoot").objectReferenceValue = animationRoot;
                    data.FindProperty("collapseAnimation").objectReferenceValue = clip;
                    var parts = data.FindProperty("parts"); parts.arraySize = 6;
                    for (int i = 0; i < 6; i++)
                    {
                        var part = parts.GetArrayElementAtIndex(i);
                        part.FindPropertyRelative("body").objectReferenceValue = animationRoot.Find("Apartment" + (i + 1));
                        var attachments = part.FindPropertyRelative("attachments"); attachments.arraySize = 3;
                        attachments.GetArrayElementAtIndex(0).objectReferenceValue = root.transform.Find("Apartment_Motion/Apartment_Surface_" + (i + 1));
                        attachments.GetArrayElementAtIndex(1).objectReferenceValue = animationRoot.Find("Sprinkler" + (i + 1));
                        attachments.GetArrayElementAtIndex(2).objectReferenceValue = animationRoot.Find("Sprinkler" + (i + 1) + "(Base)");
                        if (part.FindPropertyRelative("body").objectReferenceValue == null ||
                            Enumerable.Range(0, 3).Any(index => attachments.GetArrayElementAtIndex(index).objectReferenceValue == null))
                            throw new InvalidOperationException("Missing authored tsunami target on " + name);
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the authored Apartment(Tsunami) motion on all four apartment prefabs, keeping their ground and meshes unchanged.");
        }

        private static AnimationClip BakeClip(Transform normal)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(AnimationPath).OfType<AnimationClip>().Single(clip => !clip.name.StartsWith("__preview"));
            var result = new AnimationClip { name = "Apartment Tsunami Collapse", frameRate = source.frameRate, wrapMode = WrapMode.ClampForever };
            var bindings = AnimationUtility.GetCurveBindings(source);
            for (int i = 1; i <= 6; i++)
            {
                string path = "Apartment" + i + "(Tsunami)";
                var target = normal.Find("Apartment" + i);
                var curves = bindings.Where(binding => binding.path == path && binding.type == typeof(Transform))
                    .ToDictionary(binding => binding.propertyName, binding => AnimationUtility.GetEditorCurve(source, binding));
                foreach (string property in new[] { "m_LocalPosition", "m_LocalScale" })
                for (int axis = 0; axis < 3; axis++)
                {
                    string key = property + "." + "xyz"[axis];
                    var curve = curves[key]; var keys = curve.keys;
                    float start = curve.Evaluate(0f);
                    float rest = property == "m_LocalPosition" ? target.localPosition[axis] : target.localScale[axis];
                    float factor = property == "m_LocalScale" && Mathf.Abs(start) > .0001f ? rest / start : 1f;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        keys[k].value = property == "m_LocalPosition" ? keys[k].value + rest - start : keys[k].value * factor;
                        keys[k].inTangent *= factor; keys[k].outTangent *= factor;
                    }
                    var translated = new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
                    AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve(target.name, typeof(Transform), key), translated);
                }
                Quaternion ReadRotation(float time) => new Quaternion(curves["m_LocalRotation.x"].Evaluate(time),
                    curves["m_LocalRotation.y"].Evaluate(time), curves["m_LocalRotation.z"].Evaluate(time), curves["m_LocalRotation.w"].Evaluate(time)).normalized;
                var correction = target.localRotation * Quaternion.Inverse(ReadRotation(0f));
                var times = curves.Where(pair => pair.Key.StartsWith("m_LocalRotation")).SelectMany(pair => pair.Value.keys.Select(key => key.time)).Distinct().OrderBy(time => time).ToArray();
                var rotations = new[] { new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>() };
                foreach (float time in times)
                {
                    var rotation = correction * ReadRotation(time);
                    for (int axis = 0; axis < 4; axis++) rotations[axis].Add(new Keyframe(time, rotation[axis]));
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    var curve = new AnimationCurve(rotations[axis].ToArray());
                    for (int key = 0; key < curve.length; key++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                    }
                    AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve(target.name, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]), curve);
                }
            }
            result.EnsureQuaternionContinuity();
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (existing == null) { AssetDatabase.CreateAsset(result, ClipPath); return result; }
            EditorUtility.CopySerialized(result, existing); Object.DestroyImmediate(result); return existing;
        }

    }
}
