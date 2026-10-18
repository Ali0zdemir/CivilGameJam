using System;
using System.Linq;
using CivilGameJam.Cards;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardFactoryFolderAuthoring
    {
        [MenuItem("Tools/Card Game/Save Factory Blueprint Pocket And Worker Hint")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save the factory presentation in Edit Mode.");
            const string path = "Assets/Prefabs/FactoryCard.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var folder = root.transform.Find("FolderArt");
                var factory = root.GetComponent<FactoryProduction>();
                var station = root.GetComponent<BuildingStation>();
                var worker = folder.GetComponentsInChildren<WorkerSlot>(true).Single(slot => slot.Index == 0);
                var workerData = new SerializedObject(worker);
                var view = Ensure<FactoryFolderPresentation>(root);
                var pocket = Rect("BlueprintPocket", folder, new Vector2(88f, 67.5f), new Vector2(82f, 265f));
                pocket.SetSiblingIndex(folder.Find("WorkerPockets").GetSiblingIndex());
                if (pocket.GetComponent<RectMask2D>() == null) pocket.gameObject.AddComponent<RectMask2D>();
                var stored = Picture("StoredBlueprint", pocket, null, new Vector2(0f, -119.5f), new Vector2(72f, 96f));
                stored.raycastTarget = true;
                // Unity stops delivering drag events to a disabled object. The pocket stays
                // active when its stored image is hidden during pickup or return.
                var oldSlot = stored.GetComponent<FactoryBlueprintSlot>();
                if (oldSlot != null) Object.DestroyImmediate(oldSlot);
                var slot = Ensure<FactoryBlueprintSlot>(pocket.gameObject);
                Refs(slot, ("factory", factory));
                var shadow = Ensure<Shadow>(stored.gameObject);
                shadow.effectColor = new Color(0f, 0f, 0f, .18f); shadow.effectDistance = new Vector2(1f, -2f);
                var mouth = Rect("EntryMouth", pocket, new Vector2(0f, -27.5f), Vector2.zero);
                var preview = Picture("EntryPreview", pocket, null, Vector2.zero, new Vector2(300f, 400f));
                var previewCanvas = Ensure<Canvas>(preview.gameObject);
                previewCanvas.overrideSorting = false; previewCanvas.sortingOrder = 10;
                var flightShadow = Ensure<Shadow>(preview.gameObject);
                flightShadow.effectColor = new Color(0f, 0f, 0f, .12f); flightShadow.effectDistance = new Vector2(4f, -5f);
                stored.gameObject.SetActive(false); preview.gameObject.SetActive(false);

                Refs(view, ("factory", factory), ("station", station), ("pocket", pocket),
                    ("storedBlueprint", stored), ("entryImage", preview), ("entryCanvas", previewCanvas),
                    ("entryMouth", mouth));
                ConfigureWorkerHint(folder, view);
                var data = new SerializedObject(view);
                data.FindProperty("entrySeconds").floatValue = workerData.FindProperty("entrySeconds").floatValue;
                data.ApplyModifiedPropertiesWithoutUndo();
                Refs(factory, ("folderPresentation", view));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the Insert Card Ui worker guide and blueprint folder pocket in FactoryCard; scene instances inherit them.");
        }

        public static void ConfigureWorkerHint(Transform folder, FactoryFolderPresentation view)
        {
            var hint = (folder.Find("InsertCardHint") ?? folder.Find("OneWorkerNeeded")) as RectTransform;
            if (hint == null)
            {
                hint = Rect("InsertCardHint", folder, new Vector2(-21.3f, -9f), new Vector2(80f, 112f));
                hint.localScale = Vector3.one * 1.6f;
            }
            hint.name = "InsertCardHint";
            foreach (var child in hint.Cast<Transform>().ToArray())
                if (child.name != "InsertCardUi") Object.DestroyImmediate(child.gameObject);
            var group = Ensure<CanvasGroup>(hint.gameObject);
            group.alpha = 1f; group.interactable = false; group.blocksRaycasts = false;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/Insert Card Ui.png");
            if (sprite == null) throw new InvalidOperationException("Insert Card Ui sprite is missing.");
            var guide = Picture("InsertCardUi", hint, sprite, Vector2.zero, Vector2.zero);
            guide.rectTransform.anchorMin = Vector2.zero;
            guide.rectTransform.anchorMax = Vector2.one;
            hint.gameObject.SetActive(true);
            Refs(view, ("workerHint", group), ("personHint", guide.rectTransform), ("arrowHint", guide.rectTransform));
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent, position, size);
            var image = Ensure<Image>(rect.gameObject);
            image.sprite = sprite; image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        private static void Refs(Object target, params (string, Object)[] values)
        {
            var data = new SerializedObject(target);
            foreach (var (name, value) in values) data.FindProperty(name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Ensure<T>(GameObject owner) where T : Component
        {
            var component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }
    }
}
