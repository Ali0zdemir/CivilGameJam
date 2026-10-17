using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    // Repairs only the affected saved controls; card artwork and board geometry stay authored.
    public static class CardBlueprintReuseAuthoring
    {
        [MenuItem("Tools/Card Game/Save Reusable Blueprints and Repair Progress UI")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var school = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SchoolCard.prefab");
            var progressTemplate = school.transform.Find("ProgressBar");
            if (progressTemplate == null) throw new InvalidOperationException("The saved progress bar template is missing.");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var card = root.GetComponent<DraggableCard>();
                    if (card == null) continue;
                    bool changed = false;
                    if (card.Station != null) changed |= RepairProgress(card.Station, progressTemplate);
                    if (card.Kind == CardKind.ResearchCenter) changed |= CardPresentationAuthoring.RepairResearchLight(root);
                    if (card.IsPerson)
                    {
                        root.GetComponent<CardHover>().Details.localScale = Vector3.one * (0.7176f / card.Rect.localScale.x);
                        changed = true;
                    }
                    if (card.Kind == CardKind.Blueprint)
                    {
                        SetText(root.transform.Find("HoverDetails/Properties/Description"),
                            "Reusable blueprint.\nEach order makes 2 items.\nWorkers are kept.");
                        changed = true;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            foreach (var card in game.GetComponentsInChildren<DraggableCard>(true))
            {
                if (card.Station != null) RepairProgress(card.Station, progressTemplate);
                if (!card.IsPerson) continue;
                var details = card.GetComponent<CardHover>().Details;
                details.localScale = Vector3.one * (0.7176f / card.Rect.localScale.x);
                PrefabUtility.RecordPrefabInstancePropertyModifications(details);
            }
            foreach (var slot in game.Vault.Slots)
                SetText(slot.CardRect.Find("HoverDetails/Properties/Description"),
                    "Reusable from the Vault.\nEach order makes 2 items.");
            var choice = game.transform.Find("BlueprintChoice");
            SetText(choice.Find("SelectionWindow/Footer"), "Blueprints return to the Vault after production.");
            SetText(choice.Find("SelectionWindow/Description"), "Discover once · Reuse from the Vault · Produces 2 items per order");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved progress UI references, 38% larger people information panels, and reusable blueprint descriptions.");
        }

        private static bool RepairProgress(BuildingStation station, Transform template)
        {
            if (station.Card.Kind == CardKind.House) return false;
            var root = station.transform.Find("ProgressBar") as RectTransform;
            if (root == null)
            {
                root = (RectTransform)Object.Instantiate(template.gameObject, station.transform, false).transform;
                root.name = "ProgressBar";
                root.gameObject.SetActive(false);
            }
            var fill = root.Find("BlueProgress")?.GetComponent<Image>();
            if (fill == null) throw new InvalidOperationException(station.name + " has no saved progress fill.");
            var data = new SerializedObject(station);
            data.FindProperty("progressRoot").objectReferenceValue = root;
            data.FindProperty("progressFill").objectReferenceValue = fill;
            bool changed = data.ApplyModifiedPropertiesWithoutUndo();
            if (changed && PrefabUtility.IsPartOfPrefabInstance(station))
                PrefabUtility.RecordPrefabInstancePropertyModifications(station);
            return changed;
        }

        private static void SetText(Transform target, string text)
        {
            if (target == null) return;
            var label = target.GetComponent<TextMeshProUGUI>();
            if (label == null) return;
            label.text = text;
            if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }
    }
}
