using System;
using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>A repeatable flame jet rolling around traveling vortices.</summary>
    internal sealed class LowPolyFireStream : IDisposable
    {
        private const int Capacity = 640;
        private readonly Transform root;
        private readonly ParticleSystem gas;
        private readonly FireVisualPreset preset;
        private readonly Transform animatedFlame;
        private readonly MeshFilter animatedFlameMesh;
        private readonly Vector3 animatedFlameBasePoint;
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

        public LowPolyFireStream(Transform parent, Material material, FireVisualPreset preset = null)
        {
            this.preset = preset;
            root = new GameObject("Mouth_Fire_Stream").transform; root.SetParent(parent, false);
            gas = CreateLayer("Rolling_Gas_Wisps", Capacity);
            if (preset != null)
            {
                preset.Configure(gas, false, 1);
                if (preset.BreathAnimationPrefab != null && preset.BreathAnimation != null)
                {
                    animatedFlame = UnityEngine.Object.Instantiate(preset.BreathAnimationPrefab, root).transform;
                    animatedFlame.name = "Authored_Flame_Animation";
                    // Imported Generic clips require an Animator for direct sampling in a player.
                    if (!preset.BreathAnimation.legacy && !animatedFlame.TryGetComponent<Animator>(out _))
                        animatedFlame.gameObject.AddComponent<Animator>();
                    animatedFlame.localScale = Vector3.one * .65f;
                    animatedFlameMesh = animatedFlame.GetComponentInChildren<MeshFilter>(true);
                    var points = animatedFlameMesh.sharedMesh.vertices;
                    animatedFlameBasePoint = points[0];
                    foreach (var point in points)
                        if (point.y < animatedFlameBasePoint.y) animatedFlameBasePoint = point;
                }
            }
            else FireFlow.ConfigureGas(gas, material);
            mouthLight = CreateGlow("Mouth_Glow", 2.3f); impactLight = CreateGlow("Impact_Glow", 4f); Hide();
        }

        private ParticleSystem CreateLayer(string name, int capacity)
        {
            var child = new GameObject(name, typeof(ParticleSystem));
            child.transform.SetParent(root, false);
            var system = child.GetComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true; main.simulationSpeed = 0f; main.maxParticles = capacity; main.startSpeed = 0f;
            main.startSize3D = true; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            return system;
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
            UpdateAnimatedFlame(mouth, forward, distance, time, travelDuration, breathDuration);
            var across = Vector3.Cross(Vector3.up, forward).normalized; var up = Vector3.Cross(forward, across).normalized;
            var spread = distance * Mathf.Tan(halfAngle * Mathf.Deg2Rad); var cursor = previousTime;
            var jetSpeed = distance / travelDuration;
            var downwardAngle = Mathf.Asin(Mathf.Clamp01(-forward.y)) * Mathf.Rad2Deg;
            var coneSlope = Mathf.Tan(Mathf.Clamp(halfAngle, 1f, Mathf.Max(1f, downwardAngle - 5f)) * Mathf.Deg2Rad);
            while (cursor < time - 0.00001f)
            {
                var step = Mathf.Min(1f / 90f, time - cursor); cursor += step;
                if (cursor < breathDuration)
                {
                    remainder += step * (preset != null ? 160f : 310f);
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
                    var velocity = wisp.BaseVelocity + curl * 0.9f + roll + Vector3.up * (progress * 0.7f);
                    // Eddies can curl sideways, but cannot turn the breath back up or behind the mouth.
                    var speed = Mathf.Clamp(Vector3.Dot(velocity, forward), jetSpeed * .8f, jetSpeed * 1.15f);
                    var lateral = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, forward), speed * coneSlope);
                    var radial = Vector3.ProjectOnPlane(offset, forward);
                    if (wisp.Seed % 5u == 0u && radial.sqrMagnitude > .0025f)
                    {
                        var fold = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .7f, progress));
                        lateral = Vector3.Lerp(lateral, -radial.normalized * (speed * coneSlope * .65f), fold);
                    }
                    wisp.Velocity = forward * speed + lateral;
                    wisp.Position += wisp.Velocity * step;
                    if (wisp.Position.y <= target.y) { wisps[i] = wisps[--count]; continue; }
                    wisps[i] = wisp;
                }
            }
            previousTime = time;
            for (var i = 0; i < count; i++)
            {
                var wisp = wisps[i]; var age = wisp.Age / wisp.Lifetime;
                var fade = Mathf.SmoothStep(0f, 1f, age * 14f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, age)));
                var width = wisp.Width * Mathf.Lerp(0.5f, 3f, age) * Mathf.Min(1f, distance / 3f);
                particles[i] = new ParticleSystem.Particle
                {
                    position = wisp.Position, velocity = wisp.Velocity, startLifetime = wisp.Lifetime,
                    remainingLifetime = wisp.Lifetime - wisp.Age, randomSeed = wisp.Seed,
                    startSize3D = new Vector3(width, width * 1.45f, width),
                    rotation = Mathf.Atan2(Vector3.Dot(wisp.Velocity, across), Vector3.Dot(wisp.Velocity, up)) * Mathf.Rad2Deg * 0.3f,
                    startColor = new Color(1f, Mathf.Lerp(0.86f, 0.48f, age), 0.09f, fade * 0.55f)
                };
                if (preset == null) continue;
                // The imported flame points along local Y. Roll each saved sample around its travel direction.
                var direction = wisp.Velocity.normalized;
                particles[i].rotation3D = (Quaternion.FromToRotation(Vector3.up, direction) *
                    Quaternion.Euler(0f, RadicalInverse(wisp.Seed, 11) * 360f, 0f)).eulerAngles;
                var groundDistance = (wisp.Position.y - target.y) / Mathf.Max(.001f, -direction.y);
                particles[i].startSize3D = new Vector3(width * .85f, Mathf.Min(width * 2.4f, groundDistance), width * .85f);
                var groundFade = Mathf.SmoothStep(0f, 1f, (wisp.Position.y - target.y) / .12f);
                particles[i].startColor = new Color(1f, 1f, Mathf.Lerp(1f, .72f, age), fade * groundFade * .9f);
                particles[i].SetMeshIndex(preset.FlameIndex(wisp.Seed));
            }
            gas.SetParticles(particles, count);
            properties.SetFloat("_FireTime", time); gas.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(properties);
            mouthLight.transform.position = mouth + forward * 0.15f;
            mouthLight.intensity = time < breathDuration ? 1.1f + Mathf.Sin(time * 19f) * 0.12f : 0f;
            impactLight.transform.position = Vector3.Lerp(mouth, target, Mathf.Clamp01(time / travelDuration)) + Vector3.up * 0.3f;
            impactLight.intensity = count > 0 ? 1.4f : 0f;
        }

        private void UpdateAnimatedFlame(Vector3 mouth, Vector3 forward, float distance, float time, float travelDuration, float breathDuration)
        {
            if (animatedFlame == null) return;
            var visible = time < breathDuration;
            animatedFlame.gameObject.SetActive(visible);
            if (!visible) return;
            // Trim the source clip's empty lead-in, so the flame starts with the aimed breath.
            var range = preset.BreathAnimationRange;
            var clipTime = Mathf.Lerp(range.x, range.y, Mathf.Clamp01(time / breathDuration));
            preset.BreathAnimation.SampleAnimation(animatedFlame.gameObject, clipTime);
            animatedFlame.rotation = Quaternion.FromToRotation(Vector3.up, forward);
            animatedFlame.localScale = Vector3.one * .65f;
            var bounds = animatedFlameMesh.sharedMesh.bounds;
            var length = animatedFlameMesh.transform.TransformVector(Vector3.up * bounds.size.y).magnitude;
            var reach = distance * Mathf.Clamp01(time / travelDuration);
            if (length > reach) animatedFlame.localScale *= reach / length;
            animatedFlame.position += mouth - animatedFlameMesh.transform.TransformPoint(animatedFlameBasePoint);
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
