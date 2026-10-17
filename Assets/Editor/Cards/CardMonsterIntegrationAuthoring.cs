using System;
using System.IO;
using System.Linq;
using CivilGameJam.Disasters;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    /// <summary>Adapts only the imported monster's route and menu to the authored card scene.</summary>
    public static class CardMonsterIntegrationAuthoring
    {
        [MenuItem("Tools/Disasters/Adapt Imported Monster To Current Card City")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save monster integration in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var tsunami = components.OfType<TsunamiTestController>().Single();
            var monster = components.OfType<SeaMonsterEmergence>().Single();
            var sequence = components.OfType<MonsterSequenceController>().Single();
            var sequenceData = new SerializedObject(sequence);
            var panel = (RectTransform)sequenceData.FindProperty("panel").objectReferenceValue;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
            if (font == null) throw new InvalidOperationException("The existing Pixellari SDF font must be assigned.");
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
            {
                var go = text.gameObject;
                var value = text.text; var color = text.color; var size = text.fontSize;
                var align = text.alignment;
                Object.DestroyImmediate(text);
                var label = go.AddComponent<TextMeshProUGUI>();
                label.font = font; label.fontSize = size; label.fontSizeMax = size; label.fontSizeMin = 12;
                label.enableAutoSizing = true; label.text = value; label.color = color; label.raycastTarget = false;
                label.alignment = align == TextAnchor.MiddleLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            }
            sequenceData.FindProperty("selectionLabel").objectReferenceValue = panel.Find("Choose_Sequence/Label").GetComponent<TMP_Text>();
            sequenceData.FindProperty("statusLabel").objectReferenceValue = panel.Find("Status").GetComponent<TMP_Text>();
            sequenceData.ApplyModifiedPropertiesWithoutUndo();

            // Fit the event to MERGEALI's ground; never move or rescale buildings, ground, cards or cameras.
            var bounds = tsunami.CityBounds;
            monster.transform.localPosition = new Vector3(bounds.center.x, tsunami.SeaLevel - 0.15f, bounds.min.z - 2.8f);
            var data = new SerializedObject(tsunami);
            data.FindProperty("shorelineDistance").floatValue = bounds.min.z;
            data.FindProperty("startDistance").floatValue = bounds.min.z - 2.45f;
            data.ApplyModifiedPropertiesWithoutUndo();
            var sea = tsunami.transform.Find("Sea_Surface");
            sea.localPosition = new Vector3(bounds.center.x, tsunami.SeaLevel - 0.04f, bounds.min.z - 7f);
            sea.localScale = new Vector3(Mathf.Max(18f, bounds.size.x + 6f), 0.08f, 14f);
            var monsterData = new SerializedObject(monster);
            monsterData.FindProperty("seaSurface").objectReferenceValue = sea.gameObject;
            monsterData.ApplyModifiedPropertiesWithoutUndo();
            sea.gameObject.SetActive(false);
            monster.Model.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save imported monster references.");
            AssetDatabase.SaveAssets();
            var directory = Path.GetFullPath("Library/CardGameTestResults/GodzillaIntegration");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "authored.txt"),
                "MERGEALI ground: " + bounds + "\nMonster: " + monster.transform.position +
                "\nPoint 1: " + monster.GetComponent<SeaMonsterWalk>().WalkPoint.position +
                "\nPoint 2: " + monster.GetComponent<SeaMonsterWalk>().AfterEventPoint.position +
                "\nNo existing UI or city layout authored by this command.\n");
            Debug.Log("Imported monster references and Pixellari sequence menu saved for the existing MERGEALI city.");
        }
    }
}
