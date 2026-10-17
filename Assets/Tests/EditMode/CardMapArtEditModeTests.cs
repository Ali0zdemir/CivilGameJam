using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardMapArtEditModeTests
{
    [Test]
    public void ArtistMapTilesAndTsunamiMeshesAreSavedWithTheirOriginalMaterialsAndGameplayReferences()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        const string sourcePath = "Assets/emreAnimation/Map+Tsunami+Tiles.fbx";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        var waterSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/emreAnimation/Deniz+Tsunami(updated).fbx");
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var data = new SerializedObject(tsunami);
        var ground = tsunami.GroundSurface;
        Assert.That(ground.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(source.transform.Find("Tiles").GetComponent<MeshFilter>().sharedMesh));
        Assert.That(ground.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(source.transform.Find("Tiles").GetComponent<MeshRenderer>().sharedMaterials));
        var scenery = tsunami.transform.Find("Map_Scenery");
        Assert.That(scenery.childCount, Is.EqualTo(4));
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
        Assert.That(wave.StartPosition, Is.EqualTo(new Vector3(-9.536f, -.32f, -5.52f)));
        Assert.That(wave.EndPosition, Is.EqualTo(new Vector3(-9.536f, -.32f, 48.5f)));
        Assert.That(wave.transform.localPosition, Is.EqualTo(wave.StartPosition));
        Assert.That(wave.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(wave.Surface.transform.localScale, Is.EqualTo(waterSource.transform.Find("TSUNAMI").localScale));
        Assert.That(Vector3.Distance(wave.Surface.bounds.size,
            waterSource.transform.Find("TSUNAMI").GetComponent<MeshRenderer>().bounds.size), Is.LessThan(.0001f),
            "Keep the artist's original wave dimensions rather than fitting the wave to the city width.");
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
