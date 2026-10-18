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
            var apartment = building.GetComponent<CivilGameJam.Upgrades.ApartmentUpgradeModel>();
            var grounds = building.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.name.Contains("Ground") &&
                (apartment == null || apartment.MotionRoot == null || !filter.transform.IsChildOf(apartment.MotionRoot))).ToArray();
            Assert.That(grounds, Is.Not.Empty, building.name + " must retain its travelling ground.");
            foreach (var filter in grounds)
            {
                var original = PrefabUtility.GetCorrespondingObjectFromSource(filter).sharedMesh;
                Assert.That(filter.sharedMesh, Is.SameAs(original), "Each building retains the complete original ground and foliage mesh.");
                Assert.That(filter.transform.IsChildOf(building), Is.True, "The ground must travel with its own building.");
                var renderer = filter.GetComponent<MeshRenderer>();
                Assert.That(renderer.enabled && filter.gameObject.activeInHierarchy, Is.True);
                Assert.That(renderer.sharedMaterials.Length, Is.GreaterThanOrEqualTo(filter.sharedMesh.subMeshCount));
                var hasVisibleGroundPad = false;
                var vertices = filter.sharedMesh.vertices; var triangles = filter.sharedMesh.triangles;
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = filter.transform.TransformPoint(vertices[triangles[i]]);
                    var b = filter.transform.TransformPoint(vertices[triangles[i + 1]]);
                    var c = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                    var heightAboveMap = (a.y + b.y + c.y) / 3f - ground.GetComponent<MeshRenderer>().bounds.max.y;
                    if (Mathf.Max(a.y, b.y, c.y) - Mathf.Min(a.y, b.y, c.y) < .0002f &&
                        heightAboveMap > .001f && heightAboveMap < .03f && Vector3.Cross(b - a, c - a).magnitude * .5f > .1f)
                        hasVisibleGroundPad = true;
                }
                Assert.That(hasVisibleGroundPad, Is.True, "The full ground surface must remain visible above the map tile.");
            }
        }
        Assert.That(Object.FindObjectsByType<HouseCityUpgradeLink>(FindObjectsSortMode.None).Length, Is.EqualTo(4));
        Assert.That(tsunami.CityBounds.min.z, Is.LessThan(-3f));
    }
}
