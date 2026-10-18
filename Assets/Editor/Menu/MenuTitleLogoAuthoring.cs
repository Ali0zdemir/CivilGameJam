using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Menu.Editor
{
    public static class MenuTitleLogoAuthoring
    {
        public const string LogoPath = "Assets/EfeArts/new logo.png";

        [MenuItem("Tools/Civil Game Jam/Use NextWave Menu Title Logo")]
        public static void ApplyToSavedMenus()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit the menu title outside Play Mode.");
            var original = SceneManager.GetActiveScene();
            try
            {
                foreach (string path in new[] { MainMenuBackupAuthoring.ScenePath, "Assets/2DCardScene.unity" })
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        var root = scene.GetRootGameObjects().First(obj => obj.GetComponent<MainMenuController>() != null ||
                            obj.GetComponent<PauseMenuController>() != null);
                        var panel = root.GetComponent<PauseMenuController>() != null
                            ? root.transform.Find("PausePanel/MainPanel") : root.transform.Find("MainPanel");
                        ApplyToPanel(panel);
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the NextWave title: " + path);
                    }
                    finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally { if (original.isLoaded) SceneManager.SetActiveScene(original); }
        }

        public static void ApplyToPanel(Transform panel)
        {
            var importer = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("The NextWave title artwork is missing.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !importer.alphaIsTransparency ||
                importer.npotScale != TextureImporterNPOTScale.None || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (sprite == null) throw new InvalidOperationException("Could not import the NextWave title as a sprite.");
            var title = panel.Find("Title");
            if (title == null) throw new InvalidOperationException("The saved menu title is missing.");
            var oldText = title.GetComponent<TMP_Text>();
            if (oldText != null) Object.DestroyImmediate(oldText);
            var image = title.GetComponent<Image>() ?? title.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
        }
    }
}
