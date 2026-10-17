using System.Linq;
using CivilGameJam.Cards;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards.Editor
{
    public static class CardCityLayoutAuthoring
    {
        private static readonly Color Ink = new Color(0.045f, 0.07f, 0.085f);
        private static readonly Color Surface = new Color(0.075f, 0.11f, 0.13f);
        private static readonly Color Cream = new Color(0.91f, 0.94f, 0.92f);
        private static readonly Color Muted = new Color(0.52f, 0.64f, 0.66f);
        private static readonly Color Gold = new Color(0.96f, 0.72f, 0.35f);

        [MenuItem("Tools/Civil Game Jam/Fit City Above Single Card Row")]
        public static void FitCityAboveSingleRow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Arrange the saved city in Edit Mode.");
            var game = Object.FindFirstObjectByType<CardGameController>();
            if (game == null) throw new System.InvalidOperationException("Open 2DCardScene first.");
            var canvas = game.GetComponentInParent<Canvas>();
            var panel = (RectTransform)game.transform;
            var sidebar = (RectTransform)canvas.transform.Find("CitySidebar");
            const float sideWidth = 560f;
            const float outerMargin = 24f;
            float left = outerMargin + sideWidth;
            float designWidth = canvas.GetComponent<CanvasScaler>().referenceResolution.x;
            sidebar.anchorMin = Vector2.zero; sidebar.anchorMax = Vector2.up;
            sidebar.pivot = new Vector2(0f, 0.5f);
            sidebar.sizeDelta = new Vector2(sideWidth, -2f * outerMargin);
            sidebar.anchoredPosition = new Vector2(outerMargin, 0f);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = new Vector2(0f, 0.75f / panel.localScale.y);
            panel.pivot = Vector2.zero;
            panel.sizeDelta = new Vector2((designWidth - left - outerMargin) / panel.localScale.x, 0f);
            panel.anchoredPosition = new Vector2(left, 0f);
            CardResolutionSetup.Configure(game);
            Canvas.ForceUpdateCanvases();
            AlignPeopleWithRows(game);
            var dock = game.GetComponent<CardDock>();
            var activation = dock.ActivationArea;
            activation.anchorMin = activation.anchorMax = activation.pivot = Vector2.zero;
            activation.anchoredPosition = new Vector2(left, 0f);
            activation.sizeDelta = new Vector2(panel.rect.width * panel.localScale.x, dock.CollapsedCanvasHeight + outerMargin);
            var viewport = canvas.transform.Find("CityView").GetComponent<CityViewport>();
            Refs(viewport, ("dock", dock));
            var viewportData = new SerializedObject(viewport);
            viewportData.FindProperty("framingPadding").floatValue = 1.35f;
            viewportData.FindProperty("verticalAlignment").floatValue = 0.9f;
            viewportData.ApplyModifiedPropertiesWithoutUndo();
            viewport.RefreshViewport();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log("Saved a narrower connected sidebar and city framing above the fixed single-row boundary.");
        }

        public static void FitCityAboveSingleRowForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            FitCityAboveSingleRow();
        }

        [UnityEditor.MenuItem("Tools/Civil Game Jam/Enlarge Saved Dock Cards")]
        public static void EnlargeDockCards()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Resize the saved cards in Edit Mode.");
            var game = Object.FindFirstObjectByType<CardGameController>();
            if (game == null) throw new System.InvalidOperationException("Open 2DCardScene first.");
            Canvas.ForceUpdateCanvases();
            var panel = (RectTransform)game.transform;
            var grid = game.Grid.rectTransform;
            var first = game.Grid.GetCell(0);
            float gap = grid.InverseTransformPoint(game.Grid.GetCell(1).position).x -
                grid.InverseTransformPoint(first.position).x - first.rect.width;
            const float scale = 0.64897f * 1.6f * 1.2f;
            var nativeSize = new Vector2(300f, 400f);
            var size = nativeSize * scale;
            float extraWidth = (size.x - first.rect.width) * CardGrid.Columns;
            panel.sizeDelta += new Vector2(extraWidth, 0f);
            // Keep the left edge and the city sidebar in place while using the space on the right.
            if (!Mathf.Approximately(panel.anchorMin.x, 0f))
                panel.anchoredPosition += Vector2.right * (extraWidth * panel.localScale.x * 0.5f);
            CardResolutionSetup.Configure(game);
            Canvas.ForceUpdateCanvases();
            float width = CardGrid.Columns * size.x + (CardGrid.Columns - 1) * gap;
            for (int i = 0; i < CardGrid.CellCount; i++)
            {
                var cell = game.Grid.GetCell(i);
                cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 1f);
                cell.sizeDelta = size;
                cell.anchoredPosition = new Vector2(-width / 2f + size.x / 2f + i % CardGrid.Columns * (size.x + gap),
                    -1f - size.y / 2f - i / CardGrid.Columns * (size.y + gap));
            }
            foreach (var card in game.GetComponentsInChildren<DraggableCard>().Where(c => c.IsBuilding))
            {
                card.Rect.sizeDelta = nativeSize;
                card.SetLayoutScale(Vector3.one * scale);
                card.Rect.anchoredPosition = Vector2.zero;
                PrefabUtility.RecordPrefabInstancePropertyModifications(card.Rect);
            }
            AlignPeopleWithRows(game);
            float topPadding = -((RectTransform)panel.Find("GridPanel")).offsetMax.y;
            float collapsedHeight = topPadding + 1f + size.y + 18f;
            var dock = game.GetComponent<CardDock>();
            var dockData = new SerializedObject(dock);
            dockData.FindProperty("collapsedHeight").floatValue = collapsedHeight;
            dockData.ApplyModifiedPropertiesWithoutUndo();
            dock.ActivationArea.anchoredPosition = panel.anchoredPosition;
            dock.ActivationArea.sizeDelta = new Vector2(panel.rect.width * panel.localScale.x,
                collapsedHeight * panel.localScale.y + 24f);
            foreach (string name in new[] { "ResearchCenterCard", "FactoryCard", "SchoolCard", "HouseCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var card = prefab.GetComponent<DraggableCard>();
                    card.Rect.sizeDelta = nativeSize;
                    card.SetLayoutScale(Vector3.one * scale);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            Canvas.ForceUpdateCanvases();
            Object.FindFirstObjectByType<CityViewport>().RefreshViewport();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
            Debug.Log($"Saved 20% larger 3:4 cards, {gap:F2} row/column gaps and aligned people in the scene and prefabs.");
        }

        public static void EnlargeDockCardsForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            EnlargeDockCards();
        }

        public static void ResizeDockForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            ConfigureDockLayout(game);
            Object.FindFirstObjectByType<CityViewport>().RefreshViewport();
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            CardSceneAuthoring.VerifySavedAssets();
        }

        private static void ConfigureDockLayout(CardGameController game)
        {
            var panel = (RectTransform)game.transform;
            panel.localScale = Vector3.one * 1.1f;
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, 0f);
            CardResolutionSetup.Configure(game);
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < CardGrid.CellCount; i++)
            {
                var cell = game.Grid.GetCell(i);
                cell.anchorMin = cell.anchorMax = new Vector2(0.5f, 1f);
                float gap = game.Grid.GetCell(1).anchoredPosition.x - game.Grid.GetCell(0).anchoredPosition.x - cell.rect.width;
                cell.anchoredPosition = new Vector2(cell.anchoredPosition.x,
                    -1f - cell.rect.height / 2f - i / CardGrid.Columns * (cell.rect.height + gap));
            }
            AlignPeopleWithRows(game);
            game.CardLayer.anchoredPosition = Vector2.zero;
            var activation = game.GetComponent<CardDock>().ActivationArea;
            activation.anchoredPosition = panel.anchoredPosition;
            var dockData = new SerializedObject(game.GetComponent<CardDock>());
            float collapsedHeight = -((RectTransform)panel.Find("GridPanel")).offsetMax.y + game.Grid.CellSize.y + 19f;
            dockData.FindProperty("collapsedHeight").floatValue = collapsedHeight;
            dockData.ApplyModifiedPropertiesWithoutUndo();
            activation.sizeDelta = new Vector2(panel.rect.width * panel.localScale.x, collapsedHeight * panel.localScale.y + 24f);
        }

        private static void AlignPeopleWithRows(CardGameController game)
        {
            if (game.Sidebar != null) return;
            var people = game.GetComponentsInChildren<DraggableCard>().Where(c => c.IsPerson).OrderBy(c => c.name).ToArray();
            for (int i = 0; i < people.Length; i++)
            {
                Vector3 row = game.Grid.GetCell(i / 2 * CardGrid.Columns).position;
                people[i].Rect.anchorMin = people[i].Rect.anchorMax = new Vector2(0f, 1f);
                people[i].Rect.position = row;
                people[i].Rect.anchoredPosition = new Vector2(132f + i % 2 * 224f, people[i].Rect.anchoredPosition.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(people[i].Rect);
            }
        }

        public static void BuildForBatch()
        {
            EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            ConfigureDockLayout(game);
            var panel = (RectTransform)game.transform;
            var canvas = game.GetComponentInParent<Canvas>();
            float panelLeft = panel.anchoredPosition.x - panel.rect.width * panel.localScale.x / 2f;
            var sidebar = Box("CitySidebar", canvas.transform, Ink);
            sidebar.anchorMin = Vector2.zero; sidebar.anchorMax = new Vector2(0.5f, 1f);
            sidebar.offsetMin = new Vector2(24f, 24f); sidebar.offsetMax = new Vector2(panelLeft - 24f, -24f);
            var badge = Box("CityBadge", sidebar, Gold);
            At(badge, new Vector2(0f, 1f), new Vector2(28f, -32f), new Vector2(54f, 54f), new Vector2(0f, 1f));
            var symbol = Label("Symbol", badge, "Ş", 32f, Ink, FontStyles.Bold);
            symbol.anchorMin = Vector2.zero; symbol.anchorMax = Vector2.one; symbol.offsetMin = symbol.offsetMax = Vector2.zero;
            symbol.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            Text("CityTitle", sidebar, "ŞEHİR", 34f, Cream, 102f, -32f, -28f, 42f, FontStyles.Bold);
            Text("CitySubtitle", sidebar, "YERLEŞİM YÖNETİMİ", 15f, Muted, 102f, -78f, -28f, 24f);
            Section(sidebar, "PopulationHeading", "NÜFUS", -156f);
            var normal = Stat(sidebar, "NormalPeople", "Person", "4", 0f, 0.5f, new Color(0.98f, 0.65f, 0.28f));
            var educated = Stat(sidebar, "EducatedPeople", "Educated Person", "0", 0.5f, 1f, new Color(0.39f, 0.74f, 0.92f));
            var total = CountRow(sidebar, "TotalPopulation", "Toplam nüfus", "4", -370f);
            Section(sidebar, "BuildingsHeading", "YAPILAR", -466f);
            var houses = CountRow(sidebar, "Houses", "Evler", "4", -512f);
            var researchCount = CountRow(sidebar, "ResearchCenters", "Araştırma merkezi", "1", -590f);
            var schools = CountRow(sidebar, "Schools", "Okul", "1", -668f);
            var factories = CountRow(sidebar, "Factories", "Fabrika", "1", -746f);
            Section(sidebar, "WorkHeading", "İŞLEMLER", -858f);
            var schoolStatus = Status(sidebar, "SchoolStatus", "EĞİTİM", "Öğrenci bekleniyor", -908f);
            var researchStatus = Status(sidebar, "ResearchStatus", "ARAŞTIRMA", "Araştırmacı bekleniyor", -1020f);
            var factoryStatus = Status(sidebar, "FactoryStatus", "ÜRETİM", "Blueprint bekleniyor", -1132f);
            var footer = Label("CardHint", sidebar, "Kart alanına gelerek\nalt paneli açabilirsin.", 18f, Muted);
            footer.anchorMin = Vector2.zero; footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f); footer.sizeDelta = new Vector2(-56f, 70f); footer.anchoredPosition = new Vector2(0f, 28f);
            var status = sidebar.GetComponent<CityStatusPanel>() ?? sidebar.gameObject.AddComponent<CityStatusPanel>();
            var cards = game.GetComponentsInChildren<DraggableCard>();
            Refs(status, ("game", game), ("normalCount", normal), ("educatedCount", educated), ("populationCount", total),
                ("housesCount", houses), ("researchCount", researchCount), ("schoolCount", schools), ("factoryCount", factories),
                ("schoolStatus", schoolStatus), ("researchStatus", researchStatus), ("factoryStatus", factoryStatus),
                ("school", cards.Single(c => c.Kind == CardKind.School).Station),
                ("research", cards.Single(c => c.Kind == CardKind.ResearchCenter).Station),
                ("factory", cards.Single(c => c.Kind == CardKind.Factory).Station),
                ("production", cards.Single(c => c.Kind == CardKind.Factory).GetComponent<FactoryProduction>()));

            if (!AssetDatabase.IsValidFolder("Assets/RenderTextures")) AssetDatabase.CreateFolder("Assets", "RenderTextures");
            const string texturePath = "Assets/RenderTextures/CityView.renderTexture";
            var preview = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
            if (preview == null)
            {
                preview = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "CityView", antiAliasing = 1 };
                AssetDatabase.CreateAsset(preview, texturePath);
            }
            var view = Rect("CityView", canvas.transform);
            var raw = view.GetComponent<RawImage>() ?? view.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false; raw.texture = preview; raw.color = Color.white;
            view.SetSiblingIndex(1);
            var worldCamera = Camera.main;
            worldCamera.cullingMask &= ~(1 << canvas.gameObject.layer);
            worldCamera.targetTexture = preview; worldCamera.depth = -10f;
            var displayObject = GameObject.Find("Display Camera");
            if (displayObject == null) displayObject = new GameObject("Display Camera", typeof(Camera));
            var display = displayObject.GetComponent<Camera>();
            display.cullingMask = 1 << canvas.gameObject.layer; display.depth = 0f;
            display.clearFlags = CameraClearFlags.SolidColor; display.backgroundColor = Ink;
            display.orthographic = true; display.orthographicSize = 5f;
            display.nearClipPlane = 0.1f; display.farClipPlane = 100f;
            display.transform.position = new Vector3(0f, 0f, -10f);
            var viewport = view.GetComponent<CityViewport>() ?? view.gameObject.AddComponent<CityViewport>();
            Refs(viewport, ("sidebar", sidebar), ("dock", game.GetComponent<CardDock>()), ("cityImage", raw), ("worldCamera", worldCamera),
                ("world", GameObject.Find("World3D").transform), ("previewTexture", preview));
            FitCityAboveSingleRow();
            Debug.Log("Saved 75% screen-height dock with two responsive rows and a city viewport independent of the drawer.");
        }

        private static TextMeshProUGUI Stat(Transform parent, string name, string title, string value, float minX, float maxX, Color accent)
        {
            var box = Box(name, parent, Surface);
            box.anchorMin = new Vector2(minX, 1f); box.anchorMax = new Vector2(maxX, 1f); box.pivot = new Vector2(0.5f, 1f);
            box.offsetMin = new Vector2(minX == 0f ? 28f : 10f, -338f); box.offsetMax = new Vector2(maxX == 1f ? -28f : -10f, -210f);
            Text("Title", box, title, 18f, Muted, 18f, -16f, -18f, 28f);
            return Text("Value", box, value, 40f, accent, 18f, -53f, -18f, 56f, FontStyles.Bold);
        }

        private static TextMeshProUGUI CountRow(Transform parent, string name, string title, string value, float y)
        {
            var box = Box(name, parent, Surface);
            box.anchorMin = new Vector2(0f, 1f); box.anchorMax = Vector2.one; box.pivot = new Vector2(0.5f, 1f);
            box.sizeDelta = new Vector2(-56f, 68f); box.anchoredPosition = new Vector2(0f, y);
            var label = Text("Title", box, title, 20f, Cream, 20f, -16f, -80f, 36f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var count = Label("Value", box, value, 27f, Gold, FontStyles.Bold);
            count.anchorMin = count.anchorMax = new Vector2(1f, 0.5f); count.pivot = new Vector2(1f, 0.5f);
            count.sizeDelta = new Vector2(54f, 42f); count.anchoredPosition = new Vector2(-18f, 0f); count.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;
            return count.GetComponent<TextMeshProUGUI>();
        }

        private static TextMeshProUGUI Status(Transform parent, string name, string title, string value, float y)
        {
            var box = Box(name, parent, Surface);
            box.anchorMin = new Vector2(0f, 1f); box.anchorMax = Vector2.one; box.pivot = new Vector2(0.5f, 1f);
            box.sizeDelta = new Vector2(-56f, 98f); box.anchoredPosition = new Vector2(0f, y);
            Text("Title", box, title, 16f, Gold, 20f, -13f, -20f, 24f, FontStyles.Bold);
            return Text("Value", box, value, 19f, Cream, 20f, -45f, -20f, 40f);
        }

        private static void Section(Transform parent, string name, string title, float y)
        { Text(name, parent, title, 18f, Muted, 28f, y, -28f, 28f, FontStyles.Bold); }

        private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color,
            float left, float y, float right, float height, FontStyles style = FontStyles.Normal)
        {
            var rect = Label(name, parent, value, size, color, style);
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, y - height); rect.offsetMax = new Vector2(right, y);
            return rect.GetComponent<TextMeshProUGUI>();
        }

        private static RectTransform Label(string name, Transform parent, string value, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var rect = Rect(name, parent);
            var label = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = value; label.font = TMP_Settings.defaultFontAsset; label.fontSize = size;
            label.color = color; label.fontStyle = style; label.raycastTarget = false;
            label.enableAutoSizing = true; label.fontSizeMax = size; label.fontSizeMin = size * 0.72f;
            label.textWrappingMode = TextWrappingModes.Normal; label.alignment = TextAlignmentOptions.TopLeft;
            return rect;
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null) rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        private static void At(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }

        private static void Refs(Object component, params (string name, Object value)[] values)
        {
            var data = new SerializedObject(component);
            foreach (var value in values) data.FindProperty(value.name).objectReferenceValue = value.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
