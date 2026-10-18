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
                var personSprite = (Sprite)workerData.FindProperty("normalPortrait").objectReferenceValue;
                var educatedSprite = (Sprite)workerData.FindProperty("educatedPortrait").objectReferenceValue;
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

                var hint = Rect("OneWorkerNeeded", folder, new Vector2(-21.3f, -9f), new Vector2(80f, 112f));
                hint.localScale = Vector3.one * 1.6f;
                var group = Ensure<CanvasGroup>(hint.gameObject);
                group.alpha = 1f; group.interactable = false; group.blocksRaycasts = false;
                var person = Picture("Person", hint, personSprite, new Vector2(-7f, 0f), new Vector2(48f, 48f));
                person.color = new Color(.85f, 1f, .9f, .8f);
                var outline = Ensure<Outline>(person.gameObject);
                outline.effectColor = new Color(.48f, 1f, .7f, .6f); outline.effectDistance = new Vector2(1.3f, -1.3f);
                var educated = Picture("EducatedPerson", person.transform, educatedSprite, new Vector2(14f, 0f), new Vector2(48f, 48f));
                educated.color = new Color(1f, 1f, 1f, .8f);
                educated.transform.SetAsFirstSibling();
                var educatedOutline = Ensure<Outline>(educated.gameObject);
                educatedOutline.effectColor = outline.effectColor; educatedOutline.effectDistance = outline.effectDistance;
                var plus = Rect("AddPerson", person.transform, new Vector2(36f, 18f), new Vector2(16f, 16f));
                var mint = new Color(.72f, 1f, .81f);
                Bar("Horizontal", plus, Vector2.zero, new Vector2(12f, 3f), 0f, mint);
                Bar("Vertical", plus, Vector2.zero, new Vector2(3f, 12f), 0f, mint);
                var arrow = Rect("IntoWorkerPocket", hint, new Vector2(0f, -37f), new Vector2(22f, 16f));
                Bar("Left", arrow, new Vector2(-4f, 0f), new Vector2(12f, 3f), -45f, mint);
                Bar("Right", arrow, new Vector2(4f, 0f), new Vector2(12f, 3f), 45f, mint);
                hint.gameObject.SetActive(true);
                Refs(view, ("factory", factory), ("station", station), ("pocket", pocket),
                    ("storedBlueprint", stored), ("entryImage", preview), ("entryCanvas", previewCanvas),
                    ("entryMouth", mouth), ("workerHint", group), ("personHint", person.rectTransform), ("arrowHint", arrow));
                var data = new SerializedObject(view);
                data.FindProperty("entrySeconds").floatValue = workerData.FindProperty("entrySeconds").floatValue;
                data.ApplyModifiedPropertiesWithoutUndo();
                Refs(factory, ("folderPresentation", view));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved the overlapping normal and educated worker guide and blueprint folder pocket in FactoryCard; scene instances inherit them.");
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

        private static void Bar(string name, Transform parent, Vector2 position, Vector2 size, float rotation, Color color)
        {
            var image = Picture(name, parent, null, position, size);
            image.color = color; image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
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
