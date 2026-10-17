using System;
using System.Collections.Generic;
using UnityEngine;

namespace CivilGameJam.Upgrades
{
    /// <summary>Selects the authored elevated apartment, with support for legacy open ground floors.</summary>
    [DisallowMultipleComponent]
    public sealed class HouseColumnUpgrade : MonoBehaviour
    {
        [SerializeField] private Transform building;
        [SerializeField] private Material columnMaterial;
        [SerializeField] private Material beamMaterial;
        [SerializeField, Range(0.06f, 0.2f)] private float columnWidth = 0.12f;

        [Header("Installation dust")]
        [SerializeField] private Material installationDustMaterial;
        [SerializeField, Range(0f, 2f)] private float dustAmount = 1f;
        [SerializeField, Range(0.1f, 0.5f)] private float dustSize = 0.24f;
        [SerializeField, Range(0.3f, 1.2f)] private float dustLifetime = 0.7f;

        private Transform walls;
        private BuildingModelGeometry modelGeometry;
        private Vector3 originalWallPosition;
        private Vector3 originalWallScale;
        private GameObject[] groundFloorParts;
        private bool[] originalPartStates;
        private GameObject columnVisuals;
        private readonly List<MeshRenderer> columns = new List<MeshRenderer>(6);
        private readonly List<Vector3> columnFeet = new List<Vector3>(6);
        private readonly System.Random dustRandom = new System.Random();
        private ParticleSystem installationDust;
        private Mesh dustMesh;
        private float floorHeight;
        private bool initialized;
        private ApartmentUpgradeModel apartmentModel;
        private ApartmentConstructionDust apartmentDust;
        private HouseColumnUpgrade[] partUpgrades;

        public Transform Building => building;
        public bool IsInstalled { get; private set; }
        public bool IsAvailable => Initialize();
        public bool IsTransitioning => apartmentModel != null && apartmentModel.IsTransitioning;
        public bool UsesElevatedApartment => building != null && building.GetComponent<ApartmentUpgradeModel>() != null;
        public event Action<HouseColumnUpgrade> StateChanged;
        public static event Action<HouseColumnUpgrade> GeometryChanged;

        private void Awake() => Initialize();

        private bool Initialize()
        {
            if (initialized) return building != null && (walls != null || modelGeometry != null);
            if (building == null) return false;
            if (building.TryGetComponent(out apartmentModel))
            {
                modelGeometry = building.GetComponent<BuildingModelGeometry>();
                groundFloorParts = Array.Empty<GameObject>();
                initialized = true;
                return true;
            }
            if (building.TryGetComponent(out modelGeometry))
            {
                if (!modelGeometry.CanOpenGroundFloor) return false;
                if (modelGeometry.IsCollection)
                {
                    EnsureParts();
                    foreach (var part in partUpgrades) if (!part.IsAvailable) return false;
                    initialized = true; return true;
                }
                originalWallPosition = modelGeometry.BodyBounds.center;
                originalWallScale = modelGeometry.BodyBounds.size;
                floorHeight = modelGeometry.GroundFloorHeight;
                groundFloorParts = Array.Empty<GameObject>();
                originalPartStates = Array.Empty<bool>();
                initialized = true;
                return true;
            }
            walls = building.Find("Structure/Walls");
            var upperFloor = building.Find("Second_Floor");
            var entrance = building.Find("Windows_and_Entrance");
            var sideWindows = building.Find("Side_Windows");
            if (walls == null || upperFloor == null || entrance == null || sideWindows == null) return false;
            floorHeight = upperFloor.localPosition.y;
            if (floorHeight <= 0f || walls.localScale.y <= floorHeight) return false;
            originalWallPosition = walls.localPosition;
            originalWallScale = walls.localScale;
            groundFloorParts = new[] { entrance.gameObject, sideWindows.gameObject };
            originalPartStates = new bool[groundFloorParts.Length];
            for (var i = 0; i < groundFloorParts.Length; i++) originalPartStates[i] = groundFloorParts[i].activeSelf;
            initialized = true;
            return true;
        }

