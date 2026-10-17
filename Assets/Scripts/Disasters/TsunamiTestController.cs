using System.Collections.Generic;
using CivilGameJam.Upgrades;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CivilGameJam.Disasters
{
    /// <summary>A breaking low poly wave feeds an obstacle-aware city flood.</summary>
    [DisallowMultipleComponent]
    public sealed class TsunamiTestController : MonoBehaviour
    {
        [Header("Wave")]
        [SerializeField, Range(0.1f, 8f)] private float waveHeight = 1.05f;
        [SerializeField, Range(1f, 18f)] private float waveSpeed = 6.5f;
        [SerializeField, Range(8f, 20f)] private float waveWidth = 12f;
        [SerializeField, Range(0.15f, 5f)] private float crestLength = 1.6f;
        [SerializeField, Range(0f, 1f)] private float breakingCurl = 0.7f;
        [Header("Approach to city")]
        [Tooltip("Peak offshore height. The wave rises while moving, then lowers before landfall.")]
        [SerializeField, Range(1f, 2.5f)] private float startingHeightMultiplier = 1.85f;
        [SerializeField] private float shorelineDistance = -3.1f;

        [Header("City flood")]
        [SerializeField] private Transform groundSurface;
        [SerializeField, Range(0.1f, 0.9f)] private float floodedHouseFraction = 0.5f;
        [Tooltip("Length of the incoming surge; water starts flowing out as soon as it reaches an edge.")]
        [SerializeField, Range(0.3f, 3f)] private float floodPulseDuration = 1.35f;
        [SerializeField, Range(1f, 8f)] private float floodFlowSpeed = 3.6f;
        [SerializeField, Min(1f)] private float floodRunoffTime = 5f;
        [SerializeField, Range(0.5f, 4f)] private float runoffDistance = 2f;
        [SerializeField, Range(0.1f, 0.3f)] private float floodCellSize = 0.16f;
        [SerializeField] private TsunamiBreakwater breakwater;

        [Header("Path (local Z; rotate this object to change the incoming direction)")]
        [SerializeField] private float startDistance = -9f;
        [SerializeField] private float seaLevel = -0.32f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float buildupTime = 0.35f;
        [FormerlySerializedAs("retreatTime")]
        [SerializeField, Min(0.2f)] private float fadeOutTime = 2f;

        [Header("Water and spray")]
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material foamMaterial;
        [SerializeField] private Transform buildingsRoot;
        [SerializeField, Range(0f, 2f)] private float sprayAmount = 1f;
        [SerializeField, Range(8, 48)] private int widthSegments = 24;
        [SerializeField, Range(8, 40)] private int lengthSegments = 20;

        [Header("Test panel")]
        [SerializeField] private Button testButton;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Image progressFill;

        private static readonly int FadeProperty = Shader.PropertyToID("_Fade");
        private readonly List<ImpactSite> impactSites = new List<ImpactSite>();
        private readonly List<Bounds> floodObstacles = new List<Bounds>();
        private readonly System.Random random = new System.Random();
        private GameObject visuals;
        private Mesh mesh;
        private Mesh sprayMesh;
        private MeshRenderer waterRenderer;
        private ParticleSystem spray;
        private MaterialPropertyBlock properties;
        private Vector3[] vertices;
        private Vector3[] restVertices;
        private Color[] colors;
        private Vector3[] normals;
        private Vector3[] surfacePoints;
        private Color[] surfaceColors;
        private int[] surfaceIndices;
        private float elapsed;
        private float front;
        private float growth;
        private float drain;
        private float sprayRemainder;
        private float breakwaterSprayRemainder;
        private float phaseOffset;
        private CityFloodSurface flood;
        private BreakwaterFlowSurface transmittedFlow;
        private Bounds cityBounds;
        private float groundLevel;
        private float houseHeight;
        private float coastalCrestLevel;
        private bool layoutCached;

        private sealed class ImpactSite
        {
            public Transform Building;
            public Bounds LocalBounds;
            public Bounds Footprint;
            public bool Hit;
        }

        public bool IsRunning { get; private set; }
        public float TestDuration => ShoreArrivalTime + Mathf.Max(0.3f, floodPulseDuration) + Mathf.Max(1f, floodRunoffTime);
        public float NormalizedProgress => IsRunning ? Mathf.Clamp01(elapsed / TestDuration) : 0f;
        public float FloodWaterLevel => groundLevel + houseHeight * floodedHouseFraction;
        public float FloodCoverage => flood != null ? flood.WetCoverage : 0f;
        public Bounds CityBounds { get { CacheCityLayout(); return cityBounds; } }
        public float GroundLevel { get { CacheCityLayout(); return groundLevel; } }
        public float SeaLevel => seaLevel;
        private float ShoreArrivalTime => Mathf.Max(0.1f, (shorelineDistance - startDistance) / Mathf.Max(1f, waveSpeed));
        private float WaveScale => Mathf.Clamp(waveHeight / 4.5f, 0.05f, 2f);
        private float CrestOffset => crestLength * 0.4f;
        private float FrontReach => Mathf.Max(0.2f, crestLength * 0.25f);
        private float ApproachHeightMultiplier
        {
            get
            {
                var peakDistance = Mathf.Lerp(startDistance, shorelineDistance, 0.34f);
                var crest = front - CrestOffset;
                var peak = Mathf.Clamp(startingHeightMultiplier, 1f, 2.5f);
                if (crest < peakDistance)
                    return Mathf.Lerp(0.12f, peak, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(startDistance - CrestOffset, peakDistance, crest)));
                return Mathf.Lerp(peak, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(peakDistance, shorelineDistance, crest)));
            }
        }

        private void Awake()
        {
            CacheCityLayout();
            ResetTest(false);
        }

        private void OnEnable() => HouseColumnUpgrade.GeometryChanged += OnHouseGeometryChanged;

        [ContextMenu("Start tsunami test (Play Mode)")]
        public void StartTsunami()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsRunning) return;
            if (waterMaterial == null)
            {
                Debug.LogWarning("Assign a tsunami water material before starting the test.", this);
                return;
            }

            EnsureVisuals();
            BuildSurface();
            foreach (var site in impactSites) site.Hit = false;
            flood.Clear();
            transmittedFlow.Clear();
            RefreshFloodObstacles();
            elapsed = 0f;
            sprayRemainder = 0f;
            breakwaterSprayRemainder = 0f;
            phaseOffset = RandomRange(0f, 100f);
            front = startDistance;
            growth = 0f;
            drain = 1f;
            IsRunning = true;
            visuals.SetActive(true);
            waterRenderer.enabled = true;
            if (spray != null) spray.Play(true);
            if (testButton != null) testButton.interactable = false;
            if (statusLabel != null) statusLabel.text = "Tsunami approaching";
            UpdateSurface();
            SetWaterProperties(0f);
        }

        private void Update()
        {
            if (IsRunning) AdvanceTsunami(Time.unscaledDeltaTime);
        }

        private void AdvanceTsunami(float deltaTime)
        {
            if (!IsRunning) return;
            // Simulate the whole elapsed interval, even if a frame stalls or a test advances time.
            var remaining = Mathf.Min(Mathf.Max(0f, deltaTime), TestDuration - elapsed);
            while (remaining > 0.00001f)
            {
                var step = Mathf.Min(remaining, 1f / 60f);
                elapsed += step;
                front = startDistance + elapsed * Mathf.Max(1f, waveSpeed);
                growth = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.1f, buildupTime));
                var impactAge = elapsed - ShoreArrivalTime;
                drain = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, Mathf.Max(0.6f, fadeOutTime), impactAge));
                var pulse = Mathf.Exp(-Mathf.Pow((impactAge - 0.14f) / 0.3f, 2f));
                var tail = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(floodPulseDuration * 0.45f, floodPulseDuration, impactAge));
                var transmission = breakwater != null ? breakwater.Transmission : 1f;
                var inlet = groundLevel + (FloodWaterLevel - groundLevel +
                    (coastalCrestLevel - FloodWaterLevel) * pulse) * Mathf.SmoothStep(0f, 1f, impactAge / 0.12f) * tail * transmission;
                flood.Advance(step, inlet);
                if (drain > 0.01f) EmitCrestSpray(step);
                EmitBreakwaterSplashes(step);
                EmitBuildingSplashes();
                remaining -= step;
            }
            UpdateSurface();
            SetWaterProperties(Mathf.SmoothStep(0f, 1f, elapsed / 0.15f) * drain);
            var runoffFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TestDuration - 0.35f, TestDuration, elapsed));
            flood.UpdateMesh(FloodWaterLevel - groundLevel, runoffFade);
            transmittedFlow.Update(breakwater, flood, front, elapsed, waveSpeed, floodPulseDuration, runoffFade);
            if (progressFill != null) progressFill.fillAmount = Mathf.Floor(elapsed / TestDuration * 10f + 0.00001f) * 0.1f;
            if (statusLabel != null)
                statusLabel.text = elapsed < ShoreArrivalTime ? "Wave approaching" :
                    elapsed < ShoreArrivalTime + floodPulseDuration ? "Water flowing through streets" : "Water draining";
            if (elapsed >= TestDuration - 0.0001f) ResetTest(true);
        }

        private void EnsureVisuals()
        {
            if (visuals != null) return;
            CacheCityLayout();
            visuals = new GameObject("Tsunami_Water");
            visuals.transform.SetParent(transform, false);
            var filter = visuals.AddComponent<MeshFilter>();
            waterRenderer = visuals.AddComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = waterMaterial;
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waterRenderer.receiveShadows = false;
            properties = new MaterialPropertyBlock();
            mesh = new Mesh { name = "Tsunami travelling water", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            var obstacles = new List<Bounds>();
            foreach (var site in impactSites) obstacles.Add(site.Footprint);
            flood = new CityFloodSurface(visuals.transform, waterMaterial, cityBounds, groundLevel, floodCellSize,
                FloodWaterLevel - groundLevel, coastalCrestLevel - groundLevel + 0.06f,
                floodFlowSpeed, runoffDistance, seaLevel - 0.08f, obstacles);
            transmittedFlow = new BreakwaterFlowSurface(visuals.transform, waterMaterial, cityBounds, groundLevel, seaLevel,
                waveWidth, FloodWaterLevel - groundLevel, coastalCrestLevel - groundLevel);
            if (foamMaterial != null) CreateSpray();
        }

        private void BuildSurface()
        {
            mesh.Clear();
            var columns = Mathf.Clamp(widthSegments, 8, 48);
            var rows = Mathf.Clamp(lengthSegments, 8, 40);
            restVertices = new Vector3[(columns + 1) * (rows + 1)];
            surfacePoints = new Vector3[restVertices.Length];
            surfaceColors = new Color[restVertices.Length];
            var triangles = new List<int>(columns * rows * 6);
            // A broad asymmetric crest travels offshore; inland water is simulated separately.
            var backLength = CrestOffset + Mathf.Max(0.15f, crestLength) * 2f;
            for (var z = 0; z <= rows; z++)
            for (var x = 0; x <= columns; x++)
            {
                var index = z * (columns + 1) + x;
                var point = new Vector3(Mathf.Lerp(-waveWidth * 0.5f, waveWidth * 0.5f, (float)x / columns), 0f,
                    Mathf.Lerp(-backLength, FrontReach, (float)z / rows));
                if (x > 0 && x < columns && z > 0 && z < rows)
                    point.x += ((z & 1) == 0 ? -0.15f : 0.15f) * waveWidth / columns;
                restVertices[index] = point;
                if (x == columns || z == rows) continue;
                var right = index + 1;
                var next = index + columns + 1;
                if (((x + z) & 1) == 0)
                {
                    triangles.Add(index); triangles.Add(next); triangles.Add(right);
                    triangles.Add(right); triangles.Add(next); triangles.Add(next + 1);
                }
                else
                {
                    triangles.Add(index); triangles.Add(next); triangles.Add(next + 1);
                    triangles.Add(index); triangles.Add(next + 1); triangles.Add(right);
                }
            }
            // Separate triangle vertices keep normals and colours flat on each polygon.
            surfaceIndices = triangles.ToArray();
            vertices = new Vector3[surfaceIndices.Length];
            colors = new Color[vertices.Length];
            normals = new Vector3[vertices.Length];
            var meshIndices = new int[vertices.Length];
            for (var i = 0; i < meshIndices.Length; i++) meshIndices[i] = i;
            mesh.vertices = vertices;
            mesh.SetTriangles(meshIndices, 0);
        }

        private Vector3 SurfacePoint(float x, float z, out float foam)
        {
            var time = elapsed + phaseOffset;
            var unevenFront = Mathf.Sin(x * 0.9f + time * 1.1f) * 0.09f;
            var distance = front - z + unevenFront;
            var crestOffset = CrestOffset;
            var spread = distance < crestOffset ? crestLength * 0.43f : Mathf.Max(0.15f, crestLength);
            var profile = Mathf.Exp(-Mathf.Pow((distance - crestOffset) / spread, 2f));
            profile *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-FrontReach, 0.1f * WaveScale, distance));
            profile *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(crestOffset + crestLength * 1.4f, crestOffset + crestLength * 2f, distance));
            var crestVariation = 0.95f + Mathf.Sin(x * 1.3f + time * 1.2f) * 0.05f;
            var sideFalloff = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(waveWidth * 0.4f, waveWidth * 0.5f, Mathf.Abs(x)));
            var crestStrength = sideFalloff * ApproachHeightMultiplier;
            var height = (coastalCrestLevel - seaLevel) * profile * crestVariation * crestStrength * growth;
            var curl = breakingCurl * waveHeight * Mathf.Pow(profile, 3f) * 0.22f * growth * crestStrength;
            foam = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 0.98f, profile)) * sideFalloff;
            return new Vector3(x, seaLevel + Mathf.Max(0f, height), z + curl);
        }

        private void UpdateSurface()
        {
            var referenceHeight = Mathf.Max(0.1f, (coastalCrestLevel - seaLevel) * ApproachHeightMultiplier);
            for (var i = 0; i < restVertices.Length; i++)
            {
                var rest = restVertices[i];
                surfacePoints[i] = SurfacePoint(rest.x, front + rest.z, out var foam);
                var height = Mathf.Max(0f, surfacePoints[i].y - seaLevel);
                var visibility = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.12f, height));
                var breakDistance = BreakDistanceAt(rest.x);
                if (breakwater != null && breakwater.IsActive)
                {
                    var collapse = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(breakDistance - 0.5f, breakDistance + 0.06f, surfacePoints[i].z));
                    var reduced = groundLevel + Mathf.Min(Mathf.Max(0f, surfacePoints[i].y - groundLevel), coastalCrestLevel - groundLevel) * breakwater.Transmission;
                    var point = surfacePoints[i]; point.y = Mathf.Lerp(point.y, reduced, collapse); surfacePoints[i] = point;
                    foam *= 1f - collapse * 0.85f;
                }
                // The tall crest breaks here; the reduced flow continues in the connected surface.
                visibility *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(breakDistance - 0.04f, breakDistance + 0.12f, surfacePoints[i].z));
                surfaceColors[i] = new Color(foam, Mathf.Clamp01(height / referenceHeight), visibility, 1f);
            }
            for (var i = 0; i < vertices.Length; i += 3)
            {
                var a = surfaceIndices[i];
                var b = surfaceIndices[i + 1];
                var c = surfaceIndices[i + 2];
                vertices[i] = surfacePoints[a];
                vertices[i + 1] = surfacePoints[b];
                vertices[i + 2] = surfacePoints[c];
                var normal = Vector3.Cross(vertices[i + 1] - vertices[i], vertices[i + 2] - vertices[i]).normalized;
                normals[i] = normals[i + 1] = normals[i + 2] = normal;
                var faceColor = (surfaceColors[a] + surfaceColors[b] + surfaceColors[c]) / 3f;
                colors[i] = colors[i + 1] = colors[i + 2] = faceColor;
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        private void SetWaterProperties(float fade)
        {
            properties.SetFloat(FadeProperty, fade);
            waterRenderer.SetPropertyBlock(properties);
        }

        private void CreateSpray()
        {
            var sprayObject = new GameObject("Foam_And_Splashes");
            sprayObject.transform.SetParent(visuals.transform, false);
            sprayObject.SetActive(false);
            spray = sprayObject.AddComponent<ParticleSystem>();
            var main = spray.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.maxParticles = 1000;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startRotation3D = true;
            main.startSpeed = 0f;
            main.gravityModifier = 0.8f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = spray.emission;
            emission.enabled = false;
            var shape = spray.shape;
            shape.enabled = false;
            var color = spray.colorOverLifetime;
            color.enabled = true;
            color.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.65f, 0.9f, 0.95f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            };
            var size = spray.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.65f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
            var renderer = spray.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = foamMaterial;
            sprayMesh = new Mesh { name = "Low poly foam droplet" };
            sprayMesh.vertices = new[] { Vector3.up * 0.5f, Vector3.down * 0.5f, Vector3.left * 0.5f, Vector3.right * 0.5f, Vector3.forward * 0.5f, Vector3.back * 0.5f };
            sprayMesh.triangles = new[] { 0, 4, 3, 0, 2, 4, 0, 5, 2, 0, 3, 5, 1, 3, 4, 1, 4, 2, 1, 2, 5, 1, 5, 3 };
            var uv = new Vector2[6];
            for (var i = 0; i < uv.Length; i++) uv[i] = new Vector2(0.5f, 0.5f);
            sprayMesh.uv = uv;
            sprayMesh.RecalculateNormals();
            sprayMesh.RecalculateBounds();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = sprayMesh;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 0.035f;
            sprayObject.SetActive(true);
        }

        private void EmitCrestSpray(float deltaTime)
        {
            if (spray == null || sprayAmount <= 0f) return;
            sprayRemainder += deltaTime * 100f * sprayAmount * growth;
            var count = Mathf.Min(64, Mathf.FloorToInt(sprayRemainder));
            sprayRemainder -= count;
            for (var i = 0; i < count; i++)
            {
                var point = SurfacePoint(RandomRange(-waveWidth * 0.48f, waveWidth * 0.48f), front - CrestOffset, out _);
                var breakDistance = BreakDistanceAt(point.x);
                if (point.z >= breakDistance + 0.08f) continue;
                EmitDroplet(point + Vector3.up * 0.04f * WaveScale, new Vector3(RandomRange(-0.6f, 0.6f), RandomRange(0.5f, 2.1f), RandomRange(0.8f, 2.8f)), RandomRange(0.08f, 0.18f));
            }
        }

        private void CacheCityLayout()
        {
            if (layoutCached) return;
            impactSites.Clear();
            if (buildingsRoot == null) return;
            var houseTotal = 0f;
            var houseCount = 0;
            foreach (Transform building in buildingsRoot)
            {
                var renderers = building.GetComponentsInChildren<MeshRenderer>();
                var bounds = new Bounds();
                var footprint = new Bounds();
                var found = false;
                var foundationFound = false;
                if (building.TryGetComponent<BuildingModelGeometry>(out var model))
                {
                    bounds = LocalBounds(model.WorldBounds);
                    footprint = LocalBounds(model.WorldFoundationBounds);
                    found = foundationFound = true;
                }
                else foreach (var renderer in renderers)
                {
                    if (IsBuildingUpgrade(renderer.transform, building) || IsRoofDecoration(renderer.name)) continue;
                    var local = LocalBounds(renderer.bounds);
                    if (!found) { bounds = local; found = true; } else bounds.Encapsulate(local);
                    if (!string.Equals(renderer.name, "Foundation", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (!foundationFound) { footprint = local; foundationFound = true; } else footprint.Encapsulate(local);
                }
                if (!found) continue;
                if (!foundationFound) footprint = bounds;
                impactSites.Add(new ImpactSite { Building = building, LocalBounds = bounds, Footprint = footprint });
                if (building.name.IndexOf("House", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                houseTotal += bounds.size.y; houseCount++;
            }
            groundLevel = impactSites.Count > 0 ? impactSites[0].LocalBounds.min.y : 0.045f;
            houseHeight = houseCount > 0 ? houseTotal / houseCount : 0.72575f;
            var firstZ = float.MaxValue;
            foreach (var site in impactSites) firstZ = Mathf.Min(firstZ, site.LocalBounds.min.z);
            var firstHeight = 0f; var firstCount = 0;
            foreach (var site in impactSites)
                if (site.LocalBounds.min.z < firstZ + 0.5f) { firstHeight += site.LocalBounds.size.y; firstCount++; }
            coastalCrestLevel = groundLevel + (firstCount > 0 ? firstHeight / firstCount : 1.3f) * 0.5f * (waveHeight / 1.05f);
            var groundRenderer = groundSurface != null ? groundSurface.GetComponent<MeshRenderer>() : null;
            cityBounds = groundRenderer != null ? LocalBounds(groundRenderer.bounds) :
                new Bounds(new Vector3(0f, groundLevel, 0f), new Vector3(8.1f, 0.1f, 6.16f));
            // The incoming edge and grid always meet, including when the city is repositioned.
            shorelineDistance = cityBounds.min.z;
            layoutCached = true;
        }

        private float BreakDistanceAt(float x)
        {
            if (breakwater == null || !breakwater.IsActive) return shorelineDistance;
            // The small amount passing around either tip joins the same low surge as the bay.
            return breakwater.TryGetWaveFace(x, out var distance) ? distance : breakwater.FrontDistance;
        }

        private void EmitBreakwaterSplashes(float deltaTime)
        {
            if (spray == null || breakwater == null || !breakwater.IsActive) { breakwaterSprayRemainder = 0f; return; }
            var impactTime = (breakwater.FrontDistance - startDistance) / Mathf.Max(1f, waveSpeed);
            var age = elapsed - impactTime;
            if (age < 0f || age > 1.3f) return;
            var strength = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(age / 1.15f) * Mathf.PI));
            breakwaterSprayRemainder += deltaTime * 220f * sprayAmount * strength;
            var count = Mathf.Min(64, Mathf.FloorToInt(breakwaterSprayRemainder));
            breakwaterSprayRemainder -= count;
            for (var i = 0; i < count; i++)
            {
                var x = breakwater.CenterX + RandomRange(-0.49f, 0.49f) * breakwater.Width;
                if (Mathf.Abs(x) > waveWidth * 0.5f) continue;
                if (!breakwater.TryGetWaveFace(x, out var distance)) continue;
                var localAge = elapsed - (distance - startDistance) / Mathf.Max(1f, waveSpeed);
                if (localAge < 0f || localAge > 1f) continue;
                var point = new Vector3(x, breakwater.TopLevel + RandomRange(-0.04f, 0.06f), distance - 0.03f);
                EmitDroplet(point, new Vector3(RandomRange(-1.5f, 1.5f), RandomRange(3f, 5.5f), RandomRange(-2.2f, -0.3f)), RandomRange(0.1f, 0.2f));
            }
        }

        private void OnHouseGeometryChanged(HouseColumnUpgrade _) => RefreshFloodObstacles();

        private void RefreshFloodObstacles()
        {
            if (flood == null) return;
            floodObstacles.Clear();
            var upgrades = FindObjectsByType<HouseColumnUpgrade>(FindObjectsSortMode.None);
            foreach (var site in impactSites)
            {
                if (site.Building == null) continue;
                HouseColumnUpgrade openHouse = null;
                foreach (var upgrade in upgrades)
                    if (upgrade.Building == site.Building && upgrade.IsInstalled) { openHouse = upgrade; break; }
                if (openHouse != null) openHouse.AppendColumnBounds(transform, floodObstacles);
                else if (site.Building.TryGetComponent<BuildingModelGeometry>(out var model)) model.AppendFoundationBounds(transform, floodObstacles);
                else floodObstacles.Add(site.Footprint);
            }
            // Water can pass between the columns instead of meeting the former solid ground floor.
            flood.SetObstacles(floodObstacles);
        }

        private Bounds LocalBounds(Bounds world)
        {
            var bounds = new Bounds(transform.InverseTransformPoint(world.center), Vector3.zero);
            for (var corner = 0; corner < 8; corner++)
                bounds.Encapsulate(transform.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
            return bounds;
        }

        private static bool IsRoofDecoration(string name)
        {
            foreach (var word in new[] { "stack", "chimney", "antenna", "flag", "pole", "vent" })
                if (name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool IsBuildingUpgrade(Transform part, Transform building)
        {
            for (var parent = part; parent != null && parent != building; parent = parent.parent)
                if (parent.name == "Rail_System" || parent.name == "Roof_Sprinkler") return true;
            return false;
        }

        private void EmitBuildingSplashes()
        {
            if (spray == null || sprayAmount <= 0f) return;
            foreach (var site in impactSites)
            {
                var bounds = site.LocalBounds;
                if (site.Hit) continue;
                if (Mathf.Abs(bounds.center.x) > waveWidth * 0.5f) continue;
                var footprint = site.Footprint;
                var waterDepth = flood.SampleDepth(footprint.center.x, footprint.min.z - 0.17f);
                if (waterDepth < 0.03f) continue;
                site.Hit = true;
                var splashFloor = groundLevel + waterDepth * 0.65f;
                var splashCeiling = Mathf.Min(bounds.max.y, groundLevel + waterDepth + 0.09f);
                var count = Mathf.RoundToInt(48f * sprayAmount);
                for (var i = 0; i < count; i++)
                {
                    var x = RandomRange(bounds.min.x - 0.08f, bounds.max.x + 0.08f);
                    var point = new Vector3(x, RandomRange(splashFloor, splashCeiling), footprint.min.z - 0.06f);
                    EmitDroplet(point, new Vector3(RandomRange(-1.8f, 1.8f), RandomRange(2.5f, 5.5f), RandomRange(-0.7f, 1.3f)), RandomRange(0.1f, 0.24f));
                }
            }
        }

        private void EmitDroplet(Vector3 localPosition, Vector3 localVelocity, float size)
        {
            var effectScale = WaveScale * Mathf.Min(1.5f, ApproachHeightMultiplier);
            spray.Emit(new ParticleSystem.EmitParams
            {
                position = transform.TransformPoint(localPosition),
                velocity = transform.TransformDirection(localVelocity) * effectScale,
                startSize = size * effectScale,
                startLifetime = RandomRange(0.35f, 0.85f) * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(WaveScale)),
                rotation3D = new Vector3(RandomRange(0f, 360f), RandomRange(0f, 360f), RandomRange(0f, 360f)),
                startColor = Color.white
            }, 1);
        }

        private float RandomRange(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());

        [ContextMenu("Stop tsunami test")]
        public void StopTsunami()
        {
            if (Application.isPlaying) ResetTest(false);
        }

        private void OnDisable()
        {
            HouseColumnUpgrade.GeometryChanged -= OnHouseGeometryChanged;
            ResetTest(false);
        }

        private void ResetTest(bool completed)
        {
            IsRunning = false;
            if (flood != null) flood.Clear();
            if (transmittedFlow != null) transmittedFlow.Clear();
            if (spray != null) spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (visuals != null) visuals.SetActive(false);
            if (testButton != null) testButton.interactable = true;
            if (progressFill != null) progressFill.fillAmount = 0f;
            if (statusLabel != null) statusLabel.text = completed ? "Completed · ready" : "Ready";
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (sprayMesh != null) Destroy(sprayMesh);
            if (flood != null) flood.Dispose();
            if (transmittedFlow != null) transmittedFlow.Dispose();
        }
    }
}
