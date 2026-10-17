using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CivilGameJam.Cards.Editor
{
    public static class CardProgressAuthoring
    {
        private static readonly Regex Timing = new Regex(@"\d+(?:[.,]\d+)?\s*(?:sn\b|san[iİıI]ye\b|seconds?\b)|%",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static void BuildForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (RemoveTiming(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects()) RemoveTiming(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved card and Vault information without seconds or percentages; existing progress controls are preserved.");
        }

        private static bool RemoveTiming(GameObject root)
        {
            bool changed = false;
            foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (!Timing.IsMatch(label.text)) continue;
                label.text = string.Join("\n", label.text.Split('\n', '·')
                    .Where(part => !Timing.IsMatch(part)).Select(part => part.Trim()).Where(part => part.Length > 0));
                if (label.text == "2 KART" && label.GetComponentInParent<BlueprintVaultSlot>() != null)
                    label.text = "PRODUCES 2 ITEMS";
                if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                changed = true;
            }
            return changed;
        }
    }
}
