using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Menu.Editor
{
    public static class MainMenuButtonArtAuthoring
    {
        private const string PrimaryPath = "Assets/EfeArts/Tutorial buttons.png";
        private const string SecondaryPath = "Assets/EfeArts/Tutorial buttons 2.png";

        [MenuItem("Tools/Civil Game Jam/Apply Tutorial Button Art")]
        public static void ApplyToSavedBackup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Edit the menu outside Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(MainMenuBackupAuthoring.ScenePath);
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            if (menu == null) throw new InvalidOperationException("The menu controller is missing.");
            ApplyToMenu(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the button artwork.");
            AssetDatabase.SaveAssets();
            Debug.Log("Tutorial button art saved: first sprite on Play Game, second sprite on Options, Exit, and Back.");
        }

        public static void ApplyToMenu(MainMenuController menu)
        {
            var primary = LoadButtonSprite(PrimaryPath);
            var secondary = LoadButtonSprite(SecondaryPath);
            foreach (var button in menu.GetComponentsInChildren<Button>(true))
            {
                bool isPlay = button.name == "PlayGameButton";
                var image = button.GetComponent<Image>();
                image.sprite = isPlay ? primary : secondary;
                image.overrideSprite = null;
                image.color = Color.white;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.fillCenter = true;
                image.pixelsPerUnitMultiplier = 1f;
                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
                colors.selectedColor = Color.white;
                colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                // Both sprites already include a pixel border and drop shadow.
                foreach (var effect in button.GetComponents<BaseMeshEffect>()) effect.enabled = false;
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.color = isPlay ? new Color32(37, 39, 41, 255) : new Color32(247, 244, 232, 255);
                FitLabelToArtwork(button);
                EditorUtility.SetDirty(button);
                EditorUtility.SetDirty(image);
            }
        }

        public static void FitLabelToArtwork(Button button)
        {
            var image = button.GetComponent<Image>();
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (image == null || label == null) return;
            Vector2 buttonSize = image.rectTransform.rect.size;
            if (buttonSize.x <= 0f || buttonSize.y <= 0f) return;
            Vector2 artworkSize = buttonSize;
            var sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
            if (image.type == Image.Type.Simple && image.preserveAspect && sprite != null)
            {
                float aspect = sprite.rect.width / sprite.rect.height;
                if (artworkSize.x / artworkSize.y > aspect) artworkSize.x = artworkSize.y * aspect;
                else artworkSize.y = artworkSize.x / aspect;
            }
            // Keep the caption inside the visible artwork, clear of its pixel border and shadow.
            Vector2 insetSize = Vector2.Max(Vector2.one, artworkSize - new Vector2(32f, 16f));
            Vector2 halfAnchors = new Vector2(insetSize.x / buttonSize.x, insetSize.y / buttonSize.y) * .5f;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.one * .5f - halfAnchors;
            rect.anchorMax = Vector2.one * .5f + halfAnchors;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.margin = Vector4.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 36f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.Midline;
            EditorUtility.SetDirty(rect);
            EditorUtility.SetDirty(label);
        }

        private static Sprite LoadButtonSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("The button artwork is missing: " + path);
            var border = new Vector4(20f, 20f, 20f, 20f);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single || importer.spriteBorder != border ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !importer.alphaIsTransparency ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.spritePixelsPerUnit != 100f ||
                importer.npotScale != TextureImporterNPOTScale.None || settings.spriteMeshType != SpriteMeshType.FullRect;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Could not import the button sprite: " + path);
            return sprite;
        }
    }
}
