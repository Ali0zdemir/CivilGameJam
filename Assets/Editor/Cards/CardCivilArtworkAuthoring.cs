using System;
using System.Linq;
using CivilGameJam.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    // Saves native UI to the scene and prefabs; gameplay never builds these visuals.
    public static class CardCivilArtworkAuthoring
    {
        private const string HouseArt = "Assets/EfeArts/Ev/Civil Building Card.png";
        private const string BarArt = "Assets/EfeArts/BinasalŞeyler/";

        [MenuItem("Tools/Card Game/Save Civil Building Artwork And Orange Loading Bars")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Save the card artwork in Edit Mode.");
            ConfigureTexture(HouseArt, false);
            ConfigureTexture(BarArt + "Loading Bar.png", true);
            ConfigureTexture(BarArt + "Orange Bar.png", true);
            foreach (string name in new[] { "HouseCard", "ResearchCenterCard", "FactoryCard", "SchoolCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Configure(root.GetComponent<DraggableCard>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var card in root.GetComponentsInChildren<DraggableCard>(true)) Configure(card);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Saved Civil Building house artwork, three matching item sockets and orange progress bars above the workplaces.");
        }

        private static void ConfigureTexture(string path, bool bar)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            if (bar)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                importer.spriteImportMode = SpriteImportMode.Multiple;
                // The supplied artwork occupies the top 50 pixels of a card-sized PNG.
#pragma warning disable 618
                importer.spritesheet = new[] { new SpriteMetaData {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    rect = new Rect(0f, texture.height - 50f, texture.width, 50f),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                } };
#pragma warning restore 618
            }
            importer.SaveAndReimport();
        }

        private static Sprite Sprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();

        private static void Configure(DraggableCard card)
        {
            if (card == null) return;
            if (card.Kind == CardKind.House) ConfigureHouse(card);
            else if (card.Kind == CardKind.ResearchCenter || card.Kind == CardKind.Factory || card.Kind == CardKind.School)
                ConfigureBar(card);
        }

        private static void ConfigureHouse(DraggableCard card)
        {
            var folder = card.transform.Find("FolderArt");
            if (folder == null) throw new InvalidOperationException(card.name + " has no saved house artwork.");
            foreach (Transform child in folder)
                if (child.GetComponent<HouseUpgradeSlot>() == null && child.name != "CivilBuildingArtwork")
                { child.gameObject.SetActive(false); Record(child.gameObject); }
            var art = folder.Find("CivilBuildingArtwork")?.GetComponent<Image>();
            if (art == null)
            {
                art = new GameObject("CivilBuildingArtwork", typeof(RectTransform), typeof(Image), typeof(Shadow)).GetComponent<Image>();
                art.transform.SetParent(folder, false); art.gameObject.layer = card.gameObject.layer;
            }
            art.transform.SetAsFirstSibling();
            art.sprite = Sprite(HouseArt); art.color = Color.white; art.preserveAspect = true; art.raycastTarget = false;
            Stretch(art.rectTransform); art.gameObject.SetActive(true);
            var shadow = art.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .12f); shadow.effectDistance = new Vector2(4f, -5f);
            Assign(card.GetComponent<CardFeedback>(), "shadow", shadow);
            Rect drawing = Fit(card.Rect.rect, art.sprite.rect.size);
            foreach (var slot in folder.GetComponentsInChildren<HouseUpgradeSlot>(true))
            {
                int index = (int)slot.Kind - 1;
                Vector2 center = Pixel(drawing, art.sprite, new Vector2(160f + index * 160f, 610f));
                float side = drawing.width * 100f / art.sprite.rect.width;
                Place((RectTransform)slot.transform, center, Vector2.one * (side + 8f));
                var data = new SerializedObject(slot);
                var icon = (UpgradeIcon)data.FindProperty("icon").objectReferenceValue;
                var preview = (UpgradeIcon)data.FindProperty("installPreview").objectReferenceValue;
                Place(icon.rectTransform, Vector2.zero, Vector2.one * side);
                Place(preview.rectTransform, Vector2.zero, Vector2.one * side);
                slot.gameObject.SetActive(true); Record(slot.gameObject);
            }
            var title = card.transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                Place(title.rectTransform, Pixel(drawing, art.sprite, new Vector2(320f, 505f)),
                    new Vector2(drawing.width * 400f / art.sprite.rect.width, drawing.height * 50f / art.sprite.rect.height));
                title.alignment = TextAlignmentOptions.Center; title.color = new Color(.29f, .20f, .15f);
                title.fontSize = 23f; title.gameObject.SetActive(true); Record(title); Record(title.gameObject);
            }
            Record(art); Record(art.transform); Record(shadow);
        }

        private static void ConfigureBar(DraggableCard card)
        {
            var track = (RectTransform)card.transform.Find("ProgressBar");
            var fill = track.Find("BlueProgress").GetComponent<Image>();
            track.anchorMin = track.anchorMax = new Vector2(.5f, 1f);
            track.pivot = new Vector2(.5f, 0f); track.anchoredPosition = new Vector2(0f, 8f);
            track.sizeDelta = new Vector2(282f, 23.5f); track.localScale = Vector3.one;
            var background = track.GetComponent<Image>();
            background.sprite = Sprite(BarArt + "Loading Bar.png");
            background.color = Color.white; background.type = Image.Type.Simple; background.preserveAspect = false;
            background.raycastTarget = false;
            fill.sprite = Sprite(BarArt + "Orange Bar.png");
            // Leave brightness headroom for the existing completion flash.
            fill.color = new Color(.9f, .9f, .9f, 1f);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            fill.fillAmount = 0f; fill.preserveAspect = false; fill.raycastTarget = false;
            Stretch(fill.rectTransform);
            var glow = fill.GetComponent<Outline>();
            if (glow != null) { glow.effectColor = new Color(1f, .79f, .38f, .7f); glow.enabled = false; Record(glow); }
            Assign(card.Station, "progressRoot", track);
            Assign(card.Station, "progressFill", fill);
            Assign(card.Station, "progressFeedback", track.GetComponent<ProgressBarFeedback>());
            track.gameObject.SetActive(false);
            Record(track); Record(track.gameObject); Record(background); Record(fill); Record(fill.rectTransform);
        }

        private static Rect Fit(Rect box, Vector2 source)
        {
            float scale = Mathf.Min(box.width / source.x, box.height / source.y);
            Vector2 size = source * scale;
            return new Rect(box.center - size * .5f, size);
        }
        private static Vector2 Pixel(Rect drawing, Sprite sprite, Vector2 pixel) => drawing.min +
            new Vector2(pixel.x / sprite.rect.width * drawing.width, (1f - pixel.y / sprite.rect.height) * drawing.height);
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; Record(rect);
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
        private static void Assign(Object owner, string field, Object value)
        {
            var data = new SerializedObject(owner); data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo(); Record(owner);
        }
        private static void Record(Object target)
        { if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target); }
    }
}
