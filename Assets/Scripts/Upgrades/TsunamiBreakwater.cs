using System.Collections.Generic;
using CivilGameJam.Disasters;
using UnityEngine;

namespace CivilGameJam.Upgrades
{
    /// <summary>Builds an offshore defence when every linked house has its open ground floor.</summary>
    [DisallowMultipleComponent]
    public sealed class TsunamiBreakwater : MonoBehaviour
    {
        [SerializeField] private TsunamiTestController tsunami;
        [SerializeField] private HouseColumnUpgrade[] houses = new HouseColumnUpgrade[0];
        [SerializeField] private Material concreteMaterial;
        [SerializeField] private Material capMaterial;
        [SerializeField] private Material walkwayMaterial;
        [Header("Shape (tsunami local space)")]
        [SerializeField, Min(0.1f)] private float height = 0.28f;
        [SerializeField, Min(0.2f)] private float depth = 0.95f;
        [Tooltip("Kıyı ile dalgakıranın orta bölümü arasındaki mesafe.")]
        [SerializeField, Min(0f)] private float shorelineOffset = 3.5f;
        [Tooltip("Sağ uç kıyıyla birleşirken arada boşluk kalmaması için birleşme payı.")]
        [SerializeField, Range(0.02f, 0.15f)] private float shoreConnectionOverlap = 0.06f;
        [Tooltip("Sağdaki kolun şehir genişliğinin içinde kalması için içe alınma mesafesi.")]
        [SerializeField, Min(0f)] private float rightInset = 0.75f;
        [SerializeField, Range(0.2f, 1.5f)] private float cornerRadius = 0.9f;
        [SerializeField, Min(0f)] private float sideExtension = 0.35f;
        [Header("Shore ramp")]
        [Tooltip("Şehir zemininden yürüyüş yoluna yükselen rampanın uzunluğu.")]
        [SerializeField, Min(0.5f)] private float rampLength = 1.8f;
        [Header("Wave protection")]
        [Tooltip("Dalgakıran varken şehre geçen su oranı.")]
        [SerializeField, Range(0f, 1f)] private float waveTransmission = 0.35f;

        private GameObject visuals;
        private Mesh coreMesh;
        private Mesh rockMesh;
        private readonly List<Mesh> detailMeshes = new List<Mesh>();
        private Vector2[] waveFace;

        public bool IsActive { get; private set; }
        public float Transmission => IsActive ? Mathf.Clamp01(waveTransmission) : 1f;
        public float Width { get; private set; }
        public float FrontDistance { get; private set; }
        public float CenterX { get; private set; }
        public float TopLevel { get; private set; }

