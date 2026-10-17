using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CivilGameJam;
using CivilGameJam.Disasters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class TsunamiFloodHeightPlayModeTests
{
    [UnityTest]
    public IEnumerator FloodSurfaceNeverExceedsHalfTheShortestIndividualPrefabBuildingInBothScenes()
    {
        var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/SmallestBuildingFloodWork"));
        Directory.CreateDirectory(directory);
        var report = Path.Combine(directory, "measured-heights.txt");
        File.WriteAllText(report, "");
        var advance = typeof(TsunamiTestController).GetMethod("AdvanceTsunami", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var sceneName in new[] { "2DCardScene", "BuildingShowcase" })
        {
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return null;
            var tsunami = Object.FindFirstObjectByType<TsunamiTestController>();
            var root = (Transform)typeof(TsunamiTestController).GetField("buildingsRoot", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tsunami);
            var models = root.GetComponentsInChildren<BuildingModelGeometry>();
            Assert.That(models.Any(model => model.IsCollection), Is.True, "The fixture must include a prefab containing buildings of different heights.");
            var shortest = models.Where(model => !model.IsCollection).OrderBy(model => model.WorldBounds.size.y).First();
            var shortestBounds = shortest.WorldBounds;
            float expected = tsunami.transform.InverseTransformPoint(shortestBounds.center).y;
            Assert.That(tsunami.MaximumFloodWaterLevel, Is.EqualTo(expected).Within(0.0001f),
                "The maximum must be the midpoint of the shortest individual building, rather than a fraction of the entire group.");

            tsunami.StartTsunami();
            Assert.That(tsunami.IsRunning, Is.True);
            var water = tsunami.transform.Find("Tsunami_Water/City_Flood_Flow").GetComponent<MeshFilter>();
            var wave = tsunami.transform.Find("Tsunami_Water").GetComponent<MeshFilter>();
            float peak = float.NegativeInfinity;
            bool flooded = false;
            var city = tsunami.CityBounds;
            for (var frame = 0; frame < Mathf.CeilToInt(tsunami.TestDuration * 60f) + 2 && tsunami.IsRunning; frame++)
            {
                advance.Invoke(tsunami, new object[] { 1f / 60f });
                if (water.sharedMesh.vertexCount > 0)
                {
                    float height = water.sharedMesh.bounds.max.y;
                    peak = Mathf.Max(peak, height);
                    flooded |= tsunami.FloodCoverage > 0f;
                    Assert.That(height, Is.LessThanOrEqualTo(expected + 0.0001f),
                        sceneName + ": rendered flood facets and ripples exceeded the building midpoint at frame " + frame);
                }
                if (frame % 6 != 0) continue;
                float inlandPeak = float.NegativeInfinity;
                foreach (var point in wave.sharedMesh.vertices)
                    if (point.x >= city.min.x && point.x <= city.max.x && point.z >= city.min.z && point.z <= city.max.z)
                        inlandPeak = Mathf.Max(inlandPeak, point.y);
                Assert.That(inlandPeak, Is.LessThanOrEqualTo(expected + 0.0001f), "The incoming crest must also collapse below the limit inside the city.");
            }
            Assert.That(flooded, Is.True, "Lowering the maximum must still let the water flow into the city.");
            Assert.That(peak - tsunami.GroundLevel, Is.GreaterThan((expected - tsunami.GroundLevel) * 0.9f),
                "The flood should approach the requested midpoint during the peak, rather than only satisfy the ceiling by staying dry.");
            File.AppendAllText(report, sceneName + ": shortest=" + shortest.name +
                " worldHeight=" + shortestBounds.size.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                " midpointLocal=" + expected.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                " observedPeakLocal=" + peak.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\n");
            Assert.That(tsunami.IsRunning, Is.False);
        }
    }
}
