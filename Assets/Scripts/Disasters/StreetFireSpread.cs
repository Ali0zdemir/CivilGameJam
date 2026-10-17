using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CivilGameJam.Disasters
{
    /// <summary>Routes a soft fire front through the spaces around building foundations.</summary>
    internal sealed class StreetFireSpread : IDisposable
    {
        internal sealed class Cell
        {
            public Vector3 Position;
            public int Owner;
            public float Radius;
            public float Delay;
            public float Remainder;
            public float Strength;
        }

        public readonly List<Cell> Cells = new List<Cell>();
        public float[] ArrivalTimes { get; private set; }
        public int ActiveCellCount { get; private set; }
        private readonly Transform root;
        private readonly Mesh mesh;
        private Color32[] colors;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        public StreetFireSpread(Transform parent, Material material)
        {
            root = new GameObject("Soft_Fire_Between_Buildings", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            root.SetParent(parent, false);
            mesh = new Mesh { name = "Soft street fire front" }; mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        public void Rebuild(Bounds[] foundations, Bounds groundBounds, Vector3 mouth, Vector3 target, float halfAngle, float startTime, float spreadTime)
        {
            Cells.Clear(); ActiveCellCount = 0;
            var area = foundations[0];
            foreach (var foundation in foundations) area.Encapsulate(foundation);
            var ground = groundBounds.max.y + 0.025f;
            area.Expand(new Vector3(0.7f, 0f, 0.65f));
            area.SetMinMax(new Vector3(Mathf.Max(area.min.x, groundBounds.min.x), ground, Mathf.Max(area.min.z, groundBounds.min.z)),
                new Vector3(Mathf.Min(area.max.x, groundBounds.max.x), ground, Mathf.Min(area.max.z, groundBounds.max.z)));
            var spacing = Mathf.Max(0.13f, Mathf.Sqrt(area.size.x * area.size.z / 3200f));
            var columns = Mathf.CeilToInt(area.size.x / spacing);
            var rows = Mathf.CeilToInt(area.size.z / spacing);
            var grid = new int[columns * rows];
            var distances = new int[grid.Length];
            var cellGridIndices = new List<int>();
            var forward = target - mouth; forward.y = 0f; forward.Normalize();
            var across = Vector3.Cross(Vector3.up, forward);
            for (var index = 0; index < grid.Length; index++)
            {
                grid[index] = -1; distances[index] = -1;
                var position = new Vector3(area.min.x + (index % columns + 0.5f) * spacing, ground,
                    area.min.z + (index / columns + 0.5f) * spacing);
                var nearest = float.MaxValue; var owner = 0; var inside = false;
                for (var site = 0; site < foundations.Length; site++)
                {
                    var bounds = foundations[site];
                    var dx = Mathf.Max(bounds.min.x - position.x, 0f, position.x - bounds.max.x);
                    var dz = Mathf.Max(bounds.min.z - position.z, 0f, position.z - bounds.max.z);
                    if (dx <= 0.01f && dz <= 0.01f) { inside = true; break; }
                    var distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance < nearest) { nearest = distance; owner = site; }
                }
                if (inside) continue;
                grid[index] = Cells.Count; cellGridIndices.Add(index);
                var radius = Mathf.Min(spacing * 0.85f, nearest + 0.025f, position.x - area.min.x,
                    area.max.x - position.x, position.z - area.min.z, area.max.z - position.z);
                Cells.Add(new Cell { Position = position, Owner = owner, Radius = Mathf.Max(0.005f, radius) });
            }
            var queue = new Queue<int>();
            var slope = Mathf.Tan(halfAngle * Mathf.Deg2Rad);
            var impactDepth = Vector3.Dot(target - mouth, forward);
            foreach (var index in cellGridIndices)
            {
                var offset = Cells[grid[index]].Position - mouth;
                var depth = Vector3.Dot(offset, forward);
                if (Mathf.Abs(depth - impactDepth) > spacing * 2.5f || depth <= 0f || Mathf.Abs(Vector3.Dot(offset, across)) > depth * slope) continue;
                distances[index] = 0; queue.Enqueue(index);
            }
            if (queue.Count == 0 && Cells.Count > 0)
            {
                var nearest = 0;
                for (var i = 1; i < Cells.Count; i++)
                    if ((Cells[i].Position - target).sqrMagnitude < (Cells[nearest].Position - target).sqrMagnitude) nearest = i;
                var index = cellGridIndices[nearest]; distances[index] = 0; queue.Enqueue(index);
            }
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                Visit(index - columns, index); Visit(index + columns, index);
                if (index % columns > 0) Visit(index - 1, index);
                if (index % columns < columns - 1) Visit(index + 1, index);
            }
            void Visit(int neighbor, int previous)
            {
                if (neighbor < 0 || neighbor >= grid.Length || grid[neighbor] < 0 || distances[neighbor] >= 0) return;
                distances[neighbor] = distances[previous] + 1; queue.Enqueue(neighbor);
            }
            var maximum = 1;
            foreach (var index in cellGridIndices) maximum = Mathf.Max(maximum, distances[index]);
            ArrivalTimes = new float[foundations.Length];
            for (var i = 0; i < ArrivalTimes.Length; i++) ArrivalTimes[i] = float.MaxValue;
            for (var i = 0; i < Cells.Count; i++)
            {
                var cell = Cells[i]; var distance = distances[cellGridIndices[i]];
                // Courtyards sealed by foundations receive drifting fire after the connected street front.
                if (distance < 0) distance = maximum;
                cell.Delay = startTime + (float)distance / maximum * spreadTime;
                ArrivalTimes[cell.Owner] = Mathf.Min(ArrivalTimes[cell.Owner], cell.Delay);
            }
            for (var site = 0; site < ArrivalTimes.Length; site++)
                if (ArrivalTimes[site] == float.MaxValue) ArrivalTimes[site] = startTime + spreadTime;
            BuildMesh();
        }

        private void BuildMesh()
        {
            var vertices = new Vector3[Cells.Count * 4]; var uv = new Vector2[vertices.Length];
            colors = new Color32[vertices.Length]; var triangles = new int[Cells.Count * 6];
            for (var i = 0; i < Cells.Count; i++)
            {
                var cell = Cells[i]; var vertex = i * 4;
                for (var corner = 0; corner < 4; corner++)
                {
                    var x = (corner & 1) == 0 ? -1f : 1f; var z = (corner & 2) == 0 ? -1f : 1f;
                    vertices[vertex + corner] = root.InverseTransformPoint(cell.Position + new Vector3(x, 0f, z) * cell.Radius);
                    uv[vertex + corner] = new Vector2((x + 1f) * 0.5f, (z + 1f) * 0.5f);
                }
                var offset = i * 6;
                triangles[offset] = vertex; triangles[offset + 1] = vertex + 2; triangles[offset + 2] = vertex + 1;
                triangles[offset + 3] = vertex + 1; triangles[offset + 4] = vertex + 2; triangles[offset + 5] = vertex + 3;
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.uv = uv; mesh.colors32 = colors; mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        public void Update(float seconds, float release, float[] suppression)
        {
            properties.SetFloat("_FireTime", seconds); root.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            ActiveCellCount = 0;
            for (var i = 0; i < Cells.Count; i++)
            {
                var cell = Cells[i];
                cell.Strength = Mathf.SmoothStep(0f, 1f, (seconds - cell.Delay) / 0.5f) * release * suppression[cell.Owner];
                if (cell.Strength > 0.01f) ActiveCellCount++;
                var color = new Color(1f, 0.5f, 0.035f, cell.Strength * 0.72f);
                for (var corner = 0; corner < 4; corner++) colors[i * 4 + corner] = color;
            }
            mesh.colors32 = colors;
        }

        public void Clear()
        {
            ActiveCellCount = 0;
            if (colors == null) return;
            Array.Clear(colors, 0, colors.Length); mesh.colors32 = colors;
        }

        public void Dispose() { if (mesh != null) Object.Destroy(mesh); }
    }
}
