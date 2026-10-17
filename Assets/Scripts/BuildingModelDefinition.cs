using UnityEngine;

namespace CivilGameJam
{
    /// <summary>Surface information baked from an imported building, in its gameplay root's local space.</summary>
    public sealed class BuildingModelDefinition : ScriptableObject
    {
        [SerializeField] private bool residential;
        [SerializeField] private Mesh closedMesh;
        [SerializeField] private Mesh openGroundFloorMesh;
        [SerializeField] private Bounds bodyBounds;
        [SerializeField] private Bounds foundationBounds;
        [SerializeField] private Bounds roofBounds;
        [SerializeField] private float groundFloorHeight;
        [SerializeField] private Vector3 sprinklerAnchor;
        [SerializeField] private Vector3[] roofTriangles = new Vector3[0];

        public bool IsResidential => residential;
        public Mesh ClosedMesh => closedMesh;
        public Mesh OpenGroundFloorMesh => openGroundFloorMesh;
        public Bounds BodyBounds => bodyBounds;
        public Bounds FoundationBounds => foundationBounds;
        public Bounds RoofBounds => roofBounds;
        public float GroundFloorHeight => groundFloorHeight;
        public Vector3 SprinklerAnchor => sprinklerAnchor;
        public Vector3[] RoofTriangles => roofTriangles;
    }
}
