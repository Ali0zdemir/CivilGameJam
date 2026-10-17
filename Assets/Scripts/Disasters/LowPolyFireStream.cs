using System;
using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>A conical jet of translucent wisps rolling inward around traveling vortices.</summary>
    internal sealed class LowPolyFireStream : IDisposable
    {
        private const int Capacity = 640;
        private readonly Transform root;
        private readonly ParticleSystem gas;
        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[Capacity];
        private readonly Wisp[] wisps = new Wisp[Capacity];
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Light mouthLight;
        private readonly Light impactLight;
        private int count;
        private uint emitted;
        private float previousTime;
        private float remainder;

        private struct Wisp
        {
            public Vector3 Position, BaseVelocity, Velocity;
            public float Age, Lifetime, Width;
            public uint Seed;
        }

        public LowPolyFireStream(Transform parent, Material material)
        {
            root = new GameObject("Mouth_Fire_Stream").transform; root.SetParent(parent, false);
            var child = new GameObject("Rolling_Gas_Wisps", typeof(ParticleSystem));
            child.transform.SetParent(root, false); gas = child.GetComponent<ParticleSystem>();
            var main = gas.main;
            main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true; main.simulationSpeed = 0f; main.maxParticles = Capacity; main.startSpeed = 0f;
            main.startSize3D = true; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = gas.emission; emission.enabled = false;
            var shape = gas.shape; shape.enabled = false;
            FireFlow.ConfigureGas(gas, material);
            mouthLight = CreateGlow("Mouth_Glow", 2.3f); impactLight = CreateGlow("Impact_Glow", 4f); Hide();
        }

        private Light CreateGlow(string name, float range)
        {
            var child = new GameObject(name, typeof(Light)); child.transform.SetParent(root, false);
            var light = child.GetComponent<Light>(); light.type = LightType.Point;
            light.color = new Color(1f, 0.36f, 0.06f); light.range = range; light.shadows = LightShadows.None;
            return light;
        }

        public void Update(Vector3 mouth, Vector3 target, float time, float travelDuration, float breathDuration, float halfAngle)
        {
            if (time <= 0f || time >= breathDuration + travelDuration) { Hide(); return; }
            if (!root.gameObject.activeSelf) { root.gameObject.SetActive(true); gas.Play(); }
            var distance = Vector3.Distance(mouth, target);
            var forward = (target - mouth).normalized;
            var across = Vector3.Cross(Vector3.up, forward).normalized; var up = Vector3.Cross(forward, across).normalized;
            var spread = distance * Mathf.Tan(halfAngle * Mathf.Deg2Rad); var cursor = previousTime;
            while (cursor < time - 0.00001f)
            {
                var step = Mathf.Min(1f / 90f, time - cursor); cursor += step;
                if (cursor < breathDuration)
                {
                    remainder += step * 310f;
                    while (remainder >= 1f && count < Capacity)
                    {
                        remainder -= 1f; emitted++;
                        var side = RadicalInverse(emitted, 2) * 2f - 1f;
                        var height = (RadicalInverse(emitted, 3) * 2f - 1f) * Mathf.Sqrt(1f - side * side);
                        wisps[count++] = new Wisp
                        {
                            Position = mouth,
                            BaseVelocity = forward * (distance / travelDuration) + across * (spread / travelDuration * side * 0.98f)
                                + up * (spread / travelDuration * height * 0.31f),
                            Lifetime = travelDuration * Mathf.Lerp(0.86f, 1.05f, RadicalInverse(emitted, 5)),
                            Width = Mathf.Lerp(0.22f, 0.38f, RadicalInverse(emitted, 7)), Seed = emitted
                        };
                    }
                }
                for (var i = count - 1; i >= 0; i--)
                {
                    var wisp = wisps[i]; wisp.Age += step;
                    if (wisp.Age >= wisp.Lifetime) { wisps[i] = wisps[--count]; continue; }
                    var offset = wisp.Position - mouth; var progress = wisp.Age / wisp.Lifetime;
                    var curl = FireFlow.Curl(wisp.Position, cursor) * Mathf.SmoothStep(0f, 1f, progress * 4f);
                    var roll = FireFlow.JetRoll(offset, forward, across, up, distance, spread, cursor);
                    wisp.Velocity = wisp.BaseVelocity + curl * 0.9f + roll + Vector3.up * (progress * 0.7f);
                    wisp.Position += wisp.Velocity * step; wisps[i] = wisp;
                }
            }
            previousTime = time;
            for (var i = 0; i < count; i++)
            {
                var wisp = wisps[i]; var age = wisp.Age / wisp.Lifetime;
                var fade = Mathf.SmoothStep(0f, 1f, age * 14f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, age)));
                var width = wisp.Width * Mathf.Lerp(0.5f, 3f, age);
                particles[i] = new ParticleSystem.Particle
                {
                    position = wisp.Position, velocity = wisp.Velocity, startLifetime = wisp.Lifetime,
                    remainingLifetime = wisp.Lifetime - wisp.Age, randomSeed = wisp.Seed,
                    startSize3D = new Vector3(width, width * 1.45f, width),
                    rotation = Mathf.Atan2(Vector3.Dot(wisp.Velocity, across), Vector3.Dot(wisp.Velocity, up)) * Mathf.Rad2Deg * 0.3f,
                    startColor = new Color(1f, Mathf.Lerp(0.86f, 0.48f, age), 0.09f, fade * 0.55f)
                };
            }
            gas.SetParticles(particles, count);
            properties.SetFloat("_FireTime", time); gas.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(properties);
            mouthLight.transform.position = mouth + forward * 0.15f;
            mouthLight.intensity = time < breathDuration ? 1.1f + Mathf.Sin(time * 19f) * 0.12f : 0f;
            impactLight.transform.position = Vector3.Lerp(mouth, target, Mathf.Clamp01(time / travelDuration)) + Vector3.up * 0.3f;
            impactLight.intensity = count > 0 ? 1.4f : 0f;
        }

        private static float RadicalInverse(uint index, uint radix)
        {
            var result = 0f; var fraction = 1f / radix;
            while (index > 0) { result += index % radix * fraction; index /= radix; fraction /= radix; }
            return result;
        }

        public void Hide()
        {
            count = 0; previousTime = remainder = 0f; emitted = 0;
            if (gas != null) gas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (root != null) root.gameObject.SetActive(false);
        }

        public void Dispose() => Hide();
    }
}
