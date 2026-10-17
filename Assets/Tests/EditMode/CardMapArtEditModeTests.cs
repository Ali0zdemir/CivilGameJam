using System.Linq;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardMapArtEditModeTests
{
    [Test]
    public void ArtistMapKeepsTheSceneTunedTsunamiPoseAndGameplayReferences()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        const string sourcePath = "Assets/EfeArts/Map(Guncel1).fbx";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        var waterSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EfeArts/Map(Guncel1).fbx");
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var data = new SerializedObject(tsunami);
        var ground = tsunami.GroundSurface;
        Assert.That(ground.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(source.transform.Find("Tiles").GetComponent<MeshFilter>().sharedMesh));
        Assert.That(ground.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(source.transform.Find("Tiles").GetComponent<MeshRenderer>().sharedMaterials));
        var scenery = ground.parent;
        Assert.That(scenery.name, Is.EqualTo("Map(Guncel1)"));
        Assert.That(scenery.gameObject.activeInHierarchy, Is.True, "The actual Map(Guncel1) model must be the active main map before Play.");
        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(scenery.gameObject), Is.EqualTo(sourcePath));
        Assert.That(tsunami.transform.Find("Map_Scenery").gameObject.activeSelf, Is.False);
        Assert.That(Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .Any(filter => filter.name == "Gojira"), Is.False,
            "The raw model preview must not add a second unanimated monster or an idle tsunami.");
        Assert.That(scenery.Find("Gojira").gameObject.activeSelf, Is.False);
        Assert.That(scenery.Find("TSUNAMI").gameObject.activeSelf, Is.False);
        foreach (var name in new[] { "map_coast", "map_forest", "map_forest.001", "map_sea" })
        {
            var part = scenery.Find(name);
            var partSource = (name == "map_sea" ? waterSource : source).transform.Find(name);
            Assert.That(part.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(partSource.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(part.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(partSource.GetComponent<MeshRenderer>().sharedMaterials));
        }
        var wave = (AuthoredTsunamiWave)data.FindProperty("authoredWave").objectReferenceValue;
        Assert.That(wave, Is.Not.Null, "The artist's wave must be in the saved hierarchy before Play.");
        Assert.That(wave.IsVisible, Is.False);
        Assert.That(wave.Surface.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(waterSource.transform.Find("TSUNAMI").GetComponent<MeshFilter>().sharedMesh));
        var sourceMaterials = waterSource.transform.Find("TSUNAMI").GetComponent<MeshRenderer>().sharedMaterials;
        for (var i = 0; i < sourceMaterials.Length; i++)
        {
            var material = wave.Surface.sharedMaterials[i];
            var color = sourceMaterials[i].GetColor("_BaseColor"); color.a = 1f;
            Assert.That(Vector4.Distance(material.GetColor("_BaseColor"), color), Is.LessThan(.000001f),
                "Preserve the imported palette within float serialization precision.");
            Assert.That(material.GetColor("_BaseColor").a, Is.EqualTo(1f));
            Assert.That(material.shader, Is.SameAs(sourceMaterials[i].shader));
            Assert.That(material.GetFloat("_Surface"), Is.Zero);
            Assert.That(material.GetFloat("_Cull"), Is.Zero);
            Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1f));
            Assert.That(material.renderQueue, Is.EqualTo(2000));
        }
        Assert.That(wave.transform, Is.SameAs(scenery.Find("TSUNAMI")), "Animate the actual child inside the active main map.");
        Assert.That(wave.Surface.transform, Is.SameAs(wave.transform));
        Assert.That(Vector3.Distance(wave.StartPosition, new Vector3(-21.25f, -.8049f, 11.2f)), Is.LessThan(.00001f),
            "Use the adjusted offshore scene position instead of restoring the original FBX position on Play.");
        Assert.That(Vector3.Distance(wave.EndPosition, new Vector3(-21.3f, wave.StartPosition.y, -26.53f)), Is.LessThan(.00001f));
        Assert.That(wave.StartDistance, Is.LessThan(tsunami.CityBounds.min.z));
        Assert.That(wave.EndDistance, Is.GreaterThan(tsunami.CityBounds.max.z));
        Assert.That(new SerializedObject(wave).FindProperty("simulationFrame").objectReferenceValue, Is.SameAs(tsunami.transform));
        Assert.That(wave.transform.localPosition, Is.EqualTo(wave.StartPosition));
        Assert.That(wave.Surface.bounds.min.x, Is.LessThanOrEqualTo(tsunami.GroundSurface.GetComponent<Renderer>().bounds.min.x));
        Assert.That(wave.Surface.bounds.max.x, Is.GreaterThanOrEqualTo(tsunami.GroundSurface.GetComponent<Renderer>().bounds.max.x),
            "The current map's wave must cover all four city columns.");
        Assert.That(wave.transform.localScale, Is.EqualTo(new Vector3(.21736877f, .08990781f, .21736877f)),
            "Retain the user's smaller scene scale; do not restore the FBX scale or fit it to the camera.");
        var bounds = wave.Surface.localBounds;
        var physicalFront = Enumerable.Range(0, 8).Max(corner => tsunami.transform.InverseTransformPoint(
            wave.Surface.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f)))).z);
        Assert.That(data.FindProperty("startDistance").floatValue, Is.EqualTo(physicalFront).Within(.00001f),
            "Arrival timing must use the adjusted mesh's physical front.");
        Assert.That(new SerializedObject(wave).FindProperty("movementSpeed"), Is.Not.Null);
        Assert.That(tsunami.WaveSpeed, Is.EqualTo(wave.MovementSpeed));
        Assert.That(((Material)data.FindProperty("waterMaterial").objectReferenceValue).shader.name, Is.EqualTo("CivilGameJam/TsunamiWater"));
        var monster = (SeaMonsterEmergence)data.FindProperty("seaMonster").objectReferenceValue;
        var monsterData = new SerializedObject(monster);
        var distance = data.FindProperty("shorelineDistance").floatValue - data.FindProperty("startDistance").floatValue;
        Assert.That(monsterData.FindProperty("waveApproachOffset").vector3Value.magnitude, Is.EqualTo(distance).Within(.0001f));
        Assert.That(monsterData.FindProperty("waveApproachDuration").floatValue,
            Is.GreaterThan(0f), "Keep the saved approach for the route without a tsunami independent of the live wave speed.");
        Assert.That(monster.WaveReleaseDelay + monsterData.FindProperty("waveApproachDuration").floatValue, Is.LessThanOrEqualTo(monster.RiseDuration));
    }
}
