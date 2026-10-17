using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CivilGameJam.Disasters
{
    /// <summary>A shallow water grid: pressure moves water between cells, solid footprints block flow.</summary>
    internal sealed class CityFloodSurface : IDisposable
    {
        private readonly int columns;
        private readonly int rows;
        private readonly float dx;
        private readonly float dz;
        private readonly Bounds area;
        private readonly float floor;
        private readonly float maximumSurgeDepth;
        private readonly float flowSpeed;
        private readonly float runoffDistance;
        private readonly float runoffFloor;
        private readonly bool[] solid;
        private readonly float[] depth;
        private readonly float[] fluxX;
        private readonly float[] fluxZ;
        private readonly float[] available;
        private readonly float[] foam;
        private readonly float[] wetSince;
        private readonly WaterVertex[] corners;
        private const int HistorySamples = 256;
        private const float HistoryStep = 1f / 60f;
        private const float HydraulicDiffusivity = 0.55f;
        private const int RunoffSegments = 24;
        private readonly WaterVertex[] runoffA = new WaterVertex[RunoffSegments + 1];
        private readonly WaterVertex[] runoffB = new WaterVertex[RunoffSegments + 1];
        private readonly float[] runoffHistory;
        private readonly int outletCount;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private float clock;
        private float nextHistoryTime;
        private int historyCursor;

        private struct WaterVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Color Color;

            public WaterVertex(Vector3 position, Vector3 normal, Color color)
            { Position = position; Normal = normal; Color = color; }
        }

        public Mesh Mesh { get; }
        public float GroundLevel => floor;
        public float WetCoverage { get; private set; }
        public float MeanDepth { get; private set; }
        public float MaximumDepth { get; private set; }
        public float DischargedVolume { get; private set; }
        public float RunoffAmount { get; private set; }

        public CityFloodSurface(Transform parent, Material material, Bounds bounds, float ground,
            float cellSize, float settledDepth, float surgeDepth, float currentSpeed, float outflowLength,
            float outsideLevel, IReadOnlyList<Bounds> obstacles)
        {
            area = bounds;
            floor = ground;
            maximumSurgeDepth = Mathf.Max(settledDepth, surgeDepth);
            flowSpeed = Mathf.Max(0.5f, currentSpeed);
            runoffDistance = Mathf.Max(0.2f, outflowLength);
            runoffFloor = Mathf.Min(ground - 0.1f, outsideLevel);
            columns = Mathf.Clamp(Mathf.CeilToInt(area.size.x / cellSize), 16, 96);
            rows = Mathf.Clamp(Mathf.CeilToInt(area.size.z / cellSize), 16, 96);
            dx = area.size.x / columns;
            dz = area.size.z / rows;
            depth = new float[columns * rows];
            solid = new bool[depth.Length];
            available = new float[depth.Length];
            foam = new float[depth.Length];
            wetSince = new float[depth.Length];
            fluxX = new float[(columns + 1) * rows];
            fluxZ = new float[columns * (rows + 1)];
            corners = new WaterVertex[(columns + 1) * (rows + 1)];
            outletCount = rows * 2 + columns;
            runoffHistory = new float[outletCount * HistorySamples];
            SetObstacles(obstacles);

            var water = new GameObject("City_Flood_Flow");
            water.transform.SetParent(parent, false);
            renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Mesh = new Mesh { name = "Low poly obstacle-aware flood", indexFormat = IndexFormat.UInt32 };
            Mesh.MarkDynamic();
            water.AddComponent<MeshFilter>().sharedMesh = Mesh;
            Clear();
        }

        public void SetObstacles(IReadOnlyList<Bounds> obstacles)
        {
            for (var z = 0; z < rows; z++)
            for (var x = 0; x < columns; x++)
            {
                var i = z * columns + x;
                var point = CellPosition(x, z);
                solid[i] = false;
                foreach (var obstacle in obstacles)
                    if (point.x > obstacle.min.x && point.x < obstacle.max.x &&
                        point.z > obstacle.min.z && point.z < obstacle.max.z)
                    { solid[i] = true; break; }
                if (!solid[i]) continue;
                depth[i] = foam[i] = 0f;
                wetSince[i] = -1f;
            }
        }

        public void Clear()
        {
            Array.Clear(depth, 0, depth.Length);
            Array.Clear(fluxX, 0, fluxX.Length);
            Array.Clear(fluxZ, 0, fluxZ.Length);
            Array.Clear(foam, 0, foam.Length);
            Array.Clear(runoffHistory, 0, runoffHistory.Length);
            for (var i = 0; i < wetSince.Length; i++) wetSince[i] = -1f;
            clock = 0f;
            nextHistoryTime = HistoryStep;
            historyCursor = 0;
            DischargedVolume = RunoffAmount = 0f;
            WetCoverage = MeanDepth = MaximumDepth = 0f;
            Mesh.Clear();
        }

        public void Advance(float deltaTime, float inletLevel)
        {
            if (deltaTime <= 0f) return;
            // Account for both transport and diffusion when cell size or flow speed is adjusted.
            var rate = flowSpeed / dz + 2f * HydraulicDiffusivity * (1f / (dx * dx) + 1f / (dz * dz)) +
                Mathf.Sqrt(24f * maximumSurgeDepth) * (1f / dx + 1f / dz);
            var stableStep = Mathf.Min(0.008f, 0.8f / rate);
            var steps = Mathf.Max(1, Mathf.CeilToInt(deltaTime / stableStep));
            var step = deltaTime / steps;
            for (var i = 0; i < steps; i++) Step(step, Mathf.Max(0f, inletLevel - floor));
        }

        private void Step(float dt, float inletDepth)
        {
            clock += dt;
            // One incoming pulse; all other boundaries discharge water out of the city.
            for (var z = 0; z < rows; z++)
            {
                var left = z * columns;
                var right = left + columns - 1;
                fluxX[z * (columns + 1)] = solid[left] ? 0f : -depth[left] * Mathf.Sqrt(24f * depth[left]);
                fluxX[z * (columns + 1) + columns] = solid[right] ? 0f : depth[right] * Mathf.Sqrt(24f * depth[right]);
            }
            for (var x = 0; x < columns; x++)
            {
                var last = (rows - 1) * columns + x;
                fluxZ[x] = solid[x] ? 0f : inletDepth * flowSpeed;
                fluxZ[rows * columns + x] = solid[last] ? 0f : depth[last] * flowSpeed;
            }
            // A diffusive pressure flux avoids alternating wet/dry spikes in shallow cells.
            // Combined with upwind transport it remains stable as the trailing edge dries.
            for (var z = 0; z < rows; z++)
            for (var x = 1; x < columns; x++)
            {
                var left = z * columns + x - 1;
                var right = left + 1;
                var edge = z * (columns + 1) + x;
                fluxX[edge] = solid[left] || solid[right] ? 0f :
                    HydraulicDiffusivity * (depth[left] - depth[right]) / dx;
            }
            for (var z = 1; z < rows; z++)
            for (var x = 0; x < columns; x++)
            {
                var previous = (z - 1) * columns + x;
                var next = previous + columns;
                var edge = z * columns + x;
                // Carry the bore's forward current through the streets; pressure still diverts
                // it sideways at solid footprints. The upwind term also transports the dry tail.
                var transported = flowSpeed * depth[previous];
                fluxZ[edge] = solid[previous] || solid[next] ? 0f :
                    transported + HydraulicDiffusivity * (depth[previous] - depth[next]) / dz;
            }

            // Limit outgoing flux by available volume. No cell can create negative water.
            for (var z = 0; z < rows; z++)
            for (var x = 0; x < columns; x++)
            {
                var i = z * columns + x;
                var outgoing = (Mathf.Max(0f, -fluxX[z * (columns + 1) + x]) +
                    Mathf.Max(0f, fluxX[z * (columns + 1) + x + 1])) / dx +
                    (Mathf.Max(0f, -fluxZ[z * columns + x]) +
                    Mathf.Max(0f, fluxZ[(z + 1) * columns + x])) / dz;
                available[i] = outgoing > 0f ? Mathf.Min(1f, depth[i] / (dt * outgoing)) : 1f;
            }
            for (var z = 0; z < rows; z++)
            for (var x = 1; x < columns; x++)
            {
                var edge = z * (columns + 1) + x;
                var source = z * columns + (fluxX[edge] >= 0f ? x - 1 : x);
                fluxX[edge] *= available[source];
            }
            for (var z = 1; z < rows; z++)
            for (var x = 0; x < columns; x++)
            {
                var edge = z * columns + x;
                var source = (fluxZ[edge] >= 0f ? z - 1 : z) * columns + x;
                fluxZ[edge] *= available[source];
            }
            for (var z = 0; z < rows; z++)
            {
                var leftEdge = z * (columns + 1);
                var rightEdge = leftEdge + columns;
                fluxX[leftEdge] *= available[z * columns];
                fluxX[rightEdge] *= available[z * columns + columns - 1];
                DischargedVolume += dt * dz * (fluxX[rightEdge] - fluxX[leftEdge]);
            }
            for (var x = 0; x < columns; x++)
            {
                var edge = rows * columns + x;
                fluxZ[edge] *= available[(rows - 1) * columns + x];
                DischargedVolume += dt * dx * fluxZ[edge];
            }
            for (var z = 0; z < rows; z++)
            for (var x = 0; x < columns; x++)
            {
                var i = z * columns + x;
                if (solid[i]) continue;
                var delta = (fluxX[z * (columns + 1) + x] - fluxX[z * (columns + 1) + x + 1]) / dx +
                    (fluxZ[z * columns + x] - fluxZ[(z + 1) * columns + x]) / dz;
                var nextDepth = Mathf.Max(0f, depth[i] + dt * delta);
                // Subcell breaking crests become foam instead of narrow, tall numerical spikes.
                if (nextDepth > maximumSurgeDepth) foam[i] = 1f;
                depth[i] = Mathf.Min(maximumSurgeDepth, nextDepth);
                if (depth[i] > 0.008f && wetSince[i] < 0f) wetSince[i] = clock;
                var speed = (Mathf.Abs(fluxX[z * (columns + 1) + x]) + Mathf.Abs(fluxZ[z * columns + x])) /
                    Mathf.Max(0.04f, depth[i]);
                var atWall = (x > 0 && solid[i - 1]) || (x < columns - 1 && solid[i + 1]) ||
                    (z > 0 && solid[i - columns]) || (z < rows - 1 && solid[i + columns]);
                var sourceFoam = atWall ? Mathf.Clamp01(speed * 0.45f) : Mathf.Clamp01((speed - 1.2f) * 0.22f);
                foam[i] = Mathf.Max(foam[i] * Mathf.Exp(-dt * 1.3f), sourceFoam);
            }
            if (clock >= nextHistoryTime)
            {
                historyCursor = (historyCursor + 1) % HistorySamples;
                var offset = historyCursor * outletCount;
                for (var z = 0; z < rows; z++)
                {
                    runoffHistory[offset + z] = -fluxX[z * (columns + 1)] / flowSpeed;
                    runoffHistory[offset + rows + z] = fluxX[z * (columns + 1) + columns] / flowSpeed;
                }
                for (var x = 0; x < columns; x++) runoffHistory[offset + rows * 2 + x] = fluxZ[rows * columns + x] / flowSpeed;
                nextHistoryTime += HistoryStep;
            }
        }

        public float SampleDepth(float x, float z)
        {
            var column = Mathf.FloorToInt((x - area.min.x) / dx);
            var row = Mathf.FloorToInt((z - area.min.z) / dz);
            if (column < 0 || row < 0 || column >= columns || row >= rows) return 0f;
            return depth[row * columns + column];
        }

        public void SampleInlet(float x, out float level, out Color color)
        {
            var position = Mathf.Clamp((x - area.min.x) / dx, 0f, columns);
            var left = Mathf.Min(columns - 1, Mathf.FloorToInt(position));
            var a = corners[left]; var b = corners[left + 1];
            level = Mathf.Lerp(a.Position.y, b.Position.y, position - left);
            color = Color.Lerp(a.Color, b.Color, position - left);
        }

        public void UpdateMesh(float targetDepth, float fade)
        {
            vertices.Clear(); normals.Clear(); colors.Clear(); triangles.Clear();
            var wet = 0; var open = 0; var totalDepth = 0f; var maxDepth = 0f;
            for (var z = 0; z <= rows; z++)
            for (var x = 0; x <= columns; x++)
            {
                var height = 0f; var white = 0f; var count = 0;
                for (var oz = -1; oz <= 0; oz++)
                for (var ox = -1; ox <= 0; ox++)
                {
                    var sx = x + ox; var sz = z + oz;
                    if (sx < 0 || sz < 0 || sx >= columns || sz >= rows) continue;
                    var i = sz * columns + sx;
                    if (solid[i]) continue;
                    height += depth[i];
                    var newlyWet = wetSince[i] < 0f ? 0f : Mathf.Exp(-(clock - wetSince[i]) * 2.4f);
                    white += foam[i] * 0.45f + newlyWet * 0.2f;
                    count++;
                }
                height = count > 0 ? height / count : 0f;
                white = count > 0 ? white / count : 0f;
                // Travelling facets make the current visible even on a broad, shallow section.
                var ripple = (Mathf.Sin(x * 0.65f + z * 0.48f - clock * 3.4f) +
                    Mathf.Sin(x * 0.32f - z * 0.91f + clock * 2.2f)) * 0.004f * Mathf.Clamp01(height / 0.08f);
                var streak = Mathf.Sin(z * 0.6f - clock * flowSpeed * 3f + Mathf.Sin(x * 1.3f)) * 0.055f;
                var color = new Color(white, 0.3f + streak + 0.2f * height / Mathf.Max(0.01f, targetDepth),
                    1f, Mathf.SmoothStep(0f, 1f, height / 0.025f));
                corners[z * (columns + 1) + x] = new WaterVertex(
                    new Vector3(area.min.x + x * dx, floor + Mathf.Clamp(height + ripple, 0f, maximumSurgeDepth), area.min.z + z * dz), Vector3.up, color);
            }
            // Shared corner shading lets the surface and its fading edge cross cell boundaries smoothly.
            for (var z = 0; z <= rows; z++)
            for (var x = 0; x <= columns; x++)
            {
                var left = Mathf.Max(0, x - 1); var right = Mathf.Min(columns, x + 1);
                var back = Mathf.Max(0, z - 1); var front = Mathf.Min(rows, z + 1);
                corners[z * (columns + 1) + x].Normal = new Vector3(
                    (Corner(left, z).Position.y - Corner(right, z).Position.y) / ((right - left) * dx), 1f,
                    (Corner(x, back).Position.y - Corner(x, front).Position.y) / ((front - back) * dz)).normalized;
            }
            for (var z = 0; z < rows; z++)
            for (var x = 0; x < columns; x++)
            {
                var i = z * columns + x;
                if (solid[i]) continue;
                open++; totalDepth += depth[i]; maxDepth = Mathf.Max(maxDepth, depth[i]);
                if (depth[i] > 0.004f) wet++;
                var a = Corner(x, z); var b = Corner(x + 1, z);
                var c = Corner(x, z + 1); var d = Corner(x + 1, z + 1);
                if (Mathf.Max(Mathf.Max(a.Color.a, b.Color.a), Mathf.Max(c.Color.a, d.Color.a)) < 0.003f) continue;
                if (((x + z) & 1) == 0) { Face(a, c, b); Face(b, c, d); }
                else { Face(a, c, d); Face(a, d, b); }
            }
            WetCoverage = open > 0 ? (float)wet / open : 0f;
            MeanDepth = open > 0 ? totalDepth / open : 0f;
            MaximumDepth = maxDepth;
            AppendRunoff();
            Mesh.Clear();
            Mesh.SetVertices(vertices); Mesh.SetNormals(normals); Mesh.SetColors(colors);
            Mesh.SetTriangles(triangles, 0); Mesh.RecalculateBounds();
            properties.SetFloat("_Fade", fade);
            properties.SetFloat("_Opacity", 0.92f);
            properties.SetFloat("_FacetedLighting", 0.15f);
            renderer.SetPropertyBlock(properties);
        }

        private Vector3 CellPosition(int x, int z) => new Vector3(area.min.x + (x + 0.5f) * dx, floor, area.min.z + (z + 0.5f) * dz);
        private WaterVertex Corner(int x, int z) => corners[z * (columns + 1) + x];

        private float RunoffDepth(int outlet, float distance)
        {
            var age = distance / flowSpeed / HistoryStep;
            var newest = Mathf.FloorToInt(age);
            if (newest >= HistorySamples - 1) return 0f;
            var a = (historyCursor - newest + HistorySamples) % HistorySamples;
            var b = (a - 1 + HistorySamples) % HistorySamples;
            return Mathf.Lerp(runoffHistory[a * outletCount + outlet], runoffHistory[b * outletCount + outlet], age - newest);
        }

        private void AppendRunoff()
        {
            RunoffAmount = 0f;
            for (var z = 0; z < rows; z++)
            {
                RunoffStrip(z, Corner(0, z + 1), Corner(0, z), new Vector3(-1f, 0f, 0.3f).normalized,
                    z < rows - 1 ? z + 1 : -1, z > 0 ? z - 1 : -1);
                RunoffStrip(rows + z, Corner(columns, z), Corner(columns, z + 1), new Vector3(1f, 0f, 0.3f).normalized,
                    z > 0 ? rows + z - 1 : -1, z < rows - 1 ? rows + z + 1 : -1);
            }
            for (var x = 0; x < columns; x++)
                RunoffStrip(rows * 2 + x, Corner(x + 1, rows), Corner(x, rows), Vector3.forward,
                    x < columns - 1 ? rows * 2 + x + 1 : -1, x > 0 ? rows * 2 + x - 1 : -1);
        }

        private float RunoffEdgeDepth(int outlet, int neighbour, float distance)
        {
            var value = RunoffDepth(outlet, distance);
            return neighbour < 0 ? value : (value + RunoffDepth(neighbour, distance)) * 0.5f;
        }

        private void RunoffStrip(int outlet, WaterVertex a, WaterVertex b, Vector3 direction, int neighbourA, int neighbourB)
        {
            var across = b.Position - a.Position;
            for (var segment = 0; segment <= RunoffSegments; segment++)
            {
                var distance = runoffDistance * segment / RunoffSegments;
                runoffA[segment] = RunoffVertex(outlet, neighbourA, a, direction, distance);
                runoffB[segment] = RunoffVertex(outlet, neighbourB, b, direction, distance);
            }
            // Reuse sampled positions for the slopes instead of resampling the flow history.
            for (var segment = 0; segment <= RunoffSegments; segment++)
            {
                SmoothRunoffNormal(runoffA, segment, across);
                SmoothRunoffNormal(runoffB, segment, across);
            }
            for (var segment = 0; segment < RunoffSegments; segment++)
            {
                var p0 = runoffA[segment]; var p1 = runoffB[segment];
                var p2 = runoffA[segment + 1]; var p3 = runoffB[segment + 1];
                if (Mathf.Max(Mathf.Max(p0.Color.a, p1.Color.a), Mathf.Max(p2.Color.a, p3.Color.a)) >= 0.003f)
                {
                    Face(p0, p1, p2);
                    Face(p1, p3, p2);
                }
            }
        }

        private void SmoothRunoffNormal(WaterVertex[] strip, int segment, Vector3 across)
        {
            var tangent = strip[Mathf.Min(RunoffSegments, segment + 1)].Position - strip[Mathf.Max(0, segment - 1)].Position;
            var normal = Vector3.Cross(across, tangent).normalized;
            if (normal.y < 0f) normal = -normal;
            var join = Mathf.SmoothStep(0f, 1f, runoffDistance * segment / RunoffSegments / 0.25f);
            strip[segment].Normal = Vector3.Lerp(strip[segment].Normal, normal, join).normalized;
        }

        private WaterVertex RunoffVertex(int outlet, int neighbour, WaterVertex edge, Vector3 direction, float distance)
        {
            var waterDepth = RunoffEdgeDepth(outlet, neighbour, distance);
            RunoffAmount += waterDepth;
            var position = RunoffPoint(edge.Position, direction, distance, waterDepth);
            var join = Mathf.SmoothStep(0f, 1f, distance / 0.25f);
            var endFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(runoffDistance * 0.6f, runoffDistance, distance));
            var alpha = Mathf.Lerp(edge.Color.a, Mathf.SmoothStep(0f, 1f, waterDepth / 0.035f), join) * endFade;
            var color = new Color(0f, Mathf.Lerp(edge.Color.g, 0.45f, join), 1f, alpha);
            return new WaterVertex(position, edge.Normal, color);
        }

        private Vector3 RunoffPoint(Vector3 edge, Vector3 direction, float distance, float waterDepth)
        {
            if (distance <= 0f) return edge;
            var point = edge + direction * distance;
            var fall = Mathf.SmoothStep(0f, 1f, distance / 0.75f);
            var thickness = waterDepth * (1f - 0.7f * fall);
            point.y = Mathf.Lerp(floor, runoffFloor, fall) + thickness;
            point.y += Mathf.Sin(distance * 7f - clock * flowSpeed * 5f + edge.x * 3f) * 0.012f * fall;
            return point;
        }

        private void Face(WaterVertex a, WaterVertex b, WaterVertex c)
        {
            var index = vertices.Count;
            vertices.Add(a.Position); vertices.Add(b.Position); vertices.Add(c.Position);
            // Keep a faint polygonal character without exposing the square simulation grid.
            var normal = Vector3.Cross(b.Position - a.Position, c.Position - a.Position).normalized;
            normals.Add(Vector3.Lerp(a.Normal, normal, 0.12f).normalized);
            normals.Add(Vector3.Lerp(b.Normal, normal, 0.12f).normalized);
            normals.Add(Vector3.Lerp(c.Normal, normal, 0.12f).normalized);
            colors.Add(a.Color); colors.Add(b.Color); colors.Add(c.Color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }

        public void Dispose() { if (Mesh != null) UnityEngine.Object.Destroy(Mesh); }
    }
}
