using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardWorkplaceTextAuthoring
    {
        private static readonly string[] SharedArtwork = { "DışKenar", "Klasör", "İçBeyaz" };
        private static readonly string[] SharedWidgets = { "NameTag", "APEducated", "APNormalPeople" };

        [MenuItem("Tools/Card Game/Match School And Factory Layout To Research Center")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Match saved workplace layout in Edit Mode.");
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ResearchCenterCard.prefab");
            if (reference == null || reference.transform.Find("FolderArt/NameTag") == null)
                throw new InvalidOperationException("The saved Research Center name tag is missing.");
            foreach (string name in new[] { "SchoolCard", "FactoryCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Match(reference, root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // Apply the saved prefab design to scene instances while retaining their card positions.
            var scene = EditorSceneManager.OpenScene(CardShowcaseAuthoring.CardScene);
            var game = Object.FindFirstObjectByType<CardGameController>();
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true))
                if (card.Kind == CardKind.School || card.Kind == CardKind.Factory) Match(reference, card.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved Factory and School frames, name tags and AP badges matching the authored Research Center layout.");
        }

        private static void Match(GameObject reference, GameObject target)
        {
            var folder = target.transform.Find("FolderArt");
            var referenceFolder = reference.transform.Find("FolderArt");
            if (folder == null) throw new InvalidOperationException(target.name + " has no saved folder artwork.");
            var legacyTitle = target.transform.Find("Text (TMP)");
            if (legacyTitle != null) Object.DestroyImmediate(legacyTitle.gameObject);
            foreach (string name in SharedArtwork)
            {
                var from = referenceFolder.Find(name).GetComponent<Image>();
                var to = folder.Find(name).GetComponent<Image>();
                to.sprite = from.sprite;
                to.color = from.color;
                Save(to);
            }
            foreach (string name in SharedWidgets)
            {
                var from = referenceFolder.Find(name);
                if (from == null) throw new InvalidOperationException("Missing Research Center widget: " + name);
                var widget = folder.Find(name);
                if (widget == null)
                {
                    widget = Object.Instantiate(from.gameObject, folder, false).transform;
                    widget.name = name;
                    widget.gameObject.hideFlags = HideFlags.None;
                }
                widget.SetAsLastSibling();
                CopyRect((RectTransform)from, (RectTransform)widget);
                var fromImage = from.GetComponent<Image>();
                var toImage = widget.GetComponent<Image>();
                toImage.sprite = fromImage.sprite;
                toImage.color = fromImage.color;
                toImage.material = fromImage.material;
                toImage.type = fromImage.type;
                toImage.preserveAspect = fromImage.preserveAspect;
                toImage.raycastTarget = fromImage.raycastTarget;
                Save(toImage);
                var fromText = from.GetComponentInChildren<TextMeshProUGUI>(true);
                var toText = widget.GetComponentInChildren<TextMeshProUGUI>(true);
                CopyPresentation(fromText, toText);
                toText.text = name == "NameTag"
                    ? target.GetComponent<DraggableCard>().Kind == CardKind.School ? "SCHOOL" : "FACTORY"
                    : fromText.text;
                Save(toText);
            }
            CopyPresentation(reference.transform.Find("WorkStatus").GetComponent<TextMeshProUGUI>(),
                target.transform.Find("WorkStatus").GetComponent<TextMeshProUGUI>());
            CopyPresentation(reference.transform.Find("FolderArt/WorkerCaption").GetComponent<TextMeshProUGUI>(),
                folder.Find("WorkerCaption").GetComponent<TextMeshProUGUI>());
        }

        private static void Save(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        internal static void CopyPresentation(TextMeshProUGUI source, TextMeshProUGUI target)
        {
            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.fontSize = source.fontSize;
            target.fontSizeMin = source.fontSizeMin;
            target.fontSizeMax = source.fontSizeMax;
            target.enableAutoSizing = source.enableAutoSizing;
            target.fontStyle = source.fontStyle;
            target.fontWeight = source.fontWeight;
            target.color = source.color;
            target.alignment = source.alignment;
            target.characterSpacing = source.characterSpacing;
            target.wordSpacing = source.wordSpacing;
            target.lineSpacing = source.lineSpacing;
            target.paragraphSpacing = source.paragraphSpacing;
            target.margin = source.margin;
            target.textWrappingMode = source.textWrappingMode;
            target.overflowMode = source.overflowMode;
            target.richText = source.richText;
            target.raycastTarget = source.raycastTarget;
            CopyRect(source.rectTransform, target.rectTransform);
            Save(target);
        }

        private static void CopyRect(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.sizeDelta = from.sizeDelta;
            to.anchoredPosition3D = from.anchoredPosition3D;
            to.localScale = from.localScale;
            to.localRotation = from.localRotation;
            Save(to);
        }
    }
}
