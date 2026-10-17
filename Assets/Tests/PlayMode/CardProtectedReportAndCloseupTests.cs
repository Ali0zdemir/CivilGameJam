using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed partial class CardGamePlayModeTests
{
    [Test]
    public void ResultsShowTheProtectedSnapshotAndKeepTheAwardAndDiagnosticDamageCounts()
    {
        var results = Object.FindFirstObjectByType<MonsterSequenceResults>();
        var label = ControlField<TMP_Text>(results, "destroyedCountLabel");
        var earned = ControlField<TMP_Text>(results, "earnedPointsLabel");
        results.ShowRound(3, 4, 16, 1);
        Assert.That(results.ProtectedHouseCount, Is.EqualTo(1));
        Assert.That(results.DestroyedHouseCount, Is.EqualTo(3));
        Assert.That(label.text, Is.EqualTo("1 / 4"));
        Assert.That(earned.text, Is.EqualTo("AP EARNED: +16"));
        // Rebuilt houses can all be standing while only two were protected in the completed turn.
        results.ShowRound(0, 4, 18, 2);
        Assert.That(label.text, Is.EqualTo("2 / 4"));
        Assert.That(earned.text, Is.EqualTo("AP EARNED: +18"));
        results.ShowRound(0, 4, 46, 4);
        Assert.That(label.text, Is.EqualTo("4 / 4"));
        results.ShowRound(4, 4, 6, 0);
        Assert.That(label.text, Is.EqualTo("0 / 4"));
        results.Retry();
        Assert.That(results.IsVisible, Is.False);
    }

    [Test]
    public void UpgradeCloseupsAreOnePointFiveTimesThePreviousFrameAndStillEaseHome()
    {
        var navigation = Object.FindFirstObjectByType<CityCameraController>();
        var camera = Object.FindFirstObjectByType<CityViewport>().WorldCamera;
        float originalAspect = camera.aspect;
        try
        {
            foreach (float aspect in new[] { 16f / 9f, 4f / 3f, .75f })
            foreach (var link in CityHouseLinks())
            foreach (bool elevatedFrame in new[] { false, true })
            {
                camera.aspect = aspect;
                InvokeCity(navigation, "AdvanceNavigation", 0f);
                var homePan = navigation.PanOffset;
                float homeZoom = camera.orthographicSize;
                Assert.That(navigation.BeginPresentationFrame(link.Building, 2.2f, .2f, elevatedFrame), Is.True);
                InvokeCity(navigation, "AdvanceNavigation", .2f);
                float previousZoom = camera.orthographicSize;
                navigation.EndPresentationFocus(0f);
                Assert.That(navigation.BeginPresentationFrame(link.Building, 2.2f, .2f, elevatedFrame, 1.5f), Is.True);
                InvokeCity(navigation, "AdvanceNavigation", .1f);
                Assert.That(navigation.IsFocusReady, Is.False);
                InvokeCity(navigation, "AdvanceNavigation", .1f);
                Assert.That(previousZoom / camera.orthographicSize, Is.EqualTo(1.5f).Within(.0001f));
                navigation.EndPresentationFocus(.3f);
                InvokeCity(navigation, "AdvanceNavigation", .15f);
                Assert.That(navigation.IsPresenting, Is.True);
                InvokeCity(navigation, "AdvanceNavigation", .15f);
                Assert.That(navigation.IsPresenting, Is.False);
                Assert.That(navigation.PanOffset, Is.EqualTo(homePan));
                Assert.That(camera.orthographicSize, Is.EqualTo(homeZoom).Within(.0001f));
            }
        }
        finally { camera.aspect = originalAspect; }
    }
}
