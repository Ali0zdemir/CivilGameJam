using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator TsunamiButtonRaisesSavedMonsterThenSendsWaveIntoTheFrontOfTheCity()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var model = monster.transform.Find("Cube_Model");
        var parts = model.GetComponentsInChildren<Transform>().Select(part => part.GetInstanceID()).ToArray();
        var submergedPosition = model.localPosition;
        Assert.That(model.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y), Is.LessThan(tsunami.SeaLevel));
        var button = ControlField<Button>(tsunami, "testButton");
        button.onClick.Invoke();
        Assert.That(tsunami.IsRunning, Is.True);
        Assert.That(button.interactable, Is.False);
        InvokeCity(tsunami, "AdvanceTsunami", monster.WaveReleaseDelay * 0.5f);
        Assert.That(model.localPosition.y, Is.GreaterThan(submergedPosition.y));
        Assert.That(tsunami.transform.Find("Tsunami_Water").gameObject.activeSelf, Is.False, "The monster rises before releasing the wave.");
        var timeToWave = monster.WaveReleaseDelay * 0.5f + 0.2f;
        InvokeCity(tsunami, "AdvanceTsunami", timeToWave);
        Assert.That(tsunami.transform.Find("Tsunami_Water").gameObject.activeSelf, Is.True);
        Assert.That(monster.EmergenceProgress, Is.LessThan(1f), "The wave must start while the monster is still emerging.");
        var captureDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TsunamiWork"));
        Directory.CreateDirectory(captureDirectory);
        yield return CaptureCanvas(Path.Combine(captureDirectory, "monster-rising-wave.png"));
        var firstFront = ControlField<float>(tsunami, "front");
        InvokeCity(tsunami, "AdvanceTsunami", 0.3f);
        Assert.That(ControlField<float>(tsunami, "front"), Is.GreaterThan(firstFront));
        var shoreArrival = (tsunami.CityBounds.min.z - ControlField<float>(tsunami, "startDistance")) /
            ControlField<float>(tsunami, "waveSpeed");
        InvokeCity(tsunami, "AdvanceTsunami", Mathf.Max(0f, shoreArrival + 0.35f - ControlField<float>(tsunami, "elapsed")));
        Assert.That(tsunami.FloodCoverage, Is.GreaterThan(0f), "The reversed wave must still flood the streets.");
        yield return CaptureCanvas(Path.Combine(captureDirectory, "monster-city-flood.png"));
        InvokeCity(tsunami, "AdvanceTsunami", tsunami.TestDuration);
        Assert.That(tsunami.IsRunning, Is.False);
        Assert.That(monster.EmergenceProgress, Is.EqualTo(1f));
        Assert.That(button.interactable, Is.True);
        yield return CaptureCanvas(Path.Combine(captureDirectory, "monster-emerged.png"));
        Assert.That(model.GetComponentsInChildren<Transform>().Select(part => part.GetInstanceID()).ToArray(), Is.EqualTo(parts));
        button.onClick.Invoke();
        Assert.That(tsunami.IsRunning, Is.True);
        Assert.That(model.localPosition, Is.EqualTo(submergedPosition));
        InvokeCity(tsunami, "AdvanceTsunami", 1f);
        tsunami.StopTsunami();
        Assert.That(model.localPosition, Is.EqualTo(submergedPosition));
        Assert.That(button.interactable, Is.True);
        yield return null;
    }
}