        public void SetInstalled(bool installed)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || installed == IsInstalled || !Initialize()) return;
            if (apartmentModel != null)
            {
                IsInstalled = installed;
                if (!installed) apartmentDust?.Stop();
                apartmentModel.SetElevated(installed);
                StateChanged?.Invoke(this);
                return;
            }
            if (!installed) { ResetUpgrade(); return; }
            if (partUpgrades != null)
            {
                foreach (var part in partUpgrades) part.SetInstalled(true);
                IsInstalled = true; StateChanged?.Invoke(this); GeometryChanged?.Invoke(this); return;
            }
            if (columnMaterial == null || beamMaterial == null)
            {
                Debug.LogWarning("Assign the house column and beam materials before opening the ground floor.", this);
                return;
            }
            EnsureColumns();
            // Shorten the existing wall from the bottom; the upper storey keeps its material overrides.
            if (modelGeometry != null) modelGeometry.SetGroundFloorOpen(true);
            else
            {
                walls.localScale = originalWallScale - Vector3.up * floorHeight;
                walls.localPosition = originalWallPosition + Vector3.up * (floorHeight * 0.5f);
            }
            foreach (var part in groundFloorParts) part.SetActive(false);
            columnVisuals.SetActive(true);
            IsInstalled = true;
            PlayInstallationDust();
            StateChanged?.Invoke(this);
            GeometryChanged?.Invoke(this);
        }

        private void EnsureColumns()
        {
            if (columnVisuals != null) return;
            columnVisuals = new GameObject("Open_Ground_Floor");
            columnVisuals.SetActive(false);
            columnVisuals.transform.SetParent(building, false);
            var bottom = originalWallPosition.y - originalWallScale.y * 0.5f;
            var ceiling = bottom + floorHeight;
            var beamHeight = Mathf.Min(0.1f, floorHeight * 0.15f);
            var height = floorHeight - beamHeight;
            var width = Mathf.Min(columnWidth, Mathf.Min(originalWallScale.x, originalWallScale.z) * 0.18f);
            var offsetX = (originalWallScale.x - width) * 0.5f;
            var offsetZ = (originalWallScale.z - width) * 0.5f;
            for (var side = -1; side <= 1; side += 2)
            for (var row = -1; row <= 1; row++)
            {
                var position = new Vector3(originalWallPosition.x + side * offsetX, bottom + height * 0.5f,
                    originalWallPosition.z + row * offsetZ);
                columns.Add(Cube("Column_" + (side < 0 ? "Left_" : "Right_") + (row + 2),
                    position, new Vector3(width, height, width), columnMaterial));
                columnFeet.Add(new Vector3(position.x, bottom, position.z));
            }
            Cube("Upper_Floor_Support", new Vector3(originalWallPosition.x, ceiling - beamHeight * 0.5f, originalWallPosition.z),
                new Vector3(originalWallScale.x, beamHeight, originalWallScale.z), beamMaterial);
        }

        private MeshRenderer Cube(string label, Vector3 position, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = label;
            cube.transform.SetParent(columnVisuals.transform, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var collider = cube.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            return renderer;
        }

        public void AppendColumnBounds(Transform relativeTo, List<Bounds> output)
        {
            if (!IsInstalled) return;
            if (apartmentModel != null) { apartmentModel.AppendSupports(relativeTo, output); return; }
            if (partUpgrades != null) { foreach (var part in partUpgrades) part.AppendColumnBounds(relativeTo, output); return; }
            foreach (var column in columns)
            {
                if (column == null) continue;
                var source = column.localBounds;
                var matrix = relativeTo.worldToLocalMatrix * column.transform.localToWorldMatrix;
                var bounds = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
                for (var corner = 0; corner < 8; corner++)
                    bounds.Encapsulate(matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f))));
                output.Add(bounds);
            }
        }

        private void PlayInstallationDust()
        {
            if (installationDustMaterial == null || dustAmount <= 0f) return;
            if (apartmentModel != null)
            {
                if (apartmentDust == null) apartmentDust = new ApartmentConstructionDust(transform, modelGeometry, installationDustMaterial);
                apartmentDust.Emit(dustAmount, dustSize, dustLifetime, building.position - apartmentModel.MotionRoot.position);
                return;
            }
            if (installationDust == null)
                installationDust = ConstructionDustPuff.Create(transform, installationDustMaterial, out dustMesh);
            var bounds = apartmentModel != null ? modelGeometry.FoundationBounds : new Bounds(originalWallPosition, originalWallScale);
            ConstructionDustPuff.Emit(installationDust, building, bounds,
                dustAmount, dustSize, dustLifetime, dustRandom, Vector3.zero, columnFeet);
        }

        public void ResetUpgrade()
        {
            var changed = IsInstalled;
            apartmentDust?.Stop();
            if (apartmentModel != null) apartmentModel.SetElevated(false, false);
            else if (partUpgrades != null)
            {
                foreach (var part in partUpgrades) if (part != null) part.ResetUpgrade();
            }
            else if (initialized)
            {
                if (modelGeometry != null) modelGeometry.SetGroundFloorOpen(false);
                if (walls != null) { walls.localPosition = originalWallPosition; walls.localScale = originalWallScale; }
                for (var i = 0; i < groundFloorParts.Length; i++)
                    if (groundFloorParts[i] != null) groundFloorParts[i].SetActive(originalPartStates[i]);
            }
            if (columnVisuals != null) columnVisuals.SetActive(false);
            if (installationDust != null) installationDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            IsInstalled = false;
            if (!changed) return;
            StateChanged?.Invoke(this);
            GeometryChanged?.Invoke(this);
        }

        private void OnModelChanged() => GeometryChanged?.Invoke(this);
        private void OnInstallationCompleted() { if (IsInstalled) PlayInstallationDust(); }
        private void OnEnable()
        {
            Initialize();
            if (apartmentModel == null) return;
            apartmentModel.ModelChanged += OnModelChanged;
            apartmentModel.InstallationCompleted += OnInstallationCompleted;
        }
        private void OnDisable()
        {
            if (apartmentModel != null)
            {
                apartmentModel.ModelChanged -= OnModelChanged;
                apartmentModel.InstallationCompleted -= OnInstallationCompleted;
            }
            ResetUpgrade();
        }
        private void EnsureParts()
        {
            if (partUpgrades != null) return;
            partUpgrades = new HouseColumnUpgrade[modelGeometry.Parts.Length];
            for (var i = 0; i < partUpgrades.Length; i++)
            {
                var child = new GameObject("Columns_" + modelGeometry.Parts[i].name); child.transform.SetParent(transform, false);
                var part = child.AddComponent<HouseColumnUpgrade>(); part.building = modelGeometry.Parts[i].transform;
                part.columnMaterial = columnMaterial; part.beamMaterial = beamMaterial; part.columnWidth = columnWidth;
                part.installationDustMaterial = installationDustMaterial; part.dustAmount = dustAmount;
                part.dustSize = dustSize; part.dustLifetime = dustLifetime; partUpgrades[i] = part;
            }
        }
        private void OnDestroy()
        {
            // OnDisable already restored authored variants; do not reactivate a hierarchy being destroyed.
            if (apartmentModel == null) ResetUpgrade();
            apartmentDust?.Dispose();
            if (columnVisuals != null) Destroy(columnVisuals);
            if (dustMesh != null) Destroy(dustMesh);
            if (partUpgrades != null) foreach (var part in partUpgrades) if (part != null) Destroy(part.gameObject);
        }
    }
}
