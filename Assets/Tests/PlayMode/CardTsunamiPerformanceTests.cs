using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator DelayedTsunamiFrameKeepsItsTimelineWithoutMultiplyingSimulationCost()
    {
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var normal = new double[3];
        var delayed = new double[3];
        for (var sample = 0; sample < normal.Length; sample++)
        {
            normal[sample] = MeasureFrame(1f / 60f);
            delayed[sample] = MeasureFrame(2f);
        }
        Array.Sort(normal); Array.Sort(delayed);
        // Relative cost tolerates different test machines while detecting hundreds of catch-up solves.
        Assert.That(delayed[1], Is.LessThan(Math.Max(20d, normal[1] * 8d)),
            $"A delayed frame took {delayed[1]:F2} ms versus {normal[1]:F2} ms normally. Water catch-up must have a fixed work budget.");
        UnityEngine.Debug.Log($"Tsunami frame cost: normal={normal[1]:F3} ms, delayed={delayed[1]:F3} ms.");
        yield return null;

        double MeasureFrame(float seconds)
        {
            tsunami.StartTsunami();
            var landfall = monster.WaveReleaseDelay +
                (tsunami.CityBounds.min.z - ControlField<float>(tsunami, "startDistance")) / tsunami.WaveSpeed;
            var target = landfall + .3f;
            while (target - ControlField<float>(tsunami, "sequenceElapsed") > .0001f)
                InvokeCity(tsunami, "AdvanceTsunami", Math.Min(1f / 60f, target - ControlField<float>(tsunami, "sequenceElapsed")));
            var before = ControlField<float>(tsunami, "sequenceElapsed");
            Assert.That(tsunami.FloodCoverage, Is.GreaterThan(0f));
            var timer = Stopwatch.StartNew();
            InvokeCity(tsunami, "AdvanceTsunami", seconds);
            timer.Stop();
            Assert.That(ControlField<float>(tsunami, "sequenceElapsed"), Is.EqualTo(before + seconds).Within(.002f),
                "The effect budget must not slow down the event clock or queue a time backlog.");
            tsunami.StopTsunami();
            return timer.Elapsed.TotalMilliseconds;
        }
    }
}

public sealed partial class CardAbilityPointsPlayModeTests
{
    [UnityTest]
    public IEnumerator LiveEarthquakeVictoryRunsBossTsunamiToItsReportAndReleasesTheTurn()
    {
        bank.AddPoints(4);
        PrepareSeismic(2);
        yield return new WaitForSecondsRealtime(1.6f);
        var water = Field<TsunamiTestController>(round, "tsunami");
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var heardWater = false;
        var sawBoss = false;
        var maxFrame = 0f;
        var deadline = Time.realtimeSinceStartupAsDouble + 40;
        round.Ready();
        while (!sequence.Results.IsVisible && Time.realtimeSinceStartupAsDouble < deadline)
        {
            yield return null;
            maxFrame = Mathf.Max(maxFrame, Time.unscaledDeltaTime);
            heardWater |= water.IsRunning;
            sawBoss |= monster.Model.gameObject.activeInHierarchy;
            if (round.SequenceNumber == 2 && !sequence.Results.IsVisible)
                Assert.That(bank.CurrentPoints, Is.Zero, "Do not credit AP during the uninterrupted tsunami.");
        }
        Assert.That(heardWater && sawBoss, Is.True);
        Assert.That(sequence.Results.IsVisible, Is.True, "The real Update loop must finish the boss/tsunami presentation.");
        Assert.That(sequence.IsRunning || water.IsRunning || bank.ActionsLocked, Is.False);
        Assert.That(round.SequenceNumber, Is.EqualTo(2));
        Assert.That(bank.CurrentPoints, Is.EqualTo(24), "Pay the deferred earthquake and tsunami retry AP at the completed report.");
        Assert.That(houses.All(house => house.Building.GetComponent<ApartmentTsunamiCollapse>().HasCollapsed), Is.True,
            "Limiting visual simulation work must not bypass tsunami damage or restore the ruins before the report closes.");
        UnityEngine.Debug.Log($"Live earthquake to tsunami completed; largest frame={maxFrame * 1000f:F2} ms.");
        sequence.Results.Retry();
        var inputDeadline = Time.realtimeSinceStartupAsDouble + 3;
        while (!round.CanReady && Time.realtimeSinceStartupAsDouble < inputDeadline) yield return null;
        Assert.That(round.CanReady, Is.True, "The next turn must unlock after the smooth camera return finishes.");
    }
}
