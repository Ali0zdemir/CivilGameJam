using CivilGameJam.Upgrades;
using UnityEngine;
using UnityEngine.Rendering;

namespace CivilGameJam.Disasters
{
    [DisallowMultipleComponent]
    public sealed class MonsterFootstepFeedback : MonoBehaviour
    {
        [SerializeField] private TsunamiTestController tsunami;
        [SerializeField] private Transform seaSurface;
        [SerializeField] private Material constructionDustMaterial;
        [SerializeField, Range(.05f, .6f)] private float dustAmount = .23f;
        [SerializeField, Min(.1f)] private float rippleSeconds = 1.3f;
        [SerializeField, Min(.1f)] private float rippleRadius = .65f;
        private readonly System.Random random = new System.Random();
        private GameObject effects;
        private ParticleSystem[] dust;
        private Mesh[] meshes;
        private Ripple[] ripples;
        private int nextDust, nextRipple;

        private sealed class Ripple
        {
            public LineRenderer line;
            public Vector3 center;
            public float age;
        }

        public int LandFootfalls { get; private set; }
        public int WaterFootfalls { get; private set; }
        public int ActiveRipples
        {
            get { var count = 0; if (ripples != null) foreach (var ripple in ripples) if (ripple.line.enabled) count++; return count; }
        }
        public int DustParticleCount
        {
            get { var count = 0; if (dust != null) foreach (var puff in dust) count += puff.particleCount; return count; }
        }

        public void EmitFootfall(Transform foot)
        {
            if (!isActiveAndEnabled || foot == null || tsunami == null || constructionDustMaterial == null) return;
            EnsureEffects();
            var bounds = foot.TryGetComponent<MeshRenderer>(out var renderer) ? renderer.bounds : new Bounds(foot.position, Vector3.one * .25f);
            var point = bounds.center;
            var local = tsunami.transform.InverseTransformPoint(point);
            if (local.z < tsunami.CityBounds.min.z - .03f)
            {
                WaterFootfalls++;
                var ripple = ripples[nextRipple++ % ripples.Length];
                var waterY = seaSurface != null && seaSurface.TryGetComponent<MeshRenderer>(out var water)
                    ? water.bounds.max.y : tsunami.transform.TransformPoint(new Vector3(local.x, tsunami.SeaLevel, local.z)).y;
                ripple.center = new Vector3(point.x, waterY + .018f, point.z);
                ripple.age = 0f;
                ripple.line.enabled = true;
                DrawRipple(ripple);
            }
            else
            {
                LandFootfalls++;
                var groundY = tsunami.GroundSurface != null && tsunami.GroundSurface.TryGetComponent<MeshRenderer>(out var ground)
                    ? ground.bounds.max.y : tsunami.transform.TransformPoint(new Vector3(local.x, tsunami.GroundLevel, local.z)).y;
                bounds.center = new Vector3(point.x, groundY, point.z);
                bounds.size = new Vector3(bounds.size.x, .01f, bounds.size.z);
                ConstructionDustPuff.Emit(dust[nextDust++ % dust.Length], effects.transform, bounds, dustAmount, .16f, .75f, random, Vector3.zero);
            }
        }

        private void EnsureEffects()
        {
            if (effects != null) return;
            effects = new GameObject("Monster_Footfall_Effects");
            effects.transform.SetParent(tsunami.transform, true);
            dust = new ParticleSystem[6]; meshes = new Mesh[dust.Length];
            for (var i = 0; i < dust.Length; i++)
            {
                dust[i] = ConstructionDustPuff.Create(effects.transform, constructionDustMaterial, out meshes[i]);
                dust[i].name = "Footstep_Dust_" + (i + 1);
            }
            ripples = new Ripple[6];
            for (var i = 0; i < ripples.Length; i++)
            {
                var go = new GameObject("Footstep_Water_Ring_" + (i + 1));
                go.transform.SetParent(effects.transform, false);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = constructionDustMaterial;
                line.useWorldSpace = true; line.loop = true; line.positionCount = 40;
                line.alignment = LineAlignment.TransformZ; line.generateLightingData = true;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                line.numCornerVertices = 2;
                line.enabled = false;
                ripples[i] = new Ripple { line = line };
            }
        }

        private void Update() => AdvanceEffects(Time.unscaledDeltaTime);

        private void AdvanceEffects(float deltaTime)
        {
            if (ripples == null) return;
            foreach (var ripple in ripples)
            {
                if (!ripple.line.enabled) continue;
                ripple.age += Mathf.Max(0f, deltaTime);
                if (ripple.age >= rippleSeconds) ripple.line.enabled = false;
                else DrawRipple(ripple);
            }
        }

        private void DrawRipple(Ripple ripple)
        {
            var t = Mathf.Clamp01(ripple.age / rippleSeconds);
            var radius = Mathf.Lerp(.12f, rippleRadius, 1f - (1f - t) * (1f - t));
            var tint = new Color(.64f, .92f, .95f, .38f * (1f - t) * (1f - t));
            ripple.line.startColor = ripple.line.endColor = tint;
            ripple.line.widthMultiplier = Mathf.Lerp(.032f, .012f, t);
            for (var i = 0; i < ripple.line.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / ripple.line.positionCount;
                ripple.line.SetPosition(i, ripple.center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        public void Clear()
        {
            LandFootfalls = WaterFootfalls = nextDust = nextRipple = 0;
            if (dust != null) foreach (var puff in dust) puff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (ripples != null) foreach (var ripple in ripples) ripple.line.enabled = false;
        }

        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (effects != null) Destroy(effects);
            if (meshes != null) foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
