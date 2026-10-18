using System;
using System.Collections.Generic;
using UnityEngine;

namespace CivilGameJam.Upgrades
{
    /// <summary>Combines the authored apartment variants without moving their ground tile.</summary>
    [DisallowMultipleComponent]
    public sealed class ApartmentUpgradeModel : MonoBehaviour
    {
        [Serializable]
        private sealed class SprinklerPair
        {
            public Transform normalHead, normalBase, elevatedHead, elevatedBase;
        }

        [SerializeField] private Transform motionRoot;
        [SerializeField] private GameObject normalModel;
        [SerializeField] private GameObject elevatedModel;
        [SerializeField] private Transform normalIsolators;
        [SerializeField] private Transform elevatedIsolators;
        [SerializeField] private Transform elevatorSupports;
        [SerializeField, Min(.01f)] private float magneticPullDistance = .22f;
        [SerializeField, Min(.1f)] private float magneticPullSeconds = .38f;
        [SerializeField] private SprinklerPair[] sprinklers = Array.Empty<SprinklerPair>();
        [SerializeField] private Bounds[] elevatedSupports = Array.Empty<Bounds>();
        [SerializeField, Min(.1f)] private float transitionDuration = 1.05f;
        [SerializeField, Min(.01f)] private float jumpHeight = .2f;
        private Vector3 restPosition, normalIsolatorPosition, elevatedIsolatorPosition;
        private float seismicLift, elevationLift, startingLift, age;
        private float normalSeismicHeight = .1f, variantSeismicCompensation, compensationStart;
        private float seismicApproach, elevatorApproach, elevatorApproachStart;
        private Vector3 elevatorSupportPosition;
        private bool initialized, elevationTarget;

