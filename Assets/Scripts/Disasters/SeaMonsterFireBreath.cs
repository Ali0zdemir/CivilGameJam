using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>Charges and bends the saved monster without changing its colors or planted earthquake stance.</summary>
    [DisallowMultipleComponent]
    public sealed class SeaMonsterFireBreath : MonoBehaviour
    {
        [SerializeField] private SeaMonsterWalk walk;
        [SerializeField] private Transform model;
        [SerializeField] private Transform mouth;
        [SerializeField] private Transform neckPivot;
        [SerializeField] private Transform[] upperBody = new Transform[0];
        [SerializeField] private Transform[] headParts = new Transform[0];
        [SerializeField, Min(0.1f)] private float chargeDuration = 2.4f;
        [SerializeField, Min(0.1f)] private float leanDuration = 0.85f;
        [SerializeField, Min(0.1f)] private float breathDuration = 3.8f;
        [SerializeField, Min(0.1f)] private float travelDuration = 0.9f;
        [SerializeField, Min(0.1f)] private float recoveryDuration = 1.6f;
        [SerializeField, Range(3f, 20f)] private float leanAngle = 12f;
        [SerializeField, Range(0f, .4f)] private float headLowering = .22f;
        [SerializeField, Range(5f, 85f)] private float breathDownAngle = 55.5f;

        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private bool posing;
        private Vector3 neckPosition;
        private Vector3 restMouthPosition;
        private Vector3 fireHeading;

        public Transform Mouth => mouth;
        public SeaMonsterWalk Walk => walk;
        public float BreathDownAngle => Mathf.Clamp(breathDownAngle, 5f, 85f);
        public Vector3 FireDirection => DirectionForHeading(fireHeading.sqrMagnitude > 0.0001f ? fireHeading : model.forward);
        public Vector3 AimedMouthPosition => PredictMouth(fireHeading);
        public float ChargeDuration => Mathf.Max(0.1f, chargeDuration);
        public float FlameStartTime => ChargeDuration + Mathf.Max(0.1f, leanDuration);
        public float BreathDuration => Mathf.Max(0.1f, breathDuration);
        public float BreathEndTime => FlameStartTime + BreathDuration;
        public float TravelDuration => Mathf.Max(0.1f, travelDuration);
        public float CityIgnitionTime => FlameStartTime + TravelDuration;
        public float Heat { get; private set; }
        public float Lean { get; private set; }

        public void BeginFire()
        {
            ResetPose();
            if (walk != null) walk.CompleteWalk();
            restPositions = new Vector3[upperBody.Length];
            restRotations = new Quaternion[upperBody.Length];
            for (var i = 0; i < upperBody.Length; i++)
            {
                if (upperBody[i] == null) continue;
                restPositions[i] = upperBody[i].localPosition;
                restRotations[i] = upperBody[i].localRotation;
            }
            neckPosition = neckPivot != null ? model.InverseTransformPoint(neckPivot.position) : headParts.Length > 0 && headParts[0] != null
                ? headParts[0].localPosition - Vector3.up * (headParts[0].localScale.y * 0.5f) : new Vector3(0f, 2.59f, 0.14f);
            restMouthPosition = mouth != null ? model.InverseTransformPoint(mouth.position) : new Vector3(0f, 2.87f, 0.96f);
            fireHeading = Vector3.ProjectOnPlane(model.forward, Vector3.up).normalized;
            posing = true;
            SetFireTime(0f);
        }

        public void SetFireTime(float seconds)
        {
            if (!posing || model == null) return;
            var release = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(FlameStartTime + BreathDuration,
                    FlameStartTime + BreathDuration + Mathf.Max(0.1f, recoveryDuration), seconds));
            Heat = Mathf.SmoothStep(0f, 1f, seconds / ChargeDuration) * release;
            Lean = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ChargeDuration, FlameStartTime, seconds)) * release;
            var bend = Quaternion.Euler(Lean * leanAngle, 0f, 0f);
            var hip = new Vector3(0f, 1.15f, 0f);
            var neck = hip + bend * (neckPosition - hip) - Vector3.up * (Lean * headLowering);
            var headRotation = Quaternion.Slerp(bend, HeadRotation(fireHeading), Lean);
            for (var i = 0; i < upperBody.Length; i++)
            {
                if (upperBody[i] == null) continue;
                var isHead = false;
                foreach (var part in headParts) if (part == upperBody[i]) { isHead = true; break; }
                upperBody[i].localPosition = isHead ? neck + headRotation * (restPositions[i] - neckPosition)
                    : hip + bend * (restPositions[i] - hip);
                if (walk == null || !walk.OwnsArmRotation(upperBody[i]))
                    upperBody[i].localRotation = (isHead ? headRotation : bend) * restRotations[i];
            }
        }

        public Vector3 GroundImpact(float groundHeight)
        {
            var origin = AimedMouthPosition;
            return origin + FireDirection * Mathf.Max(0.1f, (groundHeight - origin.y) / FireDirection.y);
        }

        private Vector3 DirectionForHeading(Vector3 heading)
        {
            var flat = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
            var angle = BreathDownAngle * Mathf.Deg2Rad;
            return flat * Mathf.Cos(angle) - Vector3.up * Mathf.Sin(angle);
        }

        private Quaternion HeadRotation(Vector3 heading) => Quaternion.LookRotation(model.InverseTransformDirection(DirectionForHeading(heading)), Vector3.up);

        private Vector3 PredictMouth(Vector3 heading)
        {
            var hip = new Vector3(0f, 1.15f, 0f);
            var neck = hip + Quaternion.Euler(leanAngle, 0f, 0f) * (neckPosition - hip) - Vector3.up * headLowering;
            return model.TransformPoint(neck + HeadRotation(heading) * (restMouthPosition - neckPosition));
        }

        public void ResetPose()
        {
            if (!posing) return;
            posing = false;
            Heat = Lean = 0f;
            for (var i = 0; i < upperBody.Length; i++)
            {
                if (upperBody[i] == null) continue;
                upperBody[i].localPosition = restPositions[i];
                if (walk == null || !walk.OwnsArmRotation(upperBody[i])) upperBody[i].localRotation = restRotations[i];
            }
        }

        private void OnDisable() => ResetPose();
    }
}
