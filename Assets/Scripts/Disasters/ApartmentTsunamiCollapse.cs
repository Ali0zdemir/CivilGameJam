using System;
using CivilGameJam.Upgrades;
using UnityEngine;

namespace CivilGameJam.Disasters
{
    /// <summary>Plays the authored tsunami motion on the existing apartment meshes.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class ApartmentTsunamiCollapse : MonoBehaviour
    {
        [Serializable]
        private sealed class Part
        {
            public Transform body;
            public Transform[] attachments = Array.Empty<Transform>();
            [NonSerialized] public Pose bodyRest;
            [NonSerialized] public Matrix4x4 bodyInverse;
            [NonSerialized] public Pose[] attachmentRest;
            [NonSerialized] public Vector3[] relativePositions;
            [NonSerialized] public Quaternion[] relativeRotations;
        }

        private struct Pose
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public static Pose Read(Transform target) => new Pose
                { position = target.localPosition, rotation = target.localRotation, scale = target.localScale };
            public void Restore(Transform target)
            {
                if (target == null) return;
                target.localPosition = position; target.localRotation = rotation; target.localScale = scale;
            }
        }

        [SerializeField] private ApartmentUpgradeModel apartment;
        [SerializeField] private Transform animationRoot;
        [SerializeField] private AnimationClip collapseAnimation;
        [SerializeField] private BuildingCollapseFeedback collapseFeedback;
        [SerializeField] private Part[] parts = Array.Empty<Part>();
        private float elapsed;
        private bool captured;
        public bool HasCollapsed { get; private set; }
        public bool IsPlaying => HasCollapsed && elapsed < Duration;
        public float Duration => collapseAnimation != null ? collapseAnimation.length : 0f;
        public float Progress => HasCollapsed ? Mathf.Clamp01(elapsed / Mathf.Max(.001f, Duration)) : 0f;

        public bool TryCollapse()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || HasCollapsed || apartment == null ||
                apartment.IsElevatorProtected || animationRoot == null || collapseAnimation == null || parts.Length == 0) return false;
            if (!TryGetComponent<BuildingModelGeometry>(out var geometry) || !geometry.IsResidential) return false;
            foreach (var part in parts)
                if (part == null || part.body == null || part.attachments == null || Array.Exists(part.attachments, target => target == null)) return false;
            // A flood takes over the pose without competing with the earthquake animation.
            if (TryGetComponent<ApartmentEarthquakeAnimation>(out var earthquake)) earthquake.ReleaseForTsunami();
            CapturePose();
            if (collapseFeedback != null) collapseFeedback.PrepareCollapse(withSound: true);
            elapsed = 0f; HasCollapsed = true;
            SamplePose();
            return true;
        }

        private void CapturePose()
        {
            foreach (var part in parts)
            {
                part.bodyRest = Pose.Read(part.body);
                part.bodyInverse = Matrix4x4.TRS(part.bodyRest.position, part.bodyRest.rotation, part.bodyRest.scale).inverse;
                part.attachmentRest = new Pose[part.attachments.Length];
                part.relativePositions = new Vector3[part.attachments.Length];
                part.relativeRotations = new Quaternion[part.attachments.Length];
                for (int i = 0; i < part.attachments.Length; i++)
                {
                    var target = part.attachments[i];
                    part.attachmentRest[i] = Pose.Read(target);
                    part.relativePositions[i] = animationRoot.InverseTransformPoint(target.position);
                    part.relativeRotations[i] = Quaternion.Inverse(animationRoot.rotation) * target.rotation;
                }
            }
            captured = true;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (!HasCollapsed) return;
            if (apartment == null || apartment.IsElevatorProtected) { ResetCollapse(); return; }
            AdvanceCollapse(Time.unscaledDeltaTime);
        }

        private void AdvanceCollapse(float seconds)
        {
            if (!IsPlaying) return;
            elapsed = Mathf.Min(Duration, elapsed + Mathf.Max(0f, seconds));
            SamplePose();
            // Keep the final ruins visible until the report is dismissed and the city resets.
        }

        private void LateUpdate()
        {
            if (HasCollapsed) FollowAttachments();
        }

        private void SamplePose()
        {
            collapseAnimation.SampleAnimation(animationRoot.gameObject, Mathf.Min(elapsed, Duration));
            FollowAttachments();
            if (collapseFeedback != null)
            {
                if (!collapseFeedback.Reported || elapsed < Duration)
                    foreach (var part in parts)
                        if (part.body.localPosition.y < part.bodyRest.position.y - .05f || Quaternion.Angle(part.bodyRest.rotation, part.body.localRotation) > 12f)
                        { collapseFeedback.ReportCollapse(); break; }
                if (elapsed >= Duration) collapseFeedback.FinishCollapse();
            }
        }

        private void FollowAttachments()
        {
            foreach (var part in parts)
            {
                if (part.body == null) continue;
                var delta = Matrix4x4.TRS(part.body.localPosition, part.body.localRotation, part.body.localScale) * part.bodyInverse;
                var rotation = part.body.localRotation * Quaternion.Inverse(part.bodyRest.rotation);
                for (int i = 0; i < part.attachments.Length; i++)
                {
                    var target = part.attachments[i];
                    if (target == null) continue;
                    target.SetPositionAndRotation(animationRoot.TransformPoint(delta.MultiplyPoint3x4(part.relativePositions[i])),
                        animationRoot.rotation * rotation * part.relativeRotations[i]);
                }
            }
        }

        public void ResetCollapse()
        {
            if (captured && collapseFeedback != null) collapseFeedback.ResetFeedback();
            if (captured)
                foreach (var part in parts)
                {
                    part.bodyRest.Restore(part.body);
                    for (int i = 0; i < part.attachments.Length; i++) part.attachmentRest[i].Restore(part.attachments[i]);
                }
            captured = false; HasCollapsed = false; elapsed = 0f;
        }

        private void OnDisable() => ResetCollapse();
    }
}
