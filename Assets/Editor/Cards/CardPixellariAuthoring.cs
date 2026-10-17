using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardPixellariAuthoring
    {
        public const string FontPath = "Assets/EfeArts/Pixellari SDF.asset";
        private const string OutlinePath = "Assets/Materials/ProductHoverNameOutline.mat";

        [MenuItem("Tools/Card Game/Apply Pixellari To All Game Text")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save the game's fonts in Edit Mode.");

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/EfeArts/Pixellari.ttf");
            if (font == null || source == null) throw new InvalidOperationException("Pixellari assets are missing.");
            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(TMP_Settings.instance);

            // Include the source in builds so changing worker/status text keeps using Pixellari.
            var fontData = new SerializedObject(font);
            fontData.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
            fontData.FindProperty("m_SourceFontFile").objectReferenceValue = source;
            fontData.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            fontData.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            fontData.ApplyModifiedPropertiesWithoutUndo();
            font.ReadFontAssetDefinition();

            var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            if (outline != null)
            {
                float width = outline.GetFloat(ShaderUtilities.ID_OutlineWidth);
                Color color = outline.GetColor(ShaderUtilities.ID_OutlineColor);
                outline.shader = font.material.shader;
                outline.CopyPropertiesFromMaterial(font.material);
                outline.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
                outline.SetColor(ShaderUtilities.ID_OutlineColor, color);
                outline.EnableKeyword("OUTLINE_ON");
                EditorUtility.SetDirty(outline);
            }

            var characters = new HashSet<char>(Enumerable.Range(32, 95).Select(value => (char)value));
            foreach (char character in "ıİğĞşŞüÜöÖçÇ·×") characters.Add(character);
            int prefabLabels = 0, sceneLabels = 0;
            foreach (string path in GameAssets("t:Prefab"))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int count = Apply(root, font, outline, characters);
                    if (count == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabLabels += count;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in GameAssets("t:Scene"))
            {
                var scene = EditorSceneManager.OpenScene(path);
                int count = 0;
                foreach (var root in scene.GetRootGameObjects()) count += Apply(root, font, outline, characters);
                if (count == 0) continue;
                ValidateControllerLabels(scene.GetRootGameObjects());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                sceneLabels += count;
            }

            string glyphs = new string(characters.OrderBy(value => value).ToArray());
            if (!font.TryAddCharacters(glyphs, out string missing))
                throw new InvalidOperationException("Pixellari is missing game characters: " + missing);
            EditorUtility.SetDirty(font);
            foreach (var atlas in font.atlasTextures) if (atlas != null) EditorUtility.SetDirty(atlas);
            EditorUtility.SetDirty(font.material);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            Debug.Log($"Pixellari saved: {prefabLabels} prefab labels, {sceneLabels} scene labels; all city controller labels remain assigned.");
        }

        private static IEnumerable<string> GameAssets(string filter) => AssetDatabase.FindAssets(filter, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !path.StartsWith("Assets/TextMesh Pro/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);

        private static int Apply(GameObject root, TMP_FontAsset font, Material outline, HashSet<char> characters)
        {
            if (root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Length != 0)
                throw new InvalidOperationException("Legacy text has not been converted: " + root.name);
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var label in labels)
            {
                bool outlined = outline != null && label.fontSharedMaterial == outline;
                label.font = font;
                label.fontSharedMaterial = outlined ? outline : font.material;
                // Pixellari has no arrow or en dash; keep these small UI separators in its own glyph set.
                label.text = label.text.Replace("→", "->").Replace("–", "-");
                foreach (char character in label.text) if (!char.IsControl(character)) characters.Add(character);
                EditorUtility.SetDirty(label);
                if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
            return labels.Length;
        }

        private static void ValidateControllerLabels(IEnumerable<GameObject> roots)
        {
            foreach (var component in roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (component == null) continue;
                string type = component.GetType().Name;
                if (!(type.EndsWith("TestPanel", StringComparison.Ordinal) || type.EndsWith("TestController", StringComparison.Ordinal))) continue;
                var data = new SerializedObject(component);
                var property = data.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || !property.name.EndsWith("Label", StringComparison.Ordinal)) continue;
                    if (!(property.objectReferenceValue is TMP_Text))
                        throw new InvalidOperationException(type + "." + property.propertyPath + " lost its saved text reference.");
                }
            }
        }
    }
}
