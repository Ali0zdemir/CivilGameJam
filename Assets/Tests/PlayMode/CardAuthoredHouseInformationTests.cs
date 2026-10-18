using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    private static System.Action CaptureInformationAppearance(RectTransform root)
    {
        var rects = root.GetComponentsInChildren<RectTransform>(true).Select(rect => new
        {
            rect, position = rect.anchoredPosition, size = rect.sizeDelta, scale = rect.localScale,
            min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot
        }).ToArray();
        var images = root.GetComponentsInChildren<Image>(true).Select(image => new
        {
            image, sprite = image.sprite, color = image.color, type = image.type, aspect = image.preserveAspect
        }).ToArray();
        var labels = root.GetComponentsInChildren<TMP_Text>(true).Select(label => new
        {
            label, font = label.font, size = label.fontSize, min = label.fontSizeMin, max = label.fontSizeMax,
            auto = label.enableAutoSizing, style = label.fontStyle, color = label.color,
            alignment = label.alignment, margin = label.margin
        }).ToArray();
        return () =>
        {
            foreach (var saved in rects)
            {
                Assert.That(Vector2.Distance(saved.rect.anchoredPosition, saved.position), Is.LessThan(.01f), saved.rect.name);
                Assert.That(saved.rect.sizeDelta, Is.EqualTo(saved.size), saved.rect.name);
                Assert.That(saved.rect.localScale, Is.EqualTo(saved.scale), saved.rect.name);
                Assert.That(saved.rect.anchorMin, Is.EqualTo(saved.min), saved.rect.name);
                Assert.That(saved.rect.anchorMax, Is.EqualTo(saved.max), saved.rect.name);
                Assert.That(saved.rect.pivot, Is.EqualTo(saved.pivot), saved.rect.name);
            }
            foreach (var saved in images)
            {
                Assert.That(saved.image.sprite, Is.SameAs(saved.sprite));
                Assert.That(saved.image.color, Is.EqualTo(saved.color));
                Assert.That(saved.image.type, Is.EqualTo(saved.type));
                Assert.That(saved.image.preserveAspect, Is.EqualTo(saved.aspect));
            }
            foreach (var saved in labels)
            {
                Assert.That(saved.label.font, Is.SameAs(saved.font));
                Assert.That(saved.label.fontSize, Is.EqualTo(saved.size));
                Assert.That(saved.label.fontSizeMin, Is.EqualTo(saved.min));
                Assert.That(saved.label.fontSizeMax, Is.EqualTo(saved.max));
                Assert.That(saved.label.enableAutoSizing, Is.EqualTo(saved.auto));
                Assert.That(saved.label.fontStyle, Is.EqualTo(saved.style));
                Assert.That(saved.label.color, Is.EqualTo(saved.color));
                Assert.That(saved.label.alignment, Is.EqualTo(saved.alignment));
                Assert.That(saved.label.margin, Is.EqualTo(saved.margin));
            }
        };
    }

    private static TMP_Text AuthoredProtectionLabel(HouseUpgrades house, int index) =>
        house.GetComponent<CardHover>().Details.Find("Information (" + (index + 1) + ")/Description").GetComponent<TMP_Text>();

    [UnityTest]
    public IEnumerator AuthoredHouseProtectionSlotsTrackEachSystemWithoutChangingAnyInformationStyles()
    {
        var buildings = game.Cards.Where(card => card.IsBuilding).ToArray();
        var appearances = buildings.Select(card => CaptureInformationAppearance(card.GetComponent<CardHover>().Details)).ToArray();
        var houses = buildings.Where(card => card.Kind == CardKind.House).Select(card => card.GetComponent<HouseUpgrades>()).ToArray();
        string[] names = { "Rail System", "Elevated Floor", "Fire Protection" };
        string[] descriptions = { "Earthquake resistant.", "Flood resistant.", "Fire resistant." };
        for (int i = 0; i < 3; i++)
        {
            var kind = (UpgradeKind)(i + 1);
            var product = ProduceCityProducts(kind)[0];
            foreach (var house in houses)
            {
                var label = AuthoredProtectionLabel(house, i);
                Assert.That(label.text, Is.Empty);
                Assert.That(label.transform.parent.gameObject.activeSelf, Is.False, "An empty protection panel must be hidden.");
                Assert.That(house.TryInstall(product), Is.True);
                Assert.That(label.text, Does.Contain(names[i]).And.Contain(descriptions[i]));
                Assert.That(label.gameObject.activeSelf, Is.True);
                Assert.That(label.GetPreferredValues(label.text, label.rectTransform.rect.width, Mathf.Infinity).y,
                    Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), "Protection text must fit its authored slot.");
                var details = house.GetComponent<CardHover>().Details;
                for (int slot = 0; slot < 3; slot++)
                    Assert.That(details.Find("Information (" + (slot + 1) + ")").gameObject.activeSelf,
                        Is.EqualTo(slot == i), "Only the installed system's panel should open.");
                Assert.That(details.Find("Information/Title").gameObject.activeSelf, Is.True);
                Assert.That(details.Find("Information/Description").gameObject.activeSelf, Is.True);
                Assert.That(house.Release(kind, false), Is.SameAs(product));
                Assert.That(label.text, Is.Empty);
                Assert.That(details.Find("Information (" + (i + 1) + ")").gameObject.activeSelf, Is.False,
                    "Removing a system must hide its whole panel, including the background.");
            }
        }
        foreach (var building in buildings)
        {
            var details = building.GetComponent<CardHover>().Details;
            Assert.That(details.GetComponentsInChildren<Image>(true).Any(image => image.sprite != null &&
                image.sprite.name.StartsWith("Card Info UI")), Is.True, building.name);
            Hover(building, true);
            yield return new WaitForSecondsRealtime(.3f);
            Hover(building, false);
            yield return new WaitForSecondsRealtime(.3f);
        }
        foreach (var appearance in appearances) appearance();
    }

    [UnityTest]
    public IEnumerator AuthoredHouseInformationStackFitsOnScreenInAllFourCornerSlots()
    {
        var house = game.Cards.First(card => card.Kind == CardKind.House).GetComponent<HouseUpgrades>();
        for (int i = 0; i < 3; i++) Assert.That(house.TryInstall(ProduceCityProducts((UpgradeKind)(i + 1))[0]), Is.True);
        yield return new WaitForSecondsRealtime(.65f);
        var details = house.GetComponent<CardHover>().Details;
        var appearance = CaptureInformationAppearance(details);
        var viewport = (RectTransform)game.GetComponentInParent<Canvas>().rootCanvas.transform;
        var directory = Path.Combine(Application.dataPath, "../Library/CardGameTestResults/AuthoredHouseInformation");
        Directory.CreateDirectory(directory);
        foreach (int cell in new[] { 0, 3, 4, 7 })
        {
            Assert.That(game.Grid.TryPlaceOrSwap(house.Card, cell), Is.True);
            yield return new WaitForSecondsRealtime(.45f);
            Hover(house.Card, true);
            yield return new WaitForSecondsRealtime(.35f);
            for (int i = 0; i < 3; i++)
            {
                var panel = (RectTransform)details.Find("Information (" + (i + 1) + ")");
                var bounds = HoverRect(panel, viewport);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin + 7.9f));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax - 7.9f));
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(viewport.rect.xMin + 7.9f));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(viewport.rect.xMax - 7.9f));
            }
            if (cell == 0 || cell == 7)
                yield return CaptureCanvas(Path.Combine(directory, "house-information-cell-" + cell + ".png"), house.Card.gameObject);
            Hover(house.Card, false);
            yield return new WaitForSecondsRealtime(.3f);
            appearance();
        }
    }
}
