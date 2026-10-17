using System;
using System.Linq;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CivilGameJam.Cards.Editor
{
    public static class CardAbilityPointAuthoring
    {
        [MenuItem("Tools/Card Game/Save First Earthquake AP Round")]
        public static void SaveForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save AP controls in Edit Mode.");
            foreach (var name in new[] { "ResearchCenterCard", "FactoryCard", "SchoolCard", "HouseCard" })
            {
                string path = "Assets/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var station = root.GetComponent<BuildingStation>();
                    if (station != null)
                    {
                        var data = new SerializedObject(station);
                        int normal = name == "SchoolCard" ? 2 : name == "FactoryCard" ? 5 : 4;
                        int educated = name == "FactoryCard" ? 3 : name == "SchoolCard" ? 0 : 2;
                        data.FindProperty("normalAbilityPointCost").intValue = normal;
                        data.FindProperty("educatedAbilityPointCost").intValue = educated;
                        Assign(data, "normalCostLabel", root.transform.Find("FolderArt/APNormalPeople").GetComponentInChildren<TMP_Text>(true));
                        Assign(data, "educatedCostLabel", root.transform.Find("FolderArt/APEducated").GetComponentInChildren<TMP_Text>(true));
                        data.ApplyModifiedPropertiesWithoutUndo();
                        station.RefreshAbilityCostLabels();
                    }
                    else
                    {
                        var label = Label("EarthquakeDamage", root.transform, "DESTROYED", 28, new Color(1f, .48f, .38f));
                        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
                        label.rectTransform.sizeDelta = new Vector2(270f, 54f); label.rectTransform.anchoredPosition = new Vector2(0f, 5f);
                        label.alignment = TextAlignmentOptions.Center;
                        label.gameObject.SetActive(false);
                        var data = new SerializedObject(root.GetComponent<HouseUpgrades>());
                        Assign(data, "earthquakeDamageLabel", label); data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var name in new[] { "House", "Apartment_02", "Apartment_03", "Apartment_04" })
            {
                string path = "Assets/Art/Buildings/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var apartment = root.GetComponent<ApartmentUpgradeModel>();
                    var damage = root.GetComponent<EarthquakeHouseDamage>() ?? root.AddComponent<EarthquakeHouseDamage>();
                    var data = new SerializedObject(damage); Assign(data, "apartment", apartment);
                    var parts = root.GetComponent<BuildingModelGeometry>().Parts;
                    var pairs = new SerializedObject(apartment).FindProperty("sprinklers");
                    var towers = data.FindProperty("towers"); towers.arraySize = parts.Length;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var tower = towers.GetArrayElementAtIndex(i); var surface = new SerializedObject(parts[i]);
                        tower.FindPropertyRelative("normalBody").objectReferenceValue = ((MeshFilter)surface.FindProperty("modelMesh").objectReferenceValue).transform;
                        tower.FindPropertyRelative("elevatedBody").objectReferenceValue = ((MeshFilter)surface.FindProperty("elevatedMesh").objectReferenceValue).transform;
                        tower.FindPropertyRelative("surface").objectReferenceValue = parts[i].transform;
                        foreach (var variant in new[] { "normal", "elevated" })
                        {
                            var attachments = tower.FindPropertyRelative(variant + "Attachments"); attachments.arraySize = 2;
                            attachments.GetArrayElementAtIndex(0).objectReferenceValue = pairs.GetArrayElementAtIndex(i).FindPropertyRelative(variant + "Head").objectReferenceValue;
                            attachments.GetArrayElementAtIndex(1).objectReferenceValue = pairs.GetArrayElementAtIndex(i).FindPropertyRelative(variant + "Base").objectReferenceValue;
                        }
                    }
                    data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var game = components.OfType<CardGameController>().Single();
            var bank = game.GetComponent<AbilityPoints>() ?? game.gameObject.AddComponent<AbilityPoints>();
            var round = game.GetComponent<EarthquakeRoundController>() ?? game.gameObject.AddComponent<EarthquakeRoundController>();
            var bankPanel = Rect("AbilityPoints", game.Sidebar.transform);
            bankPanel.anchorMin = bankPanel.anchorMax = bankPanel.pivot = Vector2.one;
            bankPanel.sizeDelta = new Vector2(214f, 66f); bankPanel.anchoredPosition = new Vector2(-16f, -14f);
            bankPanel.gameObject.AddComponent<Image>().color = new Color(.06f, .09f, .1f, 1f);
            var balance = Label("Balance", bankPanel, "10 AP", 40f, new Color(1f, .77f, .39f)); Stretch(balance.rectTransform, 8f);
            balance.alignment = TextAlignmentOptions.Center;
            var bankData = new SerializedObject(bank); Assign(bankData, "balanceLabel", balance); bankData.ApplyModifiedPropertiesWithoutUndo();
            var gameData = new SerializedObject(game); Assign(gameData, "abilityPoints", bank); gameData.ApplyModifiedPropertiesWithoutUndo();

            var city = components.OfType<CityViewport>().Single();
            var panel = Rect("EarthquakeRoundControls", city.ViewRect);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.right; panel.pivot = Vector2.zero;
            panel.sizeDelta = new Vector2(-24f, 100f); panel.anchoredPosition = new Vector2(12f, 104f);
            panel.gameObject.AddComponent<Image>().color = new Color(.055f, .085f, .095f, .98f);
            var heading = Label("Round", panel, "ROUND 1", 30, new Color(1f, .77f, .39f));
            Top(heading.rectTransform, 12f, 6f, 36f, 210f);
            var status = Label("Status", panel, "Spend AP, protect your houses, then press READY.", 24f, new Color(.87f, .93f, .92f));
            Top(status.rectTransform, 12f, 46f, 40f, 210f);
            var readyRect = Rect("ReadyButton", game.Sidebar.transform);
            PlaceReady(readyRect);
            var image = readyRect.gameObject.AddComponent<Image>(); image.color = new Color(.27f, .56f, .41f);
            var ready = readyRect.gameObject.AddComponent<Button>(); ready.targetGraphic = image;
            var nav = ready.navigation; nav.mode = Navigation.Mode.None; ready.navigation = nav;
            var title = Label("Label", readyRect, "READY", 30f, Color.white); Stretch(title.rectTransform, 6f); title.alignment = TextAlignmentOptions.Center;
            UnityEventTools.AddPersistentListener(ready.onClick, round.Ready);
            var sequence = components.OfType<MonsterSequenceController>().Single();
            var quake = components.OfType<EarthquakeTestController>().Single();
            var tsunami = components.OfType<TsunamiTestController>().Single();
            var fire = components.OfType<FireTestController>().Single();
            var dataRound = new SerializedObject(round);
            Assign(dataRound, "game", game); Assign(dataRound, "abilityPoints", bank); Assign(dataRound, "sequence", sequence);
            Assign(dataRound, "earthquake", quake); Assign(dataRound, "tsunami", tsunami); Assign(dataRound, "fire", fire);
            Assign(dataRound, "navigation", components.OfType<CityCameraController>().Single());
            Assign(dataRound, "upgradePresentation", components.OfType<HouseUpgradePresentation>().Single());
            Assign(dataRound, "dock", game.GetComponent<CardDock>()); Assign(dataRound, "readyButton", ready);
            Assign(dataRound, "roundLabel", heading); Assign(dataRound, "statusLabel", status);
            var links = components.OfType<HouseCityUpgradeLink>().OrderBy(link => link.House.GetComponent<DraggableCard>().InitialGridCell).ToArray();
            var houses = dataRound.FindProperty("houses"); houses.arraySize = links.Length;
            for (int i = 0; i < links.Length; i++)
            {
                var entry = houses.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("link").objectReferenceValue = links[i];
                entry.FindPropertyRelative("damage").objectReferenceValue = links[i].Building.GetComponent<EarthquakeHouseDamage>();
            }
            var seqData = new SerializedObject(sequence);
            var blocked = new[] { (Button)seqData.FindProperty("menuButton").objectReferenceValue, (Button)seqData.FindProperty("startButton").objectReferenceValue }
                .Concat(new[] { quake, (MonoBehaviour)tsunami, fire }.Select(controller => (Button)new SerializedObject(controller).FindProperty("testButton").objectReferenceValue))
                .Concat(Enumerable.Range(0, seqData.FindProperty("sequenceButtons").arraySize).Select(i => (Button)seqData.FindProperty("sequenceButtons").GetArrayElementAtIndex(i).objectReferenceValue)).ToArray();
            var buttons = dataRound.FindProperty("debugStartButtons"); buttons.arraySize = blocked.Length;
            for (int i = 0; i < blocked.Length; i++) buttons.GetArrayElementAtIndex(i).objectReferenceValue = blocked[i];
            dataRound.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Saved first AP round, native Ready controls, per-operation workplace prices and earthquake damage on four house plots.");
        }

        [MenuItem("Tools/Card Game/Save Ready Beside AP Counter")]
        public static void SaveReadyPlacementForBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Save Ready placement in Edit Mode.");
            var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
            var game = Object.FindFirstObjectByType<CardGameController>();
            var round = Object.FindFirstObjectByType<EarthquakeRoundController>();
            var data = new SerializedObject(round);
            var ready = (Button)data.FindProperty("readyButton").objectReferenceValue;
            ready.transform.SetParent(game.Sidebar.transform, false);
            PlaceReady((RectTransform)ready.transform);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Saved Ready beside the AP counter, accessible in both card panel positions.");
        }

        private static void PlaceReady(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(144f, 66f); rect.anchoredPosition = new Vector2(-246f, -14f);
        }

        private static void Assign(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
        private static RectTransform Rect(string name, Transform parent)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }
        private static TextMeshProUGUI Label(string name, Transform parent, string value, float size, Color color)
        {
            var rect = Rect(name, parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
            label.text = value; label.fontSize = label.fontSizeMax = size; label.fontSizeMin = 14f; label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.color = color; label.raycastTarget = false;
            return label;
        }
        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * padding; rect.offsetMax = Vector2.one * -padding;
        }
        private static void Top(RectTransform rect, float left, float top, float height, float right)
        {
            rect.anchorMin = Vector2.up; rect.anchorMax = Vector2.one; rect.pivot = Vector2.up;
            rect.sizeDelta = new Vector2(-left - right, height); rect.anchoredPosition = new Vector2(left, -top);
        }
    }
}
