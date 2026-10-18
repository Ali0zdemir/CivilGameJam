using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardHouseCityLinkEditModeTests
{
    [Test]
    public void FourDistinctHouseLinksAndTheirUpgradeTargetsAreSavedBeforePlay()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var houses = Object.FindFirstObjectByType<CardGameController>().GetComponentsInChildren<HouseUpgrades>()
            .Where(house => house.GetComponent<DraggableCard>().InitialGridCell >= 0)
            .OrderBy(house => house.GetComponent<DraggableCard>().InitialGridCell).ToArray();
        Assert.That(houses.Length, Is.EqualTo(4));
        for (int i = 0; i < houses.Length; i++)
        {
            var link = houses[i].GetComponent<HouseCityUpgradeLink>();
            Assert.That(link, Is.Not.Null);
            Assert.That(link.House, Is.SameAs(houses[i]));
            Assert.That(link.Building.name, Is.EqualTo("House_" + (i + 1).ToString("00")));
            Assert.That(link.RailSystem, Is.Not.Null);
            Assert.That(link.FloodProtection, Is.Not.Null);
            Assert.That(link.FireProtection, Is.Not.Null);
            Assert.That(link.FloodProtection.Building, Is.SameAs(link.Building));
            Assert.That(link.FireProtection.Building, Is.SameAs(link.Building));
            Assert.That(!EditorUtility.IsPersistent(link) && link.Building.gameObject.scene == houses[i].gameObject.scene, Is.True,
                "City references belong to the scene instance, rather than a reusable card prefab.");
        }
        Assert.That(houses.Select(house => house.GetComponent<HouseCityUpgradeLink>().Building).Distinct().Count(), Is.EqualTo(4));
    }
}
