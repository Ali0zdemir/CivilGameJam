using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CivilGameJam.Upgrades
{
    /// <summary>A thin water layer that spreads over the roof, spills over its edges and drains down the facade.</summary>
    internal sealed class HouseWallRunoff : IDisposable
    {
        private const int RoofColumns = 10;
        private const int RoofRows = 10;
        private const int WallColumns = 10;
        private const int WallRows = 12;
        private const int RoofCells = RoofColumns * RoofRows;
        private const int FaceCells = WallColumns * WallRows;
        // Keep the original surface detail independently of the simulation grid.
        private const int RenderRoofColumns = 20, RenderRoofRows = 20;
        private const int RenderWallColumns = 20, RenderWallRows = 24;
        private const int MaxSimulationSteps = 4;
        private const int RoofVertices = (RenderRoofColumns + 1) * (RenderRoofRows + 1);
        private const int FaceVertices = (RenderWallColumns + 1) * (RenderWallRows + 2);
        private const int VertexCount = RoofVertices + 4 * FaceVertices;
        private const float RoofDiffusion = .014f;
        private const float WallDiffusion = .009f;
        private readonly Transform building;
        private readonly BuildingModelGeometry modelGeometry;
        private readonly Vector3[] basePoints = new Vector3[VertexCount];
        private readonly Vector3[] baseNormals = new Vector3[VertexCount];
        private readonly Vector3[] vertices = new Vector3[VertexCount];
        private readonly Vector3[] normals = new Vector3[VertexCount];
        private readonly Color[] colors = new Color[VertexCount];
        private readonly Vector2[] uvs = new Vector2[VertexCount];
        private float[] roofWater = new float[RoofCells];
        private float[] roofNext = new float[RoofCells];
        private float[] wallWater = new float[4 * FaceCells];
        private float[] wallNext = new float[4 * FaceCells];
        private readonly float[] roofWet = new float[RoofCells];
        private readonly float[] wallWet = new float[4 * FaceCells];
        private readonly float[] bottomFlow = new float[4 * WallColumns];
        private readonly Vector2[] roofVelocity = new Vector2[RoofCells];
        private readonly bool[] roofValid = new bool[RoofCells];
        private readonly Vector3[] sourcePoints = new Vector3[4];
        private readonly float[] sourceFlight = new float[4];
        private readonly bool[] hasSource = new bool[4];
        private readonly List<RoofPanel> roofPanels = new List<RoofPanel>(3);
        private readonly float[] faceDx = new float[4];
        private readonly float[] faceArea = new float[4];
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Bounds body;
        private Bounds roof;
        private float roofDx, roofDz, roofArea, wallDy, age, stableStep;
        private bool geometryReady;
        private Matrix4x4 surfaceToMesh, surfaceToWorld;
        private readonly Vector3[] meshBasePoints = new Vector3[VertexCount];
        private readonly Vector3[] meshOffsetAxes = new Vector3[VertexCount];
        private readonly Vector4[] ripplePhases = new Vector4[VertexCount];
        private readonly WaterSample[] samples = new WaterSample[VertexCount];
        private readonly Vector2[] roofCellCenters = new Vector2[RoofCells];
        private readonly int[] sourceCells = new int[4 * RoofCells];
        private readonly float[] sourceWeights = new float[4 * RoofCells];
        private readonly int[] sourceCounts = new int[4];
        private readonly List<int> visibleTriangles = new List<int>(14400);
        private static readonly int[][] SurfaceTriangles = CreateSurfaceTriangles();
        private static readonly Plane[] ViewPlanes = new Plane[6];
        private static Camera frustumCamera;
        private static Matrix4x4 frustumMatrix;
        private Camera viewCamera;
        private Matrix4x4 lastViewMatrix, lastBuildingMatrix, projectionToLocal, projectionNormalToWorld;
        private int visibleFaces = -1;
        private bool visibilityReady;
        private Vector2 worldScale;
        private Vector4 rippleClock;

        private struct WaterSample
        {
            public int A, B, C, D;
            public float X, Y, InverseArea;
        }

        public bool IsVisible { get; private set; } = true;
        public int VisibleWallCount { get; private set; }
        public int LastSimulationSteps { get; private set; }

        private struct RoofPanel
        {
            public Matrix4x4 ToPanel;
            public Bounds Bounds;
            public Plane Top;
        }

        public GameObject Visuals { get; }
        public Mesh Mesh { get; }
        public float WallWetCoverage { get; private set; }
        public float RoofWetCoverage { get; private set; }
        public float InjectedVolume { get; private set; }
        public float DrainedVolume { get; private set; }

        public HouseWallRunoff(Transform house, Transform parent, Material material)
        {
            building = house;
            modelGeometry = house.GetComponent<BuildingModelGeometry>();
            viewCamera = Camera.main;
            Visuals = new GameObject("Wall_Runoff"); Visuals.transform.SetParent(parent, false); Visuals.SetActive(false);
            Mesh = new Mesh { name = "Spreading roof and facade water" }; Mesh.MarkDynamic();
            Mesh.vertices = vertices;
            foreach (var surface in SurfaceTriangles) visibleTriangles.AddRange(surface);
            Mesh.SetTriangles(visibleTriangles, 0);
            Visuals.AddComponent<MeshFilter>().sharedMesh = Mesh;
            renderer = Visuals.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            properties.SetFloat("_SurfaceMode", 1f); properties.SetFloat("_Opacity", .88f);
            properties.SetColor("_DeepColor", new Color(.1f, .44f, .61f));
            properties.SetColor("_ShallowColor", new Color(.58f, .9f, .97f));
            renderer.SetPropertyBlock(properties);
            Rebuild();
        }

        private static void AddGrid(List<int> indices, int start, int columns, int rows)
        {
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var vertex = start + row * (columns + 1) + column;
                indices.Add(vertex); indices.Add(vertex + columns + 1); indices.Add(vertex + 1);
                indices.Add(vertex + 1); indices.Add(vertex + columns + 1); indices.Add(vertex + columns + 2);
            }
        }

        private static int[][] CreateSurfaceTriangles()
        {
            var result = new int[5][];
            var indices = new List<int>();
            AddGrid(indices, 0, RenderRoofColumns, RenderRoofRows); result[0] = indices.ToArray();
            for (var face = 0; face < 4; face++)
            {
                indices.Clear();
                AddGrid(indices, RoofVertices + face * FaceVertices, RenderWallColumns, RenderWallRows + 1);
                result[face + 1] = indices.ToArray();
            }
            return result;
        }

        public bool RefreshVisibility()
        {
            if (building == null) return false;
            if (viewCamera == null) viewCamera = Camera.main;
            var matrix = building.localToWorldMatrix;
            var view = viewCamera != null ? viewCamera.projectionMatrix * viewCamera.worldToCameraMatrix : Matrix4x4.identity;
            if (visibilityReady && matrix == lastBuildingMatrix && view == lastViewMatrix) return IsVisible;
            if (!visibilityReady || matrix != lastBuildingMatrix)
            {
                projectionToLocal = matrix.inverse;
                projectionNormalToWorld = projectionToLocal.transpose;
            }
            lastBuildingMatrix = matrix; lastViewMatrix = view; visibilityReady = true;
            var mask = 15;
            IsVisible = true;
            if (viewCamera != null)
            {
                if (frustumCamera != viewCamera || frustumMatrix != view)
                {
                    GeometryUtility.CalculateFrustumPlanes(viewCamera, ViewPlanes);
                    frustumCamera = viewCamera; frustumMatrix = view;
                }
                var extents = body.extents;
                var worldExtents = new Vector3(
                    Mathf.Abs(matrix.m00) * extents.x + Mathf.Abs(matrix.m01) * extents.y + Mathf.Abs(matrix.m02) * extents.z,
                    Mathf.Abs(matrix.m10) * extents.x + Mathf.Abs(matrix.m11) * extents.y + Mathf.Abs(matrix.m12) * extents.z,
                    Mathf.Abs(matrix.m20) * extents.x + Mathf.Abs(matrix.m21) * extents.y + Mathf.Abs(matrix.m22) * extents.z);
                IsVisible = GeometryUtility.TestPlanesAABB(ViewPlanes, new Bounds(matrix.MultiplyPoint3x4(body.center), worldExtents * 2f + Vector3.one * .1f));
                mask = 0;
                for (var face = 0; face < 4; face++)
                {
                    var axis = FaceDirection(face);
                    var center = body.center + Vector3.Scale(axis, body.extents);
                    var towardCamera = viewCamera.orthographic ? -viewCamera.transform.forward : viewCamera.transform.position - matrix.MultiplyPoint3x4(center);
                    if (Vector3.Dot(projectionNormalToWorld.MultiplyVector(axis), towardCamera) > .001f) mask |= 1 << face;
                }
            }
            renderer.enabled = IsVisible;
            if (mask == visibleFaces) return IsVisible;
            visibleFaces = mask; VisibleWallCount = 0;
            visibleTriangles.Clear(); visibleTriangles.AddRange(SurfaceTriangles[0]);
            for (var face = 0; face < 4; face++)
            {
                if (IsFaceVisible(face)) { visibleTriangles.AddRange(SurfaceTriangles[face + 1]); VisibleWallCount++; }
                else
                {
                    Array.Clear(wallWater, face * FaceCells, FaceCells); Array.Clear(wallNext, face * FaceCells, FaceCells);
                    Array.Clear(wallWet, face * FaceCells, FaceCells); Array.Clear(bottomFlow, face * WallColumns, WallColumns);
                }
            }
            Mesh.SetTriangles(visibleTriangles, 0, false);
            return IsVisible;
        }

        private bool IsFaceVisible(int face) => (visibleFaces & (1 << face)) != 0;

        public void Rebuild()
        {
            var previousBody = body;
            var previousWater = geometryReady ? (float[])wallWater.Clone() : null;
            var previousWet = geometryReady ? (float[])wallWet.Clone() : null;
            var previousAreas = (float[])faceArea.Clone();
            body = modelGeometry != null ? modelGeometry.BodyBounds : LocalBounds(building.Find("Structure/Walls").GetComponent<MeshRenderer>());
            roofPanels.Clear();
            var roofStarted = false;
            if (modelGeometry != null) roof = modelGeometry.RoofBounds;
            else foreach (var name in new[] { "Roof_Left", "Roof_Right", "Roof_Ridge" })
            {
                var part = building.Find("Roof/" + name);
                var bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
                var toHouse = building.worldToLocalMatrix * part.localToWorldMatrix;
                var normal = toHouse.inverse.transpose.MultiplyVector(Vector3.up).normalized;
                roofPanels.Add(new RoofPanel { ToPanel = toHouse.inverse, Bounds = bounds,
                    Top = new Plane(normal, toHouse.MultiplyPoint3x4(bounds.center + Vector3.up * bounds.extents.y)) });
                for (var x = -1; x <= 1; x += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var point = toHouse.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, 1f, z)));
                    if (!roofStarted) { roof = new Bounds(point, Vector3.zero); roofStarted = true; } else roof.Encapsulate(point);
                }
            }
            var scale = building.lossyScale;
            worldScale = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            surfaceToWorld = building.localToWorldMatrix;
            surfaceToMesh = Visuals.transform.worldToLocalMatrix * surfaceToWorld;
            var mainRoofNormal = Vector3.up;
            if (modelGeometry != null) modelGeometry.TryProjectRoof(roof.center, out _, out mainRoofNormal);
            var roofSlope = new Vector2(mainRoofNormal.x, mainRoofNormal.z).magnitude;
            roofDx = roof.size.x * Mathf.Abs(scale.x) / RoofColumns;
            roofDz = roof.size.z * Mathf.Abs(scale.z) / RoofRows;
            roofArea = roofDx * roofDz;
            wallDy = body.size.y * Mathf.Abs(scale.y) / WallRows;
            for (var face = 0; face < 4; face++)
            {
                faceDx[face] = (face < 2 ? body.size.z * Mathf.Abs(scale.z) : body.size.x * Mathf.Abs(scale.x)) / WallColumns;
                faceArea[face] = faceDx[face] * wallDy;
            }
            var spacing = Mathf.Min(roofDx, roofDz, wallDy, faceDx[0], faceDx[2]);
            stableStep = Mathf.Min(1f / 90f, .4f * spacing / 1.5f, .2f * spacing * spacing / RoofDiffusion);
            for (var z = 0; z < RoofRows; z++)
            for (var x = 0; x < RoofColumns; x++)
            {
                var point = RoofPosition((x + .5f) / RoofColumns, (z + .5f) / RoofRows);
                var index = z * RoofColumns + x;
                roofCellCenters[index] = new Vector2(point.x * worldScale.x, point.z * worldScale.y);
                roofValid[index] = TryProjectRoofLocal(point, out _, out var normal);
                roofVelocity[index] = modelGeometry != null
                    ? new Vector2(normal.x * .85f + Mathf.Sign(point.x - roof.center.x) * .23f,
                        normal.z * .85f + Mathf.Sign(point.z - roof.center.z) * .32f)
                    : new Vector2(Mathf.Sign(point.x - roof.center.x) * (.23f + Mathf.Abs(normal.x) * .85f),
                        Mathf.Sign(point.z - roof.center.z) * .32f);
            }
            for (var z = 0; z <= RenderRoofRows; z++)
            for (var x = 0; x <= RenderRoofColumns; x++)
            {
                var index = z * (RenderRoofColumns + 1) + x;
                var point = RoofPosition((float)x / RenderRoofColumns, (float)z / RenderRoofRows);
                if (!TryProjectRoofLocal(point, out var surface, out var normal))
                    TryProjectRoofLocal(Vector3.Lerp(point, roof.center, .01f), out surface, out normal);
                basePoints[index] = surface; baseNormals[index] = normal;
                // Flow follows the imported roof's slope, including roofs pitched along Z.
                if (roofSlope > .05f)
                {
                    var alongRidge = Mathf.Abs(mainRoofNormal.z) > Mathf.Abs(mainRoofNormal.x)
                        ? (point.x - roof.min.x) * Mathf.Abs(scale.x) : (point.z - roof.min.z) * Mathf.Abs(scale.z);
                    uvs[index] = new Vector2(alongRidge, Mathf.Max(0f, roof.max.y - surface.y) * Mathf.Abs(scale.y) / roofSlope);
                }
                else
                {
                    var fromRidge = new Vector2((surface.x - roof.center.x) * scale.x, (surface.y - roof.max.y) * scale.y);
                    uvs[index] = new Vector2((point.z - roof.min.z) * Mathf.Abs(scale.z), fromRidge.magnitude);
                }
                CacheVertex(index, RenderRoofColumns, RenderRoofRows, x, z, RoofColumns, RoofRows, 0, 1f / roofArea);
            }
            var surfaces = new List<Bounds>();
            foreach (var part in building.GetComponentsInChildren<MeshRenderer>())
                if (part.enabled && !IsEquipmentOrRoof(part.transform)) surfaces.Add(LocalBounds(part));
            for (var face = 0; face < 4; face++)
            {
                var direction = FaceDirection(face); var across = face < 2 ? Vector3.forward : Vector3.right;
                var padding = .005f / building.TransformVector(direction).magnitude;
                for (var column = 0; column <= RenderWallColumns; column++)
                {
                    var u = (float)column / RenderWallColumns;
                    var offset = Mathf.Lerp(face < 2 ? body.min.z : body.min.x, face < 2 ? body.max.z : body.max.x, u);
                    var lip = across * offset + direction * (Vector3.Dot(roof.center, direction) + (face < 2 ? roof.extents.x : roof.extents.z));
                    if (!TryProjectRoofLocal(lip, out var lipPoint, out var lipNormal))
                        TryProjectRoofLocal(Vector3.Lerp(lip, roof.center, .01f), out lipPoint, out lipNormal);
                    // Antennas and rooftop plant must not extend the facade water above its eaves.
                    var wallTop = modelGeometry != null ? Mathf.Min(body.max.y, lipPoint.y) : body.max.y;
                    for (var row = 0; row <= RenderWallRows + 1; row++)
                    {
                        var index = RoofVertices + face * FaceVertices + row * (RenderWallColumns + 1) + column;
                        if (row == 0) { basePoints[index] = lipPoint; baseNormals[index] = lipNormal; }
                        else
                        {
                            var height = Mathf.Lerp(wallTop, body.min.y, (float)(row - 1) / RenderWallRows);
                            var outer = Vector3.Dot(body.center, direction) + (face < 2 ? body.extents.x : body.extents.z);
                            foreach (var part in surfaces)
                            {
                                if (height < part.min.y - body.size.y / RenderWallRows * .55f || height > part.max.y + body.size.y / RenderWallRows * .55f) continue;
                                if (offset < (face < 2 ? part.min.z : part.min.x) || offset > (face < 2 ? part.max.z : part.max.x)) continue;
                                outer = Mathf.Max(outer, Vector3.Dot(part.center, direction) + (face < 2 ? part.extents.x : part.extents.z));
                            }
                            basePoints[index] = across * offset + Vector3.up * height + direction * (outer + padding);
                            baseNormals[index] = direction;
                        }
                        uvs[index] = new Vector2(u * faceDx[face] * WallColumns, (float)Mathf.Max(0, row - 1) / RenderWallRows * wallDy * WallRows);
                        CacheVertex(index, RenderWallColumns, RenderWallRows, column, Mathf.Max(0, row - 1), WallColumns, WallRows, face * FaceCells, 1f / faceArea[face]);
                    }
                }
            }
            // Preserve water on the remaining upper wall when the ground floor is opened.
            if (previousWater != null)
            {
                for (var face = 0; face < 4; face++)
                for (var row = 0; row < WallRows; row++)
                for (var x = 0; x < WallColumns; x++)
                {
                    var height = Mathf.Lerp(body.max.y, body.min.y, (row + .5f) / WallRows);
                    var oldRow = Mathf.Clamp(Mathf.FloorToInt((previousBody.max.y - height) / previousBody.size.y * WallRows), 0, WallRows - 1);
                    var old = face * FaceCells + oldRow * WallColumns + x;
                    var index = face * FaceCells + row * WallColumns + x;
                    wallWater[index] = previousWater[old] / previousAreas[face] * faceArea[face]; wallWet[index] = previousWet[old];
                }
            }
            geometryReady = true; visibilityReady = false;
            Mesh.vertices = vertices; Mesh.normals = normals; Mesh.uv = uvs;
            var waterBounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var point in vertices) waterBounds.Encapsulate(point);
            waterBounds.Expand(.035f / Mathf.Max(.01f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            Mesh.bounds = waterBounds;
            RefreshVisibility(); DrawWater();
        }

        public void Advance(float sprayAge)
        {
            LastSimulationSteps = 0;
            if (!RefreshVisibility()) { age = sprayAge; return; }
            PrepareSources(sprayAge);
            // Discard excess catch-up time instead of increasing the next frame's workload.
            var remaining = Mathf.Min(stableStep * MaxSimulationSteps, Mathf.Max(0f, sprayAge - age));
            var clock = sprayAge - remaining;
            while (remaining > .000001f && LastSimulationSteps < MaxSimulationSteps)
            {
                var step = Mathf.Min(remaining, stableStep); clock += step;
                Simulate(step, clock); remaining -= step; LastSimulationSteps++;
            }
            age = sprayAge; DrawWater();
        }

        private void Simulate(float step, float clock)
        {
            Inject(step, clock); Array.Clear(roofNext, 0, roofNext.Length); Array.Clear(wallNext, 0, wallNext.Length);
            var bottomDecay = Mathf.Exp(-step * 8f); var roofDecay = Mathf.Exp(-step * .18f);
            var wallDecay = Mathf.Exp(-step * .35f); var wetDecay = Mathf.Exp(-step * .4f);
            for (var i = 0; i < bottomFlow.Length; i++) bottomFlow[i] *= bottomDecay;
            for (var z = 0; z < RoofRows; z++)
            for (var x = 0; x < RoofColumns; x++)
            {
                var index = z * RoofColumns + x; var volume = roofWater[index]; if (volume <= 0f) continue;
                var speed = roofVelocity[index];
                var left = Outflow(volume, RoofAt(x - 1, z), roofArea, roofDx, RoofDiffusion, Mathf.Max(0f, -speed.x), step);
                var right = Outflow(volume, RoofAt(x + 1, z), roofArea, roofDx, RoofDiffusion, Mathf.Max(0f, speed.x), step);
                var front = Outflow(volume, RoofAt(x, z - 1), roofArea, roofDz, RoofDiffusion, Mathf.Max(0f, -speed.y), step);
                var back = Outflow(volume, RoofAt(x, z + 1), roofArea, roofDz, RoofDiffusion, Mathf.Max(0f, speed.y), step);
                var limit = Mathf.Min(1f, volume / Mathf.Max(.00000001f, left + right + front + back));
                left *= limit; right *= limit; front *= limit; back *= limit;
                roofNext[index] += Mathf.Max(0f, volume - left - right - front - back);
                RoofTransfer(x - 1, z, 1, (z + .5f) / RoofRows, left);
                RoofTransfer(x + 1, z, 0, (z + .5f) / RoofRows, right);
                RoofTransfer(x, z - 1, 3, (x + .5f) / RoofColumns, front);
                RoofTransfer(x, z + 1, 2, (x + .5f) / RoofColumns, back);
            }
            for (var face = 0; face < 4; face++)
            {
                if (!IsFaceVisible(face)) continue;
                for (var row = 0; row < WallRows; row++)
                for (var x = 0; x < WallColumns; x++)
                {
                    var index = face * FaceCells + row * WallColumns + x; var volume = wallWater[index]; if (volume <= 0f) continue;
                    var sway = .065f * Mathf.Sin(clock * 2f + row * .35f + x * .8f + face);
                    var left = Outflow(volume, WallAt(face, x - 1, row), faceArea[face], faceDx[face], WallDiffusion, Mathf.Max(0f, -sway), step);
                    var right = Outflow(volume, WallAt(face, x + 1, row), faceArea[face], faceDx[face], WallDiffusion, Mathf.Max(0f, sway), step);
                    var up = row > 0 ? Outflow(volume, WallAt(face, x, row - 1), faceArea[face], wallDy, WallDiffusion, 0f, step) : 0f;
                    var down = Outflow(volume, WallAt(face, x, row + 1), faceArea[face], wallDy, WallDiffusion, 1.25f + .12f * Mathf.Sin(clock * 2f + x * .9f), step);
                    var limit = Mathf.Min(1f, volume / Mathf.Max(.00000001f, left + right + up + down));
                    left *= limit; right *= limit; up *= limit; down *= limit;
                    wallNext[index] += Mathf.Max(0f, volume - left - right - up - down);
                    if (x > 0) wallNext[index - 1] += left; else AroundCorner(face, row, false, left);
                    if (x + 1 < WallColumns) wallNext[index + 1] += right; else AroundCorner(face, row, true, right);
                    if (row > 0) wallNext[index - WallColumns] += up;
                    if (row + 1 < WallRows) wallNext[index + WallColumns] += down;
                    else { DrainedVolume += down; bottomFlow[face * WallColumns + x] += down; }
                }
            }
            var swap = roofWater; roofWater = roofNext; roofNext = swap;
            swap = wallWater; wallWater = wallNext; wallNext = swap;
            var wallCount = 0; var roofCount = 0;
            for (var i = 0; i < RoofCells; i++)
            {
                roofWater[i] *= roofDecay;
                var depth = roofWater[i] / roofArea;
                roofWet[i] = Mathf.Max(roofWet[i] * wetDecay, Mathf.Clamp01(depth / .004f));
                if (depth > .0008f) roofCount++;
            }
            for (var face = 0; face < 4; face++)
            {
                if (!IsFaceVisible(face)) continue;
                for (var i = face * FaceCells; i < (face + 1) * FaceCells; i++)
                {
                    wallWater[i] *= wallDecay;
                    var depth = wallWater[i] / faceArea[i / FaceCells];
                    wallWet[i] = Mathf.Max(wallWet[i] * wetDecay, Mathf.Clamp01(depth / .003f));
                    if (depth > .0008f) wallCount++;
                }
            }
            RoofWetCoverage = (float)roofCount / RoofCells;
            WallWetCoverage = VisibleWallCount > 0 ? (float)wallCount / (VisibleWallCount * FaceCells) : 0f;
        }

        private void PrepareSources(float clock)
        {
            for (var head = 0; head < 4; head++)
            {
                sourceCounts[head] = 0;
                if (!hasSource[head]) continue;
                var source = new Vector2(sourcePoints[head].x * worldScale.x, sourcePoints[head].z * worldScale.y);
                var radius = .105f + .015f * Mathf.Sin(clock * 3f + head);
                var inverseRadius = 1f / (radius * radius); var weight = 0f;
                var offset = head * RoofCells;
                for (var cell = 0; cell < RoofCells; cell++)
                {
                    if (!roofValid[cell]) continue;
                    var delta = roofCellCenters[cell] - source;
                    var falloff = 1f - delta.sqrMagnitude * inverseRadius;
                    if (falloff <= 0f) continue;
                    var index = offset + sourceCounts[head]++;
                    sourceCells[index] = cell; sourceWeights[index] = falloff * falloff;
                    weight += sourceWeights[index];
                }
                if (weight <= .00001f) { sourceCounts[head] = 0; continue; }
                for (var i = offset; i < offset + sourceCounts[head]; i++) sourceWeights[i] /= weight;
            }
        }

        private void Inject(float step, float clock)
        {
            for (var head = 0; head < 4; head++)
            {
                if (!hasSource[head] || clock < sourceFlight[head] || sourceCounts[head] == 0) continue;
                var volume = .009f * step; InjectedVolume += volume;
                var offset = head * RoofCells;
                for (var i = offset; i < offset + sourceCounts[head]; i++) roofWater[sourceCells[i]] += volume * sourceWeights[i];
            }
        }

        private static float Outflow(float volume, float neighbour, float area, float distance, float diffusion, float velocity, float step) =>
            step * (velocity * volume / distance + diffusion * Mathf.Max(0f, volume - neighbour) / (distance * distance));

        private float RoofAt(int x, int z) => x < 0 || x >= RoofColumns || z < 0 || z >= RoofRows || !roofValid[z * RoofColumns + x] ? 0f : roofWater[z * RoofColumns + x];
        private float WallAt(int face, int x, int row) => x < 0 || x >= WallColumns || row < 0 || row >= WallRows ? 0f : wallWater[face * FaceCells + row * WallColumns + x];

        private void RoofTransfer(int x, int z, int face, float across, float amount)
        {
            if (x >= 0 && x < RoofColumns && z >= 0 && z < RoofRows && roofValid[z * RoofColumns + x]) { roofNext[z * RoofColumns + x] += amount; return; }
            if (!IsFaceVisible(face)) { DrainedVolume += amount; return; }
            var center = Mathf.Clamp(Mathf.FloorToInt(across * WallColumns), 0, WallColumns - 1); var sum = 0f;
            for (var offset = -2; offset <= 2; offset++) if (center + offset >= 0 && center + offset < WallColumns) sum += 1f / (1f + offset * offset);
            for (var offset = -2; offset <= 2; offset++)
                if (center + offset >= 0 && center + offset < WallColumns) wallNext[face * FaceCells + center + offset] += amount / (1f + offset * offset) / sum;
        }

        private void AroundCorner(int face, int row, bool positive, float volume)
        {
            var nextFace = face < 2 ? (positive ? 2 : 3) : (positive ? 0 : 1);
            if (!IsFaceVisible(nextFace)) { DrainedVolume += volume; return; }
            var column = face == 0 || face == 2 ? WallColumns - 1 : 0;
            wallNext[nextFace * FaceCells + row * WallColumns + column] += volume;
        }

        private void CacheVertex(int index, int renderColumns, int renderRows, int x, int y, int columns, int rows, int start, float inverseArea)
        {
            var gx = Mathf.Clamp((float)x / renderColumns * columns - .5f, 0f, columns - 1f);
            var gy = Mathf.Clamp((float)y / renderRows * rows - .5f, 0f, rows - 1f);
            var left = Mathf.FloorToInt(gx); var top = Mathf.FloorToInt(gy);
            var right = Mathf.Min(left + 1, columns - 1); var bottom = Mathf.Min(top + 1, rows - 1);
            samples[index] = new WaterSample { A = start + top * columns + left, B = start + top * columns + right,
                C = start + bottom * columns + left, D = start + bottom * columns + right,
                X = gx - left, Y = gy - top, InverseArea = inverseArea };
            var axis = baseNormals[index];
            meshBasePoints[index] = surfaceToMesh.MultiplyPoint3x4(basePoints[index]);
            meshOffsetAxes[index] = surfaceToMesh.MultiplyVector(axis) / Mathf.Max(.0001f, surfaceToWorld.MultiplyVector(axis).magnitude);
            normals[index] = surfaceToMesh.MultiplyVector(axis).normalized;
            vertices[index] = meshBasePoints[index];
            ripplePhases[index] = new Vector4(Mathf.Sin(uvs[index].x * 33f), Mathf.Cos(uvs[index].x * 33f),
                Mathf.Sin(uvs[index].y * 27f), Mathf.Cos(uvs[index].y * 27f));
        }

        private void DrawWater()
        {
            var phase = Mathf.Sin(age * 5f) * .2f;
            rippleClock = new Vector4(Mathf.Cos(phase), Mathf.Sin(phase), Mathf.Cos(age * 8f), Mathf.Sin(age * 8f));
            for (var index = 0; index < RoofVertices; index++) WriteVertex(index, true);
            for (var face = 0; face < 4; face++)
            {
                if (!IsFaceVisible(face)) continue;
                for (var index = RoofVertices + face * FaceVertices; index < RoofVertices + (face + 1) * FaceVertices; index++) WriteVertex(index, false);
            }
            Mesh.vertices = vertices; Mesh.colors = colors;
            properties.SetFloat("_FlowTime", age); renderer.SetPropertyBlock(properties);
        }

        private void WriteVertex(int index, bool onRoof)
        {
            var sample = samples[index];
            var water = onRoof ? roofWater : wallWater; var wetness = onRoof ? roofWet : wallWet;
            var depth = Mathf.Lerp(Mathf.Lerp(water[sample.A], water[sample.B], sample.X), Mathf.Lerp(water[sample.C], water[sample.D], sample.X), sample.Y) * sample.InverseArea;
            var wet = Mathf.Lerp(Mathf.Lerp(wetness[sample.A], wetness[sample.B], sample.X), Mathf.Lerp(wetness[sample.C], wetness[sample.D], sample.X), sample.Y);
            var phases = ripplePhases[index];
            var ripple = (phases.x * rippleClock.x + phases.y * rippleClock.y) * (phases.z * rippleClock.z - phases.w * rippleClock.w) * .0007f * Mathf.Clamp01(depth / .004f);
            vertices[index] = meshBasePoints[index] + meshOffsetAxes[index] * (.002f + Mathf.Min(.003f, depth * .15f) + ripple);
            colors[index] = new Color(Mathf.Clamp01(depth / (onRoof ? .018f : .012f)), wet, Mathf.Clamp01(depth / (onRoof ? .02f : .012f)), Mathf.Clamp01(depth / .002f));
        }

        public bool TryGetDrip(int sample, out Vector3 point, out Vector3 outward)
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var columnIndex = (sample * 17 + attempt * 23) % (4 * WallColumns);
                var face = columnIndex / WallColumns; var column = columnIndex % WallColumns;
                if (!IsFaceVisible(face)) continue;
                var cell = face * FaceCells + (WallRows - 1) * WallColumns + column;
                var fromEaves = sample % 3 != 0;
                if (fromEaves) cell = face * FaceCells + column;
                if (wallWater[cell] / faceArea[face] < .0005f) continue;
                var row = fromEaves ? 0 : RenderWallRows + 1;
                var renderColumn = Mathf.Min(RenderWallColumns - 1, Mathf.FloorToInt((column + .5f) / WallColumns * RenderWallColumns));
                var index = RoofVertices + face * FaceVertices + row * (RenderWallColumns + 1) + renderColumn;
                point = lastBuildingMatrix.MultiplyPoint3x4(Vector3.Lerp(basePoints[index], basePoints[index + 1], .5f));
                outward = projectionNormalToWorld.MultiplyVector(FaceDirection(face)).normalized; point += outward * .008f;
                return true;
            }
            point = outward = Vector3.zero; return false;
        }

        public void SetSource(int head, Vector3 point, float travelTime, bool wetsRoof)
        { sourcePoints[head] = projectionToLocal.MultiplyPoint3x4(point); sourceFlight[head] = travelTime; hasSource[head] = wetsRoof; }

        public bool TryProjectRoof(Vector3 point, out Vector3 surface, out Vector3 normal)
        {
            if (!visibilityReady) RefreshVisibility();
            if (TryProjectRoofLocal(projectionToLocal.MultiplyPoint3x4(point), out var localSurface, out var localNormal))
            { surface = lastBuildingMatrix.MultiplyPoint3x4(localSurface); normal = projectionNormalToWorld.MultiplyVector(localNormal).normalized; return true; }
            surface = normal = Vector3.zero; return false;
        }

        private bool TryProjectRoofLocal(Vector3 point, out Vector3 surface, out Vector3 normal)
        {
            if (modelGeometry != null) return modelGeometry.TryProjectRoof(point, out surface, out normal);
            var height = float.NegativeInfinity; normal = Vector3.up;
            foreach (var panel in roofPanels)
            {
                var axis = panel.Top.normal; if (axis.y <= .01f) continue;
                var y = (-panel.Top.distance - axis.x * point.x - axis.z * point.z) / axis.y;
                if (y <= height) continue;
                var onPanel = panel.ToPanel.MultiplyPoint3x4(new Vector3(point.x, y, point.z));
                if (onPanel.x < panel.Bounds.min.x - .001f || onPanel.x > panel.Bounds.max.x + .001f || onPanel.z < panel.Bounds.min.z - .001f || onPanel.z > panel.Bounds.max.z + .001f) continue;
                height = y; normal = axis;
            }
            surface = new Vector3(point.x, float.IsNegativeInfinity(height) ? roof.min.y : height, point.z);
            return !float.IsNegativeInfinity(height);
        }

        private Vector3 RoofPosition(float x, float z) => new Vector3(Mathf.Lerp(roof.min.x, roof.max.x, x), roof.max.y, Mathf.Lerp(roof.min.z, roof.max.z, z));
        private static Vector3 FaceDirection(int face) => face == 0 ? Vector3.right : face == 1 ? Vector3.left : face == 2 ? Vector3.forward : Vector3.back;

        private Bounds LocalBounds(MeshRenderer part)
        {
            var bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
            var matrix = building.worldToLocalMatrix * part.transform.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (var x = -1; x <= 1; x += 2) for (var y = -1; y <= 1; y += 2) for (var z = -1; z <= 1; z += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
            return result;
        }

        private bool IsEquipmentOrRoof(Transform part)
        {
            for (var parent = part; parent != null && parent != building; parent = parent.parent)
                if (parent.name == "Roof_Sprinkler" || parent.name == "Sprinkler_Water_Effects" || parent.name == "Roof" || parent.name == "Rail_System" || parent.name == "Open_Ground_Floor") return true;
            return false;
        }

        public void SetActive(bool active)
        {
            Visuals.SetActive(active);
            if (active) return;
            foreach (var buffer in new[] { roofWater, roofNext, wallWater, wallNext, roofWet, wallWet, bottomFlow }) Array.Clear(buffer, 0, buffer.Length);
            Array.Clear(hasSource, 0, hasSource.Length); Array.Clear(sourceCounts, 0, sourceCounts.Length); LastSimulationSteps = 0; age = InjectedVolume = DrainedVolume = WallWetCoverage = RoofWetCoverage = 0f;
        }

        public void Dispose()
        { if (Visuals != null) UnityEngine.Object.Destroy(Visuals); if (Mesh != null) UnityEngine.Object.Destroy(Mesh); }
    }
}
