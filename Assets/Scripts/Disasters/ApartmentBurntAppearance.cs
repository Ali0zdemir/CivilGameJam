using System;
using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>Uses the imported burnt apartment art on both normal and elevated bodies.</summary>
    [DisallowMultipleComponent]
    public sealed class ApartmentBurntAppearance : MonoBehaviour
    {
        [Serializable]
        private sealed class Variant
        {
            public MeshFilter body;
            public Mesh burntMesh;
            public Material[] burntMaterials = Array.Empty<Material>();
            [NonSerialized] public Mesh originalMesh;
            [NonSerialized] public Material[] originalMaterials;
            [NonSerialized] public Quaternion originalRotation;
            [NonSerialized] public MaterialPropertyBlock originalProperties;
            [NonSerialized] public MaterialPropertyBlock[] originalMaterialProperties;
        }

        [SerializeField] private Variant[] variants = Array.Empty<Variant>();
        [SerializeField] private Quaternion burntTilt = Quaternion.identity;
        public bool IsBurnt { get; private set; }

        public Color TargetColor(MeshRenderer renderer, int materialIndex, Color fallback)
        {
            foreach (var variant in variants)
                if (variant.body != null && variant.body.gameObject == renderer.gameObject &&
                    materialIndex < variant.burntMaterials.Length && variant.burntMaterials[materialIndex] != null)
                    return variant.burntMaterials[materialIndex].GetColor("_BaseColor");
            return fallback;
        }

        public void SetBurnt(bool burnt)
        {
            if (IsBurnt == burnt) return;
            foreach (var variant in variants)
            {
                if (variant.body == null || !variant.body.TryGetComponent<MeshRenderer>(out var renderer)) continue;
                if (burnt)
                {
                    variant.originalMesh = variant.body.sharedMesh;
                    variant.originalMaterials = renderer.sharedMaterials;
                    variant.originalRotation = variant.body.transform.localRotation;
                    variant.originalProperties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(variant.originalProperties);
                    variant.originalMaterialProperties = new MaterialPropertyBlock[variant.originalMaterials.Length];
                    for (var i = 0; i < variant.originalMaterials.Length; i++)
                    {
                        variant.originalMaterialProperties[i] = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(variant.originalMaterialProperties[i], i);
                        renderer.SetPropertyBlock(null, i);
                    }
                    renderer.SetPropertyBlock(null);
                    if (variant.burntMesh != null) variant.body.sharedMesh = variant.burntMesh;
                    renderer.sharedMaterials = variant.burntMaterials;
                    variant.body.transform.localRotation = variant.originalRotation * burntTilt;
                }
                else
                {
                    variant.body.sharedMesh = variant.originalMesh;
                    renderer.sharedMaterials = variant.originalMaterials;
                    variant.body.transform.localRotation = variant.originalRotation;
                    renderer.SetPropertyBlock(variant.originalProperties.isEmpty ? null : variant.originalProperties);
                    for (var i = 0; i < variant.originalMaterialProperties.Length; i++)
                        renderer.SetPropertyBlock(variant.originalMaterialProperties[i].isEmpty ? null : variant.originalMaterialProperties[i], i);
                }
            }
            IsBurnt = burnt;
        }
    }
}
