using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardAbilityPointEditModeTests
{
    [Test]
    public void FirstRoundControlsAndEconomyAreSavedInTheSceneBeforePlay()
    {
        var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        var game = components.OfType<CardGameController>().Single();
        var bank = components.OfType<AbilityPoints>().Single();
        var round = components.OfType<EarthquakeRoundController>().Single();
        Assert.That(game.AbilityPoints, Is.SameAs(bank));
        var gameData = new SerializedObject(game);
        Assert.That(game.EducationSeconds, Is.EqualTo(6.25f));
        Assert.That(game.NormalResearchSeconds, Is.EqualTo(12.5f));
        Assert.That(game.EducatedResearchSeconds, Is.EqualTo(7.5f));
        Assert.That(gameData.FindProperty("normalPeoplePerTurn").intValue, Is.EqualTo(4));
        Assert.That(game.Sidebar.PeopleArea.anchorMin.y, Is.EqualTo((2720f - 2684f) / 674f).Within(.0001f));
        Assert.That(game.Sidebar.PeopleArea.anchorMax.y, Is.EqualTo((2720f - 2152f) / 674f).Within(.0001f));
        Assert.That(gameData.FindProperty("normalPersonPrefab").objectReferenceValue,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PeopleCard.prefab")));
        var bankData = new SerializedObject(bank);
        Assert.That(bankData.FindProperty("startingPoints").intValue, Is.EqualTo(10));
        Assert.That(((TMP_Text)bankData.FindProperty("balanceLabel").objectReferenceValue).text, Is.EqualTo("10 AP"));
        var data = new SerializedObject(round);
        Assert.That(data.FindProperty("pointsPerProtectedHouse").intValue, Is.EqualTo(4));
        Assert.That(data.FindProperty("tsunamiPointsPerProtectedHouse").intValue, Is.EqualTo(9));
        Assert.That(data.FindProperty("firePointsPerProtectedHouse").intValue, Is.EqualTo(10));
        Assert.That(data.FindProperty("bossDepartureSeconds").floatValue, Is.EqualTo(10));
        Assert.That(data.FindProperty("nextRoundPoints").intValue, Is.EqualTo(10));
        Assert.That(data.FindProperty("tsunamiRoundPoints").intValue, Is.EqualTo(12));
        Assert.That(data.FindProperty("fireRoundPoints").intValue, Is.EqualTo(16));
        Assert.That(data.FindProperty("earthquakePeopleReward").intValue, Is.EqualTo(1));
        var resultsData = new SerializedObject(components.OfType<MonsterSequenceResults>().Single());
        var peopleReward = (TMP_Text)resultsData.FindProperty("peopleRewardLabel").objectReferenceValue;
        Assert.That(peopleReward, Is.Not.Null, "The earthquake reward uses the existing saved result text area.");
        Assert.That(peopleReward.gameObject.activeSelf, Is.False);
        Assert.That(data.FindProperty("nextTurnAfterCollapseSeconds").floatValue, Is.EqualTo(2.5f));
        Assert.That(data.FindProperty("houses").arraySize, Is.EqualTo(4));
        Assert.That(data.FindProperty("debugStartButtons").arraySize, Is.EqualTo(8));
        foreach (var station in components.OfType<BuildingStation>())
        {
            Assert.That(station.transform.Find("WorkStatus").gameObject.activeSelf, Is.False);
            Assert.That(station.transform.Find("WorkStatus").GetComponent<TMP_Text>().text, Is.Empty);
            var stationData = new SerializedObject(station);
            Assert.That(((TMP_Text)stationData.FindProperty("normalCostLabel").objectReferenceValue).text,
                Is.EqualTo(station.AbilityPointCost(CardKind.Person) + " AP"));
        }
        var ready = (Button)data.FindProperty("readyButton").objectReferenceValue;
        Assert.That(ready, Is.Not.Null); Assert.That(ready.onClick.GetPersistentTarget(0), Is.SameAs(round));
        Assert.That(ready.onClick.GetPersistentMethodName(0), Is.EqualTo("Ready"));
        Assert.That(ready.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("READY"));
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/EfeArts/Fonts/Pixellari SDF.asset");
        foreach (var target in new[] { ready.transform.parent, ((TMP_Text)bankData.FindProperty("balanceLabel").objectReferenceValue).transform.parent })
            foreach (var text in target.GetComponentsInChildren<TMP_Text>(true)) Assert.That(text.font, Is.SameAs(font));
        for (int i = 0; i < 4; i++)
        {
            var entry = data.FindProperty("houses").GetArrayElementAtIndex(i);
            var link = (HouseCityUpgradeLink)entry.FindPropertyRelative("link").objectReferenceValue;
            Assert.That(entry.FindPropertyRelative("damage").objectReferenceValue, Is.SameAs(link.Building.GetComponent<EarthquakeHouseDamage>()));
            Assert.That(entry.FindPropertyRelative("tsunamiDamage").objectReferenceValue, Is.SameAs(link.Building.GetComponent<ApartmentTsunamiCollapse>()));
        }
    }

    [TestCase("ResearchCenterCard", 4, 2)]
    [TestCase("FactoryCard", 5, 3)]
    [TestCase("SchoolCard", 2, 0)]
    public void WorkplacePricesAndTheirExistingBadgesAreAuthoredInThePrefab(string name, int normal, int educated)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
        var station = root.GetComponent<BuildingStation>();
        Assert.That(root.transform.Find("WorkStatus").gameObject.activeSelf, Is.False);
        Assert.That(root.transform.Find("WorkStatus").GetComponent<TMP_Text>().text, Is.Empty);
        Assert.That(station.AbilityPointCost(CardKind.Person), Is.EqualTo(normal));
        Assert.That(station.AbilityPointCost(CardKind.EducatedPerson), Is.EqualTo(educated));
        var prices = new SerializedObject(station);
        Assert.That(prices.FindProperty("tsunamiNormalAbilityPointCost").intValue, Is.EqualTo(name == "SchoolCard" ? 2 : name == "FactoryCard" ? 8 : 9));
        Assert.That(prices.FindProperty("tsunamiEducatedAbilityPointCost").intValue,
            Is.EqualTo(name == "SchoolCard" ? 0 : name == "FactoryCard" ? 4 : 3));
        Assert.That(prices.FindProperty("fireNormalAbilityPointCost").intValue,
            Is.EqualTo(name == "SchoolCard" ? 4 : 12));
        Assert.That(prices.FindProperty("fireEducatedAbilityPointCost").intValue,
            Is.EqualTo(name == "SchoolCard" ? 0 : 4));
        Assert.That(root.transform.Find("FolderArt/APNormalPeople").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(normal + " AP"));
        Assert.That(root.transform.Find("FolderArt/APEducated").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(name == "SchoolCard" ? "N/A" : educated + " AP"));
    }

    [Test]
    public void DropTargetEffectsAreSavedOnExistingArtworkAndNeverCreateBoardWidgets()
    {
        var scene = EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardGameController>(true)).Single();
        var cards = game.GetComponentsInChildren<DraggableCard>().Where(card => card.IsBuilding).ToArray();
        Assert.That(cards.Length, Is.EqualTo(7));
        foreach (var card in cards)
        {
            Assert.That(card.DropTargetFeedback, Is.Not.Null);
            Assert.That(card.DropTargetFeedback.gameObject, Is.SameAs(card.gameObject));
            CheckOutlines(card.DropTargetFeedback);
        }
        var data = new SerializedObject(game);
        foreach (var name in new[] { "vaultDropTarget", "peopleDropTarget" })
            CheckOutlines((CardDropTargetFeedback)data.FindProperty(name).objectReferenceValue);
    }

    private static void CheckOutlines(CardDropTargetFeedback target)
    {
        Assert.That(target, Is.Not.Null);
        var data = new SerializedObject(target);
        foreach (var name in new[] { "edgeOutline", "glowOutline" })
        {
            var outline = (Outline)data.FindProperty(name).objectReferenceValue;
            Assert.That(outline, Is.Not.Null);
            Assert.That(outline.GetComponent<Graphic>(), Is.Not.Null);
            Assert.That(outline.gameObject.activeInHierarchy, Is.True);
            Assert.That(outline.enabled, Is.False);
            Assert.That(outline.effectColor.a, Is.Zero);
        }
    }

    [TestCase("House")]
    [TestCase("Apartment_02")]
    [TestCase("Apartment_03")]
    [TestCase("Apartment_04")]
    public void EarthquakeDamageUsesTheNativeBodiesAndKeepsBothApartmentVariants(string name)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Buildings/Prefabs/" + name + ".prefab");
        var damage = root.GetComponent<EarthquakeHouseDamage>(); Assert.That(damage, Is.Not.Null);
        var data = new SerializedObject(damage);
        Assert.That(data.FindProperty("towers").arraySize, Is.EqualTo(6));
        for (int i = 0; i < 6; i++)
        {
            var tower = data.FindProperty("towers").GetArrayElementAtIndex(i);
            foreach (var nameField in new[] { "normalBody", "elevatedBody", "surface" }) Assert.That(tower.FindPropertyRelative(nameField).objectReferenceValue, Is.Not.Null);
            foreach (var nameField in new[] { "normalAttachments", "elevatedAttachments" })
                for (int j = 0; j < 2; j++) Assert.That(tower.FindPropertyRelative(nameField).GetArrayElementAtIndex(j).objectReferenceValue, Is.Not.Null);
        }
        Assert.That(root.transform.Find("Apartment_Ground"), Is.Not.Null);
        Assert.That(root.GetComponent<ApartmentTsunamiCollapse>(), Is.Not.Null);
    }
}
