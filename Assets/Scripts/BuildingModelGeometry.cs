using System.Collections.Generic;
using UnityEngine;

namespace CivilGameJam
{
    /// <summary>Connects imported model surfaces to the house upgrades and disaster effects.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildingModelGeometry : MonoBehaviour
    {
        [SerializeField] private BuildingModelDefinition definition;
        [SerializeField] private MeshFilter modelMesh;
        [SerializeField] private BuildingModelGeometry[] parts = new BuildingModelGeometry[0];
        [SerializeField] private Upgrades.ApartmentUpgradeModel apartmentModel;
        [SerializeField] private BuildingModelDefinition elevatedDefinition;
        [SerializeField] private MeshFilter elevatedMesh;
        private bool groundFloorOpen;

        private BuildingModelDefinition Surface => apartmentModel != null && apartmentModel.IsElevated ? elevatedDefinition : definition;
        private MeshFilter VisibleMesh => apartmentModel != null && apartmentModel.IsElevated ? elevatedMesh : modelMesh;

        public bool IsCollection => parts.Length > 0;
        public BuildingModelGeometry[] Parts => parts;
        public bool IsResidential
        {
            get
            {
                if (!IsCollection) return Surface != null && Surface.IsResidential;
                foreach (var part in parts) if (part == null || !part.IsResidential) return false;
                return true;
            }
        }
        public bool CanOpenGroundFloor
        {
            get
            {
                if (!IsCollection) return apartmentModel != null || IsResidential && modelMesh != null && Surface.OpenGroundFloorMesh != null;
                foreach (var part in parts) if (part == null || !part.CanOpenGroundFloor) return false;
                return true;
            }
        }
        public Bounds FoundationBounds => IsCollection ? CollectionBounds(1) : Surface.FoundationBounds;
        public Bounds RoofBounds => IsCollection ? CollectionBounds(2) : Surface.RoofBounds;
        public Vector3 SprinklerAnchor => IsCollection ? transform.InverseTransformPoint(parts[0].transform.TransformPoint(parts[0].SprinklerAnchor)) : Surface.SprinklerAnchor;
        public float GroundFloorHeight => IsCollection ? parts[0].GroundFloorHeight : Surface.GroundFloorHeight;
        public Bounds BodyBounds
        {
            get
            {
                if (IsCollection) return CollectionBounds(0);
                var bounds = Surface.BodyBounds;
                if (groundFloorOpen) bounds.SetMinMax(bounds.min + Vector3.up * GroundFloorHeight, bounds.max);
                return bounds;
            }
        }
        public Bounds WorldBounds => TransformBounds(transform.localToWorldMatrix, IsCollection ? CollectionBounds(0) : Surface.BodyBounds);
        public Bounds WorldFoundationBounds => TransformBounds(transform.localToWorldMatrix, FoundationBounds);

        public void SetGroundFloorOpen(bool open)
        {
            if (apartmentModel != null) { apartmentModel.SetElevated(open); return; }
            if (IsCollection) { foreach (var part in parts) part.SetGroundFloorOpen(open); return; }
            if (!CanOpenGroundFloor) return;
            modelMesh.sharedMesh = open ? definition.OpenGroundFloorMesh : definition.ClosedMesh;
            groundFloorOpen = open;
        }

        public bool TryProjectRoof(Vector3 point, out Vector3 surface, out Vector3 normal)
        {
            if (IsCollection)
            {
                var found = false;
                surface = point; surface.y = float.NegativeInfinity; normal = Vector3.up;
                foreach (var part in parts)
                {
                    if (!part.TryProjectRoof(part.transform.InverseTransformPoint(transform.TransformPoint(point)), out var hit, out var axis)) continue;
                    hit = transform.InverseTransformPoint(part.transform.TransformPoint(hit));
                    if (hit.y <= surface.y) continue;
                    surface = hit; normal = transform.InverseTransformDirection(part.transform.TransformDirection(axis)); found = true;
                }
                return found;
            }
            var triangles = Surface.RoofTriangles;
            var highest = float.NegativeInfinity;
            normal = Vector3.up;
            for (var i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = triangles[i]; var b = triangles[i + 1]; var c = triangles[i + 2];
                var denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < .0000001f) continue;
                var u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.z - c.z)) / denominator;
                var v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.z - c.z)) / denominator;
                if (u < -.001f || v < -.001f || u + v > 1.001f) continue;
                var height = a.y * u + b.y * v + c.y * (1f - u - v);
                if (height <= highest) continue;
                highest = height; normal = Vector3.Cross(b - a, c - a).normalized;
                if (normal.y < 0f) normal = -normal;
            }
            surface = new Vector3(point.x, float.IsNegativeInfinity(highest) ? RoofBounds.min.y : highest, point.z);
            return !float.IsNegativeInfinity(highest);
        }

        public void AppendRenderers(List<MeshRenderer> output, bool includeInactiveVariants = false)
        {
            if (IsCollection) { foreach (var part in parts) part.AppendRenderers(output, includeInactiveVariants); return; }
            if (VisibleMesh != null && VisibleMesh.TryGetComponent<MeshRenderer>(out var renderer)) output.Add(renderer);
            if (!includeInactiveVariants) return;
            var other = VisibleMesh == modelMesh ? elevatedMesh : modelMesh;
            if (other != null && other != VisibleMesh && other.TryGetComponent<MeshRenderer>(out var alternate)) output.Add(alternate);
        }

        public void AppendFoundationBounds(Transform relativeTo, List<Bounds> output)
        {
            if (IsCollection) { foreach (var part in parts) part.AppendFoundationBounds(relativeTo, output); return; }
            output.Add(TransformBounds(relativeTo.worldToLocalMatrix * transform.localToWorldMatrix, FoundationBounds));
        }

        private Bounds CollectionBounds(int surface)
        {
            var bounds = new Bounds(); var first = true;
            foreach (var part in parts)
            {
                var source = surface == 1 ? part.FoundationBounds : surface == 2 ? part.RoofBounds : part.BodyBounds;
                var local = TransformBounds(transform.worldToLocalMatrix * part.transform.localToWorldMatrix, source);
                if (first) { bounds = local; first = false; } else bounds.Encapsulate(local);
            }
            return bounds;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds source)
        {
            var bounds = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
            for (var i = 0; i < 8; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            return bounds;
        }
    }
}
