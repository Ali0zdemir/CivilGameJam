using TMPro;
using CivilGameJam.Presentation;
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
        [Header("Saved sea monster")]
        [SerializeField] private SeaMonsterEmergence seaMonster;
        [SerializeField] private EarthquakeTestController earthquake;
        [SerializeField] private FireTestController fire;

        [Header("Event camera")]
        [SerializeField] private CityCameraController navigation;
        [SerializeField, Range(1f, 1.3f)] private float cameraZoomFactor = 1.2f;
        [SerializeField, Min(0.1f)] private float cameraLeadSeconds = 1.6f;
        [SerializeField, Min(0.05f)] private float cameraFollowResponse = 0.22f;
        [SerializeField, Min(0.1f)] private float cameraReturnSeconds = 1.6f;

        [Header("Wave")]
        [SerializeField] private AuthoredTsunamiWave authoredWave;
        [SerializeField, Range(0.1f, 8f)] private float waveHeight = 1.05f;
        [Tooltip("Fallback speed when no authored wave is assigned. Otherwise use its Movement Speed.")]
        [SerializeField, Range(1f, 18f)] private float waveSpeed = 4.5f;
        [SerializeField, Range(8f, 20f)] private float waveWidth = 12f;
        [SerializeField, Range(0.15f, 5f)] private float crestLength = 1.6f;
        [SerializeField, Range(0f, 1f)] private float breakingCurl = 0.7f;
        [Header("Approach to city")]
        [Tooltip("Peak offshore height. The wave rises while moving, then lowers before landfall.")]
        [SerializeField, Range(1f, 2.5f)] private float startingHeightMultiplier = 1.85f;
        [SerializeField] private float shorelineDistance = -3.1f;

        [Header("City flood")]
        [SerializeField] private Transform groundSurface;
        [Tooltip("Maximum water height relative to the shortest individual building inside the prefab.")]
        [FormerlySerializedAs("floodedHouseFraction")]
        [SerializeField, Range(0.1f, 0.9f)] private float maximumFloodBuildingFraction = 0.5f;
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
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private ProgressBarFeedback progressFeedback;

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
        private float sequenceElapsed;
        private float shoreArrivalElapsed = -1f;
        private float routeEndElapsed = -1f;
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
        private Bounds floodReferenceBounds;
        private float coastalCrestLevel;
        private bool layoutCached;
        private bool cameraFocusStarted;
        private bool cameraFollowFinished;
        private bool monsterReleasedToWalk;
        private bool sequenceOwnsCamera;

        private sealed class ImpactSite
        {
            public Transform Building;
            public Bounds LocalBounds;
            public Bounds Footprint;
            public ApartmentTsunamiCollapse Collapse;
            public bool Hit;
        }

        public bool IsRunning { get; private set; }
        public float WaveSpeed => authoredWave != null ? authoredWave.MovementSpeed : Mathf.Max(1f, waveSpeed);
        public bool IsWaveCameraActive => authoredWave != null && IsRunning && cameraFocusStarted;
        public float TestDuration => Mathf.Max(seaMonster != null ? seaMonster.RiseDuration : 0f, WaveReleaseDelay + WaveDuration);
        public float NormalizedProgress => IsRunning ? Mathf.Clamp01(sequenceElapsed / TestDuration) : 0f;
        public float MaximumFloodWaterLevel
        {
            get
            {
                CacheCityLayout();
                return Mathf.Max(groundLevel, floodReferenceBounds.min.y + floodReferenceBounds.size.y * maximumFloodBuildingFraction);
            }
        }
        public float FloodWaterLevel => Mathf.Lerp(groundLevel, MaximumFloodWaterLevel, 0.9f);
        public float FloodCoverage => flood != null ? flood.WetCoverage : 0f;
        public Bounds CityBounds { get { CacheCityLayout(); return cityBounds; } }
        public Transform BuildingsRoot => buildingsRoot;
        public Transform GroundSurface => groundSurface;
        public float GroundLevel { get { CacheCityLayout(); return groundLevel; } }
        public float SeaLevel => seaLevel;
        private float FloodCrestLevel => MaximumFloodWaterLevel;
        private float WaveReleaseDelay => seaMonster != null ? seaMonster.WaveReleaseDelay : 0f;
        private float WaveDuration => Mathf.Max(ShoreArrivalTime + Mathf.Max(0.3f, floodPulseDuration) + Mathf.Max(1f, floodRunoffTime),
            authoredWave != null ? RouteEndTime + .35f : 0f);
        private float RouteEndTime => routeEndElapsed >= 0f ? routeEndElapsed :
            IsRunning ? elapsed + Mathf.Max(0f, authoredWave.EndDistance - front) / WaveSpeed :
            Mathf.Max(0f, authoredWave.EndDistance - authoredWave.StartDistance) / WaveSpeed;
        private float ShoreArrivalTime => shoreArrivalElapsed >= 0f ? shoreArrivalElapsed :
            IsRunning ? elapsed + Mathf.Max(0f, shorelineDistance - front) / WaveSpeed :
            Mathf.Max(.0001f, (shorelineDistance - startDistance) / WaveSpeed);
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
            if (progressFeedback == null && progressFill != null) progressFeedback = progressFill.GetComponentInParent<ProgressBarFeedback>(true);
            CacheCityLayout();
            ResetTest(false);
        }

        private void OnEnable() => HouseColumnUpgrade.GeometryChanged += OnHouseGeometryChanged;

        [ContextMenu("Start tsunami test (Play Mode)")]
        public void StartTsunami() => BeginTsunami(false);

        public void StartSequenceTsunami() => BeginTsunami(true);

        private void BeginTsunami(bool useSequenceCamera)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsRunning) return;
            if (waterMaterial == null)
            {
                Debug.LogWarning("Assign a tsunami water material before starting the test.", this);
                return;
            }

            if (fire != null) fire.StopFire();
            if (earthquake != null) earthquake.StopEarthquake();
            if (authoredWave != null) startDistance = authoredWave.StartDistance;
            EnsureVisuals();
            BuildSurface();
            foreach (var site in impactSites)
            {
                site.Hit = false;
                if (site.Collapse != null) site.Collapse.ResetCollapse();
            }
            flood.Clear();
            transmittedFlow.Clear();
            RefreshFloodObstacles();
            elapsed = 0f;
            sequenceElapsed = 0f;
            shoreArrivalElapsed = -1f;
            routeEndElapsed = -1f;
            sequenceOwnsCamera = useSequenceCamera;
            monsterReleasedToWalk = false;
            if (seaMonster != null) seaMonster.ResetEmergence();
            sprayRemainder = 0f;
            breakwaterSprayRemainder = 0f;
            phaseOffset = RandomRange(0f, 100f);
            front = startDistance;
            growth = 0f;
            drain = 1f;
            IsRunning = true;
            if (authoredWave != null) authoredWave.SetWave(startDistance, seaLevel, 1f, 0f);
            if (progressFeedback != null) progressFeedback.Begin();
            if (navigation != null) navigation.AcquireEventInputLock(this);
            cameraFocusStarted = cameraFollowFinished = false;
            UpdateEventCamera();
            visuals.SetActive(WaveReleaseDelay <= 0f);
            waterRenderer.enabled = authoredWave == null;
            if (spray != null && visuals.activeSelf) spray.Play(true);
            if (testButton != null) testButton.interactable = false;
            if (statusLabel != null) statusLabel.text = seaMonster != null ? "Monster emerging" : "Tsunami approaching";
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
            // Arrival/route-end times become exact during this loop; keep the requested interval
            // available rather than truncating it to the estimate made before landfall.
            var remaining = Mathf.Max(0f, deltaTime);
            while (remaining > 0.00001f && sequenceElapsed < TestDuration - 0.00001f)
            {
                var step = Mathf.Min(remaining, 1f / 60f, TestDuration - sequenceElapsed);
                var previousWaveTime = elapsed;
                sequenceElapsed += step;
                elapsed = Mathf.Max(0f, sequenceElapsed - WaveReleaseDelay);
                remaining -= step;
                var waveStep = elapsed - previousWaveTime;
                if (waveStep <= 0f) continue;
                if (!visuals.activeSelf)
                {
                    visuals.SetActive(true);
                    if (spray != null) spray.Play(true);
                }
                var previousFront = front;
                front += waveStep * WaveSpeed;
                if (shoreArrivalElapsed < 0f && front >= shorelineDistance)
                    shoreArrivalElapsed = previousWaveTime + Mathf.Max(0f, shorelineDistance - previousFront) / WaveSpeed;
                if (authoredWave != null && routeEndElapsed < 0f && front >= authoredWave.EndDistance)
                    routeEndElapsed = previousWaveTime + Mathf.Max(0f, authoredWave.EndDistance - previousFront) / WaveSpeed;
                growth = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.1f, buildupTime));
                var impactAge = elapsed - ShoreArrivalTime;
                drain = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, Mathf.Max(0.6f, fadeOutTime), impactAge));
                var pulse = Mathf.Exp(-Mathf.Pow((impactAge - 0.14f) / 0.3f, 2f));
                var tail = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(floodPulseDuration * 0.45f, floodPulseDuration, impactAge));
                var transmission = breakwater != null ? breakwater.Transmission : 1f;
                var inlet = groundLevel + (FloodWaterLevel - groundLevel +
                    (FloodCrestLevel - FloodWaterLevel) * pulse) * Mathf.SmoothStep(0f, 1f, impactAge / 0.12f) * tail * transmission;
                flood.Advance(waveStep, inlet);
                if (drain > 0.01f) EmitCrestSpray(waveStep);
                EmitBreakwaterSplashes(waveStep);
                EmitBuildingSplashes();
            }
            if (seaMonster != null && !monsterReleasedToWalk)
                seaMonster.SetEmergenceTime(sequenceElapsed, Mathf.InverseLerp(startDistance, shorelineDistance, front));
            if (authoredWave != null)
                authoredWave.SetWave(front, seaLevel, 1f, elapsed > .00001f ? 1f : 0f);
            UpdateEventCamera();
            UpdateSurface();
            SetWaterProperties(Mathf.SmoothStep(0f, 1f, elapsed / 0.15f) * drain);
            var runoffFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WaveDuration - 0.35f, WaveDuration, elapsed));
            flood.UpdateMesh(FloodWaterLevel - groundLevel, runoffFade);
            transmittedFlow.Update(breakwater, flood, front, elapsed, WaveSpeed, floodPulseDuration, runoffFade);
            if (progressFeedback != null) progressFeedback.SetProgress(sequenceElapsed / TestDuration, true);
            else if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(sequenceElapsed / TestDuration);
            if (statusLabel != null)
                statusLabel.text = sequenceElapsed < WaveReleaseDelay ? "Monster emerging" :
                    elapsed < ShoreArrivalTime ? "Wave approaching" :
                    authoredWave != null && elapsed < RouteEndTime ? "Wave crossing the city" :
                    elapsed < ShoreArrivalTime + floodPulseDuration ? "Water flowing through streets" : "Water draining";
            if (sequenceElapsed >= TestDuration - 0.0001f) ResetTest(true);
        }

        private void UpdateEventCamera()
        {
            if (navigation == null) return;
            var leadStart = Mathf.Max(0f, WaveReleaseDelay - cameraLeadSeconds);
            if (authoredWave != null)
            {
                // Keep the player's normal viewing distance and hold on the city as the wave crosses it.
                // Start immediately so a sequence's initial framing cannot widen the view first.
                if (!cameraFocusStarted)
                    cameraFocusStarted = navigation.BeginEventFocus(this, transform.TransformPoint(cityBounds.center), 1f, cameraLeadSeconds);
                return;
            }
            if (sequenceOwnsCamera) return;
            if (!cameraFocusStarted && sequenceElapsed >= leadStart)
            {
                var origin = transform.TransformPoint(new Vector3(cityBounds.center.x, seaLevel + waveHeight * 0.6f, startDistance));
                cameraFocusStarted = navigation.BeginEventFocus(this, origin, cameraZoomFactor,
                    Mathf.Max(0.1f, WaveReleaseDelay - sequenceElapsed));
            }
            if (!cameraFocusStarted || cameraFollowFinished || elapsed <= 0f) return;
            if (elapsed < ShoreArrivalTime)
            {
                var crest = transform.TransformPoint(new Vector3(cityBounds.center.x,
                    Mathf.Lerp(seaLevel, coastalCrestLevel, 0.6f), Mathf.Min(front, shorelineDistance)));
                navigation.TrackEventFocus(this, crest, cameraFollowResponse);
            }
            else
            {
                navigation.HoldEventFocus(this);
                cameraFollowFinished = true;
            }
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
                FloodWaterLevel - groundLevel, FloodCrestLevel - groundLevel,
                floodFlowSpeed, runoffDistance, seaLevel - 0.08f, obstacles);
            transmittedFlow = new BreakwaterFlowSurface(visuals.transform, waterMaterial, cityBounds, groundLevel, seaLevel,
                waveWidth, FloodWaterLevel - groundLevel, FloodCrestLevel - groundLevel);
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
                // Collapse the incoming crest to the lower city surge as it reaches the shore or wall.
                var collapseEnd = Mathf.Min(breakDistance + 0.06f, shorelineDistance - 0.04f);
                var collapse = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(breakDistance - 0.5f, collapseEnd, surfacePoints[i].z));
                var transmission = breakwater != null && breakwater.IsActive ? breakwater.Transmission : 1f;
                var reduced = groundLevel + Mathf.Min(Mathf.Max(0f, surfacePoints[i].y - groundLevel), FloodCrestLevel - groundLevel) * transmission;
                var point = surfacePoints[i]; point.y = Mathf.Lerp(point.y, Mathf.Min(point.y, reduced), collapse); surfacePoints[i] = point;
                foam *= 1f - collapse * 0.85f;
                height = Mathf.Max(0f, point.y - seaLevel);
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
            // The artist mesh already contains its foam. The hidden procedural crest has a
            // different profile, so its droplets would float ahead of a resized authored wave.
            // Keep contact splashes at the breakwater and flooded buildings independent.
            if (authoredWave != null || spray == null || sprayAmount <= 0f) return;
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
            floodReferenceBounds = new Bounds();
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
                    // A collection spans tall and short buildings; measure its individual models.
                    foreach (var part in model.GetComponentsInChildren<BuildingModelGeometry>())
                        if (!part.IsCollection) IncludeFloodReference(LocalBounds(part.WorldBounds));
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
                impactSites.Add(new ImpactSite { Building = building, LocalBounds = bounds, Footprint = footprint,
                    Collapse = building.GetComponent<ApartmentTsunamiCollapse>() });
                if (model == null) IncludeFloodReference(bounds);
            }
            groundLevel = impactSites.Count > 0 ? impactSites[0].LocalBounds.min.y : 0.045f;
            if (floodReferenceBounds.size.y <= 0f)
                floodReferenceBounds = new Bounds(new Vector3(0f, groundLevel + 0.72575f * 0.5f, 0f), new Vector3(1f, 0.72575f, 1f));
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

        private void IncludeFloodReference(Bounds bounds)
        {
            if (bounds.size.y > 0.001f && (floodReferenceBounds.size.y <= 0f || bounds.size.y < floodReferenceBounds.size.y))
                floodReferenceBounds = bounds;
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
            var age = (front - breakwater.FrontDistance) / WaveSpeed;
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
                var localAge = (front - distance) / WaveSpeed;
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
            foreach (var site in impactSites)
            {
                var bounds = site.LocalBounds;
                if (site.Building == null || !site.Building.gameObject.activeInHierarchy) continue;
                if (Mathf.Abs(bounds.center.x) > waveWidth * 0.5f) continue;
                var footprint = site.Footprint;
                // A coastal tile can leave less than the usual probe offset in front of its building.
                // Keep the probe inside the simulated shore so real inlet water still registers contact.
                var sampleX = Mathf.Clamp(footprint.center.x, cityBounds.min.x + .001f, cityBounds.max.x - .001f);
                var sampleZ = Mathf.Clamp(footprint.min.z - .17f, cityBounds.min.z + .001f, cityBounds.max.z - .001f);
                var waterDepth = flood.SampleDepth(sampleX, sampleZ);
                if (waterDepth < 0.03f) continue;
                if (site.Collapse != null) site.Collapse.TryCollapse();
                if (site.Hit) continue;
                site.Hit = true;
                if (spray == null || sprayAmount <= 0f) continue;
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

        public void ReleaseMonsterToWalk() => monsterReleasedToWalk = true;

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
            shoreArrivalElapsed = -1f;
            routeEndElapsed = -1f;
            if (authoredWave != null) authoredWave.Hide();
            if (!completed)
                foreach (var site in impactSites) if (site.Collapse != null) site.Collapse.ResetCollapse();
            if (navigation != null)
            {
                if (cameraFocusStarted) navigation.EndEventFocus(this, cameraReturnSeconds);
                navigation.ReleaseEventInputLock(this);
            }
            cameraFocusStarted = cameraFollowFinished = false;
            if (seaMonster != null && Application.isPlaying && !monsterReleasedToWalk)
            {
                if (completed) seaMonster.SetEmergenceTime(seaMonster.RiseDuration);
                else seaMonster.ResetEmergence();
            }
            if (flood != null) flood.Clear();
            if (transmittedFlow != null) transmittedFlow.Clear();
            if (spray != null) spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (visuals != null) visuals.SetActive(false);
            if (testButton != null) testButton.interactable = true;
            if (progressFeedback != null)
            {
                if (completed) progressFeedback.Complete(); else progressFeedback.ResetPresentation();
            }
            else if (progressFill != null) progressFill.fillAmount = 0f;
            if (statusLabel != null) statusLabel.text = completed ? "Completed Â· ready" : "Ready";
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
