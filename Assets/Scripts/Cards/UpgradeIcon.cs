using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UpgradeIcon : MaskableGraphic
    {
        [SerializeField] private UpgradeKind kind;
        [SerializeField] private Sprite railSystemSprite;
        [SerializeField] private Sprite breakwaterSprite;
        [SerializeField] private Sprite fireSprite;
        public Sprite Artwork => kind == UpgradeKind.RailSystem ? railSystemSprite :
            kind == UpgradeKind.Breakwater ? breakwaterSprite : kind == UpgradeKind.Fire ? fireSprite : null;
        public override Texture mainTexture => Artwork != null ? Artwork.texture : base.mainTexture;
        public UpgradeKind Kind
        {
            get => kind;
            set { if (kind == value) return; kind = value; SetVerticesDirty(); SetMaterialDirty(); }
        }
        public static Color Accent(UpgradeKind type) => type == UpgradeKind.RailSystem ? new Color(0.27f, 0.48f, 0.55f) :
            type == UpgradeKind.Breakwater ? new Color(0.18f, 0.56f, 0.74f) : new Color(0.86f, 0.32f, 0.16f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (Artwork != null)
            {
                var rect = GetPixelAdjustedRect();
                Vector2 size = Artwork.rect.size;
                float scale = Mathf.Min(rect.width / size.x, rect.height / size.y);
                Vector2 half = size * scale * 0.5f;
                Vector2 center = rect.center;
                Vector4 uv = DataUtility.GetOuterUV(Artwork);
                mesh.AddVert(center + new Vector2(-half.x, -half.y), color, new Vector2(uv.x, uv.y));
                mesh.AddVert(center + new Vector2(-half.x, half.y), color, new Vector2(uv.x, uv.w));
                mesh.AddVert(center + new Vector2(half.x, half.y), color, new Vector2(uv.z, uv.w));
                mesh.AddVert(center + new Vector2(half.x, -half.y), color, new Vector2(uv.z, uv.y));
                mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(0, 2, 3);
                return;
            }
            var dark = new Color(0.23f, 0.25f, 0.25f);
            var accent = Accent(kind);
            if (kind == UpgradeKind.RailSystem)
            {
                for (int i = 0; i < 5; i++) Q(mesh, 0.15f, 0.12f + i * 0.16f, 0.7f, 0.09f, new Color(0.52f, 0.33f, 0.15f));
                Q(mesh, 0.28f, 0.05f, 0.1f, 0.9f, dark); Q(mesh, 0.62f, 0.05f, 0.1f, 0.9f, dark);
                Q(mesh, 0.29f, 0.05f, 0.035f, 0.9f, accent); Q(mesh, 0.63f, 0.05f, 0.035f, 0.9f, accent);
            }
            else if (kind == UpgradeKind.Breakwater)
            {
                for (int i = 0; i < 3; i++)
                { Q(mesh, 0.05f + i * 0.3f, 0.12f, 0.25f, 0.07f, accent); Q(mesh, 0.12f + i * 0.27f, 0.25f, 0.2f, 0.06f, accent); }
                Q(mesh, 0.18f, 0.38f, 0.66f, 0.14f, dark);
                Q(mesh, 0.27f, 0.55f, 0.52f, 0.13f, new Color(0.47f, 0.47f, 0.39f));
                Q(mesh, 0.39f, 0.71f, 0.3f, 0.13f, dark);
            }
            else if (kind == UpgradeKind.Fire)
            {
                T(mesh, new Vector2(0.1f, 0.17f), new Vector2(0.48f, 0.96f), new Vector2(0.88f, 0.17f), accent);
                T(mesh, new Vector2(0.18f, 0.17f), new Vector2(0.82f, 0.7f), new Vector2(0.88f, 0.17f), accent);
                T(mesh, new Vector2(0.3f, 0.17f), new Vector2(0.53f, 0.63f), new Vector2(0.7f, 0.17f), new Color(1f, 0.76f, 0.2f));
            }
        }
        private Vector3 P(Vector2 point) => new Vector3(rectTransform.rect.xMin + point.x * rectTransform.rect.width,
            rectTransform.rect.yMin + point.y * rectTransform.rect.height);
        private void Q(VertexHelper m, float x, float y, float w, float h, Color tint)
        {
            int n = m.currentVertCount;
            m.AddVert(P(new Vector2(x,y)), tint * color, Vector2.zero); m.AddVert(P(new Vector2(x,y+h)), tint * color, Vector2.zero);
            m.AddVert(P(new Vector2(x+w,y+h)), tint * color, Vector2.zero); m.AddVert(P(new Vector2(x+w,y)), tint * color, Vector2.zero);
            m.AddTriangle(n,n+1,n+2); m.AddTriangle(n,n+2,n+3);
        }
        private void T(VertexHelper m, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int n = m.currentVertCount;
            m.AddVert(P(a), tint * color, Vector2.zero); m.AddVert(P(b), tint * color, Vector2.zero); m.AddVert(P(c), tint * color, Vector2.zero);
            m.AddTriangle(n,n+1,n+2);
        }
    }
}
