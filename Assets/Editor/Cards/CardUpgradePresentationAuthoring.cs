using System;
using System.Collections.Generic;
using CivilGameJam.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardUpgradePresentationAuthoring
    {
        [MenuItem("Tools/Card Game/Save Worker Hover And House Upgrade Presentation")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save card presentation in Edit Mode.");
            foreach (var name in new[] { "ResearchCenterCard", "SchoolCard", "FactoryCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var slot in root.GetComponentsInChildren<WorkerSlot>(true)) ConfigureWorker(slot);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            var game = Object.FindFirstObjectByType<CardGameController>();
            CardHouseUpgradeAuthoring.Configure(game, GameObject.Find("World3D"));
            foreach (var slot in game.GetComponentsInChildren<WorkerSlot>(true)) ConfigureWorker(slot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved worker hover, house upgrade camera presentation and rail pistons before Play Mode.");
        }

        [MenuItem("Tools/Card Game/Save Worker Portrait Pointer Coverage")]
        public static void SavePointerCoverageForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save worker pointer coverage in Edit Mode.");
            foreach (string name in new[] { "ResearchCenterCard", "SchoolCard", "FactoryCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                    foreach (var slot in root.GetComponentsInChildren<WorkerSlot>(true)) ConfigureWorkerHitArea(slot);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            Canvas.ForceUpdateCanvases();
            foreach (var slot in Object.FindFirstObjectByType<CardGameController>().GetComponentsInChildren<WorkerSlot>(true)) ConfigureWorkerHitArea(slot);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        private static void ConfigureWorker(WorkerSlot slot)
        {
            var data = new SerializedObject(slot);
            data.FindProperty("hoverLift").floatValue = 24f;
            data.FindProperty("hoverSeconds").floatValue = 0.16f;
            data.ApplyModifiedPropertiesWithoutUndo();
            ConfigureWorkerHitArea(slot);
            if (PrefabUtility.IsPartOfPrefabInstance(slot)) PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
        }

        private static void ConfigureWorkerHitArea(WorkerSlot slot)
        {
            if (slot.Portrait == null) return;
            var hitArea = slot.GetComponent<UnityEngine.UI.Image>();
            var rect = (RectTransform)slot.transform;
            var portrait = slot.Portrait.rectTransform;
            var corners = new List<Vector3>();
            var sprite = slot.Portrait.sprite;
            if (sprite != null && sprite.GetPhysicsShapeCount() > 0)
            {
                var renderSize = portrait.rect.size;
                if (slot.Portrait.preserveAspect)
                    renderSize = sprite.rect.size * Mathf.Min(renderSize.x / sprite.rect.width, renderSize.y / sprite.rect.height);
                var origin = portrait.rect.min + Vector2.Scale(portrait.rect.size - renderSize, portrait.pivot);
                var shape = new List<Vector2>();
                for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
                {
                    sprite.GetPhysicsShape(i, shape);
                    foreach (var point in shape)
                    {
                        var pixel = point * sprite.pixelsPerUnit + sprite.pivot;
                        corners.Add(portrait.TransformPoint(origin + Vector2.Scale(pixel, new Vector2(renderSize.x / sprite.rect.width, renderSize.y / sprite.rect.height))));
                    }
                }
            }
            else
            {
                var rectangle = new Vector3[4]; portrait.GetWorldCorners(rectangle); corners.AddRange(rectangle);
            }
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                Vector2 point = rect.InverseTransformPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            float lift = new SerializedObject(slot).FindProperty("hoverLift").floatValue;
            var padding = hitArea.raycastPadding;
            padding.x = Mathf.Min(padding.x, min.x - rect.rect.xMin - 2f);
            padding.z = Mathf.Min(padding.z, rect.rect.xMax - max.x - 2f);
            padding.w = Mathf.Min(padding.w, rect.rect.yMax - max.y - lift - 2f);
            hitArea.raycastPadding = padding; hitArea.raycastTarget = true;
            EditorUtility.SetDirty(hitArea);
            if (PrefabUtility.IsPartOfPrefabInstance(hitArea)) PrefabUtility.RecordPrefabInstancePropertyModifications(hitArea);
        }

        internal static HouseUpgradePresentation ConfigureCity(CardGameController game, GameObject world)
        {
            var presentation = world.GetComponent<HouseUpgradePresentation>();
            if (presentation == null) presentation = world.AddComponent<HouseUpgradePresentation>();
            var data = new SerializedObject(presentation);
            data.FindProperty("game").objectReferenceValue = game;
            data.FindProperty("dock").objectReferenceValue = game.GetComponent<CardDock>();
            data.FindProperty("navigation").objectReferenceValue = world.GetComponentInChildren<CityCameraController>(true);
            data.FindProperty("zoomFactor").floatValue = 2.2f;
            data.FindProperty("closeupMagnification").floatValue = 1.5f;
            data.FindProperty("zoomSeconds").floatValue = 0.9f;
            data.FindProperty("holdSeconds").floatValue = 1.1f;
            data.FindProperty("returnSeconds").floatValue = 1.2f;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var rail in world.GetComponentsInChildren<BuildingRailUpgrade>(true))
            {
                var settings = new SerializedObject(rail);
                settings.FindProperty("transitionDuration").floatValue = 1.05f;
                settings.FindProperty("settlingDuration").floatValue = .7f;
                settings.FindProperty("peakLiftMultiplier").floatValue = 2f;
                settings.FindProperty("removalDuration").floatValue = 0.35f;
                settings.FindProperty("dustMultiplier").floatValue = 2.25f;
                settings.FindProperty("dustLightening").floatValue = .28f;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            return presentation;
        }
    }
}
