using System;
using CivilGameJam.Upgrades;
using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>Collapses the existing, visible apartment towers around their feet; their ground stays still.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(250)]
    public sealed class EarthquakeHouseDamage : MonoBehaviour
    {
        [Serializable]
        private sealed class Tower
        {
            public Transform normalBody, elevatedBody;
            public Transform surface;
            public Transform[] normalAttachments = Array.Empty<Transform>();
            public Transform[] elevatedAttachments = Array.Empty<Transform>();
            [NonSerialized] public Transform body;
            [NonSerialized] public Transform[] followers;
            [NonSerialized] public Vector3[] positions;
            [NonSerialized] public Quaternion[] rotations;
            [NonSerialized] public Vector3 position, pivot;
            [NonSerialized] public Quaternion rotation;
        }

        [SerializeField] private ApartmentUpgradeModel apartment;
        [SerializeField] private Tower[] towers = Array.Empty<Tower>();
        [SerializeField, Min(.2f)] private float collapseSeconds = 2.2f;
        [SerializeField, Min(0f)] private float towerDelay = .12f;
        [SerializeField, Range(40f, 90f)] private float fallAngle = 78f;
        [SerializeField, Min(0f)] private float settlingDepth = .06f;
        private float elapsed;
        private ApartmentEarthquakeAnimation authored;
        private bool destroyed, manualAuthoredCollapse;
        private ApartmentEarthquakeAnimation Authored
        {
            get { if (authored == null) TryGetComponent(out authored); return authored; }
        }
        public bool UsesAuthoredAnimation => Authored != null;
        public bool IsDestroyed { get => destroyed || Authored != null && Authored.HasCollapseStarted; private set => destroyed = value; }
        public bool IsPlaying => UsesAuthoredAnimation ? Authored.IsRunning && IsDestroyed : IsDestroyed && elapsed < Duration;
        public float Duration => Mathf.Max(.2f, collapseSeconds) + Mathf.Max(0, towers.Length - 1) * Mathf.Max(0f, towerDelay);

        public bool Collapse()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsDestroyed || apartment == null || apartment.HasSeismic || towers.Length == 0) return false;
            if (UsesAuthoredAnimation)
            {
                if (Authored.IsRunning || !Authored.BeginEarthquake()) return false;
                manualAuthoredCollapse = true;
                elapsed = 0f; IsDestroyed = true;
                return true;
            }
            foreach (var tower in towers)
                if (tower == null || tower.normalBody == null || tower.elevatedBody == null || tower.surface == null) return false;
            foreach (var tower in towers)
            {
                tower.body = apartment.IsElevated ? tower.elevatedBody : tower.normalBody;
                var extras = apartment.IsElevated ? tower.elevatedAttachments : tower.normalAttachments;
                tower.followers = new Transform[extras.Length + 1];
                tower.followers[0] = tower.surface;
                Array.Copy(extras, 0, tower.followers, 1, extras.Length);
                tower.positions = new Vector3[tower.followers.Length]; tower.rotations = new Quaternion[tower.followers.Length];
                tower.position = tower.body.position; tower.rotation = tower.body.rotation;
                var bounds = tower.body.GetComponent<Renderer>().bounds;
                tower.pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                for (int i = 0; i < tower.followers.Length; i++)
                {
                    tower.positions[i] = tower.followers[i].position;
                    tower.rotations[i] = tower.followers[i].rotation;
                }
            }
            elapsed = 0f; IsDestroyed = true;
            ApplyPose();
            return true;
        }

        private void Update()
        {
            if (Time.timeScale > 0f) AdvanceCollapse(Time.unscaledDeltaTime);
        }

        private void AdvanceCollapse(float seconds)
        {
            if (UsesAuthoredAnimation)
            {
                // EarthquakeTestController owns sequence timing; only explicit standalone calls use this clock.
                if (manualAuthoredCollapse && Authored.IsRunning)
                {
                    elapsed = Mathf.Min(Duration, elapsed + Mathf.Max(0f, seconds));
                    Authored.SetEarthquakeProgress(elapsed / Duration);
                    if (elapsed >= Duration) Authored.FinishEarthquake();
                }
                return;
            }
            if (!IsDestroyed) return;
            elapsed = Mathf.Min(Duration, elapsed + Mathf.Max(0f, seconds));
            ApplyPose();
        }

        private void ApplyPose()
        {
            for (int i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                float t = Mathf.Clamp01((elapsed - i * towerDelay) / Mathf.Max(.2f, collapseSeconds));
                float eased = t * t * t * (t * (t * 6f - 15f) + 10f);
                var rotation = Quaternion.AngleAxis(eased * fallAngle * (i % 2 == 0 ? 1f : -.88f), transform.right);
                var settling = Vector3.down * (Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.75f, 1f, t)) * settlingDepth);
                tower.body.SetPositionAndRotation(tower.pivot + rotation * (tower.position - tower.pivot) + settling, rotation * tower.rotation);
                for (int j = 0; j < tower.followers.Length; j++)
                    if (tower.followers[j] != null)
                        tower.followers[j].SetPositionAndRotation(tower.pivot + rotation * (tower.positions[j] - tower.pivot) + settling, rotation * tower.rotations[j]);
            }
        }

        public void ResetDamage()
        {
            if (UsesAuthoredAnimation)
            {
                Authored.ResetEarthquake();
                manualAuthoredCollapse = false;
                IsDestroyed = false; elapsed = 0f;
                return;
            }
            if (IsDestroyed)
                foreach (var tower in towers)
                {
                    if (tower.body != null) tower.body.SetPositionAndRotation(tower.position, tower.rotation);
                    for (int i = 0; i < tower.followers.Length; i++)
                        if (tower.followers[i] != null) tower.followers[i].SetPositionAndRotation(tower.positions[i], tower.rotations[i]);
                }
            IsDestroyed = false; elapsed = 0f;
        }

        private void OnDisable() => ResetDamage();
    }
}
