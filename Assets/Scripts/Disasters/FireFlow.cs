using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CivilGameJam.Disasters
{
    /// <summary>Incompressible curl motion and rolling eddies for stylized, advected fire wisps.</summary>
    internal static class FireFlow
    {
        public static Vector3 Curl(Vector3 position, float time)
        {
            var p = position * 1.7f; var t = time * 1.3f;
            // Curl of a vector potential creates circulation rather than independent positional jitter.
            return new Vector3(
                -Mathf.Sin(p.x + t * 0.6f) * Mathf.Sin(p.y) - Mathf.Cos(p.z - t * 0.7f) * Mathf.Cos(p.x),
                -Mathf.Sin(p.y + t) * Mathf.Sin(p.z) - Mathf.Cos(p.x + t * 0.6f) * Mathf.Cos(p.y),
                -Mathf.Sin(p.z - t * 0.7f) * Mathf.Sin(p.x) - Mathf.Cos(p.y + t) * Mathf.Cos(p.z));
        }

        public static Vector3 JetRoll(Vector3 offset, Vector3 forward, Vector3 across, Vector3 up,
            float length, float spread, float time)
        {
            var depth = Vector3.Dot(offset, forward);
            var x = Vector3.Dot(offset, across); var y = Vector3.Dot(offset, up) / 0.38f;
            var radius = Mathf.Sqrt(x * x + y * y);
            if (radius < 0.02f) return Vector3.zero;
            var radial = (across * x + up * y * 0.38f) / radius;
            var result = Vector3.zero;
            for (var ring = 0; ring < 4; ring++)
            {
                var center = Mathf.Repeat(time * 0.72f + ring * 0.38f, 1.5f) - 0.1f;
                var radialOffset = radius - (0.1f + spread * Mathf.Max(0f, center)) * 0.7f;
                var axialOffset = depth - length * center;
                var scale = 0.35f + spread * Mathf.Max(0.1f, center) * 0.23f;
                var weight = Mathf.Exp(-(radialOffset * radialOffset + axialOffset * axialOffset) / (scale * scale));
                // The leading rim turns inward; inner material folds around the traveling eddy.
                result += (-radial * axialOffset + forward * radialOffset) * (weight * 7f);
            }
            return result * Mathf.SmoothStep(0f, 1f, depth / Mathf.Max(0.1f, length * 0.25f));
        }

        public static void ConfigureGas(ParticleSystem system, Material material)
        {
            var main = system.main; main.startRotation3D = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX, ParticleSystemVertexStream.Center
            });
        }
    }
}
