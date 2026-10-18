using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardCityPlacementEditModeTests
{
    [Test]
    public void CardSceneSavesSevenDistinctBuildingsAndEightAnchorsInCardRowOrder()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var game = Object.FindFirstObjectByType<CardGameController>();
        var placement = game.GetComponent<CardCityGridPlacement>();
        Assert.That(placement, Is.Not.Null);
        var cards = game.GetComponentsInChildren<DraggableCard>().Where(card => card.IsBuilding).ToArray();
        Assert.That(cards.Length, Is.EqualTo(7));
        Assert.That(cards.Select(placement.BuildingFor).Distinct().Count(), Is.EqualTo(7));
        var ground = Object.FindFirstObjectByType<TsunamiTestController>().GroundSurface;
        for (var i = 0; i < CardGrid.CellCount; i++)
            Assert.That(placement.CellAnchor(i), Is.SameAs(ground.Find("Tile_Grid_4x2/Tile_R" + (2 - i / 4) + "_C" + (i % 4 + 1))));
        foreach (var card in cards)
        {
            var building = placement.BuildingFor(card);
            Assert.That(building, Is.Not.Null);
            Assert.That(Vector2.Distance(new Vector2(building.position.x, building.position.z),
                new Vector2(placement.CellAnchor(card.InitialGridCell).position.x, placement.CellAnchor(card.InitialGridCell).position.z)), Is.LessThan(.0001f));
            if (card.Kind == CardKind.House) Assert.That(building, Is.SameAs(card.GetComponent<HouseCityUpgradeLink>().Building));
        }
        var data = new SerializedObject(placement);
        foreach (var field in new[] { "game", "grid", "sequence", "earthquake", "tsunami", "fire", "monsterWalk", "navigation" })
            Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
    }
}
