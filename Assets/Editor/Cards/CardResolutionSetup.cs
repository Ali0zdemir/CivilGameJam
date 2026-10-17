using System;
using CivilGameJam.Cards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards.Editor
{
    public static class CardResolutionSetup
    {
        public static readonly Vector2 DesignSize = new Vector2(1600f, 900f);

        [MenuItem("Tools/Civil Game Jam/Configure Resolution Scaling")]
        public static void ConfigureActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Configure the saved scene in Edit Mode.");
            var game = UnityEngine.Object.FindFirstObjectByType<CardGameController>();
            if (game == null) throw new InvalidOperationException("Open 2DCardScene first.");
            Configure(game);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            ConfigureActiveScene();
            CardSceneAuthoring.VerifySavedAssets();
        }

        public static void Configure(CardGameController game)
        {
            var canvas = game.GetComponentInParent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            var table = (RectTransform)game.transform;
            bool docked = game.GetComponent<CardDock>() != null;
            bool fromLeft = docked && Mathf.Approximately(table.anchorMin.x, 0f) && Mathf.Approximately(table.anchorMax.x, 0f);
            Vector2 panelSize = table.rect.size;
            Vector2 designSize = docked
                ? new Vector2(Mathf.Max(2600f, panelSize.x * table.localScale.x + (fromLeft
                    ? table.anchoredPosition.x + 24f : 2f * Mathf.Abs(table.anchoredPosition.x) + 48f)),
                    1800f)
                : Vector2.Max(DesignSize, panelSize);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = designSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            // One authored coordinate space in both Scene and Game. Only the canvas scales to the display.
            float anchorX = fromLeft ? 0f : 0.5f;
            table.anchorMin = table.anchorMax = table.pivot = new Vector2(anchorX, docked ? 0f : 0.5f);
            if (docked) table.anchorMax = new Vector2(anchorX, 0.75f / table.localScale.y);
            if (!docked) table.anchoredPosition = Vector2.zero;
            table.sizeDelta = docked ? new Vector2(panelSize.x, 0f) : designSize;
            if (!docked) table.localScale = Vector3.one;

            var background = canvas.transform.Find("ScreenBackground")?.GetComponent<Image>();
            if (docked && background != null)
            {
                background.gameObject.SetActive(true);
                background.color = new Color(0.035f, 0.055f, 0.065f, 1f);
            }
            if (background == null && !docked)
            {
                var rect = new GameObject("ScreenBackground", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.gameObject.layer = canvas.gameObject.layer;
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                background = rect.gameObject.AddComponent<Image>();
                background.color = table.GetComponent<Image>().color;
                background.raycastTarget = false;
                rect.SetAsFirstSibling();
            }

            Canvas.ForceUpdateCanvases();
            PlayerSettings.resizableWindow = true;
            EditorUtility.SetDirty(scaler);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        }

    }
}
