using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>Moves the saved cube model; the monster is never spawned during play.</summary>
    [DisallowMultipleComponent]
    public sealed class SeaMonsterEmergence : MonoBehaviour
    {
        [SerializeField] private Transform model;
        [SerializeField, Min(0.1f)] private float riseDuration = 5.5f;
        [SerializeField, Min(0.1f)] private float submergedDepth = 4.2f;
        [Tooltip("Release the wave partway through the rise, while the monster is still emerging.")]
        [SerializeField, Range(0f, 1f)] private float waveReleaseFraction = 0.5f;

        private Vector3 emergedPosition;
        private bool poseCached;

        public float RiseDuration => Mathf.Max(0.1f, riseDuration);
        public Transform Model => model;
        public Vector3 EmergedWorldPosition => model == null ? transform.position : model.parent.TransformPoint(poseCached ? emergedPosition : model.localPosition);
        public float WaveReleaseDelay => RiseDuration * Mathf.Clamp01(waveReleaseFraction);
        public float EmergenceProgress { get; private set; }

        private void Awake() => ResetEmergence();

        public void SetEmergenceTime(float seconds)
        {
            if (model == null) return;
            if (!poseCached)
            {
                emergedPosition = model.localPosition;
                poseCached = true;
            }
            EmergenceProgress = Mathf.Clamp01(seconds / RiseDuration);
            model.localPosition = emergedPosition + Vector3.down *
                (Mathf.Max(0.1f, submergedDepth) * (1f - Mathf.SmoothStep(0f, 1f, EmergenceProgress)));
        }

        public void ResetEmergence() => SetEmergenceTime(0f);
    }
}
