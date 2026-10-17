using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.Cards
{
    // Small UI lights and smoke are authored in prefabs; no textures or runtime objects are needed.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardSoftGraphic : MaskableGraphic
    {
        public enum Shape { Puff, Beam }
        [SerializeField] private Shape shape;
        private static readonly float[] Stops = { 0f, 0.2f, 0.5f, 0.8f, 1f };
        private static readonly float[] Radii = { 0.45f, 0.75f, 1f };

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            if (shape == Shape.Beam)
            {
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 5; x++)
                    {
                        var tint = color;
                        tint.a *= Mathf.Sin(Stops[x] * Mathf.PI) * Mathf.Sin(Stops[y] * Mathf.PI);
                        mesh.AddVert(bounds.min + Vector2.Scale(bounds.size, new Vector2(Stops[x], Stops[y])), tint, Vector2.zero);
                    }
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        int first = y * 5 + x;
                        mesh.AddTriangle(first, first + 5, first + 6);
                        mesh.AddTriangle(first, first + 6, first + 1);
                    }
                return;
            }
            const int segments = 28;
            var center = color;
            mesh.AddVert(bounds.center, center, Vector2.zero);
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    var tint = color;
                    tint.a *= ring == 0 ? 0.7f : ring == 1 ? 0.24f : 0f;
                    mesh.AddVert(bounds.center + Vector2.Scale(bounds.size * Radii[ring] * 0.5f,
                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))), tint, Vector2.zero);
                }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.AddTriangle(0, i + 1, next + 1);
                for (int ring = 0; ring < 2; ring++)
                {
                    int first = 1 + ring * segments;
                    mesh.AddTriangle(first + i, first + segments + i, first + segments + next);
                    mesh.AddTriangle(first + i, first + segments + next, first + next);
                }
            }
        }
    }
}