        public bool IsElevated => elevatedModel != null && elevatedModel.activeSelf;
        public bool IsElevatorProtected => elevationTarget || IsElevated;
        public bool HasSeismic { get; private set; }
        public bool HasFireProtection { get; private set; }
        public float SeismicOffset => Mathf.Max(0f, seismicLift + variantSeismicCompensation);
        public float SeismicCompensation => variantSeismicCompensation;
        public bool IsTransitioning { get; private set; }
        public float MagneticPullSeconds => magneticPullSeconds;
        public float SeismicAttachment => seismicApproach;
        public float ElevatorAttachment => elevatorApproach;
        public Transform MotionRoot => motionRoot;
        public event Action ModelChanged;
        public event Action InstallationCompleted;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized || motionRoot == null || normalIsolators == null || elevatedIsolators == null) return;
            restPosition = motionRoot.localPosition;
            normalIsolatorPosition = normalIsolators.localPosition;
            elevatedIsolatorPosition = elevatedIsolators.localPosition;
            variantSeismicCompensation = IsElevated ? -normalSeismicHeight : 0f;
            if (elevatorSupports != null) elevatorSupportPosition = elevatorSupports.localPosition;
            elevatorApproach = IsElevated ? 1f : 0f;
            initialized = true;
        }

        public void SetSeismic(bool installed)
        {
            Initialize();
            if (!initialized) return;
            HasSeismic = installed;
            if (!installed) seismicApproach = 0f;
            SetEquipmentActive(normalIsolators, installed && seismicApproach > 0f);
            SetEquipmentActive(elevatedIsolators, installed && seismicApproach > 0f);
        }

        public void AttachSeismic()
        {
            SetSeismicApproach(1f);
        }

        public void SetSeismicApproach(float progress)
        {
            seismicApproach = HasSeismic ? Mathf.Clamp01(progress) : 0f;
            SetSeismic(HasSeismic);
            ApplyMotion();
        }

        public void SetSeismicLift(float height, float supportHeight = .1f)
        {
            Initialize();
            seismicLift = Mathf.Max(0f, height);
            normalSeismicHeight = Mathf.Max(0f, supportHeight);
            if (!IsTransitioning) variantSeismicCompensation = IsElevated ? -normalSeismicHeight : 0f;
            ApplyMotion();
        }

        public Transform SprinklerHead(int index)
        {
            if (index < 0 || index >= sprinklers.Length || sprinklers[index] == null) return null;
            return IsElevated ? sprinklers[index].elevatedHead : sprinklers[index].normalHead;
        }

        public void SetFireProtection(bool installed)
        {
            HasFireProtection = installed;
            foreach (var pair in sprinklers)
            {
                if (pair == null) continue;
                SetEquipmentActive(pair.normalHead, installed);
                SetEquipmentActive(pair.normalBase, installed);
                SetEquipmentActive(pair.elevatedHead, installed);
                SetEquipmentActive(pair.elevatedBase, installed);
            }
        }

        private static void SetEquipmentActive(Transform equipment, bool active)
        {
            if (equipment != null && equipment.gameObject.activeSelf != active) equipment.gameObject.SetActive(active);
        }

        public void SetElevated(bool elevated, bool animate = true)
        {
            Initialize();
            if (!initialized || normalModel == null || elevatedModel == null) return;
            if (!IsTransitioning && elevationTarget == elevated && IsElevated == elevated && elevationLift == 0f) return;
            if (animate && elevationTarget == elevated && (IsTransitioning || IsElevated == elevated)) return;
            elevationTarget = elevated;
            if (!animate || !Application.isPlaying || !isActiveAndEnabled)
            {
                IsTransitioning = false;
                elevationLift = 0f;
                elevatorApproach = elevated ? 1f : 0f;
                variantSeismicCompensation = elevated ? -normalSeismicHeight : 0f;
                ShowVariant(elevated);
                ApplyMotion();
                return;
            }
            startingLift = elevationLift;
            compensationStart = variantSeismicCompensation;
            elevatorApproachStart = elevatorApproach;
            age = 0f;
            IsTransitioning = true;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (!IsTransitioning) return;
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / transitionDuration);
            float peak = Mathf.Max(startingLift, jumpHeight);
            float pull = elevationTarget ? Mathf.Clamp01((age - transitionDuration * .5f) / magneticPullSeconds) : Mathf.Clamp01(t / .5f);
            elevatorApproach = Mathf.Lerp(elevatorApproachStart, elevationTarget ? 1f : 0f, Smooth(pull));
            if (t < .5f) elevationLift = Mathf.Lerp(startingLift, peak, Smooth(t / .5f));
            else
            {
                ShowVariant(elevationTarget);
                float settling = t < .6f ? 0f : Smooth((t - .6f) / .4f);
                elevationLift = Mathf.Lerp(peak, 0f, settling);
                variantSeismicCompensation = Mathf.Lerp(compensationStart, elevationTarget ? -normalSeismicHeight : 0f, settling);
            }
            ApplyMotion();
            if (t < 1f) return;
            IsTransitioning = false;
            InstallationCompleted?.Invoke();
        }

        private void ShowVariant(bool elevated)
        {
            bool changed = IsElevated != elevated;
            if (normalModel.activeSelf == elevated) normalModel.SetActive(!elevated);
            if (elevatedModel.activeSelf != elevated) elevatedModel.SetActive(elevated);
            SetSeismic(HasSeismic);
            SetFireProtection(HasFireProtection);
            if (changed) ModelChanged?.Invoke();
        }

        private void ApplyMotion()
        {
            if (!initialized || motionRoot == null || normalIsolators == null || elevatedIsolators == null) return;
            Vector3 worldLift = Vector3.up * (SeismicOffset + elevationLift);
            motionRoot.localPosition = restPosition + motionRoot.parent.InverseTransformVector(worldLift);
            // Normal foundations seat on the isolators; elevated feet retain their authored overlap.
            // Both isolator meshes travel with the apartment during the installation and landing.
            float isolatorGap = HasSeismic ? magneticPullDistance * (1f - Smooth(seismicApproach)) : 0f;
            float seat = HasSeismic ? Mathf.Max(0f, normalSeismicHeight + variantSeismicCompensation) : 0f;
            normalIsolators.localPosition = normalIsolatorPosition - normalIsolators.parent.InverseTransformVector(Vector3.up * (seat + isolatorGap));
            elevatedIsolators.localPosition = elevatedIsolatorPosition - elevatedIsolators.parent.InverseTransformVector(Vector3.up * (seat + isolatorGap));
            if (elevatorSupports != null)
            {
                elevatorSupports.localPosition = elevatorSupportPosition - elevatorSupports.parent.InverseTransformVector(Vector3.up * magneticPullDistance * (1f - elevatorApproach));
                SetEquipmentActive(elevatorSupports, elevatorApproach > 0f);
            }
        }

        public void AppendSupports(Transform relativeTo, List<Bounds> output)
        {
            foreach (Bounds support in elevatedSupports)
            {
                Matrix4x4 matrix = relativeTo.worldToLocalMatrix * motionRoot.localToWorldMatrix;
                var bounds = new Bounds(matrix.MultiplyPoint3x4(support.center), Vector3.zero);
                for (int i = 0; i < 8; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(support.center + Vector3.Scale(support.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                output.Add(bounds);
            }
        }

        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
