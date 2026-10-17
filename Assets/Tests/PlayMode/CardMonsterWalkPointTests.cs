using System.Collections;
using System.IO;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator MonsterButtonsEmergeNearbyWalkToTheScenePointThenBreatheDownAtFortySevenDegreesInCardScene()
    {
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterWalkPointWork"));
        Directory.CreateDirectory(directory);
        foreach (var sceneName in new[] { "2DCardScene" })
        {
            yield return SceneManager.LoadSceneAsync(sceneName); yield return null;
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
            var fire = Object.FindFirstObjectByType<FireTestController>();
            var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
            var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
            var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
            var navigation = Object.FindFirstObjectByType<CityCameraController>();
            var marker = walk.WalkPoint;
            Assert.That(marker, Is.Not.Null);
            Assert.That(tsunami.IsRunning || quake.IsRunning || fire.IsRunning, Is.False, "Disasters must wait for their test buttons.");
            var distanceFromShore = tsunami.CityBounds.min.z - tsunami.transform.InverseTransformPoint(monster.transform.position).z;
            Assert.That(distanceFromShore, Is.InRange(2.6f, 3f));
            ControlField<Button>(tsunami, "testButton").onClick.Invoke();
            InvokeCity(tsunami, "AdvanceTsunami", tsunami.TestDuration);
            InvokeCity(navigation, "AdvanceNavigation", 2f);
            var emergePosition = monster.Model.position;
            Assert.That(Vector3.Distance(emergePosition, marker.position), Is.GreaterThan(2f));
            Assert.That(quake.IsRunning || fire.IsRunning, Is.False);

            ControlField<Button>(quake, "testButton").onClick.Invoke();
            var steps = walk.StepCount;
            InvokeCity(quake, "AdvanceShake", walk.WalkDuration);
            Assert.That(Vector3.Distance(monster.Model.position, marker.position), Is.LessThan(0.0001f));
            Assert.That(walk.CompletedSteps, Is.EqualTo(steps));
            Assert.That(quake.IsRunning, Is.True);
            Assert.That(quake.TestDuration - walk.WalkDuration, Is.EqualTo(2f).Within(0.0001f));
            InvokeCity(quake, "AdvanceShake", 2.01f);
            Assert.That(quake.IsRunning, Is.False);
            Assert.That(fire.IsRunning, Is.False);
            var leftFoot = monster.Model.Find("Left_Foot").position;
            var rightFoot = monster.Model.Find("Right_Foot").position;
            ControlField<Button>(fire, "testButton").onClick.Invoke();
            AdvanceMonsterFireTo(fire, breath.FlameStartTime + 0.5f);
            Assert.That(Vector3.Distance(monster.Model.position, marker.position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(monster.Model.Find("Left_Foot").position, leftFoot), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(monster.Model.Find("Right_Foot").position, rightFoot), Is.LessThan(0.0001f));
            var target = ControlField<Vector3>(fire, "ignitionPoint");
            var direction = (target - breath.Mouth.position).normalized;
            Assert.That(Mathf.Asin(-direction.y) * Mathf.Rad2Deg, Is.InRange(45f, 50f));
            Assert.That(Vector3.Angle(breath.Mouth.forward, direction), Is.LessThan(0.1f), "The head and actual fire jet share the same downward aim.");
            Assert.That(Vector3.Distance(breath.Mouth.position, breath.AimedMouthPosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(Vector3.ProjectOnPlane(direction, Vector3.up), monster.Model.forward), Is.LessThan(0.1f),
                "The downward fire stays directly ahead of the body without a sideways turn.");
            var capture = Path.Combine(directory, sceneName + "-downward-fire.png");
            if (sceneName == "2DCardScene")
            {
                game = Object.FindFirstObjectByType<CardGameController>(); game.GetComponent<CardDock>().SetExpanded(false, true);
                CaptureCityWorldFrame(capture);
            }
            else CaptureStandaloneMonster(camera: ControlField<Transform>(quake, "cameraTransform").GetComponent<Camera>(), path: capture);
            fire.StopFire();

            marker.position += new Vector3(0.5f, 0f, -0.25f);
            quake.StartEarthquake(); InvokeCity(quake, "AdvanceShake", quake.TestDuration);
            Assert.That(Vector3.Distance(monster.Model.position, marker.position), Is.LessThan(0.0001f), "Moving the scene point changes the next tested route.");
        }
    }

    [UnityTest]
    public IEnumerator MonsterWalksWhenItsBreathEndsWhileTheCityStillBurnsAndCanBeInterruptedInCardScene()
    {
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterImmediateDepartureWork"));
        Directory.CreateDirectory(directory);
        foreach (var sceneName in new[] { "2DCardScene" })
        {
            yield return SceneManager.LoadSceneAsync(sceneName); yield return null;
            var fire = Object.FindFirstObjectByType<FireTestController>();
            var quake = Object.FindFirstObjectByType<EarthquakeTestController>();
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
            var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
            var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
            var navigation = Object.FindFirstObjectByType<CityCameraController>();
            var model = monster.Model;
            var identity = model.GetInstanceID();
            var target = walk.AfterEventPoint;
            Assert.That(target, Is.Not.Null);
            // Read the point when the breath ends so moving it in the scene changes the departure.
            target.position += new Vector3(-0.2f, 0.1f, 0.3f);
            ControlField<Button>(fire, "testButton").onClick.Invoke();
            AdvanceMonsterFireTo(fire, breath.BreathEndTime - 0.01f);
            Assert.That(walk.IsWalkingAfterEvent, Is.False);
            Assert.That(Vector3.Distance(model.position, walk.WalkPoint.position), Is.LessThan(0.0001f));
            var origin = model.position;
            var rotation = model.rotation;
            var rightFoot = model.Find("Right_Foot").position;
            AdvanceMonsterFireTo(fire, breath.BreathEndTime + 0.001f);
            Assert.That(fire.IsRunning, Is.True);
            Assert.That(quake.IsRunning, Is.False);
            Assert.That(walk.IsWalkingAfterEvent, Is.True);
            Assert.That(model.position, Is.EqualTo(origin), "Departure starts from the fire stance without a teleport.");
            Assert.That(Quaternion.Angle(model.rotation, rotation), Is.LessThan(0.001f));
            Assert.That(model.Find("Right_Foot").position, Is.EqualTo(rightFoot));
            InvokeCity(walk, "AdvanceAfterEventWalk", walk.StepDuration * 0.2f);
            Assert.That(Vector3.Distance(model.Find("Right_Foot").position, rightFoot), Is.LessThan(0.0001f),
                "The planted foot remains fixed while the giant gradually turns into its departure route.");
            Assert.That(Vector3.Distance(model.position, origin), Is.GreaterThan(0.001f));
            Assert.That(Vector3.Distance(model.position, target.position), Is.GreaterThan(1f));
            Assert.That(navigation.InputBlocked, Is.True, "The city fire continues during the departure.");
            var armRotation = model.Find("Left_Arm").localRotation;
            var flameOrigin = ControlField<Vector3>(fire, "lastBreathOrigin");
            InvokeCity(fire, "AdvanceFire", 0.03f);
            Assert.That(model.Find("Left_Arm").localRotation, Is.EqualTo(armRotation), "Recovering from the breath must not overwrite the walking arms.");
            Assert.That(ControlField<Vector3>(fire, "lastBreathOrigin"), Is.EqualTo(flameOrigin), "Trailing gas stays at the last firing origin while the monster moves away.");
            var departureDuration = walk.AfterEventWalkDuration;
            InvokeCity(walk, "AdvanceAfterEventWalk", departureDuration * 0.5f);
            Assert.That(walk.IsWalkingAfterEvent, Is.True);
            Assert.That(Vector3.Distance(model.position, origin), Is.GreaterThan(1f));
            Assert.That(quake.IsRunning, Is.False, "Departure does not restart the earthquake test.");
            var departurePosition = model.position;
            armRotation = model.Find("Left_Arm").localRotation;
            AdvanceMonsterFireTo(fire, fire.TestDuration);
            Assert.That(fire.IsRunning, Is.False);
            Assert.That(model.position, Is.EqualTo(departurePosition));
            Assert.That(model.Find("Left_Arm").localRotation, Is.EqualTo(armRotation));
            Assert.That(walk.IsWalkingAfterEvent, Is.True, "City fire completion cannot restart or stop the current walk.");
            Assert.That(navigation.InputBlocked, Is.False);
            InvokeCity(walk, "AdvanceAfterEventWalk", departureDuration);
            Assert.That(walk.IsWalkingAfterEvent, Is.False);
            Assert.That(Vector3.Distance(model.position, target.position), Is.LessThan(0.0001f));
            Assert.That(model.GetInstanceID(), Is.EqualTo(identity), "The saved hierarchy model walks to the target.");
            var capture = Path.Combine(directory, sceneName + "-after-event.png");
            if (sceneName == "2DCardScene") CaptureCityWorldFrame(capture);
            else CaptureStandaloneMonster(ControlField<Transform>(quake, "cameraTransform").GetComponent<Camera>(), capture);

            fire.StartFire();
            Assert.That(walk.IsWalkingAfterEvent, Is.False);
            Assert.That(Vector3.Distance(model.position, walk.WalkPoint.position), Is.LessThan(0.0001f), "Repeating FIRE restores its original firing point.");
            fire.StopFire();
            InvokeCity(walk, "AdvanceAfterEventWalk", 2f);
            Assert.That(Vector3.Distance(model.position, walk.WalkPoint.position), Is.LessThan(0.0001f), "Cancelling fire must not launch a departure.");

            fire.StartFire(); AdvanceMonsterFireTo(fire, fire.TestDuration);
            InvokeCity(walk, "AdvanceAfterEventWalk", 2f);
            fire.StopFire();
            var stopped = model.position;
            InvokeCity(walk, "AdvanceAfterEventWalk", 2f);
            Assert.That(model.position, Is.EqualTo(stopped), "Stopping the preview also stops an active departure.");
            quake.StartEarthquake(); InvokeCity(quake, "AdvanceShake", quake.TestDuration);
            Assert.That(walk.IsWalkingAfterEvent, Is.False);
            Assert.That(Vector3.Distance(model.position, walk.WalkPoint.position), Is.LessThan(0.0001f));

            fire.StartFire(); AdvanceMonsterFireTo(fire, fire.TestDuration);
            InvokeCity(walk, "AdvanceAfterEventWalk", 2f);
            tsunami.StartTsunami();
            Assert.That(walk.IsWalkingAfterEvent, Is.False);
            InvokeCity(tsunami, "AdvanceTsunami", tsunami.TestDuration);
            InvokeCity(navigation, "AdvanceNavigation", 2f);
            Assert.That(Vector3.Distance(model.position, monster.EmergedWorldPosition), Is.LessThan(0.0001f),
                "Restarting TSUNAMI returns to the authored emergence point without accumulated movement.");
        }
    }

    private static void CaptureStandaloneMonster(Camera camera, string path)
    {
        var oldTarget = camera.targetTexture; var oldAspect = camera.aspect; var oldActive = RenderTexture.active;
        var target = new RenderTexture(1280, 640, 24); var pixels = new Texture2D(1280, 640, TextureFormat.RGB24, false);
        camera.targetTexture = target; camera.aspect = 2f; camera.Render(); RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1280, 640), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
        camera.targetTexture = oldTarget; camera.aspect = oldAspect; RenderTexture.active = oldActive;
        target.Release(); Object.Destroy(target); Object.Destroy(pixels);
    }
}
