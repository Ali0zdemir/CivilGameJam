using System;
using CivilGameJam.Upgrades;
using UnityEngine;
using UnityEngine.Rendering;

namespace CivilGameJam.Disasters
{
    /// <summary>The reduced surge travels continuously from the breakwater to the city inlet.</summary>
    internal sealed class BreakwaterFlowSurface : IDisposable
    {
        private const int Columns = 40;
        private const int Rows = 32;
        private readonly Bounds city;
        private readonly float ground;
        private readonly float sea;
        private readonly float halfWidth;
        private readonly float settledDepth;
        private readonly float crestDepth;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Vector3[] vertices = new Vector3[(Columns + 1) * (Rows + 1)];
        private readonly Color[] colors = new Color[(Columns + 1) * (Rows + 1)];
        private readonly int[] triangles = new int[Columns * Rows * 6];

        public Mesh Mesh { get; }

        public BreakwaterFlowSurface(Transform parent, Material material, Bounds cityBounds, float groundLevel,
            float seaLevel, float width, float incomingDepth, float incomingCrestDepth)
        {
            city = cityBounds; ground = groundLevel; sea = seaLevel;
            halfWidth = Mathf.Max(1f, width * 0.5f);
            settledDepth = incomingDepth; crestDepth = incomingCrestDepth;
            var water = new GameObject("Breakwater_Transmitted_Flow");
            water.transform.SetParent(parent, false);
            renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Mesh = new Mesh { name = "Reduced surge behind breakwater" };
            Mesh.MarkDynamic();
            water.AddComponent<MeshFilter>().sharedMesh = Mesh;
            var index = 0;
            for (var z = 0; z < Rows; z++)
            for (var x = 0; x < Columns; x++)
            {
                var a = z * (Columns + 1) + x; var b = a + Columns + 1;
                triangles[index++] = a; triangles[index++] = b; triangles[index++] = a + 1;
                triangles[index++] = a + 1; triangles[index++] = b; triangles[index++] = b + 1;
            }
            Clear();
        }

        public void Update(TsunamiBreakwater breakwater, CityFloodSurface flood, float front, float elapsed, float speed, float pulseDuration, float fade)
        {
            if (breakwater == null || !breakwater.IsActive || breakwater.Transmission <= 0f) { Clear(); return; }
            var start = breakwater.FrontDistance - 0.12f;
            var end = city.min.z;
            var wet = false;
            speed = Mathf.Max(1f, speed);
            pulseDuration = Mathf.Max(0.3f, pulseDuration);
            for (var z = 0; z <= Rows; z++)
            for (var x = 0; x <= Columns; x++)
            {
                var pz = Mathf.Lerp(start, end, (float)z / Rows);
                var shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(end - 0.65f, end, pz));
                var span = Mathf.Lerp(halfWidth, city.extents.x, shore);
                var px = city.center.x + Mathf.Lerp(-span, span, (float)x / Columns);
                var boundary = breakwater.TryGetWaveFace(px, out var face) ? face : breakwater.FrontDistance;
                var behindWall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(boundary - 0.12f, boundary + 0.05f, pz));
                var side = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, Mathf.Abs(px - city.center.x) / span));
                side = Mathf.Lerp(side, 1f, shore);
                // The same advancing pulse feeds the city; it never fills the bay all at once.
                var age = (front - pz) / speed;
                var head = Mathf.SmoothStep(0f, 1f, age / 0.12f);
                var tail = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pulseDuration * 0.45f, pulseDuration, age));
                var pulse = Mathf.Exp(-Mathf.Pow((age - 0.14f) / 0.3f, 2f));
                var depth = (settledDepth + (crestDepth - settledDepth) * pulse) * head * tail * breakwater.Transmission;
                var alpha = Mathf.SmoothStep(0f, 1f, depth / 0.025f) * side;
                var ripple = Mathf.Sin(pz * 6f - elapsed * speed * 2f + px * 0.8f) * 0.006f * head * tail;
                ripple *= Mathf.SmoothStep(0f, 1f, (end - pz) / 0.3f);
                var index = z * (Columns + 1) + x;
                // The flanks fall to sea level, avoiding elevated, detached water sheets.
                var level = Mathf.Lerp(sea, ground + Mathf.Clamp(depth + ripple, 0f, crestDepth), side);
                var streak = Mathf.Sin(pz * 3f - elapsed * speed + px * 0.5f) * 0.035f;
                var color = new Color(0.1f * head * tail, 0.36f + depth / Mathf.Max(0.01f, crestDepth) * 0.22f + streak, 1f, alpha);
                // Both meshes share the rendered inlet height and tint, including the side corners.
                flood.SampleInlet(px, out var inletLevel, out var inletColor);
                level = Mathf.Lerp(level, inletLevel, shore);
                color = Color.Lerp(color, inletColor, shore); color.a *= behindWall;
                vertices[index] = new Vector3(px, level, pz); colors[index] = color;
                wet |= color.a > 0.003f;
            }
            if (!wet) { Clear(); return; }
            Mesh.vertices = vertices; Mesh.colors = colors; Mesh.triangles = triangles;
            Mesh.RecalculateNormals(); Mesh.RecalculateBounds();
            properties.SetFloat("_Fade", fade);
            properties.SetFloat("_Opacity", 0.92f);
            properties.SetFloat("_FacetedLighting", 0.15f);
            renderer.SetPropertyBlock(properties);
            renderer.enabled = true;
        }

        public void Clear()
        {
            if (Mesh.vertexCount > 0) Mesh.Clear();
            renderer.enabled = false;
        }

        public void Dispose() { if (Mesh != null) UnityEngine.Object.Destroy(Mesh); }
    }
}
