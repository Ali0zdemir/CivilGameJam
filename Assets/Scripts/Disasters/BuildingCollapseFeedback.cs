using CivilGameJam.Upgrades;
using UnityEngine;

namespace CivilGameJam.Disasters
{
    [DisallowMultipleComponent]
    public sealed class BuildingCollapseFeedback : MonoBehaviour
    {
        [SerializeField] private BuildingModelGeometry geometry;
        [SerializeField] private Material constructionDustMaterial;
        [SerializeField, Range(.1f, 3f)] private float dustAmount = 1.4f;
        private readonly System.Random random = new System.Random();
        private GameObject effects;
        private ParticleSystem[] dust;
        private Mesh[] meshes;
        private Bounds[] foundations;
        private float dustElapsed;
        private bool prepared;

        public bool Reported { get; private set; }
        public bool IsDustEmitting { get; private set; }
        public int DustParticleCount
        {
            get { var count = 0; if (dust != null) foreach (var puff in dust) if (puff != null) count += puff.particleCount; return count; }
        }

        public void PrepareCollapse()
        {
            if (Reported || geometry == null) return;
            var parts = geometry.IsCollection ? geometry.Parts : new[] { geometry };
            foundations = new Bounds[parts.Length];
            for (var i = 0; i < parts.Length; i++) foundations[i] = parts[i].WorldFoundationBounds;
            prepared = true;
        }

        public void ReportCollapse()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            var firstReport = !Reported;
            if (firstReport)
            {
                if (!prepared) PrepareCollapse();
                if (!prepared) return;
                EnsureEffects();
                Reported = true;
            }
            if (IsDustEmitting) return;
            IsDustEmitting = true;
            dustElapsed = 0f;
            EmitDust(firstReport ? dustAmount : dustAmount * .24f, firstReport);
        }

        public void FinishCollapse()
        {
            IsDustEmitting = false;
            dustElapsed = 0f;
        }

        private void EmitDust(float amount, bool clearExisting)
        {
            if (dust != null)
                for (var i = 0; i < dust.Length; i++)
                    ConstructionDustPuff.Emit(dust[i], effects.transform, foundations[i], amount, .26f, 1.2f, random, Vector3.zero, clearExisting: clearExisting);
        }

        private void EnsureEffects()
        {
            if (effects != null) return;
            effects = new GameObject(name + "_Collapse_Feedback");
            // Keep transient renderers outside the city meshes used for cinematic framing.
            effects.transform.SetParent(transform.parent != null ? transform.parent.parent : null, true);
            if (constructionDustMaterial != null)
            {
                dust = new ParticleSystem[foundations.Length]; meshes = new Mesh[foundations.Length];
                for (var i = 0; i < dust.Length; i++)
                {
                    dust[i] = ConstructionDustPuff.Create(effects.transform, constructionDustMaterial, out meshes[i]);
                    dust[i].name = "Collapse_Dust_" + (i + 1);
                }
            }
        }

        private void Update() => AdvanceFeedback(Time.unscaledDeltaTime);

        private void AdvanceFeedback(float deltaTime)
        {
            if (!Reported) return;
            // Replenish the cloud without clearing older particles throughout the collapse.
            if (IsDustEmitting && dust != null)
            {
                dustElapsed += Mathf.Max(0f, deltaTime);
                if (dustElapsed >= .24f)
                {
                    var pulses = Mathf.Min(4, Mathf.FloorToInt(dustElapsed / .24f));
                    EmitDust(dustAmount * .24f * pulses, false);
                    dustElapsed %= .24f;
                }
            }
        }

        public void ResetFeedback()
        {
            Reported = prepared = false;
            FinishCollapse();
            if (dust != null) foreach (var puff in dust) if (puff != null) puff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable() => ResetFeedback();
        private void OnDestroy()
        {
            if (effects != null) Destroy(effects);
            if (meshes != null) foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
