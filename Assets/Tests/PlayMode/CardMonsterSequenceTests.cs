using System;
using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam;
using CivilGameJam.Cards;
using CivilGameJam.Disasters;
using CivilGameJam.Upgrades;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator MonsterSequenceMenuSelectsAllThreeSavedOptionsAndKeepsStopClickableDuringTheEvent()
    {
        game.GetComponent<CardDock>().SetExpanded(false, true);
        var rig = new SequencePreview();
        var sequence = rig.Sequence;
        var menu = ControlField<Button>(sequence, "menuButton");
        var start = ControlField<Button>(sequence, "startButton");
        var stop = ControlField<Button>(sequence, "stopButton");
        var choices = ControlField<GameObject>(sequence, "choices");
        var options = ControlField<Button[]>(sequence, "sequenceButtons");
        var uiIds = SequenceUIIds();
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MonsterSequencesWork"));
        Directory.CreateDirectory(directory);
        Assert.That(options.Length, Is.EqualTo(3));
        Assert.That(choices.activeSelf, Is.False);
        yield return CaptureCanvas(Path.Combine(directory, "01-sequence-menu.png"), afterRender: uiCamera =>
        {
            AssertTopButton(menu, uiCamera); AssertTopButton(start, uiCamera);
        });
        menu.onClick.Invoke();
        yield return CaptureCanvas(Path.Combine(directory, "02-sequence-options.png"), afterRender: uiCamera =>
        {
            Assert.That(choices.activeSelf, Is.True);
            foreach (var option in options) AssertTopButton(option, uiCamera);
            options[1].onClick.Invoke();
            Assert.That(sequence.SelectedSequence, Is.EqualTo(2));
            Assert.That(choices.activeSelf, Is.False);
        });
        menu.onClick.Invoke();
        options[2].onClick.Invoke();
        start.onClick.Invoke();
        Assert.That(sequence.IsRunning, Is.True);
        Assert.That(rig.Navigation.InputBlocked, Is.True);
        Assert.That(stop.IsInteractable(), Is.True);
        Assert.That(start.IsInteractable() || menu.IsInteractable(), Is.False);
        yield return CaptureCanvas(Path.Combine(directory, "03-sequence-running.png"), afterRender: uiCamera =>
        {
            AssertTopButton(stop, uiCamera);
            stop.onClick.Invoke();
            Assert.That(sequence.IsRunning, Is.False);
        });
        Assert.That(SequenceUIIds(), Is.EqualTo(uiIds), "All menu controls already exist in the scene before the preview starts.");
    }

    [UnityTest]
    public IEnumerator MonsterSequencesRunTheThreeRequestedRoutesWithASmallerSavedMonsterInCardScene()
    {
        foreach (var sceneName in new[] { "2DCardScene" })
        {
            yield return SceneManager.LoadSceneAsync(sceneName); yield return null;
            var rig = new SequencePreview();
            var sequence = rig.Sequence;
            var model = rig.Monster.Model;
            var identity = model.GetInstanceID();
            var uiIds = SequenceUIIds();
            Assert.That(Vector3.Distance(model.localScale, Vector3.one * 0.51f), Is.LessThan(0.00001f));
            for (var number = 1; number <= 3; number++)
            {
                ControlField<Button[]>(sequence, "sequenceButtons")[number - 1].onClick.Invoke();
                ControlField<Button>(sequence, "startButton").onClick.Invoke();
                Assert.That(sequence.ActiveSequence, Is.EqualTo(number));
                Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Emerging));
                Assert.That(rig.Monster.EmergenceProgress, Is.Zero);
                Assert.That(rig.Tsunami.IsRunning, Is.EqualTo(number >= 2));
                Assert.That(rig.Quake.IsRunning || rig.Fire.IsRunning, Is.False);
                sequence.SelectSequence(number == 3 ? 1 : 3);
                Assert.That(sequence.SelectedSequence, Is.EqualTo(number), "An active route cannot change mid-event.");
                rig.For(rig.Monster.WaveReleaseDelay + 0.15f);
                Assert.That(rig.Monster.EmergenceProgress, Is.InRange(0.1f, 0.99f));
                Assert.That(rig.Quake.IsRunning, Is.False);
                if (number >= 2)
                    Assert.That(rig.Tsunami.transform.Find("Tsunami_Water").gameObject.activeSelf, Is.True, "The wave forms during emergence.");
                else Assert.That(rig.Tsunami.FloodCoverage, Is.Zero);
                rig.Until(() => sequence.Stage == MonsterSequenceStage.WalkingToFirstPoint);
                Assert.That(rig.Quake.IsRunning, Is.True);
                Assert.That(rig.Tsunami.IsRunning, Is.EqualTo(number >= 2), "Walking can begin while tsunami water is still draining.");
                rig.For(0.3f);
                Assert.That(Vector3.Distance(model.position, rig.Monster.EmergedWorldPosition), Is.GreaterThan(0.001f));
                if (number >= 2)
                {
                    var position = model.position;
                    InvokeCity(rig.Tsunami, "AdvanceTsunami", rig.Tsunami.TestDuration);
                    Assert.That(model.position, Is.EqualTo(position), "Wave updates and completion cannot move a walking monster back to the sea.");
                }
                if (number == 3)
                {
                    rig.Until(() => sequence.Stage == MonsterSequenceStage.Fire);
                    Assert.That(Vector3.Distance(model.position, rig.Walk.WalkPoint.position), Is.LessThan(0.0001f));
                    Assert.That(rig.Quake.IsRunning, Is.False);
                    Assert.That(rig.Fire.IsRunning, Is.True);
                    rig.For(rig.Breath.FlameStartTime + 0.2f);
                    Assert.That(rig.Breath.Lean, Is.GreaterThan(0.99f));
                    Assert.That(Mathf.Asin(-rig.Breath.FireDirection.y) * Mathf.Rad2Deg, Is.InRange(45f, 50f));
                    Assert.That(Vector3.Angle(Vector3.ProjectOnPlane(rig.Breath.FireDirection, Vector3.up), model.forward), Is.LessThan(0.1f));
                }
                rig.Until(() => sequence.Stage == MonsterSequenceStage.WalkingToSecondPoint);
                Assert.That(rig.Fire.IsRunning, Is.EqualTo(number == 3), "Departure starts while the city is still burning.");
                Assert.That(rig.Quake.IsRunning, Is.EqualTo(number < 3), "The first two routes leave point 1 before the earthquake tail finishes.");
                Assert.That(rig.Walk.IsWalkingAfterEvent, Is.True);
                Assert.That(Vector3.Distance(model.position, rig.Walk.WalkPoint.position), Is.LessThan(0.01f));
                if (number == 3)
                    Assert.That(ControlField<float>(rig.Fire, "elapsed"), Is.InRange(rig.Breath.BreathEndTime, rig.Breath.BreathEndTime + 0.1f));
                rig.For(0.3f);
                Assert.That(Vector3.Distance(model.position, rig.Walk.WalkPoint.position), Is.GreaterThan(0.001f));
                Assert.That(model.gameObject.activeSelf, Is.True, "The boss remains visible while walking to the second point.");
                if (number < 3)
                {
                    var position = model.position;
                    InvokeCity(rig.Quake, "AdvanceShake", 0.1f);
                    Assert.That(model.position, Is.EqualTo(position), "The remaining quake must not take control of the second walking route.");
                }
                Assert.That(rig.Navigation.InputBlocked, Is.True, "The complete sequence owns camera input through the second walk.");
                foreach (var button in ControlField<Button[]>(sequence, "individualTestButtons")) Assert.That(button.interactable, Is.False);
                rig.Until(() => !sequence.IsRunning);
                Assert.That(sequence.Stage, Is.EqualTo(MonsterSequenceStage.Complete));
                Assert.That(Vector3.Distance(model.position, rig.Walk.AfterEventPoint.position), Is.LessThan(0.0001f));
                Assert.That(model.gameObject.activeSelf, Is.False, "The saved boss disappears on arrival and is reused on replay.");
                Assert.That(rig.Walk.IsWalkingAfterEvent || rig.Tsunami.IsRunning || rig.Quake.IsRunning || rig.Fire.IsRunning, Is.False);
                Assert.That(rig.Navigation.InputBlocked && rig.Navigation.IsEventCameraActive, Is.True,
                    "The smooth camera return continues after the sequence finishes.");
                InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
                Assert.That(rig.Navigation.InputBlocked, Is.False);
                Assert.That(model.GetInstanceID(), Is.EqualTo(identity));
                Assert.That(SequenceUIIds(), Is.EqualTo(uiIds));
                foreach (var button in ControlField<Button[]>(sequence, "individualTestButtons")) Assert.That(button.interactable, Is.True);
            }
        }
    }

    [UnityTest]
    public IEnumerator MonsterSequenceKeepsTheCityFireRunningAfterReachingANearbySecondPoint()
    {
        var rig = new SequencePreview();
        rig.Walk.AfterEventPoint.position = rig.Walk.WalkPoint.position + Vector3.forward * 0.35f;
        rig.Sequence.SelectSequence(3); rig.Sequence.StartSelectedSequence();
        rig.Until(() => rig.Sequence.Stage == MonsterSequenceStage.WalkingToSecondPoint);
        rig.Until(() => !rig.Walk.IsWalkingAfterEvent);
        var position = rig.Monster.Model.position;
        Assert.That(Vector3.Distance(position, rig.Walk.AfterEventPoint.position), Is.LessThan(0.0001f));
        Assert.That(rig.Monster.Model.gameObject.activeSelf, Is.False, "Disappearance does not wait for the remaining city fire.");
        Assert.That(rig.Fire.IsRunning && rig.Sequence.IsRunning && rig.Navigation.InputBlocked, Is.True,
            "Reaching the second point cannot cut the city fire short or release the active event's camera lock.");
        rig.Until(() => !rig.Sequence.IsRunning);
        Assert.That(rig.Monster.Model.position, Is.EqualTo(position), "Finishing the fire cannot restart the departure.");
        InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
        Assert.That(rig.Fire.IsRunning || rig.Navigation.InputBlocked, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MonsterSequenceStopsAndReplaysFromEveryStageWithoutLeavingCameraLocksOrMotion()
    {
        var rig = new SequencePreview();
        rig.Sequence.SelectSequence(3);
        foreach (var stage in new[] { MonsterSequenceStage.Emerging, MonsterSequenceStage.WalkingToFirstPoint,
            MonsterSequenceStage.Fire, MonsterSequenceStage.WalkingToSecondPoint })
        {
            rig.Sequence.StartSelectedSequence();
            rig.Until(() => rig.Sequence.Stage == stage);
            rig.For(0.2f);
            rig.Sequence.StopSequence();
            InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
            Assert.That(rig.Sequence.Stage, Is.EqualTo(MonsterSequenceStage.Ready));
            Assert.That(rig.Sequence.IsRunning || rig.Quake.IsRunning || rig.Fire.IsRunning || rig.Tsunami.IsRunning || rig.Walk.IsWalkingAfterEvent, Is.False);
            Assert.That(rig.Monster.EmergenceProgress, Is.Zero);
            Assert.That(rig.Navigation.InputBlocked, Is.False);
            Assert.That(rig.Breath.Heat + rig.Breath.Lean, Is.Zero);
            var position = rig.Monster.Model.position;
            rig.For(2f);
            Assert.That(rig.Monster.Model.position, Is.EqualTo(position), "Cancelled phases cannot resume walking or breathing fire.");
        }
        rig.Sequence.StartSelectedSequence();
        rig.For(0.5f);
        rig.Sequence.enabled = false;
        InvokeCity(rig.Navigation, "AdvanceNavigation", 2f);
        Assert.That(rig.Sequence.IsRunning || rig.Navigation.InputBlocked, Is.False);
        rig.Sequence.enabled = true;
        rig.Sequence.SelectSequence(1);
        rig.Sequence.StartSelectedSequence();
        rig.Until(() => !rig.Sequence.IsRunning);
        Assert.That(Vector3.Distance(rig.Monster.Model.position, rig.Walk.AfterEventPoint.position), Is.LessThan(0.0001f));
        yield return null;
    }

    private static int[] SequenceUIIds() => Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(rect => rect.GetComponent<TMPro.TextMeshPro>() == null)
        .Select(rect => rect.GetInstanceID()).OrderBy(id => id).ToArray();

    private sealed class SequencePreview
    {
        public readonly MonsterSequenceController Sequence = Object.FindFirstObjectByType<MonsterSequenceController>();
        public readonly SeaMonsterEmergence Monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        public readonly SeaMonsterWalk Walk = Object.FindFirstObjectByType<SeaMonsterWalk>();
        public readonly SeaMonsterFireBreath Breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        public readonly TsunamiTestController Tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        public readonly EarthquakeTestController Quake = Object.FindFirstObjectByType<EarthquakeTestController>();
        public readonly FireTestController Fire = Object.FindFirstObjectByType<FireTestController>();
        public readonly CityCameraController Navigation = Object.FindFirstObjectByType<CityCameraController>();

        public void For(float seconds)
        {
            while (seconds > 0.0001f) { var step = Mathf.Min(seconds, 0.05f); Advance(step); seconds -= step; }
        }

        public void Until(Func<bool> finished)
        {
            var steps = 0;
            while (!finished() && steps++ < 2400) Advance(0.05f);
            Assert.That(finished(), Is.True, "Sequence did not reach the expected phase; current phase: " + Sequence.Stage);
        }

        private void Advance(float step)
        {
            if (Tsunami.IsRunning) InvokeCity(Tsunami, "AdvanceTsunami", step);
            if (Quake.IsRunning) InvokeCity(Quake, "AdvanceShake", step);
            if (Fire.IsRunning) InvokeCity(Fire, "AdvanceFire", step);
            if (Walk.IsWalkingAfterEvent) InvokeCity(Walk, "AdvanceAfterEventWalk", step);
            InvokeCity(Sequence, "AdvanceSequence", step);
            InvokeCity(Sequence, "LateUpdate");
            InvokeCity(Navigation, "AdvanceNavigation", step);
        }
    }
}
