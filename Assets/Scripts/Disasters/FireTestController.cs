using System;
using System.Collections.Generic;
using CivilGameJam.Upgrades;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CivilGameJam.Disasters
{
    /// <summary>A repeatable low poly fire preview that spreads between the scene's buildings.</summary>
    [DisallowMultipleComponent]
    public sealed class FireTestController : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(5f)] private float duration = 10f;
        [SerializeField, Range(0.2f, 2f)] private float ignitionTime = 0.8f;
        [SerializeField, Range(0f, 5f)] private float spreadTime = 3.5f;
        [SerializeField, Range(2.2f, 4f)] private float fadeOutTime = 2.5f;

        [Header("Low poly flames")]
        [SerializeField, Range(0.3f, 2.5f)] private float flameHeight = 1.1f;
        [SerializeField, Range(0.15f, 0.8f)] private float flameWidth = 0.42f;
        [SerializeField, Range(8f, 45f)] private float flameRate = 24f;

        [Header("Smoke and embers")]
        [SerializeField, Range(0f, 2f)] private float smokeAmount = 0.8f;
        [SerializeField, Range(0.3f, 2f)] private float smokeRiseSpeed = 0.9f;
        [SerializeField, Range(0f, 2f)] private float emberAmount = 1f;
        [SerializeField] private Vector2 wind = new Vector2(0.35f, 0.12f);

        [Header("Extinguishing")]
        [SerializeField, Range(0f, 1f)] private float extinguishHissVolume = 0.16f;

        [Header("Scene")]
        [SerializeField] private Transform buildingsRoot;
        [SerializeField] private Material flameMaterial;
        [SerializeField] private Material smokeMaterial;

        [Header("Test panel")]
        [SerializeField] private Button testButton;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Image progressFill;

        private readonly List<BurnSite> sites = new List<BurnSite>();
        private readonly System.Random random = new System.Random();
        private GameObject visuals;
        private ParticleSystem flames;
        private ParticleSystem smoke;
        private ParticleSystem embers;
        private ParticleSystem extinguishSmoke;
        private AudioSource extinguishAudio;
        private AudioClip extinguishHiss;
        private Mesh flameMesh;
        private Mesh smokeMesh;
        private Mesh emberMesh;
        private float elapsed;
        private const float SprinklerExtinguishDuration = 2f;
        private readonly ParticleSystem.Particle[] extinguishBuffer = new ParticleSystem.Particle[700];

        private sealed class BurnSite
        {
            public Transform Building;
            public HouseSprinklerUpgrade Sprinkler;
            public readonly List<Vector3> Anchors = new List<Vector3>();
            public Vector3 Center;
            public float Size;
            public float Delay;
            public float FlameRemainder;
            public float SmokeRemainder;
            public float EmberRemainder;
            public float SprinklerExposure;
            public float SuppressionScale = 1f;
            public bool Extinguished;
            public uint ParticleSeed;
        }

        public bool IsRunning { get; private set; }
        public float TestDuration => Mathf.Max(5f, duration);
        public float NormalizedProgress => IsRunning ? Mathf.Clamp01(elapsed / TestDuration) : 0f;

        private void Awake() => ResetTest(false);

        [ContextMenu("Start fire test (Play Mode)")]
        public void StartFire()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsRunning) return;
            if (buildingsRoot == null || flameMaterial == null || smokeMaterial == null)
            {
                Debug.LogWarning("Assign the buildings root and both fire materials before starting the test.", this);
                return;
            }
            CacheBurnSites();
            if (sites.Count == 0) return;
            EnsureVisuals();
            elapsed = 0f;
            IsRunning = true;
            visuals.SetActive(true);
            flames.Play(true);
            smoke.Play(true);
            embers.Play(true);
            extinguishSmoke.Play(true);
            if (testButton != null) testButton.interactable = false;
            if (progressFill != null) progressFill.fillAmount = 0f;
            if (statusLabel != null) statusLabel.text = "Yangın başlıyor";
        }

        private void Update()
        {
            if (IsRunning) AdvanceFire(Time.unscaledDeltaTime);
        }

        private void AdvanceFire(float deltaTime)
        {
            if (!IsRunning) return;
            var step = Mathf.Min(Mathf.Max(0f, deltaTime), TestDuration - elapsed);
            var previousElapsed = elapsed;
            elapsed += step;
            var releaseStart = TestDuration - Mathf.Clamp(fadeOutTime, 2.2f, TestDuration * 0.45f);
            // Stop feeding the fire before the test ends so the final smoke can drift away.
            var releaseEnd = TestDuration - 1.6f;
            var release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(releaseStart, releaseEnd, elapsed));
            foreach (var site in sites)
            {
                if (site.Extinguished || elapsed <= site.Delay) continue;
                var burnStep = elapsed - Mathf.Max(previousElapsed, site.Delay);
                var protectedHouse = site.Sprinkler != null && site.Sprinkler.IsInstalled && site.Sprinkler.isActiveAndEnabled;
                // Count from this house catching fire, or from installing protection on a burning house.
                site.SprinklerExposure = protectedHouse ? site.SprinklerExposure + burnStep : 0f;
                var suppression = protectedHouse ? 1f - Mathf.SmoothStep(0f, 1f, site.SprinklerExposure / SprinklerExtinguishDuration) : 1f;
                // Shrink live particles as well as new flames, so the final frame has no abrupt cut.
                if (suppression < site.SuppressionScale)
                {
                    var ratio = suppression / Mathf.Max(0.00001f, site.SuppressionScale);
                    ScaleBurningParticles(flames, site.ParticleSeed, ratio);
                    ScaleBurningParticles(embers, site.ParticleSeed, ratio);
                }
                site.SuppressionScale = suppression;
                if (site.SprinklerExposure >= SprinklerExtinguishDuration - 0.00001f)
                {
                    site.Extinguished = true;
                    site.FlameRemainder = site.SmokeRemainder = site.EmberRemainder = 0f;
                    RemoveBurningParticles(flames, site.ParticleSeed);
                    RemoveBurningParticles(embers, site.ParticleSeed);
                    EmitExtinguishSmoke(site);
                    if (extinguishAudio != null && extinguishHissVolume > 0f)
                        extinguishAudio.PlayOneShot(extinguishHiss, extinguishHissVolume);
                    continue;
                }
                var ignitionDuration = protectedHouse ? Mathf.Min(0.12f, ignitionTime) : Mathf.Max(0.2f, ignitionTime);
                var ignition = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(site.Delay, site.Delay + ignitionDuration, elapsed));
                var strength = ignition * release * suppression;
                EmitFlames(site, burnStep, strength);
                EmitSmoke(site, burnStep, strength);
                EmitEmbers(site, burnStep, strength);
            }
            if (progressFill != null) progressFill.fillAmount = elapsed / TestDuration;
            if (statusLabel != null)
                statusLabel.text = elapsed >= releaseStart ? "Yangın sönüyor" : elapsed < Mathf.Min(spreadTime, releaseStart * 0.7f) + ignitionTime ? "Yangın yayılıyor" : "Yangın güçlendi";
            if (elapsed >= TestDuration) ResetTest(true);
        }

        private void CacheBurnSites()
        {
            sites.Clear();
            var ignitionCenter = Vector3.zero;
            var foundFactory = false;
            var sprinklers = FindObjectsByType<HouseSprinklerUpgrade>(FindObjectsSortMode.None);
            foreach (Transform building in buildingsRoot)
            {
                if (!building.gameObject.activeInHierarchy) continue;
                var renderers = Array.FindAll(building.GetComponentsInChildren<MeshRenderer>(), renderer => !IsSprinklerPart(renderer.transform, building));
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var site = new BurnSite
                {
                    Building = building,
                    ParticleSeed = (uint)sites.Count + 1u,
                    Center = bounds.center,
                    Size = Mathf.Clamp(Mathf.Sqrt(bounds.size.x * bounds.size.z), 0.65f, 1.5f)
                };
                foreach (var sprinkler in sprinklers) if (sprinkler.Building == building) { site.Sprinkler = sprinkler; break; }
                foreach (var renderer in renderers)
                {
                    var label = renderer.name;
                    if (label.IndexOf("roof", StringComparison.OrdinalIgnoreCase) < 0 || label.IndexOf("vent", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    var roof = renderer.bounds;
                    if (roof.size.x < 0.2f || roof.size.z < 0.2f) continue;
                    for (var x = -1; x <= 1; x += 2)
                    for (var z = -1; z <= 1; z += 2)
                        site.Anchors.Add(new Vector3(roof.center.x + roof.size.x * 0.28f * x, roof.center.y - 0.02f, roof.center.z + roof.size.z * 0.24f * z));
                }
                if (site.Anchors.Count == 0) site.Anchors.Add(new Vector3(bounds.center.x, bounds.max.y - 0.08f, bounds.center.z));
                // A few flames on the visible facade connect roof flames to the building itself.
                for (var x = -1; x <= 1; x += 2)
                    site.Anchors.Add(new Vector3(bounds.center.x + bounds.size.x * 0.26f * x, bounds.min.y + bounds.size.y * 0.28f, bounds.min.z - 0.025f));
                sites.Add(site);
                if (building.name.IndexOf("factory", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ignitionCenter = bounds.center;
                    foundFactory = true;
                }
            }
            if (sites.Count == 0) return;
            if (!foundFactory) ignitionCenter = sites[0].Center;
            var maximumDistance = 0.01f;
            foreach (var site in sites) maximumDistance = Mathf.Max(maximumDistance, Vector2.Distance(new Vector2(site.Center.x, site.Center.z), new Vector2(ignitionCenter.x, ignitionCenter.z)));
            var maximumDelay = Mathf.Min(Mathf.Max(0f, spreadTime), TestDuration * 0.45f);
            foreach (var site in sites)
                site.Delay = Vector2.Distance(new Vector2(site.Center.x, site.Center.z), new Vector2(ignitionCenter.x, ignitionCenter.z)) / maximumDistance * maximumDelay;
        }

        private static bool IsSprinklerPart(Transform part, Transform building)
        {
            for (var parent = part; parent != null && parent != building; parent = parent.parent)
                if (parent.name == "Roof_Sprinkler") return true;
            return false;
        }

        private void EnsureVisuals()
        {
            if (visuals != null) return;
            visuals = new GameObject("Low_Poly_Fire");
            visuals.transform.SetParent(transform, false);
            visuals.SetActive(false);
            flameMesh = CreateFlameMesh();
            smokeMesh = CreateSmokeMesh();
            emberMesh = CreateEmberMesh();
            flames = CreateParticles("Flames", flameMesh, flameMaterial, 700, 0f,
                new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.18f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0f)));
            smoke = CreateParticles("Smoke", smokeMesh, smokeMaterial, 180, 0f,
                new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.2f, 0.75f), new Keyframe(0.65f, 1.2f), new Keyframe(1f, 0f)));
            embers = CreateParticles("Embers", emberMesh, flameMaterial, 220, 0.08f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.55f, 0.8f), new Keyframe(1f, 0f)));
            extinguishSmoke = CreateParticles("Extinguish_Smoke", smokeMesh, smokeMaterial, 96, 0f,
                new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(0.1f, 0.85f), new Keyframe(0.25f, 1f), new Keyframe(0.55f, 0.8f), new Keyframe(1f, 0f)));
            extinguishHiss = CreateExtinguishHiss();
            extinguishAudio = visuals.AddComponent<AudioSource>();
            extinguishAudio.playOnAwake = false;
            extinguishAudio.spatialBlend = 0f;
        }

        private ParticleSystem CreateParticles(string label, Mesh particleMesh, Material material, int maximum, float gravity, AnimationCurve sizeCurve)
        {
            var child = new GameObject(label);
            child.transform.SetParent(visuals.transform, false);
            var system = child.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.maxParticles = maximum;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startRotation3D = true;
            main.gravityModifier = gravity;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.mesh = particleMesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enableGPUInstancing = false;
            return system;
        }

        private void EmitFlames(BurnSite site, float step, float strength)
        {
            var protection = site.Sprinkler != null ? site.Sprinkler.FireMultiplier : 1f;
            site.FlameRemainder += step * Mathf.Max(0f, flameRate) * strength;
            var count = Mathf.Min(80, Mathf.FloorToInt(site.FlameRemainder));
            site.FlameRemainder -= count;
            for (var i = 0; i < count; i++)
            {
                var width = flameWidth * site.Size * RandomRange(0.65f, 1.2f) * site.SuppressionScale;
                flames.Emit(new ParticleSystem.EmitParams
                {
                    position = RandomAnchor(site) + new Vector3(RandomRange(-0.09f, 0.09f), -0.03f, RandomRange(-0.09f, 0.09f)),
                    velocity = new Vector3(wind.x * 0.15f, RandomRange(0.1f, 0.35f), wind.y * 0.15f),
                    startLifetime = RandomRange(0.45f, 0.85f),
                    randomSeed = site.ParticleSeed,
                    startSize3D = new Vector3(width, flameHeight * site.Size * RandomRange(0.65f, 1.25f) * Mathf.Lerp(0.35f, 1f, protection) * site.SuppressionScale, width),
                    rotation3D = new Vector3(RandomRange(-10f, 10f), RandomRange(0f, 360f), RandomRange(-12f, 12f)),
                    startColor = Color.Lerp(new Color(1f, 0.26f, 0.015f), new Color(1f, 0.88f, 0.12f), RandomRange(0f, 1f))
                }, 1);
            }
        }

        private void EmitSmoke(BurnSite site, float step, float strength)
        {
            site.SmokeRemainder += step * 3.5f * Mathf.Max(0f, smokeAmount) * strength;
            var count = Mathf.Min(20, Mathf.FloorToInt(site.SmokeRemainder));
            site.SmokeRemainder -= count;
            for (var i = 0; i < count; i++)
            {
                var size = site.Size * RandomRange(0.4f, 0.65f);
                var shade = RandomRange(0.2f, 0.38f);
                smoke.Emit(new ParticleSystem.EmitParams
                {
                    position = RandomAnchor(site) + Vector3.up * flameHeight * site.Size * 0.6f,
                    velocity = new Vector3(wind.x + RandomRange(-0.12f, 0.12f), smokeRiseSpeed * RandomRange(0.8f, 1.2f), wind.y + RandomRange(-0.1f, 0.1f)),
                    startLifetime = RandomRange(1.4f, 2.1f),
                    startSize3D = new Vector3(size, size, size),
                    rotation3D = new Vector3(RandomRange(0f, 360f), RandomRange(0f, 360f), RandomRange(0f, 360f)),
                    startColor = new Color(shade, shade * 1.03f, shade * 1.06f)
                }, 1);
            }
        }

        private void EmitEmbers(BurnSite site, float step, float strength)
        {
            site.EmberRemainder += step * 5f * Mathf.Max(0f, emberAmount) * strength;
            var count = Mathf.Min(30, Mathf.FloorToInt(site.EmberRemainder));
            site.EmberRemainder -= count;
            for (var i = 0; i < count; i++)
            {
                var size = RandomRange(0.035f, 0.065f) * site.SuppressionScale;
                embers.Emit(new ParticleSystem.EmitParams
                {
                    position = RandomAnchor(site) + Vector3.up * flameHeight * site.Size * 0.55f,
                    velocity = new Vector3(wind.x + RandomRange(-0.35f, 0.35f), RandomRange(1f, 2f), wind.y + RandomRange(-0.35f, 0.35f)),
                    startLifetime = RandomRange(0.5f, 1.1f),
                    randomSeed = site.ParticleSeed,
                    startSize3D = new Vector3(size, size * 1.8f, size),
                    startColor = new Color(1f, RandomRange(0.3f, 0.75f), 0.025f)
                }, 1);
            }
        }

        private Vector3 RandomAnchor(BurnSite site) => site.Anchors[random.Next(site.Anchors.Count)];
        private float RandomRange(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());

        private void EmitExtinguishSmoke(BurnSite site)
        {
            for (var i = 0; i < 12; i++)
            {
                var angle = RandomRange(0f, Mathf.PI * 2f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var size = site.Size * RandomRange(0.22f, 0.36f);
                var shade = RandomRange(0.025f, 0.07f);
                extinguishSmoke.Emit(new ParticleSystem.EmitParams
                {
                    position = site.Anchors[i % site.Anchors.Count] + outward * RandomRange(0.02f, 0.09f) + Vector3.up * 0.025f,
                    velocity = outward * RandomRange(0.15f, 0.35f) + new Vector3(wind.x * 0.15f, RandomRange(0.6f, 1f), wind.y * 0.15f),
                    startLifetime = RandomRange(0.45f, 0.7f),
                    startSize3D = new Vector3(size, size * RandomRange(0.75f, 1.1f), size),
                    rotation3D = new Vector3(RandomRange(0f, 360f), RandomRange(0f, 360f), RandomRange(0f, 360f)),
                    startColor = new Color(shade, shade, shade),
                    randomSeed = site.ParticleSeed
                }, 1);
            }
        }

        private void ScaleBurningParticles(ParticleSystem system, uint siteSeed, float ratio)
        {
            var count = system.GetParticles(extinguishBuffer);
            for (var i = 0; i < count; i++)
                if (extinguishBuffer[i].randomSeed == siteSeed) extinguishBuffer[i].startSize3D *= ratio;
            system.SetParticles(extinguishBuffer, count);
        }

        private static AudioClip CreateExtinguishHiss()
        {
            const int sampleRate = 22050;
            const float duration = 0.18f;
            var samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            var noise = new System.Random(7351);
            var lowPass = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var time = (float)i / sampleRate;
                var value = (float)noise.NextDouble() * 2f - 1f;
                lowPass = Mathf.Lerp(lowPass, value, 0.13f);
                var envelope = Mathf.Clamp01(time / 0.012f) * Mathf.Pow(1f - time / duration, 1.8f);
                samples[i] = (value - lowPass) * envelope * 0.35f;
            }
            var clip = AudioClip.Create("Short extinguishing hiss", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void RemoveBurningParticles(ParticleSystem system, uint siteSeed)
        {
            var count = system.GetParticles(extinguishBuffer);
            var kept = 0;
            for (var i = 0; i < count; i++)
                if (extinguishBuffer[i].randomSeed != siteSeed) extinguishBuffer[kept++] = extinguishBuffer[i];
            system.SetParticles(extinguishBuffer, kept);
        }

        private static Mesh CreateFlameMesh()
        {
            const int sides = 5;
            var points = new Vector3[sides * 3 + 1];
            for (var ring = 0; ring < 3; ring++)
            for (var side = 0; side < sides; side++)
            {
                var angle = side * Mathf.PI * 2f / sides;
                var radius = ring == 0 ? 0.25f : ring == 1 ? 0.5f : 0.22f;
                var height = ring == 0 ? 0f : ring == 1 ? 0.32f : 0.72f;
                points[ring * sides + side] = new Vector3(Mathf.Cos(angle) * radius + ring * 0.04f, height, Mathf.Sin(angle) * radius);
            }
            points[points.Length - 1] = new Vector3(0.22f, 1f, 0.04f);
            var indices = new List<int>();
            for (var ring = 0; ring < 2; ring++)
            for (var side = 0; side < sides; side++)
            {
                var a = ring * sides + side;
                var b = ring * sides + (side + 1) % sides;
                indices.Add(a); indices.Add(a + sides); indices.Add(b);
                indices.Add(b); indices.Add(a + sides); indices.Add(b + sides);
            }
            for (var side = 0; side < sides; side++)
            {
                indices.Add(2 * sides + side); indices.Add(points.Length - 1); indices.Add(2 * sides + (side + 1) % sides);
            }
            return FlatMesh("Low poly flame", points, indices.ToArray(), true);
        }

        private static Mesh CreateSmokeMesh()
        {
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var points = new[] { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0), new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t), new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
            for (var i = 0; i < points.Length; i++) points[i] = points[i].normalized * 0.5f;
            return FlatMesh("Low poly smoke puff", points, new[] { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 }, false);
        }

        private static Mesh CreateEmberMesh() => FlatMesh("Low poly ember",
            new[] { Vector3.up * 0.5f, Vector3.down * 0.5f, Vector3.left * 0.5f, Vector3.right * 0.5f, Vector3.forward * 0.5f, Vector3.back * 0.5f },
            new[] { 0,4,3, 0,2,4, 0,5,2, 0,3,5, 1,3,4, 1,4,2, 1,2,5, 1,5,3 }, false);

        private static Mesh FlatMesh(string label, Vector3[] points, int[] indices, bool flameColors)
        {
            var vertices = new Vector3[indices.Length];
            var triangles = new int[indices.Length];
            // Mesh particles consume packed colours; use bytes rather than float colour attributes.
            var colors = new Color32[indices.Length];
            for (var i = 0; i < indices.Length; i += 3)
            {
                var height = (points[indices[i]].y + points[indices[i + 1]].y + points[indices[i + 2]].y) / 3f;
                var color = flameColors ? Color.Lerp(Color.white, new Color(1f, 0.46f, 0.12f), Mathf.Clamp01(height)) : Color.white;
                for (var corner = 0; corner < 3; corner++)
                {
                    vertices[i + corner] = points[indices[i + corner]];
                    triangles[i + corner] = i + corner;
                    colors[i + corner] = color;
                }
            }
            var result = new Mesh { name = label, vertices = vertices, triangles = triangles, colors32 = colors };
            result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }

        [ContextMenu("Stop fire test")]
        public void StopFire()
        {
            if (Application.isPlaying) ResetTest(false);
        }

        private void OnDisable() => ResetTest(false);

        private void ResetTest(bool completed)
        {
            IsRunning = false;
            if (flames != null) flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (extinguishSmoke != null) extinguishSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (extinguishAudio != null) extinguishAudio.Stop();
            if (visuals != null) visuals.SetActive(false);
            if (testButton != null) testButton.interactable = true;
            if (progressFill != null) progressFill.fillAmount = 0f;
            if (statusLabel != null) statusLabel.text = completed ? "Tamamlandı · tekrar deneyebilirsin" : "Yangın testi hazır";
        }

        private void OnDestroy()
        {
            if (flameMesh != null) Destroy(flameMesh);
            if (smokeMesh != null) Destroy(smokeMesh);
            if (emberMesh != null) Destroy(emberMesh);
            if (extinguishHiss != null) Destroy(extinguishHiss);
        }
    }
}
