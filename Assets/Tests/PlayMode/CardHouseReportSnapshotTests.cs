using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator ResultCardsMirrorTheirOwnThreeSlotsAndFreezeUntilTheReportIsClosed()
    {
        var links = CityHouseLinks();
        var rails = ProduceCityProducts(UpgradeKind.RailSystem);
        var elevators = ProduceCityProducts(UpgradeKind.Breakwater);
        var sprinklers = ProduceCityProducts(UpgradeKind.Fire);
        Assert.That(links[0].House.TryInstall(rails[0]), Is.True);
        Assert.That(links[1].House.TryInstall(elevators[0]), Is.True);
        Assert.That(links[2].House.TryInstall(sprinklers[0]), Is.True);
        Assert.That(links[3].House.TryInstall(rails[1]), Is.True);
        Assert.That(links[3].House.TryInstall(elevators[1]), Is.True);
        Assert.That(links[3].House.TryInstall(sprinklers[1]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        var results = Object.FindFirstObjectByType<MonsterSequenceResults>();
        var sequence = Object.FindFirstObjectByType<MonsterSequenceController>();
        var overlay = ControlField<GameObject>(results, "overlay");
        var views = overlay.GetComponentsInChildren<DraggableCard>(true).OrderBy(card => card.Rect.anchoredPosition.x).ToArray();
        var positions = views.Select(card => card.Rect.anchoredPosition).ToArray();
        var sizes = views.Select(card => card.Rect.sizeDelta).ToArray();
        var scales = views.Select(card => card.Rect.localScale).ToArray();
        var ids = SequenceUIIds();
        links[1].House.SetDestroyed(true);
        sequence.SelectSequence(3);
        results.ShowRound(1, 4, 30, 3);
        Assert.That(results.IsVisible, Is.True);
        Assert.That(ControlField<TMP_Text>(results, "earnedPointsCount").text, Is.EqualTo("+30"));
        for (int i = 0; i < views.Length; i++)
        {
            Assert.That(game.Cards.Contains(views[i]), Is.False);
            foreach (var icon in views[i].GetComponentsInChildren<UpgradeIcon>(true).Where(icon => icon.name == "InstalledIcon"))
                Assert.That(icon.gameObject.activeSelf, Is.EqualTo(links[i].House.GetInstalled(icon.Kind) != null));
            var text = views[i].transform.Find("HouseReport").GetComponent<TMP_Text>().text;
            Assert.That(text, Does.StartWith("FIRE"));
            Assert.That(text, Does.Contain(i >= 2 ? "Sprinklers: ACTIVE" : "Sprinklers: MISSING"));
            Assert.That(views[i].Rect.anchoredPosition, Is.EqualTo(positions[i]));
            Assert.That(views[i].Rect.sizeDelta, Is.EqualTo(sizes[i]));
            Assert.That(views[i].Rect.localScale, Is.EqualTo(scales[i]));
        }
        Assert.That(views[1].transform.Find("HouseReport").GetComponent<TMP_Text>().text, Does.Contain("Burned: no sprinklers."));
        Assert.That(views[1].transform.Find("EarthquakeDamage").gameObject.activeSelf, Is.True);
        var original = views.Select(card => card.transform.Find("HouseReport").GetComponent<TMP_Text>().text).ToArray();
        links[2].House.Release(UpgradeKind.Fire, false);
        sequence.SelectSequence(1);
        Assert.That(views[2].GetComponentsInChildren<UpgradeIcon>(true).Single(icon => icon.name == "InstalledIcon" && icon.Kind == UpgradeKind.Fire).gameObject.activeSelf, Is.True);
        Assert.That(views.Select(card => card.transform.Find("HouseReport").GetComponent<TMP_Text>().text), Is.EqualTo(original),
            "The next preparation and selected sequence cannot rewrite the completed turn's report.");
        var directory = Path.GetFullPath("Library/CardGameTestResults/WorkResults");
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "house-report-three-systems.png"), afterRender: camera =>
        {
            foreach (var card in views)
            {
                var text = card.transform.Find("HouseReport").GetComponent<TMP_Text>();
                text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False);
            }
        });
        Assert.That(SequenceUIIds(), Is.EqualTo(ids), "Opening a report reuses saved UI and creates no cards, icons or labels.");
        results.Retry();
        links[1].House.SetDestroyed(false);
        results.ShowRound(0, 4, 12, 4);
        Assert.That(views[0].transform.Find("HouseReport").GetComponent<TMP_Text>().text, Does.StartWith("EARTHQUAKE").And.Contain("Seismic isolator: ACTIVE"));
        Assert.That(views[2].GetComponentsInChildren<UpgradeIcon>(true).Single(icon => icon.name == "InstalledIcon" && icon.Kind == UpgradeKind.Fire).gameObject.activeSelf, Is.False);
        Assert.That(views[1].transform.Find("EarthquakeDamage").gameObject.activeSelf, Is.False);
        results.Retry();
    }
}
