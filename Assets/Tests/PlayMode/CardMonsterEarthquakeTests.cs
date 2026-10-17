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
    public IEnumerator EarthquakeWalksToSavedPointWithContinuousShakeAndTwoSecondsAfterTheLastLanding()
    {
        var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var model = monster.Model;
        var left = model.Find("Left_Foot");
        var right = model.Find("Right_Foot");
        var ids = model.GetComponentsInChildren<Transform>().Select(part => part.GetInstanceID()).ToArray();
        var camera = ControlField<Transform>(quake, "cameraTransform");
        var cameraPosition = camera.localPosition;
        var cameraRotation = camera.localRotation;
        var button = ControlField<Button>(quake, "testButton");
        button.onClick.Invoke();
        var start = model.localPosition;
        Assert.That(monster.EmergenceProgress, Is.EqualTo(1f), "Earthquake TEST must also work before a tsunami has been tested.");
        for (var step = 0; step < walk.StepCount; step++)
        {
            var movingFoot = step % 2 == 0 ? left : right;
            var plantedFoot = step % 2 == 0 ? right : left;
            var plantedPosition = plantedFoot.position;
            InvokeCity(quake, "AdvanceShake", walk.StepDuration * 0.5f);
            Assert.That(quake.IsRunning, Is.True);
            Assert.That(button.interactable, Is.False);
            Assert.That(walk.CompletedSteps, Is.EqualTo(step));
            Assert.That(movingFoot.position.y, Is.GreaterThan(plantedFoot.position.y + 0.1f * model.lossyScale.y));
            Assert.That(Vector3.Distance(plantedFoot.position, plantedPosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(camera.localPosition, cameraPosition) + Quaternion.Angle(camera.localRotation, cameraRotation),
                Is.GreaterThan(0.000001f), "The shake must continue between footfalls.");
            InvokeCity(quake, "AdvanceShake", walk.StepDuration * 0.5f);
            Assert.That(walk.CompletedSteps, Is.EqualTo(step + 1));
            Assert.That(quake.IsRunning, Is.True, "The final landing must not stop the shake immediately.");
        }
        var finalPosition = model.localPosition;
        Assert.That(Vector3.Distance(model.position, walk.WalkPoint.position), Is.LessThan(0.0001f), "The last step reaches the authored scene marker exactly.");
        Assert.That(Vector3.Distance(finalPosition, start) / (walk.StepCount - 0.5f), Is.LessThanOrEqualTo(0.5001f), "Distance must add short steps rather than stretch the stride.");
        Assert.That(quake.TestDuration - walk.WalkDuration, Is.EqualTo(2f).Within(0.0001f));
        InvokeCity(quake, "AdvanceShake", 1.99f);
        Assert.That(quake.IsRunning, Is.True);
        Assert.That(model.localPosition, Is.EqualTo(finalPosition));
        InvokeCity(quake, "AdvanceShake", 0.02f);
        Assert.That(quake.IsRunning, Is.False);
        Assert.That(walk.CompletedSteps, Is.EqualTo(walk.StepCount));
        Assert.That(camera.localPosition, Is.EqualTo(cameraPosition));
        Assert.That(camera.localRotation, Is.EqualTo(cameraRotation));
        Assert.That(button.interactable, Is.True);
        Assert.That(model.GetComponentsInChildren<Transform>().Select(part => part.GetInstanceID()).ToArray(), Is.EqualTo(ids));
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        tsunami.StartTsunami();
        Assert.That(walk.CompletedSteps, Is.Zero, "A new tsunami also resets a completed walk's staggered stance.");
        Assert.That(model.Find("Left_Leg").localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(model.Find("Right_Leg").localRotation, Is.EqualTo(Quaternion.identity));
        tsunami.StopTsunami();
        button.onClick.Invoke();
        Assert.That(walk.CompletedSteps, Is.Zero);
        Assert.That(model.localPosition, Is.EqualTo(start));
        InvokeCity(quake, "AdvanceShake", 0.5f);
        quake.StopEarthquake();
        Assert.That(model.localPosition, Is.EqualTo(start));
        Assert.That(left.localPosition.y, Is.EqualTo(right.localPosition.y));
        Assert.That(camera.localPosition, Is.EqualTo(cameraPosition));
        Assert.That(camera.localRotation, Is.EqualTo(cameraRotation));
        yield return null;
    }

    [UnityTest]
    public IEnumerator MonsterTestsHandOverTheSavedModelWithoutLeavingLiftedFeetOrCameraShake()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        tsunami.StartTsunami();
        InvokeCity(tsunami, "AdvanceTsunami", 0.5f);
        quake.StartEarthquake();
        Assert.That(tsunami.IsRunning, Is.False);
        Assert.That(quake.IsRunning, Is.True);
        InvokeCity(quake, "AdvanceShake", walk.StepDuration * 0.5f);
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterEarthquakeWork"));
        Directory.CreateDirectory(directory);
        yield return CaptureCanvas(Path.Combine(directory, "monster-first-step.png"));
        InvokeCity(quake, "AdvanceShake", walk.StepDuration * 2f);
        yield return CaptureCanvas(Path.Combine(directory, "monster-third-step.png"));
        InvokeCity(quake, "AdvanceShake", walk.WalkDuration - ControlField<float>(quake, "elapsed"));
        Assert.That(quake.IsRunning, Is.True);
        Assert.That(walk.CompletedSteps, Is.EqualTo(walk.StepCount));
        yield return CaptureCanvas(Path.Combine(directory, "monster-last-step.png"));
        tsunami.StartTsunami();
        Assert.That(quake.IsRunning, Is.False);
        Assert.That(tsunami.IsRunning, Is.True);
        Assert.That(walk.CompletedSteps, Is.Zero);
        Assert.That(monster.EmergenceProgress, Is.Zero);
        Assert.That(monster.Model.Find("Left_Leg").localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(monster.Model.Find("Right_Leg").localRotation, Is.EqualTo(Quaternion.identity));
        tsunami.StopTsunami();
    }
}
