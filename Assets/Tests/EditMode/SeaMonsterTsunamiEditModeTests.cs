using System.Linq;
using CivilGameJam;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SeaMonsterTsunamiEditModeTests
{
    [Test]
    public void TsunamiAftermathDelayIsSavedOnTheExistingSequenceBeforePlay()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var sequence = Object.FindFirstObjectByType<MonsterSequenceController>();
        Assert.That(new SerializedObject(sequence).FindProperty("tsunamiAftermathSeconds").floatValue, Is.EqualTo(4f));
        Assert.That(Object.FindFirstObjectByType<SeaMonsterFireBreath>().FlameStartTime, Is.EqualTo(3.25f).Within(.001f));
    }

    [Test]
    public void EarthquakeSavesItsPreloadedStartLoopAndEndAudioBeforePlay()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var sequence = Object.FindFirstObjectByType<MonsterSequenceController>();
        var data = new SerializedObject(Object.FindFirstObjectByType<EarthquakeTestController>());
        var source = (AudioSource)data.FindProperty("earthquakeAudio").objectReferenceValue;
        Assert.That(source, Is.Not.Null);
        Assert.That(source.gameObject, Is.SameAs(sequence.gameObject), "The sound source must remain active when the monster mesh is hidden.");
        Assert.That(source.gameObject.activeInHierarchy, Is.True);
        Assert.That(source.clip, Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/EarthquakeSong/earth quake start loop.wav")));
        Assert.That(source.playOnAwake || source.loop, Is.False);
        Assert.That(source.spatialBlend, Is.Zero, "The earthquake must be audible from the normal city camera.");
        Assert.That(source.volume, Is.EqualTo(.55f).Within(.00001f));
        Assert.That(source.pitch, Is.EqualTo(1f).Within(.00001f), "Keep the original recording speed before and during Play Mode.");
        foreach (var clip in new[] { source.clip, (AudioClip)data.FindProperty("loopSound").objectReferenceValue, (AudioClip)data.FindProperty("endSound").objectReferenceValue })
        {
            Assert.That(clip, Is.Not.Null);
            var path = AssetDatabase.GetAssetPath(clip);
            Assert.That(path, Does.StartWith("Assets/Art/EarthquakeSong/"));
            var settings = ((AudioImporter)AssetImporter.GetAtPath(path)).defaultSampleSettings;
            Assert.That(settings.preloadAudioData, Is.True);
            Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM));
        }
    }

    [TestCase("Assets/2DCardScene.unity")]
    public void MonsterAndSeaAreSavedOnTheNewIncomingSideBeforePlay(string path)
    {
        var scene = EditorSceneManager.OpenScene(path);
        var tsunami = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TsunamiTestController>()).Single();
        var data = new SerializedObject(tsunami);
        var monster = (SeaMonsterEmergence)data.FindProperty("seaMonster").objectReferenceValue;
        Assert.That(monster, Is.Not.Null, "The test button must reference an already saved monster.");
        Assert.That(Vector3.Dot(tsunami.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));
        Assert.That(tsunami.transform.InverseTransformPoint(monster.transform.position).z, Is.LessThan(tsunami.CityBounds.min.z));
        Assert.That(Vector3.Dot(monster.transform.forward, tsunami.transform.forward), Is.GreaterThan(0.999f));
        var model = monster.Model;
        Assert.That(model.name, Is.EqualTo("Gojira_Model"));
        Assert.That(Vector3.Distance(model.localScale, Vector3.one * 0.51f), Is.LessThan(0.00001f));
        var sequence = monster.GetComponent<MonsterSequenceController>();
        Assert.That(sequence, Is.Not.Null, "Sequence controls and their menu must be saved before Play.");
        var sequenceData = new SerializedObject(sequence);
        Assert.That(sequenceData.FindProperty("cameraZoomFactor").floatValue, Is.EqualTo(1.3f));
        Assert.That(sequenceData.FindProperty("cameraZoomSeconds").floatValue, Is.EqualTo(1.8f));
        Assert.That(sequenceData.FindProperty("cameraReturnSeconds").floatValue, Is.EqualTo(.8f));
        Assert.That(sequenceData.FindProperty("sequenceButtons").arraySize, Is.EqualTo(3));
        Assert.That(sequenceData.FindProperty("startButton").objectReferenceValue, Is.Not.Null);
        Assert.That(sequenceData.FindProperty("stopButton").objectReferenceValue, Is.Not.Null);
        var roundData = new SerializedObject(Object.FindFirstObjectByType<EarthquakeRoundController>());
        Assert.That(roundData.FindProperty("sequence").objectReferenceValue, Is.SameAs(sequence));
        Assert.That(roundData.FindProperty("readyButton").objectReferenceValue, Is.Not.Null,
            "READY remains the saved playable-round entry point alongside the restored debug controls.");
        Assert.That(model.Find("Left_Leg"), Is.Not.Null);
        Assert.That(model.Find("Right_Leg"), Is.Not.Null);
        var skin = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Assert.That(skin, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(skin.sharedMesh), Is.EqualTo("Assets/Art/Monsters/Gojira/GojiraRigged.asset"));
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/emreAnimation/Gojira.fbx").GetComponentInChildren<MeshFilter>();
        Assert.That(skin.sharedMesh.vertexCount, Is.EqualTo(source.sharedMesh.vertexCount));
        Assert.That(skin.sharedMesh.subMeshCount, Is.EqualTo(source.sharedMesh.subMeshCount));
        Assert.That(skin.sharedMaterials, Is.EqualTo(source.GetComponent<MeshRenderer>().sharedMaterials));
        Assert.That(skin.bones.Length, Is.EqualTo(8));
        Assert.That(skin.bones.All(bone => bone != null && bone.IsChildOf(model)), Is.True);
        Assert.That(model.GetComponentsInChildren<Renderer>(true).Max(renderer => renderer.bounds.max.y), Is.GreaterThan(tsunami.SeaLevel));
        Assert.That(tsunami.transform.Find("Sea_Surface"), Is.Not.Null);
        Assert.That(model.gameObject.activeSelf, Is.False, "The imported monster must not change the idle city view.");
        Assert.That(tsunami.transform.Find("Sea_Surface").gameObject.activeSelf, Is.False);
        Assert.That(new SerializedObject(monster).FindProperty("seaSurface").objectReferenceValue,
            Is.SameAs(tsunami.transform.Find("Sea_Surface").gameObject));
        Assert.That(tsunami.CityBounds.min.z - tsunami.transform.InverseTransformPoint(monster.transform.position).z, Is.InRange(2.6f, 3f),
            "The monster now emerges close to the city.");
        var walk = monster.GetComponent<SeaMonsterWalk>();
        Assert.That(walk, Is.Not.Null, "Walking must use a component saved before Play.");
        Assert.That(walk.WalkPoint, Is.Not.Null);
        Assert.That(walk.WalkPoint.name, Is.EqualTo("monsterWalkPoint"));
        Assert.That(walk.AfterEventPoint, Is.Not.Null);
        Assert.That(walk.AfterEventPoint.name, Is.EqualTo("monsterWalkAfterEventPoint"));
        var departure = tsunami.transform.InverseTransformPoint(walk.AfterEventPoint.position);
        Assert.That(departure.x, Is.EqualTo(.006f).Within(.0001f));
        Assert.That(departure.y, Is.EqualTo(.046f).Within(.0001f));
        Assert.That(departure.z - tsunami.CityBounds.max.z, Is.InRange(.85f, .88f),
            "The departure point must keep its clearance beyond the current map's back edge.");
        var earthquake = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EarthquakeTestController>()).Single();
        var earthquakeData = new SerializedObject(earthquake);
        var navigation = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CityCameraController>()).Single();
        Assert.That(sequenceData.FindProperty("navigation").objectReferenceValue, Is.SameAs(navigation));
        var fire = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FireTestController>()).Single();
        foreach (var controller in new MonoBehaviour[] { tsunami, earthquake, fire })
            Assert.That(new SerializedObject(controller).FindProperty("navigation").objectReferenceValue, Is.SameAs(navigation),
                "Every saved disaster must lock the same player camera.");
        Assert.That(earthquakeData.FindProperty("seaMonsterWalk").objectReferenceValue, Is.SameAs(walk));
        Assert.That(earthquakeData.FindProperty("afterLastStepDelay").floatValue, Is.EqualTo(2f));
        var breath = monster.GetComponent<SeaMonsterFireBreath>();
        Assert.That(breath, Is.Not.Null, "The fire animation component is authored before Play.");
        Assert.That(breath.Mouth, Is.SameAs(model.Find("Muzzle/Flame_Origin")));
        Assert.That(new SerializedObject(breath).FindProperty("neckPivot").objectReferenceValue, Is.SameAs(model.Find("Neck_Pivot")));
        Assert.That(breath.BreathDownAngle, Is.EqualTo(55.5f).Within(.001f));
        Assert.That(new SerializedObject(breath).FindProperty("headParts").arraySize, Is.EqualTo(2));
        Assert.That(new SerializedObject(fire).FindProperty("seaMonsterFire").objectReferenceValue, Is.SameAs(breath));
        Assert.That(earthquakeData.FindProperty("fire").objectReferenceValue, Is.SameAs(fire));
        Assert.That(data.FindProperty("fire").objectReferenceValue, Is.SameAs(fire));
        foreach (var side in new[] { "left", "right" })
            foreach (var part in new[] { "Leg", "Foot", "Arm" })
                Assert.That(new SerializedObject(walk).FindProperty(side + part).objectReferenceValue, Is.Not.Null);
    }
}
