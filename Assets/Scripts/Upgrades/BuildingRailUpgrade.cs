using System;
using UnityEngine;

namespace CivilGameJam.Upgrades
{
    /// <summary>Raises one house onto two parallel metal rails without changing its model.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildingRailUpgrade : MonoBehaviour
    {
        [SerializeField] private Transform building;
        [Header("Rail appearance (world units)")]
        [SerializeField, Range(0.04f, 0.2f)] private float liftHeight = 0.1f;
        [SerializeField, Range(0.04f, 0.16f)] private float railWidth = 0.09f;
        [SerializeField, Range(0f, 0.3f)] private float endExtension = 0.12f;
        [SerializeField, Range(0.35f, 0.8f)] private float railSpacing = 0.58f;
        [SerializeField, Range(0.2f, 2f)] private float transitionDuration = 1.05f;
        [SerializeField, Range(1f, 3f)] private float peakLiftMultiplier = 2f;
        [SerializeField, Min(0.1f)] private float removalDuration = 0.35f;
        [SerializeField] private Material railMaterial;
        [SerializeField] private Material supportMaterial;

        [Header("Installation dust")]
        [SerializeField] private Material installationDustMaterial;
        [SerializeField, Range(0f, 2f)] private float dustAmount = 1f;
        [SerializeField, Range(0f, 3f)] private float dustMultiplier = 1.5f;
        [SerializeField, Range(0.1f, 0.5f)] private float dustSize = 0.24f;
        [SerializeField, Range(0.3f, 1.2f)] private float dustLifetime = 0.7f;

        private GameObject railVisuals;
        private Vector3 originalLocalPosition;
        private Bounds foundationBounds;
        private float currentLift;
        private float startingLift;
        private float transitionAge;
        private bool initialized;
        private ParticleSystem installationDust;
        private Mesh dustMesh;
        private readonly System.Random dustRandom = new System.Random();
        private BuildingRailUpgrade[] partUpgrades;
        private bool managedExternally;
        private readonly Transform[] pistonRods = new Transform[4];
        private readonly Transform[] pistonHeads = new Transform[4];
        private float pistonUnitsY;
        private float pistonBaseHeight;
        private float pistonHeadHeight;

        public Transform Building => building;
        public bool IsAvailable => building != null && (building.TryGetComponent<BuildingModelGeometry>(out var model)
            ? model.IsResidential : building.Find("Second_Floor") != null);
        public bool IsInstalled { get; private set; }
        public bool IsTransitioning { get; private set; }
        public float CurrentLift => currentLift;
        public event Action<BuildingRailUpgrade> StateChanged;

        private void Awake() => Initialize();

        private bool Initialize()
        {
            if (!IsAvailable) return false;
            if (initialized) return building != null;
            if (building == null) return false;
            originalLocalPosition = building.localPosition;
            if (building.TryGetComponent<BuildingModelGeometry>(out var model))
            {
                if (model.IsCollection)
                {
                    EnsureParts(model);
                    foreach (var part in partUpgrades) if (!part.IsAvailable) return false;
                    initialized = true; return true;
                }
                foundationBounds = model.FoundationBounds;
                initialized = true;
                return true;
            }
            // Use the foundation, so chimneys, flags and roof overhangs cannot enlarge the rails.
            var renderers = building.GetComponentsInChildren<MeshRenderer>();
            var found = false;
            foreach (var renderer in renderers)
                if (string.Equals(renderer.name, "Foundation", StringComparison.OrdinalIgnoreCase)) Encapsulate(renderer, ref found);
            if (!found)
                foreach (var renderer in renderers) Encapsulate(renderer, ref found);
            initialized = found;
            return initialized;
        }

        private void Encapsulate(MeshRenderer renderer, ref bool found)
        {
            var bounds = renderer.localBounds;
            var matrix = building.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                point = matrix.MultiplyPoint3x4(point);
                if (!found) { foundationBounds = new Bounds(point, Vector3.zero); found = true; }
                else foundationBounds.Encapsulate(point);
            }
        }

