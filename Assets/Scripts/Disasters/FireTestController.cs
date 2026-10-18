using TMPro;
using CivilGameJam.Presentation;
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

        [Header("Spreading breath and street fire")]
        [SerializeField, Range(10f, 45f)] private float breathHalfAngle = 28f;
        [SerializeField, Range(0.8f, 6f)] private float softFireLeadTime = 4.2f;
        [SerializeField, Range(0f, 1f)] private float streetFlameDensity = .16f;

        [Header("Low poly flames")]
        [SerializeField, Range(0.3f, 2.5f)] private float flameHeight = 1.1f;
        [SerializeField, Range(0.15f, 0.8f)] private float flameWidth = 0.42f;
        [SerializeField, Range(8f, 45f)] private float flameRate = 24f;

        [Header("Smoke and embers")]
        [SerializeField, Range(0f, 2f)] private float smokeAmount = 0.22f;
        [SerializeField, Range(0.3f, 2f)] private float smokeRiseSpeed = 0.9f;
        [SerializeField, Range(0f, 2f)] private float emberAmount = 1f;
        [SerializeField] private Vector2 wind = new Vector2(0.35f, 0.12f);

        [Header("Extinguishing")]
        [SerializeField, Range(0f, 1f)] private float extinguishHissVolume = 0.16f;

        [Header("Burnt buildings")]
        [Tooltip("Fallback tint for houses without an imported burnt appearance.")]
        [SerializeField] private Color burntBuildingColor = new Color(.32f, .38f, .43f, 1f);
        [SerializeField, Min(.1f)] private float burnColorDuration = 1.5f;

        [Header("Scene")]
        [SerializeField] private SeaMonsterFireBreath seaMonsterFire;
        [SerializeField] private EarthquakeTestController earthquake;
        [SerializeField] private TsunamiTestController tsunami;
        [SerializeField] private CityCameraController navigation;
        [SerializeField] private Transform buildingsRoot;
        [SerializeField] private Material flameMaterial;
        [SerializeField] private Material smokeMaterial;
        [SerializeField] private Material softFireMaterial;
        [SerializeField] private Material gasFireMaterial;
        [SerializeField] private FireVisualPreset visualPreset;

        [Header("Test panel")]
        [SerializeField] private Button testButton;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private ProgressBarFeedback progressFeedback;

        private readonly List<BurnSite> sites = new List<BurnSite>();
        private System.Random random = new System.Random(7351);
        private uint visualSample;
        private GameObject visuals;
        private ParticleSystem flames;
        private ParticleSystem smoke;
        private ParticleSystem embers;
        private ParticleSystem extinguishSmoke;
        private ParticleSystem softFlames;
        private AudioSource extinguishAudio;
        private AudioClip extinguishHiss;
        private MaterialPropertyBlock gasProperties;
        private Mesh smokeMesh;
        private Mesh emberMesh;
        private LowPolyFireStream breathStream;
        private StreetFireSpread streetFire;
        private float[] streetSuppression;
        private Vector3 ignitionPoint;
        private Vector3 lastBreathOrigin;
        private bool monsterDepartureStarted;
        private float elapsed;
        private const float SprinklerExtinguishDuration = 2f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
        private readonly ParticleSystem.Particle[] extinguishBuffer = new ParticleSystem.Particle[1600];

        private sealed class BurnSite
        {
            public Transform Building;
            public HouseSprinklerUpgrade Sprinkler;
            public readonly List<Vector3> Anchors = new List<Vector3>();
            public Vector3 Center;
            public Bounds Bounds;
            public Bounds Foundation;
            public float Size;
            public float Delay;
            public float SoftDelay;
            public float FlameRemainder;
            public float SmokeRemainder;
            public float EmberRemainder;
            public float SprinklerExposure;
            public float SuppressionScale = 1f;
            public bool Extinguished;
            public uint ParticleSeed;
            public float BurnExposure;
            public ApartmentBurntAppearance BurntAppearance;
            public readonly List<BurnSurface> Surfaces = new List<BurnSurface>();
        }

        private sealed class BurnSurface
        {
            public MeshRenderer Renderer;
            public int MaterialIndex;
            public int ColorProperty;
            public Color OriginalColor;
            public MaterialPropertyBlock OriginalProperties;
            public MaterialPropertyBlock Properties;
        }

        public bool IsRunning { get; private set; }
        public float CityIgnitionTime => seaMonsterFire != null ? seaMonsterFire.CityIgnitionTime : 0f;
        public float BuildingIgnitionTime => CityIgnitionTime + Mathf.Max(0.8f, softFireLeadTime);
        public float SprinklerStartTime => CityIgnitionTime + .35f;
        public bool SprinklersActive => IsRunning && elapsed >= SprinklerStartTime;
        public float TestDuration => BuildingIgnitionTime + Mathf.Max(5f, duration);
        public int ActiveStreetFireCells => IsRunning && streetFire != null ? streetFire.ActiveCellCount : 0;
        public float NormalizedProgress => IsRunning ? Mathf.Clamp01(elapsed / TestDuration) : 0f;
        public int IgnitedBuildingCount
        {
            get
            {
                if (!IsRunning) return 0;
                var count = 0;
                foreach (var site in sites) if (elapsed > site.Delay && !site.Extinguished) count++;
                return count;
            }
        }

        private void Awake()
        {
            if (progressFeedback == null && progressFill != null) progressFeedback = progressFill.GetComponentInParent<ProgressBarFeedback>(true);
            ResetTest(false);
        }

        [ContextMenu("Start fire test (Play Mode)")]
        public void StartFire()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsRunning) return;
            if (buildingsRoot == null || flameMaterial == null || smokeMaterial == null ||
                seaMonsterFire != null && (softFireMaterial == null || gasFireMaterial == null))
            {
                Debug.LogWarning("Assign the buildings root and the fire, smoke, street and gas materials before starting the test.", this);
                return;
            }
            CityLayoutEvents.PrepareForDisaster();
            if (tsunami != null && tsunami.IsRunning) tsunami.StopTsunami();
            if (earthquake != null) earthquake.FinishForFire();
            random = new System.Random((int)(visualPreset != null ? visualPreset.SequenceSeed : 7351u));
            visualSample = 0;
            CacheBurnSites();
            if (sites.Count == 0) return;
            if (seaMonsterFire != null) seaMonsterFire.BeginFire();
            EnsureVisuals();
            CacheStreetSpread();
            elapsed = 0f;
            monsterDepartureStarted = false;
            IsRunning = true;
            if (progressFeedback != null) progressFeedback.Begin();
            if (navigation != null) navigation.AcquireEventInputLock(this);
            visuals.SetActive(true);
            flames.Play(true);
            smoke.Play(true);
            embers.Play(true);
            extinguishSmoke.Play(true);
            softFlames.Play(true);
            if (testButton != null) testButton.interactable = false;
            if (progressFeedback == null && progressFill != null) progressFill.fillAmount = 0f;
            if (statusLabel != null) statusLabel.text = seaMonsterFire != null ? "Monster heating up" : "Fire started";
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
            if (seaMonsterFire != null)
            {
                var departNow = !monsterDepartureStarted && elapsed >= seaMonsterFire.BreathEndTime;
                if (departNow)
                {
                    seaMonsterFire.SetFireTime(seaMonsterFire.BreathEndTime);
                    if (seaMonsterFire.Mouth != null) lastBreathOrigin = seaMonsterFire.Mouth.position;
                }
                seaMonsterFire.SetFireTime(elapsed);
                if (seaMonsterFire.Mouth != null)
                    breathStream.Update(monsterDepartureStarted || departNow ? lastBreathOrigin : seaMonsterFire.Mouth.position, ignitionPoint,
                        elapsed - seaMonsterFire.FlameStartTime, seaMonsterFire.TravelDuration, seaMonsterFire.BreathDuration, breathHalfAngle);
                if (departNow)
                {
                    monsterDepartureStarted = true;
                    if (seaMonsterFire.Walk != null) seaMonsterFire.Walk.BeginAfterEventWalk();
                }
            }
            var releaseStart = TestDuration - Mathf.Clamp(fadeOutTime, 2.2f, TestDuration * 0.45f);
            // Stop feeding the fire before the test ends so the final smoke can drift away.
            var releaseEnd = TestDuration - 1.6f;
            var release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(releaseStart, releaseEnd, elapsed));
            foreach (var site in sites)
            {
                if (site.Extinguished || elapsed <= site.SoftDelay) continue;
                var exposureStep = Mathf.Max(0f, elapsed - Mathf.Max(previousElapsed, Mathf.Max(site.SoftDelay, SprinklerStartTime)));
                var protectedHouse = SprinklersActive && site.Sprinkler != null && site.Sprinkler.IsOperational;
                // Count from this house catching fire, or from installing protection on a burning house.
                site.SprinklerExposure = protectedHouse ? site.SprinklerExposure + exposureStep : 0f;
                var suppression = protectedHouse ? 1f - Mathf.SmoothStep(0f, 1f, site.SprinklerExposure / SprinklerExtinguishDuration) : 1f;
                // Shrink live particles as well as new flames, so the final frame has no abrupt cut.
                if (suppression < site.SuppressionScale)
                {
                    var ratio = suppression / Mathf.Max(0.00001f, site.SuppressionScale);
                    ScaleBurningParticles(flames, site.ParticleSeed, ratio);
                    ScaleBurningParticles(embers, site.ParticleSeed, ratio);
                    ScaleBurningParticles(softFlames, site.ParticleSeed, ratio);
                }
                site.SuppressionScale = suppression;
                if (site.SprinklerExposure >= SprinklerExtinguishDuration - 0.00001f)
                {
                    site.Extinguished = true;
                    site.FlameRemainder = site.SmokeRemainder = site.EmberRemainder = 0f;
                    RemoveBurningParticles(flames, site.ParticleSeed);
                    RemoveBurningParticles(embers, site.ParticleSeed);
                    RemoveBurningParticles(softFlames, site.ParticleSeed);
                    EmitExtinguishSmoke(site);
                    if (extinguishAudio != null && extinguishHissVolume > 0f)
                        extinguishAudio.PlayOneShot(extinguishHiss, extinguishHissVolume);
                    continue;
                }
                if (elapsed <= site.Delay) continue;
                var burnStep = elapsed - Mathf.Max(previousElapsed, site.Delay);
                var ignitionDuration = protectedHouse ? Mathf.Min(0.12f, ignitionTime) : Mathf.Max(0.2f, ignitionTime);
                var ignition = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(site.Delay, site.Delay + ignitionDuration, elapsed));
                var strength = ignition * release * suppression;
                site.BurnExposure += burnStep * strength;
                ApplyBurnColor(site);
                EmitFlames(site, burnStep, strength);
                EmitSmoke(site, burnStep, strength);
                EmitEmbers(site, burnStep, strength);
            }
            for (var i = 0; i < sites.Count; i++) streetSuppression[i] = sites[i].SuppressionScale;
            streetFire.Update(elapsed, release, streetSuppression, streetFlameDensity);
            EmitStreetFlames(step);
            AdvectGas(flames, step, 0.65f);
            AdvectGas(softFlames, step, 0.35f);
            if (progressFeedback != null) progressFeedback.SetProgress(elapsed / TestDuration, true);
            else if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(elapsed / TestDuration);
            if (statusLabel != null)
                statusLabel.text = seaMonsterFire != null && elapsed < seaMonsterFire.ChargeDuration ? "Monster heating up"
                    : seaMonsterFire != null && elapsed < seaMonsterFire.FlameStartTime ? "Monster taking aim"
                    : elapsed < CityIgnitionTime ? "Flame approaching city"
                    : elapsed < BuildingIgnitionTime ? "Fire flowing through streets"
                    : elapsed >= releaseStart ? "Fire fading"
                    : elapsed < BuildingIgnitionTime + spreadTime + ignitionTime ? "Fire spreading" : "Fire intensified";
            if (elapsed >= TestDuration) ResetTest(true);
        }

        private void CacheBurnSites()
        {
            RestoreBuildingColors();
            sites.Clear();
            var sprinklers = FindObjectsByType<HouseSprinklerUpgrade>(FindObjectsSortMode.None);
            foreach (Transform building in buildingsRoot)
            {
                if (!building.gameObject.activeInHierarchy) continue;
                if (building.TryGetComponent<ApartmentTsunamiCollapse>(out var collapse) && collapse.HasCollapsed) continue;
                building.TryGetComponent<BuildingModelGeometry>(out var model);
                if (model != null) AppendModelSites(model, sprinklers);
                else AddBurnSite(building, null, sprinklers);
            }
            if (sites.Count == 0) return;
            var city = sites[0].Foundation;
            foreach (var site in sites) city.Encapsulate(site.Foundation);
            ignitionPoint = new Vector3(city.center.x, city.min.y + 0.3f, Mathf.Lerp(city.min.z, city.max.z, 0.3f));
        }

        private void CacheStreetSpread()
        {
            var foundations = new Bounds[sites.Count]; streetSuppression = new float[sites.Count];
            for (var i = 0; i < sites.Count; i++) { foundations[i] = sites[i].Foundation; streetSuppression[i] = 1f; }
            var ground = WorldGroundBounds();
            if (seaMonsterFire != null) ignitionPoint = seaMonsterFire.GroundImpact(ground.max.y + 0.025f);
            var mouth = seaMonsterFire != null && seaMonsterFire.Mouth != null ? seaMonsterFire.AimedMouthPosition
                : ignitionPoint - Vector3.forward * 5f;
            streetFire.Rebuild(foundations, ground, mouth, ignitionPoint, breathHalfAngle, CityIgnitionTime,
                Mathf.Min(Mathf.Max(0f, spreadTime), Mathf.Max(5f, duration) * 0.45f));
            for (var i = 0; i < sites.Count; i++)
            {
                sites[i].SoftDelay = streetFire.ArrivalTimes[i];
                sites[i].Delay = sites[i].SoftDelay + Mathf.Max(0.8f, softFireLeadTime);
            }
        }

        private Bounds WorldGroundBounds()
        {
            var ground = sites[0].Foundation;
            foreach (var site in sites) ground.Encapsulate(site.Foundation);
            ground.Expand(new Vector3(0.7f, 0f, 0.65f));
            if (tsunami != null)
            {
                var city = tsunami.CityBounds;
                ground = new Bounds(tsunami.transform.TransformPoint(city.center), Vector3.zero);
                for (var corner = 0; corner < 8; corner++)
                    ground.Encapsulate(tsunami.transform.TransformPoint(city.center + Vector3.Scale(city.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
            }
            return ground;
        }

        private void AppendModelSites(BuildingModelGeometry model, HouseSprinklerUpgrade[] sprinklers)
        {
            if (model.IsCollection)
            {
                foreach (var part in model.Parts) if (part.gameObject.activeInHierarchy) AppendModelSites(part, sprinklers);
            }
            else AddBurnSite(model.transform, model, sprinklers);
        }

        private void AddBurnSite(Transform building, BuildingModelGeometry model, HouseSprinklerUpgrade[] sprinklers)
        {
            var renderers = Array.FindAll(building.GetComponentsInChildren<MeshRenderer>(), renderer => !IsSprinklerPart(renderer.transform, building));
            if (model != null) { var surfaces = new List<MeshRenderer>(); model.AppendRenderers(surfaces); renderers = surfaces.ToArray(); }
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var site = new BurnSite
            {
                Building = building,
                ParticleSeed = (uint)sites.Count + 1u,
                Center = bounds.center,
                Bounds = bounds,
                Foundation = model != null ? model.WorldFoundationBounds : bounds,
                Size = Mathf.Clamp(Mathf.Sqrt(bounds.size.x * bounds.size.z), 0.5f, 1.3f),
                BurntAppearance = building.GetComponent<ApartmentBurntAppearance>()
            };
            if (model != null ? model.IsResidential : building.Find("Second_Floor") != null)
            {
                var burnRenderers = new List<MeshRenderer>();
                if (model != null) model.AppendRenderers(burnRenderers, true);
                else burnRenderers.AddRange(renderers);
                CacheBurnSurfaces(site, burnRenderers);
            }
            foreach (var sprinkler in sprinklers) if (sprinkler.Building == building) { site.Sprinkler = sprinkler; break; }
            if (model != null) AppendRoofAnchors(site, model);
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
            // Rounded flame clusters grow up the facade as well as over each individual roof.
            for (var x = -1; x <= 1; x += 2)
            {
                site.Anchors.Add(new Vector3(bounds.center.x + bounds.size.x * 0.26f * x, bounds.min.y + bounds.size.y * 0.28f, bounds.min.z - 0.025f));
                site.Anchors.Add(new Vector3(bounds.center.x + bounds.size.x * 0.32f * x, bounds.min.y + 0.03f, bounds.min.z - 0.04f));
                site.Anchors.Add(new Vector3(bounds.min.x - 0.025f, bounds.min.y + bounds.size.y * 0.55f,
                    bounds.center.z + bounds.size.z * 0.25f * x));
            }
            sites.Add(site);
        }

        private static void CacheBurnSurfaces(BurnSite site, List<MeshRenderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;
                    var colorProperty = material.HasProperty(BaseColor) ? BaseColor : LegacyColor;
                    if (!material.HasProperty(colorProperty)) continue;
                    var original = new MaterialPropertyBlock();
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, i);
                    renderer.GetPropertyBlock(properties, i);
                    if (properties.isEmpty) renderer.GetPropertyBlock(properties);
                    site.Surfaces.Add(new BurnSurface
                    {
                        Renderer = renderer, MaterialIndex = i, ColorProperty = colorProperty,
                        OriginalColor = properties.HasColor(colorProperty) ? properties.GetColor(colorProperty) : material.GetColor(colorProperty),
                        OriginalProperties = original, Properties = properties
                    });
                }
            }
        }

        private void ApplyBurnColor(BurnSite site)
        {
            var amount = Mathf.SmoothStep(0f, 1f, site.BurnExposure / Mathf.Max(.1f, burnColorDuration));
            if (amount <= 0f || site.Surfaces.Count == 0 || site.BurntAppearance != null && site.BurntAppearance.IsBurnt) return;
            if (amount >= .9999f && site.BurntAppearance != null)
            {
                site.BurntAppearance.SetBurnt(true);
                return;
            }
            foreach (var surface in site.Surfaces)
            {
                if (surface.Renderer == null) continue;
                var target = site.BurntAppearance != null
                    ? site.BurntAppearance.TargetColor(surface.Renderer, surface.MaterialIndex, burntBuildingColor) : burntBuildingColor;
                target.a = surface.OriginalColor.a;
                surface.Properties.SetColor(surface.ColorProperty, Color.Lerp(surface.OriginalColor, target, amount));
                surface.Renderer.SetPropertyBlock(surface.Properties, surface.MaterialIndex);
            }
        }

        private void RestoreBuildingColors()
        {
            foreach (var site in sites)
            {
                if (site.BurntAppearance != null) site.BurntAppearance.SetBurnt(false);
                site.BurnExposure = 0f;
                foreach (var surface in site.Surfaces)
                    if (surface.Renderer != null)
                        surface.Renderer.SetPropertyBlock(surface.OriginalProperties.isEmpty ? null : surface.OriginalProperties, surface.MaterialIndex);
            }
        }

        public bool WasHouseBurnt(Transform house)
        {
            if (house == null) return false;
            foreach (var site in sites)
                if (site.BurnExposure > 0f && site.Surfaces.Count > 0 && site.Building != null &&
                    (site.Building == house || site.Building.IsChildOf(house))) return true;
            return false;
        }

        private static bool IsSprinklerPart(Transform part, Transform building)
        {
            for (var parent = part; parent != null && parent != building; parent = parent.parent)
                if (parent.name == "Roof_Sprinkler") return true;
            return false;
        }

        private static void AppendRoofAnchors(BurnSite site, BuildingModelGeometry model)
        {
            if (model.IsCollection) { foreach (var part in model.Parts) AppendRoofAnchors(site, part); return; }
            var roof = model.RoofBounds;
            for (var x = -1; x <= 1; x += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var point = roof.center + new Vector3(roof.size.x * .24f * x, 0f, roof.size.z * .24f * z);
                if (model.TryProjectRoof(point, out var surface, out _)) site.Anchors.Add(model.transform.TransformPoint(surface));
            }
        }

        private void EnsureVisuals()
        {
            if (visuals != null) return;
            visuals = new GameObject("Low_Poly_Fire");
            visuals.transform.SetParent(transform, false);
            visuals.SetActive(false);
            gasProperties = new MaterialPropertyBlock();
            if (visualPreset == null) smokeMesh = CreateSmokeMesh();
            emberMesh = CreateEmberMesh();
            // Older standalone scenes can still run their saved fire test without monster-only material slots.
            var gasMaterial = gasFireMaterial != null ? gasFireMaterial : flameMaterial;
            var streetMaterial = softFireMaterial != null ? softFireMaterial : flameMaterial;
            breathStream = new LowPolyFireStream(visuals.transform, gasMaterial, visualPreset);
            streetFire = new StreetFireSpread(visuals.transform, streetMaterial);
            softFlames = CreateParticles("Soft_Street_Flames", null, gasMaterial, 1600, 0f,
                new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.25f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f)));
            flames = CreateParticles("Flames", null, gasMaterial, 1600, 0f,
                new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.18f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));
            smoke = CreateParticles("Smoke", smokeMesh, smokeMaterial, 360, 0f,
                new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.2f, 0.75f), new Keyframe(0.65f, 1.2f), new Keyframe(1f, 0f)));
            embers = CreateParticles("Embers", emberMesh, flameMaterial, 220, 0.08f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.55f, 0.8f), new Keyframe(1f, 0f)));
            extinguishSmoke = CreateParticles("Extinguish_Smoke", smokeMesh, smokeMaterial, 96, 0f,
                new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(0.1f, 0.85f), new Keyframe(0.25f, 1f), new Keyframe(0.55f, 0.8f), new Keyframe(1f, 0f)));
            if (visualPreset != null)
            {
                visualPreset.Configure(flames, false, 3);
                visualPreset.Configure(softFlames, false, 4);
                visualPreset.Configure(smoke, true, 5);
                visualPreset.Configure(extinguishSmoke, true, 6);
            }
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
            system.useAutoRandomSeed = false;
            system.randomSeed = 7351u + (uint)maximum;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.85f, 0.55f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.mesh = particleMesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enableGPUInstancing = false;
            if (particleMesh == null) FireFlow.ConfigureGas(system, material);
            return system;
        }

        private void AdvectGas(ParticleSystem system, float step, float strength)
        {
            var count = system.GetParticles(extinguishBuffer);
            var blend = 1f - Mathf.Exp(-Mathf.Max(0f, step) * 4f);
            for (var i = 0; i < count; i++)
            {
                var particle = extinguishBuffer[i];
                var curl = FireFlow.Curl(particle.position, elapsed);
                var velocity = new Vector3(wind.x * 0.12f, 0.5f, wind.y * 0.12f) + curl * strength;
                particle.velocity = Vector3.Lerp(particle.velocity, velocity, blend);
                if (visualPreset != null)
                {
                    var rotation = particle.rotation3D;
                    rotation.y += curl.x * step * 32f;
                    rotation.z += curl.z * step * 48f;
                    particle.rotation3D = rotation;
                }
                else particle.rotation += curl.z * step * 48f;
                extinguishBuffer[i] = particle;
            }
            system.SetParticles(extinguishBuffer, count);
            gasProperties.SetFloat("_FireTime", elapsed);
            system.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(gasProperties);
        }

        private void EmitFlames(BurnSite site, float step, float strength)
        {
            var protection = SprinklersActive && site.Sprinkler != null ? site.Sprinkler.FireMultiplier : 1f;
            site.FlameRemainder += step * Mathf.Max(0f, flameRate) * strength * (visualPreset != null ? .65f : 1f);
            var count = Mathf.Min(80, Mathf.FloorToInt(site.FlameRemainder));
            site.FlameRemainder -= count;
            for (var i = 0; i < count; i++)
            {
                var anchor = RandomAnchor(site);
                // Facade anchors at pavement level also belong to the sparse ground fire.
                if (anchor.y <= site.Bounds.min.y + .08f && random.NextDouble() >= streetFlameDensity) continue;
                var width = flameWidth * site.Size * RandomRange(0.65f, 1.2f) * site.SuppressionScale;
                flames.Emit(new ParticleSystem.EmitParams
                {
                    position = anchor + new Vector3(RandomRange(-0.09f, 0.09f), -0.03f, RandomRange(-0.09f, 0.09f)),
                    velocity = new Vector3(wind.x * 0.16f, RandomRange(0.16f, 0.4f), wind.y * 0.16f),
                    startLifetime = RandomRange(0.75f, 1.15f),
                    randomSeed = site.ParticleSeed,
                    meshIndex = visualPreset != null ? visualPreset.FlameIndex(++visualSample) : 0,
                    startSize3D = new Vector3(width * 1.8f, flameHeight * site.Size * RandomRange(0.8f, 1.5f) *
                        Mathf.Lerp(0.35f, 1f, protection) * site.SuppressionScale * (visualPreset != null ? .55f : 1f), width),
                    rotation3D = new Vector3(0f, visualPreset != null ? RandomRange(0f, 360f) : 0f, RandomRange(-18f, 18f)),
                    startColor = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.65f, 0.88f), RandomRange(0f, 1f))
                }, 1);
            }
        }

        private void EmitStreetFlames(float step)
        {
            for (var cellIndex = 0; cellIndex < streetFire.Cells.Count; cellIndex++)
            {
                var cell = streetFire.Cells[cellIndex];
                if (cell.Strength <= 0.001f) continue;
                // Keep the same sparse patches on every replay, while retaining the spread timing.
                if (Mathf.Repeat(cellIndex * .618034f, 1f) >= streetFlameDensity) continue;
                cell.Remainder += step * 1.8f * cell.Strength;
                var count = Mathf.Min(4, Mathf.FloorToInt(cell.Remainder)); cell.Remainder -= count;
                for (var i = 0; i < count; i++)
                {
                    var width = cell.Radius * RandomRange(2.8f, 3.8f) * .9f;
                    softFlames.Emit(new ParticleSystem.EmitParams
                    {
                        position = cell.Position + Vector3.up * 0.045f,
                        velocity = new Vector3(wind.x * 0.04f, RandomRange(0.06f, 0.18f), wind.y * 0.04f),
                        startLifetime = RandomRange(0.6f, 0.95f),
                        startSize3D = new Vector3(width, RandomRange(0.35f, 0.65f) * (visualPreset != null ? .65f : 1f), width),
                        rotation3D = new Vector3(0f, visualPreset != null ? RandomRange(0f, 360f) : 0f, RandomRange(-22f, 22f)),
                        randomSeed = sites[cell.Owner].ParticleSeed,
                        meshIndex = visualPreset != null ? visualPreset.FlameIndex(++visualSample) : 0,
                        startColor = visualPreset != null ? new Color(1f, 1f, .85f, .75f) :
                            Color.Lerp(new Color(1f, 0.29f, 0.012f, 0.32f), new Color(1f, 0.71f, 0.09f, 0.48f), RandomRange(0f, 1f))
                    }, 1);
                }
            }
        }

        private void EmitSmoke(BurnSite site, float step, float strength)
        {
            site.SmokeRemainder += step * 3.5f * Mathf.Max(0f, smokeAmount) * strength;
            var count = Mathf.Min(20, Mathf.FloorToInt(site.SmokeRemainder));
            site.SmokeRemainder -= count;
            for (var i = 0; i < count; i++)
            {
                var size = site.Size * RandomRange(0.325f, 0.475f);
                var shade = RandomRange(0.12f, 0.22f);
                smoke.Emit(new ParticleSystem.EmitParams
                {
                    position = RandomAnchor(site) + Vector3.up * flameHeight * site.Size * 0.6f,
                    velocity = new Vector3(wind.x + RandomRange(-0.12f, 0.12f), smokeRiseSpeed * RandomRange(0.8f, 1.2f), wind.y + RandomRange(-0.1f, 0.1f)),
                    startLifetime = RandomRange(1.8f, 2.5f),
                    randomSeed = site.ParticleSeed,
                    meshIndex = visualPreset != null ? visualPreset.SmokeIndex(++visualSample) : 0,
                    startSize3D = new Vector3(size, size, size),
                    rotation3D = new Vector3(RandomRange(0f, 360f), RandomRange(0f, 360f), RandomRange(0f, 360f)),
                    startColor = new Color(shade, shade, shade, visualPreset != null ? .65f : .34f)
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
            for (var i = 0; i < 8; i++)
            {
                var angle = RandomRange(0f, Mathf.PI * 2f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var size = site.Size * RandomRange(0.11f, 0.18f);
                var shade = RandomRange(0.025f, 0.07f);
                extinguishSmoke.Emit(new ParticleSystem.EmitParams
                {
                    position = site.Anchors[i % site.Anchors.Count] + outward * RandomRange(0.02f, 0.09f) + Vector3.up * 0.025f,
                    velocity = outward * RandomRange(0.15f, 0.35f) + new Vector3(wind.x * 0.15f, RandomRange(0.6f, 1f), wind.y * 0.15f),
                    startLifetime = RandomRange(0.45f, 0.7f),
                    startSize3D = new Vector3(size, size * RandomRange(0.75f, 1.1f), size),
                    rotation3D = new Vector3(RandomRange(0f, 360f), RandomRange(0f, 360f), RandomRange(0f, 360f)),
                    startColor = new Color(shade, shade, shade),
                    randomSeed = site.ParticleSeed,
                    meshIndex = visualPreset != null ? visualPreset.SmokeIndex(++visualSample) : 0
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

        private static Mesh CreateSmokeMesh()
        {
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var points = new[] { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0), new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t), new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
            for (var i = 0; i < points.Length; i++) points[i] = points[i].normalized * 0.5f;
            var indices = new[] { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var rounded = new List<Vector3>(points);
            var triangles = new List<int>();
            var midpoints = new Dictionary<int, int>();
            for (var triangle = 0; triangle < indices.Length; triangle += 3)
            {
                var a = indices[triangle]; var b = indices[triangle + 1]; var c = indices[triangle + 2];
                var ab = PuffMidpoint(a, b, rounded, midpoints);
                var bc = PuffMidpoint(b, c, rounded, midpoints);
                var ca = PuffMidpoint(c, a, rounded, midpoints);
                triangles.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var result = new Mesh { name = "Soft low poly smoke puffs" };
            result.SetVertices(rounded); result.SetTriangles(triangles, 0);
            var colors = new Color32[rounded.Count];
            for (var i = 0; i < colors.Length; i++) colors[i] = Color.white;
            result.colors32 = colors;
            result.RecalculateNormals(); result.RecalculateBounds();
            return result;
        }

        private static int PuffMidpoint(int a, int b, List<Vector3> points, Dictionary<int, int> midpoints)
        {
            var key = Mathf.Min(a, b) * 12 + Mathf.Max(a, b);
            if (midpoints.TryGetValue(key, out var index)) return index;
            index = points.Count; points.Add((points[a] + points[b]).normalized * 0.5f); midpoints.Add(key, index);
            return index;
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

        public void FinishPresentation()
        {
            if (Application.isPlaying && IsRunning) ResetTest(true);
        }

        private void OnDisable() => ResetTest(false);

        private void ResetTest(bool completed)
        {
            IsRunning = false;
            if (!completed) RestoreBuildingColors();
            if (seaMonsterFire != null) seaMonsterFire.ResetPose();
            if (breathStream != null) breathStream.Hide();
            if (streetFire != null) streetFire.Clear();
            if (navigation != null) navigation.ReleaseEventInputLock(this);
            if (flames != null) flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (extinguishSmoke != null) extinguishSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (softFlames != null) softFlames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (extinguishAudio != null) extinguishAudio.Stop();
            if (visuals != null) visuals.SetActive(false);
            if (testButton != null) testButton.interactable = true;
            if (progressFeedback != null)
            {
                if (completed) progressFeedback.Complete(); else progressFeedback.ResetPresentation();
            }
            else if (progressFill != null) progressFill.fillAmount = 0f;
            if (statusLabel != null) statusLabel.text = completed ? "Completed Â· ready" : "Ready";
            if (!completed && seaMonsterFire != null && seaMonsterFire.Walk != null) seaMonsterFire.Walk.StopAfterEventWalk();
        }

        private void OnDestroy()
        {
            if (smokeMesh != null) Destroy(smokeMesh);
            if (emberMesh != null) Destroy(emberMesh);
            if (extinguishHiss != null) Destroy(extinguishHiss);
            if (breathStream != null) breathStream.Dispose();
            if (streetFire != null) streetFire.Dispose();
        }
    }
}
