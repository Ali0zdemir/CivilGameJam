using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>Moves the saved cube model; the monster is never spawned during play.</summary>
    [DisallowMultipleComponent]
    public sealed class SeaMonsterEmergence : MonoBehaviour
    {
        [SerializeField] private Transform model;
        [Tooltip("Saved event scenery. Hidden while idle so the existing city view is preserved.")]
        [SerializeField] private GameObject seaSurface;
        [SerializeField, Min(0.1f)] private float riseDuration = 5.5f;
        [SerializeField, Min(0.1f)] private float submergedDepth = 4.2f;
        [Tooltip("Release the wave partway through the rise, while the monster is still emerging.")]
        [SerializeField, Range(0f, 1f)] private float waveReleaseFraction = 0.5f;
        [Header("Approach with the wave")]
        [SerializeField] private Vector3 waveApproachOffset;
        [SerializeField, Min(0f)] private float waveApproachDuration;

        private Vector3 emergedPosition;
        private bool poseCached;

        public float RiseDuration => Mathf.Max(0.1f, riseDuration);
        public Transform Model => model;
        public Vector3 EmergedWorldPosition => model == null ? transform.position : model.parent.TransformPoint(poseCached ? emergedPosition : model.localPosition);
        public float WaveReleaseDelay => RiseDuration * Mathf.Clamp01(waveReleaseFraction);
        public float EmergenceProgress { get; private set; }
        public float ApproachProgress { get; private set; }
        public bool HasFinishedEmerging => EmergenceProgress >= 1f && ApproachProgress >= 1f;

        private void Awake() => ResetEmergence();

        public void SetEmergenceTime(float seconds)
        {
            var travel = waveApproachDuration > 0f ? Mathf.Clamp01((seconds - WaveReleaseDelay) / waveApproachDuration) : 1f;
            SetEmergenceTime(seconds, travel);
        }

        public void SetEmergenceTime(float seconds, float approachProgress)
        {
            if (model == null) return;
            if (!poseCached)
            {
                emergedPosition = model.localPosition;
                poseCached = true;
            }
            EmergenceProgress = Mathf.Clamp01(seconds / RiseDuration);
            ApproachProgress = Mathf.Clamp01(approachProgress);
            model.gameObject.SetActive(EmergenceProgress > 0f);
            if (seaSurface != null) seaSurface.SetActive(EmergenceProgress > 0f);
            model.localPosition = emergedPosition + waveApproachOffset * (1f - ApproachProgress) + Vector3.down *
                (Mathf.Max(0.1f, submergedDepth) * (1f - Mathf.SmoothStep(0f, 1f, EmergenceProgress)));
        }

        public void ResetEmergence() => SetEmergenceTime(0f);
    }
}
