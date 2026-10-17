using UnityEngine;
using UnityEngine.Rendering;

namespace CivilGameJam.Disasters
{
    /// <summary>Moves the artist's saved tsunami mesh while the existing flood simulation handles the city.</summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredTsunamiWave : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Dalganın şehre ilerleme hızı (birim/saniye). Play Mode sırasında da değiştirilebilir.")]
        [SerializeField, Min(.01f)] private float movementSpeed = 1.1f;
        [SerializeField] private Vector3 startPosition = new Vector3(-9.536f, -.32f, -5.52f);
        [SerializeField] private Vector3 endPosition = new Vector3(-9.536f, -.32f, 48.5f);
        [Tooltip("Frame used by the flood simulation. Start and End Position remain local to this wave's parent.")]
        [SerializeField] private Transform simulationFrame;
        [Header("Appearance")]
        [SerializeField] private MeshRenderer surface;
        private Color[] colors;
        private MaterialPropertyBlock[] properties;
        private Material[] originalMaterials;
        private Material[] runtimeMaterials;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public MeshRenderer Surface => surface;
        public float MovementSpeed => Mathf.Max(.01f, movementSpeed);
        public Vector3 StartPosition => startPosition;
        public Vector3 EndPosition => endPosition;
        public float StartDistance => FrontAt(startPosition);
        public float EndDistance => FrontAt(endPosition);
        public float FrontDistance => FrontAt(transform.localPosition);
        public bool IsVisible => gameObject.activeSelf;

        public void SetWave(float front, float seaLevel, float emergence, float fade)
        {
            if (surface == null) return;
            if (colors == null)
            {
                var materials = surface.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    if (materials[i].GetFloat("_Surface") != 0f || materials[i].GetFloat("_Cull") != 0f || materials[i].GetFloat("_ZWrite") != 1f)
                    {
                        originalMaterials = materials;
                        runtimeMaterials = new Material[materials.Length];
                        for (var slot = 0; slot < materials.Length; slot++)
                        {
                            runtimeMaterials[slot] = new Material(materials[slot]) { hideFlags = HideFlags.DontSave };
                            ConfigureOpaqueMaterial(runtimeMaterials[slot]);
                        }
                        surface.sharedMaterials = materials = runtimeMaterials;
                        break;
                    }
                colors = new Color[materials.Length]; properties = new MaterialPropertyBlock[materials.Length];
                for (var i = 0; i < materials.Length; i++)
                {
                    colors[i] = materials[i].GetColor(BaseColor);
                    colors[i].a = 1f;
                    properties[i] = new MaterialPropertyBlock();
                }
            }
            transform.localPosition = Vector3.Lerp(startPosition, endPosition,
                Mathf.InverseLerp(StartDistance, EndDistance, front));
            for (var i = 0; i < colors.Length; i++)
            {
                properties[i].SetColor(BaseColor, colors[i]); surface.SetPropertyBlock(properties[i], i);
            }
            gameObject.SetActive(fade > .001f);
        }

        public void Hide() => gameObject.SetActive(false);

        private float FrontAt(Vector3 position)
        {
            // Legacy waves already use the simulation's local Z as their pivot/front coordinate.
            if (simulationFrame == null || surface == null) return position.z;
            var parentMatrix = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
            var surfaceOffset = surface.transform == transform ? Matrix4x4.identity :
                transform.worldToLocalMatrix * surface.transform.localToWorldMatrix;
            var matrix = simulationFrame.worldToLocalMatrix * parentMatrix *
                Matrix4x4.TRS(position, transform.localRotation, transform.localScale) * surfaceOffset;
            var bounds = surface.localBounds;
            var front = float.NegativeInfinity;
            for (var corner = 0; corner < 8; corner++)
                front = Mathf.Max(front, matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f))).z);
            return front;
        }

        public static void ConfigureOpaqueMaterial(Material material)
        {
            var color = material.GetColor("_BaseColor"); color.a = 1f;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 0f); material.SetFloat("_Cull", 0f); material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_AlphaClip", 0f);
            foreach (var keyword in new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_ALPHATEST_ON" })
                material.DisableKeyword(keyword);
            material.SetOverrideTag("RenderType", "Opaque"); material.renderQueue = (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("DepthOnly", true); material.doubleSidedGI = true;
        }

        private void OnDestroy()
        {
            if (runtimeMaterials == null) return;
            if (surface != null) surface.sharedMaterials = originalMaterials;
            foreach (var material in runtimeMaterials)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
