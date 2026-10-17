using System.Linq;
using CivilGameJam;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CardTileMapEditModeTests
{
    [Test]
    public void SavedTileGroundHasFourColumnsTwoRowsAndAllExistingBuildingsSeatedOnCells()
    {
        EditorSceneManager.OpenScene("Assets/2DCardScene.unity");
        var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
        var ground = tsunami.GroundSurface;
        Assert.That(ground.name, Is.EqualTo("Continuous_Ground"), "Keep the sprinkler's saved ground lookup working.");
        Assert.That(AssetDatabase.GetAssetPath(ground.GetComponent<MeshFilter>().sharedMesh), Is.EqualTo("Assets/EfeArts/Map(Guncel1).fbx"));
        var cells = ground.Find("Tile_Grid_4x2").Cast<Transform>().ToArray();
        Assert.That(cells.Length, Is.EqualTo(8));
        Assert.That(cells.Select(cell => Mathf.RoundToInt(cell.localPosition.x * 1000f)).Distinct().Count(), Is.EqualTo(4));
        Assert.That(cells.Select(cell => Mathf.RoundToInt(cell.localPosition.z * 1000f)).Distinct().Count(), Is.EqualTo(2));
        var spacing = Mathf.Abs(cells[1].localPosition.x - cells[0].localPosition.x);
        var tileWidth = ground.GetComponent<MeshFilter>().sharedMesh.bounds.size.x - spacing * 3f;
        var buildings = tsunami.BuildingsRoot.Cast<Transform>().Where(building => building.gameObject.activeSelf).ToArray();
        Assert.That(buildings.Length, Is.EqualTo(7), "Retain the current scene's three workplaces and four playable houses.");
        foreach (var building in buildings)
        {
            Assert.That(cells.Any(cell => Vector2.Distance(new Vector2(cell.position.x, cell.position.z),
                new Vector2(building.position.x, building.position.z)) < .0001f), Is.True, building.name);
            Assert.That(building.localScale, Is.EqualTo(Vector3.one), "Changing the ground must not rescale the buildings.");
            var model = building.GetComponent<BuildingModelGeometry>();
            var foundation = model.FoundationBounds;
            var cell = cells.OrderBy(candidate => (candidate.position - building.position).sqrMagnitude).First();
            for (var corner = 0; corner < 8; corner++)
            {
                var local = foundation.center + Vector3.Scale(foundation.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                var seated = ground.InverseTransformPoint(building.TransformPoint(local)) - cell.localPosition;
                Assert.That(Mathf.Abs(seated.x), Is.LessThanOrEqualTo(tileWidth * .5f + .02f), building.name + " foundation X");
                Assert.That(Mathf.Abs(seated.z), Is.LessThanOrEqualTo(tileWidth * .5f + .02f), building.name + " foundation Z");
            }
            foreach (var filter in building.GetComponentsInChildren<MeshFilter>().Where(filter => filter.name.Contains("Ground")))
            {
                Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Does.StartWith("Assets/Art/Buildings/TileFoliage/"));
                var original = PrefabUtility.GetCorrespondingObjectFromSource(filter).sharedMesh;
                Assert.That(filter.sharedMesh.vertices, Is.EqualTo(original.vertices), "Retain authored foliage positions.");
                Assert.That(filter.sharedMesh.triangles.Length, Is.LessThan(original.triangles.Length));
                var vertices = filter.sharedMesh.vertices; var triangles = filter.sharedMesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = filter.transform.TransformPoint(vertices[triangles[i]]);
                    var b = filter.transform.TransformPoint(vertices[triangles[i + 1]]);
                    var c = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                    if (Mathf.Max(a.y, b.y, c.y) - Mathf.Min(a.y, b.y, c.y) < .0002f &&
                        Mathf.Abs((a.y + b.y + c.y) / 3f - ground.GetComponent<MeshRenderer>().bounds.max.y) < .002f)
                        Assert.That(Vector3.Cross(b - a, c - a).magnitude * .5f, Is.LessThan(.1f), "The old large flat ground pads must be gone.");
                }
            }
        }
        Assert.That(Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None).Length, Is.EqualTo(4));
        Assert.That(tsunami.CityBounds.min.z, Is.LessThan(-3f));
    }
}
