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
        [Header("Building collapse sound")]
        [SerializeField] private AudioClip collapseSound;
        [SerializeField, Range(0f, 1f)] private float collapseSoundVolume = .048f;
        [Tooltip("Skip the recording's leading silence so its attack starts with the visible collapse.")]
        [SerializeField, Min(0f)] private float collapseSoundStartSeconds;
        private readonly System.Random random = new System.Random();
        private AudioSource collapseAudio;
        private GameObject effects;
        private ParticleSystem[] dust;
        private Mesh[] meshes;
        private Bounds[] foundations;
        private float dustElapsed;
        private bool prepared, playCollapseSound;
        private const float DustLifetime = 1.2f;

        public bool Reported { get; private set; }
        public bool IsDustEmitting { get; private set; }
        public bool IsPlaying => IsDustEmitting || DustParticleCount > 0;
        public float MaximumDustLifetime => constructionDustMaterial != null ? DustLifetime * 1.15f : 0f;
        public int DustParticleCount
        {
            get { var count = 0; if (dust != null) foreach (var puff in dust) if (puff != null) count += puff.particleCount; return count; }
        }

        private void Awake()
        {
            if (collapseSound == null) return;
            // Prepare the decoded clip and voice before any house starts falling.
            collapseSound.LoadAudioData();
            collapseAudio = gameObject.AddComponent<AudioSource>();
            collapseAudio.playOnAwake = false;
            collapseAudio.clip = collapseSound;
            collapseAudio.volume = collapseSoundVolume;
            collapseAudio.loop = false;
            collapseAudio.spatialBlend = 0f;
            collapseAudio.dopplerLevel = 0f;
            collapseAudio.priority = 96;
        }

        public void PrepareCollapse(bool withSound = false)
        {
            if (Reported || geometry == null) return;
            playCollapseSound = withSound;
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
                if (playCollapseSound && collapseAudio != null)
                {
                    collapseAudio.timeSamples = Mathf.Clamp(Mathf.RoundToInt(collapseSoundStartSeconds * collapseSound.frequency), 0, collapseSound.samples - 1);
                    // This is the same sampled frame that first reports visible damage; no timer or coroutine.
                    collapseAudio.Play();
                }
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
                    ConstructionDustPuff.Emit(dust[i], effects.transform, foundations[i], amount, .26f, DustLifetime, random, Vector3.zero, clearExisting: clearExisting);
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

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            AdvanceFeedback(Time.unscaledDeltaTime);
        }

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
            Reported = prepared = playCollapseSound = false;
            if (collapseAudio != null) collapseAudio.Stop();
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
