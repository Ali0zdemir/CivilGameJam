using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace CivilGameJam.Upgrades
{
    /// <summary>The shared low poly installation puff used by house upgrades.</summary>
    internal static class ConstructionDustPuff
    {
        public static ParticleSystem Create(Transform parent, Material material, out Mesh mesh)
        {
            var child = new GameObject("Installation_Dust_Puff");
            // Keep transient particles outside the building bounds used by disaster previews.
            child.transform.SetParent(parent, false);
            child.SetActive(false);
            var particles = child.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.startSize3D = true;
            main.startRotation3D = true;
            main.startSpeed = 0f;
            main.maxParticles = 128;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.18f, 1f), new Keyframe(0.6f, 1.3f), new Keyframe(1f, 1.4f)));
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0.85f, 0.12f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) }
            };
            mesh = CreateMesh();
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.sharedMaterial = material;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            child.SetActive(true);
            return particles;
        }

        public static void Emit(ParticleSystem particles, Transform building, Bounds bounds, float amount,
            float size, float lifetime, Random random, Vector3 worldOffset, IReadOnlyList<Vector3> origins = null, float lightening = 0f, bool clearExisting = true)
        {
            if (clearExisting) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!particles.isPlaying) particles.Play(true);
            var scale = building.lossyScale;
            var footprint = new Vector2(bounds.size.x * Mathf.Abs(scale.x), bounds.size.z * Mathf.Abs(scale.z));
            var sizeFactor = Mathf.Clamp(Mathf.Sqrt(footprint.x * footprint.y), 0.75f, 1.5f);
            var count = Mathf.Clamp(Mathf.RoundToInt((22f + (footprint.x + footprint.y) * 3f) * amount), 4, 96);
            for (var i = 0; i < count; i++)
            {
                Vector3 outward;
                Vector3 point;
                if (origins != null && origins.Count > 0)
                {
                    // Spread a single burst between the six column feet.
                    var angle = Range(random, 0f, Mathf.PI * 2f);
                    outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    point = origins[i % origins.Count];
                }
                else
                {
                    var side = i % 4;
                    outward = side == 0 ? Vector3.left : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.forward;
                    point = bounds.center;
                    point.y = bounds.min.y;
                    if (side < 2)
                    {
                        point.x += outward.x * bounds.extents.x;
                        point.z += Range(random, -0.95f, 0.95f) * bounds.extents.z;
                    }
                    else
                    {
                        point.x += Range(random, -0.95f, 0.95f) * bounds.extents.x;
                        point.z += outward.z * bounds.extents.z;
                    }
                }
                var direction = building.TransformDirection(outward).normalized;
                var position = building.TransformPoint(point) + worldOffset + Vector3.up * 0.04f + direction * 0.025f;
                var particleSize = size * sizeFactor * Range(random, 0.8f, 1.35f);
                var tint = (float)random.NextDouble() < .28f
                    ? Color.Lerp(new Color(.78f, .72f, .90f), new Color(.88f, .83f, .95f), Range(random, 0f, 1f))
                    : Color.Lerp(new Color(.78f, .76f, .80f), new Color(.88f, .86f, .88f), Range(random, 0f, 1f));
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = direction * Range(random, 0.3f, 0.65f) * sizeFactor + Vector3.up * Range(random, 0.1f, 0.28f),
                    startLifetime = Mathf.Max(0.3f, lifetime) * Range(random, 0.85f, 1.15f),
                    startSize3D = new Vector3(particleSize * 1.15f, particleSize * 0.75f, particleSize),
                    rotation3D = new Vector3(Range(random, 0f, 360f), Range(random, 0f, 360f), Range(random, 0f, 360f)),
                    startColor = Color.Lerp(tint, new Color(.98f, .96f, .99f), Mathf.Clamp01(lightening))
                }, 1);
            }
        }

        private static Mesh CreateMesh()
        {
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var points = new[] { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0), new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t), new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
            for (var i = 0; i < points.Length; i++) points[i] = points[i].normalized * 0.5f;
            var indices = new[] { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var vertices = new Vector3[indices.Length];
            var triangles = new int[indices.Length];
            var colors = new Color32[indices.Length];
            for (var i = 0; i < indices.Length; i++)
            {
                vertices[i] = points[indices[i]];
                triangles[i] = i;
                colors[i] = new Color32(255, 255, 255, 255);
            }
            var result = new Mesh { name = "Low poly construction dust", vertices = vertices, triangles = triangles, colors32 = colors };
            result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }

        private static float Range(Random random, float minimum, float maximum) => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    /// <summary>Keeps the original per-apartment dust density when a whole plot shares one model switch.</summary>
    internal sealed class ApartmentConstructionDust : System.IDisposable
    {
        private readonly BuildingModelGeometry[] parts;
        private readonly ParticleSystem[] particles;
        private readonly Mesh[] meshes;
        private readonly Random random = new Random();

        public ApartmentConstructionDust(Transform parent, BuildingModelGeometry geometry, Material material)
        {
            parts = geometry.Parts;
            particles = new ParticleSystem[parts.Length];
            meshes = new Mesh[parts.Length];
            for (int i = 0; i < parts.Length; i++) particles[i] = ConstructionDustPuff.Create(parent, material, out meshes[i]);
        }

        public void Emit(float amount, float size, float lifetime, Vector3 worldOffset, float lightening = 0f)
        {
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null && particles[i] != null)
                    ConstructionDustPuff.Emit(particles[i], parts[i].transform, parts[i].FoundationBounds, amount, size, lifetime, random, worldOffset, lightening: lightening);
        }

        public void Stop()
        {
            foreach (var puff in particles) if (puff != null) puff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Dispose()
        {
            Stop();
            foreach (var puff in particles) if (puff != null) Object.Destroy(puff.gameObject);
            foreach (var mesh in meshes) if (mesh != null) Object.Destroy(mesh);
        }
    }
}
