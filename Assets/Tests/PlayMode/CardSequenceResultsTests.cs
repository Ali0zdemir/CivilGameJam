using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator MonsterSequenceResultsShowTheUniqueDestroyedHouseCountAndOnlyCloseOnRetry()
    {
        var links = CityHouseLinks();
        var rails = ProduceCityProducts(UpgradeKind.RailSystem, 2);
        var columns = ProduceCityProducts(UpgradeKind.Breakwater, 2);
        Assert.That(links[1].House.TryInstall(rails[0]), Is.True);
        Assert.That(links[2].House.TryInstall(rails[1]), Is.True);
        Assert.That(links[2].House.TryInstall(columns[0]), Is.True);
        Assert.That(links[3].House.TryInstall(columns[1]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        var rig = new SequencePreview();
        var result = Object.FindFirstObjectByType<MonsterSequenceResults>();
        var overlay = ControlField<GameObject>(result, "overlay");
        var retry = ControlField<Button>(result, "retryButton");
        var label = ControlField<TMP_Text>(result, "destroyedCountLabel");
        var ap = ControlField<TMP_Text>(result, "earnedPointsLabel");
        var ids = SequenceUIIds();
        Assert.That(result.IsVisible, Is.False);
        for (var number = 1; number <= 3; number++)
        {
            rig.Sequence.SelectSequence(number); rig.Sequence.StartSelectedSequence();
            rig.Until(() => rig.Sequence.Stage == (number == 1 ? MonsterSequenceStage.Earthquake : MonsterSequenceStage.WalkingToFirstPoint));
            Assert.That(result.IsVisible, Is.False, "Running disasters cannot show a result early.");
            rig.Until(() => !rig.Sequence.IsRunning);
            Assert.That(result.IsVisible, Is.True);
            Assert.That(result.TotalHouseCount, Is.EqualTo(4));
            var expectedDestroyed = number == 1 ? 2 : number == 2 ? 3 : 4;
            Assert.That(result.DestroyedHouseCount, Is.EqualTo(expectedDestroyed), "Each house is counted once even when several disasters reach it.");
            Assert.That(result.ProtectedHouseCount, Is.EqualTo(4 - expectedDestroyed));
            Assert.That(label.text, Is.EqualTo((4 - expectedDestroyed) + " / 4"));
            var reportCards = overlay.GetComponentsInChildren<DraggableCard>(true).OrderBy(card => card.Rect.anchoredPosition.x).ToArray();
            Assert.That(reportCards.Length, Is.EqualTo(4));
            for (int i = 0; i < reportCards.Length; i++)
            {
                var reportCard = reportCards[i];
                Assert.That(reportCard.enabled, Is.False, "A result illustration must never become another playable house.");
                foreach (var icon in reportCard.GetComponentsInChildren<UpgradeIcon>(true).Where(icon => icon.name == "InstalledIcon"))
                    Assert.That(icon.gameObject.activeSelf, Is.EqualTo(links[i].House.GetInstalled(icon.Kind) != null), "Each slot mirrors its own real house.");
                var text = reportCard.transform.Find("HouseReport").GetComponent<TMP_Text>().text;
                Assert.That(text, Does.StartWith(number == 1 ? "EARTHQUAKE" : number == 2 ? "TSUNAMI" : "FIRE"));
                Assert.That(text, Does.Contain(number == 1 ? "Seismic isolator" : number == 2 ? "Elevator" : "Sprinklers"));
                Assert.That(reportCard.transform.Find("EarthquakeDamage").gameObject.activeSelf,
                    Is.EqualTo(result.IsHouseDestroyed(links[i].Building.GetComponent<CivilGameJam.BuildingModelGeometry>())));
            }
            if (number >= 2)
            {
                foreach (var link in links)
                {
                    var collapse = link.Building.GetComponent<ApartmentTsunamiCollapse>();
                    if (!collapse.HasCollapsed) continue;
                    InvokeCity(collapse, "AdvanceCollapse", collapse.Duration + 2.1f);
                    Assert.That(collapse.HasCollapsed && !collapse.IsPlaying, Is.True);
                    Assert.That(link.Building.GetComponentsInChildren<MeshRenderer>(true).Any(renderer => renderer.forceRenderingOff), Is.False,
                        "Collapsed houses remain visible behind the report until Retry.");
                }
                result.Show();
                Assert.That(result.DestroyedHouseCount, Is.EqualTo(expectedDestroyed), "Visible ruins remain in the destroyed-house total.");
            }
            Assert.That(ap.text, Is.EqualTo("DISASTER REPORT"), "Standalone debug sequences do not award gameplay AP.");
            Assert.That(ap.font, Is.SameAs(game.GetComponentInChildren<TextMeshProUGUI>(true).font));
            InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/SequenceResultsWork/Previews"));
            Directory.CreateDirectory(directory);
            yield return CaptureCanvas(Path.Combine(directory, "sequence-" + number + "-result.png"), afterRender: camera =>
            {
                foreach (var reportCard in reportCards)
                {
                    var text = reportCard.transform.Find("HouseReport").GetComponent<TMP_Text>();
                    text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, "The per-house report must fit its saved text area.");
                }
                AssertTopButton(retry, camera);
                var city = Object.FindFirstObjectByType<CityViewport>();
                var point = RectTransformUtility.WorldToScreenPoint(camera, city.ViewRect.TransformPoint(city.ViewRect.rect.center));
                var zoom = ControlField<float>(rig.Navigation, "targetZoom");
                var pan = ControlField<Vector2>(rig.Navigation, "targetPan");
                InvokeCity(rig.Navigation, "ReadPointer", point, -10f, true, true);
                InvokeCity(rig.Navigation, "ReadPointer", point + Vector2.right * 20f, -10f, false, true);
                Assert.That(ControlField<float>(rig.Navigation, "targetZoom"), Is.EqualTo(zoom), "The modal consumes scroll over the city.");
                Assert.That(ControlField<Vector2>(rig.Navigation, "targetPan"), Is.EqualTo(pan));
                var pointer = new PointerEventData(EventSystem.current) { position = point };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits[0].gameObject.transform.IsChildOf(overlay.transform) || hits[0].gameObject == overlay, Is.True);
            });
            rig.Sequence.SelectSequence(number == 3 ? 1 : 3);
            var selected = rig.Sequence.SelectedSequence;
            Assert.That(links.Any(link => link.Building.GetComponent<ApartmentEarthquakeAnimation>().HasCollapsed ||
                link.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed || rig.Fire.WasHouseBurnt(link.Building)), Is.True);
            retry.onClick.Invoke();
            Assert.That(result.IsVisible, Is.False);
            rig.For(.2f);
            Assert.That(rig.Sequence.IsRunning || rig.Quake.IsRunning || rig.Tsunami.IsRunning || rig.Fire.IsRunning || rig.Walk.IsWalkingAfterEvent, Is.False);
            Assert.That(rig.Sequence.ActiveSequence, Is.EqualTo(number));
            Assert.That(rig.Sequence.SelectedSequence, Is.EqualTo(selected));
            Assert.That(rig.Sequence.Stage, Is.EqualTo(MonsterSequenceStage.Complete));
            Assert.That(links.Any(link => link.Building.GetComponent<ApartmentEarthquakeAnimation>().HasCollapsed ||
                link.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed || rig.Fire.WasHouseBurnt(link.Building)), Is.False,
                "Closing the report restores houses for the next attempt.");
            rig.Sequence.StopSequence(); InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
            Assert.That(result.IsVisible, Is.False);
            Assert.That(SequenceUIIds(), Is.EqualTo(ids), "All result UI is saved before Play Mode and reused for every attempt.");
        }
    }

    [UnityTest]
    public IEnumerator MonsterSequenceResultsWaitForTheLastFireAndDoNotAppearOnCancellationOrIndividualTests()
    {
        var rig = new SequencePreview();
        var result = Object.FindFirstObjectByType<MonsterSequenceResults>();
        rig.Sequence.SelectSequence(3); rig.Sequence.StartSelectedSequence(); rig.For(.2f); rig.Sequence.StopSequence();
        Assert.That(result.IsVisible, Is.False);
        rig.Quake.StartEarthquake(); InvokeCity(rig.Quake, "AdvanceShake", rig.Quake.TestDuration);
        Assert.That(result.IsVisible, Is.False, "Only a completed sequence opens the result panel.");
        rig.Quake.StopEarthquake();
        rig.Walk.AfterEventPoint.position = rig.Walk.WalkPoint.position + Vector3.forward * .35f;
        rig.Sequence.StartSelectedSequence();
        rig.Until(() => rig.Sequence.Stage == MonsterSequenceStage.WalkingToSecondPoint);
        rig.Until(() => !rig.Walk.IsWalkingAfterEvent);
        Assert.That(rig.Fire.IsRunning, Is.True);
        Assert.That(result.IsVisible, Is.False, "Reaching point 2 cannot hide the remaining fire with the result modal.");
        rig.Until(() => !rig.Sequence.IsRunning);
        Assert.That(result.IsVisible, Is.True);
        Assert.That(result.DestroyedHouseCount, Is.EqualTo(4));
        Assert.That(result.TotalHouseCount, Is.EqualTo(4));
        rig.Sequence.enabled = false;
        Assert.That(result.IsVisible, Is.False, "Disabling the sequence also removes its modal.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator MonsterSequenceResultsCountBurntHousesOnceAndKeepExtinguishedHousesSafe()
    {
        var links = CityHouseLinks();
        var rails = ProduceCityProducts(UpgradeKind.RailSystem, 4);
        var columns = ProduceCityProducts(UpgradeKind.Breakwater, 4);
        for (var i = 0; i < links.Length; i++)
        {
            Assert.That(links[i].House.TryInstall(rails[i]), Is.True);
            Assert.That(links[i].House.TryInstall(columns[i]), Is.True);
        }
        var sprinklers = ProduceCityProducts(UpgradeKind.Fire, 2);
        for (var i = 0; i < 2; i++) Assert.That(links[i].House.TryInstall(sprinklers[i]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        var rig = new SequencePreview();
        rig.Sequence.SelectSequence(3); rig.Sequence.StartSelectedSequence();
        rig.Until(() => !rig.Sequence.IsRunning);
        var result = Object.FindFirstObjectByType<MonsterSequenceResults>();
        Assert.That(links.All(link => !link.Building.GetComponent<ApartmentEarthquakeAnimation>().HasCollapsed &&
            !link.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed), Is.True, "All four houses survived the physical disasters.");
        Assert.That(links.Select(link => rig.Fire.WasHouseBurnt(link.Building)), Is.EqualTo(new[] { false, false, true, true }));
        Assert.That(result.DestroyedHouseCount, Is.EqualTo(2), "Six burnt apartment parts count as one house, while extinguished houses stay safe.");
        Assert.That(ControlField<TMP_Text>(result, "destroyedCountLabel").text, Is.EqualTo("2 / 4"));
        result.Retry();
        Assert.That(result.IsVisible || rig.Sequence.IsRunning, Is.False);
        Assert.That(rig.Fire.WasHouseBurnt(links[2].Building), Is.False, "Closing the result restores the burnt houses.");
        rig.Sequence.SelectSequence(1); rig.Sequence.StartSelectedSequence();
        Assert.That(links.Any(link => rig.Fire.WasHouseBurnt(link.Building)), Is.False, "A new test clears the previous fire damage.");
        rig.Until(() => !rig.Sequence.IsRunning);
        Assert.That(result.DestroyedHouseCount, Is.Zero);
        rig.Sequence.StopSequence();
    }

    [UnityTest]
    public IEnumerator MonsterSequenceResultsCanShowZeroDestroyedHousesWhenAllFourHaveRails()
    {
        var links = CityHouseLinks(); var rails = ProduceCityProducts(UpgradeKind.RailSystem, 4);
        for (var i = 0; i < links.Length; i++) Assert.That(links[i].House.TryInstall(rails[i]), Is.True);
        yield return new WaitForSecondsRealtime(1.5f);
        var rig = new SequencePreview(); rig.Sequence.SelectSequence(1); rig.Sequence.StartSelectedSequence();
        rig.Until(() => !rig.Sequence.IsRunning);
        var result = Object.FindFirstObjectByType<MonsterSequenceResults>();
        Assert.That(result.IsVisible, Is.True);
        Assert.That(result.DestroyedHouseCount, Is.Zero);
        Assert.That(result.ProtectedHouseCount, Is.EqualTo(4));
        Assert.That(ControlField<TMP_Text>(result, "destroyedCountLabel").text, Is.EqualTo("4 / 4"));
        rig.Sequence.StopSequence();
    }
}
