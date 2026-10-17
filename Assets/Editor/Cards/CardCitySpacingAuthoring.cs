using System;
using System.Linq;
using CivilGameJam.Disasters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardCitySpacingAuthoring
    {
        private const float RowSpacing = 1.152f;

        [MenuItem("Tools/Card Game/Save City Front To Back Row Spacing")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Arrange the saved city in Edit Mode.");
            EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            Configure(GameObject.Find("World3D"));
            var view = Object.FindFirstObjectByType<CityViewport>();
            var camera = view.WorldCamera;
            var navigation = Object.FindFirstObjectByType<CityCameraController>();
            var localPose = camera.transform.localPosition;
            var worldPose = camera.transform.position;
            var settings = new SerializedObject(view);
            settings.FindProperty("frameWorldAutomatically").boolValue = true;
            settings.FindProperty("verticalAlignment").floatValue = 0.6f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            view.RefreshViewport();
            var offset = camera.transform.position - worldPose;
            camera.transform.localPosition = localPose;
            navigation.transform.position += offset;
            settings.Update();
            settings.FindProperty("frameWorldAutomatically").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var navigationSettings = new SerializedObject(navigation);
            navigationSettings.FindProperty("viewportVerticalAlignment").floatValue = 0.6f;
            navigationSettings.FindProperty("farthestZoom").floatValue = Mathf.Max(7f, camera.orthographicSize * 1.6f);
            navigationSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved updated city row spacing with fitted ground and camera.");
        }

        internal static void Configure(GameObject world)
        {
            var source = EditorSceneManager.OpenScene(CardShowcaseAuthoring.SourceScene, OpenSceneMode.Additive);
            try
            {
                // Always derive from the source scene so saving or reimporting never multiplies the spacing again.
                var original = source.GetRootGameObjects().Single(root => root.name == "Buildings_3D").transform;
                var originalBuildings = original.Find("Buildings");
                var target = world.transform.Find("Buildings_3D");
                var buildings = target.Find("Buildings");
                var groups = originalBuildings.Cast<Transform>().ToArray();
                float front = groups.Min(group => group.localPosition.z);
                float back = groups.Max(group => group.localPosition.z);
                float center = (front + back) * 0.5f;
                float addedDepth = (back - front) * (RowSpacing - 1f);
                foreach (var group in groups)
                {
                    var copy = buildings.Find(group.name);
                    var position = group.localPosition;
                    position.z = center + (position.z - center) * RowSpacing;
                    copy.localPosition = position;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(copy);
                }
                foreach (string name in new[] { "Continuous_Ground", "Island_Base" })
                {
                    var ground = original.Find("City_Environment/" + name);
                    var copy = target.Find("City_Environment/" + name);
                    var scale = ground.localScale;
                    scale.z += addedDepth;
                    copy.localScale = scale;
                }
                var lamps = original.Find("City_Environment/Street_Lamps");
                if (lamps != null)
                    foreach (Transform lamp in lamps)
                    {
                        var copy = target.Find("City_Environment/Street_Lamps/" + lamp.name);
                        var position = lamp.localPosition;
                        float row = Mathf.Clamp(position.z, front, back);
                        position.z += (row - center) * (RowSpacing - 1f);
                        copy.localPosition = position;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(copy);
                    }
                var originalTsunami = source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TsunamiTestController>(true)).Single();
                var tsunami = world.GetComponentInChildren<TsunamiTestController>(true);
                var tsunamiSettings = new SerializedObject(tsunami);
                var originalTsunamiSettings = new SerializedObject(originalTsunami);
                tsunamiSettings.FindProperty("waveWidth").floatValue =
                    originalTsunamiSettings.FindProperty("waveWidth").floatValue;
                // Keep the incoming wave and shoreline outside the extended front edge.
                foreach (string field in new[] { "shorelineDistance", "startDistance" })
                    tsunamiSettings.FindProperty(field).floatValue =
                        originalTsunamiSettings.FindProperty(field).floatValue - addedDepth * 0.5f;
                tsunamiSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            finally { EditorSceneManager.CloseScene(source, true); }
        }
    }
}
