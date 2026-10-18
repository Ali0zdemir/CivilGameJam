using System;
using System.Linq;
using System.IO;
using CivilGameJam.Cards;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class WorkersTutorialAuthoring
    {
        private static readonly Color Paper = new Color32(235, 237, 231, 255);
        private static readonly Color Gold = new Color32(244, 197, 121, 255);
        private static readonly Color Muted = new Color32(166, 180, 190, 255);
        private static Sprite PanelSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");

        private const string SpotlightAssets = "Assets/TutorialInfo/Spotlight";

        public static void BindWorkProgressBars(WorkersTutorial tutorial)
        {
            var tutorialData = new SerializedObject(tutorial);
            var data = new SerializedObject(tutorial.Spotlight);
            var bars = data.FindProperty("progressBars"); bars.arraySize = 3;
            var names = new[] { "research", "factory", "school" };
            for (int i = 0; i < names.Length; i++)
            {
                var station = (BuildingStation)tutorialData.FindProperty(names[i]).objectReferenceValue;
                Object source = names[i] == "factory" ? (Object)station.GetComponent<FactoryProduction>() : station;
                bars.GetArrayElementAtIndex(i).objectReferenceValue = new SerializedObject(source).FindProperty("progressRoot").objectReferenceValue;
                if (bars.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException("The authored progress bar is missing for " + names[i]);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ExtendResearchProductionForBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
            var data = new SerializedObject(tutorial);
            ConfigureResearchProduction(data);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureResearchProduction(SerializedObject data)
        {
            var lessons = data.FindProperty("lessons"); lessons.arraySize = 10;
            Lesson(lessons.GetArrayElementAtIndex(2), "EDUCATED PEOPLE", "Your educated person is ready",
                "Send your educated person to Research Center.\nThey research faster and spend less AP.", "");
            Lesson(lessons.GetArrayElementAtIndex(3), "RESEARCH", "Research disaster protection",
                "Drag your educated person onto Research Center.\nResearch creates blueprints for disaster protection.", "");
            Lesson(lessons.GetArrayElementAtIndex(4), "SEISMIC BLUEPRINT", "Choose earthquake protection",
                "Select Rail System, the seismic blueprint.\nIt helps protect houses from earthquakes.", "");
            Lesson(lessons.GetArrayElementAtIndex(5), "FACTORY BLUEPRINT", "Turn a blueprint into an upgrade",
                "Drag the seismic blueprint from the Vault to Factory.\nFactory uses the plan to make the real upgrade.", "");
            Lesson(lessons.GetArrayElementAtIndex(6), "FACTORY PEOPLE", "Assign a person to Factory",
                "Drag the glowing fresh person onto Factory.\nThis job costs {normalFactory} AP and produces two seismic items.", "");
            Lesson(lessons.GetArrayElementAtIndex(7), "READY", "Your seismic upgrades are ready",
                "Next, drag these items onto any two houses you choose.\nWatch each upgrade in the 3D city.", "");
            Lesson(lessons.GetArrayElementAtIndex(8), "PROTECT HOUSES", "Protect the houses you choose",
                "Drag each seismic item onto a different house.\nWatch the 3D installation after each upgrade.", "");
            Lesson(lessons.GetArrayElementAtIndex(9), "READY", "Face the earthquake",
                "When you run out of AP or feel ready, press READY\nto end your turn and face the disaster.", "");
            ConfigureResultLessons(data);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SaveReadyTutorialCopyForBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var tutorial = Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include);
            var data = new SerializedObject(tutorial);
            data.FindProperty("lessons").GetArrayElementAtIndex(9).FindPropertyRelative("description").stringValue =
                "When you run out of AP or feel ready, press READY\nto end your turn and face the disaster.";
            data.ApplyModifiedPropertiesWithoutUndo();
            CardSequenceResultsAuthoring.BindTutorialOutcome(Object.FindFirstObjectByType<CivilGameJam.Disasters.MonsterSequenceResults>());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save tutorial READY/result copy.");
        }

        public static void SaveResultLessonsForBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var tutorial = Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include);
            var data = new SerializedObject(tutorial);
            ConfigureResultLessons(data);
            data.ApplyModifiedPropertiesWithoutUndo();
            // The existing tutorial must also render above the authored results canvas.
            tutorial.GetComponent<Canvas>().sortingOrder = 2100;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save result tutorial lessons.");
        }

        private static void ConfigureResultLessons(SerializedObject data)
        {
            var lessons = data.FindProperty("lessons"); lessons.arraySize = 12;
            Lesson(lessons.GetArrayElementAtIndex(10), "AP REWARDS", "Protected houses earn AP",
                "{safeHouses} protected houses x {houseReward} AP = {survivalReward} AP.\nRound bonus: {roundBonus} AP. Total earned: +{earnedAP} AP.", "");
            Lesson(lessons.GetArrayElementAtIndex(11), "NEXT DISASTER", "Protect every house to move on",
                "Make all four houses earthquake-resistant.\nWhen every house survives, you can face the next disaster.", "");
        }

        public static void ExtendHouseInstallationForBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
            ConfigureResearchProduction(new SerializedObject(tutorial));
            var house = Object.FindFirstObjectByType<HouseUpgrades>();
            var artwork = TutorialSpotlight.HouseArtwork(house);
            if (artwork == null) throw new InvalidOperationException("The authored house has no silhouette artwork.");
            var data = new SerializedObject(tutorial.Spotlight);
            var bindings = data.FindProperty("silhouettes");
            bool found = false; int bindingIndex = -1;
            for (int i = 0; i < bindings.arraySize; i++)
                if (bindings.GetArrayElementAtIndex(i).FindPropertyRelative("artwork").objectReferenceValue == artwork.sprite)
                {
                    bindingIndex = i;
                    found = ((Sprite)bindings.GetArrayElementAtIndex(i).FindPropertyRelative("mask").objectReferenceValue).name != "HouseSilhouette";
                    break;
                }
            if (!found)
            {
                var layers = new[] { "İçBeyaz", "DışKenar", "Klasör" }
                    .Select(name => artwork.transform.parent.Find(name)?.GetComponent<Image>()).Where(image => image != null && image.sprite != null && image.sprite.rect.size == artwork.sprite.rect.size)
                    .Select(image => image.sprite).ToArray();
                if (layers.Length == 0) layers = new[] { artwork.sprite };
                var mask = BakeMask("HouseSilhouette", layers, true);
                if (bindingIndex < 0) { bindingIndex = bindings.arraySize; bindings.arraySize++; }
                var entry = bindings.GetArrayElementAtIndex(bindingIndex);
                entry.FindPropertyRelative("artwork").objectReferenceValue = artwork.sprite;
                entry.FindPropertyRelative("mask").objectReferenceValue = mask;
                entry.FindPropertyRelative("contentUV").rectValue = new Rect(8f / mask.rect.width, 8f / mask.rect.height,
                    artwork.sprite.rect.width / mask.rect.width, artwork.sprite.rect.height / mask.rect.height);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Saved house installation and READY lessons. House alpha artwork: " + artwork.sprite.name + "; reused mask: " + found);
        }

        public static void FitAuthoredTutorialForBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
            var spotlight = tutorial.Spotlight;
            var game = Object.FindFirstObjectByType<CardGameController>();
            var artwork = new[] { CardKind.ResearchCenter, CardKind.Factory, CardKind.School }
                .Select(kind => game.GetComponentsInChildren<DraggableCard>().Single(card => card.Kind == kind)
                    .transform.Find("FolderArt/DışKenar").GetComponent<Image>()).ToList();
            var feedback = new SerializedObject(Object.FindFirstObjectByType<AbilityPointFeedback>());
            artwork.Add(((RectTransform)feedback.FindProperty("counter").objectReferenceValue).GetComponent<Image>());
            if (artwork.Any(image => image == null || image.sprite == null)) throw new InvalidOperationException("A saved card/AP artwork reference is missing.");
            var folder = artwork[0].transform.parent;
            var folderSprites = new[] { "İçBeyaz", "DışKenar", "Klasör" }
                .Select(name => folder.Find(name).GetComponent<Image>().sprite).ToArray();
            var normal = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/People/People Card Small Revised.png");
            var educated = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/People/Educated People Card Small.png");
            var originals = new[] { artwork[0].sprite, artwork[3].sprite, normal, educated };
            var masks = new[] { BakeMask("FolderSilhouette", folderSprites), BakeMask("APSilhouette", new[] { originals[1] }),
                BakeMask("PersonSilhouette", new[] { normal }), BakeMask("EducatedSilhouette", new[] { educated }) };
            var data = new SerializedObject(spotlight);
            Array(data.FindProperty("targetArtwork"), artwork.Cast<Object>().ToArray());
            Ref(data, "contourMaterial", SavedMaterial("TutorialContour", "CivilGameJam/UI/Tutorial Contour"));
            var bindings = data.FindProperty("silhouettes"); bindings.arraySize = originals.Length;
            for (int i = 0; i < originals.Length; i++)
            {
                var entry = bindings.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("artwork").objectReferenceValue = originals[i];
                entry.FindPropertyRelative("mask").objectReferenceValue = masks[i];
                var rect = masks[i].rect;
                entry.FindPropertyRelative("contentUV").rectValue = new Rect(8f / rect.width, 8f / rect.height,
                    originals[i].rect.width / rect.width, originals[i].rect.height / rect.height);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            spotlight.material = SavedMaterial("TutorialDimmer", "CivilGameJam/UI/Tutorial Dimmer");
            var contour = (Material)data.FindProperty("contourMaterial").objectReferenceValue;
            var savedFrames = data.FindProperty("frames");
            for (int i = 0; i < 5; i++)
            {
                var image = (Image)(i == 4 ? data.FindProperty("personFrame").objectReferenceValue : savedFrames.GetArrayElementAtIndex(i).objectReferenceValue);
                image.sprite = masks[i == 4 ? 2 : i == 3 ? 1 : 0];
                image.material = contour; image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = false; image.fillCenter = true;
            }
            var lessonData = new SerializedObject(tutorial);
            var lessons = lessonData.FindProperty("lessons");
            lessons.GetArrayElementAtIndex(1).FindPropertyRelative("description").stringValue =
                "Drag the glowing normal person onto School.\nTraining costs {school} AP.";
            lessons.GetArrayElementAtIndex(2).FindPropertyRelative("description").stringValue =
                "Educated people spend less AP and work faster.\nUse them in Research Center or Factory.";
            lessonData.FindProperty("trainingDescription").stringValue = "School is educating this person.\nWait for the educated person to return.";
            lessonData.ApplyModifiedPropertiesWithoutUndo();
            Canvas.ForceUpdateCanvases();
            spotlight.SetLessonFocus(TutorialSpotlight.Workplaces.All, false, null);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Saved alpha silhouettes and contour materials. The authored LessonPanel and its remaining children are untouched.");
        }

        private static Material SavedMaterial(string filename, string shaderName)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SpotlightAssets + "/" + filename + ".shader");
            if (shader == null || shader.name != shaderName || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Tutorial shader failed to import: " + shaderName + " " +
                    (shader != null ? string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(message => message.message + " at " + message.line)) : "Missing shader asset"));
            var path = SpotlightAssets + "/" + filename + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            return material;
        }

        private static Sprite BakeMask(string name, Sprite[] layers, bool fillInterior = false)
        {
            const int border = 8;
            int width = Mathf.RoundToInt(layers[0].rect.width), height = Mathf.RoundToInt(layers[0].rect.height);
            var alpha = new byte[width * height];
            foreach (var sprite in layers)
            {
                if (sprite.rect.width != width || sprite.rect.height != height)
                    throw new InvalidOperationException("Card silhouette layers must share their artwork dimensions.");
                var texture = sprite.texture;
                var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var old = RenderTexture.active;
                var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
                try
                {
                    Graphics.Blit(texture, target); RenderTexture.active = target;
                    readable.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readable.Apply();
                    var pixels = readable.GetPixels32(); var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                    var padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                    float contentWidth = width - padding.x - padding.z, contentHeight = height - padding.y - padding.w;
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        // Unity trims transparent sprite margins. Restore those margins when
                        // baking, rather than stretching the trimmed portrait/flap to the full card.
                        if (x < padding.x || x >= width - padding.z || y < padding.y || y >= height - padding.w) continue;
                        int index = y * width + x;
                        int sx = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(uv.x, uv.z, (x - padding.x + .5f) / contentWidth) * texture.width), 0, texture.width - 1);
                        int sy = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(uv.y, uv.w, (y - padding.y + .5f) / contentHeight) * texture.height), 0, texture.height - 1);
                        byte a = pixels[sy * texture.width + sx].a;
                        alpha[index] = (byte)Mathf.Max(alpha[index], a);
                    }
                }
                finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(readable); }
            }
            if (fillInterior)
            {
                // The house frame has a transparent paper window. Keep the frame's
                // outer contour, and illuminate the contents inside that window too.
                for (int y = 0; y < height; y++)
                {
                    int left = 0, right = width - 1;
                    while (left < width && alpha[y * width + left] <= 25) left++;
                    while (right >= left && alpha[y * width + right] <= 25) right--;
                    for (int x = left; x <= right; x++) alpha[y * width + x] = 255;
                }
            }
            var mask = new Texture2D(width + border * 2, height + border * 2, TextureFormat.RGBA32, false, true);
            var result = new Color32[mask.width * mask.height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                result[(y + border) * mask.width + x + border] = new Color32(255, 255, 255, alpha[y * width + x]);
            mask.SetPixels32(result); mask.Apply();
            var path = SpotlightAssets + "/" + name + ".png";
            File.WriteAllBytes(path, mask.EncodeToPNG()); Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = true; importer.sRGBTexture = false; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void UpgradeHandsOnForBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var tutorial = Object.FindFirstObjectByType<WorkersTutorial>();
            ConfigureHandsOn(tutorial, tutorial.Spotlight);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Saved hands-on School lesson bindings and the additional person spotlight. Existing UI geometry is unchanged.");
        }

        private static void ConfigureHandsOn(WorkersTutorial tutorial, TutorialSpotlight spotlight)
        {
            var highlights = tutorial.transform.Find("Overlay/WorkplaceHighlights");
            var person = highlights.Find("PersonFocus");
            var personFrame = person != null ? person.GetComponent<Image>() : Image(Rect("PersonFocus", highlights), Gold);
            personFrame.fillCenter = false; personFrame.pixelsPerUnitMultiplier = .8f;
            personFrame.gameObject.SetActive(false);
            var spotData = new SerializedObject(spotlight);
            Ref(spotData, "personFrame", personFrame);
            var game = Object.FindFirstObjectByType<CardGameController>();
            Ref(spotData, "peoplePanel", game.Sidebar.PeopleArea.parent);
            spotData.ApplyModifiedPropertiesWithoutUndo();
            var data = new SerializedObject(tutorial);
            Ref(data, "skip", tutorial.transform.Find("Overlay/LessonPanel/Navigation/Skip").GetComponent<Button>());
            var lessons = data.FindProperty("lessons");
            Lesson(lessons.GetArrayElementAtIndex(1), "TRY IT YOURSELF", "Educate your first person",
                "Drag the glowing normal person onto School.\nTraining costs {school} AP. Wait for your educated person to come back.",
                "Drag the glowing person to the glowing School.");
            Lesson(lessons.GetArrayElementAtIndex(2), "EDUCATED PEOPLE", "Your educated person is ready",
                "This person has returned from School.\nEducated people spend less AP in Research Center and Factory, and work faster.",
                "Use this educated person for one job this turn.");
            data.ApplyModifiedPropertiesWithoutUndo();
            ConfigureResearchProduction(new SerializedObject(tutorial));
        }

        public static void RepairSpotlightForBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var spotlight = Object.FindFirstObjectByType<TutorialSpotlight>();
            if (spotlight.GetComponent<CanvasRenderer>() == null) spotlight.gameObject.AddComponent<CanvasRenderer>();
            Canvas.ForceUpdateCanvases(); spotlight.RefreshLayout();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        public static void BuildForBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            if (Object.FindFirstObjectByType<WorkersTutorial>() != null) throw new InvalidOperationException("The tutorial already exists; edit its saved UI instead of rebuilding it.");
            var game = Object.FindFirstObjectByType<CardGameController>();
            var canvas = game.GetComponentInParent<Canvas>().rootCanvas;
            var root = Rect("WorkersTutorial", canvas.transform); Stretch(root);
            var overlayCanvas = root.gameObject.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true; overlayCanvas.sortingOrder = 2000;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var tutorial = root.gameObject.AddComponent<WorkersTutorial>();
            var overlay = Rect("Overlay", root); Stretch(overlay);
            var layer = overlay.gameObject.AddComponent<CanvasGroup>();
            var maskRect = Rect("SpotlightDimmer", overlay); Stretch(maskRect);
            var spotlight = maskRect.gameObject.AddComponent<TutorialSpotlight>();
            spotlight.color = new Color(0.015f, .025f, .04f, .88f); spotlight.raycastTarget = true;
            var highlights = Rect("WorkplaceHighlights", overlay); Stretch(highlights);
            var stations = new[] { CardKind.ResearchCenter, CardKind.Factory, CardKind.School }
                .Select(kind => game.GetComponentsInChildren<DraggableCard>().Single(card => card.Kind == kind).GetComponent<BuildingStation>()).ToArray();
            var targets = stations.Select(station => (RectTransform)station.transform).ToList();
            var feedback = Object.FindFirstObjectByType<AbilityPointFeedback>();
            targets.Add(((Image)new SerializedObject(feedback).FindProperty("counterGlow").objectReferenceValue).rectTransform);
            Color[] accents = { new Color32(83, 185, 255, 200), new Color32(188, 151, 255, 200), new Color32(120, 222, 165, 200), Gold };
            string[] frameNames = { "ResearchCenterFocus", "FactoryFocus", "SchoolFocus", "AbilityPointsFocus" };
            var frames = new Image[4];
            for (int i = 0; i < frames.Length; i++)
            {
                var rect = Rect(frameNames[i], highlights);
                frames[i] = Image(rect, accents[i]); frames[i].fillCenter = false;
                frames[i].pixelsPerUnitMultiplier = .8f;
            }
            var spotData = new SerializedObject(spotlight);
            Array(spotData.FindProperty("targets"), targets.Cast<Object>().ToArray());
            Array(spotData.FindProperty("frames"), frames.Cast<Object>().ToArray());
            spotData.ApplyModifiedPropertiesWithoutUndo();

            var panel = Rect("LessonPanel", overlay);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0f); panel.pivot = new Vector2(.5f, 0f);
            panel.sizeDelta = new Vector2(2200f, 560f); panel.anchoredPosition = new Vector2(0f, 64f);
            Image(panel, new Color32(19, 25, 35, 255), true);
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .48f); shadow.effectDistance = new Vector2(0f, -14f);
            var border = Rect("Border", panel); Stretch(border);
            var borderImage = Image(border, new Color32(123, 127, 117, 180));
            borderImage.fillCenter = false; borderImage.pixelsPerUnitMultiplier = 1.2f;
            var accent = Rect("GoldAccent", panel); At(accent, new Vector2(0f, 1f), new Vector2(72f, -40f), new Vector2(66f, 7f), Vector2.up);
            Image(accent, Gold).sprite = null;
            var stepLabel = Label("Chapter", panel, "01 / 03   GETTING STARTED", 28f, Gold,
                new Vector2(160f, -26f), new Vector2(1000f, 55f));
            var copyRoot = Rect("LessonContent", panel); Stretch(copyRoot);
            var copyGroup = copyRoot.gameObject.AddComponent<CanvasGroup>(); copyGroup.blocksRaycasts = false;
            var heading = Label("Title", copyRoot, "Your people get things done", 64f, Paper,
                new Vector2(72f, -96f), new Vector2(1310f, 94f));
            var body = Label("Description", copyRoot,
                "Drag a person onto School, Research Center or Factory to assign work.\nEach building holds up to two people.",
                40f, Paper, new Vector2(72f, -204f), new Vector2(1300f, 178f));
            body.lineSpacing = 5f;
            var note = Label("Note", copyRoot, "Train people. Discover blueprints. Craft upgrades.", 30f, Muted,
                new Vector2(72f, -378f), new Vector2(1320f, 44f));
            var divider = Rect("ShowcaseDivider", panel);
            At(divider, new Vector2(0f, 1f), new Vector2(1440f, -98f), new Vector2(2f, 320f), Vector2.up);
            Image(divider, new Color32(66, 78, 90, 160)).sprite = null;
            var showcase = Rect("Showcase", copyRoot);
            At(showcase, new Vector2(1f, .5f), new Vector2(-376f, 50f), new Vector2(640f, 350f), new Vector2(.5f, .5f));
            var roles = Rect("WorkplaceRoles", showcase); Stretch(roles);
            Role(roles, "School", "TRAIN EDUCATED PEOPLE", 0f, accents[2]);
            Role(roles, "Research Center", "DISCOVER BLUEPRINTS", -112f, accents[0]);
            Role(roles, "Factory", "CRAFT UPGRADE ITEMS", -224f, accents[1]);
            var ap = Rect("AbilityPoints", showcase); Stretch(ap);
            var apTitle = Label("Title", ap, "ABILITY POINTS", 30f, Gold, new Vector2(20f, -6f), new Vector2(600f, 55f));
            apTitle.alignment = TextAlignmentOptions.Center;
            var apPlate = Rect("CounterPreview", ap);
            At(apPlate, new Vector2(.5f, .5f), new Vector2(0f, 8f), new Vector2(480f, 210f), Vector2.one * .5f);
            var apImage = Image(apPlate, Color.white);
            apImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/AP Ui.png");
            apImage.type = UnityEngine.UI.Image.Type.Simple; apImage.preserveAspect = true;
            var budget = Label("Budget", apPlate, "10 AP", 84f, new Color32(35, 70, 7, 255), Vector2.zero, Vector2.zero);
            Stretch(budget.rectTransform); budget.alignment = TextAlignmentOptions.Center;
            var caption = Label("Caption", ap, "Plan before you spend.", 30f, Muted,
                new Vector2(20f, -284f), new Vector2(600f, 60f)); caption.alignment = TextAlignmentOptions.Center;
            var education = Rect("Education", showcase); Stretch(education);
            var schoolPrice = Label("SchoolPrice", education, "SCHOOL  -2 AP", 28f, accents[2],
                new Vector2(20f, 6f), new Vector2(600f, 45f)); schoolPrice.alignment = TextAlignmentOptions.Center;
            Person(education, "NormalPerson", "People Card Small Revised", -176f, "NORMAL", new Color32(218, 180, 126, 255));
            Person(education, "EducatedPerson", "Educated People Card Small", 176f, "EDUCATED", accents[1]);
            var arrow = Label("TrainingArrow", education, ">", 64f, Gold, new Vector2(270f, -105f), new Vector2(100f, 80f));
            arrow.alignment = TextAlignmentOptions.Center;
            var track = Rect("TrainingTrack", education);
            At(track, Vector2.one * .5f, new Vector2(0f, 37f), new Vector2(160f, 12f), Vector2.one * .5f);
            var spark = Rect("Spark", track); spark.sizeDelta = Vector2.one * 10f;
            Image(spark, new Color32(245, 221, 171, 190)).sprite = null;
            var comparison = Label("CostComparison", education, "RESEARCH   4 AP  >  2 AP\nFACTORY     5 AP  >  3 AP", 29f,
                Muted, new Vector2(10f, -268f), new Vector2(620f, 92f));
            comparison.alignment = TextAlignmentOptions.Center; comparison.lineSpacing = 5f;

            var controls = Rect("Navigation", panel); Stretch(controls);
            var back = Button("Back", controls, "<  BACK", new Vector2(0f, 0f), new Vector2(72f, 40f), new Vector2(240f, 76f), false);
            var skip = Button("Skip", controls, "SKIP TUTORIAL", new Vector2(.5f, 0f), new Vector2(-75f, 40f), new Vector2(390f, 76f), false);
            var next = Button("Continue", controls, "CONTINUE  >", new Vector2(1f, 0f), new Vector2(-72f, 34f), new Vector2(440f, 90f), true);
            back.GetComponent<RectTransform>().pivot = Vector2.zero;
            next.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            skip.GetComponent<RectTransform>().pivot = new Vector2(.5f, 0f);
            UnityEventTools.AddPersistentListener(next.onClick, tutorial.Next);
            UnityEventTools.AddPersistentListener(back.onClick, tutorial.Back);
            UnityEventTools.AddPersistentListener(skip.onClick, tutorial.Skip);
            foreach (var button in new[] { next, back, skip })
                button.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = button == next ? skip : next, selectOnRight = next, selectOnUp = next, selectOnDown = skip };
            var dots = new Image[3];
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = Rect("Step" + (i + 1), panel);
                At(dot, new Vector2(1f, 1f), new Vector2(-180f + i * 42f, -48f), new Vector2(24f, 10f), Vector2.one * .5f);
                dots[i] = Image(dot, i == 0 ? Gold : new Color32(62, 72, 84, 255)); dots[i].sprite = null;
            }
            var data = new SerializedObject(tutorial);
            Ref(data, "game", game); Ref(data, "dock", game.GetComponent<CardDock>());
            Ref(data, "research", stations[0]); Ref(data, "factory", stations[1]); Ref(data, "school", stations[2]);
            Ref(data, "layer", layer); Ref(data, "panel", panel); Ref(data, "copy", copyGroup); Ref(data, "spotlight", spotlight);
            Ref(data, "chapter", stepLabel); Ref(data, "title", heading); Ref(data, "description", body); Ref(data, "note", note);
            Ref(data, "budget", budget); Ref(data, "schoolPrice", schoolPrice); Ref(data, "comparison", comparison);
            Ref(data, "next", next); Ref(data, "back", back); Ref(data, "nextLabel", next.GetComponentInChildren<TMP_Text>());
            Ref(data, "schoolFrame", frames[2]); Ref(data, "trainingSpark", spark);
            Array(data.FindProperty("showcases"), new Object[] { roles.gameObject, ap.gameObject, education.gameObject });
            Array(data.FindProperty("dots"), dots.Cast<Object>().ToArray());
            var lessons = data.FindProperty("lessons"); lessons.arraySize = 3;
            Lesson(lessons.GetArrayElementAtIndex(0), "GETTING STARTED", "Your people get things done",
                "Drag a person onto School, Research Center or Factory to assign work.\nEach building holds up to two people.",
                "Train people. Discover blueprints. Craft upgrades.");
            Lesson(lessons.GetArrayElementAtIndex(1), "ABILITY POINTS", "Every job spends AP",
                "Ability Points (AP) pay for work when an operation starts.\nSchool costs {school} AP. Factory production also needs a discovered blueprint.",
                "Check the prices on each building before you commit.");
            Lesson(lessons.GetArrayElementAtIndex(2), "EDUCATED PEOPLE", "Education makes AP go further",
                "Send a normal person to School to become an educated person.\nEducated people spend less AP in Research Center and Factory, and work faster.",
                "Training costs AP now and makes the next job cheaper.");
            data.ApplyModifiedPropertiesWithoutUndo();
            ap.gameObject.SetActive(false); education.gameObject.SetActive(false); back.gameObject.SetActive(false);
            ConfigureHandsOn(tutorial, spotlight);
            Canvas.ForceUpdateCanvases(); spotlight.RefreshLayout();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Saved all ten workers, research, production, house and READY tutorial lessons under Canvas/WorkersTutorial.");
        }

        private static void Lesson(SerializedProperty entry, string chapter, string title, string description, string note)
        {
            entry.FindPropertyRelative("chapter").stringValue = chapter; entry.FindPropertyRelative("title").stringValue = title;
            entry.FindPropertyRelative("description").stringValue = description; entry.FindPropertyRelative("note").stringValue = note;
        }
        private static void Ref(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
        private static void Array(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer; rect.SetParent(parent, false);
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = rect.anchoredPosition = Vector2.zero;
        }
        private static void At(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position;
        }
        private static Image Image(RectTransform rect, Color color, bool blocks = false)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = PanelSprite; image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = color; image.raycastTarget = blocks; return image;
        }
        private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, Vector2 position, Vector2 dimensions)
        {
            var rect = Rect(name, parent); At(rect, Vector2.up, position, dimensions, Vector2.up);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = Font; label.fontSize = size;
            label.enableAutoSizing = false; label.color = color; label.text = text; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.TopLeft; label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow; return label;
        }
        private static void Role(Transform parent, string title, string subtitle, float y, Color accent)
        {
            var stripe = Rect(title + "Accent", parent);
            At(stripe, Vector2.up, new Vector2(20f, -12f + y), new Vector2(6f, 75f), Vector2.up);
            Image(stripe, accent).sprite = null;
            Label(title, parent, title, 42f, accent, new Vector2(56f, y), new Vector2(550f, 58f));
            Label(title + "Purpose", parent, subtitle, 25f, Muted, new Vector2(56f, y - 62f), new Vector2(550f, 40f));
        }
        private static void Person(Transform parent, string name, string art, float x, string caption, Color color)
        {
            var rect = Rect(name, parent);
            At(rect, Vector2.one * .5f, new Vector2(x, 42f), Vector2.one * 180f, Vector2.one * .5f);
            var image = Image(rect, Color.white); image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = true;
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/People/" + art + ".png");
            var label = Label("Caption", rect, caption, 28f, color, Vector2.zero, new Vector2(250f, 45f));
            At(label.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, -12f), new Vector2(250f, 45f), new Vector2(.5f, 1f));
            label.alignment = TextAlignmentOptions.Center;
        }
        private static Button Button(string name, Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, bool primary)
        {
            var rect = Rect(name, parent); At(rect, anchor, position, size, Vector2.zero);
            var image = Image(rect, primary ? Gold : new Color32(26, 35, 47, 255), true);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1f, .94f, .8f); colors.pressedColor = new Color(.8f, .8f, .8f);
            colors.fadeDuration = .12f; button.colors = colors;
            var label = Label("Label", rect, text, 32f, primary ? new Color32(28, 33, 35, 255) : Paper, Vector2.zero, Vector2.zero);
            Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
