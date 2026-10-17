using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>Animates alternating, planted steps using the saved cube limbs.</summary>
    [DisallowMultipleComponent]
    public sealed class SeaMonsterWalk : MonoBehaviour
    {
        private const int MinimumSteps = 5;

        [SerializeField] private SeaMonsterEmergence emergence;
        [SerializeField] private TsunamiTestController tsunami;
        [SerializeField] private Transform walkPoint;
        [SerializeField] private Transform afterEventPoint;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform rightArm;
        [SerializeField, Min(0.2f)] private float stepDuration = 1.3f;
        [SerializeField, Range(0.1f, 1f)] private float stepLength = 0.5f;
        [SerializeField, Range(0.02f, 0.4f)] private float footLift = 0.18f;

        private Transform[] parts;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Vector3 startPosition;
        private Vector3 resetPosition;
        private Quaternion resetRotation;
        private Quaternion startRotation;
        private Quaternion walkRotation;
        private Vector3 path;
        private float stride;
        private int plannedSteps;
        private bool started;
        private bool afterEventRoute;
        private float afterEventElapsed;
        private readonly Vector3[] initialFootOffsets = new Vector3[2];

        public float StepDuration => Mathf.Max(0.2f, stepDuration);
        public Transform WalkPoint => walkPoint;
        public Transform AfterEventPoint => afterEventPoint;
        public bool IsWalkingAfterEvent { get; private set; }
        public bool HasStartedAfterEventWalk => started && afterEventRoute;
        public bool HasReachedWalkPoint => started && (afterEventRoute || CompletedSteps == plannedSteps);
        public bool OwnsArmRotation(Transform part) => HasStartedAfterEventWalk && (part == leftArm || part == rightArm);
        public float AfterEventWalkDuration => afterEventRoute ? plannedSteps * StepDuration : 0f;
        public int StepCount => started && !afterEventRoute ? plannedSteps : StepsForDistance(PlannedDistance);
        public float WalkDuration => StepCount * StepDuration;
        public int CompletedSteps { get; private set; }

        private float PlannedDistance => walkPoint != null && emergence != null && emergence.Model != null
            ? Vector3.Distance(emergence.Model.parent.InverseTransformPoint(emergence.EmergedWorldPosition),
                emergence.Model.parent.InverseTransformPoint(walkPoint.position)) : (MinimumSteps - 0.5f) * stepLength;

        private int StepsForDistance(float distance) => Mathf.Max(MinimumSteps,
            Mathf.CeilToInt(distance / Mathf.Max(0.1f, stepLength) + 0.5f - 0.0001f));

        public void CompleteWalk()
        {
            if (!started || afterEventRoute) BeginWalk();
            SetWalkTime(WalkDuration);
        }

        public void BeginWalk(bool preserveTsunami = false)
        {
            if (emergence == null || emergence.Model == null) return;
            CancelWalk();
            if (tsunami != null && tsunami.IsRunning)
            {
                if (preserveTsunami) tsunami.ReleaseMonsterToWalk();
                else tsunami.StopTsunami();
            }
            // The earthquake test also works when it is pressed before the tsunami test.
            emergence.SetEmergenceTime(emergence.RiseDuration);
            startPosition = emergence.Model.localPosition;
            startRotation = emergence.Model.localRotation;
            resetPosition = startPosition;
            resetRotation = startRotation;
            initialFootOffsets[0] = initialFootOffsets[1] = Vector3.zero;
            path = walkPoint != null ? emergence.Model.parent.InverseTransformPoint(walkPoint.position) - startPosition
                : Vector3.forward * ((MinimumSteps - 0.5f) * stepLength);
            plannedSteps = StepsForDistance(path.magnitude);
            stride = path.magnitude / (plannedSteps - 0.5f);
            var forward = Vector3.ProjectOnPlane(path, Vector3.up);
            if (forward.sqrMagnitude > 0.00001f) emergence.Model.localRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            walkRotation = emergence.Model.localRotation;
            if (parts == null)
            {
                parts = new[] { leftLeg, leftFoot, leftArm, rightLeg, rightFoot, rightArm };
                restPositions = new Vector3[parts.Length];
                restRotations = new Quaternion[parts.Length];
                for (var i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null) continue;
                    restPositions[i] = parts[i].localPosition;
                    restRotations[i] = parts[i].localRotation;
                }
            }
            started = true;
            SetWalkTime(0f);
        }

        public void BeginAfterEventWalk()
        {
            if (!started || afterEventRoute || afterEventPoint == null || emergence == null || emergence.Model == null) return;
            var model = emergence.Model;
            var destination = model.parent.InverseTransformPoint(afterEventPoint.position);
            if (Vector3.Distance(model.localPosition, destination) < 0.001f) return;
            // Keep the planted fire stance as the start of this route, including both feet.
            for (var side = 0; side < 2; side++)
            {
                var index = side * 3 + 1;
                initialFootOffsets[side] = parts[index] != null
                    ? model.parent.InverseTransformVector(model.TransformVector(parts[index].localPosition - restPositions[index])) : Vector3.zero;
            }
            startPosition = model.localPosition;
            startRotation = model.localRotation;
            path = destination - startPosition;
            plannedSteps = StepsForDistance(path.magnitude);
            stride = path.magnitude / (plannedSteps - 0.5f);
            var forward = Vector3.ProjectOnPlane(path, Vector3.up);
            walkRotation = forward.sqrMagnitude > 0.00001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : startRotation;
            afterEventRoute = true;
            IsWalkingAfterEvent = true;
            afterEventElapsed = 0f;
        }

        public void StopAfterEventWalk() => IsWalkingAfterEvent = false;

        private void Update()
        {
            if (IsWalkingAfterEvent) AdvanceAfterEventWalk(Time.unscaledDeltaTime);
        }

        private void AdvanceAfterEventWalk(float deltaTime)
        {
            if (!IsWalkingAfterEvent) return;
            afterEventElapsed = Mathf.Min(afterEventElapsed + Mathf.Max(0f, deltaTime), AfterEventWalkDuration);
            SetWalkTime(afterEventElapsed);
            if (afterEventElapsed >= AfterEventWalkDuration) IsWalkingAfterEvent = false;
        }

        public void SetWalkTime(float seconds)
        {
            if (!started || emergence == null || emergence.Model == null) return;
            var time = Mathf.Clamp(seconds, 0f, plannedSteps * StepDuration) / StepDuration;
            CompletedSteps = Mathf.Min(plannedSteps, Mathf.FloorToInt(time + 0.00001f));
            var phase = CompletedSteps == plannedSteps ? 0f : Mathf.Clamp01(time - CompletedSteps);
            FootPose(0, phase, out var leftDistance, out var leftLift, out var leftResidual);
            FootPose(1, phase, out var rightDistance, out var rightLift, out var rightResidual);
            var travel = (leftDistance + rightDistance) * 0.5f;
            emergence.Model.localPosition = startPosition + path.normalized * travel;
            if (CompletedSteps == plannedSteps) emergence.Model.localPosition = startPosition + path;
            if (afterEventRoute)
                emergence.Model.localRotation = Quaternion.Slerp(startRotation, walkRotation, Mathf.SmoothStep(0f, 1f, time));
            ApplyLeg(0, ToModelVector(path.normalized * (leftDistance - travel) + leftResidual + TurnOffset(0, phase)), leftLift);
            ApplyLeg(3, ToModelVector(path.normalized * (rightDistance - travel) + rightResidual + TurnOffset(1, phase)), rightLift);
        }

        private Vector3 ToModelVector(Vector3 offset) => emergence.Model.InverseTransformVector(emergence.Model.parent.TransformVector(offset));

        private Vector3 TurnOffset(int side, float phase)
        {
            if (!afterEventRoute) return Vector3.zero;
            // Each resting foot keeps its original world anchor until its own first lift.
            var turn = CompletedSteps > side ? 1f : CompletedSteps == side ? Mathf.SmoothStep(0f, 1f, phase) : 0f;
            var rest = Vector3.Scale(restPositions[side * 3 + 1], emergence.Model.localScale);
            return Quaternion.Slerp(startRotation, walkRotation, turn) * rest - emergence.Model.localRotation * rest;
        }

        private void FootPose(int side, float phase, out float distance, out float lift, out Vector3 residual)
        {
            distance = Vector3.Dot(initialFootOffsets[side], path.normalized);
            residual = initialFootOffsets[side] - path.normalized * distance;
            lift = 0f;
            for (var step = side; step < plannedSteps; step += 2)
            {
                if (step < CompletedSteps) { distance = (step + 1) * stride; residual = Vector3.zero; }
                else if (step == CompletedSteps)
                {
                    distance = Mathf.Lerp(distance, (step + 1) * stride, Mathf.SmoothStep(0f, 1f, phase));
                    residual *= 1f - Mathf.SmoothStep(0f, 1f, phase);
                    // A longer lift and a quicker, eased landing give the giant a heavier stride.
                    var liftPhase = phase < 0.65f ? phase / 0.65f : (1f - phase) / 0.35f;
                    lift = Mathf.SmoothStep(0f, 1f, liftPhase) * footLift;
                }
            }
        }

        private void ApplyLeg(int index, Vector3 offset, float lift)
        {
            var angle = -Mathf.Atan2(offset.z, 1.2f) * Mathf.Rad2Deg;
            if (parts[index] != null)
            {
                parts[index].localPosition = restPositions[index] + offset * 0.5f + Vector3.up * (lift * 0.5f);
                parts[index].localRotation = restRotations[index] * Quaternion.Euler(angle, 0f, 0f);
            }
            if (parts[index + 1] != null)
                parts[index + 1].localPosition = restPositions[index + 1] + offset + Vector3.up * lift;
            if (parts[index + 2] != null)
                parts[index + 2].localRotation = restRotations[index + 2] * Quaternion.Euler(-angle * 0.7f, 0f, 0f);
        }

        public void CancelWalk()
        {
            if (!started) return;
            started = false;
            afterEventRoute = false;
            IsWalkingAfterEvent = false;
            CompletedSteps = 0;
            if (emergence != null && emergence.Model != null) emergence.Model.SetLocalPositionAndRotation(resetPosition, resetRotation);
            for (var i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
        }
    }
}