        // Sample the actual bent shoreline, so the crest and impact foam follow each arm.
        public bool TryGetWaveFace(float x, out float distance)
        {
            distance = FrontDistance;
            if (!IsActive || waveFace == null) return false;
            var found = false;
            var firstDistance = float.MaxValue;
            for (var i = 1; i < waveFace.Length; i++)
            {
                var a = waveFace[i - 1]; var b = waveFace[i];
                if (x < Mathf.Min(a.x, b.x) - 0.0001f || x > Mathf.Max(a.x, b.x) + 0.0001f) continue;
                // A side arm can be parallel to the wave. Use its first incoming intersection.
                var z = Mathf.Abs(b.x - a.x) < 0.0001f ? Mathf.Min(a.y, b.y) :
                    Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, x));
                firstDistance = Mathf.Min(firstDistance, z);
                found = true;
            }
            if (found) distance = firstDistance;
            return found;
        }

        private void OnEnable()
        {
            foreach (var house in houses) if (house != null) house.StateChanged += OnHouseChanged;
            RefreshProtection();
        }

        private void OnHouseChanged(HouseColumnUpgrade _) => RefreshProtection();

        private void RefreshProtection()
        {
            var ready = tsunami != null && concreteMaterial != null && capMaterial != null && houses.Length > 0;
            foreach (var house in houses)
                if (house == null || house.UsesElevatedApartment || !house.isActiveAndEnabled || !house.IsInstalled) { ready = false; break; }
            if (ready) EnsureVisuals();
            IsActive = ready && visuals != null;
            if (visuals != null) visuals.SetActive(IsActive);
        }

        private void EnsureVisuals()
        {
            if (visuals != null) return;
            var bounds = tsunami.CityBounds;
            var cityCenterX = bounds.center.x;
            var thickness = Mathf.Max(0.2f, depth);
            var centerZ = bounds.min.z - Mathf.Max(shorelineOffset, thickness * 0.5f + 0.25f);
            TopLevel = tsunami.GroundLevel + Mathf.Max(0.1f, height);

            var radius = Mathf.Clamp(cornerRadius, 0.2f, 1.5f);
            // Keep the right-hand start attached when the main arm moves farther offshore.
            var reach = Mathf.Max(bounds.min.z - centerZ + Mathf.Clamp(shoreConnectionOverlap, 0.02f, 0.15f), radius + 0.1f);
            // Local -X is the city's right side with the scene's incoming tsunami direction.
            // Include the stone slope's width when aligning the return with the city's right edge.
            var inset = Mathf.Max(rightInset, thickness * 0.5f + 0.25f);
            var sideX = -bounds.extents.x + inset;
            var path = new List<Vector3> { new Vector3(sideX, 0f, reach) };
            var rampRun = Mathf.Min(Mathf.Max(0.5f, rampLength), (reach - radius) * 0.9f);
            var rampDrop = Mathf.Max(0f, TopLevel - tsunami.GroundLevel - 0.002f);
            // Extra sections ease into and out of the slope without changing the coastal outline.
            const int rampSegments = 4;
            for (var i = 1; i <= rampSegments; i++)
                path.Add(new Vector3(sideX, 0f, reach - rampRun * i / rampSegments));
            const int cornerSegments = 4;
            for (var i = 0; i <= cornerSegments; i++)
            {
                var angle = Mathf.PI + i * (Mathf.PI * 0.5f / cornerSegments);
                path.Add(new Vector3(sideX + radius + Mathf.Cos(angle) * radius, 0f,
                    radius + Mathf.Sin(angle) * radius));
            }
            path.Add(new Vector3(bounds.extents.x + Mathf.Max(0f, sideExtension), 0f, 0f));
            var route = path.ToArray();
            var joins = new Vector3[route.Length];
            var distances = new float[route.Length];
            var heightOffsets = new float[route.Length];
            waveFace = new Vector2[route.Length];
            FrontDistance = float.MaxValue;
            var minX = float.MaxValue; var maxX = float.MinValue;
            for (var i = 0; i < route.Length; i++)
            {
                if (i > 0) distances[i] = distances[i - 1] + Vector3.Distance(route[i - 1], route[i]);
                heightOffsets[i] = RampOffset(distances[i], rampRun, rampDrop);
                var previous = SideNormal(route[Mathf.Max(1, i)] - route[Mathf.Max(0, i - 1)]);
                var next = SideNormal(route[Mathf.Min(i + 1, route.Length - 1)] - route[Mathf.Min(i, route.Length - 2)]);
                var joined = (previous + next).normalized;
                joins[i] = joined / Mathf.Max(0.5f, Vector3.Dot(joined, next));
                var foot = route[i] - joins[i] * (thickness * 0.5f);
                waveFace[i] = new Vector2(cityCenterX + foot.x, centerZ + foot.z);
                FrontDistance = Mathf.Min(FrontDistance, waveFace[i].y);
                minX = Mathf.Min(minX, waveFace[i].x); maxX = Mathf.Max(maxX, waveFace[i].x);
            }
            Width = maxX - minX;
            CenterX = (minX + maxX) * 0.5f;

            visuals = new GameObject("Tsunami_Breakwater_Visuals");
            visuals.SetActive(false);
            // Following the tsunami transform keeps the structure on the incoming side.
            visuals.transform.SetParent(tsunami.transform, false);
            visuals.transform.localPosition = new Vector3(cityCenterX, 0f, centerZ);
            var bottom = tsunami.SeaLevel - 0.08f;
            var crestWidth = Mathf.Min(0.32f, thickness * 0.38f);
            coreMesh = BuildMound(route, joins, thickness, crestWidth, bottom, TopLevel - 0.035f, heightOffsets, false);
            MeshPart("Sloped_Rubble_Core", coreMesh, concreteMaterial);
            var walkway = BuildMound(route, joins, crestWidth, crestWidth, TopLevel - 0.035f, TopLevel, heightOffsets);
            detailMeshes.Add(walkway);
            MeshPart("Crest_Walkway", walkway, walkwayMaterial != null ? walkwayMaterial : capMaterial);
            for (var side = -1; side <= 1; side += 2)
            {
                var curbRoute = new Vector3[route.Length];
                for (var i = 0; i < route.Length; i++) curbRoute[i] = route[i] + joins[i] * (side * (crestWidth * 0.5f + 0.018f));
                var curb = BuildMound(curbRoute, joins, 0.045f, 0.045f, TopLevel - 0.02f, TopLevel + 0.025f, heightOffsets);
                detailMeshes.Add(curb);
                MeshPart(side < 0 ? "Seaward_Coping" : "Cityside_Coping", curb, capMaterial);
            }

            rockMesh = BuildRock();
            for (var segment = 0; segment < route.Length - 1; segment++)
            {
                var direction = (route[segment + 1] - route[segment]).normalized;
                var normal = SideNormal(direction);
                var sections = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(route[segment], route[segment + 1]) / 0.27f));
                var step = Vector3.Distance(route[segment], route[segment + 1]) / sections;
                for (var side = -1; side <= 1; side += 2)
                for (var row = 0; row < 3; row++)
                for (var i = 0; i < sections; i++)
                {
                    var variation = Mathf.Sin(i * 7.1f + row * 3.7f + segment * 11.3f + side * 2.6f);
                    var t = (row + 0.5f) / 3f;
                    var distance = Mathf.Lerp(thickness * 0.49f, crestWidth * 0.55f, t);
                    var position = Vector3.Lerp(route[segment], route[segment + 1], (i + 0.5f) / sections) +
                        direction * (variation * 0.045f) + normal * (side * (distance + variation * 0.025f));
                    var slopeOffset = RampOffset(distances[segment] + step * (i + 0.5f), rampRun, rampDrop);
                    position.y = Mathf.Lerp(bottom + 0.07f, TopLevel + slopeOffset - 0.065f, t);
                    Rock("Armour_Rock_" + segment + "_" + side + "_" + row + "_" + i, position,
                        new Vector3(step * (1.28f + variation * 0.14f), 0.23f + variation * 0.035f, 0.3f + variation * 0.035f),
                        Quaternion.Euler(variation * 24f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + variation * 65f, variation * 31f),
                        0.84f + (variation + 1f) * 0.1f);
                }
            }
        }

        private static Vector3 SideNormal(Vector3 direction) => new Vector3(-direction.z, 0f, direction.x).normalized;

        private static float RampOffset(float distance, float length, float drop) =>
            -drop * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / length)));

        private GameObject MeshPart(string label, Mesh mesh, Material material)
        {
            var part = new GameObject(label);
            part.transform.SetParent(visuals.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private void Rock(string label, Vector3 position, Vector3 size, Quaternion rotation, float tint)
        {
            var rock = MeshPart(label, rockMesh, concreteMaterial);
            rock.transform.SetLocalPositionAndRotation(position, rotation);
            rock.transform.localScale = size;
            var renderer = rock.GetComponent<MeshRenderer>();
            var material = renderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
            {
                var color = material.GetColor("_BaseColor") * tint; color.a = 1f;
                var properties = new MaterialPropertyBlock(); properties.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(properties);
            }
        }

        private static Mesh BuildMound(Vector3[] route, Vector3[] joins, float bottomWidth, float topWidth, float bottom, float top,
            float[] heightOffsets, bool slopeBottom = true)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var rings = new Vector3[route.Length][];
            for (var i = 0; i < route.Length; i++)
            {
                var lower = bottom + (slopeBottom ? heightOffsets[i] : 0f);
                var upper = top + heightOffsets[i];
                rings[i] = new[]
                {
                    route[i] - joins[i] * (bottomWidth * 0.5f) + Vector3.up * lower,
                    route[i] + joins[i] * (bottomWidth * 0.5f) + Vector3.up * lower,
                    route[i] + joins[i] * (topWidth * 0.5f) + Vector3.up * upper,
                    route[i] - joins[i] * (topWidth * 0.5f) + Vector3.up * upper
                };
                if (i == 0) continue;
                var a = rings[i - 1]; var b = rings[i];
                Quad(vertices, indices, a[0], b[0], b[1], a[1]);
                Quad(vertices, indices, a[0], a[3], b[3], b[0]);
                Quad(vertices, indices, a[3], a[2], b[2], b[3]);
                Quad(vertices, indices, a[1], b[1], b[2], a[2]);
            }
            var first = rings[0]; var last = rings[rings.Length - 1];
            Quad(vertices, indices, first[0], first[1], first[2], first[3]);
            Quad(vertices, indices, last[0], last[3], last[2], last[1]);
            var mesh = new Mesh { name = "Low poly breakwater mound" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static void Quad(List<Vector3> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }

        private static Mesh BuildRock()
        {
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var points = new[]
            {
                new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),
                new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),
                new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)
            };
            var faces = new[] { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var vertices = new Vector3[faces.Length]; var indices = new int[faces.Length];
            for (var i = 0; i < faces.Length; i++)
            {
                var p = points[faces[i]].normalized * 0.5f;
                p.y *= 1f + Mathf.Sin(faces[i] * 4.7f) * 0.17f;
                vertices[i] = p; indices[i] = i;
            }
            var mesh = new Mesh { name = "Faceted armour stone", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDisable()
        {
            foreach (var house in houses) if (house != null) house.StateChanged -= OnHouseChanged;
            IsActive = false;
            if (visuals != null) visuals.SetActive(false);
        }

        private void OnDestroy()
        {
            if (visuals != null) Destroy(visuals);
            if (coreMesh != null) Destroy(coreMesh);
            if (rockMesh != null) Destroy(rockMesh);
            foreach (var mesh in detailMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
