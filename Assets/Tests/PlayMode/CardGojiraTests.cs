using System.Collections;
using System.IO;
using System.Linq;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class CardGamePlayModeTests
{
    [UnityTest]
    public IEnumerator GojiraBowsAndPlaysTheSourceFlameAnimationFromItsMouthOnEveryBreath()
    {
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        var monster = Object.FindFirstObjectByType<SeaMonsterEmergence>();
        var preset = ControlField<FireVisualPreset>(fire, "visualPreset");
        Assert.That(preset.BreathAnimation.name, Is.EqualTo("Scene"));
        Assert.That(preset.BreathAnimationRange.x, Is.GreaterThan(1f), "Skip the source clip's empty preparation frames.");
        Assert.That(preset.BreathAnimationRange.y, Is.LessThanOrEqualTo(preset.BreathAnimation.length));
        var directory = Path.GetFullPath("Library/GojiraBreathWork/Previews");
        Directory.CreateDirectory(directory);
        fire.StartFire();
        var head = monster.Model.Find("Head");
        var standingHead = head.localPosition;
        var feet = new[] { monster.Model.Find("Left_Foot").position, monster.Model.Find("Right_Foot").position };
        AdvanceMonsterFireTo(fire, breath.FlameStartTime + .3f);
        var core = ControlField<GameObject>(fire, "visuals").transform.Find("Mouth_Fire_Stream/Authored_Flame_Animation");
        Assert.That(core, Is.Not.Null);
        var flame = core.Find("flame1");
        var firstScale = flame.localScale;
        var firstRotation = flame.localRotation;
        Assert.That(head.localPosition.y, Is.LessThan(standingHead.y - .25f));
        Assert.That(firstScale.y, Is.GreaterThan(.01f), "Fire must already be visible when the aimed breath starts.");
        CaptureCityWorldFrame(Path.Combine(directory, "01-aimed-animated-breath.png"));
        AdvanceMonsterFireTo(fire, breath.FlameStartTime + 1.7f);
        Assert.That(Quaternion.Angle(firstRotation, flame.localRotation), Is.GreaterThan(5f), "Play the source flame's rotation curve.");
        var flameVertices = flame.GetComponent<MeshFilter>().sharedMesh.vertices;
        Assert.That(flameVertices.Min(point => Vector3.Distance(flame.TransformPoint(point), breath.Mouth.position)), Is.LessThan(.0001f),
            "The visible mesh itself starts at the lips.");
        Assert.That(monster.Model.Find("Left_Foot").position, Is.EqualTo(feet[0]));
        Assert.That(monster.Model.Find("Right_Foot").position, Is.EqualTo(feet[1]));
        CaptureCityWorldFrame(Path.Combine(directory, "02-source-flame-motion.png"));
        AdvanceMonsterFireTo(fire, breath.BreathEndTime + breath.TravelDuration + .05f);
        Assert.That(core.gameObject.activeInHierarchy, Is.False);
        fire.StopFire();
        fire.StartFire();
        AdvanceMonsterFireTo(fire, breath.FlameStartTime + .3f);
        Assert.That(flame.localScale, Is.EqualTo(firstScale));
        Assert.That(flame.localRotation, Is.EqualTo(firstRotation));
        fire.StopFire();
        Assert.That(head.localPosition, Is.EqualTo(standingHead));
        Assert.That(head.localRotation, Is.EqualTo(Quaternion.identity));
        yield return null;
    }

    [UnityTest]
    public IEnumerator MouthFireTravelsDownToTheGroundWithoutTurningBackUp()
    {
        var fire = Object.FindFirstObjectByType<FireTestController>();
        var breath = Object.FindFirstObjectByType<SeaMonsterFireBreath>();
        var particles = new ParticleSystem.Particle[640];
        fire.StartFire();
        var stream = ControlField<GameObject>(fire, "visuals").transform.Find("Mouth_Fire_Stream");
        var gas = stream.Find("Rolling_Gas_Wisps").GetComponent<ParticleSystem>();
        var core = stream.Find("Authored_Flame_Animation/flame1");
        var points = core.GetComponent<MeshFilter>().sharedMesh.vertices;
        var target = ControlField<Vector3>(fire, "ignitionPoint");
        Assert.That(breath.BreathDownAngle, Is.EqualTo(55.5f).Within(.001f));
        Assert.That(stream.Find("Breath_Black_Smoke"), Is.Null);
        Assert.That(target.y, Is.LessThan(breath.AimedMouthPosition.y - .5f));
        foreach (var time in new[] { .2f, .8f, 1.6f, 2.8f, 3.7f })
        {
            AdvanceMonsterFireTo(fire, breath.FlameStartTime + time);
            var count = gas.GetParticles(particles);
            Assert.That(count, Is.GreaterThan(20));
            Assert.That(points.Min(point => Vector3.Distance(core.TransformPoint(point), breath.Mouth.position)), Is.LessThan(.0001f));
            Assert.That(Vector3.Angle(core.up, breath.FireDirection), Is.LessThan(.1f));
            for (var i = 0; i < count; i++)
            {
                var particle = particles[i];
                Assert.That(particle.position.y, Is.InRange(target.y, breath.Mouth.position.y));
                Assert.That(particle.velocity.y, Is.LessThan(0f), "Every flame moves down from the mouth.");
                Assert.That(Vector3.Angle(particle.velocity, breath.FireDirection), Is.LessThanOrEqualTo(28.01f));
                var direction = Quaternion.Euler(particle.rotation3D) * Vector3.up;
                Assert.That(direction.y, Is.LessThan(0f), "Flame tips point down with their travel direction.");
                var tip = particle.position + direction * particle.startSize3D.y;
                Assert.That(tip.y, Is.GreaterThanOrEqualTo(target.y - .0001f), "Flames finish at the ground plane.");
            }
        }
        fire.StopFire();
        yield return null;
    }

    [UnityTest]
    public IEnumerator GojiraGeometryWalksAndBendsWhileItsFeetStayPlantedDuringFire()
    {
        game.GetComponent<CivilGameJam.Cards.CardDock>().SetExpanded(false, true);
        var rig = new SequencePreview();
        var skin = rig.Monster.Model.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Assert.That(rig.Monster.Model.name, Is.EqualTo("Gojira_Model"));
        Assert.That(skin, Is.Not.Null);
        var baked = new Mesh();
        var directory = Path.GetFullPath("Library/GojiraWork");
        Directory.CreateDirectory(directory);
        try
        {
            rig.Sequence.SelectSequence(3); rig.Sequence.StartSelectedSequence();
            rig.Until(() => rig.Sequence.Stage == MonsterSequenceStage.WalkingToFirstPoint);
            skin.BakeMesh(baked);
            var standing = baked.vertices;
            rig.For(rig.Walk.StepDuration * .5f);
            skin.BakeMesh(baked);
            Assert.That(baked.vertices.Where((point, i) => Vector3.Distance(point, standing[i]) > .05f).Count(), Is.GreaterThan(20),
                "Walking must move the imported geometry as well as the saved limb controls.");
            yield return CaptureCanvas(Path.Combine(directory, "gojira-walking.png"));
            rig.Until(() => rig.Sequence.Stage == MonsterSequenceStage.Fire);
            rig.For(rig.Breath.ChargeDuration * .5f);
            skin.BakeMesh(baked);
            var beforeBreath = baked.vertices;
            var weights = skin.sharedMesh.boneWeights;
            var feet = Enumerable.Range(0, weights.Length).Where(i =>
                (weights[i].boneIndex2 == 3 || weights[i].boneIndex2 == 6) && weights[i].weight2 > .99999f).ToArray();
            var head = Enumerable.Range(0, weights.Length).Where(i => weights[i].boneIndex1 == 1 && weights[i].weight1 > .99f).ToArray();
            Assert.That(feet.Length, Is.GreaterThan(10));
            Assert.That(head.Length, Is.GreaterThan(10));
            rig.For(rig.Breath.FlameStartTime + .1f - rig.Breath.ChargeDuration * .5f);
            skin.BakeMesh(baked);
            var breathing = baked.vertices;
            Assert.That(feet.Max(i => Vector3.Distance(beforeBreath[i], breathing[i])), Is.LessThan(.0001f));
            Assert.That(head.Max(i => Vector3.Distance(beforeBreath[i], breathing[i])), Is.GreaterThan(.1f));
            Assert.That(Vector3.Distance(rig.Breath.Mouth.position, rig.Breath.AimedMouthPosition), Is.LessThan(.001f),
                "The aimed fire origin must follow Gojira's posed mouth.");
            yield return CaptureCanvas(Path.Combine(directory, "gojira-breathing.png"));
            rig.Sequence.StopSequence();
            Assert.That(rig.Monster.Model.gameObject.activeSelf, Is.False);
            Assert.That(rig.Monster.Model.Find("Head").localRotation, Is.EqualTo(Quaternion.identity));
        }
        finally { Object.Destroy(baked); }
    }
}