        public void SetInstalled(bool installed)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !Initialize() || installed == IsInstalled) return;
            if (partUpgrades != null)
            {
                foreach (var part in partUpgrades) part.SetInstalled(installed);
                IsInstalled = installed; IsTransitioning = true;
                StateChanged?.Invoke(this); return;
            }
            if (installed)
            {
                if (railMaterial == null || supportMaterial == null)
                {
                    Debug.LogWarning("Assign both rail materials before installing the rail system.", this);
                    return;
                }
                EnsureRails();
                railVisuals.SetActive(true);
            }
            else if (installationDust != null)
                installationDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            IsInstalled = installed;
            startingLift = currentLift;
            transitionAge = 0f;
            IsTransitioning = true;
            StateChanged?.Invoke(this);
        }

        private void Update()
        {
            if (!managedExternally && IsTransitioning) AdvanceTransition(Time.unscaledDeltaTime);
        }

        private void AdvanceTransition(float deltaTime)
        {
            if (!IsTransitioning) return;
            if (partUpgrades != null)
            {
                var transitioning = false; currentLift = 0f;
                foreach (var part in partUpgrades)
                { part.AdvanceTransition(deltaTime); transitioning |= part.IsTransitioning; currentLift += part.CurrentLift / partUpgrades.Length; }
                IsTransitioning = transitioning;
                if (!transitioning) StateChanged?.Invoke(this);
                return;
            }
            if (building == null) { ResetUpgrade(); return; }
            transitionAge += Mathf.Max(0f, deltaTime);
            var duration = IsInstalled ? transitionDuration : removalDuration;
            var progress = Mathf.Clamp01(transitionAge / Mathf.Max(0.1f, duration));
            float height = Mathf.Max(0.04f, liftHeight);
            float peak = Mathf.Max(startingLift, height * peakLiftMultiplier);
            if (!IsInstalled) currentLift = Mathf.Lerp(startingLift, 0f, Smooth(progress));
            else if (progress < 0.5f) currentLift = Mathf.Lerp(startingLift, peak, Smooth(progress / 0.5f));
            else if (progress < 0.6f) currentLift = peak;
            else currentLift = Mathf.Lerp(peak, height, Smooth((progress - 0.6f) / 0.4f));
            var localOffset = building.parent != null ? building.parent.InverseTransformVector(Vector3.up * currentLift) : Vector3.up * currentLift;
            building.localPosition = originalLocalPosition + localOffset;
            // Compensate for the lift: the rails stay on the ground below the rising building.
            if (railVisuals != null) railVisuals.transform.localPosition = -building.InverseTransformVector(Vector3.up * currentLift);
            UpdatePistons();
            if (progress < 1f) return;
            IsTransitioning = false;
            if (IsInstalled) PlayInstallationDust();
            if (!IsInstalled && railVisuals != null) railVisuals.SetActive(false);
            StateChanged?.Invoke(this);
        }

        private void EnsureRails()
        {
            if (railVisuals != null) return;
            railVisuals = new GameObject("Rail_System");
            railVisuals.transform.SetParent(building, false);
            var scale = building.lossyScale;
            var units = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)), 1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            var height = Mathf.Max(0.04f, liftHeight);
            var beamHeight = height * 0.65f;
            var flangeHeight = beamHeight * 0.25f;
            var shoeHeight = height - beamHeight;
            pistonUnitsY = units.y;
            pistonBaseHeight = beamHeight;
            pistonHeadHeight = shoeHeight;
            var width = Mathf.Min(railWidth * units.x, foundationBounds.size.x * 0.2f);
            var length = foundationBounds.size.z + endExtension * units.z * 2f;
            int pistonIndex = 0;
            for (var side = -1; side <= 1; side += 2)
            {
                var rail = new GameObject(side < 0 ? "Rail_Left" : "Rail_Right");
                rail.transform.SetParent(railVisuals.transform, false);
                rail.transform.localPosition = new Vector3(foundationBounds.center.x + foundationBounds.size.x * railSpacing * 0.5f * side, foundationBounds.min.y, foundationBounds.center.z);
                Cube(rail.transform, "Bottom_Flange", new Vector3(0f, flangeHeight * units.y * 0.5f, 0f), new Vector3(width, flangeHeight * units.y, length), railMaterial);
                Cube(rail.transform, "Web", new Vector3(0f, beamHeight * units.y * 0.5f, 0f), new Vector3(width * 0.4f, (beamHeight - flangeHeight * 2f) * units.y, length), supportMaterial);
                Cube(rail.transform, "Top_Flange", new Vector3(0f, (beamHeight - flangeHeight * 0.5f) * units.y, 0f), new Vector3(width, flangeHeight * units.y, length), railMaterial);
                for (var end = -1; end <= 1; end += 2)
                {
                    string prefix = end < 0 ? "Front" : "Back";
                    float z = foundationBounds.size.z * 0.3f * end;
                    var sleeve = Primitive(PrimitiveType.Cylinder, rail.transform, prefix + "_Piston_Sleeve",
                        new Vector3(0f, (beamHeight + shoeHeight * 0.22f) * units.y, z),
                        new Vector3(width * 0.85f, shoeHeight * 0.22f * units.y, width * 0.85f), supportMaterial);
                    sleeve.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    pistonRods[pistonIndex] = Primitive(PrimitiveType.Cylinder, rail.transform, prefix + "_Piston_Rod",
                        new Vector3(0f, beamHeight * units.y, z), new Vector3(width * 0.48f, 0.001f, width * 0.48f), railMaterial);
                    pistonHeads[pistonIndex] = Primitive(PrimitiveType.Cube, rail.transform, prefix + "_Shoe",
                        new Vector3(0f, (height - shoeHeight * 0.5f) * units.y, z),
                        new Vector3(width * 1.35f, shoeHeight * units.y, 0.13f * units.z), supportMaterial);
                    pistonIndex++;
                }
            }
            UpdatePistons();
        }

        private static void Cube(Transform parent, string label, Vector3 position, Vector3 size, Material material)
            => Primitive(PrimitiveType.Cube, parent, label, position, size, material);

        private static Transform Primitive(PrimitiveType type, Transform parent, string label, Vector3 position, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(type);
            cube.name = label;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = cube.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            return cube.transform;
        }

        private void UpdatePistons()
        {
            float headHeight = Mathf.Min(pistonHeadHeight, currentLift * 0.35f);
            float rodHeight = Mathf.Max(0.001f, currentLift - headHeight - pistonBaseHeight);
            for (int i = 0; i < pistonRods.Length; i++)
            {
                if (pistonRods[i] == null || pistonHeads[i] == null) continue;
                var rod = pistonRods[i];
                var head = pistonHeads[i];
                rod.localPosition = new Vector3(rod.localPosition.x,
                    (pistonBaseHeight + rodHeight * 0.5f) * pistonUnitsY, rod.localPosition.z);
                rod.localScale = new Vector3(rod.localScale.x, rodHeight * pistonUnitsY * 0.5f, rod.localScale.z);
                head.localPosition = new Vector3(head.localPosition.x,
                    (currentLift - headHeight * 0.5f) * pistonUnitsY, head.localPosition.z);
                head.localScale = new Vector3(head.localScale.x, Mathf.Max(0.001f, headHeight) * pistonUnitsY, head.localScale.z);
            }
        }

        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private void PlayInstallationDust()
        {
            if (installationDustMaterial == null || dustAmount <= 0f) return;
            if (installationDust == null)
                installationDust = ConstructionDustPuff.Create(transform, installationDustMaterial, out dustMesh);
            ConstructionDustPuff.Emit(installationDust, building, foundationBounds, dustAmount * dustMultiplier, dustSize,
                dustLifetime, dustRandom, -Vector3.up * currentLift);
        }

        public void ResetUpgrade()
        {
            if (partUpgrades != null) foreach (var part in partUpgrades) if (part != null) part.ResetUpgrade();
            if (initialized && building != null) building.localPosition = originalLocalPosition;
            IsInstalled = false;
            IsTransitioning = false;
            currentLift = 0f;
            if (installationDust != null) installationDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (railVisuals != null)
            {
                railVisuals.transform.localPosition = Vector3.zero;
                railVisuals.SetActive(false);
            }
            StateChanged?.Invoke(this);
        }

        private void OnDisable() => ResetUpgrade();
        private void EnsureParts(BuildingModelGeometry model)
        {
            if (partUpgrades != null) return;
            partUpgrades = new BuildingRailUpgrade[model.Parts.Length];
            for (var i = 0; i < partUpgrades.Length; i++)
            {
                var child = new GameObject("Rails_" + model.Parts[i].name); child.transform.SetParent(transform, false);
                var part = child.AddComponent<BuildingRailUpgrade>(); part.managedExternally = true;
                part.building = model.Parts[i].transform; part.liftHeight = liftHeight; part.railWidth = railWidth;
                part.endExtension = endExtension; part.railSpacing = railSpacing; part.transitionDuration = transitionDuration;
                part.peakLiftMultiplier = peakLiftMultiplier; part.removalDuration = removalDuration;
                part.railMaterial = railMaterial; part.supportMaterial = supportMaterial;
                part.installationDustMaterial = installationDustMaterial; part.dustAmount = dustAmount;
                part.dustMultiplier = dustMultiplier;
                part.dustSize = dustSize; part.dustLifetime = dustLifetime; partUpgrades[i] = part;
            }
        }
        private void OnDestroy()
        {
            ResetUpgrade();
            if (railVisuals != null) Destroy(railVisuals);
            if (dustMesh != null) Destroy(dustMesh);
            if (partUpgrades != null) foreach (var part in partUpgrades) if (part != null) Destroy(part.gameObject);
        }
    }
}
