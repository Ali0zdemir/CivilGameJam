using System;
using System.Linq;
using CivilGameJam.Disasters;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardSequenceResultsAuthoring
    {
        public static void BindTutorialOutcome(MonsterSequenceResults results)
        {
            var data = new SerializedObject(results);
            var overlay = ((GameObject)data.FindProperty("overlay").objectReferenceValue).transform;
            var content = overlay.Find("Card/Content");
            var label = content.Find("TutorialOutcome")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
                label = Label("TutorialOutcome", content, "", font, 18f, new Color(.96f, .77f, .48f));
                Top(label.rectTransform, 28f, 232f, 28f, 32f);
            }
            label.text = data.FindProperty("tutorialOutcomeMessage").stringValue;
            label.gameObject.SetActive(false);
            data.FindProperty("tutorialOutcomeLabel").objectReferenceValue = label;
            data.FindProperty("peopleRewardLabel").objectReferenceValue = label;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Card Game/Bind Authored Sequence House Reports")]
        public static void BindAuthoredHouseReportsForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bind result cards in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var results = Object.FindFirstObjectByType<MonsterSequenceResults>();
            var sequence = results.GetComponent<MonsterSequenceController>();
            var data = new SerializedObject(results);
            var overlay = ((GameObject)data.FindProperty("overlay").objectReferenceValue).transform;
            var roots = overlay.GetComponentsInChildren<DraggableCard>(true).OrderBy(card => card.Rect.anchoredPosition.x).ToArray();
            var sources = Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(link => link.Building != null && link.House != null).OrderBy(link => link.House.GetComponent<DraggableCard>().InitialGridCell).ToArray();
            if (roots.Length != 4 || sources.Length != 4) throw new InvalidOperationException("Expected four authored result cards and four real houses.");
            var view = results.GetComponent<SequenceHouseResultsView>() ?? results.gameObject.AddComponent<SequenceHouseResultsView>();
            var viewData = new SerializedObject(view);
            viewData.FindProperty("results").objectReferenceValue = results;
            viewData.FindProperty("sequence").objectReferenceValue = sequence;
            var entries = viewData.FindProperty("cards"); entries.arraySize = 4;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("source").objectReferenceValue = sources[i];
                var slots = root.GetComponentsInChildren<HouseUpgradeSlot>(true).OrderBy(slot => slot.Kind).ToArray();
                if (slots.Length != 3) throw new InvalidOperationException("The result card must retain its three authored slots.");
                var icons = entry.FindPropertyRelative("icons"); icons.arraySize = 3;
                for (int n = 0; n < slots.Length; n++)
                {
                    var icon = (UpgradeIcon)new SerializedObject(slots[n]).FindProperty("icon").objectReferenceValue;
                    icons.GetArrayElementAtIndex(n).objectReferenceValue = icon;
                    icon.raycastTarget = false; icon.gameObject.SetActive(false);
                }
                foreach (var preview in root.GetComponentsInChildren<UpgradeIcon>(true).Where(icon => icon.name == "InstallPreview"))
                    preview.gameObject.SetActive(false);
                var title = root.GetComponentsInChildren<TMP_Text>(true).Single(label => label.text == "HOUSE" || label.text == "HOUSE " + (i + 1));
                entry.FindPropertyRelative("title").objectReferenceValue = title;
                var damage = root.transform.Find("EarthquakeDamage");
                if (damage != null) damage.gameObject.SetActive(false);
                entry.FindPropertyRelative("destroyedMarker").objectReferenceValue = damage != null ? damage.gameObject : null;
                var reportTransform = root.transform.Find("HouseReport") as RectTransform;
                if (reportTransform == null)
                {
                    reportTransform = Rect("HouseReport", root.transform);
                    reportTransform.anchorMin = new Vector2(0f, 0f); reportTransform.anchorMax = new Vector2(1f, 0f);
                    reportTransform.pivot = new Vector2(.5f, 1f);
                    reportTransform.anchoredPosition = new Vector2(0f, -10f); reportTransform.sizeDelta = new Vector2(0f, 102f);
                    var label = reportTransform.gameObject.AddComponent<TextMeshProUGUI>();
                    label.font = font; label.fontSize = 22f; label.enableAutoSizing = true;
                    label.fontSizeMin = 16f; label.fontSizeMax = 22f;
                    label.alignment = TextAlignmentOptions.Top; label.color = new Color(.95f, .94f, .88f);
                    label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
                    label.text = "EARTHQUAKE\nSeismic isolator: MISSING\nNo seismic protection.";
                }
                entry.FindPropertyRelative("report").objectReferenceValue = reportTransform.GetComponent<TMP_Text>();
                // These are report illustrations, not extra playable houses or upgrade targets.
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(behaviour is UpgradeIcon) && behaviour.GetType().Namespace != null && behaviour.GetType().Namespace.StartsWith("CivilGameJam"))
                    {
                        behaviour.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                    }
                var group = root.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.interactable = group.blocksRaycasts = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                }
            }
            viewData.ApplyModifiedPropertiesWithoutUndo();
            var points = overlay.Find("Card/Content/ApEarned/Count")?.GetComponent<TMP_Text>();
            if (points != null) data.FindProperty("earnedPointsCount").objectReferenceValue = points;
            data.ApplyModifiedPropertiesWithoutUndo();
            WorkersTutorialAuthoring.BindWorkProgressBars(Object.FindFirstObjectByType<WorkersTutorial>(FindObjectsInactive.Include));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the authored result bindings.");
            Debug.Log("Bound four existing result cards to their real houses, saved four report labels and kept all authored card/panel transforms.");
        }

        [MenuItem("Tools/Card Game/Save Sequence Results Panel")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author sequence results in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var results = Object.FindFirstObjectByType<MonsterSequenceResults>();
            if (results == null) throw new InvalidOperationException("The saved sequence result bindings are required.");
            var data = new SerializedObject(results);
            var overlay = ((GameObject)data.FindProperty("overlay").objectReferenceValue).transform;
            var card = (RectTransform)overlay.Find("Card");
            var content = (RectTransform)card.Find("Content");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
            var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/Casualty Report.png");
            var buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/Casualty Report Button.png");
            var houseSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EfeArts/Small_House_Icon.png");
            if (font == null || panelSprite == null || buttonSprite == null || houseSprite == null)
                throw new InvalidOperationException("Assign the supplied Casualty Report sprites and Pixellari font before authoring.");

            Center(card, new Vector2(3230f, 2200f), Vector2.zero);
            card.localScale = Vector3.one * .74f;
            var background = card.GetComponent<Image>();
            background.sprite = panelSprite; background.type = Image.Type.Simple;
            background.color = Color.white; background.preserveAspect = true; background.raycastTarget = false;
            Stretch(content, 0f); content.localScale = Vector3.one;
            content.GetComponent<Image>().enabled = false;
            overlay.GetComponent<Image>().color = new Color(.035f, .025f, .04f, .72f);
            var shadow = (RectTransform)overlay.Find("Shadow");
            Center(shadow, card.sizeDelta, new Vector2(12f, -12f)); shadow.localScale = card.localScale;
            shadow.GetComponent<Image>().sprite = panelSprite;
            shadow.GetComponent<Image>().color = new Color(0f, 0f, 0f, .28f);
            shadow.GetComponent<Image>().raycastTarget = false;
            content.Find("Accent").gameObject.SetActive(false);

            var cream = new Color(1f, .94f, .84f);
            var ink = new Color(.22f, .13f, .12f);
            var heading = content.Find("Title").GetComponent<TextMeshProUGUI>();
            Place(heading.rectTransform, 210f, 100f, 2810f, 140f);
            Style(heading, font, 108f, 64f, cream); heading.text = "CASUALTY REPORT";
            var outcome = content.Find("Outcome")?.GetComponent<TextMeshProUGUI>() ?? Label("Outcome", content, "", font, 48f, cream);
            Place(outcome.rectTransform, 250f, 282f, 2730f, 66f);
            Style(outcome, font, 48f, 34f, cream);
            outcome.text = "Protect all four houses to clear this stage.";

            var statistic = (RectTransform)content.Find("House_Statistic");
            Place(statistic, 510f, 380f, 710f, 288f); statistic.GetComponent<Image>().enabled = false;
            var caption = statistic.Find("Caption").GetComponent<TextMeshProUGUI>();
            Center(caption.rectTransform, new Vector2(670f, 54f), new Vector2(0f, 90f));
            Style(caption, font, 42f, 28f, ink); caption.text = "EARTHQUAKE - PROTECTED HOUSES";
            var houseIcon = statistic.Find("Small_House_Icon") as RectTransform;
            if (houseIcon == null) { houseIcon = Rect("Small_House_Icon", statistic); houseIcon.gameObject.AddComponent<Image>(); }
            Center(houseIcon, new Vector2(150f, 150f), new Vector2(-185f, -32f));
            houseIcon.GetComponent<Image>().sprite = houseSprite; houseIcon.GetComponent<Image>().color = Color.white;
            houseIcon.GetComponent<Image>().preserveAspect = true; houseIcon.GetComponent<Image>().raycastTarget = false;
            var count = (TMP_Text)data.FindProperty("destroyedCountLabel").objectReferenceValue;
            Center(count.rectTransform, new Vector2(405f, 150f), new Vector2(95f, -32f));
            Style(count, font, 128f, 88f, ink); count.text = "0 / 4";

            var apArea = (RectTransform)content.Find("ApEarned");
            Place(apArea, 2010f, 380f, 710f, 288f); apArea.GetComponent<Image>().enabled = false;
            var apLabel = (TMP_Text)data.FindProperty("earnedPointsLabel").objectReferenceValue;
            Center(apLabel.rectTransform, new Vector2(670f, 54f), new Vector2(0f, 90f));
            Style(apLabel, font, 46f, 30f, ink); apLabel.text = "AP EARNED";
            var apCount = (TMP_Text)data.FindProperty("earnedPointsCount").objectReferenceValue;
            Center(apCount.rectTransform, new Vector2(640f, 150f), new Vector2(0f, -32f));
            Style(apCount, font, 128f, 88f, ink); apCount.text = "+0";

            var view = results.GetComponent<SequenceHouseResultsView>();
            var viewData = new SerializedObject(view);
            var entries = viewData.FindProperty("cards");
            if (entries.arraySize != 4) throw new InvalidOperationException("Keep the four authored real-house report bindings.");
            for (int i = 0; i < 4; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var report = (TMP_Text)entry.FindPropertyRelative("report").objectReferenceValue;
                var root = (RectTransform)report.transform.parent;
                root.SetParent(content, false);
                Center(root, new Vector2(300f, 400f), new Vector2(-1120f + i * 750f, 0f));
                root.localScale = Vector3.one * 1.5f;
                report.rectTransform.anchorMin = report.rectTransform.anchorMax = new Vector2(.5f, 0f);
                report.rectTransform.pivot = new Vector2(.5f, 1f);
                report.rectTransform.anchoredPosition = new Vector2(0f, -16f);
                report.rectTransform.sizeDelta = new Vector2(400f, 150f);
                Style(report, font, 28f, 20f, cream);
                report.textWrappingMode = TextWrappingModes.Normal;
                report.alignment = TextAlignmentOptions.Top;
                if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.RecordPrefabInstancePropertyModifications(root);
                if (PrefabUtility.IsPartOfPrefabInstance(report)) PrefabUtility.RecordPrefabInstancePropertyModifications(report);
            }

            var personReward = (TMP_Text)data.FindProperty("peopleRewardLabel").objectReferenceValue;
            Place(personReward.rectTransform, 250f, 1730f, 2730f, 105f);
            Style(personReward, font, 46f, 30f, cream); personReward.gameObject.SetActive(false);
            var tutorialHint = content.Find("TutorialOutcomeMessage")?.GetComponent<TextMeshProUGUI>()
                ?? Label("TutorialOutcomeMessage", content, "", font, 46f, cream);
            Place(tutorialHint.rectTransform, 250f, 1730f, 2730f, 105f);
            Style(tutorialHint, font, 46f, 30f, cream);
            tutorialHint.text = data.FindProperty("tutorialOutcomeMessage").stringValue;
            tutorialHint.gameObject.SetActive(false);

            // The supplied button PNG is a full-size transparent overlay. Its visible strip is at y=1930..2090.
            var buttonArt = content.Find("CasualtyReportButtonArtwork") as RectTransform;
            if (buttonArt == null) { buttonArt = Rect("CasualtyReportButtonArtwork", content); buttonArt.gameObject.AddComponent<Image>(); }
            Stretch(buttonArt, 0f); buttonArt.localScale = Vector3.one; buttonArt.SetAsFirstSibling();
            var buttonImage = buttonArt.GetComponent<Image>();
            buttonImage.sprite = buttonSprite; buttonImage.type = Image.Type.Simple;
            buttonImage.color = Color.white; buttonImage.preserveAspect = true; buttonImage.raycastTarget = false;
            var retry = (Button)data.FindProperty("retryButton").objectReferenceValue;
            Place((RectTransform)retry.transform, 110f, 1930f, 3010f, 160f);
            var hit = retry.GetComponent<Image>(); hit.sprite = null; hit.type = Image.Type.Simple;
            hit.color = new Color(1f, 1f, 1f, .001f); hit.raycastTarget = true;
            retry.targetGraphic = buttonImage;
            retry.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = retry.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1f, .95f, .87f);
            colors.pressedColor = new Color(.78f, .68f, .60f); colors.selectedColor = Color.white;
            colors.fadeDuration = .12f; retry.colors = colors;
            var retryLabel = retry.GetComponentInChildren<TextMeshProUGUI>(true);
            Stretch(retryLabel.rectTransform, 10f); retryLabel.rectTransform.localScale = Vector3.one;
            Style(retryLabel, font, 76f, 52f, ink); retryLabel.text = "CONTINUE";
            BindVictoryButton(data, retry);

            data.FindProperty("titleLabel").objectReferenceValue = heading;
            data.FindProperty("outcomeLabel").objectReferenceValue = outcome;
            data.FindProperty("houseStatisticCaption").objectReferenceValue = caption;
            data.FindProperty("tutorialOutcomeLabel").objectReferenceValue = tutorialHint;
            data.ApplyModifiedPropertiesWithoutUndo();
            overlay.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the Casualty Report.");
            Debug.Log("Saved Casualty Report artwork, house/AP statistics, four bound house reports and the supplied Continue button.");
        }

        [MenuItem("Tools/Card Game/Save Victory Main Menu Button")]
        public static void SaveVictoryButtonForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save the victory button in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var results = Object.FindFirstObjectByType<MonsterSequenceResults>();
            var data = new SerializedObject(results);
            BindVictoryButton(data, (Button)data.FindProperty("retryButton").objectReferenceValue);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the victory button.");
        }

        private static void BindVictoryButton(SerializedObject data, Button retry)
        {
            var menu = retry.transform.parent.Find("MainMenuButton")?.GetComponent<Button>();
            if (menu == null)
            {
                menu = Object.Instantiate(retry.gameObject, retry.transform.parent, false).GetComponent<Button>();
                menu.name = "MainMenuButton";
            }
            menu.GetComponentInChildren<TMP_Text>(true).text = "MAIN MENU";
            menu.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(menu.onClick, ((MonsterSequenceResults)data.targetObject).ReturnToMainMenu);
            menu.gameObject.SetActive(false);
            data.FindProperty("mainMenuButton").objectReferenceValue = menu;
        }

        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            Center(rect, new Vector2(width, height), new Vector2(left + width * .5f - 1615f, 1100f - top - height * .5f));
            rect.localScale = Vector3.one;
        }

        private static void Style(TMP_Text label, TMP_FontAsset font, float maximum, float minimum, Color color)
        {
            label.font = font; label.fontSize = maximum; label.enableAutoSizing = true;
            label.fontSizeMin = minimum; label.fontSizeMax = maximum;
            label.color = color; label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false; label.rectTransform.localScale = Vector3.one;
        }


        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer; return rect;
        }

        private static Image Fill(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color)
        {
            var rect = Rect(name, parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = size; label.text = text; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true; label.fontSizeMin = 16f; label.fontSizeMax = size;
            return label;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset;
        }

        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }

        private static void Top(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(-left - right, height); rect.anchoredPosition = new Vector2((left - right) * .5f, -top);
        }
    }
}
